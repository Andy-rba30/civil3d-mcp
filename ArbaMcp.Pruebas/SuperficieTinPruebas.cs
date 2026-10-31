using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>Lógica de crear_superficie_tin que no depende de Civil 3D: archivo de puntos, formato, intervalos y origen.</summary>
    public class SuperficieTinPruebas : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "arba_tin_" + Guid.NewGuid().ToString("N"));

        public SuperficieTinPruebas() { Directory.CreateDirectory(_dir); }
        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

        private string Escribir(string nombre, params string[] lineas)
        {
            string ruta = Path.Combine(_dir, nombre);
            File.WriteAllLines(ruta, lineas);
            return ruta;
        }

        // Ocho puntos PENZD sintéticos en coordenadas genéricas (ningún dato real)
        private static readonly string[] Penzd =
        {
            "# archivo de prueba, 8 puntos PENZD",
            "1,1000.0,2000.0,100.0,TN",
            "2,1010.0,2000.0,100.5,TN",
            "3,1020.0,2000.0,101.0,TN",
            "4,1000.0,2010.0,100.2,TN",
            "5,1010.0,2010.0,100.8,TN",
            "6,1020.0,2010.0,101.4,TN",
            "7,1000.0,2020.0,100.4,TN",
            "8,1020.0,2020.0,102.0,TN"
        };

        [Fact]
        public void LeeUnArchivoPenzdConComasYCalculaLaExtension()
        {
            var r = ArchivoPuntos.Leer(Escribir("puntos_prueba.csv", Penzd));
            Assert.Equal(9, r.Lineas);
            Assert.Equal(8, r.LineasPuntos);
            Assert.Equal(1, r.LineasIgnoradas);
            Assert.Equal(",", r.Separador);
            Assert.Equal("coma", r.SeparadorNombre);
            Assert.Equal(5, r.Columnas);
            Assert.Equal(new[] { 1, 2, 3 }, r.ColumnasENZ);
            Assert.Equal(8, r.Puntos.Count);
            Assert.NotNull(r.Extension);
            Assert.Equal(1000.0, r.Extension.MinX); Assert.Equal(1020.0, r.Extension.MaxX);
            Assert.Equal(2000.0, r.Extension.MinY); Assert.Equal(2020.0, r.Extension.MaxY);
            Assert.Equal(100.0, r.Extension.MinZ); Assert.Equal(102.0, r.Extension.MaxZ);
            var d = r.Extension.ComoDiccionario();
            Assert.Equal(1000.0, d["min_x"]); Assert.Equal(102.0, d["max_z"]);
            Assert.Equal(new[] { 2, 3, 4 }, (int[])r.ComoDiccionario()["columnas_este_norte_cota"]);
        }

        [Fact]
        public void DetectaPuntoYComaConComaDecimalTabuladorYEspacio()
        {
            var pc = ArchivoPuntos.Analizar(new[] { "1;1000,5;2000,5;100,2;TN", "2;1010,5;2000,5;100,7;TN" });
            Assert.Equal(";", pc.Separador);
            Assert.Equal(2, pc.LineasPuntos);
            Assert.Equal(1000.5, pc.Puntos[0].X);
            Assert.Equal(100.7, pc.Puntos[1].Z);

            var tab = ArchivoPuntos.Analizar(new[] { "1\t1000\t2000\t100", "2\t1010\t2000\t101" });
            Assert.Equal("\t", tab.Separador);
            Assert.Equal(4, tab.Columnas);
            Assert.Equal(new[] { 1, 2, 3 }, tab.ColumnasENZ);

            var esp = ArchivoPuntos.Analizar(new[] { "1000 2000 100", "1010   2000 101", "", "1020 2000 102" });
            Assert.Equal(" ", esp.Separador);
            Assert.Equal(3, esp.Columnas);
            Assert.Equal(new[] { 0, 1, 2 }, esp.ColumnasENZ);
            Assert.Equal(3, esp.LineasPuntos);
            Assert.Equal(1, esp.LineasIgnoradas);
        }

        [Fact]
        public void ElNombreDelFormatoDecideLasColumnas()
        {
            var r = ArchivoPuntos.Analizar(new[] { "1,2000.0,1000.0,100.0,TN", "2,2000.0,1010.0,101.0,TN" }, "PNEZD (comma delimited)");
            Assert.Equal(new[] { 2, 1, 3 }, r.ColumnasENZ);
            Assert.Equal(1000.0, r.Puntos[0].X);
            Assert.Equal(2000.0, r.Puntos[0].Y);
            Assert.Equal(new[] { 0, 1, 2 }, SuperficieTin.ColumnasENZ("ENZ (space delimited)", 3));
            Assert.Equal(new[] { 1, 0, 2 }, SuperficieTin.ColumnasENZ("NEZ", 3));
            Assert.Equal(new[] { 1, 2, 3 }, SuperficieTin.ColumnasENZ("PXYZ", 4));
            Assert.Null(SuperficieTin.ColumnasENZ("PENZD", 3));   // PENZD necesita al menos 4 columnas
            Assert.Null(SuperficieTin.ColumnasENZ("External Project Point Database", 2));
            Assert.Equal(new[] { 1, 2, 3 }, SuperficieTin.ColumnasENZ(null, 5));
        }

        [Fact]
        public void ArchivoSinCoordenadasOInexistente()
        {
            var r = ArchivoPuntos.Analizar(new[] { "# solo comentarios", "", "texto sin numeros" });
            Assert.Null(r.Separador);
            Assert.Equal(0, r.LineasPuntos);
            Assert.Equal(3, r.LineasIgnoradas);
            Assert.Null(r.Extension);

            var cab = ArchivoPuntos.Analizar(new[] { "P,E,N,Z,D", "1,1000,2000,100,TN", "2,1010,2000,101,TN" });
            Assert.Equal(",", cab.Separador);
            Assert.Equal(2, cab.LineasPuntos);
            Assert.Equal(1, cab.LineasIgnoradas);

            var ex = Assert.Throws<FileNotFoundException>(() => ArchivoPuntos.Leer(Path.Combine(_dir, "no_existe.csv")));
            Assert.Contains("No existe el archivo de puntos", ex.Message);
        }

        [Fact]
        public void OrigenExactamenteUno()
        {
            Assert.Equal(SuperficieTin.OrigenGrupo, SuperficieTin.Origen("Terreno", null));
            Assert.Equal(SuperficieTin.OrigenArchivo, SuperficieTin.Origen("  ", @"C:\Proyectos\puntos.csv"));
            Assert.Contains("solo uno", Assert.Throws<ArgumentException>(() => SuperficieTin.Origen("Terreno", @"C:\Proyectos\puntos.csv")).Message);
            Assert.Contains("Falta el origen", Assert.Throws<ArgumentException>(() => SuperficieTin.Origen(null, "")).Message);
        }

        [Fact]
        public void IntervalosValidos()
        {
            SuperficieTin.ValidarIntervalos(null, null);
            SuperficieTin.ValidarIntervalos(1, 5);
            SuperficieTin.ValidarIntervalos(2, null);
            SuperficieTin.ValidarIntervalos(2, 2);
            Assert.Contains("intervalo_curvas_menor", Assert.Throws<ArgumentException>(() => SuperficieTin.ValidarIntervalos(0, 5)).Message);
            Assert.Contains("intervalo_curvas_mayor", Assert.Throws<ArgumentException>(() => SuperficieTin.ValidarIntervalos(1, -5)).Message);
            Assert.Contains("no puede ser menor", Assert.Throws<ArgumentException>(() => SuperficieTin.ValidarIntervalos(5, 1)).Message);
        }

        private static readonly List<FormatoPuntos> Formatos = new List<FormatoPuntos>
        {
            new FormatoPuntos { Nombre = "PENZD (space delimited)", Delimitador = " " },
            new FormatoPuntos { Nombre = "PENZD (comma delimited)", Delimitador = "," },
            new FormatoPuntos { Nombre = "PNEZD (comma delimited)", Delimitador = "," },
            new FormatoPuntos { Nombre = "ENZ (comma delimited)", Delimitador = "," },
            new FormatoPuntos { Nombre = "PENZ (tab delimited)", Delimitador = "\t" },
            new FormatoPuntos { Nombre = "External Project Point Database", Delimitador = "" }
        };

        [Fact]
        public void EligeElFormatoPorColumnasYSeparador()
        {
            string motivo;
            Assert.Equal("PENZD (comma delimited)", SuperficieTin.ElegirFormato(Formatos, null, ",", 5, out motivo).Nombre);
            Assert.Contains("elegido automáticamente", motivo);
            Assert.Equal("PENZD (space delimited)", SuperficieTin.ElegirFormato(Formatos, null, " ", 5, out motivo).Nombre);
            Assert.Equal("ENZ (comma delimited)", SuperficieTin.ElegirFormato(Formatos, null, ",", 3, out motivo).Nombre);
            Assert.Equal("PENZ (tab delimited)", SuperficieTin.ElegirFormato(Formatos, null, "\t", 4, out motivo).Nombre);
            // 4 columnas con coma: no hay PENZ con coma, se acepta PENZD con coma (la descripción queda vacía)
            Assert.Equal("PENZD (comma delimited)", SuperficieTin.ElegirFormato(Formatos, null, ",", 4, out motivo).Nombre);
            // punto y coma: ningún formato lo usa
            Assert.Null(SuperficieTin.ElegirFormato(Formatos, null, ";", 5, out motivo));
            Assert.Null(motivo);
            Assert.Contains("'formato_archivo'", SuperficieTin.MensajeSinFormato(Formatos, ";", 5));
            Assert.Contains("'PENZD (comma delimited)'", SuperficieTin.MensajeSinFormato(Formatos, ";", 5));
        }

        [Fact]
        public void FormatoPedidoPorNombreOErrorConLaLista()
        {
            string motivo;
            Assert.Equal("PNEZD (comma delimited)", SuperficieTin.ElegirFormato(Formatos, "pnezd (COMMA delimited) ", ",", 5, out motivo).Nombre);
            Assert.Equal("indicado en 'formato_archivo'", motivo);
            var ex = Assert.Throws<ArgumentException>(() => SuperficieTin.ElegirFormato(Formatos, "PENZD (delimitado por comas)", ",", 5, out motivo));
            Assert.Contains("No existe el formato de archivo de puntos 'PENZD (delimitado por comas)'", ex.Message);
            Assert.Contains("'PENZD (comma delimited)'", ex.Message);
            Assert.Contains("ninguno", Assert.Throws<ArgumentException>(() => SuperficieTin.ElegirFormato(new List<FormatoPuntos>(), "X", ",", 5, out motivo)).Message);
        }

        [Fact]
        public void SeparadorCoincidePorDelimitadorOPorNombre()
        {
            Assert.True(SuperficieTin.SeparadorCoincide(new FormatoPuntos { Nombre = "PENZD", Delimitador = "," }, ","));
            Assert.False(SuperficieTin.SeparadorCoincide(new FormatoPuntos { Nombre = "PENZD (comma delimited)", Delimitador = " " }, ","), "el delimitador manda sobre el nombre");
            Assert.True(SuperficieTin.SeparadorCoincide(new FormatoPuntos { Nombre = "PENZD (delimitado por comas)", Delimitador = null }, ","));
            Assert.True(SuperficieTin.SeparadorCoincide(new FormatoPuntos { Nombre = "PENZD (space delimited)", Delimitador = "" }, " "));
            Assert.False(SuperficieTin.SeparadorCoincide(new FormatoPuntos { Nombre = "PENZD (space delimited)", Delimitador = "" }, null));
        }

        [Fact]
        public void AvisoDeIntervalosExplicaQueVivenEnElEstilo()
        {
            Assert.Null(SuperficieTin.AvisoIntervalos(null, null, "Estándar", 2, 10));
            string a = SuperficieTin.AvisoIntervalos(1, 5, "Estándar", 2, 10);
            Assert.Contains("menor 1 y mayor 5", a);
            Assert.Contains("sin aplicar", a);
            Assert.Contains("ContourStyle.MinorContourInterval", a);
            Assert.Contains("El estilo 'Estándar' tiene menor 2 y mayor 10", a);
            Assert.Contains("no modifica ni crea estilos", a);
            string b = SuperficieTin.AvisoIntervalos(1, 5, "Curvas 1 y 5", 1, 5);
            Assert.Contains("ya coinciden", b);
            string c = SuperficieTin.AvisoIntervalos(null, 5, "Estándar", null, null);
            Assert.Contains("(mayor 5)", c);
            Assert.DoesNotContain("tiene menor", c);
        }

        [Fact]
        public void ExtensionDeUnaListaVaciaEsNull()
        {
            Assert.Null(ExtensionPuntos.Calcular(new List<Punto3>()));
            var e = ExtensionPuntos.Calcular(new[] { new Punto3(1, 2, 3), new Punto3(-1, 5, 0) });
            Assert.Equal(2, e.N);
            Assert.Equal(-1, e.MinX); Assert.Equal(1, e.MaxX); Assert.Equal(2, e.MinY); Assert.Equal(5, e.MaxY); Assert.Equal(0, e.MinZ); Assert.Equal(3, e.MaxZ);
        }
    }
}
