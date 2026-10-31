using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Microsoft.Data.Sqlite;

namespace ExtractorCatalogo
{
    /// <summary>Conteos que devuelve una extracción (los mismos que imprime Program.cs al terminar).</summary>
    public sealed class ResultadoExtraccion
    {
        public int Ensamblados;
        public int Tipos;
        public int Metodos;
        public int Propiedades;
        public int Parametros;
        public int ArchivosSaltados;
        public int Avisos;
        public long TamanoSalida;
        public string Salida;
        public List<string> Mensajes = new List<string>();
    }

    /// <summary>
    /// Lee ensamblados .NET SOLO como metadatos (MetadataLoadContext: nunca los carga en el proceso ni ejecuta nada
    /// de ellos) y guarda sus tipos y miembros públicos en una base SQLite con índice de texto FTS5.
    /// Pensado para las DLL de AutoCAD y Civil 3D 2027, pero sirve para cualquier ensamblado .NET.
    /// </summary>
    public static class Extractor
    {
        /// <summary>DLL que referencia el plugin (HintPath de ArbaMcp/ArbaMcp.csproj). Se buscan en cada carpeta
        /// de --dll y en sus subcarpetas (AecBaseMgd está en ACA\ y AeccDbMgd en C3D\).</summary>
        public static readonly string[] DllPorDefecto =
        {
            "acdbmgd.dll", "acmgd.dll", "accoremgd.dll", "AeccDbMgd.dll", "AecBaseMgd.dll", "AdWindows.dll"
        };

        /// <summary>True si el SQLite que trae Microsoft.Data.Sqlite se compiló con FTS5.</summary>
        public static bool Fts5Disponible()
        {
            using var con = new SqliteConnection("Data Source=:memory:");
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "CREATE VIRTUAL TABLE prueba_fts USING fts5(texto)";
            try { cmd.ExecuteNonQuery(); return true; }
            catch (SqliteException) { return false; }
        }

        /// <summary>
        /// Extrae el catálogo. <paramref name="carpetas"/>: carpetas donde buscar las DLL (y donde resolver las que
        /// referencian). <paramref name="salida"/>: archivo .sqlite a crear (se sobrescribe). <paramref name="patrones"/>:
        /// patrones extra (por ejemplo "Aecc*.dll") que se añaden a las DLL por defecto; con
        /// <paramref name="soloPatrones"/> = true se procesan únicamente los patrones (lo usan las pruebas).
        /// </summary>
        public static ResultadoExtraccion Extraer(IEnumerable<string> carpetas, string salida, IEnumerable<string> patrones = null, bool soloPatrones = false)
        {
            var r = new ResultadoExtraccion { Salida = Path.GetFullPath(salida) };
            var listaCarpetas = (carpetas ?? Array.Empty<string>()).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (listaCarpetas.Count == 0) throw new ArgumentException("Indica al menos una carpeta con --dll.");
            foreach (var c in listaCarpetas)
                if (!Directory.Exists(c)) throw new DirectoryNotFoundException("No existe la carpeta: " + c);

            // 1) Qué archivos procesar.
            var aProcesar = ElegirArchivos(listaCarpetas, patrones, soloPatrones, r);

            // 2) Con qué resolver las referencias entre ensamblados (runtime de .NET + todo lo que haya en las carpetas).
            var resolver = new PathAssemblyResolver(ArchivosParaResolver(listaCarpetas, aProcesar));

            // 3) Base de datos nueva.
            var dir = Path.GetDirectoryName(r.Salida);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(r.Salida)) File.Delete(r.Salida);

            using (var mlc = new MetadataLoadContext(resolver))
            using (var con = new SqliteConnection("Data Source=" + r.Salida))
            {
                con.Open();
                CrearEsquema(con);
                using (var tx = con.BeginTransaction())
                {
                    var escritor = new Escritor(con, tx);
                    foreach (var archivo in aProcesar)
                    {
                        Assembly asm;
                        try { asm = mlc.LoadFromAssemblyPath(archivo); }
                        catch (BadImageFormatException) { r.ArchivosSaltados++; r.Mensajes.Add("Saltado (no es un ensamblado .NET): " + Path.GetFileName(archivo)); continue; }
                        catch (FileLoadException ex) { r.ArchivosSaltados++; r.Mensajes.Add("Saltado (" + ex.Message + "): " + Path.GetFileName(archivo)); continue; }
                        ExtraerEnsamblado(asm, archivo, escritor, r);
                    }
                    escritor.RellenarBusqueda();
                    escritor.GuardarInfo(r);
                    tx.Commit();
                }
            }
            SqliteConnection.ClearAllPools();
            r.TamanoSalida = new FileInfo(r.Salida).Length;
            return r;
        }

        // ------------------------------------------------------------------------------------------------
        // Selección de archivos
        // ------------------------------------------------------------------------------------------------

        private static List<string> ElegirArchivos(List<string> carpetas, IEnumerable<string> patrones, bool soloPatrones, ResultadoExtraccion r)
        {
            var elegidos = new List<string>();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Agregar(string ruta) { if (vistos.Add(Path.GetFileName(ruta))) elegidos.Add(ruta); }

            if (!soloPatrones)
            {
                foreach (var nombre in DllPorDefecto)
                {
                    string encontrado = null;
                    foreach (var c in carpetas)
                    {
                        encontrado = Directory.EnumerateFiles(c, nombre, SearchOption.AllDirectories).FirstOrDefault();
                        if (encontrado != null) break;
                    }
                    if (encontrado != null) Agregar(encontrado);
                    else r.Mensajes.Add("No se encontró " + nombre + " en las carpetas indicadas (se sigue sin ella).");
                }
            }
            foreach (var patron in patrones ?? Array.Empty<string>())
            {
                int antes = elegidos.Count;
                foreach (var c in carpetas)
                    foreach (var f in Directory.EnumerateFiles(c, patron, SearchOption.AllDirectories).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                        Agregar(f);
                if (elegidos.Count == antes) r.Mensajes.Add("El patrón '" + patron + "' no añadió ningún archivo nuevo.");
            }
            return elegidos;
        }

        /// <summary>Todas las DLL que el cargador puede usar para resolver referencias: las del runtime de .NET, las
        /// de escritorio de Windows (WPF, para AdWindows) si existen, y las de cada carpeta y sus subcarpetas
        /// directas. Si dos tienen el mismo nombre, gana la primera que aparece.</summary>
        private static IEnumerable<string> ArchivosParaResolver(List<string> carpetas, List<string> aProcesar)
        {
            var porNombre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Agregar(string ruta) { var n = Path.GetFileNameWithoutExtension(ruta); if (!porNombre.ContainsKey(n)) porNombre[n] = ruta; }
            void AgregarCarpeta(string c, SearchOption op)
            {
                if (!Directory.Exists(c)) return;
                foreach (var f in Directory.EnumerateFiles(c, "*.dll", op)) Agregar(f);
            }

            foreach (var f in aProcesar) Agregar(f);
            string runtime = RuntimeEnvironment.GetRuntimeDirectory();
            AgregarCarpeta(runtime, SearchOption.TopDirectoryOnly);
            // Carpeta hermana Microsoft.WindowsDesktop.App\<misma versión>, presente en Windows con el SDK instalado.
            try
            {
                string version = Path.GetFileName(Path.TrimEndingDirectorySeparator(runtime));
                string compartido = Path.GetDirectoryName(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(runtime)));
                if (compartido != null) AgregarCarpeta(Path.Combine(compartido, "Microsoft.WindowsDesktop.App", version), SearchOption.TopDirectoryOnly);
            }
            catch (Exception) { /* sin carpeta de escritorio: se sigue igual */ }
            foreach (var c in carpetas)
            {
                AgregarCarpeta(c, SearchOption.TopDirectoryOnly);
                foreach (var sub in Directory.EnumerateDirectories(c)) AgregarCarpeta(sub, SearchOption.TopDirectoryOnly);
            }
            return porNombre.Values;
        }

        // ------------------------------------------------------------------------------------------------
        // Extracción de un ensamblado
        // ------------------------------------------------------------------------------------------------

        private static void ExtraerEnsamblado(Assembly asm, string archivo, Escritor w, ResultadoExtraccion r)
        {
            var nombre = asm.GetName();
            var docs = Documentacion.Cargar(Path.ChangeExtension(archivo, ".xml"));
            long ensambladoId = w.Ensamblado(nombre.Name, Path.GetFileName(archivo), nombre.Version?.ToString() ?? "", docs.Archivo);
            r.Ensamblados++;

            Type[] tipos;
            try { tipos = asm.GetTypes(); }
            catch (ReflectionTypeLoadException ex)
            {
                tipos = ex.Types.Where(t => t != null).ToArray();
                r.Avisos++;
                r.Mensajes.Add("Aviso: " + nombre.Name + " no pudo listar todos sus tipos (" + ex.LoaderExceptions.Length + " fallos); se extraen " + tipos.Length + ".");
            }

            foreach (var t in tipos.Where(t => t.IsPublic || t.IsNestedPublic).OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                try { ExtraerTipo(t, ensambladoId, docs, w, r); }
                catch (Exception ex)
                {
                    r.Avisos++;
                    r.Mensajes.Add("Aviso: no se pudo extraer el tipo " + Seguro(() => t.FullName, t.Name) + ": " + ex.GetType().Name + " " + ex.Message);
                }
            }
        }

        private const BindingFlags Declarados = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static void ExtraerTipo(Type t, long ensambladoId, Documentacion docs, Escritor w, ResultadoExtraccion r)
        {
            string nombreCompleto = t.FullName ?? t.Name;
            string espacio = t.Namespace ?? "";
            string clase = t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct"
                : EsDelegado(t) ? "delegado" : "clase";
            string tipoBase = Seguro(() => t.BaseType == null ? "" : NombreTipo(t.BaseType), "?");
            var (obsoleto, mensaje) = Obsoleto(t);
            string idDoc = "T:" + Documentacion.IdTipo(t);
            var (descripcion, fuente) = docs.Buscar(idDoc);

            long tipoId = w.Tipo(t.Name, nombreCompleto, espacio, ensambladoId, clase, tipoBase, obsoleto, mensaje, descripcion, fuente);
            r.Tipos++;
            if (clase == "enum" || clase == "delegado") return;   // sus miembros son los heredados o el Invoke: no aportan

            // Constructores públicos (se guardan como métodos con es_constructor = 1).
            foreach (var c in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                var (obs, msg) = Obsoleto(c);
                var (d, f) = docs.Buscar("M:" + Documentacion.IdMetodo(c));
                long metodoId = w.Metodo(tipoId, t.Name, false, nombreCompleto, true, obs, msg, d, f, Firma(c, t.Name));
                r.Metodos++;
                r.Parametros += w.Parametros(metodoId, c.GetParameters());
            }

            foreach (var m in t.GetMethods(Declarados).Where(m => !m.IsSpecialName).OrderBy(m => m.Name, StringComparer.Ordinal))
            {
                var (obs, msg) = Obsoleto(m);
                var (d, f) = docs.Buscar("M:" + Documentacion.IdMetodo(m));
                string retorno = Seguro(() => NombreTipo(m.ReturnType), "?");
                long metodoId = w.Metodo(tipoId, m.Name, m.IsStatic, retorno, false, obs, msg, d, f, Firma(m, m.Name));
                r.Metodos++;
                r.Parametros += w.Parametros(metodoId, m.GetParameters());
            }

            foreach (var p in t.GetProperties(Declarados).OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var getter = p.GetGetMethod(false);
                var setter = p.GetSetMethod(false);
                if (getter == null && setter == null) continue;   // sin accesores públicos
                bool estatico = (getter ?? setter).IsStatic;
                var (obs, msg) = Obsoleto(p);
                var (d, f) = docs.Buscar("P:" + Documentacion.IdPropiedad(p));
                string tipoDato = Seguro(() => NombreTipo(p.PropertyType), "?");
                w.Propiedad(tipoId, p.Name, tipoDato, estatico, getter != null, setter != null, obs, msg, d, f);
                r.Propiedades++;
            }
        }

        private static bool EsDelegado(Type t)
        {
            return Seguro(() =>
            {
                for (var b = t.BaseType; b != null; b = b.BaseType)
                    if (b.FullName == "System.MulticastDelegate" || b.FullName == "System.Delegate") return true;
                return false;
            }, false);
        }

        /// <summary>Busca [Obsolete] en los metadatos (sin instanciar el atributo) y devuelve su mensaje.</summary>
        private static (bool, string) Obsoleto(MemberInfo m)
        {
            try
            {
                foreach (var a in m.GetCustomAttributesData())
                {
                    if (a.AttributeType.FullName != "System.ObsoleteAttribute") continue;
                    string msg = a.ConstructorArguments.Count > 0 ? a.ConstructorArguments[0].Value as string : null;
                    return (true, msg ?? "");
                }
            }
            catch (Exception) { /* atributo de un ensamblado no resoluble: se considera no obsoleto */ }
            return (false, "");
        }

        /// <summary>Nombre legible de un tipo: espacio de nombres y nombre, con los argumentos genéricos entre &lt; &gt;.</summary>
        internal static string NombreTipo(Type t)
        {
            if (t.IsByRef) return NombreTipo(t.GetElementType()) + "&";
            if (t.IsPointer) return NombreTipo(t.GetElementType()) + "*";
            if (t.IsArray) return NombreTipo(t.GetElementType()) + "[" + new string(',', t.GetArrayRank() - 1) + "]";
            if (t.IsGenericParameter) return t.Name;
            string nombre = t.FullName ?? t.Name;
            if (t.IsGenericType)
            {
                string baseNombre = (t.Namespace == null ? "" : t.Namespace + ".") + t.Name.Split('`')[0];
                if (t.IsNested) baseNombre = (t.FullName ?? t.Name).Split('[')[0].Split('`')[0];
                return baseNombre + "<" + string.Join(", ", t.GetGenericArguments().Select(NombreTipo)) + ">";
            }
            return nombre;
        }

        private static string Firma(MethodBase m, string nombre)
        {
            return Seguro(() => nombre + "(" + string.Join(", ", m.GetParameters().Select(p => NombreTipo(p.ParameterType) + " " + p.Name)) + ")", nombre + "(?)");
        }

        private static T Seguro<T>(Func<T> f, T siFalla)
        {
            try { return f(); } catch (Exception) { return siFalla; }
        }

        // ------------------------------------------------------------------------------------------------
        // SQLite
        // ------------------------------------------------------------------------------------------------

        private static void CrearEsquema(SqliteConnection con)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE catalogo_info (clave TEXT PRIMARY KEY, valor TEXT NOT NULL);
CREATE TABLE ensamblados (
    id INTEGER PRIMARY KEY, nombre TEXT NOT NULL, archivo TEXT NOT NULL, version TEXT NOT NULL,
    documentacion TEXT NOT NULL DEFAULT '');
CREATE TABLE espacios (id INTEGER PRIMARY KEY, nombre TEXT NOT NULL UNIQUE);
CREATE TABLE tipos (
    id INTEGER PRIMARY KEY, nombre TEXT NOT NULL, nombre_completo TEXT NOT NULL,
    espacio_id INTEGER NOT NULL REFERENCES espacios(id), ensamblado_id INTEGER NOT NULL REFERENCES ensamblados(id),
    clase TEXT NOT NULL, tipo_base TEXT NOT NULL DEFAULT '',
    obsoleto INTEGER NOT NULL DEFAULT 0, mensaje_obsoleto TEXT NOT NULL DEFAULT '',
    descripcion TEXT NOT NULL DEFAULT '', fuente_descripcion TEXT NOT NULL DEFAULT '');
CREATE TABLE metodos (
    id INTEGER PRIMARY KEY, tipo_id INTEGER NOT NULL REFERENCES tipos(id), nombre TEXT NOT NULL,
    estatico INTEGER NOT NULL DEFAULT 0, tipo_retorno TEXT NOT NULL DEFAULT '', es_constructor INTEGER NOT NULL DEFAULT 0,
    firma TEXT NOT NULL DEFAULT '',
    obsoleto INTEGER NOT NULL DEFAULT 0, mensaje_obsoleto TEXT NOT NULL DEFAULT '',
    descripcion TEXT NOT NULL DEFAULT '', fuente_descripcion TEXT NOT NULL DEFAULT '');
CREATE TABLE propiedades (
    id INTEGER PRIMARY KEY, tipo_id INTEGER NOT NULL REFERENCES tipos(id), nombre TEXT NOT NULL,
    tipo_dato TEXT NOT NULL DEFAULT '', estatico INTEGER NOT NULL DEFAULT 0,
    lectura INTEGER NOT NULL DEFAULT 0, escritura INTEGER NOT NULL DEFAULT 0,
    obsoleto INTEGER NOT NULL DEFAULT 0, mensaje_obsoleto TEXT NOT NULL DEFAULT '',
    descripcion TEXT NOT NULL DEFAULT '', fuente_descripcion TEXT NOT NULL DEFAULT '');
CREATE TABLE parametros (
    id INTEGER PRIMARY KEY, metodo_id INTEGER NOT NULL REFERENCES metodos(id),
    posicion INTEGER NOT NULL, nombre TEXT NOT NULL, tipo TEXT NOT NULL);
CREATE TABLE glosario (
    termino TEXT PRIMARY KEY, sinonimos TEXT NOT NULL DEFAULT '', concepto_api TEXT NOT NULL DEFAULT '',
    nota TEXT NOT NULL DEFAULT '');
CREATE INDEX ix_tipos_nombre ON tipos(nombre);
CREATE INDEX ix_tipos_completo ON tipos(nombre_completo);
CREATE INDEX ix_metodos_tipo ON metodos(tipo_id);
CREATE INDEX ix_metodos_nombre ON metodos(nombre);
CREATE INDEX ix_propiedades_tipo ON propiedades(tipo_id);
CREATE INDEX ix_propiedades_nombre ON propiedades(nombre);
CREATE INDEX ix_parametros_metodo ON parametros(metodo_id);
-- Índice de texto completo: una fila por tipo, método o propiedad. 'clase' dice en qué tabla está 'ref_id'.
CREATE VIRTUAL TABLE busqueda USING fts5(
    clase UNINDEXED, ref_id UNINDEXED, nombre, nombre_completo, descripcion, tokenize = 'unicode61');";
            cmd.ExecuteNonQuery();
        }

        /// <summary>Comandos preparados para insertar en cada tabla dentro de la misma transacción.</summary>
        private sealed class Escritor
        {
            private readonly SqliteConnection _con;
            private readonly SqliteTransaction _tx;
            private readonly Dictionary<string, long> _espacios = new Dictionary<string, long>(StringComparer.Ordinal);

            public Escritor(SqliteConnection con, SqliteTransaction tx) { _con = con; _tx = tx; }

            private long Insertar(string sql, params (string, object)[] args)
            {
                using var cmd = _con.CreateCommand();
                cmd.Transaction = _tx;
                cmd.CommandText = sql + "; SELECT last_insert_rowid();";
                foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v ?? "");
                return (long)cmd.ExecuteScalar();
            }

            public long Ensamblado(string nombre, string archivo, string version, string documentacion) =>
                Insertar("INSERT INTO ensamblados (nombre, archivo, version, documentacion) VALUES ($n, $a, $v, $d)",
                    ("$n", nombre), ("$a", archivo), ("$v", version), ("$d", documentacion));

            private long Espacio(string nombre)
            {
                if (_espacios.TryGetValue(nombre, out long id)) return id;
                id = Insertar("INSERT INTO espacios (nombre) VALUES ($n)", ("$n", nombre));
                _espacios[nombre] = id;
                return id;
            }

            public long Tipo(string nombre, string completo, string espacio, long ensambladoId, string clase, string tipoBase,
                bool obsoleto, string mensaje, string descripcion, string fuente) =>
                Insertar("INSERT INTO tipos (nombre, nombre_completo, espacio_id, ensamblado_id, clase, tipo_base, obsoleto, mensaje_obsoleto, descripcion, fuente_descripcion) " +
                         "VALUES ($n, $c, $e, $a, $k, $b, $o, $m, $d, $f)",
                    ("$n", nombre), ("$c", completo), ("$e", Espacio(espacio)), ("$a", ensambladoId), ("$k", clase), ("$b", tipoBase),
                    ("$o", obsoleto ? 1 : 0), ("$m", mensaje), ("$d", descripcion), ("$f", fuente));

            public long Metodo(long tipoId, string nombre, bool estatico, string retorno, bool constructor, bool obsoleto, string mensaje,
                string descripcion, string fuente, string firma) =>
                Insertar("INSERT INTO metodos (tipo_id, nombre, estatico, tipo_retorno, es_constructor, firma, obsoleto, mensaje_obsoleto, descripcion, fuente_descripcion) " +
                         "VALUES ($t, $n, $s, $r, $c, $g, $o, $m, $d, $f)",
                    ("$t", tipoId), ("$n", nombre), ("$s", estatico ? 1 : 0), ("$r", retorno), ("$c", constructor ? 1 : 0), ("$g", firma),
                    ("$o", obsoleto ? 1 : 0), ("$m", mensaje), ("$d", descripcion), ("$f", fuente));

            public int Parametros(long metodoId, ParameterInfo[] parametros)
            {
                int n = 0;
                foreach (var p in parametros)
                {
                    string tipo = Seguro(() => NombreTipo(p.ParameterType), "?");
                    Insertar("INSERT INTO parametros (metodo_id, posicion, nombre, tipo) VALUES ($m, $p, $n, $t)",
                        ("$m", metodoId), ("$p", p.Position), ("$n", p.Name ?? ""), ("$t", tipo));
                    n++;
                }
                return n;
            }

            public void Propiedad(long tipoId, string nombre, string tipoDato, bool estatico, bool lectura, bool escritura,
                bool obsoleto, string mensaje, string descripcion, string fuente) =>
                Insertar("INSERT INTO propiedades (tipo_id, nombre, tipo_dato, estatico, lectura, escritura, obsoleto, mensaje_obsoleto, descripcion, fuente_descripcion) " +
                         "VALUES ($t, $n, $d, $s, $l, $e, $o, $m, $x, $f)",
                    ("$t", tipoId), ("$n", nombre), ("$d", tipoDato), ("$s", estatico ? 1 : 0), ("$l", lectura ? 1 : 0), ("$e", escritura ? 1 : 0),
                    ("$o", obsoleto ? 1 : 0), ("$m", mensaje), ("$x", descripcion), ("$f", fuente));

            public void RellenarBusqueda()
            {
                using var cmd = _con.CreateCommand();
                cmd.Transaction = _tx;
                cmd.CommandText = @"
INSERT INTO busqueda (clase, ref_id, nombre, nombre_completo, descripcion)
    SELECT 'tipo', id, nombre, nombre_completo, descripcion FROM tipos;
INSERT INTO busqueda (clase, ref_id, nombre, nombre_completo, descripcion)
    SELECT 'metodo', m.id, m.nombre, t.nombre_completo || '.' || m.nombre, m.descripcion FROM metodos m JOIN tipos t ON t.id = m.tipo_id;
INSERT INTO busqueda (clase, ref_id, nombre, nombre_completo, descripcion)
    SELECT 'propiedad', p.id, p.nombre, t.nombre_completo || '.' || p.nombre, p.descripcion FROM propiedades p JOIN tipos t ON t.id = p.tipo_id;";
                cmd.ExecuteNonQuery();
            }

            public void GuardarInfo(ResultadoExtraccion r)
            {
                void Poner(string k, string v) => Insertar("INSERT INTO catalogo_info (clave, valor) VALUES ($k, $v)", ("$k", k), ("$v", v));
                Poner("generado", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss' UTC'", CultureInfo.InvariantCulture));
                Poner("extractor", "ExtractorCatalogo " + (typeof(Extractor).Assembly.GetName().Version?.ToString() ?? ""));
                Poner("ensamblados", r.Ensamblados.ToString(CultureInfo.InvariantCulture));
                Poner("tipos", r.Tipos.ToString(CultureInfo.InvariantCulture));
                Poner("metodos", r.Metodos.ToString(CultureInfo.InvariantCulture));
                Poner("propiedades", r.Propiedades.ToString(CultureInfo.InvariantCulture));
                Poner("archivos_saltados", r.ArchivosSaltados.ToString(CultureInfo.InvariantCulture));
            }
        }

        // ------------------------------------------------------------------------------------------------
        // Documentación XML (<nombre>.xml junto a la DLL)
        // ------------------------------------------------------------------------------------------------

        /// <summary>Lee los &lt;summary&gt; de un archivo de documentación XML de .NET, indexados por su id ("T:...", "M:...", "P:...").</summary>
        private sealed class Documentacion
        {
            private readonly Dictionary<string, string> _resumenes = new Dictionary<string, string>(StringComparer.Ordinal);
            public string Archivo { get; private set; } = "";

            public static Documentacion Cargar(string rutaXml)
            {
                var d = new Documentacion();
                if (!File.Exists(rutaXml)) return d;
                try
                {
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, IgnoreComments = true };
                    using var lector = XmlReader.Create(rutaXml, settings);
                    while (lector.Read())
                    {
                        if (lector.NodeType != XmlNodeType.Element || lector.Name != "member") continue;
                        string id = lector.GetAttribute("name");
                        if (string.IsNullOrEmpty(id)) continue;
                        using var sub = lector.ReadSubtree();
                        var doc = new XmlDocument();
                        doc.Load(sub);
                        var summary = doc.DocumentElement?["summary"];
                        if (summary != null) d._resumenes[id] = Limpiar(summary.InnerText);
                    }
                    d.Archivo = Path.GetFileName(rutaXml);
                }
                catch (Exception) { d._resumenes.Clear(); d.Archivo = ""; }
                return d;
            }

            private static string Limpiar(string s) => string.Join(" ", s.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

            public (string, string) Buscar(string id)
            {
                return _resumenes.TryGetValue(id, out var s) ? (s, Archivo) : ("", "");
            }

            // Ids con el formato de la documentación XML de .NET: T:Espacio.Tipo, P:Espacio.Tipo.Prop, M:Espacio.Tipo.Metodo(Tipo1,Tipo2).
            public static string IdTipo(Type t) => (t.FullName ?? t.Name).Replace('+', '.');
            public static string IdPropiedad(PropertyInfo p) => IdTipo(p.DeclaringType) + "." + p.Name;

            public static string IdMetodo(MethodBase m)
            {
                var sb = new StringBuilder();
                sb.Append(IdTipo(m.DeclaringType)).Append('.');
                sb.Append(m.IsConstructor ? "#ctor" : m.Name.Replace('.', '#'));
                if (m is MethodInfo mi && mi.IsGenericMethodDefinition) sb.Append("``").Append(mi.GetGenericArguments().Length);
                var ps = m.GetParameters();
                if (ps.Length > 0)
                    sb.Append('(').Append(string.Join(",", ps.Select(p => Seguro(() => IdTipoParametro(p.ParameterType), "?")))).Append(')');
                return sb.ToString();
            }

            private static string IdTipoParametro(Type t)
            {
                if (t.IsByRef) return IdTipoParametro(t.GetElementType()) + "@";
                if (t.IsPointer) return IdTipoParametro(t.GetElementType()) + "*";
                if (t.IsArray) return IdTipoParametro(t.GetElementType()) + "[" + string.Join(",", Enumerable.Repeat("", t.GetArrayRank())) + "]";
                if (t.IsGenericParameter) return (t.DeclaringMethod != null ? "``" : "`") + t.GenericParameterPosition;
                if (t.IsGenericType)
                {
                    string baseNombre = (t.Namespace == null ? "" : t.Namespace + ".") + t.Name.Split('`')[0];
                    return baseNombre + "{" + string.Join(",", t.GetGenericArguments().Select(IdTipoParametro)) + "}";
                }
                return IdTipo(t);
            }
        }
    }
}
