using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// Lista blanca de ejecutar_codigo (1.16.0): lo que sale de Civil 3D, se salta la transacción o puede colgarlo no
    /// compila, y el error lo dice. Corre en la nube con una biblioteca falsa de Autodesk (Scripts.FuenteAutodeskFalsa).
    /// </summary>
    public class CodigoRevisorPruebas
    {
        // ------------------------------------------------------------------ lo permitido
        [Fact]
        public void Permitido_ScriptBasicoConAutoCadLinqTextoYPath()
        {
            Scripts.CompilarBien(@"using System.IO;
var l = new Line(new Point3d(0, 0, 0), new Point3d(10, 0, 0));
var pares = new List<int> { 1, 2, 3, 4 }.Where(x => x % 2 == 0).ToList();
var sb = new StringBuilder();
sb.Append(Path.Combine(""a"", ""b"")).Append(Environment.NewLine);
var t = typeof(Line);
F.Escribir(""hola"");
return new { n = pares.Count, texto = sb.ToString(), largo = l.Length, tipo = t.Name, doble = F.Doble(2), cultura = 1.5.ToString(CultureInfo.InvariantCulture) };");
        }

        [Fact]
        public void Permitido_StopwatchJsonYTuplas()
        {
            Scripts.CompilarBien(@"var s = System.Diagnostics.Stopwatch.StartNew();
string nombre = Parametros.TryGetProperty(""nombre"", out var v) ? v.GetString() : ""Eje 1"";
var nodo = System.Text.Json.Nodes.JsonNode.Parse(""{\""a\"":1}"");
(int a, string b) par = (1, nombre);
return new { ms = s.ElapsedMilliseconds, par.a, par.b, json = nodo.ToJsonString() };");
        }

        [Fact]
        public void Permitido_ApiDeCivilConNombresQueNoSonArchivos()
        {
            // 'profileName' y 'alignmentName' no son archivos aunque contengan 'file'/'name'
            Scripts.CompilarBien(@"var idEje = Civ.Alignment.Create(null, ""Eje 1"", null, ""0"", ""Estilo"", ""Etiquetas"");
var idPerfil = Civ.Profile.CreateByLayout(""Rasante"", idEje, ""0"");
Database db = null;
db.Insert(""Bloque"", null, false);
return idPerfil.IsNull;");
        }

        [Fact]
        public void Permitido_IntPtrZeroEnIntersectWithYLecturaDeMdiActiveDocument()
        {
            Scripts.CompilarBien(@"Entity a = null, b = null;
a.IntersectWith(b, 0, null, IntPtr.Zero, IntPtr.Zero);
var doc = AcApp.DocumentManager.MdiActiveDocument;
var db = HostApplicationServices.WorkingDatabase;
var v = AcApp.GetSystemVariable(""CMDACTIVE"");
var vista = doc.Editor.GetCurrentView();
var todo = doc.Editor.SelectAll();
doc.Editor.WriteMessage(""hola"");
return v;");
        }

        [Fact]
        public void Permitido_ElVigilanteSoloConRevisar()
        {
            Scripts.CompilarBien("ArbaMcp.Nucleo.CodigoVigilante.Revisar(); return 1;");
            Scripts.Bloqueado("ArbaMcp.Nucleo.CodigoVigilante.Detener(\"x\"); return 1;", CodigoReglas.Plugin);
        }

        // ------------------------------------------------------------------ red, procesos, archivos
        [Fact]
        public void Red_SystemNetNoCompilaYDiceLaLinea()
        {
            var p = Scripts.Bloqueado("var a = 1;\nvar c = new System.Net.Http.HttpClient();\nreturn c;", CodigoReglas.Red);
            Assert.Equal(2, p.Linea);
            Assert.Contains("System.Net.Http.HttpClient", p.Mensaje);
            Assert.Contains("red", p.Mensaje);
        }

        [Fact]
        public void Red_UnUsingDeSystemNetNoCompilaAunqueNoSeUse()
        {
            var p = Scripts.Bloqueado("using System.Net;\nreturn 1;", CodigoReglas.Red);
            Assert.Equal(1, p.Linea);
            Assert.Contains("using System.Net", p.Mensaje);
        }

        [Fact]
        public void Red_AliasDeUnTipoDeRedNoCompila()
            => Scripts.Bloqueado("using W = System.Net.WebClient;\nreturn 1;", CodigoReglas.Red);

        [Fact]
        public void Red_GetRemoteFileDeAutoCad()
            => Scripts.Bloqueado("return HostApplicationServices.Current.GetRemoteFile(null, false);", CodigoReglas.Red);

        [Fact]
        public void Procesos_ProcessStart()
            => Scripts.Bloqueado("System.Diagnostics.Process.Start(\"notepad.exe\");\nreturn null;", CodigoReglas.Procesos);

        [Fact]
        public void Procesos_ComDeAutoCad()
            => Scripts.Bloqueado("var a = new Autodesk.AutoCAD.Interop.AcadApplication(); return a;", CodigoReglas.Procesos);

        [Fact]
        public void Archivos_FileFueraDeLaCarpetaYDiceQueUsar()
        {
            var p = Scripts.Bloqueado("System.IO.File.WriteAllText(@\"C:\\Proyectos\\x.txt\", \"x\");\nreturn null;", CodigoReglas.Archivos);
            Assert.Contains("F.EscribirTexto", p.Mensaje);
            Assert.Contains("%LOCALAPPDATA%\\ArbaMcp\\scripts", p.Mensaje);
        }

        [Fact]
        public void Archivos_StreamWriterDirectoryYTempFile()
        {
            Scripts.Bloqueado("using (var w = new System.IO.StreamWriter(\"x.txt\")) { }\nreturn null;", CodigoReglas.Archivos);
            Scripts.Bloqueado("return System.IO.Directory.GetFiles(\"C:\\\\\");", CodigoReglas.Archivos);
            Scripts.Bloqueado("return System.IO.Path.GetTempFileName();", CodigoReglas.Archivos);
        }

        [Fact]
        public void Documentos_ApiDeAutodeskConArchivos()
        {
            var p = Scripts.Bloqueado("Civ.TinSurface s = null;\ns.AddPointFile(\"C:\\\\x.txt\", 0);\nreturn null;", CodigoReglas.Documentos);
            Assert.Contains("pointFilePath", p.Mensaje);
            Scripts.Bloqueado("Database db = null; db.SaveAs(\"C:\\\\x.dwg\", false); return null;", CodigoReglas.Documentos);
            Scripts.Bloqueado("Database db = null; db.ReadDwgFile(\"C:\\\\x.dwg\", 0, false, null); return null;", CodigoReglas.Documentos);
            Scripts.Bloqueado("return AcApp.DocumentManager.Open(\"C:\\\\x.dwg\", false);", CodigoReglas.Documentos);
            Scripts.Bloqueado("Document d = null; d.CloseAndDiscard(); return null;", CodigoReglas.Documentos);
        }

        [Fact]
        public void Documentos_AsignarUnaRutaAUnaPropiedadDeLaApi()
        {
            var p = Scripts.Bloqueado("RasterImageDef d = null;\nd.SourceFileName = @\"C:\\x.png\";\nreturn null;", CodigoReglas.Documentos);
            Assert.Equal(2, p.Linea);
            Scripts.Bloqueado("BlockTableRecord b = null; b.PathName = \"x.dwg\"; return null;", CodigoReglas.Documentos);
            // Leer la ruta o asignar un nombre que no es de archivo sí vale
            Scripts.CompilarBien("RasterImageDef d = null; BlockTableRecord b = null; b.Name = \"Bloque\"; return d.SourceFileName + b.PathName;");
        }

        [Fact]
        public void Documentos_AsignarDibujoActivoOBaseDeTrabajo()
        {
            var p = Scripts.Bloqueado("AcApp.DocumentManager.MdiActiveDocument = null; return null;", CodigoReglas.Documentos);
            Assert.Contains("(asignación)", p.Mensaje);
            Scripts.Bloqueado("HostApplicationServices.WorkingDatabase = null; return null;", CodigoReglas.Documentos);
        }

        // ------------------------------------------------------------------ reflexión, proceso, nativo, hilos
        [Fact]
        public void Reflexion_TypeGetMethodAssemblyActivatorYTypeGetType()
        {
            Scripts.Bloqueado("var m = typeof(string).GetMethod(\"Trim\", new Type[0]); return m;", CodigoReglas.Reflexion);
            Scripts.Bloqueado("return typeof(string).Assembly.FullName;", CodigoReglas.Reflexion);
            Scripts.Bloqueado("return Activator.CreateInstance(typeof(object));", CodigoReglas.Reflexion);
            Scripts.Bloqueado("return Type.GetType(\"System.String\");", CodigoReglas.Reflexion);
            Scripts.Bloqueado("Action a = () => { }; return a.Method;", CodigoReglas.Reflexion);
        }

        [Fact]
        public void Reflexion_DynamicExpresionesYAssemblyLoadContext()
        {
            Scripts.Bloqueado("dynamic d = 1; return d;", CodigoReglas.Reflexion);
            Scripts.Bloqueado("System.Linq.Expressions.Expression<Func<int>> e = () => 1; return e;", CodigoReglas.Reflexion);
            Scripts.Bloqueado("return new List<int>().AsQueryable();", CodigoReglas.Reflexion);
            Scripts.Bloqueado("return System.Runtime.Loader.AssemblyLoadContext.Default;", CodigoReglas.Reflexion);
        }

        [Fact]
        public void Proceso_EnvironmentExitVariablesGcYConsola()
        {
            Scripts.Bloqueado("Environment.Exit(0); return null;", CodigoReglas.Proceso);
            Scripts.Bloqueado("return Environment.GetEnvironmentVariable(\"PATH\");", CodigoReglas.Proceso);
            Scripts.Bloqueado("GC.Collect(); return null;", CodigoReglas.Proceso);
            Scripts.Bloqueado("Console.WriteLine(\"x\"); return null;", CodigoReglas.Proceso);
            Scripts.Bloqueado("return AppDomain.CurrentDomain;", CodigoReglas.Proceso);
            Scripts.Bloqueado("Autodesk.AutoCAD.ApplicationServices.Core.Application.Quit(); return null;", CodigoReglas.Proceso);
            // Dispose está declarado en DisposableWrapper, pero sobre una Database cuenta como Database.Dispose
            Scripts.Bloqueado("Database db = null; db.Dispose(); return null;", CodigoReglas.Proceso);
            Scripts.CompilarBien("Database db = null; using (var tr = db.TransactionManager.StartTransaction()) { } return null;");
        }

        [Fact]
        public void Nativo_UnsafeDllImportPunterosMarshalYRxObjectCreate()
        {
            Scripts.Bloqueado("unsafe { int x = 1; int* p = &x; }\nreturn null;", CodigoReglas.Nativo);
            Scripts.Bloqueado("[System.Runtime.InteropServices.DllImport(\"kernel32.dll\")] static extern int GetTickCount();\nreturn GetTickCount();", CodigoReglas.Nativo);
            Scripts.Bloqueado("return System.Runtime.InteropServices.Marshal.AllocHGlobal(10);", CodigoReglas.Nativo);
            Scripts.Bloqueado("return Autodesk.AutoCAD.Runtime.RXObject.Create(IntPtr.Zero, false);", CodigoReglas.Nativo);
            Scripts.Bloqueado("return Autodesk.AutoCAD.Runtime.SystemObjects.DynamicLinker;", CodigoReglas.Nativo);
        }

        [Fact]
        public void Nativo_IntPtrDistintoDeCeroOPunteroDevuelto()
        {
            Scripts.Bloqueado("return new IntPtr(5);", CodigoReglas.Nativo);
            Scripts.Bloqueado("Entity a = null, b = null;\na.IntersectWith(b, 0, null, new IntPtr(1), IntPtr.Zero);\nreturn null;", CodigoReglas.Nativo);
            Scripts.Bloqueado("Entity a = null;\nreturn a.UnmanagedObject;", CodigoReglas.Nativo);
        }

        [Fact]
        public void Hilos_ThreadTaskAsyncLockYParalelo()
        {
            Scripts.Bloqueado("var t = new System.Threading.Thread(() => { }); t.Start(); return null;", CodigoReglas.Hilos);
            Scripts.Bloqueado("System.Threading.Tasks.Task.Run(() => 1); return null;", CodigoReglas.Hilos);
            Scripts.Bloqueado("async void f() { } f(); return null;", CodigoReglas.Hilos);
            Scripts.Bloqueado("var o = new object();\nlock (o) { }\nreturn null;", CodigoReglas.Hilos);
            Scripts.Bloqueado("return new List<int>().AsParallel().Count();", CodigoReglas.Hilos);
            Scripts.Bloqueado("System.Threading.Thread.Sleep(10); return null;", CodigoReglas.Hilos);
        }

        // ------------------------------------------------------------------ Civil 3D: comandos, interfaz, transacción, eventos, regla del proyecto
        [Fact]
        public void Comandos_SendStringToExecuteEditorCommandYLisp()
        {
            var p = Scripts.Bloqueado("Document d = null;\nd.SendStringToExecute(\"_.LINE \", true, false, false);\nreturn null;", CodigoReglas.Comandos);
            Assert.Equal(2, p.Linea);
            Assert.Contains("ejecutar_comando", p.Mensaje);
            Scripts.Bloqueado("Document d = null; d.Editor.Command(\"_.LINE\"); return null;", CodigoReglas.Comandos);
            Scripts.Bloqueado("return Autodesk.AutoCAD.ApplicationServices.Core.Application.Invoke(null);", CodigoReglas.Comandos);
        }

        [Fact]
        public void Interfaz_EntradaInteractivaDialogosYVentanas()
        {
            Scripts.Bloqueado("Document d = null; return d.Editor.GetPoint(\"Punto\");", CodigoReglas.Interfaz);
            Scripts.Bloqueado("AcApp.ShowAlertDialog(\"hola\"); return null;", CodigoReglas.Interfaz);
            Scripts.Bloqueado("return new Autodesk.AutoCAD.Windows.OpenFileDialog();", CodigoReglas.Interfaz);
        }

        [Fact]
        public void Sistema_CambiarVariablesDeSistemaODelDibujo()
        {
            Scripts.Bloqueado("AcApp.SetSystemVariable(\"PDMODE\", 3); return null;", CodigoReglas.Sistema);
            var p = Scripts.Bloqueado("Database db = null; db.Clayer = default(ObjectId); return null;", CodigoReglas.Sistema);
            Assert.Contains("(asignación)", p.Mensaje);
            Scripts.CompilarBien("Database db = null; return db.Clayer;");
        }

        [Fact]
        public void Transaccion_OpenCloseTopTransactionYObjectIdOpen()
        {
            Scripts.Bloqueado("Database db = null; return db.TransactionManager.StartOpenCloseTransaction();", CodigoReglas.Transaccion);
            Scripts.Bloqueado("Database db = null; return db.TransactionManager.TopTransaction;", CodigoReglas.Transaccion);
            Scripts.Bloqueado("ObjectId id = default; return id.Open(OpenMode.ForWrite);", CodigoReglas.Transaccion);
            Scripts.CompilarBien("Database db = null; using (var tr = db.TransactionManager.StartTransaction()) { tr.Commit(); } ObjectId id = default; return id.GetObject(OpenMode.ForRead);");
        }

        [Fact]
        public void Eventos_SuscribirseNoCompila()
        {
            Scripts.Bloqueado("AcApp.Idle += (s, e) => { };\nreturn null;", CodigoReglas.Eventos);
            Scripts.Bloqueado("Database db = null; db.ObjectAppended += (s, e) => { }; return null;", CodigoReglas.Eventos);
        }

        [Fact]
        public void Borrado_QuitarRegionesNoCompila()
        {
            var p = Scripts.Bloqueado("Civ.BaselineRegionCollection r = null; r.RemoveAt(0); return null;", CodigoReglas.Borrado);
            Assert.Contains("regiones", p.Mensaje);
        }

        // ------------------------------------------------------------------ plugin y sintaxis
        [Fact]
        public void Plugin_SoloLaFachada()
        {
            Scripts.Bloqueado("return ArbaMcp.Nucleo.Json.Serializar(1);", CodigoReglas.Plugin);
            Scripts.CompilarBien("F.Escribir(\"x\"); return F.Doble(3);");
        }

        [Fact]
        public void Sintaxis_GotoLineYNombresReservados()
        {
            Scripts.Bloqueado("inicio:\ngoto inicio;", CodigoReglas.Sintaxis);
            Scripts.Bloqueado("#line 100\nreturn 1;", CodigoReglas.Sintaxis);
            Scripts.Bloqueado("var __arbaX = 1; return __arbaX;", CodigoReglas.Sintaxis);
        }

        [Fact]
        public void ListaBlanca_LoQueNoEsBasicoTampoco()
        {
            Scripts.Bloqueado("return System.Text.RegularExpressions.Regex.IsMatch(\"a\", \"a\");", CodigoReglas.ListaBlanca);
            Scripts.Bloqueado("return new System.Collections.Concurrent.ConcurrentBag<int>();", CodigoReglas.ListaBlanca);
        }

        [Fact]
        public void Mensaje_TieneCabeceraYPieParaElAgente()
        {
            var r = Scripts.Compilar("return new System.Net.Http.HttpClient();");
            Assert.StartsWith("ejecutar_codigo: el código no se puede ejecutar (1 uso no permitido)", r.Mensaje);
            Assert.Contains("No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.", r.Mensaje);
            Assert.Contains("Línea 1, columna", r.Mensaje);
            Assert.Contains("F es la Fachada", r.Mensaje);
        }

        // ------------------------------------------------------------------ reglas sin Roslyn
        [Theory]
        [InlineData("fileName", true)]
        [InlineData("filename", true)]
        [InlineData("pointFilePath", true)]
        [InlineData("url", true)]
        [InlineData("xmlFolder", true)]
        [InlineData("profileName", false)]
        [InlineData("alignmentName", false)]
        [InlineData("styleName", false)]
        [InlineData("blockName", false)]
        public void Reglas_ParametroDeArchivoPorPalabras(string nombre, bool esArchivo)
            => Assert.Equal(esArchivo, CodigoReglas.EsParametroDeArchivo(nombre));

        [Fact]
        public void Reglas_PalabrasDeUnIdentificador()
            => Assert.Equal(new[] { "XML", "File", "Name" }, CodigoReglas.Palabras("XMLFileName"));

        [Theory]
        [InlineData("System", null)]
        [InlineData("System.Linq", null)]
        [InlineData("System.IO", null)]
        [InlineData("System.IO.Compression", CodigoReglas.Archivos)]
        [InlineData("System.Net.Http", CodigoReglas.Red)]
        [InlineData("System.Runtime.InteropServices", CodigoReglas.Nativo)]
        [InlineData("System.Runtime.CompilerServices", CodigoReglas.Reflexion)]
        [InlineData("System.Threading.Tasks", CodigoReglas.Hilos)]
        [InlineData("Autodesk.AutoCAD.DatabaseServices", null)]
        [InlineData("Autodesk.Civil.DatabaseServices.Styles", null)]
        [InlineData("Autodesk.AutoCAD.Windows", CodigoReglas.Interfaz)]
        [InlineData("Autodesk.Windows", CodigoReglas.Interfaz)]
        [InlineData("Autodesk.AutoCAD.Interop.Common", CodigoReglas.Procesos)]
        [InlineData("Autodesk.Civil.Windows", CodigoReglas.Interfaz)]
        [InlineData("Microsoft.Win32", CodigoReglas.Proceso)]
        [InlineData("Newtonsoft.Json", CodigoReglas.ListaBlanca)]
        public void Reglas_EspaciosDeNombres(string espacio, string categoria)
            => Assert.Equal(categoria, CodigoReglas.CategoriaEspacio(espacio));
    }
}
