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
}
namespace Autodesk.AutoCAD.DatabaseServices
{
    public enum OpenMode { ForRead, ForWrite }
    public struct ObjectId
    {
        public bool IsNull => true;
        public DBObject GetObject(OpenMode modo) => null;
        public DBObject Open(OpenMode modo) => null;
    }
    public class DBObject : Autodesk.AutoCAD.Runtime.RXObject
    {
        public ObjectId ObjectId => default(ObjectId);
        public void Erase() { }
    }
    public class Entity : DBObject
    {
        public string Layer { get; set; }
        public void IntersectWith(Entity otra, int modo, object puntos, System.IntPtr thisGraphicSystemMarker, System.IntPtr otherGraphicSystemMarker) { }
    }
    public class Curve : Entity { }
    public class Line : Curve
    {
        public Line() { }
        public Line(Autodesk.AutoCAD.Geometry.Point3d inicio, Autodesk.AutoCAD.Geometry.Point3d fin) { }
        public double Length => 0;
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
        public Transaction StartTransaction() => null;
        public Transaction StartOpenCloseTransaction() => null;
        public Transaction TopTransaction => null;
    }
    public class ObjectEventArgs : System.EventArgs { public DBObject DBObject => null; }
    public delegate void ObjectEventHandler(object sender, ObjectEventArgs e);
    public class Database : DBObject
    {
        public TransactionManager TransactionManager => null;
        public void SaveAs(string fileName, bool bakAndRename) { }
        public void ReadDwgFile(string fileName, int mode, bool allowCPConversion, string password) { }
        public void Insert(string blockName, Database dataBase, bool preserveSourceDatabase) { }
        public event ObjectEventHandler ObjectAppended;
    }
    public class HostApplicationServices
    {
        public static HostApplicationServices Current => null;
        public static Database WorkingDatabase { get; set; }
        public string GetRemoteFile(System.Uri url, bool ignoreCache) => null;
    }
}
namespace Autodesk.AutoCAD.EditorInput
{
    public class Editor
    {
        public void WriteMessage(string mensaje) { }
        public object GetPoint(string mensaje) => null;
        public object GetCurrentView() => null;
        public object SelectAll() => null;
        public void Command(params object[] parametros) { }
    }
}
namespace Autodesk.AutoCAD.ApplicationServices
{
    public class Document
    {
        public Autodesk.AutoCAD.DatabaseServices.Database Database => null;
        public Autodesk.AutoCAD.EditorInput.Editor Editor => null;
        public void SendStringToExecute(string comando, bool activate, bool wrapUpInactiveDoc, bool echoCommand) { }
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
    }
    public class Profile : Entity
    {
        public static Autodesk.AutoCAD.DatabaseServices.ObjectId CreateByLayout(string profileName, Autodesk.AutoCAD.DatabaseServices.ObjectId alignmentId, string layerName) => default(Autodesk.AutoCAD.DatabaseServices.ObjectId);
    }
    public class Surface : Entity { }
    public class TinSurface : Surface { public void AddPointFile(string pointFilePath, int formato) { } }
    public class BaselineRegionCollection
    {
        public int Count => 0;
        public void RemoveAt(int indice) { }
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

        public static ResultadoCompilacion Compilar(string codigo) => CodigoCompilador.Compilar(codigo, Refs.Value, Opciones());

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
