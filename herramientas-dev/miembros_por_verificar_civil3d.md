# Miembros de la API de Civil 3D por verificar

Registro de los miembros de la API de AutoCAD y Civil 3D 2027 (.NET 10) que usa el plugin `ArbaMcp` y de en qué
grado se han comprobado. Se actualiza en cada entrega: todo miembro nuevo entra como `por verificar` y cambia de
estado con la validación en un Civil 3D real (`ArbaMcp/pruebas/probar_servidor.py` o un paso manual anotado en el
informe de `VALIDACION_*.md`). Un miembro `no existe` se retira del código o se deja detrás de `Api.Invocar` con una
ruta alternativa.

Estados:

- `existe en la DLL`: comprobado **solo por metadatos** (`herramientas-dev/InspectC3D` sobre `AeccDbMgd.dll` y
  `acdbmgd.dll` con `MetadataLoadContext`). Dice que el nombre y la firma existen; no dice que funcione.
- `ejecutado en Civil 3D 2027`: una herramienta lo ejecutó en Civil 3D y devolvió lo esperado (se anota versión del
  plugin, fecha y prueba). Vacío hasta la primera validación completa.
- `por verificar`: se usa en el código y nadie lo ha ejecutado aún (ni siquiera comprobado en metadatos).
- `no existe`: no está en la DLL de 2027; se anota la alternativa usada.

Referencia de proceso: `revit-mcp/herramientas-dev/miembros_por_verificar_revit.md`.

## Estado de partida (1.2.1): comprobados por metadatos con InspectC3D

Copiado de `miembros_verificados_2027.md`; ninguno ha sido ejecutado todavía en una validación documentada.

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| propiedad | `SubassemblyTargetInfo.SubassemblyId` | — | `asignar_objetivo` (identificación) | no existe (se identifica por `AssemblyGroupName` + `SubassemblyName` + `LogicalName` recorriendo `Assembly.Groups → GetSubassemblyIds()`) |
| propiedad | `SubassemblyTargetInfo.AssemblyGroupName`, `.SubassemblyName`, `.LogicalName` | 2027 | `listar_objetivos`, `asignar_objetivo`, `asignar_objetivos_superficie` | existe en la DLL |
| propiedad | `SubassemblyTargetInfo.TargetIds` (get/set), `.UseSameSideTarget`, `.TargetToOption`, `.TargetType` | 2027 | `asignar_objetivo`, `asignar_objetivos_superficie`, `dividir_region` | existe en la DLL |
| método | `BaselineRegion.GetTargets()` / `SetTargets(SubassemblyTargetInfoCollection)` | 2027 | `asignar_objetivo`, `asignar_objetivos_superficie`, `listar_objetivos` | existe en la DLL |
| método | `CorridorSurface.AddPointCode` | — | `agregar_codigo_superficie_corredor` | no existe (se usa `AddFeatureLineCode`; `tipo=punto` se acepta como alias con aviso) |
| método | `CorridorSurface.AddLinkCode(string, bool)`, `AddFeatureLineCode(string)` | 2027 | `agregar_superficie_corredor`, `agregar_codigo_superficie_corredor` | existe en la DLL |
| método | `CorridorSurface.LinkCodes()`, `PointCodes()`, `FeatureLineCodes()` | 2027 | `estado_corredor`, `agregar_linea_rotura_superficie` | existe en la DLL |
| propiedad | `CorridorSurface.Boundaries`, `CorridorSurface.SurfaceId` | 2027 | `estado_corredor`, `listar_superficies`, `agregar_contorno_superficie_corredor` | existe en la DLL |
| método | `CorridorSurfaceBoundaryCollection.AddCorridorExtentsBoundary(string)`, `Add(string, ObjectId)` | 2027 | `agregar_contorno_superficie_corredor` | existe en la DLL |
| método | `CorridorSurfaceCollection.Add(string)` | 2027 | `agregar_superficie_corredor` | existe en la DLL |
| propiedad | `BaselineRegion.AppliedAssemblySetting.FrequencyAlongTangents`, `.FrequencyAlongCurves`, `.FrequencyAlongSpirals`, `.FrequencyAlongProfileCurves` | 2027 | `listar_regiones`, `establecer_frecuencia`, `establecer_frecuencias` | existe en la DLL |
| método | `BaselineRegion.AdditionalStations()`, `AddStation(double, string)` | 2027 | `listar_regiones`, `agregar_estacion_region` | existe en la DLL |
| método | `BaselineRegion.Split(double)` | 2027 | `dividir_region` | existe en la DLL (con reserva: recorte + `BaselineRegions.Add`) |
| método | `BaselineRegionCollection.Add(string, ObjectId, double, double)` | 2027 | `dividir_region` (reserva) | existe en la DLL |
| propiedad | `BaselineRegion.StartStation`, `.EndStation`, `.AssemblyId`, `.Name` | 2027 | `listar_regiones`, `establecer_rango_region`, `asignar_ensamblaje_region` | existe en la DLL |
| propiedad | `ProfilePVI.Station` | — | `listar_pvis` | obsoleto (CS0618); se usa `RawStation` |
| propiedad | `ProfilePVI.RawStation`, `.Elevation`; `Profile.PVIs`, `.Entities`, `.ProfileType`; `ProfileEntity.EntityType`, `.StartStation`, `.EndStation` | 2027 | `listar_pvis`, `listar_perfiles` | existe en la DLL |
| propiedad/método | `Surface.IsOutOfDate`, `Surface.Rebuild()`, `Surface.FindElevationAtXY` | 2027 | `listar_superficies`, `reconstruir_superficie`, `cota_superficie` | existe en la DLL |
| método | `TinSurface.PasteSurface(ObjectId)` | 2027 | `pegar_superficie` | existe en la DLL |
| método | `TinSurface.BreaklinesDefinition.AddStandardBreaklines(ObjectIdCollection, double, double, double, double)` | 2027 | `agregar_linea_rotura_superficie` | existe en la DLL |
| método | `SampleLineGroup.GetSectionSources()` (exige el grupo abierto para escritura) | 2027 | `listar_lineas_muestreo` (`fuentes=true`) | existe en la DLL |
| propiedad | `Assembly.Type`, `Assembly.Groups`, `AssemblyGroup.GetSubassemblyIds()`, `Subassembly.Side`, `Subassembly.ParamsString/Double/Long/Bool` | 2027 | `listar_ensamblajes`, `listar_objetivos` | existe en la DLL |
| propiedad | `Intersection.Location`, `.CorridorId`, `.IntersectionRoads[i].CenterlineAlignmentId`, `.GradeRuleType` | 2027 | `listar_intersecciones` | existe en la DLL |
| propiedad | `Baseline.MainBaselineFeatureLines.FeatureLineCollectionMap` → `CorridorFeatureLine.CodeName`, `.FeatureLinePoints` | 2027 | `agregar_linea_rotura_superficie` | existe en la DLL |
| método | `Alignment.StationOffset`, `Alignment.PointLocation`, `Profile.ElevationAt`, `Entity.IntersectWith` | 2027 | `punto_a_pk`, `pk_a_punto`, `interseccion_ejes` | existe en la DLL |
| método | `Corridor.Rebuild()`, `Corridor.IsOutOfDate`, `Corridor.RebuildAutomatic`, `Corridor.Baselines`, `Corridor.CorridorSurfaces` | 2027 | `reconstruir_corredor`, `estado_corredor`, `listar_corredores` | existe en la DLL |
| método | `Database.SaveAs(string, bool, DwgVersion, SecurityParameters)` | 2027 | `guardar_dibujo`, `guardar_copia`, copia de un dibujo sin guardar | existe en la DLL |
| método | `DocumentCollection.Open(string, bool)`, `Document.LockDocument()`, `TransactionManager.StartTransaction()` | 2027 | `abrir_dibujo`, todas las de lectura y escritura | existe en la DLL |

## 1.2.2 (contextos de ejecución y Dispatcher): escrita sin compilar

Se validan con `herramientas-dev/VALIDACION_122.md` (pasos 5 y 7). Hasta ese informe, todo `por verificar`.

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| método | `DocumentCollection.ExecuteInCommandContextAsync(Func<object, Task>, object)` (en 2027 devuelve `ExecutionResult`, no `Task`; el plugin no depende del tipo devuelto) | 2015+ | todas las de contexto `Documento` (`HiloPrincipal.EntregarAlDocumento`) | por verificar |
| propiedad | `System.Windows.Threading.Dispatcher.CurrentDispatcher` capturado en el hilo principal de AutoCAD y `BeginInvoke` desde el hilo del servidor | .NET 10 | `HiloPrincipal.Despertar` (historial `HiloPrincipal listo: despachador sí`) | por verificar |
| propiedad | `Application.MainWindow.Handle` + `user32.IsWindowEnabled` (detección de cuadro de diálogo modal) | 2027 | `HiloPrincipal.CivilLibre` (`ventana principal sí`) | por verificar |
| método | `Application.GetSystemVariable("CMDACTIVE")` desde el hilo principal para decidir si Civil 3D está libre | 2027 | `HiloPrincipal.CivilLibre`, `Escritura.Preparar` | por verificar |
| método | `Document.SendStringToExecute` de los ESC y de `_.UNDO _E` por la cola `Inmediato` con un comando esperando entrada | 2027 | `ejecutar_comando` (prueba "Civil 3D ocupado": `_.LINE` termina con `timeout con ESC`) | por verificar |
| evento | `Application.Idle` como respaldo del despachador | 2027 | `HiloPrincipal` | por verificar |
| evento | `Document.CommandWillStart/CommandEnded/CommandCancelled/CommandFailed` y `DocumentCollection.DocumentCreated/DocumentToBeDestroyed` | 2027 | `Historial`, `ejecutar_comando` | por verificar (la 1.2.0 los usaba, sin informe) |

## 1.3.0 (proceso: copia de disco, deshacer, lotes)

Se completa en esta entrega; ver el final del archivo tras el Bloque 2.
