using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ArbaMcp.Nucleo
{
    /// <summary>Petición HTTP ya analizada: línea de petición, cabeceras (sin distinguir mayúsculas) y cuerpo.</summary>
    public sealed class PeticionHttp
    {
        public string Metodo = "";
        /// <summary>Ruta sin la cadena de consulta ("/execute").</summary>
        public string Ruta = "";
        /// <summary>Ruta tal como llegó, con la cadena de consulta si la había.</summary>
        public string RutaCompleta = "";
        public Dictionary<string, string> Cabeceras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Valor de Content-Length (0 si no viene o no es un entero).</summary>
        public int LargoCuerpo;
        public string Cuerpo = "";

        public string Cabecera(string nombre) => Cabeceras.TryGetValue(nombre, out string v) ? v : null;
    }

    /// <summary>
    /// Análisis de peticiones y construcción de respuestas del servidor mínimo (sin sockets: recibe bytes o texto y
    /// devuelve datos). El servidor del plugin solo mueve bytes entre el socket y estas funciones.
    /// </summary>
    public static class Http
    {
        public const string CabeceraToken = "X-Arba-Token";
        public const string SeparadorCabeceras = "\r\n\r\n";

        /// <summary>Índice del final de las cabeceras ("\r\n\r\n") dentro de los primeros 'largo' bytes, o -1.</summary>
        public static int BuscarFinCabeceras(byte[] datos, int largo) => Buscar(datos, largo, SeparadorCabeceras);

        public static int Buscar(byte[] datos, int largo, string patron)
        {
            var p = Encoding.ASCII.GetBytes(patron);
            for (int i = 0; i <= largo - p.Length; i++)
            {
                int k = 0;
                while (k < p.Length && datos[i + k] == p[k]) k++;
                if (k == p.Length) return i;
            }
            return -1;
        }

        /// <summary>
        /// Analiza la línea de petición y las cabeceras (el texto anterior a "\r\n\r\n"). Devuelve false, con el
        /// motivo, si la línea de petición no tiene método y ruta.
        /// </summary>
        public static bool IntentarAnalizarCabeceras(string texto, out PeticionHttp peticion, out string error)
        {
            peticion = null;
            error = null;
            var lineas = (texto ?? "").Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lineas.Length == 0) { error = "Petición vacía"; return false; }
            var partes = lineas[0].Split(' ');
            if (partes.Length < 2 || partes[0].Length == 0 || partes[1].Length == 0) { error = "Petición inválida"; return false; }

            var p = new PeticionHttp { Metodo = partes[0].ToUpperInvariant(), RutaCompleta = partes[1] };
            int qs = p.RutaCompleta.IndexOf('?');
            p.Ruta = qs >= 0 ? p.RutaCompleta.Substring(0, qs) : p.RutaCompleta;
            for (int i = 1; i < lineas.Length; i++)
            {
                string l = lineas[i];
                int idx = l.IndexOf(':');
                if (idx <= 0) continue;
                p.Cabeceras[l.Substring(0, idx).Trim()] = l.Substring(idx + 1).Trim();
            }
            if (p.Cabeceras.TryGetValue("Content-Length", out string cl) && int.TryParse(cl, out int largo) && largo >= 0) p.LargoCuerpo = largo;
            peticion = p;
            return true;
        }

        public static string TextoEstado(int codigo) => codigo switch
        {
            200 => "OK", 204 => "No Content", 400 => "Bad Request", 401 => "Unauthorized", 403 => "Forbidden",
            404 => "Not Found", 413 => "Payload Too Large", 415 => "Unsupported Media Type", _ => "Internal Server Error"
        };

        /// <summary>Respuesta HTTP/1.1 completa (cabeceras y cuerpo JSON en UTF-8, Connection: close).</summary>
        public static byte[] ConstruirRespuesta(int codigo, string json)
        {
            var cuerpo = Encoding.UTF8.GetBytes(json ?? "");
            var cab = "HTTP/1.1 " + codigo + " " + TextoEstado(codigo) + "\r\n" +
                      "Content-Type: application/json; charset=utf-8\r\n" +
                      "Content-Length: " + cuerpo.Length + "\r\n" +
                      "Connection: close\r\n\r\n";
            var bytesCab = Encoding.ASCII.GetBytes(cab);
            var todo = new byte[bytesCab.Length + cuerpo.Length];
            Buffer.BlockCopy(bytesCab, 0, todo, 0, bytesCab.Length);
            Buffer.BlockCopy(cuerpo, 0, todo, bytesCab.Length, cuerpo.Length);
            return todo;
        }

        /// <summary>{"ok":false,"error":"..."}</summary>
        public static string Error(string mensaje) => Json.Serializar(new { ok = false, error = mensaje });
    }

    /// <summary>
    /// Decisión de autorización de una petición. Orden de comprobación (el primero que falla decide):
    /// cabecera Origin → 403; Host distinto de 127.0.0.1:puerto o localhost:puerto → 400; token ausente o distinto → 401
    /// (salvo GET /ping, que es público desde 1.3.0); POST sin Content-Type application/json → 415.
    /// </summary>
    public static class Autorizacion
    {
        public const int Autorizada = 0;

        /// <summary>GET /ping (o /) no exige token: es lo que sondea un cliente mientras Civil 3D arranca.</summary>
        public static bool EsPingPublico(string metodo, string ruta)
            => string.Equals(metodo, "GET", StringComparison.OrdinalIgnoreCase) && (ruta == "/ping" || ruta == "/ping/" || ruta == "/");

        public static int Decidir(PeticionHttp p, int puerto, string tokenActual)
            => Decidir(p.Metodo, p.Ruta, p.Cabeceras, puerto, tokenActual);

        public static int Decidir(string metodo, string ruta, IReadOnlyDictionary<string, string> cabeceras, int puerto, string tokenActual)
        {
            if (cabeceras.ContainsKey("Origin")) return 403;
            if (!cabeceras.TryGetValue("Host", out string host) || (host != "127.0.0.1:" + puerto && host != "localhost:" + puerto)) return 400;
            if (!EsPingPublico(metodo, ruta))
            {
                if (!cabeceras.TryGetValue(Http.CabeceraToken, out string token) || !TokenIgual(token, tokenActual)) return 401;
            }
            if (string.Equals(metodo, "POST", StringComparison.OrdinalIgnoreCase)
                && (!cabeceras.TryGetValue("Content-Type", out string ct) || !ct.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)))
                return 415;
            return Autorizada;
        }

        /// <summary>Comparación en tiempo constante (no depende de en qué carácter difieren). Distinta longitud → false.</summary>
        public static bool TokenIgual(string recibido, string actual)
        {
            if (recibido == null || actual == null) return false;
            var a = Encoding.UTF8.GetBytes(recibido);
            var b = Encoding.UTF8.GetBytes(actual);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
