using System;
using System.Collections.Generic;
using ArbaMcp.Nucleo;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.Settings;
using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivProfile = Autodesk.Civil.DatabaseServices.Profile;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Civ = Autodesk.Civil.DatabaseServices;
using CivStyles = Autodesk.Civil.DatabaseServices.Styles;

namespace ArbaMcp
{
    /// <summary>
    /// Herramientas de perfiles. Desde 1.9.0: crear_perfil_terreno_y_vista, la tercera herramienta de creación del flujo
    /// vial (tras crear_superficie_tin y crear_alineamiento_desde_polilinea), por el camino común Escritura.CrearObjeto.
    /// Crea DOS objetos en UNA sola transacción (una entrada de Deshacer): el perfil de terreno (EG) del alineamiento
    /// sobre la superficie (Profile.CreateFromSurface) y la vista de perfil de ese alineamiento en el punto de inserción
    /// (ProfileView.Create con nombre, conjunto de bandas y estilo). Si la vista falla, la transacción se descarta y el
    /// perfil no queda a medias. Después asigna el perfil a las bandas de la vista (Profile1Id/Profile2Id); si eso
    /// falla, lo dice en 'avisos' y las bandas quedan con la configuración de la plantilla.
    /// </summary>
    public static partial class Herramientas
    {
        private const string HerramientaCrearPerfilVista = "crear_perfil_terreno_y_vista";

        // ------------------------------------------------------------------ búsquedas para crear_perfil_terreno_y_vista (1.9.0)
        /// <summary>Id del perfil con ese nombre en el alineamiento o ObjectId.Null si no existe (para comprobar que un nombre está libre).</summary>
        private static ObjectId BuscarPerfilONull(Transaction tr, CivAlignment al, string nombre)
        {
            foreach (ObjectId id in al.GetProfileIds())
                if (tr.GetObject(id, OpenMode.ForRead) is CivProfile pr && string.Equals(pr.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return id;
            return ObjectId.Null;
        }

        /// <summary>Id de la vista de perfil con ese nombre en el alineamiento (Alignment.GetProfileViewIds) o ObjectId.Null si no existe.</summary>
        private static ObjectId BuscarVistaPerfilONull(Transaction tr, CivAlignment al, string nombre)
        {
            foreach (ObjectId id in al.GetProfileViewIds())
                if (tr.GetObject(id, OpenMode.ForRead) is Civ.ProfileView pv && string.Equals(pv.Name, nombre, StringComparison.OrdinalIgnoreCase))
                    return id;
            return ObjectId.Null;
        }

        private static ObjectId BuscarEstiloPerfil(Transaction tr, string nombre)
        {
            var estilos = CivilApplication.ActiveDocument.Styles.ProfileStyles;
            return BuscarEnEstilos(tr, estilos, estilos.Contains, n => estilos[n], nombre, "el estilo de perfil", "Espacio de herramientas → Configuración → Perfil → Estilos de perfil");
        }

        private static ObjectId BuscarConjuntoEtiquetasPerfil(Transaction tr, string nombre)
        {
            var conjuntos = CivilApplication.ActiveDocument.Styles.LabelSetStyles.ProfileLabelSetStyles;
            return BuscarEnEstilos(tr, conjuntos, conjuntos.Contains, n => conjuntos[n], nombre, "el conjunto de etiquetas de perfil", "Espacio de herramientas → Configuración → Perfil → Estilos de etiqueta → Conjuntos de etiquetas");
        }

        private static ObjectId BuscarEstiloVistaPerfil(Transaction tr, string nombre)
        {
            var estilos = CivilApplication.ActiveDocument.Styles.ProfileViewStyles;
            return BuscarEnEstilos(tr, estilos, estilos.Contains, n => estilos[n], nombre, "el estilo de vista de perfil", "Espacio de herramientas → Configuración → Visualización del perfil → Estilos de visualización del perfil");
        }

        private static ObjectId BuscarConjuntoBandas(Transaction tr, string nombre)
        {
            var conjuntos = CivilApplication.ActiveDocument.Styles.ProfileViewBandSetStyles;
            return BuscarEnEstilos(tr, conjuntos, conjuntos.Contains, n => conjuntos[n], nombre, "el conjunto de bandas de vista de perfil", "Espacio de herramientas → Configuración → Visualización del perfil → Estilos de banda → Conjuntos de bandas");
        }

        /// <summary>
        /// Estilo de perfil por defecto del dibujo: el de la configuración de perfiles
        /// (SettingsProfile.StyleSettings.ProfileStyleId); si no se puede leer, el primero del dibujo con aviso.
        /// </summary>
        private static ObjectId EstiloPerfilPorDefecto(Transaction tr, List<string> avisos, out string nombre)
        {
            nombre = null;
            try
            {
                var ajustes = CivilApplication.ActiveDocument.Settings.GetSettings<SettingsProfile>().StyleSettings;
                ObjectId id = ajustes.ProfileStyleId.Value;
                if (!id.IsNull) { nombre = NombreEstilo(tr, id) ?? ajustes.ProfileStyle.Value; return id; }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearPerfilVista + ": no se pudo leer el estilo de perfil por defecto (SettingsProfile.StyleSettings): " + ex.Message); }
            ObjectId primero = PrimerEstilo(CivilApplication.ActiveDocument.Styles.ProfileStyles);
            if (primero.IsNull)
                throw new ArgumentException("El dibujo no tiene ningún estilo de perfil; usa una plantilla de Civil 3D con estilos o crea uno en Espacio de herramientas → Configuración → Perfil → Estilos de perfil.");
            nombre = NombreEstilo(tr, primero);
            avisos.Add("No se pudo leer el estilo de perfil por defecto de la configuración del dibujo; se usa '" + (nombre ?? "?") + "' (el primero del dibujo). Pasa 'estilo_perfil' para elegir otro.");
            return primero;
        }

        /// <summary>
        /// Conjunto de etiquetas de perfil por defecto: el de la configuración de perfiles (ProfileLabelSetId); si no se
        /// puede leer, el primero del dibujo con aviso; si no hay ninguno, ObjectId.Null (sin etiquetas) con aviso.
        /// </summary>
        private static ObjectId ConjuntoEtiquetasPerfilPorDefecto(Transaction tr, List<string> avisos, out string nombre)
        {
            nombre = null;
            try
            {
                var ajustes = CivilApplication.ActiveDocument.Settings.GetSettings<SettingsProfile>().StyleSettings;
                ObjectId id = ajustes.ProfileLabelSetId.Value;
                if (!id.IsNull) { nombre = NombreEstilo(tr, id) ?? ajustes.ProfileLabelSet.Value; return id; }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearPerfilVista + ": no se pudo leer el conjunto de etiquetas de perfil por defecto (SettingsProfile.StyleSettings): " + ex.Message); }
            ObjectId primero = PrimerEstilo(CivilApplication.ActiveDocument.Styles.LabelSetStyles.ProfileLabelSetStyles);
            if (primero.IsNull)
            {
                avisos.Add("El dibujo no tiene conjuntos de etiquetas de perfil: el perfil se crea sin etiquetas.");
                return ObjectId.Null;
            }
            nombre = NombreEstilo(tr, primero);
            avisos.Add("No se pudo leer el conjunto de etiquetas de perfil por defecto de la configuración del dibujo; se usa '" + (nombre ?? "?") + "' (el primero del dibujo). Pasa 'conjunto_etiquetas_perfil' para elegir otro.");
            return primero;
        }

        /// <summary>
        /// Estilo de vista de perfil por defecto: el de la configuración de vistas de perfil
        /// (SettingsProfileView.Styles.ProfileViewStyleId); si no se puede leer, el primero del dibujo con aviso.
        /// </summary>
        private static ObjectId EstiloVistaPorDefecto(Transaction tr, List<string> avisos, out string nombre)
        {
            nombre = null;
            try
            {
                var ajustes = CivilApplication.ActiveDocument.Settings.GetSettings<SettingsProfileView>().Styles;
                ObjectId id = ajustes.ProfileViewStyleId.Value;
                if (!id.IsNull) { nombre = NombreEstilo(tr, id) ?? ajustes.ProfileViewStyle.Value; return id; }
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearPerfilVista + ": no se pudo leer el estilo de vista de perfil por defecto (SettingsProfileView.Styles): " + ex.Message); }
            ObjectId primero = PrimerEstilo(CivilApplication.ActiveDocument.Styles.ProfileViewStyles);
            if (primero.IsNull)
                throw new ArgumentException("El dibujo no tiene ningún estilo de vista de perfil; usa una plantilla de Civil 3D con estilos o crea uno en Espacio de herramientas → Configuración → Visualización del perfil → Estilos de visualización del perfil.");
            nombre = NombreEstilo(tr, primero);
            avisos.Add("No se pudo leer el estilo de vista de perfil por defecto de la configuración del dibujo; se usa '" + (nombre ?? "?") + "' (el primero del dibujo). Pasa 'estilo_vista' para elegir otro.");
            return primero;
        }

        /// <summary>
        /// Conjunto de bandas por defecto: el de la configuración de vistas de perfil (SettingsProfileView.Styles.
        /// ProfileViewBandStyleId) si el objeto es de verdad un conjunto de bandas (ProfileViewBandSetStyle); si no, el
        /// primero de Styles.ProfileViewBandSetStyles con aviso; si no hay ninguno, ObjectId.Null (sin bandas) con aviso.
        /// </summary>
        private static ObjectId ConjuntoBandasPorDefecto(Transaction tr, List<string> avisos, out string nombre)
        {
            nombre = null;
            try
            {
                var ajustes = CivilApplication.ActiveDocument.Settings.GetSettings<SettingsProfileView>().Styles;
                ObjectId id = ajustes.ProfileViewBandStyleId.Value;
                if (!id.IsNull && tr.GetObject(id, OpenMode.ForRead) is CivStyles.ProfileViewBandSetStyle)
                {
                    nombre = NombreEstilo(tr, id) ?? ajustes.ProfileViewBandStyle.Value;
                    return id;
                }
                Historial.Registrar(HerramientaCrearPerfilVista + ": la configuración no da un conjunto de bandas utilizable (ProfileViewBandStyleId " + (id.IsNull ? "nulo" : "no es ProfileViewBandSetStyle") + "); se usa el primero del dibujo.");
            }
            catch (Exception ex) { Historial.Registrar(HerramientaCrearPerfilVista + ": no se pudo leer el conjunto de bandas por defecto (SettingsProfileView.Styles): " + ex.Message); }
            ObjectId primero = PrimerEstilo(CivilApplication.ActiveDocument.Styles.ProfileViewBandSetStyles);
            if (primero.IsNull)
            {
                avisos.Add("El dibujo no tiene conjuntos de bandas de vista de perfil: la vista se crea sin bandas.");
                return ObjectId.Null;
            }
            nombre = NombreEstilo(tr, primero);
            avisos.Add("No se pudo leer el conjunto de bandas por defecto de la configuración del dibujo; se usa '" + (nombre ?? "?") + "' (el primero del dibujo). Pasa 'conjunto_bandas' para elegir otro.");
            return primero;
        }

        /// <summary>
        /// Capa del perfil nuevo: la capa de objetos "Perfil" de la configuración del dibujo
        /// (ObjectLayerSettings.GetObjectLayerSetting(Profile)); si no se puede leer o no existe, la capa actual, con aviso.
        /// </summary>
        private static ObjectId CapaParaPerfiles(Transaction tr, Database db, List<string> avisos)
        {
            try
            {
                var ajuste = CivilApplication.ActiveDocument.Settings.DrawingSettings.ObjectLayerSettings.GetObjectLayerSetting(ValorEnum<SettingsObjectLayerType>("Profile"));
                ObjectId idCapa = ajuste.LayerId;
                if (!idCapa.IsNull) return idCapa;
                string nombreCapa = ajuste.LayerName;
                var capas = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!string.IsNullOrWhiteSpace(nombreCapa) && capas.Has(nombreCapa)) return capas[nombreCapa];
                avisos.Add("La capa de perfiles de la configuración del dibujo (" + (string.IsNullOrWhiteSpace(nombreCapa) ? "sin nombre" : "'" + nombreCapa + "'") + ") no existe en el dibujo; el perfil va a la capa actual.");
            }
            catch (Exception ex) { avisos.Add("No se pudo leer la capa de perfiles de la configuración del dibujo (" + ex.Message + "); el perfil va a la capa actual."); }
            return db.Clayer;
        }

        /// <summary>Extensión en planta de un objeto (Entity.GeometricExtents) o null si no se puede leer.</summary>
        private static Extension2? ExtensionDe(Autodesk.AutoCAD.DatabaseServices.Entity e)
        {
            try
            {
                var ext = e.GeometricExtents;
                return new Extension2(ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y);
            }
            catch { return null; }
        }

        /// <summary>Extensión del dibujo guardada en la base (Database.Extmin/Extmax, sin recalcular) o null si no sirve.</summary>
        private static Extension2? ExtensionDibujo(Database db)
        {
            try
            {
                var ext = new Extension2(db.Extmin.X, db.Extmin.Y, db.Extmax.X, db.Extmax.Y);
                return ext.EsValida ? ext : (Extension2?)null;
            }
            catch { return null; }
        }

        /// <summary>
        /// Asigna el perfil a todas las bandas de la vista (abajo y arriba): Profile1Id y Profile2Id = el perfil de
        /// terreno, que es el único que existe. Devuelve cuántas bandas tocó; si algo falla, lo anota en avisos y
        /// devuelve -1 (las bandas quedan con la configuración de la plantilla; la vista y el perfil no se pierden).
        /// </summary>
        private static int AsignarPerfilABandas(Civ.ProfileView vista, ObjectId idPerfil, List<string> avisos)
        {
            int tocadas = 0;
            try
            {
                var bandas = vista.Bands;
                var abajo = bandas.GetBottomBandItems();
                foreach (Civ.ProfileViewBandItem b in abajo) { b.Profile1Id = idPerfil; b.Profile2Id = idPerfil; tocadas++; }
                bandas.SetBottomBandItems(abajo);
                var arriba = bandas.GetTopBandItems();
                foreach (Civ.ProfileViewBandItem b in arriba) { b.Profile1Id = idPerfil; b.Profile2Id = idPerfil; tocadas++; }
                bandas.SetTopBandItems(arriba);
                return tocadas;
            }
            catch (Exception ex)
            {
                avisos.Add("No se pudo asignar el perfil a las bandas de la vista (" + ex.GetType().Name + ": " + ex.Message + "); las bandas quedan con la configuración de la plantilla. Asígnalo en Propiedades de la vista de perfil → Bandas.");
                return -1;
            }
        }

        /// <summary>Número de bandas (abajo + arriba) de la vista, o null si no se pueden leer.</summary>
        private static int? NumeroBandas(Civ.ProfileView vista)
        {
            try { return vista.Bands.GetBottomBandItems().Count + vista.Bands.GetTopBandItems().Count; }
            catch { return null; }
        }

        // ------------------------------------------------------------------ registro
        private static void RegistrarPerfiles()
        {
            Registrar(new Herramienta
            {
                Nombre = HerramientaCrearPerfilVista,
                Descripcion = "Crea el perfil de terreno (EG) de un alineamiento sobre una superficie y la vista de perfil de ese alineamiento, los dos en una sola entrada de Deshacer y una sola transacción (si la vista falla, no queda el perfil a medias). Perfil: Profile.CreateFromSurface con el estilo y el conjunto de etiquetas de perfil del dibujo (o los pedidos). Vista: ProfileView.Create en 'punto_insercion' o, si no se pasa, automáticamente a la derecha de lo dibujado (x = extremo derecho de la extensión del alineamiento, la superficie y el dibujo + 10 % de su ancho, mínimo 50; y = extremo inferior), con el estilo de vista y el conjunto de bandas del dibujo (o los pedidos); después asigna el perfil a las bandas (si no se puede, lo dice en 'avisos'). Devuelve 'perfil' (nombre, tipo, progresivas, cotas, estilo, conjunto de etiquetas) y 'vista' (nombre, punto de inserción usado, estilo, conjunto de bandas, progresivas). Si ya existe un perfil o una vista con ese nombre en el alineamiento, error sin tocar nada.",
                Parametros =
                {
                    P("alineamiento", "string", "Nombre del alineamiento (eje) existente", true),
                    P("superficie", "string", "Nombre de la superficie existente de la que se saca el perfil de terreno", true),
                    P("nombre_perfil", "string", "Nombre del perfil nuevo. Por defecto '<superficie> - Terreno'"),
                    P("nombre_vista", "string", "Nombre de la vista de perfil nueva. Por defecto '<alineamiento> - Vista'"),
                    P("estilo_perfil", "string", "Estilo de perfil del dibujo. Si falta, el de la configuración del dibujo; si no existe, error con la lista"),
                    P("conjunto_etiquetas_perfil", "string", "Conjunto de etiquetas de perfil del dibujo. Si falta, el de la configuración del dibujo; si no existe, error con la lista"),
                    P("estilo_vista", "string", "Estilo de vista de perfil del dibujo. Si falta, el de la configuración del dibujo; si no existe, error con la lista"),
                    P("conjunto_bandas", "string", "Conjunto de bandas de vista de perfil del dibujo. Si falta, el de la configuración del dibujo; si no existe, error con la lista"),
                    P("punto_insercion", "json", "Esquina inferior izquierda de la vista: {\"x\": 1500, \"y\": 2000}. Si falta, se coloca a la derecha de lo dibujado y la respuesta devuelve el punto usado"),
                    P("simular", "boolean", "Con true comprueba alineamiento, superficie, nombres libres, estilos y conjuntos, calcula el punto de inserción y devuelve lo previsto sin tocar el dibujo (ni copia)")
                },
                Ejecutar = a => Escritura.Ejecutar(HerramientaCrearPerfilVista, a, ctx =>
                {
                    string nombreAl = Requerido(a, "alineamiento").Trim();
                    string nombreSu = Requerido(a, "superficie").Trim();
                    string nombrePerfil = PerfilVista.NombrePerfil(Str(a, "nombre_perfil"), nombreSu);
                    string nombreVista = PerfilVista.NombreVista(Str(a, "nombre_vista"), nombreAl);
                    string estiloPerfil = Str(a, "estilo_perfil"), conjuntoEtiquetas = Str(a, "conjunto_etiquetas_perfil"), estiloVista = Str(a, "estilo_vista"), conjuntoBandas = Str(a, "conjunto_bandas");
                    Punto2? puntoPedido = PerfilVista.LeerPuntoInsercion(JsonArg(a, "punto_insercion"));
                    var db = ctx.Db;

                    ObjectId idAl = ObjectId.Null, idSu = ObjectId.Null, idEstiloPerfil = ObjectId.Null, idEtiquetas = ObjectId.Null, idEstiloVista = ObjectId.Null, idBandas = ObjectId.Null, idCapa = ObjectId.Null, idVista = ObjectId.Null;
                    string nombreAlReal = nombreAl, nombreSuReal = nombreSu, estiloPerfilUsado = estiloPerfil, conjuntoUsado = conjuntoEtiquetas, estiloVistaUsado = estiloVista, bandasUsadas = conjuntoBandas, capaPerfil = null, tipoLeido = null;
                    double inicioEje = 0, finEje = 0, inicioPerfil = 0, finPerfil = 0, cotaMin = 0, cotaMax = 0, inicioVista = 0, finVista = 0;
                    Punto2 punto = default;
                    bool puntoAutomatico = !puntoPedido.HasValue;
                    int bandasAsignadas = 0;
                    int? bandasLeidas = null;

                    Dictionary<string, object> EstadoPrevisto(bool real) => new Dictionary<string, object>
                    {
                        ["alineamiento"] = nombreAlReal,
                        ["superficie"] = nombreSuReal,
                        ["perfil_existe"] = true,
                        ["vista_existe"] = true,
                        ["perfil"] = PerfilVista.RespuestaPerfil(nombrePerfil, real ? tipoLeido : "EG", real ? inicioPerfil : inicioEje, real ? finPerfil : finEje, real ? cotaMin : (double?)null, real ? cotaMax : (double?)null, estiloPerfilUsado, conjuntoUsado),
                        ["vista"] = PerfilVista.RespuestaVista(nombreVista, punto, estiloVistaUsado, bandasUsadas, real ? inicioVista : inicioEje, real ? finVista : finEje, real ? bandasLeidas : null),
                        ["punto_insercion_automatico"] = puntoAutomatico,
                        ["capa_perfil"] = capaPerfil
                    };

                    return Escritura.CrearObjeto(ctx, new Escritura.Creacion
                    {
                        Accion = "Crear el perfil de terreno '" + nombrePerfil + "' del alineamiento '" + nombreAl + "' sobre la superficie '" + nombreSu + "' y su vista de perfil '" + nombreVista + "'" + (puntoAutomatico ? " a la derecha de lo dibujado" : " en el punto pedido"),
                        Antes = tr =>
                        {
                            idAl = BuscarAlineamiento(tr, nombreAl);          // "No existe el alineamiento 'X'. Usa listar_alineamientos."
                            idSu = BuscarSuperficie(tr, nombreSu);            // "No existe la superficie 'X'. Usa listar_superficies."
                            var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                            var su = (CivSurface)tr.GetObject(idSu, OpenMode.ForRead);
                            nombreAlReal = al.Name; nombreSuReal = su.Name;
                            inicioEje = al.StartingStation; finEje = al.EndingStation;
                            if (!BuscarPerfilONull(tr, al, nombrePerfil).IsNull)
                                throw new ArgumentException("Ya existe el perfil '" + nombrePerfil + "' en el alineamiento '" + al.Name + "'. Usa listar_perfiles o pasa otro 'nombre_perfil'.");
                            if (!BuscarVistaPerfilONull(tr, al, nombreVista).IsNull)
                                throw new ArgumentException("Ya existe la vista de perfil '" + nombreVista + "' del alineamiento '" + al.Name + "'. Pasa otro 'nombre_vista' (las vistas se ven en Prospector → Alineaciones → el eje → Vistas en perfil; no hay herramienta de lectura de vistas todavía).");
                            idEstiloPerfil = string.IsNullOrWhiteSpace(estiloPerfil) ? EstiloPerfilPorDefecto(tr, ctx.Avisos, out estiloPerfilUsado) : BuscarEstiloPerfil(tr, estiloPerfil.Trim());
                            idEtiquetas = string.IsNullOrWhiteSpace(conjuntoEtiquetas) ? ConjuntoEtiquetasPerfilPorDefecto(tr, ctx.Avisos, out conjuntoUsado) : BuscarConjuntoEtiquetasPerfil(tr, conjuntoEtiquetas.Trim());
                            idEstiloVista = string.IsNullOrWhiteSpace(estiloVista) ? EstiloVistaPorDefecto(tr, ctx.Avisos, out estiloVistaUsado) : BuscarEstiloVistaPerfil(tr, estiloVista.Trim());
                            idBandas = string.IsNullOrWhiteSpace(conjuntoBandas) ? ConjuntoBandasPorDefecto(tr, ctx.Avisos, out bandasUsadas) : BuscarConjuntoBandas(tr, conjuntoBandas.Trim());
                            idCapa = CapaParaPerfiles(tr, db, ctx.Avisos);
                            try { capaPerfil = ((LayerTableRecord)tr.GetObject(idCapa, OpenMode.ForRead)).Name; } catch { capaPerfil = null; }
                            if (puntoPedido.HasValue) punto = puntoPedido.Value;
                            else
                            {
                                var total = Extension2.Unir(Extension2.Unir(ExtensionDe(al), ExtensionDe(su)), ExtensionDibujo(db));
                                punto = PerfilVista.PuntoAutomatico(total);
                                ctx.Avisos.Add(PerfilVista.ExplicacionPuntoAutomatico(total.Value, punto));
                            }
                            return new Dictionary<string, object> { ["alineamiento"] = al.Name, ["superficie"] = su.Name, ["perfil_existe"] = false, ["vista_existe"] = false };
                        },
                        Prevision = tr =>
                        {
                            ctx.Avisos.Add("Las cotas del perfil (cota_minima, cota_maxima) se calculan al crear.");
                            return EstadoPrevisto(false);
                        },
                        Esperado = new Dictionary<string, object> { ["perfil_existe"] = true, ["vista_existe"] = true },
                        Crear = tr =>
                        {
                            // 1) Perfil de terreno. Si falla, lanza y nada queda escrito.
                            ObjectId idPerfil = CivProfile.CreateFromSurface(nombrePerfil, idAl, idSu, idCapa, idEstiloPerfil, idEtiquetas);
                            if (idPerfil.IsNull) throw new InvalidOperationException("Profile.CreateFromSurface no devolvió el perfil '" + nombrePerfil + "'. Nada queda escrito y no se reintenta.");
                            // 2) Vista de perfil, en la MISMA transacción: si falla, la transacción se descarta y el perfil del paso 1 desaparece con ella.
                            try
                            {
                                idVista = Civ.ProfileView.Create(idAl, new Point3d(punto.X, punto.Y, 0), nombreVista, idBandas, idEstiloVista);
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException("Se creó el perfil pero ProfileView.Create falló al crear la vista '" + nombreVista + "' en (" + Math.Round(punto.X, 3) + ", " + Math.Round(punto.Y, 3) + "): " + ex.Message + ". La transacción se descarta: el perfil tampoco queda. No se reintenta; prueba con 'punto_insercion' u otro 'estilo_vista'/'conjunto_bandas'.", ex);
                            }
                            if (idVista.IsNull) throw new InvalidOperationException("ProfileView.Create no devolvió la vista '" + nombreVista + "'. La transacción se descarta (el perfil tampoco queda) y no se reintenta.");
                            // 3) Bandas: el perfil de terreno como fuente de todas. Si falla, aviso; la vista y el perfil se conservan.
                            var vista = (Civ.ProfileView)tr.GetObject(idVista, OpenMode.ForWrite);
                            bandasAsignadas = AsignarPerfilABandas(vista, idPerfil, ctx.Avisos);
                            return idPerfil;
                        },
                        Despues = (tr, idPerfil) =>
                        {
                            var pr = (CivProfile)tr.GetObject(idPerfil, OpenMode.ForRead);
                            inicioPerfil = pr.StartingStation; finPerfil = pr.EndingStation;
                            cotaMin = pr.ElevationMin; cotaMax = pr.ElevationMax;
                            try { tipoLeido = pr.ProfileType.ToString(); } catch { tipoLeido = null; }
                            estiloPerfilUsado = Api.Leer<string>(pr, estiloPerfilUsado, "StyleName") ?? estiloPerfilUsado;
                            try { capaPerfil = pr.Layer ?? capaPerfil; } catch { }
                            var al = (CivAlignment)tr.GetObject(idAl, OpenMode.ForRead);
                            ObjectId idV = BuscarVistaPerfilONull(tr, al, nombreVista);
                            if (idV.IsNull) idV = idVista;
                            bool vistaExiste = !idV.IsNull;
                            string nombreVistaLeido = null;
                            if (vistaExiste)
                            {
                                var pv = (Civ.ProfileView)tr.GetObject(idV, OpenMode.ForRead);
                                nombreVistaLeido = pv.Name;
                                inicioVista = pv.StationStart; finVista = pv.StationEnd;
                                try { var loc = pv.Location; punto = new Punto2(loc.X, loc.Y); } catch { }
                                estiloVistaUsado = Api.Leer<string>(pv, estiloVistaUsado, "StyleName") ?? estiloVistaUsado;
                                bandasLeidas = NumeroBandas(pv);
                            }
                            var d = EstadoPrevisto(true);
                            d["perfil_existe"] = !BuscarPerfilONull(tr, al, nombrePerfil).IsNull;
                            d["vista_existe"] = vistaExiste && string.Equals(nombreVistaLeido, nombreVista, StringComparison.OrdinalIgnoreCase);
                            return d;
                        },
                        Verificar = d =>
                        {
                            string cola = " No se reintenta; deshaz con Ctrl+Z (una sola entrada quita el perfil y la vista). despues=" + Nucleo.Json.Serializar(d);
                            string motivo = PerfilVista.MotivoPerfilNoValido(inicioPerfil, finPerfil, cotaMin, cotaMax);
                            if (motivo != null) throw new InvalidOperationException("El perfil '" + nombrePerfil + "' se creó pero " + motivo + "." + cola);
                            motivo = PerfilVista.MotivoVistaNoValida(nombreVista, inicioVista, finVista);
                            if (motivo != null) throw new InvalidOperationException("La vista de perfil '" + nombreVista + "' se creó pero " + motivo + "." + cola);
                            if (tipoLeido != null && !string.Equals(tipoLeido, "EG", StringComparison.OrdinalIgnoreCase))
                                ctx.Avisos.Add("El perfil se creó con tipo '" + tipoLeido + "' en vez de 'EG'; revisa con listar_perfiles.");
                            if (Math.Abs(inicioPerfil - inicioEje) > 0.01 || Math.Abs(finPerfil - finEje) > 0.01)
                                ctx.Avisos.Add("El perfil cubre de " + Math.Round(inicioPerfil, 4) + " a " + Math.Round(finPerfil, 4) + " y el eje va de " + Math.Round(inicioEje, 4) + " a " + Math.Round(finEje, 4) + ": la superficie no cubre todo el eje.");
                            if (bandasAsignadas > 0)
                                ctx.Avisos.Add("Perfil asignado como fuente (Profile1 y Profile2) de " + bandasAsignadas + " banda" + (bandasAsignadas == 1 ? "" : "s") + " de la vista.");
                            else if (bandasAsignadas == 0 && !idBandas.IsNull)
                                ctx.Avisos.Add("El conjunto de bandas '" + bandasUsadas + "' no tiene bandas; la vista queda sin bandas.");
                        },
                        Extra = () => new
                        {
                            alineamiento = nombreAlReal,
                            superficie = nombreSuReal,
                            perfil = PerfilVista.RespuestaPerfil(nombrePerfil, tipoLeido, inicioPerfil, finPerfil, cotaMin, cotaMax, estiloPerfilUsado, conjuntoUsado),
                            vista = PerfilVista.RespuestaVista(nombreVista, punto, estiloVistaUsado, bandasUsadas, inicioVista, finVista, bandasLeidas),
                            punto_insercion_automatico = puntoAutomatico,
                            bandas_asignadas = bandasAsignadas < 0 ? (int?)null : bandasAsignadas,
                            capa_perfil = capaPerfil,
                            progresiva_inicial_eje = N(inicioEje),
                            progresiva_final_eje = N(finEje)
                        }
                    });
                })
            });
        }
    }
}
