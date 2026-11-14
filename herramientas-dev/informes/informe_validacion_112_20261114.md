# Informe de validación 1.12.0 (ejecutado sobre 1.14.1, rama `tarea/2.7-volumenes`)

- **Fecha:** 2026-11-14
- **Civil 3D:** Autodesk Civil 3D 2027 - Español Métrico (`es-ES`)
- **Plugin / Puente:** `ArbaMcp` `1.14.1` (53 herramientas del plugin + `buscar_api` del puente; en esta sesión `call_mcp_tool` invocó las herramientas del plugin por nombre)
- **Dibujo:** `PRUEBA_112.dwg` (copia de `PRUEBA_111.dwg`)

## Resumen de pasos

| Paso | Resultado | Respuesta literal (resumen / literal) | Tiempo (`ms_puente`) |
|---|---|---|---|
| 0. Instalar / `ping` | OK | `{"plugin": "ArbaMcp", "version": "1.14.1", "puerto": 8765, "dibujo": "C:\\Proyectos\\dibujos_prueba\\PRUEBA_112.dwg", "hay_dibujo": true, ...}` | 30 ms |
| 1. Preparar el dibujo | OK | `Eje 1` (0..831.3274), `Rasante 1` (FG, 3 PVIs), `Terreno` y `Terreno 2` (TIN, `corredor: null`), `Ensamblaje 1` (4 subensamblajes: `LaneSuperelevationAOR` + `DaylightBench` D/I), `Corredor 1` (`esta_desactualizado: false`, 1 línea base) | 14–42 ms |
| 2. Punto de partida | OK | `{"nombre": "Corredor 1", "esta_desactualizado": false, "reconstruir_automatico": false, "ultima_reconstruccion": null, "n_lineas_base": 1, "superficies": [], ...}` · `backups\`: 4 archivos | 11 ms |
| 3. Simulación (decisivo) | OK | `{"simulado": true, "herramienta": "crear_superficies_corredor", "accion": "Crear en el corredor 'Corredor 1' las superficies 'Corredor 1 - Top' ... y 'Corredor 1 - Datum' ...", "despues": {"superficies": ["Corredor 1 - Datum", "Corredor 1 - Top"], "top_voladizo": "TopLinks", "datum_voladizo": "BottomLinks", ...}}` | 23 ms |
| 4a. Error `NoExiste` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el corredor 'NoExiste'. Corredores del dibujo: Corredor 1."}` | 43 ms |
| 4b. Error nombres iguales | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: 'nombre_top' y 'nombre_datum' no pueden ser iguales ('Superficie C1'): pasa dos nombres distintos."}` | 17 ms |
| 4c. Error `nombre_top="Terreno"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe la superficie 'Terreno'. Usa listar_superficies o estado_corredor, o pasa otro 'nombre_top'."}` | 15 ms |
| 4d. Error `nombre_datum="Terreno"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe la superficie 'Terreno'. Usa listar_superficies o estado_corredor, o pasa otro 'nombre_datum'."}` | 19 ms |
| 4e. Error sin `corredor` | OK | Validación del puente: `1 validation error for crear_superficies_corredorArguments: args.corredor Field required` | — |
| 5. Creación real (decisivo) | OK | `{"simulado": false, "herramienta": "crear_superficies_corredor", ... "copia": {"metodo": "copia_de_disco", "reutilizada": false, "estado": "terminada"}, "datos": {"codigos_corredor": ["Base", "Bench", "Datum", "Daylight", "Daylight_Cut", "Daylight_Fill", "Pave", "Pave1", "Pave2", "Slope_Link", "SubBase", "Top"], "valores_voladizo": ["None", "TopLinks", "BottomLinks"], "reconstrucciones": 1, "ms_reconstruccion": 9, ...}, "avisos": null}` + `_.ZOOM` OK | 206 ms (+126 ms ZOOM) |
| 6. Lectura cruzada (decisivo) | OK | `listar_superficies`: `Corredor 1 - Top` y `Corredor 1 - Datum` (`tipo_superficie: "Corridor"`, `corredor: "Corridor 1"`, `esta_desactualizada: false`). `cota_superficie(1150,2000)`: Top=`103.75`, Datum=`103.3`, Terreno=`100.75`. `extension_superficie`: Top `n_puntos: 587`, Datum `n_puntos: 756` (ambas en x `1000..1600`, y `1985.6083..2313.7333`) | 10–22 ms |
| 7. Una entrada Deshacer y copia | OK | `backups\` pasó de 4 a 5 archivos (`PRUEBA_112_20261114_094034_crear_superficies_corredor.dwg`). | 8 ms |
| 8. Error nombre repetido | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe la superficie 'Corredor 1 - Top' (la genera el corredor 'Corredor 1')..."}`. `leer_historial` muestra `MCP ✗ crear_superficies_corredor no se ejecutó: falló la comprobación previa (simulación); sin contexto de comando, sin copia y sin entrada en Deshacer`. | 19 ms |
| 9. Nombres propios (simular) | OK | `{"simulado": true, "despues": {"superficies": ["Corredor 1 - Datum", "Corredor 1 - Top", "Rasante C1", "Subrasante C1"], "top_voladizo": "TopLinks", "datum_voladizo": "BottomLinks"}}` | 13 ms |
| 10. `_.UNDO 1` | Hallazgo | `_.UNDO 1` (`ejecutar_comando`) devolvió `"terminado"`, pero **las dos superficies siguieron existiendo** tanto en `estado_corredor` como en `listar_superficies` (nota: en el paso 5 se ejecutó `_.ZOOM` después de `crear_superficies_corredor`, por lo que `_.UNDO 1` pudo deshacer el zoom o bien `CorridorSurface` no se revierte con un solo `_.UNDO 1`). | 472 ms |
| 11. Listo para VALIDACION_113 | OK | Como las superficies siguieron tras el paso 10, no se volvieron a crear. `guardar_dibujo` OK: `"Dibujo guardado en C:\\Proyectos\\dibujos_prueba\\PRUEBA_112.dwg"`, `bytes: 1562842`, fecha en disco `2026-11-14 09:41:28`. | 175 ms |
| 12. Cierre (`leer_log`) | OK | `leer_log` (`ultimas_n: 40`) registró todas las llamadas con `ok: true` / `ok: false` según lo esperado. | — |

## Respuestas a las preguntas para la memoria técnica

1. **Valores de `OverhangCorrectionType` (`datos.valores_voladizo`):** `["None", "TopLinks", "BottomLinks"]`. Sí existían `TopLinks` y `BottomLinks` y quedaron así en `estado_corredor` (`correccion_voladizo`).
2. **Códigos de vínculo del corredor (`datos.codigos_corredor`):** `["Base", "Bench", "Datum", "Daylight", "Daylight_Cut", "Daylight_Fill", "Pave", "Pave1", "Pave2", "Slope_Link", "SubBase", "Top"]` (con `LaneSuperelevationAOR` y `DaylightBench` a cada lado).
3. **`IsBuild` (`construir`) y presencia en `listar_superficies`:** Tanto `datos.top.construir` como `datos.datum.construir` y `estado_corredor` devolvieron `true`. Ambas superficies aparecen en `listar_superficies` con `tipo_superficie: "Corridor"`, `tipo: "TinSurface"`, `corredor: "Corridor 1"`, `esta_desactualizada: false`.
4. **`AddCorridorExtentsBoundary`:** Sí funcionó antes de `Rebuild()`. Quedó `tipo_contorno: "OutsideBoundary"`, `contorno_es_extension: true`, `es_extension_corredor: true`, `usar_como_exterior: true`, `avisos: null`.
5. **Superficie del dibujo, cotas, puntos y reconstrucción:** `datos.top.superficie_dibujo = "Corredor 1 - Top"`, `datos.datum.superficie_dibujo = "Corredor 1 - Datum"`. En `(1150, 2000)`: Top = `103.75`, Datum = `103.3` (espesor `0.45` m), Terreno = `100.75`. Puntos (`extension_superficie`): Top = `587`, Datum = `756`. `ms_reconstruccion` = `9` ms.
6. **`_.UNDO 1` (paso 10):** No quitó las superficies (en `estado_corredor` y `listar_superficies` siguieron apareciendo `Corredor 1 - Top` y `Corredor 1 - Datum` con todos sus códigos y contornos intactos).
7. **`guardar_dibujo` (paso 11):** Funcionó directamente sin Ctrl+S (`ms: 165`), dejando `PRUEBA_112.dwg` guardado en disco (`2026-11-14 09:41:28`) con `Corredor 1`, `Corredor 1 - Top` y `Corredor 1 - Datum`.
8. **Visibilidad tras `_.ZOOM`:** No evaluado visualmente en pantalla (ejecución desatendida por MCP; comando `_.ZOOM` terminó OK en 117 ms).

## Miembros verificados en Civil 3D 2027 (sección 1.12.0, fecha 2026-11-14)
- `Corridor.GetLinkCodes()`
- `CorridorSurfaceCollection.Add(nombre)`
- `CorridorSurface.AddLinkCode(código, true)`
- `CorridorSurface.LinkCodes()`
- `CorridorSurface.IsLinkCodeAsBreakLine(código)`
- `CorridorSurface.OverhangCorrection` (`None`, `TopLinks`, `BottomLinks`)
- `CorridorSurface.IsBuild`
- `CorridorSurface.SurfaceId`
- `CorridorSurfaceBoundaryCollection.AddCorridorExtentsBoundary(nombre)`
- `CorridorSurface.BoundaryNames()`
- `CorridorSurfaceBoundary.BoundaryType` / `IsCorridorExtents` / `Name`
- `Corridor.Rebuild()` con superficies nuevas
