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
propio y el rechazo de la 1.3.3.

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
| método | `Corridor.CorridorSurfaces` → `CorridorSurface.SurfaceId` / `Name` comparados con el objetivo para rechazar la superficie del propio corredor (`ComprobarNoSuperficieDelCorredor`) | 2027 | `asignar_objetivo`, `asignar_objetivos` (elemento a `fallidos`), `asignar_objetivos_superficie`; `VALIDACION_13` paso 9b y fase 13 | por verificar |
| método | Pila `Restauraciones` en memoria (handles de los `TargetIds` anteriores, `TargetToOption`, `UseSameSideTarget`) y `deshacer_objetivos`: `Database.TryGetObjectId(Handle)` para volver a los objetos anteriores y `SetTargets` con ellos, comprobando antes que los handles actuales son los que dejó la escritura | 2027 | `deshacer_objetivos` (1.3.3); `VALIDACION_13` pasos 9 y 14, prueba 6 de `--dwg` y fase 13 | por verificar |
| propiedad | `Pendiente.MsEspera` / `MsEjecucion` medidos alrededor de `ExecuteInCommandContextAsync` (`ms_espera`, `ms_ejecucion` del envoltorio) | — | todas; paso 2 | ejecutado en Civil 3D 2027 (1.3.1, 28/09/2026: `ms_espera` y `ms_ejecucion` enteros y coherentes en todas las respuestas, por ejemplo 16 y 7 ms en `asignar_objetivo`; con Civil 3D ocupado la llamada se descarta antes de responder, así que la espera larga no se midió) |
