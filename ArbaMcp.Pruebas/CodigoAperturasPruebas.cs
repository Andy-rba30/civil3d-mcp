using System.Linq;
using ArbaMcp.Nucleo;
using Xunit;

namespace ArbaMcp.Pruebas
{
    /// <summary>
    /// ejecutar_codigo sin solo_lectura (1.19.2): un cambio sobre un objeto que el script abrió para lectura no compila
    /// (CodigoAperturas, categoría «abierto para lectura»), porque Curve.Extend sobre una línea abierta para lectura cerró
    /// Civil 3D (17/11/2026, VALIDACION_1176, paso 9). Lo que no se puede saber se deja pasar, y con solo_lectura todo
    /// sigue como en la 1.19.1. Con la Fachada de firmas reales de la biblioteca falsa (Scripts.CompilarConFachadaReal),
    /// para compilar literales los scripts de las validaciones y la receta crear_capa_con_color.
    /// </summary>
    public class CodigoAperturasPruebas
    {
        private const string Cabecera = "ejecutar_codigo: el código no se puede ejecutar (1 uso no permitido). No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.";

        /// <summary>El script del paso 9 de VALIDACION_1176, literal (allí con solo_lectura; aquí también sin ella).</summary>
        private const string Paso9 = "Line l = null;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) l = x;\nif (l == null) return \"sin líneas\";\nl.Extend(false, new Point3d(l.EndPoint.X + 50, l.EndPoint.Y, 0));\nreturn l.Length;";

        private static ResultadoCompilacion Compilar(string codigo, bool soloLectura = false) => Scripts.CompilarConFachadaReal(codigo, soloLectura);

        /// <summary>
        /// Sin solo_lectura no compila por «abierto para lectura» (con el mensaje que dice cómo corregirlo); con solo_lectura
        /// tampoco, pero por su regla de la 1.19.1 («solo lectura»), nunca por esta.
        /// </summary>
        private static ProblemaCodigo Rechazado(string codigo, string contiene = null)
        {
            var r = Compilar(codigo);
            Assert.False(r.Ok, "Debía rechazarse (abierto para lectura) y compiló: " + codigo);
            var p = r.Problemas.FirstOrDefault(x => x.Categoria == CodigoReglas.AbiertoParaLectura);
            Assert.True(p != null, "Se esperaba un rechazo «abierto para lectura». Mensaje: " + r.Mensaje);
            Assert.Contains("[bloqueado: abierto para lectura]", r.Mensaje);
            Assert.Contains("abre el objeto con F.Abrir(id, true)", r.Mensaje);
            Assert.Contains("No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.", r.Mensaje);
            if (contiene != null) Assert.Contains(contiene, r.Mensaje);

            var sl = Compilar(codigo, true);
            Assert.False(sl.Ok, "Con solo_lectura tampoco debía compilar: " + codigo);
            Assert.Contains(sl.Problemas, x => x.Categoria == CodigoReglas.SoloLectura);
            Assert.DoesNotContain(sl.Problemas, x => x.Categoria == CodigoReglas.AbiertoParaLectura);
            return p;
        }

        /// <summary>Compila sin solo_lectura y sin ningún rechazo «abierto para lectura».</summary>
        private static ResultadoCompilacion Valido(string codigo)
        {
            var r = Compilar(codigo);
            Assert.True(r.Ok, "Debía compilar sin solo_lectura: " + r.Mensaje);
            Assert.DoesNotContain(r.Problemas, x => x.Categoria == CodigoReglas.AbiertoParaLectura);
            return r;
        }

        // ------------------------------------------------------------------ el caso de la validación
        [Fact]
        public void Paso9DeLa1176SinSoloLectura_NoCompila()
        {
            var r = Compilar(Paso9);
            Assert.False(r.Ok);
            Assert.StartsWith(Cabecera, r.Mensaje);
            var p = Assert.Single(r.Problemas);
            Assert.Equal(CodigoReglas.AbiertoParaLectura, p.Categoria);
            Assert.Equal(5, p.Linea);
            Assert.Equal(3, p.Columna);
            Assert.Contains("Línea 5, columna 3: [bloqueado: abierto para lectura] Autodesk.AutoCAD.DatabaseServices.Curve.Extend sobre 'l', abierto para lectura en la línea 3 (F.Abrir<Entity>(id)): ", r.Mensaje);
            Assert.Contains("abre el objeto con F.Abrir(id, true)", p.Mensaje);
            Assert.Single(r.Escrituras);   // Extend sigue contado entre las escrituras reconocidas

            // Con solo_lectura, el texto de la 1.19.1 (VALIDACION_1176, paso 9), sin el rechazo nuevo
            var sl = Compilar(Paso9, true);
            Assert.StartsWith(Cabecera, sl.Mensaje);
            Assert.Contains("Línea 5, columna 3: [bloqueado: solo lectura] Autodesk.AutoCAD.DatabaseServices.Curve.Extend: con solo_lectura=true el script solo puede leer", sl.Mensaje);
            Assert.Contains("solo se pueden llamar métodos de lectura", sl.Mensaje);
        }

        [Fact]
        public void Paso9Corregido_Compila()
        {
            // Abriendo para escritura
            Valido(Paso9.Replace("F.Abrir<Entity>(id)", "F.Abrir<Entity>(id, true)"));
            Valido(Paso9.Replace("F.Abrir<Entity>(id)", "F.Abrir<Entity>(id, escribir: true)"));
            // Con UpgradeOpen antes del cambio, sobre la misma variable o sobre otra que es el mismo objeto
            Valido(Paso9.Replace("l.Extend(", "l.UpgradeOpen();\nl.Extend("));
            Valido(Paso9.Replace("is Line x) l = x;", "is Line x) { x.UpgradeOpen(); l = x; }"));
            Valido("var e = F.Abrir<Entity>(ObjectId.Null);\nvar l = (Line)e;\ne.UpgradeOpen();\nl.Extend(false, new Point3d(0, 0, 0));\nreturn 0;");
            Valido("var e = F.Abrir<Entity>(ObjectId.Null);\ne.UpgradeFromNotify();\ne.Layer = \"0\";\nreturn 0;");
        }

        // ------------------------------------------------------------------ rechazos
        [Fact]
        public void ObjetoAbiertoParaLectura_VariantesQueNoCompilan()
        {
            // Directamente sobre la variable del patrón
            Rechazado("foreach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) x.Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "Curve.Extend sobre 'x', abierto para lectura en la línea 2");
            // Sobre la expresión, sin variable
            Rechazado("F.Abrir<Line>(ObjectId.Null).Extend(true, new Point3d(0, 0, 0));\nreturn 0;", "sobre 'F.Abrir<Line>(ObjectId.Null)', abierto para lectura en la línea 1");
            // Con la constante false
            Rechazado("var l = F.Abrir<Line>(ObjectId.Null, false);\nl.Erase();\nreturn 0;", "DBObject.Erase sobre 'l'");
            // GetObject con OpenMode.ForRead, con conversión, con 'as' y con ?.
            Rechazado("var l = (Line)F.Tr.GetObject(ObjectId.Null, OpenMode.ForRead);\nl.Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "abierto para lectura en la línea 1 (F.Tr.GetObject(ObjectId.Null, OpenMode.ForRead))");
            Rechazado("var l = ObjectId.Null.GetObject(OpenMode.ForRead) as Line;\nl?.Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "Curve.Extend sobre 'l'");
            // ForNotify tampoco deja escribir; TransactionManager.GetObject con sus cuatro argumentos
            Rechazado("var e = (Entity)F.Db.TransactionManager.GetObject(ObjectId.Null, OpenMode.ForNotify, false, false);\ne.SetDatabaseDefaults();\nreturn 0;", "Entity.SetDatabaseDefaults sobre 'e'");
            // Asignaciones (también ++ y por desestructuración)
            Rechazado("var l = F.Abrir<Line>(ObjectId.Null);\nl.Layer = \"0\";\nreturn 0;", "Entity.Layer (asignación) sobre 'l'");
            Rechazado("var l = F.Abrir<Line>(ObjectId.Null);\nl.ColorIndex++;\nreturn 0;", "DBObject.ColorIndex (asignación) sobre 'l'");
            Rechazado("var l = F.Abrir<Line>(ObjectId.Null);\n(l.Layer, _) = (\"0\", 1);\nreturn 0;", "Entity.Layer (asignación) sobre 'l'");
            // switch y ?: (las dos ramas, de lectura)
            Rechazado("var e = F.Abrir<Entity>(ObjectId.Null);\nswitch (e) { case Line x: x.Extend(false, new Point3d(0, 0, 0)); break; }\nreturn 0;", "Curve.Extend sobre 'x'");
            Rechazado("Line a = F.Abrir<Line>(ObjectId.Null), b = F.Abrir<Line>(ObjectId.Null);\nvar l = F.Booleano(\"primera\") ? a : b;\nl.Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "abierto para lectura en la línea 1");
            // Una función local que devuelve un objeto abierto para lectura
            Rechazado("Line Linea(ObjectId id) => F.Abrir<Line>(id);\nforeach (var id in F.EspacioModelo())\n    Linea(id).Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "sobre 'Linea(id)', abierto para lectura en la línea 1");
            // ... también si es recursiva (el cálculo termina)
            Rechazado("Line Buscar(int n) => n > 0 ? Buscar(n - 1) : F.Abrir<Line>(ObjectId.Null);\nBuscar(3).Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "sobre 'Buscar(3)', abierto para lectura en la línea 1");
            // Close y Cancel no se exceptúan: sobre un objeto abierto para lectura se rechazan
            Rechazado("var e = F.Abrir<Entity>(ObjectId.Null);\ne.Close();\nreturn 0;", "DBObject.Close sobre 'e'");
            Rechazado("var e = F.Abrir<Entity>(ObjectId.Null);\ne.Cancel();\nreturn 0;", "DBObject.Cancel sobre 'e'");
        }

        [Fact]
        public void AppendEntityEnUnEspacioModeloAbiertoParaLectura()
        {
            const string codigo = "var bt = (BlockTable)F.Tr.GetObject(F.Db.BlockTableId, OpenMode.ForRead);\nvar ms = (BlockTableRecord)F.Tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);\nvar l = new Line(new Point3d(0, 0, 0), new Point3d(1, 0, 0));\nms.AppendEntity(l);\nF.Tr.AddNewlyCreatedDBObject(l, true);\nreturn l.Length;";
            var p = Rechazado(codigo, "BlockTableRecord.AppendEntity sobre 'ms', abierto para lectura en la línea 2");
            Assert.Equal(4, p.Linea);
            // El patrón de siempre, con el espacio modelo para escritura: compila (la línea nueva y la transacción no son aperturas)
            Valido(codigo.Replace("bt[BlockTableRecord.ModelSpace], OpenMode.ForRead", "bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite"));
        }

        [Fact]
        public void ColeccionesYLinq()
        {
            const string linq = "var lineas = F.EspacioModelo().Select(id => F.Abrir<Entity>(id)).OfType<Line>().ToList();\n";
            Rechazado(linq + "foreach (var l in lineas) l.Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "Curve.Extend sobre 'l', abierto para lectura en la línea 1 (F.Abrir<Entity>(id))");
            Rechazado(linq + "lineas.ForEach(l => l.Layer = \"0\");\nreturn 0;", "Entity.Layer (asignación) sobre 'l'");
            Rechazado(linq + "lineas.First().Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "sobre 'lineas.First()'");
            Rechazado(linq + "lineas[0].Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "sobre 'lineas[0]'");
            Valido(linq.Replace("F.Abrir<Entity>(id)", "F.Abrir<Entity>(id, true)") + "foreach (var l in lineas) l.Extend(false, new Point3d(0, 0, 0));\nreturn 0;");

            // Una lista llenada con Add
            const string lista = "var lineas = new List<Line>();\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) lineas.Add(x);\n";
            Rechazado(lista + "foreach (var l in lineas) l.Extend(false, new Point3d(0, 0, 0));\nreturn 0;", "Curve.Extend sobre 'l', abierto para lectura en la línea 3");
            // UpgradeOpen sobre los elementos: cualquier variable que pueda ser uno de ellos deja de rechazarse
            Valido(lista + "foreach (var l in lineas) l.UpgradeOpen();\nforeach (var m in lineas) m.Extend(false, new Point3d(0, 0, 0));\nreturn 0;");
            // Un arreglo con aperturas para lectura
            Rechazado("var arr = new[] { F.Abrir<Line>(ObjectId.Null) };\narr[0].Erase();\nreturn 0;", "DBObject.Erase sobre 'arr[0]'");
        }

        [Fact]
        public void SubobjetosDeUnCorredorAbiertoParaLectura()
        {
            const string corredor = "var c = F.Abrir<Civ.Corridor>(F.Corredor(\"Corredor 1\"));\n";
            Rechazado(corredor + "c.Baselines[0].BaselineRegions[0].StartStation = 100;\nreturn 0;",
                "Autodesk.Civil.DatabaseServices.BaselineRegion.StartStation (asignación) sobre 'c.Baselines[0].BaselineRegions[0]', abierto para lectura en la línea 1");
            Rechazado(corredor + "var region = c.Baselines[0].BaselineRegions[0];\nregion.StartStation = 100;\nreturn 0;", "sobre 'region'");
            Valido(corredor.Replace("(\"Corredor 1\"))", "(\"Corredor 1\"), true)") + "c.Baselines[0].BaselineRegions[0].StartStation = 100;\nreturn 0;");
            Valido(corredor + "c.UpgradeOpen();\nvar region = c.Baselines[0].BaselineRegions[0];\nregion.StartStation = 100;\nreturn 0;");
            // Leer los subobjetos sigue valiendo
            Valido(corredor + "return c.Baselines[0].BaselineRegions[0].StartStation;");
        }

        // ------------------------------------------------------------------ lo que sigue compilando
        [Fact]
        public void LaRecetaCrearCapaConColorSigueCompilando()
        {
            // El script literal de la receta (informe de VALIDACION_117, paso 2)
            const string receta = "var nombre = F.Texto(\"nombre_capa\");\nvar color = (short)Math.Round(F.Numero(\"color\"));\nvar lt = F.Abrir<LayerTable>(F.Db.LayerTableId, true);\nbool creada = !lt.Has(nombre);\nif (creada)\n{\n    var ltr = new LayerTableRecord();\n    ltr.Name = nombre;\n    ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, color);\n    lt.Add(ltr);\n    F.Tr.AddNewlyCreatedDBObject(ltr, true);\n}\nreturn new { capa = nombre, color = (int)color, creada };";
            var r = Valido(receta);
            Assert.Equal(5, r.Escrituras.Count);
            // Con solo_lectura, los mismos 5 usos que rechazó en el paso 16 del informe 1176_b
            var sl = Compilar(receta, true);
            Assert.Contains("(5 usos no permitidos)", sl.Mensaje);
            Assert.DoesNotContain(sl.Problemas, x => x.Categoria == CodigoReglas.AbiertoParaLectura);
            // La misma receta olvidando el true: lt.Add sobre la tabla abierta para lectura no compila
            Rechazado(receta.Replace("F.Abrir<LayerTable>(F.Db.LayerTableId, true)", "F.Abrir<LayerTable>(F.Db.LayerTableId)"),
                "Autodesk.AutoCAD.DatabaseServices.SymbolTable.Add sobre 'lt', abierto para lectura en la línea 3");
        }

        [Fact]
        public void LoQueLeeOCopiaUnObjetoAbiertoParaLecturaCompila()
        {
            // Los métodos de CodigoReglas.MetodosValidosSobreObjetoDeLectura (salvo los que promueven, que se prueban aparte)
            Valido("var e = F.Abrir<Entity>(ObjectId.Null);\nvar copia = (Entity)e.Clone();\ncopia.Layer = \"0\";\nF.Agregar(copia);\nvar piezas = new DBObjectCollection();\ne.Explode(piezas);\ne.Highlight();\ne.Unhighlight();\ne.List();\nvar c2 = e.DeepClone(e, null, true);\nvar c3 = e.WblockClone(null, null, true);\ne.DowngradeOpen();\ne.DowngradeToNotify(false);\nreturn piezas.Count;");
            // Leer sigue valiendo, claro
            Valido("var l = F.Abrir<Line>(ObjectId.Null);\nvar capa = l.Layer;\nvar p = l.StartPoint;\nreturn new { capa, p.X, l.Length };");
            // Close y Cancel no están en la lista (ajuste de la orquestadora, 17/11/2026)
            Assert.DoesNotContain("Close", CodigoReglas.MetodosValidosSobreObjetoDeLectura);
            Assert.DoesNotContain("Cancel", CodigoReglas.MetodosValidosSobreObjetoDeLectura);
            Assert.Contains("DowngradeToNotify", CodigoReglas.MetodosValidosSobreObjetoDeLectura);
        }

        [Fact]
        public void LoQueNoSePuedeSaber_SeDejaPasar()
        {
            // Un parámetro de una función local: no se sigue a quién la llama
            Valido("void Alargar(Line l) => l.Extend(false, new Point3d(0, 0, 0));\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) Alargar(x);\nreturn 0;");
            // Lectura y escritura mezcladas en la misma variable
            Valido("var l = F.Abrir<Line>(ObjectId.Null);\nif (F.Booleano(\"escribir\")) l = F.Abrir<Line>(ObjectId.Null, true);\nl.Extend(false, new Point3d(0, 0, 0));\nreturn 0;");
            // 'escribir' u OpenMode que no son constantes
            Valido("F.Abrir<Line>(ObjectId.Null, F.Booleano(\"escribir\")).Extend(false, new Point3d(0, 0, 0));\nreturn 0;");
            Valido("var modo = F.Booleano(\"escribir\") ? OpenMode.ForWrite : OpenMode.ForRead;\n((Line)F.Tr.GetObject(ObjectId.Null, modo)).Extend(false, new Point3d(0, 0, 0));\nreturn 0;");
            // Una colección que entra en otro método: ya no se sabe qué tiene
            Valido("var lineas = new List<Line>();\nvoid Llenar(List<Line> destino) { foreach (var id in F.EspacioModelo()) destino.Add(F.Abrir<Line>(id, true)); }\nforeach (var id in F.EspacioModelo()) lineas.Add(F.Abrir<Line>(id));\nLlenar(lineas);\nforeach (var l in lineas) l.Extend(false, new Point3d(0, 0, 0));\nreturn 0;");
            // Un objeto que no se abrió: nuevo en memoria
            Valido("var l = new Line(new Point3d(0, 0, 0), new Point3d(1, 0, 0));\nl.Layer = \"0\";\nl.Extend(false, new Point3d(2, 0, 0));\nF.Agregar(l);\nreturn 0;");
        }

        [Fact]
        public void ConErroresDeCompilacionNoFallaElAnalisis()
        {
            var r = Compilar("var l = F.Abrir<Line>(ObjectId.Null);\nl.Extend(false, noExiste);\nreturn 0;");
            Assert.False(r.Ok);
            Assert.Contains("CS0103", r.Mensaje);
        }

        // ------------------------------------------------------------------ VALIDACION_1192, literales
        [Fact]
        public void LosPasosDeVALIDACION_1192()
        {
            // Paso 3 (decisivo): el del paso 9 de la 1176, sin solo_lectura
            Assert.Contains("Línea 5, columna 3: [bloqueado: abierto para lectura] Autodesk.AutoCAD.DatabaseServices.Curve.Extend sobre 'l', abierto para lectura en la línea 3 (F.Abrir<Entity>(id)): cambiar un objeto abierto para lectura no siempre da un error en Civil 3D: con Curve.Extend, Civil 3D 2027 se cerró (17/11/2026). Para cambiarlo, abre el objeto con F.Abrir(id, true) (o con OpenMode.ForWrite en GetObject), o llama a UpgradeOpen() sobre él antes de cambiarlo.", Compilar(Paso9).Mensaje);
            // Paso 4a: asignar una propiedad
            var a = Compilar("foreach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) x.Layer = \"0\";\nreturn 0;");
            Assert.StartsWith(Cabecera, a.Mensaje);
            Assert.Contains("Línea 2, columna 42: [bloqueado: abierto para lectura] Autodesk.AutoCAD.DatabaseServices.Entity.Layer (asignación) sobre 'x', abierto para lectura en la línea 2 (F.Abrir<Entity>(id)): ", a.Mensaje);
            // Paso 4b (decisivo): Transaction.GetObject con OpenMode.ForRead
            var b = Compilar("foreach (var id in F.EspacioModelo())\n{\n    var e = (Entity)F.Tr.GetObject(id, OpenMode.ForRead);\n    if (e is Line) e.Erase();\n}\nreturn 0;");
            Assert.StartsWith(Cabecera, b.Mensaje);
            Assert.Contains("Línea 4, columna 22: [bloqueado: abierto para lectura] Autodesk.AutoCAD.DatabaseServices.DBObject.Erase sobre 'e', abierto para lectura en la línea 3 (F.Tr.GetObject(id, OpenMode.ForRead)): ", b.Mensaje);
            // Pasos 7 y 8 (decisivos): las dos correcciones compilan, con 2 escrituras reconocidas cada una
            Assert.Equal(2, Valido(Paso9.Replace("F.Abrir<Entity>(id)", "F.Abrir<Entity>(id, true)")).Escrituras.Count);
            Assert.Equal(2, Valido(Paso9.Replace("l.Extend(", "l.UpgradeOpen();\nl.Extend(")).Escrituras.Count);
        }

        // ------------------------------------------------------------------ VALIDACION_1176 (informe 1176_b), literales
        [Fact]
        public void Paso8_ConSoloLecturaComoEnLa1191YSinElloCompila()
        {
            var casos = new[]
            {
                // 8a a 8e
                ("var l = new Line(new Point3d(0, 0, 0), new Point3d(10, 0, 0));\nF.Agregar(l);\nreturn 1;", "Línea 2, columna 3: [bloqueado: solo lectura] F.Agregar: con solo_lectura=true el script solo puede leer"),
                ("foreach (var id in F.EspacioModelo())\n{\n    var o = F.Tr.GetObject(id, OpenMode.ForWrite);\n}\nreturn 0;", "[bloqueado: solo lectura] OpenMode distinto de OpenMode.ForRead (OpenMode.ForWrite)"),
                ("foreach (var id in F.EspacioModelo())\n    F.Abrir<Entity>(id, true);\nreturn 0;", "[bloqueado: solo lectura] F.Abrir con 'escribir' distinto de false (true)"),
                ("LayoutManager.Current.DeleteLayout(\"Presentación1\");\nreturn 1;", "[bloqueado: solo lectura] Autodesk.AutoCAD.DatabaseServices.LayoutManager.DeleteLayout"),
                ("foreach (var id in F.EspacioModelo())\n{\n    var o = new[] { id }.Zip(Enum.GetValues<OpenMode>().Skip(1), F.Tr.GetObject).First();\n}\nreturn 0;", "grupo de métodos (delegado) con un parámetro OpenMode: GetObject"),
            };
            foreach (var (codigo, esperado) in casos)
            {
                var sl = Compilar(codigo, true);
                Assert.False(sl.Ok, codigo);
                Assert.Contains(esperado, sl.Mensaje);
                Assert.DoesNotContain(sl.Problemas, x => x.Categoria == CodigoReglas.AbiertoParaLectura);
                Valido(codigo);
            }
            Assert.StartsWith(Cabecera, Compilar(casos[0].Item1, true).Mensaje);
            Assert.Contains("OpenMode como argumento genérico de GetValues", Compilar(casos[4].Item1, true).Mensaje);
        }

        [Fact]
        public void Paso17_SigueBloqueadoIgualEnTodoModo()
        {
            foreach (bool soloLectura in new[] { false, true })
            {
                var a = Compilar("var id = F.Db.AddDBObject(new Line());\nreturn id;", soloLectura);
                Assert.StartsWith(Cabecera, a.Mensaje);
                Assert.Contains("[bloqueado: transacción]", a.Mensaje);
                Assert.Contains("AddDBObject: usa F.Tr o una transacción anidada", a.Mensaje);
                var b = Compilar("F.Db.XBindXrefs(new ObjectIdCollection(), true);\nF.Db.DetachXref(ObjectId.Null);\nreturn null;", soloLectura);
                Assert.Contains("(2 usos no permitidos)", b.Mensaje);
                Assert.Contains("[bloqueado: documentos] Autodesk.AutoCAD.DatabaseServices.Database.XBindXrefs", b.Mensaje);
                Assert.Contains("[bloqueado: documentos] Autodesk.AutoCAD.DatabaseServices.Database.DetachXref", b.Mensaje);
                var c = Compilar("(F.Db.Clayer, _) = (F.Db.Clayer, 0);\nreturn null;", soloLectura);
                Assert.False(c.Ok);
                Assert.Contains("[bloqueado: variables de sistema]", c.Mensaje);
                Assert.Contains("Clayer (asignación)", c.Mensaje);
                foreach (var r in new[] { a, b, c }) Assert.DoesNotContain(r.Problemas, x => x.Categoria == CodigoReglas.AbiertoParaLectura);
            }
        }

        [Fact]
        public void LosScriptsDeLasValidacionesQueLeenOAgreganSiguenCompilando()
        {
            // VALIDACION_1176, paso 5 (sin solo_lectura): F.Agregar de una línea nueva
            var p5 = Valido("var l = new Line(new Point3d(1000, 2000, 0), new Point3d(1100, 2000, 0));\nF.Agregar(l);\nF.Escribir(\"línea de prueba\");\nreturn new { largo = l.Length, capa = l.Layer };");
            Assert.Single(p5.Escrituras);
            // Contar (pasos 7 y 11), medir (paso 9), escribir en la línea de comandos (paso 9b) y el ejemplo de CONTRATO.md,
            // con y sin solo_lectura
            foreach (var codigo in new[]
            {
                "int n = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line) n++;\nreturn n;",
                "double largo = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) largo = x.Length;\nreturn largo;",
                "var ed = F.Doc.Editor;\ned.WriteMessage(\"\\nPrueba 1.19.1\");\nint n = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line) n++;\nreturn n;",
                "var al = F.Abrir<Civ.Alignment>(F.Alineamiento(F.Texto(\"eje\"))); return new { al.Name, al.Length };",
            })
            {
                var r = Valido(codigo);
                Assert.Empty(r.Escrituras);
                var sl = Compilar(codigo, true);
                Assert.True(sl.Ok, sl.Mensaje);
            }
        }
    }
}
