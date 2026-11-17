using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>Progresivas de las líneas de muestreo: el rango usado (el pedido o el eje entero) y cada progresiva, con los avisos de los ajustes.</summary>
    public sealed class RangoMuestreo
    {
        public double Inicio, Fin;
        public List<double> Progresivas = new List<double>();
        public List<string> Avisos = new List<string>();
        public int Numero => Progresivas.Count;
    }

    /// <summary>
    /// Una fuente que se puede muestrear en un grupo de líneas de muestreo (SampleLineGroup.GetSectionSources): su
    /// nombre, su clase (superficie, corredor, superficie_corredor u otra; la decide el plugin por el tipo del objeto),
    /// el texto de SectionSourceType tal como lo da la API y si se muestrea.
    /// </summary>
    public sealed class FuenteSeccion
    {
        public string Nombre;
        public string Clase;
        public string Tipo;
        public bool Muestrear;

        public FuenteSeccion() { }
        public FuenteSeccion(string nombre, string clase, string tipo = null) { Nombre = nombre; Clase = clase; Tipo = tipo; }
    }

    /// <summary>
    /// Nombres de las líneas de un grupo nuevo, únicos en todo el dibujo (crear_secciones, desde 1.14.2): Civil 3D no deja
    /// crear una línea de muestreo con el nombre de otra que ya existe en el dibujo ("Sample line name should not
    /// duplicate.", informe 1.13.0 del 14/11/2026, paso 9), aunque sea de otro grupo o de otro eje.
    /// </summary>
    public sealed class NombresDeLineas
    {
        /// <summary>Nombre de cada línea, uno por progresiva: el que se pasa a SampleLine.Create.</summary>
        public List<string> Lineas = new List<string>();
        /// <summary>Nombre por progresiva sin prefijo ("0+020.00"): el que va detrás del grupo en el nombre de la vista.</summary>
        public List<string> Base = new List<string>();
        /// <summary>Texto que va delante de cada línea ("Muestreo 100 "); null si ningún nombre chocaba.</summary>
        public string Prefijo;
        /// <summary>Nombres por progresiva que ya existían en el dibujo (los que obligaron a poner el prefijo).</summary>
        public List<string> Chocaban = new List<string>();
        /// <summary>Aviso para la respuesta (qué chocaba, con qué y qué nombres se usan); null si no hubo que cambiar nada.</summary>
        public string Aviso;
    }

    /// <summary>
    /// Nombres de un grupo nuevo con un nombre de grupo dado (crear_secciones, desde 1.19.3): sus líneas (únicas en el
    /// dibujo, NombresLineasUnicos), sus vistas ('&lt;grupo&gt; &lt;progresiva&gt;', " (2)" si ya existen) y los avisos
    /// que tocan. Se calcula para el nombre pedido y, si ese nombre ya lo usa un grupo de otro eje, también para el
    /// alternativo, por si Civil 3D lo rechaza.
    /// </summary>
    public sealed class PlanNombresGrupo
    {
        public string Grupo;
        public NombresDeLineas Lineas;
        public List<string> Vistas = new List<string>();
        public int VistasRenombradas;
        public List<string> Avisos = new List<string>();
    }

    /// <summary>
    /// Nombre del grupo nuevo frente a los grupos de los demás ejes (desde 1.19.3). Civil 3D no deja repetir el nombre de
    /// una línea de muestreo en todo el dibujo (1.14.2); si tampoco deja repetir el de un grupo en otro eje está por
    /// verificar (VALIDACION_1193). Si el nombre ya lo usa un grupo de otro eje, se prepara un nombre alternativo para
    /// usarlo solo si Civil 3D rechaza el pedido.
    /// </summary>
    public sealed class NombreDeGrupo
    {
        /// <summary>El nombre pedido (o el de por defecto).</summary>
        public string Pedido;
        /// <summary>Ejes que ya tienen un grupo con ese nombre (sin distinguir mayúsculas); vacío si ninguno.</summary>
        public List<string> EnOtrosEjes = new List<string>();
        /// <summary>"&lt;eje&gt; - &lt;nombre&gt;" (o con " (2)"), libre en todo el dibujo; null si el nombre no está en otro eje.</summary>
        public string Alternativo;
        /// <summary>Aviso previo (simulación y antes de crear); null si el nombre no está en otro eje.</summary>
        public string Aviso;
        /// <summary>Grupos leídos de todos los ejes.</summary>
        public int GruposLeidos;
    }

    /// <summary>
    /// Una fuente muestreada del grupo, para saber de qué objeto del dibujo es cada sección de una línea (desde 1.19.3):
    /// su nombre en el dibujo, su clase (Secciones.Clase*), la clave de su id, el corredor si es una superficie de
    /// corredor y la clave de la sección que devolvió SampleLine.GetSectionId con su id (null si no se llamó o no dio nada).
    /// </summary>
    public sealed class FuenteParaIdentificar
    {
        public string Nombre;
        public string Clase;
        public string Id;
        public string Corredor;
        public string ClaveGetSectionId;
        public FuenteParaIdentificar() { }
        public FuenteParaIdentificar(string nombre, string clase, string id, string corredor = null, string claveGetSectionId = null)
        {
            Nombre = nombre; Clase = clase; Id = id; Corredor = corredor; ClaveGetSectionId = claveGetSectionId;
        }
    }

    /// <summary>
    /// De qué fuente del dibujo es una sección (datos.lineas[].secciones_detalle de crear_secciones, desde 1.19.3):
    /// Section.SourceName, la superficie del dibujo (null si la fuente no es una superficie o no se identificó), la clase
    /// de la fuente y con qué intento de IdentificacionSecciones se encontró (null si no se identificó).
    /// </summary>
    public sealed class SeccionDetalle
    {
        public string SourceName;
        public string Superficie;
        public string Clase;
        public string Metodo;
    }

    /// <summary>Cuántas secciones del grupo se identificaron con cada intento y cuántas no (datos.secciones_identificadas, desde 1.19.3).</summary>
    public sealed class ResumenSecciones
    {
        public Dictionary<string, int> Metodos = new Dictionary<string, int>();
        public int Secciones, SinIdentificar;

        public void Anotar(IEnumerable<SeccionDetalle> detalle)
        {
            foreach (var d in detalle ?? new SeccionDetalle[0])
            {
                if (d == null) continue;
                Secciones++;
                if (string.IsNullOrEmpty(d.Metodo)) { SinIdentificar++; continue; }
                Metodos.TryGetValue(d.Metodo, out int n);
                Metodos[d.Metodo] = n + 1;
            }
        }
    }

    /// <summary>
    /// Colocación de las vistas de sección en cuadrícula (crear_secciones, 1.13.0): columnas de izquierda a derecha y,
    /// dentro de cada columna, una vista encima de la otra (progresiva creciente de abajo arriba, como la esquina de
    /// inicio "inferior izquierda" de Civil 3D). Cada vista se coloca con la extensión real de la anterior: la siguiente
    /// de la columna va encima de su borde superior más la separación; la primera de una columna nueva, a la derecha
    /// del borde derecho máximo de la columna anterior más la separación, a la altura del punto de inserción. Así no
    /// hace falta conocer el alto de las vistas de antemano (depende de las cotas de cada sección). Si una extensión no
    /// se pudo leer, se usa una estimada (el tamaño de la anterior o el dado).
    /// </summary>
    public sealed class CuadriculaVistas
    {
        private readonly Punto2 _insercion;
        private readonly int _filas;
        private readonly double _separacion;
        private readonly List<(Punto2 lugar, Extension2 ext, bool leida)> _colocadas = new List<(Punto2, Extension2, bool)>();
        private double _xColumna, _maxXColumna;

        public CuadriculaVistas(Punto2 insercion, int filas, double separacion)
        {
            if (filas < 1) throw new ArgumentException("Las filas por columna deben ser al menos 1.");
            _insercion = insercion; _filas = filas; _separacion = Math.Max(0, separacion);
        }

        public int Colocadas => _colocadas.Count;
        public int ExtensionesLeidas => _colocadas.Count(c => c.leida);
        public IReadOnlyList<Extension2> Extensiones => _colocadas.Select(c => c.ext).ToList();

        /// <summary>Fila y columna (desde 1) de la vista de índice i (desde 0): la fila 1 es la de abajo.</summary>
        public static (int fila, int columna) FilaColumna(int indice, int filas) => (indice % filas + 1, indice / filas + 1);

        /// <summary>Punto de inserción de la vista siguiente.</summary>
        public Punto2 Siguiente()
        {
            int i = _colocadas.Count;
            if (i == 0) return _insercion;
            var (lugar, ext, _) = _colocadas[i - 1];
            if (i % _filas == 0)
                // columna nueva: a la derecha de toda la columna anterior, a la altura del punto de inserción
                return new Punto2(_maxXColumna + _separacion + (lugar.X - ext.MinX), _insercion.Y);
            // misma columna: encima de la anterior
            return new Punto2(_xColumna, ext.MaxY + _separacion + (lugar.Y - ext.MinY));
        }

        /// <summary>
        /// Anota la vista recién creada en 'lugar' con su extensión real (null o no válida si no se pudo leer: se estima
        /// con el tamaño de la anterior o, si es la primera, con anchoEstimado × altoEstimado). Devuelve true si la
        /// extensión era real.
        /// </summary>
        public bool Registrar(Punto2 lugar, Extension2? extension, double anchoEstimado, double altoEstimado)
        {
            bool leida = extension.HasValue && extension.Value.EsValida && extension.Value.Ancho > 1e-9 && extension.Value.Alto > 1e-9;
            Extension2 ext;
            if (leida) ext = extension.Value;
            else
            {
                double ancho = anchoEstimado, alto = altoEstimado;
                if (_colocadas.Count > 0) { var previa = _colocadas[_colocadas.Count - 1].ext; ancho = previa.Ancho; alto = previa.Alto; }
                ext = new Extension2(lugar.X, lugar.Y, lugar.X + Math.Max(1, ancho), lugar.Y + Math.Max(1, alto));
            }
            if (_colocadas.Count % _filas == 0) { _xColumna = lugar.X; _maxXColumna = ext.MaxX; }
            else _maxXColumna = Math.Max(_maxXColumna, ext.MaxX);
            _colocadas.Add((lugar, ext, leida));
            return leida;
        }
    }

    /// <summary>
    /// Lógica de crear_secciones (1.13.0) que no depende de Civil 3D: validación de intervalo, anchos y filas, rango y
    /// progresivas de las líneas de muestreo, nombres por defecto, lectura de 'fuentes' y elección de las fuentes que se
    /// muestrean, separación de la cuadrícula de vistas, solapes entre vistas, texto de la acción y comprobación de lo
    /// releído del dibujo. Se prueba en ArbaMcp.Pruebas con datos sintéticos.
    /// </summary>
    public static class Secciones
    {
        /// <summary>Un 'inicio' o 'fin' que se sale del eje menos que esto (metros) se ajusta al extremo con aviso.</summary>
        public const double ToleranciaExtremo = 0.01;
        /// <summary>Margen al comparar progresivas y anchos releídos con los pedidos (metros).</summary>
        public const double ToleranciaProgresiva = 0.01;
        /// <summary>Más líneas que esto en una llamada es casi siempre un intervalo equivocado (y una transacción enorme).</summary>
        public const int MaximoLineas = 500;
        public const int FilasPorDefecto = 5;
        /// <summary>Separación entre vistas: el 10 % del ancho de franja (izquierdo + derecho), nunca menos de SeparacionMinima.</summary>
        public const double FraccionSeparacion = 0.10;
        public const double SeparacionMinima = 5.0;

        public const string ClaseSuperficie = "superficie";
        public const string ClaseCorredor = "corredor";
        public const string ClaseSuperficieCorredor = "superficie_corredor";
        public const string ClaseOtra = "otra";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ nombres
        /// <summary>Nombre del grupo de líneas de muestreo: el pedido o "&lt;eje&gt; - Muestreo".</summary>
        public static string NombreGrupo(string pedido, string alineamiento)
            => string.IsNullOrWhiteSpace(pedido) ? alineamiento.Trim() + " - Muestreo" : pedido.Trim();

        /// <summary>Nombre de una línea de muestreo por su progresiva, con el formato de Civil 3D en metros: 20 → "0+020.00", 1234.5 → "1+234.50".</summary>
        public static string NombreLinea(double progresiva)
        {
            double r = Math.Round(Math.Abs(progresiva), 2, MidpointRounding.AwayFromZero);
            long km = (long)Math.Floor(r / 1000.0);
            double resto = Math.Round(r - km * 1000.0, 2, MidpointRounding.AwayFromZero);
            if (resto >= 1000.0) { km++; resto -= 1000.0; }
            return (progresiva < 0 && r > 0 ? "-" : "") + km.ToString(Inv) + "+" + resto.ToString("000.00", Inv);
        }

        /// <summary>Nombres de las líneas, uno por progresiva; si dos coinciden al redondear, la segunda lleva " (2)".</summary>
        public static List<string> NombresLineas(IList<double> progresivas)
        {
            var nombres = new List<string>();
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (double p in progresivas)
            {
                string n = NombreLinea(p), candidato = n;
                for (int k = 2; !usados.Add(candidato); k++) candidato = n + " (" + k + ")";
                nombres.Add(candidato);
            }
            return nombres;
        }

        /// <summary>Nombre de la vista de sección de una línea: "&lt;grupo&gt; &lt;línea&gt;" ("Eje 1 - Muestreo 0+020.00").</summary>
        public static string NombreVista(string grupo, string linea) => grupo.Trim() + " " + linea;

        /// <summary>
        /// Nombres de las líneas de un grupo nuevo, únicos en el dibujo (desde 1.14.2). 'existentes': nombre de cada línea
        /// de muestreo que ya hay en el dibujo → dónde está (por ejemplo "grupo 'Eje 1 - Muestreo' del eje 'Eje 1'"), sin
        /// distinguir mayúsculas. Si ningún nombre por progresiva ("0+020.00") choca, se usan tal cual (el caso normal: el
        /// primer grupo). Si choca alguno, TODAS las líneas del grupo llevan delante el nombre del grupo
        /// ("Muestreo 100 0+100.00"), para que los nombres del grupo sean homogéneos, y si aun así alguno choca, " (2)",
        /// " (3)"... El aviso dice qué chocaba, con qué y qué nombres se usan.
        /// </summary>
        public static NombresDeLineas NombresLineasUnicos(IList<double> progresivas, string grupo, IDictionary<string, string> existentes)
        {
            var r = new NombresDeLineas { Base = NombresLineas(progresivas ?? new List<double>()) };
            var ocupados = Ocupados(existentes?.Keys);
            r.Chocaban = r.Base.Where(n => ocupados.Contains(Limpio(n))).ToList();
            if (r.Chocaban.Count == 0)
            {
                r.Lineas = r.Base.ToList();
                return r;
            }
            string g = (grupo ?? "").Trim();
            r.Prefijo = g + " ";
            r.Lineas = NombresUnicos(r.Base.Select(b => r.Prefijo + b).ToList(), ocupados);

            var donde = r.Chocaban
                .Select(n => existentes.FirstOrDefault(kv => kv.Key != null && string.Equals(Limpio(kv.Key), Limpio(n), StringComparison.OrdinalIgnoreCase)).Value)
                .Where(d => !string.IsNullOrWhiteSpace(d)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            r.Aviso = "Ya hay líneas de muestreo con " + (r.Chocaban.Count == 1 ? "el nombre " : "los nombres ") + Enumerar(r.Chocaban, 5)
                + " en el dibujo" + (donde.Count == 0 ? "" : " (" + string.Join("; ", donde.Take(3)) + (donde.Count > 3 ? "; …" : "") + ")")
                + " y Civil 3D no deja repetir el nombre de una línea en todo el dibujo: las " + r.Lineas.Count + " líneas del grupo '" + g
                + "' llevan delante el nombre del grupo ('" + r.Lineas[0] + "'" + (r.Lineas.Count > 1 ? " … '" + r.Lineas[r.Lineas.Count - 1] + "'" : "")
                + "). Las vistas se siguen llamando '<grupo> <progresiva>'.";
            return r;
        }

        /// <summary>
        /// Hace únicos unos nombres frente a los 'ocupados' (sin distinguir mayúsculas ni espacios en los extremos) y entre
        /// sí: el que choca lleva " (2)", " (3)"... Los que no chocan no cambian.
        /// </summary>
        public static List<string> NombresUnicos(IList<string> nombres, IEnumerable<string> ocupados)
        {
            var usados = Ocupados(ocupados);
            var lista = new List<string>();
            foreach (string n in nombres ?? new List<string>())
            {
                string candidato = n;
                for (int k = 2; usados.Contains(Limpio(candidato)); k++) candidato = n + " (" + k + ")";
                usados.Add(Limpio(candidato));
                lista.Add(candidato);
            }
            return lista;
        }

        /// <summary>
        /// True si el mensaje de una excepción de SampleLine.Create o SectionView.Create es el de nombre repetido
        /// ("Sample line name should not duplicate.", informe 1.13.0 del 14/11/2026; se acepta también en español).
        /// </summary>
        public static bool EsNombreRepetido(string mensaje)
            => !string.IsNullOrEmpty(mensaje) && (mensaje.IndexOf("duplic", StringComparison.OrdinalIgnoreCase) >= 0 || mensaje.IndexOf("repetid", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>
        /// Error claro cuando Civil 3D rechaza un nombre que el plugin no vio al leer el dibujo (red de seguridad: el
        /// texto crudo de Civil 3D no llega al agente). 'que': "la línea de muestreo" o "la vista de sección".
        /// </summary>
        public static string MensajeNombreRepetido(string que, string nombre, double progresiva, int nombresLeidos)
            => "Civil 3D no deja crear " + que + " '" + nombre + "' (progresiva " + Num(progresiva) + "): ya hay otro objeto con ese nombre en el dibujo (Civil 3D no deja repetir ese nombre en todo el dibujo, aunque sea de otro grupo o de otro eje) y el plugin no lo encontró entre los "
               + nombresLeidos + " nombres que leyó (grupos de muestreo de todos los ejes y espacio modelo). Nada queda escrito (la transacción se descarta) y no se reintenta. Revisa los grupos de líneas de muestreo de todos los ejes en Prospector y anótalo como hallazgo (miembros_por_verificar_civil3d.md, sección 1.14.2).";

        /// <summary>
        /// Nombres de líneas y vistas de un grupo llamado 'grupo' (desde 1.19.3; la regla es la de 1.14.2): las líneas
        /// frente a 'lineasExistentes' (NombresLineasUnicos) y las vistas '&lt;grupo&gt; &lt;progresiva&gt;' frente a
        /// 'vistasExistentes' (" (2)" si chocan). Los avisos: el del prefijo de las líneas y, con vistas, el de las vistas
        /// renombradas.
        /// </summary>
        public static PlanNombresGrupo PlanNombres(IList<double> progresivas, string grupo, IDictionary<string, string> lineasExistentes, IEnumerable<string> vistasExistentes, bool conVistas)
        {
            var p = new PlanNombresGrupo { Grupo = (grupo ?? "").Trim() };
            p.Lineas = NombresLineasUnicos(progresivas, p.Grupo, lineasExistentes);
            if (p.Lineas.Aviso != null) p.Avisos.Add(p.Lineas.Aviso);
            var vistasBase = p.Lineas.Base.Select(b => NombreVista(p.Grupo, b)).ToList();
            p.Vistas = NombresUnicos(vistasBase, vistasExistentes);
            p.VistasRenombradas = vistasBase.Where((v, i) => !string.Equals(v, p.Vistas[i], StringComparison.Ordinal)).Count();
            if (conVistas && p.VistasRenombradas > 0)
                p.Avisos.Add(p.VistasRenombradas + " vistas de sección llevan ' (2)' detrás porque ya hay vistas con su nombre en el dibujo (por ejemplo '" + p.Vistas.First(v => !vistasBase.Contains(v)) + "').");
            return p;
        }

        /// <summary>
        /// Mira si el nombre del grupo nuevo ya lo usa un grupo de otro eje (desde 1.19.3). 'gruposDelDibujo': (nombre del
        /// grupo, nombre de su eje) de los grupos de todos los ejes. Los del propio eje no cuentan como choque (ese caso
        /// ya es error antes) pero sí como ocupados para el alternativo. Si choca: el alternativo es "&lt;eje&gt; -
        /// &lt;nombre&gt;" ("Eje 2 - Muestreo 100"), o el nombre tal cual si ya empieza por el del eje, con " (2)", " (3)"...
        /// mientras exista algún grupo así en el dibujo; y un aviso que dice qué pasará en cada caso.
        /// </summary>
        public static NombreDeGrupo NombreDeGrupoNuevo(string grupo, string eje, IEnumerable<(string grupo, string eje)> gruposDelDibujo)
        {
            var lista = (gruposDelDibujo ?? new (string, string)[0]).Where(x => !string.IsNullOrWhiteSpace(x.grupo)).ToList();
            string g = Limpio(grupo), e = Limpio(eje);
            var r = new NombreDeGrupo { Pedido = g, GruposLeidos = lista.Count };
            r.EnOtrosEjes = lista
                .Where(x => string.Equals(Limpio(x.grupo), g, StringComparison.OrdinalIgnoreCase) && !string.Equals(Limpio(x.eje), e, StringComparison.OrdinalIgnoreCase))
                .Select(x => Limpio(x.eje)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (r.EnOtrosEjes.Count == 0) return r;

            bool empiezaPorEje = e.Length > 0 && (string.Equals(g, e, StringComparison.OrdinalIgnoreCase) || g.StartsWith(e + " ", StringComparison.OrdinalIgnoreCase));
            string candidato = e.Length == 0 || empiezaPorEje ? g : e + " - " + g;
            r.Alternativo = NombresUnicos(new List<string> { candidato }, lista.Select(x => x.grupo).Concat(new[] { g }))[0];
            r.Aviso = "Ya hay un grupo de líneas de muestreo llamado '" + g + "' en " + EnEjes(r.EnOtrosEjes)
                + ". No se sabe todavía si Civil 3D deja repetir el nombre de un grupo en otro eje (por verificar, VALIDACION_1193): se intenta con '" + g
                + "' y, si Civil 3D lo rechaza por repetido, el grupo se crea como '" + r.Alternativo + "' (con sus líneas y vistas) en la misma transacción, con otro aviso.";
            return r;
        }

        /// <summary>
        /// True si, tras rechazar Civil 3D el nombre del grupo, se puede intentar con el alternativo (desde 1.19.3): solo si
        /// el rechazo es por nombre repetido (EsNombreRepetido), el plugin había visto ese nombre en otro eje (hay
        /// alternativo) y el intento rechazado no dejó nada (el eje tiene los mismos grupos que antes, leídos los dos).
        /// </summary>
        public static bool PuedeReintentarGrupo(string mensajeCivil3d, string alternativo, int gruposAntes, int gruposDespues)
            => EsNombreRepetido(mensajeCivil3d) && !string.IsNullOrWhiteSpace(alternativo) && gruposAntes >= 0 && gruposAntes == gruposDespues;

        /// <summary>
        /// Error cuando Civil 3D rechaza el nombre del grupo y no se reintenta (desde 1.19.3): porque no era por nombre
        /// repetido no se llama aquí (sale el error de Civil 3D); sí cuando el plugin no vio el nombre en otro eje o cuando
        /// el intento rechazado dejó algo en el eje. Sin el texto crudo de Civil 3D (va al historial).
        /// </summary>
        public static string MensajeGrupoRechazado(NombreDeGrupo grupo, string eje, int gruposAntes, int gruposDespues)
        {
            string g = grupo?.Pedido ?? "", cola = " Nada queda escrito (la transacción se descarta) y no se reintenta.";
            if (grupo == null || string.IsNullOrWhiteSpace(grupo.Alternativo))
                return "Civil 3D no deja crear el grupo de líneas de muestreo '" + g + "' en el eje '" + eje + "': dice que el nombre está repetido y el plugin no lo encontró entre los "
                     + (grupo?.GruposLeidos ?? 0) + " grupos que leyó de todos los ejes." + cola
                     + " Pasa otro 'nombre', revisa los grupos de líneas de muestreo de todos los ejes en Prospector y anótalo como hallazgo (miembros_por_verificar_civil3d.md, sección 1.19.3).";
            return "Civil 3D no deja crear el grupo de líneas de muestreo '" + g + "' en el eje '" + eje + "' porque ya hay uno con ese nombre en " + EnEjes(grupo.EnOtrosEjes)
                 + ", y no se prueba con '" + grupo.Alternativo + "' porque "
                 + (gruposAntes < 0 || gruposDespues < 0 ? "no se pudieron contar los grupos del eje para comprobar que el intento rechazado no dejó nada"
                                                         : "el eje pasó de " + gruposAntes + " a " + gruposDespues + " grupos con el intento rechazado")
                 + "." + cola + " Pasa otro 'nombre' (por ejemplo '" + grupo.Alternativo + "').";
        }

        /// <summary>Error cuando Civil 3D rechaza también el nombre alternativo del grupo (desde 1.19.3).</summary>
        public static string MensajeAlternativoRechazado(NombreDeGrupo grupo, string eje, string motivo)
            => "Civil 3D no deja crear el grupo de líneas de muestreo '" + (grupo?.Pedido ?? "") + "' en el eje '" + eje + "' (ya hay uno con ese nombre en " + EnEjes(grupo?.EnOtrosEjes)
             + ") ni con el nombre alternativo '" + (grupo?.Alternativo ?? "") + "': " + motivo + ". Nada queda escrito (la transacción se descarta) y no se reintenta más. Pasa otro 'nombre'.";

        /// <summary>Aviso cuando Civil 3D rechazó el nombre del grupo y se creó con el alternativo (desde 1.19.3).</summary>
        public static string AvisoGrupoRenombrado(NombreDeGrupo grupo, string eje)
            => "Civil 3D no dejó crear el grupo '" + grupo.Pedido + "' en el eje '" + eje + "' porque ya hay uno con ese nombre en " + EnEjes(grupo.EnOtrosEjes)
             + " (rechazo por nombre repetido, en datos.nombre_grupo.texto_civil3d): se creó como '" + grupo.Alternativo + "', y sus líneas y vistas llevan ese nombre. El grupo del otro eje no se tocó.";

        /// <summary>Aviso cuando Civil 3D aceptó un nombre de grupo que ya usa otro eje (desde 1.19.3).</summary>
        public static string AvisoGrupoAceptado(NombreDeGrupo grupo, string eje)
            => "Civil 3D dejó crear el grupo '" + grupo.Pedido + "' en el eje '" + eje + "' aunque " + EnEjes(grupo.EnOtrosEjes)
             + " ya tiene uno con ese nombre (datos.nombre_grupo.rechazado_por_civil3d: false): el nombre del grupo se dejó tal cual.";

        private static string EnEjes(IList<string> ejes)
        {
            var l = (ejes ?? new List<string>()).ToList();
            if (l.Count == 0) return "otro eje";
            return (l.Count == 1 ? "el eje " : "los ejes ") + Enumerar(l, 5);
        }

        /// <summary>
        /// De qué fuente del dibujo es cada sección de una línea (desde 1.19.3), con IdentificacionSecciones.Identificar
        /// (el orden validado en calcular_volumenes: GetSectionId, CorridorSurfaceId, SourceId, SourceName y
        /// "&lt;corredor&gt; &lt;superficie&gt;"). Primero las superficies (terreno y de corredor) y después los corredores
        /// y lo demás; una sección nunca se asigna a dos fuentes. Devuelve un detalle por sección, en el orden de 'secciones'.
        /// </summary>
        public static List<SeccionDetalle> IdentificarSecciones(IList<SeccionCandidata> secciones, IList<FuenteParaIdentificar> fuentes)
        {
            var l = secciones ?? new List<SeccionCandidata>();
            var detalle = l.Select(s => new SeccionDetalle { SourceName = s?.NombreFuente }).ToList();
            var usadas = new HashSet<int>();
            int Orden(FuenteParaIdentificar f) => f.Clase == ClaseSuperficie || f.Clase == ClaseSuperficieCorredor ? 0 : f.Clase == ClaseCorredor ? 1 : 2;
            foreach (var f in (fuentes ?? new List<FuenteParaIdentificar>()).Where(x => x != null && !string.IsNullOrWhiteSpace(x.Nombre)).OrderBy(Orden))
            {
                var buscada = new SuperficieBuscada(f.Id, f.Nombre.Trim(), f.Clase == ClaseSuperficieCorredor ? f.Corredor : null);
                var r = IdentificacionSecciones.Identificar(l, buscada, f.ClaveGetSectionId, usadas);
                if (!r.Encontrada) continue;
                usadas.Add(r.Indice);
                var d = detalle[r.Indice];
                d.Clase = f.Clase;
                d.Metodo = r.Metodo;
                d.Superficie = f.Clase == ClaseSuperficie || f.Clase == ClaseSuperficieCorredor ? f.Nombre.Trim() : null;
            }
            return detalle;
        }

        private static HashSet<string> Ocupados(IEnumerable<string> nombres)
            => new HashSet<string>((nombres ?? new string[0]).Where(n => !string.IsNullOrWhiteSpace(n)).Select(Limpio), StringComparer.OrdinalIgnoreCase);

        private static string Limpio(string n) => (n ?? "").Trim();

        private static string Enumerar(IList<string> l, int max)
            => string.Join(", ", l.Take(max).Select(x => "'" + x + "'")) + (l.Count > max ? " y " + (l.Count - max) + " más" : "");

        // ------------------------------------------------------------------ validación de parámetros
        /// <summary>El intervalo (m) es obligatorio, finito y mayor que 0.</summary>
        public static double ValidarIntervalo(double? intervalo)
        {
            if (!intervalo.HasValue) throw new ArgumentException("Falta el parámetro obligatorio 'intervalo' (metros entre líneas de muestreo, por ejemplo 20).");
            if (!Finito(intervalo.Value) || intervalo.Value <= 0)
                throw new ArgumentException("El 'intervalo' debe ser un número mayor que 0 (metros entre líneas de muestreo); llegó " + Num(intervalo.Value) + ".");
            return intervalo.Value;
        }

        /// <summary>Un ancho de franja (m) es obligatorio, finito y mayor que 0.</summary>
        public static double ValidarAncho(double? ancho, string parametro)
        {
            string lado = parametro.EndsWith("izquierdo", StringComparison.OrdinalIgnoreCase) ? "izquierda" : "derecha";
            if (!ancho.HasValue) throw new ArgumentException("Falta el parámetro obligatorio '" + parametro + "' (metros de franja a la " + lado + " del eje, por ejemplo 30).");
            if (!Finito(ancho.Value) || ancho.Value <= 0)
                throw new ArgumentException("El '" + parametro + "' debe ser un número mayor que 0 (metros de franja a la " + lado + " del eje); llegó " + Num(ancho.Value) + ".");
            return ancho.Value;
        }

        /// <summary>Filas por columna de la cuadrícula de vistas: por defecto 5; si se pasa, un entero mayor o igual que 1.</summary>
        public static int ValidarFilas(double? filas)
        {
            if (!filas.HasValue) return FilasPorDefecto;
            double f = filas.Value;
            if (!Finito(f) || f < 1 || Math.Abs(f - Math.Round(f)) > 1e-9 || f > 10000)
                throw new ArgumentException("'filas_por_columna' debe ser un número entero mayor o igual que 1 (vistas de sección por columna de la cuadrícula); llegó " + Num(f) + ".");
            return (int)Math.Round(f);
        }

        // ------------------------------------------------------------------ rango y progresivas
        /// <summary>
        /// Progresivas de las líneas: desde 'inicio' (o el del eje) cada 'intervalo' y, al final, el 'fin' (o el del eje)
        /// si queda a más de 0,01 de la última; si queda a menos, la última se sustituye por el fin. Un 'inicio' o 'fin'
        /// que se sale del eje menos de 0,01 se ajusta al extremo con aviso; más lejos, error con la pista
        /// listar_alineamientos. Error si el fin no es mayor que el inicio o si salen más de MaximoLineas líneas.
        /// </summary>
        public static RangoMuestreo Progresivas(double inicioEje, double finEje, double? inicioPedido, double? finPedido, double intervalo, string eje)
        {
            if (!Finito(inicioEje) || !Finito(finEje))
                throw new ArgumentException("No se pudieron leer las progresivas del eje '" + eje + "'. Usa listar_alineamientos.");
            if (inicioPedido.HasValue && !Finito(inicioPedido.Value)) throw new ArgumentException("El parámetro 'inicio' no es un número válido.");
            if (finPedido.HasValue && !Finito(finPedido.Value)) throw new ArgumentException("El parámetro 'fin' no es un número válido.");
            ValidarIntervalo(intervalo);

            var r = new RangoMuestreo();
            string textoEje = "del eje '" + eje + "' (" + Num(inicioEje) + " a " + Num(finEje) + ")";
            r.Inicio = Ajustar(inicioPedido ?? inicioEje, inicioEje, finEje, "inicio", textoEje, r.Avisos);
            r.Fin = Ajustar(finPedido ?? finEje, inicioEje, finEje, "fin", textoEje, r.Avisos);
            if (r.Fin - r.Inicio <= 1e-6)
                throw new ArgumentException("El 'fin' (" + Num(r.Fin) + ") debe ser mayor que el 'inicio' (" + Num(r.Inicio) + ").");

            double longitud = r.Fin - r.Inicio;
            double estimadas = Math.Floor(longitud / intervalo) + 2;
            if (estimadas > MaximoLineas + 1)
                throw new ArgumentException("Con un 'intervalo' de " + Num(intervalo) + " m entre " + Num(r.Inicio) + " y " + Num(r.Fin) + " saldrían unas " + Num(estimadas - 1) + " líneas de muestreo; el máximo por llamada es " + MaximoLineas + ". Usa un intervalo mayor o parte el tramo con 'inicio' y 'fin'.");

            for (long k = 0; ; k++)
            {
                double s = r.Inicio + k * intervalo;
                if (s >= r.Fin - ToleranciaProgresiva) break;
                r.Progresivas.Add(s);
            }
            r.Progresivas.Add(r.Fin);
            if (r.Progresivas.Count > MaximoLineas)
                throw new ArgumentException("Salen " + r.Progresivas.Count + " líneas de muestreo; el máximo por llamada es " + MaximoLineas + ". Usa un intervalo mayor o parte el tramo con 'inicio' y 'fin'.");
            if (r.Progresivas.Count >= 2)
            {
                double ultimo = r.Progresivas[r.Progresivas.Count - 1] - r.Progresivas[r.Progresivas.Count - 2];
                if (Math.Abs(ultimo - intervalo) > ToleranciaProgresiva)
                    r.Avisos.Add("La última línea va en el final del tramo (" + Num(r.Fin) + "), a " + Num(ultimo) + " m de la anterior (el intervalo es " + Num(intervalo) + " m).");
            }
            return r;
        }

        private static double Ajustar(double v, double inicioEje, double finEje, string nombre, string textoEje, List<string> avisos)
        {
            if (v < inicioEje)
            {
                if (inicioEje - v > ToleranciaExtremo)
                    throw new ArgumentException("El '" + nombre + "' (" + Num(v) + ") queda fuera " + textoEje + ": debe estar entre " + Num(inicioEje) + " y " + Num(finEje) + ". Usa listar_alineamientos para ver el rango.");
                avisos.Add("El '" + nombre + "' " + Num(v) + " se ajusta al inicio del eje (" + Num(inicioEje) + ").");
                return inicioEje;
            }
            if (v > finEje)
            {
                if (v - finEje > ToleranciaExtremo)
                    throw new ArgumentException("El '" + nombre + "' (" + Num(v) + ") queda fuera " + textoEje + ": debe estar entre " + Num(inicioEje) + " y " + Num(finEje) + ". Usa listar_alineamientos para ver el rango.");
                avisos.Add("El '" + nombre + "' " + Num(v) + " se ajusta al final del eje (" + Num(finEje) + ").");
                return finEje;
            }
            return v;
        }

        // ------------------------------------------------------------------ fuentes
        /// <summary>
        /// Lee 'fuentes': una lista JSON de nombres (["Terreno", "Corredor 1"], como arreglo o como texto) o un texto con
        /// los nombres separados por ';'. Vacía si no se pasó. Error si el texto empieza por '[' y no es una lista de textos.
        /// </summary>
        public static List<string> LeerFuentes(JsonElement args, string parametro = "fuentes")
        {
            var lista = new List<string>();
            if (!Argumentos.Tiene(args, parametro)) return lista;
            var v = args.GetProperty(parametro);
            if (v.ValueKind == JsonValueKind.String)
            {
                string texto = (v.GetString() ?? "").Trim();
                if (texto.Length == 0) return lista;
                if (!texto.StartsWith("[", StringComparison.Ordinal))
                    return Distintos(texto.Split(';').Select(c => c.Trim()));
                try { using (var doc = JsonDocument.Parse(texto)) v = doc.RootElement.Clone(); }
                catch (JsonException ex) { throw new ArgumentException("El parámetro '" + parametro + "' debe ser una lista JSON de nombres, por ejemplo [\"Terreno\", \"Corredor 1\"]: " + ex.Message); }
            }
            if (v.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("El parámetro '" + parametro + "' debe ser una lista de nombres, por ejemplo [\"Terreno\", \"Corredor 1\"].");
            var nombres = new List<string>();
            foreach (var e in v.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.String)
                    throw new ArgumentException("El parámetro '" + parametro + "' debe ser una lista de nombres (textos), por ejemplo [\"Terreno\", \"Corredor 1\"].");
                nombres.Add(e.GetString());
            }
            return Distintos(nombres);
        }

        private static List<string> Distintos(IEnumerable<string> nombres)
            => nombres.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>
        /// Marca qué fuentes se muestrean. Sin 'pedidas': todas las superficies (terreno), los corredores y sus
        /// superficies; las de otra clase (redes de tuberías...) no, con aviso. Error si no hay ninguna superficie de
        /// terreno ("muestrea al menos el terreno"). Con 'pedidas': exactamente esas (error con la lista si alguna no
        /// está; aviso si ninguna es una superficie de terreno). Devuelve las que se muestrean.
        /// </summary>
        public static List<FuenteSeccion> ElegirFuentes(IList<FuenteSeccion> disponibles, IList<string> pedidas, List<string> avisos)
        {
            var lista = (disponibles ?? new List<FuenteSeccion>()).Where(f => f != null && !string.IsNullOrWhiteSpace(f.Nombre)).ToList();
            foreach (var f in lista) f.Muestrear = false;
            var pedidasLimpias = Distintos(pedidas ?? new List<string>());
            if (pedidasLimpias.Count == 0)
            {
                if (!lista.Any(f => f.Clase == ClaseSuperficie))
                    throw new ArgumentException("El dibujo no tiene ninguna superficie de terreno que muestrear" + (lista.Count == 0 ? "" : " (fuentes disponibles: " + Lista(lista) + ")") + ". Crea el terreno con crear_superficie_tin. Usa listar_superficies.");
                foreach (var f in lista) f.Muestrear = f.Clase != ClaseOtra;
                var otras = lista.Where(f => f.Clase == ClaseOtra).ToList();
                if (otras.Count > 0)
                    avisos?.Add("No se muestrean " + Lista(otras) + " (no son superficies ni corredores); pásalas en 'fuentes' si las quieres.");
            }
            else
            {
                foreach (string p in pedidasLimpias)
                {
                    var f = lista.FirstOrDefault(x => string.Equals(x.Nombre.Trim(), p, StringComparison.OrdinalIgnoreCase));
                    if (f == null)
                        throw new ArgumentException("No existe la fuente '" + p + "' para las líneas de muestreo. Fuentes disponibles: " + (lista.Count == 0 ? "ninguna" : Lista(lista)) + ". Usa listar_superficies y listar_corredores.");
                    f.Muestrear = true;
                }
                if (!lista.Any(f => f.Muestrear && f.Clase == ClaseSuperficie))
                    avisos?.Add("Entre las 'fuentes' pedidas no hay ninguna superficie de terreno: las secciones no tendrán la línea del terreno.");
            }
            return lista.Where(f => f.Muestrear).ToList();
        }

        private static string Lista(IEnumerable<FuenteSeccion> fuentes) => string.Join(", ", fuentes.Select(f => f.Nombre + " (" + f.Clase + ")"));

        /// <summary>Null si las fuentes muestreadas releídas son las previstas (sin importar el orden); si no, el texto de la diferencia.</summary>
        public static string DiferenciaFuentes(IEnumerable<string> previstas, IEnumerable<string> leidas)
        {
            var p = new HashSet<string>((previstas ?? new string[0]).Where(n => n != null).Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);
            var l = new HashSet<string>((leidas ?? new string[0]).Where(n => n != null).Select(n => n.Trim()), StringComparer.OrdinalIgnoreCase);
            var faltan = p.Where(n => !l.Contains(n)).ToList();
            var sobran = l.Where(n => !p.Contains(n)).ToList();
            if (faltan.Count == 0 && sobran.Count == 0) return null;
            return (faltan.Count > 0 ? "no quedaron muestreadas " + string.Join(", ", faltan) : "")
                 + (faltan.Count > 0 && sobran.Count > 0 ? "; " : "")
                 + (sobran.Count > 0 ? "quedaron muestreadas además " + string.Join(", ", sobran) : "");
        }

        // ------------------------------------------------------------------ vistas
        /// <summary>Separación entre vistas de la cuadrícula: el 10 % de la franja (izquierda + derecha), mínimo 5.</summary>
        public static double Separacion(double anchoIzquierdo, double anchoDerecho) => Math.Max(SeparacionMinima, FraccionSeparacion * (anchoIzquierdo + anchoDerecho));

        /// <summary>Columnas de la cuadrícula: n vistas con 'filas' por columna.</summary>
        public static int Columnas(int vistas, int filas) => vistas <= 0 ? 0 : (vistas + filas - 1) / filas;

        /// <summary>Número de pares de vistas cuyas extensiones se solapan (con área; tocarse en el borde no cuenta).</summary>
        public static int Solapes(IList<Extension2> extensiones)
        {
            int n = 0;
            for (int i = 0; i < extensiones.Count; i++)
                for (int j = i + 1; j < extensiones.Count; j++)
                {
                    var a = extensiones[i]; var b = extensiones[j];
                    if (!a.EsValida || !b.EsValida) continue;
                    if (Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX) > 1e-6 && Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY) > 1e-6) n++;
                }
            return n;
        }

        // ------------------------------------------------------------------ textos
        /// <summary>Texto de la acción para la respuesta.</summary>
        public static string Accion(string grupo, string eje, RangoMuestreo rango, double intervalo, double anchoIzquierdo, double anchoDerecho,
            IEnumerable<string> fuentes, bool conVistas, int filas, Punto2? insercion)
        {
            var nombres = (fuentes ?? new string[0]).ToList();
            string texto = "Crear el grupo de líneas de muestreo '" + grupo + "' en el eje '" + eje + "' con " + rango.Numero + " línea" + (rango.Numero == 1 ? "" : "s")
                + " de " + Num(rango.Inicio) + " a " + Num(rango.Fin) + " cada " + Num(intervalo) + " m (" + Num(anchoIzquierdo) + " m a la izquierda y " + Num(anchoDerecho) + " m a la derecha)"
                + ", muestrear " + (nombres.Count == 0 ? "ninguna fuente" : string.Join(", ", nombres));
            if (!conVistas) return texto + ", sin vistas de sección";
            int columnas = Columnas(rango.Numero, filas);
            return texto + " y crear " + rango.Numero + " vista" + (rango.Numero == 1 ? "" : "s") + " de sección en " + columnas + " columna" + (columnas == 1 ? "" : "s") + " de " + filas + " fila" + (filas == 1 ? "" : "s")
                + (insercion.HasValue ? " desde (" + Num(insercion.Value.X) + ", " + Num(insercion.Value.Y) + ")" : "");
        }

        // ------------------------------------------------------------------ comprobación de lo releído
        /// <summary>Null si las progresivas releídas son las previstas (mismo número y cada una ±0,01, en orden de progresiva); si no, el motivo.</summary>
        public static string MotivoLineasNoCoinciden(IList<double> previstas, IList<double> leidas)
        {
            var p = (previstas ?? new List<double>()).OrderBy(x => x).ToList();
            var l = (leidas ?? new List<double>()).OrderBy(x => x).ToList();
            if (l.Count != p.Count) return "tiene " + l.Count + " líneas de muestreo y se pidieron " + p.Count;
            for (int i = 0; i < p.Count; i++)
            {
                if (!Finito(l[i])) return "la línea " + (i + 1) + " no tiene una progresiva legible";
                if (Math.Abs(l[i] - p[i]) > ToleranciaProgresiva) return "la línea " + (i + 1) + " está en la progresiva " + Num(l[i]) + " y se pidió " + Num(p[i]);
            }
            return null;
        }

        /// <summary>Null si un ancho releído coincide con el pedido (±0,01); si no, el texto del aviso.</summary>
        public static string AvisoAncho(string linea, string lado, double pedido, double? leido)
        {
            if (!leido.HasValue || !Finito(leido.Value)) return null;
            if (Math.Abs(leido.Value - pedido) <= ToleranciaProgresiva) return null;
            return "La línea '" + linea + "' quedó con " + Num(leido.Value) + " m a la " + lado + " (se pidieron " + Num(pedido) + "): revísala en Civil 3D (Propiedades de la línea de muestreo).";
        }

        private static bool Finito(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        private static string Num(double v) => v.ToString("0.###", Inv);
    }
}
