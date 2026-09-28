using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivProfile = Autodesk.Civil.DatabaseServices.Profile;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>Parámetro de una herramienta (string | number | boolean | json). Se mantiene como alias de ArbaMcp.Nucleo.Parametro para los plugins que ya lo usaban.</summary>
    public class Parametro : Nucleo.Parametro { }

    /// <summary>Herramienta expuesta por MCP: la descripción (nombre, texto, parámetros) es la del núcleo; aquí se añade cómo se ejecuta.</summary>
    public class Herramienta : DescripcionHerramienta
    {
        public Func<JsonElement, object> Ejecutar;
        public Func<JsonElement, Task<object>> EjecutarAsync;
        /// <summary>
        /// Dónde corre 'Ejecutar' (siempre en el hilo principal). Por defecto, en el contexto de comando del dibujo
        /// activo esperando a que Civil 3D esté libre; ver ContextoEjecucion en HiloPrincipal.cs.
        /// </summary>
        public ContextoEjecucion Contexto = ContextoEjecucion.Documento;
    }

    /// <summary>
    /// Registro de herramientas expuestas por MCP. Otros plugins de la pestaña ARBA pueden llamar a
    /// Herramientas.Registrar(...) desde su Initialize para añadir las suyas.
    /// </summary>
    public static partial class Herramientas
    {
        private static readonly List<Herramienta> Lista = new List<Herramienta>();

        /// <summary>Registra (o sustituye por nombre) una herramienta. Valida el nombre y los tipos de sus parámetros.</summary>
        public static void Registrar(Herramienta h)
        {
            Catalogo.Validar(h);
            lock (Lista)
            {
                Lista.RemoveAll(x => x.Nombre == h.Nombre);
                Lista.Add(h);
            }
        }

        public static Herramienta Buscar(string nombre)
        {
            lock (Lista) return Lista.FirstOrDefault(h => string.Equals(h.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Copia de las descripciones registradas, en orden, para GET /tools.</summary>
        public static List<DescripcionHerramienta> Descripciones()
        {
            lock (Lista) return Lista.Cast<DescripcionHerramienta>().ToList();
        }

        public static object Describir() => Catalogo.Describir(Descripciones());

        private static Parametro P(string nombre, string tipo, string desc, bool req = false)
            => new Parametro { name = nombre, type = tipo, description = desc, required = req };

        // ------------------------------------------------------------------ lectura de argumentos (ArbaMcp.Nucleo.Argumentos)
        private static bool Tiene(JsonElement a, string n) => Argumentos.Tiene(a, n);
        private static string Str(JsonElement a, string n, string def = null) => Argumentos.Str(a, n, def);
        private static double Num(JsonElement a, string n, double def) => Argumentos.Num(a, n, def);
        private static bool Bool(JsonElement a, string n, bool def) => Argumentos.Bool(a, n, def);
        private static string Requerido(JsonElement a, string n) => Argumentos.Requerido(a, n);
        private static List<string> ListaTextos(JsonElement a, string n) => Argumentos.Lista(a, n);
        private static JsonElement JsonArg(JsonElement a, string n) => Argumentos.Json(a, n);
        private static double? N(double v) => Argumentos.Redondear(v);

        // ------------------------------------------------------------------ acceso al dibujo
        /// <summary>
        /// Bloqueo de solo lectura del dibujo (DocumentLockMode.Read) para las lecturas y las simulaciones, que corren en
        /// contexto de aplicación. Un bloqueo de escritura fuera de un comando hace que AutoCAD anote un "Grupo de
        /// comandos" en el menú Deshacer aunque no se cambie nada (validado el 28/09/2026); el de lectura no.
        /// </summary>
        private static DocumentLock BloquearParaLeer(Document doc) => doc.LockDocument(DocumentLockMode.Read, null, null, false);

        private static Document DocActivo()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("No hay ningún dibujo abierto en Civil 3D.");
            return doc;
        }

        private static ObjectId BuscarAlineamiento(Transaction tr, string nombre)
        {
            foreach (ObjectId id in CivilApplication.ActiveDocument.GetAlignmentIds())
                if (tr.GetObject(id, OpenMode.ForRead) is CivAlignment al && string.Equals(al.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return id;
            throw new ArgumentException("No existe el alineamiento '" + nombre + "'. Usa listar_alineamientos.");
        }

        private static ObjectId BuscarPerfil(Transaction tr, ObjectId idAl, string nombre)
        {
            var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
            foreach (ObjectId id in al.GetProfileIds())
                if (tr.GetObject(id, OpenMode.ForRead) is CivProfile pr && string.Equals(pr.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return id;
            throw new ArgumentException("El alineamiento '" + al.Name + "' no tiene un perfil llamado '" + nombre + "'. Usa listar_perfiles.");
        }

        private static ObjectId BuscarSuperficie(Transaction tr, string nombre)
        {
            foreach (ObjectId id in CivilApplication.ActiveDocument.GetSurfaceIds())
                if (tr.GetObject(id, OpenMode.ForRead) is CivSurface su && string.Equals(su.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return id;
            throw new ArgumentException("No existe la superficie '" + nombre + "'. Usa listar_superficies.");
        }

        // ------------------------------------------------------------------ registro inicial
        static Herramientas()
        {
            Registrar(new Herramienta
            {
                Nombre = "ping",
                Contexto = ContextoEjecucion.Inmediato,
                Descripcion = "Comprueba que el plugin responde. Devuelve versión, dibujo activo y hora. Responde aunque Civil 3D esté ocupado.",
                Ejecutar = a =>
                {
                    var doc = AcApp.DocumentManager.MdiActiveDocument;
                    string nombre = null;
                    try { nombre = doc?.Database?.Filename; } catch { }
                    return new
                    {
                        plugin = "ArbaMcp",
                        version = typeof(Herramientas).Assembly.GetName().Version?.ToString(),
                        puerto = Servidor.Puerto,
                        dibujo = nombre,
                        hay_dibujo = doc != null,
                        hora = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "listar_alineamientos",
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Lista los alineamientos del dibujo activo con sus progresivas inicial y final y sus perfiles.",
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    var lista = new List<object>();
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId id in CivilApplication.ActiveDocument.GetAlignmentIds())
                        {
                            if (!(tr.GetObject(id, OpenMode.ForRead) is CivAlignment al)) continue;
                            var perfiles = new List<string>();
                            foreach (ObjectId pid in al.GetProfileIds())
                                if (tr.GetObject(pid, OpenMode.ForRead) is CivProfile pr) perfiles.Add(pr.Name);
                            lista.Add(new
                            {
                                nombre = al.Name,
                                inicio = N(al.StartingStation),
                                fin = N(al.EndingStation),
                                longitud = N(al.Length),
                                perfiles
                            });
                        }
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "listar_perfiles",
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Lista los perfiles de un alineamiento: nombre, tipo (EG terreno, FG rasante), progresivas y número de PVI.",
                Parametros = { P("alineamiento", "string", "Nombre del alineamiento", true) },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string nombreAl = Requerido(a, "alineamiento");
                    var lista = new List<object>();
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var idAl = BuscarAlineamiento(tr, nombreAl);
                        var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                        foreach (ObjectId pid in al.GetProfileIds())
                        {
                            if (!(tr.GetObject(pid, OpenMode.ForRead) is CivProfile pr)) continue;
                            int nPvi = 0;
                            try { nPvi = pr.PVIs.Count; } catch { }
                            lista.Add(new
                            {
                                nombre = pr.Name,
                                tipo = pr.ProfileType.ToString(),
                                inicio = N(pr.StartingStation),
                                fin = N(pr.EndingStation),
                                pvis = nPvi
                            });
                        }
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "listar_superficies",
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Lista las superficies del dibujo activo: tipo (TIN, Grid, TinVolume, GridVolume o Corridor), si está desactualizada, número de líneas de rotura y de contornos de la definición y, en superficies de corredor, el corredor que la genera.",
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    var lista = new List<object>();
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        // Superficies generadas por corredores: nombre de superficie → corredor
                        var deCorredor = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var idsDeCorredor = new Dictionary<ObjectId, string>();
                        foreach (ObjectId idCor in CivilApplication.ActiveDocument.CorridorCollection)
                        {
                            if (!(tr.GetObject(idCor, OpenMode.ForRead) is Civ.Corridor cor)) continue;
                            try
                            {
                                foreach (Civ.CorridorSurface cs in cor.CorridorSurfaces)
                                {
                                    deCorredor[cs.Name] = cor.Name;
                                    var idSu = Api.Leer<ObjectId>(cs, ObjectId.Null, "SurfaceId");
                                    if (!idSu.IsNull) idsDeCorredor[idSu] = cor.Name;
                                }
                            }
                            catch (System.Exception ex) { Historial.Registrar("listar_superficies: no se pudieron leer las superficies del corredor " + cor.Name + ": " + ex.Message); }
                        }

                        foreach (ObjectId id in CivilApplication.ActiveDocument.GetSurfaceIds())
                        {
                            if (!(tr.GetObject(id, OpenMode.ForRead) is CivSurface su)) continue;
                            string corredor = idsDeCorredor.TryGetValue(id, out string c1) ? c1 : deCorredor.TryGetValue(su.Name, out string c2) ? c2 : null;
                            lista.Add(new
                            {
                                nombre = su.Name,
                                tipo = su.GetType().Name,
                                tipo_superficie = corredor != null ? "Corridor" : TipoSuperficie(su),
                                esta_desactualizada = Api.Leer<bool?>(su, null, "IsOutOfDate", "OutOfDate"),
                                n_lineas_rotura = su is Civ.TinSurface tin ? (int?)tin.BreaklinesDefinition.Count : null,
                                n_contornos = su is Civ.TinSurface tin2 ? (int?)tin2.BoundariesDefinition.Count : su is Civ.GridSurface grid ? (int?)grid.BoundariesDefinition.Count : null,
                                corredor
                            });
                        }
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "abrir_dibujo",
                Contexto = ContextoEjecucion.Aplicacion,
                Descripcion = "Abre un archivo DWG en Civil 3D y lo deja como dibujo activo. Si ya estaba abierto, activa esa pestaña en vez de abrirlo otra vez. Espera a que Civil 3D esté libre (sin comando ni cuadro de diálogo).",
                Parametros = { P("ruta", "string", "Ruta completa del DWG", true) },
                Ejecutar = a =>
                {
                    string ruta = Requerido(a, "ruta");
                    if (!File.Exists(ruta)) throw new FileNotFoundException("No existe el archivo: " + ruta);
                    ruta = Path.GetFullPath(ruta);
                    foreach (Document d in AcApp.DocumentManager)
                    {
                        if (!string.Equals(Escritura.RutaDibujo(d), ruta, StringComparison.OrdinalIgnoreCase)) continue;
                        AcApp.DocumentManager.MdiActiveDocument = d;
                        return new { abierto = d.Name, ya_estaba_abierto = true };
                    }
                    var doc = AcApp.DocumentManager.Open(ruta, false);
                    AcApp.DocumentManager.MdiActiveDocument = doc;
                    return new { abierto = doc.Name, ya_estaba_abierto = false };
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "ejecutar_comando",
                Descripcion = "Último recurso; usa primero las herramientas de API. Envía un comando a la línea de comandos del dibujo activo y espera a que termine. Devuelve 'terminado', 'cancelado', 'fallido', 'el comando no se inició en N s' o 'timeout con ESC'. Solo con undo=true la orden va en un grupo de UNDO (un solo Ctrl+Z la revierte).",
                Parametros =
                {
                    P("comando", "string", "Texto del comando, por ejemplo 'REGEN' o '_.ZOOM E'", true),
                    P("timeout_s", "number", "Segundos máximos de espera (por defecto 60). Si se agota, se envían dos ESC para cancelar"),
                    P("undo", "boolean", "Con true envuelve la orden en _.UNDO _BE / _.UNDO _E (por defecto false)")
                },
                EjecutarAsync = a =>
                {
                    string comando = Requerido(a, "comando").Trim();
                    int timeoutMs = (int)(Math.Max(1, Num(a, "timeout_s", 60)) * 1000);
                    return EjecutarComando(comando, timeoutMs, Bool(a, "undo", false));
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "leer_historial",
                Contexto = ContextoEjecucion.Inmediato,
                Descripcion = "Devuelve las últimas líneas del historial del plugin: comandos iniciados y terminados, llamadas MCP y mensajes.",
                Parametros = { P("ultimas_n", "number", "Cantidad de líneas (por defecto 50)") },
                Ejecutar = a => Historial.Ultimas((int)Num(a, "ultimas_n", 50))
            });

            Registrar(new Herramienta
            {
                Nombre = "capturar_pantalla",
                Contexto = ContextoEjecucion.Inmediato,
                Descripcion = "Guarda una captura PNG de la ventana principal de Civil 3D (incluye cuadros de diálogo abiertos) y devuelve la ruta.",
                Parametros = { P("ruta", "string", "Ruta del PNG a crear (por defecto en la carpeta temporal)") },
                Ejecutar = a =>
                {
                    string ruta = Str(a, "ruta");
                    if (string.IsNullOrWhiteSpace(ruta))
                        ruta = Path.Combine(Path.GetTempPath(), "arba_captura_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                    var h = AcApp.MainWindow.Handle;
                    if (!GetWindowRect(h, out RECT r)) throw new InvalidOperationException("No se pudo obtener el rectángulo de la ventana.");
                    int w = Math.Max(1, r.Right - r.Left), alto = Math.Max(1, r.Bottom - r.Top);
                    using (var bmp = new Bitmap(w, alto))
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(r.Left, r.Top, 0, 0, new Size(w, alto));
                        Directory.CreateDirectory(Path.GetDirectoryName(ruta) ?? ".");
                        bmp.Save(ruta, ImageFormat.Png);
                    }
                    return new { ruta, ancho = w, alto };
                }
            });

            RegistrarAdicionales();
            RegistrarSeguridad();
            RegistrarCorredores();
            RegistrarSuperficies();
        }

        // ------------------------------------------------------------------ herramientas genéricas adicionales
        private static void RegistrarAdicionales()
        {
            Registrar(new Herramienta
            {
                Nombre = "listar_pvis",
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Devuelve la geometría vertical de un perfil: cada PVI con progresiva, cota, pendientes de entrada y salida, y la curva vertical que lo contiene (tipo y longitud) si existe.",
                Parametros =
                {
                    P("alineamiento", "string", "Nombre del alineamiento", true),
                    P("perfil", "string", "Nombre del perfil", true)
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    var lista = new List<object>();
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var idAl = BuscarAlineamiento(tr, Requerido(a, "alineamiento"));
                        var idPr = BuscarPerfil(tr, idAl, Requerido(a, "perfil"));
                        var pr = (CivProfile)tr.GetObject(idPr, OpenMode.ForRead);

                        var curvas = new List<(double ini, double fin, string tipo)>();
                        foreach (Autodesk.Civil.DatabaseServices.ProfileEntity ent in pr.Entities)
                        {
                            string tipo = ent.EntityType.ToString();
                            if (tipo.IndexOf("Tangent", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                            curvas.Add((ent.StartStation, ent.EndStation, tipo));
                        }

                        var pvis = new List<(double s, double z)>();
                        foreach (Autodesk.Civil.DatabaseServices.ProfilePVI pvi in pr.PVIs) pvis.Add((pvi.RawStation, pvi.Elevation));
                        pvis.Sort((x, y) => x.s.CompareTo(y.s));

                        for (int i = 0; i < pvis.Count; i++)
                        {
                            double? pe = i > 0 && pvis[i].s - pvis[i - 1].s > 1e-6 ? (pvis[i].z - pvis[i - 1].z) / (pvis[i].s - pvis[i - 1].s) * 100 : (double?)null;
                            double? ps = i < pvis.Count - 1 && pvis[i + 1].s - pvis[i].s > 1e-6 ? (pvis[i + 1].z - pvis[i].z) / (pvis[i + 1].s - pvis[i].s) * 100 : (double?)null;
                            string tipoCurva = null; double? lCurva = null;
                            foreach (var c in curvas)
                                if (pvis[i].s > c.ini + 1e-4 && pvis[i].s < c.fin - 1e-4) { tipoCurva = c.tipo; lCurva = c.fin - c.ini; break; }
                            lista.Add(new
                            {
                                n = i + 1,
                                progresiva = N(pvis[i].s),
                                cota = N(pvis[i].z),
                                pe_pct = pe.HasValue ? N(pe.Value) : null,
                                ps_pct = ps.HasValue ? N(ps.Value) : null,
                                a_pct = pe.HasValue && ps.HasValue ? N(Math.Abs(ps.Value - pe.Value)) : null,
                                tiene_curva = tipoCurva != null,
                                tipo_curva = tipoCurva,
                                longitud_curva = lCurva.HasValue ? N(lCurva.Value) : null
                            });
                        }
                        tr.Commit();
                    }
                    return lista;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = "leer_variable",
                Contexto = ContextoEjecucion.Inmediato,
                Descripcion = "Lee una variable de sistema de AutoCAD (por ejemplo DWGNAME, CMDACTIVE, INSUNITS, SECURELOAD).",
                Parametros = { P("nombre", "string", "Nombre de la variable de sistema", true) },
                Ejecutar = a =>
                {
                    string n = Requerido(a, "nombre");
                    object v = AcApp.GetSystemVariable(n);
                    return new { variable = n.ToUpperInvariant(), valor = v?.ToString(), tipo = v?.GetType().Name };
                }
            });
        }

        // ------------------------------------------------------------------ envío de comandos (núcleo de ejecutar_comando y de las exportaciones)
        /// <summary>
        /// Envía un comando a la línea de comandos y espera a que termine, se cancele, falle o se agote el tiempo.
        /// Con undo=true lo envuelve en _.UNDO _BE / _.UNDO _E. Corre desde el hilo del servidor y toca AutoCAD solo
        /// a través de HiloPrincipal: el envío espera a que Civil 3D esté libre (contexto Aplicacion) y los ESC y el
        /// cierre del grupo van por la cola Inmediato, que el hilo principal atiende incluso mientras un comando
        /// espera entrada. Nada de la API de AutoCAD se llama desde el hilo del servidor.
        /// </summary>
        internal static async Task<object> EjecutarComando(string comando, int timeoutMs, bool undo)
        {
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

            string cmdParse = comando.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
            cmdParse = cmdParse.TrimStart('_', '.').ToUpperInvariant();

            Document doc = null;  // lo asigna el hilo principal al enviar; el servidor lo lee solo después de esperar ese envío
            int iniciado = 0;     // se escribe en el hilo principal y se lee en el del servidor
            int grupoCerrado = 0;
            Historial.Registrar("ejecutar_comando: '" + comando + "' con tiempo máximo " + (timeoutMs / 1000) + " s" + (undo ? " (con grupo de UNDO)" : ""));

            // Cierra el grupo de UNDO (si lo hay) una sola vez, venga de donde venga (fin, cancelación, fallo o tiempo
            // agotado). Siempre en el hilo principal: en línea desde los eventos de comando, por la cola Inmediato desde el servidor.
            void CerrarGrupo(string prefijo)
            {
                if (System.Threading.Interlocked.Exchange(ref grupoCerrado, 1) != 0) return;
                string texto = prefijo + (undo ? "_.UNDO _E\n" : "");
                var d = doc;
                if (texto.Length == 0 || d == null) return;
                HiloPrincipal.EjecutarInmediato(() => d.SendStringToExecute(texto, false, false, false));
            }

            void OnCommandWillStart(object s, CommandEventArgs e)
            {
                if (e.GlobalCommandName.ToUpperInvariant() == cmdParse) System.Threading.Interlocked.Exchange(ref iniciado, 1);
            }
            void OnCommandEnded(object s, CommandEventArgs e)
            {
                if (e.GlobalCommandName.ToUpperInvariant() == cmdParse) { CerrarGrupo(""); tcs.TrySetResult("terminado"); }
            }
            void OnCommandCancelled(object s, CommandEventArgs e)
            {
                if (e.GlobalCommandName.ToUpperInvariant() == cmdParse) { CerrarGrupo(""); tcs.TrySetResult("cancelado"); }
            }
            void OnCommandFailed(object s, CommandEventArgs e)
            {
                if (e.GlobalCommandName.ToUpperInvariant() == cmdParse) { CerrarGrupo(""); tcs.TrySetResult("fallido"); }
            }

            // El envío espera a que Civil 3D esté libre (sin comando activo ni cuadro de diálogo). Si no se libera en el
            // tiempo máximo se descarta: así el comando no entra a destiempo cuando el usuario termine lo suyo.
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            var envio = HiloPrincipal.Encolar(() =>
            {
                var d = DocActivo();
                if (Convert.ToInt32(AcApp.GetSystemVariable("CMDACTIVE")) > 0)
                    throw new InvalidOperationException("Hay un comando activo en Civil 3D; termínalo o cancélalo antes de enviar otro.");

                d.CommandWillStart += OnCommandWillStart;
                d.CommandEnded += OnCommandEnded;
                d.CommandCancelled += OnCommandCancelled;
                d.CommandFailed += OnCommandFailed;
                try
                {
                    d.SendStringToExecute((undo ? "_.UNDO _BE\n" : "") + comando + "\n", true, false, false);
                }
                catch
                {
                    d.CommandWillStart -= OnCommandWillStart;
                    d.CommandEnded -= OnCommandEnded;
                    d.CommandCancelled -= OnCommandCancelled;
                    d.CommandFailed -= OnCommandFailed;
                    throw;
                }
                doc = d;
                return true;
            }, ContextoEjecucion.Aplicacion, "ejecutar_comando");

            if (await Task.WhenAny(envio.Tarea, Task.Delay(timeoutMs)) != envio.Tarea && envio.Descartar())
            {
                Historial.Registrar("ejecutar_comando: '" + comando + "' no se envió; Civil 3D siguió ocupado " + (timeoutMs / 1000) + " s");
                return "no se envió: Civil 3D siguió ocupado (comando activo o cuadro de diálogo) durante " + (timeoutMs / 1000) + " s";
            }
            await envio.Tarea; // si no hay dibujo o el envío falló, la excepción llega al servidor como error

            // Espera a que el comando arranque (o termine) dentro del tiempo máximo. Civil 3D puede tardar
            // varios segundos en procesar la cola de entrada si está ocupado con el dibujo.
            while (!tcs.Task.IsCompleted && System.Threading.Volatile.Read(ref iniciado) == 0 && reloj.ElapsedMilliseconds < timeoutMs)
                await Task.Delay(100);

            if (!tcs.Task.IsCompleted && System.Threading.Volatile.Read(ref iniciado) == 0)
            {
                // No arrancó en todo el tiempo. Los ESC van delante del cierre por si arranca tarde:
                // así el UNDO _E nunca entra como respuesta a un comando que acaba de empezar.
                Historial.Registrar("ejecutar_comando: '" + comando + "' no se inició en " + (timeoutMs / 1000) + " s");
                CerrarGrupo("\x1B\x1B");
                tcs.TrySetResult("el comando no se inició en " + (timeoutMs / 1000) + " s (¿nombre incorrecto o Civil 3D ocupado?)");
            }

            int restante = Math.Max(0, timeoutMs - (int)reloj.ElapsedMilliseconds);
            var completada = await Task.WhenAny(tcs.Task, Task.Delay(restante));

            if (completada != tcs.Task && !tcs.Task.IsCompleted)
            {
                // Los ESC y el cierre del grupo van por la cola Inmediato: el hilo principal la atiende por el
                // despachador aunque el comando esté esperando entrada del usuario.
                Historial.Registrar("ejecutar_comando: tiempo agotado a los " + (timeoutMs / 1000) + " s, se envían ESC");
                CerrarGrupo("\x1B\x1B");
                tcs.TrySetResult("timeout con ESC");
            }

            // Los manejadores se desenganchan en el hilo principal (cola Inmediato); no hace falta esperar
            _ = HiloPrincipal.EjecutarInmediato(() =>
            {
                doc.CommandWillStart -= OnCommandWillStart;
                doc.CommandEnded -= OnCommandEnded;
                doc.CommandCancelled -= OnCommandCancelled;
                doc.CommandFailed -= OnCommandFailed;
            });

            return await tcs.Task;
        }

        private static string TipoSuperficie(CivSurface su)
        {
            if (su is Civ.TinVolumeSurface) return "TinVolume";
            if (su is Civ.GridVolumeSurface) return "GridVolume";
            if (su is Civ.TinSurface) return "TIN";
            if (su is Civ.GridSurface) return "Grid";
            return su.GetType().Name;
        }

        // ------------------------------------------------------------------ Win32
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    }
}


