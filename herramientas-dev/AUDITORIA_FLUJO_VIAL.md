# Auditoría del flujo vial: qué hay, qué falta y por dónde seguir

Fecha: 24/10/2026. Versión auditada: **1.4.0** (44 herramientas). Documento de análisis, sin cambios de código.

**Fuentes**: `ArbaMcp/Herramientas*.cs` (registro de las 44 herramientas), `ArbaMcp/CONTRATO.md`,
`PuenteMcp/main.py`, `herramientas-dev/miembros_por_verificar_civil3d.md`, los informes de
`herramientas-dev/informes/` (1.2.2, 1.3.1, 1.3.2 y 1.3.3, todos del 28/09/2026) y la suite
`ArbaMcp/pruebas/probar_servidor.py`. La 1.4.0 (`extension_superficie`) consta como validada el 12/10/2026 en la tabla de
miembros, pero su informe (`informe_validacion_14_20261012.md`) **no está en el repositorio**.

**Cómo leer la columna "Validada"**:

- **Sí**: una persona la ejecutó en Civil 3D 2027 y el informe recoge la respuesta literal.
- **Parcial**: se ejecutó sin paso formal de validación, o solo se validó una variante.
- **No**: nunca se ha ejecutado en Civil 3D. El código compila contra los sustitutos de `CompilarSinCivil` y, en casi
  todos los casos, `InspectC3D` confirmó que los miembros de la API existen en la DLL; pero nadie ha visto que funcione.

**Cómo leer las clases de la API en la sección 2**: se nombran clases y miembros de `Autodesk.Civil.DatabaseServices`
(y alguna de `Autodesk.AutoCAD.DatabaseServices`). Marca de confianza:

- ✓ ya se usa en el plugin y está ejecutado o existe en la DLL (tabla de miembros).
- ◐ clase conocida de la API pública; el miembro o la firma exacta **se comprueba con `InspectC3D` antes de escribir
  código** y entra en la tabla de miembros como `por verificar` (regla 5 de `CLAUDE.md`).
- ✗ no hay API conocida: haría falta `ejecutar_comando` (sección 3).

El puente (`PuenteMcp/main.py`) **no aporta herramientas propias**: expone las 44 del plugin tal como las publica
`GET /tools`. Lo suyo son mecanismos: tiempos máximos por herramienta (30/120/300 s), `ms_puente`, relectura del token,
sondeo de `/ping` mientras Civil 3D arranca y la tabla de nombres retirados (vacía).

---

## 1. Herramientas existentes

### 1.1 Generales (`Herramientas.cs`, `Herramientas.Seguridad.cs`)

| Herramienta | Qué hace | Tipo | Validada en Civil 3D 2027 |
|---|---|---|---|
| `ping` | Plugin, versión, puerto, dibujo activo, hora | Lectura (Inmediato) | **Sí** (1.2.2 a 1.3.3) |
| `listar_alineamientos` | Nombre, inicio, fin, longitud y perfiles de cada eje | Lectura | **Sí** (1.2.2, 68 ejes) |
| `listar_perfiles` | Perfiles de un eje: nombre, tipo EG/FG, inicio, fin, nº de PVI | Lectura | **Sí** (1.2.2) |
| `listar_pvis` | PVI de un perfil: progresiva, cota, pendientes, curva vertical | Lectura | **Sí** (1.2.2) |
| `listar_superficies` | Nombre, tipo (TIN, Grid, volumen, corredor), desactualizada, nº de líneas de rotura y contornos, corredor que la genera | Lectura | **Sí** (1.2.2 a 1.3.3) |
| `abrir_dibujo` | Abre un `.dwg` o activa su pestaña si ya estaba abierto | Escritura de aplicación (no toca el dibujo) | **Sí** (1.2.2 a 1.3.2) |
| `ejecutar_comando` | Envía texto a la línea de comandos; espera a que termine; `undo` opcional | Escritura (último recurso) | **Sí** (REGEN, `_.LINE` con ESC, `_.UNDO 1`) |
| `leer_historial` | Últimas líneas del historial del plugin | Lectura (Inmediato) | **Sí** |
| `leer_log` | Últimas líneas de `mcp_log.jsonl` | Lectura (Inmediato) | **Sí** (1.2.2 a 1.3.2) |
| `leer_variable` | Valor de una variable de sistema | Lectura (Inmediato) | **Sí** (`CMDACTIVE`) |
| `capturar_pantalla` | PNG de la ventana principal de Civil 3D | Lectura (Inmediato) | **No** (no aparece en ningún informe) |
| `guardar_dibujo` | QSAVE por API, o "guardar como" | Escritura | **No** (el `SaveAs` subyacente sí se ejecutó en `guardar_copia`, pero no esta herramienta) |
| `guardar_copia` | Copia en `backups\` con sufijo, estado en memoria | Escritura | **Sí** (1.3.1 y 1.3.2, 71 MB en 3,8 s; también sobre dibujo sin guardar) |

### 1.2 Lectura de corredores y geometría (`Herramientas.Corredores.cs`, `Herramientas.Superficies.cs`)

| Herramienta | Qué hace | Tipo | Validada en Civil 3D 2027 |
|---|---|---|---|
| `listar_corredores` | Corredores con sus líneas base (eje, perfil, rango, nº de regiones) | Lectura | **Sí** |
| `listar_regiones` | Regiones de una línea base: rango, ensamblaje, frecuencias, estaciones adicionales | Lectura | **Sí** |
| `listar_objetivos` | Objetivos de cada subensamblaje de una región (tipo, objeto, opción) | Lectura | **Sí** (muy ejercitada en 1.3.x) |
| `listar_ensamblajes` | Ensamblajes, grupos, subensamblajes con parámetros, dónde se usan | Lectura | **Sí** (1.2.2) |
| `listar_intersecciones` | Intersecciones: ejes, PK, corredor, regiones generadas | Lectura | **Sí** (1.2.2) |
| `listar_lineas_muestreo` | Grupos de líneas de muestreo; con `fuentes=true`, las fuentes de sección | Lectura (con `fuentes=true` abre el grupo para escritura) | **Parcial**: sin `fuentes` sí (1.2.2); con `fuentes=true`, no (`GetSectionSources` solo "existe en la DLL") |
| `estado_corredor` | Desactualizado, reconstrucción automática, superficies del corredor con códigos y contornos | Lectura | **Sí** (1.2.2) |
| `punto_a_pk` | Coordenadas → progresiva y desplazamiento | Lectura | **Sí** (1.2.2) |
| `pk_a_punto` | Progresiva (+ desplazamiento, + perfil) → coordenadas y cota | Lectura | **Sí** (1.2.2) |
| `cota_superficie` | Cota de una superficie en X, Y | Lectura | **Sí** (1.2.2; cruce con Revit en 1.4.0) |
| `extension_superficie` | Extensión, centro, origen sugerido, sistema de coordenadas, avisos de 16 y 33 km | Lectura | **Sí** según la tabla de miembros (12/10/2026), **informe ausente del repo** |
| `interseccion_ejes` | Cruces entre dos ejes (x, y, PK en cada uno) | Lectura | **Sí** (1.2.2) |

### 1.3 Escritura (todas con `simular`, copia, log, antes/después y una entrada de Deshacer)

| Herramienta | Qué hace | Tipo | Validada en Civil 3D 2027 |
|---|---|---|---|
| `asignar_ensamblaje_region` | Cambia el ensamblaje de una región | Escritura | **No** (`AssemblyId` existe en la DLL) |
| `asignar_objetivo` | Objetivo de superficie, elevación o desplazamiento de un subensamblaje | Escritura | **Sí** (1.2.2 a 1.3.3, con `restaurar`) |
| `asignar_objetivos` | Lote de objetivos: una copia, un log, una entrada de Deshacer | Escritura (lote) | **Sí** (1.3.1 a 1.3.3) |
| `asignar_objetivos_superficie` | La misma superficie en todos los objetivos de superficie del ámbito | Escritura | **No** |
| `deshacer_objetivos` | Devuelve los objetivos a lo que había antes de una escritura (pila en memoria) | Escritura | **Sí** (1.3.3, pasos 9 y 14 decisivos; suites en TODO OK) |
| `dividir_region` | Parte una región en una progresiva | Escritura | **No** (`Split` y `BaselineRegions.Add` existen en la DLL) |
| `establecer_rango_region` | Cambia inicio y fin de una región | Escritura | **No** |
| `establecer_frecuencia` | Frecuencias de una región | Escritura | **Parcial**: solo a través del lote (mismo miembro `AppliedAssemblySetting`) |
| `establecer_frecuencias` | Lote de frecuencias | Escritura (lote) | **Sí** (1.3.1 a 1.3.3; `_.UNDO 1` las revierte) |
| `agregar_estacion_region` | Estación adicional en una región | Escritura | **No** (`AddStation` existe en la DLL) |
| `reconstruir_corredor` | `Corridor.Rebuild()` | Escritura (hasta 300 s) | **Parcial**: ejecutada una vez en 1.3.1 fuera del prompt, sin paso formal |
| `agregar_superficie_corredor` | Crea una superficie de corredor con un código de enlace (Top o Datum) | Escritura | **No** |
| `agregar_codigo_superficie_corredor` | Añade códigos de enlace o de línea característica a la superficie | Escritura | **No** |
| `agregar_contorno_superficie_corredor` | Contorno automático, por línea base o polígono exterior | Escritura | **No** |
| `agregar_linea_rotura_superficie` | Líneas características del corredor como líneas de rotura de una superficie TIN | Escritura | **No** |
| `pegar_superficie` | Pega una superficie en otra y reconstruye | Escritura | **No** |
| `reconstruir_superficie` | `Surface.Rebuild()` | Escritura (hasta 300 s) | **No** |

### 1.4 Exportación por comando (sin API)

| Herramienta | Qué hace | Tipo | Validada en Civil 3D 2027 |
|---|---|---|---|
| `exportar_landxml` | Envía `LANDXMLOUT`; el usuario completa el cuadro | Comando | **No** |
| `exportar_imx` | Envía `EXPORTIMX` | Comando | **No** |

### 1.5 Balance

| Estado | Herramientas | Cuántas |
|---|---|---|
| Validadas | las 26 marcadas **Sí** | 26 |
| Parciales | `listar_lineas_muestreo` (`fuentes=true`), `establecer_frecuencia`, `reconstruir_corredor` | 3 |
| Nunca ejecutadas | `capturar_pantalla`, `guardar_dibujo`, `asignar_ensamblaje_region`, `asignar_objetivos_superficie`, `dividir_region`, `establecer_rango_region`, `agregar_estacion_region`, `agregar_superficie_corredor`, `agregar_codigo_superficie_corredor`, `agregar_contorno_superficie_corredor`, `agregar_linea_rotura_superficie`, `pegar_superficie`, `reconstruir_superficie`, `exportar_landxml`, `exportar_imx` | 15 |

Lo validado es, sobre todo, **leer** y **cambiar objetivos y frecuencias de un corredor que ya existe**. Todo lo que
**crea** o **edita superficies de corredor** está escrito pero sin probar. Ninguna herramienta **crea** objetos
nuevos (superficie, eje, perfil, ensamblaje, corredor, líneas de muestreo, vistas).

---

## 2. El flujo vial paso a paso

Formato de cada paso: qué herramienta existe hoy, qué falta y qué clases de la API se usarían (con la marca de
confianza de la cabecera). Los nombres de herramienta propuestos son orientativos, para discutirlos en el plan de cada
tarea.

### Paso 1. Superficie TIN desde puntos

- **Hoy**: `listar_superficies`, `cota_superficie`, `extension_superficie` (leer); `agregar_linea_rotura_superficie`,
  `pegar_superficie`, `reconstruir_superficie` (editar una superficie que ya existe). **Nada crea una superficie ni
  importa puntos.**
- **Falta**: crear la superficie; importar un archivo de puntos (PENZD/CSV) o puntos COGO; crear un grupo de puntos y
  añadirlo a la definición; contorno exterior de la superficie TIN (distinto del contorno de superficie de corredor);
  elegir estilo. Herramientas candidatas: `crear_superficie`, `importar_puntos` (archivo → puntos COGO + grupo),
  `agregar_puntos_superficie` (grupo o archivo), `agregar_contorno_superficie` (TIN, polilínea cerrada).
- **API**: `TinSurface.Create(Database, string nombre)` / `Create(string nombre, ObjectId estilo)` ◐;
  `TinSurface.PointFilesDefinition.AddPointFile(ruta, nombreFormato)` (`SurfaceDefinitionPointFiles`) ◐;
  `TinSurface.AddVertices(Point3dCollection)` ◐ (alternativa sin depender del nombre del formato: el plugin lee el
  CSV y añade los vértices); `CivilDocument.CogoPoints` (`CogoPointCollection.Add(Point3d, descripcion)`) ◐;
  `CivilDocument.PointGroups` (`PointGroupCollection.Add(nombre)`, `PointGroup.SetQuery(StandardPointGroupQuery)`) ◐;
  `TinSurface.PointGroupsDefinition.AddPointGroup(ObjectId)` ◐; `TinSurface.BoundariesDefinition.AddBoundaries(ObjectIdCollection, double, SurfaceBoundaryType.Outer, bool)` ◐;
  `TinSurface.DrawingObjectsDefinition.AddFromPolylines(...)` ◐; `Surface.Rebuild()` ✓; `Surface.StyleId` ◐.
- **Nota**: el nombre del formato de puntos cambia con el idioma de Civil 3D ("PENZD (delimitado por espacios)"): leer
  los formatos disponibles antes de usarlos o evitar el formato con `AddVertices`.

### Paso 2. Alineamiento desde polilínea con curvas

- **Hoy**: `listar_alineamientos`, `punto_a_pk`, `pk_a_punto`, `interseccion_ejes` (leer). **Nada crea un eje.**
- **Falta**: crear el eje desde una polilínea (respetando sus arcos o añadiendo curvas entre tangentes); fijar la
  progresiva inicial; leer sus entidades (tangentes, curvas, radios, espirales); estilo y conjunto de etiquetas;
  velocidad de diseño. Candidatas: `crear_alineamiento_desde_polilinea` (handle de la polilínea, nombre, sitio,
  `curvas_entre_tangentes`, `borrar_polilinea`), `listar_entidades_alineamiento`, `establecer_pk_inicial`.
- **API**: `Alignment.Create(CivilDocument, PolylineOptions, nombre, siteId, layerName, styleName, labelSetName)` ◐
  (hay variantes con `ObjectId` de estilo y etiquetas); `PolylineOptions { PlineId, AddCurvesBetweenTangents, EraseExistingEntities }` ◐;
  `Alignment.ReferencePointStation` / estación inicial ◐; `Alignment.Entities` (`AlignmentEntityCollection`:
  `AlignmentLine`, `AlignmentArc`, `AlignmentSpiral`, `AlignmentSCS`...) ◐; `Alignment.Entities.AddFixedLine`,
  `AddFreeCurve`, `AddFixedCurve` ◐ (construcción entidad a entidad si la polilínea no vale);
  `Alignment.AlignmentType` ◐; `Alignment.DesignSpeeds` ◐; `CivilDocument.GetSitelessAlignmentIds()` ◐;
  `Alignment.StationOffset`, `PointLocation` ✓.
- **Nota**: con `AddCurvesBetweenTangents` el radio lo decide la configuración de comandos del dibujo, no la API. Si se
  necesita un radio concreto por curva, hay que construir las entidades una a una.

### Paso 3. Perfil de terreno (EG)

- **Hoy**: `listar_perfiles` (distingue EG/FG), `listar_pvis`, `pk_a_punto` con `perfil` (cota). **Nada crea un perfil.**
- **Falta**: crear el perfil de superficie de un eje (con desplazamientos opcionales para los bordes de calzada).
  Candidata: `crear_perfil_terreno` (eje, superficie, nombre, `desplazamiento`, estilo).
- **API**: `Profile.CreateFromSurface(nombre, alignmentId, surfaceId, layerId, styleId, labelSetId)` ◐;
  `Profile.Offset` ◐; `Profile.ProfileType` ✓; `Profile.ElevationAt` ✓; `Profile.StartingStation` / `EndingStation` ◐;
  `ProfileStyle` ◐.

### Paso 4. Vista de perfil

- **Hoy**: nada. Ni lectura ni escritura.
- **Falta**: crear la vista de perfil de un eje en un punto del dibujo, con estilo y conjunto de bandas; fijar rangos
  de progresiva y cota; asignar a las bandas qué perfil es terreno y cuál rasante; leer las vistas existentes.
  Candidatas: `listar_vistas_perfil`, `crear_vista_perfil` (eje, nombre, x, y, estilo, conjunto de bandas),
  `asignar_perfiles_bandas`.
- **API**: `ProfileView.Create(alignmentId, Point3d insercion, nombre, bandSetId, styleId)` ◐ (y la variante corta
  `Create(alignmentId, Point3d)`) ◐; `Alignment.GetProfileViewIds()` ◐; `ProfileView.StationStart/StationEnd`,
  `ElevationMin/ElevationMax`, `StationRangeMode`, `ElevationRangeMode` ◐; `ProfileViewStyle`, `ProfileViewBandSet`
  (`CivilDocument.Styles.ProfileViewBandSetStyles`) ◐; `ProfileView.Bands` → `ProfileViewBandItem.Profile1Id/Profile2Id` ◐
  (asignar las fuentes de cada banda).
- **Nota**: la vista es solo dibujo; sin ella la rasante se puede crear igual por API, pero el usuario no la ve.

### Paso 5. Rasante (perfil de diseño, FG)

- **Hoy**: `listar_pvis` (leer PVI, pendientes y curvas). **Nada crea ni edita una rasante.**
- **Falta**: crear el perfil por composición; añadir PVI (progresiva, cota); curvas verticales (parábola simétrica por
  longitud o por K, circular); mover un PVI; leer pendientes de entrada y salida; comprobar criterios (K mínimo,
  longitud mínima). Candidatas: `crear_rasante` (eje, nombre, lista de PVI en `json`), `agregar_pvi`, `mover_pvi`,
  `establecer_curva_vertical` (PVI, longitud o K).
- **API**: `Profile.CreateByLayout(nombre, alignmentId, layerId, styleId, labelSetId)` ◐;
  `Profile.PVIs.AddPVI(estacion, cota)` (`ProfilePVICollection`) ◐; `ProfilePVI.RawStation` ✓ (`Station` está obsoleto),
  `ProfilePVI.Elevation`, `GradeIn`, `GradeOut`, `PVIType` ◐; `Profile.Entities.AddFixedTangent(Point2d, Point2d)` ◐;
  `Profile.Entities.AddFreeSymmetricParabolaByLength(idAnterior, idSiguiente, longitud, esConvexa)` ◐ (firma por
  verificar: los identificadores de entidad de perfil son enteros, no `ObjectId`); `AddFreeCircularCurveByRadius` ◐;
  `ProfileParabolaSymmetric.K`, `.Length` ◐; `ProfileTangent.Grade` ◐; `Profile.ProfileType` ✓.
- **Nota**: Civil 3D recalcula las entidades al mover un PVI; después de cada escritura se relee con `listar_pvis`
  (antes/después), como exige `Escritura.cs`.

### Paso 6. Ensamblaje

- **Hoy**: `listar_ensamblajes` (grupos, subensamblajes, parámetros, dónde se usa); `asignar_ensamblaje_region`
  (asigna uno que ya existe, sin validar). **Nada crea un ensamblaje ni cambia parámetros.**
- **Falta**: crear el ensamblaje; insertar subensamblajes del catálogo (carril con peralte, berma, cuneta, talud) a
  izquierda y derecha; fijar parámetros (ancho, pendiente, talud de corte y relleno); leer el catálogo disponible.
  Candidatas: `crear_ensamblaje`, `agregar_subensamblaje` (ensamblaje, nombre de catálogo, lado, parámetros en `json`),
  `establecer_parametro_subensamblaje`.
- **API**: `Assembly.Create(nombre, Point3d)` ◐; `Subassembly.CreateFromMacro(nombre, nombreMacro, proyectoMacro, ...)` ◐
  (los nombres internos de macro y proyecto de los subensamblajes de serie de 2027 hay que sacarlos con `InspectC3D` y
  del catálogo instalado; es el punto más incierto de todo el flujo); `Assembly.InsertSubassembly(...)` /
  `AddSubassembly` ◐; `AssemblyGroup` ✓; `Subassembly.ParamsDouble/ParamsLong/ParamsString/ParamsBool` ✓ en lectura, el
  cambio de valor ◐; `Subassembly.Side` ✓; `SubassemblyTargetInfo` ✓.
- **Alternativa de menor riesgo**: mantener una **biblioteca `.dwg` de ensamblajes propios** y copiarlos al dibujo con
  la API de AutoCAD (`Database.WblockCloneObjects`, ◐), en vez de componerlos desde cero por API.

### Paso 7. Corredor

- **Hoy**: es el paso mejor cubierto. Lectura: `listar_corredores`, `listar_regiones`, `listar_objetivos`,
  `estado_corredor`. Escritura validada: `asignar_objetivo`, `asignar_objetivos`, `deshacer_objetivos`,
  `establecer_frecuencias`. Escritura sin validar: `asignar_ensamblaje_region`, `asignar_objetivos_superficie`,
  `dividir_region`, `establecer_rango_region`, `establecer_frecuencia`, `agregar_estacion_region`;
  `reconstruir_corredor` parcial.
- **Falta**: crear el corredor; añadir una línea base (eje + rasante); añadir la primera región (ensamblaje, inicio,
  fin); reconstrucción automática sí/no. Candidatas: `crear_corredor` (nombre, eje, rasante, ensamblaje, inicio, fin:
  todo en una llamada, con una entrada de Deshacer), `agregar_linea_base`, `agregar_region`.
- **API**: `CivilDocument.CorridorCollection.Add(nombre)` ◐ (la colección ya se lee ✓); `Corridor.Baselines.Add(nombre, alignmentId, profileId)` ◐;
  `BaselineRegionCollection.Add(nombre, assemblyId, inicio, fin)` ✓ (existe en la DLL); `Baseline.StartStation/EndStation` ◐;
  `Corridor.Rebuild()` ✓; `Corridor.RebuildAutomatic` (asignación) ◐; `Corridor.IsOutOfDate` ✓;
  `BaselineRegion.AppliedAssemblySetting.FrequencyAlong*` ✓ (frecuencias iniciales).
- **Nota**: antes de añadir más herramientas aquí conviene **validar las seis que ya existen y nunca se ejecutaron**.

### Paso 8. Superficies de corredor Top y Datum

- **Hoy**: `agregar_superficie_corredor` (código Top o Datum), `agregar_codigo_superficie_corredor`,
  `agregar_contorno_superficie_corredor`, y `estado_corredor` para comprobar. **Escritas, nunca ejecutadas.**
- **Falta**: validarlas en Civil 3D (VALIDACION propia); corrección de voladizo (overhang); estilo de la superficie
  generada; borrar un código añadido por error (hoy ninguna herramienta borra: habría que decidir si se permite para
  códigos, que no son objetos del dibujo).
- **API**: `CorridorSurfaceCollection.Add(nombre)` ✓; `CorridorSurface.AddLinkCode(codigo, comoLineaRotura)` ✓;
  `AddFeatureLineCode(codigo)` ✓; `Boundaries.AddCorridorExtentsBoundary(nombre)` ✓; `Boundaries.Add(nombre, idPolilinea)` ✓;
  `CorridorSurface.OverhangCorrection` (`OverhangCorrectionType`) ◐; `CorridorSurface.SurfaceId` ✓; `StyleId` ◐.
- **Nota**: los códigos de enlace (`Top`, `Datum`, `Pave`...) se llaman igual en Civil 3D en español; dependen del
  conjunto de códigos de los subensamblajes usados.

### Paso 9. Líneas de muestreo

- **Hoy**: `listar_lineas_muestreo` (grupos y, con `fuentes=true`, fuentes de sección; esta variante sin validar).
  **Nada crea líneas de muestreo.**
- **Falta**: crear el grupo de líneas de muestreo de un eje; crear líneas por rango de progresivas con un intervalo y
  anchos de franja izquierda/derecha; marcar qué fuentes se muestrean (terreno, rasante, superficies del corredor,
  el propio corredor). Candidatas: `crear_lineas_muestreo` (eje, nombre, inicio, fin, intervalo, ancho_izq,
  ancho_der), `establecer_fuentes_muestreo`.
- **API**: `SampleLineGroup.Create(nombre, alignmentId)` ◐; `SampleLine.Create(nombre, sampleLineGroupId, estacion)` ◐
  (y la variante con vértices `Point2dCollection` para fijar los anchos) ◐; `SampleLine.Vertices` /
  anchos de franja ◐; `SampleLineGroup.GetSampleLineIds()` ◐; `SampleLineGroup.GetSectionSources()` ✓ (existe en la DLL),
  `SectionSource.IsSampled`, `SourceId`, `StyleId`, `SourceType` ◐.
- **Nota**: el asistente de Civil 3D ("Crear líneas de muestreo" → "Por rango de P.K.") es un cuadro de diálogo; por
  API hay que calcular las progresivas y crear cada línea.

### Paso 10. Vistas de sección

- **Hoy**: nada.
- **Falta**: crear vistas de sección, una por línea de muestreo o el grupo completo ("vistas de sección múltiples")
  con estilo, conjunto de bandas y disposición en hoja; leer las existentes. Candidatas: `listar_vistas_seccion`,
  `crear_vista_seccion` (línea de muestreo, x, y), `crear_vistas_seccion` (grupo entero).
- **API**: `SectionView.Create(nombre, sampleLineId, Point3d)` ◐; `SampleLineGroup.SectionViewGroups`
  (`SectionViewGroupCollection.Add(...)`) ◐ (la firma y qué estilos exige, por verificar); `SectionViewStyle`,
  `SectionViewBandSet`, `GroupPlotStyle` ◐; `SectionViewGroup.SectionViews` ◐.
- **Nota**: es el paso con más probabilidad de acabar en `ejecutar_comando` (sección 3).

### Paso 11. Volúmenes de corte y relleno

- **Hoy**: nada. `listar_superficies` ya distingue las superficies de volumen (`TinVolume`, `GridVolume`).
- **Falta**: dos caminos distintos. (a) **Volumen total** entre terreno y rasante (superficie Datum del corredor):
  superficie de volumen TIN y lectura de corte, relleno y neto. (b) **Volúmenes por sección** (cubicación clásica):
  criterios de cubicación, lista de materiales sobre el grupo de líneas de muestreo, tabla de volúmenes, informe y
  diagrama de masas. Candidatas: `crear_superficie_volumen` (base, comparación) + `volumen_superficie` (corte, relleno,
  neto); `calcular_materiales` (grupo, criterio, terreno, rasante) si la API lo permite.
- **API**: `TinVolumeSurface.Create(nombre, baseSurfaceId, comparisonSurfaceId)` ◐;
  `TinVolumeSurface.GetVolumeProperties()` → `UnadjustedCutVolume`, `UnadjustedFillVolume`, `UnadjustedNetVolume` ◐;
  `SampleLineGroup.MaterialLists` (`MaterialListCollection`, `MaterialList`, `Material`) ◐; `QuantityTakeoffCriteria` ◐;
  `MassHaulView`, `MassHaulLine` ◐ (lectura). Tabla de volúmenes totales, informe y diagrama de masas: ✗ (comandos,
  sección 3).
- **Nota**: el camino (a) es seguro y rápido de hacer; el (b) depende de cuánta API real haya detrás de "Calcular
  materiales", que hay que inspeccionar antes de prometer nada.

---

## 3. Riesgos: dónde hará falta `ejecutar_comando`

`ejecutar_comando` envía texto a la línea de comandos: no sabe qué cambió, no verifica, no deja `antes`/`despues` y, si
el comando abre un cuadro de diálogo, la persona tiene que completarlo. Por eso es el último recurso. Estos son los
puntos del flujo donde probablemente no se pueda evitar o donde la API es dudosa:

| Paso | Acción | Comando que usaría Civil 3D | Riesgo | Mitigación |
|---|---|---|---|---|
| 1 | Importar archivo de puntos con formato | `IMPORTPOINTS` (cuadro) | Medio: la API existe, pero el nombre del formato depende del idioma | Leer los formatos disponibles por API o leer el CSV en el plugin y usar `AddVertices` |
| 2 | Curvas con radio concreto al crear el eje | `CREATEALIGNMENTENTITIES` (asistente) | Medio | Construir las entidades una a una (`AddFixedLine`, `AddFreeCurve`) |
| 4 | Bandas de la vista de perfil con sus perfiles | `EDITPROFILEVIEWPROPERTIES` (cuadro) | Medio | Conjunto de bandas de la plantilla + asignar `Profile1Id/Profile2Id` por API si existe |
| 5 | Comprobación de criterios de diseño (K, longitud mínima) | revisión en la ventana de composición | Bajo | Calcular los criterios en el plugin y devolverlos como `avisos` |
| 6 | Insertar subensamblajes del catálogo | paleta de herramientas `TOOLPALETTES` (arrastrar) | **Alto**: `ejecutar_comando` no puede arrastrar desde la paleta; `CreateFromMacro` exige nombres internos | Biblioteca `.dwg` de ensamblajes propios y copia al dibujo; `listar_ensamblajes` para elegir |
| 6 | Cambiar parámetros de un subensamblaje | Propiedades del subensamblaje (cuadro) | Medio | Asignar `Params*` por API si el setter existe (verificar con `InspectC3D`) |
| 8 | Contorno automático que no cierra | `ADDCORRIDORSURFACEBOUNDARY` (interactivo) | Medio (ya documentado) | `exterior_poligono` con una polilínea cerrada (ya existe) |
| 9 | Líneas de muestreo por rango de progresivas | `CREATESAMPLELINES` (asistente) | Medio | Calcular las progresivas y crear cada línea por API |
| 10 | Vistas de sección múltiples | `CREATEMULTIPLESECTIONVIEW` (asistente de varias páginas) | **Alto** | Probar `SectionViewGroups.Add`; si no, una vista por línea con `SectionView.Create` colocadas por el plugin |
| 11 | Calcular materiales (cubicación) | `COMPUTEMATERIALS` (cuadro) | **Alto**: API de listas de materiales incierta | Camino (a) con `TinVolumeSurface` para el total; (b) con el patrón de `exportar_landxml` (el comando abre el cuadro, la persona lo completa, el plugin verifica después) |
| 11 | Tabla de volúmenes totales, informe, diagrama de masas | `ADDTOTALVOLUMETABLE`, `GENERATEQUANTITIESREPORT`, `CREATEMASSHAULDIAGRAM` | **Alto**: sin API conocida | Patrón de `exportar_landxml`; verificar leyendo el objeto creado |
| 2 y 6 | Peralte (cálculo automático) | `CALCULATESUPERELEVATION` (asistente) | **Alto** | Lectura por `Alignment.SuperelevationCurves`; el cálculo lo hace la persona |
| todos | Estilos y conjuntos de etiquetas | — | Medio: los nombres dependen de la plantilla y del idioma ("Básico", "Estándar") | Aceptar el nombre como parámetro opcional y usar el estilo por defecto si no se pasa; fallar con la lista de estilos disponibles |

Regla para estos casos, ya aplicada en `exportar_landxml` y `exportar_imx`: el comando se envía sin `UNDO`, con
`FILEDIA=0` si pide archivos, los parámetros son informativos para que el agente le diga a la persona qué marcar, y
**la verificación se hace después leyendo el dibujo** (`existe`, el objeto nuevo en `listar_*`). Nunca se usa
`ejecutar_comando` para esquivar las reglas de `Escritura.cs`.

---

## 4. Semilla del glosario

Términos en español que se usan en este proyecto y a qué clase o concepto de la API corresponden. Completa la tabla de
`PuenteMcp/INSTRUCCIONES_AGENTE.md` (sección 4); cuando exista `memoria-firma/`, este glosario debería vivir allí.

| Paso | Término en español (variantes) | API o concepto de Civil 3D 2027 |
|---|---|---|
| 1 | Superficie, superficie TIN, MDT | `TinSurface` (hereda de `Surface`); `GridSurface` para malla |
| 1 | Puntos, puntos COGO, puntos topográficos | `CogoPoint`, `CogoPointCollection` (`CivilDocument.CogoPoints`) |
| 1 | Grupo de puntos | `PointGroup`, `PointGroupCollection`, `StandardPointGroupQuery` |
| 1 | Archivo de puntos PENZD / PNEZD, CSV | `SurfaceDefinitionPointFiles.AddPointFile`, formato `PointFileFormat` |
| 1 | Contorno (de superficie), contorno exterior, isla | `SurfaceBoundaries`, `SurfaceBoundaryType` (Outer, Hide, Show) |
| 1 | Línea de rotura | `Breakline`, `SurfaceDefinitionBreaklines.AddStandardBreaklines` |
| 1 | Curva de nivel | `Contour`, `SurfaceDefinitionContours`; estilo de superficie `SurfaceStyle` |
| 1 | Cota, elevación | `Surface.FindElevationAtXY`, `Profile.ElevationAt`, eje Z |
| 1 | Terreno natural, topografía, EG | Superficie TIN de terreno; perfil `ProfileType.EG` |
| 1 | Reconstruir (superficie) | `Surface.Rebuild()`; `IsOutOfDate` |
| 2 | Eje, alineamiento, trazado en planta | `Alignment` (`AlignmentType.Centerline`) |
| 2 | Eje de desplazamiento, borde de calzada | `Alignment` de tipo `Offset` (`AlignmentType.Offset`) |
| 2 | Acuerdo, curva de esquina (en intersecciones) | `Alignment` de tipo `CurbReturn` |
| 2 | Progresiva, PK, estación, kilometraje | `Station` (double, metros desde el inicio); `Alignment.StationOffset` |
| 2 | Progresiva inicial | `Alignment.ReferencePointStation` / estación de referencia |
| 2 | PI (punto de intersección de tangentes en planta) | Vértice entre dos `AlignmentLine`; no es una clase propia (`AlignmentPI` en algunas versiones) |
| 2 | Tangente, recta | `AlignmentLine` |
| 2 | Curva circular, radio | `AlignmentArc` (`Radius`) |
| 2 | Espiral, clotoide, curva de transición | `AlignmentSpiral`; conjuntos `AlignmentSCS`, `AlignmentSTS` |
| 2 | Peralte, sobreelevación | `SuperelevationCurve`, `SuperelevationCriticalStation`, `Alignment.SuperelevationCurves` |
| 2 | Bombeo | Pendiente transversal normal; parámetro del subensamblaje de carril (`Params*`), referencia `SuperelevationCurve` (normal crown) |
| 2 | Velocidad de diseño | `Alignment.DesignSpeeds` (`DesignSpeed`) |
| 2 | Sitio, emplazamiento | `Site`; `GetSitelessAlignmentIds` para ejes sin sitio |
| 3 | Perfil, perfil longitudinal | `Profile` |
| 3 | Perfil de terreno, perfil de superficie, EG | `Profile.CreateFromSurface`, `ProfileType.EG` |
| 3 | Desplazamiento del perfil | `Profile.Offset` |
| 4 | Vista de perfil, guitarra (hoja de perfil) | `ProfileView` |
| 4 | Bandas, guitarra de datos | `ProfileViewBandSet`, `ProfileViewBandItem` |
| 4 | Estilo de vista de perfil | `ProfileViewStyle` |
| 5 | Rasante, perfil de diseño, subrasante, FG | `Profile.CreateByLayout`, `ProfileType.FG` |
| 5 | PIV, PVI, vértice vertical | `ProfilePVI` (`RawStation`, `Elevation`); `Profile.PVIs` |
| 5 | Pendiente (de entrada / de salida), rampa, % | `ProfilePVI.GradeIn` / `GradeOut`; `ProfileTangent.Grade` |
| 5 | Tangente vertical | `ProfileTangent` |
| 5 | Curva vertical, acuerdo vertical, parábola | `ProfileParabolaSymmetric`, `ProfileParabolaAsymmetric`, `ProfileCircular` |
| 5 | Convexa / cóncava (cresta / valle) | `isCrest` en los métodos de curva; `VerticalCurveType` |
| 5 | Parámetro K | `ProfileParabolaSymmetric.K` |
| 6 | Ensamblaje, sección tipo | `Assembly` (`AssemblyType`) |
| 6 | Subensamblaje, componente | `Subassembly` (`Side`, `Params*`) |
| 6 | Grupo (del ensamblaje), lado izquierdo / derecho | `AssemblyGroup`; `SubassemblySideType` |
| 6 | Media sección | Ensamblaje con un solo grupo lateral (half assembly) |
| 6 | Carril, calzada | Subensamblajes de carril (`LaneSuperelevationAOR`, `BasicLane`...) |
| 6 | Berma, arcén | Subensamblajes `Shoulder*` |
| 6 | Cuneta | Subensamblajes `*Ditch*`, `BasicSideSlopeCutDitch` |
| 6 | Talud (de corte / de relleno) | Subensamblajes `Daylight*`, `LinkSlopeToSurface`; siempre con objetivo de superficie |
| 6 | Catálogo de subensamblajes, paleta | Tool catalog / `Subassembly.CreateFromMacro` |
| 6 | Objetivo (de superficie / elevación / anchura) | `SubassemblyTargetInfo`, `SubassemblyLogicalNameType` (Surface / Elevation / Offset) |
| 7 | Corredor, obra lineal | `Corridor` |
| 7 | Línea base | `Baseline` (eje + perfil) |
| 7 | Región | `BaselineRegion` (ensamblaje + rango) |
| 7 | Frecuencia (de secciones) | `AppliedAssemblySetting.FrequencyAlong*` |
| 7 | Estación adicional | `BaselineRegion.AddStation` / `AdditionalStations` |
| 7 | Reconstruir (corredor), desactualizado | `Corridor.Rebuild()`, `IsOutOfDate`, `RebuildAutomatic` |
| 7 | Intersección, cruce | `Intersection`, `IntersectionRoad` |
| 8 | Superficie de corredor | `CorridorSurface`, `CorridorSurfaceCollection` |
| 8 | Código de enlace / de punto / de línea característica | Link code / Point code / Feature line code (`AddLinkCode`, `AddFeatureLineCode`) |
| 8 | Top (rasante acabada), Datum (explanada, subrasante) | Códigos de enlace `Top` y `Datum` |
| 8 | Contorno (de superficie de corredor), envolvente | `CorridorSurfaceBoundary`, `UseAsOuterBoundary`, `AddCorridorExtentsBoundary` |
| 8 | Línea característica (del corredor) | `CorridorFeatureLine` (`CodeName`, `FeatureLinePoints`) |
| 8 | Corrección de voladizo | `OverhangCorrectionType` |
| 9 | Línea de muestreo, sección de muestreo | `SampleLine` |
| 9 | Grupo de líneas de muestreo | `SampleLineGroup` |
| 9 | Franja, ancho de muestreo (izquierda / derecha) | Swath width (`SampleLine.Vertices`) |
| 9 | Fuentes de sección (terreno, rasante, corredor) | `SectionSource` (`IsSampled`, `SourceType`) |
| 10 | Sección transversal | `Section` (una por fuente y línea de muestreo) |
| 10 | Vista de sección, hoja de secciones | `SectionView`, `SectionViewGroup` |
| 10 | Estilo de trazado de grupo | `GroupPlotStyle` |
| 11 | Corte, desmonte, excavación | Cut (`UnadjustedCutVolume`) |
| 11 | Relleno, terraplén | Fill (`UnadjustedFillVolume`) |
| 11 | Volumen neto, compensación | Net (`UnadjustedNetVolume`) |
| 11 | Superficie de volumen, superficie diferencia | `TinVolumeSurface`, `GridVolumeSurface` (`GetVolumeProperties`) |
| 11 | Cubicación, cálculo de materiales | `MaterialList`, `Material`, `SampleLineGroup.MaterialLists` |
| 11 | Criterios de cubicación (QTO) | `QuantityTakeoffCriteria` |
| 11 | Diagrama de masas, curva masa | `MassHaulView`, `MassHaulLine` |
| 11 | Esponjamiento, compactación (factores) | Factores de corte y relleno del criterio de cubicación |
| todos | Estilo, conjunto de etiquetas | `*Style`, `*LabelSetStyle` (`CivilDocument.Styles`) |
| todos | Plantilla | `.dwt` con estilos, códigos y ensamblajes |

---

## Qué conviene hacer después (propuestas para otras sesiones, una por sesión)

1. **Validar lo que ya existe y nunca se ejecutó**: un `VALIDACION_15.md` que recorra las 15 herramientas de la
   sección 1.5 sobre una copia del dibujo, sin código nuevo. Es lo más barato y lo que más memoria técnica produce.
2. **Crear corredor** (paso 7): `crear_corredor` + `agregar_linea_base` + `agregar_region`. Riesgo bajo, la API ya está
   medio verificada y encaja con las herramientas validadas.
3. **Superficie TIN** (paso 1) con `AddVertices` desde CSV, sin depender de formatos.
4. **Eje desde polilínea** (paso 2) y **perfil de terreno** (paso 3): dos herramientas de creación con API clara.
5. **Rasante** (paso 5) y **vista de perfil** (paso 4): empezar por PVI y tangentes; las curvas verticales después.
6. **Volumen total** (paso 11, camino a) con `TinVolumeSurface`.
7. **Líneas de muestreo y vistas de sección** (pasos 9 y 10): primero inspeccionar con `InspectC3D` qué hay en
   `SampleLineGroup` y `SectionViewGroupCollection`; decidir entonces API o comando asistido.
8. **Ensamblaje** (paso 6): decidir entre biblioteca `.dwg` y composición por API tras inspeccionar
   `Subassembly.CreateFromMacro` y el catálogo de 2027.
