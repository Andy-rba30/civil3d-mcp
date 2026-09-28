using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.ApplicationServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace ArbaMcp
{
    /// <summary>
    /// Servidor HTTP mínimo (sin http.sys, sin permisos de administrador) que escucha solo en 127.0.0.1.
    /// Contrato:
    ///   GET  /ping                  → {"ok":true,"servidor":"ArbaMcp","version":"1.3.0"} (sin token; nada del dibujo)
    ///   GET  /tools                 → lista de herramientas con parámetros
    ///   POST /execute {tool, args}  → {"ok":true,"result":...} o {"ok":false,"error":"..."}
    /// Puerto: variable de entorno ARBA_MCP_PORT (por defecto 8765). ARBA_MCP=0 desactiva el servidor.
    /// Este código corre en hilos del ThreadPool: nunca llama a la API de AutoCAD; cada herramienta se
    /// encola en HiloPrincipal, que la ejecuta en el hilo principal en el contexto que la herramienta declara.
    /// El análisis de la petición, la decisión de autorización y el formato de las respuestas están en
    /// ArbaMcp.Nucleo (Http, Autorizacion) y se prueban sin Civil 3D; aquí solo se mueven bytes por el socket.
    /// </summary>
    internal static class Servidor
    {
        public const int PuertoPorDefecto = 8765;
        public static int Puerto { get; private set; }
        public static bool Activo => _oyente != null;
        public static string UltimoError { get; private set; } = "";
        public static string TokenActual { get; private set; }

        /// <summary>Versión del plugin ("1.3.0"), la que devuelve GET /ping.</summary>
        public static string Version => typeof(Servidor).Assembly.GetName().Version?.ToString(3) ?? "?";

        private static TcpListener _oyente;
        private static CancellationTokenSource _cts;
        private static readonly Registro401 Rechazos401 = new Registro401();

        /// <summary>Opciones de serialización compartidas (tildes sin escapar); las define el núcleo.</summary>
        internal static readonly JsonSerializerOptions Json = Nucleo.Json.Opciones;

        public static void Iniciar()
        {
            if (Activo) return;
            if (Environment.GetEnvironmentVariable("ARBA_MCP") == "0") return;

            // 32 bytes del generador criptográfico, en hexadecimal (64 caracteres); un Guid no está pensado como secreto
            TokenActual = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            try {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArbaMcp");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "token");
                File.WriteAllText(file, TokenActual);
                var fi = new FileInfo(file);
                var sec = fi.GetAccessControl();
                sec.SetAccessRuleProtection(true, false);
                var id = System.Security.Principal.WindowsIdentity.GetCurrent().User;
                sec.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(id, System.Security.AccessControl.FileSystemRights.FullControl, System.Security.AccessControl.AccessControlType.Allow));
                fi.SetAccessControl(sec);
            } catch { }


            Puerto = PuertoPorDefecto;
            if (int.TryParse(Environment.GetEnvironmentVariable("ARBA_MCP_PORT"), out int p) && p > 0 && p < 65536) Puerto = p;

            try
            {
                _cts = new CancellationTokenSource();
                _oyente = new TcpListener(IPAddress.Loopback, Puerto);
                _oyente.Start();
                HiloPrincipal.Iniciar();
                Historial.Iniciar();
                Task.Run(() => Bucle(_cts.Token));
                Historial.Registrar("Servidor MCP escuchando en http://127.0.0.1:" + Puerto + "/");
            }
            catch (System.Exception ex)
            {
                UltimoError = ex.Message;
                _oyente = null;
                Historial.Registrar("No se pudo iniciar el servidor MCP: " + ex.Message);
            }
        }

        public static void Detener()
        {
            try { _cts?.Cancel(); _oyente?.Stop(); } catch { }
            _oyente = null;
            HiloPrincipal.Detener();
        }

        private static async Task Bucle(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                TcpClient cliente;
                try { cliente = await _oyente.AcceptTcpClientAsync(); }
                catch { break; }
                _ = Task.Run(() => Atender(cliente));
            }
        }

        private static async Task Atender(TcpClient cliente)
        {
            using (cliente)
            {
                try
                {
                    cliente.ReceiveTimeout = 10000;
                    var ns = cliente.GetStream();

                    // 1. Cabeceras
                    var acumulado = new MemoryStream();
                    var tmp = new byte[8192];
                    int finCab = -1;
                    while (finCab < 0)
                    {
                        int n = await ns.ReadAsync(tmp, 0, tmp.Length);
                        if (n <= 0) return;
                        acumulado.Write(tmp, 0, n);
                        finCab = Http.BuscarFinCabeceras(acumulado.GetBuffer(), (int)acumulado.Length);
                        if (acumulado.Length > 1 << 20) { await Responder(ns, 413, Http.Error("Petición demasiado grande")); return; }
                    }

                    string cabeceras = Encoding.ASCII.GetString(acumulado.GetBuffer(), 0, finCab);
                    if (!Http.IntentarAnalizarCabeceras(cabeceras, out var peticion, out string motivo)) { await Responder(ns, 400, Http.Error(motivo)); return; }

                    // 2. Cuerpo
                    int inicioCuerpo = finCab + 4;
                    var cuerpo = new MemoryStream();
                    cuerpo.Write(acumulado.GetBuffer(), inicioCuerpo, (int)acumulado.Length - inicioCuerpo);
                    while (cuerpo.Length < peticion.LargoCuerpo)
                    {
                        int n = await ns.ReadAsync(tmp, 0, tmp.Length);
                        if (n <= 0) break;
                        cuerpo.Write(tmp, 0, n);
                    }
                    peticion.Cuerpo = Encoding.UTF8.GetString(cuerpo.GetBuffer(), 0, (int)Math.Min(cuerpo.Length, peticion.LargoCuerpo));

                    // 3. Autorización, DESPUÉS de leer el cuerpo: si se cierra la conexión con bytes sin leer, Windows
                    // envía un reset y el cliente ve un error en vez del 401/403/415.
                    int rechazo = Autorizacion.Decidir(peticion, Puerto, TokenActual);
                    foreach (var linea in Rechazos401.Cerrar(DateTime.Now)) Historial.Registrar(linea);
                    if (rechazo != Autorizacion.Autorizada)
                    {
                        // Los 401 se anotan una vez por ruta y minuto con el recuento, no uno por petición
                        if (rechazo == 401) foreach (var linea in Rechazos401.Anotar(peticion.Metodo, peticion.Ruta, DateTime.Now)) Historial.Registrar(linea);
                        await Responder(ns, rechazo, Http.Error(Http.TextoEstado(rechazo)));
                        return;
                    }

                    // 4. Enrutado
                    string metodo = peticion.Metodo, ruta = peticion.Ruta;
                    if (metodo == "OPTIONS") { await Responder(ns, 204, ""); return; }
                    if (metodo == "GET" && (ruta == "/tools" || ruta == "/tools/"))
                    {
                        await Responder(ns, 200, Catalogo.SerializarTools(Herramientas.Descripciones()));
                        return;
                    }
                    if (Autorizacion.EsPingPublico(metodo, ruta))
                    {
                        // Sin token y sin datos del dibujo: es lo que sondea el puente mientras Civil 3D arranca
                        await Responder(ns, 200, Nucleo.Json.Serializar(new { ok = true, servidor = "ArbaMcp", version = Version }));
                        return;
                    }
                    if (metodo == "POST" && (ruta == "/execute" || ruta == "/execute/"))
                    {
                        await Responder(ns, 200, await Ejecutar(peticion.Cuerpo));
                        return;
                    }
                    await Responder(ns, 404, Http.Error("Ruta no encontrada: " + metodo + " " + ruta));
                }
                catch (System.Exception ex)
                {
                    try { await Responder(cliente.GetStream(), 500, Http.Error(ex.Message)); } catch { }
                }
            }
        }

        /// <summary>Ejecuta {tool, args, timeout_s} en el hilo principal y devuelve el JSON de respuesta.</summary>
        private static async Task<string> Ejecutar(string cuerpo)
        {
            string nombre = "";
            JsonElement args = default;
            int timeoutS = 120;
            try
            {
                using (var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(cuerpo) ? "{}" : cuerpo))
                {
                    var raiz = doc.RootElement;
                    if (raiz.TryGetProperty("tool", out var t)) nombre = t.GetString() ?? "";
                    if (raiz.TryGetProperty("args", out var a)) args = a.Clone();
                    if (raiz.TryGetProperty("timeout_s", out var to) && to.TryGetInt32(out int ts) && ts > 0) timeoutS = ts;
                }
            }
            catch (System.Exception ex) { return Http.Error("JSON inválido: " + ex.Message); }

            var herramienta = Herramientas.Buscar(nombre);
            if (herramienta == null) return Http.Error("Herramienta desconocida: '" + nombre + "'. Consulta GET /tools.");

            Historial.Registrar("MCP → " + nombre);
            var reloj = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Task<object> tareaPrincipal;
                Pendiente pendiente = null;
                if (herramienta.EjecutarAsync != null)
                {
                    tareaPrincipal = herramienta.EjecutarAsync(args);
                }
                else
                {
                    // Una simulación (simular=true) solo lee: va en contexto de aplicación con bloqueo de lectura, como las
                    // lecturas, y así no deja entrada en el menú Deshacer. La escritura real sigue en contexto de comando.
                    var contexto = herramienta.Contexto;
                    if (contexto == ContextoEjecucion.Documento && Argumentos.LeerSimular(args)) contexto = ContextoEjecucion.Aplicacion;
                    pendiente = HiloPrincipal.Encolar(() => herramienta.Ejecutar(args), contexto, nombre);
                    tareaPrincipal = pendiente.Tarea;
                }

                var terminada = await Task.WhenAny(tareaPrincipal, Task.Delay(TimeSpan.FromSeconds(timeoutS + 5)));
                if (terminada != tareaPrincipal)
                {
                    string detalle = Pendiente.DescribirTiempoAgotado(pendiente);
                    Historial.Registrar("MCP ← " + nombre + " TIEMPO AGOTADO (" + timeoutS + " s): " + detalle);
                    return Http.Error("Tiempo agotado (" + timeoutS + " s): " + detalle + " Civil 3D puede estar ocupado o con un cuadro de diálogo abierto.");
                }
                object resultado = await tareaPrincipal;
                Historial.Registrar("MCP ✓ " + nombre + " OK (" + reloj.ElapsedMilliseconds + " ms)");
                // ms: total visto por el servidor; ms_espera: en cola hasta que Civil 3D quedó libre; ms_ejecucion: la herramienta
                // en el hilo principal (null en las herramientas asíncronas, que gestionan su propia espera).
                return JsonSerializer.Serialize(new { ok = true, tool = nombre, ms = reloj.ElapsedMilliseconds, ms_espera = pendiente?.MsEspera, ms_ejecucion = pendiente?.MsEjecucion, result = resultado }, Json);
            }
            catch (OperationCanceledException)
            {
                Historial.Registrar("MCP ← " + nombre + " CANCELADA: no llegó a ejecutarse");
                return Http.Error("La herramienta no llegó a ejecutarse: Civil 3D descartó la petición (¿se cerró el dibujo o se descargó el plugin?). Consulta leer_historial.");
            }
            catch (System.Exception ex)
            {
                var raiz = ex; while (raiz.InnerException != null) raiz = raiz.InnerException;
                Historial.Registrar("MCP ← " + nombre + " ERROR: " + raiz.Message + " " + raiz.StackTrace);
                return Http.Error(raiz.GetType().Name + ": " + raiz.Message);
            }
        }

        private static async Task Responder(NetworkStream ns, int codigo, string json)
        {
            var bytes = Http.ConstruirRespuesta(codigo, json);
            await ns.WriteAsync(bytes, 0, bytes.Length);
            await ns.FlushAsync();
        }
    }

    /// <summary>
    /// Registro de comandos ejecutados y mensajes del plugin, consultable por MCP. Además de las 1000 líneas
    /// en memoria, cada línea se añade a %LOCALAPPDATA%\ArbaMcp\historial.log (rota a 5 MB) para que
    /// sobreviva a un cierre de Civil 3D.
    /// </summary>
    internal static class Historial
    {
        public const long TamanoMaximoArchivo = 5L * 1024 * 1024;

        private static readonly List<string> Lineas = new List<string>();
        private static readonly object Cerrojo = new object();
        private static readonly UTF8Encoding Utf8SinBom = new UTF8Encoding(false);
        private static bool _iniciado;
        private static string _rutaArchivo;
        private static bool _archivoFallido;

        public static string RutaArchivo
        {
            get
            {
                if (_rutaArchivo == null)
                    _rutaArchivo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArbaMcp", "historial.log");
                return _rutaArchivo;
            }
        }

        public static void Registrar(string texto)
        {
            string hora = DateTime.Now.ToString("HH:mm:ss");
            lock (Cerrojo)
            {
                Lineas.Add(hora + "  " + texto);
                if (Lineas.Count > 1000) Lineas.RemoveRange(0, Lineas.Count - 1000);
                EscribirArchivo(DateTime.Now.ToString("yyyy-MM-dd ") + hora + "  " + texto);
            }
        }

        // Se llama con el cerrojo tomado. Si el archivo no se puede escribir, se deja de intentar (no molesta al usuario).
        private static void EscribirArchivo(string linea)
        {
            if (_archivoFallido) return;
            try
            {
                string ruta = RutaArchivo;
                Directory.CreateDirectory(Path.GetDirectoryName(ruta));
                var fi = new FileInfo(ruta);
                if (fi.Exists && fi.Length > TamanoMaximoArchivo)
                {
                    string anterior = Path.Combine(Path.GetDirectoryName(ruta), "historial.1.log");
                    if (File.Exists(anterior)) File.Delete(anterior);
                    File.Move(ruta, anterior);
                }
                File.AppendAllText(ruta, linea + Environment.NewLine, Utf8SinBom);
            }
            catch
            {
                _archivoFallido = true;
                Lineas.Add(DateTime.Now.ToString("HH:mm:ss") + "  No se puede escribir historial.log; se sigue solo en memoria");
            }
        }

        public static List<string> Ultimas(int n)
        {
            lock (Cerrojo) return Lineas.Skip(Math.Max(0, Lineas.Count - n)).ToList();
        }

        public static void Iniciar()
        {
            if (_iniciado) return;
            _iniciado = true;
            try
            {
                foreach (Document d in AcApp.DocumentManager) Enganchar(d);
                AcApp.DocumentManager.DocumentCreated += (s, e) => Enganchar(e.Document);
                AcApp.DocumentManager.DocumentToBeDestroyed += (s, e) => Desenganchar(e.Document);
            }
            catch (System.Exception ex) { Registrar("No se pudieron enganchar los eventos de documento: " + ex.Message); }
        }

        // Manejadores con nombre para poder desengancharlos cuando el dibujo se cierra
        private static void AlIniciarComando(object s, CommandEventArgs e) => Registrar("Comando inicia: " + e.GlobalCommandName);
        private static void AlTerminarComando(object s, CommandEventArgs e) => Registrar("Comando termina: " + e.GlobalCommandName);
        private static void AlCancelarComando(object s, CommandEventArgs e) => Registrar("Comando cancelado: " + e.GlobalCommandName);
        private static void AlFallarComando(object s, CommandEventArgs e) => Registrar("Comando falló: " + e.GlobalCommandName);

        private static void Enganchar(Document d)
        {
            if (d == null) return;
            d.CommandWillStart += AlIniciarComando;
            d.CommandEnded += AlTerminarComando;
            d.CommandCancelled += AlCancelarComando;
            d.CommandFailed += AlFallarComando;
        }

        private static void Desenganchar(Document d)
        {
            if (d == null) return;
            d.CommandWillStart -= AlIniciarComando;
            d.CommandEnded -= AlTerminarComando;
            d.CommandCancelled -= AlCancelarComando;
            d.CommandFailed -= AlFallarComando;
        }
    }
}

