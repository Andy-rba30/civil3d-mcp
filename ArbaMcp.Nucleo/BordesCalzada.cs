using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Un ensanchamiento (sobreancho) pedido en un borde de calzada: el tramo de progresivas del eje que se pasa a
    /// OffsetAlignmentInfo.AddWidening, el sobreancho que se añade al desplazamiento nominal y, si se pide, la longitud
    /// de la transición de entrada y de salida.
    /// </summary>
    public sealed class Ensanchamiento
    {
        /// <summary>Posición en la lista 'ensanchamientos' (desde 1), para los mensajes.</summary>
        public int Numero;
        /// <summary>BordesCalzada.Izquierdo o BordesCalzada.Derecho.</summary>
        public string Borde;
        public double ProgresivaInicial, ProgresivaFinal;
        /// <summary>Lo que se añade al desplazamiento nominal (m, &gt; 0).</summary>
        public double Sobreancho;
        /// <summary>Longitud de la transición de entrada y de salida (m, ≥ 0) o null si la decide Civil 3D.</summary>
        public double? LongitudTransicion;

        /// <summary>Lo que el pedido ocupa contando la transición (si se pidió); sin ella, el propio tramo.</summary>
        public double InicioOcupado => ProgresivaInicial - (LongitudTransicion ?? 0);
        public double FinOcupado => ProgresivaFinal + (LongitudTransicion ?? 0);

        /// <summary>El pedido como diccionario para la respuesta, con el desplazamiento total que se pasará a AddWidening.</summary>
        public Dictionary<string, object> ComoDiccionario(double desplazamientoNominal)
        {
            var d = new Dictionary<string, object>
            {
                ["numero"] = Numero,
                ["borde"] = Borde,
                ["progresiva_inicial"] = Argumentos.Redondear(ProgresivaInicial),
                ["progresiva_final"] = Argumentos.Redondear(ProgresivaFinal),
                ["sobreancho"] = Argumentos.Redondear(Sobreancho),
                ["desplazamiento_total"] = Argumentos.Redondear(Math.Abs(desplazamientoNominal) + Sobreancho),
                ["longitud_transicion"] = LongitudTransicion.HasValue ? Argumentos.Redondear(LongitudTransicion.Value) : null
            };
            if (LongitudTransicion.HasValue)
                d["transicion_prevista"] = new Dictionary<string, object>
                {
                    ["entrada"] = new[] { Argumentos.Redondear(InicioOcupado), Argumentos.Redondear(ProgresivaInicial) },
                    ["salida"] = new[] { Argumentos.Redondear(ProgresivaFinal), Argumentos.Redondear(FinOcupado) },
                    ["supuesto"] = BordesCalzada.SupuestoTransicion
                };
            else
                d["transicion_prevista"] = "La decide Civil 3D (AddWidening): se mide al crear.";
            return d;
        }
    }

    /// <summary>
    /// Una zona ensanchada medida en un borde: progresivas del eje donde empieza y acaba la transición de entrada, el
    /// tramo pleno y la transición de salida, y el sobreancho medido. Las transiciones se estiman con los puntos donde
    /// el sobreancho cruza el 10 % y el 90 % del máximo, extrapolados como una rampa lineal.
    /// </summary>
    public sealed class ZonaMedida
    {
        public double InicioTransicion, InicioPleno, FinPleno, FinTransicion;
        /// <summary>Máximo sobreancho medido en la zona.</summary>
        public double SobreanchoMaximo;
        /// <summary>Media del sobreancho medido en el tramo pleno.</summary>
        public double SobreanchoMedio;
        /// <summary>La zona empieza o acaba en un extremo del tramo medido: esa transición no se pudo ver entera.</summary>
        public bool CortadaAlInicio, CortadaAlFinal;
        /// <summary>Sobreancho medido al 0, 25, 50, 75 y 100 % de cada transición estimada (progresiva, sobreancho).</summary>
        public List<double[]> PuntosEntrada = new List<double[]>(), PuntosSalida = new List<double[]>();

        public double LongitudEntrada => InicioPleno - InicioTransicion;
        public double LongitudSalida => FinTransicion - FinPleno;

        public Dictionary<string, object> ComoDiccionario()
        {
            Func<List<double[]>, List<object>> puntos = l => l.Select(p => (object)new Dictionary<string, object>
            {
                ["progresiva"] = Argumentos.Redondear(p[0]),
                ["sobreancho"] = Argumentos.Redondear(p[1])
            }).ToList();
            return new Dictionary<string, object>
            {
                ["inicio_transicion"] = Argumentos.Redondear(InicioTransicion),
                ["inicio_pleno"] = Argumentos.Redondear(InicioPleno),
                ["fin_pleno"] = Argumentos.Redondear(FinPleno),
                ["fin_transicion"] = Argumentos.Redondear(FinTransicion),
                ["longitud_entrada"] = Argumentos.Redondear(LongitudEntrada),
                ["longitud_salida"] = Argumentos.Redondear(LongitudSalida),
                ["sobreancho_maximo"] = Argumentos.Redondear(SobreanchoMaximo),
                ["sobreancho_medio"] = Argumentos.Redondear(SobreanchoMedio),
                ["cortada_al_inicio"] = CortadaAlInicio,
                ["cortada_al_final"] = CortadaAlFinal,
                ["puntos_entrada"] = puntos(PuntosEntrada),
                ["puntos_salida"] = puntos(PuntosSalida)
            };
        }
    }

    /// <summary>
    /// Lo medido en un borde a lo largo del eje: el desplazamiento (con signo, negativo a la izquierda, como punto_a_pk)
    /// en cada progresiva muestreada y las zonas ensanchadas que salen de ahí.
    /// </summary>
    public sealed class PerfilBorde
    {
        public double Inicio, Fin, Paso;
        public int Muestras, MuestrasFallidas;
        /// <summary>-1: el borde queda a la izquierda del eje; +1: a la derecha; 0: no se pudo medir.</summary>
        public int Signo;
        /// <summary>Desplazamiento medido (valor absoluto) fuera de las zonas ensanchadas; null si no hay muestras así.</summary>
        public double? NominalMedido;
        public List<ZonaMedida> Zonas = new List<ZonaMedida>();
        /// <summary>Muestras válidas (progresiva, desplazamiento con signo), en orden de progresiva.</summary>
        public List<double[]> Puntos = new List<double[]>();

        public string Lado => Signo < 0 ? BordesCalzada.Izquierdo : Signo > 0 ? BordesCalzada.Derecho : null;

        /// <summary>Mayor sobreancho medido entre dos progresivas (muestras y, si se dan, puntos extra); null si no hay ninguno.</summary>
        public double? SobreanchoMaximoEntre(double a, double b, double nominal)
        {
            double? max = null;
            foreach (var p in Puntos)
            {
                if (p[0] < a - 1e-9 || p[0] > b + 1e-9) continue;
                double e = Math.Abs(p[1]) - nominal;
                if (!max.HasValue || e > max.Value) max = e;
            }
            return max;
        }

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["lado_medido"] = Lado,
            ["desplazamiento_medido"] = NominalMedido.HasValue ? Argumentos.Redondear(NominalMedido.Value) : null,
            ["muestreo"] = new Dictionary<string, object>
            {
                ["desde"] = Argumentos.Redondear(Inicio),
                ["hasta"] = Argumentos.Redondear(Fin),
                ["paso"] = Argumentos.Redondear(Paso),
                ["muestras"] = Muestras,
                ["fallidas"] = MuestrasFallidas
            },
            ["ensanchamientos_medidos"] = Zonas.Select(z => (object)z.ComoDiccionario()).ToList()
        };
    }

    /// <summary>Resultado de comparar un ensanchamiento pedido con lo medido en el dibujo.</summary>
    public sealed class ComparacionEnsanchamiento
    {
        /// <summary>Texto del error (regla 4: el dibujo no refleja lo pedido) o null si el sobreancho pleno coincide.</summary>
        public string Error;
        /// <summary>Aviso sobre la forma de la transición o null.</summary>
        public string Aviso;
        /// <summary>"fuera" (el rango pedido es el tramo pleno), "dentro" (el rango incluye las transiciones), "otra" o null.</summary>
        public string Transicion;
        public double? SobreanchoMedido;
        public ZonaMedida Zona;

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["sobreancho_medido"] = SobreanchoMedido.HasValue ? Argumentos.Redondear(SobreanchoMedido.Value) : null,
            ["transicion"] = Transicion,
            ["explicacion_transicion"] = Zona == null ? null : BordesCalzada.ExplicarTransicion(Transicion, Zona),
            ["zona_medida"] = Zona?.ComoDiccionario()
        };
    }

    /// <summary>
    /// Bordes de calzada (crear_bordes_calzada y listar_bordes_calzada, 1.21.0) sin Civil 3D: lectura y validación de
    /// los ensanchamientos pedidos, desplazamiento total que se pasa a AddWidening, medición del borde a lo largo del eje
    /// (a partir de una función que el plugin construye con Alignment.PointLocation y Alignment.StationOffset) y
    /// comparación de lo medido con lo pedido. Cómo modela Civil 3D la transición de un ensanchamiento no se sabe
    /// todavía (VALIDACION_121, paso decisivo): por eso la verificación se hace con lo medido en el dibujo, no con las
    /// regiones de la API, y la forma de la transición solo avisa.
    /// </summary>
    public static class BordesCalzada
    {
        public const string Izquierdo = "izquierdo";
        public const string Derecho = "derecho";
        public const int MaximoEnsanchamientos = 200;
        /// <summary>Desplazamiento nominal releído frente al pedido (m).</summary>
        public const double ToleranciaNominal = 0.001;
        /// <summary>Desplazamiento medido fuera de las zonas y sobreancho pleno medido frente al pedido (m).</summary>
        public const double ToleranciaMedida = 0.005;
        /// <summary>Por encima de esto (m) una muestra cuenta como ensanchada.</summary>
        public const double UmbralZona = 0.002;
        /// <summary>Progresivas de la transición medida frente a las previstas (m).</summary>
        public const double ToleranciaTransicion = 0.1;
        /// <summary>Precisión de la búsqueda por bisección de los bordes de cada transición (m).</summary>
        public const double PrecisionBiseccion = 0.005;
        /// <summary>Muestras como mucho por borde: el paso es max(1 m, longitud / 2000).</summary>
        public const int MaximoMuestras = 2000;

        public const string SupuestoTransicion = "Se supone que el rango pedido es el tramo pleno y que la transición queda fuera de él; al crear se mide y se avisa si Civil 3D la puso de otra forma.";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Nombre por defecto de un borde: «Eje 1 - Borde izq» o «Eje 1 - Borde der».</summary>
        public static string NombrePorDefecto(string eje, string borde)
            => (eje ?? "").Trim() + (borde == Izquierdo ? " - Borde izq" : " - Borde der");

        /// <summary>Paso de muestreo para un tramo: 1 m, o más si hiciera falta más de MaximoMuestras muestras.</summary>
        public static double PasoMuestreo(double longitud)
            => Math.Max(1.0, Math.Abs(longitud) / MaximoMuestras);

        /// <summary>Lanza si el desplazamiento (media calzada) no es un número positivo.</summary>
        public static void ValidarDesplazamiento(double d)
        {
            if (double.IsNaN(d) || double.IsInfinity(d) || d <= 0)
                throw new ArgumentException("'desplazamiento' debe ser un número mayor que 0: la distancia del eje a cada borde (media calzada), en unidades del dibujo.");
        }

        /// <summary>«izquierdo», «izq», «izquierda», «left», «l» → izquierdo; lo mismo a la derecha; null si no se reconoce.</summary>
        public static string NormalizarBorde(string texto)
        {
            string t = (texto ?? "").Trim().ToLowerInvariant();
            switch (t)
            {
                case "izquierdo": case "izquierda": case "izq": case "i": case "left": case "l": return Izquierdo;
                case "derecho": case "derecha": case "der": case "d": case "right": case "r": return Derecho;
                default: return null;
            }
        }

        // ------------------------------------------------------------------ lectura y validación de los pedidos
        /// <summary>
        /// Lee 'ensanchamientos' (lista JSON de objetos con borde, progresiva_inicial, progresiva_final, sobreancho y
        /// longitud_transicion opcional) y lo valida contra el rango del eje: inicial &lt; final, dentro del eje contando la
        /// transición, sobreancho &gt; 0 y sin solapes en el mismo borde (contando las transiciones pedidas). Lista vacía si
        /// no se pasó. Lanza ArgumentException con un mensaje claro.
        /// </summary>
        public static List<Ensanchamiento> Leer(JsonElement json, double inicioEje, double finEje)
        {
            var lista = new List<Ensanchamiento>();
            if (json.ValueKind == JsonValueKind.Undefined || json.ValueKind == JsonValueKind.Null) return lista;
            if (json.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("'ensanchamientos' debe ser una lista JSON: [{\"borde\": \"izquierdo\", \"progresiva_inicial\": 220, \"progresiva_final\": 345.66, \"sobreancho\": 0.6, \"longitud_transicion\": 40}].");
            int n = 0;
            foreach (var e in json.EnumerateArray())
            {
                n++;
                string donde = "El ensanchamiento " + n;
                if (e.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException(donde + " no es un objeto JSON con borde, progresiva_inicial, progresiva_final y sobreancho: " + e.GetRawText());
                string textoBorde = e.TryGetProperty("borde", out var jb) && jb.ValueKind == JsonValueKind.String ? jb.GetString() : null;
                string borde = NormalizarBorde(textoBorde);
                if (borde == null)
                    throw new ArgumentException(donde + " tiene 'borde' = " + (textoBorde == null ? "(falta)" : "'" + textoBorde + "'") + ": usa 'izquierdo' o 'derecho' (mirando en el sentido de avance del eje).");
                var x = new Ensanchamiento
                {
                    Numero = n,
                    Borde = borde,
                    ProgresivaInicial = Numero(e, "progresiva_inicial", donde, true).Value,
                    ProgresivaFinal = Numero(e, "progresiva_final", donde, true).Value,
                    Sobreancho = Numero(e, "sobreancho", donde, true).Value,
                    LongitudTransicion = Numero(e, "longitud_transicion", donde, false)
                };
                if (x.Sobreancho <= 0)
                    throw new ArgumentException(donde + " tiene 'sobreancho' = " + Texto(x.Sobreancho) + ": debe ser mayor que 0 (lo que se añade a la media calzada, en m).");
                if (x.LongitudTransicion.HasValue && x.LongitudTransicion.Value < 0)
                    throw new ArgumentException(donde + " tiene 'longitud_transicion' negativa: debe ser 0 o más (m), o no pasarla para que la decida Civil 3D.");
                if (x.ProgresivaFinal <= x.ProgresivaInicial)
                    throw new ArgumentException(donde + " va de " + Texto(x.ProgresivaInicial) + " a " + Texto(x.ProgresivaFinal) + ": la progresiva final debe ser mayor que la inicial.");
                if (x.InicioOcupado < inicioEje - ToleranciaNominal || x.FinOcupado > finEje + ToleranciaNominal)
                    throw new ArgumentException(donde + " ocupa de " + Texto(x.InicioOcupado) + " a " + Texto(x.FinOcupado)
                        + (x.LongitudTransicion.HasValue ? " contando la transición de " + Texto(x.LongitudTransicion.Value) + " m" : "")
                        + " y el eje va de " + Texto(inicioEje) + " a " + Texto(finEje) + ". Acorta el tramo o la transición (ver listar_pis).");
                lista.Add(x);
                if (lista.Count > MaximoEnsanchamientos)
                    throw new ArgumentException("'ensanchamientos' tiene más de " + MaximoEnsanchamientos + " elementos.");
            }
            foreach (var grupo in lista.GroupBy(x => x.Borde))
            {
                var orden = grupo.OrderBy(x => x.InicioOcupado).ToList();
                for (int i = 1; i < orden.Count; i++)
                    if (orden[i].InicioOcupado < orden[i - 1].FinOcupado - ToleranciaNominal)
                        throw new ArgumentException("Los ensanchamientos " + orden[i - 1].Numero + " y " + orden[i].Numero + " del borde " + grupo.Key
                            + " se solapan (contando las transiciones): " + Texto(orden[i - 1].InicioOcupado) + "–" + Texto(orden[i - 1].FinOcupado)
                            + " y " + Texto(orden[i].InicioOcupado) + "–" + Texto(orden[i].FinOcupado) + ". Únelos en uno o sepáralos.");
            }
            return lista;
        }

        private static double? Numero(JsonElement e, string nombre, string donde, bool obligatorio)
        {
            if (!e.TryGetProperty(nombre, out var v) || v.ValueKind == JsonValueKind.Null)
            {
                if (obligatorio) throw new ArgumentException(donde + " no tiene '" + nombre + "' (número).");
                return null;
            }
            double r;
            if (v.ValueKind == JsonValueKind.Number) r = v.GetDouble();
            else if (v.ValueKind == JsonValueKind.String && double.TryParse((v.GetString() ?? "").Replace(',', '.'), NumberStyles.Float, Inv, out double d)) r = d;
            else throw new ArgumentException(donde + " tiene '" + nombre + "' que no es un número: " + v.GetRawText());
            if (double.IsNaN(r) || double.IsInfinity(r)) throw new ArgumentException(donde + " tiene '" + nombre + "' que no es un número finito.");
            return r;
        }

        private static string Texto(double v) => Math.Round(v, 4).ToString("0.####", Inv);

        /// <summary>Progresivas y longitudes medidas: la bisección las da con ±0,005 m, así que se escriben con 2 decimales.</summary>
        private static string Medido(double v) => Math.Round(v, 2).ToString("0.##", Inv);

        /// <summary>
        /// Desplazamiento total que se pasa a AddWidening: el nominal más el sobreancho, con el signo del NominalOffset
        /// releído del borde (el catálogo no documenta el signo; la validación lo decide). Lanza si el nominal es 0.
        /// </summary>
        public static double DesplazamientoTotal(double nominalReleido, double sobreancho)
        {
            if (Math.Abs(nominalReleido) < 1e-9)
                throw new InvalidOperationException("El borde se creó con desplazamiento nominal 0: no se puede calcular el desplazamiento ensanchado.");
            return Math.Sign(nominalReleido) * (Math.Abs(nominalReleido) + sobreancho);
        }

        // ------------------------------------------------------------------ medición del borde
        /// <summary>
        /// Mide un borde entre dos progresivas del eje: 'medir' devuelve el desplazamiento con signo del borde en una
        /// progresiva del eje (null si no se pudo). Muestra con el paso dado, separa las zonas cuyo desplazamiento pasa
        /// del nominal en más de UmbralZona y, en cada una, estima las transiciones buscando por bisección dónde el
        /// sobreancho cruza el 10 % y el 90 % del máximo.
        /// </summary>
        public static PerfilBorde Medir(Func<double, double?> medir, double inicio, double fin, double paso, double nominalEsperado)
        {
            if (paso <= 0) paso = 1.0;
            var perfil = new PerfilBorde { Inicio = inicio, Fin = fin, Paso = paso };
            var estaciones = new List<double>();
            for (int i = 0; ; i++)
            {
                double s = inicio + i * paso;
                if (s > fin - 1e-9) { estaciones.Add(fin); break; }
                estaciones.Add(s);
            }
            foreach (double s in estaciones)
            {
                perfil.Muestras++;
                double? off = Seguro(medir, s);
                if (!off.HasValue) { perfil.MuestrasFallidas++; continue; }
                perfil.Puntos.Add(new[] { s, off.Value });
            }
            if (perfil.Puntos.Count == 0) return perfil;

            var signos = perfil.Puntos.Select(p => Math.Sign(p[1])).Where(x => x != 0).ToList();
            perfil.Signo = signos.Count == 0 ? 0 : Math.Sign(signos.Sum());
            double nominal = Math.Abs(nominalEsperado);
            var fuera = perfil.Puntos.Select(p => Math.Abs(p[1])).Where(a => Math.Abs(a - nominal) <= 0.05).OrderBy(a => a).ToList();
            perfil.NominalMedido = fuera.Count == 0 ? (double?)null : fuera[fuera.Count / 2];
            double referencia = perfil.NominalMedido ?? nominal;
            Func<double, double?> exceso = s => { double? o = Seguro(medir, s); return o.HasValue ? Math.Abs(o.Value) - referencia : (double?)null; };

            // Rachas de muestras ensanchadas (índices en perfil.Puntos)
            var pts = perfil.Puntos;
            int k = 0;
            while (k < pts.Count)
            {
                if (Math.Abs(pts[k][1]) - referencia <= UmbralZona) { k++; continue; }
                int a = k;
                while (k < pts.Count && Math.Abs(pts[k][1]) - referencia > UmbralZona) k++;
                int b = k - 1;
                perfil.Zonas.Add(Zona(pts, a, b, referencia, exceso));
            }
            return perfil;
        }

        private static double? Seguro(Func<double, double?> f, double s)
        {
            try
            {
                double? v = f(s);
                return v.HasValue && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value) ? v : null;
            }
            catch { return null; }
        }

        private static ZonaMedida Zona(List<double[]> pts, int a, int b, double referencia, Func<double, double?> exceso)
        {
            Func<int, double> e = i => Math.Abs(pts[i][1]) - referencia;
            var z = new ZonaMedida { CortadaAlInicio = a == 0, CortadaAlFinal = b == pts.Count - 1 };
            double max = 0;
            for (int i = a; i <= b; i++) max = Math.Max(max, e(i));
            z.SobreanchoMaximo = max;

            // Subida: entre la última muestra no ensanchada (a-1) y la primera que llega al 90 %
            int primera90 = a;
            while (primera90 < b && e(primera90) < 0.9 * max) primera90++;
            double desde = a > 0 ? pts[a - 1][0] : pts[a][0];
            double s10 = Cruce(exceso, desde, pts[primera90][0], 0.1 * max, true);
            double s90 = Cruce(exceso, desde, pts[primera90][0], 0.9 * max, true);
            double rampa = Math.Max(0, s90 - s10) / 8.0;   // de 10-90 % a 0-100 % en una rampa lineal
            z.InicioTransicion = z.CortadaAlInicio ? pts[a][0] : s10 - rampa;
            z.InicioPleno = s90 + rampa;

            // Bajada: entre la última muestra al 90 % y la primera no ensanchada (b+1)
            int ultima90 = b;
            while (ultima90 > a && e(ultima90) < 0.9 * max) ultima90--;
            double hasta = b < pts.Count - 1 ? pts[b + 1][0] : pts[b][0];
            double t90 = Cruce(exceso, pts[ultima90][0], hasta, 0.9 * max, false);
            double t10 = Cruce(exceso, pts[ultima90][0], hasta, 0.1 * max, false);
            rampa = Math.Max(0, t10 - t90) / 8.0;
            z.FinPleno = t90 - rampa;
            z.FinTransicion = z.CortadaAlFinal ? pts[b][0] : t10 + rampa;
            if (z.InicioPleno > z.FinPleno) { double m = (z.InicioPleno + z.FinPleno) / 2; z.InicioPleno = m; z.FinPleno = m; }

            var plenas = new List<double>();
            for (int i = a; i <= b; i++) if (pts[i][0] >= z.InicioPleno - 1e-9 && pts[i][0] <= z.FinPleno + 1e-9) plenas.Add(e(i));
            z.SobreanchoMedio = plenas.Count > 0 ? plenas.Average() : max;
            z.PuntosEntrada = Perfil(exceso, z.InicioTransicion, z.InicioPleno);
            z.PuntosSalida = Perfil(exceso, z.FinPleno, z.FinTransicion);
            return z;
        }

        /// <summary>
        /// Progresiva donde el exceso cruza 'nivel' entre lo y hi, por bisección. 'subiendo': en lo está por debajo y en hi
        /// por encima; si no, al revés. Si una medida falla, se queda con lo que tiene.
        /// </summary>
        private static double Cruce(Func<double, double?> exceso, double lo, double hi, double nivel, bool subiendo)
        {
            if (hi <= lo) return lo;
            for (int i = 0; i < 60 && hi - lo > PrecisionBiseccion; i++)
            {
                double m = (lo + hi) / 2;
                double? v = exceso(m);
                if (!v.HasValue) break;
                bool encima = v.Value >= nivel;
                if (encima == subiendo) hi = m; else lo = m;
            }
            return (lo + hi) / 2;
        }

        private static List<double[]> Perfil(Func<double, double?> exceso, double desde, double hasta)
        {
            var l = new List<double[]>();
            if (hasta - desde < 1e-6) return l;
            foreach (double f in new[] { 0, 0.25, 0.5, 0.75, 1.0 })
            {
                double s = desde + f * (hasta - desde);
                double? v = exceso(s);
                if (v.HasValue) l.Add(new[] { s, v.Value });
            }
            return l;
        }

        // ------------------------------------------------------------------ comparación
        /// <summary>
        /// Compara un ensanchamiento pedido con lo medido: error si el mayor sobreancho medido dentro del rango pedido no
        /// es el pedido (± ToleranciaMedida); aviso si la transición no es la pedida o no se encuentra la zona. Dice si
        /// Civil 3D dejó el tramo pleno en el rango pedido ("fuera": transiciones fuera) o las transiciones dentro de él.
        /// </summary>
        public static ComparacionEnsanchamiento Comparar(Ensanchamiento pedido, PerfilBorde perfil, double nominal, string nombreBorde)
        {
            var c = new ComparacionEnsanchamiento();
            string cual = "El ensanchamiento " + pedido.Numero + " del borde '" + nombreBorde + "' (" + Texto(pedido.ProgresivaInicial) + "–" + Texto(pedido.ProgresivaFinal) + ")";
            c.SobreanchoMedido = perfil.SobreanchoMaximoEntre(pedido.ProgresivaInicial, pedido.ProgresivaFinal, perfil.NominalMedido ?? nominal);
            if (!c.SobreanchoMedido.HasValue)
            {
                c.Error = cual + " no se pudo medir: ninguna muestra del borde dentro del tramo.";
                return c;
            }
            if (Math.Abs(c.SobreanchoMedido.Value - pedido.Sobreancho) > ToleranciaMedida)
            {
                c.Error = cual + " quedó con un sobreancho medido de " + Texto(c.SobreanchoMedido.Value) + " m y se pidió " + Texto(pedido.Sobreancho)
                    + " m (desplazamiento total pasado a AddWidening: " + Texto(Math.Abs(nominal) + pedido.Sobreancho) + " m)."
                    + (Math.Abs(c.SobreanchoMedido.Value - (Math.Abs(nominal) + pedido.Sobreancho)) <= ToleranciaMedida
                        ? " Parece que AddWidening suma el valor al nominal en vez de tomarlo como total."
                        : c.SobreanchoMedido.Value <= ToleranciaMedida ? " El borde no se ensanchó en ese tramo." : "");
                return c;
            }
            c.Zona = perfil.Zonas
                .Where(z => z.FinTransicion >= pedido.ProgresivaInicial && z.InicioTransicion <= pedido.ProgresivaFinal)
                .OrderByDescending(z => Math.Min(z.FinTransicion, pedido.ProgresivaFinal) - Math.Max(z.InicioTransicion, pedido.ProgresivaInicial))
                .FirstOrDefault();
            if (c.Zona == null)
            {
                c.Aviso = cual + ": el sobreancho medido es el pedido pero no se pudo delimitar la zona ensanchada.";
                return c;
            }
            var z0 = c.Zona;
            bool plenoEnRango = Math.Abs(z0.InicioPleno - pedido.ProgresivaInicial) <= ToleranciaTransicion && Math.Abs(z0.FinPleno - pedido.ProgresivaFinal) <= ToleranciaTransicion;
            bool transicionEnRango = Math.Abs(z0.InicioTransicion - pedido.ProgresivaInicial) <= ToleranciaTransicion && Math.Abs(z0.FinTransicion - pedido.ProgresivaFinal) <= ToleranciaTransicion;
            c.Transicion = plenoEnRango ? "fuera" : transicionEnRango ? "dentro" : "otra";
            var avisos = new List<string>();
            if (c.Transicion != "fuera")
                avisos.Add(cual + ": " + ExplicarTransicion(c.Transicion, z0) + " La previsión suponía el tramo pleno en el rango pedido y las transiciones fuera.");
            if (pedido.LongitudTransicion.HasValue && !z0.CortadaAlInicio && !z0.CortadaAlFinal
                && (Math.Abs(z0.LongitudEntrada - pedido.LongitudTransicion.Value) > ToleranciaTransicion || Math.Abs(z0.LongitudSalida - pedido.LongitudTransicion.Value) > ToleranciaTransicion))
                avisos.Add(cual + ": se pidió una transición de " + Texto(pedido.LongitudTransicion.Value) + " m y se midió " + Medido(z0.LongitudEntrada) + " m a la entrada y " + Medido(z0.LongitudSalida) + " m a la salida.");
            if (avisos.Count > 0) c.Aviso = string.Join(" ", avisos);
            return c;
        }

        /// <summary>Frase sobre dónde quedó la transición respecto al rango pedido.</summary>
        public static string ExplicarTransicion(string tipo, ZonaMedida z)
        {
            string tramos = "tramo pleno de " + Medido(z.InicioPleno) + " a " + Medido(z.FinPleno) + ", entrada de " + Medido(z.LongitudEntrada) + " m (desde " + Medido(z.InicioTransicion) + ") y salida de " + Medido(z.LongitudSalida) + " m (hasta " + Medido(z.FinTransicion) + ")";
            switch (tipo)
            {
                case "fuera": return "Civil 3D dejó el sobreancho pleno en el rango pedido y las transiciones fuera de él: " + tramos + ".";
                case "dentro": return "Civil 3D puso las transiciones dentro del rango pedido: " + tramos + ".";
                default: return "La zona medida no coincide con el rango pedido ni con el rango más las transiciones: " + tramos + ".";
            }
        }
    }
}
