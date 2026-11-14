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

        // ------------------------------------------------------------------ 1.17.2: estilo elegido por sus curvas
        private static EstiloCurvas Est(string nombre, double? menor, double? mayor, bool? menorVisible = true, bool? mayorVisible = true)
            => new EstiloCurvas { Nombre = nombre, Menor = menor, Mayor = mayor, MenorVisible = menorVisible, MayorVisible = mayorVisible };

        /// <summary>Los estilos de superficie del dibujo de prueba (informe 1.14, paso 5.7) con intervalos plausibles.</summary>
        private static List<EstiloCurvas> EstilosPlantilla() => new List<EstiloCurvas>
        {
            Est("Contours 5m and 25m (Background)", 5, 25),
            Est("Border Only", 1, 5, false, false),
            Est("Contours 2m and 10m (Background)", 2, 10),
            Est("Contours and Triangles", 1, 5),
            Est("Contours 5m and 25m (Design)", 5, 25),
            Est("Elevation Banding 1m Interval (2D)", 1, 5, false, false),
            Est("Contours 1m and 5m (Background)", 1, 5),
            Est("_No Display", 1, 5, false, false),
            Est("Contours 2m and 10m (Design)", 2, 10),
            Est("Contours 1m and 5m (Design)", 1, 5)
        };

        [Fact]
        public void IntervalosCoincidenConToleranciaDeUnMilimetro()
        {
            Assert.Equal(0.001, SuperficieTin.ToleranciaIntervalos);
            Assert.True(SuperficieTin.IntervaloCoincide(1, 1));
            Assert.True(SuperficieTin.IntervaloCoincide(1, 1.0009));
            Assert.True(SuperficieTin.IntervaloCoincide(1, 0.999));
            Assert.False(SuperficieTin.IntervaloCoincide(1, 1.002));
            Assert.False(SuperficieTin.IntervaloCoincide(1, null));
            Assert.True(SuperficieTin.Coinciden(1, 5, 1.0005, 4.9995));
            Assert.False(SuperficieTin.Coinciden(1, 5, 1, 25));
            Assert.True(SuperficieTin.Coinciden(1, null, 1, 25), "el intervalo que no se pide no se compara");
            Assert.True(SuperficieTin.Coinciden(null, 5, 2, 5));
            Assert.False(SuperficieTin.Coinciden(null, null, 1, 5), "sin intervalos pedidos no hay nada que coincida");
        }

        [Fact]
        public void FamiliaEsElUltimoParentesisDelNombre()
        {
            Assert.Equal("background", SuperficieTin.Familia("Contours 5m and 25m (Background)"));
            Assert.Equal("design", SuperficieTin.Familia(" Contours 1m and 5m (Design) "));
            Assert.Equal("2d", SuperficieTin.Familia("Elevation Banding (2D)"));
            Assert.Null(SuperficieTin.Familia("Contours and Triangles"));
            Assert.Null(SuperficieTin.Familia("Raro (abierto"));
            Assert.Null(SuperficieTin.Familia("Vacío ()"));
            Assert.Null(SuperficieTin.Familia(null));
        }

        [Fact]
        public void SinIntervalosPedidosNoHayEleccion()
        {
            Assert.Null(SuperficieTin.ElegirEstilo(EstilosPlantilla(), null, null, null, "Contours 5m and 25m (Background)"));
            Assert.Null(SuperficieTin.ElegirEstilo(EstilosPlantilla(), null, null, "Border Only", null));
        }

        [Fact]
        public void SinEstiloEligeElDeLaFamiliaDelEstiloPorDefecto()
        {
            var r = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 1, 5, null, "Contours 5m and 25m (Background)");
            Assert.Equal(EleccionEstilo.Automatica, r.Eleccion);
            Assert.Equal("Contours 1m and 5m (Background)", r.Estilo);
            Assert.Equal(new[] { "Contours 1m and 5m (Design)", "Contours and Triangles" }, r.OtrosQueCoinciden);
            Assert.Equal(new[] { "Border Only", "Elevation Banding 1m Interval (2D)", "_No Display" }, r.DescartadosSinCurvasVisibles);
            Assert.Contains("aplicados sin 'estilo'", r.Motivo);
            Assert.Contains("misma familia («Background»)", r.Motivo);
            Assert.Contains("'Contours 5m and 25m (Background)'", r.Motivo);
            Assert.Contains("También coinciden: 'Contours 1m and 5m (Design)', 'Contours and Triangles'", r.Motivo);
            Assert.Contains("descartados", r.Motivo);
            Assert.Contains("No se modificó ni se creó ningún estilo", r.Motivo);
            Assert.DoesNotContain("compruébalo en pantalla", r.Motivo);

            var d = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 1, 5, null, "Contours 5m and 25m (Design)");
            Assert.Equal("Contours 1m and 5m (Design)", d.Estilo);
            Assert.Contains("(«Design»)", d.Motivo);
        }

        [Fact]
        public void SinEstiloPorDefectoLegibleSeTomaElPrimeroPorOrdenAlfabetico()
        {
            var r = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 1, 5, null, null);
            Assert.Equal("Contours 1m and 5m (Background)", r.Estilo);
            Assert.Contains("no se pudo leer el estilo por defecto", r.Motivo);
            Assert.Contains("orden alfabético", r.Motivo);

            // El por defecto no tiene familia: alfabético, y se dice
            var s = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 1, 5, null, "Standard");
            Assert.Equal("Contours 1m and 5m (Background)", s.Estilo);
            Assert.Contains("ninguno es de la familia del estilo por defecto del dibujo 'Standard'", s.Motivo);
        }

        [Fact]
        public void ElEstiloPorDefectoGanaSiYaTieneLosIntervalos()
        {
            var r = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 1, 5, null, "contours 1m and 5m (design)");
            Assert.Equal(EleccionEstilo.Automatica, r.Eleccion);
            Assert.Equal("Contours 1m and 5m (Design)", r.Estilo);
            Assert.Contains("además es el estilo por defecto", r.Motivo);
        }

        [Fact]
        public void UnSoloEstiloQueCoincideSeUsaSinDesempate()
        {
            var r = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 2, 10, null, "Contours 2m and 10m (Design)");
            Assert.Equal("Contours 2m and 10m (Design)", r.Estilo);
            var solo = SuperficieTin.ElegirEstilo(new List<EstiloCurvas> { Est("Curvas 0.5 y 2.5", 0.5, 2.5) }, 0.5, 2.5, null, "Otro");
            Assert.Equal("Curvas 0.5 y 2.5", solo.Estilo);
            Assert.Contains("el único del dibujo con esos intervalos", solo.Motivo);
            Assert.Empty(solo.OtrosQueCoinciden);
            Assert.True(SuperficieTin.Aplicados(0.5, 2.5, solo.Usado));
        }

        [Fact]
        public void LosEstilosQueOcultanLasCurvasNuncaSeEligen()
        {
            // Border Only va antes por orden alfabético, pero no dibuja curvas: no se elige
            var estilos = new List<EstiloCurvas> { Est("Border Only", 1, 5, false, false), Est("Curvas 1 y 5", 1, 5) };
            var r = SuperficieTin.ElegirEstilo(estilos, 1, 5, null, null);
            Assert.Equal("Curvas 1 y 5", r.Estilo);
            Assert.Equal(new[] { "Border Only" }, r.DescartadosSinCurvasVisibles);

            // Solo la mayor oculta y se pide la mayor: descartado; se pide solo la menor: vale
            var soloMenores = new List<EstiloCurvas> { Est("Solo menores", 1, 5, true, false) };
            Assert.Equal(EleccionEstilo.PorDefecto, SuperficieTin.ElegirEstilo(soloMenores, 1, 5, null, null).Eleccion);
            Assert.Equal("Solo menores", SuperficieTin.ElegirEstilo(soloMenores, 1, null, null, null).Estilo);
        }

        [Fact]
        public void LaVisibilidadDesconocidaVaDetrasYSeAvisa()
        {
            var estilos = new List<EstiloCurvas> { Est("A curvas", 1, 5, null, null), Est("B curvas", 1, 5) };
            var r = SuperficieTin.ElegirEstilo(estilos, 1, 5, null, null);
            Assert.Equal("B curvas", r.Estilo);
            Assert.Equal(new[] { "A curvas" }, r.OtrosQueCoinciden);

            var desconocidos = new List<EstiloCurvas> { Est("A curvas", 1, 5, null, null) };
            var d = SuperficieTin.ElegirEstilo(desconocidos, 1, 5, null, null);
            Assert.Equal("A curvas", d.Estilo);
            Assert.Contains("compruébalo en pantalla", d.Motivo);
            Assert.Null(SuperficieTin.CurvasVisibles(d.Usado, 1, 5));
            Assert.True(SuperficieTin.Aplicados(1, 5, d.Usado), "desconocida no impide aplicar; se avisa");
        }

        [Fact]
        public void SinNingunEstiloQueCoincidaSeQuedaElPorDefectoComoAntes()
        {
            var r = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 0.37, 3.7, null, "Contours 5m and 25m (Background)");
            Assert.Equal(EleccionEstilo.PorDefecto, r.Eleccion);
            Assert.Null(r.Estilo);
            Assert.Equal("Contours 5m and 25m (Background)", r.Usado.Nombre);
            Assert.Contains("Intervalos de curvas pedidos (menor 0.37 y mayor 3.7) sin aplicar", r.Motivo);
            Assert.Contains("no modifica ni crea estilos", r.Motivo);
            Assert.Contains("El estilo 'Contours 5m and 25m (Background)' tiene menor 5 y mayor 25", r.Motivo);
            Assert.Contains("Ningún estilo de superficie del dibujo tiene esos intervalos", r.Motivo);
            Assert.False(SuperficieTin.Aplicados(0.37, 3.7, r.Usado));

            // Estilo por defecto que no se pudo leer: el aviso sale igual, sin intervalos del estilo
            var s = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 0.37, 3.7, null, null);
            Assert.Null(s.Usado);
            Assert.Contains("sin aplicar", s.Motivo);
            Assert.DoesNotContain("tiene menor", s.Motivo);

            // Solo coinciden estilos que ocultan las curvas: se dicen como descartados
            var t = SuperficieTin.ElegirEstilo(new List<EstiloCurvas> { Est("Border Only", 1, 5, false, false) }, 1, 5, null, null);
            Assert.Equal(EleccionEstilo.PorDefecto, t.Eleccion);
            Assert.Contains("(descartados): 'Border Only'", t.Motivo);
        }

        [Fact]
        public void ElEstiloIndicadoSeRespetaSiempre()
        {
            var r = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 1, 5, "contours 5m and 25m (background)", "Contours 5m and 25m (Background)");
            Assert.Equal(EleccionEstilo.Indicada, r.Eleccion);
            Assert.Equal("Contours 5m and 25m (Background)", r.Estilo);
            Assert.Contains("sin aplicar", r.Motivo);
            Assert.Contains("El estilo 'Contours 5m and 25m (Background)' tiene menor 5 y mayor 25", r.Motivo);
            Assert.Contains("Estilos del dibujo con esos intervalos: 'Contours 1m and 5m (Background)', 'Contours 1m and 5m (Design)', 'Contours and Triangles'", r.Motivo);
            Assert.Contains("El estilo indicado se respeta y no se cambia", r.Motivo);
            Assert.False(SuperficieTin.Aplicados(1, 5, r.Usado));

            var ok = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 1, 5, "Contours 1m and 5m (Design)", "Contours 5m and 25m (Background)");
            Assert.Equal(EleccionEstilo.Indicada, ok.Eleccion);
            Assert.Contains("ya coinciden", ok.Motivo);
            Assert.True(SuperficieTin.Aplicados(1, 5, ok.Usado));
            Assert.Equal(new[] { "Contours 1m and 5m (Background)", "Contours and Triangles" }, ok.OtrosQueCoinciden);

            var oculto = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 1, 5, "Border Only", null);
            Assert.Equal("Border Only", oculto.Estilo);
            Assert.Contains("no dibuja esas curvas en planta", oculto.Motivo);
            Assert.False(SuperficieTin.Aplicados(1, 5, oculto.Usado));
        }

        [Fact]
        public void ResumenDeIntervalosParaLaRespuesta()
        {
            var r = SuperficieTin.ElegirEstilo(EstilosPlantilla(), 1, 5, null, "Contours 5m and 25m (Background)");
            var d = SuperficieTin.ResumenIntervalos(1, 5, r, r.Usado);
            Assert.Equal(1.0, d["pedido_menor"]);
            Assert.Equal(5.0, d["pedido_mayor"]);
            Assert.Equal(true, d["aplicados"]);
            Assert.Equal("Contours 1m and 5m (Background)", d["estilo"]);
            Assert.Equal("automatica", d["eleccion"]);
            Assert.Equal(r.Motivo, d["motivo"]);
            Assert.Equal(1.0, d["estilo_menor"]);
            Assert.Equal(5.0, d["estilo_mayor"]);
            Assert.Equal(true, d["curvas_visibles"]);
            Assert.Equal(0.001, d["tolerancia"]);
            Assert.Equal("SurfaceStyle.ContourStyle (estilo de superficie)", d["donde_viven"]);
            Assert.Equal(2, ((List<string>)d["otros_que_coinciden"]).Count);
            Assert.Equal(3, ((List<string>)d["descartados_sin_curvas_visibles"]).Count);

            // Sin intervalos pedidos: el bloque de siempre, aplicados=false
            var sin = SuperficieTin.ResumenIntervalos(null, null, null, Est("Contours 5m and 25m (Background)", 5, 25));
            Assert.Equal(false, sin["aplicados"]);
            Assert.Null(sin["pedido_menor"]);
            Assert.Null(sin["eleccion"]);
            Assert.Null(sin["curvas_visibles"]);
            Assert.Equal(5.0, sin["estilo_menor"]);

            // Estilo releído desconocido (null): aplicados=false
            Assert.Equal(false, SuperficieTin.ResumenIntervalos(1, 5, r, null)["aplicados"]);
        }

        [Fact]
        public void AvisoDeIntervalosConToleranciaYAlternativas()
        {
            Assert.Contains("ya coinciden", SuperficieTin.AvisoIntervalos(1, 5, "Casi", 1.0004, 5.0008));
            Assert.Contains("sin aplicar", SuperficieTin.AvisoIntervalos(1, 5, "Lejos", 1.01, 5));
            string conAlternativas = SuperficieTin.AvisoIntervalos(1, 5, "Estándar", 2, 10, new List<string> { "Curvas 1 y 5" }, null);
            Assert.Contains("Estilos del dibujo con esos intervalos: 'Curvas 1 y 5'", conAlternativas);
            string sinNinguno = SuperficieTin.AvisoIntervalos(1, 5, "Estándar", 2, 10, new List<string>(), new List<string> { "Border Only" });
            Assert.Contains("Ningún estilo de superficie del dibujo tiene esos intervalos", sinNinguno);
            Assert.Contains("(descartados): 'Border Only'", sinNinguno);
            Assert.DoesNotContain("elige con 'estilo'", sinNinguno);
        }
    }
}
