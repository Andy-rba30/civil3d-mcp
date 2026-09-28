using System;
using System.Collections.Generic;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Agrupa los 401 en el historial: una línea por ruta y minuto, con el recuento, en vez de una por petición (el
    /// puente sondea /tools cada 5 s y, mientras relee el token, generaría docenas de líneas). El primer 401 de cada
    /// (ruta, minuto) se anota en el acto; al cerrarse ese minuto, si hubo más, se anota una línea con el total.
    /// </summary>
    public sealed class Registro401
    {
        private sealed class Cuenta { public string Ruta; public DateTime Minuto; public int Total; }
        private readonly Dictionary<string, Cuenta> _abiertas = new Dictionary<string, Cuenta>(StringComparer.Ordinal);
        private readonly object _cerrojo = new object();

        private static DateTime MinutoDe(DateTime t) => new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Kind);

        /// <summary>Anota un 401. Devuelve las líneas para el historial (normalmente ninguna o una).</summary>
        public List<string> Anotar(string metodo, string ruta, DateTime ahora)
        {
            var lineas = new List<string>();
            string clave = metodo + " " + ruta;
            var minuto = MinutoDe(ahora);
            lock (_cerrojo)
            {
                lineas.AddRange(CerrarAnteriores(minuto));
                if (!_abiertas.TryGetValue(clave, out var c) || c.Minuto != minuto)
                {
                    _abiertas[clave] = new Cuenta { Ruta = clave, Minuto = minuto, Total = 1 };
                    lineas.Add("MCP 401 " + clave + ": token ausente o distinto (los siguientes 401 de esta ruta en este minuto se agrupan)");
                }
                else c.Total++;
            }
            return lineas;
        }

        /// <summary>Cierra los minutos ya pasados: una línea con el recuento por ruta cuando hubo más de un 401.</summary>
        public List<string> Cerrar(DateTime ahora)
        {
            lock (_cerrojo) return CerrarAnteriores(MinutoDe(ahora));
        }

        private List<string> CerrarAnteriores(DateTime minutoActual)
        {
            var lineas = new List<string>();
            foreach (var kv in _abiertas.Where(kv => kv.Value.Minuto < minutoActual).ToList())
            {
                var c = kv.Value;
                if (c.Total > 1) lineas.Add("MCP 401 " + c.Ruta + ": " + c.Total + " peticiones rechazadas en el minuto " + c.Minuto.ToString("HH:mm"));
                _abiertas.Remove(kv.Key);
            }
            return lineas;
        }
    }
}
