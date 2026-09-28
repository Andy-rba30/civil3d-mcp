using System.Text.Encodings.Web;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>Opciones de serialización compartidas por el servidor, el log y las respuestas de escritura.</summary>
    public static class Json
    {
        public static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };

        public static string Serializar(object o) => JsonSerializer.Serialize(o, Opciones);
    }
}
