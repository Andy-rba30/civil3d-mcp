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
| método | `BaselineRegion.GetTargets()` / `SetTargets(SubassemblyTargetInfoCollection)` | 2027 | `asignar_objetivo`, `asignar_objetivos_superficie`, `listar_objetivos` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, dos escrituras reales de `asignar_objetivo`; 1.3.1: el cambio se aplica y se lee, **pero `_.UNDO 1` no lo revierte** aunque retire la entrada `Executefunction`, pasos 9, 14, 17 y 18; ver `RegistrarDeshacer` en la sección 1.3.x) |
| método | `CorridorSurface.AddPointCode` | — | `agregar_codigo_superficie_corredor` | no existe (se usa `AddFeatureLineCode`; `tipo=punto` se acepta como alias con aviso) |
| método | `CorridorSurface.AddLinkCode(string, bool)`, `AddFeatureLineCode(string)` | 2027 | `agregar_superficie_corredor`, `agregar_codigo_superficie_corredor` | existe en la DLL |
| método | `CorridorSurface.LinkCodes()`, `PointCodes()`, `FeatureLineCodes()` | 2027 | `estado_corredor`, `agregar_linea_rotura_superficie` | existe en la DLL (`estado_corredor` OK en 0,07 s; el informe no dice si el corredor tenía superficies) |
| propiedad | `CorridorSurface.Boundaries`, `CorridorSurface.SurfaceId` | 2027 | `estado_corredor`, `listar_superficies`, `agregar_contorno_superficie_corredor` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, `listar_superficies` y `estado_corredor` OK; misma reserva sobre las superficies de corredor) |
| método | `CorridorSurfaceBoundaryCollection.AddCorridorExtentsBoundary(string)`, `Add(string, ObjectId)` | 2027 | `agregar_contorno_superficie_corredor` | existe en la DLL |
| método | `CorridorSurfaceCollection.Add(string)` | 2027 | `agregar_superficie_corredor` | existe en la DLL |
| propiedad | `BaselineRegion.AppliedAssemblySetting.FrequencyAlongTangents`, `.FrequencyAlongCurves`, `.FrequencyAlongSpirals`, `.FrequencyAlongProfileCurves` | 2027 | `listar_regiones`, `establecer_frecuencia`, `establecer_frecuencias` | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, lectura en `listar_regiones` OK; 1.3.1: asignación individual y en lote aplicada y `_.UNDO 1` la revierte, pasos 10 y 11 y fase 13) |
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

## 1.3.x (proceso: copia de disco, deshacer, lotes): escrita sin compilar el plugin

Se validan con `herramientas-dev/VALIDACION_13.md` (el informe dice qué pasa a `ejecutado en Civil 3D 2027`). El
código propio compila contra los sustitutos de `herramientas-dev/CompilarSinCivil`, que no demuestran nada sobre la API.
La 1.3.1 se validó el 28/09/2026 (`herramientas-dev/informes/informe_validacion_131_20260928.md`): todo lo de abajo
pasó a `ejecutado` salvo el deshacer de los objetivos, que motivó la 1.3.2. La 1.3.2 demostró (9b) que Civil 3D no deshace
los objetivos ni desde su propia interfaz, y que `SetTargets` acepta la superficie del propio corredor: de ahí el deshacer
propio y el rechazo de la 1.3.3, validados el 28/09/2026 (`informe_validacion_133_20260928.md`: todos los pasos OK y las dos
suites de `probar_servidor.py` en `TODO OK`).

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| hallazgo | Menú Deshacer: cada trabajo de contexto `Documento` es una entrada `Executefunction` (el pseudocomando de `ExecuteInCommandContextAsync`), con todas sus transacciones; una escritura = una entrada. No hacen falta `StartUndoMark`/`EndUndoMark` (retirados de la 1.3.0) | 2027 | todas las de escritura | ejecutado en Civil 3D 2027 (1.2.2, 28/09/2026, paso 9: dos `asignar_objetivo` = dos `Executefunction`) |
| contexto | Lecturas (`listar_*`, `estado_corredor`, `punto_a_pk`, `pk_a_punto`, `cota_superficie`, `interseccion_ejes`) en contexto `Aplicacion` con `Document.LockDocument()` (bloqueo de escritura) y transacción | 2027 | todas las de lectura salvo `listar_lineas_muestreo` | ejecutado en Civil 3D 2027 (1.3.0, 28/09/2026: las lecturas responden, pero **cada una deja una entrada `Grupo de comandos` en Deshacer**: el bloqueo de escritura fuera de un comando abre un grupo de deshacer aunque no se cambie nada) |
| método | `Document.LockDocument(DocumentLockMode.Read, null, null, false)` para las lecturas y las simulaciones en contexto `Aplicacion`, con `StartTransaction` y `GetObject(ForRead)` bajo ese bloqueo | 2027 | las 14 lecturas, y las simulaciones de todas las escrituras (1.3.1); pasos 8 y 21 de `VALIDACION_13.md` | ejecutado en Civil 3D 2027 (1.3.1, 28/09/2026: las 14 lecturas y las simulaciones responden bajo el bloqueo de lectura y no dejan entrada en Deshacer; `VALIDACION_13` pasos 6, 8 y 21 sin `Grupo de comandos`) |
| contexto | Simulaciones (`simular=true`) de las herramientas de escritura en contexto `Aplicacion` (`Servidor.Ejecutar` cambia el contexto) con bloqueo de lectura | 2027 | todas las de escritura con `simular=true`; paso 6 de `VALIDACION_13.md` | ejecutado en Civil 3D 2027 (1.3.1, 28/09/2026, paso 6: la simulación no añadió ninguna entrada al menú Deshacer) |
| método | `System.IO.File.Copy` del `.dwg` abierto en Civil 3D desde un hilo aparte (`Task.Run`) mientras el dibujo sigue abierto (AutoCAD abre el archivo con compartición de lectura) | .NET 10 | copia de seguridad de todas las escrituras (`ArbaMcp.Nucleo.CopiaSeguridad`); pasos 7 y 10 | ejecutado en Civil 3D 2027 (1.3.1, 28/09/2026, paso 7: copia de 70,9 MB en 33 ms con el dibujo abierto, `estado: terminada`, `espera_ms: 0`) |
| propiedad | `FileInfo.LastWriteTimeUtc` y `FileInfo.Length` del `.dwg` abierto para decidir si la copia se reutiliza | .NET 10 | `CopiaSeguridad.Planificar` (`copia.reutilizada`, `copia.refleja_guardado_de`); paso 10 | ejecutado en Civil 3D 2027 (1.3.1, 28/09/2026, pasos 10 y 14 y fase 13: `reutilizada: true`, `ms: 0`, `espera_ms: 0`, `refleja_guardado_de` con la fecha del último guardado) |
| propiedad | `Database.Filename` como ruta del `.dwg` guardado (null o sin ruta en un dibujo nuevo → `SaveAs` en `%LOCALAPPDATA%`) | 2027 | `Escritura.RutaDibujo`; paso 16 | existe en la DLL (ya lo usaba 1.2.x); la rama SaveAs de un dibujo sin guardar con corredor no se cubre en la validación |
| método | `BaselineRegion.GetTargets()` / `SetTargets()` repetidos en la misma transacción (varias asignaciones sobre la misma región) | 2027 | `asignar_objetivos`; pasos 6 a 9 | ejecutado en Civil 3D 2027 (1.3.1, 28/09/2026, paso 7 y fase 13: el lote se aplica, `listar_objetivos` lo refleja y queda como una sola entrada `Executefunction`). **Pero `_.UNDO 1` no lo revierte**: la entrada desaparece y los objetivos siguen en el valor nuevo (pasos 9, 14, 17 y 18; un `reconstruir_corredor` posterior tampoco cambió el valor leído). Ver la fila de `RegistrarDeshacer` (1.3.2) |
| propiedad | `AppliedAssemblySetting.FrequencyAlong*` asignadas a varias regiones en la misma transacción | 2027 | `establecer_frecuencias`; pasos 10 y 11 | ejecutado en Civil 3D 2027 (1.3.1, 28/09/2026, pasos 10 y 11 y fase 13: las dos regiones cambian y `_.UNDO 1` las devuelve al valor anterior) |
| comando | `_.UNDO 1` enviado por `ejecutar_comando` tras una escritura (revierte la herramienta entera) | 2027 | `probar_servidor.py --dwg` prueba 6 y `--fase 13`; pasos 9, 11 y 14 | ejecutado en Civil 3D 2027 (1.3.1 y 1.3.2, 28/09/2026): revierte `establecer_frecuencias` (paso 11, fase 13) pero **no** los objetivos (pasos 9 y 14, prueba 6 de `--dwg`, fase 13), y tampoco los cambiados desde la interfaz (9b). Desde 1.3.3 los objetivos se deshacen con `deshacer_objetivos` |
| método | `Autodesk.Civil.DatabaseServices.Entity.Description` escrita y restaurada en el corredor abierto ForWrite, antes de `cambiar` (`Herramientas.RegistrarDeshacer`, llamado por `CambiarCorredor` y `LoteCorredor`), para que AutoCAD grabe el estado del corredor en la pila de deshacer | 2027 | todas las escrituras de corredor (1.3.2); `VALIDACION_13` pasos 9 y 14 | ejecutado en Civil 3D 2027 (1.3.2, 28/09/2026, paso 9: **sin efecto**, `_.UNDO 1` retiró la entrada y los objetivos siguieron en el valor nuevo). Retirado en 1.3.3 |
| hallazgo | Deshacer de objetivos cambiados desde la interfaz de Civil 3D (Propiedades de corredor → Parámetros → Asignación de objetivos) frente a `_.UNDO 1` enviado por `ejecutar_comando` | 2027 | diagnóstico 9b de `VALIDACION_13` (solo si el paso 9 falla) | ejecutado en Civil 3D 2027 (1.3.2, 28/09/2026, 9b: un cambio hecho desde Propiedades de corredor (Topografia → Subrasante, superficie válida) no volvió ni con `_.UNDO 1` ni con varios Ctrl+Z, aunque la entrada `Editcorridorproperties` sí se retira del menú). **Civil 3D no deshace los objetivos**: de ahí el deshacer propio de la 1.3.3 |
| hallazgo | `BaselineRegion.SetTargets` acepta como objetivo la superficie generada por el propio corredor (Civil 3D no la ofrece en Propiedades de corredor: sería circular) | 2027 | validación de la 1.3.2 (paso 9b: `OTRA` era 'Interseccion 3', la superficie del corredor 'Interseccion 3') | ejecutado en Civil 3D 2027 (1.3.2, 28/09/2026): la asignación se aplicó sin error; desde 1.3.3 `ComprobarNoSuperficieDelCorredor` la rechaza |
| método | `Corridor.CorridorSurfaces` → `CorridorSurface.SurfaceId` / `Name` comparados con el objetivo para rechazar la superficie del propio corredor (`ComprobarNoSuperficieDelCorredor`) | 2027 | `asignar_objetivo`, `asignar_objetivos` (elemento a `fallidos`), `asignar_objetivos_superficie`; `VALIDACION_13` paso 9b y fase 13 | ejecutado en Civil 3D 2027 (1.3.3, 28/09/2026, paso 9b: 'Interseccion 3' rechazada con `simular` y sin él, sin tocar el dibujo; en el lote va a `fallidos`; fase 13 OK) |
| método | Pila `Restauraciones` en memoria (handles de los `TargetIds` anteriores, `TargetToOption`, `UseSameSideTarget`) y `deshacer_objetivos`: `Database.TryGetObjectId(Handle)` para volver a los objetos anteriores y `SetTargets` con ellos, comprobando antes que los handles actuales son los que dejó la escritura | 2027 | `deshacer_objetivos` (1.3.3); `VALIDACION_13` pasos 9 y 14, prueba 6 de `--dwg` y fase 13 | ejecutado en Civil 3D 2027 (1.3.3, 28/09/2026, `VALIDACION_13` pasos 9 y 14, prueba 6 de `--dwg` y fase 13 `TODO OK`): devuelve el lote y la escritura individual, detecta el cambio posterior (`fallidos` con "ya no está como lo dejó la escritura"), deshace por `id`, y la entrada consumida desaparece de `pendientes` |
| propiedad | `Pendiente.MsEspera` / `MsEjecucion` medidos alrededor de `ExecuteInCommandContextAsync` (`ms_espera`, `ms_ejecucion` del envoltorio) | — | todas; paso 2 | ejecutado en Civil 3D 2027 (1.3.1, 28/09/2026: `ms_espera` y `ms_ejecucion` enteros y coherentes en todas las respuestas, por ejemplo 16 y 7 ms en `asignar_objetivo`; con Civil 3D ocupado la llamada se descarta antes de responder, así que la espera larga no se midió) |

## 1.4.0 (`extension_superficie`, superficies a Revit): escrita sin compilar

Herramienta de lectura `extension_superficie` (`Herramientas.Superficies.cs`, contexto `Aplicacion`, bloqueo de lectura)
para georreferenciar un modelo de Revit antes de vincular el DWG por coordenadas compartidas (flujo en
`PuenteMcp/INSTRUCCIONES_AGENTE.md`, sección 7; herramientas `get_project_coordinates`, `set_project_coordinates` y
`link_file` con `placement` en el MCP de Revit, rama `feature/coordenadas-compartidas`). Escrita el 12/10/2026 sin
Civil 3D: el código compila contra los sustitutos de `CompilarSinCivil`, los miembros nuevos entran como `por verificar`
y se validan con `herramientas-dev/VALIDACION_14.md`. Validada el 12/10/2026 (`informes/informe_validacion_14_20261012.md`): pasos 0 a 7 y 9 en OK, paso 8 omitido; el vínculo por coordenadas compartidas en Revit quedó a 1,3 km del origen interno sin aviso y la cota cruzada coincidió (263,80 m en ambos).

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| método | `Surface.GetGeneralProperties()` → `GeneralSurfaceProperties` | 2027 | `extension_superficie` | ejecutado en Civil 3D 2027 (1.4.0, 12/10/2026, `VALIDACION_14` paso 2 sobre `260716VC_SUP_HARTRE`, 23 355 puntos, 46 ms; informe `informes/informe_validacion_14_20261012.md`) |
| propiedad | `GeneralSurfaceProperties.MinimumCoordinateX`, `.MaximumCoordinateX`, `.MinimumCoordinateY`, `.MaximumCoordinateY`, `.MinimumElevation`, `.MaximumElevation`, `.NumberOfPoints` (uint; se lee con `Convert.ToInt64`) | 2027 | `extension_superficie` | ejecutado en Civil 3D 2027 (1.4.0, 12/10/2026, paso 2: mínimos, máximos y número de puntos iguales a Propiedades de superficie → Estadísticas) |
| propiedad | `CivilDocument.Settings.DrawingSettings.UnitZoneSettings` (`Autodesk.Civil.Settings.SettingsUnitZone`): `.CoordinateSystemCode` (string; vacío sin sistema asignado), `.DrawingUnits` (`Autodesk.Civil.DrawingUnitType`, `Meters`/`Feet`) | 2027 | `extension_superficie` (`sistema_coordenadas`; con `Feet` convierte la diagonal a metros para los avisos) | ejecutado en Civil 3D 2027 (1.4.0, 12/10/2026, paso 2: `UTM84-18S` y `Meters`, iguales a Configuración del dibujo → Unidades y zona) |

## 1.6.0 (registro como dataset: `pedido`, `id_log`, `aprobar_resultado`): escrita sin compilar

Escrita el 31/10/2026 sin Civil 3D. **No usa ningún miembro nuevo de la API de AutoCAD ni de Civil 3D**: el punto
común de registro (`Herramientas.EjecutarRegistrando`) y `aprobar_resultado` (`Herramientas.Seguridad.cs`, contexto
`Inmediato`, como `leer_log`) solo usan `DocumentManager.MdiActiveDocument` y `Database.Filename`, ya en la tabla de
partida, y archivos de disco. Lo que sí hay que confirmar en Civil 3D (`herramientas-dev/VALIDACION_16.md`): que
una lectura registrada en contexto `Aplicacion` y una llamada `Inmediato` siguen **sin dejar entrada en Deshacer**
(el registro es solo escritura de archivo), y que `ejecutar_comando` y las exportaciones (asíncronas) dejan su línea
por la cola `Inmediato` al terminar.

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| — | ninguno nuevo | — | `aprobar_resultado`, `Herramientas.EjecutarRegistrando` | sin miembros nuevos; comportamiento (Deshacer, línea de las asíncronas) por verificar en `VALIDACION_16` |

## 1.7.0 (`crear_superficie_tin`: superficie TIN desde grupo de puntos o archivo): escrita sin compilar

Escrita el 31/10/2026 sin Civil 3D. Todos los miembros de esta sección se **confirmaron en el catálogo de la memoria
técnica** (`buscar_api`, catálogo de Civil 3D 2027 extraído por metadatos) antes de usarlos: existen en la DLL con esa
firma, pero nadie los había ejecutado. Decide la validación `herramientas-dev/VALIDACION_17.md`. **Validada el 31/10/2026**
(`informes/informe_validacion_17_20261031.md`): pasos 0 a 11 en OK, los decisivos 2, 3, 4 y 6 incluidos; `Terreno` creada
desde `puntos_prueba.csv` con 8 puntos y 7 triángulos. Dos hallazgos: `_.UNDO 1` **no elimina** la superficie creada (paso 10)
y la lista de estilos de los errores sale como "ninguno legible" (paso 8). Hallazgo del catálogo:
los **intervalos de curvas de nivel** no son propiedades de la superficie sino del estilo
(`SurfaceStyle.ContourStyle.MinorContourInterval` / `MajorContourInterval`, lectura/escritura); como el plugin no
modifica ni crea estilos, la herramienta solo los **lee** para el aviso. No está en el catálogo ninguna propiedad de
`TinSurface`/`Surface` con intervalos de curvas, ni `PointFileFormatCollection` con un método `Contains` o indexador
por nombre confirmado (se recorre con `foreach` y se compara `PointFileFormat.Name`).

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| método estático | `TinSurface.Create(Database, string)` y `TinSurface.Create(string, ObjectId styleId)` | 2027 | `crear_superficie_tin` (`Escritura.CrearObjeto`) | `Create(Database, string)` ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, paso 3: crea `Terreno` con el estilo por defecto `Contours 5m and 25m (Background)`, 8 puntos y 7 triángulos; informe `informes/informe_validacion_17_20261031.md`); `Create(string, ObjectId styleId)` por verificar (paso 8 solo simuló con un estilo elegido, y la simulación no crea) |
| propiedad | `TinSurface.PointFilesDefinition` (`SurfaceDefinitionPointFiles`) y método `AddPointFile(string, PointFileFormat)` | 2027 | `crear_superficie_tin` con `archivo` | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, paso 3: `puntos_prueba.csv` con formato `PENZD (comma delimited)`, 8 líneas de puntos y 1 ignorada) |
| método estático | `PointFileFormatCollection.GetPointFileFormats(Database)`; enumeración (`GetEnumerator`), `Count`, `Dispose` | 2027 | `crear_superficie_tin` con `archivo` (elección del formato) | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, pasos 2, 3 y 7: el error por formato inexistente listó los 23 formatos del dibujo) |
| propiedad | `PointFileFormat.Name`, `PointFileFormat.Delimiter` | 2027 | `crear_superficie_tin` (elección del formato por nombre y separador) | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, pasos 2 y 3: `PENZD (comma delimited)` elegido automáticamente con separador coma; el informe no dice si `Delimiter` se leyó por reflexión o se decidió por el nombre) |
| propiedad | `CivilDocument.PointGroups` (`PointGroupCollection`): enumeración de `ObjectId`, `Contains(string)`, indexador | 2027 | `crear_superficie_tin` con `grupo_puntos` | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, paso 9: el error por grupo inexistente listó `_All Points`) |
| propiedad | `PointGroup.Name`, `PointGroup.PointsCount` (UInt32) | 2027 | `crear_superficie_tin` (comprobar que el grupo existe y tiene puntos; `puntos` previsto en `simular`) | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, paso 9: `_All Points` con `puntos: 2` en la simulación) |
| propiedad | `TinSurface.PointGroupsDefinition` (`SurfaceDefinitionPointGroups`) y método `AddPointGroup(ObjectId)` | 2027 | `crear_superficie_tin` con `grupo_puntos` | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, paso 9: el miembro se ejecutó, pero la creación terminó en el error controlado `InvalidOperationException: The surface has no point or triangle` porque el grupo solo tenía 2 puntos, sin triángulos posibles); la vía completa con un grupo de 3 o más puntos queda **por verificar** |
| método | `TinSurface.GetTinProperties()` → `TinSurfaceProperties.NumberOfTriangles` (Int32) | 2027 | `crear_superficie_tin` (`triangulos`) | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, paso 3: `triangulos: 7`) |
| método | `Surface.GetGeneralProperties()` → `NumberOfPoints`, `Minimum/MaximumCoordinateX/Y`, `Minimum/MaximumElevation` | 2027 | `crear_superficie_tin` (`puntos`, `extension`) | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, pasos 3 y 4 sobre `Terreno` recién creada: 8 puntos y extensión 1000..1020 / 2000..2020 / 100..102, igual que `extension_superficie`) |
| método | `Surface.Rebuild()` | 2027 | `crear_superficie_tin` (tras añadir el origen) | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, paso 3 sobre la superficie nueva: `esta_desactualizada: false`; ya lo ejecutaba `reconstruir_superficie`) |
| propiedad | `CivilDocument.Styles.SurfaceStyles` (`SurfaceStyleCollection`): `Contains(string)`, indexador por nombre, enumeración de `ObjectId` | 2027 | `crear_superficie_tin` con `estilo` | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, paso 8: `Contains` rechazó `NoExiste` y resolvió `Contours 5m and 25m (Background)` por nombre en la simulación) |
| propiedad | `Styles.StyleBase.Name` (lectura) | 2027 | `crear_superficie_tin` (lista de estilos en el error) | ejecutado en Civil 3D 2027 pero **no legible** (1.7.0, 31/10/2026, paso 8: la lista salió "ninguno legible"; igual en 1.8.0 paso 8 con estilos y conjuntos de etiquetas de alineamiento y en 1.9.0 paso 3 con los de perfil y vista). Pendiente: corregir la lectura del nombre del estilo en otra sesión |
| propiedad | `Entity.StyleName` (lectura), `Entity.StyleId` (lectura) de `Autodesk.Civil.DatabaseServices.Entity` | 2027 | `crear_superficie_tin` (`estilo` usado y lectura de los intervalos del estilo por defecto) | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, paso 3: `estilo: "Contours 5m and 25m (Background)"` releído de la superficie) |
| propiedad | `Styles.SurfaceStyle.ContourStyle` → `SurfaceContourStyle.MinorContourInterval`, `MajorContourInterval` (lectura) | 2027 | `crear_superficie_tin` (aviso de intervalos; **solo lectura**, nunca se asignan) | ejecutado en Civil 3D 2027 (1.7.0, 31/10/2026, pasos 2 y 3: el aviso de intervalos sin aplicar se devolvió sin error) |
| — | intervalos de curvas en la superficie | — | `crear_superficie_tin` | **no existe** en el catálogo de 2027: viven en el estilo; la herramienta devuelve aviso y `intervalos_curvas.aplicados = false` |


## 1.8.0 (`crear_alineamiento_desde_polilinea`: eje desde polilínea de tangentes con una curva por PI): escrita sin compilar

Escrita el 31/10/2026 sin Civil 3D. Todos los tipos, métodos y propiedades de esta sección se **confirmaron en el
catálogo de la memoria técnica** (`buscar_api`, catálogo de Civil 3D 2027 extraído por metadatos) antes de usarlos:
existen en la DLL con esa firma, pero nadie los había ejecutado. Decide la validación `herramientas-dev/VALIDACION_18.md`.
**Validada el 31/10/2026** (`informes/informe_validacion_18_20261031.md`): pasos 0 a 8 y 11 en OK, los decisivos 2, 4, 5 y 7
incluidos; pasos 9 (polilínea con arcos) y 10 (deshacer) omitidos, el eje `Eje 1` se conservó para la 1.9.0. Hallazgo del paso 0:
`ejecutar_comando` recorta los espacios finales del texto (`Trim`), así que un comando interactivo que se cierra con un espacio
doble final no termina; el agente lo resolvió con un archivo `.scr` (pendiente de corregir en el plugin).
**El catálogo no registra los valores de los enumerados**: `CurveParamType.Radius`, `CurveType.Compound` y
`SettingsObjectLayerType.Alignment` se resuelven en tiempo de ejecución por su nombre (`Herramientas.ValorEnum<T>`,
`Enum.TryParse`), así el plugin compila aunque el nombre cambie y, si no existe, el error lista los valores reales.
Vía de creación elegida: `Alignment.Create` con `PolylineOptions` **sin** curvas automáticas
(`AddCurvesBetweenTangents=false`: con `true` el radio lo decide la configuración de comandos del dibujo, no la API, y
el número de curvas creadas sería imprevisible) y después `AlignmentEntityCollection.AddFreeCurve` por PI con el radio
exacto. La alternativa si `AddFreeCurve` fallara con el radio: la sobrecarga `AddFreeCurve(int, int, Point3d
passThroughPoint)` con el punto medio de la curva (E = R·(sec(Δ/2) − 1) desde el PI sobre la bisectriz), confirmada en
el catálogo y sin enumerados.

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| método estático | `Alignment.Create(CivilDocument, PolylineOptions, string alignmentName, ObjectId siteId, ObjectId layerId, ObjectId styleId, ObjectId labelSetId)` | 2027 | `crear_alineamiento_desde_polilinea` (`Escritura.CrearObjeto`) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4: `Eje 1` de 831,3274 m con `siteId = ObjectId.Null`, `SiteName` devolvió nulo; informe `informes/informe_validacion_18_20261031.md`); `labelSetId = ObjectId.Null` sigue **por confirmar** (el dibujo tenía el conjunto `All Labels`) |
| struct | `PolylineOptions` con `PlineId`, `AddCurvesBetweenTangents`, `EraseExistingEntities` (lectura/escritura) | 2027 | `crear_alineamiento_desde_polilinea` (siempre `AddCurvesBetweenTangents=false`, `EraseExistingEntities=false`) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4: polilínea con handle `21B84`, 4 vértices, sin curvas automáticas y sin borrar la polilínea) |
| propiedad | `Alignment.Entities` (`AlignmentEntityCollection`): `Count`, `GetEntityByOrder(int)` | 2027 | `crear_alineamiento_desde_polilinea` (tangentes creadas desde la polilínea, en orden; curvas releídas) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4: 3 tangentes releídas en orden y 2 curvas añadidas) |
| método | `AlignmentEntityCollection.AddFreeCurve(int previousEntityId, int nextEntityId, double paramValue, CurveParamType paramType, bool isGreaterThan180, CurveType curveType)` | 2027 | `crear_alineamiento_desde_polilinea` (una curva libre por PI con `paramValue = radio`) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4: dos curvas libres con radio 80 exacto, longitud 125,6637 cada una); los valores `CurveParamType.Radius` y `CurveType.Compound` resueltos por nombre en tiempo de ejecución **quedan confirmados** por la ejecución |
| propiedad | `AlignmentEntity.EntityId` (Int32), `AlignmentEntity.EntityType` | 2027 | `crear_alineamiento_desde_polilinea` (ids de las tangentes para `AddFreeCurve`; el tipo se decide con `is AlignmentLine` / `is AlignmentArc`, sin el enumerado) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4: ids de las tangentes usados en `AddFreeCurve`; tipos `AlignmentLine` y `AlignmentArc` distinguidos) |
| clase | `AlignmentLine`, `AlignmentArc` (ambas derivan de `AlignmentCurve` → `AlignmentEntity`) | 2027 | `crear_alineamiento_desde_polilinea` | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4) |
| propiedad | `AlignmentCurve.StartStation`, `AlignmentCurve.EndStation` (lectura) | 2027 | `crear_alineamiento_desde_polilinea` (`progresiva_pc`, `progresiva_pt` y longitud de cada curva como `EndStation − StartStation`) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4: PC 220 y PT 345,6637 en el PI 1, PC 485,6637 y PT 611,3274 en el PI 2, iguales a la simulación) |
| propiedad | `AlignmentArc.Radius`, `AlignmentArc.Delta`, `AlignmentArc.PIStation`, `AlignmentArc.Clockwise` (lectura) | 2027 | `crear_alineamiento_desde_polilinea` (`curvas` leídas y verificación del radio ±0,01) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4: radio 80, `deflexion_grados: 90` en ambos PI, así que `Delta` **sí viene en radianes** y la conversión es correcta; `Clockwise` false/true coherente con giro izquierda/derecha; `PIStation` 300 y 565,6637) |
| propiedad | `Alignment.Length`, `Alignment.StartingStation`, `Alignment.EndingStation` | 2027 | `crear_alineamiento_desde_polilinea` (relectura) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, pasos 4 y 5: 0 a 831,3274, igual en `listar_alineamientos`) |
| propiedad | `Alignment.SiteName` (lectura), `Entity.StyleName` (lectura, por reflexión con `Api.Leer`), `Entity.Layer` | 2027 | `crear_alineamiento_desde_polilinea` (`sitio`, `estilo`, `capa` releídos) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4: `sitio` nulo, `estilo: "Proposed"`, `capa: "C-ROAD"`) |
| propiedad | `CivilDocument.Styles.AlignmentStyles` (`AlignmentStyleCollection`): `Contains(string)`, indexador por nombre, enumeración de `ObjectId` | 2027 | `crear_alineamiento_desde_polilinea` con `estilo` y estilo por defecto de reserva | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 8: `Contains` rechazó un estilo inexistente con la lista "ninguno legible"); el indexador con un estilo existente pasado en `estilo` no se ejercitó, **por verificar** |
| propiedad | `CivilDocument.Styles.LabelSetStyles.AlignmentLabelSetStyles` (`AlignmentLabelSetStyleCollection`): `Contains(string)`, indexador, enumeración | 2027 | `crear_alineamiento_desde_polilinea` con `conjunto_etiquetas` | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 8: `Contains` rechazó un conjunto inexistente con la lista "ninguno legible"); el indexador con un conjunto existente pasado en `conjunto_etiquetas` no se ejercitó, **por verificar** |
| método | `CivilDocument.Settings.GetSettings<SettingsAlignment>()` → `StyleSettings.AlignmentStyleId.Value`, `AlignmentStyle.Value`, `AlignmentLabelSetId.Value`, `AlignmentLabelSet.Value` (`PropertyObjectId`/`PropertyString`) | 2027 | `crear_alineamiento_desde_polilinea` (estilo y conjunto de etiquetas **por defecto** del dibujo) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, pasos 2 y 4: estilo por defecto `Proposed` y conjunto `All Labels`, sin aviso de reserva) |
| método | `CivilDocument.Settings.DrawingSettings.ObjectLayerSettings.GetObjectLayerSetting(SettingsObjectLayerType)` → `SettingsObjectLayer.LayerId`, `LayerName` | 2027 | `crear_alineamiento_desde_polilinea` (capa de objetos de las alineaciones) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 4: capa `C-ROAD`); el valor `SettingsObjectLayerType.Alignment` resuelto por nombre **queda confirmado** por la ejecución |
| método | `CivilDocument.GetSiteIds()` → `Site.Name` | 2027 | `crear_alineamiento_desde_polilinea` con `sitio` | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, paso 8: el error por sitio inexistente listó `Sitio 1`, `Km 3+740` y `Km 2+600`) |
| método / propiedad | `Polyline.NumberOfVertices`, `GetPoint2dAt(int)`, `GetBulgeAt(int)`, `Closed`, `Normal`, `Layer`; `Database.TryGetObjectId(Handle)`; `LayerTable.Has(string)` e indexador | AutoCAD 2027 | `crear_alineamiento_desde_polilinea` (lectura de la polilínea por handle o capa) | ejecutado en Civil 3D 2027 (1.8.0, 31/10/2026, pasos 2, 4 y 8: polilínea leída por handle `21B84` con 4 vértices y bulge 0 en todos, `LayerTable.Has` rechazó una capa inexistente); el rechazo de una polilínea **con arcos** (paso 9) se omitió, solo lo cubren las pruebas de `ArbaMcp.Nucleo` |
| — | `CivilDocument.GetSitelessAlignmentIds()` | 2027 | — | confirmado en el catálogo pero **no se usa**: el nombre se comprueba con `GetAlignmentIds()` (todos los ejes, con y sin sitio), como `listar_alineamientos` |
| — | `AlignmentType.Centerline` | 2027 | — | **no se usa**: la sobrecarga de `Create` con `PolylineOptions` no lleva tipo (el eje queda del tipo por defecto, eje central); el valor del enumerado no está en el catálogo |

## 1.9.0 (`crear_perfil_terreno_y_vista`: perfil de terreno de un eje sobre una superficie y su vista de perfil): escrita sin compilar

Escrita el 31/10/2026 sin Civil 3D. Todos los tipos, métodos y propiedades de esta sección se **confirmaron en el
catálogo de la memoria técnica** (`buscar_api`, catálogo de Civil 3D 2027 extraído por metadatos) antes de usarlos:
existen en la DLL con esa firma, pero nadie los había ejecutado. Decide la validación `herramientas-dev/VALIDACION_19.md`.
**Validada el 31/10/2026** (`informes/informe_validacion_19_20261031.md`): pasos 0 a 8 y 10 en OK, los decisivos 2, 4, 5, 6 y 8
incluidos; paso 9 (deshacer) omitido, los objetos se conservaron. Hallazgo del paso 2: el punto de inserción automático usa la
extensión del dibujo entera (`Extmin`/`Extmax`), así que con contenido lejano la vista quedó en x ≈ 602 712 con un eje en x ≈ 1000..1600.
**El catálogo no registra los valores de los enumerados**: `SettingsObjectLayerType.Profile` se resuelve por nombre en
tiempo de ejecución (`Herramientas.ValorEnum<T>`); si no existe, el perfil va a la capa actual con aviso (no es error).
El tipo del perfil (`ProfileType.EG`) **no se pasa**: lo decide `CreateFromSurface`; se relee con `Profile.ProfileType`
y se compara como texto con `"EG"` (si sale otro, aviso). Los dos objetos se crean en la **misma transacción** de
`Escritura.CrearObjeto` (`Crear` devuelve el id del perfil; la vista se crea dentro y, si lanza, la transacción se
descarta y el perfil desaparece con ella).

| Tipo | Miembro | Versión | Herramienta que lo usa | Estado |
|---|---|---|---|---|
| método estático | `Profile.CreateFromSurface(string profileName, ObjectId alignmentId, ObjectId surfaceId, ObjectId layerId, ObjectId styleId, ObjectId labelSetId)` | 2027 | `crear_perfil_terreno_y_vista` (`Escritura.CrearObjeto`, paso 1 de `Crear`) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 4 y 8: perfiles `Terreno - Terreno` y `EG 2`, tipo `EG`, de 0 a 20 porque la superficie no cubre todo el eje; informe `informes/informe_validacion_19_20261031.md`); `labelSetId = ObjectId.Null` sigue **por confirmar** (el dibujo tenía el conjunto `_No Labels`) |
| propiedad | `Profile.ElevationMin`, `Profile.ElevationMax` (lectura) | 2027 | `crear_perfil_terreno_y_vista` (`cota_minima`, `cota_maxima` releídas; verificación `cota_maxima ≥ cota_minima`) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, paso 4: cotas 100 y 101 m) |
| propiedad | `Profile.StartingStation`, `Profile.EndingStation`, `Profile.ProfileType`, `Profile.Name` | 2027 | `crear_perfil_terreno_y_vista` (relectura) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 4 y 5 sobre el perfil nuevo: 0 a 20, tipo `EG`, igual en `listar_perfiles`) |
| método | `Alignment.GetProfileViewIds()` | 2027 | `crear_perfil_terreno_y_vista` (comprobar que el nombre de la vista está libre y releer la vista) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 4 y 7: relectura de la vista y rechazo del nombre repetido `Eje 1 - Vista`) |
| método estático | `ProfileView.Create(ObjectId alignmentId, Point3d insertPosition, string profileViewName, ObjectId profileViewBandSetId, ObjectId profileViewStyleId)` → `ObjectId` | 2027 | `crear_perfil_terreno_y_vista` (paso 2 de `Crear`, misma transacción que el perfil) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 4 y 8: vistas `Eje 1 - Vista` y `Vista 2`, en la misma transacción que el perfil y con una sola entrada de Deshacer, paso 6); `profileViewBandSetId = ObjectId.Null` sigue **por confirmar** (el dibujo tenía el conjunto `EG-FG Elevations and Stations`) |
| propiedad | `ProfileView.Name`, `ProfileView.Location` (`Graph.Location`, Point3d), `ProfileView.StationStart`, `ProfileView.StationEnd`, `ProfileView.AlignmentName`, `Entity.StyleName` (por reflexión con `Api.Leer`) | 2027 | `crear_perfil_terreno_y_vista` (relectura de la vista: nombre, punto de inserción, progresivas, estilo) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 4 y 8: `Location` devolvió exactamente el punto pasado a `Create`, (602711,7935; 1999,25) y (602711,794; 1499,25); progresivas 0 a 831,3274; estilo `trujillo`) |
| propiedad / método | `ProfileView.Bands` (`ProfileViewBandSet`): `GetBottomBandItems()`, `GetTopBandItems()` → `ProfileViewBandItemCollection` (enumerable, `Count`); `SetBottomBandItems(col)`, `SetTopBandItems(col)` | 2027 | `crear_perfil_terreno_y_vista` (asignar el perfil a las bandas y contar `bandas`) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, paso 4: `bandas: 1`, `bandas_asignadas: 1`, sin aviso) |
| propiedad | `ProfileViewBandItem.Profile1Id`, `ProfileViewBandItem.Profile2Id` (lectura/escritura) | 2027 | `crear_perfil_terreno_y_vista` (los dos = el perfil de terreno, el único que existe) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 4 y 6: el perfil quedó como `Profile1` y `Profile2` de la banda tras `SetBottomBandItems`, "Perfil asignado como fuente (Profile1 y Profile2) de 1 banda de la vista"); que la banda **muestre** las cotas en pantalla no consta en el informe |
| propiedad | `CivilDocument.Styles.ProfileStyles`, `Styles.ProfileViewStyles`, `Styles.ProfileViewBandSetStyles`, `Styles.LabelSetStyles.ProfileLabelSetStyles` (`Contains(string)`, indexador por nombre, enumeración de `ObjectId`, de `TreeNodeCollectionBase`) | 2027 | `crear_perfil_terreno_y_vista` con `estilo_perfil`, `estilo_vista`, `conjunto_bandas`, `conjunto_etiquetas_perfil` y como reserva de los valores por defecto | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, paso 3: `Contains` rechazó estilos y conjuntos inexistentes con la lista "ninguno legible", sin abortar); el indexador con nombres existentes pasados como parámetro no se ejercitó, **por verificar** |
| clase | `Autodesk.Civil.DatabaseServices.Styles.ProfileViewBandSetStyle` (comprobación `is` sobre el objeto de la configuración) | 2027 | `crear_perfil_terreno_y_vista` (conjunto de bandas por defecto) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 2 y 4: el conjunto por defecto `EG-FG Elevations and Stations` pasó la comprobación, sin aviso de reserva) |
| método | `CivilDocument.Settings.GetSettings<SettingsProfile>()` → `StyleSettings.ProfileStyleId.Value`, `ProfileStyle.Value`, `ProfileLabelSetId.Value`, `ProfileLabelSet.Value` | 2027 | `crear_perfil_terreno_y_vista` (estilo y conjunto de etiquetas de perfil **por defecto**) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 2 y 4: estilo `Existing Ground Profile` y conjunto `_No Labels`) |
| método | `CivilDocument.Settings.GetSettings<SettingsProfileView>()` → `Styles.ProfileViewStyleId.Value`, `ProfileViewStyle.Value`, `ProfileViewBandStyleId.Value`, `ProfileViewBandStyle.Value` (ojo: la propiedad se llama `Styles`, no `StyleSettings`) | 2027 | `crear_perfil_terreno_y_vista` (estilo de vista y conjunto de bandas **por defecto**) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 2 y 4: estilo `trujillo` y conjunto `EG-FG Elevations and Stations`; `ProfileViewBandStyleId` **sí** fue un conjunto de bandas, no hizo falta la reserva) |
| método | `ObjectLayerSettings.GetObjectLayerSetting(SettingsObjectLayerType.Profile)` → `LayerId`, `LayerName` | 2027 | `crear_perfil_terreno_y_vista` (capa de objetos de los perfiles) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, paso 4: capa `C-ROAD-PROF`); el valor `SettingsObjectLayerType.Profile` resuelto por nombre **queda confirmado** por la ejecución |
| propiedad | `Entity.GeometricExtents` (`Extents3d.MinPoint`/`MaxPoint`) sobre `Alignment` y `Surface`; `Database.Extmin`, `Database.Extmax` (lectura, sin `UpdateExt`) | AutoCAD 2027 | `crear_perfil_terreno_y_vista` (punto de inserción automático a la derecha de lo dibujado) | ejecutado en Civil 3D 2027 (1.9.0, 31/10/2026, pasos 2 y 4: el punto automático previsto y el leído coinciden, x = 602711,7935). **Hallazgo**: `Extmin`/`Extmax` abarcan todo el dibujo, así que con contenido lejano la vista quedó en x ≈ 602 712 aunque el eje está en x ≈ 1000..1600; pendiente decidir si usar solo la extensión del eje |
| — | `ProfileView.ElevationMin/ElevationMax`, `StationRangeMode`, `ElevationRangeMode` | 2027 | — | confirmados en el catálogo pero **no se usan**: la vista se crea con los rangos automáticos de Civil 3D (todo el eje y toda la cota del perfil) |
| — | `Alignment.GetProfileViewIds()` como lectura general | 2027 | — | **no hay herramienta de lectura de vistas de perfil** (`listar_vistas_perfil`): propuesta para otra sesión; el error por nombre repetido explica dónde verlas en el Prospector |
