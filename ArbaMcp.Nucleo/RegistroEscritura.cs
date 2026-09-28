using System;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>Línea de mcp_log.jsonl: una por llamada de escritura (hora, herramienta, args, ok, ms, error).</summary>
    public static class RegistroEscritura
    {
        public const string FormatoHora = "yyyy-MM-dd HH:mm:ss";

        public static string LineaLog(DateTime hora, string herramienta, JsonElement args, bool ok, long ms, string error)
        {
            object argsJson = args.ValueKind == JsonValueKind.Undefined ? null : (object)args;
            return Json.Serializar(new
            {
                hora = hora.ToString(FormatoHora),
                herramienta,
                args = argsJson,
                ok,
                ms,
                error
            });
        }

        /// <summary>Texto que va al historial del plugin por cada escritura.</summary>
        public static string LineaHistorial(string herramienta, bool ok, long ms, string error)
            => "Escritura " + herramienta + (ok ? " OK" : " ERROR: " + error) + " (" + ms + " ms)";
    }
}
