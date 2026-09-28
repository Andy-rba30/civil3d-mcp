using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ArbaMcp
{
    /// <summary>
    /// Reglas comunes a toda herramienta de escritura:
    ///  1. Rechaza si hay un comando activo, el dibujo es de solo lectura o el mismo archivo está abierto dos veces.
    ///  2. Antes del primer cambio de cada minuto guarda una copia en &lt;carpeta del dwg&gt;\backups (conserva 20).
    ///  3. Registra cada llamada en Historial y en &lt;carpeta del dwg&gt;\mcp_log.jsonl.
    ///  4. Compara el estado antes y después y falla si el dibujo no refleja el cambio pedido.
    ///  6. Parámetro 'simular': devuelve lo que haría sin tocar nada.
    /// Todo corre en el hilo principal de AutoCAD, en el contexto de comando del dibujo activo (lo llama el cuerpo de cada herramienta).
    /// </summary>
    internal static class Escritura
    {
        public const int CopiasConservadas = Copias.Conservadas;

        public sealed class Contexto
        {
            public string Herramienta;
            public JsonElement Args;
            public bool Simular;
            public Document Doc;
            public Database Db => Doc.Database;
            /// <summary>Ruta de la copia de seguridad hecha en esta llamada; null si ya había una de este minuto o se simula.</summary>
            public string Copia;
            public List<string> Avisos = new List<string>();
        }

        private static readonly Dictionary<string, string> MinutoUltimaCopia = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Cerrojo = new object();
        private static readonly UTF8Encoding Utf8SinBom = new UTF8Encoding(false);

        // ------------------------------------------------------------------ 1 y 2: comprobaciones y copia
        /// <summary>Comprueba que se puede escribir en el dibujo activo y, salvo en simulación, hace la copia del minuto.</summary>
        public static Contexto Preparar(string herramienta, JsonElement args, bool conCopia = true)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("No hay ningún dibujo abierto en Civil 3D.");
            var ctx = new Contexto { Herramienta = herramienta, Args = args, Simular = LeerSimular(args), Doc = doc };

            int cmdActive = 0;
            try { cmdActive = Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")); } catch { }
            // Dentro de un trabajo de contexto Documento, CMDACTIVE lo pone el propio pseudocomando de
            // ExecuteInCommandContextAsync; HiloPrincipal ya comprobó que Civil 3D estaba libre antes de empezarlo.
            if (cmdActive > 0 && !HiloPrincipal.EnTrabajoDeDocumento)
                throw new InvalidOperationException("Hay un comando activo en Civil 3D (CMDACTIVE=" + cmdActive + "); termínalo o cancélalo antes de escribir.");
            if (doc.IsReadOnly)
                throw new InvalidOperationException("El dibujo activo está abierto en modo solo lectura; no se puede escribir en él.");

            string ruta = RutaDibujo(doc);
            if (ruta != null)
            {
                int repetidos = 0;
                foreach (Document d in AcApp.DocumentManager)
                    if (string.Equals(RutaDibujo(d), ruta, StringComparison.OrdinalIgnoreCase)) repetidos++;
                if (repetidos > 1)
                    throw new InvalidOperationException("El archivo " + ruta + " está abierto " + repetidos + " veces en Civil 3D; cierra las copias antes de escribir.");
            }

            if (!ctx.Simular && conCopia) ctx.Copia = CopiaDelMinuto(doc, herramienta);
            return ctx;
        }

        /// <summary>Guarda una copia si aún no se hizo ninguna en este minuto para este dibujo. Devuelve la ruta o null.</summary>
        public static string CopiaDelMinuto(Document doc, string herramienta)
        {
            string clave = RutaDibujo(doc) ?? doc.Name;
            string minuto = DateTime.Now.ToString("yyyyMMddHHmm");
            lock (Cerrojo)
            {
                if (MinutoUltimaCopia.TryGetValue(clave, out string ultimo) && ultimo == minuto) return null;
                MinutoUltimaCopia[clave] = minuto;
            }
            return GuardarCopia(doc, herramienta);
        }

        /// <summary>
        /// Guarda backups\&lt;nombre&gt;_&lt;fecha&gt;_&lt;sufijo&gt;.dwg sin renombrar el dibujo activo (SaveAs con bBakAndRename=false)
        /// y borra las copias de este dibujo que sobrepasen las últimas 20.
        /// </summary>
        public static string GuardarCopia(Document doc, string sufijo)
        {
            var db = doc.Database;
            string dir = Path.Combine(CarpetaDatos(doc), "backups");
            Directory.CreateDirectory(dir);
            string nombre = Path.GetFileNameWithoutExtension(RutaDibujo(doc) ?? doc.Name);
            string destino = Path.Combine(dir, Copias.NombreCopia(nombre, DateTime.Now, sufijo));
            using (doc.LockDocument())
                db.SaveAs(destino, false, DwgVersion.Current, db.SecurityParameters);
            Historial.Registrar("Copia de seguridad: " + destino);
            PodarCopias(dir, nombre);
            return destino;
        }

        /// <summary>Borra las copias de este dibujo que sobrepasen las últimas 20 (ArbaMcp.Nucleo.Copias decide cuáles).</summary>
        public static void PodarCopias(string dir, string nombreDibujo)
        {
            foreach (var sobrante in Copias.Sobrantes(dir, nombreDibujo))
            {
                try { File.Delete(sobrante); Historial.Registrar("Copia antigua borrada: " + Path.GetFileName(sobrante)); }
                catch (Exception ex) { Historial.Registrar("No se pudo borrar la copia antigua " + Path.GetFileName(sobrante) + ": " + ex.Message); }
            }
        }

        // ------------------------------------------------------------------ 3: registro por llamada
        public static string RutaLog(Document doc) => Path.Combine(CarpetaDatos(doc), "mcp_log.jsonl");

        public static void RegistrarLog(Document doc, string herramienta, JsonElement args, bool ok, long ms, string error)
        {
            string linea = RegistroEscritura.LineaLog(DateTime.Now, herramienta, args, ok, ms, error);
            Historial.Registrar(RegistroEscritura.LineaHistorial(herramienta, ok, ms, error));
            try
            {
                string ruta = RutaLog(doc);
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                lock (Cerrojo) File.AppendAllText(ruta, linea + Environment.NewLine, Utf8SinBom);
            }
            catch (Exception ex) { Historial.Registrar("No se pudo escribir mcp_log.jsonl: " + ex.Message); }
        }

        /// <summary>Últimas n líneas de mcp_log.jsonl del dibujo activo, ya interpretadas como JSON.</summary>
        public static List<object> LeerLog(Document doc, int n)
        {
            string ruta = RutaLog(doc);
            var lista = new List<object>();
            if (!File.Exists(ruta)) return lista;
            string[] lineas;
            lock (Cerrojo) lineas = File.ReadAllLines(ruta, Encoding.UTF8);
            foreach (var l in lineas.Skip(Math.Max(0, lineas.Length - Math.Max(1, n))))
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                try { using (var jd = JsonDocument.Parse(l)) lista.Add(jd.RootElement.Clone()); }
                catch { lista.Add(l); }
            }
            return lista;
        }

        // ------------------------------------------------------------------ envoltorio de una herramienta de escritura
        /// <summary>
        /// Ejecuta el cuerpo de una herramienta de escritura: prepara (comprobaciones y copia), mide el tiempo y
        /// registra el resultado o el error en el log. El cuerpo recibe el contexto y devuelve el objeto de respuesta.
        /// </summary>
        public static object Ejecutar(string herramienta, JsonElement args, Func<Contexto, object> cuerpo, bool conCopia = true)
        {
            var reloj = Stopwatch.StartNew();
            Document doc = null;
            string error = null;
            try
            {
                var ctx = Preparar(herramienta, args, conCopia);
                doc = ctx.Doc;
                return cuerpo(ctx);
            }
            catch (Exception ex)
            {
                var raiz = ex; while (raiz.InnerException != null) raiz = raiz.InnerException;
                error = raiz.Message;
                throw;
            }
            finally
            {
                if (doc == null) { try { doc = AcApp.DocumentManager.MdiActiveDocument; } catch { } }
                RegistrarLog(doc, herramienta, args, error == null, reloj.ElapsedMilliseconds, error);
            }
        }

        // ------------------------------------------------------------------ 4 y 6: antes/después, simulación (ArbaMcp.Nucleo.Verificacion)
        /// <summary>Respuesta de una simulación: el estado actual y el estado que tendría después.</summary>
        public static object Simulacion(Contexto ctx, IDictionary<string, object> antes, IDictionary<string, object> esperado, string accion)
            => Verificacion.Simulacion(ctx.Herramienta, antes, esperado, accion, ctx.Avisos);

        /// <summary>
        /// Respuesta de una escritura real. Comprueba que 'despues' (leído del dibujo tras el cambio) coincide con
        /// 'esperado'; si no, lanza un error con la explicación y no reintenta.
        /// </summary>
        public static object Resultado(Contexto ctx, IDictionary<string, object> antes, IDictionary<string, object> despues, IDictionary<string, object> esperado, string mensaje = null, object datos = null)
            => Verificacion.Resultado(ctx.Herramienta, antes, despues, esperado, mensaje, datos, ctx.Copia, ctx.Avisos);

        public static List<string> Cambios(IDictionary<string, object> antes, IDictionary<string, object> despues) => Verificacion.Cambios(antes, despues);

        public static bool Igual(object a, object b) => Verificacion.Igual(a, b);

        // ------------------------------------------------------------------ rutas
        /// <summary>Ruta completa del dwg si está guardado en disco; null si es un dibujo nuevo sin guardar.</summary>
        public static string RutaDibujo(Document doc)
        {
            try
            {
                string f = doc?.Database?.Filename;
                if (string.IsNullOrWhiteSpace(f) || !Path.IsPathRooted(f) || !File.Exists(f)) return null;
                return Path.GetFullPath(f);
            }
            catch { return null; }
        }

        /// <summary>Carpeta del dwg (para backups y mcp_log.jsonl); si el dibujo no está guardado, %LOCALAPPDATA%\ArbaMcp.</summary>
        public static string CarpetaDatos(Document doc)
        {
            string ruta = doc == null ? null : RutaDibujo(doc);
            if (ruta != null) return Path.GetDirectoryName(ruta);
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArbaMcp");
        }

        private static string LimpiarNombre(string s) => Copias.LimpiarNombre(s);

        private static bool LeerSimular(JsonElement a) => Argumentos.LeerSimular(a);
    }
}
