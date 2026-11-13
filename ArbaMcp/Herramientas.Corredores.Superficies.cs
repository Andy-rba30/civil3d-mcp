using System;
using System.Collections.Generic;
using System.Linq;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Civ = Autodesk.Civil.DatabaseServices;

namespace ArbaMcp
{
    /// <summary>
    /// Superficies de corredor para el movimiento de tierras. Desde 1.12.0: crear_superficies_corredor crea en un corredor
    /// existente la superficie Top (código de vínculo Top) y la Datum (código Datum), las dos como líneas de rotura, con
    /// corrección de voladizo (Top por los vínculos superiores, Datum por los inferiores) y un contorno exterior con la
    /// extensión del corredor, y reconstruye el corredor, todo en una sola transacción y una sola entrada de Deshacer, por
    /// el camino común CambiarCorredor (el objeto que cambia es un corredor existente). Las comprobaciones van en
    /// 'esperar', que corre igual en la simulación y en la escritura real, así que la comprobación previa de la 1.10.1
    /// valida lo mismo que la escritura. Reutiliza las llamadas de agregar_superficie_corredor y
    /// agregar_contorno_superficie_corredor sin cambiarlas. Lo que no depende de Civil 3D está en
    /// ArbaMcp.Nucleo.SuperficiesCorredor. Los valores del enumerado OverhangCorrectionType no están en el catálogo: se
    /// buscan por nombre al ejecutar (nunca se escribe un valor literal que podría no compilar con las DLL reales).
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaCrearSuperficiesCorredor = "crear_superficies_corredor";

        // ------------------------------------------------------------------ lecturas de una superficie de corredor
        // Por reflexión, para que estado_corredor (que las usa desde 1.12.0) nunca falle por ellas.

        /// <summary>Códigos de enlace de la superficie que están como línea de rotura (IsLinkCodeAsBreakLine); null si no se pueden leer.</summary>
        private static List<string> CodigosRotura(Civ.CorridorSurface cs)
        {
            var codigos = Codigos(cs, "enlace");
            if (codigos == null) return null;
            var l = new List<string>();
            foreach (var c in codigos)
            {
                try
                {
                    if (!Api.IntentarInvocar(cs, out object r, new[] { "IsLinkCodeAsBreakLine" }, c)) return null;
                    if (r is bool b && b) l.Add(c);
                }
                catch { return null; }
            }
            return Ordenada(l);
        }

        /// <summary>Corrección de voladizo (OverhangCorrection) como texto; null si no se puede leer.</summary>
        private static string Voladizo(Civ.CorridorSurface cs)
        {
            if (cs == null) return null;
            try { return Api.Leer<object>(cs, null, "OverhangCorrection")?.ToString(); } catch { return null; }
        }

        /// <summary>CorridorSurface.IsBuild; null si no se puede leer.</summary>
        private static bool? Construir(Civ.CorridorSurface cs)
        {
            if (cs == null) return null;
            try { return Api.Leer<bool?>(cs, null, "IsBuild"); } catch { return null; }
        }

        /// <summary>Nombres de los contornos de la superficie (BoundaryNames), ordenados; null si no se pueden leer.</summary>
        private static List<string> NombresContornosSuperficie(Civ.CorridorSurface cs)
        {
            if (cs == null) return null;
            try { return Ordenada(cs.Boundaries.BoundaryNames()); } catch { return null; }
        }

        /// <summary>Tipo del contorno con ese nombre y si es la extensión del corredor; (null, null) si no está o no se puede leer.</summary>
        private static (string tipo, bool? esExtension) ContornoDeSuperficie(Civ.CorridorSurface cs, string nombre)
        {
            try
            {
                foreach (Civ.CorridorSurfaceBoundary b in cs.Boundaries)
                    if (string.Equals(b.Name, nombre, StringComparison.OrdinalIgnoreCase))
                        return (Seguro(() => b.BoundaryType.ToString(), null), Seguro(() => (bool?)b.IsCorridorExtents, null));
            }
            catch { }
            return (null, null);
        }

        /// <summary>Nombres de superficie ya usados en el dibujo → corredor que la genera (null si no es de un corredor).</summary>
        private static Dictionary<string, string> NombresDeSuperficiesOcupados(Transaction tr)
        {
            var porId = SuperficiesDeCorredores(tr, out var porNombre);
            var d = new Dictionary<string, string>(porNombre, StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId idS in CivilApplication.ActiveDocument.GetSurfaceIds())
            {
                if (!(tr.GetObject(idS, OpenMode.ForRead) is CivSurface su)) continue;
                if (porId.TryGetValue(idS, out string corredor)) d[su.Name] = corredor;
                else if (!d.ContainsKey(su.Name)) d[su.Name] = null;
            }
            return d;
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarSuperficiesCorredor()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaCrearSuperficiesCorredor,
                Descripcion = "Crea en un corredor que ya existe sus dos superficies de corredor para el movimiento de tierras: '<corredor> - Top' con el código de vínculo Top y '<corredor> - Datum' con el código Datum, las dos como líneas de rotura, con corrección de voladizo (Top por los vínculos superiores, TopLinks; Datum por los inferiores, BottomLinks) y un contorno exterior con la extensión del corredor ('" + SuperficiesCorredor.NombreContorno + "'), y reconstruye el corredor, todo en una sola entrada de Deshacer. Antes de tocar nada comprueba que el corredor existe, que los dos nombres están libres (ninguna superficie del dibujo ni de un corredor se llama así) y que el corredor tiene los códigos Top y Datum (Corridor.GetLinkCodes); los valores del voladizo se buscan por nombre en el enumerado OverhangCorrectionType de la instalación y, si no están, da error con los valores reales. Vía: CorridorSurfaces.Add, AddLinkCode(código, true), OverhangCorrection, Boundaries.AddCorridorExtentsBoundary y Corridor.Rebuild(). Relee el corredor y comprueba códigos, líneas de rotura, voladizo, contorno y que no quedó desactualizado (si no, error sin reintentar). Devuelve los nombres de las dos superficies y 'estado_corredor' (el mismo bloque que la herramienta estado_corredor). Con simular=true solo comprueba e informa (sin copia ni cambios). Si ya existe alguna de las dos superficies, error sin tocar nada.",
                Parametros =
                {
                    P("corredor", "string", "Nombre del corredor (ver listar_corredores)", true),
                    P("nombre_top", "string", "Nombre de la superficie Top (por defecto \"<corredor> - Top\"; no debe existir, ver listar_superficies)"),
                    P("nombre_datum", "string", "Nombre de la superficie Datum (por defecto \"<corredor> - Datum\"; no debe existir, ver listar_superficies)"),
                    P("simular", "boolean", "Con true comprueba el corredor, los nombres, los códigos Top y Datum y los valores del voladizo y devuelve lo previsto sin tocar el dibujo (ni copia)")
                },
                Ejecutar = a => Escritura.Ejecutar(HerramientaCrearSuperficiesCorredor, a, ctx =>
                {
                    string corredor = Requerido(a, "corredor").Trim();
                    string nombreTop = SuperficiesCorredor.NombreTop(corredor, Str(a, "nombre_top"));
                    string nombreDatum = SuperficiesCorredor.NombreDatum(corredor, Str(a, "nombre_datum"));
                    string accion = SuperficiesCorredor.Accion(corredor, nombreTop, nombreDatum);

                    // Lo comprobado en 'esperar' (simulación y escritura real)
                    string nombreCorredor = corredor, codigoTop = null, codigoDatum = null, voladizoTop = null, voladizoDatum = null;
                    List<string> codigosCorredor = null, valoresVoladizo = null;
                    // Lo que deja la escritura
                    long msReconstruccion = 0;
                    // Lo releído (la última lectura es la de 'despues')
                    object infoTop = null, infoDatum = null, estadoCorredor = null;
                    var avisosRelectura = new List<string>();

                    object InfoNueva(Transaction tr, Civ.CorridorSurface cs)
                    {
                        if (cs == null) return null;
                        var (tipoContorno, esExtension) = ContornoDeSuperficie(cs, SuperficiesCorredor.NombreContorno);
                        bool? construir = Construir(cs);
                        ObjectId idSu = Seguro(() => cs.SurfaceId, ObjectId.Null);
                        avisosRelectura.AddRange(SuperficiesCorredor.AvisosRelectura(cs.Name, tipoContorno, construir, !idSu.IsNull));
                        return new
                        {
                            nombre = cs.Name,
                            construir,
                            tipo_contorno = tipoContorno,
                            contorno_es_extension = esExtension,
                            superficie_dibujo = idSu.IsNull ? null : NombreDe(tr, idSu)
                        };
                    }

                    Dictionary<string, object> Leer(Transaction tr, Civ.Corridor cor)
                    {
                        nombreCorredor = cor.Name;
                        var nombres = new List<string>();
                        Civ.CorridorSurface top = null, datum = null;
                        foreach (Civ.CorridorSurface cs in cor.CorridorSurfaces)
                        {
                            nombres.Add(cs.Name);
                            if (string.Equals(cs.Name, nombreTop, StringComparison.OrdinalIgnoreCase)) top = cs;
                            else if (string.Equals(cs.Name, nombreDatum, StringComparison.OrdinalIgnoreCase)) datum = cs;
                        }
                        var d = new Dictionary<string, object> { ["superficies"] = Ordenada(nombres) };
                        SuperficiesCorredor.AnadirEstado(d, SuperficiesCorredor.PrefijoTop, top != null, Codigos(top, "enlace"), CodigosRotura(top), Voladizo(top), NombresContornosSuperficie(top));
                        SuperficiesCorredor.AnadirEstado(d, SuperficiesCorredor.PrefijoDatum, datum != null, Codigos(datum, "enlace"), CodigosRotura(datum), Voladizo(datum), NombresContornosSuperficie(datum));
                        d["esta_desactualizado"] = Seguro(() => (bool?)cor.IsOutOfDate, null);
                        avisosRelectura.Clear();
                        infoTop = InfoNueva(tr, top);
                        infoDatum = InfoNueva(tr, datum);
                        estadoCorredor = EstadoCorredorInfo(tr, cor);
                        return d;
                    }

                    Dictionary<string, object> Esperar(Transaction tr, Civ.Corridor cor)
                    {
                        // 1. Nombres libres en todo el dibujo (superficies de corredores y del dibujo)
                        SuperficiesCorredor.ValidarNombres(nombreTop, nombreDatum, NombresDeSuperficiesOcupados(tr));
                        // 2. Códigos Top y Datum en el corredor, con su grafía
                        string[] codigos;
                        try { codigos = cor.GetLinkCodes(); }
                        catch (Exception ex) when (!(ex is ArgumentException))
                        {
                            throw new InvalidOperationException("No se pudieron leer los códigos de vínculo del corredor '" + cor.Name + "' (Corridor.GetLinkCodes: " + ex.Message + "). No se crea nada; revísalo con estado_corredor y reconstruir_corredor.", ex);
                        }
                        codigosCorredor = Ordenada(codigos ?? new string[0]);
                        codigoTop = SuperficiesCorredor.CodigoDelCorredor(codigosCorredor, SuperficiesCorredor.CodigoTop, cor.Name, nombreTop);
                        codigoDatum = SuperficiesCorredor.CodigoDelCorredor(codigosCorredor, SuperficiesCorredor.CodigoDatum, cor.Name, nombreDatum);
                        // 3. Valores del voladizo, por nombre en el enumerado de esta instalación (sin adivinar)
                        valoresVoladizo = Enum.GetNames(typeof(Civ.OverhangCorrectionType)).ToList();
                        voladizoTop = SuperficiesCorredor.ValorEnumerado(valoresVoladizo, SuperficiesCorredor.VoladizoTop, SuperficiesCorredor.EnumeradoVoladizo, "corrección de voladizo de '" + nombreTop + "' por los vínculos superiores");
                        voladizoDatum = SuperficiesCorredor.ValorEnumerado(valoresVoladizo, SuperficiesCorredor.VoladizoDatum, SuperficiesCorredor.EnumeradoVoladizo, "corrección de voladizo de '" + nombreDatum + "' por los vínculos inferiores");

                        if (Seguro(() => cor.IsOutOfDate, false))
                            ctx.Avisos.Add("El corredor '" + cor.Name + "' estaba desactualizado: se reconstruye al crear las superficies.");
                        if (ctx.Simular) ctx.Avisos.Add(SuperficiesCorredor.AvisoSimulacion);

                        var superficiesAntes = new List<string>();
                        foreach (Civ.CorridorSurface cs in cor.CorridorSurfaces) superficiesAntes.Add(cs.Name);
                        return SuperficiesCorredor.Esperado(superficiesAntes, nombreTop, nombreDatum, codigoTop, codigoDatum, voladizoTop, voladizoDatum);
                    }

                    void CrearSuperficie(Civ.Corridor cor, string nombre, string codigo, string voladizo)
                    {
                        // Las mismas llamadas que agregar_superficie_corredor y agregar_contorno_superficie_corredor
                        var cs = cor.CorridorSurfaces.Add(nombre);
                        if (cs == null)
                            throw new InvalidOperationException("CorridorSurfaces.Add no devolvió la superficie '" + nombre + "'. Nada queda escrito (la transacción se descarta) y no se reintenta.");
                        cs.AddLinkCode(codigo, true);
                        cs.OverhangCorrection = (Civ.OverhangCorrectionType)Enum.Parse(typeof(Civ.OverhangCorrectionType), voladizo);
                        Civ.CorridorSurfaceBoundary contorno;
                        try { contorno = cs.Boundaries.AddCorridorExtentsBoundary(SuperficiesCorredor.NombreContorno); }
                        catch (Exception ex) when (EsErrorContornoAbierto(ex))
                        {
                            throw new InvalidOperationException("No se pudo añadir a '" + nombre + "' el contorno de la extensión del corredor (" + ex.Message + "). Nada queda escrito (la transacción se descarta) y no se reintenta. Si el talud no cierra, crea las superficies una a una con agregar_superficie_corredor y ponles el contorno con agregar_contorno_superficie_corredor tipo exterior_poligono.", ex);
                        }
                        if (contorno == null)
                        {
                            ctx.Avisos.Add("AddCorridorExtentsBoundary no devolvió el contorno de '" + nombre + "': no se pudo comprobar que sea exterior.");
                            return;
                        }
                        try
                        {
                            if (contorno.BoundaryType != Civ.CorridorSurfaceBoundaryType.OutsideBoundary)
                                contorno.BoundaryType = Civ.CorridorSurfaceBoundaryType.OutsideBoundary;
                        }
                        catch (Exception ex) { ctx.Avisos.Add("No se pudo poner como exterior el contorno '" + SuperficiesCorredor.NombreContorno + "' de '" + nombre + "' (CorridorSurfaceBoundary.BoundaryType: " + ex.Message + ")."); }
                    }

                    void Cambiar(Transaction tr, Civ.Corridor cor)
                    {
                        CrearSuperficie(cor, nombreTop, codigoTop, voladizoTop);
                        CrearSuperficie(cor, nombreDatum, codigoDatum, voladizoDatum);
                        var reloj = System.Diagnostics.Stopwatch.StartNew();
                        try { cor.Rebuild(); }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("No se pudo reconstruir el corredor '" + cor.Name + "' (Corridor.Rebuild: " + ex.Message + "). Nada queda escrito (la transacción se descarta) y no se reintenta.", ex);
                        }
                        msReconstruccion = reloj.ElapsedMilliseconds;
                    }

                    object Extra()
                    {
                        ctx.Avisos.AddRange(avisosRelectura);
                        return new
                        {
                            corredor = nombreCorredor,
                            superficie_top = nombreTop,
                            superficie_datum = nombreDatum,
                            codigo_top = codigoTop,
                            codigo_datum = codigoDatum,
                            codigos_corredor = codigosCorredor,
                            voladizo_top = voladizoTop,
                            voladizo_datum = voladizoDatum,
                            valores_voladizo = valoresVoladizo,
                            contorno = SuperficiesCorredor.NombreContorno,
                            top = infoTop,
                            datum = infoDatum,
                            reconstrucciones = 1,
                            ms_reconstruccion = msReconstruccion,
                            estado_corredor = estadoCorredor
                        };
                    }

                    return CambiarCorredor(ctx, corredor, Leer, Esperar, Cambiar, accion, Extra,
                        () => { lock (UltimaReconstruccion) UltimaReconstruccion[nombreCorredor] = DateTime.Now; });
                })
            });
        }
    }
}
