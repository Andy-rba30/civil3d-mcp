using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Lectura de los argumentos de una herramienta (el objeto "args" de /execute). Los parámetros del contrato son
    /// string, number, boolean o json; aquí se aceptan además con tolerancia: números como texto, booleanos como
    /// "si"/"1"/"true", listas separadas por ';' o como arreglo JSON.
    /// </summary>
    public static class Argumentos
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static bool Tiene(JsonElement a, string n)
            => a.ValueKind == JsonValueKind.Object && a.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null;

        public static string Str(JsonElement a, string n, string def = null)
        {
            if (!Tiene(a, n)) return def;
            var v = a.GetProperty(n);
            return v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
        }

        public static double Num(JsonElement a, string n, double def)
        {
            if (!Tiene(a, n)) return def;
            var v = a.GetProperty(n);
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString().Replace(',', '.'), NumberStyles.Float, Inv, out double d)) return d;
            throw new ArgumentException("El parámetro '" + n + "' debe ser numérico.");
        }

        public static bool Bool(JsonElement a, string n, bool def)
        {
            if (!Tiene(a, n)) return def;
            var v = a.GetProperty(n);
            if (v.ValueKind == JsonValueKind.True) return true;
            if (v.ValueKind == JsonValueKind.False) return false;
            if (v.ValueKind == JsonValueKind.String) return v.GetString().Trim().ToLowerInvariant() is "1" or "si" or "sí" or "true" or "yes";
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble() != 0;
            return def;
        }

        public static string Requerido(JsonElement a, string n)
        {
            string s = Str(a, n);
            if (string.IsNullOrWhiteSpace(s)) throw new ArgumentException("Falta el parámetro obligatorio '" + n + "'.");
            return s;
        }

        /// <summary>'simular': true, 1, "1", "si", "sí", "true" o "yes"; cualquier otra cosa (o ausente) es false.</summary>
        public static bool LeerSimular(JsonElement a) => Bool(a, "simular", false);

        /// <summary>Lista de textos: "a; b;c" → [a, b, c] (vacíos fuera); también acepta un arreglo JSON de textos.</summary>
        public static List<string> Lista(JsonElement a, string n)
        {
            var l = new List<string>();
            if (!Tiene(a, n)) return l;
            var v = a.GetProperty(n);
            if (v.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in v.EnumerateArray())
                {
                    string s = e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) l.Add(s.Trim());
                }
                return l;
            }
            string texto = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
            return (texto ?? "").Split(';').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
        }

        /// <summary>
        /// Parámetro de tipo json: llega como texto (el puente expone json como str) o ya estructurado si el cliente
        /// habla HTTP directamente. Devuelve un JsonElement de valor Undefined si falta; lanza ArgumentException si el
        /// texto no es JSON válido.
        /// </summary>
        public static JsonElement Json(JsonElement a, string n)
        {
            if (!Tiene(a, n)) return default;
            var v = a.GetProperty(n);
            if (v.ValueKind != JsonValueKind.String) return v.Clone();
            string texto = v.GetString();
            if (string.IsNullOrWhiteSpace(texto)) return default;
            try
            {
                using (var doc = JsonDocument.Parse(texto)) return doc.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("El parámetro '" + n + "' debe ser JSON válido: " + ex.Message);
            }
        }

        /// <summary>Redondeo a 4 decimales para las respuestas; null si el número no es finito.</summary>
        public static double? Redondear(double v) => double.IsNaN(v) || double.IsInfinity(v) ? (double?)null : Math.Round(v, 4);
    }
}
