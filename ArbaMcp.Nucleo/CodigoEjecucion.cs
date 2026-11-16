using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;

namespace ArbaMcp.Nucleo
{
    // Todo lo de este archivo es independiente de Roslyn: el plugin lo usa desde el contexto de carga general de Civil 3D
    // sin que se cargue el compilador (que vive en su propio contexto, ver CargadorCompilador).

    /// <summary>Límites de ejecutar_codigo (desde 1.16.0).</summary>
    public static class CodigoLimites
    {
        /// <summary>Tiempo límite cooperativo del script. Con la espera en cola y la compilación cabe en los 120 s de escritura del puente.</summary>
        public const int TiempoLimiteSegundos = 60;
        /// <summary>Lo máximo que el script espera en cola a que Civil 3D quede libre antes de descartarse sin ejecutarse.</summary>
        public const int EsperaMaximaSegundos = 30;
        public const int MaximoObjetosListados = 200;
        public const int MaximoMensajes = 200;
        public const int MaximoBytesArchivo = 10 * 1024 * 1024;
    }

    /// <summary>Activación de ejecutar_codigo por la variable de entorno ARBA_MCP_CODIGO (desde 1.16.0; desactivada por defecto).</summary>
    public static class CodigoActivacion
    {
        public const string Variable = "ARBA_MCP_CODIGO";

        public const string MensajeDesactivada = "ejecutar_codigo está desactivada: se activa con la variable de entorno de usuario ARBA_MCP_CODIGO=1 y reiniciando Civil 3D, y eso lo decide el dueño del equipo. No se compiló ni se ejecutó nada: sin copia de seguridad y sin entrada en Deshacer. Usa las herramientas específicas o dile al usuario que la herramienta está desactivada.";

        /// <summary>Solo el valor exacto "1" (sin espacios alrededor) la activa; cualquier otro valor, o ninguno, la deja desactivada.</summary>
        public static bool Activada(string valor) => string.Equals(valor?.Trim(), "1", StringComparison.Ordinal);
    }

    /// <summary>
    /// Tiempo límite cooperativo de los scripts (desde 1.16.0). Un script corre en el hilo principal de AutoCAD y no se
    /// puede detener a la fuerza: .NET 10 no tiene Thread.Abort, y aunque lo tuviera, cortar ese hilo en mitad de una
    /// llamada a la API dejaría el dibujo a medio escribir (o cerraría Civil 3D). Por eso el script se detiene solo:
    /// Revisar() comprueba un CancellationToken que se cancela al cumplirse el tiempo y lanza OperationCanceledException.
    /// La llaman todas las ayudas de la Fachada y, además, el compilador la inserta al principio del script y de cada
    /// bucle, función local y lambda, así que un bucle infinito se corta aunque no llame a la Fachada. También comprueba
    /// la pila (una recursión infinita daría un error normal en vez de cerrar Civil 3D) y la orden de parada que da el
    /// plugin si el script incumple una regla (borrar una superficie...). Fuera de una ejecución no hace nada.
    /// Lo que no puede cortar: una sola llamada larga a la API (un Rebuild enorme) hasta que vuelve.
    /// </summary>
    public static class CodigoVigilante
    {
        private static CancellationTokenSource _cts;
        private static CancellationToken _token;
        private static volatile bool _activo;
        private static volatile string _motivoParada;
        private static long _revisiones;
        private static int _limiteSegundos;

        public static bool Activo => _activo;
        public static long Revisiones => Interlocked.Read(ref _revisiones);
        public static string MotivoParada => _motivoParada;
        public static CancellationToken Token => _token;

        public static string MensajeTiempo(int segundos)
            => "El script superó el tiempo límite de " + segundos + " s y se detuvo en la siguiente revisión (las hay al principio de cada bucle, función y lambda y en cada ayuda de la Fachada).";

        /// <summary>Punto de revisión del script: tiempo límite, orden de parada y pila. Fuera de una ejecución no hace nada.</summary>
        public static void Revisar()
        {
            if (!_activo) return;
            Interlocked.Increment(ref _revisiones);
            string motivo = _motivoParada;
            if (motivo != null) throw new InvalidOperationException(motivo);
            if (_token.IsCancellationRequested) throw new OperationCanceledException(MensajeTiempo(_limiteSegundos), _token);
            RuntimeHelpers.EnsureSufficientExecutionStack();
        }

        /// <summary>Empieza la vigilancia de un script (una a la vez). Se termina con Dispose de la sesión devuelta.</summary>
        public static Sesion Iniciar(TimeSpan limite)
        {
            if (_activo) throw new InvalidOperationException("Ya hay un script en ejecución; ejecutar_codigo corre de uno en uno.");
            _cts = new CancellationTokenSource(limite);
            _token = _cts.Token;
            _limiteSegundos = (int)Math.Round(limite.TotalSeconds);
            _motivoParada = null;
            Interlocked.Exchange(ref _revisiones, 0);
            _activo = true;
            return new Sesion();
        }

        /// <summary>Pide que el script se detenga en la siguiente revisión (lo usa el plugin desde un evento de la base de datos).</summary>
        public static void Detener(string motivo)
        {
            if (_activo && _motivoParada == null) _motivoParada = motivo;
        }

        public sealed class Sesion : IDisposable
        {
            private bool _cerrada;

            public void Dispose()
            {
                if (_cerrada) return;
                _cerrada = true;
                _activo = false;
                try { _cts?.Dispose(); } catch { }
                _cts = null;
                _token = default;
            }
        }
    }

    /// <summary>
    /// La carpeta donde los scripts pueden leer y escribir archivos (F.LeerTexto, F.EscribirTexto, F.ListarArchivos):
    /// %LOCALAPPDATA%\ArbaMcp\scripts. Solo nombres relativos dentro de ella, sin '..', sin unidad ni nombres de
    /// dispositivo de Windows (CON, NUL, COM1...).
    /// </summary>
    public static class CodigoCarpeta
    {
        private static readonly HashSet<string> Reservados = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public const string Texto = "%LOCALAPPDATA%\\ArbaMcp\\scripts";

        public static string Predeterminada()
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArbaMcp", "scripts");

        /// <summary>Ruta completa de 'nombre' dentro de 'carpeta'; lanza ArgumentException con el motivo si sale de ella.</summary>
        public static string Resolver(string carpeta, string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                throw new ArgumentException("Falta el nombre del archivo, relativo a la carpeta permitida " + Texto + " (por ejemplo 'datos.csv').");
            string n = nombre.Trim();
            if (Path.IsPathRooted(n) || n.Contains(':') || n.StartsWith("/") || n.StartsWith("\\"))
                throw new ArgumentException("'" + nombre + "': usa un nombre relativo a la carpeta permitida " + Texto + ", sin unidad ni ruta absoluta ('datos.csv' o 'sub/datos.csv').");
            var partes = n.Split('/', '\\');
            foreach (var p in partes)
            {
                if (p.Length == 0 || p.Trim().Length == 0 || p == "." || p == ".." || p.EndsWith(".") || p.EndsWith(" "))
                    throw new ArgumentException("'" + nombre + "': no se admiten '..', '.' ni partes vacías en el nombre; el archivo debe quedar dentro de " + Texto + ".");
                if (p.Any(c => c < 32 || "<>:\"|?*".IndexOf(c) >= 0))
                    throw new ArgumentException("'" + nombre + "': el nombre tiene caracteres no válidos (< > : \" | ? * o de control).");
                string raiz = p.Split('.')[0];
                if (Reservados.Contains(raiz))
                    throw new ArgumentException("'" + nombre + "': '" + raiz + "' es un nombre de dispositivo de Windows y no se admite.");
            }
            string baseCompleta = Path.GetFullPath(carpeta).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string ruta = Path.GetFullPath(Path.Combine(new[] { baseCompleta }.Concat(partes).ToArray()));
            if (!ruta.StartsWith(baseCompleta, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("'" + nombre + "': queda fuera de la carpeta permitida " + Texto + ".");
            return ruta;
        }

        /// <summary>Archivos de la carpeta (y sus subcarpetas) como nombres relativos con '/', ordenados; vacío si no existe.</summary>
        public static string[] Listar(string carpeta)
        {
            if (!Directory.Exists(carpeta)) return new string[0];
            return Directory.GetFiles(carpeta, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(carpeta, f).Replace('\\', '/'))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>Lo que devuelve la compilación aislada (CodigoPuerta.Compilar), ya con nombres.</summary>
    public sealed class ResultadoAislado
    {
        public bool Ok;
        public byte[] Ensamblado;
        public byte[] Pdb;
        public string Mensaje;
        public string[] Avisos = new string[0];
        public long Ms;
        public bool DeCache;
        public int Lineas;
        public string Tipo;
        public string Metodo;
        public string NombreEnsamblado;
        public string[] ReferenciasFallidas = new string[0];

        public static ResultadoAislado DesdeArreglo(object[] a)
        {
            if (a == null || a.Length < 12) throw new InvalidOperationException("El compilador devolvió una respuesta con otro formato (¿versiones distintas de ArbaMcp.Nucleo?).");
            return new ResultadoAislado
            {
                Ok = (bool)a[0],
                Ensamblado = a[1] as byte[],
                Pdb = a[2] as byte[],
                Mensaje = a[3] as string,
                Avisos = a[4] as string[] ?? new string[0],
                Ms = Convert.ToInt64(a[5]),
                DeCache = (bool)a[6],
                Lineas = Convert.ToInt32(a[7]),
                Tipo = a[8] as string,
                Metodo = a[9] as string,
                NombreEnsamblado = a[10] as string,
                ReferenciasFallidas = a[11] as string[] ?? new string[0]
            };
        }
    }

    /// <summary>
    /// Carga Roslyn en un AssemblyLoadContext propio (no el general de Civil 3D) con una segunda copia de
    /// ArbaMcp.Nucleo, y compila a través de CodigoPuerta pasando solo tipos básicos. Así, si AutoCAD, Dynamo u otro
    /// plugin traen otra versión de Microsoft.CodeAnalysis, ninguna de las dos estorba a la otra. Las DLL de Roslyn
    /// están en 'carpetaRoslyn' (Contents\roslyn del bundle, con la subcarpeta 'es' de los mensajes en español), nunca
    /// junto a ArbaMcp.dll, para que el contexto general no las encuentre por su cuenta.
    /// </summary>
    public sealed class CargadorCompilador
    {
        private readonly string _carpetaRoslyn;
        private readonly string _rutaNucleo;
        private readonly object _cerrojo = new object();
        private MethodInfo _compilar;

        public string VersionRoslyn { get; private set; }

        public CargadorCompilador(string carpetaRoslyn, string rutaNucleo)
        {
            _carpetaRoslyn = carpetaRoslyn;
            _rutaNucleo = rutaNucleo;
        }

        public ResultadoAislado Compilar(string codigo, string[] referencias, string tipoFachada)
        {
            MethodInfo m;
            lock (_cerrojo)
            {
                if (_compilar == null) Preparar();
                m = _compilar;
            }
            var arreglo = (object[])m.Invoke(null, BindingFlags.DoNotWrapExceptions, null, new object[] { codigo, referencias, tipoFachada }, null);
            return ResultadoAislado.DesdeArreglo(arreglo);
        }

        private void Preparar()
        {
            if (!File.Exists(Path.Combine(_carpetaRoslyn, "Microsoft.CodeAnalysis.dll")) || !File.Exists(Path.Combine(_carpetaRoslyn, "Microsoft.CodeAnalysis.CSharp.dll")))
                throw new FileNotFoundException("No se encontró el compilador (Roslyn) en " + _carpetaRoslyn + ". Reinstala el plugin con instalar.ps1: debe dejar Microsoft.CodeAnalysis.dll y Microsoft.CodeAnalysis.CSharp.dll en Contents\\roslyn.");
            if (!File.Exists(_rutaNucleo)) throw new FileNotFoundException("No se encontró " + _rutaNucleo + ".");
            var contexto = new ContextoCompilador(_carpetaRoslyn, _rutaNucleo);
            var nucleo = contexto.LoadFromAssemblyPath(_rutaNucleo);
            var puerta = nucleo.GetType("ArbaMcp.Nucleo.CodigoPuerta", throwOnError: true);
            _compilar = puerta.GetMethod("Compilar", BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException("ArbaMcp.Nucleo.CodigoPuerta.Compilar");
            VersionRoslyn = puerta.GetMethod("VersionRoslyn", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null) as string;
        }

        /// <summary>Contexto del compilador: Roslyn y la copia de ArbaMcp.Nucleo; el resto (System.*) viene del contexto general.</summary>
        private sealed class ContextoCompilador : AssemblyLoadContext
        {
            private readonly string _carpeta;
            private readonly string _nucleo;

            public ContextoCompilador(string carpeta, string nucleo) : base("ArbaMcp.Compilador", isCollectible: false)
            {
                _carpeta = carpeta;
                _nucleo = nucleo;
            }

            protected override Assembly Load(AssemblyName nombre)
            {
                string n = nombre.Name ?? "";
                if (n == "ArbaMcp.Nucleo") return LoadFromAssemblyPath(_nucleo);
                if (!n.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal)) return null;
                string cultura = nombre.CultureName;
                string ruta = string.IsNullOrEmpty(cultura) || cultura == "neutral"
                    ? Path.Combine(_carpeta, n + ".dll")
                    : Path.Combine(_carpeta, cultura, n + ".dll");
                return File.Exists(ruta) ? LoadFromAssemblyPath(ruta) : null;
            }
        }
    }

    /// <summary>
    /// Una ejecución de un script compilado: lo carga en un AssemblyLoadContext coleccionable propio (uno por ejecución),
    /// llama a su método y lo descarga al terminar. Las referencias del script (ArbaMcp, ArbaMcp.Nucleo, las DLL de
    /// AutoCAD y Civil 3D) se resuelven a los ensamblados ya cargados en Civil 3D, para que los tipos sean los mismos que
    /// usa el plugin. Si algo del script quedara enganchado (no debería: los eventos están prohibidos), su memoria no
    /// se liberaría hasta cerrar Civil 3D, pero nada más.
    /// </summary>
    public sealed class EjecucionScript : IDisposable
    {
        private ContextoScript _contexto;
        private MethodInfo _metodo;

        public EjecucionScript(byte[] ensamblado, byte[] pdb, string tipo, string metodo, string nombre, params Assembly[] preferidos)
        {
            _contexto = new ContextoScript(nombre ?? "ArbaMcpScript", preferidos ?? new Assembly[0]);
            Assembly asm;
            using (var pe = new MemoryStream(ensamblado))
            using (var simbolos = pdb != null ? new MemoryStream(pdb) : null)
                asm = _contexto.LoadFromStream(pe, simbolos);
            _metodo = asm.GetType(tipo, throwOnError: true).GetMethod(metodo, BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(tipo + "." + metodo);
        }

        /// <summary>Llama al método del script; una excepción del script sale tal cual (sin TargetInvocationException).</summary>
        public object Invocar(params object[] argumentos)
        {
            if (_metodo == null) throw new ObjectDisposedException(nameof(EjecucionScript));
            return _metodo.Invoke(null, BindingFlags.DoNotWrapExceptions, null, argumentos, null);
        }

        /// <summary>Descarga el contexto del script; la referencia débil permite comprobar que la memoria se libera.</summary>
        public WeakReference Descargar()
        {
            if (_contexto == null) return new WeakReference(null);
            _metodo = null;
            var debil = new WeakReference(_contexto);
            _contexto.Unload();
            _contexto = null;
            return debil;
        }

        public void Dispose() => Descargar();

        /// <summary>Línea del código del agente donde ocurrió una excepción (por el PDB, archivo 'script'), o null.</summary>
        public static int? LineaDe(Exception ex)
        {
            if (ex == null) return null;
            try
            {
                foreach (var f in new StackTrace(ex, true).GetFrames() ?? new StackFrame[0])
                    if (string.Equals(f.GetFileName(), CodigoCompiladorNombres.RutaScript, StringComparison.Ordinal) && f.GetFileLineNumber() > 0)
                        return f.GetFileLineNumber();
            }
            catch { }
            return null;
        }

        private sealed class ContextoScript : AssemblyLoadContext
        {
            private readonly Assembly[] _preferidos;

            public ContextoScript(string nombre, Assembly[] preferidos) : base(nombre, isCollectible: true)
            {
                _preferidos = preferidos;
            }

            protected override Assembly Load(AssemblyName nombre)
            {
                foreach (var a in _preferidos)
                    if (a != null && string.Equals(a.GetName().Name, nombre.Name, StringComparison.OrdinalIgnoreCase)) return a;
                foreach (var a in Default.Assemblies)
                    if (string.Equals(a.GetName().Name, nombre.Name, StringComparison.OrdinalIgnoreCase)) return a;
                // Un ensamblado cargado en otro contexto no coleccionable (por ejemplo, si AutoCAD aísla sus plugins);
                // nunca el del compilador ni otro script
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a.IsDynamic || !string.Equals(a.GetName().Name, nombre.Name, StringComparison.OrdinalIgnoreCase)) continue;
                    var ctx = GetLoadContext(a);
                    if (ctx == null || ctx.IsCollectible || ctx.Name == "ArbaMcp.Compilador") continue;
                    return a;
                }
                return null;
            }
        }
    }

    /// <summary>Nombres compartidos con el compilador sin depender de Roslyn.</summary>
    public static class CodigoCompiladorNombres
    {
        /// <summary>Nombre de archivo que llevan las líneas del código del agente en las directivas #line y en el PDB.</summary>
        public const string RutaScript = "script";
    }
}
