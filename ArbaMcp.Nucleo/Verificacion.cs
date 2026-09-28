using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Verificación antes/después de las escrituras y construcción de sus respuestas (mismo formato que en 1.2.x:
    /// simulado, herramienta, accion/mensaje, cambios, antes, despues, copia, datos, avisos).
    /// </summary>
    public static class Verificacion
    {
        /// <summary>Respuesta de una simulación: el estado actual y el estado que tendría después.</summary>
        public static object Simulacion(string herramienta, IDictionary<string, object> antes, IDictionary<string, object> esperado, string accion, IList<string> avisos = null)
        {
            var despues = new Dictionary<string, object>(antes);
            foreach (var kv in esperado) despues[kv.Key] = kv.Value;
            var cambios = Cambios(antes, despues);
            return new
            {
                simulado = true,
                herramienta,
                accion,
                cambios,
                antes = Filtrar(antes, cambios),
                despues = Filtrar(despues, cambios),
                avisos = avisos != null && avisos.Count > 0 ? avisos : null
            };
        }

        /// <summary>
        /// Respuesta de una escritura real. Comprueba que 'despues' (leído del dibujo tras el cambio) coincide con
        /// 'esperado'; si no, lanza InvalidOperationException con antes, despues y copia, y no se reintenta.
        /// </summary>
        public static object Resultado(string herramienta, IDictionary<string, object> antes, IDictionary<string, object> despues, IDictionary<string, object> esperado,
            string mensaje = null, object datos = null, object copia = null, IList<string> avisos = null)
        {
            ComprobarEsperado(antes, despues, esperado, copia, null);
            var cambios = Cambios(antes, despues);
            return new
            {
                simulado = false,
                herramienta,
                mensaje,
                cambios,
                antes = Filtrar(antes, cambios),
                despues = Filtrar(despues, cambios),
                copia,
                datos,
                avisos = avisos != null && avisos.Count > 0 ? avisos : null
            };
        }

        /// <summary>Lanza si algún campo de 'esperado' no coincide con 'despues'. 'prefijo' se antepone al nombre del campo en el mensaje (lotes).</summary>
        public static void ComprobarEsperado(IDictionary<string, object> antes, IDictionary<string, object> despues, IDictionary<string, object> esperado, object copia, string prefijo)
        {
            foreach (var kv in esperado)
            {
                despues.TryGetValue(kv.Key, out object real);
                if (!Igual(real, kv.Value))
                    throw new InvalidOperationException(MensajeNoRefleja(prefijo + kv.Key, kv.Value, real, antes, despues, copia));
            }
        }

        public static string MensajeNoRefleja(string campo, object pedido, object real, IDictionary<string, object> antes, IDictionary<string, object> despues, object copia)
        {
            string rutaCopia = RutaDeCopia(copia);
            return "El dibujo no refleja el cambio pedido en '" + campo + "': se pidió " + Texto(pedido) + " y después de escribir tiene " + Texto(real)
                + ". No se reintenta. antes=" + Json.Serializar(antes) + " despues=" + Json.Serializar(despues)
                + (rutaCopia != null ? " copia=" + rutaCopia : "");
        }

        /// <summary>Ruta de la copia de seguridad, venga como texto o como objeto con 'ruta'/'Ruta'.</summary>
        public static string RutaDeCopia(object copia)
        {
            if (copia == null) return null;
            if (copia is string s) return s;
            return Api.Leer<string>(copia, null, "Ruta", "ruta");
        }

        public static List<string> Cambios(IDictionary<string, object> antes, IDictionary<string, object> despues)
        {
            var claves = antes.Keys.Union(despues.Keys).ToList();
            var cambios = new List<string>();
            foreach (var k in claves)
            {
                antes.TryGetValue(k, out object a);
                despues.TryGetValue(k, out object d);
                if (!Igual(a, d)) cambios.Add(k);
            }
            return cambios;
        }

        /// <summary>Deja solo los campos que cambiaron; si no cambió nada, devuelve todo (para ver el estado).</summary>
        public static IDictionary<string, object> Filtrar(IDictionary<string, object> d, List<string> cambios)
        {
            if (cambios.Count == 0) return d;
            var r = new Dictionary<string, object>();
            foreach (var k in cambios) if (d.TryGetValue(k, out object v)) r[k] = v;
            return r;
        }

        /// <summary>
        /// Igualdad tolerante: números con 1e-6 de margen (aunque sean de tipos distintos), textos sin espacios en los
        /// extremos ni distinción de mayúsculas, booleanos, y listas elemento a elemento.
        /// </summary>
        public static bool Igual(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (EsNumero(a) && EsNumero(b)) return Math.Abs(Convert.ToDouble(a, CultureInfo.InvariantCulture) - Convert.ToDouble(b, CultureInfo.InvariantCulture)) < 1e-6;
            if (a is string sa && b is string sb) return string.Equals(sa.Trim(), sb.Trim(), StringComparison.OrdinalIgnoreCase);
            if (a is bool ba && b is bool bb) return ba == bb;
            if (a is IEnumerable ea && b is IEnumerable eb && !(a is string) && !(b is string))
            {
                var la = ea.Cast<object>().ToList(); var lb = eb.Cast<object>().ToList();
                if (la.Count != lb.Count) return false;
                for (int i = 0; i < la.Count; i++) if (!Igual(la[i], lb[i])) return false;
                return true;
            }
            return a.Equals(b);
        }

        public static bool EsNumero(object o) => o is double || o is float || o is int || o is long || o is short || o is decimal || o is byte;

        public static string Texto(object o)
            => o == null ? "null"
             : o is string s ? "'" + s + "'"
             : o is IEnumerable e && !(o is string) ? "[" + string.Join(", ", e.Cast<object>().Select(Texto)) + "]"
             : Convert.ToString(o, CultureInfo.InvariantCulture);
    }
}
