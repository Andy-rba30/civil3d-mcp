using System;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Relectura del estilo de un objeto recién creado (desde 1.17.6): crear_alineamiento_desde_polilinea,
    /// crear_perfil_terreno_y_vista (perfil y vista), crear_rasante y calcular_volumenes. Hasta la 1.17.5 el estilo se
    /// releía por reflexión (Api.Leer "StyleName") con el estilo previsto como respaldo: si la reflexión no encontraba un
    /// getter (varias clases de Civil 3D vuelven a declarar StyleId o StyleName solo de escritura, como Surface.StyleId),
    /// la respuesta repetía el estilo previsto sin decirlo. Ahora el plugin lo lee con enlace directo
    /// (Autodesk.Civil.DatabaseServices.Entity.StyleId → DBObject.Name, o StyleName) y aquí se decide qué decir:
    /// un aviso si no se pudo releer y un error (regla 4: el dibujo no refleja lo pedido) si se releyó otro estilo.
    /// </summary>
    public static class RelecturaEstilo
    {
        /// <summary>True si los dos nombres son el mismo estilo (sin espacios de más ni mayúsculas), como Verificacion.Igual.</summary>
        public static bool Mismo(string a, string b)
            => a != null && b != null && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Aviso cuando no se pudo releer el estilo de 'objeto' ("del alineamiento 'Eje 1'"): qué falló y que 'estilo' es
        /// el previsto, sin comprobar. Null si no hubo fallo.
        /// </summary>
        public static string Aviso(string objeto, string fallo, string previsto)
        {
            if (string.IsNullOrWhiteSpace(fallo)) return null;
            return "No se pudo releer el estilo " + objeto + " (" + fallo + "): "
                + (string.IsNullOrWhiteSpace(previsto) ? "'estilo' queda sin comprobar." : "'estilo' es el previsto ('" + previsto.Trim() + "'), sin comprobar.");
        }

        /// <summary>
        /// Texto del error (regla 4) si el estilo releído no es el previsto; null si coinciden o si falta alguno de los
        /// dos (sin relectura no se puede comparar: eso lo dice Aviso). 'objeto': "del alineamiento 'Eje 1'".
        /// </summary>
        public static string Discrepancia(string objeto, string releido, string previsto)
        {
            if (string.IsNullOrWhiteSpace(releido) || string.IsNullOrWhiteSpace(previsto) || Mismo(releido, previsto)) return null;
            return "El estilo releído " + objeto + " es '" + releido.Trim() + "' y se pidió '" + previsto.Trim()
                + "' (releído con Entity.StyleId y DBObject.Name): el dibujo no refleja el estilo pedido.";
        }
    }
}
