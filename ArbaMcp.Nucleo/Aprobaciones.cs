using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>Línea de llamada de mcp_log.jsonl ya interpretada (solo los campos que usa aprobar_resultado).</summary>
    public sealed class LlamadaRegistrada
    {
        public string Id;
        public string Hora;
        public string Herramienta;
        public string Pedido;
        public bool Ok;
    }

    /// <summary>
    /// Aprobaciones (desde 1.6.0): el usuario confirma que el resultado de una llamada fue correcto y el agente lo anota
    /// con aprobar_resultado. Es una línea más de mcp_log.jsonl, de tipo "aprobacion", enlazada por 'id_log' con la
    /// línea de la llamada. No toca el dibujo. exportar_dataset (puente) solo exporta las llamadas aprobadas.
    /// </summary>
    public static class Aprobaciones
    {
        public const string TipoAprobacion = "aprobacion";

        /// <summary>Línea JSON de una aprobación: tipo, id propio, hora, id_log de la llamada, aprobado y comentario.</summary>
        public static string LineaAprobacion(string id, DateTime hora, string idLog, bool aprobado, string comentario)
            => Json.Serializar(new
            {
                tipo = TipoAprobacion,
                id,
                hora = hora.ToString(RegistroEscritura.FormatoHora),
                id_log = idLog,
                aprobado,
                comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim()
            });

        /// <summary>
        /// Busca en las líneas del log la llamada con ese id. Las aprobaciones (tipo "aprobacion") y las líneas que no
        /// son JSON se ignoran; las líneas anteriores a la 1.6.0, sin 'id', nunca coinciden. Devuelve null si no está.
        /// </summary>
        public static LlamadaRegistrada BuscarLlamada(IEnumerable<string> lineas, string idLog)
        {
            if (lineas == null || string.IsNullOrWhiteSpace(idLog)) return null;
            idLog = idLog.Trim();
            foreach (var linea in lineas)
            {
                if (string.IsNullOrWhiteSpace(linea)) continue;
                try
                {
                    using (var doc = JsonDocument.Parse(linea))
                    {
                        var r = doc.RootElement;
                        if (r.ValueKind != JsonValueKind.Object) continue;
                        if (Texto(r, "tipo") == TipoAprobacion) continue;
                        if (!string.Equals(Texto(r, "id"), idLog, StringComparison.Ordinal)) continue;
                        return new LlamadaRegistrada
                        {
                            Id = idLog,
                            Hora = Texto(r, "hora"),
                            Herramienta = Texto(r, "herramienta"),
                            Pedido = Texto(r, "pedido"),
                            Ok = r.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True
                        };
                    }
                }
                catch (JsonException) { }
            }
            return null;
        }

        /// <summary>Mensaje de error cuando el id no existe: dice qué herramienta usar para encontrarlo.</summary>
        public static string MensajeNoExiste(string idLog, string rutaLog)
            => "No existe ninguna llamada con id_log '" + idLog + "' en " + rutaLog + ". Usa leer_log para ver los id de las últimas llamadas.";

        /// <summary>Texto que va al historial del plugin por cada aprobación.</summary>
        public static string LineaHistorial(string idLog, string herramienta, bool aprobado, string comentario)
            => "Aprobación " + idLog + " (" + herramienta + "): " + (aprobado ? "aprobado" : "rechazado")
               + (string.IsNullOrWhiteSpace(comentario) ? "" : " - " + RegistroEscritura.Recortar(comentario.Trim(), 80));

        private static string Texto(JsonElement r, string nombre)
        {
            if (!r.TryGetProperty(nombre, out var v) || v.ValueKind == JsonValueKind.Null) return null;
            return v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
        }
    }
}
