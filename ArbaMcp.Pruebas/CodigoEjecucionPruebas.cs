using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Ejecución de scripts de ejecutar_codigo (1.16.0) en la nube con la Fachada de prueba: resultado, tiempo límite
    /// cooperativo (bucles, lambdas, LINQ), recursión infinita sin cerrar el proceso, línea de un error y descarga del
    /// contexto coleccionable. CodigoVigilante es estático: estas pruebas no corren en paralelo entre sí.
    /// </summary>
    [Collection("CodigoVigilante")]
    public class CodigoEjecucionPruebas
    {
        private static readonly TimeSpan Corto = TimeSpan.FromMilliseconds(300);

        [Fact]
        public void Ejecuta_DevuelveElResultadoYLeeParametros()
        {
            var r = Scripts.CompilarBien(@"F.Escribir(""hola"");
var nombre = Parametros.GetProperty(""nombre"").GetString();
return new { suma = 1 + 2, lista = new[] { 1, 2, 3 }, texto = nombre, doble = F.Doble(21) };");
            var f = new FachadaPrueba();
            object res;
            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(10)))
                res = Scripts.Ejecutar(r, f, "{\"nombre\":\"Eje 1\"}");
            var d = Assert.IsType<Dictionary<string, object>>(res);
            Assert.Equal(3, d["suma"]);
            Assert.Equal(new List<object> { 1, 2, 3 }, d["lista"]);
            Assert.Equal("Eje 1", d["texto"]);
            Assert.Equal(42, d["doble"]);
            Assert.Equal(new[] { "hola" }, f.Mensajes);
        }

        [Fact]
        public void SinReturn_DevuelveNull()
        {
            var r = Scripts.CompilarBien("var a = 1;");
            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(10)))
                Assert.Null(Scripts.Ejecutar(r, new FachadaPrueba()));
        }

        [Theory]
        [InlineData("while (true) { }")]
        [InlineData("for (;;) ;")]
        [InlineData("long n = 0;\ndo { n++; } while (true);")]
        [InlineData("foreach (var x in System.Linq.Enumerable.Repeat(1, int.MaxValue)) { }")]
        [InlineData("return System.Linq.Enumerable.Range(0, int.MaxValue).Where(x => x >= 0).Count();")]
        public void BucleInfinito_SeCortaPorElTiempoLimite(string codigo)
        {
            var r = Scripts.CompilarBien(codigo);
            var reloj = Stopwatch.StartNew();
            OperationCanceledException ex;
            using (CodigoVigilante.Iniciar(Corto))
                ex = Assert.Throws<OperationCanceledException>(() => Scripts.Ejecutar(r, new FachadaPrueba()));
            Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(10), "Tardó " + reloj.Elapsed);
            Assert.Contains("superó el tiempo límite", ex.Message);
            Assert.NotNull(EjecucionScript.LineaDe(ex));
        }

        [Fact]
        public void BucleDeVariasLineas_SeCortaEnLaPrimeraLineaDelCuerpo()
        {
            // El paso 9 de VALIDACION_116.md espera "se detuvo en la línea 4": la revisión va delante de la primera sentencia del cuerpo
            var r = Scripts.CompilarBien("long n = 0;\nwhile (true)\n{\n    n++;\n}");
            OperationCanceledException ex;
            using (CodigoVigilante.Iniciar(Corto))
                ex = Assert.Throws<OperationCanceledException>(() => Scripts.Ejecutar(r, new FachadaPrueba()));
            Assert.Equal(4, EjecucionScript.LineaDe(ex));
        }

        [Theory]
        [InlineData("Func<int, int> f = null;\nf = x => f(x + 1);\nreturn f(0);")]
        [InlineData("int f(int x) => f(x + 1);\nreturn f(0);")]
        [InlineData("int f(int x) { return f(x + 1); }\nreturn f(0);")]
        public void RecursionInfinita_DaUnErrorEnVezDeCerrarElProceso(string codigo)
        {
            // Sin la revisión insertada, esto desbordaría la pila y cerraría el proceso de pruebas (y Civil 3D)
            var r = Scripts.CompilarBien(codigo);
            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(30)))
                Assert.Throws<InsufficientExecutionStackException>(() => Scripts.Ejecutar(r, new FachadaPrueba()));
        }

        [Fact]
        public void ErrorEnTiempoDeEjecucion_DiceLaLinea()
        {
            var r = Scripts.CompilarBien("var a = 1;\nstring s = null;\nreturn s.Length + a;");
            NullReferenceException ex;
            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(10)))
                ex = Assert.Throws<NullReferenceException>(() => Scripts.Ejecutar(r, new FachadaPrueba()));
            Assert.Equal(3, EjecucionScript.LineaDe(ex));
        }

        [Fact]
        public void RevisionesInsertadas_NoCambianLasLineasDeLosErrores()
        {
            var r = Scripts.CompilarBien("for (int i = 0; i < 3; i++)\n    F.Escribir(\"x\");\nFunc<int, int> f = x => x + 1;\nstring s = null;\nreturn s.Length + f(1);");
            var fachada = new FachadaPrueba();
            NullReferenceException ex;
            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(10)))
                ex = Assert.Throws<NullReferenceException>(() => Scripts.Ejecutar(r, fachada));
            Assert.Equal(5, EjecucionScript.LineaDe(ex));
            Assert.Equal(3, fachada.Mensajes.Count);
        }

        [Fact]
        public void OrdenDeParada_DetieneElScriptEnLaSiguienteRevision()
        {
            var r = Scripts.CompilarBien("while (true) { F.Escribir(\"x\"); }");
            var f = new FachadaPrueba();
            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(30)))
            {
                CodigoVigilante.Detener("regla del proyecto: ninguna herramienta borra superficies.");
                var ex = Assert.Throws<InvalidOperationException>(() => Scripts.Ejecutar(r, f));
                Assert.Contains("borra superficies", ex.Message);
            }
            Assert.Empty(f.Mensajes);
        }

        [Fact]
        public void Vigilante_FueraDeUnaEjecucionNoHaceNadaYNoAdmiteDosALaVez()
        {
            CodigoVigilante.Revisar();
            Assert.False(CodigoVigilante.Activo);
            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(10)))
            {
                Assert.True(CodigoVigilante.Activo);
                Assert.Throws<InvalidOperationException>(() => CodigoVigilante.Iniciar(TimeSpan.FromSeconds(1)));
                CodigoVigilante.Revisar();
                Assert.Equal(1, CodigoVigilante.Revisiones);
            }
            Assert.False(CodigoVigilante.Activo);
            CodigoVigilante.Revisar();
        }

        [Fact]
        public void Descarga_ElContextoDelScriptSeLibera()
        {
            var debil = EjecutarYDescargar();
            for (int i = 0; i < 20 && debil.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            Assert.False(debil.IsAlive, "El AssemblyLoadContext coleccionable del script no se liberó.");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference EjecutarYDescargar()
        {
            var r = Scripts.CompilarBien("return new { a = 1, b = \"dos\" };");
            var e = new EjecucionScript(r.Ensamblado, r.Pdb, ResultadoCompilacion.Tipo, ResultadoCompilacion.Metodo, r.NombreEnsamblado,
                typeof(FachadaPrueba).Assembly, typeof(CodigoVigilante).Assembly);
            object res;
            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(10)))
                res = CodigoResultado.Normalizar(e.Invocar(new FachadaPrueba(), Scripts.Json("{}")));
            Assert.IsType<Dictionary<string, object>>(res);
            return e.Descargar();
        }

        [Fact]
        public void Cargador_DeExtremoAExtremoComoEnCivil3D()
        {
            string bin = Path.GetDirectoryName(typeof(CodigoPuerta).Assembly.Location);
            var cargador = new CargadorCompilador(bin, typeof(CodigoPuerta).Assembly.Location);
            var r = cargador.Compilar("return F.Doble(Parametros.GetProperty(\"n\").GetInt32());", Scripts.RutasReferencias(), Scripts.TipoFachada);
            Assert.True(r.Ok, r.Mensaje);
            object res;
            using (var e = new EjecucionScript(r.Ensamblado, r.Pdb, r.Tipo, r.Metodo, r.NombreEnsamblado, typeof(FachadaPrueba).Assembly, typeof(CodigoVigilante).Assembly))
            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(10)))
                res = e.Invocar(new FachadaPrueba(), Scripts.Json("{\"n\":21}"));
            Assert.Equal(42, res);
        }
    }

    /// <summary>Activación por ARBA_MCP_CODIGO y carpeta permitida de los archivos de los scripts (1.16.0).</summary>
    public class CodigoActivacionYCarpetaPruebas
    {
        [Theory]
        [InlineData("1", true)]
        [InlineData(" 1 ", true)]
        [InlineData("0", false)]
        [InlineData("true", false)]
        [InlineData("si", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Activacion_SoloConUno(string valor, bool activada)
            => Assert.Equal(activada, CodigoActivacion.Activada(valor));

        [Fact]
        public void Activacion_ElMensajeEmpiezaComoEsperaLaReceta()
        {
            Assert.StartsWith("ejecutar_codigo está desactivada: ", CodigoActivacion.MensajeDesactivada);
            Assert.Contains("ARBA_MCP_CODIGO=1", CodigoActivacion.MensajeDesactivada);
            Assert.Equal("ARBA_MCP_CODIGO", CodigoActivacion.Variable);
        }

        private static string Carpeta() => Path.Combine(Path.GetTempPath(), "arbamcp_scripts_" + Guid.NewGuid().ToString("N"));

        [Theory]
        [InlineData("datos.csv")]
        [InlineData("sub/datos.csv")]
        [InlineData("sub\\otra\\x.txt")]
        public void Carpeta_NombresRelativosQuedanDentro(string nombre)
        {
            string c = Carpeta();
            string ruta = CodigoCarpeta.Resolver(c, nombre);
            Assert.StartsWith(Path.GetFullPath(c), ruta);
            Assert.EndsWith(Path.GetFileName(nombre.Replace('\\', '/')), ruta.Replace('\\', '/'));
        }

        [Theory]
        [InlineData("")]
        [InlineData("../fuera.txt")]
        [InlineData("..\\fuera.txt")]
        [InlineData("sub/../../fuera.txt")]
        [InlineData("C:\\Proyectos\\x.txt")]
        [InlineData("/etc/passwd")]
        [InlineData("\\\\servidor\\x.txt")]
        [InlineData("x.txt:flujo")]
        [InlineData("CON")]
        [InlineData("nul.txt")]
        [InlineData("a/./b.txt")]
        [InlineData("x?.txt")]
        public void Carpeta_RechazaLoQueSaleONoEsValido(string nombre)
            => Assert.Throws<ArgumentException>(() => CodigoCarpeta.Resolver(Carpeta(), nombre));

        [Fact]
        public void Carpeta_ListaConNombresRelativos()
        {
            string c = Carpeta();
            Assert.Empty(CodigoCarpeta.Listar(c));
            Directory.CreateDirectory(Path.Combine(c, "sub"));
            File.WriteAllText(Path.Combine(c, "b.txt"), "b");
            File.WriteAllText(Path.Combine(c, "sub", "a.txt"), "a");
            Assert.Equal(new[] { "b.txt", "sub/a.txt" }, CodigoCarpeta.Listar(c));
            Directory.Delete(c, true);
        }

        [Fact]
        public void Carpeta_PorDefectoEnLocalAppDataArbaMcpScripts()
            => Assert.EndsWith(Path.Combine("ArbaMcp", "scripts"), CodigoCarpeta.Predeterminada());
    }

    /// <summary>Conversión del valor que devuelve un script a valores simples para el JSON de la respuesta (1.16.0).</summary>
    public class CodigoResultadoPruebas
    {
        [Fact]
        public void Simples_YAnonimos()
        {
            var d = Assert.IsType<Dictionary<string, object>>(CodigoResultado.Normalizar(new { a = 1, b = "x", c = 2.5, d = true, e = (string)null, f = double.NaN }));
            Assert.Equal(1, d["a"]);
            Assert.Equal("x", d["b"]);
            Assert.Equal(2.5, d["c"]);
            Assert.Equal(true, d["d"]);
            Assert.Null(d["e"]);
            Assert.Null(d["f"]);
        }

        [Fact]
        public void ListasDiccionariosYTuplas()
        {
            var l = Assert.IsType<List<object>>(CodigoResultado.Normalizar(new[] { 1, 2 }));
            Assert.Equal(2, l.Count);
            var d = Assert.IsType<Dictionary<string, object>>(CodigoResultado.Normalizar(new Dictionary<int, string> { [1] = "uno" }));
            Assert.Equal("uno", d["1"]);
            var t = Assert.IsType<List<object>>(CodigoResultado.Normalizar((1, "dos")));
            Assert.Equal(new List<object> { 1, "dos" }, t);
        }

        [Fact]
        public void ListaLarga_SeRecortaConAviso()
        {
            var avisos = new List<string>();
            var l = Assert.IsType<List<object>>(CodigoResultado.Normalizar(new int[5000], null, avisos));
            Assert.Equal(1000, l.Count);
            Assert.Contains(avisos, a => a.Contains("1000"));
        }

        [Fact]
        public void Ciclo_NoSeCuelga()
        {
            var a = new Nodo();
            a.Siguiente = a;
            var d = Assert.IsType<Dictionary<string, object>>(CodigoResultado.Normalizar(a));
            Assert.Equal("(ciclo)", d["Siguiente"]);
        }

        public sealed class Nodo { public Nodo Siguiente { get; set; } }

        [Fact]
        public void Convertidor_DelPluginSeAplicaPrimero()
        {
            CodigoResultado.Convertidor conv = (object v, out object c) =>
            {
                if (v is Nodo) { c = "nodo"; return true; }
                c = null;
                return false;
            };
            var d = Assert.IsType<Dictionary<string, object>>(CodigoResultado.Normalizar(new { n = new Nodo() }, conv));
            Assert.Equal("nodo", d["n"]);
        }

        [Fact]
        public void TiposDeSystem_ComoTexto()
        {
            Assert.Equal("System.String", CodigoResultado.Normalizar(typeof(string)));
            Assert.Equal("ab", CodigoResultado.Normalizar(new System.Text.StringBuilder("ab")));
            Assert.Equal("Monday", CodigoResultado.Normalizar(DayOfWeek.Monday));
        }
    }
}
