using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace ArbaMcp.Nucleo
{
    /// <summary>Qué ve el script al compilarse: el tipo de la Fachada, el vigilante del tiempo límite y los using por defecto.</summary>
    public sealed class OpcionesCompilacion
    {
        /// <summary>
        /// Using por defecto. Los tipos de Civil 3D van con el alias Civ (Civ.Alignment, Civ.Surface): con un using de
        /// Autodesk.Civil.DatabaseServices, Entity, DBObject o Surface serían ambiguos con los de AutoCAD.
        /// </summary>
        public static readonly string[] UsingsPorDefecto =
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "System.Text",
            "System.Globalization",
            "Autodesk.AutoCAD.ApplicationServices",
            "Autodesk.AutoCAD.DatabaseServices",
            "Autodesk.AutoCAD.Geometry",
            "Civ = Autodesk.Civil.DatabaseServices",
            "AcApp = Autodesk.AutoCAD.ApplicationServices.Application",
        };

        public string TipoFachada = "ArbaMcp.Fachada";
        public string TipoVigilante = "ArbaMcp.Nucleo.CodigoVigilante";
        public IList<string> Usings = UsingsPorDefecto.ToList();

        internal string Firma => TipoFachada + "|" + TipoVigilante + "|" + string.Join(";", Usings);
    }

    /// <summary>Resultado de compilar un script: el ensamblado y su PDB en memoria, o el mensaje de error para el agente.</summary>
    public sealed class ResultadoCompilacion
    {
        public const string Tipo = "ArbaMcpScript.Script";
        public const string Metodo = "Ejecutar";

        public bool Ok;
        public byte[] Ensamblado;
        public byte[] Pdb;
        /// <summary>Mensaje completo para el agente si no compila (null si compila).</summary>
        public string Mensaje;
        /// <summary>Avisos del compilador sobre el código del agente (miembros obsoletos: CS0618, CS0612).</summary>
        public List<string> Avisos = new List<string>();
        public long Ms;
        public bool DeCache;
        /// <summary>Líneas del código del agente.</summary>
        public int Lineas;
        public string NombreEnsamblado;
        /// <summary>Problemas de la lista blanca (para las pruebas y el historial).</summary>
        public List<ProblemaCodigo> Problemas = new List<ProblemaCodigo>();
        /// <summary>Errores de compilación ya formateados (para las pruebas).</summary>
        public List<string> Errores = new List<string>();

        internal ResultadoCompilacion Copia(bool deCache) => new ResultadoCompilacion
        {
            Ok = Ok, Ensamblado = Ensamblado, Pdb = Pdb, Mensaje = Mensaje, Avisos = Avisos.ToList(), Ms = Ms, DeCache = deCache,
            Lineas = Lineas, NombreEnsamblado = NombreEnsamblado, Problemas = Problemas.ToList(), Errores = Errores.ToList()
        };
    }

    /// <summary>
    /// Compilación de los scripts de ejecutar_codigo (desde 1.16.0) con Roslyn, sin Civil 3D:
    ///  1. Separa los using del principio del código del agente y envuelve el resto como cuerpo del método
    ///     ArbaMcpScript.Script.Ejecutar(Fachada F, JsonElement Parametros), con directivas #line para que los errores
    ///     y las trazas den la línea del código del agente.
    ///  2. Comprueba que el código queda dentro del método (sin llaves que lo cierren antes ni clases nuevas).
    ///  3. Compila, revisa la lista blanca (CodigoRevisor) y, si hay errores o usos no permitidos, devuelve un mensaje
    ///     claro con la línea, la columna, el texto de la línea y una pista.
    ///  4. Inserta CodigoVigilante.Revisar() al principio del script y de cada bucle, función local y lambda (tiempo
    ///     límite cooperativo y control de la recursión) sin cambiar los números de línea, y emite el ensamblado y su PDB.
    /// Guarda las últimas compilaciones por el texto del código: un script repetido (las recetas) no se recompila.
    /// </summary>
    public static class CodigoCompilador
    {
        public const string RutaScript = CodigoCompiladorNombres.RutaScript;
        public const int MaximoErrores = 10;
        private const int TamanoCache = 32;
        private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es");
        private static readonly object CerrojoCache = new object();
        private static readonly Dictionary<string, ResultadoCompilacion> Cache = new Dictionary<string, ResultadoCompilacion>(StringComparer.Ordinal);
        private static readonly LinkedList<string> OrdenCache = new LinkedList<string>();

        public const string Pie = "El código es el cuerpo de un método: F es la Fachada (F.Doc, F.Db, F.Tr, F.Civil, F.Alineamiento(nombre), F.Agregar(entidad), F.Escribir(texto)...), Parametros es el JSON de solo lectura de 'parametros', y lo que devuelvas con return sale en 'resultado'.";

        public static ResultadoCompilacion Compilar(string codigo, IReadOnlyList<MetadataReference> referencias, OpcionesCompilacion op = null)
        {
            op = op ?? new OpcionesCompilacion();
            codigo = codigo ?? "";
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            string clave = Clave(codigo, referencias, op);
            lock (CerrojoCache)
            {
                if (Cache.TryGetValue(clave, out var guardado))
                {
                    OrdenCache.Remove(clave);
                    OrdenCache.AddFirst(clave);
                    var copia = guardado.Copia(true);
                    copia.Ms = reloj.ElapsedMilliseconds;
                    return copia;
                }
            }

            var r = CompilarSinCache(codigo, referencias, op, clave);
            r.Ms = reloj.ElapsedMilliseconds;
            if (r.Ok)
            {
                lock (CerrojoCache)
                {
                    Cache[clave] = r.Copia(false);
                    OrdenCache.AddFirst(clave);
                    while (OrdenCache.Count > TamanoCache)
                    {
                        Cache.Remove(OrdenCache.Last.Value);
                        OrdenCache.RemoveLast();
                    }
                }
            }
            return r;
        }

        private static ResultadoCompilacion CompilarSinCache(string codigo, IReadOnlyList<MetadataReference> referencias, OpcionesCompilacion op, string clave)
        {
            var r = new ResultadoCompilacion { NombreEnsamblado = "ArbaMcpScript_" + clave.Substring(0, 16) };
            string[] lineas = codigo.Replace("\r\n", "\n").Split('\n');
            r.Lineas = lineas.Length;
            if (string.IsNullOrWhiteSpace(codigo))
            {
                r.Mensaje = "ejecutar_codigo: falta el código ('codigo_csharp' está vacío). No se ejecutó nada. " + Pie;
                return r;
            }

            var (cuerpo, usings) = SepararUsings(codigo);
            string fuente = Envolver(cuerpo, usings, op, out TextSpan zonaCuerpo, out List<TextSpan> zonasUsings);
            var zonas = new List<TextSpan>(zonasUsings) { zonaCuerpo };

            var opcionesParseo = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None, SourceCodeKind.Regular);
            var arbol = CSharpSyntaxTree.ParseText(SourceText.From(fuente, Encoding.UTF8), opcionesParseo, path: "");
            var opcionesCompilacion = new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug,
                    allowUnsafe: false,
                    nullableContextOptions: NullableContextOptions.Disable,
                    concurrentBuild: false,
                    deterministic: true)
                .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
                {
                    ["CS0162"] = ReportDiagnostic.Suppress,   // código inalcanzable: el 'return null;' que el plugin añade al final
                    ["CS1701"] = ReportDiagnostic.Suppress,
                    ["CS1702"] = ReportDiagnostic.Suppress,
                });
            var compilacion = CSharpCompilation.Create(r.NombreEnsamblado, new[] { arbol }, referencias, opcionesCompilacion);
            var modelo = compilacion.GetSemanticModel(arbol);

            string estructura = ComprobarEstructura(arbol, zonaCuerpo);
            var diagnosticos = compilacion.GetDiagnostics();
            var errores = diagnosticos.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            r.Problemas = CodigoRevisor.Revisar(modelo, zonas, new OpcionesRevision { TipoFachada = op.TipoFachada, TipoVigilante = op.TipoVigilante });

            foreach (var d in errores.OrderBy(d => d.Location.SourceSpan.Start))
                r.Errores.Add(Formatear(d, zonas, lineas));
            foreach (var d in diagnosticos.Where(d => d.Severity == DiagnosticSeverity.Warning && (d.Id == "CS0618" || d.Id == "CS0612")))
                if (EnZonas(d.Location, zonas)) r.Avisos.Add(Formatear(d, zonas, lineas, conTexto: false).TrimStart('-', ' '));

            if (estructura != null || errores.Count > 0 || r.Problemas.Count > 0)
            {
                r.Mensaje = Mensaje(r.Problemas, estructura, r.Errores);
                return r;
            }

            // Tiempo límite cooperativo: revisión al principio de cada bucle, función local y lambda del código del agente
            var instrumentado = new Instrumentador(modelo, zonaCuerpo, op.TipoVigilante).Visit(arbol.GetRoot());
            var arbol2 = CSharpSyntaxTree.Create((CSharpSyntaxNode)instrumentado, opcionesParseo, path: "", encoding: Encoding.UTF8);
            var compilacion2 = compilacion.ReplaceSyntaxTree(arbol, arbol2);
            using (var pe = new MemoryStream())
            using (var pdb = new MemoryStream())
            {
                var emision = compilacion2.Emit(pe, pdb, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
                if (!emision.Success)
                {
                    var internos = emision.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(MaximoErrores)
                        .Select(d => d.Id + " " + d.GetMessage(Espanol));
                    r.Mensaje = "ejecutar_codigo: error interno al preparar el código (después de añadir las revisiones del tiempo límite). No se ejecutó nada. Avisa al desarrollador con este texto: "
                        + string.Join(" | ", internos);
                    return r;
                }
                r.Ensamblado = pe.ToArray();
                r.Pdb = pdb.ToArray();
            }
            r.Ok = true;
            return r;
        }

        // ------------------------------------------------------------------ envoltorio
        /// <summary>
        /// Separa los using del principio del código (C# exige que vayan antes del espacio de nombres). Devuelve el cuerpo
        /// con esos using sustituidos por espacios (mismas líneas y columnas) y cada using con su número de línea (base 0).
        /// </summary>
        internal static (string cuerpo, List<(string texto, int linea)> usings) SepararUsings(string codigo)
        {
            var usings = new List<(string, int)>();
            var arbol = CSharpSyntaxTree.ParseText(codigo, new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None, SourceCodeKind.Script));
            var cu = (CompilationUnitSyntax)arbol.GetRoot();
            if (cu.Usings.Count == 0) return (codigo, usings);
            var chars = codigo.ToCharArray();
            foreach (var u in cu.Usings)
            {
                var span = u.Span;
                usings.Add((codigo.Substring(span.Start, span.Length), arbol.GetLineSpan(span).StartLinePosition.Line));
                for (int i = span.Start; i < span.End; i++)
                    if (chars[i] != '\n' && chars[i] != '\r') chars[i] = ' ';
            }
            return (new string(chars), usings);
        }

        internal static string Envolver(string cuerpo, List<(string texto, int linea)> usings, OpcionesCompilacion op, out TextSpan zonaCuerpo, out List<TextSpan> zonasUsings)
        {
            var sb = new StringBuilder();
            zonasUsings = new List<TextSpan>();
            foreach (var (texto, linea) in usings)
            {
                sb.Append("#line ").Append(linea + 1).Append(" \"").Append(RutaScript).Append("\"\n");
                int inicio = sb.Length;
                sb.Append(texto);
                zonasUsings.Add(new TextSpan(inicio, texto.Length));
                sb.Append('\n');
            }
            sb.Append("#line hidden\n");
            foreach (var u in op.Usings) sb.Append("using ").Append(u).Append(";\n");
            sb.Append("namespace ArbaMcpScript\n{\n    public static class Script\n    {\n");
            sb.Append("        public static object ").Append(ResultadoCompilacion.Metodo).Append("(global::").Append(op.TipoFachada)
              .Append(" F, global::System.Text.Json.JsonElement Parametros)\n        {\n");
            sb.Append("            global::").Append(op.TipoVigilante).Append(".Revisar();\n");
            sb.Append("#line 1 \"").Append(RutaScript).Append("\"\n");
            int inicioCuerpo = sb.Length;
            sb.Append(cuerpo);
            zonaCuerpo = new TextSpan(inicioCuerpo, cuerpo.Length);
            sb.Append("\n#line hidden\n            return null;\n        }\n    }\n}\n");
            return sb.ToString();
        }

        /// <summary>
        /// null si el código del agente queda entero dentro del método Ejecutar; si no (llaves que cierran el método antes
        /// de tiempo, clases o métodos nuevos), el texto del error. Es lo que impide declarar tipos fuera del método.
        /// </summary>
        internal static string ComprobarEstructura(SyntaxTree arbol, TextSpan zonaCuerpo)
        {
            const string error = "El código no queda dentro del método: revisa las llaves { } (cada { necesita su }) y no declares clases, métodos ni espacios de nombres; usa variables y funciones locales.";
            var cu = (CompilationUnitSyntax)arbol.GetRoot();
            if (cu.Members.Count != 1 || !(cu.Members[0] is NamespaceDeclarationSyntax ns) || ns.CloseBraceToken.IsMissing) return error;
            if (ns.Members.Count != 1 || !(ns.Members[0] is ClassDeclarationSyntax clase) || clase.Identifier.Text != "Script" || clase.CloseBraceToken.IsMissing) return error;
            if (clase.Members.Count != 1 || !(clase.Members[0] is MethodDeclarationSyntax metodo) || metodo.Body == null) return error;
            var cuerpo = metodo.Body;
            if (cuerpo.CloseBraceToken.IsMissing || cuerpo.OpenBraceToken.Span.End > zonaCuerpo.Start || cuerpo.CloseBraceToken.SpanStart < zonaCuerpo.End) return error;
            return null;
        }

        // ------------------------------------------------------------------ mensajes
        private static bool EnZonas(Location lugar, IEnumerable<TextSpan> zonas)
            => lugar.IsInSource && zonas.Any(z => z.Contains(lugar.SourceSpan.Start) || lugar.SourceSpan.Start == z.End);

        private static string Formatear(Diagnostic d, IList<TextSpan> zonas, string[] lineas, bool conTexto = true)
        {
            string mensaje = d.GetMessage(Espanol).TrimEnd();
            string pista = Pista(d.Id);
            if (pista.Length > 0 && mensaje.Length > 0 && char.IsLetterOrDigit(mensaje[mensaje.Length - 1])) mensaje += ".";   // "no existe en el contexto actual." (no "Se esperaba ;.")
            string texto = d.Id + " " + mensaje + pista;
            if (!EnZonas(d.Location, zonas))
                return "- (al final del código o en el envoltorio que añade el plugin) " + texto;
            var span = d.Location.GetMappedLineSpan();
            int linea = span.StartLinePosition.Line + 1, columna = span.StartLinePosition.Character + 1;
            var sb = new StringBuilder("- Línea ").Append(linea).Append(", columna ").Append(columna).Append(": ").Append(texto);
            if (conTexto && linea >= 1 && linea <= lineas.Length)
            {
                string l = lineas[linea - 1].TrimEnd('\r').Replace('\t', ' ');
                if (l.Length > 200) l = l.Substring(0, 200) + "…";
                sb.Append("\n    ").Append(l).Append("\n    ").Append(new string(' ', Math.Max(0, Math.Min(columna - 1, 200)))).Append('^');
            }
            return sb.ToString();
        }

        private static string Pista(string id)
        {
            switch (id)
            {
                case "CS0246":
                case "CS0103":
                case "CS0234":
                    return " Pista: comprueba el nombre. Los using por defecto son System, System.Collections.Generic, System.Linq, System.Text, System.Globalization y los de AutoCAD (ApplicationServices, DatabaseServices, Geometry); los tipos de Civil 3D van con el alias Civ (Civ.Alignment) y la Fachada es F.";
                case "CS0104": return " Pista: nombre ambiguo; escribe el espacio de nombres completo o, para Civil 3D, el alias Civ (Civ.Surface).";
                case "CS0126": return " Pista: devuelve un valor (return algo;) o quita el return.";
                case "CS1002":
                case "CS1003":
                case "CS1026":
                case "CS1513":
                case "CS1514":
                case "CS1525":
                    return " Pista: error de sintaxis; revisa los punto y coma, los paréntesis y las llaves.";
                case "CS0012": return " Pista: ese miembro necesita una DLL que el plugin no pasa al compilador; usa otro camino o avisa al desarrollador.";
                case "CS0122": return " Pista: ese miembro no es público en la API.";
                case "CS0128":
                case "CS0136":
                    return " Pista: F y Parametros ya existen en el script; usa otro nombre para tu variable.";
                case "CS0227": return " Pista: ejecutar_codigo no admite código unsafe.";
                case "CS0656": return " Pista: dynamic no está disponible en ejecutar_codigo.";
                default: return "";
            }
        }

        private static string Mensaje(List<ProblemaCodigo> problemas, string estructura, List<string> errores)
        {
            var partes = new List<string>();
            if (problemas.Count > 0) partes.Add(problemas.Count + (problemas.Count == 1 ? " uso no permitido" : " usos no permitidos"));
            if (estructura != null || errores.Count > 0) partes.Add((errores.Count + (estructura != null ? 1 : 0)) + " error(es) de compilación");
            var sb = new StringBuilder("ejecutar_codigo: el código no se puede ejecutar (" + string.Join(" y ", partes)
                + "). No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.");
            if (problemas.Count > 0)
            {
                sb.Append("\nNo permitido:");
                foreach (var p in problemas) sb.Append("\n- ").Append(p);
            }
            if (estructura != null || errores.Count > 0)
            {
                sb.Append("\nErrores de compilación:");
                if (estructura != null) sb.Append("\n- ").Append(estructura);
                foreach (var e in errores.Take(MaximoErrores)) sb.Append('\n').Append(e);
                if (errores.Count > MaximoErrores) sb.Append("\n- ... y ").Append(errores.Count - MaximoErrores).Append(" errores más.");
            }
            sb.Append('\n').Append(Pie);
            return sb.ToString();
        }

        private static string Clave(string codigo, IReadOnlyList<MetadataReference> referencias, OpcionesCompilacion op)
        {
            var sb = new StringBuilder(codigo).Append("\n\0").Append(op.Firma).Append('\0');
            foreach (var r in referencias) sb.Append(r.Display).Append('|');
            using (var sha = SHA256.Create())
                return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        /// <summary>Vacía la caché de compilaciones (para las pruebas).</summary>
        internal static void VaciarCache()
        {
            lock (CerrojoCache) { Cache.Clear(); OrdenCache.Clear(); }
        }

        // ------------------------------------------------------------------ revisiones del tiempo límite
        /// <summary>
        /// Inserta 'global::...CodigoVigilante.Revisar();' al principio del cuerpo de cada bucle (while, do, for, foreach),
        /// función local y lambda del código del agente. Las lambdas y funciones de expresión (x => x + 1) pasan a bloque
        /// ({ Revisar(); return x + 1; }) según devuelvan valor o no, que dice el modelo semántico. No añade saltos de
        /// línea: las líneas de los errores y de las trazas siguen siendo las del agente.
        /// </summary>
        private sealed class Instrumentador : CSharpSyntaxRewriter
        {
            private readonly SemanticModel _m;
            private readonly TextSpan _zona;
            private readonly string _llamada;

            public Instrumentador(SemanticModel m, TextSpan zona, string tipoVigilante)
            {
                _m = m;
                _zona = zona;
                _llamada = "global::" + tipoVigilante + ".Revisar(); ";
            }

            private bool EnZona(SyntaxNode n) => _zona.Contains(n.Span);
            private StatementSyntax Llamada() => SyntaxFactory.ParseStatement(_llamada);
            private static SyntaxToken Abre() => SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(SyntaxFactory.Space);
            private static SyntaxToken Cierra() => SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia(SyntaxFactory.Space);

            private StatementSyntax ConRevision(StatementSyntax cuerpo)
            {
                if (cuerpo is BlockSyntax b) return b.WithStatements(b.Statements.Insert(0, Llamada()));
                return SyntaxFactory.Block(Abre(), SyntaxFactory.List(new[] { Llamada(), cuerpo }), Cierra());
            }

            private BlockSyntax BloqueDeExpresion(ExpressionSyntax expr, bool devuelveVoid)
            {
                StatementSyntax s;
                if (expr is ThrowExpressionSyntax te)
                    s = SyntaxFactory.ThrowStatement(te.ThrowKeyword, te.Expression, SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                else if (devuelveVoid)
                    s = SyntaxFactory.ExpressionStatement(expr, SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                else
                    s = SyntaxFactory.ReturnStatement(SyntaxFactory.Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(SyntaxFactory.Space), expr, SyntaxFactory.Token(SyntaxKind.SemicolonToken));
                return SyntaxFactory.Block(Abre(), SyntaxFactory.List(new[] { Llamada(), s }), Cierra());
            }

            public override SyntaxNode VisitWhileStatement(WhileStatementSyntax node)
            {
                var n = (WhileStatementSyntax)base.VisitWhileStatement(node);
                return EnZona(node) ? n.WithStatement(ConRevision(n.Statement)) : n;
            }

            public override SyntaxNode VisitDoStatement(DoStatementSyntax node)
            {
                var n = (DoStatementSyntax)base.VisitDoStatement(node);
                return EnZona(node) ? n.WithStatement(ConRevision(n.Statement)) : n;
            }

            public override SyntaxNode VisitForStatement(ForStatementSyntax node)
            {
                var n = (ForStatementSyntax)base.VisitForStatement(node);
                return EnZona(node) ? n.WithStatement(ConRevision(n.Statement)) : n;
            }

            public override SyntaxNode VisitForEachStatement(ForEachStatementSyntax node)
            {
                var n = (ForEachStatementSyntax)base.VisitForEachStatement(node);
                return EnZona(node) ? n.WithStatement(ConRevision(n.Statement)) : n;
            }

            public override SyntaxNode VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
            {
                var n = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node);
                return EnZona(node) ? n.WithStatement(ConRevision(n.Statement)) : n;
            }

            public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
            {
                var simbolo = _m.GetDeclaredSymbol(node) as IMethodSymbol;
                var n = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node);
                if (!EnZona(node) || simbolo == null || simbolo.ReturnsByRef || simbolo.ReturnsByRefReadonly) return n;
                if (n.Body != null) return n.WithBody((BlockSyntax)ConRevision(n.Body));
                if (n.ExpressionBody == null) return n;
                var bloque = BloqueDeExpresion(n.ExpressionBody.Expression, simbolo.ReturnsVoid);
                bloque = bloque.WithCloseBraceToken(bloque.CloseBraceToken.WithTrailingTrivia(n.SemicolonToken.TrailingTrivia));
                return n.WithExpressionBody(null).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None)).WithBody(bloque);
            }

            public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
            {
                var simbolo = _m.GetSymbolInfo(node).Symbol as IMethodSymbol;
                var n = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node);
                if (!Instrumentable(node, simbolo)) return n;
                if (n.Block != null) return n.WithBlock((BlockSyntax)ConRevision(n.Block));
                return n.ExpressionBody == null ? n : n.WithExpressionBody(null).WithBlock(BloqueDeExpresion(n.ExpressionBody, simbolo.ReturnsVoid));
            }

            public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
            {
                var simbolo = _m.GetSymbolInfo(node).Symbol as IMethodSymbol;
                var n = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node);
                if (!Instrumentable(node, simbolo)) return n;
                if (n.Block != null) return n.WithBlock((BlockSyntax)ConRevision(n.Block));
                return n.ExpressionBody == null ? n : n.WithExpressionBody(null).WithBlock(BloqueDeExpresion(n.ExpressionBody, simbolo.ReturnsVoid));
            }

            public override SyntaxNode VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
            {
                var simbolo = _m.GetSymbolInfo(node).Symbol as IMethodSymbol;
                var n = (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node);
                if (!Instrumentable(node, simbolo) || n.Block == null) return n;
                return n.WithBlock((BlockSyntax)ConRevision(n.Block));
            }

            private bool Instrumentable(SyntaxNode node, IMethodSymbol simbolo)
                => EnZona(node) && simbolo != null && !simbolo.IsAsync && !simbolo.ReturnsByRef && !simbolo.ReturnsByRefReadonly;
        }
    }

    /// <summary>
    /// Puerta de entrada al compilador desde otro contexto de carga (AssemblyLoadContext). Dentro de Civil 3D, Roslyn y
    /// una segunda copia de ArbaMcp.Nucleo se cargan en un contexto propio (CargadorCompilador) para no chocar con otras
    /// versiones de Roslyn que pueda haber en el proceso; entre los dos contextos solo pasan tipos básicos (texto, bytes).
    /// El orden del arreglo lo lee CargadorCompilador (ResultadoAislado).
    /// </summary>
    public static class CodigoPuerta
    {
        private static readonly ConcurrentDictionary<string, MetadataReference> Referencias = new ConcurrentDictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Compila el script contra las DLL de 'rutas'. Devuelve { ok, ensamblado, pdb, mensaje, avisos[], ms, deCache,
        /// lineas, tipo, metodo, nombreEnsamblado, referenciasFallidas[] }.
        /// </summary>
        public static object[] Compilar(string codigo, string[] rutas, string tipoFachada)
        {
            var refs = new List<MetadataReference>();
            var fallidas = new List<string>();
            foreach (var ruta in rutas ?? new string[0])
            {
                try { refs.Add(Referencias.GetOrAdd(ruta, p => MetadataReference.CreateFromFile(p))); }
                catch (Exception ex) { fallidas.Add(Path.GetFileName(ruta) + ": " + ex.Message); }
            }
            var op = new OpcionesCompilacion();
            if (!string.IsNullOrWhiteSpace(tipoFachada)) op.TipoFachada = tipoFachada;
            var r = CodigoCompilador.Compilar(codigo, refs, op);
            return new object[]
            {
                r.Ok, r.Ensamblado, r.Pdb, r.Mensaje, r.Avisos.ToArray(), r.Ms, r.DeCache, r.Lineas,
                ResultadoCompilacion.Tipo, ResultadoCompilacion.Metodo, r.NombreEnsamblado, fallidas.ToArray()
            };
        }

        /// <summary>Versión de Roslyn (Microsoft.CodeAnalysis.CSharp) cargada en este contexto.</summary>
        public static string VersionRoslyn() => typeof(CSharpCompilation).Assembly.GetName().Version?.ToString();
    }
}
