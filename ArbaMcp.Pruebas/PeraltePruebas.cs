using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Peralte (1.20.0) sin Civil 3D: lectura y validación de 'curvas' de aplicar_peralte, comprobación contra la tabla de
    /// PI del eje, estado y previsión, verificación tras escribir y mensajes. Los nombres de tipo, región y segmento son
    /// de ejemplo (TipoA, RegionA, SegmentoIzq...): los reales los da listar_peralte en Civil 3D y el núcleo no los conoce.
    /// </summary>
    public class PeraltePruebas
    {
        private static JsonElement J(string json)
        {
            using (var d = JsonDocument.Parse(json)) return d.RootElement.Clone();
        }

        private const string UnaCurva = "[{\"pi\": 1, \"estaciones\": ["
            + "{\"progresiva\": 80, \"tipo\": \"TipoA\", \"region\": \"RegionA\", \"pendientes\": {\"SegmentoIzq\": -2, \"SegmentoDer\": -2}},"
            + "{\"progresiva\": 110.5, \"tipo\": \"TipoB\", \"pendientes\": {\"SegmentoIzq\": 6, \"SegmentoDer\": \"-6,0\"}}]}]";

        /// <summary>Eje de ejemplo: PI 1 con curva (PC 100, PT 150, R 100, derecha), PI 2 sin curva, PI 3 espiral; de 0 a 400.</summary>
        private static ResultadoTablaPi Tabla()
        {
            var t = new ResultadoTablaPi { Alineamiento = "Eje 1", ProgresivaInicial = 0, ProgresivaFinal = 400 };
            t.Pis.Add(new FilaPi { Numero = 1, Tipo = EntidadEje.Curva, TieneCurva = true, Radio = 100, ProgresivaPc = 100, ProgresivaPt = 150, Giro = "derecha" });
            t.Pis.Add(new FilaPi { Numero = 2, Tipo = FilaPi.SinCurva, TieneCurva = false });
            t.Pis.Add(new FilaPi { Numero = 3, Tipo = "AlignmentSCS", TieneCurva = false });
            t.Pis.Add(new FilaPi { Numero = 4, Tipo = EntidadEje.Curva, TieneCurva = true, Radio = 200, ProgresivaPc = 300, ProgresivaPt = 350, Giro = "izquierda" });
            return t;
        }

        [Fact]
        public void LeeCurvasConPendientesEnPorcentajeYComaDecimal()
        {
            var curvas = Peralte.LeerCurvas(J(UnaCurva));
            Assert.Single(curvas);
            var c = curvas[0];
            Assert.Equal(1, c.Pi);
            Assert.Equal(2, c.Estaciones.Count);
            Assert.Equal("RegionA", c.Estaciones[0].Region);
            Assert.Null(c.Estaciones[1].Region);   // sin región: Add(progresiva, tipo)
            Assert.Equal(-6.0, c.Estaciones[1].Pendientes.Single(p => p.Key == "SegmentoDer").Value);
        }

        [Fact]
        public void OrdenaCurvasPorPiYEstacionesPorProgresiva()
        {
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": 4, \"estaciones\": [{\"progresiva\": 320, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}, {\"progresiva\": 290, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]},"
                + "{\"pi\": 1, \"estaciones\": [{\"progresiva\": 90, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}]"));
            Assert.Equal(new[] { 1, 4 }, curvas.Select(c => c.Pi).ToArray());
            Assert.Equal(new[] { 290.0, 320.0 }, curvas[1].Estaciones.Select(e => e.Progresiva).ToArray());
        }

        [Theory]
        [InlineData("", "Falta el parámetro obligatorio 'curvas'")]
        [InlineData("{\"pi\": 1}", "debe ser una lista JSON")]
        [InlineData("[]", "está vacía")]
        [InlineData("[{\"estaciones\": []}]", "no tiene 'pi'")]
        [InlineData("[{\"pi\": 1.5, \"estaciones\": []}]", "debe ser un número entero")]
        [InlineData("[{\"pi\": 0, \"estaciones\": []}]", "debe ser 1 o más")]
        [InlineData("[{\"pi\": 1, \"estaciones\": []}]", "no tiene 'estaciones'")]
        [InlineData("[{\"pi\": 1, \"estaciones\": [{\"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}]", "no tiene 'progresiva'")]
        [InlineData("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 10, \"pendientes\": {\"S\": 1}}]}]", "no tiene 'tipo'")]
        [InlineData("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 10, \"tipo\": \"T\"}]}]", "no tiene 'pendientes'")]
        [InlineData("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 10, \"tipo\": \"T\", \"pendientes\": {}}]}]", "'pendientes' está vacío")]
        [InlineData("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 10, \"tipo\": \"T\", \"pendientes\": {\"S\": \"mucho\"}}]}]", "debe ser un número")]
        [InlineData("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": \"NaN\", \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}]", "número finito")]
        [InlineData("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 10, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}, {\"pi\": 1, \"estaciones\": [{\"progresiva\": 20, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}]", "está dos veces en 'curvas'")]
        [InlineData("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 10, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}, {\"progresiva\": 10.0005, \"tipo\": \"U\", \"pendientes\": {\"S\": 1}}]}]", "está repetida")]
        [InlineData("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 10, \"tipo\": \"T\", \"pendientes\": {\"S\": 1, \"s\": 2}}]}]", "está dos veces en 'pendientes'")]
        public void RechazaCurvasMalFormadas(string json, string mensaje)
        {
            var ex = Assert.Throws<ArgumentException>(() => Peralte.LeerCurvas(json == "" ? default : J(json)));
            Assert.Contains(mensaje, ex.Message);
        }

        [Fact]
        public void ComprobarContraEjeCompletaLaCurvaYNoAvisaSiNoSeCruzan()
        {
            var curvas = Peralte.LeerCurvas(J(UnaCurva));
            var avisos = Peralte.ComprobarContraEje(curvas, Tabla());
            Assert.Empty(avisos);
            Assert.Equal(100, curvas[0].Radio);
            Assert.Equal(100, curvas[0].ProgresivaPc);
            Assert.Equal(150, curvas[0].ProgresivaPt);
            Assert.Equal("derecha", curvas[0].Giro);
        }

        [Theory]
        [InlineData(9, "no tiene el PI 9 (tiene 4). Usa listar_pis.")]
        [InlineData(2, "no tiene curva (dos tangentes seguidas)")]
        [InlineData(3, "es de tipo AlignmentSCS, no soportado todavía")]
        public void RechazaPiSinCurvaCircular(int pi, string mensaje)
        {
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": " + pi + ", \"estaciones\": [{\"progresiva\": 10, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}]"));
            var ex = Assert.Throws<ArgumentException>(() => Peralte.ComprobarContraEje(curvas, Tabla()));
            Assert.Contains(mensaje, ex.Message);
        }

        [Fact]
        public void RechazaProgresivaFueraDelEje()
        {
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": 4, \"estaciones\": [{\"progresiva\": 400.5, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}]"));
            var ex = Assert.Throws<ArgumentException>(() => Peralte.ComprobarContraEje(curvas, Tabla()));
            Assert.Contains("0+400.500", ex.Message);
            Assert.Contains("fuera del eje 'Eje 1' (de 0+000.000 a 0+400.000)", ex.Message);
        }

        [Fact]
        public void AvisaSiLasProgresivasDeDosCurvasSeCruzan()
        {
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 90, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}, {\"progresiva\": 310, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]},"
                + "{\"pi\": 4, \"estaciones\": [{\"progresiva\": 280, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}]"));
            var avisos = Peralte.ComprobarContraEje(curvas, Tabla());
            Assert.Single(avisos);
            Assert.Contains("PI 1 (hasta 0+310.000) se cruzan con las del PI 4 (desde 0+280.000)", avisos[0]);
        }

        private static PeralteLeido Leido(params (double pk, string tipo, double izq, double der)[] estaciones)
        {
            var c = new CurvaLeida { Nombre = "Curva.1", Inicio = 70, Fin = 180 };
            foreach (var e in estaciones)
            {
                var l = new EstacionLeida { Progresiva = e.pk, Tipo = e.tipo, Curva = "Curva.1" };
                l.Pendientes["SegmentoIzq"] = e.izq;
                l.Pendientes["SegmentoDer"] = e.der;
                c.Estaciones.Add(l);
            }
            var p = new PeralteLeido { Alineamiento = "Eje 1" };
            if (estaciones.Length > 0) p.Curvas.Add(c);
            return p;
        }

        [Fact]
        public void SinDiferenciasSiCuadraDentroDeLaTolerancia()
        {
            var curvas = Peralte.LeerCurvas(J(UnaCurva));
            var leido = Leido((80.0004, "tipoa", -2.0004, -2), (110.5, "TipoB", 6, -6.0009));
            Assert.Empty(Peralte.Diferencias(curvas, leido, out var extra));
            Assert.Empty(extra);
        }

        [Fact]
        public void DiferenciasDeTipoPendienteYEstacionQueFalta()
        {
            var curvas = Peralte.LeerCurvas(J(UnaCurva));
            var leido = Leido((80, "TipoX", -2, -2.5));
            var dif = Peralte.Diferencias(curvas, leido, out _);
            Assert.Equal(3, dif.Count);
            Assert.Contains("PI 1, 0+080.000: tipo 'TipoX' en vez de 'TipoA'", dif);
            Assert.Contains("PI 1, 0+080.000: pendiente de 'SegmentoDer' -2.5 % en vez de -2 %", dif);
            Assert.Contains("PI 1, 0+110.500: no está en el eje después de escribir", dif);
        }

        [Fact]
        public void PendienteSinLeerEsDiferencia()
        {
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 80, \"tipo\": \"TipoA\", \"pendientes\": {\"SegmentoCentral\": 1}}]}]"));
            var dif = Peralte.Diferencias(curvas, Leido((80, "TipoA", 0, 0)), out _);
            Assert.Equal(new[] { "PI 1, 0+080.000: no se pudo leer la pendiente de 'SegmentoCentral'" }, dif);
        }

        [Fact]
        public void LasEstacionesQueNoSePidieronSonAvisoNoError()
        {
            var curvas = Peralte.LeerCurvas(J(UnaCurva));
            var leido = Leido((60, "TipoAuto", -2, -2), (80, "TipoA", -2, -2), (110.5, "TipoB", 6, -6));
            Assert.Empty(Peralte.Diferencias(curvas, leido, out var extra));
            Assert.Equal(new[] { "0+060.000 (TipoAuto, curva 'Curva.1')" }, extra);
        }

        [Fact]
        public void BuscaTambienEnLaColeccionDelEjeSinDuplicar()
        {
            var curvas = Peralte.LeerCurvas(J(UnaCurva));
            var leido = Leido((80, "TipoA", -2, -2));
            var delEje = new EstacionLeida { Progresiva = 110.5, Tipo = "TipoB", Curva = "Curva.1" };
            delEje.Pendientes["SegmentoIzq"] = 6; delEje.Pendientes["SegmentoDer"] = -6;
            leido.EstacionesDelEje.Add(delEje);
            leido.EstacionesDelEje.Add(new EstacionLeida { Progresiva = 80, Tipo = "TipoA", Curva = "Curva.1" });   // la misma que la de la curva: no cuenta dos veces
            Assert.Empty(Peralte.Diferencias(curvas, leido, out var extra));
            Assert.Empty(extra);
        }

        [Fact]
        public void EstadoPrevisionYEsperado()
        {
            var curvas = Peralte.LeerCurvas(J(UnaCurva));
            Peralte.ComprobarContraEje(curvas, Tabla());
            var antes = Peralte.Estado(new PeralteLeido { Alineamiento = "Eje 1" });
            Assert.Equal(false, antes["tiene_peralte"]);
            Assert.Equal(0, antes["numero_curvas"]);
            var prevision = Peralte.Prevision("Eje 1", curvas);
            Assert.Equal(true, prevision["tiene_peralte"]);
            Assert.Equal(2, prevision["numero_estaciones"]);
            var curva = (Dictionary<string, object>)((List<object>)prevision["curvas"])[0];
            Assert.Equal(1, curva["pi"]);
            Assert.Equal("derecha", curva["giro"]);
            var esperado = Peralte.Esperado(curvas);
            Assert.Equal(true, esperado["tiene_peralte"]);
            Assert.Equal(1, esperado["numero_curvas"]);

            var despues = Peralte.Estado(Leido((80, "TipoA", -2, -2), (110.5, "TipoB", 6, -6)));
            var r = Verificacion.Resultado("aplicar_peralte", antes, despues, esperado);   // no lanza: cumple lo esperado
            Assert.NotNull(r);
        }

        [Fact]
        public void MensajeYaTienePeralteDiceQueUsarYNoBorra()
        {
            var p = Leido((80, "TipoA", -2, -2), (110.5, "TipoB", 6, -6));
            string m = Peralte.MensajeYaTienePeralte(p);
            Assert.Equal("El eje 'Eje 1' ya tiene peralte (1 curva de peralte, 2 progresivas críticas): aplicar_peralte solo escribe en un eje sin peralte y no borra el que hay. Míralo con listar_peralte.", m);
            var soloEje = new PeralteLeido { Alineamiento = "Eje 2" };
            soloEje.EstacionesDelEje.Add(new EstacionLeida { Progresiva = 0, Tipo = "T" });
            Assert.True(soloEje.TienePeralte);
            Assert.Contains("(0 curvas de peralte, 1 progresiva crítica)", Peralte.MensajeYaTienePeralte(soloEje));
        }

        /// <summary>
        /// Lo que Civil 3D 2027 crea solo con el eje (17/11/2026, VALIDACION_120 paso 2): una curva de peralte vacía por
        /// curva horizontal, de PC a PT, y las progresivas BeginAlignment y EndAlignment del eje con las pendientes en 0.
        /// </summary>
        private static PeralteLeido EjeRecienCreado()
        {
            var p = new PeralteLeido { Alineamiento = "Eje 1" };
            p.Curvas.Add(new CurvaLeida { Nombre = "Curva.1", Inicio = 100, Fin = 150 });
            p.Curvas.Add(new CurvaLeida { Nombre = "Curva.2", Inicio = 300, Fin = 350 });
            foreach (var (pk, tipo) in new[] { (0.0, "BeginAlignment"), (400.0, "EndAlignment") })
            {
                var e = new EstacionLeida { Progresiva = pk, Tipo = tipo, Region = "Out", Curva = "" };
                e.Pendientes["LeftOutLaneCrossSlope"] = 0;
                e.Pendientes["RightOutLaneCrossSlope"] = 0;
                p.EstacionesDelEje.Add(e);
            }
            return p;
        }

        [Fact]
        public void EjeRecienCreadoNoTienePeralte()
        {
            var p = EjeRecienCreado();
            Assert.False(p.TienePeralte);
            Assert.Equal(0, p.NumeroCurvasConEstaciones);
            Assert.Equal(0, p.NumeroEstacionesDelEjeSinContorno);
            var estado = Peralte.Estado(p);
            Assert.Equal(false, estado["tiene_peralte"]);
            Assert.Equal(2, estado["numero_curvas"]);
            Assert.Equal(0, estado["numero_estaciones"]);
        }

        [Fact]
        public void UnaProgresivaQueNoEsDeContornoSiEsPeralte()
        {
            var enCurva = EjeRecienCreado();
            enCurva.Curvas[0].Estaciones.Add(new EstacionLeida { Progresiva = 80, Tipo = "EndNormalCrown", Curva = "Curva.1" });
            Assert.True(enCurva.TienePeralte);
            Assert.Contains("(1 curva de peralte, 1 progresiva crítica)", Peralte.MensajeYaTienePeralte(enCurva));

            var enEje = EjeRecienCreado();
            enEje.EstacionesDelEje.Add(new EstacionLeida { Progresiva = 80, Tipo = "LevelCrown" });
            Assert.True(enEje.TienePeralte);
            Assert.Contains("(0 curvas de peralte, 1 progresiva crítica)", Peralte.MensajeYaTienePeralte(enEje));

            Assert.True(Peralte.EsDeContorno(new EstacionLeida { Tipo = "beginalignment" }));
            Assert.False(Peralte.EsDeContorno(new EstacionLeida { Tipo = "BeginNormalCrown" }));
            Assert.False(Peralte.EsDeContorno(null));
        }

        [Fact]
        public void UsaLaCurvaVaciaDeCadaArco()
        {
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 90, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]},"
                + "{\"pi\": 4, \"estaciones\": [{\"progresiva\": 320, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}]"));
            Peralte.ComprobarContraEje(curvas, Tabla());
            var actual = EjeRecienCreado();
            actual.Curvas[1].Inicio = 300.0004;   // dentro de 0,001 m
            Peralte.AsignarCurvasExistentes(curvas, actual);
            Assert.Equal("Curva.1", curvas[0].CurvaExistente);
            Assert.Equal("Curva.2", curvas[1].CurvaExistente);

            var prevision = Peralte.Prevision("Eje 1", curvas, actual.Curvas.Count);
            Assert.Equal(2, prevision["numero_curvas"]);   // no se crean curvas nuevas
            var c1 = (Dictionary<string, object>)((List<object>)prevision["curvas"])[0];
            Assert.Equal("Curva.1", c1["nombre"]);
            Assert.Equal("existente", c1["curva_de_peralte"]);
            Assert.Equal(2, Peralte.Esperado(curvas, actual.Curvas.Count)["numero_curvas"]);
        }

        [Fact]
        public void SinCurvaVaciaDelArcoSeCreaUnaNueva()
        {
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 90, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]},"
                + "{\"pi\": 4, \"estaciones\": [{\"progresiva\": 320, \"tipo\": \"T\", \"pendientes\": {\"S\": 1}}]}]"));
            Peralte.ComprobarContraEje(curvas, Tabla());
            var actual = EjeRecienCreado();
            actual.Curvas[0].Fin = 150.01;   // no acaba en el PT
            actual.Curvas[1].Estaciones.Add(new EstacionLeida { Progresiva = 320, Tipo = "T" });   // no está vacía
            Peralte.AsignarCurvasExistentes(curvas, actual);
            Assert.Null(curvas[0].CurvaExistente);
            Assert.Null(curvas[1].CurvaExistente);
            Assert.Equal(4, Peralte.Esperado(curvas, actual.Curvas.Count)["numero_curvas"]);
            var c1 = (Dictionary<string, object>)((List<object>)Peralte.Prevision("Eje 1", curvas, actual.Curvas.Count)["curvas"])[0];
            Assert.Equal("(lo pone Civil 3D)", c1["nombre"]);
            Assert.Equal("nueva", c1["curva_de_peralte"]);
        }

        [Fact]
        public void LasProgresivasDeContornoNoSonExtraNiSeAnaden()
        {
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 90, \"tipo\": \"EndNormalCrown\", \"pendientes\": {\"LeftOutLaneCrossSlope\": -2}}]}]"));
            var leido = EjeRecienCreado();
            var e = new EstacionLeida { Progresiva = 90, Tipo = "EndNormalCrown", Curva = "Curva.1" };
            e.Pendientes["LeftOutLaneCrossSlope"] = -2;
            leido.Curvas[0].Estaciones.Add(e);
            Assert.Empty(Peralte.Diferencias(curvas, leido, out var extra));
            Assert.Empty(extra);
            var anadido = (List<object>)Peralte.BloqueDeshacer(leido, null)["anadido"];
            Assert.Single(anadido);
            Assert.Equal("Curva.1", ((Dictionary<string, object>)anadido[0])["curva"]);
        }

        [Fact]
        public void PendientesEnTantoPorUnoParaLaApi()
        {
            Assert.Equal(-0.02, Peralte.PendienteApi(-2), 12);
            Assert.Equal(6.0, Peralte.PendientePct(0.06), 12);
        }

        [Fact]
        public void MensajeNoReflejaYBloqueDeshacer()
        {
            string m = Peralte.MensajeNoRefleja("Eje 1", new[] { "PI 1, 0+080.000: no está en el eje después de escribir" }, "C:\\Proyectos\\backups\\prueba.dwg");
            Assert.StartsWith("El dibujo no refleja el peralte pedido en el eje 'Eje 1': PI 1, 0+080.000", m);
            Assert.Contains("No se reintenta.", m);
            Assert.Contains("C:\\Proyectos\\backups\\prueba.dwg", m);
            var d = Peralte.BloqueDeshacer(Leido((80, "TipoA", -2, -2)), null);
            Assert.Contains("_.UNDO 1", (string)d["como"]);
            Assert.Contains("POR VERIFICAR", (string)d["como"]);
            Assert.Equal("Aplicar peralte en el eje 'Eje 1': 1 curva de peralte (PI 1) con 2 progresivas críticas", Peralte.Accion("Eje 1", Peralte.LeerCurvas(J(UnaCurva))));
        }

        // ------------------------------------------------------------------ peralte entre progresivas críticas (tarea 7.4)
        /// <summary>Curva del PI 1 con la transición completa: bombeo normal, nivelado, invertido, peralte total de 120 a 140 y vuelta.</summary>
        private const string CurvaCompleta = "[{\"pi\": 1, \"estaciones\": ["
            + "{\"progresiva\": 60, \"tipo\": \"EndNormalCrown\", \"pendientes\": {\"Izq\": -2, \"Der\": -2}},"
            + "{\"progresiva\": 75, \"tipo\": \"LevelCrown\", \"pendientes\": {\"Izq\": 0, \"Der\": -2}},"
            + "{\"progresiva\": 90, \"tipo\": \"ReverseCrown\", \"pendientes\": {\"Izq\": 2, \"Der\": -2}},"
            + "{\"progresiva\": 120, \"tipo\": \"BeginFullSuper\", \"pendientes\": {\"Izq\": 6, \"Der\": -6}},"
            + "{\"progresiva\": 140, \"tipo\": \"EndFullSuper\", \"pendientes\": {\"Izq\": 6, \"Der\": -6}},"
            + "{\"progresiva\": 170, \"tipo\": \"BeginNormalCrown\", \"pendientes\": {\"Izq\": -2, \"Der\": -2}}]}]";

        [Fact]
        public void LeeProgresivasOpcionalesEnOrdenYConComa()
        {
            Assert.Null(Peralte.LeerProgresivas(default));
            Assert.Null(Peralte.LeerProgresivas(J("null")));
            Assert.Equal(new[] { 550.0, 100, 280.5 }, Peralte.LeerProgresivas(J("[550, 100, \"280,5\"]")));
        }

        [Theory]
        [InlineData("100", "debe ser una lista JSON")]
        [InlineData("[]", "está vacía")]
        [InlineData("[100, \"x\"]", "La progresiva 2 de 'progresivas' debe ser un número")]
        [InlineData("[100, \"NaN\"]", "número finito")]
        public void RechazaProgresivasMalFormadasSinLeerElDibujo(string json, string mensaje)
        {
            var ex = Assert.Throws<ArgumentException>(() => Peralte.LeerProgresivas(J(json)));
            Assert.Contains(mensaje, ex.Message);
        }

        [Fact]
        public void RechazaMasProgresivasDelMaximo()
        {
            string json = "[" + string.Join(",", Enumerable.Range(1, Peralte.MaximoProgresivas + 1)) + "]";
            var ex = Assert.Throws<ArgumentException>(() => Peralte.LeerProgresivas(J(json)));
            Assert.Contains("el máximo es " + Peralte.MaximoProgresivas, ex.Message);
        }

        [Fact]
        public void PendientesEnProgresivasEnPorcentajeConErroresEnSuLugar()
        {
            var filas = Peralte.PendientesEnProgresivas(new[] { 100.0, 280, 900 }, 0, 831.3274, new[] { "Izq", "Der", "Malo", "Infinito" }, (pk, seg) =>
            {
                if (seg == "Malo") throw new InvalidOperationException("envoltorio", new ArgumentException("eInvalidInput"));
                if (seg == "Infinito") return double.NaN;
                return pk == 280 ? (seg == "Izq" ? 0.06 : -0.06) : -0.02;
            });
            Assert.Equal(3, filas.Count);
            var recta = (Dictionary<string, object>)filas[0]["pendientes"];
            Assert.Equal(-2.0, (double?)recta["Izq"]);
            Assert.Equal("error: ArgumentException: eInvalidInput", recta["Malo"]);
            Assert.StartsWith("error: Civil 3D devolvió un valor no finito", (string)recta["Infinito"]);
            Assert.False(filas[0].ContainsKey("error"));
            var curva = (Dictionary<string, object>)filas[1]["pendientes"];
            Assert.Equal(6.0, (double?)curva["Izq"]);
            Assert.Equal(-6.0, (double?)curva["Der"]);
            Assert.Equal("0+280.000", filas[1]["progresiva_texto"]);
            // Fuera del eje: no se lee
            Assert.Empty((Dictionary<string, object>)filas[2]["pendientes"]);
            Assert.StartsWith("Fuera del eje (de 0+000.000 a 0+831.327)", (string)filas[2]["error"]);
        }

        [Fact]
        public void PuntoDeComprobacionEnElCentroDelPeralteTotal()
        {
            var puntos = Peralte.PuntosEntreProgresivas(Peralte.LeerCurvas(J(CurvaCompleta)));
            var p = Assert.Single(puntos);
            Assert.Equal(1, p.Pi);
            Assert.Equal(130.0, p.Progresiva);
            Assert.Null(p.Motivo);
            Assert.Contains("BeginFullSuper", p.Criterio);
            Assert.Equal(new[] { "Izq", "Der" }, p.Escrito.Select(x => x.Key));
            Assert.Equal(new[] { 6.0, -6.0 }, p.Escrito.Select(x => x.Value));
        }

        [Fact]
        public void SinPeralteTotalUsaElTramoDePendientesIgualesMasInclinado()
        {
            // Sin BeginFullSuper ni EndFullSuper: dos tramos de pendientes iguales (bombeo -2 de 0 a 10 y 5 % de 50 a 70)
            const string json = "[{\"pi\": 1, \"estaciones\": ["
                + "{\"progresiva\": 0, \"tipo\": \"A\", \"pendientes\": {\"Izq\": -2, \"Der\": -2}},"
                + "{\"progresiva\": 10, \"tipo\": \"B\", \"pendientes\": {\"Izq\": -2, \"Der\": -2}},"
                + "{\"progresiva\": 50, \"tipo\": \"C\", \"pendientes\": {\"Izq\": 5, \"Der\": -5}},"
                + "{\"progresiva\": 70, \"tipo\": \"D\", \"pendientes\": {\"izq\": 5, \"Der\": -5}}]}]";
            var p = Assert.Single(Peralte.PuntosEntreProgresivas(Peralte.LeerCurvas(J(json))));
            Assert.Equal(60.0, p.Progresiva);
            Assert.Contains("entre C y D", p.Criterio);
            Assert.Equal(new[] { 5.0, -5.0 }, p.Escrito.Select(x => x.Value));
        }

        [Fact]
        public void BeginYEndFullSuperDistintosSeComparanConLaMedia()
        {
            const string json = "[{\"pi\": 1, \"estaciones\": ["
                + "{\"progresiva\": 100, \"tipo\": \"beginfullsuper\", \"pendientes\": {\"Izq\": 4, \"Der\": -4, \"Solo\": 1}},"
                + "{\"progresiva\": 110, \"tipo\": \"EndFullSuper\", \"pendientes\": {\"Izq\": 6, \"Der\": -6}}]}]";
            var p = Assert.Single(Peralte.PuntosEntreProgresivas(Peralte.LeerCurvas(J(json))));
            Assert.Equal(105.0, p.Progresiva);
            Assert.Equal(new[] { "Izq", "Der" }, p.Escrito.Select(x => x.Key));   // 'Solo' no está en las dos
            Assert.Equal(new[] { 5.0, -5.0 }, p.Escrito.Select(x => x.Value));
        }

        [Fact]
        public void SinTramoNoSeComprueba()
        {
            const string json = "[{\"pi\": 1, \"estaciones\": ["
                + "{\"progresiva\": 100, \"tipo\": \"A\", \"pendientes\": {\"Izq\": -2}},"
                + "{\"progresiva\": 110, \"tipo\": \"B\", \"pendientes\": {\"Izq\": 6}}]}]";
            var p = Assert.Single(Peralte.PuntosEntreProgresivas(Peralte.LeerCurvas(J(json))));
            Assert.Null(p.Progresiva);
            Assert.Contains("no tiene BeginFullSuper y EndFullSuper", p.Motivo);
            var filas = Peralte.ComprobarEntreProgresivas(new[] { p }, (pk, s) => throw new InvalidOperationException("no debe leer"), out var avisos);
            Assert.Null(Assert.Single(filas)["coincide"]);
            Assert.Equal(p.Motivo, Assert.Single(avisos));
        }

        [Fact]
        public void ComprobacionCoincideSinAvisos()
        {
            var puntos = Peralte.PuntosEntreProgresivas(Peralte.LeerCurvas(J(CurvaCompleta)));
            var leidos = new List<double>();
            var filas = Peralte.ComprobarEntreProgresivas(puntos, (pk, s) => { leidos.Add(pk); return s == "Izq" ? 0.0603 : -0.06; }, out var avisos);
            Assert.Empty(avisos);
            Assert.Equal(new[] { 130.0, 130.0 }, leidos);
            Assert.Equal(2, filas.Count);
            Assert.All(filas, f => Assert.Equal(true, f["coincide"]));
            Assert.Equal(6.03, (double?)filas[0]["leido"]);
            Assert.Equal(0.03, (double)filas[0]["diferencia"], 9);
        }

        [Fact]
        public void ComprobacionQueNoCoincideDaAvisoNoError()
        {
            var puntos = Peralte.PuntosEntreProgresivas(Peralte.LeerCurvas(J(CurvaCompleta)));
            // El eje devuelve el bombeo normal en el centro de la curva: lo que pasaría si no interpola entre progresivas
            var filas = Peralte.ComprobarEntreProgresivas(puntos, (pk, s) => s == "Izq" ? -0.02 : throw new Exception("eNotApplicable"), out var avisos);
            Assert.Equal(false, filas[0]["coincide"]);
            Assert.Equal(6.0, (double?)filas[0]["escrito"]);
            Assert.Equal(-2.0, (double?)filas[0]["leido"]);
            Assert.Null(filas[1]["coincide"]);
            Assert.Equal("error: Exception: eNotApplicable", filas[1]["error"]);
            Assert.Equal(2, avisos.Count);
            Assert.Equal("El eje no devuelve el peralte escrito en el pk 0+130.000 (PI 1; segmento Izq: leído -2 %, escrito 6 %): el corredor podría no seguirlo. "
                + "Mira listar_peralte con progresivas.", avisos[0]);
            Assert.StartsWith("No se pudo leer el peralte en el pk 0+130.000 (PI 1) con Alignment.GetCrossSlopeAtStation (segmento Der: error: Exception: eNotApplicable)", avisos[1]);
        }

        [Fact]
        public void ToleranciaDeLaComprobacionEsDeCincoCentesimas()
        {
            var puntos = Peralte.PuntosEntreProgresivas(Peralte.LeerCurvas(J(CurvaCompleta)));
            Peralte.ComprobarEntreProgresivas(puntos, (pk, s) => s == "Izq" ? 0.0606 : -0.06, out var avisos);
            Assert.Single(avisos);   // 6,06 frente a 6: 0,06 puntos > 0,05
            Assert.Contains("leído 6.06 %, escrito 6 %", avisos[0]);
        }

        // ------------------------------------------------------------------ tarea 7.6: bombeo en los extremos del eje
        /// <summary>
        /// El eje de la validación de la ronda E (20/11/2026): de 0 a 831.3274, dos curvas; Civil 3D deja BeginAlignment y
        /// EndAlignment en 0 % y el pk 100 salía a −1.0417 % (interpola de 0 en el pk 0 a −2 en EndNormalCrown, pk 192).
        /// </summary>
        private const string DosCurvasRondaE = "[{\"pi\": 2, \"estaciones\": ["
            + "{\"progresiva\": 192, \"tipo\": \"EndNormalCrown\", \"pendientes\": {\"LeftOutLaneCrossSlope\": -2, \"RightOutLaneCrossSlope\": -2}},"
            + "{\"progresiva\": 220, \"tipo\": \"BeginFullSuper\", \"pendientes\": {\"LeftOutLaneCrossSlope\": 6, \"RightOutLaneCrossSlope\": -6}},"
            + "{\"progresiva\": 345.6637, \"tipo\": \"EndFullSuper\", \"pendientes\": {\"LeftOutLaneCrossSlope\": 6, \"RightOutLaneCrossSlope\": -6}},"
            + "{\"progresiva\": 373.6637, \"tipo\": \"BeginNormalCrown\", \"pendientes\": {\"LeftOutLaneCrossSlope\": -2, \"RightOutLaneCrossSlope\": -2}}]},"
            + "{\"pi\": 3, \"estaciones\": ["
            + "{\"progresiva\": 457.6637, \"tipo\": \"EndNormalCrown\", \"pendientes\": {\"LeftOutLaneCrossSlope\": -2, \"RightOutLaneCrossSlope\": -2}},"
            + "{\"progresiva\": 485.6637, \"tipo\": \"BeginFullSuper\", \"pendientes\": {\"LeftOutLaneCrossSlope\": -6, \"RightOutLaneCrossSlope\": 6}},"
            + "{\"progresiva\": 611.3274, \"tipo\": \"EndFullSuper\", \"pendientes\": {\"LeftOutLaneCrossSlope\": -6, \"RightOutLaneCrossSlope\": 6}},"
            + "{\"progresiva\": 639.3274, \"tipo\": \"BeginNormalCrown\", \"pendientes\": {\"LeftOutLaneCrossSlope\": -2.5, \"RightOutLaneCrossSlope\": -2.5}}]}]";

        private static PeralteLeido EjeRondaE(double izq = 0, double der = 0)
        {
            var p = new PeralteLeido { Alineamiento = "Eje Peralte" };
            foreach (var (pk, tipo) in new[] { (0.0, "BeginAlignment"), (831.3274, "EndAlignment") })
            {
                var e = new EstacionLeida { Progresiva = pk, Tipo = tipo, Curva = "" };
                e.Pendientes["LeftOutLaneCrossSlope"] = izq;
                e.Pendientes["RightOutLaneCrossSlope"] = der;
                p.EstacionesDelEje.Add(e);
            }
            return p;
        }

        [Fact]
        public void BombeoEnLosExtremosSaleDeLaPrimeraYLaUltimaProgresivaDeBombeoNormal()
        {
            var extremos = Peralte.BombeoEnExtremos(Peralte.LeerCurvas(J(DosCurvasRondaE)), EjeRondaE());
            Assert.Equal(2, extremos.Count);
            var ini = extremos[0];
            Assert.Equal("inicio", ini.Extremo);
            Assert.Equal("BeginAlignment", ini.TipoContorno);
            Assert.Equal(0.0, ini.Progresiva);
            Assert.Equal(2, ini.Pi);
            Assert.Equal("EndNormalCrown", ini.TipoOrigen);
            Assert.Equal(192.0, ini.ProgresivaOrigen);
            Assert.Equal(new[] { "LeftOutLaneCrossSlope", "RightOutLaneCrossSlope" }, ini.Pendientes.Select(x => x.Key));
            Assert.Equal(new[] { -2.0, -2.0 }, ini.Pendientes.Select(x => x.Value));
            Assert.Null(ini.Motivo);
            Assert.Null(ini.Escrito);
            var fin = extremos[1];
            Assert.Equal("EndAlignment", fin.TipoContorno);
            Assert.Equal(831.3274, fin.Progresiva);
            Assert.Equal(3, fin.Pi);
            Assert.Equal("BeginNormalCrown", fin.TipoOrigen);
            Assert.Equal(639.3274, fin.ProgresivaOrigen);
            Assert.Equal(new[] { -2.5, -2.5 }, fin.Pendientes.Select(x => x.Value));
            Assert.Null(fin.Motivo);
            var d = ini.ComoDiccionario();
            Assert.Equal(true, d["se_escribe"]);
            Assert.Equal("0+000.000", d["progresiva_texto"]);
            Assert.Equal(-2.0, ((Dictionary<string, object>)d["pendientes"])["LeftOutLaneCrossSlope"]);
            Assert.Equal("EndNormalCrown", ((Dictionary<string, object>)d["desde"])["tipo"]);
        }

        [Fact]
        public void SinProgresivaDeBombeoNormalOSinContornoNoSeEscribeYSeDicePorQue()
        {
            // Una curva sin EndNormalCrown ni BeginNormalCrown, y un eje sin EndAlignment
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 120, \"tipo\": \"BeginFullSuper\", \"pendientes\": {\"Izq\": 6}}]}]"));
            var eje = EjeRondaE();
            eje.EstacionesDelEje.RemoveAll(e => e.Tipo == "EndAlignment");
            var extremos = Peralte.BombeoEnExtremos(curvas, eje);
            Assert.StartsWith("La primera curva pedida (PI 1) no tiene EndNormalCrown (fin del bombeo normal): no se sabe qué bombeo poner en BeginAlignment, que queda como la dejó Civil 3D (0 %)", extremos[0].Motivo);
            Assert.Contains("no tiene BeginNormalCrown", extremos[1].Motivo);
            Assert.Empty(Peralte.DiferenciasExtremos(extremos, eje));
            // Solo los de la curva: los extremos que no se escriben no se comprueban
            Assert.All(Peralte.PuntosDeComprobacion(curvas, extremos), p => Assert.Null(p.Tramo));

            var sinFin = Peralte.BombeoEnExtremos(Peralte.LeerCurvas(J(DosCurvasRondaE)), eje);
            Assert.Null(sinFin[0].Motivo);
            Assert.Null(sinFin[1].Progresiva);
            Assert.StartsWith("El eje no tiene la progresiva de contorno EndAlignment (Civil 3D la crea con el eje)", sinFin[1].Motivo);
        }

        [Fact]
        public void ContornoQueNoQuedaAntesDelBombeoNoSeEscribe()
        {
            var curvas = Peralte.LeerCurvas(J("[{\"pi\": 1, \"estaciones\": [{\"progresiva\": 0, \"tipo\": \"EndNormalCrown\", \"pendientes\": {\"Izq\": -2}},"
                + "{\"progresiva\": 831.3274, \"tipo\": \"BeginNormalCrown\", \"pendientes\": {\"Izq\": -2}}]}]"));
            var extremos = Peralte.BombeoEnExtremos(curvas, EjeRondaE());
            Assert.Equal("BeginAlignment (0+000.000) no queda antes de EndNormalCrown (0+000.000, PI 1): no hay recta inicial y no se le pone el bombeo.", extremos[0].Motivo);
            Assert.Contains("no queda después de BeginNormalCrown", extremos[1].Motivo);
        }

        [Fact]
        public void SinCurvasNoHayExtremos()
        {
            Assert.Empty(Peralte.BombeoEnExtremos(new List<CurvaPeralte>(), EjeRondaE()));
        }

        [Fact]
        public void DiferenciasDeLosExtremosSoloEnLosEscritos()
        {
            var extremos = Peralte.BombeoEnExtremos(Peralte.LeerCurvas(J(DosCurvasRondaE)), EjeRondaE());
            // Simulación (Escrito null): no se verifica
            Assert.Empty(Peralte.DiferenciasExtremos(extremos, EjeRondaE()));
            extremos[0].Escrito = true;
            extremos[1].Escrito = true;
            // Civil 3D dejó el 0 %: el dibujo no refleja el bombeo → diferencias (error sin reintentar)
            var dif = Peralte.DiferenciasExtremos(extremos, EjeRondaE());
            Assert.Equal(4, dif.Count);
            Assert.Equal("BeginAlignment, 0+000.000: pendiente de 'LeftOutLaneCrossSlope' 0 % en vez de -2 % (el bombeo normal)", dif[0]);
            // Con el bombeo escrito, cuadra (el fin lleva -2.5)
            var leido = EjeRondaE(-2, -2);
            leido.EstacionesDelEje[1].Pendientes["LeftOutLaneCrossSlope"] = -2.5;
            leido.EstacionesDelEje[1].Pendientes["RightOutLaneCrossSlope"] = -2.5004;
            Assert.Empty(Peralte.DiferenciasExtremos(extremos, leido));
            // El que falló al escribir no se verifica
            extremos[1].Escrito = false;
            extremos[1].Motivo = Peralte.MensajeBombeoNoEscrito(extremos[1], "eNotApplicable");
            Assert.Equal("No se pudo poner el bombeo normal en EndAlignment (0+831.327) (SuperelevationCriticalStation.SetSlope: eNotApplicable): el corredor podría salir casi plano en la recta final. "
                + "Ponlo en el editor tabular del peralte de Civil 3D y míralo con listar_peralte con progresivas.", extremos[1].Motivo);
            Assert.Single(Peralte.DiferenciasExtremos(extremos, EjeRondaE(0, -2)));   // solo el del inicio (izquierdo en 0)
            var d = extremos[0].ComoDiccionario(leido);
            Assert.Equal(-2.0, ((Dictionary<string, object>)d["leido"])["LeftOutLaneCrossSlope"]);
            Assert.Equal(true, d["escrito"]);
        }

        [Fact]
        public void LasRectasDeLosExtremosSeCompruebanEnSuMitadConElBombeo()
        {
            var curvas = Peralte.LeerCurvas(J(DosCurvasRondaE));
            var extremos = Peralte.BombeoEnExtremos(curvas, EjeRondaE());
            foreach (var b in extremos) b.Escrito = true;
            var puntos = Peralte.PuntosDeComprobacion(curvas, extremos);
            Assert.Equal(4, puntos.Count);   // recta inicial, PI 2, PI 3, recta final
            Assert.Equal("recta inicial", puntos[0].Tramo);
            Assert.Equal(96.0, puntos[0].Progresiva);
            Assert.Equal(2, puntos[0].Pi);
            Assert.Equal(new[] { -2.0, -2.0 }, puntos[0].Escrito.Select(x => x.Value));
            Assert.Equal("recta inicial: mitad entre BeginAlignment (0+000.000) y EndNormalCrown (0+192.000) del PI 2; espera el bombeo normal", puntos[0].Criterio);
            Assert.Null(puntos[1].Tramo);
            Assert.Equal(282.83185, puntos[1].Progresiva.Value, 6);
            Assert.Equal("recta final", puntos[3].Tramo);
            Assert.Equal((639.3274 + 831.3274) / 2, puntos[3].Progresiva.Value, 9);

            // Como en Civil 3D antes de la tarea 7.6: interpola de 0 (pk 0) a -2 (pk 192) → -1 % en el pk 96: aviso
            var filas = Peralte.ComprobarEntreProgresivas(new[] { puntos[0] }, (pk, s) => -0.02 * pk / 192, out var avisos);
            Assert.Equal("recta inicial", filas[0]["tramo"]);
            Assert.Equal(false, filas[0]["coincide"]);
            Assert.Equal(-1.0, (double?)filas[0]["leido"]);
            Assert.Equal("El eje no devuelve el peralte escrito en el pk 0+096.000 (recta inicial, junto al PI 2; segmento LeftOutLaneCrossSlope: leído -1 %, escrito -2 %; "
                + "segmento RightOutLaneCrossSlope: leído -1 %, escrito -2 %): el corredor podría no seguirlo. Mira listar_peralte con progresivas.", Assert.Single(avisos));
            // Con el bombeo en BeginAlignment: -2 en toda la recta, sin aviso
            Peralte.ComprobarEntreProgresivas(new[] { puntos[0] }, (pk, s) => -0.02, out var sinAvisos);
            Assert.Empty(sinAvisos);

            // Un extremo que falló al escribir no se comprueba
            extremos[1].Escrito = false;
            Assert.Equal(3, Peralte.PuntosDeComprobacion(curvas, extremos).Count);
        }

        // ------------------------------------------------------------------ tarea 7.6: carriles que ignoran el peralte
        [Theory]
        [InlineData("UseSuperelevation", 6, null, true)]
        [InlineData("UseSuperelevation", 6L, null, true)]
        [InlineData("usesuperelevation", "6", null, true)]
        [InlineData("UseSuperelevation", 2, "RightLO", false)]
        [InlineData("UseSuperelevation", 0, "LeftLO", false)]
        [InlineData("UseSuperelevation", 99, "None", true)]       // manda la opción leída con GetEnumData
        [InlineData("UseSuperelevation", 6, "RightLO", false)]
        [InlineData("UseSuperelevation", 2, null, false)]
        [InlineData("Usar peralte", 6, "None", false)]            // el nombre visible no cuenta: solo la clave
        [InlineData(null, 6, "None", false)]
        public void CarrilSinPeraltePorClaveYOpcion(string clave, object valor, string opcion, bool esperado)
            => Assert.Equal(esperado, CarrilesPeralte.EsSinPeralte(clave, valor, opcion));

        [Fact]
        public void CarrilesSinPeralteConLosParametrosDeParametrosDetalle()
        {
            var parametros = new List<object>
            {
                new Dictionary<string, object> { ["nombre"] = "Width", ["clave"] = "Width", ["tipo"] = "double", ["valor"] = 3.6 },
                new Dictionary<string, object> { ["nombre"] = "12427", ["clave"] = "UseSuperelevation", ["tipo"] = "long", ["valor"] = 6, ["opcion"] = "None", ["etiqueta"] = "12427" },
                new Dictionary<string, object> { ["nombre"] = "x", ["clave"] = null, ["tipo"] = "long", ["valor"] = 1 }
            };
            Assert.True(CarrilesPeralte.TieneSinPeralte(parametros, out var valor, out var opcion, out int sinClave));
            Assert.Equal(6, valor);
            Assert.Equal("None", opcion);
            Assert.Equal(1, sinClave);
            parametros[1] = new Dictionary<string, object> { ["clave"] = "UseSuperelevation", ["valor"] = 2, ["opcion"] = "RightLO" };
            Assert.False(CarrilesPeralte.TieneSinPeralte(parametros, out _, out _, out _));
        }

        [Fact]
        public void AvisoDeCarrilSinPeralteLiteralYSinRepetir()
        {
            var c = new CarrilSinPeralte { Corredor = "Corredor 1", Ensamblaje = "Ensamblaje 1", Grupo = "Derecha", Subensamblaje = "LaneSuperelevationAOR - (Derecha)", Lado = "derecha", Valor = 6, Opcion = "None" };
            var avisos = CarrilesPeralte.Avisos(new[] { c, c });
            Assert.Equal("El corredor 'Corredor 1' usa el subensamblaje 'LaneSuperelevationAOR - (Derecha)' con UseSuperelevation = None: ese carril no sigue el peralte del eje (validado el 20/11/2026). "
                + "Cámbialo en Propiedades del subensamblaje (por ejemplo, a RightLO/LeftLO según el lado) y reconstruye el corredor.", Assert.Single(avisos));
            var d = c.ComoDiccionario();
            Assert.Equal("UseSuperelevation", d["clave"]);
            Assert.Equal("None", d["opcion"]);
            Assert.StartsWith("No se pudo comprobar si los carriles del corredor 'Corredor 1' siguen el peralte del eje (ensamblaje: eWasErased). Míralo con listar_ensamblajes → parametros_detalle",
                CarrilesPeralte.AvisoNoComprobado("Corredor 1", "ensamblaje", "eWasErased"));
        }
    }
}
