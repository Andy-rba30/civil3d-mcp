using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ArbaMcp.Nucleo
{
    // Todo lo de este archivo es independiente de Roslyn: el plugin lo usa desde el contexto de carga general de Civil 3D
    // sin que se cargue el compilador (que vive en su propio contexto, ver CargadorCompilador).

    /// <summary>
    /// Qué DLL se dan a Roslyn como referencias en ejecutar_codigo (desde 1.17.3). En acad.exe la lista de ensamblados
    /// de confianza del runtime (TRUSTED_PLATFORM_ASSEMBLIES) trae, además del runtime, todos los .dll y .exe de la
    /// carpeta de AutoCAD, también los nativos (slidelib.exe, accoreconsole.exe, w3dtk_wt.dll...). Roslyn no los
    /// comprueba al crear la referencia sino al compilar, y entonces cada uno es un error CS0009 «La imagen PE no
    /// contiene metadatos administrados» (informe de VALIDACION_116, paso 4a, 14/11/2026: 282 errores).
    /// </summary>
    public static class CodigoReferencias
    {
        /// <summary>
        /// true solo si el archivo es un ensamblado .NET legible: una imagen PE con metadatos administrados que se
        /// pueden leer y con manifiesto de ensamblado. Un PE nativo, un archivo que no es PE, una ruta vacía o que no
        /// existe, o cualquier error al leerlo: false.
        /// </summary>
        public static bool EsEnsambladoAdministrado(string ruta)
        {
            if (string.IsNullOrEmpty(ruta)) return false;
            try
            {
                if (!File.Exists(ruta)) return false;
                using (var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var pe = new PEReader(flujo))
                {
                    if (!pe.HasMetadata) return false;
                    MetadataReader lector = pe.GetMetadataReader();
                    return lector.IsAssembly;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// true si 'ruta' está dentro de 'carpeta' (en ella o en una subcarpeta), sin distinguir mayúsculas y tratando
        /// '\' y '/' igual. Exige el separador tras la carpeta: '...\AutoCAD 2027 X\a.dll' no está dentro de
        /// '...\AutoCAD 2027'. Solo compara texto (sin Path.GetFullPath), así que da lo mismo en Windows y en Linux.
        /// </summary>
        public static bool DentroDe(string ruta, string carpeta)
        {
            string r = Normalizar(ruta), c = Normalizar(carpeta);
            if (r.Length == 0 || c.Length == 0) return false;
            return r.StartsWith(c + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Quita de la lista del runtime (TRUSTED_PLATFORM_ASSEMBLIES) lo que está en la carpeta de AutoCAD 'dirAcad' o
        /// en sus subcarpetas: lo de Autodesk entra por la otra vía, solo lo que ya está cargado. No quita nada por
        /// carpeta si falta 'dirAcad' o si el runtime ('dirRuntime') está en esa carpeta o dentro de ella (un runtime
        /// autocontenido en AutoCAD). Las entradas vacías se descartan. Solo mira el texto de las rutas: si cada una es
        /// un ensamblado .NET lo dice EsEnsambladoAdministrado.
        /// </summary>
        public static IEnumerable<string> FiltrarPlataforma(IEnumerable<string> tpa, string dirAcad, string dirRuntime)
        {
            var salida = new List<string>();
            if (tpa == null) return salida;
            bool porCarpeta = Normalizar(dirAcad).Length > 0 && !MismaODentro(dirRuntime, dirAcad);
            foreach (var r in tpa)
            {
                if (string.IsNullOrEmpty(r)) continue;
                if (porCarpeta && DentroDe(r, dirAcad)) continue;
                salida.Add(r);
            }
            return salida;
        }

        /// <summary>true si 'ruta' es la propia 'carpeta' o está dentro de ella (mismas reglas que DentroDe).</summary>
        private static bool MismaODentro(string ruta, string carpeta)
        {
            string r = Normalizar(ruta), c = Normalizar(carpeta);
            if (r.Length == 0 || c.Length == 0) return false;
            return string.Equals(r, c, StringComparison.OrdinalIgnoreCase) || DentroDe(r, c);
        }

        /// <summary>'/' pasa a '\' y se quitan los separadores finales; null o vacío da "".</summary>
        private static string Normalizar(string ruta)
            => string.IsNullOrEmpty(ruta) ? "" : ruta.Replace('/', '\\').TrimEnd('\\');
    }
}
