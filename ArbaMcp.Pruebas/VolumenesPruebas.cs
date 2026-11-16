using System;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Lógica de calcular_volumenes (1.14.0) que no depende de Civil 3D: elección del terreno, del Datum, del corredor y
    /// del nombre; factores; resumen de corte, relleno y neto con la convención del plugin; signo del neto de Civil 3D;
    /// áreas de sección y tabla por progresivas por áreas medias. Nombres genéricos (Terreno, Corredor 1); ningún dato real.
    /// </summary>
    public class VolumenesPruebas
    {
        private static readonly List<SuperficieVolumen> SupBase = new List<SuperficieVolumen>
        {
            new SuperficieVolumen("Terreno", "TIN"),
            new SuperficieVolumen("Corredor 1 - Top", "TIN", "Corredor 1"),
            new SuperficieVolumen("Corredor 1 - Datum", "TIN", "Corredor 1")
        };

        private static readonly List<SuperficieDeCorredorVolumen> SupCorBase = new List<SuperficieDeCorredorVolumen>
        {
            new SuperficieDeCorredorVolumen("Corredor 1", "Corredor 1 - Top", true, "Top"),
            new SuperficieDeCorredorVolumen("Corredor 1", "Corredor 1 - Datum", true, "Datum")
        };

        private static EleccionVolumen Elegir(List<SuperficieVolumen> sup = null, List<string> cors = null, List<SuperficieDeCorredorVolumen> supCor = null,
            string terreno = null, string datum = null, string corredor = null, string nombre = null)
            => Volumenes.Elegir(sup ?? SupBase, cors ?? new List<string> { "Corredor 1" }, supCor ?? SupCorBase, terreno, datum, corredor, nombre);

        private static List<SuperficieVolumen> Con(params SuperficieVolumen[] extra) => SupBase.Concat(extra).ToList();

        // ---------------------------------------------------------------- elección automática
        [Fact]
        public void SinNombresEligeElUnicoTerrenoYElDatumDelUnicoCorredor()
        {
            var e = Elegir();
            Assert.Equal("Terreno", e.Terreno);
            Assert.Equal("Corredor 1 - Datum", e.Datum);
            Assert.Equal("Corredor 1", e.Corredor);
            Assert.Equal("Corredor 1 - Volumen", e.Nombre);
            Assert.True(e.TerrenoAutomatico && e.DatumAutomatico && e.CorredorAutomatico && e.NombreAutomatico);
            Assert.Empty(e.Avisos);
        }

        [Fact]
        public void ElTerrenoExcluyeSuperficiesDeCorredorDeVolumenYGrid()
        {
            var e = Elegir(Con(new SuperficieVolumen("Corredor 1 - Volumen", "TinVolume"), new SuperficieVolumen("Malla", "Grid"), new SuperficieVolumen("Vol 2", "GridVolume")), nombre: "Otro");
            Assert.Equal("Terreno", e.Terreno);
        }

        [Fact]
        public void VariosTerrenosPosiblesDanErrorConLaLista()
        {
            var ex = Assert.Throws<ArgumentException>(() => Elegir(Con(new SuperficieVolumen("Terreno 2", "TIN"))));
            Assert.Contains("varias superficies TIN", ex.Message);
            Assert.Contains("Terreno, Terreno 2", ex.Message);
            Assert.Contains("Pasa 'terreno'", ex.Message);
        }

        [Fact]
        public void SinTerrenoPosibleDaErrorConCrearSuperficieTin()
        {
            var sup = SupBase.Where(s => s.Corredor != null).ToList();
            var ex = Assert.Throws<ArgumentException>(() => Elegir(sup));
            Assert.Contains("No hay ninguna superficie TIN de terreno", ex.Message);
            Assert.Contains("crear_superficie_tin", ex.Message);
        }

        [Fact]
        public void ConTerrenoPedidoNoMiraLosDemas()
        {
            var e = Elegir(Con(new SuperficieVolumen("Terreno 2", "TIN")), terreno: " terreno 2 ");
            Assert.Equal("Terreno 2", e.Terreno);
            Assert.False(e.TerrenoAutomatico);
        }

        [Fact]
        public void TerrenoPedidoQueNoExisteOEsDeVolumen()
        {
            var ex = Assert.Throws<ArgumentException>(() => Elegir(terreno: "Nada"));
            Assert.Equal("No existe la superficie 'Nada' ('terreno'). Usa listar_superficies.", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => Elegir(Con(new SuperficieVolumen("V", "TinVolume")), terreno: "V"));
            Assert.Contains("es una superficie de volumen (TinVolume)", ex.Message);
        }

        [Fact]
        public void TerrenoDeCorredorSeAceptaConAviso()
        {
            var e = Elegir(terreno: "Corredor 1 - Top");
            Assert.Equal("Corredor 1 - Top", e.Terreno);
            Assert.Contains(e.Avisos, a => a.Contains("la genera el corredor 'Corredor 1'"));
        }

        [Fact]
        public void VariosCorredoresSinCorredorNiDatumDanError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Elegir(cors: new List<string> { "Corredor 1", "Corredor 2" }));
            Assert.Contains("varios corredores", ex.Message);
            Assert.Contains("Corredor 1, Corredor 2", ex.Message);
        }

        [Fact]
        public void SinCorredoresSinDatumDaError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Elegir(cors: new List<string>(), supCor: new List<SuperficieDeCorredorVolumen>()));
            Assert.Contains("No hay ningún corredor", ex.Message);
            Assert.Contains("crear_superficies_corredor", ex.Message);
        }

        [Fact]
        public void CorredorPedidoConVariosCorredores()
        {
            var supCor = SupCorBase.Concat(new[] { new SuperficieDeCorredorVolumen("Corredor 2", "Corredor 2 - Datum", true, "Datum") }).ToList();
            var e = Elegir(Con(new SuperficieVolumen("Corredor 2 - Datum", "TIN", "Corredor 2")), new List<string> { "Corredor 1", "Corredor 2" }, supCor, corredor: "corredor 2");
            Assert.Equal("Corredor 2 - Datum", e.Datum);
            Assert.Equal("Corredor 2", e.Corredor);
            Assert.Equal("Corredor 2 - Volumen", e.Nombre);
            Assert.False(e.CorredorAutomatico);
        }

        [Fact]
        public void CorredorPedidoQueNoExiste()
        {
            var ex = Assert.Throws<ArgumentException>(() => Elegir(corredor: "Corredor 9"));
            Assert.Equal("No existe el corredor 'Corredor 9'. Corredores del dibujo: Corredor 1. Usa listar_corredores.", ex.Message);
        }

        [Fact]
        public void DatumPorCodigoCuandoNoSeLlamaComoPorDefecto()
        {
            var supCor = new List<SuperficieDeCorredorVolumen>
            {
                new SuperficieDeCorredorVolumen("Corredor 1", "Rasante C1", true, "Top"),
                new SuperficieDeCorredorVolumen("Corredor 1", "Subrasante C1", true, "Datum")
            };
            var e = Elegir(supCor: supCor);
            Assert.Equal("Subrasante C1", e.Datum);
            Assert.Contains(e.Avisos, a => a.Contains("la única con el código de vínculo Datum"));
        }

        [Fact]
        public void CorredorSinDatumOConVariosDatum()
        {
            var ex = Assert.Throws<ArgumentException>(() => Elegir(supCor: new List<SuperficieDeCorredorVolumen> { SupCorBase[0] }));
            Assert.Contains("no tiene superficie Datum", ex.Message);
            Assert.Contains("crear_superficies_corredor", ex.Message);
            var varios = new List<SuperficieDeCorredorVolumen>
            {
                new SuperficieDeCorredorVolumen("Corredor 1", "D1", true, "Datum"),
                new SuperficieDeCorredorVolumen("Corredor 1", "D2", true, "Datum", "Top")
            };
            ex = Assert.Throws<ArgumentException>(() => Elegir(supCor: varios));
            Assert.Contains("varias superficies con el código de vínculo Datum: D1, D2", ex.Message);
        }

        [Fact]
        public void DatumSinSuperficieEnElDibujo()
        {
            var supCor = new List<SuperficieDeCorredorVolumen> { SupCorBase[0], new SuperficieDeCorredorVolumen("Corredor 1", "Corredor 1 - Datum", false, "Datum") };
            var sup = SupBase.Where(s => s.Nombre != "Corredor 1 - Datum").ToList();
            var ex = Assert.Throws<ArgumentException>(() => Elegir(sup, supCor: supCor));
            Assert.Contains("no tiene todavía su superficie en el dibujo", ex.Message);
            Assert.Contains("reconstruir_corredor", ex.Message);
            // Y pedida por nombre, el mismo mensaje (no "No existe")
            ex = Assert.Throws<ArgumentException>(() => Elegir(sup, supCor: supCor, datum: "Corredor 1 - Datum"));
            Assert.Contains("no tiene todavía su superficie en el dibujo", ex.Message);
        }

        [Fact]
        public void DatumPedidoDeOtroCorredorDaError()
        {
            var sup = Con(new SuperficieVolumen("Corredor 2 - Datum", "TIN", "Corredor 2"));
            var ex = Assert.Throws<ArgumentException>(() => Elegir(sup, new List<string> { "Corredor 1", "Corredor 2" }, datum: "Corredor 2 - Datum", corredor: "Corredor 1"));
            Assert.Contains("no es del corredor 'Corredor 1' (la genera el corredor 'Corredor 2')", ex.Message);
        }

        [Fact]
        public void DatumPedidoQueNoEsDeCorredorUsaOtroNombrePorDefecto()
        {
            var e = Elegir(Con(new SuperficieVolumen("Proyecto", "TIN")), terreno: "Terreno", datum: "Proyecto");
            Assert.Null(e.Corredor);
            Assert.Equal("Volumen Terreno - Proyecto", e.Nombre);
            Assert.Contains(e.Avisos, a => a.Contains("no la genera ningún corredor"));
        }

        [Fact]
        public void DatumPedidoDeCorredorTomaSuCorredor()
        {
            var e = Elegir(cors: new List<string> { "Corredor 1", "Corredor 2" }, datum: "corredor 1 - datum");
            Assert.Equal("Corredor 1 - Datum", e.Datum);
            Assert.Equal("Corredor 1", e.Corredor);
            Assert.False(e.DatumAutomatico);
        }

        [Fact]
        public void BaseYComparacionIgualesDanError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Elegir(terreno: "Terreno", datum: "Terreno"));
            Assert.Contains("no pueden ser la misma", ex.Message);
        }

        [Fact]
        public void NombrePedido()
        {
            var e = Elegir(nombre: "  Volumen C1  ");
            Assert.Equal("Volumen C1", e.Nombre);
            Assert.False(e.NombreAutomatico);
        }

        [Fact]
        public void NombreLibreUOcupado()
        {
            var ocupados = new Dictionary<string, string> { ["Terreno"] = null, ["Corredor 1 - Datum"] = "Corredor 1" };
            Volumenes.ComprobarNombreLibre("Corredor 1 - Volumen", ocupados);
            Volumenes.ComprobarNombreLibre("Corredor 1 - Volumen", null);
            var ex = Assert.Throws<ArgumentException>(() => Volumenes.ComprobarNombreLibre("terreno", ocupados));
            Assert.Equal("Ya existe la superficie 'terreno'. Usa listar_superficies o pasa otro 'nombre'.", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => Volumenes.ComprobarNombreLibre("Corredor 1 - Datum", ocupados));
            Assert.Contains("(la genera el corredor 'Corredor 1')", ex.Message);
            Assert.Throws<ArgumentException>(() => Volumenes.ComprobarNombreLibre("  ", ocupados));
        }

        // ---------------------------------------------------------------- factores
        [Fact]
        public void FactoresPorDefectoYValidos()
        {
            Assert.Equal(1, Volumenes.ValidarFactor(null, "factor_esponjamiento"));
            Assert.Equal(1.25, Volumenes.ValidarFactor(1.25, "factor_esponjamiento"));
            Assert.Null(Volumenes.AvisoFactor(1.25, "factor_esponjamiento"));
            Assert.Null(Volumenes.AvisoFactor(0.9, "factor_compactacion"));
            Assert.Contains("poco habitual", Volumenes.AvisoFactor(3, "factor_esponjamiento"));
            Assert.Contains("poco habitual", Volumenes.AvisoFactor(0.3, "factor_compactacion"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1.2)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(125)]
        public void FactoresNoValidos(double v)
        {
            var ex = Assert.Throws<ArgumentException>(() => Volumenes.ValidarFactor(v, "factor_compactacion"));
            Assert.Contains("'factor_compactacion'", ex.Message);
        }

        [Fact]
        public void AccionDiceBaseComparacionYFactores()
        {
            Assert.Equal("Crear la superficie de volumen TIN 'Corredor 1 - Volumen' con base 'Terreno' y comparación 'Corredor 1 - Datum', factor de esponjamiento (corte) 1.25 y de compactación (relleno) 1.1, y reconstruirla",
                Volumenes.Accion("Corredor 1 - Volumen", "Terreno", "Corredor 1 - Datum", 1.25, 1.1));
        }

        // ---------------------------------------------------------------- resumen
        [Fact]
        public void EjemploDelContrato()
        {
            // 1000 m³ de corte × 1,25 = 1250; 800 m³ de relleno × 1,10 = 880; neto +370 (sobran)
            var v = new VolumenesCivil { CorteSinAjustar = 1000, RellenoSinAjustar = 800, NetoSinAjustar = 200, CorteAjustado = 1250, RellenoAjustado = 880, NetoAjustado = 370 };
            var r = Volumenes.Resumir(v, 1.25, 1.10);
            Assert.Equal(1250, r.Corte, 6);
            Assert.Equal(880, r.Relleno, 6);
            Assert.Equal(370, r.Neto, 6);
            Assert.Equal(200, r.NetoSinAjustar, 6);
            Assert.Equal("sobra", r.Balance);
            Assert.StartsWith("Sobran 370 m³ de material (corte ajustado 1250 m³ y relleno ajustado 880 m³)", r.Texto);
            Assert.Equal("corte_menos_relleno", r.ConvencionNetoCivil3d);
            Assert.Empty(r.Avisos);
        }

        [Fact]
        public void SinAjustadosDeCivilSeCalculanConAviso()
        {
            var r = Volumenes.Resumir(new VolumenesCivil { CorteSinAjustar = 100, RellenoSinAjustar = 400, NetoSinAjustar = 300 }, 1.2, 1.1);
            Assert.Equal(120, r.Corte, 6);
            Assert.Equal(440, r.Relleno, 6);
            Assert.Equal(-320, r.Neto, 6);
            Assert.Equal("falta", r.Balance);
            Assert.StartsWith("Faltan 320 m³", r.Texto);
            Assert.Equal("relleno_menos_corte", r.ConvencionNetoCivil3d);
            Assert.Equal(2, r.Avisos.Count(a => a.Contains("No se pudo leer el")));
        }

        [Fact]
        public void AjustadoDeCivilDistintoAvisaYSeDevuelveElDeCivil()
        {
            var r = Volumenes.Resumir(new VolumenesCivil { CorteSinAjustar = 1000, RellenoSinAjustar = 800, NetoSinAjustar = 200, CorteAjustado = 800, RellenoAjustado = 880 }, 1.25, 1.1);
            Assert.Equal(800, r.Corte, 6);
            Assert.Contains(r.Avisos, a => a.Contains("AdjustedCutVolume = 800") && a.Contains("1250"));
        }

        [Fact]
        public void SinCorteORellenoLanza()
        {
            Assert.Throws<InvalidOperationException>(() => Volumenes.Resumir(new VolumenesCivil { CorteSinAjustar = 10 }, 1, 1));
            Assert.Throws<InvalidOperationException>(() => Volumenes.Resumir(null, 1, 1));
        }

        [Fact]
        public void ConvencionDelNeto()
        {
            Assert.Equal("corte_menos_relleno", Volumenes.ConvencionNeto(1000, 800, 200));
            Assert.Equal("relleno_menos_corte", Volumenes.ConvencionNeto(1000, 800, -200));
            Assert.Equal("otra", Volumenes.ConvencionNeto(1000, 800, 1800));
            Assert.Equal("indeterminada", Volumenes.ConvencionNeto(500, 500, 0));
            Assert.Equal("otra", Volumenes.ConvencionNeto(500, 500, 3));
        }

        [Fact]
        public void BalanceCompensado()
        {
            Assert.Equal("compensado", Volumenes.Balance(0.0005));
            Assert.StartsWith("Corte y relleno quedan compensados", Volumenes.TextoBalance(10, 10, 0));
        }

        // ---------------------------------------------------------------- áreas de sección
        private static List<Punto2> P(params double[] xy)
        {
            var l = new List<Punto2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) l.Add(new Punto2(xy[i], xy[i + 1]));
            return l;
        }

        [Fact]
        public void TerrenoPorEncimaEsCorte()
        {
            var a = Volumenes.Areas(P(-10, 102, 10, 102), P(-5, 100, 5, 100));
            Assert.Equal(20, a.Corte, 9);     // 10 m de ancho común × 2 m
            Assert.Equal(0, a.Relleno, 9);
            Assert.Equal(-5, a.Desde, 9);
            Assert.Equal(5, a.Hasta, 9);
        }

        [Fact]
        public void DatumPorEncimaEsRelleno()
        {
            var a = Volumenes.Areas(P(-10, 98, 10, 98), P(-5, 100, 0, 101, 5, 100));
            Assert.Equal(0, a.Corte, 9);
            Assert.Equal(25, a.Relleno, 9);   // 2 m × 10 + el triángulo de 1 m × 10 / 2
        }

        [Fact]
        public void CruceDentroDeUnTramoSeReparte()
        {
            // Terreno inclinado de 101 a 99 sobre un Datum plano a 100: corte 1×5/2 a la izquierda y relleno igual a la derecha
            var a = Volumenes.Areas(P(-5, 101, 5, 99), P(-5, 100, 5, 100));
            Assert.Equal(2.5, a.Corte, 9);
            Assert.Equal(2.5, a.Relleno, 9);
        }

        [Fact]
        public void EscalonVerticalNoSumaAreaYElOrdenSeRespeta()
        {
            // Datum con un bordillo: de 100 a 100,15 en el desplazamiento 3
            var datum = P(-3, 100, 3, 100, 3, 100.15, 5, 100.15);
            var a = Volumenes.Areas(P(-3, 101, 5, 101), datum);
            Assert.Equal(6 * 1 + 2 * 0.85, a.Corte, 9);
            Assert.Equal(0, a.Relleno, 9);
        }

        [Fact]
        public void PuntosDesordenadosSeOrdenan()
        {
            var a = Volumenes.Areas(P(10, 102, -10, 102), P(5, 100, -5, 100));
            Assert.Equal(20, a.Corte, 9);
        }

        [Fact]
        public void SinSolapeOSinPuntosDaNull()
        {
            Assert.Null(Volumenes.Areas(P(-10, 100, -6, 100), P(-5, 100, 5, 100)));
            Assert.Null(Volumenes.Areas(P(1, 100), P(-5, 100, 5, 100)));
            Assert.Null(Volumenes.Areas(null, P(-5, 100, 5, 100)));
            Assert.Null(Volumenes.Areas(P(double.NaN, 100, 2, double.NaN), P(-5, 100, 5, 100)));
        }

        [Fact]
        public void CotaEnUnDesplazamiento()
        {
            Assert.Equal(101, Volumenes.CotaEn(P(-10, 100, 10, 102), 0).Value, 9);
            Assert.Equal(100.15, Volumenes.CotaEn(P(-3, 100, 3, 100.15), 3).Value, 9);
            Assert.Null(Volumenes.CotaEn(P(-10, 100, 10, 102), 11));
        }

        [Fact]
        public void SupuestoDeEjesDeLaSeccion()
        {
            Assert.Null(Volumenes.MotivoPuntosNoValidos(P(-30, 100, 30, 101), -30, 30, 100, 101, "Terreno"));
            Assert.Null(Volumenes.MotivoPuntosNoValidos(P(-30, 100, 30, 101), 30, 30, null, null, "Terreno"));   // izquierdo sin signo
            Assert.Contains("X = 450", Volumenes.MotivoPuntosNoValidos(P(450, 100, 451, 101), -30, 30, 100, 101, "Terreno"));
            // Si X fuera la cota (100..101) y quedara dentro de ±30 no se detectaría por X, pero sí por la cota
            Assert.Contains("Y = 5", Volumenes.MotivoPuntosNoValidos(P(-5, 5, 5, 6), -30, 30, 100, 101, "Corredor 1 - Datum"));
            Assert.Contains("1 punto", Volumenes.MotivoPuntosNoValidos(P(0, 100), -30, 30, 100, 101, "Terreno"));
        }

        // ---------------------------------------------------------------- tabla por progresivas
        private static AreasSeccion A(double corte, double relleno) => new AreasSeccion { Corte = corte, Relleno = relleno };

        [Fact]
        public void TablaPorAreasMediasConFactoresYMasa()
        {
            var lineas = new List<SeccionLinea>
            {
                new SeccionLinea("0+020.00", 20, A(30, 0)),
                new SeccionLinea("0+000.00", 0, A(10, 4)),    // desordenada a propósito
                new SeccionLinea("0+040.00", 40, A(0, 20))
            };
            var t = Volumenes.Tabla(lineas, 1.25, 1.1);
            Assert.Equal(new[] { 0.0, 20, 40 }, t.Filas.Select(f => f.Progresiva));
            Assert.Null(t.Filas[0].VolumenCorte);
            Assert.Equal(400, t.Filas[1].VolumenCorte.Value, 9);     // (10 + 30) / 2 × 20
            Assert.Equal(40, t.Filas[1].VolumenRelleno.Value, 9);    // (4 + 0) / 2 × 20
            Assert.Equal(300, t.Filas[2].VolumenCorte.Value, 9);
            Assert.Equal(200, t.Filas[2].VolumenRelleno.Value, 9);
            Assert.Equal(700, t.Corte, 9);
            Assert.Equal(240, t.Relleno, 9);
            Assert.Equal(875, t.CorteAjustado, 9);
            Assert.Equal(264, t.RellenoAjustado, 9);
            Assert.Equal(611, t.Neto, 9);
            Assert.Equal(500 - 44, t.Filas[1].Masa, 9);
            Assert.Equal(611, t.Filas[2].Masa, 9);
            Assert.Equal(2, t.Tramos);
            Assert.Equal(0, t.TramosSinVolumen);
            Assert.Empty(t.Avisos);
        }

        [Fact]
        public void LineaSinAreasDejaSusTramosSinVolumen()
        {
            var lineas = new List<SeccionLinea>
            {
                new SeccionLinea("a", 0, A(10, 0)),
                new SeccionLinea("b", 20, null, "falta la sección de 'Corredor 1 - Datum'"),
                new SeccionLinea("c", 40, A(10, 0)),
                new SeccionLinea("d", 60, A(10, 0))
            };
            var t = Volumenes.Tabla(lineas, 1, 1);
            Assert.Equal(3, t.Tramos);
            Assert.Equal(2, t.TramosSinVolumen);
            Assert.Equal(200, t.Corte, 9);
            Assert.Equal("falta la sección de 'Corredor 1 - Datum'", t.Filas[1].Falta);
            Assert.Contains(t.Avisos, a => a.StartsWith("2 de 3 tramos no suman volumen") && a.Contains("0 a 20; 20 a 40"));
        }

        [Fact]
        public void TablaConMenosDeDosLineasAvisa()
        {
            var t = Volumenes.Tabla(new[] { new SeccionLinea("a", 0, A(1, 1)) }, 1, 1);
            Assert.Single(t.Filas);
            Assert.Contains(t.Avisos, a => a.Contains("al menos dos líneas"));
        }

        [Fact]
        public void DiferenciaEnPorcentaje()
        {
            Assert.Equal(5, Volumenes.DiferenciaPorcentaje(1050, 1000).Value, 9);
            Assert.Null(Volumenes.DiferenciaPorcentaje(10, 0));
        }

        // ---------------------------------------------------------------- identificación de secciones (1.14.2)
        // Ids como texto (los handles); "T" = terreno, "T2" = Terreno 2, "C" = corredor, "D" = Datum, "P" = Top.
        private static readonly SuperficieBuscada BuscaTerreno = new SuperficieBuscada("T", "Terreno");
        private static readonly SuperficieBuscada BuscaDatum = new SuperficieBuscada("D", "Corredor 1 - Datum", "Corredor 1");

        /// <summary>
        /// Las cinco secciones de cada línea en la validación 1.13.0 (informe del 14/11/2026, pregunta 2), con lo observado en
        /// la 1.14.0: SourceName "&lt;corredor&gt; &lt;superficie&gt;" en las de corredor y SourceId que no es la superficie (aquí,
        /// el corredor). CorridorSurfaceId sin leer.
        /// </summary>
        private static List<SeccionCandidata> CincoSecciones() => new List<SeccionCandidata>
        {
            new SeccionCandidata("s0", "T", null, "Terreno"),
            new SeccionCandidata("s1", "T2", null, "Terreno 2"),
            new SeccionCandidata("s2", "C", null, "Corredor 1"),
            new SeccionCandidata("s3", "C", null, "Corredor 1 Corredor 1 - Top"),
            new SeccionCandidata("s4", "C", null, "Corredor 1 Corredor 1 - Datum")
        };

        [Fact]
        public void LaDatumSeEncuentraPorElNombreConElCorredorDelante()
        {
            var (t, d) = IdentificacionSecciones.IdentificarLasDos(CincoSecciones(), BuscaTerreno, null, BuscaDatum, null);
            Assert.Equal(0, t.Indice);
            Assert.Equal(IdentificacionSecciones.PorSourceId, t.Metodo);
            Assert.Equal(4, d.Indice);   // ni la del corredor (2) ni la de la Top (3)
            Assert.Equal(IdentificacionSecciones.PorNombreConCorredor, d.Metodo);
        }

        [Fact]
        public void GetSectionIdGanaSiEsUnaSeccionDeLaLinea()
        {
            var (t, d) = IdentificacionSecciones.IdentificarLasDos(CincoSecciones(), BuscaTerreno, "s0", BuscaDatum, "S4");
            Assert.Equal((0, IdentificacionSecciones.PorGetSectionId), (t.Indice, t.Metodo));
            Assert.Equal((4, IdentificacionSecciones.PorGetSectionId), (d.Indice, d.Metodo));
            // Si GetSectionId devuelve una sección que no es de la línea, se ignora y se sigue con el siguiente intento
            var otra = IdentificacionSecciones.Identificar(CincoSecciones(), BuscaDatum, "s99");
            Assert.Equal((4, IdentificacionSecciones.PorNombreConCorredor), (otra.Indice, otra.Metodo));
        }

        [Fact]
        public void CorridorSurfaceIdVaAntesQueSourceIdYQueElNombre()
        {
            var l = CincoSecciones();
            l[3].SuperficieCorredorId = "P";
            l[4].SuperficieCorredorId = "D";
            var d = IdentificacionSecciones.Identificar(l, BuscaDatum, null);
            Assert.Equal((4, IdentificacionSecciones.PorCorridorSurfaceId), (d.Indice, d.Metodo));
        }

        [Fact]
        public void SourceIdIgualALaSuperficie()
        {
            var l = new List<SeccionCandidata> { new SeccionCandidata("a", "C", null, "x"), new SeccionCandidata("b", "D", null, "y") };
            Assert.Equal((1, IdentificacionSecciones.PorSourceId), (IdentificacionSecciones.Identificar(l, BuscaDatum, null).Indice, IdentificacionSecciones.Identificar(l, BuscaDatum, null).Metodo));
        }

        [Fact]
        public void NombreExactoSinMayusculasNiEspaciosDeMas()
        {
            var l = new List<SeccionCandidata> { new SeccionCandidata("a", null, null, "Terreno 2"), new SeccionCandidata("b", null, null, "  terreno ") };
            var t = IdentificacionSecciones.Identificar(l, BuscaTerreno, null);
            Assert.Equal((1, IdentificacionSecciones.PorNombre), (t.Indice, t.Metodo));   // "Terreno 2" no es "Terreno"
            Assert.True(IdentificacionSecciones.NombreConCorredorCoincide("Corredor 1   Corredor 1 - Datum", "Corredor 1", "Corredor 1 - Datum"));
            Assert.False(IdentificacionSecciones.NombreConCorredorCoincide("Corredor 1 Corredor 1 - Datum", null, "Corredor 1 - Datum"));
            Assert.False(IdentificacionSecciones.NombreCoincide("Corredor 1 Corredor 1 - Datum", "Corredor 1"));
        }

        [Fact]
        public void LaDatumNuncaTomaLaSeccionDelTerreno()
        {
            // Datos raros: la única sección cumple las dos cosas (SourceId del terreno y CorridorSurfaceId de la Datum)
            var l = new List<SeccionCandidata> { new SeccionCandidata("a", "T", "D", "Terreno") };
            var (t, d) = IdentificacionSecciones.IdentificarLasDos(l, BuscaTerreno, null, BuscaDatum, null);
            Assert.Equal(0, t.Indice);
            Assert.False(d.Encontrada);
            Assert.Null(d.Metodo);
        }

        [Fact]
        public void SinSeccionDeLaSuperficie()
        {
            var l = new List<SeccionCandidata> { new SeccionCandidata("s0", "T", null, "Terreno"), null };
            Assert.False(IdentificacionSecciones.Identificar(l, BuscaDatum, null).Encontrada);
            Assert.False(IdentificacionSecciones.Identificar(null, BuscaDatum, null).Encontrada);
            Assert.False(IdentificacionSecciones.Identificar(l, null, null).Encontrada);
        }

        // ---------------------------------------------------------------- supuesto de ejes (1.14.3)
        [Fact]
        public void ElCasoRealYaNoBloquea()
        {
            // Informe 1.14.2 del 14/11/2026, paso 8: sección de la Datum con puntos de −30 a 30 en una línea de 30/30 y
            // LeftOffset/RightOffset −3.9 y 3.6. Con los offsets sale la diferencia (solo diagnóstico); con el ancho de la línea, nada
            var datum = P(-30, 108.4, -3.9, 109.0, 0, 109.08, 3.6, 109.0, 30, 108.5);
            Assert.Contains("X = -30", Volumenes.MotivoPuntosNoValidos(datum, -3.9, 3.6, null, null, "Corredor 1 - Datum"));
            Assert.Null(Volumenes.MotivoFueraDeLaLinea(datum, 30, 30, "Corredor 1 - Datum"));
        }

        [Fact]
        public void PuntosFueraDelAnchoDeLaLinea()
        {
            Assert.Null(Volumenes.MotivoFueraDeLaLinea(P(-30.005, 100, 30.004, 101), 30, 30, "Terreno"));   // ±0,01
            Assert.Equal("un punto de la sección de 'Terreno' tiene X = 31, fuera del ancho de la línea de muestreo (30 m a la izquierda y 30 m a la derecha)",
                Volumenes.MotivoFueraDeLaLinea(P(-30, 100, 31, 101), 30, 30, "Terreno"));
            Assert.Contains("X = -12", Volumenes.MotivoFueraDeLaLinea(P(-12, 100, 25, 101), 10, 25, "Terreno"));   // 10 a la izquierda, 25 a la derecha
            Assert.Contains("X = 1300", Volumenes.MotivoFueraDeLaLinea(P(1300, 100, 1330, 101), 30, 30, "Terreno"));   // coordenadas absolutas
            Assert.Contains("X = -1", Volumenes.MotivoFueraDeLaLinea(P(-1, 100, 20, 101), null, 25, "Terreno"));   // lado sin ancho = 0
            Assert.Null(Volumenes.MotivoFueraDeLaLinea(P(-1000, 100, 1000, 101), null, null, "Terreno"));   // sin anchos, no se comprueba
        }

        [Fact]
        public void DesplazamientosDeComprobacion()
        {
            Assert.Equal(new[] { 0.0, -5, 5 }, Volumenes.DesplazamientosDeComprobacion(-30, 30));
            Assert.Equal(new[] { 0.0, -1.5, 5 }, Volumenes.DesplazamientosDeComprobacion(-3, 25));   // la mitad si a ese lado hay menos de 10 m
            Assert.Equal(new[] { 4.0 }, Volumenes.DesplazamientosDeComprobacion(2, 8));   // el eje no está en el tramo: solo la derecha
            Assert.Empty(Volumenes.DesplazamientosDeComprobacion(5, 5));
            Assert.Empty(Volumenes.DesplazamientosDeComprobacion(double.NaN, 5));
        }

        [Fact]
        public void ComparacionDeCotas()
        {
            var bien = new List<ComparacionCota>
            {
                new ComparacionCota("Terreno", 0, 104.1867, 104.1900),
                new ComparacionCota("Corredor 1 - Datum", 0, 109.0011, 109.0011),
                new ComparacionCota("Corredor 1 - Datum", -5, null, 108.9),   // fuera de la sección: no cuenta
                new ComparacionCota("Terreno", 5, 104.2, null)                 // fuera de la TIN: no cuenta
            };
            Assert.True(Volumenes.ComprobacionCompleta(bien, "Terreno", "Corredor 1 - Datum"));
            Assert.Null(Volumenes.MotivoCotasNoCoinciden(bien, "0+400.00", 400));
            Assert.False(Volumenes.ComprobacionCompleta(bien.Where(c => c.Superficie == "Terreno").ToList(), "Terreno", "Corredor 1 - Datum"));
            Assert.Equal(-0.0033, bien[0].Diferencia.Value, 9);
            Assert.Null(bien[2].Diferencia);

            // El signo izquierda/derecha cambiado: en −5 la sección da lo que la superficie tiene en +5
            var mal = new List<ComparacionCota> { new ComparacionCota("Terreno", 0, 104.1867, 104.1867), new ComparacionCota("Terreno", -5, 104.25, 104.12) };
            Assert.Equal("en la línea '0+400.00' (pk 400) la sección de 'Terreno' da cota 104.25 a 5 m a la izquierda del eje y la superficie da 104.12: SectionPoint.Location no es (desplazamiento, cota) o el signo de los desplazamientos está cambiado",
                Volumenes.MotivoCotasNoCoinciden(mal, "0+400.00", 400));
            Assert.Equal("en la línea 'L' (pk 0) la sección de 'D' da cota 110 en el eje y la superficie da 109: SectionPoint.Location no es (desplazamiento, cota)",
                Volumenes.MotivoCotasNoCoinciden(new[] { new ComparacionCota("D", 0, 110, 109) }, "L", 0));
        }

        [Fact]
        public void RecuentoPorMetodo()
        {
            var r = new RecuentoIdentificacion();
            r.Anotar(new SeccionIdentificada { Indice = 0, Metodo = IdentificacionSecciones.PorGetSectionId });
            r.Anotar(new SeccionIdentificada { Indice = 2, Metodo = IdentificacionSecciones.PorGetSectionId });
            r.Anotar(new SeccionIdentificada { Indice = 1, Metodo = IdentificacionSecciones.PorNombre });
            r.Anotar(new SeccionIdentificada());
            Assert.Equal(4, r.Lineas);
            Assert.Equal(1, r.SinSeccion);
            Assert.Equal(2, r.Metodos[IdentificacionSecciones.PorGetSectionId]);
            Assert.Equal(1, r.Metodos[IdentificacionSecciones.PorNombre]);
            Assert.Equal(5, IdentificacionSecciones.Orden.Count);
        }

        // ---------------------------------------------------------------- recorte a las superficies (1.17.1)
        /// <summary>Función «existe en x» que cuenta cuántas veces se le pregunta.</summary>
        private sealed class Existe
        {
            private readonly Func<double, bool> f;
            public int Llamadas;
            public Existe(Func<double, bool> f) { this.f = f; }
            public bool En(double x) { Llamadas++; return f(x); }
        }

        private static Func<double, bool> Entre(params double[] bordes)
            => x => { for (int i = 0; i + 1 < bordes.Length; i += 2) if (x >= bordes[i] && x <= bordes[i + 1]) return true; return false; };

        [Fact]
        public void MuestrasConPuntosMediosYPasoMaximo()
        {
            var m = Volumenes.MuestrasRecorte(-10, 10, new[] { 0.0, 0.0, 50, double.NaN });
            // −10, 0 y 10 (50 queda fuera), sus puntos medios (−5 y 5) y nada más: ningún hueco pasa de 5
            Assert.Equal(new[] { -10.0, -5, 0, 5, 10 }, m);
            var largo = Volumenes.MuestrasRecorte(0, 30, null);   // 0, 15 y 30, y cada hueco de 15 en tres de 5
            Assert.Equal(new[] { 0.0, 5, 10, 15, 20, 25, 30 }, largo.Select(x => Math.Round(x, 9)));
            Assert.Empty(Volumenes.MuestrasRecorte(1, 1, null));
        }

        [Fact]
        public void TodoDentroNoSeRecorta()
        {
            var e = new Existe(x => true);
            var r = Volumenes.Recortar(-30, 30, new[] { 0.0 }, e.En);
            Assert.Single(r.Tramos);
            Assert.Equal((-30.0, 30.0), (r.Tramos[0].Desde, r.Tramos[0].Hasta));
            Assert.Equal(0, r.AnchoDescartado, 9);
            Assert.False(r.Recortada);
            Assert.Equal(e.Llamadas, r.PuntosProbados);   // sin bordes no hay bisección: solo las muestras
            Assert.Equal(Volumenes.MuestrasRecorte(-30, 30, new[] { 0.0 }).Count, r.PuntosProbados);
        }

        [Fact]
        public void BordesAfinadosHastaUnCentimetroDelLadoDeDentro()
        {
            var e = new Existe(Entre(-12.345, 14.4));
            var r = Volumenes.Recortar(-30, 30, new[] { -3.9, 0, 3.6, 22.76 }, e.En);
            Assert.Single(r.Tramos);
            var t = r.Tramos[0];
            Assert.InRange(t.Desde, -12.345, -12.345 + Volumenes.PrecisionRecorte);
            Assert.InRange(t.Hasta, 14.4 - Volumenes.PrecisionRecorte, 14.4);
            Assert.InRange(r.AnchoDescartado, 60 - 26.745, 60 - 26.745 + 2 * Volumenes.PrecisionRecorte);
            Assert.True(r.Recortada);
            Assert.Equal(e.Llamadas, r.PuntosProbados);   // cada desplazamiento se pregunta una vez
            Assert.InRange(r.PuntosProbados, 1, 60);
        }

        [Fact]
        public void VariosTramosSeparados()
        {
            // La Datum existe a los dos lados de un hueco (−5 a 3) y no más allá de ±20
            var r = Volumenes.Recortar(-30, 30, null, Entre(-20, -5, 3, 20));
            Assert.Equal(2, r.Tramos.Count);
            Assert.InRange(r.Tramos[0].Desde, -20, -20 + 0.01);
            Assert.InRange(r.Tramos[0].Hasta, -5 - 0.01, -5);
            Assert.InRange(r.Tramos[1].Desde, 3, 3 + 0.01);
            Assert.InRange(r.Tramos[1].Hasta, 20 - 0.01, 20);
            Assert.InRange(r.AnchoValido, 32 - 0.04, 32);
        }

        [Fact]
        public void NadaDentroOFuncionQueLanza()
        {
            var r = Volumenes.Recortar(-30, 30, new[] { 0.0 }, x => false);
            Assert.Empty(r.Tramos);
            Assert.Equal(60, r.AnchoDescartado, 9);
            // Si la función lanza (FindElevationAtXY fuera de la TIN), ese punto cuenta como fuera
            var l = Volumenes.Recortar(-10, 10, null, x => x < 0 ? throw new InvalidOperationException("fuera de la TIN") : true);
            Assert.Single(l.Tramos);
            Assert.InRange(l.Tramos[0].Desde, 0, 0.01);
            Assert.Throws<ArgumentNullException>(() => Volumenes.Recortar(0, 1, null, null));
        }

        [Fact]
        public void SinSolapeNoHayRecorte()
            => Assert.Null(Volumenes.RecortarSeccion(P(-10, 100, -6, 100), P(-5, 100, 5, 100), x => true));

        [Fact]
        public void AreasSoloDentroDeLosTramosValidos()
        {
            // Terreno 2 m por encima de la Datum en todo el ancho: el corte es 2 × el ancho válido
            var t = P(-30, 102, 30, 102);
            var d = P(-30, 100, 30, 100);
            Assert.Equal(120, Volumenes.Areas(t, d).Corte, 9);
            Assert.Equal(120, Volumenes.Areas(t, d, null).Corte, 9);   // sin recorte, como hasta la 1.14.3
            var uno = Volumenes.Areas(t, d, new[] { new TramoDesplazamientos(-10, 5) });
            Assert.Equal(30, uno.Corte, 9);
            Assert.Equal((-30.0, 30.0), (uno.Desde, uno.Hasta));        // Desde y Hasta siguen siendo el tramo común
            var dos = Volumenes.Areas(t, d, new[] { new TramoDesplazamientos(-20, -5), new TramoDesplazamientos(3, 20) });
            Assert.Equal(2 * (15 + 17), dos.Corte, 9);
            var vacio = Volumenes.Areas(t, d, new TramoDesplazamientos[0]);
            Assert.Equal((0.0, 0.0), (vacio.Corte, vacio.Relleno));
        }

        [Fact]
        public void CruceDentroDeUnTramoValidoRecortado()
        {
            // Terreno de 101 a 99 sobre una Datum plana a 100, con la Datum solo de −2 a 5: corte 0,4 × 2 / 2, relleno 1 × 5 / 2
            var a = Volumenes.Areas(P(-5, 101, 5, 99), P(-5, 100, 5, 100), new[] { new TramoDesplazamientos(-2, 5) });
            Assert.Equal(0.4, a.Corte, 9);
            Assert.Equal(2.5, a.Relleno, 9);
        }

        /// <summary>
        /// La línea 0+000.00 de PRUEBA_1142.dwg (informe 1.14.3 del 14/11/2026, puntos_secciones): terreno con sus 3 puntos y
        /// Datum con los 6 puntos conocidos (los 3 primeros y los 3 últimos) más los 3 centrales reconstruidos: bombeo del 2 %
        /// con 99.55 en el eje (la cota medida) y 99.478 a ±3.6 (MinmumElevation). Dan las cotas que midió el informe en −5 y +5
        /// (100.1045 y 99.5379) y las áreas sin recorte de su primera fila (5.1339 y 7.521).
        /// </summary>
        private static readonly List<Punto2> TerrenoLinea0 = P(-30, 100.6, 0, 100, 30, 99.4);
        private static readonly List<Punto2> DatumLinea0 = P(-30, 100.7066, -3.9, 100.078, -3.6002, 99.9281, -3.6, 99.478, 0, 99.55, 3.6, 99.478,
            14.0508, 99.925, 22.7597, 99.9224, 30, 99.9203);

        [Fact]
        public void CasoRealDeLaLinea0()
        {
            Assert.Equal(99.55, Volumenes.CotaEn(DatumLinea0, 0).Value, 3);
            Assert.Equal(100.1045, Volumenes.CotaEn(DatumLinea0, -5).Value, 3);
            Assert.Equal(99.5379, Volumenes.CotaEn(DatumLinea0, 5).Value, 3);

            // Sin recorte salen las áreas de la fila 0+000.00 del informe 1.14.3
            var sin = Volumenes.Areas(TerrenoLinea0, DatumLinea0);
            Assert.InRange(sin.Corte, 5.1339 - 0.001, 5.1339 + 0.001);
            Assert.InRange(sin.Relleno, 7.521 - 0.001, 7.521 + 0.001);
            // En el informe FindElevationAtXY dio cota de la Datum en el eje y null a ±5 m: bordes supuestos a −3.75 y 3.75
            var e = new Existe(Entre(-3.75, 3.75));
            var r = Volumenes.RecortarSeccion(TerrenoLinea0, DatumLinea0, e.En);
            Assert.Equal((-30.0, 30.0), (r.Desde, r.Hasta));
            Assert.Single(r.Tramos);
            Assert.InRange(r.Tramos[0].Desde, -3.75, -3.75 + 0.01);
            Assert.InRange(r.Tramos[0].Hasta, 3.75 - 0.01, 3.75);
            Assert.InRange(r.AnchoDescartado, 52.5, 52.52);
            Assert.InRange(r.PuntosProbados, 1, 60);       // «unas decenas» por línea
            Assert.Equal(e.Llamadas, r.PuntosProbados);

            var con = Volumenes.Areas(TerrenoLinea0, DatumLinea0, r.Tramos);
            Assert.True(con.Corte < sin.Corte && con.Relleno < sin.Relleno);
            // Lo que queda es solo la calzada: el terreno (≈100) por encima de la Datum (≈99.5 a 99.9) y nada de relleno
            double fuera = Volumenes.Areas(TerrenoLinea0, DatumLinea0, new[] { new TramoDesplazamientos(-30, -3.75), new TramoDesplazamientos(3.75, 30) }).Corte;
            Assert.InRange(con.Corte - (sin.Corte - fuera), -0.02, 0.02);   // los bordes se afinan hasta 0,01 m
            Assert.True(con.Relleno < 0.01);
        }

        [Fact]
        public void SiSoloExisteEnElEjeElAreaEsCero()
        {
            var r = Volumenes.RecortarSeccion(TerrenoLinea0, DatumLinea0, x => Math.Abs(x) < 1e-12);
            Assert.Empty(r.Tramos);   // un tramo de ancho 0 no cuenta
            var a = Volumenes.Areas(TerrenoLinea0, DatumLinea0, r.Tramos);
            Assert.Equal((0.0, 0.0), (a.Corte, a.Relleno));
        }

        [Fact]
        public void ResumenDelRecorte()
        {
            RecorteSeccion R(double desde, double hasta, int puntos, params double[] tramos)
            {
                var r = new RecorteSeccion { Desde = desde, Hasta = hasta, PuntosProbados = puntos };
                for (int i = 0; i + 1 < tramos.Length; i += 2) r.Tramos.Add(new TramoDesplazamientos(tramos[i], tramos[i + 1]));
                return r;
            }
            var s = Volumenes.ResumirRecorte(new[]
            {
                R(-30, 30, 20, -30, 30),                 // sin recorte
                R(-30, 30, 40, -10, 14),                 // descarta 36
                R(-30, 30, 50, -20, -5, 3, 20),          // dos tramos, descarta 28
                R(-30, 30, 30),                          // sin tramo válido, descarta 60
                null
            });
            Assert.Equal(4, s.Lineas);
            Assert.Equal(3, s.LineasRecortadas);
            Assert.Equal(1, s.LineasSinTramoValido);
            Assert.Equal(1, s.LineasConVariosTramos);
            Assert.Equal(140, s.PuntosProbados);
            Assert.Equal((36 + 28 + 60) / 3.0, s.AnchoMedioDescartado, 9);
            Assert.Equal(60, s.AnchoMaximoDescartado, 9);
            Assert.Equal(0, Volumenes.ResumirRecorte(null).AnchoMedioDescartado, 9);
        }

        [Fact]
        public void TablaConRecorteDaElAnchoDescartadoYAvisaDeLasLineasSinTramo()
        {
            var vacio = new RecorteSeccion { Desde = -30, Hasta = 30 };
            var parcial = new RecorteSeccion { Desde = -30, Hasta = 30 };
            parcial.Tramos.Add(new TramoDesplazamientos(-10, 14));
            var lineas = new List<SeccionLinea>
            {
                new SeccionLinea("0+000.00", 0, A(0, 0)) { Recorte = vacio },
                new SeccionLinea("0+020.00", 20, A(10, 2)) { Recorte = parcial },
                new SeccionLinea("0+040.00", 40, A(10, 2))
            };
            var t = Volumenes.Tabla(lineas, 1, 1);
            Assert.Equal(60, t.Filas[0].AnchoDescartado.Value, 9);
            Assert.Equal(36, t.Filas[1].AnchoDescartado.Value, 9);
            Assert.Null(t.Filas[2].AnchoDescartado);
            Assert.Equal(0, t.TramosSinVolumen);        // una línea sin tramo válido suma 0, no es un tramo sin volumen
            Assert.Equal(100 + 200, t.Corte, 9);       // (0 + 10) / 2 × 20 + (10 + 10) / 2 × 20
            Assert.Contains(t.Avisos, a => a.StartsWith("1 línea (de pk 0) no tiene ningún tramo en que existan las dos superficies"));
        }
    }
}
