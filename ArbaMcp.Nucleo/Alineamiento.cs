using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>Un vértice de la polilínea de tangentes (x = este, y = norte). Sin cota: el eje es plano.</summary>
    public struct Punto2
    {
        public double X, Y;
        public Punto2(double x, double y) { X = x; Y = y; }
        public double DistanciaA(Punto2 o) => Math.Sqrt((o.X - X) * (o.X - X) + (o.Y - Y) * (o.Y - Y));
    }

    /// <summary>
    /// Un PI (punto de intersección de tangentes, vértice intermedio de la polilínea) del alineamiento previsto, con la
    /// curva circular que le corresponde si cabe. Las progresivas siguen el convenio de Civil 3D: la del PI es la del
    /// PC más la tangente (PIStation).
    /// </summary>
    public sealed class PiAlineamiento
    {
        /// <summary>1 para el primer PI intermedio (el vértice 2 de la polilínea).</summary>
        public int Numero;
        public double X, Y;
        /// <summary>Deflexión Δ entre la tangente de entrada y la de salida, en grados (0 a 180).</summary>
        public double DeflexionGrados;
        /// <summary>"derecha", "izquierda" o "ninguno" (vértices alineados).</summary>
        public string Giro;
        /// <summary>Radio pedido para este PI; 0 = sin curva pedida.</summary>
        public double RadioPedido;
        /// <summary>Radio de la curva que se crea; null si el PI queda sin curva.</summary>
        public double? RadioAplicado;
        /// <summary>Tangente T = R·tan(Δ/2) de la curva aplicada; null sin curva.</summary>
        public double? Tangente;
        /// <summary>Longitud de curva L = R·Δ (Δ en radianes); null sin curva.</summary>
        public double? LongitudCurva;
        /// <summary>Progresiva del PI (PC + T, como PIStation de Civil 3D; sin curva, la del vértice).</summary>
        public double Progresiva;
        public double? ProgresivaPc, ProgresivaPt;
        /// <summary>Radio máximo que cabría en este PI con los tramos que le quedan libres; null si no cabe ninguna curva.</summary>
        public double? RadioMaximo;
        /// <summary>Explicación cuando el PI queda sin la curva pedida; null si la curva se aplica o no se pidió.</summary>
        public string Aviso;

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["numero"] = Numero,
            ["progresiva"] = Argumentos.Redondear(Progresiva),
            ["x"] = Argumentos.Redondear(X),
            ["y"] = Argumentos.Redondear(Y),
            ["deflexion_grados"] = Argumentos.Redondear(DeflexionGrados),
            ["giro"] = Giro,
            ["radio_pedido"] = Argumentos.Redondear(RadioPedido),
            ["radio_aplicado"] = RadioAplicado.HasValue ? Argumentos.Redondear(RadioAplicado.Value) : null,
            ["tangente"] = Tangente.HasValue ? Argumentos.Redondear(Tangente.Value) : null,
            ["longitud_curva"] = LongitudCurva.HasValue ? Argumentos.Redondear(LongitudCurva.Value) : null,
            ["progresiva_pc"] = ProgresivaPc.HasValue ? Argumentos.Redondear(ProgresivaPc.Value) : null,
            ["progresiva_pt"] = ProgresivaPt.HasValue ? Argumentos.Redondear(ProgresivaPt.Value) : null,
            ["radio_maximo"] = RadioMaximo.HasValue ? Argumentos.Redondear(RadioMaximo.Value) : null,
            ["aviso"] = Aviso
        };
    }

    /// <summary>El alineamiento previsto a partir de la polilínea: vértices, tramos, PI con sus curvas y longitudes.</summary>
    public sealed class PlanAlineamiento
    {
        public List<Punto2> Vertices = new List<Punto2>();
        /// <summary>Longitud de cada tramo recto de la polilínea (vértice i → i+1).</summary>
        public List<double> Tramos = new List<double>();
        public List<PiAlineamiento> Pis = new List<PiAlineamiento>();
        /// <summary>Radio resuelto por PI (0 = sin curva pedida), en el orden de los PI.</summary>
        public List<double> Radios = new List<double>();
        public double LongitudPolilinea;
        /// <summary>Longitud del eje con las curvas: la de la polilínea menos, por cada curva, 2T − L.</summary>
        public double LongitudTotal;
        public double ProgresivaInicial, ProgresivaFinal;
        public int NumeroPi => Pis.Count;
        public int NumeroCurvas => Pis.Count(p => p.RadioAplicado.HasValue);
        /// <summary>Avisos: PI sin curva (radio que no cabe, vértices alineados, giro de 180°).</summary>
        public List<string> Avisos = new List<string>();

        public List<Dictionary<string, object>> PisComoDiccionarios() => Pis.Select(p => p.ComoDiccionario()).ToList();
    }

    /// <summary>
    /// Geometría del alineamiento desde una polilínea de tangentes (crear_alineamiento_desde_polilinea, 1.8.0), sin
    /// Civil 3D: deflexiones, tangentes, longitudes de curva, si cabe cada curva, radio máximo que cabría y progresivas.
    /// Regla para que una curva quepa: la suma de las tangentes de dos curvas consecutivas no puede superar la longitud
    /// del tramo entre sus PI, y la primera y la última no pueden superar el tramo inicial o final. Cuando dos curvas
    /// compiten por un tramo corto se conserva la de menor progresiva (la primera) y la siguiente se deja sin curva
    /// con un aviso que dice el radio máximo que sí cabría.
    /// </summary>
    public static class Alineamiento
    {
        /// <summary>Margen al comparar tangentes con tramos (metros).</summary>
        public const double Tolerancia = 1e-6;
        /// <summary>Deflexión por debajo de la cual los vértices se consideran alineados (radianes, ≈ 0,00006°).</summary>
        public const double DeflexionMinima = 1e-6;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static double Tangente(double radio, double deflexionRad) => radio * Math.Tan(deflexionRad / 2);
        public static double LongitudCurva(double radio, double deflexionRad) => radio * deflexionRad;
        public static double Grados(double rad) => rad * 180.0 / Math.PI;

        /// <summary>Radio por defecto: debe ser un número mayor que 0.</summary>
        public static void ValidarRadio(double radio)
        {
            if (double.IsNaN(radio) || double.IsInfinity(radio) || radio <= 0)
                throw new ArgumentException("El parámetro 'radio' debe ser un número mayor que 0 (radio por defecto de las curvas, en unidades del dibujo).");
        }

        /// <summary>
        /// Comprueba los vértices: al menos dos y sin vértices repetidos consecutivos (un tramo de longitud 0 no es una
        /// tangente). Devuelve las longitudes de los tramos.
        /// </summary>
        public static List<double> Tramos(IList<Punto2> vertices)
        {
            if (vertices == null || vertices.Count < 2)
                throw new ArgumentException("La polilínea tiene " + (vertices?.Count ?? 0) + " vértices; un eje necesita al menos 2 (inicio y fin).");
            var tramos = new List<double>();
            for (int i = 1; i < vertices.Count; i++)
            {
                double d = vertices[i - 1].DistanciaA(vertices[i]);
                if (d <= Tolerancia)
                    throw new ArgumentException("Los vértices " + i + " y " + (i + 1) + " de la polilínea están en el mismo punto (" + vertices[i].X.ToString("0.###", Inv) + ", " + vertices[i].Y.ToString("0.###", Inv) + "); quita el vértice repetido.");
                tramos.Add(d);
            }
            return tramos;
        }

        /// <summary>Deflexión (radianes, 0..π) en el vértice b entre los tramos a→b y b→c, y el giro: 1 derecha, -1 izquierda, 0 alineados.</summary>
        public static double Deflexion(Punto2 a, Punto2 b, Punto2 c, out int giro)
        {
            double ux = b.X - a.X, uy = b.Y - a.Y, vx = c.X - b.X, vy = c.Y - b.Y;
            double lu = Math.Sqrt(ux * ux + uy * uy), lv = Math.Sqrt(vx * vx + vy * vy);
            double cos = (ux * vx + uy * vy) / (lu * lv);
            cos = Math.Max(-1.0, Math.Min(1.0, cos));
            double cruz = ux * vy - uy * vx;   // > 0: giro a la izquierda (antihorario); < 0: a la derecha (horario)
            double delta = Math.Acos(cos);
            giro = delta < DeflexionMinima ? 0 : cruz < 0 ? 1 : -1;
            return delta;
        }

        /// <summary>
        /// Radios por PI a partir del parámetro json 'radios_por_pi': una lista de números en el orden de los PI
        /// ([80, 0, 120]; null o 0 = sin curva) o una lista de objetos {"pi": n, "radio": r} con n desde 1. Los PI que no
        /// se nombran usan el radio por defecto. Devuelve un radio por PI (0 = sin curva).
        /// </summary>
        public static List<double> RadiosPorPi(JsonElement json, int numeroPi, double radioDefecto)
        {
            var radios = Enumerable.Repeat(radioDefecto, numeroPi).ToList();
            if (json.ValueKind == JsonValueKind.Undefined || json.ValueKind == JsonValueKind.Null) return radios;
            if (json.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("El parámetro 'radios_por_pi' debe ser una lista JSON: [80, 0, 120] (un radio por PI, 0 o null = sin curva) o [{\"pi\": 1, \"radio\": 80}, {\"pi\": 3, \"radio\": 0}].");
            int posicion = 0;
            foreach (var e in json.EnumerateArray())
            {
                posicion++;
                int pi; double radio;
                if (e.ValueKind == JsonValueKind.Object)
                {
                    if (!e.TryGetProperty("pi", out var jp) || !LeerEntero(jp, out pi))
                        throw new ArgumentException("El elemento " + posicion + " de 'radios_por_pi' no tiene un 'pi' entero: " + e.GetRawText());
                    radio = e.TryGetProperty("radio", out var jr) ? LeerRadio(jr, "el PI " + pi) : 0;
                }
                else
                {
                    pi = posicion;
                    radio = LeerRadio(e, "la posición " + posicion);
                }
                if (pi < 1 || pi > numeroPi)
                    throw new ArgumentException("'radios_por_pi' nombra el PI " + pi + " y la polilínea tiene " + numeroPi + " PI intermedio" + (numeroPi == 1 ? "" : "s") + " (vértices sin contar el primero y el último). Usa 'simular' para ver la lista de PI.");
                radios[pi - 1] = radio;
            }
            return radios;
        }

        private static bool LeerEntero(JsonElement e, out int v)
        {
            v = 0;
            if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out v)) return true;
            if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), NumberStyles.Integer, Inv, out v)) return true;
            return false;
        }

        private static double LeerRadio(JsonElement e, string donde)
        {
            double r;
            if (e.ValueKind == JsonValueKind.Null) return 0;
            if (e.ValueKind == JsonValueKind.Number) r = e.GetDouble();
            else if (e.ValueKind == JsonValueKind.String && double.TryParse((e.GetString() ?? "").Replace(',', '.'), NumberStyles.Float, Inv, out double d)) r = d;
            else throw new ArgumentException("El radio de " + donde + " en 'radios_por_pi' debe ser un número (0 o null = sin curva): " + e.GetRawText());
            if (double.IsNaN(r) || double.IsInfinity(r) || r < 0)
                throw new ArgumentException("El radio de " + donde + " en 'radios_por_pi' no puede ser negativo (0 o null = sin curva).");
            return r;
        }

        /// <summary>
        /// Calcula el alineamiento previsto: un PI por vértice intermedio con su deflexión, la curva del radio que le
        /// toca si cabe (si no, sin curva y aviso con el radio máximo), las progresivas (PC, PI, PT) y la longitud total.
        /// 'radios' trae un radio por PI (0 = sin curva); si es null, todos usan 'radioDefecto'.
        /// </summary>
        public static PlanAlineamiento Planificar(IList<Punto2> vertices, double radioDefecto, IList<double> radios = null, double progresivaInicial = 0)
        {
            ValidarRadio(radioDefecto);
            var plan = new PlanAlineamiento { Vertices = vertices.ToList(), ProgresivaInicial = progresivaInicial };
            plan.Tramos = Tramos(vertices);
            plan.LongitudPolilinea = plan.Tramos.Sum();
            int numeroPi = vertices.Count - 2;
            if (radios == null) radios = Enumerable.Repeat(radioDefecto, numeroPi).ToList();
            if (radios.Count != numeroPi) throw new ArgumentException("Se esperaban " + numeroPi + " radios (uno por PI) y llegaron " + radios.Count + ".");
            plan.Radios = radios.ToList();

            double tangenteAnterior = 0;   // tangente ya consumida en el tramo anterior por la curva del PI anterior
            for (int k = 1; k <= numeroPi; k++)
            {
                var pi = new PiAlineamiento { Numero = k, X = vertices[k].X, Y = vertices[k].Y, RadioPedido = radios[k - 1] };
                double delta = Deflexion(vertices[k - 1], vertices[k], vertices[k + 1], out int giro);
                pi.DeflexionGrados = Grados(delta);
                pi.Giro = giro == 0 ? "ninguno" : giro > 0 ? "derecha" : "izquierda";
                double libreAntes = plan.Tramos[k - 1] - tangenteAnterior;   // lo que queda del tramo anterior
                double libreDespues = plan.Tramos[k];                         // el tramo siguiente entero (la curva siguiente se ajusta a lo que quede)

                if (pi.RadioPedido <= 0)
                {
                    // sin curva pedida: PI de quiebre
                }
                else if (giro == 0)
                {
                    pi.Aviso = "PI " + k + ": los vértices están alineados (deflexión 0°); no hace falta curva y no se crea.";
                }
                else if (Math.PI - delta < DeflexionMinima)
                {
                    pi.Aviso = "PI " + k + ": la polilínea vuelve sobre sí misma (deflexión 180°); no cabe ninguna curva.";
                }
                else
                {
                    double tan = Math.Tan(delta / 2);
                    double radioMaximo = Math.Min(libreAntes, libreDespues) / tan;
                    pi.RadioMaximo = radioMaximo > Tolerancia ? Math.Floor(radioMaximo * 1000) / 1000 : (double?)null;
                    double t = Tangente(pi.RadioPedido, delta);
                    if (t <= libreAntes + Tolerancia && t <= libreDespues + Tolerancia)
                    {
                        pi.RadioAplicado = pi.RadioPedido;
                        pi.Tangente = t;
                        pi.LongitudCurva = LongitudCurva(pi.RadioPedido, delta);
                    }
                    else
                    {
                        pi.Aviso = "PI " + k + ": el radio " + Num(pi.RadioPedido) + " no cabe (tangente " + Num(t) + " m; libre " + Num(libreAntes) + " m antes y " + Num(libreDespues) + " m después); se deja sin curva. "
                            + (pi.RadioMaximo.HasValue ? "Radio máximo que cabría: " + Num(pi.RadioMaximo.Value) + "." : "No cabe ninguna curva: la curva anterior consume todo el tramo.");
                    }
                }
                if (pi.Aviso != null) plan.Avisos.Add(pi.Aviso);
                plan.Pis.Add(pi);
                tangenteAnterior = pi.Tangente ?? 0;
            }

            // Progresivas: se recorre el eje con las curvas ya decididas
            double s = progresivaInicial;   // progresiva del punto de partida del tramo recto en curso (inicio o PT anterior)
            double consumidaAntes = 0;
            for (int k = 1; k <= numeroPi; k++)
            {
                var pi = plan.Pis[k - 1];
                double rectaHastaPi = plan.Tramos[k - 1] - consumidaAntes;
                if (pi.Tangente.HasValue)
                {
                    pi.ProgresivaPc = s + rectaHastaPi - pi.Tangente.Value;
                    pi.Progresiva = pi.ProgresivaPc.Value + pi.Tangente.Value;
                    pi.ProgresivaPt = pi.ProgresivaPc.Value + pi.LongitudCurva.Value;
                    s = pi.ProgresivaPt.Value;
                    consumidaAntes = pi.Tangente.Value;
                }
                else
                {
                    pi.Progresiva = s + rectaHastaPi;
                    s = pi.Progresiva;
                    consumidaAntes = 0;
                }
            }
            plan.ProgresivaFinal = s + plan.Tramos[plan.Tramos.Count - 1] - consumidaAntes;
            plan.LongitudTotal = plan.ProgresivaFinal - progresivaInicial;
            return plan;
        }

        private static string Num(double v) => v.ToString("0.###", Inv);

        /// <summary>Texto del error cuando la polilínea no es 2D de tangentes (arcos, 3D o cerrada), con cómo dibujarla.</summary>
        public static string MensajePolilineaNoValida(string referencia, string motivo)
            => "La polilínea " + referencia + " no sirve como eje: " + motivo + ". Se espera una polilínea 2D abierta de tangentes (solo tramos rectos; cada vértice intermedio es un PI). "
             + "Dibújala en Civil 3D con _.PLINE marcando los PI sin la opción Arco, o con ejecutar_comando: \"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  \" (dos espacios al final para terminar). "
             + "Las curvas las añade esta herramienta con 'radio' y 'radios_por_pi'.";
    }
}
