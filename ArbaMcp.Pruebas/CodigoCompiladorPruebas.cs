using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>Compilación de los scripts de ejecutar_codigo (1.16.0): mensajes claros con la línea del agente, caché y contexto aislado.</summary>
    public class CodigoCompiladorPruebas
    {
        [Fact]
        public void ErrorDeSintaxis_DiceLineaColumnaTextoYPista()
        {
            var r = Scripts.Compilar("var x = 1\nreturn x;");
            Assert.False(r.Ok);
            var e = Assert.Single(r.Errores);
            Assert.Contains("Línea 1, columna 10: CS1002", e);
            Assert.Contains("    var x = 1", e);
            Assert.Contains("^", e);
            Assert.Contains("punto y coma", e);
            Assert.StartsWith("ejecutar_codigo: el código no se puede ejecutar (1 error(es) de compilación)", r.Mensaje);
            Assert.Contains("No se ejecutó nada", r.Mensaje);
        }

        [Fact]
        public void NombreQueNoExiste_EnLaLineaDelAgente()
        {
            var r = Scripts.Compilar("var x = 1;\nreturn x + y;");
            Assert.False(r.Ok);
            Assert.Contains(r.Errores, e => e.Contains("Línea 2, columna 12: CS0103") && e.Contains("Pista") && e.Contains("Civ.Alignment"));
        }

        [Fact]
        public void DosErrores_CadaUnoConSuLinea()
        {
            // El paso 8 de VALIDACION_116.md espera exactamente estos dos errores
            var r = Scripts.Compilar("var x = 1\nreturn x + y;");
            Assert.False(r.Ok);
            Assert.Equal(2, r.Errores.Count);
            Assert.Contains("Línea 1, columna 10: CS1002", r.Errores[0]);
            Assert.Contains("Línea 2, columna 12: CS0103", r.Errores[1]);
            Assert.Contains("'y'", r.Errores[1]);
            Assert.Contains("CS1002 Se esperaba ; Pista:", r.Errores[0]);
            Assert.Contains("contexto actual. Pista:", r.Errores[1]);
        }

        [Fact]
        public void RedYTareas_DosBloqueos()
        {
            // El paso 6 de VALIDACION_116.md espera los dos bloqueos, cada uno en su línea
            var r = Scripts.Compilar("var cliente = new System.Net.Http.HttpClient();\nreturn cliente.GetStringAsync(\"https://example.com\").Result;");
            Assert.False(r.Ok);
            Assert.Contains(r.Problemas, p => p.Categoria == CodigoReglas.Red && p.Linea == 1 && p.Mensaje.Contains("System.Net.Http.HttpClient"));
            Assert.Contains(r.Problemas, p => p.Categoria == CodigoReglas.Hilos && p.Linea == 2);
        }

        [Fact]
        public void LosUsingDelPrincipioNoCambianLasLineas()
        {
            var r = Scripts.Compilar("using System.Text;\n\nvar sb = new StringBuilder();\nreturn sb.Length + zz;");
            Assert.False(r.Ok);
            Assert.Contains(r.Errores, e => e.Contains("Línea 4,") && e.Contains("CS0103"));
            Assert.Empty(r.Problemas);
        }

        [Fact]
        public void ReturnSinValor_TienePista()
        {
            var r = Scripts.Compilar("return;");
            Assert.False(r.Ok);
            Assert.Contains(r.Errores, e => e.Contains("CS0126") && e.Contains("return algo;"));
        }

        [Fact]
        public void ElCodigoQueSaleDelMetodo_NoCompila()
        {
            var r = Scripts.Compilar("return 1; } } }\nnamespace Otro { public class Malo { } }\nnamespace ArbaMcpScript { public static class S2 { static void M() {");
            Assert.False(r.Ok);
            Assert.Contains("El código no queda dentro del método", r.Mensaje);
            Assert.Contains(r.Problemas, p => p.Categoria == CodigoReglas.Sintaxis);
        }

        [Fact]
        public void LlavesSinCerrar_NoCompila()
        {
            var r = Scripts.Compilar("if (true) {\nreturn 1;");
            Assert.False(r.Ok);
            Assert.Contains("revisa las llaves", r.Mensaje);
        }

        [Fact]
        public void CodigoVacio_DiceQueFalta()
        {
            var r = Scripts.Compilar("   ");
            Assert.False(r.Ok);
            Assert.Contains("falta el código", r.Mensaje);
        }

        [Fact]
        public void MiembroObsoleto_SaleComoAviso()
        {
            var r = Scripts.CompilarBien("Line l = null;\nreturn l.Largo;");
            var aviso = Assert.Single(r.Avisos);
            Assert.Contains("Línea 2", aviso);
            Assert.Contains("CS0618", aviso);
        }

        [Fact]
        public void ElMismoCodigo_SaleDeLaCache()
        {
            string codigo = "return " + Guid.NewGuid().GetHashCode() + ";";
            var a = Scripts.CompilarBien(codigo);
            var b = Scripts.CompilarBien(codigo);
            Assert.False(a.DeCache);
            Assert.True(b.DeCache);
            Assert.Equal(a.Ensamblado, b.Ensamblado);
        }

        [Fact]
        public void Lineas_CuentaLasDelAgente()
            => Assert.Equal(3, Scripts.CompilarBien("var a = 1;\nvar b = 2;\nreturn a + b;").Lineas);

        [Fact]
        public void Puerta_CompilaConRutasYSoloTiposBasicos()
        {
            var a = CodigoPuerta.Compilar("return 40 + 2;", Scripts.RutasReferencias(), Scripts.TipoFachada);
            var r = ResultadoAislado.DesdeArreglo(a);
            Assert.True(r.Ok, r.Mensaje);
            Assert.NotNull(r.Ensamblado);
            Assert.NotNull(r.Pdb);
            Assert.Equal(ResultadoCompilacion.Tipo, r.Tipo);
            Assert.Equal(ResultadoCompilacion.Metodo, r.Metodo);
            Assert.Empty(r.ReferenciasFallidas);
            Assert.StartsWith("5.", CodigoPuerta.VersionRoslyn());
        }

        [Fact]
        public void Cargador_CompilaConRoslynEnUnContextoPropio()
        {
            // Como en Civil 3D: Roslyn y una segunda copia de ArbaMcp.Nucleo en el contexto 'ArbaMcp.Compilador'
            string bin = Path.GetDirectoryName(typeof(CodigoPuerta).Assembly.Location);
            var cargador = new CargadorCompilador(bin, typeof(CodigoPuerta).Assembly.Location);
            var r = cargador.Compilar("return 6 * 7;", Scripts.RutasReferencias(), Scripts.TipoFachada);
            Assert.True(r.Ok, r.Mensaje);
            Assert.StartsWith("5.", cargador.VersionRoslyn);
            var contexto = AssemblyLoadContext.All.FirstOrDefault(c => c.Name == "ArbaMcp.Compilador");
            Assert.NotNull(contexto);
            Assert.Contains(contexto.Assemblies, a => a.GetName().Name == "Microsoft.CodeAnalysis.CSharp");
            Assert.Contains(contexto.Assemblies, a => a.GetName().Name == "ArbaMcp.Nucleo");

            var mal = cargador.Compilar("return new System.Net.Http.HttpClient();", Scripts.RutasReferencias(), Scripts.TipoFachada);
            Assert.False(mal.Ok);
            Assert.Contains("[bloqueado: red]", mal.Mensaje);
        }

        [Fact]
        public void Cargador_SinLaCarpetaDeRoslyn_DiceQueReinstale()
        {
            var cargador = new CargadorCompilador(Path.Combine(Path.GetTempPath(), "no_existe_" + Guid.NewGuid().ToString("N")), typeof(CodigoPuerta).Assembly.Location);
            var ex = Assert.Throws<FileNotFoundException>(() => cargador.Compilar("return 1;", new string[0], Scripts.TipoFachada));
            Assert.Contains("Contents\\roslyn", ex.Message);
            Assert.Contains("instalar.ps1", ex.Message);
        }
    }
}
