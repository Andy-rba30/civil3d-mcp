using System.Linq;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Comprobación previa de las escrituras reales (desde 1.10.1). Una escritura real corre en el contexto de comando
    /// del dibujo (ExecuteInCommandContextAsync) y AutoCAD anota ese pseudocomando como una entrada EXECUTEFUNCTION del
    /// menú Deshacer aunque la herramienta falle sin tocar nada (informe 1.10.0, pasos 8 y 10: crear_rasante con un
    /// nombre repetido dejó una entrada vacía). Por eso, antes de entregarla al contexto de comando, el hilo principal
    /// corre la misma herramienta con simular=true en contexto de aplicación (como cualquier simulación: bloqueo de
    /// lectura, sin copia ni entrada de Deshacer). Si la simulación falla, se responde ese error y el contexto de comando
    /// no se abre. Si pasa, su resultado se descarta y la escritura sigue como siempre.
    /// </summary>
    public static class ComprobacionPrevia
    {
        /// <summary>Nombre del parámetro que deben declarar las herramientas para tener comprobación previa.</summary>
        public const string ParametroSimular = "simular";

        /// <summary>True si la herramienta declara el parámetro boolean 'simular' (todas las de escritura lo hacen, regla 4.3 de CLAUDE.md).</summary>
        public static bool DeclaraSimular(DescripcionHerramienta h)
            => h?.Parametros != null && h.Parametros.Any(p => p != null && p.name == ParametroSimular && p.type == "boolean");

        /// <summary>
        /// True si esta llamada lleva comprobación previa: va al contexto Documento (escritura real), la herramienta es
        /// síncrona (las asíncronas, ejecutar_comando y exportar_*, gestionan su propia espera), declara 'simular' y la
        /// llamada no es ya una simulación.
        /// </summary>
        public static bool Aplica(DescripcionHerramienta h, ContextoEjecucion contexto, bool asincrona, JsonElement args)
            => contexto == ContextoEjecucion.Documento && !asincrona && DeclaraSimular(h) && !Argumentos.LeerSimular(args);
    }
}
