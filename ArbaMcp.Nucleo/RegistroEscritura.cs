using System;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Línea de mcp_log.jsonl por cada llamada de herramienta. Hasta la 1.5.0 solo se registraban las escrituras; desde
    /// la 1.6.0 se registra toda llamada (lecturas incluidas) con un identificador único 'id', que el servidor devuelve
    /// como 'id_log' para que el agente pueda referirse a ella (aprobar_resultado), y con el 'pedido' del usuario.
    /// El nombre de la clase se conserva por compatibilidad.
    /// </summary>
    public static class RegistroEscritura
    {
        public const string FormatoHora = "yyyy-MM-dd HH:mm:ss";
        /// <summary>Valor de 'tipo' de una línea de llamada (las aprobaciones llevan "aprobacion"; ver Aprobaciones).</summary>
        public const string TipoLlamada = "llamada";

        /// <summary>
        /// Identificador único de una línea del log: fecha y hora (legible y ordenable) más seis caracteres al azar para
        /// que dos llamadas en el mismo segundo no choquen. Ejemplo: 20261031-143501-a3f9c2.
        /// </summary>
        public static string NuevoId(DateTime hora)
            => hora.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);

        /// <summary>Línea de una llamada con id, pedido (si lo trae args) y versión del plugin.</summary>
        public static string LineaLog(string id, DateTime hora, string herramienta, JsonElement args, bool ok, long ms, string error, string version)
        {
            object argsJson = args.ValueKind == JsonValueKind.Undefined ? null : (object)args;
            return Json.Serializar(new
            {
                tipo = TipoLlamada,
                id,
                hora = hora.ToString(FormatoHora),
                herramienta,
                pedido = args.ValueKind == JsonValueKind.Undefined ? null : Argumentos.LeerPedido(args),
                args = argsJson,
                ok,
                ms,
                error,
                version
            });
        }

        /// <summary>Forma anterior a la 1.6.0 (sin id ni versión): genera un id nuevo. Se conserva por compatibilidad.</summary>
        public static string LineaLog(DateTime hora, string herramienta, JsonElement args, bool ok, long ms, string error)
            => LineaLog(NuevoId(hora), hora, herramienta, args, ok, ms, error, null);

        /// <summary>Texto que va al historial del plugin por cada llamada (forma anterior a la 1.6.0).</summary>
        public static string LineaHistorial(string herramienta, bool ok, long ms, string error)
            => "Escritura " + herramienta + (ok ? " OK" : " ERROR: " + error) + " (" + ms + " ms)";

        /// <summary>Texto del historial con el id de la línea y el pedido (recortado) si lo hay.</summary>
        public static string LineaHistorial(string id, string herramienta, bool ok, long ms, string error, string pedido)
            => "Registro " + id + ": " + herramienta + (ok ? " OK" : " ERROR: " + error) + " (" + ms + " ms)"
               + (string.IsNullOrEmpty(pedido) ? "" : " pedido: \"" + Recortar(pedido, 80) + "\"");

        public static string Recortar(string texto, int maximo)
        {
            if (texto == null) return null;
            texto = texto.Replace("\r", " ").Replace("\n", " ");
            return texto.Length <= maximo ? texto : texto.Substring(0, maximo - 1) + "…";
        }
    }
}
