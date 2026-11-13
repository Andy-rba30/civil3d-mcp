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
    }
}
