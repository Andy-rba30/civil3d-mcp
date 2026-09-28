using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>Nombres y poda de las copias de seguridad (backups\&lt;nombre&gt;_&lt;fecha&gt;_&lt;sufijo&gt;.dwg; se conservan las últimas 20).</summary>
    public static class Copias
    {
        public const int Conservadas = 20;

        /// <summary>Sustituye por '_' los caracteres no válidos en un nombre de archivo y los espacios; vacío → "mcp".</summary>
        public static string LimpiarNombre(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "mcp";
            var malos = Path.GetInvalidFileNameChars();
            return new string(s.Trim().Select(c => malos.Contains(c) || char.IsWhiteSpace(c) ? '_' : c).ToArray());
        }

        /// <summary>&lt;nombre&gt;_&lt;yyyyMMdd_HHmmss&gt;_&lt;sufijo limpio&gt;.dwg</summary>
        public static string NombreCopia(string nombreDibujo, DateTime momento, string sufijo)
            => nombreDibujo + "_" + momento.ToString("yyyyMMdd_HHmmss") + "_" + LimpiarNombre(sufijo) + ".dwg";

        /// <summary>Patrón de búsqueda de las copias de un dibujo dentro de backups\.</summary>
        public static string PatronCopias(string nombreDibujo) => nombreDibujo + "_*.dwg";

        /// <summary>Rutas que sobran: todas menos las 'conservar' más recientes (por fecha de modificación).</summary>
        public static List<string> Sobrantes(IEnumerable<(string Ruta, DateTime Modificado)> copias, int conservar = Conservadas)
            => copias.OrderByDescending(c => c.Modificado).Skip(Math.Max(0, conservar)).Select(c => c.Ruta).ToList();

        /// <summary>Copias sobrantes de un dibujo en una carpeta real (las últimas 'conservar' se quedan).</summary>
        public static List<string> Sobrantes(string carpeta, string nombreDibujo, int conservar = Conservadas)
        {
            var dir = new DirectoryInfo(carpeta);
            if (!dir.Exists) return new List<string>();
            return Sobrantes(dir.GetFiles(PatronCopias(nombreDibujo)).Select(f => (f.FullName, f.LastWriteTimeUtc)), conservar);
        }
    }
}
