using System;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Texto que ejecutar_comando envía a la línea de comandos de Civil 3D (desde 1.10.1). En la línea de comandos un
    /// espacio o un salto de línea equivalen a Enter (salvo dentro de un texto), así que los espacios finales cierran
    /// las entradas de una orden interactiva: "_.PLINE 1000,2000 1300,2000  " termina la polilínea con el segundo
    /// espacio. Hasta 1.10.0 se recortaban (Trim) y la orden se quedaba esperando (informes 1.8.0 y 1.10.0, paso 0).
    /// Aquí no se toca AutoCAD: lo envía Herramientas.EjecutarComando con SendStringToExecute.
    /// </summary>
    public static class Comando
    {
        /// <summary>Separadores de la primera palabra (el nombre del comando), los mismos que hasta 1.10.0.</summary>
        private static readonly char[] Separadores = { ' ', '\n', '\r' };

        /// <summary>
        /// Prepara el texto del parámetro 'comando': quita los espacios y saltos de línea del principio (en la línea
        /// de comandos vacía, un espacio o un Enter repiten la última orden) y conserva los del final. Vacío, nulo o
        /// solo espacios: ArgumentException con el mismo texto que Argumentos.Requerido.
        /// </summary>
        public static string Preparar(string texto)
        {
            string t = (texto ?? "").TrimStart();
            if (t.Length == 0) throw new ArgumentException("Falta el parámetro obligatorio 'comando'.");
            return t;
        }

        /// <summary>
        /// Nombre global del comando, para reconocer sus eventos CommandWillStart/CommandEnded: la primera palabra sin
        /// '_' ni '.' iniciales y en mayúsculas ("_.PLINE 1000,2000  " → "PLINE"). Igual que hasta 1.10.0.
        /// </summary>
        public static string Nombre(string comando)
        {
            string primera = (comando ?? "").Split(Separadores, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            return primera.TrimStart('_', '.').ToUpperInvariant();
        }

        /// <summary>True si el texto ya termina en espacio o salto de línea, es decir, si ya trae su Enter final.</summary>
        public static bool TerminaConEnter(string comando)
        {
            if (string.IsNullOrEmpty(comando)) return false;
            char ultimo = comando[comando.Length - 1];
            return ultimo == ' ' || ultimo == '\n' || ultimo == '\r';
        }

        /// <summary>
        /// Texto completo para SendStringToExecute: "_.UNDO _BE\n" delante si undo, el comando tal cual y un "\n" final
        /// solo si no lo trae ya. Si el texto termina en espacio o salto de línea ya está cerrado: un Enter de más en la
        /// línea de comandos vacía repetiría la orden (con _.PLINE quedaría otra polilínea esperando su primer punto).
        /// Un comando sin espacios finales ("REGEN", "_.ZOOM E", "_.UNDO 1") se envía igual que hasta 1.10.0.
        /// </summary>
        public static string TextoAEnviar(string comando, bool undo)
        {
            string c = comando ?? "";
            return (undo ? "_.UNDO _BE\n" : "") + c + (TerminaConEnter(c) ? "" : "\n");
        }
    }
}
