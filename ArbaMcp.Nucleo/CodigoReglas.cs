using System;
using System.Collections.Generic;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Lista blanca y lista de bloqueo de ejecutar_codigo (desde 1.16.0). Solo datos y decisiones sobre nombres: no usa
    /// Roslyn (eso lo hace CodigoRevisor, que pregunta aquí por cada tipo y cada miembro que el script nombra).
    ///
    /// Principio: un script solo puede usar lo básico de System (números, textos, fechas, colecciones, LINQ, StringBuilder,
    /// JSON de solo lectura, Path) y la API de AutoCAD y Civil 3D; todo lo demás no compila. Dentro de lo permitido hay una
    /// lista de bloqueo para lo que sale de Civil 3D (red, procesos, archivos), lo que se salta la transacción de la
    /// herramienta (comandos, LISP, StartOpenCloseTransaction, ObjectId.Open) y lo que podría colgar o cerrar Civil 3D
    /// (diálogos, entrada interactiva, hilos, eventos, código nativo). Los nombres de la API que se bloquean se comparan
    /// como texto: un nombre que no exista en la DLL nunca coincide y no hace daño.
    /// </summary>
    public static class CodigoReglas
    {
        // ------------------------------------------------------------------ categorías (motivo de cada bloqueo)
        public const string Red = "red";
        public const string Procesos = "procesos";
        public const string Archivos = "archivos";
        public const string Reflexion = "reflexión";
        public const string Hilos = "hilos";
        public const string Nativo = "código nativo";
        public const string Proceso = "proceso de Civil 3D";
        public const string Interfaz = "ventanas y diálogos";
        public const string Eventos = "eventos";
        public const string Comandos = "comandos";
        public const string Transaccion = "transacción";
        public const string Documentos = "documentos";
        public const string Sistema = "variables de sistema";
        public const string Plugin = "plugin";
        public const string Borrado = "regla del proyecto";
        public const string Sintaxis = "sintaxis";
        public const string ListaBlanca = "lista blanca";
        /// <summary>Con solo_lectura=true (desde 1.17.6): el script usa algo que escribe en el dibujo o en un archivo.</summary>
        public const string SoloLectura = "solo lectura";

        /// <summary>Explicación de cada categoría para el agente (va detrás del nombre bloqueado).</summary>
        public static string Explicacion(string categoria)
        {
            switch (categoria)
            {
                case Red: return "ejecutar_codigo no admite acceso a la red.";
                case Procesos: return "ejecutar_codigo no admite abrir programas ni procesos.";
                case Archivos: return "ejecutar_codigo no admite archivos fuera de la carpeta permitida: usa F.LeerTexto, F.EscribirTexto y F.ListarArchivos, que trabajan en %LOCALAPPDATA%\\ArbaMcp\\scripts.";
                case Reflexion: return "ejecutar_codigo no admite reflexión ni generación de código.";
                case Hilos: return "ejecutar_codigo no admite hilos, tareas ni código asíncrono: todo corre en el hilo principal de AutoCAD.";
                case Nativo: return "ejecutar_codigo no admite código nativo, punteros ni P/Invoke (podrían cerrar Civil 3D).";
                case Proceso: return "ejecutar_codigo no admite tocar el proceso de Civil 3D (Environment, AppDomain, GC, consola, registro de Windows).";
                case Interfaz: return "ejecutar_codigo no admite ventanas, diálogos ni entrada interactiva: el script no puede esperar al usuario.";
                case Eventos: return "ejecutar_codigo no admite eventos: un evento ejecutaría código del script cuando ya terminó.";
                case Comandos: return "ejecutar_codigo no admite enviar comandos ni LISP: se ejecutarían fuera de la transacción, también al simular. Si hace falta, usa ejecutar_comando.";
                case Transaccion: return "usa F.Tr o una transacción anidada (F.Db.TransactionManager.StartTransaction()): este miembro se salta la transacción de la herramienta y la simulación no podría deshacerlo.";
                case Documentos: return "ejecutar_codigo no admite abrir, cerrar ni guardar dibujos, ni leer o escribir archivos con la API: usa abrir_dibujo, guardar_dibujo, guardar_copia o la herramienta específica (crear_superficie_tin, exportar_landxml...).";
                case Sistema: return "ejecutar_codigo no admite cambiar variables de sistema ni del dibujo (propiedades de Database) ni el perfil de AutoCAD: la simulación no podría deshacerlas.";
                case Plugin: return "de ArbaMcp, el script solo puede usar la Fachada F.";
                case Borrado: return "ninguna herramienta borra ni recrea regiones, líneas base, ensamblajes, corredores ni superficies.";
                case SoloLectura: return "con solo_lectura=true el script solo puede leer: no compila nada que cree, cambie o borre objetos del dibujo, abra objetos para escritura ni escriba archivos. Si el script necesita escribir, quita solo_lectura y simula primero (simular=true).";
                default: return "no está en la lista blanca de ejecutar_codigo (lo básico de System, colecciones, LINQ, texto, JSON, Path y la API de AutoCAD y Civil 3D).";
            }
        }

        // ------------------------------------------------------------------ espacios de nombres
        /// <summary>Espacios de nombres de System permitidos (coincidencia exacta del espacio de nombres del tipo).</summary>
        public static readonly HashSet<string> EspaciosSystemPermitidos = new HashSet<string>(StringComparer.Ordinal)
        {
            "System",
            "System.Collections",
            "System.Collections.Generic",
            "System.Collections.ObjectModel",
            "System.Collections.Specialized",
            "System.Linq",
            "System.Text",
            "System.Text.Json",
            "System.Text.Json.Nodes",
            "System.Globalization",
        };

        /// <summary>
        /// Espacios de nombres de los que solo se permiten algunos tipos (TiposPermitidosEn). Un using de ellos se acepta;
        /// cada tipo se revisa al usarse.
        /// </summary>
        public static readonly Dictionary<string, string[]> TiposPermitidosEn = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["System.IO"] = new[] { "Path", "StringReader", "StringWriter", "IOException", "FileNotFoundException", "DirectoryNotFoundException" },
            ["System.Diagnostics"] = new[] { "Stopwatch" },
        };

        /// <summary>Prefijos de Autodesk permitidos (el espacio de nombres del tipo es el prefijo o empieza por prefijo + ".").</summary>
        public static readonly string[] PrefijosAutodeskPermitidos =
        {
            "Autodesk.AutoCAD.ApplicationServices",
            "Autodesk.AutoCAD.DatabaseServices",
            "Autodesk.AutoCAD.Geometry",
            "Autodesk.AutoCAD.EditorInput",
            "Autodesk.AutoCAD.Colors",
            "Autodesk.AutoCAD.Runtime",
            "Autodesk.Civil",
            "Autodesk.Aec",
        };

        /// <summary>
        /// Espacios de nombres bloqueados aunque sean de Autodesk (se comprueban antes que los permitidos): COM, ventanas,
        /// cinta, gráficos transitorios (dejarían a AutoCAD llamando a código del script ya descargado), trazado e internos.
        /// </summary>
        public static readonly (string prefijo, string categoria)[] PrefijosBloqueados =
        {
            ("Autodesk.AutoCAD.Interop", Procesos),
            ("Autodesk.AutoCAD.Windows", Interfaz),
            ("Autodesk.Windows", Interfaz),
            ("Autodesk.AutoCAD.Internal", Proceso),
            ("Autodesk.AutoCAD.GraphicsInterface", Interfaz),
            ("Autodesk.AutoCAD.GraphicsSystem", Interfaz),
            ("Autodesk.AutoCAD.PlottingServices", Documentos),
            ("Autodesk.AutoCAD.Publishing", Documentos),
            ("Autodesk.AutoCAD.Customization", Interfaz),
            ("System.Net", Red),
            ("System.Diagnostics", Procesos),
            ("System.IO", Archivos),
            ("System.Reflection", Reflexion),
            ("System.Linq.Expressions", Reflexion),
            ("System.Runtime.InteropServices", Nativo),
            ("System.Runtime.Intrinsics", Nativo),
            ("System.Runtime", Reflexion),
            ("System.Threading", Hilos),
            ("System.Timers", Hilos),
            ("System.Windows", Interfaz),
            ("System.Drawing", Interfaz),
            ("System.Xaml", Interfaz),
            ("System.Media", Interfaz),
            ("System.Security", Proceso),
            ("System.Resources", Reflexion),
            ("System.ComponentModel", Reflexion),
            ("System.Configuration", Proceso),
            ("System.Management", Proceso),
            ("System.ServiceProcess", Procesos),
            ("System.Data", Red),
            ("System.Xml", Red),
            ("System.Web", Red),
            ("System.Text.RegularExpressions", ListaBlanca),
            ("Microsoft.Win32", Proceso),
            ("Microsoft.CSharp", Reflexion),
            ("Microsoft.VisualBasic", Reflexion),
        };

        /// <summary>Segmentos que, en cualquier espacio de nombres de Autodesk, lo bloquean (Autodesk.Civil.X.Windows, ...Interop).</summary>
        public static readonly string[] SegmentosAutodeskBloqueados = { "Windows", "Interop", "Internal" };

        // ------------------------------------------------------------------ tipos bloqueados dentro de lo permitido
        /// <summary>Tipos bloqueados (nombre completo sin genéricos → categoría) aunque su espacio de nombres esté permitido.</summary>
        public static readonly Dictionary<string, string> TiposBloqueados = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["System.AppDomain"] = Proceso,
            ["System.AppContext"] = Proceso,
            ["System.Activator"] = Reflexion,
            ["System.GC"] = Proceso,
            ["System.Console"] = Proceso,
            ["System.RuntimeTypeHandle"] = Reflexion,
            ["System.RuntimeMethodHandle"] = Reflexion,
            ["System.RuntimeFieldHandle"] = Reflexion,
            ["System.ModuleHandle"] = Reflexion,
            ["System.TypedReference"] = Nativo,
            ["System.ArgIterator"] = Nativo,
            ["System.RuntimeArgumentHandle"] = Nativo,
            ["System.MarshalByRefObject"] = Reflexion,
            ["System.Progress"] = Hilos,
            ["System.Linq.Queryable"] = Reflexion,
            ["System.Linq.IQueryable"] = Reflexion,
            ["System.Linq.IOrderedQueryable"] = Reflexion,
            ["System.Linq.IQueryProvider"] = Reflexion,
            ["System.Linq.EnumerableQuery"] = Reflexion,
            ["System.Linq.ParallelEnumerable"] = Hilos,
            ["System.Linq.ParallelQuery"] = Hilos,
            ["System.Linq.OrderedParallelQuery"] = Hilos,
            ["Autodesk.AutoCAD.Runtime.DynamicLinker"] = Nativo,
            ["Autodesk.AutoCAD.Runtime.Interop"] = Nativo,
        };

        /// <summary>
        /// Tipos de System con solo algunos miembros permitidos. System.Type sigue disponible para typeof(...) y
        /// comparaciones (RXObject.GetClass(typeof(Line))), pero no para reflexión; de Environment solo NewLine.
        /// </summary>
        public static readonly Dictionary<string, (HashSet<string> permitidos, string categoria)> MiembrosPermitidosDe = new Dictionary<string, (HashSet<string>, string)>(StringComparer.Ordinal)
        {
            ["System.Type"] = (new HashSet<string>(StringComparer.Ordinal) { "Name", "FullName", "Namespace", "IsAssignableFrom", "IsSubclassOf", "IsInstanceOfType", "Equals", "GetHashCode", "ToString", "op_Equality", "op_Inequality" }, Reflexion),
            ["System.Environment"] = (new HashSet<string>(StringComparer.Ordinal) { "NewLine" }, Proceso),
            ["System.IntPtr"] = (new HashSet<string>(StringComparer.Ordinal) { "Zero", "Equals", "GetHashCode", "ToString", "op_Equality", "op_Inequality" }, Nativo),
            ["System.UIntPtr"] = (new HashSet<string>(StringComparer.Ordinal) { "Zero", "Equals", "GetHashCode", "ToString", "op_Equality", "op_Inequality" }, Nativo),
        };

        /// <summary>Miembros bloqueados de tipos de System permitidos (nombre completo del tipo → miembros).</summary>
        public static readonly Dictionary<string, (string[] miembros, string categoria)> MiembrosSystemBloqueados = new Dictionary<string, (string[], string)>(StringComparer.Ordinal)
        {
            ["System.Delegate"] = (new[] { "DynamicInvoke", "Method", "CreateDelegate" }, Reflexion),
            ["System.MulticastDelegate"] = (new[] { "DynamicInvoke", "Method" }, Reflexion),
            ["System.IO.Path"] = (new[] { "GetTempFileName" }, Archivos),
        };

        /// <summary>
        /// Miembros bloqueados de tipos de Autodesk. Se buscan por el nombre corto del tipo (en el tipo y en sus bases) y el
        /// nombre del miembro; 'soloEscritura' bloquea solo la asignación de una propiedad (leer MdiActiveDocument sí vale).
        /// 'prefijo' bloquea todo miembro cuyo nombre empiece así (los Get* interactivos de Editor). Varias clases de
        /// extensión (DocumentExtension, DocumentCollectionExtension) se incluyen por su propio nombre.
        /// </summary>
        public static readonly ReglaMiembro[] MiembrosAutodeskBloqueados =
        {
            new ReglaMiembro("Document", "SendStringToExecute", Comandos),
            new ReglaMiembro("Document", "CloseAndDiscard", Documentos),
            new ReglaMiembro("Document", "CloseAndSave", Documentos),
            new ReglaMiembro("Document", "AcadDocument", Procesos),
            new ReglaMiembro("DocumentExtension", "CloseAndDiscard", Documentos),
            new ReglaMiembro("DocumentExtension", "CloseAndSave", Documentos),
            new ReglaMiembro("DocumentExtension", "GetAcadDocument", Procesos),
            new ReglaMiembro("DocumentCollection", "Open", Documentos),
            new ReglaMiembro("DocumentCollection", "Add", Documentos),
            new ReglaMiembro("DocumentCollection", "CloseAll", Documentos),
            new ReglaMiembro("DocumentCollection", "ExecuteInCommandContextAsync", Comandos),
            new ReglaMiembro("DocumentCollection", "ExecuteInApplicationContext", Comandos),
            new ReglaMiembro("DocumentCollection", "MdiActiveDocument", Documentos, soloEscritura: true),
            new ReglaMiembro("DocumentCollectionExtension", "Open", Documentos),
            new ReglaMiembro("DocumentCollectionExtension", "Add", Documentos),
            new ReglaMiembro("DocumentCollectionExtension", "CloseAll", Documentos),
            new ReglaMiembro("Application", "Quit", Proceso),
            new ReglaMiembro("Application", "SetSystemVariable", Sistema),
            new ReglaMiembro("Application", "ShowAlertDialog", Interfaz),
            new ReglaMiembro("Application", "ShowModalDialog", Interfaz),
            new ReglaMiembro("Application", "ShowModelessDialog", Interfaz),
            new ReglaMiembro("Application", "ShowModalWindow", Interfaz),
            new ReglaMiembro("Application", "ShowModelessWindow", Interfaz),
            new ReglaMiembro("Application", "Invoke", Comandos),
            new ReglaMiembro("Application", "AcadApplication", Procesos),
            new ReglaMiembro("Application", "MainWindow", Interfaz),
            new ReglaMiembro("Application", "LoadPartialMenu", Interfaz),
            new ReglaMiembro("Application", "UnloadPartialMenu", Interfaz),
            new ReglaMiembro("Editor", "Command", Comandos),
            new ReglaMiembro("Editor", "CommandAsync", Comandos),
            new ReglaMiembro("Editor", "Drag", Interfaz),
            new ReglaMiembro("Editor", "StartUserInteraction", Interfaz),
            new ReglaMiembro("Editor", "Get", Interfaz, prefijo: true, excepto: new[] { "GetCurrentView", "GetHashCode", "GetType" }),
            new ReglaMiembro("Document", "Dispose", Proceso),
            new ReglaMiembro("DocumentCollection", "Dispose", Proceso),
            new ReglaMiembro("Editor", "Dispose", Proceso),
            new ReglaMiembro("Application", "UserConfigurationManager", Sistema),
            // Las variables del dibujo (db.Clayer, db.Insunits...) no se deshacen al abortar la transacción
            new ReglaMiembro("Database", "", Sistema, soloEscritura: true, prefijo: true),
            new ReglaMiembro("Database", "SaveAs", Documentos),
            new ReglaMiembro("Database", "Save", Documentos),
            new ReglaMiembro("Database", "ReadDwgFile", Documentos),
            new ReglaMiembro("Database", "DxfOut", Documentos),
            new ReglaMiembro("Database", "DxfIn", Documentos),
            new ReglaMiembro("Database", "Dispose", Proceso),
            new ReglaMiembro("Database", "CloseInput", Documentos),
            new ReglaMiembro("TransactionManager", "StartOpenCloseTransaction", Transaccion),
            new ReglaMiembro("TransactionManager", "TopTransaction", Transaccion),
            new ReglaMiembro("ObjectId", "Open", Transaccion),
            // 1.17.6: Database.AddDBObject hace residente un objeto fuera de la transacción si no se sigue de
            // AddNewlyCreatedDBObject; al abortar (simulación) quedaría en el dibujo. Para añadir: F.Agregar, o
            // BlockTableRecord.AppendEntity / DBDictionary.SetAt más F.Tr.AddNewlyCreatedDBObject
            new ReglaMiembro("Database", "AddDBObject", Transaccion),
            // 1.17.6 (catálogo, consulta de la orquestadora del 17/11/2026): métodos de Database que meten, clonan o
            // borran objetos fuera del flujo normal de la transacción, o trabajan con otros dibujos o referencias externas
            new ReglaMiembro("Database", "DeepCloneObjects", Transaccion),
            new ReglaMiembro("Database", "WblockCloneObjects", Transaccion),
            new ReglaMiembro("Database", "EraseEmptyObjects", Transaccion),
            new ReglaMiembro("Database", "ReclaimMemoryFromErasedObjects", Transaccion),
            new ReglaMiembro("Database", "Wblock", Documentos),
            new ReglaMiembro("Database", "BindXrefs", Documentos),
            new ReglaMiembro("HostApplicationServices", "WorkingDatabase", Documentos, soloEscritura: true),
            new ReglaMiembro("HostApplicationServices", "GetRemoteFile", Red),
            new ReglaMiembro("HostApplicationServices", "PutRemoteFile", Red),
            new ReglaMiembro("HostApplicationServices", "LaunchBrowserDialog", Interfaz),
            new ReglaMiembro("SystemObjects", "DynamicLinker", Nativo),
            new ReglaMiembro("RXObject", "Create", Nativo),
            new ReglaMiembro("DisposableWrapper", "Create", Nativo),
            new ReglaMiembro("DisposableWrapper", "Attach", Nativo),
            new ReglaMiembro("DisposableWrapper", "Detach", Nativo),
            new ReglaMiembro("BaselineCollection", "Remove", Borrado),
            new ReglaMiembro("BaselineCollection", "RemoveAt", Borrado),
            new ReglaMiembro("BaselineCollection", "Clear", Borrado),
            new ReglaMiembro("BaselineRegionCollection", "Remove", Borrado),
            new ReglaMiembro("BaselineRegionCollection", "RemoveAt", Borrado),
            new ReglaMiembro("BaselineRegionCollection", "Clear", Borrado),
            new ReglaMiembro("CorridorSurfaceCollection", "Remove", Borrado),
            new ReglaMiembro("CorridorSurfaceCollection", "RemoveAt", Borrado),
            new ReglaMiembro("CorridorCollection", "Remove", Borrado),
            new ReglaMiembro("AssemblyCollection", "Remove", Borrado),
        };

        /// <summary>
        /// Palabras (separando el nombre en camelCase) que marcan un parámetro de texto de la API de Autodesk como un
        /// archivo o una ruta: 'fileName', 'pointFilePath' o 'url' lo son; 'profileName' no ('profile' no es 'file').
        /// </summary>
        public static readonly HashSet<string> PalabrasDeArchivo = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "file", "filename", "filepath", "path", "pathname", "url", "uri", "folder", "directory", "dir", "dwg"
        };

        /// <summary>True si el nombre de un parámetro (camelCase o minúsculas) contiene una palabra de archivo o ruta.</summary>
        public static bool EsParametroDeArchivo(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return false;
            if (PalabrasDeArchivo.Contains(nombre)) return true;
            return Palabras(nombre).Any(p => PalabrasDeArchivo.Contains(p));
        }

        /// <summary>Separa un identificador en palabras por mayúsculas, dígitos y guiones bajos ("pointFilePath" → point, File, Path).</summary>
        public static List<string> Palabras(string nombre)
        {
            var palabras = new List<string>();
            var actual = new System.Text.StringBuilder();
            for (int i = 0; i < nombre.Length; i++)
            {
                char c = nombre[i];
                if (c == '_' || char.IsDigit(c))
                {
                    if (actual.Length > 0) { palabras.Add(actual.ToString()); actual.Clear(); }
                    continue;
                }
                bool nuevaPalabra = char.IsUpper(c) && actual.Length > 0
                    && (!char.IsUpper(nombre[i - 1]) || (i + 1 < nombre.Length && char.IsLower(nombre[i + 1])));
                if (nuevaPalabra) { palabras.Add(actual.ToString()); actual.Clear(); }
                actual.Append(c);
            }
            if (actual.Length > 0) palabras.Add(actual.ToString());
            return palabras;
        }

        // ------------------------------------------------------------------ decisiones
        /// <summary>
        /// Categoría por la que se bloquea un espacio de nombres, o null si está permitido (del todo o en parte). Lo usan
        /// los using del script y la revisión de cada tipo.
        /// </summary>
        public static string CategoriaEspacio(string espacio)
        {
            espacio = espacio ?? "";
            if (espacio.StartsWith("Autodesk", StringComparison.Ordinal))
            {
                var segmentos = espacio.Split('.');
                if (segmentos.Any(s => SegmentosAutodeskBloqueados.Contains(s)))
                    return Prefijo(espacio, PrefijosBloqueados) ?? (segmentos.Contains("Windows") ? Interfaz : segmentos.Contains("Interop") ? Procesos : Proceso);
            }
            string bloqueo = Prefijo(espacio, PrefijosBloqueados);
            if (bloqueo != null && !TiposPermitidosEn.ContainsKey(espacio)) return bloqueo;
            if (EspaciosSystemPermitidos.Contains(espacio) || TiposPermitidosEn.ContainsKey(espacio)) return null;
            if (PrefijosAutodeskPermitidos.Any(p => espacio == p || espacio.StartsWith(p + ".", StringComparison.Ordinal))) return null;
            return ListaBlanca;
        }

        /// <summary>True si el espacio de nombres tiene una lista cerrada de tipos permitidos (System.IO, System.Diagnostics).</summary>
        public static bool EsEspacioParcial(string espacio) => espacio != null && TiposPermitidosEn.ContainsKey(espacio);

        /// <summary>
        /// Categoría por la que se bloquea un tipo (por su espacio de nombres y su nombre corto sin genéricos), o null si
        /// está permitido. 'nombreCompleto' = espacio + "." + nombre. La Fachada y CodigoVigilante se deciden en el revisor.
        /// </summary>
        public static string CategoriaTipo(string espacio, string nombre)
        {
            string completo = string.IsNullOrEmpty(espacio) ? nombre : espacio + "." + nombre;
            if (TiposBloqueados.TryGetValue(completo, out string cat)) return cat;
            if (espacio != null && TiposPermitidosEn.TryGetValue(espacio, out string[] tipos))
                return tipos.Contains(nombre) ? null : Prefijo(espacio, PrefijosBloqueados) ?? ListaBlanca;
            return CategoriaEspacio(espacio);
        }

        /// <summary>
        /// Categoría por la que se bloquea un miembro de un tipo permitido, o null. 'declarante' es el nombre completo del
        /// tipo que declara el miembro; 'relacionados', los del tipo sobre el que se usa (el de la expresión a la izquierda
        /// del punto), los del declarante y los de sus bases: así F.Db.Dispose() cuenta como Database.Dispose aunque
        /// Dispose esté declarado en DisposableWrapper. 'escritura' indica que el código asigna la propiedad.
        /// </summary>
        public static string CategoriaMiembro(string declarante, IList<string> relacionados, string miembro, bool escritura)
        {
            if (declarante != null && MiembrosPermitidosDe.TryGetValue(declarante, out var permitidos) && !permitidos.permitidos.Contains(miembro))
                return permitidos.categoria;
            var tiposCompletos = new List<string>();
            if (declarante != null) tiposCompletos.Add(declarante);
            if (relacionados != null) tiposCompletos.AddRange(relacionados);
            foreach (var t in tiposCompletos.Distinct())
            {
                if (MiembrosSystemBloqueados.TryGetValue(t, out var b) && b.miembros.Contains(miembro)) return b.categoria;
                if (!t.StartsWith("Autodesk.", StringComparison.Ordinal)) continue;
                string corto = t.Substring(t.LastIndexOf('.') + 1);
                foreach (var r in MiembrosAutodeskBloqueados)
                    if (r.Coincide(corto, miembro, escritura)) return r.Categoria;
            }
            return null;
        }

        private static string Prefijo(string espacio, (string prefijo, string categoria)[] lista)
        {
            foreach (var (prefijo, categoria) in lista)
                if (espacio == prefijo || espacio.StartsWith(prefijo + ".", StringComparison.Ordinal)) return categoria;
            return null;
        }

        // ------------------------------------------------------------------ solo lectura (desde 1.17.6)
        // Con solo_lectura=true el script corre con el dibujo bloqueado para lectura, en contexto de aplicación, con la
        // transacción abortada al final y sin copia ni entrada en Deshacer. Estas reglas son la primera capa: rechazan al
        // compilar las formas habituales de escribir. No pueden ser completas (la API de Civil 3D tiene cientos de métodos
        // que escriben): lo que se les escape lo para el bloqueo de lectura y, si aun así cambia algo, la comprobación de
        // después (las cinco fuentes de CodigoCambios y DBMOD) responde error. Sin solo_lectura no bloquean nada: solo se
        // cuentan, para el aviso que sugiere solo_lectura cuando un script no escribe.

        /// <summary>Miembros de la Fachada que escriben: F.Agregar (añade al dibujo) y F.EscribirTexto (escribe un archivo).</summary>
        public static readonly HashSet<string> FachadaQueEscribe = new HashSet<string>(StringComparer.Ordinal) { "Agregar", "EscribirTexto" };

        /// <summary>F.Abrir(id, escribir): con solo_lectura, 'escribir' tiene que faltar o ser la constante false.</summary>
        public const string FachadaAbrir = "Abrir";
        public const string ParametroEscribir = "escribir";

        /// <summary>Tipo del modo de apertura: con solo_lectura, toda expresión de este tipo tiene que ser la constante ForRead.</summary>
        public const string TipoOpenMode = "Autodesk.AutoCAD.DatabaseServices.OpenMode";
        public const string OpenModeLectura = "ForRead";

        /// <summary>
        /// Métodos que escriben o se saltan el bloqueo de lectura, en cualquier tipo de Autodesk (por nombre exacto):
        /// abrir para escritura un objeto ya abierto, borrar, añadir al dibujo, intercambiar o clonar objetos, auditar
        /// (corrige errores) y bloquear el documento (LockDocument pide bloqueo de escritura).
        /// </summary>
        public static readonly HashSet<string> MetodosQueEscriben = new HashSet<string>(StringComparer.Ordinal)
        {
            "UpgradeOpen", "UpgradeFromNotify", "Erase", "AppendEntity", "AddNewlyCreatedDBObject", "SwapIdWith", "HandOverTo",
            "DeepCloneObjects", "WblockCloneObjects", "Wblock", "Audit", "LockDocument"
        };

        /// <summary>
        /// Prefijos de métodos que escriben (Add..., Create..., Set...) en los tipos del dibujo: DBObject y derivados,
        /// Database, Transaction, TransactionManager, Editor (SetCurrentView, SetImpliedSelection) y cualquier tipo de
        /// Civil 3D. El prefijo cuenta si va solo o seguido de mayúscula, dígito o '_' («SetTargets», no «Settings»).
        /// Lo que vive solo en memoria (Point3d, Extents3d.AddExtents, ObjectIdCollection.Add) no se toca.
        /// </summary>
        public static readonly string[] PrefijosQueEscriben =
        {
            "Add", "Append", "Create", "Remove", "Delete", "Set", "Rebuild", "Paste", "Import", "Update", "Rename", "Insert",
            "Extract", "Clear", "Reverse", "Join", "Transform"
        };

        /// <summary>Tipos de AutoCAD (nombre completo) en los que, con sus derivados, cuentan los prefijos y las asignaciones.</summary>
        public static readonly HashSet<string> TiposDelDibujo = new HashSet<string>(StringComparer.Ordinal)
        {
            "Autodesk.AutoCAD.DatabaseServices.DBObject",
            "Autodesk.AutoCAD.DatabaseServices.Database",
            "Autodesk.AutoCAD.DatabaseServices.Transaction",
            "Autodesk.AutoCAD.DatabaseServices.TransactionManager",
            "Autodesk.AutoCAD.EditorInput.Editor",
        };

        /// <summary>
        /// Categoría SoloLectura si este uso de un miembro escribe, o null. 'tipos': nombres completos del tipo sobre el que
        /// se usa y de todas sus bases, y del que declara el miembro y sus bases. 'escritura': se asigna (propiedad o
        /// indexador). 'esMetodo': es una llamada a un método (no una propiedad).
        /// </summary>
        public static string CategoriaSoloLectura(IEnumerable<string> tipos, string miembro, bool escritura, bool esMetodo)
        {
            var lista = (tipos ?? Enumerable.Empty<string>()).Where(t => t != null).ToList();
            if (!lista.Any(t => t.StartsWith("Autodesk.", StringComparison.Ordinal))) return null;
            if (esMetodo && MetodosQueEscriben.Contains(miembro ?? "")) return SoloLectura;
            bool delDibujo = lista.Any(t => TiposDelDibujo.Contains(t) || t.StartsWith("Autodesk.Civil.", StringComparison.Ordinal));
            if (!delDibujo) return null;
            if (escritura) return SoloLectura;
            if (esMetodo && PrefijosQueEscriben.Any(p => EmpiezaPorPalabra(miembro, p))) return SoloLectura;
            return null;
        }

        /// <summary>True si 'nombre' empieza por 'prefijo' como palabra: solo, o seguido de mayúscula, dígito o '_'.</summary>
        public static bool EmpiezaPorPalabra(string nombre, string prefijo)
        {
            if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(prefijo) || !nombre.StartsWith(prefijo, StringComparison.Ordinal)) return false;
            if (nombre.Length == prefijo.Length) return true;
            char c = nombre[prefijo.Length];
            return char.IsUpper(c) || char.IsDigit(c) || c == '_';
        }
    }

    /// <summary>Un miembro bloqueado de la API de Autodesk (ver CodigoReglas.MiembrosAutodeskBloqueados).</summary>
    public sealed class ReglaMiembro
    {
        public string Tipo { get; }
        public string Miembro { get; }
        public string Categoria { get; }
        public bool SoloEscritura { get; }
        public bool EsPrefijo { get; }
        public string[] Excepto { get; }

        public ReglaMiembro(string tipo, string miembro, string categoria, bool soloEscritura = false, bool prefijo = false, string[] excepto = null)
        {
            Tipo = tipo; Miembro = miembro; Categoria = categoria; SoloEscritura = soloEscritura; EsPrefijo = prefijo; Excepto = excepto ?? new string[0];
        }

        public bool Coincide(string tipoCorto, string miembro, bool escritura)
        {
            if (!string.Equals(tipoCorto, Tipo, StringComparison.Ordinal)) return false;
            if (SoloEscritura && !escritura) return false;
            if (Excepto.Contains(miembro)) return false;
            return EsPrefijo ? miembro.StartsWith(Miembro, StringComparison.Ordinal) : string.Equals(miembro, Miembro, StringComparison.Ordinal);
        }
    }
}
