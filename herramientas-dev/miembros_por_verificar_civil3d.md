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

Validación de la 1.2.2 (28/09/2026, Civil 3D 2027 en español, `herramientas-dev/informes/informe_validacion_122_20260928.md`):
`probar_servidor.py` 13/13 (segunda ejecución; la primera corrió con el cuadro de diálogo de `ARBAMCP` abierto y el
plugin esperó y descartó como debía) y `--dwg` 31/31 sobre `PRUEBA_AISLAMIENTO_validacion122.dwg` (corredor
`Interseccion 3`, 68 alineamientos). Hallazgo: cada herramienta en contexto de comando deja una entrada
`Executefunction` en el menú Deshacer, también las lecturas; una escritura es una sola entrada. Por eso en 1.3.0 las
lecturas pasan a contexto de aplicación y se retiran las marcas de deshacer. La escritura real tardó 4252 ms (el
`SaveAs` de la copia de 70 MB) frente a 6 ms la siguiente.

## Estado de partida (1.2.1): comprobados por metadatos con InspectC3D

Copiado de `miembros_verificados_2027.md`. Lo que el paso 8 de la validación de la 1.2.2 ejecutó pasa a
`ejecutado en Civil 3D 2027`; el resto sigue comprobado solo por metadatos.

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| propiedad | `SubassemblyTargetInfo.SubassemblyId` | — | `asignar_objetivo` (identificación) | no existe (se identifica por `AssemblyGroupName` + `SubassemblyName` + `LogicalName` recorriendo `Assembly.Groups → GetSubassemblyIds()`) |
| propiedad | `SubassemblyTargetInfo.AssemblyGroupName`, `.SubassemblyName`, `.LogicalName` | 2027 | `listar_objetivos`, `asignar_objetivo`, `asignar_objetivos_superficie` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `listar_objetivos` y `asignar_objetivo` con grupo `Derecha` y parametro `TargetDTM`) |
| propiedad | `SubassemblyTargetInfo.TargetIds` (get/set), `.UseSameSideTarget`, `.TargetToOption`, `.TargetType` | 2027 | `asignar_objetivo`, `asignar_objetivos_superficie`, `dividir_region` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `TargetIds` y `TargetType`: `asignar_objetivo` Topografia → Interseccion 3 → Topografia, verificado con `listar_objetivos`; los setters de `UseSameSideTarget` y `TargetToOption` no se pasaron: existe en la DLL) |
| método | `BaselineRegion.GetTargets()` / `SetTargets(SubassemblyTargetInfoCollection)` | 2027 | `asignar_objetivo`, `asignar_objetivos_superficie`, `listar_objetivos` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, dos escrituras reales de `asignar_objetivo`) |
| método | `CorridorSurface.AddPointCode` | — | `agregar_codigo_superficie_corredor` | no existe (se usa `AddFeatureLineCode`; `tipo=punto` se acepta como alias con aviso) |
| método | `CorridorSurface.AddLinkCode(string, bool)`, `AddFeatureLineCode(string)` | 2027 | `agregar_superficie_corredor`, `agregar_codigo_superficie_corredor` | existe en la DLL |
| método | `CorridorSurface.LinkCodes()`, `PointCodes()`, `FeatureLineCodes()` | 2027 | `estado_corredor`, `agregar_linea_rotura_superficie` | existe en la DLL (`estado_corredor` OK en 0,07 s; el informe no dice si el corredor tenía superficies) |
| propiedad | `CorridorSurface.Boundaries`, `CorridorSurface.SurfaceId` | 2027 | `estado_corredor`, `listar_superficies`, `agregar_contorno_superficie_corredor` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `listar_superficies` y `estado_corredor` OK; misma reserva sobre las superficies de corredor) |
| método | `CorridorSurfaceBoundaryCollection.AddCorridorExtentsBoundary(string)`, `Add(string, ObjectId)` | 2027 | `agregar_contorno_superficie_corredor` | existe en la DLL |
| método | `CorridorSurfaceCollection.Add(string)` | 2027 | `agregar_superficie_corredor` | existe en la DLL |
| propiedad | `BaselineRegion.AppliedAssemblySetting.FrequencyAlongTangents`, `.FrequencyAlongCurves`, `.FrequencyAlongSpirals`, `.FrequencyAlongProfileCurves` | 2027 | `listar_regiones`, `establecer_frecuencia`, `establecer_frecuencias` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, lectura en `listar_regiones` OK; la asignación, existe en la DLL y se prueba en `VALIDACION_13` pasos 10 a 12) |
| método | `BaselineRegion.AdditionalStations()`, `AddStation(double, string)` | 2027 | `listar_regiones`, `agregar_estacion_region` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `AdditionalStations()` en `listar_regiones` OK; `AddStation`, existe en la DLL) |
| método | `BaselineRegion.Split(double)` | 2027 | `dividir_region` | existe en la DLL (con reserva: recorte + `BaselineRegions.Add`) |
| método | `BaselineRegionCollection.Add(string, ObjectId, double, double)` | 2027 | `dividir_region` (reserva) | existe en la DLL |
| propiedad | `BaselineRegion.StartStation`, `.EndStation`, `.AssemblyId`, `.Name` | 2027 | `listar_regiones`, `establecer_rango_region`, `asignar_ensamblaje_region` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, lectura en `listar_regiones` OK; la asignación de rango y de ensamblaje, existe en la DLL) |
| propiedad | `ProfilePVI.Station` | — | `listar_pvis` | obsoleto (CS0618); se usa `RawStation` |
| propiedad | `ProfilePVI.RawStation`, `.Elevation`; `Profile.PVIs`, `.Entities`, `.ProfileType`; `ProfileEntity.EntityType`, `.StartStation`, `.EndStation` | 2027 | `listar_pvis`, `listar_perfiles` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `listar_perfiles` y `listar_pvis` OK) |
| propiedad/método | `Surface.IsOutOfDate`, `Surface.Rebuild()`, `Surface.FindElevationAtXY` | 2027 | `listar_superficies`, `reconstruir_superficie`, `cota_superficie` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `IsOutOfDate` en `listar_superficies` y `FindElevationAtXY` en `cota_superficie` OK; `Rebuild()`, existe en la DLL) |
| método | `TinSurface.PasteSurface(ObjectId)` | 2027 | `pegar_superficie` | existe en la DLL |
| método | `TinSurface.BreaklinesDefinition.AddStandardBreaklines(ObjectIdCollection, double, double, double, double)` | 2027 | `agregar_linea_rotura_superficie` | existe en la DLL |
| método | `SampleLineGroup.GetSectionSources()` (exige el grupo abierto para escritura) | 2027 | `listar_lineas_muestreo` (`fuentes=true`) | existe en la DLL |
| propiedad | `Assembly.Type`, `Assembly.Groups`, `AssemblyGroup.GetSubassemblyIds()`, `Subassembly.Side`, `Subassembly.ParamsString/Double/Long/Bool` | 2027 | `listar_ensamblajes`, `listar_objetivos` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `listar_ensamblajes` OK en 0,15 s) |
| propiedad | `Intersection.Location`, `.CorridorId`, `.IntersectionRoads[i].CenterlineAlignmentId`, `.GradeRuleType` | 2027 | `listar_intersecciones` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `listar_intersecciones` OK; el dibujo tiene el corredor `Interseccion 3`) |
| propiedad | `Baseline.MainBaselineFeatureLines.FeatureLineCollectionMap` → `CorridorFeatureLine.CodeName`, `.FeatureLinePoints` | 2027 | `agregar_linea_rotura_superficie` | existe en la DLL |
| método | `Alignment.StationOffset`, `Alignment.PointLocation`, `Profile.ElevationAt`, `Entity.IntersectWith` | 2027 | `punto_a_pk`, `pk_a_punto`, `interseccion_ejes` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `punto_a_pk`, `pk_a_punto`, `cota_superficie` e `interseccion_ejes` OK) |
| método | `Corridor.Rebuild()`, `Corridor.IsOutOfDate`, `Corridor.RebuildAutomatic`, `Corridor.Baselines`, `Corridor.CorridorSurfaces` | 2027 | `reconstruir_corredor`, `estado_corredor`, `listar_corredores` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `listar_corredores` y `estado_corredor` OK; `Rebuild()`, existe en la DLL) |
| método | `Database.SaveAs(string, bool, DwgVersion, SecurityParameters)` | 2027 | `guardar_dibujo`, `guardar_copia`, copia de un dibujo sin guardar | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, copia de seguridad de 70 972 777 bytes con `bBakAndRename=false`, unos 4,2 s en el hilo principal) |
| método | `DocumentCollection.Open(string, bool)`, `Document.LockDocument()`, `TransactionManager.StartTransaction()` | 2027 | `abrir_dibujo`, todas las de lectura y escritura | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `abrir_dibujo` y las 21 lecturas del paso 8) |

## 1.2.2 (contextos de ejecución y Dispatcher): escrita sin compilar

Validadas el 28/09/2026 con `herramientas-dev/VALIDACION_122.md` (pasos 5, 7 y 8; informe en `informes/`).

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| método | `DocumentCollection.ExecuteInCommandContextAsync(Func<object, Task>, object)` (en 2027 devuelve `ExecutionResult`, no `Task`; el plugin no depende del tipo devuelto) | 2015+ | todas las de contexto `Documento` (`HiloPrincipal.EntregarAlDocumento`) | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, paso 8: 21 lecturas y 2 escrituras; cada trabajo deja una entrada `Executefunction` en Deshacer) |
| propiedad | `System.Windows.Threading.Dispatcher.CurrentDispatcher` capturado en el hilo principal de AutoCAD y `BeginInvoke` desde el hilo del servidor | .NET 10 | `HiloPrincipal.Despertar` (historial `HiloPrincipal listo: despachador sí`) | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `HiloPrincipal listo: despachador sí, ventana principal sí`; `ping` en 0,02 s con `_.LINE` activo) |
| propiedad | `Application.MainWindow.Handle` + `user32.IsWindowEnabled` (detección de cuadro de diálogo modal) | 2027 | `HiloPrincipal.CivilLibre` (`ventana principal sí`) | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, con el cuadro de `ARBAMCP` abierto el historial anotó `espera: hay un cuadro de diálogo abierto en Civil 3D`) |
| método | `Application.GetSystemVariable("CMDACTIVE")` desde el hilo principal para decidir si Civil 3D está libre | 2027 | `HiloPrincipal.CivilLibre`, `Escritura.Preparar` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `leer_variable CMDACTIVE` = 1 durante `_.LINE`; `listar_alineamientos` esperó y se descartó) |
| método | `Document.SendStringToExecute` de los ESC y de `_.UNDO _E` por la cola `Inmediato` con un comando esperando entrada | 2027 | `ejecutar_comando` (prueba "Civil 3D ocupado": `_.LINE` termina con `timeout con ESC`) | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `_.LINE` canceló a los 12 s y `listar_alineamientos` respondió 0,07 s después) |
| evento | `Application.Idle` como respaldo del despachador | 2027 | `HiloPrincipal` | existe en la DLL (respaldo; la validación no lo necesitó porque el despachador funcionó) |
| evento | `Document.CommandWillStart/CommandEnded/CommandCancelled/CommandFailed` y `DocumentCollection.DocumentCreated/DocumentToBeDestroyed` | 2027 | `Historial`, `ejecutar_comando` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `REGEN terminado`, `comando inexistente: no se inició`, líneas `Comando inicia/termina` en el historial) |

## 1.3.0 (proceso: copia de disco, deshacer, lotes): escrita sin compilar el plugin

Se validan con `herramientas-dev/VALIDACION_13.md` (el informe dice qué pasa a `ejecutado en Civil 3D 2027`). El
código propio compila contra los sustitutos de `herramientas-dev/CompilarSinCivil`, que no demuestran nada sobre la API.

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| hallazgo | Menú Deshacer: cada trabajo de contexto `Documento` es una entrada `Executefunction` (el pseudocomando de `ExecuteInCommandContextAsync`), con todas sus transacciones; una escritura = una entrada. No hacen falta `StartUndoMark`/`EndUndoMark` (retirados de la 1.3.0) | 2027 | todas las de escritura | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, paso 9: dos `asignar_objetivo` = dos `Executefunction`) |
| contexto | Lecturas (`listar_*`, `estado_corredor`, `punto_a_pk`, `pk_a_punto`, `cota_superficie`, `interseccion_ejes`) en contexto `Aplicacion` con `Document.LockDocument()` (bloqueo de escritura) y transacción | 2027 | todas las de lectura salvo `listar_lineas_muestreo` | ejecutado en Civil 3D 2027 (1.3.0, 28/09/2026: las lecturas responden, pero **cada una deja una entrada `Grupo de comandos` en Deshacer**: el bloqueo de escritura fuera de un comando abre un grupo de deshacer aunque no se cambie nada) |
| método | `Document.LockDocument(DocumentLockMode.Read, null, null, false)` para las lecturas y las simulaciones en contexto `Aplicacion`, con `StartTransaction` y `GetObject(ForRead)` bajo ese bloqueo | 2027 | las 14 lecturas, y las simulaciones de todas las escrituras (1.3.1); pasos 8 y 21 de `VALIDACION_13.md` | por verificar (si el bloqueo de lectura no admitiera la transacción, las lecturas responderían error `eLockViolation`; entonces se vuelve al bloqueo de escritura) |
| contexto | Simulaciones (`simular=true`) de las herramientas de escritura en contexto `Aplicacion` (`Servidor.Ejecutar` cambia el contexto) con bloqueo de lectura | 2027 | todas las de escritura con `simular=true`; paso 6 de `VALIDACION_13.md` | por verificar (en 1.3.0 una simulación dejaba una entrada `Executefunction`) |
| método | `System.IO.File.Copy` del `.dwg` abierto en Civil 3D desde un hilo aparte (`Task.Run`) mientras el dibujo sigue abierto (AutoCAD abre el archivo con compartición de lectura) | .NET 10 | copia de seguridad de todas las escrituras (`ArbaMcp.Nucleo.CopiaSeguridad`); pasos 7 y 10 | por verificar (probado en Linux con archivos normales, 83 pruebas xUnit) |
| propiedad | `FileInfo.LastWriteTimeUtc` y `FileInfo.Length` del `.dwg` abierto para decidir si la copia se reutiliza | .NET 10 | `CopiaSeguridad.Planificar` (`copia.reutilizada`, `copia.refleja_guardado_de`); paso 10 | por verificar |
| propiedad | `Database.Filename` como ruta del `.dwg` guardado (null o sin ruta en un dibujo nuevo → `SaveAs` en `%LOCALAPPDATA%`) | 2027 | `Escritura.RutaDibujo`; paso 16 | existe en la DLL (ya lo usaba 1.2.x); la rama SaveAs de un dibujo sin guardar con corredor no se cubre en la validación |
| método | `BaselineRegion.GetTargets()` / `SetTargets()` repetidos en la misma transacción (varias asignaciones sobre la misma región) | 2027 | `asignar_objetivos`; pasos 6 a 9 | existe en la DLL; el uso en lote, por verificar |
| propiedad | `AppliedAssemblySetting.FrequencyAlong*` asignadas a varias regiones en la misma transacción | 2027 | `establecer_frecuencias`; pasos 10 y 11 | existe en la DLL; el uso en lote, por verificar |
| comando | `_.UNDO 1` enviado por `ejecutar_comando` tras una escritura (revierte la herramienta entera) | 2027 | `probar_servidor.py --dwg` prueba 6 y `--fase 13`; pasos 9, 11 y 14 | por verificar (la entrada única por herramienta ya está demostrada; falta ejecutar el UNDO sobre una escritura y sobre un lote) |
| propiedad | `Pendiente.MsEspera` / `MsEjecucion` medidos alrededor de `ExecuteInCommandContextAsync` (`ms_espera`, `ms_ejecucion` del envoltorio) | — | todas; paso 2 | por verificar que `ms_espera` refleje la espera real con Civil 3D ocupado |
