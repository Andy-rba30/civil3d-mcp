using System;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;

namespace ArbaMcp
{
    /// <summary>
    /// Bordes de calzada (1.21.0). crear_bordes_calzada crea, en una sola entrada de Deshacer y por el camino común
    /// Escritura.CrearObjeto, los dos alineamientos desplazados de un eje a media calzada
    /// (Alignment.CreateOffsetAlignment) y, si se piden, sus ensanchamientos (OffsetAlignmentInfo.AddWidening).
    /// listar_bordes_calzada lee los bordes de un eje. Los dos MIDEN cada borde sobre el dibujo (desplazamiento con
    /// signo en cada progresiva del eje, con Alignment.PointLocation y Alignment.StationOffset, miembros ya ejecutados)
    /// en vez de fiarse de las regiones de la API: cómo modela AddWidening la transición no se sabe todavía
    /// (VALIDACION_121, paso decisivo), y las regiones (OffsetAlignmentInfo.Regions) solo se copian en la respuesta
    /// para compararlas. Los enumerados (Side, RegionType, TransitionType) se devuelven con ToString(): el catálogo no
    /// trae sus valores. Ningún tipo nuevo de la API se nombra en el código (se usa var), así que un espacio de nombres
    /// distinto no rompe la compilación. La lógica sin Civil 3D está en ArbaMcp.Nucleo.BordesCalzada.
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaCrearBordes = "crear_bordes_calzada";
        private const string HerramientaListarBordes = "listar_bordes_calzada";

        /// <summary>Nombres de todos los alineamientos del dibujo, para los errores de nombre repetido.</summary>
        private static List<string> NombresAlineamientos(Transaction tr)
        {
            var nombres = new List<string>();
            foreach (ObjectId id in CivilApplication.ActiveDocument.GetAlignmentIds())
                if (tr.GetObject(id, OpenMode.ForRead) is CivAlignment al) nombres.Add(al.Name);
            return nombres;
        }

        /// <summary>
        /// Error de una llamada a Civil 3D en crear_bordes_calzada (1.21.3): el texto dice qué miembro falló y con qué
        /// valores, más el texto original de Civil 3D; se lanza SIN InnerException porque el servidor y el log informan la
        /// excepción más interna (Raiz) y el contexto se perdía. El texto original y su traza van al historial.
        /// </summary>
        private static InvalidOperationException ErrorDeCivil3d(Exception ex, string que)
        {
            var raiz = Raiz(ex);
            Historial.Registrar(HerramientaCrearBordes + ": " + que + " → " + raiz.GetType().Name + ": " + raiz.Message + " " + raiz.StackTrace);
            return new InvalidOperationException(que + ". Civil 3D respondió: " + raiz.GetType().Name + ": " + raiz.Message.Replace("\r", " ").Replace("\n", " ") + ". Nada queda escrito (la transacción se descarta) y no se reintenta. El detalle está en leer_historial.");
        }

        /// <summary>Nombre de un alineamiento por id o null.</summary>
        private static string NombreAlineamiento(Transaction tr, ObjectId id)
            => id.IsNull ? null : Seguro(() => (tr.GetObject(id, OpenMode.ForRead) as CivAlignment)?.Name);

        /// <summary>
        /// Desplazamiento con signo (negativo a la izquierda, como punto_a_pk) del borde en la progresiva 's' del eje:
        /// se parte del punto del eje a la distancia 'inicial' sobre su normal (Alignment.PointLocation con desplazamiento)
        /// y se corrige con la distancia de ese punto al borde (Alignment.StationOffset del borde) hasta que cae sobre
        /// él. null si Civil 3D no da el punto (fuera del rango del borde) o si no converge en 6 pasos.
        /// </summary>
        private static Func<double, double?> MedidorDeBorde(CivAlignment eje, CivAlignment borde, double inicial)
        {
            return s =>
            {
                double off = inicial;
                for (int i = 0; i < 6; i++)
                {
                    double x = 0, y = 0, t = 0, d = 0;
                    eje.PointLocation(s, off, ref x, ref y);
                    borde.StationOffset(x, y, ref t, ref d);
                    if (Math.Abs(d) < 1e-5) return off;
                    off -= d;
                }
                return null;
            };
        }

        /// <summary>Lo leído de un borde: datos para la respuesta, medición y lo que hace falta para verificar.</summary>
        private sealed class BordeLeido
        {
            public Dictionary<string, object> Datos;
            public PerfilBorde Perfil;
            public bool EsDesplazado;
            public ObjectId Padre = ObjectId.Null;
            public double? Nominal;
            public string Estilo, FalloEstilo, Nombre;
        }

        /// <summary>Datos de una transición de una región (EntryTransition o ExitTransition), cada uno en su try.</summary>
        private static Dictionary<string, object> DatosTransicion(Func<object> transicion, Func<string> tipo, Func<double> inicio, Func<double> fin, Func<double> largo, Func<string> clase)
        {
            if (Seguro(transicion) == null) return null;
            return new Dictionary<string, object>
            {
                ["tipo"] = Seguro(tipo),
                ["descripcion"] = Seguro(clase),
                ["progresiva_inicial"] = Seguro(() => N(inicio())),
                ["progresiva_final"] = Seguro(() => N(fin())),
                ["longitud"] = Seguro(() => N(largo()))
            };
        }

        /// <summary>
        /// Las regiones del borde tal como las da Civil 3D (OffsetAlignmentInfo.Regions, por verificar): progresivas,
        /// longitud, Offset, OffsetDist, IncreasedWidth, tipo y transiciones. Solo para comparar con lo medido.
        /// </summary>
        private static List<object> RegionesCivil3d(CivAlignment al, List<string> avisos)
        {
            var lista = new List<object>();
            try
            {
                var info = al.OffsetAlignmentInfo;
                foreach (var r in info.Regions)
                {
                    var reg = r;
                    lista.Add(new Dictionary<string, object>
                    {
                        ["tipo"] = Seguro(() => reg.RegionType.ToString()),
                        ["progresiva_inicial"] = Seguro(() => N(reg.StartStation)),
                        ["progresiva_final"] = Seguro(() => N(reg.EndStation)),
                        ["longitud"] = Seguro(() => N(reg.Length)),
                        ["offset"] = Seguro(() => N(reg.Offset)),
                        ["offset_dist"] = Seguro(() => N(reg.OffsetDist)),
                        ["increased_width"] = Seguro(() => N(reg.IncreasedWidth)),
                        ["entrada"] = DatosTransicion(() => reg.EntryTransition, () => reg.EntryTransition.TransitionType.ToString(),
                            () => reg.EntryTransition.TransitionDescription.StartStation, () => reg.EntryTransition.TransitionDescription.EndStation,
                            () => reg.EntryTransition.TransitionDescription.Length, () => reg.EntryTransition.TransitionDescription.GetType().Name),
                        ["salida"] = DatosTransicion(() => reg.ExitTransition, () => reg.ExitTransition.TransitionType.ToString(),
                            () => reg.ExitTransition.TransitionDescription.StartStation, () => reg.ExitTransition.TransitionDescription.EndStation,
                            () => reg.ExitTransition.TransitionDescription.Length, () => reg.ExitTransition.TransitionDescription.GetType().Name)
                    });
                }
            }
            catch (Exception ex) { avisos.Add("No se pudieron leer las regiones del borde '" + al.Name + "' (OffsetAlignmentInfo.Regions): " + ex.GetType().Name + ": " + ex.Message + ". La medición no depende de ellas."); }
            return lista;
        }

        /// <summary>
        /// Intenta fijar la longitud de la transición de entrada y de salida de la región creada por AddWidening
        /// (TransitionDescription.Length, por verificar). La región es la que más se solapa con el rango pedido y, entre
        /// las que lo cubren al menos a medias, la más corta. Devuelve null si se aplicó o el motivo si no; la medición dice
        /// si de verdad cambió.
        /// </summary>
        private static string FijarTransicion(CivAlignment al, Ensanchamiento p)
        {
            try
            {
                var info = al.OffsetAlignmentInfo;
                double rango = p.ProgresivaFinal - p.ProgresivaInicial;
                bool aplicada = false;
                double mejorLargo = double.MaxValue;
                foreach (var r in info.Regions)
                {
                    var reg = r;
                    double ini = reg.StartStation, fin = reg.EndStation;
                    double solape = Math.Min(fin, p.ProgresivaFinal) - Math.Max(ini, p.ProgresivaInicial);
                    if (solape < 0.5 * rango || fin - ini >= mejorLargo) continue;
                    mejorLargo = fin - ini;
                    reg.EntryTransition.TransitionDescription.Length = p.LongitudTransicion.Value;
                    reg.ExitTransition.TransitionDescription.Length = p.LongitudTransicion.Value;
                    aplicada = true;
                }
                return aplicada ? null : "ninguna región de OffsetAlignmentInfo.Regions cubre el rango pedido";
            }
            catch (Exception ex) { return "TransitionDescription.Length: " + ex.GetType().Name + ": " + ex.Message; }
        }

        /// <summary>
        /// Lee un borde del dibujo: si es desplazado, su eje, desplazamiento nominal, lado según Civil 3D, estilo, capa,
        /// rango, regiones de Civil 3D y la medición a lo largo del eje 'eje' (o de su padre si 'eje' es null).
        /// </summary>
        private static BordeLeido LeerBorde(Transaction tr, ObjectId idBorde, CivAlignment eje, double? nominalEsperado, List<string> avisos)
        {
            var al = (CivAlignment)tr.GetObject(idBorde, OpenMode.ForRead);
            var b = new BordeLeido { Nombre = al.Name };
            b.EsDesplazado = Seguro(() => al.IsOffsetAlignment);
            string lado = null;
            if (b.EsDesplazado)
            {
                try
                {
                    var info = al.OffsetAlignmentInfo;
                    b.Padre = info.ParentAlignmentId;
                    b.Nominal = info.NominalOffset;
                    lado = Seguro(() => info.Side.ToString());
                }
                catch (Exception ex) { avisos.Add("No se pudo leer OffsetAlignmentInfo del borde '" + al.Name + "': " + ex.GetType().Name + ": " + ex.Message); }
            }
            b.Estilo = LeerEstiloDe(tr, al, out b.FalloEstilo);
            if (eje == null && !b.Padre.IsNull) eje = Seguro(() => tr.GetObject(b.Padre, OpenMode.ForRead) as CivAlignment);
            double nominal = Math.Abs(nominalEsperado ?? b.Nominal ?? 0);
            if (eje != null && nominal > 0)
            {
                double inicial = (b.Nominal.HasValue && b.Nominal.Value != 0 ? Math.Sign(b.Nominal.Value) : 1) * nominal;
                b.Perfil = BordesCalzada.Medir(MedidorDeBorde(eje, al, inicial), eje.StartingStation, eje.EndingStation, BordesCalzada.PasoMuestreo(eje.Length), nominal);
            }
            else avisos.Add("El borde '" + al.Name + "' no se midió: " + (eje == null ? "no se encontró su eje" : "su desplazamiento nominal es 0 o no se pudo leer") + ".");
            b.Datos = new Dictionary<string, object>
            {
                ["nombre"] = al.Name,
                ["es_desplazado"] = b.EsDesplazado,
                ["eje"] = NombreAlineamiento(tr, b.Padre),
                ["desplazamiento_nominal"] = b.Nominal.HasValue ? N(b.Nominal.Value) : null,
                ["lado_civil3d"] = lado,
                ["lado"] = b.Perfil?.Lado,
                ["estilo"] = b.Estilo,
                ["capa"] = Seguro(() => al.Layer),
                ["longitud"] = Seguro(() => N(al.Length)),
                ["progresiva_inicial"] = Seguro(() => N(al.StartingStation)),
                ["progresiva_final"] = Seguro(() => N(al.EndingStation)),
                ["medicion"] = b.Perfil?.ComoDiccionario(),
                ["regiones_civil3d"] = b.EsDesplazado ? RegionesCivil3d(al, avisos) : null
            };
            return b;
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarBordes()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaCrearBordes,
                Descripcion = "Crea los dos bordes de calzada de un eje: dos alineamientos desplazados (Alignment.CreateOffsetAlignment), a la izquierda y a la derecha, a 'desplazamiento' (media calzada) del eje, en UNA entrada de Deshacer. Opcionalmente añade ensanchamientos (sobreanchos) por tramo con 'ensanchamientos' (OffsetAlignmentInfo.AddWidening): por cada uno, el borde, el rango de progresivas del eje, el sobreancho que se suma a la media calzada y, si se quiere, la longitud de la transición. Después de crear, relee cada borde (eje padre, desplazamiento nominal, estilo, capa) y lo MIDE cada metro a lo largo del eje: si un borde no queda en su lado o el sobreancho medido no es el pedido, error sin reintentar; si la transición no quedó como se previó, aviso con lo medido. Estilo y capa por defecto: los de alineaciones de la configuración del dibujo. Si ya existe un alineamiento con alguno de los nombres, error con la lista. Después, asigna los bordes a los carriles del corredor con asignar_objetivos (tipo desplazamiento) y reconstruir_corredor. Sin valores de norma: el sobreancho de la DG-2018 lo calcula el puente con velocidad_diseno.",
                Parametros =
                {
                    P("eje", "string", "Nombre del alineamiento (eje) del que salen los bordes; ver listar_alineamientos", true),
                    P("desplazamiento", "number", "Distancia (> 0) del eje a cada borde, media calzada, en unidades del dibujo (3.6 para una calzada de 7.2)", true),
                    P("nombre_izquierdo", "string", "Nombre del borde izquierdo (no debe existir). Por defecto '<eje> - Borde izq'"),
                    P("nombre_derecho", "string", "Nombre del borde derecho (no debe existir). Por defecto '<eje> - Borde der'"),
                    P("estilo", "string", "Estilo de alineamiento de los bordes. Si falta, el de la configuración del dibujo; si no existe, error con la lista"),
                    P("ensanchamientos", "json", "Lista de sobreanchos: [{\"borde\": \"izquierdo\", \"progresiva_inicial\": 220, \"progresiva_final\": 345.66, \"sobreancho\": 0.6, \"longitud_transicion\": 40}]. borde: izquierdo o derecho (en el sentido de avance del eje). El rango es el tramo de sobreancho pleno (se supone: la validación lo confirma) y sobreancho lo que se añade a la media calzada (> 0). longitud_transicion (opcional, m): se intenta fijar en la transición de entrada y de salida; si falta, la decide Civil 3D. Sin solapes en un mismo borde"),
                    P("simular", "boolean", "Con true comprueba el eje, los nombres, el estilo y los ensanchamientos y devuelve los bordes previstos sin tocar el dibujo (ni copia)")
                },
                Ejecutar = a => Escritura.Ejecutar(HerramientaCrearBordes, a, ctx =>
                {
                    string nombreEje = Requerido(a, "eje").Trim();
                    if (!Tiene(a, "desplazamiento")) throw new ArgumentException("Falta el parámetro obligatorio 'desplazamiento' (media calzada, > 0).");
                    double d = Num(a, "desplazamiento", double.NaN);
                    BordesCalzada.ValidarDesplazamiento(d);
                    string nombreIzq = (Str(a, "nombre_izquierdo") ?? "").Trim(), nombreDer = (Str(a, "nombre_derecho") ?? "").Trim();
                    if (nombreIzq.Length == 0) nombreIzq = BordesCalzada.NombrePorDefecto(nombreEje, BordesCalzada.Izquierdo);
                    if (nombreDer.Length == 0) nombreDer = BordesCalzada.NombrePorDefecto(nombreEje, BordesCalzada.Derecho);
                    if (string.Equals(nombreIzq, nombreDer, StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("'nombre_izquierdo' y 'nombre_derecho' son iguales ('" + nombreIzq + "'): cada borde necesita su nombre.");
                    string estilo = Str(a, "estilo");
                    var jsonEns = JsonArg(a, "ensanchamientos");
                    var db = ctx.Db;

                    ObjectId idEje = ObjectId.Null, idEstilo = ObjectId.Null, idCapa = ObjectId.Null, idDer = ObjectId.Null;
                    string estiloUsado = estilo, capaBordes = null;
                    List<Ensanchamiento> pedidos = null;
                    var existentes = new List<string>();
                    var transicionesNoAplicadas = new Dictionary<int, string>();
                    var leidos = new Dictionary<string, BordeLeido>();
                    var comparaciones = new Dictionary<int, ComparacionEnsanchamiento>();

                    Dictionary<string, object> Previsto(string lado) => new Dictionary<string, object>
                    {
                        ["nombre"] = lado == BordesCalzada.Izquierdo ? nombreIzq : nombreDer,
                        ["lado"] = lado,
                        ["desplazamiento_nominal"] = N(lado == BordesCalzada.Izquierdo ? -d : d),
                        ["estilo"] = estiloUsado,
                        ["capa"] = capaBordes,
                        ["ensanchamientos"] = pedidos.Where(p => p.Borde == lado).Select(p => (object)p.ComoDiccionario(d)).ToList()
                    };

                    Dictionary<string, object> Real(string lado)
                    {
                        if (!leidos.TryGetValue(lado, out var b)) return null;
                        var datos = new Dictionary<string, object>(b.Datos);
                        datos["ensanchamientos"] = pedidos.Where(p => p.Borde == lado).Select(p =>
                        {
                            var x = p.ComoDiccionario(d);
                            if (comparaciones.TryGetValue(p.Numero, out var c)) x["medido"] = c.ComoDiccionario();
                            if (p.LongitudTransicion.HasValue)
                                x["longitud_transicion_aplicada"] = transicionesNoAplicadas.TryGetValue(p.Numero, out string motivo) ? (object)("no: " + motivo) : true;
                            return (object)x;
                        }).ToList();
                        return datos;
                    }

                    return Escritura.CrearObjeto(ctx, new Escritura.Creacion
                    {
                        Accion = "Crear los bordes de calzada '" + nombreIzq + "' y '" + nombreDer + "' del eje '" + nombreEje + "' a "
                            + d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " de cada lado"
                            + (jsonEns.ValueKind == System.Text.Json.JsonValueKind.Undefined ? "" : " con ensanchamientos"),
                        Antes = tr =>
                        {
                            idEje = BuscarAlineamiento(tr, nombreEje);   // "No existe el alineamiento 'X'. Usa listar_alineamientos."
                            var eje = (CivAlignment)tr.GetObject(idEje, OpenMode.ForRead);
                            if (Seguro(() => eje.IsOffsetAlignment))
                            {
                                string padre = Seguro(() => NombreAlineamiento(tr, eje.OffsetAlignmentInfo.ParentAlignmentId));
                                throw new ArgumentException("El alineamiento '" + eje.Name + "' ya es un borde (desplazado" + (padre == null ? "" : " de '" + padre + "'") + "): pasa en 'eje' el eje del que salen los bordes. Usa listar_bordes_calzada.");
                            }
                            foreach (string nombre in new[] { nombreIzq, nombreDer })
                                if (!BuscarAlineamientoONull(tr, nombre).IsNull)
                                    throw new ArgumentException("Ya existe el alineamiento '" + nombre + "'. Alineamientos del dibujo: " + string.Join(", ", NombresAlineamientos(tr)) + ". Pasa 'nombre_izquierdo' o 'nombre_derecho' con otro nombre, o usa listar_bordes_calzada para ver los bordes que ya tiene el eje.");
                            pedidos = BordesCalzada.Leer(jsonEns, eje.StartingStation, eje.EndingStation);
                            try
                            {
                                foreach (ObjectId idHijo in eje.GetChildOffsetAlignmentIds())
                                {
                                    string n = NombreAlineamiento(tr, idHijo);
                                    if (n != null) existentes.Add(n);
                                }
                            }
                            catch (Exception ex) { Historial.Registrar(HerramientaCrearBordes + ": no se pudieron leer los bordes existentes (GetChildOffsetAlignmentIds): " + ex.Message); }
                            if (existentes.Count > 0)
                                ctx.Avisos.Add("El eje '" + eje.Name + "' ya tiene " + existentes.Count + " alineamiento" + (existentes.Count == 1 ? "" : "s") + " desplazado" + (existentes.Count == 1 ? "" : "s") + ": " + string.Join(", ", existentes) + ". Se crean dos bordes más; si sobran, revisa con listar_bordes_calzada.");
                            idEstilo = string.IsNullOrWhiteSpace(estilo) ? EstiloAlineamientoPorDefecto(tr, ctx.Avisos, out estiloUsado) : BuscarEstiloAlineamiento(tr, estilo.Trim());
                            idCapa = CapaParaAlineamientos(tr, db, ctx.Avisos);
                            try { capaBordes = ((LayerTableRecord)tr.GetObject(idCapa, OpenMode.ForRead)).Name; } catch { capaBordes = null; }
                            return new Dictionary<string, object>
                            {
                                ["eje"] = eje.Name,
                                ["existe_izquierdo"] = false,
                                ["existe_derecho"] = false,
                                ["bordes_existentes"] = existentes
                            };
                        },
                        Prevision = tr => new Dictionary<string, object>
                        {
                            ["eje"] = nombreEje,
                            ["existe_izquierdo"] = true,
                            ["existe_derecho"] = true,
                            ["bordes_existentes"] = existentes,
                            ["izquierdo"] = Previsto(BordesCalzada.Izquierdo),
                            ["derecho"] = Previsto(BordesCalzada.Derecho)
                        },
                        Esperado = new Dictionary<string, object> { ["existe_izquierdo"] = true, ["existe_derecho"] = true },
                        Crear = tr =>
                        {
                            // 1.21.3: cada llamada a Civil 3D con su propio error, lanzado SIN la excepción interna (el
                            // servidor informa la más interna, y entonces se perdía qué miembro falló); el texto original y
                            // su traza van al historial (leer_historial).
                            ObjectId idIzq;
                            try { idIzq = CivAlignment.CreateOffsetAlignment(nombreIzq, idEje, -d, idEstilo); }
                            catch (Exception ex) { throw ErrorDeCivil3d(ex, "Civil 3D no dejó crear el borde izquierdo '" + nombreIzq + "' con desplazamiento " + (-d).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " (Alignment.CreateOffsetAlignment, borde izquierdo)"); }
                            try { idDer = CivAlignment.CreateOffsetAlignment(nombreDer, idEje, d, idEstilo); }
                            catch (Exception ex) { throw ErrorDeCivil3d(ex, "Civil 3D no dejó crear el borde derecho '" + nombreDer + "' con desplazamiento " + d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " (Alignment.CreateOffsetAlignment, borde derecho)"); }
                            if (idIzq.IsNull || idDer.IsNull)
                                throw new InvalidOperationException("Alignment.CreateOffsetAlignment no devolvió el borde " + (idIzq.IsNull ? "izquierdo" : "derecho") + ". Nada queda escrito (la transacción se descarta) y no se reintenta.");
                            foreach (var par in new[] { (lado: BordesCalzada.Izquierdo, id: idIzq), (lado: BordesCalzada.Derecho, id: idDer) })
                            {
                                var al = (CivAlignment)tr.GetObject(par.id, OpenMode.ForWrite);
                                if (!idCapa.IsNull)
                                {
                                    try { al.LayerId = idCapa; }
                                    catch (Exception ex) { ctx.Avisos.Add("No se pudo poner el borde '" + al.Name + "' en la capa '" + capaBordes + "' (Entity.LayerId): " + ex.Message + ". Queda en la capa en que lo dejó Civil 3D."); }
                                }
                                var propios = pedidos.Where(p => p.Borde == par.lado).ToList();
                                if (propios.Count == 0) continue;
                                var info = al.OffsetAlignmentInfo;
                                double nominal;
                                try { nominal = info.NominalOffset; }
                                catch (Exception ex) { throw new InvalidOperationException("No se pudo leer el desplazamiento nominal del borde recién creado '" + al.Name + "' (OffsetAlignmentInfo.NominalOffset): " + ex.GetType().Name + ": " + ex.Message + ". Sin él no se puede calcular el desplazamiento ensanchado. Nada queda escrito (la transacción se descarta) y no se reintenta.", ex); }
                                foreach (var p in propios)
                                {
                                    // 1.21.3: sin signo; con −4.2 Civil 3D respondió «offsetDistance must greater than 0» (20/11/2026)
                                    double total = BordesCalzada.DistanciaEnsanchada(nominal, p.Sobreancho);
                                    try { info.AddWidening(p.ProgresivaInicial, p.ProgresivaFinal, total); }
                                    catch (Exception ex) { throw ErrorDeCivil3d(ex, "Civil 3D no dejó añadir el ensanchamiento " + p.Numero + " al borde " + par.lado + " '" + al.Name + "' (OffsetAlignmentInfo.AddWidening de " + p.ProgresivaInicial.ToString(System.Globalization.CultureInfo.InvariantCulture) + " a " + p.ProgresivaFinal.ToString(System.Globalization.CultureInfo.InvariantCulture) + " con " + total.ToString(System.Globalization.CultureInfo.InvariantCulture) + ", nominal releído " + nominal.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")"); }
                                    if (p.LongitudTransicion.HasValue)
                                    {
                                        string motivo = FijarTransicion(al, p);
                                        if (motivo != null) transicionesNoAplicadas[p.Numero] = motivo;
                                    }
                                }
                            }
                            return idIzq;
                        },
                        Despues = (tr, idIzq) =>
                        {
                            var eje = (CivAlignment)tr.GetObject(idEje, OpenMode.ForRead);
                            foreach (var par in new[] { (lado: BordesCalzada.Izquierdo, id: idIzq), (lado: BordesCalzada.Derecho, id: idDer) })
                            {
                                if (par.id.IsNull || Seguro(() => par.id.IsErased, true)) continue;
                                var b = LeerBorde(tr, par.id, eje, d, ctx.Avisos);
                                leidos[par.lado] = b;
                                if (b.Perfil != null)
                                    foreach (var p in pedidos.Where(x => x.Borde == par.lado))
                                        comparaciones[p.Numero] = BordesCalzada.Comparar(p, b.Perfil, d, b.Nombre);
                            }
                            return new Dictionary<string, object>
                            {
                                ["eje"] = eje.Name,
                                ["existe_izquierdo"] = leidos.ContainsKey(BordesCalzada.Izquierdo),
                                ["existe_derecho"] = leidos.ContainsKey(BordesCalzada.Derecho),
                                ["bordes_existentes"] = existentes,
                                ["izquierdo"] = Real(BordesCalzada.Izquierdo),
                                ["derecho"] = Real(BordesCalzada.Derecho)
                            };
                        },
                        Verificar = despues =>
                        {
                            var errores = new List<string>();
                            foreach (var lado in new[] { BordesCalzada.Izquierdo, BordesCalzada.Derecho })
                            {
                                if (!leidos.TryGetValue(lado, out var b)) { errores.Add("No se encontró el borde " + lado + " después de crearlo."); continue; }
                                string cual = "El borde " + lado + " '" + b.Nombre + "'";
                                if (!b.EsDesplazado) errores.Add(cual + " no es un alineamiento desplazado (IsOffsetAlignment = false).");
                                else if (b.Padre != idEje) errores.Add(cual + " no sale del eje '" + nombreEje + "' (su eje es '" + ((b.Datos["eje"] as string) ?? "?") + "').");
                                if (!b.Nominal.HasValue) errores.Add(cual + ": no se pudo releer el desplazamiento nominal (OffsetAlignmentInfo.NominalOffset).");
                                else if (Math.Abs(Math.Abs(b.Nominal.Value) - d) > BordesCalzada.ToleranciaNominal)
                                    errores.Add(cual + " tiene desplazamiento nominal " + b.Nominal.Value + " y se pidió " + d + ".");
                                if (b.Perfil == null || b.Perfil.Signo == 0) errores.Add(cual + " no se pudo medir a lo largo del eje (Alignment.PointLocation / StationOffset).");
                                else
                                {
                                    if (b.Perfil.Lado != lado)
                                        errores.Add(cual + " quedó a la " + (lado == BordesCalzada.Izquierdo ? "derecha" : "izquierda") + " del eje (desplazamiento medido con signo " + (b.Perfil.Signo > 0 ? "+" : "-") + "): el signo de Alignment.CreateOffsetAlignment es el contrario del supuesto (negativo = izquierda). Anótalo en miembros_por_verificar_civil3d.md.");
                                    if (b.Perfil.NominalMedido.HasValue && Math.Abs(b.Perfil.NominalMedido.Value - d) > BordesCalzada.ToleranciaMedida)
                                        errores.Add(cual + " está medido a " + Math.Round(b.Perfil.NominalMedido.Value, 4) + " del eje fuera de los ensanchamientos y se pidió " + d + ".");
                                }
                                string distinto = RelecturaEstilo.Discrepancia("del borde '" + b.Nombre + "'", b.Estilo, estiloUsado);
                                if (distinto != null) errores.Add(distinto);
                                else if (b.Estilo == null) ctx.Avisos.Add(RelecturaEstilo.Aviso("del borde '" + b.Nombre + "'", b.FalloEstilo, estiloUsado));
                            }
                            foreach (var p in pedidos)
                            {
                                if (!comparaciones.TryGetValue(p.Numero, out var c)) continue;
                                if (c.Error != null) errores.Add(c.Error);
                                else if (c.Aviso != null) ctx.Avisos.Add(c.Aviso);
                                if (transicionesNoAplicadas.TryGetValue(p.Numero, out string motivo))
                                    ctx.Avisos.Add("La longitud de transición " + p.LongitudTransicion + " m del ensanchamiento " + p.Numero + " no se pudo fijar (" + motivo + "): queda la de Civil 3D; ver 'medido'.");
                            }
                            if (errores.Count > 0)
                                throw new InvalidOperationException(string.Join(" ", errores) + " No se reintenta; deshaz con Ctrl+Z (_.UNDO 1) si no quieres los bordes. despues=" + Nucleo.Json.Serializar(despues));
                        },
                        Extra = () => new
                        {
                            eje = nombreEje,
                            desplazamiento = d,
                            estilo = estiloUsado,
                            capa = capaBordes,
                            bordes = new[] { Real(BordesCalzada.Izquierdo), Real(BordesCalzada.Derecho) },
                            numero_ensanchamientos = pedidos.Count,
                            siguiente_paso = "Asigna los bordes a los carriles del corredor con asignar_objetivos (tipo desplazamiento, objetivo = el nombre del borde, el izquierdo a los subensamblajes (Left) y el derecho a los (Right)) y después reconstruir_corredor."
                        }
                    });
                })
            });

            Registrar(new Herramienta
            {
                Nombre = HerramientaListarBordes,
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Lista los bordes de calzada (alineamientos desplazados) de un eje. 'alineamiento' puede ser el eje o uno de sus bordes (entonces se busca su eje). Por cada borde: nombre, eje, desplazamiento nominal (con el signo de Civil 3D), lado según Civil 3D y lado medido, estilo, capa, longitud, progresivas, las regiones tal como las da Civil 3D (regiones_civil3d) y la MEDICIÓN a lo largo del eje (cada metro, o más si el eje es muy largo): desplazamiento fuera de los ensanchamientos y cada zona ensanchada con el inicio y fin de la transición de entrada, el tramo pleno, la transición de salida y el sobreancho medido. Solo lee: no cambia el dibujo.",
                Parametros = { P("alineamiento", "string", "Nombre del eje o de uno de sus bordes; ver listar_alineamientos", true) },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string nombre = Requerido(a, "alineamiento").Trim();
                    var avisos = new List<string>();
                    object respuesta;
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var idConsultado = BuscarAlineamiento(tr, nombre);   // "No existe el alineamiento 'X'. Usa listar_alineamientos."
                        var consultado = (CivAlignment)tr.GetObject(idConsultado, OpenMode.ForRead);
                        bool esBorde = Seguro(() => consultado.IsOffsetAlignment);
                        ObjectId idEje = idConsultado;
                        if (esBorde)
                        {
                            ObjectId padre = Seguro(() => consultado.OffsetAlignmentInfo.ParentAlignmentId, ObjectId.Null);
                            if (padre.IsNull) avisos.Add("'" + consultado.Name + "' es un alineamiento desplazado pero no se pudo leer su eje (OffsetAlignmentInfo.ParentAlignmentId): solo se lista él.");
                            else idEje = padre;
                        }
                        var eje = (CivAlignment)tr.GetObject(idEje, OpenMode.ForRead);
                        var ids = new List<ObjectId>();
                        if (idEje == idConsultado && esBorde) ids.Add(idConsultado);
                        else
                        {
                            try { foreach (ObjectId id in eje.GetChildOffsetAlignmentIds()) ids.Add(id); }
                            catch (Exception ex) { avisos.Add("No se pudieron leer los bordes del eje '" + eje.Name + "' (Alignment.GetChildOffsetAlignmentIds): " + ex.GetType().Name + ": " + ex.Message); if (esBorde) ids.Add(idConsultado); }
                        }
                        var bordes = new List<object>();
                        foreach (var id in ids)
                        {
                            if (Seguro(() => id.IsErased, true)) continue;
                            bordes.Add(LeerBorde(tr, id, idEje == idConsultado && esBorde ? null : eje, null, avisos).Datos);
                        }
                        if (bordes.Count == 0) avisos.Add("El eje '" + eje.Name + "' no tiene bordes (alineamientos desplazados). Créalos con crear_bordes_calzada.");
                        respuesta = new
                        {
                            eje = eje.Name,
                            consultado = consultado.Name,
                            es_borde = esBorde,
                            numero_bordes = bordes.Count,
                            bordes,
                            avisos = avisos.Count > 0 ? avisos : null
                        };
                        tr.Commit();
                    }
                    return respuesta;
                }
            });
        }
    }
}
