using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace ArbaMcp.Nucleo
{
    /// <summary>Un uso del script que la lista blanca no admite, con su posición en el código del agente.</summary>
    public sealed class ProblemaCodigo
    {
        public int Linea;
        public int Columna;
        public string Categoria;
        public string Mensaje;

        public override string ToString() => "Línea " + Linea + ", columna " + Columna + ": [bloqueado: " + Categoria + "] " + Mensaje;
    }

    /// <summary>Nombres de los tipos del plugin que el script puede usar.</summary>
    public sealed class OpcionesRevision
    {
        /// <summary>Nombre completo de la Fachada (sin global::). El script usa todos sus miembros públicos.</summary>
        public string TipoFachada = "ArbaMcp.Fachada";
        /// <summary>Nombre completo de CodigoVigilante: el script (y el código que se inserta) solo puede llamar a Revisar().</summary>
        public string TipoVigilante = "ArbaMcp.Nucleo.CodigoVigilante";
        /// <summary>
        /// solo_lectura=true (desde 1.17.6): lo que escribe (CodigoReglas, categoría «solo lectura») no compila. Sin ella
        /// esos usos solo se cuentan en 'escrituras' (para el aviso que sugiere solo_lectura).
        /// </summary>
        public bool SoloLectura;
    }

    /// <summary>
    /// Revisión de la lista blanca de ejecutar_codigo (desde 1.16.0) con el árbol sintáctico y el modelo semántico de
    /// Roslyn, sin Civil 3D. Recorre solo el código del agente (las 'zonas': su cuerpo y sus using), no el envoltorio que
    /// añade el plugin, y por cada nodo comprueba la sintaxis prohibida (unsafe, goto, lock, await, atributos, #line...)
    /// y el símbolo al que se refiere (tipo, miembro, evento) y el tipo de cada expresión, con las reglas de
    /// CodigoReglas. Un script con algún problema no se compila.
    /// </summary>
    public static class CodigoRevisor
    {
        public const int MaximoProblemas = 20;

        public static List<ProblemaCodigo> Revisar(SemanticModel modelo, IReadOnlyList<TextSpan> zonas, OpcionesRevision op = null)
            => Revisar(modelo, zonas, op, null);

        /// <summary>
        /// Igual, y además deja en 'escrituras' (si no es null) cada uso que escribe según las reglas de solo lectura
        /// (desde 1.17.6), haya o no solo_lectura: con op.SoloLectura también van a los problemas y el script no compila.
        /// </summary>
        public static List<ProblemaCodigo> Revisar(SemanticModel modelo, IReadOnlyList<TextSpan> zonas, OpcionesRevision op, List<ProblemaCodigo> escrituras)
        {
            op = op ?? new OpcionesRevision();
            var r = new Recorrido(modelo, op);
            var raiz = modelo.SyntaxTree.GetRoot();
            foreach (var zona in zonas)
            {
                foreach (var nodo in raiz.DescendantNodes(zona))
                {
                    if (!zona.Contains(nodo.Span)) continue;
                    r.Nodo(nodo);
                }
                foreach (var token in raiz.DescendantTokens(zona))
                {
                    if (!zona.Contains(token.Span)) continue;
                    r.Token(token);
                }
                foreach (var tr in raiz.DescendantTrivia(zona))
                    r.Trivia(tr, zona);
            }
            escrituras?.AddRange(r.Escrituras.OrderBy(p => p.Linea).ThenBy(p => p.Columna).Take(MaximoProblemas));
            return r.Problemas.OrderBy(p => p.Linea).ThenBy(p => p.Columna).Take(MaximoProblemas).ToList();
        }

        private sealed class Recorrido
        {
            private readonly SemanticModel _m;
            private readonly OpcionesRevision _op;
            private readonly HashSet<string> _vistos = new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> _vistasEscrituras = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<ProblemaCodigo> Problemas = new List<ProblemaCodigo>();
            /// <summary>Usos que escriben según las reglas de solo lectura (1.17.6), con o sin solo_lectura.</summary>
            public readonly List<ProblemaCodigo> Escrituras = new List<ProblemaCodigo>();

            public Recorrido(SemanticModel m, OpcionesRevision op) { _m = m; _op = op; }

            private void Anotar(SyntaxNode nodo, string categoria, string mensaje) => Anotar(nodo.GetLocation(), categoria, mensaje);

            private void Anotar(Location lugar, string categoria, string mensaje)
            {
                var span = lugar.GetMappedLineSpan();
                int linea = span.StartLinePosition.Line + 1, columna = span.StartLinePosition.Character + 1;
                // El mismo nombre bloqueado en la misma línea se anota una vez (un nombre cualificado lo verían varios nodos)
                if (!_vistos.Add(linea + "|" + categoria + "|" + mensaje)) return;
                Problemas.Add(new ProblemaCodigo { Linea = linea, Columna = columna, Categoria = categoria, Mensaje = mensaje });
            }

            private void Bloqueo(SyntaxNode nodo, string categoria, string nombre)
                => Anotar(nodo, categoria, nombre + ": " + CodigoReglas.Explicacion(categoria));

            /// <summary>
            /// Un uso que escribe (1.17.6): siempre se cuenta en Escrituras; con solo_lectura además es un problema y el
            /// script no compila.
            /// </summary>
            private void Escritura(SyntaxNode nodo, string que)
            {
                string mensaje = que + ": " + CodigoReglas.Explicacion(CodigoReglas.SoloLectura);
                var span = nodo.GetLocation().GetMappedLineSpan();
                int linea = span.StartLinePosition.Line + 1, columna = span.StartLinePosition.Character + 1;
                if (_vistasEscrituras.Add(linea + "|" + mensaje))
                    Escrituras.Add(new ProblemaCodigo { Linea = linea, Columna = columna, Categoria = CodigoReglas.SoloLectura, Mensaje = mensaje });
                if (_op.SoloLectura) Anotar(nodo, CodigoReglas.SoloLectura, mensaje);
            }

            // ------------------------------------------------------------------ sintaxis
            public void Token(SyntaxToken t)
            {
                switch (t.Kind())
                {
                    case SyntaxKind.UnsafeKeyword: Anotar(t.GetLocation(), CodigoReglas.Nativo, "unsafe: " + CodigoReglas.Explicacion(CodigoReglas.Nativo)); break;
                    case SyntaxKind.ExternKeyword: Anotar(t.GetLocation(), CodigoReglas.Nativo, "extern: " + CodigoReglas.Explicacion(CodigoReglas.Nativo)); break;
                    case SyntaxKind.AsyncKeyword:
                        if (t.Parent is LocalFunctionStatementSyntax || t.Parent is LambdaExpressionSyntax || t.Parent is AnonymousMethodExpressionSyntax)
                            Anotar(t.GetLocation(), CodigoReglas.Hilos, "async: " + CodigoReglas.Explicacion(CodigoReglas.Hilos));
                        break;
                    case SyntaxKind.AwaitKeyword: Anotar(t.GetLocation(), CodigoReglas.Hilos, "await: " + CodigoReglas.Explicacion(CodigoReglas.Hilos)); break;
                    case SyntaxKind.ArgListKeyword:
                    case SyntaxKind.MakeRefKeyword:
                    case SyntaxKind.RefTypeKeyword:
                    case SyntaxKind.RefValueKeyword:
                        Anotar(t.GetLocation(), CodigoReglas.Nativo, t.Text + ": " + CodigoReglas.Explicacion(CodigoReglas.Nativo)); break;
                    case SyntaxKind.IdentifierToken:
                        if (t.ValueText.StartsWith("__arba", StringComparison.OrdinalIgnoreCase))
                            Anotar(t.GetLocation(), CodigoReglas.Sintaxis, "'" + t.ValueText + "': los nombres que empiezan por __arba están reservados para el plugin.");
                        break;
                }
            }

            /// <summary>
            /// Directivas #line escritas por el agente. Solo las que están dentro de su zona: el #line 1 "script" del plugin
            /// es trivia del primer token del cuerpo, y un #line al final del código sería trivia del 'return' del plugin.
            /// </summary>
            public void Trivia(SyntaxTrivia tr, TextSpan zona)
            {
                if (!zona.Contains(tr.Span)) return;
                if (tr.IsKind(SyntaxKind.LineDirectiveTrivia) || tr.IsKind(SyntaxKind.LineSpanDirectiveTrivia))
                    Anotar(tr.GetLocation(), CodigoReglas.Sintaxis, "#line: el script no puede cambiar los números de línea (los usa el plugin para los mensajes de error).");
            }

            public void Nodo(SyntaxNode n)
            {
                switch (n)
                {
                    case UnsafeStatementSyntax _:
                    case FixedStatementSyntax _:
                    case StackAllocArrayCreationExpressionSyntax _:
                    case ImplicitStackAllocArrayCreationExpressionSyntax _:
                    case PointerTypeSyntax _:
                    case FunctionPointerTypeSyntax _:
                        Bloqueo(n, CodigoReglas.Nativo, Primeras(n));
                        return;
                    case PrefixUnaryExpressionSyntax pu when pu.IsKind(SyntaxKind.AddressOfExpression) || pu.IsKind(SyntaxKind.PointerIndirectionExpression):
                        Bloqueo(n, CodigoReglas.Nativo, Primeras(n));
                        return;
                    case MemberAccessExpressionSyntax pm when pm.IsKind(SyntaxKind.PointerMemberAccessExpression):
                        Bloqueo(n, CodigoReglas.Nativo, Primeras(n));
                        return;
                    case GotoStatementSyntax _:
                        Anotar(n, CodigoReglas.Sintaxis, "goto: no está permitido (el tiempo límite revisa los bucles; con goto se podría saltar esa revisión). Usa while, for o foreach.");
                        return;
                    case LockStatementSyntax _:
                        Bloqueo(n, CodigoReglas.Hilos, "lock");
                        return;
                    case AttributeListSyntax al:
                        bool nativo = al.Attributes.Any(a => a.Name.ToString().Contains("DllImport") || a.Name.ToString().Contains("LibraryImport"));
                        if (nativo) Bloqueo(n, CodigoReglas.Nativo, al.ToString());
                        else Anotar(n, CodigoReglas.Sintaxis, al + ": el script no admite atributos.");
                        return;
                    case MemberDeclarationSyntax md when !(md is GlobalStatementSyntax):
                        Anotar(n, CodigoReglas.Sintaxis, "el código es el cuerpo de un método: no declares clases, métodos, propiedades ni campos (usa variables y funciones locales).");
                        return;
                    case UsingDirectiveSyntax u:
                        Using(u);
                        return;
                }

                // Semántica: a qué se refiere cada nombre, constructor, indexador u operador
                if (n is SimpleNameSyntax || n is ObjectCreationExpressionSyntax || n is ImplicitObjectCreationExpressionSyntax
                    || n is ElementAccessExpressionSyntax || n is BinaryExpressionSyntax || n is PrefixUnaryExpressionSyntax
                    || n is PostfixUnaryExpressionSyntax || n is AssignmentExpressionSyntax)
                {
                    if (n is SimpleNameSyntax && n.Ancestors().Any(a => a is UsingDirectiveSyntax)) return;
                    var info = _m.GetSymbolInfo(n);
                    var s = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                    if (s != null) Simbolo(s, n);
                }

                // Argumentos IntPtr: solo IntPtr.Zero (Entity.IntersectWith pide dos IntPtr que siempre van a cero)
                if (n is InvocationExpressionSyntax || n is ObjectCreationExpressionSyntax || n is ImplicitObjectCreationExpressionSyntax)
                    Argumentos(n);

                // Tipo de cada expresión (atrapa un tipo bloqueado aunque no se nombre: var x = algo.Que.Devuelve();)
                if (n is ExpressionSyntax e && !(n.Parent is UsingDirectiveSyntax))
                {
                    var ti = _m.GetTypeInfo(e);
                    Tipo(ti.Type, n);
                    if (!SymbolEqualityComparer.Default.Equals(ti.Type, ti.ConvertedType)) Tipo(ti.ConvertedType, n);
                    ModoDeApertura(e, ti.Type);
                    OpenModeEnTipoCompuesto(e, ti);
                }
                OpenModeEnMetodosYTypeof(n);
            }

            // ------------------------------------------------------------------ OpenMode escondido (auditoría de la ronda C, I4)
            // Sin escribir ninguna expresión OpenMode se podía abrir para escritura: new[] { id }.Zip(Enum.GetValues<OpenMode>()
            // .Skip(1), F.Tr.GetObject), o una función local genérica con T = OpenMode. Con solo_lectura no compila ningún tipo
            // que lleve OpenMode dentro (OpenMode[], IEnumerable<OpenMode>, Func<..., OpenMode, ...>), ni typeof(OpenMode), ni
            // un método con OpenMode como argumento genérico, ni un grupo de métodos (delegado) con un parámetro OpenMode.

            private static bool EsOpenMode(ITypeSymbol t) => t is INamedTypeSymbol nt && NombreCompleto(nt) == CodigoReglas.TipoOpenMode;

            /// <summary>True si el tipo lleva OpenMode dentro (elemento de un arreglo, argumento genérico, elemento de una tupla), sin ser OpenMode.</summary>
            private static bool ContieneOpenMode(ITypeSymbol t, int profundidad = 0)
            {
                if (t == null || profundidad > 8) return false;
                switch (t)
                {
                    case IArrayTypeSymbol a: return EsOpenMode(a.ElementType) || ContieneOpenMode(a.ElementType, profundidad + 1);
                    case INamedTypeSymbol nt:
                        foreach (var ta in nt.TypeArguments)
                            if (EsOpenMode(ta) || ContieneOpenMode(ta, profundidad + 1)) return true;
                        return nt.IsTupleType && nt.TupleElements.Any(e => EsOpenMode(e.Type) || ContieneOpenMode(e.Type, profundidad + 1));
                }
                return false;
            }

            private void OpenModeEnTipoCompuesto(ExpressionSyntax e, TypeInfo ti)
            {
                var t = ContieneOpenMode(ti.Type) ? ti.Type : ContieneOpenMode(ti.ConvertedType) ? ti.ConvertedType : null;
                if (t == null) return;
                // Solo la expresión más exterior con ese tipo (en a.b.c, no también a.b si lleva el mismo tipo)
                if (e.Parent is ExpressionSyntax padre)
                {
                    var tp = _m.GetTypeInfo(padre);
                    if (ContieneOpenMode(tp.Type) || ContieneOpenMode(tp.ConvertedType)) return;
                }
                Escritura(e, "tipo con OpenMode dentro (" + t.ToDisplayString() + ")");
            }

            private void OpenModeEnMetodosYTypeof(SyntaxNode n)
            {
                if (n is TypeOfExpressionSyntax tof)
                {
                    if (EsOpenMode(_m.GetTypeInfo(tof.Type).Type)) Escritura(n, "typeof(OpenMode)");
                    return;
                }
                if (!(n is SimpleNameSyntax sn)) return;
                var info = _m.GetSymbolInfo(sn);
                if (!((info.Symbol ?? info.CandidateSymbols.FirstOrDefault()) is IMethodSymbol m)) return;
                if (m.TypeArguments.Any(t => EsOpenMode(t) || ContieneOpenMode(t)))
                    Escritura(sn, "OpenMode como argumento genérico de " + m.Name);
                if (EsGrupoDeMetodos(sn) && m.Parameters.Any(p => EsOpenMode(p.Type) || ContieneOpenMode(p.Type)))
                    Escritura(sn, "grupo de métodos (delegado) con un parámetro OpenMode: " + m.Name);
            }

            /// <summary>True si el nombre de un método se usa sin llamarlo (F.Tr.GetObject pasado como delegado).</summary>
            private static bool EsGrupoDeMetodos(SimpleNameSyntax sn)
            {
                SyntaxNode expr = sn;
                if (sn.Parent is MemberAccessExpressionSyntax ma && ma.Name == sn) expr = ma;
                else if (sn.Parent is MemberBindingExpressionSyntax mb) expr = mb;
                if (expr.Parent is InvocationExpressionSyntax inv && inv.Expression == expr) return false;
                // a?.Metodo(...): la llamada envuelve al acceso condicional
                if (expr is MemberBindingExpressionSyntax && expr.Parent is InvocationExpressionSyntax inv2 && inv2.Expression == expr) return false;
                return !(expr.Parent is MemberAccessExpressionSyntax padre && padre.Expression == expr);   // x.Metodo.Algo no es C# válido con un método
            }

            /// <summary>
            /// Solo lectura (1.17.6): toda expresión de tipo OpenMode tiene que ser la constante OpenMode.ForRead. Atrapa
            /// ForWrite y ForNotify, las conversiones ((OpenMode)1), las variables y las condiciones. Se mira la expresión
            /// más exterior de ese tipo (en OpenMode.ForWrite, el acceso entero; en c ? a : b, la condición entera). El
            /// valor de ForRead se toma del propio enumerado, no de un número.
            /// </summary>
            private void ModoDeApertura(ExpressionSyntax e, ITypeSymbol tipo)
            {
                if (!(tipo is INamedTypeSymbol nt) || NombreCompleto(nt) != CodigoReglas.TipoOpenMode) return;
                var simbolo = _m.GetSymbolInfo(e).Symbol;
                if (simbolo is ITypeSymbol || simbolo is IAliasSymbol || simbolo is INamespaceSymbol) return;   // el nombre del tipo, no un valor
                if (e.Parent is MemberAccessExpressionSyntax pm && pm.Name == e) return;                          // ForWrite dentro de OpenMode.ForWrite
                if (e.Parent is ExpressionSyntax padre && _m.GetTypeInfo(padre).Type is INamedTypeSymbol tp && NombreCompleto(tp) == CodigoReglas.TipoOpenMode
                    && !(_m.GetSymbolInfo(padre).Symbol is ITypeSymbol)) return;                                     // se mira la expresión exterior
                var lectura = nt.GetMembers(CodigoReglas.OpenModeLectura).OfType<IFieldSymbol>().FirstOrDefault();
                var valor = _m.GetConstantValue(e);
                if (lectura != null && lectura.HasConstantValue && valor.HasValue && Equals(valor.Value, lectura.ConstantValue)) return;
                Escritura(e, "OpenMode distinto de OpenMode.ForRead (" + Primeras(e) + ")");
            }

            /// <summary>
            /// Solo lectura (1.17.6): F.Agregar y F.EscribirTexto escriben; F.Abrir solo vale con 'escribir' ausente o la
            /// constante false (F.Abrir&lt;Line&gt;(id) o F.Abrir&lt;Line&gt;(id, false)), y llamándola, no como grupo de métodos.
            /// </summary>
            private void FachadaSoloLectura(ISymbol s, SyntaxNode n)
            {
                if (CodigoReglas.FachadaQueEscribe.Contains(s.Name))
                {
                    Escritura(n, "F." + s.Name);
                    return;
                }
                if (s.Name != CodigoReglas.FachadaAbrir || !(s is IMethodSymbol)) return;
                SyntaxNode expr = n.Parent is MemberAccessExpressionSyntax ma && ma.Name == n ? ma
                    : n.Parent is MemberBindingExpressionSyntax mb ? mb : n;
                if (!(expr.Parent is InvocationExpressionSyntax inv) || inv.Expression != expr)
                {
                    Escritura(n, "F.Abrir usado sin llamarlo (como grupo de métodos)");
                    return;
                }
                if (!(_m.GetOperation(inv) is IInvocationOperation op)) return;
                foreach (var a in op.Arguments)
                {
                    if (a.Parameter == null || a.Parameter.Name != CodigoReglas.ParametroEscribir) continue;
                    if (a.ArgumentKind == ArgumentKind.DefaultValue) return;
                    var cv = a.Value.ConstantValue;
                    if (cv.HasValue && Equals(cv.Value, false)) return;
                    Escritura(a.Syntax, "F.Abrir con '" + CodigoReglas.ParametroEscribir + "' distinto de false (" + Primeras(a.Syntax) + ")");
                    return;
                }
            }

            /// <summary>Nombres completos del tipo sobre el que se usa un miembro y de sus bases, y del que lo declara y las suyas.</summary>
            private static List<string> TiposConBases(INamedTypeSymbol receptor, INamedTypeSymbol declarante)
            {
                var l = new List<string>();
                for (var t = receptor; t != null; t = t.BaseType) l.Add(NombreCompleto(t));
                for (var t = declarante; t != null; t = t.BaseType) l.Add(NombreCompleto(t));
                return l;
            }

            private static string Primeras(SyntaxNode n)
            {
                string s = n.ToString();
                int i = s.IndexOfAny(new[] { '\n', '{' });
                if (i > 0) s = s.Substring(0, i);
                return s.Trim();
            }

            private void Using(UsingDirectiveSyntax u)
            {
                if (u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                {
                    Anotar(u, CodigoReglas.Sintaxis, "global using: no está permitido en un script.");
                    return;
                }
                if (u.NamespaceOrType == null) return;
                var info = _m.GetSymbolInfo(u.NamespaceOrType);
                var s = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                if (s is INamespaceSymbol ns)
                {
                    string espacio = ns.ToDisplayString();
                    string cat = EsDelPlugin(espacio) ? null : CodigoReglas.CategoriaEspacio(espacio);
                    if (cat != null) Bloqueo(u, cat, "using " + espacio);
                }
                else if (s is ITypeSymbol t) Tipo(t, u);
            }

            private bool EsDelPlugin(string espacio) => espacio == "ArbaMcp" || espacio.StartsWith("ArbaMcp.", StringComparison.Ordinal);

            // ------------------------------------------------------------------ símbolos
            private void Simbolo(ISymbol s, SyntaxNode n)
            {
                switch (s)
                {
                    case IAliasSymbol alias: Simbolo(alias.Target, n); return;
                    case ITypeSymbol t: Tipo(t, n); return;
                    case IEventSymbol ev:
                        Bloqueo(n, CodigoReglas.Eventos, Nombre(ev.ContainingType) + "." + ev.Name);
                        return;
                    case IMethodSymbol _:
                    case IPropertySymbol _:
                    case IFieldSymbol _:
                        Miembro(s, n);
                        return;
                }
            }

            private void Miembro(ISymbol s, SyntaxNode n)
            {
                var metodo = s as IMethodSymbol;
                var original = metodo?.ReducedFrom ?? metodo;   // método de extensión: su clase estática (DocumentExtension...)
                INamedTypeSymbol declarante = (original ?? s).ContainingType;
                if (declarante == null) return;
                if (EsDelCompilado(declarante)) return;
                string nombreTipo = NombreCompleto(declarante);
                if (nombreTipo == _op.TipoFachada)
                {
                    FachadaSoloLectura(s, n);
                    return;
                }
                if (nombreTipo == _op.TipoVigilante)
                {
                    if (s.Name != "Revisar") Anotar(n, CodigoReglas.Plugin, nombreTipo + "." + s.Name + ": del vigilante del tiempo límite solo se puede llamar a Revisar().");
                    return;
                }
                var receptor = TipoReceptor(n);

                // System.Type sigue sirviendo para typeof(...).Name aunque Name esté declarado en System.Reflection.MemberInfo
                string nombreReceptor = NombreCompleto(receptor);
                if (nombreReceptor != null && CodigoReglas.MiembrosPermitidosDe.TryGetValue(nombreReceptor, out var delReceptor))
                {
                    if (delReceptor.permitidos.Contains(s.Name)) return;
                    Bloqueo(n, delReceptor.categoria, Nombre(receptor) + "." + s.Name);
                    return;
                }
                if (!TipoPermitido(declarante, n)) return;   // el tipo ya se anotó

                if (metodo != null && metodo.MethodKind == MethodKind.BuiltinOperator) return;
                bool escritura = EsEscritura(n);
                // Tipos en los que se buscan las reglas: el receptor y sus bases hasta el declarante (F.Db.Dispose() es
                // Database.Dispose aunque Dispose esté en DisposableWrapper), el declarante y los tipos de los miembros que
                // sobrescribe. No las bases del declarante: Civ.Alignment.Create no es RXObject.Create.
                var relacionados = new List<string>();
                if (receptor != null && metodo?.ReducedFrom == null)
                {
                    for (var t = receptor; t != null; t = t.BaseType)
                    {
                        relacionados.Add(NombreCompleto(t));
                        if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, declarante.OriginalDefinition)) break;
                    }
                }
                else if (receptor != null) relacionados.Add(NombreCompleto(receptor));   // método de extensión: d.CloseAndDiscard() es de Document
                relacionados.Add(nombreTipo);
                for (var sobre = Sobrescrito(s); sobre != null; sobre = Sobrescrito(sobre))
                    if (sobre.ContainingType != null) relacionados.Add(NombreCompleto(sobre.ContainingType));

                string nombreMiembro = s is IMethodSymbol m2 && m2.MethodKind == MethodKind.Constructor ? ".ctor" : s.Name;
                string cat = CodigoReglas.CategoriaMiembro(nombreTipo, relacionados, nombreMiembro, escritura);
                if (cat != null)
                {
                    Bloqueo(n, cat, Nombre(declarante) + "." + s.Name + (escritura ? " (asignación)" : ""));
                    return;
                }

                // Solo lectura (1.17.6): lo que escribe en el dibujo (UpgradeOpen, Erase, Add..., Create..., asignaciones...);
                // desde la 1.19.1, también todo método de los objetos del dibujo que no sea de lectura (Curve.Extend)
                if (CodigoReglas.CategoriaSoloLectura(TiposConBases(receptor, declarante), nombreMiembro, escritura, metodo != null, nombreTipo) != null)
                    Escritura(n, Nombre(declarante) + "." + s.Name + (escritura ? " (asignación)" : ""));

                // Punteros nativos devueltos o guardados (UnmanagedObject, campos IntPtr): solo IntPtr.Zero
                bool esIntPtrPropio = nombreTipo == "System.IntPtr" || nombreTipo == "System.UIntPtr";
                ITypeSymbol devuelto = s is IMethodSymbol mm ? mm.ReturnType : s is IPropertySymbol pp ? pp.Type : s is IFieldSymbol ff ? ff.Type : null;
                if (!esIntPtrPropio && EsPuntero(devuelto))
                {
                    Bloqueo(n, CodigoReglas.Nativo, Nombre(declarante) + "." + s.Name + " (devuelve un puntero nativo)");
                    return;
                }

                // La API de Autodesk con parámetros de archivo o ruta: SaveAs(fileName), AddPointFile(path), AttachXref...
                if (nombreTipo.StartsWith("Autodesk", StringComparison.Ordinal))
                {
                    // Asignar una ruta a una propiedad (RasterImageDef.SourceFileName, BlockTableRecord.PathName) hace que
                    // Civil 3D lea un archivo de cualquier carpeta; leerla (Database.Filename) no
                    if (escritura && s is IPropertySymbol propiedad && propiedad.Type.SpecialType == SpecialType.System_String && CodigoReglas.EsParametroDeArchivo(propiedad.Name))
                    {
                        Anotar(n, CodigoReglas.Documentos, Nombre(declarante) + "." + s.Name + " (asignación) es una ruta de archivo: " + CodigoReglas.Explicacion(CodigoReglas.Documentos));
                        return;
                    }
                    var parametros = (original as IMethodSymbol)?.Parameters ?? (s as IPropertySymbol)?.Parameters ?? ImmutableArrayVacio;
                    var deArchivo = parametros.FirstOrDefault(p => p.Type.SpecialType == SpecialType.System_String && CodigoReglas.EsParametroDeArchivo(p.Name));
                    if (deArchivo != null)
                        Anotar(n, CodigoReglas.Documentos, Nombre(declarante) + "." + s.Name + " trabaja con archivos o rutas (parámetro '" + deArchivo.Name + "'): " + CodigoReglas.Explicacion(CodigoReglas.Documentos));
                }
            }

            private static readonly System.Collections.Immutable.ImmutableArray<IParameterSymbol> ImmutableArrayVacio = System.Collections.Immutable.ImmutableArray<IParameterSymbol>.Empty;

            private void Argumentos(SyntaxNode n)
            {
                IEnumerable<IArgumentOperation> args = null;
                var op = _m.GetOperation(n);
                if (op is IInvocationOperation inv) args = inv.Arguments;
                else if (op is IObjectCreationOperation oc) args = oc.Arguments;
                if (args == null) return;
                foreach (var a in args)
                {
                    if (a.Parameter == null || !EsPuntero(a.Parameter.Type)) continue;
                    if (a.ArgumentKind == ArgumentKind.DefaultValue || EsCero(a.Value)) continue;
                    Anotar(a.Syntax, CodigoReglas.Nativo, "argumento '" + a.Parameter.Name + "' de tipo puntero nativo: solo se admite IntPtr.Zero. " + CodigoReglas.Explicacion(CodigoReglas.Nativo));
                }
            }

            private static bool EsCero(IOperation v)
            {
                while (v is IConversionOperation c) v = c.Operand;
                if (v is IFieldReferenceOperation f) return f.Field.Name == "Zero" && (NombreCompleto(f.Field.ContainingType) == "System.IntPtr" || NombreCompleto(f.Field.ContainingType) == "System.UIntPtr");
                if (v is IDefaultValueOperation) return true;
                if (v is ILiteralOperation l && l.ConstantValue.HasValue) return Equals(l.ConstantValue.Value, 0) || Equals(l.ConstantValue.Value, 0L);
                return false;
            }

            private static bool EsPuntero(ITypeSymbol t)
            {
                if (t == null) return false;
                if (t is IPointerTypeSymbol || t is IFunctionPointerTypeSymbol) return true;
                return t.SpecialType == SpecialType.System_IntPtr || t.SpecialType == SpecialType.System_UIntPtr;
            }

            /// <summary>True si el nombre es el destino de una asignación (o de ++/--): F.Doc... = x, new X { P = 1 }.</summary>
            private static bool EsEscritura(SyntaxNode n)
            {
                SyntaxNode expr = n;
                if (n.Parent is MemberAccessExpressionSyntax ma && ma.Name == n) expr = ma;
                else if (n.Parent is MemberBindingExpressionSyntax mb) expr = mb;
                // Auditoría de la ronda C (I5): (l.Layer, _) = ("0", 0) asigna cada elemento de la tupla de la izquierda,
                // también en tuplas anidadas (el foreach con destinos que no son variables nuevas no compila: CS8186).
                // Segunda revisión (N1): los paréntesis tampoco cambian el destino: (l.Layer) = "0", (l.ColorIndex)++,
                // ((l.Layer), _) = ..., en cualquier anidación y combinados con tuplas
                while (true)
                {
                    if (expr.Parent is ParenthesizedExpressionSyntax parentesis) { expr = parentesis; continue; }
                    if (expr.Parent is ArgumentSyntax arg && arg.Parent is TupleExpressionSyntax tupla) { expr = tupla; continue; }
                    break;
                }
                var p = expr.Parent;
                if (p is AssignmentExpressionSyntax a && a.Left == expr) return true;
                if (p is ForEachVariableStatementSyntax fe && fe.Variable == expr) return true;
                if (p is PrefixUnaryExpressionSyntax pre && (pre.IsKind(SyntaxKind.PreIncrementExpression) || pre.IsKind(SyntaxKind.PreDecrementExpression))) return true;
                if (p is PostfixUnaryExpressionSyntax post && (post.IsKind(SyntaxKind.PostIncrementExpression) || post.IsKind(SyntaxKind.PostDecrementExpression))) return true;
                return false;
            }

            /// <summary>Tipo de la expresión a la izquierda del punto (x en x.Miembro o x?.Miembro), o null.</summary>
            private INamedTypeSymbol TipoReceptor(SyntaxNode n)
            {
                ExpressionSyntax izquierda = null;
                if (n.Parent is MemberAccessExpressionSyntax ma && ma.Name == n) izquierda = ma.Expression;
                else if (n.Parent is MemberBindingExpressionSyntax mb)
                    izquierda = mb.Ancestors().OfType<ConditionalAccessExpressionSyntax>().FirstOrDefault()?.Expression;
                if (izquierda == null) return null;
                return _m.GetTypeInfo(izquierda).Type as INamedTypeSymbol;
            }

            private static ISymbol Sobrescrito(ISymbol s)
                => s is IMethodSymbol m ? m.OverriddenMethod : s is IPropertySymbol p ? p.OverriddenProperty : s is IEventSymbol e ? (ISymbol)e.OverriddenEvent : null;

            // ------------------------------------------------------------------ tipos
            private void Tipo(ITypeSymbol t, SyntaxNode n) => TipoPermitido(t, n);

            /// <summary>Comprueba un tipo (y sus argumentos genéricos y elementos); anota el problema y devuelve false si está bloqueado.</summary>
            private bool TipoPermitido(ITypeSymbol t, SyntaxNode n, int profundidad = 0)
            {
                if (t == null || t.TypeKind == TypeKind.Error || profundidad > 8) return true;
                switch (t)
                {
                    case IArrayTypeSymbol a: return TipoPermitido(a.ElementType, n, profundidad + 1);
                    case IPointerTypeSymbol _:
                    case IFunctionPointerTypeSymbol _:
                        Bloqueo(n, CodigoReglas.Nativo, t.ToDisplayString());
                        return false;
                    case ITypeParameterSymbol _: return true;
                    case IDynamicTypeSymbol _:
                        Anotar(n, CodigoReglas.Reflexion, "dynamic: " + CodigoReglas.Explicacion(CodigoReglas.Reflexion));
                        return false;
                }
                if (!(t is INamedTypeSymbol nt)) return true;
                if (nt.IsAnonymousType)
                {
                    bool ok = true;
                    foreach (var p in nt.GetMembers().OfType<IPropertySymbol>()) ok &= TipoPermitido(p.Type, n, profundidad + 1);
                    return ok;
                }
                if (nt.IsTupleType)
                {
                    bool ok = true;
                    foreach (var e in nt.TupleElements) ok &= TipoPermitido(e.Type, n, profundidad + 1);
                    return ok;
                }
                if (EsDelCompilado(nt)) return true;

                bool permitido = true;
                foreach (var ta in nt.TypeArguments) permitido &= TipoPermitido(ta, n, profundidad + 1);

                var exterior = nt;
                while (exterior.ContainingType != null) exterior = exterior.ContainingType;
                string espacio = exterior.ContainingNamespace == null || exterior.ContainingNamespace.IsGlobalNamespace ? "" : exterior.ContainingNamespace.ToDisplayString();
                string completo = NombreCompleto(exterior);
                if (completo == _op.TipoFachada || completo == _op.TipoVigilante) return permitido;
                if (EsDelPlugin(espacio))
                {
                    Bloqueo(n, CodigoReglas.Plugin, Nombre(nt));
                    return false;
                }
                // Un tipo anidado (Environment.SpecialFolder) se decide por el tipo exterior que lo contiene
                string cat = CodigoReglas.CategoriaTipo(espacio, exterior.Name);
                if (cat != null)
                {
                    Bloqueo(n, cat, Nombre(nt));
                    return false;
                }
                return permitido;
            }

            private bool EsDelCompilado(INamedTypeSymbol t)
                => t.ContainingAssembly != null && SymbolEqualityComparer.Default.Equals(t.ContainingAssembly, _m.Compilation.Assembly);
        }

        /// <summary>Nombre completo sin genéricos ni global:: (System.Collections.Generic.List, Autodesk.AutoCAD.DatabaseServices.Database).</summary>
        public static string NombreCompleto(INamedTypeSymbol t)
        {
            if (t == null) return null;
            string nombre = t.Name;
            for (var c = t.ContainingType; c != null; c = c.ContainingType) nombre = c.Name + "." + nombre;
            var ns = t.ContainingNamespace;
            return ns == null || ns.IsGlobalNamespace ? nombre : ns.ToDisplayString() + "." + nombre;
        }

        private static string Nombre(ITypeSymbol t) => t?.ToDisplayString() ?? "?";
    }
}
