using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Geometría de crear_alineamiento_desde_polilinea que no depende de Civil 3D: deflexiones, tangentes, longitudes de
    /// curva, si cabe cada curva, radio máximo, progresivas y lectura de 'radios_por_pi'. Polilíneas sintéticas en
    /// coordenadas genéricas (ningún dato real).
    /// </summary>
    public class AlineamientoPruebas
    {
        private static List<Punto2> Puntos(params double[] xy)
        {
            var l = new List<Punto2>();
            for (int i = 0; i < xy.Length; i += 2) l.Add(new Punto2(xy[i], xy[i + 1]));
            return l;
        }

        private static JsonElement Json(string texto)
        {
            using (var d = JsonDocument.Parse(texto)) return d.RootElement.Clone();
        }

        // ---------------------------------------------------------------- tres vértices, curva que cabe
        [Fact]
        public void TresVerticesConCurvaQueCabe()
        {
            // Giro de 90° a la izquierda en (100, 0); tramos de 100 y 100; radio 50 → T = 50, L = 78,54
            var plan = Alineamiento.Planificar(Puntos(0, 0, 100, 0, 100, 100), 50);
            Assert.Equal(1, plan.NumeroPi);
            Assert.Equal(1, plan.NumeroCurvas);
            Assert.Empty(plan.Avisos);
            var pi = plan.Pis[0];
            Assert.Equal(1, pi.Numero);
            Assert.Equal(90.0, pi.DeflexionGrados, 6);
            Assert.Equal("izquierda", pi.Giro);
            Assert.Equal(50.0, pi.RadioPedido);
            Assert.Equal(50.0, pi.RadioAplicado);
            Assert.Equal(50.0, pi.Tangente.Value, 6);
            Assert.Equal(Math.PI * 25, pi.LongitudCurva.Value, 6);          // 50 · π/2
            Assert.Equal(50.0, pi.ProgresivaPc.Value, 6);
            Assert.Equal(100.0, pi.Progresiva, 6);                             // PC + T
            Assert.Equal(50 + Math.PI * 25, pi.ProgresivaPt.Value, 6);
            Assert.Equal(100.0, pi.RadioMaximo.Value, 3);                      // min(100, 100) / tan(45°)
            Assert.Null(pi.Aviso);
            Assert.Equal(200.0, plan.LongitudPolilinea, 6);
            Assert.Equal(200 - 100 + Math.PI * 25, plan.LongitudTotal, 6);     // polilínea − (2T − L)
            Assert.Equal(0.0, plan.ProgresivaInicial);
            Assert.Equal(plan.LongitudTotal, plan.ProgresivaFinal, 6);
        }

        [Fact]
        public void GiroALaDerechaYProgresivaInicialDistintaDeCero()
        {
            var plan = Alineamiento.Planificar(Puntos(0, 0, 100, 0, 100, -100), 50, null, 1000);
            Assert.Equal("derecha", plan.Pis[0].Giro);
            Assert.Equal(1050.0, plan.Pis[0].ProgresivaPc.Value, 6);
            Assert.Equal(1000.0, plan.ProgresivaInicial);
            Assert.Equal(1000 + plan.LongitudTotal, plan.ProgresivaFinal, 6);
        }

        // ---------------------------------------------------------------- radio que no cabe
        [Fact]
        public void RadioQueNoCabeDejaElPiSinCurvaYAvisaElRadioMaximo()
        {
            // Radio 150 → T = 150 > tramos de 100: sin curva; el radio máximo que cabe es 100 / tan(45°) = 100
            var plan = Alineamiento.Planificar(Puntos(0, 0, 100, 0, 100, 100), 150);
            var pi = plan.Pis[0];
            Assert.Null(pi.RadioAplicado);
            Assert.Null(pi.Tangente);
            Assert.Null(pi.LongitudCurva);
            Assert.Null(pi.ProgresivaPc);
            Assert.Equal(150.0, pi.RadioPedido);
            Assert.Equal(100.0, pi.RadioMaximo.Value, 3);
            Assert.NotNull(pi.Aviso);
            Assert.Contains("no cabe", pi.Aviso);
            Assert.Contains("Radio máximo que cabría: 100", pi.Aviso);
            Assert.Single(plan.Avisos);
            Assert.Equal(0, plan.NumeroCurvas);
            Assert.Equal(100.0, pi.Progresiva, 6);        // sin curva, el PI está en el vértice
            Assert.Equal(200.0, plan.LongitudTotal, 6);   // el eje mide lo que la polilínea
        }

        // ---------------------------------------------------------------- dos curvas consecutivas que compiten por un tramo corto
        [Fact]
        public void DosCurvasConsecutivasEnUnTramoCortoConservanLaPrimera()
        {
            // Tramo central de 60 entre dos giros de 90°; radio 50 → T = 50 cada una: solo cabe la primera
            var plan = Alineamiento.Planificar(Puntos(0, 0, 100, 0, 100, 60, 200, 60), 50);
            Assert.Equal(2, plan.NumeroPi);
            Assert.Equal(1, plan.NumeroCurvas);
            var pi1 = plan.Pis[0]; var pi2 = plan.Pis[1];
            Assert.Equal(50.0, pi1.RadioAplicado);
            Assert.Null(pi2.RadioAplicado);
            Assert.Equal(10.0, pi2.RadioMaximo.Value, 3);   // queda 60 − 50 = 10 libre antes del PI 2
            Assert.Contains("PI 2", pi2.Aviso);
            Assert.Contains("libre 10 m antes", pi2.Aviso);
            Assert.Contains("Radio máximo que cabría: 10", pi2.Aviso);
            // Progresivas: PC1 = 50, PT1 = 50 + 78,54; PI 2 = PT1 + (60 − 50); fin = PI 2 + 100
            Assert.Equal(50.0, pi1.ProgresivaPc.Value, 6);
            Assert.Equal(pi1.ProgresivaPt.Value + 10, pi2.Progresiva, 6);
            Assert.Equal(pi2.Progresiva + 100, plan.ProgresivaFinal, 6);
            Assert.Equal(260 - 100 + Math.PI * 25, plan.LongitudTotal, 6);
        }

        [Fact]
        public void DosCurvasConsecutivasQueSiCabenCompartenElTramo()
        {
            // Tramo central de 100 y tangentes de 50 + 50 = 100: justo cabe (con tolerancia)
            var plan = Alineamiento.Planificar(Puntos(0, 0, 100, 0, 100, 100, 200, 100), 50);
            Assert.Equal(2, plan.NumeroCurvas);
            Assert.Empty(plan.Avisos);
            Assert.Equal(plan.Pis[0].ProgresivaPt.Value, plan.Pis[1].ProgresivaPc.Value, 6);   // PT1 = PC2: sin recta entre ellas
        }

        // ---------------------------------------------------------------- radios por PI mixtos
        [Fact]
        public void RadiosPorPiMixtosConCeroYRadioDistinto()
        {
            // 5 vértices → 3 PI; radios [80 (defecto), 0 (sin curva), 40]
            var v = Puntos(0, 0, 200, 0, 200, 200, 400, 200, 400, 400);
            var radios = Alineamiento.RadiosPorPi(Json("[{\"pi\": 2, \"radio\": 0}, {\"pi\": 3, \"radio\": 40}]"), 3, 80);
            Assert.Equal(new List<double> { 80, 0, 40 }, radios);
            var plan = Alineamiento.Planificar(v, 80, radios);
            Assert.Equal(3, plan.NumeroPi);
            Assert.Equal(2, plan.NumeroCurvas);
            Assert.Equal(80.0, plan.Pis[0].RadioAplicado);
            Assert.Null(plan.Pis[1].RadioAplicado);
            Assert.Null(plan.Pis[1].Aviso);                 // radio 0 pedido: sin curva y sin aviso
            Assert.Equal(0.0, plan.Pis[1].RadioPedido);
            Assert.Equal(40.0, plan.Pis[2].RadioAplicado);
            Assert.Empty(plan.Avisos);
            // PI 2 sin curva está en su vértice: PT1 + (200 − 80)
            Assert.Equal(plan.Pis[0].ProgresivaPt.Value + 120, plan.Pis[1].Progresiva, 6);
            Assert.Equal(plan.Pis[1].Progresiva + 200 - 40, plan.Pis[2].ProgresivaPc.Value, 6);
        }

        [Fact]
        public void RadiosPorPiComoListaSimpleEnOrden()
        {
            var radios = Alineamiento.RadiosPorPi(Json("[60, null, \"35,5\"]"), 3, 80);
            Assert.Equal(new List<double> { 60, 0, 35.5 }, radios);
            // Lista más corta: los PI que faltan usan el radio por defecto
            Assert.Equal(new List<double> { 60, 80, 80 }, Alineamiento.RadiosPorPi(Json("[60]"), 3, 80));
            // Sin parámetro: todos por defecto
            Assert.Equal(new List<double> { 80, 80 }, Alineamiento.RadiosPorPi(default, 2, 80));
        }

        [Fact]
        public void RadiosPorPiRechazaPiFueraDeRangoNegativosYFormatosRaros()
        {
            var ex = Assert.Throws<ArgumentException>(() => Alineamiento.RadiosPorPi(Json("[10, 20, 30, 40]"), 3, 80));
            Assert.Contains("nombra el PI 4", ex.Message);
            Assert.Contains("3 PI intermedios", ex.Message);
            Assert.Throws<ArgumentException>(() => Alineamiento.RadiosPorPi(Json("[{\"pi\": 0, \"radio\": 10}]"), 3, 80));
            Assert.Throws<ArgumentException>(() => Alineamiento.RadiosPorPi(Json("[-5]"), 3, 80));
            Assert.Throws<ArgumentException>(() => Alineamiento.RadiosPorPi(Json("{\"pi\": 1}"), 3, 80));
            Assert.Throws<ArgumentException>(() => Alineamiento.RadiosPorPi(Json("[{\"radio\": 10}]"), 3, 80));
        }

        // ---------------------------------------------------------------- polilínea de dos vértices, sin PI
        [Fact]
        public void DosVerticesSinPi()
        {
            var plan = Alineamiento.Planificar(Puntos(1000, 2000, 1300, 2400), 80);
            Assert.Equal(0, plan.NumeroPi);
            Assert.Equal(0, plan.NumeroCurvas);
            Assert.Empty(plan.Pis);
            Assert.Empty(plan.Avisos);
            Assert.Equal(500.0, plan.LongitudPolilinea, 6);
            Assert.Equal(500.0, plan.LongitudTotal, 6);
            Assert.Equal(500.0, plan.ProgresivaFinal, 6);
        }

        // ---------------------------------------------------------------- casos de borde de la geometría
        [Fact]
        public void VerticesAlineadosNoLlevanCurva()
        {
            var plan = Alineamiento.Planificar(Puntos(0, 0, 100, 0, 200, 0), 50);
            var pi = plan.Pis[0];
            Assert.Equal(0.0, pi.DeflexionGrados, 6);
            Assert.Equal("ninguno", pi.Giro);
            Assert.Null(pi.RadioAplicado);
            Assert.Contains("alineados", pi.Aviso);
            Assert.Equal(200.0, plan.LongitudTotal, 6);
        }

        [Fact]
        public void PolilineaQueVuelveSobreSiMismaNoLlevaCurva()
        {
            var plan = Alineamiento.Planificar(Puntos(0, 0, 100, 0, 0, 0), 50);
            Assert.Equal(180.0, plan.Pis[0].DeflexionGrados, 6);
            Assert.Null(plan.Pis[0].RadioAplicado);
            Assert.Contains("180°", plan.Pis[0].Aviso);
        }

        [Fact]
        public void RechazaMenosDeDosVerticesVerticesRepetidosYRadioNoPositivo()
        {
            Assert.Throws<ArgumentException>(() => Alineamiento.Planificar(Puntos(0, 0), 50));
            var ex = Assert.Throws<ArgumentException>(() => Alineamiento.Planificar(Puntos(0, 0, 100, 0, 100, 0, 200, 50), 50));
            Assert.Contains("vértices 2 y 3", ex.Message);
            Assert.Throws<ArgumentException>(() => Alineamiento.Planificar(Puntos(0, 0, 100, 0), 0));
            Assert.Throws<ArgumentException>(() => Alineamiento.ValidarRadio(-1));
            Assert.Throws<ArgumentException>(() => Alineamiento.Planificar(Puntos(0, 0, 100, 0, 100, 100), 50, new List<double> { 50, 50 }));
        }

        [Fact]
        public void DeflexionYTangenteDeUnGiroDe60Grados()
        {
            // Giro de 60° a la derecha: T = R·tan(30°), L = R·π/3
            double delta = Alineamiento.Deflexion(new Punto2(0, 0), new Punto2(100, 0), new Punto2(100 + 50 * Math.Cos(-Math.PI / 3), 50 * Math.Sin(-Math.PI / 3)), out int giro);
            Assert.Equal(60.0, Alineamiento.Grados(delta), 6);
            Assert.Equal(1, giro);
            Assert.Equal(100 * Math.Tan(Math.PI / 6), Alineamiento.Tangente(100, delta), 6);
            Assert.Equal(100 * Math.PI / 3, Alineamiento.LongitudCurva(100, delta), 6);
        }

        [Fact]
        public void ElDiccionarioDeUnPiTieneLosCamposDeLaRespuesta()
        {
            var plan = Alineamiento.Planificar(Puntos(0, 0, 100, 0, 100, 100), 50);
            var d = plan.PisComoDiccionarios().Single();
            foreach (var campo in new[] { "numero", "progresiva", "x", "y", "deflexion_grados", "giro", "radio_pedido", "radio_aplicado", "tangente", "longitud_curva", "progresiva_pc", "progresiva_pt", "radio_maximo", "aviso" })
                Assert.True(d.ContainsKey(campo), campo);
            Assert.Equal(1, d["numero"]);
            Assert.Equal(100.0, d["x"]);
            Assert.Equal(50.0, d["radio_aplicado"]);
            Assert.Null(d["aviso"]);
            Assert.Equal(78.5398, d["longitud_curva"]);
        }

        [Fact]
        public void ElMensajeDePolilineaNoValidaExplicaComoDibujarla()
        {
            string m = Alineamiento.MensajePolilineaNoValida("'1A2B'", "tiene 2 tramos de arco");
            Assert.Contains("polilínea 2D abierta de tangentes", m);
            Assert.Contains("_.PLINE", m);
            Assert.Contains("tiene 2 tramos de arco", m);
        }
    }
}
