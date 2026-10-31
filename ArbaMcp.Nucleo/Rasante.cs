using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Un PVI (punto de intersección vertical) de la rasante pedida, con la curva vertical que le corresponde si cabe.
    /// Las pendientes van en % con signo (positivo = sube); A es el cambio algebraico de pendiente (salida − entrada).
    /// </summary>
    public sealed class PviRasante
    {
        /// <summary>1 para el primer PVI de la lista.</summary>
        public int Numero;
        public double Progresiva;
        public double Cota;
        /// <summary>Longitud de curva pedida para este PVI (0 = sin curva, quiebre de pendiente).</summary>
        public double LongitudPedida;
        /// <summary>True si la longitud vino en el propio PVI (false si es la de 'longitud_curva' o ninguna).</summary>
        public bool LongitudPropia;
        /// <summary>Longitud de la curva que se crea; null si el PVI queda como quiebre.</summary>
        public double? LongitudAplicada;
        /// <summary>Pendiente del tramo que llega (%); null en el primer PVI.</summary>
        public double? PendienteEntrada;
        /// <summary>Pendiente del tramo que sale (%); null en el último PVI.</summary>
        public double? PendienteSalida;
        /// <summary>A = salida − entrada (%); null en los extremos.</summary>
        public double? CambioPendiente;
        /// <summary>K = L / |A| (metros por cada 1 % de cambio de pendiente); null sin curva.</summary>
        public double? K;
        /// <summary>"cresta" (convexa, A &lt; 0) o "valle" (cóncava, A &gt; 0); null si A = 0 o en los extremos.</summary>
        public string Tipo;
        public double? ProgresivaInicio, ProgresivaFin, CotaInicio, CotaFin;
        /// <summary>Longitud máxima de curva que cabría en este PVI con lo que dejan libres las curvas vecinas; null si no cabe ninguna.</summary>
        public double? LongitudMaxima;
        /// <summary>Explicación cuando el PVI queda sin la curva pedida; null si la curva se aplica o no se pidió.</summary>
        public string Aviso;
        public bool EsExtremo;

        /// <summary>Entrada de la lista 'curvas' de la respuesta (una por PVI intermedio, con o sin curva).</summary>
        public Dictionary<string, object> ComoCurva() => new Dictionary<string, object>
        {
            ["pvi"] = Numero,
            ["progresiva_pvi"] = Argumentos.Redondear(Progresiva),
            ["cota_pvi"] = Argumentos.Redondear(Cota),
            ["pendiente_entrada_pct"] = PendienteEntrada.HasValue ? Argumentos.Redondear(PendienteEntrada.Value) : null,
            ["pendiente_salida_pct"] = PendienteSalida.HasValue ? Argumentos.Redondear(PendienteSalida.Value) : null,
            ["a_pct"] = CambioPendiente.HasValue ? Argumentos.Redondear(CambioPendiente.Value) : null,
            ["longitud_pedida"] = Argumentos.Redondear(LongitudPedida),
            ["longitud"] = LongitudAplicada.HasValue ? Argumentos.Redondear(LongitudAplicada.Value) : null,
            ["k"] = K.HasValue ? Argumentos.Redondear(K.Value) : null,
            ["tipo"] = Tipo,
            ["progresiva_inicio"] = ProgresivaInicio.HasValue ? Argumentos.Redondear(ProgresivaInicio.Value) : null,
            ["progresiva_fin"] = ProgresivaFin.HasValue ? Argumentos.Redondear(ProgresivaFin.Value) : null,
            ["cota_inicio"] = CotaInicio.HasValue ? Argumentos.Redondear(CotaInicio.Value) : null,
            ["cota_fin"] = CotaFin.HasValue ? Argumentos.Redondear(CotaFin.Value) : null,
            ["longitud_maxima"] = LongitudMaxima.HasValue ? Argumentos.Redondear(LongitudMaxima.Value) : null,
            ["aviso"] = Aviso
        };

        /// <summary>Entrada de la lista 'pvis' prevista (progresiva, cota y si lleva curva).</summary>
        public Dictionary<string, object> ComoPvi() => new Dictionary<string, object>
        {
            ["numero"] = Numero,
            ["progresiva"] = Argumentos.Redondear(Progresiva),
            ["cota"] = Argumentos.Redondear(Cota),
            ["longitud_curva"] = LongitudAplicada.HasValue ? Argumentos.Redondear(LongitudAplicada.Value) : null
        };
    }

    /// <summary>Un tramo recto de la rasante entre dos PVI consecutivos (la tangente, antes de recortarla con las curvas).</summary>
    public sealed class TramoRasante
    {
        /// <summary>1 para el tramo entre el PVI 1 y el PVI 2.</summary>
        public int Numero;
        public double ProgresivaInicio, ProgresivaFin, CotaInicio, CotaFin;
        public double Longitud => ProgresivaFin - ProgresivaInicio;
        public double Desnivel => CotaFin - CotaInicio;
        /// <summary>Pendiente en % con signo (positivo = sube en el sentido de las progresivas).</summary>
        public double PendientePct => Desnivel / Longitud * 100.0;
        public string Aviso;

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["numero"] = Numero,
            ["progresiva_inicio"] = Argumentos.Redondear(ProgresivaInicio),
            ["progresiva_fin"] = Argumentos.Redondear(ProgresivaFin),
            ["longitud"] = Argumentos.Redondear(Longitud),
            ["cota_inicio"] = Argumentos.Redondear(CotaInicio),
            ["cota_fin"] = Argumentos.Redondear(CotaFin),
            ["desnivel"] = Argumentos.Redondear(Desnivel),
            ["pendiente_pct"] = Argumentos.Redondear(PendientePct),
            ["aviso"] = Aviso
        };
    }

    /// <summary>La rasante prevista: PVI con sus curvas, tramos con sus pendientes, rango y cotas extremas.</summary>
    public sealed class PlanRasante
    {
        public List<PviRasante> Pvis = new List<PviRasante>();
        public List<TramoRasante> Tramos = new List<TramoRasante>();
        public double ProgresivaInicial => Pvis[0].Progresiva;
        public double ProgresivaFinal => Pvis[Pvis.Count - 1].Progresiva;
        public double Longitud => ProgresivaFinal - ProgresivaInicial;
        /// <summary>Cotas extremas de la rasante dibujada (con las curvas: el vértice de una parábola, no el PVI, cuando hay curva).</summary>
        public double CotaMinima, CotaMaxima;
        public int NumeroPvis => Pvis.Count;
        public int NumeroCurvas => Pvis.Count(p => p.LongitudAplicada.HasValue);
        /// <summary>Avisos: curvas que no caben, PVI sin cambio de pendiente, pendientes fuertes, progresivas ajustadas al extremo del eje.</summary>
        public List<string> Avisos = new List<string>();

        public List<PviRasante> Intermedios => Pvis.Where(p => !p.EsExtremo).ToList();
        public List<Dictionary<string, object>> CurvasComoDiccionarios() => Intermedios.Select(p => p.ComoCurva()).ToList();
        public List<Dictionary<string, object>> TramosComoDiccionarios() => Tramos.Select(t => t.ComoDiccionario()).ToList();
        public List<Dictionary<string, object>> PvisComoDiccionarios() => Pvis.Select(p => p.ComoPvi()).ToList();
    }

    /// <summary>
    /// Geometría de crear_rasante (1.10.0) sin Civil 3D: lectura de la lista de PVI, pendientes de cada tramo, cambio de
    /// pendiente A, K = L/|A|, cresta o valle, progresivas y cotas de inicio y fin de cada curva, si cabe cada curva
    /// (la mitad de una curva no puede pasar del PVI vecino y dos curvas consecutivas no pueden solaparse: cuando
    /// compiten, se conserva la de menor progresiva y la siguiente queda sin curva con la longitud máxima que cabría),
    /// cota de la rasante en cualquier progresiva y comprobación de lo releído del dibujo. No hay motor normativo: la
    /// pendiente máxima (12 %) es solo un aviso informativo.
    /// </summary>
    public static class Rasante
    {
        /// <summary>Margen al comparar progresivas y longitudes (metros).</summary>
        public const double Tolerancia = 1e-6;
        /// <summary>Un PVI que se sale del eje menos que esto (metros) se ajusta al extremo del eje con aviso, en vez de dar error (831.33 → 831.3274).</summary>
        public const double ToleranciaExtremo = 0.01;
        /// <summary>Por encima de esta pendiente (%, en valor absoluto) se avisa. No es una norma: es informativo.</summary>
        public const double PendienteAviso = 12.0;
        public const string ParabolaSimetrica = "parabola_simetrica";
        public const string Circular = "circular";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ parámetros
        /// <summary>Tipo de curva normalizado: null o vacío → parábola simétrica; acepta sinónimos; otra cosa es error.</summary>
        public static string TipoCurvaNormalizado(string pedido)
        {
            string t = (pedido ?? "").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
            t = t.Replace("á", "a").Replace("é", "e").Replace("í", "i").Replace("ó", "o").Replace("ú", "u");
            if (t.Length == 0 || t == ParabolaSimetrica || t == "parabola" || t == "parabolica" || t == "simetrica" || t == "parabola_simetrica") return ParabolaSimetrica;
            if (t == Circular || t == "circulo" || t == "arco") return Circular;
            throw new ArgumentException("El parámetro 'tipo_curva' debe ser '" + ParabolaSimetrica + "' (por defecto) o '" + Circular + "'; llegó '" + pedido + "'. Las parábolas asimétricas no están disponibles en esta versión.");
        }

        /// <summary>Texto en español del tipo de curva, para la acción y los avisos.</summary>
        public static string TextoTipoCurva(string tipo) => tipo == Circular ? "curva circular" : "parábola simétrica";

        /// <summary>Una longitud de curva, si viene, debe ser un número finito mayor que 0.</summary>
        public static void ValidarLongitudCurva(double? longitud, string donde)
        {
            if (!longitud.HasValue) return;
            if (double.IsNaN(longitud.Value) || double.IsInfinity(longitud.Value) || longitud.Value <= 0)
                throw new ArgumentException("La longitud de curva de " + donde + " debe ser un número mayor que 0 (metros); 0 o nada = sin curva.");
        }

        /// <summary>
        /// Lee 'pvis': lista JSON ordenada de objetos {"progresiva": n, "cota": n, "longitud_curva": n opcional} (también se
        /// aceptan [progresiva, cota, longitud] y las claves pk/estacion, elevacion/z, longitud/curva). Los PVI intermedios
        /// sin longitud propia reciben 'longitudDefecto' (null = sin curva). Mínimo 2 PVI. El orden y el rango se
        /// comprueban en Planificar.
        /// </summary>
        public static List<PviRasante> LeerPvis(JsonElement json, double? longitudDefecto)
        {
            if (json.ValueKind == JsonValueKind.Undefined || json.ValueKind == JsonValueKind.Null)
                throw new ArgumentException("Falta el parámetro obligatorio 'pvis': lista JSON ordenada por progresiva, por ejemplo [{\"progresiva\": 0, \"cota\": 100}, {\"progresiva\": 400, \"cota\": 110, \"longitud_curva\": 120}, {\"progresiva\": 831.33, \"cota\": 105}].");
            if (json.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("El parámetro 'pvis' debe ser una lista JSON de objetos {\"progresiva\": n, \"cota\": n, \"longitud_curva\": n opcional}; llegó " + json.ValueKind + ".");
            var lista = new List<PviRasante>();
            int posicion = 0;
            foreach (var e in json.EnumerateArray())
            {
                posicion++;
                double? progresiva = null, cota = null, longitud = null;
                if (e.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in e.EnumerateObject())
                    {
                        string n = p.Name.Trim().ToLowerInvariant();
                        if (n == "progresiva" || n == "pk" || n == "estacion" || n == "station" || n == "progresiva_pvi") progresiva = Numero(p.Value, posicion, p.Name);
                        else if (n == "cota" || n == "elevacion" || n == "z" || n == "elevation" || n == "cota_pvi") cota = Numero(p.Value, posicion, p.Name);
                        else if (n == "longitud_curva" || n == "longitud" || n == "curva" || n == "l") longitud = p.Value.ValueKind == JsonValueKind.Null ? (double?)null : Numero(p.Value, posicion, p.Name);
                    }
                }
                else if (e.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var v in e.EnumerateArray())
                    {
                        if (i == 0) progresiva = Numero(v, posicion, "progresiva");
                        else if (i == 1) cota = Numero(v, posicion, "cota");
                        else if (i == 2) longitud = v.ValueKind == JsonValueKind.Null ? (double?)null : Numero(v, posicion, "longitud_curva");
                        i++;
                    }
                }
                else throw new ArgumentException("El elemento " + posicion + " de 'pvis' no es un objeto {\"progresiva\": n, \"cota\": n} ni una lista [progresiva, cota]: " + e.GetRawText());
                if (!progresiva.HasValue) throw new ArgumentException("El elemento " + posicion + " de 'pvis' no tiene 'progresiva': " + e.GetRawText());
                if (!cota.HasValue) throw new ArgumentException("El elemento " + posicion + " de 'pvis' no tiene 'cota': " + e.GetRawText());
                bool propia = longitud.HasValue;
                if (longitud.HasValue && longitud.Value != 0) ValidarLongitudCurva(longitud, "el PVI " + posicion);
                if (!longitud.HasValue) longitud = longitudDefecto ?? 0;
                lista.Add(new PviRasante { Numero = posicion, Progresiva = progresiva.Value, Cota = cota.Value, LongitudPedida = longitud.Value, LongitudPropia = propia });
            }
            if (lista.Count < 2)
                throw new ArgumentException("'pvis' tiene " + lista.Count + " PVI; una rasante necesita al menos 2 (inicio y fin).");
            return lista;
        }

        private static double Numero(JsonElement v, int posicion, string campo)
        {
            if (v.ValueKind == JsonValueKind.Number) return Finito(v.GetDouble(), posicion, campo);
            if (v.ValueKind == JsonValueKind.String && double.TryParse((v.GetString() ?? "").Trim().Replace(',', '.'), NumberStyles.Float, Inv, out double d)) return Finito(d, posicion, campo);
            throw new ArgumentException("El campo '" + campo + "' del elemento " + posicion + " de 'pvis' debe ser un número: " + v.GetRawText());
        }

        private static double Finito(double d, int posicion, string campo)
        {
            if (double.IsNaN(d) || double.IsInfinity(d)) throw new ArgumentException("El campo '" + campo + "' del elemento " + posicion + " de 'pvis' no es un número válido.");
            return d;
        }

        // ------------------------------------------------------------------ geometría
        /// <summary>Planifica sin comprobar el rango del eje (pruebas y cálculos sueltos).</summary>
        public static PlanRasante Planificar(IList<PviRasante> pvis) => Planificar(pvis, double.NaN, double.NaN);

        /// <summary>
        /// Calcula la rasante prevista: comprueba el orden (progresivas estrictamente crecientes) y el rango del eje
        /// (un PVI hasta ToleranciaExtremo fuera se ajusta al extremo con aviso; más lejos, error), las pendientes de los
        /// tramos, y en cada PVI intermedio A, K, cresta/valle y si la curva cabe. 'inicioEje'/'finEje' en NaN = sin
        /// comprobar el rango.
        /// </summary>
        public static PlanRasante Planificar(IList<PviRasante> pvis, double inicioEje, double finEje)
        {
            if (pvis == null || pvis.Count < 2)
                throw new ArgumentException("'pvis' tiene " + (pvis?.Count ?? 0) + " PVI; una rasante necesita al menos 2 (inicio y fin).");
            var plan = new PlanRasante();
            bool conRango = !double.IsNaN(inicioEje) && !double.IsNaN(finEje);
            int n = pvis.Count;

            // Orden y rango
            for (int i = 0; i < n; i++)
            {
                var p = pvis[i];
                p.Numero = i + 1;
                if (i > 0 && p.Progresiva <= pvis[i - 1].Progresiva + Tolerancia)
                    throw new ArgumentException("Las progresivas de 'pvis' deben ir en orden estrictamente creciente: el PVI " + (i + 1) + " (" + Num(p.Progresiva) + ") no es mayor que el PVI " + i + " (" + Num(pvis[i - 1].Progresiva) + "). Ordena la lista por progresiva y quita los repetidos.");
                if (conRango)
                {
                    if (p.Progresiva < inicioEje - ToleranciaExtremo || p.Progresiva > finEje + ToleranciaExtremo)
                        throw new ArgumentException("El PVI " + (i + 1) + " está en la progresiva " + Num(p.Progresiva) + ", fuera del alineamiento (va de " + Num(inicioEje) + " a " + Num(finEje) + "). Usa listar_alineamientos para ver el rango del eje.");
                    if (p.Progresiva < inicioEje) { plan.Avisos.Add("PVI " + (i + 1) + ": la progresiva " + Num(p.Progresiva) + " se ajusta al inicio del eje (" + Num(inicioEje) + ")."); p.Progresiva = inicioEje; }
                    if (p.Progresiva > finEje) { plan.Avisos.Add("PVI " + (i + 1) + ": la progresiva " + Num(p.Progresiva) + " se ajusta al final del eje (" + Num(finEje) + ")."); p.Progresiva = finEje; }
                }
                p.EsExtremo = i == 0 || i == n - 1;
                p.LongitudAplicada = null; p.K = null; p.Tipo = null; p.Aviso = null; p.LongitudMaxima = null;
                p.ProgresivaInicio = p.ProgresivaFin = p.CotaInicio = p.CotaFin = null;
                plan.Pvis.Add(p);
            }

            // Tramos y pendientes
            for (int i = 0; i < n - 1; i++)
            {
                var t = new TramoRasante { Numero = i + 1, ProgresivaInicio = pvis[i].Progresiva, ProgresivaFin = pvis[i + 1].Progresiva, CotaInicio = pvis[i].Cota, CotaFin = pvis[i + 1].Cota };
                if (Math.Abs(t.PendientePct) > PendienteAviso + Tolerancia)
                {
                    t.Aviso = "Tramo " + t.Numero + " (" + Num(t.ProgresivaInicio) + " a " + Num(t.ProgresivaFin) + "): pendiente " + Num(t.PendientePct) + " %, mayor que " + Num(PendienteAviso) + " % en valor absoluto (aviso informativo; no hay norma cargada).";
                    plan.Avisos.Add(t.Aviso);
                }
                plan.Tramos.Add(t);
                pvis[i].PendienteSalida = t.PendientePct;
                pvis[i + 1].PendienteEntrada = t.PendientePct;
            }

            // Extremos con longitud: no llevan curva
            foreach (var p in new[] { pvis[0], pvis[n - 1] })
                if (p.LongitudPedida > 0 && p.LongitudPropia)
                    plan.Avisos.Add("PVI " + p.Numero + ": es el " + (p.Numero == 1 ? "primero" : "último") + " de la rasante y no lleva curva vertical; se ignora su longitud_curva " + Num(p.LongitudPedida) + ".");

            // Curvas en los PVI intermedios
            double mitadAnterior = 0;   // mitad de la curva del PVI anterior, ya consumida en el tramo que llega
            for (int k = 1; k <= n - 2; k++)
            {
                var p = pvis[k];
                double gIn = plan.Tramos[k - 1].PendientePct, gOut = plan.Tramos[k].PendientePct;
                double a = gOut - gIn;
                p.CambioPendiente = a;
                p.Tipo = a < -Tolerancia ? "cresta" : a > Tolerancia ? "valle" : null;
                double libreAntes = plan.Tramos[k - 1].Longitud - mitadAnterior;
                double libreDespues = plan.Tramos[k].Longitud;
                double maxima = 2 * Math.Min(libreAntes, libreDespues);
                p.LongitudMaxima = maxima > Tolerancia ? Math.Floor(maxima * 1000) / 1000 : (double?)null;
                double aplicada = 0;
                if (p.LongitudPedida <= 0)
                {
                    // sin curva pedida: quiebre de pendiente
                }
                else if (p.Tipo == null)
                {
                    p.Aviso = "PVI " + p.Numero + " (" + Num(p.Progresiva) + "): las pendientes de entrada y salida son iguales (" + Num(gIn) + " %, A = 0); no hace falta curva vertical y no se crea.";
                }
                else
                {
                    double mitad = p.LongitudPedida / 2;
                    if (mitad <= libreAntes + Tolerancia && mitad <= libreDespues + Tolerancia)
                    {
                        aplicada = p.LongitudPedida;
                        p.LongitudAplicada = aplicada;
                        p.K = aplicada / Math.Abs(a);
                        p.ProgresivaInicio = p.Progresiva - mitad;
                        p.ProgresivaFin = p.Progresiva + mitad;
                        p.CotaInicio = p.Cota - gIn / 100 * mitad;
                        p.CotaFin = p.Cota + gOut / 100 * mitad;
                    }
                    else
                    {
                        p.Aviso = "PVI " + p.Numero + " (" + Num(p.Progresiva) + "): la curva de " + Num(p.LongitudPedida) + " m no cabe (necesita " + Num(mitad) + " m a cada lado; libre " + Num(libreAntes) + " m antes y " + Num(libreDespues) + " m después); se deja sin curva, como quiebre de pendiente. "
                            + (p.LongitudMaxima.HasValue ? "Longitud máxima que cabría: " + Num(p.LongitudMaxima.Value) + " m." : "No cabe ninguna curva: la curva anterior llega hasta este PVI.");
                    }
                }
                if (p.Aviso != null) plan.Avisos.Add(p.Aviso);
                mitadAnterior = aplicada / 2;
            }

            // Cotas extremas de la rasante dibujada (vértices de las parábolas incluidos)
            var candidatas = new List<double>();
            foreach (var p in plan.Pvis) candidatas.Add(p.Progresiva);
            foreach (var p in plan.Pvis.Where(q => q.LongitudAplicada.HasValue))
            {
                candidatas.Add(p.ProgresivaInicio.Value); candidatas.Add(p.ProgresivaFin.Value);
                double? v = ProgresivaVertice(p);
                if (v.HasValue) candidatas.Add(v.Value);
            }
            plan.CotaMinima = candidatas.Min(s => CotaEn(plan, s));
            plan.CotaMaxima = candidatas.Max(s => CotaEn(plan, s));
            return plan;
        }

        /// <summary>Progresiva del punto alto (cresta) o bajo (valle) de la parábola si queda dentro de la curva (las pendientes cambian de signo); si no, null.</summary>
        public static double? ProgresivaVertice(PviRasante p)
        {
            if (!p.LongitudAplicada.HasValue || !p.PendienteEntrada.HasValue || !p.PendienteSalida.HasValue) return null;
            double gIn = p.PendienteEntrada.Value, gOut = p.PendienteSalida.Value;
            if (gIn * gOut >= 0) return null;
            double x = gIn / (gIn - gOut) * p.LongitudAplicada.Value;
            return p.ProgresivaInicio.Value + x;
        }

        /// <summary>
        /// Cota de la rasante prevista en una progresiva: sobre la parábola si cae dentro de una curva (la circular se
        /// aproxima con la parábola: la diferencia es milimétrica en curvas de carretera), si no sobre la tangente.
        /// Error si la progresiva se sale de la rasante.
        /// </summary>
        public static double CotaEn(PlanRasante plan, double progresiva)
        {
            if (progresiva < plan.ProgresivaInicial - Tolerancia || progresiva > plan.ProgresivaFinal + Tolerancia)
                throw new ArgumentException("La progresiva " + Num(progresiva) + " está fuera de la rasante (" + Num(plan.ProgresivaInicial) + " a " + Num(plan.ProgresivaFinal) + ").");
            foreach (var p in plan.Pvis)
            {
                if (!p.LongitudAplicada.HasValue) continue;
                if (progresiva >= p.ProgresivaInicio.Value - Tolerancia && progresiva <= p.ProgresivaFin.Value + Tolerancia)
                {
                    double x = Math.Max(0, Math.Min(p.LongitudAplicada.Value, progresiva - p.ProgresivaInicio.Value));
                    double gIn = p.PendienteEntrada.Value / 100, a = p.CambioPendiente.Value / 100, l = p.LongitudAplicada.Value;
                    return p.CotaInicio.Value + gIn * x + a / (2 * l) * x * x;
                }
            }
            for (int i = 0; i < plan.Tramos.Count; i++)
            {
                var t = plan.Tramos[i];
                if (progresiva <= t.ProgresivaFin + Tolerancia || i == plan.Tramos.Count - 1)
                    return t.CotaInicio + t.PendientePct / 100 * (progresiva - t.ProgresivaInicio);
            }
            return plan.Pvis[plan.Pvis.Count - 1].Cota;
        }

        // ------------------------------------------------------------------ comprobaciones tras crear
        /// <summary>
        /// Compara los PVI releídos del dibujo (progresiva, cota; en cualquier orden) con los previstos: mismo número y,
        /// en orden de progresiva, cada progresiva y cada cota dentro de 'tolerancia'. Devuelve el motivo del fallo o null.
        /// </summary>
        public static string MotivoPvisNoCoinciden(IList<PviRasante> previstos, IList<(double progresiva, double cota)> leidos, double tolerancia = 0.01)
        {
            if (leidos == null) return "no se pudieron leer sus PVI";
            if (leidos.Count != previstos.Count) return "tiene " + leidos.Count + " PVI y se pidieron " + previstos.Count;
            var orden = leidos.OrderBy(l => l.progresiva).ToList();
            for (int i = 0; i < previstos.Count; i++)
            {
                if (Math.Abs(orden[i].progresiva - previstos[i].Progresiva) > tolerancia)
                    return "su PVI " + (i + 1) + " quedó en la progresiva " + Num(orden[i].progresiva) + " y se pidió " + Num(previstos[i].Progresiva);
                if (Math.Abs(orden[i].cota - previstos[i].Cota) > tolerancia)
                    return "su PVI " + (i + 1) + " (progresiva " + Num(previstos[i].Progresiva) + ") quedó con cota " + Num(orden[i].cota) + " y se pidió " + Num(previstos[i].Cota);
            }
            return null;
        }

        /// <summary>Comprueba el rango releído: progresivas inicial y final iguales a las pedidas (±tolerancia). Devuelve el motivo o null.</summary>
        public static string MotivoRangoNoCoincide(double inicioPrevisto, double finPrevisto, double inicioLeido, double finLeido, double tolerancia = 0.01)
        {
            if (double.IsNaN(inicioLeido) || double.IsNaN(finLeido)) return "sus progresivas no son números válidos";
            if (Math.Abs(inicioLeido - inicioPrevisto) > tolerancia) return "empieza en " + Num(inicioLeido) + " y se pidió " + Num(inicioPrevisto);
            if (Math.Abs(finLeido - finPrevisto) > tolerancia) return "termina en " + Num(finLeido) + " y se pidió " + Num(finPrevisto);
            return null;
        }

        /// <summary>
        /// Decide en qué unidad devuelve la API una pendiente comparándola con la prevista (%): "tanto_por_uno" si
        /// leida × 100 ≈ prevista (0.025 = 2,5 %), "porcentaje" si leida ≈ prevista, null si no coincide con ninguna.
        /// </summary>
        public static string UnidadPendiente(double previstaPct, double leida, double tolerancia = 0.01)
        {
            if (Math.Abs(leida * 100 - previstaPct) <= tolerancia) return "tanto_por_uno";
            if (Math.Abs(leida - previstaPct) <= tolerancia) return "porcentaje";
            return null;
        }

        /// <summary>Pendiente leída de la API pasada a %, según la unidad decidida con UnidadPendiente (si no se sabe, se asume tanto por uno).</summary>
        public static double PendienteEnPorcentaje(double leida, string unidad) => unidad == "porcentaje" ? leida : leida * 100;

        /// <summary>Texto de la acción para la respuesta y el log.</summary>
        public static string Accion(string nombre, string alineamiento, PlanRasante plan, string tipoCurva)
            => "Crear la rasante '" + nombre + "' (perfil de diseño FG) del alineamiento '" + alineamiento + "' con " + plan.NumeroPvis + " PVI y " + plan.NumeroCurvas + " curva" + (plan.NumeroCurvas == 1 ? "" : "s") + " vertical" + (plan.NumeroCurvas == 1 ? "" : "es") + (plan.NumeroCurvas > 0 ? " (" + TextoTipoCurva(tipoCurva) + ")" : "") + ", de " + Num(plan.ProgresivaInicial) + " a " + Num(plan.ProgresivaFinal);

        private static string Num(double v) => v.ToString("0.###", Inv);
    }
}
