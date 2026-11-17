using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Una progresiva crítica de peralte pedida a aplicar_peralte (1.20.0). Los nombres de 'Tipo', 'Region' y de los
    /// segmentos de 'Pendientes' son los de los enumerados de Civil 3D (SuperelevationCriticalStationType,
    /// SuperelevationAttainmentRegionType y SuperelevationCrossSegmentType) tal como los lista listar_peralte en
    /// 'enumerados': el núcleo no los conoce ni los inventa; el plugin los resuelve en tiempo de ejecución.
    /// </summary>
    public sealed class EstacionPeralte
    {
        public double Progresiva;
        public string Tipo;
        /// <summary>Null si no se pidió: se usa la sobrecarga Add(progresiva, tipo).</summary>
        public string Region;
        /// <summary>Segmento → pendiente transversal en %, en el orden pedido.</summary>
        public List<KeyValuePair<string, double>> Pendientes = new List<KeyValuePair<string, double>>();

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["progresiva"] = Argumentos.Redondear(Progresiva),
            ["progresiva_texto"] = TablaPi.Progresiva(Progresiva),
            ["tipo"] = Tipo,
            ["region"] = Region,
            ["pendientes"] = Pendientes.ToDictionary(p => p.Key, p => (object)Argumentos.Redondear(p.Value))
        };
    }

    /// <summary>Una curva de peralte pedida: el PI de la curva circular (numeración de listar_pis) y sus progresivas críticas.</summary>
    public sealed class CurvaPeralte
    {
        public int Pi;
        public List<EstacionPeralte> Estaciones = new List<EstacionPeralte>();
        /// <summary>Datos de la curva del eje (los rellena Peralte.ComprobarContraEje con la tabla de PI).</summary>
        public double? Radio, ProgresivaPc, ProgresivaPt;
        public string Giro;
    }

    /// <summary>Una progresiva crítica tal como la lee el plugin del eje (listar_peralte y la relectura de aplicar_peralte).</summary>
    public sealed class EstacionLeida
    {
        public double Progresiva;
        /// <summary>SuperelevationCriticalStation.StationType, como texto del enumerado.</summary>
        public string Tipo;
        /// <summary>SuperelevationCriticalStation.TransitionRegionType, como texto del enumerado (SuperelevationTransitionRegionType).</summary>
        public string Region;
        /// <summary>SuperelevationCriticalStation.SuperelevationCurveName.</summary>
        public string Curva;
        /// <summary>SuperelevationCriticalStation.TransitionDescription, como texto.</summary>
        public string Descripcion;
        /// <summary>Segmento → pendiente en % (la de GetSlope × 100); solo los segmentos que se pudieron leer.</summary>
        public Dictionary<string, double> Pendientes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["progresiva"] = Argumentos.Redondear(Progresiva),
            ["progresiva_texto"] = TablaPi.Progresiva(Progresiva),
            ["tipo"] = Tipo,
            ["region"] = Region,
            ["descripcion"] = Descripcion,
            ["pendientes"] = Pendientes.ToDictionary(p => p.Key, p => (object)Argumentos.Redondear(p.Value))
        };
    }

    /// <summary>Una curva de peralte del eje (SuperelevationCurve) con sus progresivas críticas.</summary>
    public sealed class CurvaLeida
    {
        public string Nombre;
        public double? Inicio, Fin;
        public List<EstacionLeida> Estaciones = new List<EstacionLeida>();

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["nombre"] = Nombre,
            ["progresiva_inicio"] = Inicio.HasValue ? Argumentos.Redondear(Inicio.Value) : null,
            ["progresiva_fin"] = Fin.HasValue ? Argumentos.Redondear(Fin.Value) : null,
            ["numero_estaciones"] = Estaciones.Count,
            ["estaciones"] = Estaciones.OrderBy(e => e.Progresiva).Select(e => e.ComoDiccionario()).ToList()
        };
    }

    /// <summary>Lo que el plugin lee del peralte de un eje.</summary>
    public sealed class PeralteLeido
    {
        public string Alineamiento;
        public List<CurvaLeida> Curvas = new List<CurvaLeida>();
        /// <summary>Progresivas críticas de Alignment.SuperelevationCriticalStations (la colección del eje, aparte de las de cada curva).</summary>
        public List<EstacionLeida> EstacionesDelEje = new List<EstacionLeida>();
        public int NumeroEstacionesCurvas => Curvas.Sum(c => c.Estaciones.Count);
        public bool TienePeralte => Curvas.Count > 0 || EstacionesDelEje.Count > 0;
    }

    /// <summary>
    /// Lógica de listar_peralte y aplicar_peralte (1.20.0) que no toca Civil 3D: lectura y validación de 'curvas',
    /// estado antes/después, previsión de la simulación, verificación tras escribir y mensajes.
    /// Reglas: aplicar_peralte solo escribe en un eje SIN peralte (decisión del plan 6.5: no borra ni recrea nada, y no
    /// se sabe aún si `_.UNDO 1` deshace el peralte); cada curva pedida es un PI con curva circular; las pendientes van en
    /// % y a la API se pasan en tanto por uno (FactorApi, por verificar en Civil 3D, VALIDACION_120 paso 3).
    /// </summary>
    public static class Peralte
    {
        /// <summary>Tolerancia de progresiva (m) para encontrar una progresiva crítica (GetCriticalStationAt) y verificarla.</summary>
        public const double ToleranciaProgresiva = 0.001;
        /// <summary>Tolerancia de pendiente (%) al verificar.</summary>
        public const double ToleranciaPendiente = 0.001;
        /// <summary>
        /// % → valor de la API: SetSlope y GetSlope en tanto por uno (−2 % = −0,02), como ProfileTangent.Grade (validado en
        /// 1.10.0). POR VERIFICAR para el peralte: VALIDACION_120 paso 3 compara con el editor tabular de Civil 3D.
        /// </summary>
        public const double FactorApi = 100.0;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static double PendienteApi(double pct) => pct / FactorApi;
        public static double PendientePct(double valorApi) => valorApi * FactorApi;

        // ------------------------------------------------------------------ lectura de 'curvas'
        /// <summary>
        /// Lee el parámetro 'curvas': [{"pi": 2, "estaciones": [{"progresiva": 105.2, "tipo": "...", "region": "...",
        /// "pendientes": {"segmento": -2.0}}]}]. Lanza ArgumentException con el motivo (sin tocar nada) si la forma no es
        /// válida: lista vacía, PI no entero o repetido, curva sin estaciones, estación sin progresiva, tipo o pendientes,
        /// números no finitos o progresivas repetidas (a menos de 0,001 m, en todo el pedido).
        /// </summary>
        public static List<CurvaPeralte> LeerCurvas(JsonElement curvas)
        {
            const string Forma = " Forma: [{\"pi\": 2, \"estaciones\": [{\"progresiva\": 105.2, \"tipo\": \"<tipo de listar_peralte>\", \"region\": \"<región>\", \"pendientes\": {\"<segmento>\": -2.0}}]}].";
            if (curvas.ValueKind == JsonValueKind.Undefined || curvas.ValueKind == JsonValueKind.Null)
                throw new ArgumentException("Falta el parámetro obligatorio 'curvas' (lista JSON de curvas con sus progresivas críticas)." + Forma);
            if (curvas.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("'curvas' debe ser una lista JSON." + Forma);
            var lista = new List<CurvaPeralte>();
            int posicion = 0;
            foreach (var c in curvas.EnumerateArray())
            {
                posicion++;
                string donde = "la curva " + posicion + " de 'curvas'";
                if (c.ValueKind != JsonValueKind.Object) throw new ArgumentException(Mayuscula(donde) + " no es un objeto." + Forma);
                var curva = new CurvaPeralte { Pi = Entero(c, "pi", donde) };
                if (curva.Pi < 1) throw new ArgumentException(Mayuscula(donde) + ": 'pi' debe ser 1 o más (el número de PI de listar_pis).");
                if (lista.Any(x => x.Pi == curva.Pi)) throw new ArgumentException("El PI " + curva.Pi + " está dos veces en 'curvas': junta sus progresivas en una sola curva.");
                if (!c.TryGetProperty("estaciones", out var est) || est.ValueKind != JsonValueKind.Array || est.GetArrayLength() == 0)
                    throw new ArgumentException("La curva del PI " + curva.Pi + " no tiene 'estaciones' (lista de progresivas críticas)." + Forma);
                int n = 0;
                foreach (var e in est.EnumerateArray())
                {
                    n++;
                    curva.Estaciones.Add(LeerEstacion(e, "la estación " + n + " de la curva del PI " + curva.Pi));
                }
                lista.Add(curva);
            }
            if (lista.Count == 0) throw new ArgumentException("'curvas' está vacía: pide al menos una curva." + Forma);

            var todas = lista.SelectMany(c => c.Estaciones.Select(e => new { c.Pi, e.Progresiva })).OrderBy(x => x.Progresiva).ToList();
            for (int i = 1; i < todas.Count; i++)
                if (todas[i].Progresiva - todas[i - 1].Progresiva <= ToleranciaProgresiva)
                    throw new ArgumentException("La progresiva " + TablaPi.Progresiva(todas[i].Progresiva) + " está repetida (PI " + todas[i - 1].Pi + " y PI " + todas[i].Pi
                        + "): cada progresiva crítica va una sola vez.");
            foreach (var c in lista) c.Estaciones = c.Estaciones.OrderBy(e => e.Progresiva).ToList();
            return lista.OrderBy(c => c.Pi).ToList();
        }

        private static EstacionPeralte LeerEstacion(JsonElement e, string donde)
        {
            if (e.ValueKind != JsonValueKind.Object) throw new ArgumentException(Mayuscula(donde) + " no es un objeto.");
            var est = new EstacionPeralte
            {
                Progresiva = Numero(e, "progresiva", donde),
                Tipo = Texto(e, "tipo"),
                Region = Texto(e, "region")
            };
            if (est.Tipo == null) throw new ArgumentException(Mayuscula(donde) + " no tiene 'tipo' (el tipo de progresiva crítica, con el nombre que da listar_peralte en enumerados.tipo_estacion).");
            if (!e.TryGetProperty("pendientes", out var p) || p.ValueKind != JsonValueKind.Object)
                throw new ArgumentException(Mayuscula(donde) + " no tiene 'pendientes' (objeto segmento → pendiente en %, con los segmentos de enumerados.segmento de listar_peralte).");
            foreach (var kv in p.EnumerateObject())
            {
                string segmento = (kv.Name ?? "").Trim();
                if (segmento.Length == 0) throw new ArgumentException(Mayuscula(donde) + ": hay un segmento sin nombre en 'pendientes'.");
                if (est.Pendientes.Any(x => string.Equals(x.Key, segmento, StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException(Mayuscula(donde) + ": el segmento '" + segmento + "' está dos veces en 'pendientes'.");
                est.Pendientes.Add(new KeyValuePair<string, double>(segmento, ValorNumero(kv.Value, "la pendiente '" + segmento + "' de " + donde)));
            }
            if (est.Pendientes.Count == 0) throw new ArgumentException(Mayuscula(donde) + ": 'pendientes' está vacío; pon al menos un segmento con su pendiente en %.");
            return est;
        }

        // ------------------------------------------------------------------ comprobación contra el eje
        /// <summary>
        /// Comprueba el pedido contra la tabla de PI del eje (la de listar_pis) y su rango de progresivas, y completa cada
        /// curva con radio, PC, PT y giro. Lanza ArgumentException si un PI no existe, no tiene curva circular o una
        /// progresiva cae fuera del eje. Devuelve los avisos (por ejemplo, dos curvas cuyas progresivas se cruzan).
        /// </summary>
        public static List<string> ComprobarContraEje(IList<CurvaPeralte> curvas, ResultadoTablaPi tabla)
        {
            var avisos = new List<string>();
            foreach (var c in curvas)
            {
                var fila = tabla.Pis.FirstOrDefault(p => p.Numero == c.Pi);
                if (fila == null)
                    throw new ArgumentException("El eje '" + tabla.Alineamiento + "' no tiene el PI " + c.Pi + " (tiene " + tabla.Pis.Count + "). Usa listar_pis.");
                if (!fila.TieneCurva)
                {
                    string motivo = fila.Tipo == FilaPi.SinCurva ? "no tiene curva (dos tangentes seguidas)"
                        : "es de tipo " + fila.Tipo + ", no soportado todavía (solo curvas circulares)";
                    throw new ArgumentException("El PI " + c.Pi + " del eje '" + tabla.Alineamiento + "' " + motivo + ": aplicar_peralte solo pone peralte en curvas circulares. Usa listar_pis.");
                }
                c.Radio = fila.Radio; c.ProgresivaPc = fila.ProgresivaPc; c.ProgresivaPt = fila.ProgresivaPt; c.Giro = fila.Giro;
                foreach (var e in c.Estaciones)
                    if (e.Progresiva < tabla.ProgresivaInicial - ToleranciaProgresiva || e.Progresiva > tabla.ProgresivaFinal + ToleranciaProgresiva)
                        throw new ArgumentException("La progresiva " + TablaPi.Progresiva(e.Progresiva) + " (curva del PI " + c.Pi + ") está fuera del eje '" + tabla.Alineamiento
                            + "' (de " + TablaPi.Progresiva(tabla.ProgresivaInicial) + " a " + TablaPi.Progresiva(tabla.ProgresivaFinal) + "). Usa listar_pis.");
            }
            for (int i = 1; i < curvas.Count; i++)
            {
                var a = curvas[i - 1]; var b = curvas[i];
                double finA = a.Estaciones.Max(e => e.Progresiva), inicioB = b.Estaciones.Min(e => e.Progresiva);
                if (finA > inicioB + ToleranciaProgresiva)
                    avisos.Add("Las progresivas de la curva del PI " + a.Pi + " (hasta " + TablaPi.Progresiva(finA) + ") se cruzan con las del PI " + b.Pi
                        + " (desde " + TablaPi.Progresiva(inicioB) + "): Civil 3D puede marcar el solape de las transiciones.");
            }
            return avisos;
        }

        // ------------------------------------------------------------------ estado, previsión y verificación
        /// <summary>
        /// Mensaje de rechazo cuando el eje ya tiene peralte. aplicar_peralte no borra ni reemplaza el que hay (regla 4 y
        /// decisión del plan 6.5).
        /// </summary>
        public static string MensajeYaTienePeralte(PeralteLeido p)
            => "El eje '" + p.Alineamiento + "' ya tiene peralte (" + p.Curvas.Count + " curva" + (p.Curvas.Count == 1 ? "" : "s") + " de peralte, "
               + Math.Max(p.NumeroEstacionesCurvas, p.EstacionesDelEje.Count) + " progresiva" + (Math.Max(p.NumeroEstacionesCurvas, p.EstacionesDelEje.Count) == 1 ? "" : "s")
               + " crítica" + (Math.Max(p.NumeroEstacionesCurvas, p.EstacionesDelEje.Count) == 1 ? "" : "s") + "): aplicar_peralte solo escribe en un eje sin peralte y no borra el que hay. Míralo con listar_peralte.";

        /// <summary>Estado del peralte del eje para 'antes' y 'despues'.</summary>
        public static Dictionary<string, object> Estado(PeralteLeido p) => new Dictionary<string, object>
        {
            ["alineamiento"] = p.Alineamiento,
            ["tiene_peralte"] = p.TienePeralte,
            ["numero_curvas"] = p.Curvas.Count,
            ["numero_estaciones"] = p.NumeroEstacionesCurvas,
            ["curvas"] = p.Curvas.Select(c => c.ComoDiccionario()).ToList()
        };

        /// <summary>Estado previsto (el 'despues' de la simulación): las curvas pedidas, con el nombre que les ponga Civil 3D.</summary>
        public static Dictionary<string, object> Prevision(string alineamiento, IList<CurvaPeralte> curvas) => new Dictionary<string, object>
        {
            ["alineamiento"] = alineamiento,
            ["tiene_peralte"] = true,
            ["numero_curvas"] = curvas.Count,
            ["numero_estaciones"] = curvas.Sum(c => c.Estaciones.Count),
            ["curvas"] = curvas.Select(c => (object)new Dictionary<string, object>
            {
                ["pi"] = c.Pi,
                ["nombre"] = "(lo pone Civil 3D)",
                ["radio"] = c.Radio.HasValue ? Argumentos.Redondear(c.Radio.Value) : null,
                ["giro"] = c.Giro,
                ["progresiva_pc"] = c.ProgresivaPc.HasValue ? Argumentos.Redondear(c.ProgresivaPc.Value) : null,
                ["progresiva_pt"] = c.ProgresivaPt.HasValue ? Argumentos.Redondear(c.ProgresivaPt.Value) : null,
                ["numero_estaciones"] = c.Estaciones.Count,
                ["estaciones"] = c.Estaciones.Select(e => e.ComoDiccionario()).ToList()
            }).ToList()
        };

        /// <summary>Campos que 'despues' debe cumplir exactamente: hay peralte y una curva de peralte por curva pedida.</summary>
        public static Dictionary<string, object> Esperado(IList<CurvaPeralte> curvas) => new Dictionary<string, object>
        {
            ["tiene_peralte"] = true,
            ["numero_curvas"] = curvas.Count
        };

        /// <summary>
        /// Compara lo pedido con lo leído tras escribir. Devuelve las diferencias (vacía si todo cuadra): progresiva pedida
        /// que no está (a 0,001 m), tipo distinto o pendiente distinta (más de 0,001 %) o sin leer. 'extra' recibe las
        /// progresivas que el eje tiene y no se pidieron (Civil 3D pudo crearlas al añadir la curva): son aviso, no error.
        /// </summary>
        public static List<string> Diferencias(IList<CurvaPeralte> pedido, PeralteLeido leido, out List<string> extra)
        {
            var dif = new List<string>();
            var leidas = leido.Curvas.SelectMany(c => c.Estaciones).ToList();
            var usadas = new HashSet<EstacionLeida>();
            foreach (var c in pedido)
                foreach (var e in c.Estaciones)
                {
                    string donde = "PI " + c.Pi + ", " + TablaPi.Progresiva(e.Progresiva);
                    var l = leidas.Where(x => !usadas.Contains(x) && Math.Abs(x.Progresiva - e.Progresiva) <= ToleranciaProgresiva)
                                  .OrderBy(x => Math.Abs(x.Progresiva - e.Progresiva)).FirstOrDefault();
                    if (l == null) { dif.Add(donde + ": no está en el eje después de escribir"); continue; }
                    usadas.Add(l);
                    if (!string.Equals((l.Tipo ?? "").Trim(), e.Tipo.Trim(), StringComparison.OrdinalIgnoreCase))
                        dif.Add(donde + ": tipo '" + l.Tipo + "' en vez de '" + e.Tipo + "'");
                    foreach (var p in e.Pendientes)
                    {
                        if (!l.Pendientes.TryGetValue(p.Key, out double real)) dif.Add(donde + ": no se pudo leer la pendiente de '" + p.Key + "'");
                        else if (Math.Abs(real - p.Value) > ToleranciaPendiente)
                            dif.Add(donde + ": pendiente de '" + p.Key + "' " + Num(real) + " % en vez de " + Num(p.Value) + " %");
                    }
                }
            extra = leidas.Where(x => !usadas.Contains(x)).OrderBy(x => x.Progresiva)
                          .Select(x => TablaPi.Progresiva(x.Progresiva) + " (" + (x.Tipo ?? "?") + ", curva '" + (x.Curva ?? "?") + "')").ToList();
            return dif;
        }

        /// <summary>Mensaje de error cuando el dibujo no refleja lo pedido (no se reintenta).</summary>
        public static string MensajeNoRefleja(string alineamiento, IList<string> diferencias, string rutaCopia)
            => "El dibujo no refleja el peralte pedido en el eje '" + alineamiento + "': " + string.Join("; ", diferencias)
               + ". No se reintenta. Míralo con listar_peralte" + (rutaCopia != null ? "; la copia previa está en " + rutaCopia : "") + ".";

        /// <summary>Texto de la acción para la respuesta.</summary>
        public static string Accion(string alineamiento, IList<CurvaPeralte> curvas)
            => "Aplicar peralte en el eje '" + alineamiento + "': " + curvas.Count + " curva" + (curvas.Count == 1 ? "" : "s") + " de peralte (PI "
               + string.Join(", ", curvas.Select(c => c.Pi)) + ") con " + curvas.Sum(c => c.Estaciones.Count) + " progresivas críticas";

        /// <summary>Bloque 'deshacer' de la respuesta real: qué se añadió y cómo volver atrás.</summary>
        public static Dictionary<string, object> BloqueDeshacer(PeralteLeido despues, string rutaCopia) => new Dictionary<string, object>
        {
            ["anadido"] = despues.Curvas.Select(c => (object)new Dictionary<string, object> { ["curva"] = c.Nombre, ["estaciones"] = c.Estaciones.Count }).ToList(),
            ["como"] = "Prueba `_.UNDO 1` sin hacer zoom antes (la entrada de arriba del menú Deshacer debe ser Executefunction) y comprueba con listar_peralte que el eje "
                       + "ya no tiene peralte; está POR VERIFICAR en Civil 3D (VALIDACION_120). Si no lo quita, vuelve a la copia previa"
                       + (rutaCopia != null ? " (" + rutaCopia + ")" : "") + ": aplicar_peralte no borra peralte."
        };

        // ------------------------------------------------------------------ ayudantes
        private static int Entero(JsonElement o, string campo, string donde)
        {
            if (!o.TryGetProperty(campo, out var v) || v.ValueKind == JsonValueKind.Null)
                throw new ArgumentException(Mayuscula(donde) + " no tiene '" + campo + "'.");
            double d = ValorNumero(v, "'" + campo + "' de " + donde);
            if (Math.Abs(d - Math.Round(d)) > 1e-9) throw new ArgumentException("'" + campo + "' de " + donde + " debe ser un número entero.");
            return (int)Math.Round(d);
        }

        private static double Numero(JsonElement o, string campo, string donde)
        {
            if (!o.TryGetProperty(campo, out var v) || v.ValueKind == JsonValueKind.Null)
                throw new ArgumentException(Mayuscula(donde) + " no tiene '" + campo + "'.");
            return ValorNumero(v, "'" + campo + "' de " + donde);
        }

        private static double ValorNumero(JsonElement v, string que)
        {
            double d;
            if (v.ValueKind == JsonValueKind.Number) d = v.GetDouble();
            else if (v.ValueKind == JsonValueKind.String && double.TryParse((v.GetString() ?? "").Trim().Replace(',', '.'), NumberStyles.Float, Inv, out double t)) d = t;
            else throw new ArgumentException(Mayuscula(que) + " debe ser un número.");
            if (double.IsNaN(d) || double.IsInfinity(d)) throw new ArgumentException(Mayuscula(que) + " debe ser un número finito.");
            return d;
        }

        private static string Texto(JsonElement o, string campo)
        {
            if (!o.TryGetProperty(campo, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            string s = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static string Mayuscula(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        private static string Num(double v) => v.ToString("0.####", Inv);
    }
}
