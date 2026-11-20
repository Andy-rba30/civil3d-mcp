using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Fachada de prueba para ejecutar_codigo (1.16.0): hace de ArbaMcp.Fachada en la nube, sin AutoCAD. Como la de
    /// verdad, cada ayuda llama a CodigoVigilante.Revisar().
    /// </summary>
    public sealed class FachadaPrueba
    {
        public readonly List<string> Mensajes = new List<string>();

        public void Escribir(string texto)
        {
            CodigoVigilante.Revisar();
            Mensajes.Add(texto);
        }

        public int Doble(int x)
        {
            CodigoVigilante.Revisar();
            return 2 * x;
        }

        // 1.17.6: los miembros de la Fachada de verdad que cuentan en solo lectura (los tipos dan igual: el revisor los
        // reconoce por el nombre en la Fachada; en la nube no hay entidades ni ObjectId de AutoCAD).
        public int Agregar(object entidad) { CodigoVigilante.Revisar(); return 0; }
        public T Abrir<T>(object id, bool escribir = false) where T : class { CodigoVigilante.Revisar(); return null; }
        public void EscribirTexto(string nombre, string texto) { CodigoVigilante.Revisar(); }
    }

    /// <summary>
    /// Lo común a las pruebas de ejecutar_codigo: una biblioteca falsa de AutoCAD y Civil 3D (solo nombres y firmas,
    /// compilada aquí con Roslyn; nunca las DLL de Autodesk), las referencias del runtime y la compilación de scripts
    /// con la Fachada de prueba.
    /// </summary>
    internal static class Scripts
    {
        public const string TipoFachada = "ArbaMcp.Pruebas.FachadaPrueba";

        /// <summary>Sustitutos mínimos de la API de Autodesk con los nombres que la lista blanca permite o bloquea.</summary>
        public const string FuenteAutodeskFalsa = @"
namespace Autodesk.AutoCAD.Runtime
{
    public class DisposableWrapper : System.IDisposable
    {
        public System.IntPtr UnmanagedObject => System.IntPtr.Zero;
        public bool IsDisposed => false;
        public void Dispose() { }
        public static DisposableWrapper Create(System.Type t, System.IntPtr p, bool autoDelete) => null;
    }
    public class RXObject : DisposableWrapper
    {
        public static RXClass GetClass(System.Type t) => null;
        public static RXObject Create(System.IntPtr p, bool autoDelete) => null;
        // 1.19.2: copia en memoria (válida sobre un objeto abierto para lectura)
        public virtual object Clone() => null;
    }
    public class RXClass : RXObject { public string Name => """"; }
    public class DynamicLinker { public bool LoadModule(string fileName, bool printit, bool asCmd) => false; }
    public static class SystemObjects { public static DynamicLinker DynamicLinker => null; }
}
namespace Autodesk.AutoCAD.Geometry
{
    public struct Point3d
    {
        public Point3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double DistanceTo(Point3d p) => 0;
    }
    public struct Matrix3d { public static Matrix3d Identity => default(Matrix3d); }
}
namespace Autodesk.AutoCAD.DatabaseServices
{
    public enum OpenMode { ForRead, ForWrite, ForNotify }
    public struct ObjectId
    {
        public static ObjectId Null => default(ObjectId);
        public bool IsNull => true;
        public DBObject GetObject(OpenMode modo) => null;
        public DBObject Open(OpenMode modo) => null;
    }
    public class DBObject : Autodesk.AutoCAD.Runtime.RXObject
    {
        public ObjectId ObjectId => default(ObjectId);
        public void Erase() { }
        public void UpgradeOpen() { }
        // Auditoría de la ronda C (I3, I6)
        public DBObject DeepClone(DBObject owner, object idMap, bool isPrimary) => null;
        public DBObject WblockClone(object owner, object idMap, bool isPrimary) => null;
        public void Close() { }
        public void Cancel() { }
        public void DowngradeOpen() { }
        public void DisableUndoRecording(bool disable) { }
        public void CopyFrom(object otro) { }
        public int ColorIndex { get; set; }
        // 1.19.2: cambian el modo de apertura
        public void UpgradeFromNotify() { }
        public void DowngradeToNotify(bool wasWritable) { }
    }
    // 1.19.2: Entity.Explode deja las piezas aquí, en memoria
    public class DBObjectCollection
    {
        public int Count => 0;
        public int Add(DBObject o) => 0;
    }
    // Auditoría de la ronda C (I3): gestores que heredan de RXObject, no de DBObject, y cambian el dibujo
    public class LayoutManager : Autodesk.AutoCAD.Runtime.RXObject
    {
        public static LayoutManager Current => null;
        public string CurrentLayout { get; set; }
        public ObjectId CreateLayout(string newLayoutName) => default(ObjectId);
        public void DeleteLayout(string layoutName) { }
        public ObjectId GetLayoutId(string layoutName) => default(ObjectId);
        public int LayoutCount => 0;
    }
    public class LayerStateManager : Autodesk.AutoCAD.Runtime.RXObject
    {
        public void SaveLayerState(string sName, int mask, ObjectId idVp) { }
        public void DeleteLayerState(string sName) { }
        public bool HasLayerState(string sName) => false;
    }
    public class ObjectIdCollection
    {
        public int Count => 0;
        public int Add(ObjectId id) => 0;
    }
    public struct Extents3d
    {
        public void AddExtents(Extents3d otra) { }
        public void AddPoint(Autodesk.AutoCAD.Geometry.Point3d p) { }
    }
    public class Entity : DBObject
    {
        public string Layer { get; set; }
        public void SetDatabaseDefaults() { }
        public void IntersectWith(Entity otra, int modo, object puntos, System.IntPtr thisGraphicSystemMarker, System.IntPtr otherGraphicSystemMarker) { }
        // 1.19.2: leen la entidad (válidos sobre una abierta para lectura)
        public void Explode(DBObjectCollection entitySet) { }
        public void Highlight() { }
        public void Unhighlight() { }
        public void List() { }
    }
    public class Curve : Entity
    {
        public void Extend(bool extendStart, Autodesk.AutoCAD.Geometry.Point3d toPoint) { }
    }
    public class Line : Curve
    {
        public Line() { }
        public Line(Autodesk.AutoCAD.Geometry.Point3d inicio, Autodesk.AutoCAD.Geometry.Point3d fin) { }
        public double Length => 0;
        public Autodesk.AutoCAD.Geometry.Point3d StartPoint { get; set; }
        public Autodesk.AutoCAD.Geometry.Point3d EndPoint { get; set; }
        [System.Obsolete(""Usa Length."")] public double Largo => 0;
    }
    public class Transaction : Autodesk.AutoCAD.Runtime.DisposableWrapper
    {
        public DBObject GetObject(ObjectId id, OpenMode modo) => null;
        public void AddNewlyCreatedDBObject(DBObject o, bool add) { }
        public void Commit() { }
        public void Abort() { }
    }
    public class TransactionManager
    {
        public DBObject GetObject(ObjectId id, OpenMode mode, bool openErased, bool forceOpenOnLockedLayers) => null;
        public Transaction StartTransaction() => null;
        public Transaction StartOpenCloseTransaction() => null;
        public Transaction TopTransaction => null;
    }
    public class ObjectEventArgs : System.EventArgs { public DBObject DBObject => null; }
    public delegate void ObjectEventHandler(object sender, ObjectEventArgs e);
    public class Database : DBObject
    {
        public TransactionManager TransactionManager => null;
        public ObjectId LayerTableId => default(ObjectId);
        public ObjectId BlockTableId => default(ObjectId);
        public ObjectId Clayer { get; set; }
        public void SaveAs(string fileName, bool bakAndRename) { }
        public void ReadDwgFile(string fileName, int mode, bool allowCPConversion, string password) { }
        public void Insert(string blockName, Database dataBase, bool preserveSourceDatabase) { }
        public void Insert(Autodesk.AutoCAD.Geometry.Matrix3d transform, Database dataBase, bool preserveSourceDatabase) { }
        public void Insert(string sourceBlockName, string destinationBlockName, Database dataBase, bool preserveSourceDatabase) { }
        public ObjectId AddDBObject(DBObject o) => default(ObjectId);
        public LayerStateManager LayerStateManager => null;
        public void XBindXrefs(ObjectIdCollection xrefBlocks, bool insertBind) { }
        public void DetachXref(ObjectId xrefId) { }
        public void ReloadXrefs(ObjectIdCollection xrefIds) { }
        public void UnloadXrefs(ObjectIdCollection xrefIds) { }
        public void ResolveXrefs(bool useThreadEngine, bool doNewOnly) { }
        public void RestoreOriginalXrefSymbols() { }
        public void RestoreForwardingXrefSymbols() { }
        public int EvaluateFields() => 0;
        public void DisableUndoRecording(bool disable) { }
        public void DeepCloneObjects(ObjectIdCollection ids, ObjectId owner, object mapping, bool deferTranslation) { }
        public void WblockCloneObjects(ObjectIdCollection ids, ObjectId owner, object mapping, int cloning, bool deferTranslation) { }
        public void EraseEmptyObjects(int flags) { }
        public void ReclaimMemoryFromErasedObjects(ObjectIdCollection ids) { }
        public Database Wblock() => null;
        public void BindXrefs(ObjectIdCollection xrefIds, bool insertBind) { }
        public ObjectId AttachXref(string fileName, string blockName) => default(ObjectId);
        public void Purge(ObjectIdCollection ids) { }
        public event ObjectEventHandler ObjectAppended;
    }
    public class RasterImageDef : DBObject { public string SourceFileName { get; set; } }
    // Solo para la prueba de parámetros que contienen 'name' y no son archivos (en la API real, BlockTable.Has(string key))
    public class BlockTable : DBObject
    {
        public bool Has(string blockName) => false;
        public ObjectId this[string key] => default(ObjectId);
    }
    // 1.19.2: la receta crear_capa_con_color, tal cual
    public class SymbolTableRecord : DBObject { public string Name { get; set; } }
    public class SymbolTable : DBObject
    {
        public bool Has(string key) => false;
        public ObjectId Add(SymbolTableRecord value) => default(ObjectId);
        public ObjectId this[string key] => default(ObjectId);
    }
    public class LayerTableRecord : SymbolTableRecord { public Autodesk.AutoCAD.Colors.Color Color { get; set; } }
    public class LayerTable : SymbolTable { }
    public class BlockTableRecord : DBObject, System.Collections.Generic.IEnumerable<ObjectId>
    {
        public System.Collections.Generic.IEnumerator<ObjectId> GetEnumerator() => null;
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null;
        public const string ModelSpace = ""*Model_Space"";
        public string PathName { get; set; }
        public string Name { get; set; }
        public ObjectId AppendEntity(Entity e) => default(ObjectId);
    }
    public class HostApplicationServices
    {
        public static HostApplicationServices Current => null;
        public static Database WorkingDatabase { get; set; }
        public string GetRemoteFile(System.Uri url, bool ignoreCache) => null;
    }
}
namespace Autodesk.AutoCAD.Colors
{
    public enum ColorMethod { ByAci = 195 }
    public sealed class Color { public static Color FromColorIndex(ColorMethod colorMethod, short colorIndex) => null; }
}
namespace Autodesk.AutoCAD.EditorInput
{
    public class Editor
    {
        public void WriteMessage(string mensaje) { }
        public object GetPoint(string mensaje) => null;
        public object GetCurrentView() => null;
        public void SetImpliedSelection(object ids) { }
        public object SelectAll() => null;
        public void Command(params object[] parametros) { }
    }
}
namespace Autodesk.AutoCAD.ApplicationServices
{
    // 1.17.5 (H3): como en la API real (catálogo), el nombre TransactionManager existe aquí (accoremgd) y en
    // DatabaseServices (acdbmgd), y esta deriva de aquella. Con los dos using, sin el alias, daba CS0104.
    public class TransactionManager : Autodesk.AutoCAD.DatabaseServices.TransactionManager { }
    public class Document
    {
        public Autodesk.AutoCAD.DatabaseServices.Database Database => null;
        public TransactionManager TransactionManager => null;
        public Autodesk.AutoCAD.EditorInput.Editor Editor => null;
        public void SendStringToExecute(string comando, bool activate, bool wrapUpInactiveDoc, bool echoCommand) { }
        public System.IDisposable LockDocument() => null;
    }
    public static class DocumentExtension { public static void CloseAndDiscard(this Document doc) { } }
    public class DocumentCollection
    {
        public Document MdiActiveDocument { get; set; }
        public Document Open(string fileName, bool forReadOnly) => null;
    }
    public static class Application
    {
        public static DocumentCollection DocumentManager => null;
        public static object GetSystemVariable(string nombre) => null;
        public static void SetSystemVariable(string nombre, object valor) { }
        public static void ShowAlertDialog(string mensaje) { }
        public static event System.EventHandler Idle;
    }
}
namespace Autodesk.AutoCAD.ApplicationServices.Core
{
    public static class Application
    {
        public static void Quit() { }
        public static object Invoke(object argumentos) => null;
    }
}
namespace Autodesk.AutoCAD.Windows { public class OpenFileDialog { } }
namespace Autodesk.AutoCAD.Interop { public class AcadApplication { } }
namespace Autodesk.Civil.DatabaseServices
{
    public class Entity : Autodesk.AutoCAD.DatabaseServices.Entity { public string Name { get; set; } }
    public class Alignment : Entity
    {
        public static Autodesk.AutoCAD.DatabaseServices.ObjectId Create(object documento, string alignmentName, string siteName, string layerName, string styleName, string labelSetName) => default(Autodesk.AutoCAD.DatabaseServices.ObjectId);
        public void PointLocation(double station, double offset, ref double easting, ref double northing) { }
        public void StationOffset(double easting, double northing, ref double station, ref double offset) { }
        public double Length => 0;
    }
    // 1.19.2: subobjetos de un corredor (no son DBObject: viven dentro del corredor)
    public class Corridor : Entity { public BaselineCollection Baselines => null; }
    public class BaselineCollection { public int Count => 0; public Baseline this[int index] => null; }
    public class Baseline { public BaselineRegionCollection BaselineRegions => null; }
    public class BaselineRegion { public double StartStation { get; set; } }
    public class Profile : Entity
    {
        public static Autodesk.AutoCAD.DatabaseServices.ObjectId CreateByLayout(string profileName, Autodesk.AutoCAD.DatabaseServices.ObjectId alignmentId, string layerName) => default(Autodesk.AutoCAD.DatabaseServices.ObjectId);
        public double ElevationAt(double station) => 0;
    }
    public class Surface : Entity
    {
        public void Rebuild() { }
        public object GetGeneralProperties() => null;
        public double FindElevationAtXY(double x, double y) => 0;
    }
    public class SurfaceStyleCollection
    {
        public bool Contains(string nombre) => false;
        public Autodesk.AutoCAD.DatabaseServices.ObjectId Add(string nombre) => default(Autodesk.AutoCAD.DatabaseServices.ObjectId);
    }
    public class TinSurface : Surface { public void AddPointFile(string pointFilePath, int formato) { } public void RaiseSurface(double amount) { } }
    public class BaselineRegionCollection
    {
        public int Count => 0;
        public void RemoveAt(int indice) { }
        public BaselineRegion this[int index] => null;
    }
}
// 1.19.2: la Fachada con las firmas de ArbaMcp.Fachada (Fachada.cs), para compilar literales los scripts de las
// validaciones (F.Tr, F.Db, F.EspacioModelo(), F.Abrir<T>(ObjectId, bool)...). Solo firmas: no se ejecuta.
namespace ArbaMcp
{
    public sealed class Fachada
    {
        public Autodesk.AutoCAD.ApplicationServices.Document Doc => null;
        public Autodesk.AutoCAD.DatabaseServices.Database Db => null;
        public Autodesk.AutoCAD.DatabaseServices.Transaction Tr => null;
        public bool Simulando => true;
        public bool SoloLectura => false;
        public T Abrir<T>(Autodesk.AutoCAD.DatabaseServices.ObjectId id, bool escribir = false) where T : Autodesk.AutoCAD.DatabaseServices.DBObject => null;
        public Autodesk.AutoCAD.DatabaseServices.ObjectId Alineamiento(string nombre) => default(Autodesk.AutoCAD.DatabaseServices.ObjectId);
        public Autodesk.AutoCAD.DatabaseServices.ObjectId Corredor(string nombre) => default(Autodesk.AutoCAD.DatabaseServices.ObjectId);
        public Autodesk.AutoCAD.DatabaseServices.ObjectId Capa(string nombre) => default(Autodesk.AutoCAD.DatabaseServices.ObjectId);
        public System.Collections.Generic.IEnumerable<Autodesk.AutoCAD.DatabaseServices.ObjectId> EspacioModelo() { yield break; }
        public Autodesk.AutoCAD.DatabaseServices.ObjectId Agregar(Autodesk.AutoCAD.DatabaseServices.Entity entidad) => default(Autodesk.AutoCAD.DatabaseServices.ObjectId);
        public void Escribir(string texto) { }
        public string Texto(string nombre, string porDefecto = null) => porDefecto;
        public double Numero(string nombre, double porDefecto = 0) => porDefecto;
        public bool Booleano(string nombre, bool porDefecto = false) => porDefecto;
        public string LeerTexto(string nombre) => null;
        public void EscribirTexto(string nombre, string texto) { }
    }
}
";

        private static readonly Lazy<byte[]> ImagenAutodesk = new Lazy<byte[]>(() =>
        {
            var arbol = CSharpSyntaxTree.ParseText(FuenteAutodeskFalsa);
            var c = CSharpCompilation.Create("AutodeskFalsa", new[] { arbol }, Runtime().Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            using (var ms = new MemoryStream())
            {
                var r = c.Emit(ms);
                if (!r.Success) throw new InvalidOperationException("La biblioteca falsa de Autodesk no compila: " + string.Join("; ", r.Diagnostics));
                return ms.ToArray();
            }
        });

        private static readonly Lazy<string> RutaAutodesk = new Lazy<string>(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), "arbamcp_pruebas_codigo");
            Directory.CreateDirectory(dir);
            string ruta = Path.Combine(dir, "AutodeskFalsa_" + Guid.NewGuid().ToString("N") + ".dll");
            File.WriteAllBytes(ruta, ImagenAutodesk.Value);
            return ruta;
        });

        /// <summary>Rutas de las DLL del runtime (las de la lista de ensamblados de confianza del proceso de pruebas).</summary>
        public static IEnumerable<string> Runtime()
            => ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator).Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

        /// <summary>Rutas para compilar un script: runtime, la biblioteca falsa en disco, las pruebas (la Fachada) y el núcleo.</summary>
        public static string[] RutasReferencias()
        {
            var rutas = Runtime().ToList();
            rutas.Add(RutaAutodesk.Value);
            rutas.Add(typeof(FachadaPrueba).Assembly.Location);
            if (!rutas.Contains(typeof(CodigoVigilante).Assembly.Location)) rutas.Add(typeof(CodigoVigilante).Assembly.Location);
            return rutas.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static readonly Lazy<List<MetadataReference>> Refs = new Lazy<List<MetadataReference>>(() =>
        {
            var l = Runtime().Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
            l.Add(MetadataReference.CreateFromImage(ImagenAutodesk.Value));
            l.Add(MetadataReference.CreateFromFile(typeof(FachadaPrueba).Assembly.Location));
            return l;
        });

        public static OpcionesCompilacion Opciones() => new OpcionesCompilacion { TipoFachada = TipoFachada };

        /// <summary>
        /// 1.19.2: compila con la Fachada de firmas reales de la biblioteca falsa (ArbaMcp.Fachada, el tipo por defecto de
        /// OpcionesCompilacion), así los scripts de las validaciones compilan literales.
        /// </summary>
        public static ResultadoCompilacion CompilarConFachadaReal(string codigo, bool soloLectura = false)
            => CodigoCompilador.Compilar(codigo, Refs.Value, new OpcionesCompilacion { SoloLectura = soloLectura });

        public static ResultadoCompilacion Compilar(string codigo) => CodigoCompilador.Compilar(codigo, Refs.Value, Opciones());

        /// <summary>Compila con otras opciones (por ejemplo, otros using por defecto).</summary>
        public static ResultadoCompilacion Compilar(string codigo, OpcionesCompilacion opciones) => CodigoCompilador.Compilar(codigo, Refs.Value, opciones);

        /// <summary>Compila y comprueba que compila; si no, la prueba falla con el mensaje para el agente.</summary>
        public static ResultadoCompilacion CompilarBien(string codigo)
        {
            var r = Compilar(codigo);
            Assert.True(r.Ok, r.Mensaje);
            return r;
        }

        /// <summary>Compila, comprueba que NO compila por la lista blanca y devuelve el problema de esa categoría.</summary>
        public static ProblemaCodigo Bloqueado(string codigo, string categoria)
        {
            var r = Compilar(codigo);
            Assert.False(r.Ok, "Debía bloquearse (" + categoria + ") y compiló: " + codigo);
            var p = r.Problemas.FirstOrDefault(x => x.Categoria == categoria);
            Assert.True(p != null, "Se esperaba un bloqueo de '" + categoria + "'. Mensaje: " + r.Mensaje);
            Assert.Contains("[bloqueado: " + categoria + "]", r.Mensaje);
            return p;
        }

        public static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

        /// <summary>Ejecuta un script ya compilado con la Fachada de prueba (sin vigilancia: la sesión la abre la prueba si la necesita).</summary>
        public static object Ejecutar(ResultadoCompilacion r, FachadaPrueba f, string parametros = "{}")
        {
            using (var e = new EjecucionScript(r.Ensamblado, r.Pdb, ResultadoCompilacion.Tipo, ResultadoCompilacion.Metodo, r.NombreEnsamblado,
                typeof(FachadaPrueba).Assembly, typeof(CodigoVigilante).Assembly))
                return CodigoResultado.Normalizar(e.Invocar(f, Json(parametros)));
        }
    }
}
