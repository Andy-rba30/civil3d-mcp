// Sustitutos mínimos de la API de AutoCAD (acmgd/acdbmgd/accoremgd/AdWindows) SOLO para compilar el plugin sin Civil 3D.
// No reproducen el comportamiento real: solo nombres y tipos que el código del plugin usa. Ver LEEME.md.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Autodesk.AutoCAD.Runtime
{
    public interface IExtensionApplication { void Initialize(); void Terminate(); }
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)] public class ExtensionApplicationAttribute : Attribute { public ExtensionApplicationAttribute(Type t) { } }
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)] public class CommandClassAttribute : Attribute { public CommandClassAttribute(Type t) { } }
    [AttributeUsage(AttributeTargets.Method)] public class CommandMethodAttribute : Attribute { public CommandMethodAttribute(string nombre) { } }
    public class RXClass { public bool IsDerivedFrom(RXClass otra) => false; public string Name => ""; }
    public class RXObject { public static RXClass GetClass(Type t) => new RXClass(); }
}

namespace Autodesk.AutoCAD.Geometry
{
    public struct Point2d
    {
        public Point2d(double x, double y) { X = x; Y = y; }
        public double X { get; } public double Y { get; }
        public double GetDistanceTo(Point2d p) => 0;
    }
    public struct Point3d
    {
        public Point3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public double X { get; } public double Y { get; } public double Z { get; }
        public double DistanceTo(Point3d p) => 0;
        public Point2d Convert2d(Plane p) => new Point2d(X, Y);
    }
    public class Plane { }
    public class Point3dCollection : IEnumerable
    {
        public int Count => 0;
        public Point3d this[int i] => new Point3d();
        public void Add(Point3d p) { }
        public IEnumerator GetEnumerator() => new List<Point3d>().GetEnumerator();
    }
    public struct Vector3d { }
}

namespace Autodesk.AutoCAD.DatabaseServices
{
    using Autodesk.AutoCAD.Geometry;

    public struct Handle { public Handle(long v) { } public long Value => 0; public override string ToString() => ""; }
    public struct ObjectId
    {
        public static readonly ObjectId Null = new ObjectId();
        public bool IsNull => true;
        public bool IsValid => false;
        public bool IsErased => false;
        public Handle Handle => new Handle(0);
        public Autodesk.AutoCAD.Runtime.RXClass ObjectClass => new Autodesk.AutoCAD.Runtime.RXClass();
        public static bool operator ==(ObjectId a, ObjectId b) => true;
        public static bool operator !=(ObjectId a, ObjectId b) => false;
        public override bool Equals(object o) => true;
        public override int GetHashCode() => 0;
    }
    public class ObjectIdCollection : IEnumerable
    {
        public int Count => 0;
        public ObjectId this[int i] => ObjectId.Null;
        public int Add(ObjectId id) => 0;
        public IEnumerator GetEnumerator() => new List<ObjectId>().GetEnumerator();
    }
    public enum OpenMode { ForRead, ForWrite }
    public enum DwgVersion { Current }
    public enum Intersect { OnBothOperands, ExtendThis, ExtendArgument, ExtendBoth }
    public class SecurityParameters { }
    public class DBObject : IDisposable
    {
        public ObjectId ObjectId => ObjectId.Null;
        public Handle Handle => new Handle(0);
        public bool IsWriteEnabled => false;
        public void UpgradeOpen() { }
        public void DowngradeOpen() { }
        public void Dispose() { }
    }
    public class Entity : DBObject
    {
        public string Layer { get; set; }
        public ObjectId LayerId { get; set; }
        public void IntersectWith(Entity otra, Intersect modo, Point3dCollection puntos, IntPtr a, IntPtr b) { }
        public void IntersectWith(Entity otra, Intersect modo, Plane plano, Point3dCollection puntos, IntPtr a, IntPtr b) { }
    }
    public class Curve : Entity
    {
        public Point3d StartPoint => new Point3d();
        public Point3d EndPoint => new Point3d();
        public double StartParam => 0;
        public double EndParam => 0;
        public bool Closed { get; set; }
        public Point3d GetPointAtParameter(double p) => new Point3d();
        public Point3d GetPointAtDist(double d) => new Point3d();
        public double GetDistanceAtParameter(double p) => 0;
        public double GetParameterAtDistance(double d) => 0;
    }
    public class Polyline : Curve
    {
        public int NumberOfVertices => 0;
        public Point2d GetPoint2dAt(int i) => new Point2d();
        public Point3d GetPoint3dAt(int i) => new Point3d();
        public double Length => 0;
    }
    public class Polyline2d : Curve { }
    public class Polyline3d : Curve
    {
        public Polyline3d() { }
        public Polyline3d(Poly3dType tipo, Point3dCollection puntos, bool cerrada) { }
    }
    public enum Poly3dType { SimplePoly }
    public class SymbolTable : DBObject, IEnumerable
    {
        public bool Has(string nombre) => false;
        public ObjectId this[string nombre] => ObjectId.Null;
        public IEnumerator GetEnumerator() => new List<ObjectId>().GetEnumerator();
    }
    public class SymbolTableRecord : DBObject { public string Name { get; set; } }
    public class BlockTable : SymbolTable { }
    public class BlockTableRecord : SymbolTableRecord, IEnumerable
    {
        public static string ModelSpace => "*MODEL_SPACE";
        public ObjectId AppendEntity(Entity e) => ObjectId.Null;
        public new IEnumerator GetEnumerator() => new List<ObjectId>().GetEnumerator();
    }
    public class LayerTable : SymbolTable { public ObjectId Add(LayerTableRecord r) => ObjectId.Null; }
    public class LayerTableRecord : SymbolTableRecord { }
    public class Transaction : IDisposable
    {
        public DBObject GetObject(ObjectId id, OpenMode modo) => new DBObject();
        public DBObject GetObject(ObjectId id, OpenMode modo, bool openErased) => new DBObject();
        public void AddNewlyCreatedDBObject(DBObject o, bool add) { }
        public void Commit() { }
        public void Abort() { }
        public void Dispose() { }
    }
    public class TransactionManager { public Transaction StartTransaction() => new Transaction(); }
    public class Database
    {
        public string Filename => "";
        public ObjectId BlockTableId => ObjectId.Null;
        public ObjectId LayerTableId => ObjectId.Null;
        public ObjectId Clayer { get; set; }
        public SecurityParameters SecurityParameters => new SecurityParameters();
        public TransactionManager TransactionManager => new TransactionManager();
        public void SaveAs(string ruta, bool bBakAndRename, DwgVersion version, SecurityParameters sec) { }
        public bool TryGetObjectId(Handle h, out ObjectId id) { id = ObjectId.Null; return false; }
    }
}

namespace Autodesk.AutoCAD.EditorInput
{
    public class Editor { public void WriteMessage(string s) { } }
}

namespace Autodesk.AutoCAD.ApplicationServices
{
    using Autodesk.AutoCAD.DatabaseServices;

    public class CommandEventArgs : EventArgs { public string GlobalCommandName => ""; }
    public delegate void CommandEventHandler(object sender, CommandEventArgs e);
    public class DocumentLock : IDisposable { public void Dispose() { } }
    public class Document
    {
        public Database Database => new Database();
        public Autodesk.AutoCAD.EditorInput.Editor Editor => new Autodesk.AutoCAD.EditorInput.Editor();
        public string Name => "";
        public bool IsReadOnly => false;
        public bool IsActive => true;
        public DocumentLock LockDocument() => new DocumentLock();
        public void SendStringToExecute(string s, bool activate, bool wrapUpInactiveDoc, bool echo) { }
        public void StartUndoMark() { }
        public void EndUndoMark() { }
        public event CommandEventHandler CommandWillStart;
        public event CommandEventHandler CommandEnded;
        public event CommandEventHandler CommandCancelled;
        public event CommandEventHandler CommandFailed;
    }
    public class DocumentCollectionEventArgs : EventArgs { public Document Document => new Document(); }
    public delegate void DocumentCollectionEventHandler(object sender, DocumentCollectionEventArgs e);
    public class DocumentCollection : IEnumerable
    {
        public Document MdiActiveDocument { get; set; }
        public int Count => 0;
        public Document Open(string ruta, bool soloLectura) => new Document();
        public object ExecuteInCommandContextAsync(Func<object, Task> accion, object parametro) => Task.CompletedTask;
        public event DocumentCollectionEventHandler DocumentCreated;
        public event DocumentCollectionEventHandler DocumentToBeDestroyed;
        public IEnumerator GetEnumerator() => new List<Document>().GetEnumerator();
    }
    public class SystemVariableChangedEventArgs : EventArgs { public string Name => ""; public bool Changed => true; }
    public delegate void SystemVariableChangedEventHandler(object sender, SystemVariableChangedEventArgs e);
    public class Ventana { public IntPtr Handle => IntPtr.Zero; }
    public static class Application
    {
        public static DocumentCollection DocumentManager { get; } = new DocumentCollection();
        public static Ventana MainWindow => new Ventana();
        public static event EventHandler Idle;
        public static event SystemVariableChangedEventHandler SystemVariableChanged;
        public static object GetSystemVariable(string nombre) => 0;
        public static void SetSystemVariable(string nombre, object valor) { }
        public static void ShowAlertDialog(string texto) { }
    }
}

namespace Autodesk.Windows
{
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public class RibbonItemEventArgs : EventArgs { }
    public delegate void RibbonItemEventHandler(object sender, RibbonItemEventArgs e);
    public class RibbonItem { public string Id { get; set; } public string Name { get; set; } public string Text { get; set; } }
    public enum RibbonItemSize { Standard, Large }
    public class RibbonToolTip { public string Title { get; set; } public string Command { get; set; } public object Content { get; set; } public bool IsHelpEnabled { get; set; } }
    public class RibbonButton : RibbonItem
    {
        public bool ShowText { get; set; } public bool ShowImage { get; set; } public RibbonItemSize Size { get; set; }
        public System.Windows.Controls.Orientation Orientation { get; set; }
        public ImageSource Image { get; set; } public ImageSource LargeImage { get; set; }
        public object CommandParameter { get; set; } public System.Windows.Input.ICommand CommandHandler { get; set; }
        public object ToolTip { get; set; }
    }
    public class RibbonPanelSource { public string Id { get; set; } public string Title { get; set; } public string Name { get; set; } public List<RibbonItem> Items { get; } = new List<RibbonItem>(); }
    public class RibbonPanel { public RibbonPanelSource Source { get; set; } }
    public class RibbonTab { public string Id { get; set; } public string Title { get; set; } public string Name { get; set; } public bool IsVisible { get; set; } public List<RibbonPanel> Panels { get; } = new List<RibbonPanel>(); }
    public class RibbonControl { public List<RibbonTab> Tabs { get; } = new List<RibbonTab>(); }
    public static class ComponentManager
    {
        public static RibbonControl Ribbon => null;
        public static event RibbonItemEventHandler ItemInitialized;
    }
}
