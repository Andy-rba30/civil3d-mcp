using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Convierte lo que devuelve un script (return ...) en un árbol de valores simples (texto, números, booleanos, listas y
    /// diccionarios) que se puede serializar a JSON sin sorpresas (desde 1.16.0). Se hace dentro de la transacción del
    /// script, mientras sus objetos siguen abiertos, y antes de descargar su contexto: así el resultado no guarda
    /// ninguna referencia a tipos del script (los tipos anónimos) ni a objetos de AutoCAD ya cerrados. Con límites de
    /// profundidad, de elementos por lista y de largo de texto para no enviar respuestas enormes al agente.
    /// </summary>
    public static class CodigoResultado
    {
        /// <summary>Conversión propia de un valor (el plugin la usa para ObjectId, Point3d, DBObject...). True si lo convirtió.</summary>
        public delegate bool Convertidor(object valor, out object convertido);

        public sealed class Limites
        {
            public int Profundidad = 8;
            public int ElementosPorLista = 1000;
            public int Nodos = 20000;
            public int LargoTexto = 100000;
        }

        public static object Normalizar(object valor, Convertidor convertir = null, IList<string> avisos = null, Limites limites = null)
        {
            var estado = new Estado { Convertir = convertir, Avisos = avisos, L = limites ?? new Limites() };
            return estado.Valor(valor, 0);
        }

        private sealed class Estado
        {
            public Convertidor Convertir;
            public IList<string> Avisos;
            public Limites L;
            private int _nodos;
            private bool _avisoNodos, _avisoLista, _avisoTexto, _avisoProfundidad;
            private readonly HashSet<object> _camino = new HashSet<object>(ReferenceEqualityComparer.Instance);

            private void Avisar(ref bool dado, string texto)
            {
                if (dado) return;
                dado = true;
                Avisos?.Add(texto);
            }

            public object Valor(object v, int profundidad)
            {
                if (v == null) return null;
                if (++_nodos > L.Nodos)
                {
                    Avisar(ref _avisoNodos, "resultado: se recortó; tiene más de " + L.Nodos + " valores.");
                    return null;
                }
                if (Convertir != null && Convertir(v, out object convertido))
                    return ReferenceEquals(convertido, v) ? v.ToString() : Valor(convertido, profundidad + 1);

                switch (v)
                {
                    case string s: return Texto(s);
                    case bool _: return v;
                    case char c: return c.ToString();
                    case byte _: case sbyte _: case short _: case ushort _: case int _: case uint _: case long _: case ulong _: case decimal _:
                        return v;
                    case float f: return float.IsNaN(f) || float.IsInfinity(f) ? null : (object)(double)f;
                    case double d: return double.IsNaN(d) || double.IsInfinity(d) ? null : (object)d;
                    case Enum e: return e.ToString();
                    case DateTime dt: return dt.ToString("o", CultureInfo.InvariantCulture);
                    case DateTimeOffset dto: return dto.ToString("o", CultureInfo.InvariantCulture);
                    case TimeSpan ts: return ts.ToString("c", CultureInfo.InvariantCulture);
                    case Guid g: return g.ToString();
                    case JsonElement je: return je.Clone();
                    case JsonNode jn:
                        using (var doc = JsonDocument.Parse(jn.ToJsonString())) return doc.RootElement.Clone();
                    case Type t: return t.FullName;
                    case Exception ex: return ex.GetType().Name + ": " + ex.Message;
                    case Delegate _: return "(función)";
                }

                if (profundidad >= L.Profundidad)
                {
                    Avisar(ref _avisoProfundidad, "resultado: se recortó; tiene más de " + L.Profundidad + " niveles anidados.");
                    return "(recortado: demasiados niveles)";
                }
                bool referencia = !v.GetType().IsValueType;
                if (referencia && !_camino.Add(v)) return "(ciclo)";
                try
                {
                    if (v is IDictionary dic)
                    {
                        var r = new Dictionary<string, object>();
                        int i = 0;
                        foreach (DictionaryEntry kv in dic)
                        {
                            if (i++ >= L.ElementosPorLista) { Avisar(ref _avisoLista, "resultado: una lista o diccionario se recortó a " + L.ElementosPorLista + " elementos."); break; }
                            r[Convert.ToString(kv.Key, CultureInfo.InvariantCulture) ?? ""] = Valor(kv.Value, profundidad + 1);
                        }
                        return r;
                    }
                    if (v is ITuple tupla)
                    {
                        var r = new List<object>();
                        for (int i = 0; i < tupla.Length; i++) r.Add(Valor(tupla[i], profundidad + 1));
                        return r;
                    }
                    if (v is IEnumerable lista)
                    {
                        var r = new List<object>();
                        foreach (var e in lista)
                        {
                            if (r.Count >= L.ElementosPorLista) { Avisar(ref _avisoLista, "resultado: una lista o diccionario se recortó a " + L.ElementosPorLista + " elementos."); break; }
                            r.Add(Valor(e, profundidad + 1));
                        }
                        return r;
                    }
                    return Objeto(v, profundidad);
                }
                finally
                {
                    if (referencia) _camino.Remove(v);
                }
            }

            private string Texto(string s)
            {
                if (s.Length <= L.LargoTexto) return s;
                Avisar(ref _avisoTexto, "resultado: un texto se recortó a " + L.LargoTexto + " caracteres.");
                return s.Substring(0, L.LargoTexto) + "…";
            }

            /// <summary>Tipos anónimos y objetos con propiedades: diccionario de sus propiedades públicas legibles. Los de System, con ToString().</summary>
            private object Objeto(object v, int profundidad)
            {
                var tipo = v.GetType();
                string espacio = tipo.Namespace ?? "";
                bool anonimo = tipo.IsDefined(typeof(CompilerGeneratedAttribute), false) && tipo.Name.Contains("AnonymousType");
                if (!anonimo && (espacio == "System" || espacio.StartsWith("System.", StringComparison.Ordinal)))
                    return Texto(Convert.ToString(v, CultureInfo.InvariantCulture) ?? "");
                var r = new Dictionary<string, object>();
                foreach (var p in tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                    object valor;
                    try { valor = p.GetValue(v); }
                    catch (TargetInvocationException ex) { valor = "(error al leer: " + (ex.InnerException ?? ex).Message + ")"; }
                    catch (Exception ex) { valor = "(error al leer: " + ex.Message + ")"; }
                    r[p.Name] = Valor(valor, profundidad + 1);
                }
                return r;
            }
        }
    }
}
