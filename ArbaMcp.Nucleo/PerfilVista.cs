using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace ArbaMcp.Nucleo
{
    /// <summary>
    /// Rectángulo que ocupa algo en planta (x = este, y = norte). Se usa para colocar la vista de perfil (y las de
    /// sección) a la derecha del eje: desde 1.14.1, la extensión del alineamiento y de sus vistas (hasta la 1.14.0, la
    /// del alineamiento, la de la superficie y la del dibujo entero).
    /// </summary>
    public struct Extension2
    {
        public double MinX, MinY, MaxX, MaxY;
        public Extension2(double minX, double minY, double maxX, double maxY) { MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY; }
        public double Ancho => MaxX - MinX;
        public double Alto => MaxY - MinY;
        /// <summary>Una extensión sirve si sus números son finitos, razonables (menos de 1e15, el "vacío" de AutoCAD es 1e20) y el mínimo no supera al máximo.</summary>
        public bool EsValida =>
            Finito(MinX) && Finito(MinY) && Finito(MaxX) && Finito(MaxY) && MinX <= MaxX && MinY <= MaxY;
        private static bool Finito(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && Math.Abs(v) < 1e15;
        /// <summary>La menor extensión que contiene a las dos (las no válidas se ignoran).</summary>
        public static Extension2? Unir(Extension2? a, Extension2? b)
        {
            bool va = a.HasValue && a.Value.EsValida, vb = b.HasValue && b.Value.EsValida;
            if (!va && !vb) return null;
            if (!va) return b;
            if (!vb) return a;
            return new Extension2(Math.Min(a.Value.MinX, b.Value.MinX), Math.Min(a.Value.MinY, b.Value.MinY), Math.Max(a.Value.MaxX, b.Value.MaxX), Math.Max(a.Value.MaxY, b.Value.MaxY));
        }
    }

    /// <summary>
    /// Lógica de crear_perfil_terreno_y_vista que no depende de Civil 3D: nombres por defecto del perfil y de la vista,
    /// lectura del punto de inserción pedido, regla del punto de inserción automático (a la derecha del eje y de sus
    /// vistas desde 1.14.1),
    /// comprobación de los rangos leídos tras crear y armado de las respuestas 'perfil' y 'vista'. Se prueba en
    /// ArbaMcp.Pruebas con datos sintéticos.
    /// </summary>
    public static class PerfilVista
    {
        /// <summary>Margen a la derecha de la extensión de referencia: el 10 % de su ancho, nunca menos de MargenMinimo.</summary>
        public const double FraccionMargen = 0.10;
        /// <summary>Margen mínimo (unidades del dibujo) cuando lo dibujado es muy estrecho o no tiene ancho.</summary>
        public const double MargenMinimo = 50.0;

        // ------------------------------------------------------------------ nombres por defecto
        /// <summary>Nombre del perfil de terreno: el pedido o "&lt;superficie&gt; - Terreno".</summary>
        public static string NombrePerfil(string pedido, string superficie)
            => string.IsNullOrWhiteSpace(pedido) ? superficie.Trim() + " - Terreno" : pedido.Trim();

        /// <summary>Nombre de la vista de perfil: el pedido o "&lt;alineamiento&gt; - Vista".</summary>
        public static string NombreVista(string pedido, string alineamiento)
            => string.IsNullOrWhiteSpace(pedido) ? alineamiento.Trim() + " - Vista" : pedido.Trim();

        // ------------------------------------------------------------------ punto de inserción
        /// <summary>
        /// Lee 'punto_insercion' ({"x": .., "y": ..} o [x, y]); null si no se pasó. Error si viene incompleto o con
        /// algo que no es un número.
        /// </summary>
        public static Punto2? LeerPuntoInsercion(JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Undefined || json.ValueKind == JsonValueKind.Null) return null;
            double? x = null, y = null;
            if (json.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in json.EnumerateObject())
                {
                    string n = p.Name.Trim().ToLowerInvariant();
                    if (n == "x") x = Numero(p.Value, "x");
                    else if (n == "y") y = Numero(p.Value, "y");
                }
            }
            else if (json.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var v in json.EnumerateArray())
                {
                    if (i == 0) x = Numero(v, "x");
                    else if (i == 1) y = Numero(v, "y");
                    i++;
                }
            }
            else throw new ArgumentException(MensajePuntoNoValido("no es un objeto ni una lista"));
            if (!x.HasValue || !y.HasValue) throw new ArgumentException(MensajePuntoNoValido("faltan 'x' o 'y'"));
            return new Punto2(x.Value, y.Value);
        }

        private static double Numero(JsonElement v, string campo)
        {
            if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
            throw new ArgumentException(MensajePuntoNoValido("'" + campo + "' no es un número"));
        }

        private static string MensajePuntoNoValido(string motivo)
            => "El parámetro 'punto_insercion' no es válido (" + motivo + "). Debe ser {\"x\": 1500, \"y\": 2000} con las coordenadas de la esquina inferior izquierda de la vista, o no pasarlo para colocarla automáticamente a la derecha del eje.";

        /// <summary>
        /// Punto de inserción automático: a la derecha de la extensión de referencia. x = el extremo máximo en X de la
        /// extensión más un margen (el 10 % de su ancho, mínimo MargenMinimo); y = el extremo mínimo en Y. Así la esquina
        /// inferior izquierda de la vista queda fuera de lo que ya hay, alineada por abajo. La extensión es la unión de
        /// las que se pasan (desde 1.14.1, la que da ReferenciaDelEje); las no válidas se ignoran. Si ninguna sirve, error.
        /// </summary>
        public static Punto2 PuntoAutomatico(params Extension2?[] extensiones)
        {
            Extension2? total = null;
            foreach (var e in extensiones) total = Extension2.Unir(total, e);
            if (!total.HasValue)
                throw new InvalidOperationException("No se pudo calcular el punto de inserción automático: ni el alineamiento, ni sus vistas, ni el dibujo tienen una extensión legible. Pasa 'punto_insercion' con {\"x\": .., \"y\": ..}.");
            return new Punto2(total.Value.MaxX + Margen(total.Value.Ancho), total.Value.MinY);
        }

        /// <summary>El 10 % del ancho, nunca menos de MargenMinimo.</summary>
        public static double Margen(double ancho) => Math.Max(MargenMinimo, FraccionMargen * Math.Max(0, ancho));

        /// <summary>Texto de la regla aplicada, para 'avisos' y la memoria técnica. 'origen' dice de dónde sale el extremo derecho (ReferenciaPunto.Origen).</summary>
        public static string ExplicacionPuntoAutomatico(Extension2 total, Punto2 punto, string origen = "extremo derecho de lo dibujado")
            => "Punto de inserción automático: x = " + F(total.MaxX) + " (" + origen + ") + " + F(Margen(total.Ancho)) + " de margen (10 % del ancho " + F(total.Ancho) + ", mínimo " + F(MargenMinimo) + ") = " + F(punto.X) + "; y = " + F(total.MinY) + " (extremo inferior). Pasa 'punto_insercion' para elegir otro.";

        /// <summary>Extensión de referencia del punto automático y de dónde sale (para la explicación de 'avisos').</summary>
        public sealed class ReferenciaPunto
        {
            /// <summary>La unión que se pasa a PuntoAutomatico; null si nada es legible.</summary>
            public Extension2? Extension;
            /// <summary>Texto para ExplicacionPuntoAutomatico ("extremo derecho del eje 'Eje 1' y de su vista de perfil").</summary>
            public string Origen;
            /// <summary>Vistas de perfil y de sección del eje con extensión legible que entraron en la unión.</summary>
            public int VistasPerfil, VistasSeccion;
            /// <summary>True si se tuvo que usar la extensión del dibujo (ni el eje ni sus vistas eran legibles).</summary>
            public bool DelDibujo;
        }

        /// <summary>
        /// Extensión de referencia del punto automático desde 1.14.1: la del eje unida a la de lo que ya cuelga de él
        /// (sus vistas de perfil y, en crear_secciones, también las vistas de sección de sus grupos de muestreo), sin la
        /// superficie ni el resto del dibujo. Hasta la 1.14.0 se usaba la extensión del dibujo entero y, con contenido
        /// lejano, la vista quedaba muy a la derecha (informe 1.9.0, paso 2: x ≈ 6 × 10^5 con un eje en x ≈ 1000..1600).
        /// Las vistas existentes entran para que una vista nueva no caiga encima de otra del mismo eje (y las de sección,
        /// a la derecha de la de perfil). Solo si ni el eje ni sus vistas son legibles se usa la del dibujo, con aviso; si
        /// tampoco esa sirve, Extension es null y PuntoAutomatico da el error de siempre.
        /// </summary>
        public static ReferenciaPunto ReferenciaDelEje(string nombreEje, Extension2? eje, IEnumerable<Extension2?> vistasPerfil, IEnumerable<Extension2?> vistasSeccion, Extension2? dibujo, List<string> avisos)
        {
            var r = new ReferenciaPunto();
            bool ejeValido = eje.HasValue && eje.Value.EsValida;
            Extension2? total = ejeValido ? eje : null;
            foreach (var v in vistasPerfil ?? new List<Extension2?>())
                if (v.HasValue && v.Value.EsValida) { total = Extension2.Unir(total, v); r.VistasPerfil++; }
            foreach (var v in vistasSeccion ?? new List<Extension2?>())
                if (v.HasValue && v.Value.EsValida) { total = Extension2.Unir(total, v); r.VistasSeccion++; }

            string eje_ = "'" + (nombreEje ?? "").Trim() + "'";
            if (total.HasValue)
            {
                var partes = new List<string>();
                if (r.VistasPerfil > 0) partes.Add(r.VistasPerfil == 1 ? "su vista de perfil" : "sus " + r.VistasPerfil + " vistas de perfil");
                if (r.VistasSeccion > 0) partes.Add(r.VistasSeccion == 1 ? "su vista de sección" : "sus " + r.VistasSeccion + " vistas de sección");
                string vistas = partes.Count == 0 ? "" : " y de " + string.Join(" y ", partes);
                r.Origen = ejeValido
                    ? "extremo derecho del eje " + eje_ + vistas
                    : "extremo derecho de las vistas del eje " + eje_ + " (" + string.Join(" y ", partes) + "; la extensión del eje no se pudo leer)";
                r.Extension = total;
                return r;
            }

            if (dibujo.HasValue && dibujo.Value.EsValida)
            {
                r.Extension = dibujo;
                r.DelDibujo = true;
                r.Origen = "extremo derecho del dibujo entero";
                avisos?.Add("Ni el eje " + eje_ + " ni sus vistas tienen una extensión legible: el punto de inserción automático se calcula con la extensión del dibujo entero (si hay objetos lejos, la vista quedará lejos). Pasa 'punto_insercion' para elegir otro.");
            }
            return r;
        }

        private static string F(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ comprobaciones tras crear
        /// <summary>
        /// Comprueba el perfil releído: progresiva final mayor que la inicial, cotas finitas y cota máxima no menor que la
        /// mínima. Devuelve el motivo del fallo o null si todo cumple.
        /// </summary>
        public static string MotivoPerfilNoValido(double progresivaInicial, double progresivaFinal, double cotaMinima, double cotaMaxima)
        {
            if (!Fin(progresivaInicial) || !Fin(progresivaFinal)) return "sus progresivas no son números válidos";
            if (progresivaFinal <= progresivaInicial) return "su progresiva final (" + F(progresivaFinal) + ") no es mayor que la inicial (" + F(progresivaInicial) + ")";
            if (!Fin(cotaMinima) || !Fin(cotaMaxima)) return "sus cotas no son números válidos (la superficie puede no cubrir el eje)";
            if (cotaMaxima < cotaMinima) return "su cota máxima (" + F(cotaMaxima) + ") es menor que la mínima (" + F(cotaMinima) + ")";
            return null;
        }

        /// <summary>Comprueba la vista releída: existe un nombre y la progresiva final supera a la inicial. Devuelve el motivo o null.</summary>
        public static string MotivoVistaNoValida(string nombre, double progresivaInicial, double progresivaFinal)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "no tiene nombre";
            if (!Fin(progresivaInicial) || !Fin(progresivaFinal)) return "sus progresivas no son números válidos";
            if (progresivaFinal <= progresivaInicial) return "su progresiva final (" + F(progresivaFinal) + ") no es mayor que la inicial (" + F(progresivaInicial) + ")";
            return null;
        }

        private static bool Fin(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        // ------------------------------------------------------------------ respuestas
        /// <summary>Bloque 'perfil' de la respuesta. Las cotas van como null cuando aún no se calculan (simulación).</summary>
        public static Dictionary<string, object> RespuestaPerfil(string nombre, string tipo, double progresivaInicial, double progresivaFinal, double? cotaMinima, double? cotaMaxima, string estilo, string conjuntoEtiquetas)
            => new Dictionary<string, object>
            {
                ["nombre"] = nombre,
                ["tipo"] = tipo,
                ["progresiva_inicial"] = Argumentos.Redondear(progresivaInicial),
                ["progresiva_final"] = Argumentos.Redondear(progresivaFinal),
                ["cota_minima"] = cotaMinima.HasValue ? Argumentos.Redondear(cotaMinima.Value) : null,
                ["cota_maxima"] = cotaMaxima.HasValue ? Argumentos.Redondear(cotaMaxima.Value) : null,
                ["estilo"] = estilo,
                ["conjunto_etiquetas"] = conjuntoEtiquetas ?? "(sin etiquetas)"
            };

        /// <summary>Bloque 'vista' de la respuesta.</summary>
        public static Dictionary<string, object> RespuestaVista(string nombre, Punto2 punto, string estilo, string conjuntoBandas, double progresivaInicial, double progresivaFinal, int? bandas)
            => new Dictionary<string, object>
            {
                ["nombre"] = nombre,
                ["punto_insercion"] = new Dictionary<string, object> { ["x"] = Argumentos.Redondear(punto.X), ["y"] = Argumentos.Redondear(punto.Y) },
                ["estilo"] = estilo,
                ["conjunto_bandas"] = conjuntoBandas ?? "(sin bandas)",
                ["progresiva_inicial"] = Argumentos.Redondear(progresivaInicial),
                ["progresiva_final"] = Argumentos.Redondear(progresivaFinal),
                ["bandas"] = bandas
            };
    }
}
