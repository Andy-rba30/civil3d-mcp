using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;

namespace ArbaMcp
{
    /// <summary>
    /// Ayudas para los scripts de ejecutar_codigo (desde 1.16.0): el script la ve como F. Lo básico: el documento activo,
    /// la transacción de la ejecución y la búsqueda de objetos por nombre, más parámetros, mensajes y archivos en la
    /// carpeta permitida. Cada ayuda revisa el tiempo límite (CodigoVigilante.Revisar).
    ///
    /// F.Tr es una transacción anidada dentro de la del plugin: si el script la confirma, aborta o abre las suyas
    /// (using (var tr = F.Db.TransactionManager.StartTransaction()) {...}), el plugin puede deshacerlo todo al simular,
    /// porque la transacción exterior es suya y el script no la ve. Los archivos de F.EscribirTexto se escriben al
    /// final y solo si el script termina bien y no es una simulación.
    /// </summary>
    public sealed class Fachada
    {
        private readonly Document _doc;
        private readonly Transaction _tr;
        private readonly JsonElement _parametros;
        private readonly bool _simulando;
        private readonly string _carpeta;
        private readonly Dictionary<string, string> _pendientes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private long _bytesPendientes;
        private bool _cerrada;

        internal readonly List<string> Mensajes = new List<string>();
        internal readonly List<string> Leidos = new List<string>();

        internal Fachada(Document doc, Transaction tr, JsonElement parametros, bool simulando, string carpeta)
        {
            _doc = doc;
            _tr = tr;
            _parametros = parametros;
            _simulando = simulando;
            _carpeta = carpeta;
        }

        // ------------------------------------------------------------------ lo básico
        /// <summary>Revisa el tiempo límite (también lo hacen todas las ayudas y el código que añade el plugin a cada bucle).</summary>
        public void Revisar()
        {
            if (_cerrada) throw new InvalidOperationException("La Fachada ya no está disponible: el script terminó.");
            CodigoVigilante.Revisar();
        }

        /// <summary>Documento activo de Civil 3D (bloqueado mientras corre el script).</summary>
        public Document Doc { get { Revisar(); return _doc; } }

        /// <summary>Base de datos del dibujo activo.</summary>
        public Database Db { get { Revisar(); return _doc.Database; } }

        /// <summary>Transacción de la ejecución (anidada en la del plugin). Con simular=true se aborta al final.</summary>
        public Transaction Tr { get { Revisar(); return _tr; } }

        /// <summary>Documento de Civil 3D (CivilApplication.ActiveDocument): alineamientos, superficies, corredores...</summary>
        public CivilDocument Civil { get { Revisar(); return CivilApplication.ActiveDocument; } }

        /// <summary>True si es una simulación (simular=true): los cambios se deshacen al final y no se escribe ningún archivo.</summary>
        public bool Simulando => _simulando;

        /// <summary>El JSON de 'parametros' (un objeto; vacío si no se pasó). Es de solo lectura.</summary>
        public JsonElement Parametros => _parametros;

        // ------------------------------------------------------------------ objetos
        /// <summary>Abre un objeto en la transacción del script (para escribir con escribir=true).</summary>
        public T Abrir<T>(ObjectId id, bool escribir = false) where T : DBObject
        {
            Revisar();
            var o = _tr.GetObject(id, escribir ? OpenMode.ForWrite : OpenMode.ForRead);
            if (o is T t) return t;
            throw new InvalidOperationException("El objeto " + id.Handle + " es " + (o?.GetType().Name ?? "null") + ", no " + typeof(T).Name + ".");
        }

        /// <summary>Alineamiento (eje) por nombre. Error "No existe el alineamiento... Usa listar_alineamientos." si no está.</summary>
        public ObjectId Alineamiento(string nombre) { Revisar(); return Herramientas.FachadaAlineamiento(_tr, nombre); }

        /// <summary>Perfil de un alineamiento por nombre.</summary>
        public ObjectId Perfil(string alineamiento, string nombre) { Revisar(); return Herramientas.FachadaPerfil(_tr, alineamiento, nombre); }

        /// <summary>Superficie por nombre.</summary>
        public ObjectId Superficie(string nombre) { Revisar(); return Herramientas.FachadaSuperficie(_tr, nombre); }

        /// <summary>Corredor por nombre.</summary>
        public ObjectId Corredor(string nombre) { Revisar(); return Herramientas.FachadaCorredor(_tr, nombre); }

        /// <summary>Ensamblaje por nombre.</summary>
        public ObjectId Ensamblaje(string nombre) { Revisar(); return Herramientas.FachadaEnsamblaje(_tr, nombre); }

        /// <summary>Capa por nombre.</summary>
        public ObjectId Capa(string nombre)
        {
            Revisar();
            var capas = (LayerTable)_tr.GetObject(_doc.Database.LayerTableId, OpenMode.ForRead);
            if (string.IsNullOrWhiteSpace(nombre) || !capas.Has(nombre)) throw new ArgumentException("No existe la capa '" + nombre + "'.");
            return capas[nombre];
        }

        /// <summary>Definición de bloque por nombre.</summary>
        public ObjectId Bloque(string nombre)
        {
            Revisar();
            var bloques = (BlockTable)_tr.GetObject(_doc.Database.BlockTableId, OpenMode.ForRead);
            if (string.IsNullOrWhiteSpace(nombre) || !bloques.Has(nombre)) throw new ArgumentException("No existe el bloque '" + nombre + "'.");
            return bloques[nombre];
        }

        /// <summary>Los objetos del espacio modelo, uno a uno (revisa el tiempo límite en cada uno).</summary>
        public IEnumerable<ObjectId> EspacioModelo()
        {
            Revisar();
            var bloques = (BlockTable)_tr.GetObject(_doc.Database.BlockTableId, OpenMode.ForRead);
            var modelo = (BlockTableRecord)_tr.GetObject(bloques[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in modelo)
            {
                Revisar();
                yield return id;
            }
        }

        /// <summary>Añade una entidad nueva al espacio modelo en la transacción del script y devuelve su ObjectId.</summary>
        public ObjectId Agregar(Entity entidad)
        {
            Revisar();
            if (entidad == null) throw new ArgumentNullException(nameof(entidad));
            var bloques = (BlockTable)_tr.GetObject(_doc.Database.BlockTableId, OpenMode.ForRead);
            var modelo = (BlockTableRecord)_tr.GetObject(bloques[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            var id = modelo.AppendEntity(entidad);
            _tr.AddNewlyCreatedDBObject(entidad, true);
            return id;
        }

        // ------------------------------------------------------------------ mensajes y parámetros
        /// <summary>Mensaje para la respuesta (datos.mensajes); hasta 200 mensajes de 2000 caracteres.</summary>
        public void Escribir(string texto)
        {
            Revisar();
            if (Mensajes.Count >= CodigoLimites.MaximoMensajes) return;
            texto = texto ?? "";
            Mensajes.Add(texto.Length > 2000 ? texto.Substring(0, 2000) + "…" : texto);
        }

        /// <summary>Texto de 'parametros' (o porDefecto si no está).</summary>
        public string Texto(string nombre, string porDefecto = null) { Revisar(); return Argumentos.Str(_parametros, nombre, porDefecto); }

        /// <summary>Número de 'parametros' (o porDefecto si no está; acepta "1.5" y "1,5").</summary>
        public double Numero(string nombre, double porDefecto = 0) { Revisar(); return Argumentos.Num(_parametros, nombre, porDefecto); }

        /// <summary>Booleano de 'parametros' (o porDefecto si no está).</summary>
        public bool Booleano(string nombre, bool porDefecto = false) { Revisar(); return Argumentos.Bool(_parametros, nombre, porDefecto); }

        // ------------------------------------------------------------------ archivos en la carpeta permitida
        /// <summary>La carpeta permitida: %LOCALAPPDATA%\ArbaMcp\scripts.</summary>
        public string CarpetaPermitida => _carpeta;

        /// <summary>Lee un archivo de texto (UTF-8) de la carpeta permitida; 'nombre' es relativo ('datos.csv', 'sub/datos.csv').</summary>
        public string LeerTexto(string nombre)
        {
            Revisar();
            string ruta = CodigoCarpeta.Resolver(_carpeta, nombre);
            if (_pendientes.TryGetValue(ruta, out string pendiente)) return pendiente;
            if (!File.Exists(ruta)) throw new FileNotFoundException("No existe '" + nombre + "' en la carpeta permitida " + CodigoCarpeta.Texto + ". Usa F.ListarArchivos().");
            if (new FileInfo(ruta).Length > CodigoLimites.MaximoBytesArchivo)
                throw new InvalidOperationException("'" + nombre + "' pasa de " + (CodigoLimites.MaximoBytesArchivo / 1024 / 1024) + " MB.");
            Leidos.Add(Relativo(ruta));
            return File.ReadAllText(ruta, Encoding.UTF8);
        }

        /// <summary>
        /// Escribe un archivo de texto (UTF-8) en la carpeta permitida. Se escribe al terminar el script, solo si termina
        /// bien y no es una simulación (al simular se anota lo que se habría escrito).
        /// </summary>
        public void EscribirTexto(string nombre, string texto)
        {
            Revisar();
            string ruta = CodigoCarpeta.Resolver(_carpeta, nombre);
            texto = texto ?? "";
            long bytes = Encoding.UTF8.GetByteCount(texto);
            if (bytes > CodigoLimites.MaximoBytesArchivo || _bytesPendientes + bytes > 5L * CodigoLimites.MaximoBytesArchivo)
                throw new InvalidOperationException("'" + nombre + "' es demasiado grande (hasta " + (CodigoLimites.MaximoBytesArchivo / 1024 / 1024) + " MB por archivo).");
            _bytesPendientes += bytes;
            _pendientes[ruta] = texto;
        }

        /// <summary>Archivos de la carpeta permitida (nombres relativos), con los que el script ya pidió escribir.</summary>
        public string[] ListarArchivos()
        {
            Revisar();
            return CodigoCarpeta.Listar(_carpeta).Concat(_pendientes.Keys.Select(Relativo))
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        // ------------------------------------------------------------------ uso interno del plugin
        /// <summary>Archivos que el script pidió escribir (nombres relativos).</summary>
        internal List<string> Pendientes => _pendientes.Keys.Select(Relativo).ToList();

        /// <summary>Escribe los archivos pendientes (solo tras confirmar una ejecución real). Devuelve sus nombres relativos.</summary>
        internal List<string> EscribirPendientes()
        {
            var escritos = new List<string>();
            foreach (var kv in _pendientes)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(kv.Key));
                File.WriteAllText(kv.Key, kv.Value, new UTF8Encoding(false));
                escritos.Add(Relativo(kv.Key));
            }
            return escritos;
        }

        internal void Cerrar() => _cerrada = true;

        private string Relativo(string ruta) => Path.GetRelativePath(_carpeta, ruta).Replace('\\', '/');
    }
}
