using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// ejecutar_codigo sin solo_lectura (desde 1.19.2): de dónde viene cada objeto del script, para que no compile un
    /// cambio sobre un objeto abierto para lectura. Motivo: Curve.Extend sobre una línea abierta para lectura no dio
    /// eNotOpenForWrite, cerró Civil 3D 2027 (17/11/2026, VALIDACION_1176, paso 9). Con solo_lectura ese uso ya no compilaba
    /// desde la 1.19.1; sin ella, un script que olvida F.Abrir(id, true) antes de cambiar un objeto podía cerrar Civil 3D.
    ///
    /// Cómo: recorre una vez las operaciones de Roslyn del método del script y anota todo lo que se asigna a cada variable,
    /// a los elementos de cada colección local, a cada parámetro de lambda y a lo que devuelve cada lambda o función
    /// local. No mira el orden de las líneas: una variable es «de lectura» solo si TODO lo que se le asigna es una
    /// apertura para lectura (o null). Aperturas para lectura: F.Abrir&lt;T&gt;(id) o F.Abrir&lt;T&gt;(id, false), y cualquier
    /// método de Autodesk con un parámetro OpenMode al que se le pasa la constante ForRead o ForNotify (Transaction,
    /// ObjectId y TransactionManager.GetObject). El objeto se sigue por variables, conversiones, patrones (is, switch),
    /// ?:, ?? y ?., cadenas de LINQ desde un Select, foreach, listas llenadas con Add, funciones locales y los subobjetos de
    /// Civil 3D (c.Baselines[0].BaselineRegions[0] de un corredor abierto para lectura).
    ///
    /// Lo que no se puede saber se deja pasar, sin aviso: un parámetro, una colección que sale a otro método, una mezcla
    /// de lectura y escritura, un OpenMode o un 'escribir' que no es constante, o un UpgradeOpen() sobre la variable o
    /// sobre otra que puede ser el mismo objeto (no se mira si va antes o después del cambio). Un aviso no serviría: sale
    /// en la respuesta, después de ejecutar, y si el script cierra Civil 3D nunca llega. Así no se rompe ningún script
    /// válido, y cada rechazo se arregla igual: abrir para escritura, que siempre vale en un script que escribe.
    /// Si el análisis falla por cualquier motivo, no rechaza nada (el resto de la lista blanca sigue mandando).
    /// </summary>
    internal sealed class CodigoAperturas
    {
        private enum Clase { Nada, Lectura, Escritura, Desconocido }
        private enum Aspecto { Valor, Elementos, Retorno }

        /// <summary>Qué se sabe de un valor: nada (null o sin asignar), lectura (con el sitio de la apertura), escritura o desconocido.</summary>
        private readonly struct Estado
        {
            public readonly Clase Clase;
            public readonly SyntaxNode Origen;
            public Estado(Clase clase, SyntaxNode origen = null) { Clase = clase; Origen = origen; }
            public bool Igual(Estado o) => Clase == o.Clase && MismaSintaxis(Origen, o.Origen);
        }

        private static readonly Estado Nada = new Estado(Clase.Nada);
        private static readonly Estado Escritura = new Estado(Clase.Escritura);
        private static readonly Estado Desconocido = new Estado(Clase.Desconocido);

        /// <summary>
        /// Una variable o un parámetro (por su símbolo) o una lambda o función local (por su sintaxis: árbol, posición y
        /// clase, comparadas por valor), con el aspecto que se anota de ella.
        /// </summary>
        private readonly struct Nodo : IEquatable<Nodo>
        {
            public readonly object Clave;
            public readonly Aspecto Aspecto;
            public Nodo(ISymbol simbolo, Aspecto aspecto) { Clave = simbolo; Aspecto = aspecto; }
            public Nodo(SyntaxNode funcion, Aspecto aspecto) { Clave = (funcion.SyntaxTree, funcion.Span, funcion.RawKind); Aspecto = aspecto; }
            public bool Equals(Nodo o)
            {
                if (Aspecto != o.Aspecto) return false;
                if (Clave is ISymbol a && o.Clave is ISymbol b) return SymbolEqualityComparer.Default.Equals(a, b);
                return Equals(Clave, o.Clave);
            }
            public override bool Equals(object obj) => obj is Nodo n && Equals(n);
            public override int GetHashCode()
                => (Clave is ISymbol s ? SymbolEqualityComparer.Default.GetHashCode(s) : Clave.GetHashCode()) * 3 + (int)Aspecto;
        }

        /// <summary>Lo que se asigna a un nodo: una operación (vista como valor o como colección) o un estado fijo.</summary>
        private sealed class Definicion
        {
            public Nodo Destino;
            public IOperation Fuente;
            public Aspecto AspectoFuente;
            public Estado? Fijo;
        }

        private const string TipoOpenMode = CodigoReglas.TipoOpenMode;
        private const string TipoDBObject = "Autodesk.AutoCAD.DatabaseServices.DBObject";
        private const int MaximoVueltas = 200;

        /// <summary>Métodos de LINQ que devuelven los mismos elementos que su fuente (o parte de ellos).</summary>
        private static readonly HashSet<string> LinqConservan = new HashSet<string>(StringComparer.Ordinal)
        {
            "Where", "OfType", "Cast", "ToList", "ToArray", "ToHashSet", "AsEnumerable", "OrderBy", "OrderByDescending",
            "ThenBy", "ThenByDescending", "Take", "Skip", "TakeWhile", "SkipWhile", "TakeLast", "SkipLast", "Distinct",
            "DistinctBy", "Reverse", "DefaultIfEmpty", "Order", "OrderDescending"
        };

        /// <summary>Métodos de LINQ que unen dos secuencias.</summary>
        private static readonly HashSet<string> LinqUnen = new HashSet<string>(StringComparer.Ordinal) { "Concat", "Union", "Intersect", "Except" };

        /// <summary>Métodos que devuelven un elemento de su fuente (LINQ y colecciones del sistema).</summary>
        private static readonly HashSet<string> DevuelvenUnElemento = new HashSet<string>(StringComparer.Ordinal)
        {
            "First", "FirstOrDefault", "Last", "LastOrDefault", "Single", "SingleOrDefault", "ElementAt", "ElementAtOrDefault",
            "MinBy", "MaxBy", "Find", "FindLast", "Pop", "Peek", "Dequeue"
        };

        /// <summary>Métodos cuya lambda recibe los elementos de la fuente como primer parámetro (LINQ, List y Array).</summary>
        private static readonly HashSet<string> LambdaSobreElementos = new HashSet<string>(StringComparer.Ordinal)
        {
            "Select", "Where", "SelectMany", "First", "FirstOrDefault", "Last", "LastOrDefault", "Single", "SingleOrDefault",
            "Any", "All", "Count", "LongCount", "TakeWhile", "SkipWhile", "OrderBy", "OrderByDescending", "ThenBy",
            "ThenByDescending", "GroupBy", "ToDictionary", "ToLookup", "Sum", "Min", "Max", "Average", "MinBy", "MaxBy",
            "DistinctBy", "ForEach", "Find", "FindAll", "FindIndex", "FindLast", "FindLastIndex", "Exists", "TrueForAll",
            "RemoveAll"
        };

        /// <summary>Métodos de las colecciones del sistema que meten un elemento (el último argumento) o los de otra secuencia.</summary>
        private static readonly HashSet<string> MetenUnElemento = new HashSet<string>(StringComparer.Ordinal) { "Add", "Insert", "Push", "Enqueue", "AddFirst", "AddLast" };
        private static readonly HashSet<string> MetenVarios = new HashSet<string>(StringComparer.Ordinal) { "AddRange", "InsertRange", "UnionWith" };

        /// <summary>Métodos que dejan el objeto abierto para escritura: con ellos la variable ya no se considera de lectura.</summary>
        private static readonly HashSet<string> Promueven = new HashSet<string>(StringComparer.Ordinal) { "UpgradeOpen", "UpgradeFromNotify" };

        private readonly SemanticModel _m;
        private readonly string _tipoFachada;
        private readonly List<Definicion> _definiciones = new List<Definicion>();
        private readonly Dictionary<Nodo, Nodo> _padre = new Dictionary<Nodo, Nodo>();
        private readonly HashSet<Nodo> _promovidos = new HashSet<Nodo>();
        private Dictionary<Nodo, Estado> _estados = new Dictionary<Nodo, Estado>();
        private readonly HashSet<Nodo> _conDefiniciones = new HashSet<Nodo>();
        private readonly bool _valido;

        /// <summary>Analiza el método del script ('metodo': su declaración en el árbol que ve 'modelo').</summary>
        public CodigoAperturas(SemanticModel modelo, SyntaxNode metodo, string tipoFachada)
        {
            _m = modelo;
            _tipoFachada = tipoFachada;
            try
            {
                var raiz = metodo == null ? null : modelo.GetOperation(metodo);
                if (raiz == null) return;
                foreach (var op in raiz.Descendants()) Recoger(op);
                var promovidos = _promovidos.Select(Raiz).ToList();
                _promovidos.Clear();
                foreach (var p in promovidos) _promovidos.Add(p);
                foreach (var d in _definiciones) _conDefiniciones.Add(d.Destino);
                _valido = Calcular();
            }
            catch (Exception)
            {
                _valido = false;   // nunca rechaza por un fallo del análisis
            }
        }

        /// <summary>
        /// El sitio de la apertura para lectura de la que viene con seguridad 'receptor' (el objeto sobre el que se llama a
        /// un método o se asigna una propiedad), o null si no viene de una apertura para lectura o no se puede saber.
        /// </summary>
        public SyntaxNode OrigenLectura(IOperation receptor)
        {
            if (!_valido || receptor == null) return null;
            try
            {
                var e = Valor(receptor, 0);
                if (e.Clase != Clase.Lectura) return null;
                foreach (var r in Referencias(receptor, Aspecto.Valor, 0))
                    if (_promovidos.Contains(Raiz(r))) return null;
                return e.Origen;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ recogida de definiciones
        private void Recoger(IOperation op)
        {
            switch (op)
            {
                case IVariableDeclaratorOperation d:
                {
                    var init = d.GetVariableInitializer();
                    if (d.Symbol == null) break;
                    if (d.Symbol.RefKind != RefKind.None)
                    {
                        Fijar(d.Symbol, Desconocido);
                        if (init != null) foreach (var l in LocalesEn(init.Value)) Fijar(l, Desconocido);   // ref var r = ref l: r = x cambiaría l
                        break;
                    }
                    if (init == null) break;   // sin valor inicial (o el control de un foreach, que se anota aparte)
                    Definir(new Nodo(d.Symbol, Aspecto.Valor), init.Value, Aspecto.Valor);
                    Definir(new Nodo(d.Symbol, Aspecto.Elementos), init.Value, Aspecto.Elementos);
                    break;
                }
                case ISimpleAssignmentOperation a:
                    Asignar(a.Target, a.Value);
                    break;
                case ICoalesceAssignmentOperation ca:
                    Asignar(ca.Target, ca.Value);
                    break;
                case ICompoundAssignmentOperation c:
                    foreach (var l in LocalesDestino(c.Target)) Fijar(l, Desconocido);   // l += ..., no l.ColorIndex += ...
                    break;
                case IIncrementOrDecrementOperation i:
                    foreach (var l in LocalesDestino(i.Target)) Fijar(l, Desconocido);
                    break;
                case IDeconstructionAssignmentOperation da:
                    Deconstruir(da.Target, da.Value);
                    break;
                case IIsPatternOperation ip:
                    Patron(ip.Pattern, ip.Value, Aspecto.Valor);
                    break;
                case ISwitchExpressionOperation sw:
                    foreach (var arm in sw.Arms) Patron(arm.Pattern, sw.Value, Aspecto.Valor);
                    break;
                case ISwitchOperation s:
                    foreach (var caso in s.Cases)
                        foreach (var clausula in caso.Clauses)
                            if (clausula is IPatternCaseClauseOperation pc) Patron(pc.Pattern, s.Value, Aspecto.Valor);
                    break;
                case IForEachLoopOperation fe:
                    ControlDeForEach(fe.LoopControlVariable, fe.Collection);
                    break;
                case IArgumentOperation arg:
                    Argumento(arg);
                    break;
                case IInvocationOperation inv:
                    Invocacion(inv);
                    break;
                case IAnonymousFunctionOperation af:
                    Funcion(af.Syntax, af.Symbol, af.Body, LambdaFuenteDeElementos(af));
                    break;
                case ILocalFunctionOperation lf:
                    Funcion(lf.Syntax, lf.Symbol, lf.Body ?? lf.IgnoredBody, null);
                    break;
            }
        }

        private void Definir(Nodo destino, IOperation fuente, Aspecto aspectoFuente)
        {
            _definiciones.Add(new Definicion { Destino = destino, Fuente = fuente, AspectoFuente = aspectoFuente });
            foreach (var r in Referencias(fuente, aspectoFuente, 0)) Unir(destino, r);
        }

        private void Fijar(Nodo destino, Estado estado) => _definiciones.Add(new Definicion { Destino = destino, Fijo = estado });

        private void Fijar(ISymbol simbolo, Estado estado)
        {
            Fijar(new Nodo(simbolo, Aspecto.Valor), estado);
            Fijar(new Nodo(simbolo, Aspecto.Elementos), estado);
        }

        private void Asignar(IOperation destino, IOperation valor)
        {
            destino = Desenvolver(destino);
            switch (destino)
            {
                case IDeclarationExpressionOperation de:
                    Asignar(de.Expression, valor);
                    return;
                case ITupleOperation _:
                    Deconstruir(destino, valor);
                    return;
                case ILocalReferenceOperation lr:
                    Definir(new Nodo(lr.Local, Aspecto.Valor), valor, Aspecto.Valor);
                    Definir(new Nodo(lr.Local, Aspecto.Elementos), valor, Aspecto.Elementos);
                    // Dos variables con la misma colección: lo que se meta por una lo ve la otra
                    if (Desenvolver(valor) is ILocalReferenceOperation origen && !SymbolEqualityComparer.Default.Equals(origen.Local, lr.Local))
                        Definir(new Nodo(origen.Local, Aspecto.Elementos), lr, Aspecto.Elementos);
                    return;
                case IParameterReferenceOperation pr:
                    Definir(new Nodo(pr.Parameter, Aspecto.Valor), valor, Aspecto.Valor);
                    Definir(new Nodo(pr.Parameter, Aspecto.Elementos), valor, Aspecto.Elementos);
                    return;
                case IPropertyReferenceOperation ind when ind.Arguments.Length > 0 && EsColeccionDelSistema(ind.Instance?.Type):
                    // lista[i] = x
                    foreach (var n in NodosDeColeccion(ind.Instance)) Definir(n, valor, Aspecto.Valor);
                    return;
                case IArrayElementReferenceOperation ae:
                    foreach (var n in NodosDeColeccion(ae.ArrayReference)) Definir(n, valor, Aspecto.Valor);
                    return;
                default:
                    // Una colección local guardada en otro sitio (campo, propiedad): ya no se sabe qué se le mete
                    if (Desenvolver(valor) is ILocalReferenceOperation sale) Fijar(new Nodo(sale.Local, Aspecto.Elementos), Desconocido);
                    return;
            }
        }

        private void Deconstruir(IOperation destino, IOperation valor)
        {
            destino = Desenvolver(destino);
            if (destino is IDeclarationExpressionOperation de) destino = Desenvolver(de.Expression);
            var v = Desenvolver(valor);
            if (destino is ITupleOperation td && v is ITupleOperation tv && td.Elements.Length == tv.Elements.Length)
            {
                for (int i = 0; i < td.Elements.Length; i++) Asignar(td.Elements[i], tv.Elements[i]);
                return;
            }
            foreach (var l in LocalesDestino(destino)) Fijar(l, Desconocido);
        }

        /// <summary>Las variables que declara un patrón reciben 'entrada' (como valor o, en un patrón de lista, sus elementos).</summary>
        private void Patron(IPatternOperation patron, IOperation entrada, Aspecto aspecto)
        {
            switch (patron)
            {
                case IDeclarationPatternOperation dp:
                    if (dp.DeclaredSymbol is ILocalSymbol l) Recibir(l, entrada, aspecto);
                    return;
                case IRecursivePatternOperation rp:
                    if (rp.DeclaredSymbol is ILocalSymbol r) Recibir(r, entrada, aspecto);
                    foreach (var sub in rp.PropertySubpatterns) Desconocidas(sub);
                    foreach (var sub in rp.DeconstructionSubpatterns) Desconocidas(sub);
                    return;
                case IListPatternOperation lp:
                    if (lp.DeclaredSymbol is ILocalSymbol lista) Recibir(lista, entrada, aspecto);
                    foreach (var sub in lp.Patterns)
                    {
                        if (sub is ISlicePatternOperation) Desconocidas(sub);
                        else if (aspecto == Aspecto.Valor) Patron(sub, entrada, Aspecto.Elementos);
                        else Desconocidas(sub);
                    }
                    return;
                case IBinaryPatternOperation bp:
                    Patron(bp.LeftPattern, entrada, aspecto);
                    Patron(bp.RightPattern, entrada, aspecto);
                    return;
                case INegatedPatternOperation np:
                    Patron(np.Pattern, entrada, aspecto);
                    return;
            }
        }

        private void Recibir(ILocalSymbol l, IOperation entrada, Aspecto aspecto)
        {
            Definir(new Nodo(l, Aspecto.Valor), entrada, aspecto);
            if (aspecto == Aspecto.Valor) Definir(new Nodo(l, Aspecto.Elementos), entrada, Aspecto.Elementos);
            else Fijar(new Nodo(l, Aspecto.Elementos), Desconocido);
        }

        /// <summary>Toda variable que declara un subpatrón queda como desconocida.</summary>
        private void Desconocidas(IOperation op)
        {
            if (op == null) return;
            foreach (var d in new[] { op }.Concat(op.Descendants()))
            {
                ISymbol s = d is IDeclarationPatternOperation dp ? dp.DeclaredSymbol
                    : d is IRecursivePatternOperation rp ? rp.DeclaredSymbol
                    : d is IListPatternOperation lp ? lp.DeclaredSymbol : null;
                if (s != null) Fijar(s, Desconocido);
            }
        }

        private void ControlDeForEach(IOperation control, IOperation coleccion)
        {
            if (control is IVariableDeclaratorOperation d && d.Symbol != null)
            {
                if (d.Symbol.RefKind != RefKind.None)
                {
                    Fijar(d.Symbol, Desconocido);
                    foreach (var n in NodosDeColeccion(coleccion)) Fijar(n, Desconocido);   // foreach (ref var x in ...) cambia la colección
                    return;
                }
                Definir(new Nodo(d.Symbol, Aspecto.Valor), coleccion, Aspecto.Elementos);
                Fijar(new Nodo(d.Symbol, Aspecto.Elementos), Desconocido);
                return;
            }
            if (Desenvolver(control) is ILocalReferenceOperation lr)
            {
                Definir(new Nodo(lr.Local, Aspecto.Valor), coleccion, Aspecto.Elementos);
                Fijar(new Nodo(lr.Local, Aspecto.Elementos), Desconocido);
                return;
            }
            foreach (var l in LocalesDestino(control)) Fijar(l, Desconocido);   // foreach (var (a, b) in ...)
        }

        private void Argumento(IArgumentOperation arg)
        {
            var valor = Desenvolver(arg.Value);
            if (arg.Parameter != null && arg.Parameter.RefKind != RefKind.None && arg.Parameter.RefKind != RefKind.In)
            {
                // out y ref: el método puede poner cualquier cosa
                foreach (var l in LocalesDestino(valor)) Fijar(l, Desconocido);
                return;
            }
            if (!(valor is ILocalReferenceOperation lr)) return;
            // Una colección local que entra en un método que no es de LINQ ni de las colecciones del sistema: ya no se sabe qué se le mete
            var metodo = (arg.Parent as IInvocationOperation)?.TargetMethod ?? (arg.Parent as IObjectCreationOperation)?.Constructor;
            string espacio = metodo?.ContainingType?.ContainingNamespace?.ToDisplayString() ?? "";
            bool soloLee = espacio == "System.Linq" || espacio.StartsWith("System.Collections", StringComparison.Ordinal)
                || (metodo?.ContainingType?.SpecialType == SpecialType.System_String);
            if (!soloLee) Fijar(new Nodo(lr.Local, Aspecto.Elementos), Desconocido);
        }

        private void Invocacion(IInvocationOperation inv)
        {
            var m = inv.TargetMethod;
            if (m == null) return;
            // UpgradeOpen(): la variable (y las que pueden ser el mismo objeto) deja de considerarse de lectura
            if (Promueven.Contains(m.Name) && inv.Instance != null && EsDeAutodesk(m.ContainingType))
            {
                foreach (var r in Referencias(inv.Instance, Aspecto.Valor, 0)) _promovidos.Add(r);
                return;
            }
            // lista.Add(x), pila.Push(x), lista.AddRange(otra)...
            if (inv.Instance != null && EsColeccionDelSistema(inv.Instance.Type) && inv.Arguments.Length > 0)
            {
                var nodos = NodosDeColeccion(inv.Instance).ToList();
                if (nodos.Count == 0) return;
                var ultimo = inv.Arguments.Last().Value;
                if (MetenUnElemento.Contains(m.Name)) foreach (var n in nodos) Definir(n, ultimo, Aspecto.Valor);
                else if (MetenVarios.Contains(m.Name)) foreach (var n in nodos) Definir(n, ultimo, Aspecto.Elementos);
            }
        }

        private void Funcion(SyntaxNode sintaxis, IMethodSymbol simbolo, IOperation cuerpo, IOperation fuenteDeElementos)
        {
            if (simbolo == null) return;
            for (int i = 0; i < simbolo.Parameters.Length; i++)
            {
                var p = simbolo.Parameters[i];
                if (i == 0 && fuenteDeElementos != null) Definir(new Nodo(p, Aspecto.Valor), fuenteDeElementos, Aspecto.Elementos);
                else Fijar(new Nodo(p, Aspecto.Valor), Desconocido);
                Fijar(new Nodo(p, Aspecto.Elementos), Desconocido);
            }
            if (cuerpo == null) return;
            var retorno = new Nodo(sintaxis, Aspecto.Retorno);
            foreach (var r in cuerpo.Descendants().OfType<IReturnOperation>())
            {
                if (!MismaSintaxis(FuncionQueContiene(r), sintaxis)) continue;
                if (r.Kind == OperationKind.Return && r.ReturnedValue != null) Definir(retorno, r.ReturnedValue, Aspecto.Valor);
                else if (r.Kind == OperationKind.YieldReturn) Fijar(retorno, Desconocido);
            }
        }

        /// <summary>Si la lambda es el argumento de un método que le pasa los elementos de una fuente (Select, Where, ForEach...), esa fuente.</summary>
        private static IOperation LambdaFuenteDeElementos(IAnonymousFunctionOperation af)
        {
            IOperation hijo = af;
            var padre = af.Parent;
            if (padre is IDelegateCreationOperation dc) { hijo = dc; padre = dc.Parent; }
            if (padre is IConversionOperation cv) { hijo = cv; padre = cv.Parent; }
            if (!(padre is IArgumentOperation arg) || !(arg.Parent is IInvocationOperation inv)) return null;
            var m = inv.TargetMethod;
            if (m == null || !LambdaSobreElementos.Contains(m.Name)) return null;
            string tipo = CodigoRevisor.NombreCompleto(m.ContainingType);
            if (tipo != "System.Linq.Enumerable" && tipo != "System.Collections.Generic.List" && tipo != "System.Array") return null;
            var fuente = Fuente(inv);
            return fuente == null || MismaOperacion(fuente, arg.Value) || MismaOperacion(fuente, hijo) ? null : fuente;
        }

        private static SyntaxNode FuncionQueContiene(IOperation op)
        {
            for (var p = op.Parent; p != null; p = p.Parent)
            {
                if (p is IAnonymousFunctionOperation || p is ILocalFunctionOperation) return p.Syntax;
                if (p is IMethodBodyOperation) return null;
            }
            return null;
        }

        // ------------------------------------------------------------------ cálculo (punto fijo)
        private bool Calcular()
        {
            for (int vuelta = 0; vuelta < MaximoVueltas; vuelta++)
            {
                var nuevos = new Dictionary<Nodo, Estado>();
                foreach (var d in _definiciones)
                {
                    Estado e = d.Fijo ?? (d.AspectoFuente == Aspecto.Elementos ? Elementos(d.Fuente, 0) : Valor(d.Fuente, 0));
                    nuevos[d.Destino] = nuevos.TryGetValue(d.Destino, out var antes) ? Unir(antes, e) : e;
                }
                bool igual = nuevos.Count == _estados.Count && nuevos.All(kv => _estados.TryGetValue(kv.Key, out var v) && v.Igual(kv.Value));
                _estados = nuevos;
                if (igual) return true;
            }
            return false;   // no se estabilizó: no se rechaza nada
        }

        private static Estado Unir(Estado a, Estado b)
        {
            if (a.Clase == Clase.Nada) return b;
            if (b.Clase == Clase.Nada) return a;
            if (a.Clase != b.Clase) return Desconocido;
            if (a.Clase != Clase.Lectura) return a;
            // Dos aperturas para lectura: se cita la primera del código
            if (a.Origen == null) return b;
            if (b.Origen == null) return a;
            return b.Origen.SpanStart < a.Origen.SpanStart ? b : a;
        }

        /// <summary>
        /// El estado de un nodo. Si tiene definiciones y aún no se calculó, «nada» (el punto de partida del cálculo: así una
        /// función recursiva converge); si no tiene ninguna, 'porDefecto' (nada para una variable, desconocido para un
        /// parámetro o una función sin return).
        /// </summary>
        private Estado Leer(Nodo n, Estado porDefecto)
        {
            if (_estados.TryGetValue(n, out var e)) return e;
            return _conDefiniciones.Contains(n) ? Nada : porDefecto;
        }

        /// <summary>De dónde viene el valor de una expresión.</summary>
        private Estado Valor(IOperation op, int profundidad)
        {
            if (op == null || profundidad > 40) return Desconocido;
            op = Desenvolver(op);
            int p = profundidad + 1;
            switch (op)
            {
                case IConversionOperation _:
                    return Desconocido;   // conversión definida por el usuario
                case ILiteralOperation lit when lit.ConstantValue.HasValue && lit.ConstantValue.Value == null:
                case IDefaultValueOperation _:
                    return Nada;
                case ILocalReferenceOperation lr:
                    return Leer(new Nodo(lr.Local, Aspecto.Valor), Nada);
                case IParameterReferenceOperation pr:
                    return Leer(new Nodo(pr.Parameter, Aspecto.Valor), Desconocido);
                case IObjectCreationOperation _:
                case IAnonymousObjectCreationOperation _:
                case IArrayCreationOperation _:
                case ITypeParameterObjectCreationOperation _:
                case ICollectionExpressionOperation _:
                    return Escritura;   // un objeto nuevo, en memoria
                case IInvocationOperation inv:
                    return ValorDeInvocacion(inv, p);
                case IPropertyReferenceOperation pr:
                    if (pr.Arguments.Length > 0 && EsColeccionDelSistema(pr.Instance?.Type)) return Elementos(pr.Instance, p);
                    if (pr.Instance != null && EsSubobjetoCivil(pr.Type)) return Subobjeto(pr.Instance, p);
                    return Desconocido;
                case IArrayElementReferenceOperation ae:
                    return Elementos(ae.ArrayReference, p);
                case IConditionalOperation c when c.WhenFalse != null:
                    return Unir(Valor(c.WhenTrue, p), Valor(c.WhenFalse, p));
                case ICoalesceOperation co:
                    return Unir(Valor(co.Value, p), Valor(co.WhenNull, p));
                case IConditionalAccessOperation ca:
                    return Valor(ca.WhenNotNull, p);
                case IConditionalAccessInstanceOperation ci:
                    return Valor(ReceptorCondicional(ci), p);
                case ISwitchExpressionOperation sw:
                {
                    var e = Nada;
                    foreach (var arm in sw.Arms) e = Unir(e, Valor(arm.Value, p));
                    return e;
                }
                case ISimpleAssignmentOperation a:
                    return Valor(a.Value, p);
                case ICoalesceAssignmentOperation ca2:
                    return Unir(Valor(ca2.Target, p), Valor(ca2.Value, p));
                default:
                    return Desconocido;
            }
        }

        private Estado ValorDeInvocacion(IInvocationOperation inv, int p)
        {
            var m = inv.TargetMethod;
            if (m == null) return Desconocido;
            // F.Abrir<T>(id, escribir)
            if (CodigoRevisor.NombreCompleto(m.ContainingType) == _tipoFachada)
            {
                if (m.Name != CodigoReglas.FachadaAbrir) return Desconocido;
                var escribir = inv.Arguments.FirstOrDefault(a => a.Parameter?.Name == CodigoReglas.ParametroEscribir);
                if (escribir == null || escribir.ArgumentKind == ArgumentKind.DefaultValue) return new Estado(Clase.Lectura, inv.Syntax);
                var cv = escribir.Value.ConstantValue;
                if (!cv.HasValue || !(cv.Value is bool b)) return Desconocido;
                return b ? Escritura : new Estado(Clase.Lectura, inv.Syntax);
            }
            // tr.GetObject(id, OpenMode.X), id.GetObject(OpenMode.X)...: un método de Autodesk con un parámetro OpenMode
            var modo = inv.Arguments.FirstOrDefault(a => a.Parameter?.Type is INamedTypeSymbol t && CodigoRevisor.NombreCompleto(t) == TipoOpenMode);
            if (modo != null && EsDeAutodesk(m.ContainingType) && DerivaDe(m.ReturnType, TipoDBObject))
                return EstadoDelModo(modo, inv.Syntax);
            // Funciones locales: lo que devuelven
            if (m.MethodKind == MethodKind.LocalFunction)
            {
                var sintaxis = m.OriginalDefinition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                return sintaxis == null ? Desconocido : Leer(new Nodo(sintaxis, Aspecto.Retorno), Desconocido);
            }
            // Un elemento de una secuencia o colección (First, Find, Pop...)
            if (DevuelvenUnElemento.Contains(m.Name) && (EsLinq(m) || EsColeccionDelSistema(m.ContainingType)))
                return Elementos(Fuente(inv), p);
            // Un subobjeto de Civil 3D de un objeto abierto para lectura
            if (inv.Instance != null && EsSubobjetoCivil(m.ReturnType)) return Subobjeto(inv.Instance, p);
            return Desconocido;
        }

        private static Estado EstadoDelModo(IArgumentOperation modo, SyntaxNode sitio)
        {
            var cv = modo.Value.ConstantValue;
            if (!cv.HasValue || !(modo.Parameter.Type is INamedTypeSymbol tipo)) return Desconocido;
            object escritura = tipo.GetMembers("ForWrite").OfType<IFieldSymbol>().FirstOrDefault()?.ConstantValue;
            if (escritura == null) return Desconocido;
            return Equals(cv.Value, escritura) ? Escritura : new Estado(Clase.Lectura, sitio);   // ForRead y ForNotify: no se puede escribir
        }

        /// <summary>De dónde vienen los elementos de una secuencia o colección.</summary>
        private Estado Elementos(IOperation op, int profundidad)
        {
            if (op == null || profundidad > 40) return Desconocido;
            op = Desenvolver(op);
            int p = profundidad + 1;
            switch (op)
            {
                case ILocalReferenceOperation lr:
                    return Leer(new Nodo(lr.Local, Aspecto.Elementos), Nada);
                case IParameterReferenceOperation pr:
                    return Leer(new Nodo(pr.Parameter, Aspecto.Elementos), Desconocido);
                case ILiteralOperation lit when lit.ConstantValue.HasValue && lit.ConstantValue.Value == null:
                case IDefaultValueOperation _:
                    return Nada;
                case IArrayCreationOperation ac:
                    return ac.Initializer == null ? Nada : ElementosDeInicializador(ac.Initializer, p);
                case ICollectionExpressionOperation ce:
                {
                    var e = Nada;
                    foreach (var el in ce.Elements) e = Unir(e, el is ISpreadOperation sp ? Elementos(sp.Operand, p) : Valor(el, p));
                    return e;
                }
                case IObjectCreationOperation oc:
                {
                    var e = Nada;
                    // new List<Line>(otra): los de la otra
                    foreach (var a in oc.Arguments)
                        if (EsColeccionDelSistema(a.Value?.Type) || a.Value?.Type?.AllInterfaces.Any(i => i.Name == "IEnumerable") == true)
                            e = Unir(e, Elementos(a.Value, p));
                    // new List<Line> { a, b }: los del inicializador
                    if (oc.Initializer != null)
                        foreach (var ini in oc.Initializer.Initializers)
                            if (ini is IInvocationOperation add && add.Arguments.Length > 0) e = Unir(e, Valor(add.Arguments.Last().Value, p));
                            else e = Desconocido;
                    return e;
                }
                case IInvocationOperation inv:
                {
                    var m = inv.TargetMethod;
                    if (m == null || !EsLinq(m)) return SubobjetoColeccion(op, p);
                    if (LinqConservan.Contains(m.Name)) return Elementos(Fuente(inv), p);
                    if (LinqUnen.Contains(m.Name) && inv.Arguments.Length > 1) return Unir(Elementos(inv.Arguments[0].Value, p), Elementos(inv.Arguments[1].Value, p));
                    if ((m.Name == "Append" || m.Name == "Prepend") && inv.Arguments.Length > 1) return Unir(Elementos(inv.Arguments[0].Value, p), Valor(inv.Arguments[1].Value, p));
                    if (m.Name == "Select" && inv.Arguments.Length > 1)
                    {
                        var lambda = Lambda(inv.Arguments[1].Value);
                        return lambda == null ? Desconocido : Leer(new Nodo(lambda.Syntax, Aspecto.Retorno), Desconocido);
                    }
                    return Desconocido;
                }
                case IConditionalOperation c when c.WhenFalse != null:
                    return Unir(Elementos(c.WhenTrue, p), Elementos(c.WhenFalse, p));
                case ICoalesceOperation co:
                    return Unir(Elementos(co.Value, p), Elementos(co.WhenNull, p));
                case ISimpleAssignmentOperation a:
                    return Elementos(a.Value, p);
                default:
                    return SubobjetoColeccion(op, p);
            }
        }

        private Estado ElementosDeInicializador(IArrayInitializerOperation ini, int p)
        {
            var e = Nada;
            foreach (var v in ini.ElementValues)
                e = Unir(e, v is IArrayInitializerOperation anidado ? ElementosDeInicializador(anidado, p) : Valor(v, p));
            return e;
        }

        /// <summary>Una colección de Civil 3D de un objeto abierto para lectura (c.Baselines): sus elementos también lo están.</summary>
        private Estado SubobjetoColeccion(IOperation op, int p)
        {
            if (!EsSubobjetoCivil(op.Type)) return Desconocido;
            var e = Valor(op, p);
            return e.Clase == Clase.Lectura ? e : Desconocido;
        }

        private Estado Subobjeto(IOperation instancia, int p)
        {
            var e = Valor(instancia, p);
            return e.Clase == Clase.Lectura ? e : Desconocido;
        }

        // ------------------------------------------------------------------ alias (para UpgradeOpen)
        /// <summary>Los nodos que pueden ser el mismo objeto (o la misma colección) que la expresión.</summary>
        private IEnumerable<Nodo> Referencias(IOperation op, Aspecto aspecto, int profundidad)
        {
            if (op == null || profundidad > 40) return Enumerable.Empty<Nodo>();
            op = Desenvolver(op);
            int p = profundidad + 1;
            var lista = new List<Nodo>();
            switch (op)
            {
                case ILocalReferenceOperation lr:
                    lista.Add(new Nodo(lr.Local, aspecto == Aspecto.Elementos ? Aspecto.Elementos : Aspecto.Valor));
                    break;
                case IParameterReferenceOperation pr:
                    lista.Add(new Nodo(pr.Parameter, aspecto == Aspecto.Elementos ? Aspecto.Elementos : Aspecto.Valor));
                    break;
                case IConditionalOperation c when c.WhenFalse != null:
                    lista.AddRange(Referencias(c.WhenTrue, aspecto, p));
                    lista.AddRange(Referencias(c.WhenFalse, aspecto, p));
                    break;
                case ICoalesceOperation co:
                    lista.AddRange(Referencias(co.Value, aspecto, p));
                    lista.AddRange(Referencias(co.WhenNull, aspecto, p));
                    break;
                case ISwitchExpressionOperation sw:
                    foreach (var arm in sw.Arms) lista.AddRange(Referencias(arm.Value, aspecto, p));
                    break;
                case ISimpleAssignmentOperation a:
                    lista.AddRange(Referencias(a.Value, aspecto, p));
                    break;
                case ICoalesceAssignmentOperation ca:
                    lista.AddRange(Referencias(ca.Target, aspecto, p));
                    lista.AddRange(Referencias(ca.Value, aspecto, p));
                    break;
                case IConditionalAccessOperation cao:
                    lista.AddRange(Referencias(cao.WhenNotNull, aspecto, p));
                    break;
                case IConditionalAccessInstanceOperation ci:
                    lista.AddRange(Referencias(ReceptorCondicional(ci), aspecto, p));
                    break;
                case IArrayElementReferenceOperation ae when aspecto == Aspecto.Valor:
                    lista.AddRange(Referencias(ae.ArrayReference, Aspecto.Elementos, p));
                    break;
                case IPropertyReferenceOperation ind when aspecto == Aspecto.Valor && ind.Arguments.Length > 0 && EsColeccionDelSistema(ind.Instance?.Type):
                    lista.AddRange(Referencias(ind.Instance, Aspecto.Elementos, p));
                    break;
                case IPropertyReferenceOperation sub when sub.Instance != null && EsSubobjetoCivil(sub.Type):
                    lista.AddRange(Referencias(sub.Instance, Aspecto.Valor, p));
                    break;
                case IArrayCreationOperation ac when aspecto == Aspecto.Elementos && ac.Initializer != null:
                    foreach (var v in ac.Initializer.ElementValues) lista.AddRange(Referencias(v, Aspecto.Valor, p));
                    break;
                case ICollectionExpressionOperation ce when aspecto == Aspecto.Elementos:
                    foreach (var el in ce.Elements)
                        lista.AddRange(el is ISpreadOperation sp ? Referencias(sp.Operand, Aspecto.Elementos, p) : Referencias(el, Aspecto.Valor, p));
                    break;
                case IObjectCreationOperation oc when aspecto == Aspecto.Elementos:
                    foreach (var a in oc.Arguments) lista.AddRange(Referencias(a.Value, Aspecto.Elementos, p));
                    if (oc.Initializer != null)
                        foreach (var ini in oc.Initializer.Initializers)
                            if (ini is IInvocationOperation add && add.Arguments.Length > 0) lista.AddRange(Referencias(add.Arguments.Last().Value, Aspecto.Valor, p));
                    break;
                case IInvocationOperation inv:
                {
                    var m = inv.TargetMethod;
                    if (m == null) break;
                    if (aspecto == Aspecto.Valor)
                    {
                        if (m.MethodKind == MethodKind.LocalFunction)
                        {
                            var sintaxis = m.OriginalDefinition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax();
                            if (sintaxis != null) lista.Add(new Nodo(sintaxis, Aspecto.Retorno));
                        }
                        else if (DevuelvenUnElemento.Contains(m.Name) && (EsLinq(m) || EsColeccionDelSistema(m.ContainingType)))
                            lista.AddRange(Referencias(Fuente(inv), Aspecto.Elementos, p));
                        else if (inv.Instance != null && EsSubobjetoCivil(m.ReturnType))
                            lista.AddRange(Referencias(inv.Instance, Aspecto.Valor, p));
                    }
                    else if (EsLinq(m))
                    {
                        if (LinqConservan.Contains(m.Name)) lista.AddRange(Referencias(Fuente(inv), Aspecto.Elementos, p));
                        else if (LinqUnen.Contains(m.Name) && inv.Arguments.Length > 1)
                        {
                            lista.AddRange(Referencias(inv.Arguments[0].Value, Aspecto.Elementos, p));
                            lista.AddRange(Referencias(inv.Arguments[1].Value, Aspecto.Elementos, p));
                        }
                        else if ((m.Name == "Append" || m.Name == "Prepend") && inv.Arguments.Length > 1)
                        {
                            lista.AddRange(Referencias(inv.Arguments[0].Value, Aspecto.Elementos, p));
                            lista.AddRange(Referencias(inv.Arguments[1].Value, Aspecto.Valor, p));
                        }
                        else if (m.Name == "Select" && inv.Arguments.Length > 1 && Lambda(inv.Arguments[1].Value) is IAnonymousFunctionOperation lambda)
                            lista.Add(new Nodo(lambda.Syntax, Aspecto.Retorno));
                    }
                    else if (EsSubobjetoCivil(inv.Type)) lista.AddRange(Referencias(inv, Aspecto.Valor, p));
                    break;
                }
                default:
                    if (aspecto == Aspecto.Elementos && EsSubobjetoCivil(op.Type)) lista.AddRange(Referencias(op, Aspecto.Valor, p));
                    break;
            }
            return lista;
        }

        private Nodo Raiz(Nodo n)
        {
            while (_padre.TryGetValue(n, out var p) && !p.Equals(n)) n = p;
            return n;
        }

        private void Unir(Nodo a, Nodo b)
        {
            var ra = Raiz(a);
            var rb = Raiz(b);
            if (!ra.Equals(rb)) _padre[ra] = rb;
        }

        // ------------------------------------------------------------------ ayudas
        private static IOperation Desenvolver(IOperation op)
        {
            while (true)
            {
                if (op is IConversionOperation c && c.OperatorMethod == null) { op = c.Operand; continue; }
                if (op is IParenthesizedOperation pa) { op = pa.Operand; continue; }
                return op;
            }
        }

        /// <summary>El objeto de a?.B: la expresión a la izquierda del ?. que contiene esta instancia.</summary>
        private static IOperation ReceptorCondicional(IConditionalAccessInstanceOperation ci)
        {
            IOperation hijo = ci;
            for (var p = ci.Parent; p != null; hijo = p, p = p.Parent)
                if (p is IConditionalAccessOperation ca && MismaOperacion(ca.WhenNotNull, hijo)) return ca.Operation;
            return null;
        }

        private static bool MismaSintaxis(SyntaxNode a, SyntaxNode b)
            => ReferenceEquals(a, b) || (a != null && b != null && a.SyntaxTree == b.SyntaxTree && a.Span == b.Span && a.RawKind == b.RawKind);

        private static bool MismaOperacion(IOperation a, IOperation b)
            => ReferenceEquals(a, b) || (a != null && b != null && a.Kind == b.Kind && MismaSintaxis(a.Syntax, b.Syntax));

        private static IAnonymousFunctionOperation Lambda(IOperation op)
        {
            op = Desenvolver(op);
            if (op is IDelegateCreationOperation dc) op = dc.Target;
            return op as IAnonymousFunctionOperation;
        }

        /// <summary>La fuente de un método de LINQ o de una colección: el objeto (lista.Find) o el primer argumento (extensión, Array.ForEach).</summary>
        private static IOperation Fuente(IInvocationOperation inv)
            => inv.Instance ?? (inv.Arguments.Length > 0 ? inv.Arguments[0].Value : null);

        /// <summary>Los nodos de una colección local (para lista.Add(x), lista[i] = x, arr[i] = x).</summary>
        private static IEnumerable<Nodo> NodosDeColeccion(IOperation op)
        {
            op = Desenvolver(op);
            if (op is ILocalReferenceOperation lr) yield return new Nodo(lr.Local, Aspecto.Elementos);
            else if (op is IParameterReferenceOperation pr) yield return new Nodo(pr.Parameter, Aspecto.Elementos);
        }

        /// <summary>
        /// Las variables que reciben un valor en un destino: la variable misma, o las de una tupla o una declaración
        /// ((a, var b) = ..., out var x). No las de un miembro: en l.ColorIndex++ no cambia l.
        /// </summary>
        private static IEnumerable<ILocalSymbol> LocalesDestino(IOperation op)
        {
            op = Desenvolver(op);
            switch (op)
            {
                case ILocalReferenceOperation lr:
                    yield return lr.Local;
                    break;
                case IDeclarationExpressionOperation de:
                    foreach (var l in LocalesDestino(de.Expression)) yield return l;
                    break;
                case ITupleOperation t:
                    foreach (var e in t.Elements)
                        foreach (var l in LocalesDestino(e)) yield return l;
                    break;
            }
        }

        private static IEnumerable<ILocalSymbol> LocalesEn(IOperation op)
        {
            if (op == null) yield break;
            foreach (var d in new[] { op }.Concat(op.Descendants()))
                if (d is ILocalReferenceOperation lr) yield return lr.Local;
        }

        private static bool EsLinq(IMethodSymbol m) => CodigoRevisor.NombreCompleto(m.ContainingType) == "System.Linq.Enumerable";

        private static bool EsDeAutodesk(ITypeSymbol t)
            => t is INamedTypeSymbol nt && (CodigoRevisor.NombreCompleto(nt) ?? "").StartsWith("Autodesk.", StringComparison.Ordinal);

        /// <summary>Arreglos y colecciones de System.Collections (List, HashSet, Stack, Queue, IEnumerable...).</summary>
        private static bool EsColeccionDelSistema(ITypeSymbol t)
        {
            if (t is IArrayTypeSymbol) return true;
            if (!(t is INamedTypeSymbol nt)) return false;
            string espacio = nt.ContainingNamespace?.ToDisplayString() ?? "";
            return espacio == "System.Collections" || espacio.StartsWith("System.Collections.", StringComparison.Ordinal);
        }

        /// <summary>Un tipo de Civil 3D que no es un DBObject (líneas base, regiones, colecciones, entidades de un eje...): vive dentro de su objeto.</summary>
        private static bool EsSubobjetoCivil(ITypeSymbol t)
        {
            if (!(t is INamedTypeSymbol nt) || (nt.TypeKind != TypeKind.Class && nt.TypeKind != TypeKind.Interface)) return false;
            if (!(CodigoRevisor.NombreCompleto(nt) ?? "").StartsWith("Autodesk.Civil.", StringComparison.Ordinal)) return false;
            return !DerivaDe(nt, TipoDBObject);
        }

        private static bool DerivaDe(ITypeSymbol t, string nombre)
        {
            if (t is ITypeParameterSymbol tp) return tp.ConstraintTypes.Any(c => DerivaDe(c, nombre));
            for (var b = t as INamedTypeSymbol; b != null; b = b.BaseType)
                if (CodigoRevisor.NombreCompleto(b) == nombre) return true;
            return false;
        }
    }
}
