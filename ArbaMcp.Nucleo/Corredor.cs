using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Rango de un corredor nuevo: el pedido (o el común del eje y la rasante si no se pide) y el común, con los avisos
    /// de los ajustes al extremo.
    /// </summary>
    public sealed class RangoCorredor
    {
        public double Inicio, Fin;
        /// <summary>Rango común del eje y de la rasante (lo máximo que puede abarcar el corredor).</summary>
        public double InicioComun, FinComun;
        /// <summary>True si 'inicio' o 'fin' vinieron en los argumentos (false si se usó el rango común).</summary>
        public bool InicioPedido, FinPedido;
        public List<string> Avisos = new List<string>();
        public double Longitud => Fin - Inicio;

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["inicio"] = Argumentos.Redondear(Inicio),
            ["fin"] = Argumentos.Redondear(Fin),
            ["longitud"] = Argumentos.Redondear(Longitud),
            ["inicio_comun"] = Argumentos.Redondear(InicioComun),
            ["fin_comun"] = Argumentos.Redondear(FinComun)
        };
    }

    /// <summary>
    /// Lógica de crear_corredor (1.11.0) que no depende de Civil 3D: nombres por defecto de la línea base y de la región,
    /// rango común del eje y la rasante con el ajuste de 'inicio' y 'fin', validación de la frecuencia y las cuatro
    /// frecuencias que se aplican, elección de la superficie objetivo cuando no se pide, texto de la acción y
    /// comprobación de lo releído del dibujo (rango y objetivos de superficie). Se prueba en ArbaMcp.Pruebas con datos
    /// sintéticos.
    /// </summary>
    public static class Corredor
    {
        /// <summary>Un 'inicio' o 'fin' que se sale del rango común menos que esto (metros) se ajusta al extremo con aviso, en vez de dar error.</summary>
        public const double ToleranciaExtremo = 0.01;
        /// <summary>Margen al comparar progresivas releídas con las pedidas (metros).</summary>
        public const double ToleranciaRango = 0.01;
        public const string LineaBasePorDefecto = "Línea base 1";
        public const string RegionPorDefecto = "Región 1";

        /// <summary>Claves de las cuatro frecuencias, las mismas que usa establecer_frecuencia (EstadoFrecuencias/AplicarFrecuencias).</summary>
        public static readonly string[] ClavesFrecuencia = { "frecuencia_tangentes", "frecuencia_curvas", "frecuencia_espirales", "frecuencia_perfil" };

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ------------------------------------------------------------------ nombres
        /// <summary>Nombre de la línea base: el pedido o "Línea base 1".</summary>
        public static string NombreLineaBase(string pedido) => string.IsNullOrWhiteSpace(pedido) ? LineaBasePorDefecto : pedido.Trim();

        /// <summary>Nombre de la región: el pedido o "Región 1".</summary>
        public static string NombreRegion(string pedido) => string.IsNullOrWhiteSpace(pedido) ? RegionPorDefecto : pedido.Trim();

        // ------------------------------------------------------------------ rango
        /// <summary>
        /// Rango del corredor. El común es la intersección del eje y de la rasante (la rasante puede ser más corta que el
        /// eje); sin 'inicio' ni 'fin' se usa entero. Un 'inicio' o 'fin' que se sale del común menos de 0,01 se ajusta
        /// al extremo con aviso; más lejos, error que dice el rango común y las herramientas de lectura. Error también si
        /// el eje y la rasante no se solapan o si el inicio no es menor que el fin.
        /// </summary>
        public static RangoCorredor Rango(double inicioEje, double finEje, double inicioPerfil, double finPerfil,
            double? inicioPedido, double? finPedido, string eje, string perfil)
        {
            if (!Finito(inicioEje) || !Finito(finEje) || !Finito(inicioPerfil) || !Finito(finPerfil))
                throw new ArgumentException("No se pudieron leer las progresivas del eje '" + eje + "' o de la rasante '" + perfil + "'. Usa listar_alineamientos y listar_perfiles.");
            if (inicioPedido.HasValue && !Finito(inicioPedido.Value)) throw new ArgumentException("El parámetro 'inicio' no es un número válido.");
            if (finPedido.HasValue && !Finito(finPedido.Value)) throw new ArgumentException("El parámetro 'fin' no es un número válido.");

            var r = new RangoCorredor
            {
                InicioComun = Math.Max(inicioEje, inicioPerfil),
                FinComun = Math.Min(finEje, finPerfil),
                InicioPedido = inicioPedido.HasValue,
                FinPedido = finPedido.HasValue
            };
            string textoComun = "del eje '" + eje + "' (" + Num(inicioEje) + " a " + Num(finEje) + ") y la rasante '" + perfil + "' (" + Num(inicioPerfil) + " a " + Num(finPerfil) + ")";
            if (r.FinComun - r.InicioComun <= 1e-6)
                throw new ArgumentException("El eje y la rasante no tienen un tramo común: rango " + textoComun + ". Usa listar_alineamientos y listar_perfiles, o crea la rasante con crear_rasante a lo largo del eje.");

            r.Inicio = inicioPedido ?? r.InicioComun;
            r.Fin = finPedido ?? r.FinComun;
            r.Inicio = Ajustar(r.Inicio, r, "inicio", textoComun);
            r.Fin = Ajustar(r.Fin, r, "fin", textoComun);
            if (r.Fin - r.Inicio <= 1e-6)
                throw new ArgumentException("El 'fin' (" + Num(r.Fin) + ") debe ser mayor que el 'inicio' (" + Num(r.Inicio) + ").");
            if (!inicioPedido.HasValue && !finPedido.HasValue && (inicioPerfil > inicioEje + 1e-6 || finPerfil < finEje - 1e-6))
                r.Avisos.Add("La rasante '" + perfil + "' no cubre todo el eje: el corredor va de " + Num(r.Inicio) + " a " + Num(r.Fin) + ", el tramo común " + textoComun + ".");
            return r;
        }

        private static double Ajustar(double v, RangoCorredor r, string nombre, string textoComun)
        {
            if (v < r.InicioComun)
            {
                if (r.InicioComun - v > ToleranciaExtremo)
                    throw new ArgumentException("El '" + nombre + "' (" + Num(v) + ") queda fuera del tramo común " + textoComun + ": debe estar entre " + Num(r.InicioComun) + " y " + Num(r.FinComun) + ". Usa listar_alineamientos y listar_perfiles para ver los rangos.");
                r.Avisos.Add("El '" + nombre + "' " + Num(v) + " se ajusta al inicio del tramo común (" + Num(r.InicioComun) + ").");
                return r.InicioComun;
            }
            if (v > r.FinComun)
            {
                if (v - r.FinComun > ToleranciaExtremo)
                    throw new ArgumentException("El '" + nombre + "' (" + Num(v) + ") queda fuera del tramo común " + textoComun + ": debe estar entre " + Num(r.InicioComun) + " y " + Num(r.FinComun) + ". Usa listar_alineamientos y listar_perfiles para ver los rangos.");
                r.Avisos.Add("El '" + nombre + "' " + Num(v) + " se ajusta al final del tramo común (" + Num(r.FinComun) + ").");
                return r.FinComun;
            }
            return v;
        }

        // ------------------------------------------------------------------ frecuencia
        /// <summary>La frecuencia (m) es obligatoria, finita y mayor que 0. Devuelve su valor.</summary>
        public static double ValidarFrecuencia(double? frecuencia)
        {
            if (!frecuencia.HasValue) throw new ArgumentException("Falta el parámetro obligatorio 'frecuencia' (metros entre secciones del corredor, por ejemplo 10).");
            if (!Finito(frecuencia.Value) || frecuencia.Value <= 0)
                throw new ArgumentException("La 'frecuencia' debe ser un número mayor que 0 (metros entre secciones del corredor); llegó " + Num(frecuencia.Value) + ".");
            return frecuencia.Value;
        }

        /// <summary>Las cuatro frecuencias de la región (tangentes, curvas, espirales y curvas del perfil) con el mismo valor.</summary>
        public static Dictionary<string, double> Frecuencias(double frecuencia) => ClavesFrecuencia.ToDictionary(k => k, k => frecuencia);

        /// <summary>Aviso si la frecuencia es mayor que la longitud del corredor (solo habría secciones en los extremos y en los puntos geométricos); null si no.</summary>
        public static string AvisoFrecuencia(double frecuencia, double longitud)
            => frecuencia > longitud + 1e-6
                ? "La frecuencia " + Num(frecuencia) + " m es mayor que la longitud del corredor (" + Num(longitud) + " m): solo habrá secciones en los extremos y en los puntos geométricos."
                : null;

        // ------------------------------------------------------------------ superficie objetivo
        /// <summary>True si se pidió expresamente no asignar objetivos ("ninguna", "ninguno" o "none").</summary>
        public static bool EsSinObjetivo(string pedida)
        {
            string p = (pedida ?? "").Trim();
            return string.Equals(p, "ninguna", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p, "ninguno", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p, "none", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Superficie que se pondrá en los objetivos de superficie (taludes). Pedida: se devuelve tal cual (el plugin la
        /// busca en el dibujo y da el error "Usa listar_superficies" si no está). "ninguna": null, sin objetivos. Sin
        /// pedir: si el dibujo tiene una sola superficie que no sea de corredor, esa (con aviso); si no tiene ninguna,
        /// null (el corredor se crea sin objetivos, con aviso); si tiene varias, error con la lista.
        /// </summary>
        public static string ElegirSuperficie(string pedida, IList<string> disponibles, List<string> avisos)
        {
            if (EsSinObjetivo(pedida))
            {
                avisos?.Add("Se pidió 'superficie_objetivo': \"" + pedida.Trim() + "\": el corredor se crea sin objetivos de superficie; los taludes no se dibujarán hasta que tengan uno (asignar_objetivos_superficie).");
                return null;
            }
            if (!string.IsNullOrWhiteSpace(pedida)) return pedida.Trim();
            var lista = (disponibles ?? new List<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (lista.Count == 1)
            {
                avisos?.Add("No se indicó 'superficie_objetivo': se usa '" + lista[0] + "', la única superficie del dibujo que no es de un corredor.");
                return lista[0];
            }
            if (lista.Count == 0)
            {
                avisos?.Add("El dibujo no tiene ninguna superficie de terreno: el corredor se crea sin objetivos de superficie; los taludes no se dibujarán hasta que tengan uno (crea el terreno con crear_superficie_tin y asígnalo con asignar_objetivos_superficie).");
                return null;
            }
            throw new ArgumentException("Indica 'superficie_objetivo': el dibujo tiene " + lista.Count + " superficies (" + string.Join(", ", lista) + ") y no se sabe cuál es el terreno. Usa listar_superficies, o pasa \"ninguna\" para crear el corredor sin objetivos.");
        }

        // ------------------------------------------------------------------ textos
        /// <summary>Texto de la acción para la respuesta ("Crear el corredor 'Corredor 1' sobre el eje ... de 0 a 831.327 cada 10 m ...").</summary>
        public static string Accion(string nombre, string eje, string perfil, string ensamblaje, RangoCorredor rango, double frecuencia, string superficie)
            => "Crear el corredor '" + nombre + "' sobre el eje '" + eje + "' y la rasante '" + perfil + "' con el ensamblaje '" + ensamblaje + "', de "
               + Num(rango.Inicio) + " a " + Num(rango.Fin) + " cada " + Num(frecuencia) + " m, "
               + (superficie == null ? "sin objetivos de superficie" : "con '" + superficie + "' como objetivo de los taludes")
               + ", y reconstruirlo";

        // ------------------------------------------------------------------ capa (1.14.1)
        /// <summary>Lo que se añade a los avisos de capa: el corredor no cambia de capa y se puede cambiar a mano.</summary>
        public const string ColaAvisoCapa = "el corredor se queda en la capa que le pone Civil 3D (en la 1.11.0 fue la capa 0). Cámbiala después en Propiedades del corredor si hace falta.";

        /// <summary>
        /// Aviso cuando la capa de corredores de la configuración del dibujo (Configuración del dibujo → Capas de objeto
        /// → Corredor) no sirve: con 'motivo' (el error de la lectura) no se pudo leer; sin él, la capa configurada no
        /// existe en el dibujo. En los dos casos el corredor no se mueve de capa (a diferencia del eje y el perfil, que
        /// van a la capa actual).
        /// </summary>
        public static string AvisoCapaNoEncontrada(string nombreConfigurado, string motivo = null)
        {
            if (!string.IsNullOrWhiteSpace(motivo))
                return "No se pudo leer la capa de corredores de la configuración del dibujo (" + motivo.Trim() + "); " + ColaAvisoCapa;
            return "La capa de corredores de la configuración del dibujo (" + (string.IsNullOrWhiteSpace(nombreConfigurado) ? "sin nombre" : "'" + nombreConfigurado.Trim() + "'")
                   + ") no existe en el dibujo; " + ColaAvisoCapa;
        }

        /// <summary>Aviso cuando la API no deja poner la capa al corredor recién creado (Entity.LayerId lanzó): el corredor se crea igual.</summary>
        public static string AvisoCapaNoAplicada(string capa, string error)
            => "No se pudo poner el corredor en la capa '" + capa + "' de la configuración del dibujo (Entity.LayerId: " + (error ?? "?").Trim() + "); " + ColaAvisoCapa;

        /// <summary>
        /// Comprobación de la capa releída tras crear: null si no había capa prevista (no se tocó) o si la releída es la
        /// prevista (sin distinguir mayúsculas); si no, el aviso. No es un error: el corredor está bien creado y la capa
        /// se puede cambiar a mano.
        /// </summary>
        public static string AvisoCapa(string prevista, string leida)
        {
            if (string.IsNullOrWhiteSpace(prevista)) return null;
            if (!string.IsNullOrWhiteSpace(leida) && string.Equals(prevista.Trim(), leida.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
            return "El corredor quedó en la capa " + (string.IsNullOrWhiteSpace(leida) ? "(no legible)" : "'" + leida.Trim() + "'") + " y se pidió '" + prevista.Trim()
                   + "', la de corredores de la configuración del dibujo. Cámbiala en Propiedades del corredor si hace falta (anótalo para la memoria técnica).";
        }

        // ------------------------------------------------------------------ comprobación de lo releído
        /// <summary>Null si la región releída empieza y termina donde se pidió (±0,01); si no, el motivo.</summary>
        public static string MotivoRangoNoCoincide(double inicioPedido, double finPedido, double inicioLeido, double finLeido)
        {
            if (!Finito(inicioLeido) || !Finito(finLeido)) return "las progresivas de la región no son números válidos";
            if (Math.Abs(inicioLeido - inicioPedido) > ToleranciaRango) return "la región empieza en " + Num(inicioLeido) + " y se pidió " + Num(inicioPedido);
            if (Math.Abs(finLeido - finPedido) > ToleranciaRango) return "la región termina en " + Num(finLeido) + " y se pidió " + Num(finPedido);
            return null;
        }

        /// <summary>
        /// Null si todos los objetivos de superficie releídos tienen exactamente la superficie pedida (o, sin superficie,
        /// ninguno); si no, el motivo con el primero que no cuadra. Cada elemento: dónde está el objetivo (subensamblaje y
        /// parámetro) y los nombres de los objetos asignados.
        /// </summary>
        public static string MotivoObjetivosNoCoinciden(IList<(string donde, IList<string> objetivos)> leidos, string superficie)
        {
            if (leidos == null) return null;
            foreach (var (donde, objetivos) in leidos)
            {
                var o = (objetivos ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                if (superficie == null)
                {
                    if (o.Count > 0) return "el objetivo de " + donde + " tiene " + string.Join(", ", o) + " y no se pidió ninguno";
                    continue;
                }
                if (o.Count != 1 || !string.Equals(o[0].Trim(), superficie.Trim(), StringComparison.OrdinalIgnoreCase))
                    return "el objetivo de " + donde + " tiene " + (o.Count == 0 ? "ninguno" : string.Join(", ", o)) + " y se pidió '" + superficie + "'";
            }
            return null;
        }

        private static bool Finito(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        private static string Num(double v) => v.ToString("0.###", Inv);
    }
}
