using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Geometría de crear_rasante que no depende de Civil 3D: lectura de 'pvis', pendientes de los tramos, A, K,
    /// cresta/valle, curvas que caben o no, solapes, orden y rango de las progresivas, cota de la rasante en cualquier
    /// progresiva y comprobaciones de lo releído. Rasantes sintéticas con números genéricos (ningún dato real).
    /// </summary>
    public class RasantePruebas
    {
        private static JsonElement Json(string texto)
        {
            using (var d = JsonDocument.Parse(texto)) return d.RootElement.Clone();
        }

        private static List<PviRasante> Pvis(params (double progresiva, double cota, double longitud)[] datos)
            => datos.Select((d, i) => new PviRasante { Numero = i + 1, Progresiva = d.progresiva, Cota = d.cota, LongitudPedida = d.longitud, LongitudPropia = d.longitud > 0 }).ToList();

        /// <summary>El ejemplo del dueño: PVI en 0 a cota 100, 400 a cota 110 con curva de 120 m y 831,3274 a cota 105 (el eje de VALIDACION_18).</summary>
        private static PlanRasante Ejemplo() => Rasante.Planificar(Pvis((0, 100, 0), (400, 110, 120), (831.3274, 105, 0)), 0, 831.3274);

        // ---------------------------------------------------------------- dos PVI sin curva
        [Fact]
        public void DosPviSinCurvaEsUnTramoConPendiente()
        {
            var plan = Rasante.Planificar(Pvis((0, 100, 0), (200, 105, 0)));
            Assert.Equal(2, plan.NumeroPvis);
            Assert.Equal(0, plan.NumeroCurvas);
            Assert.Single(plan.Tramos);
            Assert.Equal(2.5, plan.Tramos[0].PendientePct, 6);
            Assert.Equal(200, plan.Tramos[0].Longitud, 6);
            Assert.Equal(5, plan.Tramos[0].Desnivel, 6);
            Assert.Empty(plan.Intermedios);
            Assert.Empty(plan.Avisos);
            Assert.Equal(100, plan.CotaMinima, 6);
            Assert.Equal(105, plan.CotaMaxima, 6);
            Assert.Equal(200, plan.Longitud, 6);
            Assert.Null(plan.Pvis[0].PendienteEntrada);
            Assert.Equal(2.5, plan.Pvis[0].PendienteSalida.Value, 6);
            Assert.Equal(2.5, plan.Pvis[1].PendienteEntrada.Value, 6);
            Assert.Null(plan.Pvis[1].PendienteSalida);
        }

        // ---------------------------------------------------------------- tres PVI con curva que cabe (el ejemplo del dueño)
        [Fact]
        public void TresPviConCurvaQueCabe()
        {
            var plan = Ejemplo();
            Assert.Equal(3, plan.NumeroPvis);
            Assert.Equal(1, plan.NumeroCurvas);
            Assert.Empty(plan.Avisos);
            Assert.Equal(2, plan.Tramos.Count);
            Assert.Equal(2.5, plan.Tramos[0].PendientePct, 6);
            Assert.Equal(-1.159212, plan.Tramos[1].PendientePct, 5);
            Assert.Equal(431.3274, plan.Tramos[1].Longitud, 6);
            var c = plan.Pvis[1];
            Assert.Equal(2, c.Numero);
            Assert.Equal(-3.659212, c.CambioPendiente.Value, 5);
            Assert.Equal("cresta", c.Tipo);
            Assert.Equal(120, c.LongitudAplicada.Value, 6);
            Assert.Equal(32.7939, c.K.Value, 3);
            Assert.Equal(340, c.ProgresivaInicio.Value, 6);
            Assert.Equal(460, c.ProgresivaFin.Value, 6);
            Assert.Equal(108.5, c.CotaInicio.Value, 6);
            Assert.Equal(109.3045, c.CotaFin.Value, 3);
            Assert.Equal(800, c.LongitudMaxima.Value, 3);        // 2 · min(400, 431,3274)
            Assert.Null(c.Aviso);
            // La cota máxima está en el vértice de la parábola (421,98), no en el PVI (110)
            Assert.Equal(109.5248, plan.CotaMaxima, 3);
            Assert.Equal(100, plan.CotaMinima, 6);
            Assert.Equal(421.9849, Rasante.ProgresivaVertice(c).Value, 3);
            var curvas = plan.CurvasComoDiccionarios();
            Assert.Single(curvas);
            Assert.Equal(2, curvas[0]["pvi"]);
            Assert.Equal(120.0, curvas[0]["longitud"]);
            Assert.Equal("cresta", curvas[0]["tipo"]);
            Assert.Null(curvas[0]["aviso"]);
            Assert.Equal(2, plan.TramosComoDiccionarios().Count);
        }

        [Fact]
        public void CotaEnTangenteDentroDeCurvaYEnElVertice()
        {
            var plan = Ejemplo();
            Assert.Equal(105.0, Rasante.CotaEn(plan, 200), 6);              // tramo 1, en la tangente
            Assert.Equal(107.6816, Rasante.CotaEn(plan, 600), 3);           // tramo 2
            Assert.Equal(108.5, Rasante.CotaEn(plan, 340), 6);              // inicio de la curva
            Assert.Equal(109.4511, Rasante.CotaEn(plan, 400), 3);           // bajo el PVI: cota del PVI menos la externa A·L/800 = 0,5489
            Assert.Equal(109.3045, Rasante.CotaEn(plan, 460), 3);           // fin de la curva
            Assert.Equal(109.5248, Rasante.CotaEn(plan, 421.9849), 3);      // vértice
            Assert.Equal(100, Rasante.CotaEn(plan, 0), 6);
            Assert.Equal(105, Rasante.CotaEn(plan, 831.3274), 6);
            Assert.Throws<ArgumentException>(() => Rasante.CotaEn(plan, 900));
        }

        [Fact]
        public void ValleConPendientesDelMismoSignoNoTieneVerticeDentro()
        {
            // Sube 1 % y luego 4 %: valle (A = +3), sin punto bajo dentro de la curva
            var plan = Rasante.Planificar(Pvis((0, 100, 0), (300, 103, 60), (600, 115, 0)));
            var c = plan.Pvis[1];
            Assert.Equal("valle", c.Tipo);
            Assert.Equal(3, c.CambioPendiente.Value, 6);
            Assert.Equal(20, c.K.Value, 6);
            Assert.Null(Rasante.ProgresivaVertice(c));
            Assert.Equal(100, plan.CotaMinima, 6);
            Assert.Equal(115, plan.CotaMaxima, 6);
        }

        // ---------------------------------------------------------------- curva que no cabe
        [Fact]
        public void CurvaQueNoCabeQuedaComoQuiebreConLongitudMaxima()
        {
            // El PVI 2 está a 100 m del inicio: una curva de 300 m necesitaría 150 m a cada lado
            var plan = Rasante.Planificar(Pvis((0, 100, 0), (100, 102, 300), (500, 100, 0)));
            Assert.Equal(0, plan.NumeroCurvas);
            var c = plan.Pvis[1];
            Assert.Null(c.LongitudAplicada);
            Assert.Null(c.K);
            Assert.Equal("cresta", c.Tipo);
            Assert.Equal(200, c.LongitudMaxima.Value, 3);        // 2 · min(100, 400)
            Assert.Contains("la curva de 300 m no cabe", c.Aviso);
            Assert.Contains("150 m a cada lado", c.Aviso);
            Assert.Contains("Longitud máxima que cabría: 200 m", c.Aviso);
            Assert.Single(plan.Avisos);
            // Sin curva, la cota máxima es la del PVI
            Assert.Equal(102, plan.CotaMaxima, 6);
            var dic = plan.CurvasComoDiccionarios()[0];
            Assert.Null(dic["longitud"]);
            Assert.Equal(300.0, dic["longitud_pedida"]);
            Assert.NotNull(dic["aviso"]);
        }

        [Fact]
        public void CurvaQueLlegaJustoAlPviVecinoCabe()
        {
            // Tramo de 100 m antes del PVI 2: una curva de 200 m lo ocupa entero (mitad = 100) y cabe por el margen
            var plan = Rasante.Planificar(Pvis((0, 100, 0), (100, 102, 200), (500, 100, 0)));
            Assert.Equal(1, plan.NumeroCurvas);
            Assert.Equal(0, plan.Pvis[1].ProgresivaInicio.Value, 6);
            Assert.Equal(200, plan.Pvis[1].ProgresivaFin.Value, 6);
        }

        // ---------------------------------------------------------------- dos curvas que se solapan
        [Fact]
        public void DosCurvasQueSeSolapanConservanLaPrimera()
        {
            // PVI 2 y 3 a 100 m: curvas de 150 m (75 a cada lado) se solaparían 50 m; la segunda queda sin curva
            var plan = Rasante.Planificar(Pvis((0, 100, 0), (300, 106, 150), (400, 104, 150), (800, 110, 0)));
            Assert.Equal(1, plan.NumeroCurvas);
            Assert.Equal(150, plan.Pvis[1].LongitudAplicada.Value, 6);
            Assert.Null(plan.Pvis[2].LongitudAplicada);
            Assert.Equal(50, plan.Pvis[2].LongitudMaxima.Value, 3);        // 2 · (100 − 75)
            Assert.Contains("libre 25 m antes", plan.Pvis[2].Aviso);
            Assert.Contains("Longitud máxima que cabría: 50 m", plan.Pvis[2].Aviso);
            Assert.Single(plan.Avisos);
            // Con 50 m la segunda sí cabe y las dos curvas se tocan en la progresiva 375
            plan = Rasante.Planificar(Pvis((0, 100, 0), (300, 106, 150), (400, 104, 50), (800, 110, 0)));
            Assert.Equal(2, plan.NumeroCurvas);
            Assert.Equal(375, plan.Pvis[1].ProgresivaFin.Value, 6);
            Assert.Equal(375, plan.Pvis[2].ProgresivaInicio.Value, 6);
            Assert.Empty(plan.Avisos);
        }

        [Fact]
        public void CurvaAnteriorQueOcupaTodoElTramoNoDejaSitio()
        {
            // La curva del PVI 2 (200 m) llega justo al PVI 3: para el PVI 3 no cabe ninguna
            var plan = Rasante.Planificar(Pvis((0, 100, 0), (100, 102, 200), (200, 103, 40), (600, 100, 0)));
            Assert.Equal(1, plan.NumeroCurvas);
            Assert.Null(plan.Pvis[2].LongitudMaxima);
            Assert.Contains("No cabe ninguna curva", plan.Pvis[2].Aviso);
        }

        // ---------------------------------------------------------------- progresivas no crecientes
        [Fact]
        public void ProgresivasNoCrecientesEsError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Rasante.Planificar(Pvis((0, 100, 0), (400, 110, 0), (300, 105, 0))));
            Assert.Contains("orden estrictamente creciente", ex.Message);
            Assert.Contains("PVI 3 (300) no es mayor que el PVI 2 (400)", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => Rasante.Planificar(Pvis((0, 100, 0), (0, 110, 0))));
            Assert.Contains("PVI 2 (0) no es mayor que el PVI 1 (0)", ex.Message);
        }

        [Fact]
        public void MenosDeDosPviEsError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Rasante.Planificar(Pvis((0, 100, 0))));
            Assert.Contains("al menos 2", ex.Message);
        }

        // ---------------------------------------------------------------- PVI fuera del rango del eje
        [Fact]
        public void PviFueraDelEjeEsErrorYCercaDelExtremoSeAjustaConAviso()
        {
            var ex = Assert.Throws<ArgumentException>(() => Rasante.Planificar(Pvis((0, 100, 0), (900, 105, 0)), 0, 831.3274));
            Assert.Contains("PVI 2 está en la progresiva 900, fuera del alineamiento (va de 0 a 831.327)", ex.Message);
            Assert.Contains("listar_alineamientos", ex.Message);
            ex = Assert.Throws<ArgumentException>(() => Rasante.Planificar(Pvis((-5, 100, 0), (800, 105, 0)), 0, 831.3274));
            Assert.Contains("PVI 1", ex.Message);
            // 831.33 está a 0,0026 del final: se ajusta a 831.3274 con aviso (así lo dice el dueño de palabra)
            var plan = Rasante.Planificar(Pvis((0, 100, 0), (400, 110, 120), (831.33, 105, 0)), 0, 831.3274);
            Assert.Equal(831.3274, plan.ProgresivaFinal, 6);
            Assert.Contains(plan.Avisos, a => a.Contains("se ajusta al final del eje (831.327)"));
            // Sin rango (NaN) no se comprueba nada
            plan = Rasante.Planificar(Pvis((0, 100, 0), (5000, 105, 0)));
            Assert.Equal(5000, plan.ProgresivaFinal, 6);
        }

        // ---------------------------------------------------------------- A = 0: sin curva necesaria
        [Fact]
        public void PviSinCambioDePendienteNoNecesitaCurvaYAvisa()
        {
            var plan = Rasante.Planificar(Pvis((0, 100, 0), (200, 104, 80), (400, 108, 0)));
            Assert.Equal(0, plan.NumeroCurvas);
            var c = plan.Pvis[1];
            Assert.Null(c.Tipo);
            Assert.Equal(0, c.CambioPendiente.Value, 9);
            Assert.Null(c.LongitudAplicada);
            Assert.Contains("A = 0", c.Aviso);
            Assert.Contains("no hace falta curva vertical y no se crea", c.Aviso);
            Assert.Single(plan.Avisos);
        }

        // ---------------------------------------------------------------- avisos informativos
        [Fact]
        public void PendienteMayorQueDocePorCientoSoloAvisa()
        {
            var plan = Rasante.Planificar(Pvis((0, 100, 0), (100, 115, 0)));
            Assert.Equal(15, plan.Tramos[0].PendientePct, 6);
            Assert.Contains("pendiente 15 %", plan.Tramos[0].Aviso);
            Assert.Contains("aviso informativo", plan.Tramos[0].Aviso);
            Assert.Single(plan.Avisos);
            // Un 12 % exacto (en bajada) no supera el límite: sin aviso
            plan = Rasante.Planificar(Pvis((0, 100, 0), (100, 88, 0)));
            Assert.Equal(-12, plan.Tramos[0].PendientePct, 6);
            Assert.Empty(plan.Avisos);
        }

        [Fact]
        public void LongitudEnElPrimeroOElUltimoSeIgnoraConAviso()
        {
            var plan = Rasante.Planificar(Pvis((0, 100, 50), (200, 105, 0), (400, 100, 70)));
            Assert.Equal(0, plan.NumeroCurvas);
            Assert.Equal(2, plan.Avisos.Count);
            Assert.Contains("PVI 1: es el primero", plan.Avisos[0]);
            Assert.Contains("PVI 3: es el último", plan.Avisos[1]);
        }

        // ---------------------------------------------------------------- lectura de 'pvis'
        [Fact]
        public void LeePvisComoObjetosListasYTextosConLongitudPorDefecto()
        {
            var pvis = Rasante.LeerPvis(Json("[{\"progresiva\": 0, \"cota\": 100}, {\"progresiva\": \"400\", \"cota\": \"110,5\", \"longitud_curva\": 120}, [700, 108], {\"pk\": 831.33, \"z\": 105, \"longitud\": null}]"), 60);
            Assert.Equal(4, pvis.Count);
            Assert.Equal(0, pvis[0].Progresiva); Assert.Equal(100, pvis[0].Cota); Assert.Equal(60, pvis[0].LongitudPedida); Assert.False(pvis[0].LongitudPropia);
            Assert.Equal(400, pvis[1].Progresiva); Assert.Equal(110.5, pvis[1].Cota); Assert.Equal(120, pvis[1].LongitudPedida); Assert.True(pvis[1].LongitudPropia);
            Assert.Equal(700, pvis[2].Progresiva); Assert.Equal(108, pvis[2].Cota); Assert.Equal(60, pvis[2].LongitudPedida);
            Assert.Equal(831.33, pvis[3].Progresiva); Assert.Equal(105, pvis[3].Cota); Assert.Equal(60, pvis[3].LongitudPedida);
            // Sin longitud por defecto: los intermedios quedan a 0 (quiebres)
            pvis = Rasante.LeerPvis(Json("[[0, 100], [400, 110], [800, 105]]"), null);
            Assert.All(pvis, p => Assert.Equal(0, p.LongitudPedida));
            var plan = Rasante.Planificar(pvis);
            Assert.Equal(0, plan.NumeroCurvas);
            Assert.Empty(plan.Avisos);
            // Con longitud por defecto, el primero y el último no avisan (no es longitud propia) y el intermedio lleva curva
            plan = Rasante.Planificar(Rasante.LeerPvis(Json("[[0, 100], [400, 110], [800, 105]]"), 100));
            Assert.Equal(1, plan.NumeroCurvas);
            Assert.Empty(plan.Avisos);
            // Un PVI con longitud_curva 0 explícita queda sin curva aunque haya longitud por defecto
            plan = Rasante.Planificar(Rasante.LeerPvis(Json("[[0, 100], [400, 110, 0], [800, 105]]"), 100));
            Assert.Equal(0, plan.NumeroCurvas);
        }

        [Fact]
        public void PvisMalFormadosSonErroresClaros()
        {
            Assert.Contains("Falta el parámetro obligatorio 'pvis'", Assert.Throws<ArgumentException>(() => Rasante.LeerPvis(default, null)).Message);
            Assert.Contains("debe ser una lista JSON", Assert.Throws<ArgumentException>(() => Rasante.LeerPvis(Json("{\"progresiva\": 0}"), null)).Message);
            Assert.Contains("al menos 2", Assert.Throws<ArgumentException>(() => Rasante.LeerPvis(Json("[{\"progresiva\": 0, \"cota\": 100}]"), null)).Message);
            Assert.Contains("El elemento 2 de 'pvis' no tiene 'cota'", Assert.Throws<ArgumentException>(() => Rasante.LeerPvis(Json("[{\"progresiva\": 0, \"cota\": 100}, {\"progresiva\": 100}]"), null)).Message);
            Assert.Contains("El elemento 1 de 'pvis' no tiene 'progresiva'", Assert.Throws<ArgumentException>(() => Rasante.LeerPvis(Json("[{\"cota\": 100}, {\"progresiva\": 100, \"cota\": 1}]"), null)).Message);
            Assert.Contains("debe ser un número", Assert.Throws<ArgumentException>(() => Rasante.LeerPvis(Json("[{\"progresiva\": \"abc\", \"cota\": 100}, [1, 2]]"), null)).Message);
            Assert.Contains("no es un objeto", Assert.Throws<ArgumentException>(() => Rasante.LeerPvis(Json("[5, 6]"), null)).Message);
            Assert.Contains("mayor que 0", Assert.Throws<ArgumentException>(() => Rasante.LeerPvis(Json("[[0, 100], [100, 101, -5], [200, 100]]"), null)).Message);
        }

        [Fact]
        public void TipoDeCurvaYLongitudPorDefecto()
        {
            Assert.Equal("parabola_simetrica", Rasante.TipoCurvaNormalizado(null));
            Assert.Equal("parabola_simetrica", Rasante.TipoCurvaNormalizado("Parábola simétrica"));
            Assert.Equal("parabola_simetrica", Rasante.TipoCurvaNormalizado("parabola"));
            Assert.Equal("circular", Rasante.TipoCurvaNormalizado("Circular"));
            Assert.Equal("circular", Rasante.TipoCurvaNormalizado("arco"));
            var ex = Assert.Throws<ArgumentException>(() => Rasante.TipoCurvaNormalizado("asimetrica"));
            Assert.Contains("'parabola_simetrica' (por defecto) o 'circular'", ex.Message);
            Rasante.ValidarLongitudCurva(null, "'longitud_curva'");
            Rasante.ValidarLongitudCurva(40, "'longitud_curva'");
            Assert.Contains("mayor que 0", Assert.Throws<ArgumentException>(() => Rasante.ValidarLongitudCurva(0, "'longitud_curva'")).Message);
            Assert.Contains("mayor que 0", Assert.Throws<ArgumentException>(() => Rasante.ValidarLongitudCurva(-10, "'longitud_curva'")).Message);
            Assert.Equal("parábola simétrica", Rasante.TextoTipoCurva("parabola_simetrica"));
            Assert.Equal("curva circular", Rasante.TextoTipoCurva("circular"));
        }

        // ---------------------------------------------------------------- comprobaciones tras crear
        [Fact]
        public void ComparaLosPviLeidosConLosPrevistos()
        {
            var plan = Ejemplo();
            Assert.Null(Rasante.MotivoPvisNoCoinciden(plan.Pvis, new List<(double, double)> { (831.3274, 105), (0, 100), (400.004, 110.003) }));
            Assert.Contains("tiene 2 PVI y se pidieron 3", Rasante.MotivoPvisNoCoinciden(plan.Pvis, new List<(double, double)> { (0, 100), (831.3274, 105) }));
            Assert.Contains("PVI 2 quedó en la progresiva 410 y se pidió 400", Rasante.MotivoPvisNoCoinciden(plan.Pvis, new List<(double, double)> { (0, 100), (410, 110), (831.3274, 105) }));
            Assert.Contains("quedó con cota 111 y se pidió 110", Rasante.MotivoPvisNoCoinciden(plan.Pvis, new List<(double, double)> { (0, 100), (400, 111), (831.3274, 105) }));
            Assert.Contains("no se pudieron leer", Rasante.MotivoPvisNoCoinciden(plan.Pvis, null));
            Assert.Null(Rasante.MotivoRangoNoCoincide(0, 831.3274, 0.001, 831.33));
            Assert.Contains("termina en 800 y se pidió 831.327", Rasante.MotivoRangoNoCoincide(0, 831.3274, 0, 800));
            Assert.Contains("empieza en 10", Rasante.MotivoRangoNoCoincide(0, 831.3274, 10, 831.3274));
            Assert.Contains("no son números válidos", Rasante.MotivoRangoNoCoincide(0, 831.3274, double.NaN, 831.3274));
        }

        [Fact]
        public void DecideLaUnidadDeLasPendientesDeLaApi()
        {
            Assert.Equal("tanto_por_uno", Rasante.UnidadPendiente(2.5, 0.025));
            Assert.Equal("porcentaje", Rasante.UnidadPendiente(2.5, 2.5));
            Assert.Null(Rasante.UnidadPendiente(2.5, 1.0));
            Assert.Equal(2.5, Rasante.PendienteEnPorcentaje(0.025, "tanto_por_uno"), 9);
            Assert.Equal(2.5, Rasante.PendienteEnPorcentaje(2.5, "porcentaje"), 9);
            Assert.Equal(2.5, Rasante.PendienteEnPorcentaje(0.025, null), 9);
        }

        [Fact]
        public void TextoDeLaAccion()
        {
            Assert.Equal("Crear la rasante 'Rasante 1' (perfil de diseño FG) del alineamiento 'Eje 1' con 3 PVI y 1 curva vertical (parábola simétrica), de 0 a 831.327", Rasante.Accion("Rasante 1", "Eje 1", Ejemplo(), "parabola_simetrica"));
            var sinCurvas = Rasante.Planificar(Pvis((0, 100, 0), (200, 105, 0)));
            Assert.Equal("Crear la rasante 'R' (perfil de diseño FG) del alineamiento 'E' con 2 PVI y 0 curvas verticales, de 0 a 200", Rasante.Accion("R", "E", sinCurvas, "circular"));
        }
    }
}
