using System;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Tabla de PI de listar_pis (1.15.0) sin Civil 3D: PI con curva y sin curva, tipos no soportados, comprobaciones,
    /// formatos de progresiva y de grados-minutos-segundos. Las entidades se construyen a partir del plan de
    /// crear_alineamiento_desde_polilinea (ArbaMcp.Nucleo.Alineamiento), así la tabla debe devolver los mismos PI que se
    /// planificaron. Coordenadas genéricas del eje de prueba (ningún dato real).
    /// </summary>
    public class TablaPiPruebas
    {
        private static List<Punto2> Puntos(params double[] xy)
        {
            var l = new List<Punto2>();
            for (int i = 0; i < xy.Length; i += 2) l.Add(new Punto2(xy[i], xy[i + 1]));
            return l;
        }

        private static Punto2 Unitario(Punto2 a, Punto2 b)
        {
            double d = a.DistanciaA(b);
            return new Punto2((b.X - a.X) / d, (b.Y - a.Y) / d);
        }

        private static EntidadEje Linea(Punto2 a, Punto2 b, double sa, double sb)
            => new EntidadEje { Tipo = EntidadEje.Tangente, PuntoInicio = a, PuntoFin = b, ProgresivaInicio = sa, ProgresivaFin = sb };

        /// <summary>
        /// Entidades que Civil 3D tendría para el eje del plan: tangente, curva, tangente... con los PC y PT a T del vértice,
        /// las progresivas del plan, PIStation = PC + T y el PIPoint en el vértice.
        /// </summary>
        private static List<EntidadEje> EntidadesDesdePlan(PlanAlineamiento plan)
        {
            var v = plan.Vertices;
            var lista = new List<EntidadEje>();
            Punto2 desde = v[0];
            double sDesde = plan.ProgresivaInicial;
            for (int k = 1; k <= plan.NumeroPi; k++)
            {
                var pi = plan.Pis[k - 1];
                Punto2 V = v[k];
                if (pi.Tangente.HasValue)
                {
                    double t = pi.Tangente.Value;
                    var uin = Unitario(v[k - 1], V);
                    var uout = Unitario(V, v[k + 1]);
                    var pc = new Punto2(V.X - t * uin.X, V.Y - t * uin.Y);
                    var pt = new Punto2(V.X + t * uout.X, V.Y + t * uout.Y);
                    lista.Add(Linea(desde, pc, sDesde, pi.ProgresivaPc.Value));
                    lista.Add(new EntidadEje
                    {
                        Tipo = EntidadEje.Curva,
                        PuntoInicio = pc,
                        PuntoFin = pt,
                        ProgresivaInicio = pi.ProgresivaPc,
                        ProgresivaFin = pi.ProgresivaPt,
                        Radio = pi.RadioAplicado.Value,
                        DeflexionRad = pi.DeflexionGrados * Math.PI / 180,
                        Horario = pi.Giro == "derecha",
                        ProgresivaPi = pi.Progresiva,
                        PiCivil3d = V
                    });
                    desde = pt;
                    sDesde = pi.ProgresivaPt.Value;
                }
                else
                {
                    lista.Add(Linea(desde, V, sDesde, pi.Progresiva));
                    desde = V;
                    sDesde = pi.Progresiva;
                }
            }
            lista.Add(Linea(desde, v[v.Count - 1], sDesde, plan.ProgresivaFinal));
            return lista;
        }

        private static ResultadoTablaPi TablaDe(PlanAlineamiento plan)
            => TablaPi.Calcular("Eje 1", EntidadesDesdePlan(plan), plan.ProgresivaInicial, plan.ProgresivaFinal,
                plan.Vertices[0], plan.Vertices[plan.Vertices.Count - 1], "Meters", plan.LongitudTotal);

        // ---------------------------------------------------------------- el eje de prueba (PRUEBA_112.dwg): radio 80 en los dos PI
        [Fact]
        public void EjeDePruebaConDosCurvasDeRadio80()
        {
            var plan = Alineamiento.Planificar(Puntos(1000, 2000, 1300, 2000, 1300, 2300, 1600, 2300), 80);
            var tabla = TablaDe(plan);
            Assert.Equal(2, tabla.Pis.Count);
            Assert.Equal(2, tabla.NumeroCurvas);
            Assert.Empty(tabla.Avisos);

            var d = tabla.ComoDiccionario();
            Assert.Equal("Eje 1", d["alineamiento"]);
            Assert.Equal("Meters", d["unidades"]);
            Assert.Equal(831.3274, d["longitud"]);
            var inicio = (Dictionary<string, object>)d["inicio"];
            var fin = (Dictionary<string, object>)d["fin"];
            Assert.Equal("0+000.000", inicio["progresiva_texto"]);
            Assert.Equal(1000.0, inicio["x"]);
            Assert.Equal(2000.0, inicio["y"]);
            Assert.Equal(831.3274, fin["progresiva"]);
            Assert.Equal("0+831.327", fin["progresiva_texto"]);
            Assert.Equal(1600.0, fin["x"]);
            Assert.Equal(2300.0, fin["y"]);

            var pis = (List<Dictionary<string, object>>)d["pis"];
            var pi1 = pis[0];
            Assert.Equal(1, pi1["numero"]);
            Assert.Equal("curva", pi1["tipo"]);
            Assert.Equal(true, pi1["tiene_curva"]);
            Assert.Equal(300.0, pi1["progresiva"]);
            Assert.Equal("0+300.000", pi1["progresiva_texto"]);
            Assert.Equal(1300.0, pi1["x"]);
            Assert.Equal(2000.0, pi1["y"]);
            Assert.Equal(90.0, pi1["deflexion_grados"]);
            Assert.Equal("90°00'00.00\"", pi1["deflexion_gms"]);
            Assert.Equal("izquierda", pi1["giro"]);
            Assert.Equal(80.0, pi1["radio"]);
            Assert.Equal(125.6637, pi1["longitud_curva"]);
            Assert.Equal(80.0, pi1["tangente"]);
            Assert.Equal(220.0, pi1["progresiva_pc"]);
            Assert.Equal("0+220.000", pi1["progresiva_pc_texto"]);
            Assert.Equal(345.6637, pi1["progresiva_pt"]);
            Assert.Equal("0+345.664", pi1["progresiva_pt_texto"]);
            Assert.Equal(0.0, pi1["diferencia_pi_civil3d"]);
            Assert.Null(pi1["aviso"]);

            var pi2 = pis[1];
            Assert.Equal(2, pi2["numero"]);
            Assert.Equal(565.6637, pi2["progresiva"]);
            Assert.Equal("0+565.664", pi2["progresiva_texto"]);
            Assert.Equal(1300.0, pi2["x"]);
            Assert.Equal(2300.0, pi2["y"]);
            Assert.Equal("derecha", pi2["giro"]);
            Assert.Equal(80.0, pi2["radio"]);
            Assert.Equal(125.6637, pi2["longitud_curva"]);
            Assert.Equal(485.6637, pi2["progresiva_pc"]);
            Assert.Equal(611.3274, pi2["progresiva_pt"]);
            Assert.Equal("0+611.327", pi2["progresiva_pt_texto"]);
        }

        // ---------------------------------------------------------------- PI sin curva (radios_por_pi [0, 80], el caso de VALIDACION_115)
        [Fact]
        public void PiSinCurvaEnElPrimerVertice()
        {
            var plan = Alineamiento.Planificar(Puntos(1000, 2000, 1300, 2000, 1300, 2300, 1600, 2300), 80, new List<double> { 0, 80 });
            var tabla = TablaDe(plan);
            Assert.Empty(tabla.Avisos);
            Assert.Equal(2, tabla.Pis.Count);
            Assert.Equal(1, tabla.NumeroCurvas);

            var pi1 = tabla.Pis[0].ComoDiccionario();
            Assert.Equal("sin_curva", pi1["tipo"]);
            Assert.Equal(false, pi1["tiene_curva"]);
            Assert.Equal(300.0, pi1["progresiva"]);
            Assert.Equal(1300.0, pi1["x"]);
            Assert.Equal(2000.0, pi1["y"]);
            Assert.Equal(90.0, pi1["deflexion_grados"]);
            Assert.Equal("izquierda", pi1["giro"]);
            foreach (var campo in new[] { "radio", "longitud_curva", "tangente", "progresiva_pc", "progresiva_pc_texto", "progresiva_pt", "progresiva_pt_texto", "diferencia_pi_civil3d", "aviso" })
                Assert.Null(pi1[campo]);

            var pi2 = tabla.Pis[1].ComoDiccionario();
            Assert.Equal(600.0, pi2["progresiva"]);
            Assert.Equal(1300.0, pi2["x"]);
            Assert.Equal(2300.0, pi2["y"]);
            Assert.Equal(520.0, pi2["progresiva_pc"]);
            Assert.Equal(645.6637, pi2["progresiva_pt"]);
            Assert.Equal("derecha", pi2["giro"]);
            Assert.Equal(865.6637, ((Dictionary<string, object>)tabla.ComoDiccionario()["fin"])["progresiva"]);
        }

        // ---------------------------------------------------------------- la tabla devuelve los PI planificados, en varios ejes
        [Theory]
        [InlineData(new double[] { 0, 0, 200, 0, 200, 200, 400, 200, 400, 400 }, new double[] { 80, 0, 40 })]
        [InlineData(new double[] { 0, 0, 100, 0, 150, -86.6025403784, 300, -86.6025403784 }, new double[] { 60, 25 })]
        [InlineData(new double[] { 1000, 1000, 1250, 1100, 1400, 1050, 1700, 1300 }, new double[] { 0, 0 })]
        public void CoincideConElPlanDeCrearAlineamiento(double[] xy, double[] radios)
        {
            var plan = Alineamiento.Planificar(Puntos(xy), 50, radios.ToList(), 100);
            var tabla = TablaDe(plan);
            Assert.Empty(tabla.Avisos);
            Assert.Equal(plan.NumeroPi, tabla.Pis.Count);
            Assert.Equal(plan.NumeroCurvas, tabla.NumeroCurvas);
            for (int i = 0; i < plan.NumeroPi; i++)
            {
                var esperado = plan.Pis[i];
                var fila = tabla.Pis[i];
                Assert.Equal(i + 1, fila.Numero);
                Assert.Equal(esperado.Progresiva, fila.Progresiva.Value, 6);
                Assert.Equal(esperado.X, fila.X.Value, 6);
                Assert.Equal(esperado.Y, fila.Y.Value, 6);
                Assert.Equal(esperado.DeflexionGrados, fila.DeflexionGrados.Value, 6);
                Assert.Equal(esperado.Giro, fila.Giro);
                Assert.Equal(esperado.RadioAplicado.HasValue, fila.TieneCurva);
                if (!fila.TieneCurva)
                {
                    Assert.Null(fila.Radio);
                    Assert.Null(fila.ProgresivaPc);
                    continue;
                }
                Assert.Equal(esperado.RadioAplicado.Value, fila.Radio.Value, 6);
                Assert.Equal(esperado.LongitudCurva.Value, fila.LongitudCurva.Value, 6);
                Assert.Equal(esperado.Tangente.Value, fila.Tangente.Value, 6);
                Assert.Equal(esperado.ProgresivaPc.Value, fila.ProgresivaPc.Value, 6);
                Assert.Equal(esperado.ProgresivaPt.Value, fila.ProgresivaPt.Value, 6);
                Assert.True(fila.DiferenciaPiCivil3d.Value < 1e-6);
            }
        }

        // ---------------------------------------------------------------- tipos no soportados
        [Fact]
        public void EspiralUOtroTipoDaUnaFilaConAvisoSinRadio()
        {
            var entidades = new List<EntidadEje>
            {
                Linea(new Punto2(0, 0), new Punto2(100, 0), 0, 100),
                new EntidadEje { Tipo = "AlignmentSCS", ProgresivaInicio = 100, ProgresivaFin = 300 },
                Linea(new Punto2(250, 120), new Punto2(250, 400), 300, 580)
            };
            var tabla = TablaPi.Calcular("Eje E", entidades, 0, 580, new Punto2(0, 0), new Punto2(250, 400));
            var fila = Assert.Single(tabla.Pis);
            var d = fila.ComoDiccionario();
            Assert.Equal(1, d["numero"]);
            Assert.Equal("AlignmentSCS", d["tipo"]);
            Assert.Equal(false, d["tiene_curva"]);
            Assert.Null(d["radio"]);
            Assert.Null(d["progresiva"]);
            Assert.Contains("tipo no soportado todavía", (string)d["aviso"]);
            Assert.Contains("de 0+100.000 a 0+300.000", (string)d["aviso"]);
            Assert.Single(tabla.Avisos);
            Assert.Null(tabla.ComoDiccionario()["unidades"]);
        }

        // ---------------------------------------------------------------- ejes sin PI
        [Fact]
        public void UnaSolaTangenteNoTienePiYUnEjeVacioAvisa()
        {
            var una = TablaPi.Calcular("Recta", new List<EntidadEje> { Linea(new Punto2(0, 0), new Punto2(500, 0), 0, 500) }, 0, 500, new Punto2(0, 0), new Punto2(500, 0));
            Assert.Empty(una.Pis);
            Assert.Contains("una sola tangente", Assert.Single(una.Avisos));
            Assert.Equal(0, una.ComoDiccionario()["numero_pi"]);

            var vacio = TablaPi.Calcular("Vacío", new List<EntidadEje>(), 0, 0, null, null);
            Assert.Empty(vacio.Pis);
            Assert.Contains("no tiene entidades", Assert.Single(vacio.Avisos));
            Assert.Null(((Dictionary<string, object>)vacio.ComoDiccionario()["inicio"])["x"]);
        }

        // ---------------------------------------------------------------- comprobaciones
        [Fact]
        public void AvisaSiElPiDeCivil3dLaProgresivaOLasTangentesNoCuadran()
        {
            var plan = Alineamiento.Planificar(Puntos(1000, 2000, 1300, 2000, 1300, 2300, 1600, 2300), 80);
            var entidades = EntidadesDesdePlan(plan);
            entidades[1].PiCivil3d = new Punto2(1300.01, 2000);   // PIPoint 1 cm al este
            entidades[3].ProgresivaPi = 566;                       // PIStation que no es PC + T
            var tabla = TablaPi.Calcular("Eje 1", entidades, 0, plan.ProgresivaFinal, plan.Vertices[0], plan.Vertices[3]);
            Assert.Equal(2, tabla.Avisos.Count);
            Assert.Equal(0.01, tabla.Pis[0].DiferenciaPiCivil3d.Value, 6);
            Assert.Contains("PI 1", tabla.Pis[0].Aviso);
            Assert.Contains("difieren en 0.01", tabla.Pis[0].Aviso);
            Assert.Equal(1300.0, tabla.Pis[0].X.Value, 6);          // la coordenada es la calculada, no la de Civil 3D
            Assert.Contains("no es PC + T", tabla.Pis[1].Aviso);
            Assert.Equal(566.0, tabla.Pis[1].Progresiva.Value);   // la progresiva es la de Civil 3D

            // Dos tangentes seguidas que no se tocan
            var separadas = new List<EntidadEje>
            {
                Linea(new Punto2(0, 0), new Punto2(100, 0), 0, 100),
                Linea(new Punto2(100, 0.5), new Punto2(100, 100), 100, 199.5)
            };
            var t2 = TablaPi.Calcular("Eje S", separadas, 0, 199.5, new Punto2(0, 0), new Punto2(100, 100));
            Assert.Contains("no se tocan", Assert.Single(t2.Avisos));
            Assert.Equal(0.0, t2.Pis[0].Y.Value);                 // el final de la primera tangente
        }

        [Fact]
        public void UnaCurvaSolaSinTangentesTambienDaSuPi()
        {
            // Curva de 90° a la derecha con R 50 de (0, 0) hacia el norte: PT en (50, 50), PI en (0, 50)
            var arco = new EntidadEje
            {
                Tipo = EntidadEje.Curva, PuntoInicio = new Punto2(0, 0), PuntoFin = new Punto2(50, 50),
                ProgresivaInicio = 0, ProgresivaFin = 25 * Math.PI, Radio = 50, DeflexionRad = Math.PI / 2, Horario = true
            };
            var tabla = TablaPi.Calcular("Arco", new List<EntidadEje> { arco }, 0, 25 * Math.PI, new Punto2(0, 0), new Punto2(50, 50));
            var fila = Assert.Single(tabla.Pis);
            Assert.Empty(tabla.Avisos);
            Assert.Equal(0.0, fila.X.Value, 6);
            Assert.Equal(50.0, fila.Y.Value, 6);
            Assert.Equal(50.0, fila.Progresiva.Value, 6);          // sin PIStation: PC + T
            Assert.Null(fila.DiferenciaPiCivil3d);                 // sin PIPoint no se compara
        }

        [Fact]
        public void PiDeCurvaDe180GradosNoEstaDefinido()
        {
            Assert.Null(TablaPi.PiDeCurva(new Punto2(0, 0), new Punto2(0, 100), 50, Math.PI, false));
            Assert.Null(TablaPi.PiDeCurva(new Punto2(0, 0), new Punto2(0, 0), 50, Math.PI / 2, false));
            var izquierda = TablaPi.PiDeCurva(new Punto2(0, 0), new Punto2(50, 50), 50, Math.PI / 2, false).Value;
            Assert.Equal(50.0, izquierda.X, 6);                    // entra hacia el este y gira a la izquierda
            Assert.Equal(0.0, izquierda.Y, 6);

            var arco = new EntidadEje
            {
                Tipo = EntidadEje.Curva, PuntoInicio = new Punto2(0, 0), PuntoFin = new Punto2(0, 100),
                ProgresivaInicio = 0, ProgresivaFin = 50 * Math.PI, Radio = 50, DeflexionRad = Math.PI, Horario = false
            };
            var fila = Assert.Single(TablaPi.Calcular("U", new List<EntidadEje> { arco }, 0, 50 * Math.PI, null, null).Pis);
            Assert.Null(fila.X);
            Assert.Null(fila.Tangente);
            Assert.Contains("180°", fila.Aviso);
        }

        // ---------------------------------------------------------------- formatos
        [Theory]
        [InlineData(0, "0+000.000")]
        [InlineData(300, "0+300.000")]
        [InlineData(831.3274, "0+831.327")]
        [InlineData(345.6637, "0+345.664")]
        [InlineData(1234.5, "1+234.500")]
        [InlineData(999.9996, "1+000.000")]
        [InlineData(12345.6789, "12+345.679")]
        [InlineData(-50, "-0+050.000")]
        [InlineData(-0.0001, "0+000.000")]
        public void ProgresivaConElFormatoDeCivil3d(double pk, string texto)
        {
            Assert.Equal(texto, TablaPi.Progresiva(pk));
        }

        [Theory]
        [InlineData(90, "90°00'00.00\"")]
        [InlineData(45.5, "45°30'00.00\"")]
        [InlineData(12.3456, "12°20'44.16\"")]
        [InlineData(29.9999999, "30°00'00.00\"")]
        [InlineData(0, "0°00'00.00\"")]
        [InlineData(-10.5, "-10°30'00.00\"")]
        public void GradosMinutosSegundosComoElInformeDeCivil3d(double grados, string texto)
        {
            Assert.Equal(texto, TablaPi.GradosMinutosSegundos(grados));
        }

        [Fact]
        public void LaRespuestaTieneTodosLosCampos()
        {
            var plan = Alineamiento.Planificar(Puntos(0, 0, 100, 0, 100, 100), 50);
            var d = TablaDe(plan).ComoDiccionario();
            foreach (var campo in new[] { "alineamiento", "unidades", "longitud", "inicio", "fin", "numero_pi", "numero_curvas", "pis", "avisos" })
                Assert.True(d.ContainsKey(campo), campo);
            var fila = ((List<Dictionary<string, object>>)d["pis"]).Single();
            foreach (var campo in new[] { "numero", "tipo", "tiene_curva", "progresiva", "progresiva_texto", "x", "y", "deflexion_grados", "deflexion_gms", "giro", "radio", "longitud_curva", "tangente", "progresiva_pc", "progresiva_pc_texto", "progresiva_pt", "progresiva_pt_texto", "diferencia_pi_civil3d", "aviso" })
                Assert.True(fila.ContainsKey(campo), campo);
            foreach (var campo in new[] { "progresiva", "progresiva_texto", "x", "y" })
                Assert.True(((Dictionary<string, object>)d["inicio"]).ContainsKey(campo), campo);
        }
    }
}
