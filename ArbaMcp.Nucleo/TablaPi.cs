using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Una entidad del alineamiento tal como la lee el plugin (listar_pis, 1.15.0), en el orden del eje: tangente
    /// (AlignmentLine), curva circular (AlignmentArc) u otro tipo que aún no se trata (espirales, SCS, STS...). Los
    /// puntos son los de Alignment.PointLocation en las progresivas de inicio y fin, con desplazamiento 0.
    /// </summary>
    public sealed class EntidadEje
    {
        public const string Tangente = "tangente";
        public const string Curva = "curva";

        /// <summary>"tangente", "curva" o, si no es ninguna de las dos, el nombre de la clase de la API (por ejemplo AlignmentSCS).</summary>
        public string Tipo;
        public double? ProgresivaInicio, ProgresivaFin;
        public Punto2? PuntoInicio, PuntoFin;
        /// <summary>Solo curva: AlignmentArc.Radius.</summary>
        public double Radio;
        /// <summary>Solo curva: AlignmentArc.Delta, en radianes (validado en 1.8.0).</summary>
        public double DeflexionRad;
        /// <summary>Solo curva: AlignmentArc.Clockwise (true = giro a la derecha).</summary>
        public bool Horario;
        /// <summary>Solo curva: AlignmentArc.PIStation (PC + T, validado en 1.8.0).</summary>
        public double? ProgresivaPi;
        /// <summary>Solo curva: el PI que da Civil 3D (AlignmentSubEntityArc.PIPoint), o null si no se pudo leer.</summary>
        public Punto2? PiCivil3d;
    }

    /// <summary>Una fila de la tabla de PI. Los campos de la curva van vacíos (null) si el PI no tiene curva.</summary>
    public sealed class FilaPi
    {
        public const string SinCurva = "sin_curva";

        public int Numero;
        /// <summary>"curva", "sin_curva" (dos tangentes seguidas) o el nombre de la clase de un tipo no soportado.</summary>
        public string Tipo;
        public bool TieneCurva;
        public double? Progresiva, X, Y;
        public double? DeflexionGrados;
        /// <summary>"izquierda", "derecha" o "ninguno" (deflexión 0).</summary>
        public string Giro;
        public double? Radio, LongitudCurva, Tangente, ProgresivaPc, ProgresivaPt;
        /// <summary>Distancia entre el PI calculado y el de Civil 3D (PIPoint); null si no se pudo comparar.</summary>
        public double? DiferenciaPiCivil3d;
        public string Aviso;

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["numero"] = Numero,
            ["tipo"] = Tipo,
            ["tiene_curva"] = TieneCurva,
            ["progresiva"] = R(Progresiva),
            ["progresiva_texto"] = Progresiva.HasValue ? TablaPi.Progresiva(Progresiva.Value) : null,
            ["x"] = R(X),
            ["y"] = R(Y),
            ["deflexion_grados"] = R(DeflexionGrados),
            ["deflexion_gms"] = DeflexionGrados.HasValue ? TablaPi.GradosMinutosSegundos(DeflexionGrados.Value) : null,
            ["giro"] = Giro,
            ["radio"] = R(Radio),
            ["longitud_curva"] = R(LongitudCurva),
            ["tangente"] = R(Tangente),
            ["progresiva_pc"] = R(ProgresivaPc),
            ["progresiva_pc_texto"] = ProgresivaPc.HasValue ? TablaPi.Progresiva(ProgresivaPc.Value) : null,
            ["progresiva_pt"] = R(ProgresivaPt),
            ["progresiva_pt_texto"] = ProgresivaPt.HasValue ? TablaPi.Progresiva(ProgresivaPt.Value) : null,
            ["diferencia_pi_civil3d"] = R(DiferenciaPiCivil3d),
            ["aviso"] = Aviso
        };

        private static double? R(double? v) => v.HasValue ? Argumentos.Redondear(v.Value) : null;
    }

    /// <summary>La tabla de PI de un alineamiento: inicio y fin del eje (aparte, no son PI), las filas y los avisos.</summary>
    public sealed class ResultadoTablaPi
    {
        public string Alineamiento;
        public string Unidades;
        public double? Longitud;
        public double ProgresivaInicial, ProgresivaFinal;
        public Punto2? PuntoInicial, PuntoFinal;
        public List<FilaPi> Pis = new List<FilaPi>();
        public List<string> Avisos = new List<string>();
        public int NumeroCurvas => Pis.Count(p => p.TieneCurva);

        public Dictionary<string, object> ComoDiccionario() => new Dictionary<string, object>
        {
            ["alineamiento"] = Alineamiento,
            ["unidades"] = Unidades,
            ["longitud"] = Longitud.HasValue ? Argumentos.Redondear(Longitud.Value) : null,
            ["inicio"] = Extremo(ProgresivaInicial, PuntoInicial),
            ["fin"] = Extremo(ProgresivaFinal, PuntoFinal),
            ["numero_pi"] = Pis.Count,
            ["numero_curvas"] = NumeroCurvas,
            ["pis"] = Pis.Select(p => p.ComoDiccionario()).ToList(),
            ["avisos"] = Avisos
        };

        private static Dictionary<string, object> Extremo(double pk, Punto2? p) => new Dictionary<string, object>
        {
            ["progresiva"] = Argumentos.Redondear(pk),
            ["progresiva_texto"] = TablaPi.Progresiva(pk),
            ["x"] = p.HasValue ? Argumentos.Redondear(p.Value.X) : null,
            ["y"] = p.HasValue ? Argumentos.Redondear(p.Value.Y) : null
        };
    }

    /// <summary>
    /// Tabla de PI de un alineamiento existente (listar_pis, 1.15.0), sin Civil 3D. Recorre las entidades en orden:
    /// - cada curva circular es un PI con curva: progresiva = PIStation (la de Civil 3D, PC + T); coordenadas = PC + T en
    ///   la dirección de la tangente de entrada, que es la cuerda PC→PT girada Δ/2 (a la derecha si la curva gira a la
    ///   izquierda y al revés); radio, longitud (PT − PC), tangente T = R·tan(Δ/2), deflexión Δ y giro;
    /// - dos tangentes seguidas son un PI sin curva: su punto común, la progresiva de ese punto y la deflexión entre las
    ///   dos tangentes;
    /// - cualquier otro tipo (espirales, SCS...) da una fila con su tipo y el aviso "tipo no soportado todavía".
    /// Comprobaciones (avisos si difieren en más de 0,001): PIStation frente a PC + T, longitud frente a R·Δ, cuerda
    /// frente a 2R·sen(Δ/2), PI calculado frente al PIPoint de Civil 3D, y que dos tangentes seguidas se toquen.
    /// </summary>
    public static class TablaPi
    {
        /// <summary>Tolerancia de las comprobaciones (unidades del dibujo), la misma con la que se compara con el informe de Civil 3D.</summary>
        public const double Tolerancia = 0.001;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Progresiva con el formato de Civil 3D: 300 → "0+300.000"; 1234.5 → "1+234.500"; −50 → "-0+050.000".</summary>
        public static string Progresiva(double pk, int decimales = 3)
        {
            double a = Math.Round(Math.Abs(pk), decimales, MidpointRounding.AwayFromZero);
            string signo = pk < 0 && a > 0 ? "-" : "";
            long km = (long)Math.Floor(a / 1000);
            double resto = Math.Round(a - km * 1000.0, decimales, MidpointRounding.AwayFromZero);
            if (resto >= 1000) { km++; resto -= 1000; }
            string formato = decimales > 0 ? "000." + new string('0', decimales) : "000";
            return signo + km.ToString(Inv) + "+" + resto.ToString(formato, Inv);
        }

        /// <summary>Ángulo en grados, minutos y segundos con dos decimales, como el informe de Civil 3D: 90 → 90°00'00.00".</summary>
        public static string GradosMinutosSegundos(double grados)
        {
            long centesimas = (long)Math.Round(Math.Abs(grados) * 360000.0, MidpointRounding.AwayFromZero);
            long g = centesimas / 360000, resto = centesimas % 360000;
            long m = resto / 6000, s = (resto % 6000) / 100, c = resto % 100;
            return (grados < 0 && centesimas > 0 ? "-" : "") + g.ToString(Inv) + "°" + m.ToString("00", Inv) + "'" + s.ToString("00", Inv) + "." + c.ToString("00", Inv) + "\"";
        }

        /// <summary>
        /// PI de una curva circular a partir de PC, PT, radio, deflexión (radianes) y sentido: PC + T·u, con T = R·tan(Δ/2)
        /// y u la dirección de la cuerda PC→PT girada −Δ/2 si la curva gira a la izquierda (antihoraria) o +Δ/2 si gira a
        /// la derecha (horaria). Null si la cuerda es nula o la deflexión es 180° (las tangentes son paralelas).
        /// </summary>
        public static Punto2? PiDeCurva(Punto2 pc, Punto2 pt, double radio, double deflexionRad, bool horario)
        {
            double cx = pt.X - pc.X, cy = pt.Y - pc.Y, cuerda = Math.Sqrt(cx * cx + cy * cy);
            if (cuerda <= 1e-9 || Math.Abs(Math.PI - deflexionRad) < Alineamiento.DeflexionMinima) return null;
            double giro = horario ? deflexionRad / 2 : -deflexionRad / 2;
            double ux = cx / cuerda, uy = cy / cuerda;
            double dx = ux * Math.Cos(giro) - uy * Math.Sin(giro), dy = ux * Math.Sin(giro) + uy * Math.Cos(giro);
            double t = Alineamiento.Tangente(radio, deflexionRad);
            return new Punto2(pc.X + t * dx, pc.Y + t * dy);
        }

        /// <summary>
        /// Calcula la tabla. 'entidades' en el orden del eje; inicio y fin del eje van aparte (no son PI). 'unidades' y
        /// 'longitud' se copian tal cual a la respuesta.
        /// </summary>
        public static ResultadoTablaPi Calcular(string alineamiento, IList<EntidadEje> entidades, double progresivaInicial, double progresivaFinal,
            Punto2? puntoInicial, Punto2? puntoFinal, string unidades = null, double? longitud = null)
        {
            var r = new ResultadoTablaPi
            {
                Alineamiento = alineamiento,
                Unidades = unidades,
                Longitud = longitud,
                ProgresivaInicial = progresivaInicial,
                ProgresivaFinal = progresivaFinal,
                PuntoInicial = puntoInicial,
                PuntoFinal = puntoFinal
            };
            entidades = entidades ?? new List<EntidadEje>();
            if (entidades.Count == 0)
            {
                r.Avisos.Add("El alineamiento '" + alineamiento + "' no tiene entidades (ni tangentes ni curvas): no hay PI que listar.");
                return r;
            }

            for (int i = 0; i < entidades.Count; i++)
            {
                var e = entidades[i];
                FilaPi fila;
                if (e.Tipo == EntidadEje.Curva) fila = FilaDeCurva(e, r.Pis.Count + 1);
                else if (e.Tipo == EntidadEje.Tangente)
                {
                    if (i + 1 >= entidades.Count || entidades[i + 1].Tipo != EntidadEje.Tangente) continue;
                    fila = FilaSinCurva(e, entidades[i + 1], r.Pis.Count + 1);
                }
                else fila = FilaNoSoportada(e, r.Pis.Count + 1);
                r.Pis.Add(fila);
                if (fila.Aviso != null) r.Avisos.Add(fila.Aviso);
            }
            if (r.Pis.Count == 0)
                r.Avisos.Add("El alineamiento '" + alineamiento + "' no tiene PI: es una sola tangente.");
            return r;
        }

        private static FilaPi FilaDeCurva(EntidadEje e, int numero)
        {
            var f = new FilaPi { Numero = numero, Tipo = EntidadEje.Curva, TieneCurva = true, Radio = e.Radio };
            var avisos = new List<string>();
            double delta = e.DeflexionRad;
            f.DeflexionGrados = Alineamiento.Grados(delta);
            f.Giro = delta < Alineamiento.DeflexionMinima ? "ninguno" : e.Horario ? "derecha" : "izquierda";
            f.ProgresivaPc = e.ProgresivaInicio;
            f.ProgresivaPt = e.ProgresivaFin;
            double teorica = Alineamiento.LongitudCurva(e.Radio, delta);
            f.LongitudCurva = e.ProgresivaInicio.HasValue && e.ProgresivaFin.HasValue ? e.ProgresivaFin.Value - e.ProgresivaInicio.Value : teorica;
            if (Math.Abs(f.LongitudCurva.Value - teorica) > Tolerancia)
                avisos.Add("la longitud (PT − PC = " + Num(f.LongitudCurva.Value) + ") no es R·Δ (" + Num(teorica) + ")");

            bool ciento80 = Math.Abs(Math.PI - delta) < Alineamiento.DeflexionMinima;
            if (ciento80) avisos.Add("la deflexión es 180°: las tangentes son paralelas y el PI no está definido");
            else
            {
                f.Tangente = Alineamiento.Tangente(e.Radio, delta);
                if (delta > Math.PI) avisos.Add("la curva pasa de 180°: el PI queda detrás de la curva");
            }

            f.Progresiva = e.ProgresivaPi ?? (e.ProgresivaInicio.HasValue && f.Tangente.HasValue ? e.ProgresivaInicio + f.Tangente : null);
            if (e.ProgresivaPi.HasValue && e.ProgresivaInicio.HasValue && f.Tangente.HasValue
                && Math.Abs(e.ProgresivaPi.Value - (e.ProgresivaInicio.Value + f.Tangente.Value)) > Tolerancia)
                avisos.Add("la progresiva del PI de Civil 3D (" + Num(e.ProgresivaPi.Value) + ") no es PC + T (" + Num(e.ProgresivaInicio.Value + f.Tangente.Value) + ")");

            Punto2? pi = null;
            if (e.PuntoInicio.HasValue && e.PuntoFin.HasValue)
            {
                pi = PiDeCurva(e.PuntoInicio.Value, e.PuntoFin.Value, e.Radio, delta, e.Horario);
                double cuerda = e.PuntoInicio.Value.DistanciaA(e.PuntoFin.Value), cuerdaTeorica = 2 * e.Radio * Math.Sin(delta / 2);
                if (Math.Abs(cuerda - cuerdaTeorica) > Tolerancia)
                    avisos.Add("la distancia PC–PT (" + Num(cuerda) + ") no es la cuerda 2R·sen(Δ/2) (" + Num(cuerdaTeorica) + "); revisa las coordenadas con el informe de Civil 3D");
            }
            else avisos.Add("no se pudieron leer los puntos del PC y del PT");

            if (pi.HasValue && e.PiCivil3d.HasValue)
            {
                f.DiferenciaPiCivil3d = pi.Value.DistanciaA(e.PiCivil3d.Value);
                if (f.DiferenciaPiCivil3d > Tolerancia)
                    avisos.Add("el PI calculado (" + Num(pi.Value.X) + ", " + Num(pi.Value.Y) + ") y el de Civil 3D (" + Num(e.PiCivil3d.Value.X) + ", " + Num(e.PiCivil3d.Value.Y) + ") difieren en " + Num(f.DiferenciaPiCivil3d.Value));
            }
            else if (!pi.HasValue && e.PiCivil3d.HasValue)
            {
                pi = e.PiCivil3d;
                avisos.Add("las coordenadas son las del PI de Civil 3D (PIPoint): no se pudieron calcular");
            }
            if (pi.HasValue) { f.X = pi.Value.X; f.Y = pi.Value.Y; }

            if (avisos.Count > 0) f.Aviso = "PI " + numero + ": " + string.Join("; ", avisos) + ".";
            return f;
        }

        private static FilaPi FilaSinCurva(EntidadEje a, EntidadEje b, int numero)
        {
            var f = new FilaPi { Numero = numero, Tipo = FilaPi.SinCurva, TieneCurva = false };
            var avisos = new List<string>();
            f.Progresiva = a.ProgresivaFin ?? b.ProgresivaInicio;
            if (a.ProgresivaFin.HasValue && b.ProgresivaInicio.HasValue && Math.Abs(a.ProgresivaFin.Value - b.ProgresivaInicio.Value) > Tolerancia)
                avisos.Add("la tangente anterior acaba en " + Num(a.ProgresivaFin.Value) + " y la siguiente empieza en " + Num(b.ProgresivaInicio.Value));

            Punto2? p = a.PuntoFin ?? b.PuntoInicio;
            if (a.PuntoFin.HasValue && b.PuntoInicio.HasValue && a.PuntoFin.Value.DistanciaA(b.PuntoInicio.Value) > Tolerancia)
                avisos.Add("las dos tangentes no se tocan (" + Num(a.PuntoFin.Value.DistanciaA(b.PuntoInicio.Value)) + " entre el final de una y el inicio de la otra); se usa el final de la primera");
            if (p.HasValue) { f.X = p.Value.X; f.Y = p.Value.Y; }

            if (a.PuntoInicio.HasValue && p.HasValue && b.PuntoFin.HasValue
                && a.PuntoInicio.Value.DistanciaA(p.Value) > 1e-9 && p.Value.DistanciaA(b.PuntoFin.Value) > 1e-9)
            {
                double delta = Alineamiento.Deflexion(a.PuntoInicio.Value, p.Value, b.PuntoFin.Value, out int giro);
                f.DeflexionGrados = Alineamiento.Grados(delta);
                f.Giro = giro == 0 ? "ninguno" : giro > 0 ? "derecha" : "izquierda";
            }
            else avisos.Add("no se pudo calcular la deflexión (faltan los puntos de las tangentes)");

            if (avisos.Count > 0) f.Aviso = "PI " + numero + " (sin curva): " + string.Join("; ", avisos) + ".";
            return f;
        }

        private static FilaPi FilaNoSoportada(EntidadEje e, int numero)
        {
            string rango = e.ProgresivaInicio.HasValue && e.ProgresivaFin.HasValue
                ? " de " + Progresiva(e.ProgresivaInicio.Value) + " a " + Progresiva(e.ProgresivaFin.Value)
                : "";
            return new FilaPi
            {
                Numero = numero,
                Tipo = string.IsNullOrWhiteSpace(e.Tipo) ? "desconocido" : e.Tipo,
                TieneCurva = false,
                Aviso = "PI " + numero + ": entidad " + (string.IsNullOrWhiteSpace(e.Tipo) ? "desconocida" : e.Tipo) + rango + ": tipo no soportado todavía (solo tangentes y curvas circulares); compara este tramo con el informe de alineamiento de Civil 3D."
            };
        }

        private static string Num(double v) => v.ToString("0.###", Inv);
    }
}
