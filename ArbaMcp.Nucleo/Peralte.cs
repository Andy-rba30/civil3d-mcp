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
        /// <summary>
        /// Nombre de la curva de peralte vacía que Civil 3D ya creó para este arco (de PC a PT), si la hay: aplicar_peralte
        /// le añade las progresivas en vez de crear otra (1.21.2). Null: se crea con AddUserDefinedCurve. Lo rellena
        /// Peralte.AsignarCurvasExistentes.
        /// </summary>
        public string CurvaExistente;
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

    /// <summary>
    /// Un pk entre progresivas críticas donde aplicar_peralte comprueba, tras escribir, que el eje devuelve el peralte
    /// escrito con Alignment.GetCrossSlopeAtStation (tarea 7.4): el centro del tramo de peralte total de una curva.
    /// </summary>
    public sealed class PuntoComprobacion
    {
        public int Pi;
        /// <summary>Null si no se pudo elegir un pk (entonces 'Motivo' dice por qué).</summary>
        public double? Progresiva;
        /// <summary>Cómo se eligió el pk (entre BeginFullSuper y EndFullSuper, o el tramo de pendientes iguales).</summary>
        public string Criterio;
        /// <summary>Segmento → pendiente en % que debe devolver el eje en el pk (la media de las dos progresivas del tramo).</summary>
        public List<KeyValuePair<string, double>> Escrito = new List<KeyValuePair<string, double>>();
        public string Motivo;
    }

    /// <summary>Lo que el plugin lee del peralte de un eje.</summary>
    public sealed class PeralteLeido
    {
        public string Alineamiento;
        public List<CurvaLeida> Curvas = new List<CurvaLeida>();
        /// <summary>Progresivas críticas de Alignment.SuperelevationCriticalStations (la colección del eje, aparte de las de cada curva).</summary>
        public List<EstacionLeida> EstacionesDelEje = new List<EstacionLeida>();
        public int NumeroEstacionesCurvas => Curvas.Sum(c => c.Estaciones.Count);
        /// <summary>Curvas de peralte con alguna progresiva crítica (las vacías que Civil 3D crea con el eje no cuentan).</summary>
        public int NumeroCurvasConEstaciones => Curvas.Count(c => c.Estaciones.Count > 0);
        /// <summary>Progresivas críticas de la colección del eje que no son de contorno (BeginAlignment y EndAlignment).</summary>
        public int NumeroEstacionesDelEjeSinContorno => EstacionesDelEje.Count(e => !Peralte.EsDeContorno(e));
        /// <summary>
        /// El eje tiene peralte si alguna curva de peralte tiene progresivas críticas o si la colección del eje tiene alguna
        /// que no sea de contorno. Desde 1.21.2: Civil 3D 2027 crea con el eje una curva de peralte vacía por cada curva
        /// horizontal y las progresivas BeginAlignment y EndAlignment (17/11/2026, VALIDACION_120 paso 2); eso es un eje
        /// sin peralte. Hasta la 1.21.1 contaba cualquier curva o progresiva, y aplicar_peralte rechazaba todos los ejes.
        /// </summary>
        public bool TienePeralte => NumeroEstacionesCurvas > 0 || NumeroEstacionesDelEjeSinContorno > 0;
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
        /// <summary>
        /// Tipos de progresiva crítica que Civil 3D 2027 pone solo en todo eje, en su inicio y su fin, con las pendientes en
        /// 0 (SuperelevationCriticalStationType; leídos con listar_peralte el 17/11/2026, VALIDACION_120 paso 2). No son
        /// peralte.
        /// </summary>
        public static readonly string[] TiposDeContorno = { "BeginAlignment", "EndAlignment" };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool EsDeContorno(EstacionLeida e)
            => e != null && TiposDeContorno.Any(t => string.Equals(t, (e.Tipo ?? "").Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// La curva de peralte leída es la vacía que Civil 3D creó para el arco de PC a PT: empieza en el PC, acaba en el PT
        /// (a 0,001 m) y no tiene progresivas críticas.
        /// </summary>
        public static bool EsCurvaVaciaDelArco(double? inicio, double? fin, int numeroEstaciones, double pc, double pt)
            => numeroEstaciones == 0 && inicio.HasValue && fin.HasValue
               && Math.Abs(inicio.Value - pc) <= ToleranciaProgresiva && Math.Abs(fin.Value - pt) <= ToleranciaProgresiva;

        /// <summary>
        /// Para cada curva pedida (con PC y PT ya completados por ComprobarContraEje), busca la curva de peralte vacía de su
        /// arco entre las que tiene el eje y guarda su nombre en CurvaExistente (o null si no la hay). Cada curva del eje se
        /// usa una sola vez.
        /// </summary>
        public static void AsignarCurvasExistentes(IList<CurvaPeralte> curvas, PeralteLeido actual)
        {
            var usadas = new HashSet<CurvaLeida>();
            foreach (var c in curvas)
            {
                c.CurvaExistente = null;
                if (!c.ProgresivaPc.HasValue || !c.ProgresivaPt.HasValue) continue;
                var l = actual.Curvas.FirstOrDefault(x => !usadas.Contains(x) && !string.IsNullOrEmpty(x.Nombre)
                    && EsCurvaVaciaDelArco(x.Inicio, x.Fin, x.Estaciones.Count, c.ProgresivaPc.Value, c.ProgresivaPt.Value));
                if (l == null) continue;
                usadas.Add(l);
                c.CurvaExistente = l.Nombre;
            }
        }

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
        {
            // Cuentan las curvas con progresivas y las progresivas de peralte: las curvas vacías y las de contorno
            // (BeginAlignment, EndAlignment) las crea Civil 3D con el eje (1.21.2).
            int curvas = p.NumeroCurvasConEstaciones;
            int estaciones = Math.Max(p.NumeroEstacionesCurvas, p.NumeroEstacionesDelEjeSinContorno);
            return "El eje '" + p.Alineamiento + "' ya tiene peralte (" + curvas + " curva" + (curvas == 1 ? "" : "s") + " de peralte, "
               + estaciones + " progresiva" + (estaciones == 1 ? "" : "s") + " crítica" + (estaciones == 1 ? "" : "s")
               + "): aplicar_peralte solo escribe en un eje sin peralte y no borra el que hay. Míralo con listar_peralte.";
        }

        /// <summary>Estado del peralte del eje para 'antes' y 'despues'.</summary>
        public static Dictionary<string, object> Estado(PeralteLeido p) => new Dictionary<string, object>
        {
            ["alineamiento"] = p.Alineamiento,
            ["tiene_peralte"] = p.TienePeralte,
            ["numero_curvas"] = p.Curvas.Count,
            ["numero_estaciones"] = p.NumeroEstacionesCurvas,
            ["curvas"] = p.Curvas.Select(c => c.ComoDiccionario()).ToList()
        };

        /// <summary>
        /// Estado previsto (el 'despues' de la simulación): las curvas pedidas, con el nombre de la curva vacía que se usa o
        /// el que les ponga Civil 3D. 'curvasAntes' es el número de curvas de peralte que ya tiene el eje (las vacías que
        /// crea Civil 3D): 'numero_curvas' las suma a las nuevas.
        /// </summary>
        public static Dictionary<string, object> Prevision(string alineamiento, IList<CurvaPeralte> curvas, int curvasAntes = 0) => new Dictionary<string, object>
        {
            ["alineamiento"] = alineamiento,
            ["tiene_peralte"] = true,
            ["numero_curvas"] = NumeroCurvasPrevisto(curvas, curvasAntes),
            ["numero_estaciones"] = curvas.Sum(c => c.Estaciones.Count),
            ["curvas"] = curvas.Select(c => (object)new Dictionary<string, object>
            {
                ["pi"] = c.Pi,
                ["nombre"] = c.CurvaExistente ?? "(lo pone Civil 3D)",
                ["curva_de_peralte"] = c.CurvaExistente != null ? "existente" : "nueva",
                ["radio"] = c.Radio.HasValue ? Argumentos.Redondear(c.Radio.Value) : null,
                ["giro"] = c.Giro,
                ["progresiva_pc"] = c.ProgresivaPc.HasValue ? Argumentos.Redondear(c.ProgresivaPc.Value) : null,
                ["progresiva_pt"] = c.ProgresivaPt.HasValue ? Argumentos.Redondear(c.ProgresivaPt.Value) : null,
                ["numero_estaciones"] = c.Estaciones.Count,
                ["estaciones"] = c.Estaciones.Select(e => e.ComoDiccionario()).ToList()
            }).ToList()
        };

        /// <summary>Curvas de peralte que tendrá el eje: las que ya tenía más una nueva por cada curva pedida sin curva vacía.</summary>
        public static int NumeroCurvasPrevisto(IList<CurvaPeralte> curvas, int curvasAntes)
            => curvasAntes + curvas.Count(c => c.CurvaExistente == null);

        /// <summary>
        /// Campos que 'despues' debe cumplir exactamente: hay peralte y las curvas de peralte son las de antes más una por
        /// cada curva pedida que no reutiliza una curva vacía.
        /// </summary>
        public static Dictionary<string, object> Esperado(IList<CurvaPeralte> curvas, int curvasAntes = 0) => new Dictionary<string, object>
        {
            ["tiene_peralte"] = true,
            ["numero_curvas"] = NumeroCurvasPrevisto(curvas, curvasAntes)
        };

        /// <summary>
        /// Compara lo pedido con lo leído tras escribir. Devuelve las diferencias (vacía si todo cuadra): progresiva pedida
        /// que no está (a 0,001 m), tipo distinto o pendiente distinta (más de 0,001 %) o sin leer. 'extra' recibe las
        /// progresivas que el eje tiene y no se pidieron (Civil 3D pudo crearlas al añadir la curva): son aviso, no error.
        /// Se buscan en las curvas de peralte y, además, en la colección del propio eje (EstacionesDelEje) las que no
        /// están en ninguna curva: no se sabe aún si Civil 3D guarda las mismas en las dos (VALIDACION_120, paso 5a).
        /// </summary>
        public static List<string> Diferencias(IList<CurvaPeralte> pedido, PeralteLeido leido, out List<string> extra)
        {
            var dif = new List<string>();
            var leidas = leido.Curvas.SelectMany(c => c.Estaciones).ToList();
            leidas.AddRange(leido.EstacionesDelEje.Where(x => !leidas.Any(y => Math.Abs(y.Progresiva - x.Progresiva) <= ToleranciaProgresiva)).ToList());
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
            // Las de contorno (BeginAlignment, EndAlignment) ya estaban: Civil 3D las pone con el eje (1.21.2)
            extra = leidas.Where(x => !usadas.Contains(x) && !EsDeContorno(x)).OrderBy(x => x.Progresiva)
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
            ["anadido"] = despues.Curvas.Where(c => c.Estaciones.Count > 0).Select(c => (object)new Dictionary<string, object> { ["curva"] = c.Nombre, ["estaciones"] = c.Estaciones.Count }).ToList(),
            ["como"] = "Prueba `_.UNDO 1` sin hacer zoom antes (la entrada de arriba del menú Deshacer debe ser Executefunction) y comprueba con listar_peralte que el eje "
                       + "ya no tiene peralte; está POR VERIFICAR en Civil 3D (VALIDACION_120). Si no lo quita, vuelve a la copia previa"
                       + (rutaCopia != null ? " (" + rutaCopia + ")" : "") + ": aplicar_peralte no borra peralte."
        };

        // ------------------------------------------------------------------ peralte entre progresivas críticas (tarea 7.4)
        /// <summary>
        /// Tolerancia (puntos de %) al comparar la pendiente que devuelve el eje entre progresivas críticas
        /// (Alignment.GetCrossSlopeAtStation) con la escrita. Más holgada que ToleranciaPendiente porque el valor sale
        /// interpolado por Civil 3D.
        /// </summary>
        public const double ToleranciaEntreProgresivas = 0.05;
        /// <summary>Máximo de progresivas que acepta listar_peralte en 'progresivas' (8 lecturas por progresiva).</summary>
        public const int MaximoProgresivas = 100;
        /// <summary>
        /// Tercer parámetro de Alignment.GetCrossSlopeAtStation (applySmoothing): true aplica el suavizado de las
        /// transiciones de peralte si la curva lo tiene, que es lo que usa el corredor. aplicar_peralte no define
        /// suavizado, así que en sus curvas true y false deberían dar lo mismo (por verificar en Civil 3D).
        /// </summary>
        public const bool AplicarSuavizado = true;
        /// <summary>Tipos de progresiva crítica que limitan el tramo de peralte total (SuperelevationCriticalStationType, validados el 17/11/2026).</summary>
        public const string TipoInicioPeralteTotal = "BeginFullSuper";
        public const string TipoFinPeralteTotal = "EndFullSuper";

        /// <summary>
        /// Lee el parámetro opcional 'progresivas' de listar_peralte: lista JSON de números (acepta texto con coma
        /// decimal). Null si no se pasó. Lanza ArgumentException (sin leer el dibujo) si no es una lista, está vacía,
        /// tiene más de MaximoProgresivas o algún valor no es un número finito. Conserva el orden pedido.
        /// </summary>
        public static List<double> LeerProgresivas(JsonElement v)
        {
            const string Forma = " Forma: [100, 280, 550] (progresivas del eje en metros).";
            if (v.ValueKind == JsonValueKind.Undefined || v.ValueKind == JsonValueKind.Null) return null;
            if (v.ValueKind != JsonValueKind.Array) throw new ArgumentException("'progresivas' debe ser una lista JSON de números." + Forma);
            var lista = new List<double>();
            int n = 0;
            foreach (var e in v.EnumerateArray())
            {
                n++;
                lista.Add(ValorNumero(e, "la progresiva " + n + " de 'progresivas'"));
            }
            if (lista.Count == 0) throw new ArgumentException("'progresivas' está vacía: pon al menos una progresiva o quita el parámetro." + Forma);
            if (lista.Count > MaximoProgresivas)
                throw new ArgumentException("'progresivas' tiene " + lista.Count + " valores; el máximo es " + MaximoProgresivas + ". Pide menos progresivas.");
            return lista;
        }

        /// <summary>
        /// Pendiente transversal de cada segmento en cada progresiva pedida, en %, con 'leer' (pk, segmento) → valor de la
        /// API en tanto por uno (Alignment.GetCrossSlopeAtStation en el plugin). Cada lectura va en su try: si falla o no
        /// es un número finito, en el lugar del valor va el texto del error (no tumba la respuesta). Una progresiva fuera
        /// del eje no se lee: lleva 'error' y 'pendientes' vacío.
        /// </summary>
        public static List<Dictionary<string, object>> PendientesEnProgresivas(IList<double> progresivas, double inicioEje, double finEje,
            IEnumerable<string> segmentos, Func<double, string, double> leer)
        {
            var segs = segmentos.ToList();
            var filas = new List<Dictionary<string, object>>();
            foreach (double pk in progresivas)
            {
                var pendientes = new Dictionary<string, object>();
                var fila = new Dictionary<string, object>
                {
                    ["progresiva"] = Argumentos.Redondear(pk),
                    ["progresiva_texto"] = TablaPi.Progresiva(pk),
                    ["pendientes"] = pendientes
                };
                if (pk < inicioEje - ToleranciaProgresiva || pk > finEje + ToleranciaProgresiva)
                {
                    fila["error"] = "Fuera del eje (de " + TablaPi.Progresiva(inicioEje) + " a " + TablaPi.Progresiva(finEje) + "): no se lee.";
                    filas.Add(fila);
                    continue;
                }
                foreach (var s in segs) pendientes[s] = LeerUna(pk, s, leer, out _, out _);
                filas.Add(fila);
            }
            return filas;
        }

        /// <summary>Una lectura en % (redondeada) o el texto del error en su lugar.</summary>
        private static object LeerUna(double pk, string segmento, Func<double, string, double> leer, out double? pct, out string error)
        {
            pct = null; error = null;
            try
            {
                double v = leer(pk, segmento);
                if (double.IsNaN(v) || double.IsInfinity(v)) error = "error: Civil 3D devolvió un valor no finito (" + v.ToString(Inv) + ")";
                else pct = PendientePct(v);
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                error = "error: " + ex.GetType().Name + ": " + ex.Message;
            }
            return pct.HasValue ? (object)Argumentos.Redondear(pct.Value) : error;
        }

        /// <summary>
        /// Para cada curva pedida, el pk donde comprobar el peralte entre progresivas críticas: el centro del tramo de
        /// peralte total, entre la progresiva BeginFullSuper y la EndFullSuper escritas; si la curva no tiene las dos, el
        /// centro del tramo entre dos progresivas seguidas con las mismas pendientes escritas (el de mayor pendiente y, a
        /// igualdad, el más largo). Lo escrito en el pk es la media de las dos progresivas, solo en los segmentos que
        /// llevan las dos. Si no hay tramo (o mide menos de 0,002 m), 'Motivo' dice por qué y no se comprueba.
        /// </summary>
        public static List<PuntoComprobacion> PuntosEntreProgresivas(IList<CurvaPeralte> curvas)
        {
            var puntos = new List<PuntoComprobacion>();
            foreach (var c in curvas)
            {
                var est = c.Estaciones.OrderBy(e => e.Progresiva).ToList();
                var punto = new PuntoComprobacion { Pi = c.Pi };
                EstacionPeralte a = est.FirstOrDefault(e => EsTipo(e, TipoInicioPeralteTotal)), b = null;
                if (a != null) b = est.FirstOrDefault(e => EsTipo(e, TipoFinPeralteTotal) && e.Progresiva > a.Progresiva);
                if (a != null && b != null) punto.Criterio = "centro del peralte total, entre " + TipoInicioPeralteTotal + " y " + TipoFinPeralteTotal;
                else
                {
                    a = b = null;
                    double mejorPendiente = -1, mejorLongitud = -1;
                    for (int i = 1; i < est.Count; i++)
                    {
                        var x = est[i - 1]; var y = est[i];
                        if (!MismasPendientes(x, y)) continue;
                        double pend = x.Pendientes.Max(p => Math.Abs(p.Value)), lon = y.Progresiva - x.Progresiva;
                        if (pend > mejorPendiente + ToleranciaPendiente || (Math.Abs(pend - mejorPendiente) <= ToleranciaPendiente && lon > mejorLongitud))
                        { a = x; b = y; mejorPendiente = pend; mejorLongitud = lon; }
                    }
                    if (a != null) punto.Criterio = "centro del tramo entre " + a.Tipo + " y " + b.Tipo + " con las mismas pendientes (sin " + TipoInicioPeralteTotal + " y " + TipoFinPeralteTotal + ")";
                }
                if (a == null)
                {
                    punto.Motivo = "La curva del PI " + c.Pi + " no tiene " + TipoInicioPeralteTotal + " y " + TipoFinPeralteTotal
                        + " ni dos progresivas seguidas con las mismas pendientes: no se comprueba el peralte entre progresivas.";
                    puntos.Add(punto);
                    continue;
                }
                if (b.Progresiva - a.Progresiva < 2 * ToleranciaProgresiva)
                {
                    punto.Motivo = "El tramo de peralte total de la curva del PI " + c.Pi + " mide 0 (" + TablaPi.Progresiva(a.Progresiva)
                        + "): no hay pk entre progresivas que comprobar.";
                    puntos.Add(punto);
                    continue;
                }
                punto.Progresiva = (a.Progresiva + b.Progresiva) / 2;
                foreach (var p in a.Pendientes)
                {
                    var q = b.Pendientes.FirstOrDefault(x => string.Equals(x.Key, p.Key, StringComparison.OrdinalIgnoreCase));
                    if (q.Key != null) punto.Escrito.Add(new KeyValuePair<string, double>(p.Key, (p.Value + q.Value) / 2));
                }
                if (punto.Escrito.Count == 0)
                {
                    punto.Progresiva = null;
                    punto.Motivo = "Las progresivas " + TablaPi.Progresiva(a.Progresiva) + " y " + TablaPi.Progresiva(b.Progresiva) + " de la curva del PI " + c.Pi
                        + " no tienen ningún segmento en común: no se comprueba el peralte entre progresivas.";
                }
                puntos.Add(punto);
            }
            return puntos;
        }

        private static bool EsTipo(EstacionPeralte e, string tipo) => string.Equals((e.Tipo ?? "").Trim(), tipo, StringComparison.OrdinalIgnoreCase);

        private static bool MismasPendientes(EstacionPeralte x, EstacionPeralte y)
        {
            if (x.Pendientes.Count == 0 || x.Pendientes.Count != y.Pendientes.Count) return false;
            foreach (var p in x.Pendientes)
            {
                var q = y.Pendientes.FirstOrDefault(z => string.Equals(z.Key, p.Key, StringComparison.OrdinalIgnoreCase));
                if (q.Key == null || Math.Abs(q.Value - p.Value) > ToleranciaPendiente) return false;
            }
            return true;
        }

        /// <summary>
        /// Lee con 'leer' (pk, segmento) → valor de la API (Alignment.GetCrossSlopeAtStation) cada segmento escrito en
        /// cada punto y lo compara con lo escrito (ToleranciaEntreProgresivas). Devuelve una fila por segmento (pi,
        /// progresiva, segmento, escrito, leido, diferencia, coincide y, si la lectura falla, error) o una por curva sin
        /// pk (con 'motivo' y coincide null). 'avisos' recibe un aviso por pk que no coincide o no se pudo leer, y uno por
        /// curva sin pk: es aviso, no error, porque esta lectura aún no está validada en Civil 3D.
        /// </summary>
        public static List<Dictionary<string, object>> ComprobarEntreProgresivas(IList<PuntoComprobacion> puntos, Func<double, string, double> leer, out List<string> avisos)
        {
            avisos = new List<string>();
            var filas = new List<Dictionary<string, object>>();
            foreach (var p in puntos)
            {
                if (!p.Progresiva.HasValue)
                {
                    filas.Add(new Dictionary<string, object> { ["pi"] = p.Pi, ["progresiva"] = null, ["motivo"] = p.Motivo, ["coincide"] = null });
                    if (p.Motivo != null) avisos.Add(p.Motivo);
                    continue;
                }
                double pk = p.Progresiva.Value;
                var distintos = new List<string>();
                var fallidos = new List<string>();
                foreach (var s in p.Escrito)
                {
                    LeerUna(pk, s.Key, leer, out double? leido, out string error);
                    bool? coincide = leido.HasValue ? Math.Abs(leido.Value - s.Value) <= ToleranciaEntreProgresivas : (bool?)null;
                    var fila = new Dictionary<string, object>
                    {
                        ["pi"] = p.Pi,
                        ["progresiva"] = Argumentos.Redondear(pk),
                        ["progresiva_texto"] = TablaPi.Progresiva(pk),
                        ["criterio"] = p.Criterio,
                        ["segmento"] = s.Key,
                        ["escrito"] = Argumentos.Redondear(s.Value),
                        ["leido"] = leido.HasValue ? Argumentos.Redondear(leido.Value) : null,
                        ["diferencia"] = leido.HasValue ? Argumentos.Redondear(leido.Value - s.Value) : null,
                        ["coincide"] = coincide
                    };
                    if (error != null) { fila["error"] = error; fallidos.Add("segmento " + s.Key + ": " + error); }
                    else if (coincide == false) distintos.Add("segmento " + s.Key + ": leído " + Num(leido.Value) + " %, escrito " + Num(s.Value) + " %");
                    filas.Add(fila);
                }
                if (distintos.Count > 0)
                    avisos.Add("El eje no devuelve el peralte escrito en el pk " + TablaPi.Progresiva(pk) + " (PI " + p.Pi + "; " + string.Join("; ", distintos)
                        + "): el corredor podría no seguirlo. Mira listar_peralte con progresivas.");
                if (fallidos.Count > 0)
                    avisos.Add("No se pudo leer el peralte en el pk " + TablaPi.Progresiva(pk) + " (PI " + p.Pi + ") con Alignment.GetCrossSlopeAtStation ("
                        + string.Join("; ", fallidos) + "): no se sabe si el corredor lo sigue. Mira listar_peralte con progresivas.");
            }
            return filas;
        }

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
