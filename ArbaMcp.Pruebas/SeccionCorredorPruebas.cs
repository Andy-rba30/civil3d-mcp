using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// seccion_corredor (tarea 7.3, ronda E) sin Civil 3D: pendientes de los tramos y de los carriles (Crown → ETW) con
    /// signo al alejarse del eje, perfil superior con capas de pavimento, elección de progresivas calculadas, errores de
    /// rango y forma de la respuesta. Secciones genéricas de prueba (calzada de 2 × 3,6 m, ningún dato real).
    /// </summary>
    public class SeccionCorredorPruebas
    {
        private static PuntoSeccion P(double desplazamiento, double cota, params string[] codigos)
            => new PuntoSeccion { Progresiva = 100, Desplazamiento = desplazamiento, Cota = cota, Codigos = codigos.ToList() };

        /// <summary>Carril de 3,6 m desde el eje hacia un lado (signo = -1 izquierda, +1 derecha) con bombeo o peralte en % al alejarse.</summary>
        private static List<PuntoSeccion> Carril(int signo, double pendientePct, double cotaEje = 100.0)
        {
            double borde = cotaEje + 3.6 * pendientePct / 100.0;
            return new List<PuntoSeccion>
            {
                P(0, cotaEje, "Crown", "Top"),
                P(signo * 3.6, borde, "ETW", "Top"),
                // capa de pavimento debajo: repite los desplazamientos 0,25 m más abajo
                P(0, cotaEje - 0.25, "Crown_Pave1", "Pave1"),
                P(signo * 3.6, borde - 0.25, "ETW_Pave1", "Pave1")
            };
        }

        [Fact]
        public void Bombeo_normal_da_menos_2_en_los_dos_carriles()
        {
            var der = SeccionCorredor.Carril(Carril(+1, -2));
            var izq = SeccionCorredor.Carril(Carril(-1, -2));
            Assert.Equal(-2.0, der.PendientePct.Value, 6);
            Assert.Equal("derecha", der.Lado);
            Assert.Equal(-2.0, izq.PendientePct.Value, 6);
            Assert.Equal("izquierda", izq.Lado);
        }

        [Fact]
        public void Peralte_de_curva_a_la_derecha_baja_a_la_derecha_y_sube_a_la_izquierda()
        {
            // curva a la derecha con peralte del 6 %: el borde derecho (interior) baja y el izquierdo (exterior) sube
            var der = SeccionCorredor.Carril(Carril(+1, -6));
            var izq = SeccionCorredor.Carril(Carril(-1, +6));
            Assert.Equal(-6.0, der.PendientePct.Value, 6);
            Assert.Equal(6.0, izq.PendientePct.Value, 6);
            Assert.Equal(99.784, der.Borde.Cota.Value, 6);
            Assert.Equal(100.216, izq.Borde.Cota.Value, 6);
        }

        [Fact]
        public void Pendiente_alejandose_no_depende_del_orden_de_los_puntos()
        {
            Assert.Equal(-2.0, SeccionCorredor.PendienteAlejandose(-3.6, 99.928, 0, 100).Value, 6);
            Assert.Equal(-2.0, SeccionCorredor.PendienteAlejandose(0, 100, -3.6, 99.928).Value, 6);
            Assert.Equal(-2.0, SeccionCorredor.PendienteAlejandose(3.6, 99.928, 0, 100).Value, 6);
            Assert.Null(SeccionCorredor.PendienteAlejandose(1.0, 100, 1.0002, 99));   // vertical
            // centrado en el eje: de izquierda a derecha
            Assert.Equal(10.0, SeccionCorredor.PendienteAlejandose(-1, 100, 1, 100.2).Value, 6);
        }

        [Fact]
        public void Tramos_usan_el_perfil_superior_y_no_mezclan_las_capas()
        {
            var puntos = Carril(+1, -2);
            puntos.Add(P(4.6, 99.928 - 0.04, "EPS"));   // arcén de 1 m al -4 %
            var tramos = SeccionCorredor.Tramos(puntos);
            Assert.Equal(2, tramos.Count);
            Assert.Equal(-2.0, tramos[0].PendientePct.Value, 6);
            Assert.Equal(-4.0, tramos[1].PendientePct.Value, 6);
            Assert.All(tramos, t => Assert.Equal("derecha", t.Lado));
            Assert.Contains("Crown", tramos[0].Desde.Codigos);
            Assert.Contains("ETW", tramos[0].Hasta.Codigos);
            Assert.Equal(3.6, tramos[0].Ancho, 6);
        }

        [Fact]
        public void Tramos_a_la_izquierda_se_ordenan_por_desplazamiento_y_miden_al_alejarse()
        {
            var puntos = Carril(-1, -2);
            puntos.Add(P(-4.6, 99.928 - 0.04, "EPS"));
            var tramos = SeccionCorredor.Tramos(puntos);
            Assert.Equal(2, tramos.Count);
            Assert.Equal(-4.6, tramos[0].Desde.Desplazamiento.Value, 6);   // de izquierda a derecha
            Assert.Equal(-4.0, tramos[0].PendientePct.Value, 6);
            Assert.Equal(-2.0, tramos[1].PendientePct.Value, 6);
        }

        [Fact]
        public void Puntos_incompletos_quedan_al_final_y_no_forman_tramos()
        {
            var puntos = new List<PuntoSeccion>
            {
                new PuntoSeccion { Desplazamiento = null, Cota = 5 },
                P(3.6, 99.928, "ETW"),
                P(0, 100, "Crown")
            };
            var orden = SeccionCorredor.OrdenarPorDesplazamiento(puntos);
            Assert.Equal(0, orden[0].Desplazamiento.Value);
            Assert.Null(orden[2].Desplazamiento);
            Assert.Single(SeccionCorredor.Tramos(puntos));
        }

        [Fact]
        public void Carril_sin_ETW_o_sin_Crown_es_null_y_los_codigos_no_distinguen_mayusculas()
        {
            Assert.Null(SeccionCorredor.Carril(new[] { P(0, 100, "Crown"), P(3.6, 99.9, "EPS") }));
            Assert.Null(SeccionCorredor.Carril(new[] { P(0, 100, "Top"), P(3.6, 99.9, "ETW") }));
            Assert.Null(SeccionCorredor.Carril(null));
            var c = SeccionCorredor.Carril(new[] { P(0, 100, "crown"), P(3.6, 99.928, "etw") });
            Assert.Equal(-2.0, c.PendientePct.Value, 6);
        }

        [Fact]
        public void Carril_con_varios_Crown_toma_el_mas_alto_del_eje()
        {
            var puntos = new List<PuntoSeccion> { P(0, 99.75, "Crown"), P(0, 100, "Crown"), P(3.6, 99.928, "ETW") };
            var c = SeccionCorredor.Carril(puntos);
            Assert.Equal(100, c.Corona.Cota.Value, 6);
            Assert.Equal(-2.0, c.PendientePct.Value, 6);
        }

        [Fact]
        public void En_rango_y_error_fuera_de_rango_con_el_rango_de_cada_linea_base()
        {
            Assert.True(SeccionCorredor.EnRango(0, 0, 831.3274));
            Assert.True(SeccionCorredor.EnRango(831.32745, 0, 831.3274));
            Assert.False(SeccionCorredor.EnRango(900, 0, 831.3274));
            Assert.False(SeccionCorredor.EnRango(10, null, 831.3274));

            var antes = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("es-ES");
                var rangos = new List<(string, double?, double?)> { ("Línea base 1", 0, 831.3274), ("Línea base 2", null, null) };
                string e = SeccionCorredor.ErrorFueraDeRango("Corredor 1", 1200, rangos);
                Assert.Equal("La progresiva 1200 está fuera del rango de ninguna línea base del corredor 'Corredor 1'. Rangos: Línea base 1: de 0 a 831.3274; Línea base 2: rango no legible. Usa listar_corredores.", e);
                string e2 = SeccionCorredor.ErrorFueraDeRango("Corredor 1", 900.5, rangos.Take(1), "Línea base 1");
                Assert.Contains("fuera del rango de la línea base 'Línea base 1' del corredor 'Corredor 1'", e2);
                Assert.Contains("900.5", e2);
                Assert.Contains("el corredor no tiene líneas base", SeccionCorredor.ErrorFueraDeRango("Corredor 1", 5, new List<(string, double?, double?)>()));
            }
            finally { CultureInfo.CurrentCulture = antes; }
        }

        [Fact]
        public void Estaciones_vecinas_de_una_progresiva()
        {
            var est = new double[] { 30, 0, 10, 20 };
            var v = SeccionCorredor.EstacionesVecinas(est, 14);
            Assert.Equal(10, v.anterior);
            Assert.Equal(20, v.siguiente);
            Assert.Equal(10, v.masCercana);
            Assert.False(v.exacta);

            var w = SeccionCorredor.EstacionesVecinas(est, 20.00001);
            Assert.True(w.exacta);
            Assert.Equal(20, w.anterior);
            Assert.Equal(20, w.siguiente);
            Assert.Equal(20, w.masCercana);

            var fuera = SeccionCorredor.EstacionesVecinas(est, 50);
            Assert.Equal(30, fuera.anterior);
            Assert.Null(fuera.siguiente);

            var vacia = SeccionCorredor.EstacionesVecinas(null, 5);
            Assert.Null(vacia.masCercana);
            Assert.False(vacia.exacta);
        }

        [Fact]
        public void Normalizar_valores_de_parametros()
        {
            Assert.Equal(1.2346, (double)SeccionCorredor.Normalizar(1.234567));
            Assert.Equal(3, SeccionCorredor.Normalizar(3));
            Assert.Equal(true, SeccionCorredor.Normalizar(true));
            Assert.Equal("Crown", SeccionCorredor.Normalizar("Crown"));
            Assert.Null(SeccionCorredor.Normalizar(null));
            Assert.Null(SeccionCorredor.Normalizar(double.NaN));
            Assert.Equal("Otro", SeccionCorredor.Normalizar(new Otro()));
        }

        private sealed class Otro { public override string ToString() => "Otro"; }

        [Fact]
        public void Aviso_si_la_X_de_los_puntos_no_es_la_progresiva_usada()
        {
            var l = new LineaBaseSeccion { Nombre = "Línea base 1", ProgresivaUsada = 100 };
            l.Subensamblajes.Add(new SubensamblajeSeccion { Nombre = "Carril derecho", Puntos = Carril(+1, -2) });
            Assert.Null(SeccionCorredor.AvisoEjes(l));

            l.Subensamblajes[0].Puntos[1].Progresiva = 3.6;   // como si X fuese el desplazamiento
            string aviso = SeccionCorredor.AvisoEjes(l);
            Assert.NotNull(aviso);
            Assert.Contains("por verificar", aviso);
            Assert.Contains(aviso, (List<string>)l.ComoDiccionario()["avisos"]);
        }

        [Fact]
        public void Respuesta_con_carriles_por_linea_base_y_aviso_de_corredor_desactualizado()
        {
            var linea = new LineaBaseSeccion
            {
                Nombre = "Línea base 1", Alineamiento = "Eje 1", Perfil = "Rasante 1", Inicio = 0, Fin = 831.3274,
                Region = "Región 1", Ensamblaje = "Ensamblaje 1", ProgresivaUsada = 100, EstacionAnterior = 100, EstacionSiguiente = 100
            };
            linea.Subensamblajes.Add(new SubensamblajeSeccion
            {
                Nombre = "LaneSuperelevationAOR - (Right)", Clase = "Subassembly.LaneSuperelevationAOR", Lado = "derecha",
                Parametros = { new ParametroAplicado { Clave = "Width", Nombre = "Width", Valor = 3.6, ValorDiseno = 3.6, Modificado = false } },
                Puntos = Carril(+1, -6)
            });
            linea.Subensamblajes.Add(new SubensamblajeSeccion { Nombre = "DaylightBasic - (Right)", Lado = "derecha", Puntos = { P(3.6, 99.784, "Daylight") } });
            var r = new ResultadoSeccionCorredor { Corredor = "Corredor 1", ProgresivaPedida = 100, EstaDesactualizado = true };
            r.Lineas.Add(linea);

            var d = r.ComoDiccionario();
            Assert.Equal("Corredor 1", d["corredor"]);
            Assert.Equal("0+100.000", d["progresiva_texto"]);
            Assert.Contains((List<string>)d["avisos"], a => a.Contains("desactualizado"));
            var lb = (Dictionary<string, object>)((List<object>)d["lineas_base"])[0];
            Assert.Equal("Eje 1", lb["alineamiento"]);
            Assert.Equal(2, lb["numero_subensamblajes"]);
            var carriles = (List<object>)lb["carriles"];
            var carril = (Dictionary<string, object>)Assert.Single(carriles);
            Assert.Equal("LaneSuperelevationAOR - (Right)", carril["subensamblaje"]);
            Assert.Equal(-6.0, (double)carril["pendiente_pct"], 6);
            var sub = (Dictionary<string, object>)((List<object>)lb["subensamblajes"])[0];
            var par = (Dictionary<string, object>)((List<object>)sub["parametros"])[0];
            Assert.Equal("Width", par["clave"]);
            Assert.Equal(3.6, par["valor"]);
            Assert.Equal(4, ((List<object>)sub["puntos"]).Count);
            Assert.Single((List<object>)sub["tramos"]);
            var sinCarril = (Dictionary<string, object>)((List<object>)lb["subensamblajes"])[1];
            Assert.Null(sinCarril["pendiente_carril"]);
        }
    }
}
