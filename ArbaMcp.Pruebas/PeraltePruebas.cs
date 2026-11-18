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
    }
}
