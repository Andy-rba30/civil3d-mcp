using System.Linq;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// ejecutar_codigo con solo_lectura=true (1.17.6): lo que escribe no compila (primera capa; las demás, el bloqueo de
    /// lectura, la transacción abortada y la comprobación de después, solo se prueban en Civil 3D) y, sin solo_lectura,
    /// el mismo script compila igual que en la 1.17.5 y la escritura solo se cuenta. Con la biblioteca falsa de Autodesk
    /// (Scripts.FuenteAutodeskFalsa).
    /// </summary>
    public class CodigoSoloLecturaPruebas
    {
        private static ResultadoCompilacion SoloLectura(string codigo)
            => Scripts.Compilar(codigo, new OpcionesCompilacion { TipoFachada = Scripts.TipoFachada, SoloLectura = true });

        /// <summary>Con solo_lectura no compila (categoría «solo lectura»); sin ella compila y la escritura queda contada.</summary>
        private static ProblemaCodigo Bloqueado(string codigo, string contiene = null)
        {
            var r = SoloLectura(codigo);
            Assert.False(r.Ok, "Debía bloquearse con solo_lectura y compiló: " + codigo);
            var p = r.Problemas.FirstOrDefault(x => x.Categoria == CodigoReglas.SoloLectura);
            Assert.True(p != null, "Se esperaba un bloqueo de solo lectura. Mensaje: " + r.Mensaje);
            Assert.Contains("[bloqueado: solo lectura]", r.Mensaje);
            Assert.Contains("No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.", r.Mensaje);
            if (contiene != null) Assert.Contains(contiene, r.Mensaje);

            var normal = Scripts.Compilar(codigo);
            Assert.True(normal.Ok, "Sin solo_lectura debía compilar como en la 1.17.5: " + normal.Mensaje);
            Assert.NotEmpty(normal.Escrituras);
            Assert.All(normal.Escrituras, e => Assert.Equal(CodigoReglas.SoloLectura, e.Categoria));
            return p;
        }

        /// <summary>Compila con y sin solo_lectura, y no cuenta ninguna escritura.</summary>
        private static void Permitido(string codigo)
        {
            var r = SoloLectura(codigo);
            Assert.True(r.Ok, r.Mensaje);
            Assert.Empty(r.Escrituras);
            var normal = Scripts.Compilar(codigo);
            Assert.True(normal.Ok, normal.Mensaje);
            Assert.Empty(normal.Escrituras);
        }

        // ------------------------------------------------------------------ la Fachada
        [Fact]
        public void Fachada_AgregarYEscribirTextoNoCompilan()
        {
            var p = Bloqueado("var l = new Line(new Point3d(0, 0, 0), new Point3d(1, 0, 0));\nF.Agregar(l);\nreturn 1;", "F.Agregar");
            Assert.Equal(2, p.Linea);
            Assert.StartsWith("F.Agregar: con solo_lectura=true el script solo puede leer", p.Mensaje);
            Bloqueado("F.EscribirTexto(\"a.txt\", \"x\");\nreturn null;", "F.EscribirTexto");
        }

        [Fact]
        public void Fachada_AbrirSoloSinEscribir()
        {
            Bloqueado("var o = F.Abrir<Line>(default(ObjectId), true);\nreturn null;", "F.Abrir con 'escribir' distinto de false (true)");
            Bloqueado("var o = F.Abrir<Line>(default(ObjectId), escribir: true);\nreturn null;", "F.Abrir con 'escribir'");
            // Una variable no es la constante false: no se sabe hasta ejecutar
            Bloqueado("bool w = Parametros.ValueKind == System.Text.Json.JsonValueKind.Object;\nvar o = F.Abrir<Line>(default(ObjectId), w);\nreturn null;", "distinto de false (w)");
            Bloqueado("System.Func<object, bool, Line> abrir = F.Abrir<Line>;\nreturn null;", "F.Abrir usado sin llamarlo");

            Permitido("var a = F.Abrir<Line>(default(ObjectId));\nvar b = F.Abrir<Line>(default(ObjectId), false);\nvar c = F.Abrir<Line>(default(ObjectId), escribir: false);\nF.Escribir(\"leído\");\nreturn F.Doble(2);");
        }

        // ------------------------------------------------------------------ OpenMode
        [Fact]
        public void OpenMode_SoloLaConstanteForRead()
        {
            Bloqueado("Transaction tr = null;\nvar o = tr.GetObject(default(ObjectId), OpenMode.ForWrite);\nreturn null;", "OpenMode distinto de OpenMode.ForRead (OpenMode.ForWrite)");
            Bloqueado("Transaction tr = null;\nvar o = tr.GetObject(default(ObjectId), OpenMode.ForNotify);\nreturn null;", "(OpenMode.ForNotify)");
            Bloqueado("Transaction tr = null;\nvar o = tr.GetObject(default(ObjectId), (OpenMode)1);\nreturn null;", "((OpenMode)1)");
            Bloqueado("ObjectId id = default(ObjectId);\nvar o = id.GetObject(OpenMode.ForWrite);\nreturn null;", "OpenMode.ForWrite");
            // Variables y condiciones: no son la constante (aunque valgan ForRead)
            Bloqueado("Transaction tr = null;\nOpenMode m = OpenMode.ForRead;\nvar o = tr.GetObject(default(ObjectId), m);\nreturn null;", "(m)");
            Bloqueado("Transaction tr = null;\nbool w = false;\nvar o = tr.GetObject(default(ObjectId), w ? OpenMode.ForWrite : OpenMode.ForRead);\nreturn null;", "(w ? OpenMode.ForWrite : OpenMode.ForRead)");

            Permitido("Transaction tr = null;\nconst OpenMode lectura = OpenMode.ForRead;\nvar a = tr.GetObject(default(ObjectId), OpenMode.ForRead);\nvar b = tr.GetObject(default(ObjectId), lectura);\nvar c = tr.GetObject(default(ObjectId), (OpenMode.ForRead));\nvar d = tr.GetObject(default(ObjectId), default(OpenMode));\nreturn null;");
        }

        // ------------------------------------------------------------------ métodos y asignaciones que escriben
        [Fact]
        public void MetodosQueEscribenPorNombre()
        {
            Bloqueado("DBObject o = null;\no.UpgradeOpen();\nreturn null;", "Autodesk.AutoCAD.DatabaseServices.DBObject.UpgradeOpen");
            Bloqueado("DBObject o = null;\no.Erase();\nreturn null;", ".Erase");
            Bloqueado("BlockTableRecord b = null;\nb.AppendEntity(new Line());\nreturn null;", ".AppendEntity");
            Bloqueado("Transaction tr = null;\ntr.AddNewlyCreatedDBObject(new Line(), true);\nreturn null;", ".AddNewlyCreatedDBObject");
            // LockDocument pide bloqueo de escritura: se saltaría el bloqueo de lectura
            Bloqueado("Document d = null;\nusing (d.LockDocument()) { }\nreturn null;", ".LockDocument");
        }

        [Fact]
        public void MetodosQueEscribenPorPrefijoEnObjetosDelDibujoYDeCivil()
        {
            Bloqueado("Line l = null;\nl.SetDatabaseDefaults();\nreturn null;", ".SetDatabaseDefaults");
            Bloqueado("Autodesk.AutoCAD.EditorInput.Editor ed = null;\ned.SetImpliedSelection(null);\nreturn null;", ".SetImpliedSelection");
            Bloqueado("var id = Civ.Alignment.Create(null, \"Eje 1\", null, \"0\", \"Estilo\", \"Etiquetas\");\nreturn id;", "Autodesk.Civil.DatabaseServices.Alignment.Create");
            Bloqueado("Civ.Surface s = null;\ns.Rebuild();\nreturn null;", ".Rebuild");
            Bloqueado("Civ.SurfaceStyleCollection estilos = null;\nvar id = estilos.Add(\"Nuevo\");\nreturn id;", "SurfaceStyleCollection.Add");
        }

        [Fact]
        public void AsignarPropiedadesDeObjetosDelDibujo()
        {
            Bloqueado("Line l = null;\nl.Layer = \"0\";\nreturn null;", ".Layer (asignación)");
            // Line.StartPoint es de lectura y escritura (catálogo, 17/11/2026): asignarla cambia la línea
            Bloqueado("Line l = null;\nl.StartPoint = new Point3d(1, 2, 3);\nreturn null;", ".StartPoint (asignación)");
            Bloqueado("var l = new Line { Layer = \"0\" };\nreturn null;", ".Layer (asignación)");
            Bloqueado("Civ.Entity e = null;\ne.Name = \"X\";\nreturn null;", "Autodesk.Civil.DatabaseServices.Entity.Name (asignación)");
            // Leerlas sí vale
            Permitido("Line l = null;\nvar capa = l.Layer;\nvar p = l.StartPoint;\nCiv.Entity e = null;\nreturn new { capa, x = p.X, nombre = e.Name };");
        }

        // ------------------------------------------------------------------ auditoría de la ronda C
        [Fact]
        public void I3_GestoresQueNoSonDBObjectYMetodosSinApertura()
        {
            // El ejemplo literal del auditor
            Bloqueado("LayoutManager.Current.DeleteLayout(\"Presentación1\");\nreturn 1;", "LayoutManager.DeleteLayout");
            Bloqueado("var id = LayoutManager.Current.CreateLayout(\"Nueva\");\nreturn id;", "LayoutManager.CreateLayout");
            Bloqueado("LayoutManager.Current.CurrentLayout = \"Model\";\nreturn null;", "LayoutManager.CurrentLayout (asignación)");
            Bloqueado("Database db = null;\ndb.LayerStateManager.SaveLayerState(\"E\", 0, default(ObjectId));\nreturn null;", "LayerStateManager.SaveLayerState");
            Bloqueado("Database db = null;\ndb.LayerStateManager.DeleteLayerState(\"E\");\nreturn null;", "LayerStateManager.DeleteLayerState");
            Bloqueado("DBObject o = null;\nvar c = o.DeepClone(o, null, true);\nreturn null;", ".DeepClone");
            Bloqueado("DBObject o = null;\no.Close();\nreturn null;", ".Close");
            Bloqueado("DBObject o = null;\no.Cancel();\nreturn null;", ".Cancel");
            Bloqueado("DBObject o = null;\no.DowngradeOpen();\nreturn null;", ".DowngradeOpen");
            Bloqueado("DBObject o = null;\no.CopyFrom(null);\nreturn null;", ".CopyFrom");
            // Segunda revisión (n2): actualizar los campos escribe
            Bloqueado("Database db = null;\nvar n = db.EvaluateFields();\nreturn n;", ".EvaluateFields");
            // Leer de los gestores sigue valiendo
            Permitido("var n = LayoutManager.Current.LayoutCount;\nvar actual = LayoutManager.Current.CurrentLayout;\nDatabase db = null;\nvar hay = db.LayerStateManager.HasLayerState(\"E\");\nreturn new { n, actual, hay };");
        }

        [Fact]
        public void I4_OpenModeEscondidoEnTiposDelegadosYGenericos()
        {
            // Los dos ejemplos literales del auditor: compilaban con 0 escrituras reconocidas
            var zip = Bloqueado("Transaction tr = null; ObjectId id = default(ObjectId);\nvar o = new[] { id }.Zip(Enum.GetValues<OpenMode>().Skip(1), tr.GetObject).First();\nreturn null;");
            Assert.Equal(2, zip.Linea);
            var r = SoloLectura("Transaction tr = null; ObjectId id = default(ObjectId);\nvar o = new[] { id }.Zip(Enum.GetValues<OpenMode>().Skip(1), tr.GetObject).First();\nreturn null;");
            Assert.Contains("grupo de métodos (delegado) con un parámetro OpenMode: GetObject", r.Mensaje);
            Assert.Contains("OpenMode como argumento genérico de GetValues", r.Mensaje);
            Bloqueado("Transaction tr = null; ObjectId id = default(ObjectId);\nDBObject Abre<T>(Func<ObjectId, T, DBObject> f, ObjectId i, T[] m) => f(i, m[1]);\nvar o = Abre(tr.GetObject, id, Enum.GetValues<OpenMode>());\nreturn null;",
                "grupo de métodos (delegado) con un parámetro OpenMode: GetObject");
            // Tipos compuestos, typeof y Parse sin escribir ForWrite
            Bloqueado("OpenMode[] modos = null;\nreturn modos;", "tipo con OpenMode dentro (Autodesk.AutoCAD.DatabaseServices.OpenMode[])");
            Bloqueado("var l = new List<OpenMode>();\nreturn l.Count;", "tipo con OpenMode dentro");
            Bloqueado("Func<ObjectId, OpenMode, DBObject> f = null;\nreturn f;", "tipo con OpenMode dentro");
            Bloqueado("var t = typeof(OpenMode);\nreturn t.Name;", "typeof(OpenMode)");
            Bloqueado("var m = Enum.Parse<OpenMode>(\"ForWrite\");\nreturn null;", "OpenMode como argumento genérico de Parse");
            Bloqueado("Enum.TryParse<OpenMode>(\"ForWrite\", out var m);\nreturn null;", "OpenMode como argumento genérico de TryParse");
            // Llamar a GetObject con la constante ForRead sigue valiendo
            Permitido("Transaction tr = null; ObjectId id = default(ObjectId);\nvar o = new[] { id }.Select(x => tr.GetObject(x, OpenMode.ForRead)).First();\nreturn null;");
        }

        [Fact]
        public void I5_AsignacionPorDesestructuracion()
        {
            Bloqueado("Line l = null;\n(l.Layer, _) = (\"0\", 0);\nreturn null;", ".Layer (asignación)");
            Bloqueado("Line l = null; int x;\n((l.Layer, x), _) = ((\"0\", 1), 2);\nreturn null;", ".Layer (asignación)");
            // (foreach ((l.Layer, var n) in ...) no es C# válido: CS8186, el foreach tiene que declarar sus variables)
            // Segunda revisión (N1): con el destino entre paréntesis, en cualquier anidación y combinado con tuplas
            Bloqueado("Line l = null;\n(l.Layer) = \"0\";\nreturn null;", ".Layer (asignación)");
            Bloqueado("Line l = null;\n((l.Layer), _) = (\"0\", 0);\nreturn null;", ".Layer (asignación)");
            Bloqueado("Line l = null;\n(l.ColorIndex)++;\nreturn null;", ".ColorIndex (asignación)");
            Bloqueado("Line l = null;\n((l.ColorIndex)) += 1;\nreturn null;", ".ColorIndex (asignación)");
            Bloqueado("(LayoutManager.Current.CurrentLayout) = \"Model\";\nreturn null;", "LayoutManager.CurrentLayout (asignación)");
            // Leer entre paréntesis sigue valiendo
            Permitido("Line l = null;\nvar capa = (l.Layer);\nvar n = (l.ColorIndex) + 1;\nreturn (capa, n);");
            // Asignación condicional de C# 14 (a?.b = c): también es una asignación
            Bloqueado("Line l = null;\nl?.Layer = \"0\";\nreturn null;", ".Layer (asignación)");
            // Desestructurar en variables nuevas no escribe nada
            Permitido("Line l = null;\nvar (capa, largo) = (l.Layer, l.Length);\nreturn capa + largo;");
        }

        // ------------------------------------------------------------------ lo que solo vive en memoria y las lecturas
        [Fact]
        public void LoQueVivePorEnMemoriaYLasLecturasCompilan()
        {
            Permitido(@"Transaction tr = null;
int n = 0;
var o = tr.GetObject(default(ObjectId), OpenMode.ForRead);
if (o is Line l && l.Length > 0) n++;
var ids = new ObjectIdCollection();
ids.Add(default(ObjectId));
var ext = new Extents3d();
ext.AddExtents(ext);
ext.AddPoint(new Point3d(1, 2, 3));
var linea = new Line(new Point3d(0, 0, 0), new Point3d(1, 0, 0));
Civ.Surface s = null;
var g = s.GetGeneralProperties();
tr.Commit();
return new { n, ids.Count, largo = linea.Length, g };");
        }

        [Fact]
        public void UnaEscrituraQueLasReglasNoVenCompilaYLaParaElBloqueoEnEjecucion()
        {
            // Curve.Extend cambia la curva (catálogo, 17/11/2026) pero no está en las reglas: compila con solo_lectura y la
            // detiene Civil 3D al ejecutarse (objeto abierto para lectura). Es el paso (c) de VALIDACION_1176.
            Permitido("Transaction tr = null;\nvar l = (Line)tr.GetObject(default(ObjectId), OpenMode.ForRead);\nl.Extend(false, new Point3d(10, 0, 0));\nreturn null;");
        }

        [Fact]
        public void LasReglasGeneralesSiguenMandando()
        {
            // Con solo_lectura, lo que ya estaba bloqueado sigue con su categoría de siempre
            var r = SoloLectura("System.IO.File.WriteAllText(\"x.txt\", \"x\");\nreturn null;");
            Assert.False(r.Ok);
            Assert.Contains(r.Problemas, p => p.Categoria == CodigoReglas.Archivos);
            // Database.AddDBObject: bloqueado en todo modo desde 1.17.6 (regla general de transacción)
            var a = SoloLectura("Database db = null;\ndb.AddDBObject(new Line());\nreturn null;");
            Assert.False(a.Ok);
            Assert.Contains(a.Problemas, p => p.Categoria == CodigoReglas.Transaccion);
        }

        // ------------------------------------------------------------------ caché y puerta entre contextos
        [Fact]
        public void LaCacheDistingueSoloLectura()
        {
            string codigo = "var l = new Line();\nF.Agregar(l);\nreturn 7;   // caché solo lectura";
            var normal = Scripts.Compilar(codigo);
            Assert.True(normal.Ok, normal.Mensaje);
            var conSoloLectura = SoloLectura(codigo);
            Assert.False(conSoloLectura.Ok);
            Assert.False(conSoloLectura.DeCache);
            var otraVez = Scripts.Compilar(codigo);
            Assert.True(otraVez.Ok);
            Assert.True(otraVez.DeCache);
            Assert.Single(otraVez.Escrituras);
        }

        [Fact]
        public void LaPuertaPasaSoloLecturaYDevuelveLasEscrituras()
        {
            string codigo = "F.EscribirTexto(\"a.txt\", \"x\");\nreturn 1;   // puerta";
            var con = ResultadoAislado.DesdeArreglo(CodigoPuerta.Compilar(codigo, Scripts.RutasReferencias(), Scripts.TipoFachada, true));
            Assert.False(con.Ok);
            Assert.Contains("[bloqueado: solo lectura] F.EscribirTexto", con.Mensaje);
            Assert.Single(con.Escrituras);

            var sin = ResultadoAislado.DesdeArreglo(CodigoPuerta.Compilar(codigo, Scripts.RutasReferencias(), Scripts.TipoFachada, false));
            Assert.True(sin.Ok, sin.Mensaje);
            Assert.Single(sin.Escrituras);
            Assert.Contains("Línea 1, columna 3: [bloqueado: solo lectura] F.EscribirTexto", sin.Escrituras[0]);   // la columna del nombre, como los demás bloqueos
        }

        [Fact]
        public void UnArregloDeLa1175SinEscriturasSigueSiendoLegible()
        {
            var a = new object[] { true, new byte[0], new byte[0], null, new string[0], 1L, false, 1, "T", "M", "N", new string[0] };
            var r = ResultadoAislado.DesdeArreglo(a);
            Assert.True(r.Ok);
            Assert.Empty(r.Escrituras);
        }

        // ------------------------------------------------------------------ CodigoReglas
        [Fact]
        public void PrefijoComoPalabra()
        {
            Assert.True(CodigoReglas.EmpiezaPorPalabra("SetTargets", "Set"));
            Assert.True(CodigoReglas.EmpiezaPorPalabra("Add", "Add"));
            Assert.True(CodigoReglas.EmpiezaPorPalabra("Set_X", "Set"));
            Assert.True(CodigoReglas.EmpiezaPorPalabra("Add3dPoint", "Add"));
            Assert.False(CodigoReglas.EmpiezaPorPalabra("Settings", "Set"));
            Assert.False(CodigoReglas.EmpiezaPorPalabra("Address", "Add"));
            Assert.False(CodigoReglas.EmpiezaPorPalabra(null, "Add"));
        }

        [Fact]
        public void CategoriaSoloLecturaSegunElTipo()
        {
            string[] linea = { "Autodesk.AutoCAD.DatabaseServices.Line", "Autodesk.AutoCAD.DatabaseServices.Curve", "Autodesk.AutoCAD.DatabaseServices.Entity", "Autodesk.AutoCAD.DatabaseServices.DBObject" };
            Assert.Equal(CodigoReglas.SoloLectura, CodigoReglas.CategoriaSoloLectura(linea, "SetDatabaseDefaults", false, true));
            Assert.Equal(CodigoReglas.SoloLectura, CodigoReglas.CategoriaSoloLectura(linea, "Layer", true, false));
            Assert.Null(CodigoReglas.CategoriaSoloLectura(linea, "Layer", false, false));
            Assert.Null(CodigoReglas.CategoriaSoloLectura(linea, "GetPointAtParameter", false, true));
            Assert.Null(CodigoReglas.CategoriaSoloLectura(linea, "Extend", false, true));

            // En memoria: ObjectIdCollection y la geometría no son del dibujo
            Assert.Null(CodigoReglas.CategoriaSoloLectura(new[] { "Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection" }, "Add", false, true));
            Assert.Null(CodigoReglas.CategoriaSoloLectura(new[] { "Autodesk.AutoCAD.Geometry.Point3dCollection" }, "Add", false, true));
            // Por nombre, en cualquier tipo de Autodesk
            Assert.Equal(CodigoReglas.SoloLectura, CodigoReglas.CategoriaSoloLectura(new[] { "Autodesk.AutoCAD.DatabaseServices.Transaction" }, "AddNewlyCreatedDBObject", false, true));
            Assert.Equal(CodigoReglas.SoloLectura, CodigoReglas.CategoriaSoloLectura(new[] { "Autodesk.AutoCAD.ApplicationServices.Document" }, "LockDocument", false, true));
            // Civil 3D: cualquier tipo
            Assert.Equal(CodigoReglas.SoloLectura, CodigoReglas.CategoriaSoloLectura(new[] { "Autodesk.Civil.DatabaseServices.Styles.SurfaceStyleCollection" }, "Add", false, true));
            Assert.Null(CodigoReglas.CategoriaSoloLectura(new[] { "Autodesk.Civil.DatabaseServices.Alignment" }, "PointLocation", false, true));
            // Lo que no es de Autodesk no cuenta
            Assert.Null(CodigoReglas.CategoriaSoloLectura(new[] { "System.Collections.Generic.List" }, "Add", false, true));
        }

        // ------------------------------------------------------------------ CodigoSoloLectura (textos de la respuesta)
        [Fact]
        public void ReconoceLosIntentosDeEscribir()
        {
            Assert.True(CodigoSoloLectura.EsIntentoDeEscritura("eLockViolation"));
            Assert.True(CodigoSoloLectura.EsIntentoDeEscritura("eNotOpenForWrite"));
            Assert.True(CodigoSoloLectura.EsIntentoDeEscritura("Autodesk.AutoCAD.Runtime.Exception: eWasOpenedForRead"));
            Assert.True(CodigoSoloLectura.EsIntentoDeEscritura(CodigoSoloLectura.MensajeFachada("F.Agregar")));
            Assert.False(CodigoSoloLectura.EsIntentoDeEscritura("eInvalidInput"));
            Assert.False(CodigoSoloLectura.EsIntentoDeEscritura(null));
        }

        [Fact]
        public void MensajesDeError()
        {
            string intento = CodigoSoloLectura.MensajeIntento(3, "Exception", "eNotOpenForWrite");
            Assert.Equal("Con solo_lectura=true el script intentó escribir en la línea 3 y se impidió (Exception: eNotOpenForWrite)."
                + " No se tocó el dibujo: solo lectura (dibujo bloqueado para lectura y transacción abortada), sin copia de seguridad y sin entrada en Deshacer."
                + " Si el script necesita escribir, quita solo_lectura y simula primero (simular=true).", intento);
            Assert.StartsWith("solo_lectura=true: F.Agregar escribe en el dibujo o en un archivo", CodigoSoloLectura.MensajeFachada("F.Agregar"));

            Assert.Null(CodigoSoloLectura.MensajeCambios(0, 0, 0, 0, null));
            string cambios = CodigoSoloLectura.MensajeCambios(1, 2, 0, 0, new[] { "Line 2A3" });
            Assert.Contains("se detectaron objetos 1 creado(s), 2 modificado(s) (Line 2A3)", cambios);
            Assert.Contains("La transacción se abortó: no se aplicó nada", cambios);

            Assert.Null(CodigoSoloLectura.MensajeTrasCerrar(0, 0, null));
            Assert.Null(CodigoSoloLectura.MensajeTrasCerrar(null, 1, new string[0]));
            string tras = CodigoSoloLectura.MensajeTrasCerrar(0, 1, new[] { "Line 2A4" });
            Assert.StartsWith("ATENCIÓN: en un script de solo lectura DBMOD pasó de 0 a 1", tras);
            Assert.Contains("1 objeto(s) nuevo(s) al abortar (Line 2A4)", tras);
            Assert.Contains("esta llamada no hizo copia de seguridad", tras);

            Assert.StartsWith("Ejecutar un script de solo lectura de 4 línea(s)", CodigoSoloLectura.Accion(4));
        }

        [Fact]
        public void SugiereSoloLecturaSoloSiNoEscribeNiCambiaNada()
        {
            Assert.Contains("pásalo con solo_lectura=true", CodigoSoloLectura.AvisoSugerencia(false, 0, 0, 0, 0, 0));
            Assert.Null(CodigoSoloLectura.AvisoSugerencia(true, 0, 0, 0, 0, 0));    // ya es de solo lectura
            Assert.Null(CodigoSoloLectura.AvisoSugerencia(false, 1, 0, 0, 0, 0));   // usa algo que escribe
            Assert.Null(CodigoSoloLectura.AvisoSugerencia(false, 0, 1, 0, 0, 0));   // creó algo
            Assert.Null(CodigoSoloLectura.AvisoSugerencia(false, 0, 0, 0, 0, 1));   // temporales
        }
    }
}
