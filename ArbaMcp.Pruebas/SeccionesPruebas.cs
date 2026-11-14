using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Lógica de crear_secciones (1.13.0) que no depende de Civil 3D: validación de parámetros, progresivas de las líneas
    /// de muestreo, nombres, lectura y elección de fuentes, cuadrícula de vistas, solapes, texto de la acción y
    /// comprobación de lo releído. Datos sintéticos (el eje genérico de las validaciones va de 0 a 831.3274).
    /// </summary>
    public class SeccionesPruebas
    {
        private const double FinEje = 831.3274;

        private static JsonElement Args(string json) { using (var d = JsonDocument.Parse(json)) return d.RootElement.Clone(); }

        // ---------------------------------------------------------------- nombres
        [Fact]
        public void NombreDelGrupoPorDefectoYPedido()
        {
            Assert.Equal("Eje 1 - Muestreo", Secciones.NombreGrupo(null, "Eje 1"));
            Assert.Equal("Eje 1 - Muestreo", Secciones.NombreGrupo("  ", " Eje 1 "));
            Assert.Equal("Secciones 20 m", Secciones.NombreGrupo(" Secciones 20 m ", "Eje 1"));
        }

        [Theory]
        [InlineData(0, "0+000.00")]
        [InlineData(20, "0+020.00")]
        [InlineData(831.3274, "0+831.33")]
        [InlineData(1234.5, "1+234.50")]
        [InlineData(999.999, "1+000.00")]
        [InlineData(12000, "12+000.00")]
        [InlineData(-15.5, "-0+015.50")]
        public void NombreDeLineaConFormatoDeProgresiva(double pk, string esperado)
            => Assert.Equal(esperado, Secciones.NombreLinea(pk));

        [Fact]
        public void NombresRepetidosAlRedondearLlevanSufijo()
        {
            var n = Secciones.NombresLineas(new List<double> { 10, 10.001, 10.002, 20 });
            Assert.Equal(new[] { "0+010.00", "0+010.00 (2)", "0+010.00 (3)", "0+020.00" }, n);
        }

        [Fact]
        public void NombreDeVistaUneGrupoYLinea()
            => Assert.Equal("Eje 1 - Muestreo 0+020.00", Secciones.NombreVista("Eje 1 - Muestreo", "0+020.00"));

        // ---------------------------------------------------------------- validación
        [Fact]
        public void IntervaloObligatorioYPositivo()
        {
            Assert.Equal(20, Secciones.ValidarIntervalo(20));
            Assert.Contains("Falta el parámetro obligatorio 'intervalo'", Assert.Throws<ArgumentException>(() => Secciones.ValidarIntervalo(null)).Message);
            Assert.Contains("llegó 0", Assert.Throws<ArgumentException>(() => Secciones.ValidarIntervalo(0)).Message);
            Assert.Contains("mayor que 0", Assert.Throws<ArgumentException>(() => Secciones.ValidarIntervalo(-5)).Message);
            Assert.Throws<ArgumentException>(() => Secciones.ValidarIntervalo(double.NaN));
        }

        [Fact]
        public void AnchosObligatoriosYPositivos()
        {
            Assert.Equal(30, Secciones.ValidarAncho(30, "ancho_izquierdo"));
            var ex = Assert.Throws<ArgumentException>(() => Secciones.ValidarAncho(null, "ancho_izquierdo"));
            Assert.Equal("Falta el parámetro obligatorio 'ancho_izquierdo' (metros de franja a la izquierda del eje, por ejemplo 30).", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => Secciones.ValidarAncho(0, "ancho_derecho"));
            Assert.Equal("El 'ancho_derecho' debe ser un número mayor que 0 (metros de franja a la derecha del eje); llegó 0.", ex.Message);
        }

        [Fact]
        public void FilasPorDefectoYEnteras()
        {
            Assert.Equal(5, Secciones.ValidarFilas(null));
            Assert.Equal(3, Secciones.ValidarFilas(3));
            Assert.Equal(1, Secciones.ValidarFilas(1));
            Assert.Contains("entero mayor o igual que 1", Assert.Throws<ArgumentException>(() => Secciones.ValidarFilas(0)).Message);
            Assert.Contains("llegó 2.5", Assert.Throws<ArgumentException>(() => Secciones.ValidarFilas(2.5)).Message);
        }

        // ---------------------------------------------------------------- progresivas
        [Fact]
        public void EjeEnteroCada20IncluyeElFinal()
        {
            var r = Secciones.Progresivas(0, FinEje, null, null, 20, "Eje 1");
            Assert.Equal(43, r.Numero);   // 0, 20, ..., 820 (42) + 831.3274
            Assert.Equal(0, r.Progresivas[0]);
            Assert.Equal(820, r.Progresivas[41], 9);
            Assert.Equal(FinEje, r.Progresivas[42]);
            Assert.Equal(0, r.Inicio);
            Assert.Equal(FinEje, r.Fin);
            Assert.Single(r.Avisos);
            Assert.Equal("La última línea va en el final del tramo (831.327), a 11.327 m de la anterior (el intervalo es 20 m).", r.Avisos[0]);
        }

        [Fact]
        public void FinMultiploDelIntervaloNoRepiteLinea()
        {
            var r = Secciones.Progresivas(0, 100, null, null, 20, "Eje 1");
            Assert.Equal(new double[] { 0, 20, 40, 60, 80, 100 }, r.Progresivas);
            Assert.Empty(r.Avisos);
        }

        [Fact]
        public void UltimaMuyCercaDelFinSeSustituyePorElFin()
        {
            var r = Secciones.Progresivas(0, 100.005, null, null, 20, "Eje 1");
            Assert.Equal(new double[] { 0, 20, 40, 60, 80, 100.005 }, r.Progresivas);
        }

        [Fact]
        public void TramoParcialConInicioYFin()
        {
            var r = Secciones.Progresivas(0, FinEje, 100, 200, 25, "Eje 1");
            Assert.Equal(new double[] { 100, 125, 150, 175, 200 }, r.Progresivas);
            Assert.Empty(r.Avisos);
        }

        [Fact]
        public void FueraDelEjeAjustaOErrores()
        {
            var r = Secciones.Progresivas(0, FinEje, -0.005, FinEje + 0.005, 400, "Eje 1");
            Assert.Equal(0, r.Inicio);
            Assert.Equal(FinEje, r.Fin);
            Assert.Contains(r.Avisos, a => a.StartsWith("El 'inicio' -0.005 se ajusta al inicio del eje (0)."));
            Assert.Contains(r.Avisos, a => a.StartsWith("El 'fin' 831.332 se ajusta al final del eje (831.327)."));

            var ex = Assert.Throws<ArgumentException>(() => Secciones.Progresivas(0, FinEje, null, 900, 20, "Eje 1"));
            Assert.Equal("El 'fin' (900) queda fuera del eje 'Eje 1' (0 a 831.327): debe estar entre 0 y 831.327. Usa listar_alineamientos para ver el rango.", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => Secciones.Progresivas(0, FinEje, 500, 400, 20, "Eje 1"));
            Assert.Equal("El 'fin' (400) debe ser mayor que el 'inicio' (500).", ex.Message);
        }

        [Fact]
        public void DemasiadasLineasEsError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Secciones.Progresivas(0, FinEje, null, null, 0.5, "Eje 1"));
            Assert.Contains("el máximo por llamada es 500", ex.Message);
            Assert.Contains("Usa un intervalo mayor", ex.Message);
            Assert.Equal(500, Secciones.Progresivas(0, 499, null, null, 1, "Eje 1").Numero);
        }

        // ---------------------------------------------------------------- fuentes
        [Fact]
        public void LeerFuentesComoListaJsonTextoOSeparadas()
        {
            Assert.Empty(Secciones.LeerFuentes(Args("{}")));
            Assert.Equal(new[] { "Terreno", "Corredor 1" }, Secciones.LeerFuentes(Args("{\"fuentes\":[\"Terreno\",\" Corredor 1 \"]}")));
            Assert.Equal(new[] { "Terreno", "Corredor 1" }, Secciones.LeerFuentes(Args("{\"fuentes\":\"[\\\"Terreno\\\",\\\"Corredor 1\\\",\\\"terreno\\\"]\"}")));
            Assert.Equal(new[] { "Terreno", "Corredor 1 - Top" }, Secciones.LeerFuentes(Args("{\"fuentes\":\"Terreno; Corredor 1 - Top;\"}")));
            Assert.Throws<ArgumentException>(() => Secciones.LeerFuentes(Args("{\"fuentes\":\"[Terreno\"}")));
            Assert.Throws<ArgumentException>(() => Secciones.LeerFuentes(Args("{\"fuentes\":[1,2]}")));
        }

        private static List<FuenteSeccion> Disponibles() => new List<FuenteSeccion>
        {
            new FuenteSeccion("Terreno", Secciones.ClaseSuperficie, "TinSurface"),
            new FuenteSeccion("Corredor 1", Secciones.ClaseCorredor, "Corridor"),
            new FuenteSeccion("Corredor 1 - Top", Secciones.ClaseSuperficieCorredor, "TinSurface"),
            new FuenteSeccion("Corredor 1 - Datum", Secciones.ClaseSuperficieCorredor, "TinSurface"),
            new FuenteSeccion("Red 1", Secciones.ClaseOtra, "Pipe")
        };

        [Fact]
        public void SinPedirSeMuestreanSuperficiesYCorredores()
        {
            var avisos = new List<string>();
            var d = Disponibles();
            var elegidas = Secciones.ElegirFuentes(d, null, avisos);
            Assert.Equal(new[] { "Terreno", "Corredor 1", "Corredor 1 - Top", "Corredor 1 - Datum" }, elegidas.Select(f => f.Nombre));
            Assert.False(d.Single(f => f.Nombre == "Red 1").Muestrear);
            Assert.Single(avisos);
            Assert.StartsWith("No se muestrean Red 1 (otra)", avisos[0]);
        }

        [Fact]
        public void SinTerrenoEsError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Secciones.ElegirFuentes(new List<FuenteSeccion>(), null, new List<string>()));
            Assert.Equal("El dibujo no tiene ninguna superficie de terreno que muestrear. Crea el terreno con crear_superficie_tin. Usa listar_superficies.", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => Secciones.ElegirFuentes(new List<FuenteSeccion> { new FuenteSeccion("Corredor 1", Secciones.ClaseCorredor) }, null, new List<string>()));
            Assert.Contains("(fuentes disponibles: Corredor 1 (corredor))", ex.Message);
        }

        [Fact]
        public void PedidasSoloEsasYErrorSiNoExisten()
        {
            var avisos = new List<string>();
            var elegidas = Secciones.ElegirFuentes(Disponibles(), new List<string> { "terreno", "Corredor 1 - Datum" }, avisos);
            Assert.Equal(new[] { "Terreno", "Corredor 1 - Datum" }, elegidas.Select(f => f.Nombre));
            Assert.Empty(avisos);

            var ex = Assert.Throws<ArgumentException>(() => Secciones.ElegirFuentes(Disponibles(), new List<string> { "NoExiste" }, new List<string>()));
            Assert.StartsWith("No existe la fuente 'NoExiste' para las líneas de muestreo. Fuentes disponibles: Terreno (superficie), Corredor 1 (corredor)", ex.Message);
            Assert.EndsWith("Usa listar_superficies y listar_corredores.", ex.Message);

            avisos.Clear();
            Secciones.ElegirFuentes(Disponibles(), new List<string> { "Corredor 1" }, avisos);
            Assert.Single(avisos);
            Assert.StartsWith("Entre las 'fuentes' pedidas no hay ninguna superficie de terreno", avisos[0]);
        }

        [Fact]
        public void DiferenciaDeFuentes()
        {
            Assert.Null(Secciones.DiferenciaFuentes(new[] { "Terreno", "Corredor 1" }, new[] { "corredor 1", "Terreno" }));
            Assert.Equal("no quedaron muestreadas Corredor 1", Secciones.DiferenciaFuentes(new[] { "Terreno", "Corredor 1" }, new[] { "Terreno" }));
            Assert.Equal("no quedaron muestreadas Corredor 1; quedaron muestreadas además Terreno 2", Secciones.DiferenciaFuentes(new[] { "Terreno", "Corredor 1" }, new[] { "Terreno", "Terreno 2" }));
        }

        // ---------------------------------------------------------------- cuadrícula de vistas
        [Fact]
        public void SeparacionYColumnas()
        {
            Assert.Equal(6, Secciones.Separacion(30, 30), 9);
            Assert.Equal(5, Secciones.Separacion(10, 10), 9);
            Assert.Equal(9, Secciones.Columnas(43, 5));
            Assert.Equal(1, Secciones.Columnas(5, 5));
            Assert.Equal(0, Secciones.Columnas(0, 5));
            Assert.Equal((1, 1), CuadriculaVistas.FilaColumna(0, 5));
            Assert.Equal((5, 1), CuadriculaVistas.FilaColumna(4, 5));
            Assert.Equal((1, 2), CuadriculaVistas.FilaColumna(5, 5));
            Assert.Equal((3, 9), CuadriculaVistas.FilaColumna(42, 5));
        }

        /// <summary>Vistas sintéticas de 80 × 30 cuya extensión empieza 10 a la izquierda y 5 por debajo del punto de inserción.</summary>
        private static Extension2 Caja(Punto2 p, double ancho = 80, double alto = 30) => new Extension2(p.X - 10, p.Y - 5, p.X - 10 + ancho, p.Y - 5 + alto);

        [Fact]
        public void ApilaHaciaArribaYAbreColumnaALaDerecha()
        {
            var c = new CuadriculaVistas(new Punto2(1000, 2000), 2, 6);
            var p1 = c.Siguiente();
            Assert.Equal(new Punto2(1000, 2000), p1);
            Assert.True(c.Registrar(p1, Caja(p1), 0, 0));
            var p2 = c.Siguiente();
            // encima: borde superior 2025 + 6 de separación + 5 (del punto a la base de la vista)
            Assert.Equal(1000, p2.X, 9);
            Assert.Equal(2036, p2.Y, 9);
            c.Registrar(p2, Caja(p2, 90, 40), 0, 0);   // la segunda es más ancha
            var p3 = c.Siguiente();
            // columna nueva: borde derecho máximo de la columna (990 + 90 = 1080) + 6 + 10, a la altura de la inserción
            Assert.Equal(1096, p3.X, 9);
            Assert.Equal(2000, p3.Y, 9);
            c.Registrar(p3, Caja(p3), 0, 0);
            Assert.Equal(3, c.Colocadas);
            Assert.Equal(3, c.ExtensionesLeidas);
            Assert.Equal(0, Secciones.Solapes(c.Extensiones.ToList()));
        }

        [Fact]
        public void SinExtensionSeEstimaConLaAnterior()
        {
            var c = new CuadriculaVistas(new Punto2(0, 0), 5, 6);
            var p1 = c.Siguiente();
            Assert.False(c.Registrar(p1, null, 60, 20));   // la primera: el tamaño dado
            var p2 = c.Siguiente();
            Assert.Equal(26, p2.Y, 9);                     // 0 + 20 + 6
            Assert.False(c.Registrar(p2, new Extension2(1e20, 1e20, -1e20, -1e20), 60, 20));   // la "vacía" de AutoCAD no sirve
            Assert.Equal(52, c.Siguiente().Y, 9);
            Assert.Equal(0, c.ExtensionesLeidas);
        }

        [Fact]
        public void CuarentaYTresVistasEnNueveColumnasSinSolapes()
        {
            var c = new CuadriculaVistas(new Punto2(2000, 1900), 5, 6);
            var lugares = new List<Punto2>();
            for (int i = 0; i < 43; i++)
            {
                var p = c.Siguiente();
                lugares.Add(p);
                c.Registrar(p, Caja(p, 80, 20 + (i % 3) * 5), 0, 0);   // altos distintos según la sección
            }
            Assert.Equal(9, lugares.Select(p => Math.Round(p.X, 6)).Distinct().Count());
            Assert.Equal(0, Secciones.Solapes(c.Extensiones.ToList()));
            Assert.All(lugares.Where((p, i) => i % 5 == 0), p => Assert.Equal(1900, p.Y, 9));   // la fila de abajo alineada
        }

        [Fact]
        public void SolapesCuentaParesQueSeCruzan()
        {
            var a = new Extension2(0, 0, 10, 10);
            var b = new Extension2(5, 5, 15, 15);
            var c = new Extension2(10, 0, 20, 10);   // solo toca el borde de a
            Assert.Equal(2, Secciones.Solapes(new List<Extension2> { a, b, c }));
            Assert.Equal(0, Secciones.Solapes(new List<Extension2> { a, c }));
        }

        // ---------------------------------------------------------------- textos y comprobaciones
        [Fact]
        public void TextoDeLaAccion()
        {
            var r = Secciones.Progresivas(0, FinEje, null, null, 20, "Eje 1");
            string t = Secciones.Accion("Eje 1 - Muestreo", "Eje 1", r, 20, 30, 30, new[] { "Terreno", "Corredor 1" }, true, 5, new Punto2(1700, 1900));
            Assert.Equal("Crear el grupo de líneas de muestreo 'Eje 1 - Muestreo' en el eje 'Eje 1' con 43 líneas de 0 a 831.327 cada 20 m (30 m a la izquierda y 30 m a la derecha), muestrear Terreno, Corredor 1 y crear 43 vistas de sección en 9 columnas de 5 filas desde (1700, 1900)", t);
            string sin = Secciones.Accion("G", "Eje 1", Secciones.Progresivas(0, 100, null, null, 100, "Eje 1"), 100, 10, 25, new[] { "Terreno" }, false, 5, null);
            Assert.Equal("Crear el grupo de líneas de muestreo 'G' en el eje 'Eje 1' con 2 líneas de 0 a 100 cada 100 m (10 m a la izquierda y 25 m a la derecha), muestrear Terreno, sin vistas de sección", sin);
        }

        [Fact]
        public void LineasReleidas()
        {
            var previstas = new List<double> { 0, 20, 40 };
            Assert.Null(Secciones.MotivoLineasNoCoinciden(previstas, new List<double> { 40.004, 0, 19.999 }));
            Assert.Equal("tiene 2 líneas de muestreo y se pidieron 3", Secciones.MotivoLineasNoCoinciden(previstas, new List<double> { 0, 20 }));
            Assert.Equal("la línea 2 está en la progresiva 21 y se pidió 20", Secciones.MotivoLineasNoCoinciden(previstas, new List<double> { 0, 21, 40 }));
        }

        [Fact]
        public void AvisoDeAnchoSoloSiDifiere()
        {
            Assert.Null(Secciones.AvisoAncho("0+020.00", "izquierda", 30, 30.004));
            Assert.Null(Secciones.AvisoAncho("0+020.00", "izquierda", 30, null));
            Assert.Equal("La línea '0+020.00' quedó con 25 m a la derecha (se pidieron 30): revísala en Civil 3D (Propiedades de la línea de muestreo).", Secciones.AvisoAncho("0+020.00", "derecha", 30, 25));
        }

        // ---------------------------------------------------------------- nombres únicos en el dibujo (1.14.2)
        private static readonly List<double> Cada100 = new List<double> { 0, 100, 200, 300, 400, 500, 600, 700, 800, FinEje };

        /// <summary>Nombres de las 43 líneas de 'Eje 1 - Muestreo' (cada 20 m), como los deja el primer grupo.</summary>
        private static Dictionary<string, string> PrimerGrupo()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string n in Secciones.NombresLineas(Secciones.Progresivas(0, FinEje, null, null, 20, "Eje 1").Progresivas))
                d[n] = "grupo 'Eje 1 - Muestreo' del eje 'Eje 1'";
            return d;
        }

        [Fact]
        public void SinChoqueLosNombresNoCambian()
        {
            var r = Secciones.NombresLineasUnicos(new List<double> { 0, 20, 40 }, "Eje 1 - Muestreo", new Dictionary<string, string>());
            Assert.Equal(new[] { "0+000.00", "0+020.00", "0+040.00" }, r.Lineas);
            Assert.Equal(r.Lineas, r.Base);
            Assert.Null(r.Prefijo);
            Assert.Empty(r.Chocaban);
            Assert.Null(r.Aviso);
            // Sin diccionario (dibujo sin líneas) es lo mismo
            Assert.Equal(new[] { "0+000.00", "0+020.00", "0+040.00" }, Secciones.NombresLineasUnicos(new List<double> { 0, 20, 40 }, "G", null).Lineas);
        }

        [Fact]
        public void SegundoGrupoEnElMismoEjeLlevaElNombreDelGrupoDelanteEnTodasLasLineas()
        {
            // El caso del paso 9 de VALIDACION_113: 'Muestreo 100' cada 100 m después de 'Eje 1 - Muestreo' cada 20 m
            var r = Secciones.NombresLineasUnicos(Cada100, "Muestreo 100", PrimerGrupo());
            Assert.Equal("Muestreo 100 ", r.Prefijo);
            Assert.Equal(10, r.Lineas.Count);
            Assert.Equal("Muestreo 100 0+000.00", r.Lineas[0]);
            Assert.Equal("Muestreo 100 0+100.00", r.Lineas[1]);
            Assert.Equal("Muestreo 100 0+831.33", r.Lineas[9]);
            Assert.Equal("0+100.00", r.Base[1]);
            Assert.Equal(10, r.Chocaban.Count);   // también 0+831.33, la última de los dos grupos
            Assert.StartsWith("Ya hay líneas de muestreo con los nombres '0+000.00', '0+100.00', '0+200.00', '0+300.00', '0+400.00' y 5 más en el dibujo (grupo 'Eje 1 - Muestreo' del eje 'Eje 1') y Civil 3D no deja repetir", r.Aviso);
            Assert.Contains("las 10 líneas del grupo 'Muestreo 100' llevan delante el nombre del grupo ('Muestreo 100 0+000.00' … 'Muestreo 100 0+831.33')", r.Aviso);
        }

        [Fact]
        public void UnSoloChoquePrefijaTodoElGrupo()
        {
            var existentes = new Dictionary<string, string> { ["0+200.00"] = "espacio modelo" };
            var r = Secciones.NombresLineasUnicos(new List<double> { 0, 100, 200 }, " Otro ", existentes);
            Assert.Equal(new[] { "Otro 0+000.00", "Otro 0+100.00", "Otro 0+200.00" }, r.Lineas);
            Assert.Equal(new[] { "0+200.00" }, r.Chocaban);
            Assert.Contains("con el nombre '0+200.00' en el dibujo (espacio modelo)", r.Aviso);
        }

        [Fact]
        public void SiElNombreConPrefijoTambienChocaLlevaNumero()
        {
            var existentes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["0+000.00"] = "a",
                ["MUESTREO 100 0+000.00"] = "b",      // sin distinguir mayúsculas
                ["Muestreo 100 0+000.00 (2)"] = "c"
            };
            var r = Secciones.NombresLineasUnicos(new List<double> { 0, 100 }, "Muestreo 100", existentes);
            Assert.Equal(new[] { "Muestreo 100 0+000.00 (3)", "Muestreo 100 0+100.00" }, r.Lineas);
        }

        [Fact]
        public void ElChoqueNoDistingueMayusculasNiEspacios()
        {
            var r = Secciones.NombresLineasUnicos(new List<double> { 0 }, "G", new Dictionary<string, string> { [" 0+000.00 "] = "x" });
            Assert.Equal("G ", r.Prefijo);
            Assert.Equal(new[] { "G 0+000.00" }, r.Lineas);
        }

        [Fact]
        public void LosDuplicadosDentroDelGrupoSeConservanConPrefijo()
        {
            var r = Secciones.NombresLineasUnicos(new List<double> { 10, 10.001, 20 }, "G", new Dictionary<string, string> { ["0+020.00"] = "x" });
            Assert.Equal(new[] { "G 0+010.00", "G 0+010.00 (2)", "G 0+020.00" }, r.Lineas);
        }

        [Fact]
        public void LaVistaNoRepiteElGrupoYSoloLlevaNumeroSiChoca()
        {
            var r = Secciones.NombresLineasUnicos(new List<double> { 0, 100 }, "Muestreo 100", PrimerGrupo());
            var vistas = Secciones.NombresUnicos(r.Base.Select(b => Secciones.NombreVista("Muestreo 100", b)).ToList(), new[] { "muestreo 100 0+100.00" });
            Assert.Equal(new[] { "Muestreo 100 0+000.00", "Muestreo 100 0+100.00 (2)" }, vistas);
            Assert.DoesNotContain(vistas, v => v.Contains("Muestreo 100 Muestreo 100"));
            Assert.Equal(new[] { "a", "b" }, Secciones.NombresUnicos(new[] { "a", "b" }, null));
            Assert.Equal(new[] { "a", "a (2)" }, Secciones.NombresUnicos(new[] { "a", "a" }, new string[0]));
        }

        [Theory]
        [InlineData("Sample line name should not duplicate.", true)]
        [InlineData("El nombre está duplicado", true)]
        [InlineData("Nombre repetido", true)]
        [InlineData("eInvalidInput", false)]
        [InlineData(null, false)]
        public void ReconoceElErrorDeNombreRepetido(string mensaje, bool esperado)
            => Assert.Equal(esperado, Secciones.EsNombreRepetido(mensaje));

        [Fact]
        public void ElMensajeDeNombreRepetidoNoLlevaElTextoCrudo()
        {
            string m = Secciones.MensajeNombreRepetido("la línea de muestreo", "0+100.00", 100, 53);
            Assert.StartsWith("Civil 3D no deja crear la línea de muestreo '0+100.00' (progresiva 100): ya hay otro objeto con ese nombre en el dibujo", m);
            Assert.Contains("entre los 53 nombres que leyó", m);
            Assert.Contains("no se reintenta", m);
            Assert.DoesNotContain("should not duplicate", m);
        }
    }
}
