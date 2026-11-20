using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// Peralte de un eje (1.20.0): listar_peralte (lectura) y aplicar_peralte (escritura con valores explícitos). El
    /// cálculo con la DG-2018 lo hace el puente (norma_peralte.py), igual que velocidad_diseno en
    /// crear_alineamiento_desde_polilinea; el plugin no conoce la norma ni el archivo XML de criterios de diseño.
    ///
    /// Miembros usados (catálogo de Civil 3D 2027, consulta de la orquestadora del 17/11/2026; por verificar en Civil 3D):
    /// Alignment.SuperelevationCurves (SuperelevationCurveCollection: enumerable, Count, AddUserDefinedCurve(sub, sub) →
    /// SuperelevationCurve), SuperelevationCurve.Name/StartStation/EndStation/CriticalStations,
    /// Alignment.SuperelevationCriticalStations, SuperelevationCriticalStationCollection (enumerable, Count,
    /// Add(pk, tipo, región) y Add(pk, tipo) → void, GetCriticalStationAt(pk, tolerancia)), SuperelevationCriticalStation
    /// (Station, StationType, TransitionRegionType, SuperelevationCurveName, TransitionDescription, GetSlope, SetSlope),
    /// Alignment.SuperelevationType, UseDesignCriteriaFile y CriteriaFileName (solo se informan).
    /// Los valores de los enumerados (Autodesk.Civil.SuperelevationCriticalStationType, SuperelevationAttainmentRegionType,
    /// SuperelevationCrossSegmentType y SuperelevationTransitionRegionType) no están en el catálogo: listar_peralte los lee
    /// de la DLL con Enum.GetNames y aplicar_peralte acepta esos nombres; ninguno está escrito en el código.
    /// Lo que no toca Civil 3D (lectura de 'curvas', verificación, mensajes) está en ArbaMcp.Nucleo.Peralte.
    /// Tarea 7.4: Alignment.GetCrossSlopeAtStation(double station, SuperelevationCrossSegmentType crossSegmentType, bool
    /// applySmoothing) (catálogo, 20/11/2026; por verificar) lee el peralte en cualquier pk, también entre progresivas
    /// críticas: listar_peralte con 'progresivas' y la comprobación de aplicar_peralte en el centro del peralte total.
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaListarPeralte = "listar_peralte";
        private const string HerramientaAplicarPeralte = "aplicar_peralte";

        // ------------------------------------------------------------------ lectura del peralte
        /// <summary>Una progresiva crítica del eje como datos del núcleo; las pendientes que no se leen se anotan una vez en 'avisos'.</summary>
        private static EstacionLeida LeerEstacionPeralte(Civ.SuperelevationCriticalStation st, HashSet<string> avisos)
        {
            var e = new EstacionLeida
            {
                Progresiva = st.Station,
                Tipo = st.StationType.ToString(),
                Region = Seguro(() => st.TransitionRegionType.ToString(), null),
                Curva = Seguro(() => st.SuperelevationCurveName, null),
                Descripcion = Seguro(() => Convert.ToString(st.TransitionDescription, CultureInfo.InvariantCulture), null)
            };
            foreach (SuperelevationCrossSegmentType segmento in Enum.GetValues(typeof(SuperelevationCrossSegmentType)))
            {
                try
                {
                    double v = st.GetSlope(segmento);
                    if (!double.IsNaN(v) && !double.IsInfinity(v)) e.Pendientes[segmento.ToString()] = Peralte.PendientePct(v);
                }
                catch (Exception ex) { avisos.Add("No se pudo leer la pendiente de '" + segmento + "' (SuperelevationCriticalStation.GetSlope): " + Raiz(ex).Message); }
            }
            return e;
        }

        /// <summary>Curvas de peralte del eje con sus progresivas críticas, y las progresivas de la colección del propio eje.</summary>
        private static PeralteLeido LeerPeralte(CivAlignment al, HashSet<string> avisos)
        {
            var p = new PeralteLeido { Alineamiento = al.Name };
            foreach (var curva in al.SuperelevationCurves)
            {
                var c = new CurvaLeida
                {
                    Nombre = Seguro(() => curva.Name, null),
                    Inicio = Seguro(() => (double?)curva.StartStation, null),
                    Fin = Seguro(() => (double?)curva.EndStation, null)
                };
                foreach (var st in curva.CriticalStations) c.Estaciones.Add(LeerEstacionPeralte(st, avisos));
                p.Curvas.Add(c);
            }
            foreach (var st in al.SuperelevationCriticalStations) p.EstacionesDelEje.Add(LeerEstacionPeralte(st, avisos));
            return p;
        }

        /// <summary>
        /// Pendiente transversal del eje en un pk y un segmento (nombre del enumerado), en tanto por uno, con
        /// Alignment.GetCrossSlopeAtStation y el suavizado de Peralte.AplicarSuavizado (tarea 7.4). Lanza si falla: el
        /// núcleo pone el texto del error en el lugar del valor.
        /// </summary>
        private static double PendienteEnPk(CivAlignment al, double pk, string segmento)
            => al.GetCrossSlopeAtStation(pk, EnumPeralte<SuperelevationCrossSegmentType>(segmento, "segmento de la sección"), Peralte.AplicarSuavizado);

        private static string TextoLecturaEntreProgresivas()
            => "Alignment.GetCrossSlopeAtStation(pk, segmento, applySmoothing: " + (Peralte.AplicarSuavizado ? "true" : "false") + "); % = valor de la API × "
               + Peralte.FactorApi.ToString(CultureInfo.InvariantCulture) + "; tolerancia de la comprobación de aplicar_peralte: "
               + Peralte.ToleranciaEntreProgresivas.ToString(CultureInfo.InvariantCulture) + " puntos de % (por verificar en Civil 3D)";

        /// <summary>Los nombres de los enumerados del peralte, leídos de la DLL de Civil 3D (no se inventan).</summary>
        private static Dictionary<string, object> EnumeradosPeralte() => new Dictionary<string, object>
        {
            ["tipo_estacion"] = Enum.GetNames(typeof(SuperelevationCriticalStationType)),
            ["region"] = Enum.GetNames(typeof(SuperelevationAttainmentRegionType)),
            ["region_transicion"] = Enum.GetNames(typeof(SuperelevationTransitionRegionType)),
            ["segmento"] = Enum.GetNames(typeof(SuperelevationCrossSegmentType)),
            ["tipo_peralte_eje"] = Enum.GetNames(typeof(Civ.SuperelevationType))
        };

        /// <summary>
        /// Valor de un enumerado del peralte por su nombre exacto (sin distinguir mayúsculas; los números no valen). Si no
        /// existe, error con la lista real y sin tocar nada.
        /// </summary>
        private static T EnumPeralte<T>(string nombre, string que) where T : struct
        {
            string n = (nombre ?? "").Trim();
            string real = Enum.GetNames(typeof(T)).FirstOrDefault(x => string.Equals(x, n, StringComparison.OrdinalIgnoreCase));
            if (real != null && Enum.TryParse(real, false, out T v)) return v;
            throw new ArgumentException("'" + nombre + "' no es un " + que + " de Civil 3D (" + typeof(T).Name + "). Valores: " + string.Join(", ", Enum.GetNames(typeof(T))) + ". Usa listar_peralte (enumerados).");
        }

        /// <summary>Tabla de PI del eje (la de listar_pis) para identificar las curvas por su número de PI.</summary>
        private static ResultadoTablaPi TablaPiDelEje(CivAlignment al)
        {
            double inicio = al.StartingStation, fin = al.EndingStation;
            return TablaPi.Calcular(al.Name, EntidadesDelEje(al), inicio, fin, PuntoDelEje(al, inicio), PuntoDelEje(al, fin), null, al.Length);
        }

        /// <summary>La curva circular del eje que empieza en ese PC (la del PI pedido).</summary>
        private static Civ.AlignmentArc ArcoEnPc(CivAlignment al, double pc, int pi)
        {
            var entidades = al.Entities;
            for (int i = 0; i < entidades.Count; i++)
                if (entidades.GetEntityByOrder(i) is Civ.AlignmentArc arco && Math.Abs(arco.StartStation - pc) <= Peralte.ToleranciaProgresiva)
                    return arco;
            throw new InvalidOperationException("No se encontró la curva circular del PI " + pi + " (PC en " + TablaPi.Progresiva(pc) + ") en las entidades del eje '" + al.Name + "'. Usa listar_pis.");
        }

        /// <summary>
        /// La curva de peralte vacía que Civil 3D creó para el arco del PI (1.21.2): la del nombre que eligió
        /// Peralte.AsignarCurvasExistentes, comprobando otra vez que va de PC a PT y que sigue sin progresivas.
        /// </summary>
        private static Civ.SuperelevationCurve CurvaPeralteExistente(CivAlignment al, CurvaPeralte c)
        {
            foreach (var curva in al.SuperelevationCurves)
            {
                if (!string.Equals(Seguro(() => curva.Name, null), c.CurvaExistente, StringComparison.Ordinal)) continue;
                if (Peralte.EsCurvaVaciaDelArco(Seguro(() => (double?)curva.StartStation, null), Seguro(() => (double?)curva.EndStation, null),
                        curva.CriticalStations.Count, c.ProgresivaPc.Value, c.ProgresivaPt.Value))
                    return curva;
            }
            throw new InvalidOperationException("No se encontró la curva de peralte vacía '" + c.CurvaExistente + "' del PI " + c.Pi + " (de " + TablaPi.Progresiva(c.ProgresivaPc.Value)
                + " a " + TablaPi.Progresiva(c.ProgresivaPt.Value) + ") al escribir. No se reintenta. Míralo con listar_peralte.");
        }

        /// <summary>
        /// Tarea 7.6: la progresiva de contorno (BeginAlignment o EndAlignment) de la colección del eje, por su tipo y su
        /// progresiva (la más cercana a 'pk'); null si no está. Miembros ya usados: Alignment.SuperelevationCriticalStations,
        /// StationType y Station.
        /// </summary>
        private static Civ.SuperelevationCriticalStation EstacionDeContornoDelEje(CivAlignment al, string tipo, double pk)
        {
            Civ.SuperelevationCriticalStation mejor = null;
            foreach (var st in al.SuperelevationCriticalStations)
            {
                if (!string.Equals(Seguro(() => st.StationType.ToString(), null), tipo, StringComparison.OrdinalIgnoreCase)) continue;
                if (mejor == null || Math.Abs(st.Station - pk) < Math.Abs(mejor.Station - pk)) mejor = st;
            }
            return mejor;
        }

        // ------------------------------------------------------------------ escritura
        /// <summary>
        /// Cuerpo de aplicar_peralte, con el patrón de CambiarSuperficie: simulación con bloqueo de lectura (sin entrada de
        /// Deshacer), escritura con la copia terminada en una transacción, relectura en otra transacción y verificación
        /// (Peralte.Diferencias: si el dibujo no refleja lo pedido, error sin reintentar).
        /// </summary>
        private static object AplicarPeralte(Escritura.Contexto ctx, string nombreAl, List<CurvaPeralte> curvas)
        {
            var doc = ctx.Doc;
            Dictionary<string, object> antes, prevision;
            int curvasAntes;
            List<BombeoExtremo> extremos;   // tarea 7.6: el bombeo normal en BeginAlignment y EndAlignment
            ObjectId idEje;
            var avisosLectura = new HashSet<string>();
            string nombreReal = nombreAl;
            using (ctx.Simular ? BloquearParaLeer(doc) : doc.LockDocument())   // simulación: contexto de aplicación y solo lectura, sin entrada de Deshacer
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var idAl = BuscarAlineamiento(tr, nombreAl);   // "No existe el alineamiento 'X'. Usa listar_alineamientos."
                idEje = idAl;
                var al = (CivAlignment)tr.GetObject(idAl, ctx.Simular ? OpenMode.ForRead : OpenMode.ForWrite);
                nombreReal = al.Name;
                var actual = LeerPeralte(al, avisosLectura);
                if (actual.TienePeralte) throw new ArgumentException(Peralte.MensajeYaTienePeralte(actual));
                ctx.Avisos.AddRange(Peralte.ComprobarContraEje(curvas, TablaPiDelEje(al)));
                // Civil 3D crea con el eje una curva de peralte vacía por curva horizontal: se le añaden las progresivas (1.21.2)
                Peralte.AsignarCurvasExistentes(curvas, actual);
                curvasAntes = actual.Curvas.Count;
                // Los nombres de los enumerados se resuelven ya (también al simular): uno que no exista falla sin tocar nada
                foreach (var c in curvas)
                    foreach (var e in c.Estaciones)
                    {
                        EnumPeralte<SuperelevationCriticalStationType>(e.Tipo, "tipo de progresiva crítica");
                        if (e.Region != null) EnumPeralte<SuperelevationAttainmentRegionType>(e.Region, "tipo de región de transición");
                        foreach (var p in e.Pendientes) EnumPeralte<SuperelevationCrossSegmentType>(p.Key, "segmento de la sección");
                    }
                if (Seguro(() => al.UseDesignCriteriaFile, false))
                    ctx.Avisos.Add("El eje '" + al.Name + "' tiene activado el archivo de criterios de diseño (" + (Seguro(() => al.CriteriaFileName, null) ?? "sin nombre")
                        + "): aplicar_peralte no lo usa; los valores son los pedidos.");
                // Tarea 7.6: Civil 3D deja BeginAlignment y EndAlignment en 0 % y el corredor interpolaría de 0 al bombeo
                // en las rectas de los extremos (−1.0417 % en el pk 100, 20/11/2026): se les pone el bombeo normal pedido
                extremos = Peralte.BombeoEnExtremos(curvas, actual);
                foreach (var b in extremos) if (b.Motivo != null) ctx.Avisos.Add(b.Motivo);
                antes = Peralte.Estado(actual);
                prevision = Peralte.Prevision(al.Name, curvas, curvasAntes);
                prevision["bombeo_en_extremos"] = extremos.Select(b => (object)b.ComoDiccionario()).ToList();
                if (ctx.Simular)
                {
                    tr.Commit();
                    ctx.Avisos.AddRange(avisosLectura);
                    return Escritura.Simulacion(ctx, antes, prevision, Peralte.Accion(nombreReal, curvas));
                }

                ctx.EsperarCopia();   // nunca se escribe sin copia terminada
                foreach (var c in curvas)
                {
                    Civ.SuperelevationCurve curvaPeralte;
                    if (c.CurvaExistente != null) curvaPeralte = CurvaPeralteExistente(al, c);
                    else
                    {
                        var arco = ArcoEnPc(al, c.ProgresivaPc.Value, c.Pi);
                        if (arco.SubEntityCount < 1) throw new InvalidOperationException("La curva del PI " + c.Pi + " no tiene subentidades (AlignmentEntity.SubEntityCount = 0): no se puede crear su curva de peralte.");
                        var sub = arco[0];
                        curvaPeralte = al.SuperelevationCurves.AddUserDefinedCurve(sub, sub);
                        if (curvaPeralte == null) throw new InvalidOperationException("Civil 3D no devolvió la curva de peralte del PI " + c.Pi + " (SuperelevationCurveCollection.AddUserDefinedCurve). No se reintenta.");
                    }
                    var estaciones = curvaPeralte.CriticalStations;
                    foreach (var e in c.Estaciones)
                    {
                        var tipo = EnumPeralte<SuperelevationCriticalStationType>(e.Tipo, "tipo de progresiva crítica");
                        if (e.Region != null) estaciones.Add(e.Progresiva, tipo, EnumPeralte<SuperelevationAttainmentRegionType>(e.Region, "tipo de región de transición"));
                        else estaciones.Add(e.Progresiva, tipo);
                        var st = estaciones.GetCriticalStationAt(e.Progresiva, Peralte.ToleranciaProgresiva);
                        if (st == null) throw new InvalidOperationException("Civil 3D no devolvió la progresiva crítica " + TablaPi.Progresiva(e.Progresiva) + " del PI " + c.Pi + " después de añadirla (GetCriticalStationAt). No se reintenta.");
                        foreach (var p in e.Pendientes)
                            st.SetSlope(EnumPeralte<SuperelevationCrossSegmentType>(p.Key, "segmento de la sección"), Peralte.PendienteApi(p.Value));
                    }
                }
                // Tarea 7.6: el bombeo normal en las progresivas de contorno, con el mismo SetSlope. Si falla, aviso y el
                // resto del peralte se escribe igual (no se verifica ese extremo).
                foreach (var b in extremos.Where(x => x.Motivo == null))
                {
                    try
                    {
                        var st = EstacionDeContornoDelEje(al, b.TipoContorno, b.Progresiva.Value);
                        if (st == null) throw new InvalidOperationException("no está en Alignment.SuperelevationCriticalStations al escribir");
                        foreach (var p in b.Pendientes)
                            st.SetSlope(EnumPeralte<SuperelevationCrossSegmentType>(p.Key, "segmento de la sección"), Peralte.PendienteApi(p.Value));
                        b.Escrito = true;
                    }
                    catch (Exception ex)
                    {
                        b.Escrito = false;
                        b.Motivo = Peralte.MensajeBombeoNoEscrito(b, Raiz(ex).Message);
                        ctx.Avisos.Add(b.Motivo);
                    }
                }
                tr.Commit();
            }

            PeralteLeido leido;
            var avisosRelectura = new HashSet<string>();
            List<Dictionary<string, object>> comprobacion;
            List<string> avisosComprobacion;
            var avisosCarriles = new List<string>();
            List<CarrilSinPeralte> carriles;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var al = (CivAlignment)tr.GetObject(BuscarAlineamiento(tr, nombreReal), OpenMode.ForRead);
                leido = LeerPeralte(al, avisosRelectura);
                // Tarea 7.4: el peralte en el centro del peralte total de cada curva, entre progresivas críticas (lo que pide
                // el corredor en cualquier pk). Solo en la escritura real; si no coincide, aviso (no error: por verificar).
                // Tarea 7.6: más un pk en la mitad de la recta inicial y otro en la de la final, que esperan el bombeo normal
                comprobacion = Peralte.ComprobarEntreProgresivas(Peralte.PuntosDeComprobacion(curvas, extremos), (pk, seg) => PendienteEnPk(al, pk, seg), out avisosComprobacion);
                // Tarea 7.6: los corredores que usan este eje con carriles que ignoran el peralte (UseSuperelevation = None)
                carriles = CarrilesSinPeralteDelEje(tr, idEje, avisosCarriles);
                tr.Commit();
            }
            var despues = Peralte.Estado(leido);
            string rutaCopia = Verificacion.RutaDeCopia(ctx.Copia);
            var diferencias = Peralte.Diferencias(curvas, leido, out var extra);
            diferencias.AddRange(Peralte.DiferenciasExtremos(extremos, leido));
            if (diferencias.Count > 0) throw new InvalidOperationException(Peralte.MensajeNoRefleja(nombreReal, diferencias, rutaCopia));
            if (extra.Count > 0)
                ctx.Avisos.Add("El eje tiene progresivas críticas que no se pidieron (Civil 3D pudo crearlas al añadir la curva de peralte): " + string.Join("; ", extra) + ". Míralas con listar_peralte.");
            if (leido.NumeroEstacionesDelEjeSinContorno != leido.NumeroEstacionesCurvas)
                ctx.Avisos.Add("Alignment.SuperelevationCriticalStations tiene " + leido.NumeroEstacionesDelEjeSinContorno + " progresivas críticas (sin las de contorno BeginAlignment y EndAlignment) y las curvas de peralte suman "
                    + leido.NumeroEstacionesCurvas + " (dato para VALIDACION_120).");
            ctx.Avisos.AddRange(avisosRelectura);
            ctx.Avisos.AddRange(avisosComprobacion);
            ctx.Avisos.AddRange(avisosCarriles);
            var datos = new Dictionary<string, object>
            {
                ["estaciones_del_eje"] = leido.EstacionesDelEje.Count,
                ["bombeo_en_extremos"] = extremos.Select(b => (object)b.ComoDiccionario(leido)).ToList(),
                ["comprobacion_entre_progresivas"] = comprobacion,
                ["carriles_sin_peralte"] = carriles.Select(c => (object)c.ComoDiccionario()).ToList(),
                ["lectura_entre_progresivas"] = TextoLecturaEntreProgresivas(),
                ["escala_pendientes"] = "% = valor de la API × " + Peralte.FactorApi.ToString(CultureInfo.InvariantCulture) + " (por verificar en Civil 3D)",
                ["deshacer"] = Peralte.BloqueDeshacer(leido, rutaCopia)
            };
            datos["curvas_de_peralte"] = curvas.Select(c => (object)new Dictionary<string, object>
            {
                ["pi"] = c.Pi,
                ["curva"] = c.CurvaExistente,
                ["origen"] = c.CurvaExistente != null ? "existente (la curva vacía que creó Civil 3D con el eje)" : "nueva (AddUserDefinedCurve)"
            }).ToList();
            return Escritura.Resultado(ctx, antes, despues, Peralte.Esperado(curvas, curvasAntes), Peralte.Accion(nombreReal, curvas), datos);
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarPeralte()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaListarPeralte,
                Contexto = ContextoEjecucion.Aplicacion,   // lectura: sin entrada en el menú Deshacer (ver CONTRATO, Contextos)
                Descripcion = "Devuelve el peralte de un alineamiento existente: 'tiene_peralte', las curvas de peralte (nombre, progresivas de inicio y fin) con sus progresivas críticas en orden (progresiva, tipo, región de transición, descripción y pendientes transversales por segmento en %), las progresivas críticas de la colección del propio eje ('estaciones_del_eje'), el tipo de peralte del eje, si usa un archivo de criterios de diseño (solo informativo) y 'enumerados': los nombres reales de los tipos de progresiva crítica, de región y de segmento de esta versión de Civil 3D, que son los que acepta aplicar_peralte. Con 'progresivas' (lista de pk) devuelve además 'pendientes_en_progresivas': la pendiente de cada segmento en esos pk, también entre progresivas críticas, como la pide el corredor (Alignment.GetCrossSlopeAtStation). Solo lee: no cambia el dibujo.",
                Parametros =
                {
                    P("alineamiento", "string", "Nombre del alineamiento (eje); ver listar_alineamientos", true),
                    P("progresivas", "json", "Opcional. Lista de progresivas del eje en metros, por ejemplo [100, 280, 550] (máximo " + Peralte.MaximoProgresivas + "): en cada una devuelve la pendiente transversal de los 8 segmentos en %, leída del eje en ese pk aunque no sea una progresiva crítica")
                },
                Ejecutar = a =>
                {
                    var doc = DocActivo();
                    string nombreAl = Requerido(a, "alineamiento").Trim();
                    var progresivas = Peralte.LeerProgresivas(JsonArg(a, "progresivas"));   // forma inválida: error sin leer el dibujo
                    var avisos = new HashSet<string>();
                    Dictionary<string, object> respuesta;
                    using (BloquearParaLeer(doc))
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var al = (CivAlignment)tr.GetObject(BuscarAlineamiento(tr, nombreAl), OpenMode.ForRead);
                        var p = LeerPeralte(al, avisos);
                        respuesta = Peralte.Estado(p);
                        respuesta["estaciones_del_eje"] = p.EstacionesDelEje.OrderBy(e => e.Progresiva).Select(e =>
                        {
                            var d = e.ComoDiccionario();
                            d["curva"] = e.Curva;
                            return (object)d;
                        }).ToList();
                        respuesta["tipo_peralte"] = Seguro(() => al.SuperelevationType.ToString(), null);
                        respuesta["usa_archivo_criterios"] = Seguro(() => (bool?)al.UseDesignCriteriaFile, null);
                        respuesta["archivo_criterios"] = Seguro(() => al.CriteriaFileName, null);
                        respuesta["escala_pendientes"] = "% = valor de la API × " + Peralte.FactorApi.ToString(CultureInfo.InvariantCulture) + " (por verificar en Civil 3D)";
                        respuesta["enumerados"] = EnumeradosPeralte();
                        if (progresivas != null)
                        {
                            // Tarea 7.4: el peralte en cualquier pk (cada lectura en su try, dentro del núcleo)
                            respuesta["pendientes_en_progresivas"] = Peralte.PendientesEnProgresivas(progresivas, al.StartingStation, al.EndingStation,
                                Enum.GetNames(typeof(SuperelevationCrossSegmentType)), (pk, seg) => PendienteEnPk(al, pk, seg));
                            respuesta["lectura_en_progresivas"] = TextoLecturaEntreProgresivas();
                        }
                        respuesta["avisos"] = avisos.ToList();
                        tr.Commit();
                    }
                    return respuesta;
                }
            });

            Registrar(new Herramienta
            {
                Nombre = HerramientaAplicarPeralte,
                Descripcion = "Pone el peralte en las curvas circulares de un eje SIN peralte con los valores que se le pasan (sin norma: con 'velocidad_diseno' y los datos del proyecto los calcula el puente con la DG-2018). Por cada curva (identificada por su número de PI de listar_pis) usa la curva de peralte vacía que Civil 3D crea con el eje para ese arco (si no la hay, crea una curva de peralte de usuario) y añade sus progresivas críticas con su tipo, su región de transición y las pendientes transversales por segmento en % (los nombres de tipo, región y segmento son los de listar_peralte → enumerados). Si el eje ya tiene peralte (alguna progresiva crítica en sus curvas de peralte, o en el eje aparte de BeginAlignment y EndAlignment) responde error sin tocar nada: no borra ni reemplaza el que hay. Comprueba que los PI existen y tienen curva y que las progresivas están dentro del eje y no se repiten. Pone además en las progresivas de contorno del eje (BeginAlignment y EndAlignment, que Civil 3D deja en 0 %) el bombeo normal de la primera EndNormalCrown y de la última BeginNormalCrown pedidas, para que las rectas de los extremos no salgan casi planas. Tras escribir relee el peralte y verifica cada progresiva y pendiente pedida y el bombeo de los extremos, comprueba el peralte entre progresivas (centro de cada curva y mitad de las rectas inicial y final) y avisa si un corredor que usa el eje tiene carriles con UseSuperelevation = None ('carriles_sin_peralte'). Una sola entrada en el menú Deshacer; copia de seguridad previa. Simula primero (simular=true).",
                Parametros =
                {
                    P("alineamiento", "string", "Nombre del alineamiento (eje) sin peralte; ver listar_alineamientos", true),
                    P("curvas", "json", "Lista de curvas: [{\"pi\": 2, \"estaciones\": [{\"progresiva\": 105.2, \"tipo\": \"<tipo de progresiva crítica>\", \"region\": \"<región de transición>\", \"pendientes\": {\"<segmento>\": -2.0, \"<segmento>\": -2.0}}]}]. 'pi': número de PI de listar_pis (debe tener curva circular). 'tipo', 'region' (opcional) y los segmentos: nombres exactos de listar_peralte → enumerados. Pendientes en % con signo", true),
                    P("simular", "boolean", "Con true comprueba el pedido contra el eje y devuelve el peralte previsto sin tocar el dibujo (ni copia)")
                },
                Ejecutar = a => Escritura.Ejecutar(HerramientaAplicarPeralte, a, ctx =>
                {
                    string nombreAl = Requerido(a, "alineamiento").Trim();
                    var curvas = Peralte.LeerCurvas(JsonArg(a, "curvas"));
                    return AplicarPeralte(ctx, nombreAl, curvas);
                })
            });
        }
    }
}
