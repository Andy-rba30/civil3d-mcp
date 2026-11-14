using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Civ = Autodesk.Civil.DatabaseServices;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace ArbaMcp
{
    /// <summary>
    /// ejecutar_codigo (desde 1.16.0): compila un fragmento de C# con Roslyn y lo ejecuta en Civil 3D, en el hilo
    /// principal, en contexto de comando del dibujo activo y con el documento bloqueado. Desactivada por defecto
    /// (ARBA_MCP_CODIGO=1 y reiniciar Civil 3D).
    ///
    /// Recorrido de una llamada (herramienta asíncrona, como ejecutar_comando, así que no lleva la comprobación previa
    /// por simulación de 1.10.1 y la simulación también corre en contexto Documento):
    ///  1. En el hilo del servidor, sin tocar AutoCAD: activación, parámetros y compilación con la lista blanca
    ///     (ArbaMcp.Nucleo: CodigoCompilador y CodigoRevisor, con Roslyn en su propio contexto de carga). Si no compila,
    ///     responde el error sin copia, sin contexto de comando y sin entrada en Deshacer.
    ///  2. En el hilo principal (contexto Documento, una entrada Executefunction de Deshacer), por Escritura.Ejecutar:
    ///     comprobaciones previas y copia de seguridad terminada antes de ejecutar nada, también al simular (excepción
    ///     documentada en CONTRATO.md). El script corre en una transacción anidada dentro de una del plugin, con el
    ///     tiempo límite cooperativo de CodigoVigilante, en un AssemblyLoadContext coleccionable que se descarga al acabar.
    ///  3. Qué cambió: Transaction.GetAllObjects() con IsNewObject, IsModified e IsErased (camino principal) y los
    ///     eventos ObjectAppended, ObjectModified y ObjectErased de la base de datos como apoyo (cada uno en su try; si
    ///     no están, la herramienta sigue).
    ///  4. Simulación: aborta y comprueba que lo creado desapareció. Escritura real: confirma, vuelve a leer en otra
    ///     transacción y verifica lo creado y lo borrado; si no cuadra, error sin reintentar. Si el script borró una
    ///     superficie, un corredor o un ensamblaje, aborta y responde error (regla del proyecto).
    /// </summary>
    public static partial class Herramientas
    {
        private const string NombreCodigo = "ejecutar_codigo";
        private static readonly object CerrojoCodigo = new object();
        private static CargadorCompilador _cargadorCodigo;
        private static string[] _referenciasCodigo;

        private static void RegistrarCodigo()
        {
            Registrar(new Herramienta
            {
                Nombre = NombreCodigo,
                Descripcion = "Ejecuta un fragmento de C# (el cuerpo de un método) dentro de Civil 3D, en el hilo principal y con el dibujo bloqueado. Desactivada por defecto: se activa con la variable de entorno ARBA_MCP_CODIGO=1 y reiniciando Civil 3D. Úsala solo si ninguna herramienta específica ni receta lo cubre (antes de ejecutar_comando) y siempre primero con simular=true, que es el valor por defecto: el script corre en una transacción que se aborta al final, con copia de seguridad, y la respuesta dice qué se habría creado, modificado o borrado (con objetos de Civil 3D no es garantía total: la copia es la protección real). Con simular=false aplica los cambios con copia, una entrada de Deshacer y antes/después. El código ve F (Fachada: F.Doc, F.Db, F.Tr, F.Civil, F.Alineamiento(nombre), F.Perfil(eje, nombre), F.Superficie, F.Corredor, F.Ensamblaje, F.Capa, F.Bloque, F.Abrir<T>(id, escribir), F.Agregar(entidad), F.EspacioModelo(), F.Escribir(texto), F.Texto/F.Numero/F.Booleano(nombre), F.LeerTexto/F.EscribirTexto/F.ListarArchivos) y Parametros (JSON de solo lectura); lo que devuelva con return sale en 'resultado'. Using por defecto: System, System.Linq, System.Collections.Generic, System.Text, System.Globalization y los de AutoCAD (ApplicationServices, DatabaseServices, Geometry); los tipos de Civil 3D van con el alias Civ (Civ.Alignment). No compila si usa red, procesos, archivos fuera de %LOCALAPPDATA%\\ArbaMcp\\scripts, reflexión, hilos, código nativo, eventos, comandos o LISP. Tiempo límite: 60 s.",
                Parametros =
                {
                    P("codigo_csharp", "string", "Cuerpo de un método en C#: sentencias, variables y funciones locales, sin declarar clases. Puede empezar con using. Tiene F (la Fachada) y Parametros; devuelve el resultado con return. Ejemplo: var l = new Line(new Point3d(0,0,0), new Point3d(100,0,0)); F.Agregar(l); return l.Length;", true),
                    P("simular", "boolean", "Por defecto true: ejecuta dentro de una transacción que se aborta al final (con copia de seguridad) y devuelve qué se habría creado, modificado o borrado. Con false aplica los cambios (copia, una entrada de Deshacer, antes/después)"),
                    P("parametros", "json", "Objeto JSON (o texto con un objeto) que el script lee como Parametros, de solo lectura, o con F.Texto/F.Numero/F.Booleano. Ejemplo: {\"capa\":\"0\",\"largo\":100}")
                },
                EjecutarAsync = EjecutarCodigo
            });
        }

        // ------------------------------------------------------------------ búsquedas por nombre de la Fachada (mismos mensajes que las herramientas)
        internal static ObjectId FachadaAlineamiento(Transaction tr, string nombre) => BuscarAlineamiento(tr, nombre);
        internal static ObjectId FachadaPerfil(Transaction tr, string alineamiento, string nombre) => BuscarPerfil(tr, BuscarAlineamiento(tr, alineamiento), nombre);
        internal static ObjectId FachadaSuperficie(Transaction tr, string nombre) => BuscarSuperficie(tr, nombre);
        internal static ObjectId FachadaCorredor(Transaction tr, string nombre) => BuscarCorredor(tr, nombre);
        internal static ObjectId FachadaEnsamblaje(Transaction tr, string nombre) => BuscarEnsamblaje(tr, nombre);

        // ------------------------------------------------------------------ 1. hilo del servidor: activación, parámetros y compilación
        private static async Task<object> EjecutarCodigo(JsonElement a)
        {
            if (!CodigoActivacion.Activada(Environment.GetEnvironmentVariable(CodigoActivacion.Variable)))
                throw new InvalidOperationException(CodigoActivacion.MensajeDesactivada);
            string codigo = Requerido(a, "codigo_csharp");
            bool simular = Bool(a, "simular", true);
            JsonElement parametros = LeerParametrosCodigo(a);

            var reloj = Stopwatch.StartNew();
            var compilado = CompiladorCodigo().Compilar(codigo, ReferenciasCodigo(), typeof(Fachada).FullName);
            long msCompilacion = reloj.ElapsedMilliseconds;
            Historial.Registrar("ejecutar_codigo: " + (compilado.Ok ? "compilado" : "no compila") + " (" + compilado.Lineas + " líneas, " + msCompilacion + " ms"
                + (compilado.DeCache ? ", de la caché" : "") + (compilado.ReferenciasFallidas.Length > 0 ? ", referencias que no se pudieron leer: " + string.Join("; ", compilado.ReferenciasFallidas) : "") + ")");
            if (!compilado.Ok) throw new ArgumentException(compilado.Mensaje);

            // 2. Hilo principal, contexto Documento. Si Civil 3D sigue ocupado, se descarta sin ejecutarse.
            Pendiente pendiente = null;
            pendiente = HiloPrincipal.Encolar(() => EjecutarCodigoEnDibujo(a, compilado, simular, parametros, msCompilacion, pendiente), ContextoEjecucion.Documento, NombreCodigo);
            var terminada = await Task.WhenAny(pendiente.Tarea, Task.Delay(TimeSpan.FromSeconds(CodigoLimites.EsperaMaximaSegundos)));
            if (terminada != pendiente.Tarea && pendiente.Descartar())
            {
                Historial.Registrar("ejecutar_codigo: no se ejecutó; Civil 3D siguió ocupado " + CodigoLimites.EsperaMaximaSegundos + " s");
                throw new TimeoutException("ejecutar_codigo no se ejecutó: Civil 3D siguió ocupado (comando activo o cuadro de diálogo) durante " + CodigoLimites.EsperaMaximaSegundos
                    + " s. No se hizo copia ni se tocó el dibujo. Pide al usuario que termine lo que tenga abierto y repite la llamada.");
            }
            return await pendiente.Tarea;
        }

        /// <summary>'parametros': un objeto JSON, como texto (lo que manda el puente) o ya estructurado; vacío si falta.</summary>
        private static JsonElement LeerParametrosCodigo(JsonElement a)
        {
            var v = JsonArg(a, "parametros");
            if (v.ValueKind == JsonValueKind.Undefined || v.ValueKind == JsonValueKind.Null)
                using (var vacio = JsonDocument.Parse("{}")) return vacio.RootElement.Clone();
            if (v.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("El parámetro 'parametros' debe ser un objeto JSON (por ejemplo {\"capa\":\"0\",\"largo\":100}), como texto o ya estructurado; llegó un valor de tipo " + v.ValueKind + ".");
            return v;
        }

        /// <summary>Roslyn se carga una vez, en su propio contexto, desde Contents\roslyn (junto a ArbaMcp.dll).</summary>
        private static CargadorCompilador CompiladorCodigo()
        {
            lock (CerrojoCodigo)
            {
                if (_cargadorCodigo == null)
                {
                    string carpeta = Path.Combine(Path.GetDirectoryName(typeof(Herramientas).Assembly.Location) ?? ".", "roslyn");
                    _cargadorCodigo = new CargadorCompilador(carpeta, typeof(CodigoVigilante).Assembly.Location);
                }
                return _cargadorCodigo;
            }
        }

        /// <summary>
        /// DLL contra las que se compila el script: las del runtime (TRUSTED_PLATFORM_ASSEMBLIES), las de Autodesk ya
        /// cargadas desde la carpeta de AutoCAD (por Assembly.Location; no se fuerza ninguna carga desde este hilo),
        /// ArbaMcp.dll y ArbaMcp.Nucleo.dll. Lo que el script puede usar de verdad lo decide la lista blanca. Se guarda
        /// la lista cuando ya incluye AeccDbMgd.
        /// </summary>
        private static string[] ReferenciasCodigo()
        {
            lock (CerrojoCodigo) { if (_referenciasCodigo != null) return _referenciasCodigo; }
            var rutas = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            void Agregar(string ruta)
            {
                if (string.IsNullOrEmpty(ruta) || !File.Exists(ruta)) return;
                string nombre = Path.GetFileNameWithoutExtension(ruta);
                if (nombre.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase)) return;
                if (!rutas.ContainsKey(nombre)) rutas[nombre] = ruta;
            }
            if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
                foreach (var r in tpa.Split(Path.PathSeparator)) Agregar(r);
            else
            {
                string dirRuntime = Path.GetDirectoryName(typeof(object).Assembly.Location);
                if (dirRuntime != null)
                    foreach (var f in Directory.GetFiles(dirRuntime, "*.dll"))
                    {
                        string n = Path.GetFileName(f);
                        if (n.StartsWith("System.", StringComparison.OrdinalIgnoreCase) || n == "netstandard.dll" || n == "mscorlib.dll") Agregar(f);
                    }
            }
            var cargados = AppDomain.CurrentDomain.GetAssemblies().Where(x => !x.IsDynamic).Select(x => new { a = x, ruta = Ubicacion(x) })
                .Where(x => !string.IsNullOrEmpty(x.ruta)).ToList();
            string dirAcad = cargados.Where(x => x.a.GetName().Name == "acdbmgd").Select(x => Path.GetDirectoryName(x.ruta)).FirstOrDefault();
            bool hayCivil = false;
            if (dirAcad != null)
                foreach (var x in cargados.Where(x => x.ruta.StartsWith(dirAcad, StringComparison.OrdinalIgnoreCase)))
                {
                    Agregar(x.ruta);
                    if (x.a.GetName().Name == "AeccDbMgd") hayCivil = true;
                }
            Agregar(typeof(Fachada).Assembly.Location);
            Agregar(typeof(CodigoVigilante).Assembly.Location);
            var lista = rutas.Values.ToArray();
            if (hayCivil) lock (CerrojoCodigo) _referenciasCodigo = lista;
            return lista;
        }

        private static string Ubicacion(System.Reflection.Assembly a)
        {
            try { return a.Location; } catch { return null; }
        }

        // ------------------------------------------------------------------ 2. hilo principal: copia, transacciones, ejecución, verificación
        private static object EjecutarCodigoEnDibujo(JsonElement a, ResultadoAislado compilado, bool simular, JsonElement parametros, long msCompilacion, Pendiente pendiente)
            => Escritura.Ejecutar(NombreCodigo, a, ctx => CuerpoCodigo(ctx, compilado, parametros, msCompilacion, pendiente?.MsEspera),
                conCopia: true, copiaAlSimular: true, simular: simular);

        private static object CuerpoCodigo(Escritura.Contexto ctx, ResultadoAislado compilado, JsonElement parametros, long msCompilacion, long? msEspera)
        {
            var doc = ctx.Doc;
            var db = ctx.Db;
            var avisos = ctx.Avisos;
            foreach (var aviso in compilado.Avisos) avisos.Add("Compilador: " + aviso);

            // Nunca se ejecuta sin copia terminada, tampoco al simular (excepción a la regla de las simulaciones)
            ctx.EsperarCopia();

            var cambios = new CambiosScript();
            Fachada fachada = null;
            EjecucionScript ejecucion = null;
            object resultado = null;
            long msScript = 0, revisiones = 0;
            var reloj = Stopwatch.StartNew();
            try
            {
                using (doc.LockDocument())
                {
                    var t0 = db.TransactionManager.StartTransaction();
                    var pila = new PilaTransacciones(db, t0);
                    try
                    {
                        var t1 = db.TransactionManager.StartTransaction();
                        pila.AnotarScript(t1);
                        fachada = new Fachada(doc, t1, parametros, ctx.Simular, CodigoCarpeta.Predeterminada());
                        ejecucion = new EjecucionScript(compilado.Ensamblado, compilado.Pdb, compilado.Tipo, compilado.Metodo, compilado.NombreEnsamblado,
                            typeof(Fachada).Assembly, typeof(CodigoVigilante).Assembly);
                        EngancharEventos(db, cambios);
                        string parada = null;
                        try
                        {
                            using (CodigoVigilante.Iniciar(TimeSpan.FromSeconds(CodigoLimites.TiempoLimiteSegundos)))
                            {
                                try { resultado = ejecucion.Invocar(fachada, parametros); }
                                finally
                                {
                                    revisiones = CodigoVigilante.Revisiones;
                                    parada = CodigoVigilante.MotivoParada;
                                }
                            }
                            msScript = reloj.ElapsedMilliseconds;
                            // Dentro de la transacción: los objetos que devuelve el script siguen abiertos
                            resultado = CodigoResultado.Normalizar(resultado, ConvertirResultado, avisos);
                        }
                        catch (Exception ex)
                        {
                            throw ErrorDelScript(ex, parada, ctx);
                        }
                        finally
                        {
                            DesengancharEventos(db, cambios);
                            fachada.Cerrar();
                        }

                        pila.CerrarLasDelScript(avisos);
                        cambios.AgregarDeTransaccion(t0);
                        cambios.Detallar(t0);
                        string violacion = parada ?? cambios.Violacion;
                        if (violacion != null)
                        {
                            pila.AbortarEncimaDelPlugin();
                            t0.Abort();
                            throw new InvalidOperationException(violacion + SufijoNadaAplicado(ctx));
                        }
                        if (ctx.Simular) t0.Abort();
                        else t0.Commit();
                    }
                    finally
                    {
                        pila.AbortarEncimaDelPlugin();
                        t0.Dispose();
                    }
                }
            }
            finally
            {
                ejecucion?.Descargar();
            }

            // Archivos de F.EscribirTexto: solo tras confirmar una ejecución real
            List<string> escritos = new List<string>(), noEscritos = new List<string>();
            if (ctx.Simular) noEscritos = fachada.Pendientes;
            else
            {
                try { escritos = fachada.EscribirPendientes(); }
                catch (Exception ex) { avisos.Add("Los cambios del dibujo se aplicaron, pero no se pudieron escribir los archivos de F.EscribirTexto en " + CodigoCarpeta.Texto + ": " + ex.Message); }
            }

            var antes = new Dictionary<string, object> { ["objetos_creados"] = 0, ["objetos_modificados"] = 0, ["objetos_borrados"] = 0 };
            var hechos = new Dictionary<string, object> { ["objetos_creados"] = cambios.Creados.Count, ["objetos_modificados"] = cambios.Modificados.Count, ["objetos_borrados"] = cambios.Borrados.Count };
            object datos = new
            {
                objetos = new
                {
                    creados = cambios.Lista(cambios.Creados),
                    modificados = cambios.Lista(cambios.Modificados),
                    borrados = cambios.Lista(cambios.Borrados),
                    temporales = cambios.Temporales,
                    recortado = cambios.Recortado
                },
                mensajes = fachada.Mensajes,
                archivos = new { leidos = fachada.Leidos, escritos, no_escritos_por_simulacion = noEscritos, carpeta = CodigoCarpeta.Texto },
                compilacion = new { ms = msCompilacion, de_cache = compilado.DeCache, lineas = compilado.Lineas, roslyn = CompiladorCodigo().VersionRoslyn },
                ms_espera = msEspera,
                ms_script = msScript,
                revisiones,
                tiempo_limite_s = CodigoLimites.TiempoLimiteSegundos,
                eventos_bd = cambios.FallosEventos.Count == 0 ? "vigilados" : "no disponibles: " + string.Join("; ", cambios.FallosEventos)
            };
            if (cambios.FallosEventos.Count > 0)
                avisos.Add("No se pudieron vigilar los eventos de la base de datos (" + string.Join("; ", cambios.FallosEventos) + "): la lista de cambios sale solo de la transacción.");

            if (ctx.Simular)
            {
                avisos.Insert(0, "Simulación: el script se ejecutó de verdad dentro de una transacción que se abortó al final. Con objetos de Civil 3D no es una garantía total (Civil 3D puede guardar datos fuera de la transacción); la protección real es la copia de seguridad (copia.ruta).");
                avisos.Add("La simulación puede dejar una entrada vacía (Executefunction) en el menú Deshacer: no deshace nada.");
                var quedaron = cambios.Creados.Where(id => id.IsValid && !id.IsErased).ToList();
                if (quedaron.Count > 0)
                    avisos.Add("ATENCIÓN: después de abortar siguen en el dibujo " + quedaron.Count + " objeto(s) que creó el script (" + string.Join(", ", quedaron.Take(20).Select(cambios.Describir))
                        + "): Civil 3D no los deshizo con la transacción. Quítalos con _.UNDO o vuelve a la copia (copia.ruta).");
                if (noEscritos.Count > 0)
                    avisos.Add("Simulación: no se escribió ningún archivo; se habrían escrito en " + CodigoCarpeta.Texto + ": " + string.Join(", ", noEscritos) + ".");
                string accion = "Ejecutar un script de " + compilado.Lineas + " línea(s) en simulación: se ejecutó dentro de una transacción que se abortó al final.";
                Historial.Registrar("ejecutar_codigo: simulación terminada (creados " + cambios.Creados.Count + ", modificados " + cambios.Modificados.Count + ", borrados " + cambios.Borrados.Count + ", " + msScript + " ms)");
                var sim = Escritura.Simulacion(ctx, antes, hechos, accion);
                return ConExtras(sim, new Dictionary<string, object> { ["copia"] = ctx.Copia, ["datos"] = datos, ["resultado"] = resultado });
            }

            // Escritura real: se vuelve a leer en una transacción nueva y se comprueba lo creado y lo borrado
            Dictionary<string, object> despues;
            using (doc.LockDocument()) despues = VerificarCambios(db, cambios);
            Historial.Registrar("ejecutar_codigo: aplicado (creados " + cambios.Creados.Count + ", modificados " + cambios.Modificados.Count + ", borrados " + cambios.Borrados.Count + ", " + msScript + " ms)");
            string mensaje = "Script de " + compilado.Lineas + " línea(s) ejecutado y confirmado: " + cambios.Creados.Count + " objeto(s) creado(s), "
                + cambios.Modificados.Count + " modificado(s) y " + cambios.Borrados.Count + " borrado(s).";
            var res = Escritura.Resultado(ctx, antes, despues, hechos, mensaje, datos);
            return ConExtras(res, new Dictionary<string, object> { ["resultado"] = resultado });
        }

        private static string SufijoNadaAplicado(Escritura.Contexto ctx)
        {
            string ruta = ctx.Copia?.Ruta;
            return " No se aplicó nada: la transacción se abortó" + (ctx.Simular ? " (era una simulación)" : "") + "." + (ruta != null ? " Copia de seguridad: " + ruta + "." : "");
        }

        /// <summary>Error del script con su línea y lo que pasó con el dibujo (sin InnerException: el servidor muestra este texto).</summary>
        private static Exception ErrorDelScript(Exception ex, string parada, Escritura.Contexto ctx)
        {
            int? linea = EjecucionScript.LineaDe(ex);
            string donde = linea.HasValue ? " en la línea " + linea.Value : "";
            string sufijo = SufijoNadaAplicado(ctx);
            if (ex is OperationCanceledException)
                return new TimeoutException("El script superó el tiempo límite de " + CodigoLimites.TiempoLimiteSegundos + " s y se detuvo" + donde
                    + " (en la siguiente revisión: las hay al principio de cada bucle, función y lambda y en cada ayuda de F)." + sufijo + " Si necesita más tiempo, divídelo en varias llamadas.");
            if (ex is InsufficientExecutionStackException)
                return new InvalidOperationException("Recursión demasiado profunda" + donde + ": el script se detuvo antes de agotar la pila de Civil 3D." + sufijo);
            if (parada != null)
                return new InvalidOperationException(parada + " El script se detuvo" + donde + "." + sufijo);
            return new InvalidOperationException("El script falló" + donde + ": " + ex.GetType().Name + ": " + ex.Message + sufijo);
        }

        /// <summary>
        /// Las dos transacciones de una ejecución: t0, la del plugin (el script no la ve), y t1, la del script (F.Tr),
        /// anidada en t0. Al terminar el script hay que saber si t1 sigue abierta y si el script dejó otras suyas
        /// abiertas encima. Se comprueba una vez, al abrir t1, si TopTransaction devuelve algo comparable con ella; si sí,
        /// se usa esa comparación (la de fiar), y si no, el número de transacciones activas (por reflexión). Si no se
        /// puede saber ninguna de las dos cosas, la escritura falla con un error y no se aplica nada.
        /// </summary>
        private sealed class PilaTransacciones
        {
            private readonly Database _db;
            private readonly Transaction _t0;
            private readonly int? _nivelT0;
            private Transaction _t1;
            private int? _nivelT1;
            private bool _identidadFiable;

            public PilaTransacciones(Database db, Transaction t0)
            {
                _db = db;
                _t0 = t0;
                _nivelT0 = Activas();
            }

            public void AnotarScript(Transaction t1)
            {
                _t1 = t1;
                _nivelT1 = Activas();
                _identidadFiable = MismaTransaccion(Arriba(), t1);
            }

            /// <summary>Aborta las que el script dejó abiertas, confirma F.Tr si sigue abierta y comprueba que queda la del plugin.</summary>
            public void CerrarLasDelScript(List<string> avisos)
            {
                int abiertas = AbortarEncimaDe(_t1, _nivelT1);
                if (abiertas > 0) avisos.Add("El script dejó " + abiertas + " transacción(es) suya(s) sin cerrar: se abortaron y sus cambios no cuentan.");
                if (EsLaDeArriba(_t1, _nivelT1)) _t1.Commit();
                try { _t1.Dispose(); } catch { }
                if (!EsLaDeArriba(_t0, _nivelT0))
                    throw new InvalidOperationException("La transacción de ejecutar_codigo ya no está activa después del script (o no se puede saber cuál es la activa): no se puede asegurar qué quedó en el dibujo. Revisa el dibujo y, si hace falta, usa _.UNDO o la copia de seguridad.");
            }

            /// <summary>Aborta todo lo que haya encima de la transacción del plugin (al terminar, también si hubo un error).</summary>
            public void AbortarEncimaDelPlugin() => AbortarEncimaDe(_t0, _nivelT0);

            private int AbortarEncimaDe(Transaction t, int? nivel)
            {
                int n = 0;
                for (int i = 0; i < 64; i++)
                {
                    if (EsLaDeArriba(t, nivel) || EsLaDeArriba(_t0, _nivelT0)) return n;
                    int? activas = Activas();
                    if (activas.HasValue && _nivelT0.HasValue && activas.Value < _nivelT0.Value) return n;   // ya no hay nada por encima del plugin
                    var arriba = Arriba();
                    if (arriba == null) return n;
                    try { arriba.Abort(); } catch { }
                    try { arriba.Dispose(); } catch { }
                    n++;
                }
                return n;
            }

            private bool EsLaDeArriba(Transaction t, int? nivel)
            {
                if (t == null) return false;
                if (_identidadFiable) return MismaTransaccion(Arriba(), t);
                int? activas = Activas();
                if (activas.HasValue && nivel.HasValue) return activas.Value == nivel.Value;
                return MismaTransaccion(Arriba(), t);
            }

            private Transaction Arriba()
            {
                try { return _db.TransactionManager.TopTransaction; } catch { return null; }
            }

            /// <summary>TransactionManager.NumberOfActiveTransactions por reflexión (por verificar en 2027); null si no se puede leer.</summary>
            private int? Activas()
            {
                try { return Api.Leer<int?>(_db.TransactionManager, null, "NumberOfActiveTransactions"); }
                catch { return null; }
            }
        }

        /// <summary>TopTransaction puede devolver otro envoltorio .NET de la misma transacción: se comparan también por UnmanagedObject.</summary>
        private static bool MismaTransaccion(Transaction a, Transaction b)
        {
            if (a == null || b == null) return false;
            if (ReferenceEquals(a, b)) return true;
            try { if (a.Equals(b)) return true; } catch { }
            var pa = Api.Leer<IntPtr>(a, IntPtr.Zero, "UnmanagedObject");
            return pa != IntPtr.Zero && pa == Api.Leer<IntPtr>(b, IntPtr.Zero, "UnmanagedObject");
        }

        /// <summary>Escritura real: relee lo creado y lo modificado en una transacción nueva y comprueba lo borrado.</summary>
        private static Dictionary<string, object> VerificarCambios(Database db, CambiosScript c)
        {
            int creados = 0, modificados = 0, borrados = 0;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in c.Creados) if (Existe(tr, id)) creados++;
                foreach (var id in c.Modificados) if (Existe(tr, id)) modificados++;
                foreach (var id in c.Borrados) if (id.IsErased) borrados++;
                tr.Commit();
            }
            return new Dictionary<string, object> { ["objetos_creados"] = creados, ["objetos_modificados"] = modificados, ["objetos_borrados"] = borrados };
        }

        private static bool Existe(Transaction tr, ObjectId id)
        {
            try { return id.IsValid && !id.IsErased && tr.GetObject(id, OpenMode.ForRead) != null; }
            catch { return false; }
        }

        // ------------------------------------------------------------------ 3. qué cambió
        private static void EngancharEventos(Database db, CambiosScript c)
        {
            c.AlAnadir = (s, e) =>
            {
                try { var o = e.DBObject; if (o != null) c.Anotar(o.ObjectId, o.GetType().Name, nuevo: true); } catch { }
            };
            c.AlModificar = (s, e) =>
            {
                try { var o = e.DBObject; if (o != null) c.Anotar(o.ObjectId, o.GetType().Name, modificado: true); } catch { }
            };
            c.AlBorrar = (s, e) =>
            {
                try
                {
                    var o = e.DBObject;
                    if (o == null) return;
                    c.Anotar(o.ObjectId, o.GetType().Name, borrado: e.Erased, recuperado: !e.Erased);
                    string protegido = e.Erased ? CambiosScript.Protegido(o) : null;
                    if (protegido != null) CodigoVigilante.Detener(protegido);
                }
                catch { }
            };
            try { db.ObjectAppended += c.AlAnadir; c.EnAnadir = true; } catch (Exception ex) { c.FallosEventos.Add("ObjectAppended: " + ex.Message); }
            try { db.ObjectModified += c.AlModificar; c.EnModificar = true; } catch (Exception ex) { c.FallosEventos.Add("ObjectModified: " + ex.Message); }
            try { db.ObjectErased += c.AlBorrar; c.EnBorrar = true; } catch (Exception ex) { c.FallosEventos.Add("ObjectErased: " + ex.Message); }
        }

        private static void DesengancharEventos(Database db, CambiosScript c)
        {
            if (c.EnAnadir) { try { db.ObjectAppended -= c.AlAnadir; } catch { } c.EnAnadir = false; }
            if (c.EnModificar) { try { db.ObjectModified -= c.AlModificar; } catch { } c.EnModificar = false; }
            if (c.EnBorrar) { try { db.ObjectErased -= c.AlBorrar; } catch { } c.EnBorrar = false; }
        }

        /// <summary>Objetos que el script creó, modificó o borró, con su tipo, nombre y capa para la respuesta.</summary>
        private sealed class CambiosScript
        {
            private sealed class Estado
            {
                public string Tipo;
                public bool Nuevo, Borrado, Modificado;
                public string Nombre, Capa, Handle;
            }

            private readonly Dictionary<ObjectId, Estado> _estados = new Dictionary<ObjectId, Estado>();
            public ObjectEventHandler AlAnadir, AlModificar;
            public ObjectErasedEventHandler AlBorrar;
            public bool EnAnadir, EnModificar, EnBorrar;
            public readonly List<string> FallosEventos = new List<string>();
            public List<ObjectId> Creados = new List<ObjectId>(), Modificados = new List<ObjectId>(), Borrados = new List<ObjectId>();
            public int Temporales;
            public bool Recortado;
            public string Violacion;

            public void Anotar(ObjectId id, string tipo, bool nuevo = false, bool modificado = false, bool borrado = false, bool recuperado = false)
            {
                if (id.IsNull) return;
                if (!_estados.TryGetValue(id, out var e)) _estados[id] = e = new Estado { Tipo = tipo };
                e.Nuevo |= nuevo;
                e.Modificado |= modificado;
                if (borrado) e.Borrado = true;
                if (recuperado) e.Borrado = false;
            }

            /// <summary>Camino principal: los objetos abiertos en la transacción del plugin (con los de F.Tr y las anidadas, ya confirmadas en ella).</summary>
            public void AgregarDeTransaccion(Transaction t0)
            {
                DBObjectCollection objetos;
                try { objetos = t0.GetAllObjects(); }
                catch (Exception ex) { FallosEventos.Add("Transaction.GetAllObjects: " + ex.Message); return; }
                foreach (DBObject o in objetos)
                {
                    try
                    {
                        var id = o.ObjectId;
                        if (id.IsNull) continue;
                        if (!_estados.TryGetValue(id, out var e)) _estados[id] = e = new Estado { Tipo = o.GetType().Name };
                        e.Nuevo |= o.IsNewObject;
                        e.Borrado = o.IsErased;
                        e.Modificado |= o.IsModified;
                    }
                    catch { }
                }
            }

            /// <summary>
            /// Clasifica y, con la transacción aún abierta, lee tipo, nombre y capa de los primeros objetos de cada lista
            /// (los que salen en la respuesta) y comprueba la regla del proyecto en todos los borrados.
            /// </summary>
            public void Detallar(Transaction t0)
            {
                foreach (var kv in _estados)
                {
                    var e = kv.Value;
                    if (e.Nuevo && e.Borrado) { Temporales++; continue; }
                    var lista = e.Nuevo ? Creados : e.Borrado ? Borrados : e.Modificado ? Modificados : null;
                    if (lista == null) continue;
                    lista.Add(kv.Key);
                    bool detallar = lista.Count <= CodigoLimites.MaximoObjetosListados;
                    if (!detallar && !e.Borrado) continue;
                    try
                    {
                        e.Handle = kv.Key.Handle.ToString();
                        var o = t0.GetObject(kv.Key, OpenMode.ForRead, true);
                        e.Tipo = o.GetType().Name;
                        if (e.Borrado && Violacion == null) Violacion = Protegido(o);
                        if (!detallar) continue;
                        e.Nombre = o is SymbolTableRecord str ? str.Name : Api.Leer<string>(o, null, "Name");
                        e.Capa = (o as Entity)?.Layer;
                    }
                    catch { }
                }
            }

            /// <summary>Texto de la regla del proyecto si el objeto es una superficie, un corredor o un ensamblaje; null si no.</summary>
            public static string Protegido(DBObject o)
            {
                string que = o is CivSurface ? "la superficie" : o is Civ.Corridor ? "el corredor" : o is Civ.Assembly ? "el ensamblaje" : null;
                if (que == null) return null;
                string nombre = null;
                try { nombre = Api.Leer<string>(o, null, "Name"); } catch { }
                return "Regla del proyecto: el script borró " + que + (nombre != null ? " '" + nombre + "'" : "") + " (" + o.GetType().Name + "); "
                    + CodigoReglas.Explicacion(CodigoReglas.Borrado);
            }

            public List<object> Lista(List<ObjectId> ids)
            {
                if (ids.Count > CodigoLimites.MaximoObjetosListados) Recortado = true;
                return ids.Take(CodigoLimites.MaximoObjetosListados).Select(id =>
                {
                    _estados.TryGetValue(id, out var e);
                    return (object)new { handle = e?.Handle ?? Handle(id), tipo = e?.Tipo, nombre = e?.Nombre, capa = e?.Capa };
                }).ToList();
            }

            public string Describir(ObjectId id)
            {
                _estados.TryGetValue(id, out var e);
                return (e?.Tipo ?? "objeto") + " " + (e?.Handle ?? Handle(id)) + (e?.Nombre != null ? " '" + e.Nombre + "'" : "");
            }

            private static string Handle(ObjectId id)
            {
                try { return id.Handle.ToString(); } catch { return null; }
            }
        }

        // ------------------------------------------------------------------ conversión del resultado (ObjectId, puntos, objetos)
        private static bool ConvertirResultado(object v, out object c)
        {
            switch (v)
            {
                case ObjectId id: c = id.IsNull ? null : id.Handle.ToString(); return true;
                case Handle h: c = h.ToString(); return true;
                case Point3d p: c = new Dictionary<string, object> { ["x"] = N(p.X), ["y"] = N(p.Y), ["z"] = N(p.Z) }; return true;
                case Point2d p2: c = new Dictionary<string, object> { ["x"] = N(p2.X), ["y"] = N(p2.Y) }; return true;
                case Vector3d vec: c = new Dictionary<string, object> { ["x"] = N(vec.X), ["y"] = N(vec.Y), ["z"] = N(vec.Z) }; return true;
                case Extents3d ext:
                    c = new Dictionary<string, object>
                    {
                        ["min"] = new Dictionary<string, object> { ["x"] = N(ext.MinPoint.X), ["y"] = N(ext.MinPoint.Y), ["z"] = N(ext.MinPoint.Z) },
                        ["max"] = new Dictionary<string, object> { ["x"] = N(ext.MaxPoint.X), ["y"] = N(ext.MaxPoint.Y), ["z"] = N(ext.MaxPoint.Z) }
                    };
                    return true;
                case DBObject o:
                    var d = new Dictionary<string, object> { ["tipo"] = o.GetType().Name };
                    try { d["handle"] = o.ObjectId.IsNull ? null : o.ObjectId.Handle.ToString(); } catch { }
                    try { d["nombre"] = o is SymbolTableRecord str ? str.Name : Api.Leer<string>(o, null, "Name"); } catch { }
                    try { if (o is Entity ent) d["capa"] = ent.Layer; } catch { }
                    c = d;
                    return true;
            }
            if (v != null && (v.GetType().Namespace ?? "").StartsWith("Autodesk", StringComparison.Ordinal))
            {
                c = v.ToString();
                return true;
            }
            c = null;
            return false;
        }
    }
}
