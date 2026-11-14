using System;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// Volúmenes de movimiento de tierras. Desde 1.14.0: calcular_volumenes, la séptima herramienta de creación del flujo
    /// vial, por el camino común Escritura.CrearObjeto. En UNA transacción (una entrada de Deshacer) crea una superficie de
    /// volumen TIN (TinVolumeSurface.Create) con base el terreno y comparación la superficie Datum del corredor, le pone
    /// los factores (CutFactor = factor_esponjamiento, FillFactor = factor_compactacion) y la reconstruye; relee
    /// GetVolumeProperties y devuelve corte, relleno y neto (neto = corte − relleno, convención del plugin; el signo del
    /// neto de Civil 3D va aparte). Además, si hay un grupo de líneas de muestreo con secciones de las dos superficies
    /// (crear_secciones), calcula la tabla por progresivas LEYENDO esas secciones (Section.SectionPoints) y aplicando
    /// áreas medias en ArbaMcp.Nucleo.Volumenes: no crea listas de materiales ni usa ejecutar_comando. Vía alternativa no
    /// usada (propuesta para una 1.14.x): las listas de materiales de la API (SampleLineGroup.MaterialLists,
    /// QTOMaterialList, GetTotalVolumeResultDataForMaterialList), ver miembros_por_verificar_civil3d.md, sección 1.14.0.
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaCalcularVolumenes = "calcular_volumenes";

        /// <summary>Un grupo de líneas de muestreo candidato para la tabla por progresivas.</summary>
        private sealed class GrupoTabla
        {
            public ObjectId Id;
            public string Nombre;
            public ObjectId IdEje;
            public string Eje;
            public int Lineas;
        }

        // ------------------------------------------------------------------ lecturas para elegir superficies
        /// <summary>Superficies del dibujo para la elección (tipo y corredor que la genera) y sus ids por nombre.</summary>
        private static List<SuperficieVolumen> SuperficiesParaVolumen(Transaction tr, Dictionary<string, ObjectId> idsPorNombre)
        {
            var porId = SuperficiesDeCorredores(tr, out var porNombre);
            var lista = new List<SuperficieVolumen>();
            foreach (ObjectId id in CivilApplication.ActiveDocument.GetSurfaceIds())
            {
                if (!(tr.GetObject(id, OpenMode.ForRead) is CivSurface su)) continue;
                string corredor = porId.TryGetValue(id, out string c) ? c : porNombre.TryGetValue(su.Name, out string c2) ? c2 : null;
                lista.Add(new SuperficieVolumen(su.Name, TipoSuperficie(su), corredor));
                if (!idsPorNombre.ContainsKey(su.Name)) idsPorNombre[su.Name] = id;
            }
            return lista;
        }

        /// <summary>
        /// Corredores del dibujo (nombre → id) y sus superficies de corredor con los códigos de vínculo; 'idsDibujo' recibe
        /// la superficie del dibujo de cada una (CorridorSurface.SurfaceId) por nombre de la superficie de corredor.
        /// </summary>
        private static List<SuperficieDeCorredorVolumen> SuperficiesDeCorredorParaVolumen(Transaction tr, Dictionary<string, ObjectId> corredores, Dictionary<string, ObjectId> idsDibujo)
        {
            var lista = new List<SuperficieDeCorredorVolumen>();
            foreach (ObjectId idCor in CivilApplication.ActiveDocument.CorridorCollection)
            {
                if (!(tr.GetObject(idCor, OpenMode.ForRead) is Civ.Corridor cor)) continue;
                corredores[cor.Name] = idCor;
                try
                {
                    foreach (Civ.CorridorSurface cs in cor.CorridorSurfaces)
                    {
                        ObjectId idSu = Seguro(() => cs.SurfaceId, ObjectId.Null);
                        if (!idSu.IsNull && !idsDibujo.ContainsKey(cs.Name)) idsDibujo[cs.Name] = idSu;
                        lista.Add(new SuperficieDeCorredorVolumen
                        {
                            Corredor = cor.Name,
                            Nombre = cs.Name,
                            CodigosEnlace = Codigos(cs, "enlace") ?? new List<string>(),
                            TieneSuperficie = !idSu.IsNull
                        });
                    }
                }
                catch (Exception ex) { Historial.Registrar(HerramientaCalcularVolumenes + ": no se pudieron leer las superficies del corredor " + cor.Name + ": " + ex.Message); }
            }
            return lista;
        }

        // ------------------------------------------------------------------ tabla por progresivas
        /// <summary>Grupos de líneas de muestreo de esos ejes (con su número de líneas).</summary>
        private static List<GrupoTabla> GruposDeEjes(Transaction tr, IEnumerable<ObjectId> ejes)
        {
            var lista = new List<GrupoTabla>();
            foreach (ObjectId idAl in ejes.Distinct())
            {
                if (!(tr.GetObject(idAl, OpenMode.ForRead) is CivAlignment al)) continue;
                foreach (ObjectId idG in al.GetSampleLineGroupIds())
                    if (tr.GetObject(idG, OpenMode.ForRead) is Civ.SampleLineGroup g)
                        lista.Add(new GrupoTabla { Id = idG, Nombre = g.Name, IdEje = idAl, Eje = al.Name, Lineas = Seguro(() => g.GetSampleLineIds().Count, 0) });
            }
            return lista;
        }

        /// <summary>Una superficie cuya sección se busca en las líneas: su id en el dibujo, su nombre y el corredor que la genera (null si no es de corredor).</summary>
        private sealed class SuperficieTabla
        {
            public ObjectId Id;
            public string Nombre;
            public string Corredor;
            public SuperficieBuscada Buscada => new SuperficieBuscada(ClaveId(Id), Nombre, Corredor);
        }

        /// <summary>Un ObjectId como texto para ArbaMcp.Nucleo.IdentificacionSecciones (su handle); null si está vacío.</summary>
        private static string ClaveId(ObjectId id) => id.IsNull ? null : id.Handle.ToString();

        /// <summary>Las secciones de una línea con la del terreno y la de la Datum identificadas (1.14.2).</summary>
        private sealed class SeccionesLeidas
        {
            public List<Civ.Section> Secciones = new List<Civ.Section>();
            public List<SeccionCandidata> Candidatas = new List<SeccionCandidata>();
            public ObjectId GetTerreno = ObjectId.Null, GetDatum = ObjectId.Null;
            public SeccionIdentificada IdTerreno = new SeccionIdentificada(), IdDatum = new SeccionIdentificada();
            public Civ.Section Terreno => IdTerreno.Encontrada ? Secciones[IdTerreno.Indice] : null;
            public Civ.Section Datum => IdDatum.Encontrada ? Secciones[IdDatum.Indice] : null;
        }

        /// <summary>Una línea del grupo con sus secciones identificadas.</summary>
        private sealed class LineaLeida
        {
            public string Nombre;
            public double Pk;
            public Civ.SampleLine Linea;
            public SeccionesLeidas Secciones;
        }

        /// <summary>
        /// Secciones de una línea y cuál es la del terreno y cuál la de la Datum (desde 1.14.2, ArbaMcp.Nucleo.IdentificacionSecciones):
        /// 1) SampleLine.GetSectionId(id de la superficie), 2) Section.CorridorSurfaceId, 3) Section.SourceId, 4) SourceName igual
        /// al nombre, 5) SourceName "&lt;corredor&gt; &lt;superficie&gt;". Cada lectura en su Seguro: si un miembro no existe o lanza
        /// (GetSectionId con una superficie que no es fuente muestreada del grupo), se pasa al intento siguiente sin aviso.
        /// No se usa SectionSource.GetSectionIds: llegar a él exige SampleLineGroup.GetSectionSources con el grupo abierto
        /// para escritura, imposible con el bloqueo de lectura de la simulación (con el grupo en lectura Civil 3D 2027 aborta).
        /// </summary>
        private static SeccionesLeidas LeerSeccionesDeLinea(Transaction tr, Civ.SampleLine sl, SuperficieTabla t, SuperficieTabla d)
        {
            var r = new SeccionesLeidas();
            try
            {
                foreach (ObjectId idS in sl.GetSectionIds())
                {
                    if (!(tr.GetObject(idS, OpenMode.ForRead) is Civ.Section s)) continue;
                    r.Secciones.Add(s);
                    r.Candidatas.Add(new SeccionCandidata(ClaveId(idS), ClaveId(Seguro(() => s.SourceId, ObjectId.Null)),
                        ClaveId(Seguro(() => s.CorridorSurfaceId, ObjectId.Null)), Seguro(() => s.SourceName, null)));
                }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCalcularVolumenes + ": no se pudieron leer las secciones de " + Seguro(() => sl.Name, "?") + ": " + ex.Message); }
            r.GetTerreno = Seguro(() => sl.GetSectionId(t.Id), ObjectId.Null);
            r.GetDatum = Seguro(() => sl.GetSectionId(d.Id), ObjectId.Null);
            (r.IdTerreno, r.IdDatum) = IdentificacionSecciones.IdentificarLasDos(r.Candidatas, t.Buscada, ClaveId(r.GetTerreno), d.Buscada, ClaveId(r.GetDatum));
            return r;
        }

        /// <summary>Todas las líneas del grupo con sus secciones identificadas.</summary>
        private static List<LineaLeida> LeerLineas(Transaction tr, GrupoTabla g, SuperficieTabla t, SuperficieTabla d)
        {
            var slg = (Civ.SampleLineGroup)tr.GetObject(g.Id, OpenMode.ForRead);
            var lineas = new List<LineaLeida>();
            foreach (ObjectId idL in slg.GetSampleLineIds())
            {
                if (!(tr.GetObject(idL, OpenMode.ForRead) is Civ.SampleLine sl)) continue;
                lineas.Add(new LineaLeida { Nombre = Seguro(() => sl.Name, null), Pk = Seguro(() => sl.Station, double.NaN), Linea = sl, Secciones = LeerSeccionesDeLinea(tr, sl, t, d) });
            }
            return lineas;
        }

        /// <summary>True si alguna línea del grupo tiene sección de las dos superficies.</summary>
        private static bool GrupoMuestreaLasDos(Transaction tr, GrupoTabla g, SuperficieTabla t, SuperficieTabla d)
        {
            try
            {
                var slg = (Civ.SampleLineGroup)tr.GetObject(g.Id, OpenMode.ForRead);
                foreach (ObjectId idL in slg.GetSampleLineIds())
                {
                    if (!(tr.GetObject(idL, OpenMode.ForRead) is Civ.SampleLine sl)) continue;
                    var s = LeerSeccionesDeLinea(tr, sl, t, d);
                    if (s.Terreno != null && s.Datum != null) return true;
                }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCalcularVolumenes + ": no se pudieron leer las secciones del grupo " + g.Nombre + ": " + ex.Message); }
            return false;
        }

        /// <summary>Nombre y tipo del objeto de un id leído de una sección (null si está vacío), para el diagnóstico.</summary>
        private static object DescribirIdSeccion(Transaction tr, ObjectId id, SuperficieTabla t, SuperficieTabla d)
        {
            if (id.IsNull) return null;
            return new
            {
                nombre = NombreDe(tr, id),
                tipo = Seguro(() => tr.GetObject(id, OpenMode.ForRead).GetType().Name, null),
                es_el_terreno = id == t.Id,
                es_la_datum = id == d.Id
            };
        }

        /// <summary>Lo que devolvió SampleLine.GetSectionId para una superficie: null si nada (o lanzó); si no, si es una sección de la línea y su SourceName.</summary>
        private static object DescribirDevueltaPorGetSectionId(Transaction tr, ObjectId id, SeccionesLeidas s)
        {
            if (id.IsNull) return null;
            int i = s.Candidatas.FindIndex(c => string.Equals(c.Clave, ClaveId(id), StringComparison.OrdinalIgnoreCase));
            return new
            {
                de_la_linea = i >= 0,
                source_name = i >= 0 ? s.Candidatas[i].NombreFuente : Seguro(() => (tr.GetObject(id, OpenMode.ForRead) as Civ.Section)?.SourceName, null)
            };
        }

        /// <summary>
        /// Bloque identificacion_secciones (1.14.2, para la memoria técnica): con qué intento se encontró la sección del terreno
        /// y la de la Datum en cada línea del grupo y, de la primera línea con secciones, los valores crudos de cada sección
        /// (SourceName, SourceType, el objeto de SourceId y de CorridorSurfaceId) y lo que devolvió GetSectionId.
        /// </summary>
        private static object DiagnosticoIdentificacion(Transaction tr, GrupoTabla g, List<LineaLeida> lineas, SuperficieTabla t, SuperficieTabla d)
        {
            var rt = new RecuentoIdentificacion();
            var rd = new RecuentoIdentificacion();
            foreach (var l in lineas) { rt.Anotar(l.Secciones.IdTerreno); rd.Anotar(l.Secciones.IdDatum); }
            var p = lineas.FirstOrDefault(l => l.Secciones.Secciones.Count > 0) ?? lineas.FirstOrDefault();
            return new
            {
                grupo = g.Nombre,
                alineamiento = g.Eje,
                orden = IdentificacionSecciones.Orden,
                terreno = new { superficie = t.Nombre, corredor = t.Corredor, metodos = rt.Metodos, lineas = rt.Lineas, lineas_sin_seccion = rt.SinSeccion },
                datum = new { superficie = d.Nombre, corredor = d.Corredor, metodos = rd.Metodos, lineas = rd.Lineas, lineas_sin_seccion = rd.SinSeccion },
                primera_linea = p == null ? null : new
                {
                    linea = p.Nombre,
                    pk = double.IsNaN(p.Pk) ? (double?)null : N(p.Pk),
                    get_section_id = new
                    {
                        terreno = DescribirDevueltaPorGetSectionId(tr, p.Secciones.GetTerreno, p.Secciones),
                        datum = DescribirDevueltaPorGetSectionId(tr, p.Secciones.GetDatum, p.Secciones)
                    },
                    secciones = p.Secciones.Secciones.Select((s, i) => (object)new
                    {
                        source_name = p.Secciones.Candidatas[i].NombreFuente,
                        source_type = Seguro(() => s.SourceType.ToString(), null),
                        source_id = DescribirIdSeccion(tr, Seguro(() => s.SourceId, ObjectId.Null), t, d),
                        corridor_surface_id = DescribirIdSeccion(tr, Seguro(() => s.CorridorSurfaceId, ObjectId.Null), t, d),
                        es = i == p.Secciones.IdTerreno.Indice ? "terreno" : i == p.Secciones.IdDatum.Indice ? "datum" : null
                    }).ToList()
                }
            };
        }

        /// <summary>
        /// Los bloques identificacion_secciones y, desde 1.14.3, la comprobación del supuesto de ejes (con puntos_secciones) de
        /// un grupo, leyendo todas sus líneas; para la simulación. Nunca rompe la herramienta.
        /// </summary>
        private static object DiagnosticoDeGrupo(Transaction tr, GrupoTabla g, CivSurface terreno, CivSurface datum, SuperficieTabla t, SuperficieTabla d, out SupuestoEjes supuesto)
        {
            supuesto = null;
            try
            {
                var lineas = LeerLineas(tr, g, t, d);
                try { supuesto = ComprobarSupuestoEjes(tr, g, lineas, terreno, datum, t, d); }
                catch (Exception ex) { supuesto = new SupuestoEjes { Diagnostico = new { error = ex.Message } }; }
                return DiagnosticoIdentificacion(tr, g, lineas, t, d);
            }
            catch (Exception ex) { return new { grupo = g.Nombre, alineamiento = g.Eje, error = ex.Message }; }
        }

        private const string PistaIdentificacion = " El bloque identificacion_secciones de la respuesta dice qué sección de cada línea se tomó y con qué intento.";

        /// <summary>
        /// Grupo para la tabla: el pedido (error si no existe: se pidió expresamente) o, si no, el único grupo de los ejes
        /// del corredor (o de todos los ejes si el Datum no es de un corredor) con secciones de las dos superficies. Sin
        /// grupo o con varios, null y aviso: la tabla no bloquea los totales. 'paraDiagnostico': el grupo cuyo bloque
        /// identificacion_secciones se devuelve (el elegido o, si no hay, el primer candidato, para ver por qué).
        /// </summary>
        private static GrupoTabla ElegirGrupoTabla(Transaction tr, string pedido, List<ObjectId> ejesCorredor, SuperficieTabla t, SuperficieTabla d, List<string> avisos, out GrupoTabla paraDiagnostico)
        {
            paraDiagnostico = null;
            string nT = t.Nombre, nD = d.Nombre;
            var todosLosEjes = new List<ObjectId>();
            foreach (ObjectId id in CivilApplication.ActiveDocument.GetAlignmentIds()) todosLosEjes.Add(id);
            if (!string.IsNullOrWhiteSpace(pedido))
            {
                var todos = GruposDeEjes(tr, todosLosEjes);
                var iguales = todos.Where(g => string.Equals(g.Nombre, pedido.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (iguales.Count == 0)
                    throw new ArgumentException("No existe el grupo de líneas de muestreo '" + pedido.Trim() + "'. Grupos del dibujo: "
                        + (todos.Count == 0 ? "ninguno" : string.Join(", ", todos.Select(g => g.Nombre + " (" + g.Eje + ")"))) + ". Usa listar_lineas_muestreo o créalo con crear_secciones.");
                if (iguales.Count > 1)
                {
                    var delCorredor = iguales.Where(g => ejesCorredor.Contains(g.IdEje)).ToList();
                    if (delCorredor.Count != 1)
                        throw new ArgumentException("Hay varios grupos de líneas de muestreo llamados '" + pedido.Trim() + "': " + string.Join(", ", iguales.Select(g => g.Nombre + " (" + g.Eje + ")"))
                            + ". Renombra uno en Civil 3D o usa listar_lineas_muestreo.");
                    iguales = delCorredor;
                }
                var elegido = iguales[0];
                paraDiagnostico = elegido;
                if (!GrupoMuestreaLasDos(tr, elegido, t, d))
                    avisos.Add("El grupo '" + elegido.Nombre + "' no tiene ninguna línea con secciones de '" + nT + "' y de '" + nD + "': la tabla saldrá sin volúmenes. Revisa sus fuentes con listar_lineas_muestreo (fuentes=true)." + PistaIdentificacion);
                return elegido;
            }

            var candidatos = GruposDeEjes(tr, ejesCorredor.Count > 0 ? ejesCorredor : todosLosEjes);
            if (candidatos.Count == 0)
            {
                avisos.Add("No hay ningún grupo de líneas de muestreo en " + (ejesCorredor.Count > 0 ? "el eje del corredor" : "el dibujo") + ": sin tabla por progresivas. Créalo con crear_secciones y repite, o pasa 'grupo_muestreo'.");
                return null;
            }
            var conLasDos = candidatos.Where(g => GrupoMuestreaLasDos(tr, g, t, d)).ToList();
            paraDiagnostico = conLasDos.Count > 0 ? conLasDos[0] : candidatos[0];
            if (conLasDos.Count == 1) return conLasDos[0];
            if (conLasDos.Count == 0)
                avisos.Add("Ningún grupo de líneas de muestreo (" + string.Join(", ", candidatos.Select(g => g.Nombre))
                    + ") tiene secciones de '" + nT + "' y de '" + nD + "': sin tabla por progresivas. Crea las líneas con crear_secciones después de crear las superficies del corredor, o revisa las fuentes con listar_lineas_muestreo (fuentes=true)." + PistaIdentificacion);
            else
                avisos.Add("Hay varios grupos de líneas de muestreo con secciones de las dos superficies: " + string.Join(", ", conLasDos.Select(g => g.Nombre + " (" + g.Eje + ")"))
                    + ". Sin tabla por progresivas: pasa 'grupo_muestreo' con el que quieres usar.");
            return null;
        }

        /// <summary>Puntos de una sección: X = desplazamiento, Y = cota (supuesto que comprueba ComprobarSupuestoEjes).</summary>
        private static List<Punto2> PuntosSeccion(Civ.Section s)
        {
            var l = new List<Punto2>();
            var c = s.SectionPoints;
            for (int i = 0; i < c.Count; i++)
            {
                var p = c[i].Location;
                l.Add(new Punto2(p.X, p.Y));
            }
            return l;
        }

        /// <summary>Diferencia entre los puntos y Section.LeftOffset/RightOffset/MinmumElevation/MaximumElevation: desde 1.14.3 solo diagnóstico (aviso).</summary>
        private static string MotivoSeccion(Civ.Section s, List<Punto2> puntos, string nombre)
            => Volumenes.MotivoPuntosNoValidos(puntos,
                Seguro(() => (double?)s.LeftOffset, null), Seguro(() => (double?)s.RightOffset, null),
                Seguro(() => (double?)s.MinmumElevation, null), Seguro(() => (double?)s.MaximumElevation, null), nombre);

        /// <summary>Resultado de la comprobación del supuesto de ejes de un grupo (1.14.3).</summary>
        private sealed class SupuestoEjes
        {
            public string Motivo;
            public int Comprobadas;
            /// <summary>Bloque puntos_secciones (memoria técnica).</summary>
            public object Diagnostico;
            public List<string> Avisos = new List<string>();
        }

        /// <summary>Cota de la superficie en (pk, desplazamiento) del eje; null si el punto o la cota no se pueden leer (fuera de la TIN, FindElevationAtXY lanza).</summary>
        private static double? CotaSuperficieEn(CivAlignment al, CivSurface su, double pk, double desplazamiento)
        {
            try
            {
                double x = 0, y = 0;
                al.PointLocation(pk, desplazamiento, ref x, ref y);
                return su.FindElevationAtXY(x, y);
            }
            catch { return null; }
        }

        private static object PuntoXY(Punto2 p) => new { x = N(p.X), y = N(p.Y) };

        /// <summary>Lo que dice Civil 3D de una sección y sus puntos, para puntos_secciones.</summary>
        private static object DescribirPuntosSeccion(Civ.Section s, List<Punto2> p, string superficie)
            => new
            {
                superficie,
                left_offset = Opt(Seguro(() => (double?)s.LeftOffset, null)),
                right_offset = Opt(Seguro(() => (double?)s.RightOffset, null)),
                left_swath_width = Opt(Seguro(() => (double?)s.LeftSwathWidth, null)),
                right_swath_width = Opt(Seguro(() => (double?)s.RightSwathWidth, null)),
                minmum_elevation = Opt(Seguro(() => (double?)s.MinmumElevation, null)),
                maximum_elevation = Opt(Seguro(() => (double?)s.MaximumElevation, null)),
                numero_puntos = p.Count,
                primeros = p.Take(3).Select(PuntoXY).ToList(),
                ultimos = p.Skip(Math.Max(0, p.Count - 3)).Select(PuntoXY).ToList()
            };

        /// <summary>
        /// Comprueba el supuesto de ejes de SectionPoint.Location (X = desplazamiento, Y = cota) en las líneas del grupo
        /// (desde 1.14.3). Bloquea la tabla (Motivo): un punto fuera del ancho real de su línea de muestreo (sus vértices con
        /// Alignment.StationOffset, como AnchosLinea de crear_secciones) o, en la primera línea en que se puede, una cota de
        /// la sección del terreno o de la Datum distinta (±0,01) de la de su superficie (Surface.FindElevationAtXY en
        /// Alignment.PointLocation) en el eje y a cada lado (Volumenes.DesplazamientosDeComprobacion), lo que comprueba
        /// también el signo izquierda/derecha. Section.LeftOffset/RightOffset y las cotas mínima/máxima ya no bloquean: si
        /// no cuadran, aviso. Diagnostico: el bloque puntos_secciones de la línea comprobada (o de la primera con las dos
        /// secciones). Solo lectura.
        /// </summary>
        private static SupuestoEjes ComprobarSupuestoEjes(Transaction tr, GrupoTabla g, List<LineaLeida> lineas, CivSurface terreno, CivSurface datum, SuperficieTabla t, SuperficieTabla d)
        {
            var r = new SupuestoEjes();
            var al = (CivAlignment)tr.GetObject(g.IdEje, OpenMode.ForRead);
            bool avisoOffsets = false;
            foreach (var l in lineas.Where(x => !double.IsNaN(x.Pk)).OrderBy(x => x.Pk))
            {
                var st = l.Secciones.Terreno;
                var sd = l.Secciones.Datum;
                if (st == null || sd == null) continue;
                var pt = PuntosSeccion(st);
                var pd = PuntosSeccion(sd);
                var (izq, der) = l.Linea == null ? ((double?)null, (double?)null) : AnchosLinea(al, l.Linea);

                if (!avisoOffsets)
                {
                    string dif = MotivoSeccion(st, pt, t.Nombre) ?? MotivoSeccion(sd, pd, d.Nombre);
                    if (dif != null)
                    {
                        avisoOffsets = true;
                        r.Avisos.Add("En la línea '" + l.Nombre + "' " + dif + " (Section.LeftOffset/RightOffset y las cotas mínima y máxima): no bloquea la tabla, se anota en puntos_secciones para miembros_por_verificar_civil3d.md.");
                    }
                }

                var comparaciones = new List<ComparacionCota>();
                string fuera = Volumenes.MotivoFueraDeLaLinea(pt, izq, der, t.Nombre) ?? Volumenes.MotivoFueraDeLaLinea(pd, izq, der, d.Nombre);
                if (fuera == null && r.Comprobadas == 0)
                {
                    var areas = Volumenes.Areas(pt, pd);
                    if (areas != null)
                        foreach (double o in Volumenes.DesplazamientosDeComprobacion(areas.Desde, areas.Hasta))
                        {
                            comparaciones.Add(new ComparacionCota(t.Nombre, o, Volumenes.CotaEn(pt, o), CotaSuperficieEn(al, terreno, l.Pk, o)));
                            comparaciones.Add(new ComparacionCota(d.Nombre, o, Volumenes.CotaEn(pd, o), CotaSuperficieEn(al, datum, l.Pk, o)));
                        }
                }
                bool completa = Volumenes.ComprobacionCompleta(comparaciones, t.Nombre, d.Nombre);
                if (fuera != null || completa || r.Diagnostico == null)
                    r.Diagnostico = new
                    {
                        linea = l.Nombre,
                        pk = N(l.Pk),
                        ancho_linea = new { izquierdo = Opt(izq), derecho = Opt(der) },
                        terreno = DescribirPuntosSeccion(st, pt, t.Nombre),
                        datum = DescribirPuntosSeccion(sd, pd, d.Nombre),
                        cotas_comparadas = comparaciones.Select(c => new
                        {
                            superficie = c.Superficie,
                            desplazamiento = N(c.Desplazamiento),
                            cota_seccion = Opt(c.CotaSeccion),
                            cota_superficie = Opt(c.CotaSuperficie),
                            diferencia = Opt(c.Diferencia)
                        }).ToList(),
                        comprobacion_completa = completa
                    };
                if (fuera != null)
                {
                    r.Motivo = "en la línea '" + l.Nombre + "' (pk " + N(l.Pk) + ") " + fuera;
                    break;
                }
                if (completa)
                {
                    r.Comprobadas = 1;
                    r.Motivo = Volumenes.MotivoCotasNoCoinciden(comparaciones, l.Nombre, l.Pk);
                    if (r.Motivo != null) break;
                }
            }
            return r;
        }

        /// <summary>
        /// Lee las secciones de cada línea del grupo, comprueba el supuesto de ejes (ComprobarSupuestoEjes; si no se cumple,
        /// 'supuesto.Motivo' lo dice y la tabla no da números) y calcula sus áreas (ArbaMcp.Nucleo.Volumenes.Areas).
        /// </summary>
        private static List<SeccionLinea> SeccionesDelGrupo(Transaction tr, GrupoTabla g, CivSurface terreno, CivSurface datum, SuperficieTabla t, SuperficieTabla d,
            out SupuestoEjes supuesto, out object identificacion)
        {
            string nT = t.Nombre, nD = d.Nombre;
            var leidas = LeerLineas(tr, g, t, d);
            identificacion = DiagnosticoIdentificacion(tr, g, leidas, t, d);
            supuesto = ComprobarSupuestoEjes(tr, g, leidas, terreno, datum, t, d);
            var lineas = new List<SeccionLinea>();
            foreach (var l in leidas)
            {
                double pk = l.Pk;
                string nombre = l.Nombre;
                var st = l.Secciones.Terreno;
                var sd = l.Secciones.Datum;
                if (st == null || sd == null)
                {
                    lineas.Add(new SeccionLinea(nombre, pk, null, "falta la sección de " + (st == null ? "'" + nT + "'" : "") + (st == null && sd == null ? " y de " : "") + (sd == null ? "'" + nD + "'" : "")));
                    continue;
                }
                var areas = Volumenes.Areas(PuntosSeccion(st), PuntosSeccion(sd));
                lineas.Add(new SeccionLinea(nombre, pk, areas, areas == null ? "las secciones de '" + nT + "' y '" + nD + "' no se solapan" : null));
            }
            return lineas;
        }

        private static object TablaComoRespuesta(GrupoTabla g, TablaVolumenes t, string motivo, int comprobadas, ResumenVolumenes r)
        {
            var supuesto = new { comprobado = motivo == null && comprobadas > 0, lineas_comprobadas_con_la_superficie = comprobadas, motivo };
            if (motivo != null || t == null)
                return new { grupo = g.Nombre, alineamiento = g.Eje, metodo = Volumenes.Metodo, numeros = false, supuesto_ejes = supuesto, numero_lineas = g.Lineas, filas = (object)null, totales = (object)null };
            return new
            {
                grupo = g.Nombre,
                alineamiento = g.Eje,
                metodo = Volumenes.Metodo,
                numeros = true,
                supuesto_ejes = supuesto,
                numero_lineas = t.Filas.Count,
                tramos = t.Tramos,
                tramos_sin_volumen = t.TramosSinVolumen,
                totales = new { corte = N(t.Corte), relleno = N(t.Relleno), corte_ajustado = N(t.CorteAjustado), relleno_ajustado = N(t.RellenoAjustado), neto = N(t.Neto) },
                diferencia_con_superficie_pct = r == null ? null : new
                {
                    corte = Opt(Volumenes.DiferenciaPorcentaje(t.CorteAjustado, r.Corte)),
                    relleno = Opt(Volumenes.DiferenciaPorcentaje(t.RellenoAjustado, r.Relleno))
                },
                filas = t.Filas.Select(f => new
                {
                    linea = f.Linea,
                    pk = N(f.Progresiva),
                    area_corte = Opt(f.AreaCorte),
                    area_relleno = Opt(f.AreaRelleno),
                    volumen_corte = Opt(f.VolumenCorte),
                    volumen_relleno = Opt(f.VolumenRelleno),
                    volumen_corte_ajustado = Opt(f.VolumenCorteAjustado),
                    volumen_relleno_ajustado = Opt(f.VolumenRellenoAjustado),
                    corte_acumulado = N(f.CorteAcumulado),
                    relleno_acumulado = N(f.RellenoAcumulado),
                    masa = N(f.Masa),
                    falta = f.Falta
                }).ToList()
            };
        }

        private static double? Opt(double? v) => v.HasValue ? N(v.Value) : null;

        /// <summary>Id de la superficie del dibujo elegida: por su nombre en el dibujo o, si es de corredor y se llama distinto, por CorridorSurface.SurfaceId.</summary>
        private static ObjectId IdSuperficie(Dictionary<string, ObjectId> delDibujo, Dictionary<string, ObjectId> deCorredor, string nombre)
        {
            if (delDibujo.TryGetValue(nombre, out ObjectId id)) return id;
            if (deCorredor.TryGetValue(nombre, out id)) return id;
            throw new ArgumentException("No existe la superficie '" + nombre + "'. Usa listar_superficies.");
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarVolumenes()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaCalcularVolumenes,
                Descripcion = "Calcula los volúmenes de corte y relleno entre el terreno y la superficie Datum del corredor: crea una superficie de volumen TIN (TinVolumeSurface.Create, base = terreno, comparación = Datum), le pone factor_esponjamiento como CutFactor y factor_compactacion como FillFactor, la reconstruye y devuelve corte, relleno y neto sin ajustar y ajustados (corte ajustado = corte × esponjamiento; relleno ajustado = relleno × compactación; neto = corte − relleno: positivo sobra material, negativo falta) y el nombre de la superficie creada, todo en una sola entrada de Deshacer. Sin 'terreno' usa la única superficie TIN que no es de corredor ni de volumen; sin 'datum', la '<corredor> - Datum' del corredor pedido o del único corredor (errores claros si hay varias). Si hay un grupo de líneas de muestreo con secciones de las dos superficies (crear_secciones), añade la tabla por progresivas: áreas de corte y relleno de cada sección y volúmenes entre líneas por áreas medias, con los factores, acumulados y ordenada de masas (la calcula el plugin leyendo las secciones; no es la tabla de materiales de Civil 3D). Relee la superficie y comprueba tipo, factores, base y comparación, y que corte o relleno no sean 0 (si no, error sin reintentar). Con simular=true solo comprueba e informa (sin copia ni cambios). Si ya existe una superficie con ese nombre, error sin tocar nada.",
                Parametros =
                {
                    P("terreno", "string", "Superficie base (terreno natural). Si falta: la única superficie TIN que no es de corredor ni de volumen (ver listar_superficies)"),
                    P("datum", "string", "Superficie de comparación (la Datum del corredor). Si falta: '<corredor> - Datum' (ver estado_corredor y crear_superficies_corredor)"),
                    P("corredor", "string", "Corredor cuya superficie Datum se usa si falta 'datum'. Si falta: el único corredor del dibujo (ver listar_corredores)"),
                    P("nombre", "string", "Nombre de la superficie de volumen nueva (no debe existir). Por defecto '<corredor> - Volumen'"),
                    P("factor_esponjamiento", "number", "Factor del corte (CutFactor): el volumen excavado se multiplica por él, por ejemplo 1.25. Por defecto 1; debe ser mayor que 0 y no pasar de " + Volumenes.FactorMaximo),
                    P("factor_compactacion", "number", "Factor del relleno (FillFactor): el volumen a rellenar se multiplica por él, por ejemplo 1.10. Por defecto 1; debe ser mayor que 0 y no pasar de " + Volumenes.FactorMaximo),
                    P("estilo", "string", "Estilo de superficie del dibujo para la superficie de volumen. Si falta, el estilo por defecto del dibujo"),
                    P("grupo_muestreo", "string", "Grupo de líneas de muestreo para la tabla por progresivas (ver listar_lineas_muestreo). Si falta: el único grupo del eje del corredor con secciones de las dos superficies"),
                    P("tabla_progresivas", "boolean", "Con false no calcula la tabla por progresivas (por defecto true)"),
                    P("simular", "boolean", "Con true comprueba superficies, nombre libre, factores, estilo y grupo y devuelve lo previsto sin tocar el dibujo (ni copia)")
                },
                Ejecutar = a => Escritura.Ejecutar(HerramientaCalcularVolumenes, a, ctx =>
                {
                    double esponjamiento = Volumenes.ValidarFactor(Tiene(a, "factor_esponjamiento") ? Num(a, "factor_esponjamiento", double.NaN) : (double?)null, "factor_esponjamiento");
                    double compactacion = Volumenes.ValidarFactor(Tiene(a, "factor_compactacion") ? Num(a, "factor_compactacion", double.NaN) : (double?)null, "factor_compactacion");
                    string estilo = Str(a, "estilo");
                    bool conTabla = Bool(a, "tabla_progresivas", true);
                    string grupoPedido = Str(a, "grupo_muestreo");

                    EleccionVolumen e = null;
                    ObjectId idTerreno = ObjectId.Null, idDatum = ObjectId.Null, idEstilo = ObjectId.Null;
                    string estiloUsado = null;
                    GrupoTabla grupo = null;

                    // Lo releído
                    VolumenesCivil civil = null;
                    ResumenVolumenes resumen = null;
                    string errorResumen = null, baseLeida = null, comparacionLeida = null, estiloLeido = null;
                    int? triangulos = null;
                    Dictionary<string, object> extension = null;
                    object tabla = null;
                    // Superficies cuya sección se busca en las líneas y bloque identificacion_secciones (1.14.2)
                    SuperficieTabla supT = null, supD = null;
                    object identificacionAntes = null, identificacionDespues = null;
                    // Comprobación del supuesto de ejes y bloque puntos_secciones (1.14.3): la de la simulación y la de la escritura
                    SupuestoEjes supuestoAntes = null;
                    object puntosDespues = null;
                    var avisosTabla = new List<string>();

                    Escritura.Creacion creacion = null;
                    creacion = new Escritura.Creacion
                    {
                        // El texto definitivo (nombre, base y comparación) se completa en Antes
                        Accion = "Crear una superficie de volumen TIN",
                        Antes = tr =>
                        {
                            var idsSuperficie = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                            var corredores = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                            var idsDeCorredor = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                            var superficies = SuperficiesParaVolumen(tr, idsSuperficie);
                            var deCorredor = SuperficiesDeCorredorParaVolumen(tr, corredores, idsDeCorredor);
                            e = Volumenes.Elegir(superficies, corredores.Keys, deCorredor, Str(a, "terreno"), Str(a, "datum"), Str(a, "corredor"), Str(a, "nombre"));
                            ctx.Avisos.AddRange(e.Avisos);
                            Volumenes.ComprobarNombreLibre(e.Nombre, NombresDeSuperficiesOcupados(tr));   // también los nombres de superficies de corredor
                            idTerreno = IdSuperficie(idsSuperficie, idsDeCorredor, e.Terreno);
                            idDatum = IdSuperficie(idsSuperficie, idsDeCorredor, e.Datum);
                            foreach (var (parametro, valor) in new[] { ("factor_esponjamiento", esponjamiento), ("factor_compactacion", compactacion) })
                            {
                                string av = Volumenes.AvisoFactor(valor, parametro);
                                if (av != null) ctx.Avisos.Add(av);
                            }

                            // Superficies o corredor desactualizados: se avisa, no se reconstruye nada ajeno
                            var terreno = (CivSurface)tr.GetObject(idTerreno, OpenMode.ForRead);
                            var datum = (CivSurface)tr.GetObject(idDatum, OpenMode.ForRead);
                            foreach (var su in new[] { terreno, datum })
                                if (Seguro(() => Desactualizada(su), null) == true)
                                    ctx.Avisos.Add("La superficie '" + su.Name + "' está desactualizada: el volumen se calcula con lo que tiene ahora. Reconstrúyela (reconstruir_superficie o reconstruir_corredor) y repite si quieres el valor al día.");
                            var ejesCorredor = new List<ObjectId>();
                            if (e.Corredor != null && corredores.TryGetValue(e.Corredor, out ObjectId idCor) && tr.GetObject(idCor, OpenMode.ForRead) is Civ.Corridor cor)
                            {
                                if (Seguro(() => cor.IsOutOfDate, false))
                                    ctx.Avisos.Add("El corredor '" + cor.Name + "' está desactualizado: su superficie Datum puede no reflejar el diseño actual. Reconstrúyelo con reconstruir_corredor y repite.");
                                try { foreach (Civ.Baseline bl in cor.Baselines) ejesCorredor.Add(bl.AlignmentId); }
                                catch (Exception ex) { Historial.Registrar(HerramientaCalcularVolumenes + ": no se pudieron leer las líneas base de " + cor.Name + ": " + ex.Message); }
                            }

                            if (!string.IsNullOrWhiteSpace(estilo))
                            {
                                idEstilo = BuscarEstiloSuperficie(tr, estilo.Trim());   // error con la lista (DBObject.Name) si no existe
                                estiloUsado = NombreEstilo(tr, idEstilo) ?? estilo.Trim();
                            }

                            var corredorDe = SuperficiesDeCorredores(tr, out _);
                            supT = new SuperficieTabla { Id = idTerreno, Nombre = e.Terreno, Corredor = corredorDe.TryGetValue(idTerreno, out string corT) ? corT : null };
                            supD = new SuperficieTabla { Id = idDatum, Nombre = e.Datum, Corredor = e.Corredor ?? (corredorDe.TryGetValue(idDatum, out string corD) ? corD : null) };
                            if (conTabla)
                            {
                                grupo = ElegirGrupoTabla(tr, grupoPedido, ejesCorredor, supT, supD, avisosTabla, out GrupoTabla paraDiagnostico);
                                if (paraDiagnostico != null) identificacionAntes = DiagnosticoDeGrupo(tr, paraDiagnostico, terreno, datum, supT, supD, out supuestoAntes);
                                if (ctx.Simular && supuestoAntes != null)
                                {
                                    ctx.Avisos.AddRange(supuestoAntes.Avisos);
                                    if (grupo != null && supuestoAntes.Motivo != null)
                                        ctx.Avisos.Add("La tabla por progresivas saldrá sin números: " + supuestoAntes.Motivo + ". Mira puntos_secciones; los totales de la superficie de volumen no dependen de esto.");
                                }
                            }
                            else if (!string.IsNullOrWhiteSpace(grupoPedido)) ctx.Avisos.Add("Con tabla_progresivas=false no se calcula la tabla: 'grupo_muestreo' no se usa.");
                            ctx.Avisos.AddRange(avisosTabla);
                            avisosTabla.Clear();

                            creacion.Accion = Volumenes.Accion(e.Nombre, e.Terreno, e.Datum, esponjamiento, compactacion)
                                + (grupo != null ? ", con la tabla por progresivas del grupo '" + grupo.Nombre + "'" : "");
                            return new Dictionary<string, object> { ["superficie"] = e.Nombre, ["existe"] = false };
                        },
                        Prevision = tr =>
                        {
                            if (ctx.Simular)
                                ctx.Avisos.Add("Corte, relleno, neto y la tabla por progresivas se calculan al crear la superficie de volumen: la simulación solo dice qué superficies, factores y grupo se usarán.");
                            return new Dictionary<string, object>
                            {
                                ["existe"] = true,
                                ["tipo"] = Volumenes.TipoVolumenTin,
                                ["superficie_base"] = e.Terreno,
                                ["superficie_comparacion"] = e.Datum,
                                ["factor_corte"] = esponjamiento,
                                ["factor_relleno"] = compactacion,
                                ["estilo"] = estiloUsado ?? "(estilo por defecto del dibujo)",
                                ["grupo_muestreo"] = grupo?.Nombre,
                                ["lineas_tabla"] = grupo?.Lineas ?? 0,
                                // La simulación no devuelve 'datos': el diagnóstico va en 'despues' (1.14.2)
                                ["identificacion_secciones"] = ctx.Simular ? identificacionAntes : null,
                                // 1.14.3: lo que dirá la tabla del supuesto de ejes y los puntos de las secciones, ya en la simulación
                                ["supuesto_ejes"] = ctx.Simular && supuestoAntes != null
                                    ? new { comprobado = supuestoAntes.Motivo == null && supuestoAntes.Comprobadas > 0, lineas_comprobadas_con_la_superficie = supuestoAntes.Comprobadas, motivo = supuestoAntes.Motivo }
                                    : null,
                                ["puntos_secciones"] = ctx.Simular ? supuestoAntes?.Diagnostico : null
                            };
                        },
                        Esperado = new Dictionary<string, object>
                        {
                            ["existe"] = true,
                            ["tipo"] = Volumenes.TipoVolumenTin,
                            ["factor_corte"] = esponjamiento,
                            ["factor_relleno"] = compactacion
                        },
                        Crear = tr =>
                        {
                            ObjectId id;
                            try
                            {
                                id = idEstilo.IsNull
                                    ? Civ.TinVolumeSurface.Create(e.Nombre, idTerreno, idDatum)
                                    : Civ.TinVolumeSurface.Create(e.Nombre, idTerreno, idDatum, idEstilo);
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException("No se pudo crear la superficie de volumen '" + e.Nombre + "' (TinVolumeSurface.Create: " + ex.Message + "). Nada queda escrito (la transacción se descarta) y no se reintenta.", ex);
                            }
                            if (id.IsNull) return id;
                            try
                            {
                                var vs = (Civ.TinVolumeSurface)tr.GetObject(id, OpenMode.ForWrite);
                                vs.CutFactor = esponjamiento;
                                vs.FillFactor = compactacion;
                                vs.Rebuild();
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException("No se pudieron poner los factores o reconstruir la superficie de volumen '" + e.Nombre + "' (TinVolumeSurface.CutFactor/FillFactor/Rebuild: " + ex.Message + "). Nada queda escrito (la transacción se descarta) y no se reintenta.", ex);
                            }
                            return id;
                        },
                        Despues = (tr, id) =>
                        {
                            var su = (CivSurface)tr.GetObject(id, OpenMode.ForRead);
                            var vs = su as Civ.TinVolumeSurface;
                            Civ.VolumeSurfaceProperties vp = null;
                            if (vs != null)
                            {
                                try { vp = vs.GetVolumeProperties(); }
                                catch (Exception ex) { errorResumen = "TinVolumeSurface.GetVolumeProperties: " + ex.Message; }
                            }
                            civil = new VolumenesCivil
                            {
                                CorteSinAjustar = vp == null ? null : Seguro(() => (double?)vp.UnadjustedCutVolume, null),
                                RellenoSinAjustar = vp == null ? null : Seguro(() => (double?)vp.UnadjustedFillVolume, null),
                                NetoSinAjustar = vp == null ? null : Seguro(() => (double?)vp.UnadjustedNetVolume, null),
                                CorteAjustado = vp == null ? null : Seguro(() => (double?)vp.AdjustedCutVolume, null),
                                RellenoAjustado = vp == null ? null : Seguro(() => (double?)vp.AdjustedFillVolume, null),
                                NetoAjustado = vp == null ? null : Seguro(() => (double?)vp.AdjustedNetVolume, null),
                                FactorCorte = vs == null ? null : Seguro(() => (double?)vs.CutFactor, null),
                                FactorRelleno = vs == null ? null : Seguro(() => (double?)vs.FillFactor, null)
                            };
                            if (vp != null)
                            {
                                baseLeida = NombreDe(tr, Seguro(() => vp.BaseSurface, ObjectId.Null));
                                comparacionLeida = NombreDe(tr, Seguro(() => vp.ComparisonSurface, ObjectId.Null));
                            }
                            if (errorResumen == null)
                            {
                                try { resumen = Volumenes.Resumir(civil, esponjamiento, compactacion); }
                                catch (Exception ex) { errorResumen = ex.Message; }
                            }
                            triangulos = vs == null ? null : Seguro(() => (int?)vs.GetTinProperties().NumberOfTriangles, null);
                            try { extension = ExtensionDe(su, out _); }
                            catch (Exception ex) { ctx.Avisos.Add("No se pudo leer la extensión de la superficie de volumen (Surface.GetGeneralProperties): " + ex.Message); }
                            estiloLeido = Api.Leer<string>(su, estiloUsado, "StyleName") ?? estiloUsado;

                            // Tabla por progresivas (solo lectura de las secciones; nunca rompe la herramienta)
                            if (grupo != null)
                            {
                                try
                                {
                                    var terreno = (CivSurface)tr.GetObject(idTerreno, OpenMode.ForRead);
                                    var datumSu = (CivSurface)tr.GetObject(idDatum, OpenMode.ForRead);
                                    var lineas = SeccionesDelGrupo(tr, grupo, terreno, datumSu, supT, supD, out SupuestoEjes supuesto, out identificacionDespues);
                                    string motivo = supuesto.Motivo;
                                    int comprobadas = supuesto.Comprobadas;
                                    puntosDespues = supuesto.Diagnostico;
                                    avisosTabla.AddRange(supuesto.Avisos);
                                    TablaVolumenes t = null;
                                    if (motivo != null)
                                        avisosTabla.Add("La tabla por progresivas sale sin números: " + motivo + ". Anótalo en miembros_por_verificar_civil3d.md (SectionPoint.Location) para corregir la herramienta; los totales de la superficie de volumen no dependen de esto.");
                                    else
                                    {
                                        t = Volumenes.Tabla(lineas, esponjamiento, compactacion);
                                        avisosTabla.AddRange(t.Avisos);
                                        if (comprobadas == 0)
                                            avisosTabla.Add("No se pudo comprobar con las superficies '" + e.Terreno + "' y '" + e.Datum + "' que los puntos de sección son (desplazamiento, cota): en ninguna línea se pudieron comparar las cotas de las dos secciones con las de sus superficies (mira puntos_secciones). Revisa la tabla con las vistas de sección.");
                                    }
                                    tabla = TablaComoRespuesta(grupo, t, motivo, comprobadas, resumen);
                                    if (t != null && resumen != null)
                                    {
                                        double? dc = Volumenes.DiferenciaPorcentaje(t.CorteAjustado, resumen.Corte), dr = Volumenes.DiferenciaPorcentaje(t.RellenoAjustado, resumen.Relleno);
                                        if ((dc.HasValue && Math.Abs(dc.Value) > 10) || (dr.HasValue && Math.Abs(dr.Value) > 10))
                                            avisosTabla.Add("La tabla por progresivas difiere más de un 10 % de la superficie de volumen (corte " + (dc.HasValue ? N(dc.Value) + " %" : "—") + ", relleno " + (dr.HasValue ? N(dr.Value) + " %" : "—")
                                                + "): es normal si el grupo no cubre todo el corredor o las líneas están muy separadas (áreas medias). Manda la superficie de volumen.");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    tabla = null;
                                    avisosTabla.Add("No se pudo calcular la tabla por progresivas del grupo '" + grupo.Nombre + "' (" + ex.Message + "): los totales de la superficie de volumen no dependen de ella.");
                                }
                            }

                            return new Dictionary<string, object>
                            {
                                ["superficie"] = su.Name,
                                ["existe"] = true,
                                ["tipo"] = TipoSuperficie(su),
                                ["superficie_base"] = baseLeida,
                                ["superficie_comparacion"] = comparacionLeida,
                                ["factor_corte"] = civil.FactorCorte,
                                ["factor_relleno"] = civil.FactorRelleno,
                                ["estilo"] = estiloLeido,
                                ["corte"] = resumen == null ? null : N(resumen.Corte),
                                ["relleno"] = resumen == null ? null : N(resumen.Relleno),
                                ["neto"] = resumen == null ? null : N(resumen.Neto),
                                ["triangulos"] = triangulos,
                                ["esta_desactualizada"] = Seguro(() => Desactualizada(su), null),
                                ["grupo_muestreo"] = grupo?.Nombre
                            };
                        },
                        Verificar = d =>
                        {
                            string cola = " No se reintenta. La superficie '" + e.Nombre + "' queda en el dibujo: prueba _.UNDO 1 y comprueba con listar_superficies que desapareció; si sigue, bórrala en Prospector (Superficies → " + e.Nombre + " → Eliminar). despues=" + Nucleo.Json.Serializar(d);
                            if (baseLeida != null && !string.Equals(baseLeida, e.Terreno, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("La superficie de volumen '" + e.Nombre + "' se creó con base '" + baseLeida + "' y se pidió '" + e.Terreno + "'." + cola);
                            if (comparacionLeida != null && !string.Equals(comparacionLeida, e.Datum, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException("La superficie de volumen '" + e.Nombre + "' se creó con comparación '" + comparacionLeida + "' y se pidió '" + e.Datum + "'." + cola);
                            if (errorResumen != null)
                                throw new InvalidOperationException("La superficie de volumen '" + e.Nombre + "' se creó pero no se pudieron leer sus volúmenes (" + errorResumen + ")." + cola);
                            if (Math.Abs(resumen.CorteSinAjustar) <= Volumenes.ToleranciaVolumen && Math.Abs(resumen.RellenoSinAjustar) <= Volumenes.ToleranciaVolumen)
                                throw new InvalidOperationException("La superficie de volumen '" + e.Nombre + "' se creó pero corte y relleno salen 0: '" + e.Terreno + "' y '" + e.Datum
                                    + "' no se solapan o Civil 3D no calculó el volumen (revisa con extension_superficie que se cruzan)." + cola);
                            if (baseLeida == null || comparacionLeida == null)
                                ctx.Avisos.Add("No se pudieron leer la base o la comparación de la superficie de volumen (VolumeSurfaceProperties.BaseSurface/ComparisonSurface): compruébalas en Propiedades de la superficie → Definición.");
                            if (triangulos == 0)
                                ctx.Avisos.Add("La superficie de volumen '" + e.Nombre + "' tiene 0 triángulos: revisa en Civil 3D que se ve y que el Panel de volúmenes da lo mismo.");
                            ctx.Avisos.AddRange(resumen.Avisos);
                            ctx.Avisos.AddRange(avisosTabla);
                        },
                        Extra = () => new
                        {
                            superficie_volumen = e.Nombre,
                            superficie_base = e.Terreno,
                            superficie_comparacion = e.Datum,
                            corredor = e.Corredor,
                            elegidas_automaticamente = new { terreno = e.TerrenoAutomatico, datum = e.DatumAutomatico, corredor = e.CorredorAutomatico, nombre = e.NombreAutomatico },
                            factores = new { esponjamiento, compactacion, convencion = Volumenes.Convencion },
                            corte = N(resumen.Corte),
                            relleno = N(resumen.Relleno),
                            neto = N(resumen.Neto),
                            balance = resumen.Balance,
                            texto = resumen.Texto,
                            sin_ajustar = new { corte = N(resumen.CorteSinAjustar), relleno = N(resumen.RellenoSinAjustar), neto = N(resumen.NetoSinAjustar) },
                            civil3d = new
                            {
                                corte_sin_ajustar = Opt(civil.CorteSinAjustar),
                                relleno_sin_ajustar = Opt(civil.RellenoSinAjustar),
                                neto_sin_ajustar = Opt(civil.NetoSinAjustar),
                                corte_ajustado = Opt(civil.CorteAjustado),
                                relleno_ajustado = Opt(civil.RellenoAjustado),
                                neto_ajustado = Opt(civil.NetoAjustado),
                                factor_corte = Opt(civil.FactorCorte),
                                factor_relleno = Opt(civil.FactorRelleno)
                            },
                            convencion_neto_civil3d = resumen.ConvencionNetoCivil3d,
                            unidades = Volumenes.Unidades,
                            estilo = estiloLeido ?? estiloUsado ?? "(estilo por defecto del dibujo)",
                            triangulos,
                            extension,
                            tabla_progresivas = tabla,
                            identificacion_secciones = identificacionDespues ?? identificacionAntes,
                            puntos_secciones = puntosDespues ?? supuestoAntes?.Diagnostico
                        }
                    };
                    return Escritura.CrearObjeto(ctx, creacion);
                })
            });
        }
    }
}
