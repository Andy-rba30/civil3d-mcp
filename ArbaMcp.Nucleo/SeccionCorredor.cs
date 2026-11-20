using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Un punto calculado de un subensamblaje aplicado (seccion_corredor, tarea 7.3), tal como lo lee el plugin:
    /// CalculatedPoint.StationOffsetElevationToBaseline (X = progresiva, Y = desplazamiento, Z = cota: **por verificar**)
    /// y CalculatedPoint.CorridorCodes. Un valor que no se pudo leer queda en null.
    /// </summary>
    public sealed class PuntoSeccion
    {
        public double? Progresiva, Desplazamiento, Cota;
        public List<string> Codigos = new List<string>();

        public bool TieneCodigo(string codigo) => Codigos != null && Codigos.Any(c => string.Equals(c, codigo, StringComparison.OrdinalIgnoreCase));

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["codigos"] = Codigos ?? new List<string>(),
            ["desplazamiento"] = SeccionCorredor.R(Desplazamiento),
            ["cota"] = SeccionCorredor.R(Cota),
            ["progresiva"] = SeccionCorredor.R(Progresiva)
        };
    }

    /// <summary>Un parámetro aplicado de un subensamblaje (IAppliedSubassemblyParam): clave interna, nombre visible, valor, valor de diseño y si se cambió a mano.</summary>
    public sealed class ParametroAplicado
    {
        public string Clave, Nombre;
        public object Valor, ValorDiseno;
        public bool? Modificado;

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["clave"] = Clave,
            ["nombre"] = Nombre,
            ["valor"] = SeccionCorredor.Normalizar(Valor),
            ["valor_diseno"] = SeccionCorredor.Normalizar(ValorDiseno),
            ["modificado"] = Modificado
        };
    }

    /// <summary>Tramo entre dos puntos consecutivos del perfil superior de un subensamblaje, ordenados por desplazamiento.</summary>
    public sealed class TramoSeccion
    {
        public PuntoSeccion Desde, Hasta;
        /// <summary>"izquierda", "derecha" o "eje", según el desplazamiento medio del tramo.</summary>
        public string Lado;
        /// <summary>Pendiente en % al alejarse del eje (negativa si baja); null si el tramo es vertical.</summary>
        public double? PendientePct;
        public double Ancho, DiferenciaCota;

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["desde_desplazamiento"] = SeccionCorredor.R(Desde.Desplazamiento),
            ["hasta_desplazamiento"] = SeccionCorredor.R(Hasta.Desplazamiento),
            ["desde_codigos"] = Desde.Codigos ?? new List<string>(),
            ["hasta_codigos"] = Hasta.Codigos ?? new List<string>(),
            ["lado"] = Lado,
            ["ancho"] = SeccionCorredor.R(Ancho),
            ["diferencia_cota"] = SeccionCorredor.R(DiferenciaCota),
            ["pendiente_pct"] = SeccionCorredor.R(PendientePct),
            ["vertical"] = !PendientePct.HasValue
        };
    }

    /// <summary>Pendiente de un carril: del punto de código Crown al de código ETW del mismo subensamblaje.</summary>
    public sealed class PendienteCarril
    {
        public PuntoSeccion Corona, Borde;
        public string Lado;
        public double? PendientePct;

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["lado"] = Lado,
            ["crown_desplazamiento"] = SeccionCorredor.R(Corona.Desplazamiento),
            ["crown_cota"] = SeccionCorredor.R(Corona.Cota),
            ["etw_desplazamiento"] = SeccionCorredor.R(Borde.Desplazamiento),
            ["etw_cota"] = SeccionCorredor.R(Borde.Cota),
            ["pendiente_pct"] = SeccionCorredor.R(PendientePct)
        };
    }

    /// <summary>Un subensamblaje aplicado en la progresiva: datos del subensamblaje, parámetros aplicados y puntos.</summary>
    public sealed class SubensamblajeSeccion
    {
        public string Nombre, Clase, Lado, Grupo;
        public List<ParametroAplicado> Parametros = new List<ParametroAplicado>();
        public List<PuntoSeccion> Puntos = new List<PuntoSeccion>();

        public List<TramoSeccion> Tramos() => SeccionCorredor.Tramos(Puntos);
        public PendienteCarril Carril() => SeccionCorredor.Carril(Puntos);

        public Dictionary<string, object> ComoDiccionario()
        {
            var carril = Carril();
            return new Dictionary<string, object>
            {
                ["nombre"] = Nombre,
                ["clase"] = Clase,
                ["lado"] = Lado,
                ["grupo"] = Grupo,
                ["parametros"] = Parametros.Select(p => (object)p.ComoDiccionario()).ToList(),
                ["puntos"] = SeccionCorredor.OrdenarPorDesplazamiento(Puntos).Select(p => (object)p.ComoDiccionario()).ToList(),
                ["tramos"] = Tramos().Select(t => (object)t.ComoDiccionario()).ToList(),
                ["pendiente_carril"] = carril?.ComoDiccionario()
            };
        }
    }

    /// <summary>La sección aplicada de una línea base en una progresiva.</summary>
    public sealed class LineaBaseSeccion
    {
        public string Nombre, Alineamiento, Perfil, Region, Ensamblaje;
        public double? Inicio, Fin;
        /// <summary>La progresiva con la que se leyó la sección (la pedida o, si en esa no hay sección calculada, la más cercana del corredor).</summary>
        public double? ProgresivaUsada;
        /// <summary>Las progresivas calculadas del corredor (Baseline.SortedStations) más cercanas por debajo y por encima de la pedida.</summary>
        public double? EstacionAnterior, EstacionSiguiente;
        public List<SubensamblajeSeccion> Subensamblajes = new List<SubensamblajeSeccion>();
        public List<string> Avisos = new List<string>();

        public Dictionary<string, object> ComoDiccionario()
        {
            var avisos = new List<string>(Avisos);
            string ejes = SeccionCorredor.AvisoEjes(this);
            if (ejes != null) avisos.Add(ejes);
            var carriles = new List<object>();
            foreach (var s in Subensamblajes)
            {
                var c = s.Carril();
                if (c == null) continue;
                var d = c.ComoDiccionario();
                d["subensamblaje"] = s.Nombre;
                carriles.Add(d);
            }
            return new Dictionary<string, object>
            {
                ["nombre"] = Nombre,
                ["alineamiento"] = Alineamiento,
                ["perfil"] = Perfil,
                ["inicio"] = SeccionCorredor.R(Inicio),
                ["fin"] = SeccionCorredor.R(Fin),
                ["region"] = Region,
                ["ensamblaje"] = Ensamblaje,
                ["progresiva_usada"] = SeccionCorredor.R(ProgresivaUsada),
                ["progresiva_usada_texto"] = ProgresivaUsada.HasValue ? TablaPi.Progresiva(ProgresivaUsada.Value) : null,
                ["estacion_anterior"] = SeccionCorredor.R(EstacionAnterior),
                ["estacion_siguiente"] = SeccionCorredor.R(EstacionSiguiente),
                ["numero_subensamblajes"] = Subensamblajes.Count,
                ["carriles"] = carriles,
                ["subensamblajes"] = Subensamblajes.Select(s => (object)s.ComoDiccionario()).ToList(),
                ["avisos"] = avisos
            };
        }
    }

    /// <summary>La respuesta de seccion_corredor: el corredor, la progresiva pedida y una sección por línea base.</summary>
    public sealed class ResultadoSeccionCorredor
    {
        public string Corredor;
        public double ProgresivaPedida;
        public bool? EstaDesactualizado;
        public List<LineaBaseSeccion> Lineas = new List<LineaBaseSeccion>();
        public List<string> Avisos = new List<string>();

        public Dictionary<string, object> ComoDiccionario()
        {
            var avisos = new List<string>(Avisos);
            if (EstaDesactualizado == true)
                avisos.Add("El corredor '" + Corredor + "' está desactualizado: la sección es la de la última reconstrucción. Reconstrúyelo (reconstruir_corredor) para ver la actual.");
            return new Dictionary<string, object>
            {
                ["corredor"] = Corredor,
                ["progresiva"] = SeccionCorredor.R(ProgresivaPedida),
                ["progresiva_texto"] = TablaPi.Progresiva(ProgresivaPedida),
                ["esta_desactualizado"] = EstaDesactualizado,
                ["convencion"] = SeccionCorredor.Convencion,
                ["lineas_base"] = Lineas.Select(l => (object)l.ComoDiccionario()).ToList(),
                ["avisos"] = avisos
            };
        }
    }

    /// <summary>
    /// seccion_corredor (tarea 7.3): la sección aplicada de un corredor en una progresiva, sin Civil 3D. Ordena los puntos
    /// por desplazamiento, calcula la pendiente de cada tramo y la de cada carril (Crown → ETW), elige las líneas base
    /// cuyo rango incluye la progresiva y redacta los errores de rango.
    /// </summary>
    public static class SeccionCorredor
    {
        public const string CodigoCorona = "Crown";
        public const string CodigoBorde = "ETW";
        /// <summary>Dos puntos con desplazamientos a menos de esto (m) están en la misma vertical.</summary>
        public const double ToleranciaDesplazamiento = 0.001;
        /// <summary>Margen (m) para decidir si una progresiva está dentro del rango de una línea base o es una progresiva calculada.</summary>
        public const double ToleranciaProgresiva = 1e-4;
        /// <summary>Diferencia (m) entre la X de los puntos y la progresiva usada a partir de la cual se avisa.</summary>
        public const double ToleranciaEjes = 0.01;

        public const string Convencion = "Desplazamiento negativo a la izquierda del eje. Pendientes en %, con signo al alejarse del eje: negativa si baja. "
            + "Los puntos salen de CalculatedPoint.StationOffsetElevationToBaseline, leído como X = progresiva, Y = desplazamiento, Z = cota (por verificar). "
            + "Los tramos unen, en orden de desplazamiento, el punto más alto de cada vertical del subensamblaje (perfil superior; las capas de debajo no forman tramos).";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        internal static double? R(double? v) => v.HasValue ? Argumentos.Redondear(v.Value) : null;

        /// <summary>"izquierda" si el desplazamiento es negativo, "derecha" si es positivo y "eje" si es 0 (a menos de la tolerancia).</summary>
        public static string Lado(double desplazamiento)
            => desplazamiento < -ToleranciaDesplazamiento / 2 ? "izquierda" : desplazamiento > ToleranciaDesplazamiento / 2 ? "derecha" : "eje";

        /// <summary>
        /// Pendiente en % entre dos puntos, con signo al alejarse del eje: negativa si baja al alejarse. El sentido "alejarse"
        /// lo da el desplazamiento medio del tramo (a la derecha, hacia desplazamientos mayores; a la izquierda, hacia menores;
        /// un tramo centrado en el eje se mide de izquierda a derecha). Null si el tramo es vertical.
        /// </summary>
        public static double? PendienteAlejandose(double x1, double z1, double x2, double z2)
        {
            double dx = x2 - x1;
            if (Math.Abs(dx) < ToleranciaDesplazamiento) return null;
            double izqDer = (z2 - z1) / dx * 100.0;   // de izquierda a derecha si x2 > x1; el cociente no depende del orden
            double medio = (x1 + x2) / 2;
            return Lado(medio) == "izquierda" ? -izqDer : izqDer;
        }

        /// <summary>Puntos con desplazamiento y cota, ordenados por desplazamiento y, en la misma vertical, de arriba abajo; los incompletos, al final.</summary>
        public static List<PuntoSeccion> OrdenarPorDesplazamiento(IEnumerable<PuntoSeccion> puntos)
        {
            var lista = (puntos ?? Enumerable.Empty<PuntoSeccion>()).Where(p => p != null).ToList();
            var completos = lista.Where(Completo).OrderBy(p => p.Desplazamiento.Value).ThenByDescending(p => p.Cota.Value).ToList();
            completos.AddRange(lista.Where(p => !Completo(p)));
            return completos;
        }

        private static bool Completo(PuntoSeccion p) => p.Desplazamiento.HasValue && p.Cota.HasValue
            && !double.IsNaN(p.Desplazamiento.Value) && !double.IsNaN(p.Cota.Value)
            && !double.IsInfinity(p.Desplazamiento.Value) && !double.IsInfinity(p.Cota.Value);

        /// <summary>
        /// Perfil superior del subensamblaje: los puntos ordenados por desplazamiento y, en cada vertical (desplazamientos a menos
        /// de ToleranciaDesplazamiento), solo el más alto. Así las capas del pavimento, que repiten los desplazamientos debajo
        /// de la superficie, no dan pendientes sin sentido entre la superficie y el fondo.
        /// </summary>
        public static List<PuntoSeccion> PerfilSuperior(IEnumerable<PuntoSeccion> puntos)
        {
            var orden = OrdenarPorDesplazamiento(puntos).Where(Completo).ToList();
            var perfil = new List<PuntoSeccion>();
            int i = 0;
            while (i < orden.Count)
            {
                double x0 = orden[i].Desplazamiento.Value;
                var grupo = new List<PuntoSeccion>();
                while (i < orden.Count && Math.Abs(orden[i].Desplazamiento.Value - x0) < ToleranciaDesplazamiento) grupo.Add(orden[i++]);
                perfil.Add(grupo.OrderByDescending(p => p.Cota.Value).First());
            }
            return perfil;
        }

        /// <summary>Tramos entre puntos consecutivos del perfil superior, con su pendiente al alejarse del eje.</summary>
        public static List<TramoSeccion> Tramos(IEnumerable<PuntoSeccion> puntos)
        {
            var perfil = PerfilSuperior(puntos);
            var tramos = new List<TramoSeccion>();
            for (int k = 1; k < perfil.Count; k++)
            {
                var a = perfil[k - 1];
                var b = perfil[k];
                double xa = a.Desplazamiento.Value, xb = b.Desplazamiento.Value;
                tramos.Add(new TramoSeccion
                {
                    Desde = a,
                    Hasta = b,
                    Lado = Lado((xa + xb) / 2),
                    Ancho = Math.Abs(xb - xa),
                    DiferenciaCota = b.Cota.Value - a.Cota.Value,
                    PendientePct = PendienteAlejandose(xa, a.Cota.Value, xb, b.Cota.Value)
                });
            }
            return tramos;
        }

        /// <summary>
        /// Pendiente del carril: del punto de código Crown (el más cercano al eje si hay varios) al de código ETW (el más
        /// lejano si hay varios), con signo al alejarse del eje. Null si falta alguno de los dos o están en la misma vertical.
        /// </summary>
        public static PendienteCarril Carril(IEnumerable<PuntoSeccion> puntos)
        {
            var completos = (puntos ?? Enumerable.Empty<PuntoSeccion>()).Where(p => p != null && Completo(p)).ToList();
            var corona = completos.Where(p => p.TieneCodigo(CodigoCorona))
                .OrderBy(p => Math.Abs(p.Desplazamiento.Value)).ThenByDescending(p => p.Cota.Value).FirstOrDefault();
            var borde = completos.Where(p => p.TieneCodigo(CodigoBorde))
                .OrderByDescending(p => Math.Abs(p.Desplazamiento.Value)).ThenByDescending(p => p.Cota.Value).FirstOrDefault();
            if (corona == null || borde == null) return null;
            double xc = corona.Desplazamiento.Value, xb = borde.Desplazamiento.Value;
            var pendiente = PendienteAlejandose(xc, corona.Cota.Value, xb, borde.Cota.Value);
            if (!pendiente.HasValue) return null;
            return new PendienteCarril { Corona = corona, Borde = borde, Lado = Lado((xc + xb) / 2), PendientePct = pendiente };
        }

        /// <summary>Valor de un parámetro listo para JSON: números reales redondeados a 4 decimales, enteros, textos y booleanos tal cual, el resto como texto.</summary>
        public static object Normalizar(object v)
        {
            switch (v)
            {
                case null: return null;
                case double d: return Argumentos.Redondear(d);
                case float f: return Argumentos.Redondear(f);
                case decimal m: return Argumentos.Redondear((double)m);
                case bool _:
                case string _:
                case int _:
                case long _:
                case short _:
                case uint _:
                case ushort _:
                case byte _:
                    return v;
                default: return v.ToString();
            }
        }

        /// <summary>Si la progresiva está en el rango [inicio, fin] (con la tolerancia). Un rango desconocido no la contiene.</summary>
        public static bool EnRango(double progresiva, double? inicio, double? fin)
            => inicio.HasValue && fin.HasValue && progresiva >= inicio.Value - ToleranciaProgresiva && progresiva <= fin.Value + ToleranciaProgresiva;

        /// <summary>"Línea base 1: de 0 a 831.3274; Línea base 2: rango no legible".</summary>
        public static string TextoRangos(IEnumerable<(string nombre, double? inicio, double? fin)> rangos)
            => string.Join("; ", (rangos ?? Enumerable.Empty<(string, double?, double?)>()).Select(r =>
                r.nombre + ": " + (r.inicio.HasValue && r.fin.HasValue ? "de " + Numero(r.inicio.Value) + " a " + Numero(r.fin.Value) : "rango no legible")));

        /// <summary>Error de seccion_corredor cuando la progresiva no cae en ninguna línea base (o en la pedida), con el rango de cada una.</summary>
        public static string ErrorFueraDeRango(string corredor, double progresiva, IEnumerable<(string nombre, double? inicio, double? fin)> rangos, string lineaBase = null)
        {
            string donde = string.IsNullOrWhiteSpace(lineaBase)
                ? "ninguna línea base del corredor '" + corredor + "'"
                : "la línea base '" + lineaBase + "' del corredor '" + corredor + "'";
            var lista = (rangos ?? Enumerable.Empty<(string, double?, double?)>()).ToList();
            return "La progresiva " + Numero(progresiva) + " está fuera del rango de " + donde + ". Rangos: "
                + (lista.Count == 0 ? "el corredor no tiene líneas base" : TextoRangos(lista)) + ". Usa listar_corredores.";
        }

        /// <summary>
        /// Las progresivas calculadas del corredor (Baseline.SortedStations) alrededor de la pedida: la anterior (≤), la
        /// siguiente (≥), la más cercana y si la pedida es una de ellas (a menos de ToleranciaProgresiva).
        /// </summary>
        public static (double? anterior, double? siguiente, double? masCercana, bool exacta) EstacionesVecinas(IEnumerable<double> estaciones, double progresiva)
        {
            var lista = (estaciones ?? Enumerable.Empty<double>()).Where(e => !double.IsNaN(e) && !double.IsInfinity(e)).OrderBy(e => e).ToList();
            if (lista.Count == 0) return (null, null, null, false);
            double? anterior = null, siguiente = null;
            foreach (var e in lista)
            {
                if (e <= progresiva + ToleranciaProgresiva) anterior = e;
                if (e >= progresiva - ToleranciaProgresiva && !siguiente.HasValue) siguiente = e;
            }
            double masCercana = lista.OrderBy(e => Math.Abs(e - progresiva)).First();
            bool exacta = Math.Abs(masCercana - progresiva) <= ToleranciaProgresiva;
            return (anterior, siguiente, masCercana, exacta);
        }

        /// <summary>
        /// Aviso si la X de StationOffsetElevationToBaseline de algún punto no coincide con la progresiva usada (más de
        /// ToleranciaEjes): entonces el orden X = progresiva, Y = desplazamiento, Z = cota no es el que se supone. Null si cuadra o no hay con qué comparar.
        /// </summary>
        public static string AvisoEjes(LineaBaseSeccion linea)
        {
            if (linea?.ProgresivaUsada == null) return null;
            double pk = linea.ProgresivaUsada.Value;
            var xs = linea.Subensamblajes.SelectMany(s => s.Puntos).Where(p => p?.Progresiva != null).Select(p => p.Progresiva.Value).ToList();
            if (xs.Count == 0) return null;
            double peor = xs.OrderByDescending(x => Math.Abs(x - pk)).First();
            if (Math.Abs(peor - pk) <= ToleranciaEjes) return null;
            return "La X de StationOffsetElevationToBaseline de algún punto (" + Numero(peor) + ") no coincide con la progresiva usada (" + Numero(pk)
                + "): el orden X = progresiva, Y = desplazamiento, Z = cota puede no ser el supuesto (por verificar).";
        }

        /// <summary>Número con punto decimal y hasta 4 decimales, para los mensajes ("831.3274").</summary>
        public static string Numero(double v) => Math.Round(v, 4).ToString("0.####", Inv);
    }
}
