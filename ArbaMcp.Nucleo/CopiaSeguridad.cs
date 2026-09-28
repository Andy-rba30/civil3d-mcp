using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ArbaMcp.Nucleo
{
    /// <summary>Lo que la respuesta de una escritura dice de su copia de seguridad (campo 'copia').</summary>
    public sealed class InfoCopia
    {
        [JsonPropertyName("ruta")] public string Ruta { get; set; }
        /// <summary>"copia_de_disco" (File.Copy del .dwg guardado) o "SaveAs" (dibujo sin guardar en disco).</summary>
        [JsonPropertyName("metodo")] public string Metodo { get; set; }
        /// <summary>True si se reutilizó la copia anterior porque el .dwg no cambió de fecha ni tamaño.</summary>
        [JsonPropertyName("reutilizada")] public bool Reutilizada { get; set; }
        /// <summary>Milisegundos que tardó la copia (0 si se reutilizó).</summary>
        [JsonPropertyName("ms")] public long? Ms { get; set; }
        /// <summary>Milisegundos que la escritura esperó a que la copia terminara antes de tocar el dibujo.</summary>
        [JsonPropertyName("espera_ms")] public long EsperaMs { get; set; }
        /// <summary>Fecha del último guardado del .dwg que la copia refleja (yyyy-MM-dd HH:mm:ss); null con SaveAs.</summary>
        [JsonPropertyName("refleja_guardado_de")] public string RefleaGuardadoDe { get; set; }
        [JsonPropertyName("bytes")] public long? Bytes { get; set; }
        /// <summary>pendiente | terminada | error</summary>
        [JsonPropertyName("estado")] public string Estado { get; set; }
        [JsonPropertyName("nota")] public string Nota { get; set; }
    }

    /// <summary>
    /// Copia de seguridad del .dwg guardado en disco, hecha en un hilo aparte con File.Copy: se inicia al entrar en la
    /// escritura y se espera justo antes de tocar el dibujo. Como copia el archivo de disco, refleja el último guardado,
    /// no el estado en memoria (NotaDisco). Si el .dwg no cambió de fecha ni tamaño desde la copia anterior y esa copia
    /// sigue en disco, se reutiliza sin copiar. Nunca se escribe sin copia terminada: Esperar lanza si la copia falló.
    /// </summary>
    public sealed class CopiaSeguridad
    {
        public const string NotaDisco = "La copia refleja el último guardado en disco, no el estado en memoria.";
        public const string MetodoDisco = "copia_de_disco";
        public const string MetodoSaveAs = "SaveAs";

        /// <summary>Copiador de archivos (origen, destino) → bytes copiados. Sustituible en pruebas (por ejemplo, para hacerlo lento o fallar).</summary>
        public static Func<string, string, long> Copiador = (origen, destino) =>
        {
            File.Copy(origen, destino, true);
            return new FileInfo(destino).Length;
        };

        private sealed class Ultima { public string Destino; public DateTime Modificado; public long Tamano; }
        private static readonly Dictionary<string, Ultima> Ultimas = new Dictionary<string, Ultima>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Cerrojo = new object();

        public readonly InfoCopia Info;
        private readonly string _origen, _destino, _carpeta, _nombre;
        private readonly DateTime _modificado;
        private readonly long _tamano;
        private readonly Action<string> _log;
        private Task _tarea;
        private Exception _error;
        private readonly Stopwatch _reloj = new Stopwatch();

        private CopiaSeguridad(InfoCopia info, string origen, string destino, string carpeta, string nombre, DateTime modificado, long tamano, Action<string> log)
        {
            Info = info; _origen = origen; _destino = destino; _carpeta = carpeta; _nombre = nombre;
            _modificado = modificado; _tamano = tamano; _log = log ?? (_ => { });
        }

        /// <summary>
        /// Decide la copia de 'rutaDwg' (guardado en disco) en 'carpetaBackups': reutiliza la anterior si el archivo no
        /// cambió, o planifica una nueva con nombre &lt;dibujo&gt;_&lt;fecha&gt;_&lt;sufijo&gt;.dwg. No copia nada todavía.
        /// </summary>
        public static CopiaSeguridad Planificar(string rutaDwg, string carpetaBackups, string sufijo, DateTime ahora, Action<string> log = null)
        {
            var fi = new FileInfo(rutaDwg);
            if (!fi.Exists) throw new FileNotFoundException("No existe el archivo del dibujo: " + rutaDwg);
            string nombre = Path.GetFileNameWithoutExtension(rutaDwg);
            var info = new InfoCopia
            {
                Metodo = MetodoDisco,
                RefleaGuardadoDe = fi.LastWriteTime.ToString(RegistroEscritura.FormatoHora),
                Nota = NotaDisco,
                Estado = "pendiente"
            };
            lock (Cerrojo)
            {
                if (Ultimas.TryGetValue(fi.FullName, out var u) && u.Modificado == fi.LastWriteTimeUtc && u.Tamano == fi.Length && File.Exists(u.Destino))
                {
                    info.Ruta = u.Destino;
                    info.Reutilizada = true;
                    info.Ms = 0;
                    info.Bytes = new FileInfo(u.Destino).Length;
                    info.Estado = "terminada";
                    return new CopiaSeguridad(info, fi.FullName, u.Destino, carpetaBackups, nombre, fi.LastWriteTimeUtc, fi.Length, log);
                }
            }
            string destino = Path.Combine(carpetaBackups, Copias.NombreCopia(nombre, ahora, sufijo));
            info.Ruta = destino;
            return new CopiaSeguridad(info, fi.FullName, destino, carpetaBackups, nombre, fi.LastWriteTimeUtc, fi.Length, log);
        }

        /// <summary>Lanza la copia en un hilo aparte (no hace nada si se reutiliza). Devuelve this.</summary>
        public CopiaSeguridad Iniciar()
        {
            if (Info.Reutilizada || _tarea != null) return this;
            _reloj.Start();
            _tarea = Task.Run(() =>
            {
                try
                {
                    Directory.CreateDirectory(_carpeta);
                    Info.Bytes = Copiador(_origen, _destino);
                    lock (Cerrojo) Ultimas[_origen] = new Ultima { Destino = _destino, Modificado = _modificado, Tamano = _tamano };
                    foreach (var sobrante in Copias.Sobrantes(_carpeta, _nombre))
                    {
                        try { File.Delete(sobrante); _log("Copia antigua borrada: " + Path.GetFileName(sobrante)); }
                        catch (Exception ex) { _log("No se pudo borrar la copia antigua " + Path.GetFileName(sobrante) + ": " + ex.Message); }
                    }
                }
                catch (Exception ex) { _error = ex; }
                finally { _reloj.Stop(); }
            });
            return this;
        }

        public bool Terminada => Info.Reutilizada || (_tarea != null && _tarea.IsCompleted);

        /// <summary>
        /// Espera a que la copia termine (idempotente), anota espera_ms y ms, y devuelve la información. Si la copia
        /// falló, deja estado=error y lanza: el dibujo no se toca sin copia.
        /// </summary>
        public InfoCopia Esperar()
        {
            if (Info.Reutilizada) return Info;
            if (_tarea == null) Iniciar();
            if (!_tarea.IsCompleted)
            {
                var espera = Stopwatch.StartNew();
                _tarea.Wait();
                Info.EsperaMs += espera.ElapsedMilliseconds;
            }
            Info.Ms = _reloj.ElapsedMilliseconds;
            if (_error != null)
            {
                Info.Estado = "error";
                Info.Nota = "No se pudo hacer la copia de seguridad: " + _error.Message;
                throw new InvalidOperationException("No se pudo hacer la copia de seguridad en " + _destino + ": " + _error.Message + ". No se ha tocado el dibujo.", _error);
            }
            if (Info.Estado != "terminada")
            {
                Info.Estado = "terminada";
                _log("Copia de seguridad: " + _destino + " (" + Info.Ms + " ms)");
            }
            return Info;
        }

        /// <summary>Olvida las copias recordadas (pruebas).</summary>
        public static void Olvidar()
        {
            lock (Cerrojo) Ultimas.Clear();
        }
    }
}
