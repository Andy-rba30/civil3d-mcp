# Informes de validación consolidados (2026-11-14)


---

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

---

# Informe de validación 1.13.0 (ejecutado sobre 1.14.1, rama `tarea/2.7-volumenes`)

- **Fecha:** 2026-11-14
- **Civil 3D:** Autodesk Civil 3D 2027 - Español Métrico (`es-ES`)
- **Plugin / Puente:** `ArbaMcp` `1.14.1` (53 herramientas del plugin + `buscar_api` del puente)
- **Dibujo:** `PRUEBA_112.dwg` (estado guardado al final de `VALIDACION_112.md`)

## Resumen de pasos

| Paso | Resultado | Respuesta literal (resumen / literal) | Tiempo (`ms_puente`) |
|---|---|---|---|
| 0. Comprobar instalación | OK | `ping` → `version: "1.14.1"` | — |
| 1. Comprobar dibujo | OK | `Eje 1` (0..831.3274), `Terreno`, `Terreno 2`, `Corredor 1 - Top`, `Corredor 1 - Datum`, `Corredor 1` (`esta_desactualizado: false`) | — |
| 2. Punto de partida | OK | `backups\`: 5 archivos (antes de `crear_secciones`). No se llamó a `listar_lineas_muestreo`. | — |
| 3. Simulación (decisivo) | OK | `{"simulado": true, "despues": {"existe": true, "numero_lineas": 43, "numero_vistas": 43, "progresiva_primera": 0, "progresiva_ultima": 831.3274, "fuentes_muestreadas": ["Terreno", "Terreno 2", "Corredor 1 - Top", "Corredor 1 - Datum", "Corredor 1"], "filas_por_columna": 5, "columnas": 9, "estilo_vista": "Road Section", "punto_insercion": {"x": 1660, "y": 2000}}}` | 32 ms |
| 4.1 `NoExiste` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el alineamiento 'NoExiste'. Usa listar_alineamientos."}` | 15 ms |
| 4.2 `intervalo: 0` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: El 'intervalo' debe ser un número mayor que 0 (metros entre líneas de muestreo); llegó 0."}` | 8 ms |
| 4.3 `ancho_izquierdo: -5` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: El 'ancho_izquierdo' debe ser un número mayor que 0 (metros de franja a la izquierda del eje); llegó -5."}` | 9 ms |
| 4.4 `fin: 900` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: El 'fin' (900) queda fuera del eje 'Eje 1' (0 a 831.327): debe estar entre 0 y 831.327. Usa listar_alineamientos para ver el rango."}` | 15 ms |
| 4.5 `intervalo: 1` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Con un 'intervalo' de 1 m entre 0 y 831.327 saldrían unas 832 líneas de muestreo; el máximo por llamada es 500. Usa un intervalo mayor o parte el tramo con 'inicio' y 'fin'."}` | 14 ms |
| 4.6 `filas_por_columna: 2.5` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: 'filas_por_columna' debe ser un número entero mayor o igual que 1 (vistas de sección por columna de la cuadrícula); llegó 2.5."}` | 7 ms |
| 4.7 `fuentes: ["NoExiste"]` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe la fuente 'NoExiste' para las líneas de muestreo. Fuentes disponibles: Terreno (superficie), Terreno 2 (superficie), Corredor 1 - Top (superficie_corredor), Corredor 1 - Datum (superficie_corredor), Corredor 1 (corredor). Usa listar_superficies y listar_corredores."}` | 21 ms |
| 4.8 `estilo_vista: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el estilo de vista de sección 'NoExiste' en el dibujo. Disponibles: Land Desktop Sections, Road Section. ..."}` | 20 ms |
| 4.9 Solo `Terreno`, sin vistas | OK | `{"simulado": true, "despues": {"existe": true, "numero_lineas": 10, "fuentes_muestreadas": ["Terreno"], "columnas": 0}}` | 11 ms |
| 5. Creación real (decisivo) | OK | `{"simulado": false, "despues": {"numero_lineas": 43, "numero_vistas": 43, "columnas": 9, "estilo_vista": "Road Section", "punto_insercion": {"x": 1660, "y": 2000}}, "datos": {"lineas_sin_secciones": 0, "extensiones_leidas": 43, "solapes": 0, "grupos_vistas": {"numero": 0, "grupos": [], "individuales": 43}}}` | 476 ms |
| 6. Vistas en pantalla (decisivo) | OK (por coordenadas) | `_.ZOOM _E` (114 ms) y `_.ZOOM _W 1640,1980 2268,2182` (117 ms) ejecutados OK. Coordenadas `vistas_colocadas`: `0+000.00` (1,1), `0+080.00` (5,1), `0+100.00` (1,2), `0+831.33` (3,9), `solapes: 0`. | 231 ms |
| 7. Deshacer y copia (decisivo) | OK | `backups\` pasó de 5 a 6 archivos (`PRUEBA_112_20261114_094227_crear_secciones.dwg`). | — |
| 8. Lectura cruzada | OK | `listar_lineas_muestreo` (`fuentes: true`): `Eje 1 - Muestreo`, `n_lineas: 43`, `inicio: 0`, `fin: 831.3274`, las 5 fuentes con `muestreada: true`. | 33 ms |
| 9. Segundo grupo `Muestreo 100` (decisivo) | **FALLO (hallazgo)** | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Sample line name should not duplicate."}` — Al existir ya `Eje 1 - Muestreo` en `Eje 1` con líneas nombradas `0+000.00`, `0+100.00`, etc., Civil 3D rechaza crear un segundo grupo en el mismo alineamiento con los mismos nombres de línea. | 43 ms |
| 10. Deshacer (decisivo) | No evaluable para `Muestreo 100` | Como el paso 9 falló con rollback de transacción, `Muestreo 100` no llegó a crearse. `_.UNDO 1` terminó OK (144 ms) y `Eje 1 - Muestreo` siguió con 43 líneas. | 144 ms + 38 ms |
| 11. Error nombre repetido | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe el grupo de líneas de muestreo 'Eje 1 - Muestreo' en el alineamiento 'Eje 1'. Usa listar_lineas_muestreo o pasa otro 'nombre'."}`. `leer_historial` confirma `MCP ✗ crear_secciones no se ejecutó: falló la comprobación previa (simulación)...` sin `EXECUTEFUNCTION`. | 15 ms |
| 12. Cierre (`leer_log`) | OK | `leer_log` (`ultimas_n: 40`) registró todas las llamadas. No se guardó el dibujo. | — |

## Respuestas a las preguntas para la memoria técnica

1. **Fuentes en `datos.fuentes` (paso 5):**
   - `Terreno` (`clase: "superficie"`, `tipo: "TinSurface"`, `muestreada: true`)
   - `Terreno 2` (`clase: "superficie"`, `tipo: "TinSurface"`, `muestreada: true`)
   - `Corredor 1` (`clase: "corredor"`, `tipo: "Corridor"`, `muestreada: true`)
   - `Corredor 1 - Top` (`clase: "superficie_corredor"`, `tipo: "CorridorSurface"`, `muestreada: true`)
   - `Corredor 1 - Datum` (`clase: "superficie_corredor"`, `tipo: "CorridorSurface"`, `muestreada: true`)
   Coincidieron en conjunto con `fuentes_previstas` (en orden distinto). No aparecieron fuentes de otra clase.
2. **Secciones en la misma transacción:** Sí (`lineas_sin_secciones: 0`). Cada línea (ej. `0+000.00`, `0+420.00`, `0+831.33`) tiene 5 secciones: `['Terreno', 'Terreno 2', 'Corredor 1', 'Corredor 1 Corredor 1 - Top', 'Corredor 1 Corredor 1 - Datum']`.
3. **`Entity.GeometricExtents` y cuadrícula:** `extensiones_leidas: 43`, `solapes: 0`. Las vistas tienen `ancho: 60`, `alto: 60` (la última `0+831.33` tiene `ancho: 60`, `alto: 50`).
4. **Grupos de vistas (`datos.grupos_vistas`):** `{"numero": 0, "grupos": [], "individuales": 43}` (`SectionView.Create` las dejó como vistas individuales).
5. **`_.UNDO 1` (paso 10):** No aplicable sobre `Muestreo 100` porque el paso 9 falló antes de crear el grupo (`Sample line name should not duplicate.`).
6. **Anchos y lado (paso 9):** No verificado por el fallo de nombre duplicado de línea de muestreo al crear un segundo grupo en el mismo alineamiento.
7. **Estilos de vista de sección:** Por defecto `Road Section`. Disponibles (paso 4.8): `Land Desktop Sections, Road Section`. No se pudo verificar en paso 9 por el fallo de nombre duplicado.
8. **Tiempos (`ms`):** Paso 5 = `459` ms (`ms_puente: 476`); Paso 9 = error en `43` ms (`ms_puente`).

## Miembros verificados en Civil 3D 2027 (sección 1.13.0, fecha 2026-11-14)
- `SampleLineGroup.Create`
- `SampleLineGroup.GetSectionSources` / `SectionSource.IsSampled`
- `SampleLine.Create(nombre, grupo, Point2dCollection)` (nota: falla si el nombre de línea ya existe en otro grupo del mismo alineamiento: `Sample line name should not duplicate.`)
- `Alignment.PointLocation` / `SampleLine.Vertices` (anchos leídos = 30 / 30)
- `SampleLine.GetSectionIds` / `Section.SourceName`
- `SectionView.Create` / `SectionView.StyleId`
- `Entity.GeometricExtents` en `SectionView`
- `SectionViewGroups` / `IndividualSectionViewGroup` (`individuales: 43`)
- `SettingsSectionView`

---

# Informe de validación 1.14.0 (ejecutado sobre 1.14.1, rama `tarea/2.7-volumenes`)

- **Fecha:** 2026-11-14
- **Civil 3D:** Autodesk Civil 3D 2027 - Español Métrico (`es-ES`)
- **Plugin / Puente:** `ArbaMcp` `1.14.1` (53 herramientas del plugin + `buscar_api` del puente)
- **Dibujo:** `PRUEBA_112.dwg` (misma sesión tras `VALIDACION_113.md`, sin guardar)

## Resumen de pasos

| Paso | Resultado | Respuesta literal (resumen / literal) | Tiempo (`ms_puente`) |
|---|---|---|---|
| 0. Comprobar instalación | OK | `ping` → `version: "1.14.1"` | — |
| 1. Comprobar dibujo | OK | `Terreno`, `Terreno 2`, `Corredor 1 - Top`, `Corredor 1 - Datum`, `Corredor 1`, `Eje 1 - Muestreo` (43 líneas, 0..831.3274). No existía `Corredor 1 - Volumen`. | — |
| 2. Punto de partida | OK | `backups\`: 6 archivos. | — |
| 3. Simulación sin nombres | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Hay varias superficies TIN que pueden ser el terreno: Terreno, Terreno 2. Pasa 'terreno' con el nombre de la que quieres usar como base (ver listar_superficies)."}` | 27 ms |
| 4. Simulación con datos (decisivo) | OK con aviso (hallazgo) | `{"simulado": true, "despues": {"existe": true, "tipo": "TinVolume", "superficie_base": "Terreno", "superficie_comparacion": "Corredor 1 - Datum", "factor_corte": 1.25, "factor_relleno": 1.1, "estilo": "(estilo por defecto del dibujo)", "grupo_muestreo": "Eje 1 - Muestreo", "lineas_tabla": 43}}`. Aviso: `"El grupo 'Eje 1 - Muestreo' no tiene ninguna línea con secciones de 'Terreno' y de 'Corredor 1 - Datum': la tabla saldrá sin volúmenes..."` | 23 ms |
| 5.1 `factor_esponjamiento: 0` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: 'factor_esponjamiento' debe ser mayor que 0 (se recibió 0). Sin factor, pasa 1 o no lo pases."}` | 9 ms |
| 5.2 `factor_compactacion: 125` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: 'factor_compactacion' = 125 es demasiado grande (máximo 5): un factor de 25 % se escribe 1.25, no 125 ni 25."}` | 8 ms |
| 5.3 `terreno: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe la superficie 'NoExiste' ('terreno'). Usa listar_superficies."}` | 16 ms |
| 5.4 `corredor: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el corredor 'NoExiste'. Corredores del dibujo: Corredor 1. Usa listar_corredores."}` | 15 ms |
| 5.5 Base y comparación iguales | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: La superficie base y la de comparación no pueden ser la misma ('Terreno'): ..."}` | 19 ms |
| 5.6 Datum no es del corredor | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: La superficie 'Terreno' no es del corredor 'Corredor 1' (no la genera ningún corredor)..."}` | 29 ms |
| 5.7 `estilo: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el estilo de superficie 'NoExiste' en el dibujo. Estilos disponibles: Contours 5m and 25m (Background), Border Only, Slope Banding (2D), Contours 2m and 10m (Background), Elevation Banding 10m Interval (2D), Contours and Triangles, Contours 5m and 25m (Design), Elevation Banding 1m Interval (2D), Contours 1m and 5m (Background), _No Display, Elevation Banding (2D), Cut and Fill Banding 0.5m Interval (2D), Contours 2m and 10m (Design), Contours 1m and 5m (Design)..."}` | 19 ms |
| 5.8 `grupo_muestreo: "NoExiste"` | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el grupo de líneas de muestreo 'NoExiste'. Grupos del dibujo: Eje 1 - Muestreo (Eje 1). Usa listar_lineas_muestreo o créalo con crear_secciones."}` | 20 ms |
| 5.9 Factor poco habitual (`3`) | OK | `{"simulado": true, "despues": {"factor_corte": 3, "lineas_tabla": 0}, "avisos": ["'factor_esponjamiento' = 3 es poco habitual (lo normal está entre 0.5 y 2): revisa que no sea un error.", ...]}` | 11 ms |
| 6. Creación real (decisivo) | OK superficie / **Hallazgo tabla** | Superficie `Corredor 1 - Volumen` creada (`tipo: "TinVolume"`, `triangulos: 1516`, `estilo: "Contours 5m and 25m (Background)"`, `corte: 9014.1785`, `relleno: 32898.6449`, `neto: -23884.4664`, `sin_ajustar: {corte: 7211.3428, relleno: 29907.859, neto: -22696.5162}`). **Hallazgo:** `tabla_progresivas` salió con `tramos_sin_volumen: 42`, `totales: 0`, `falta: "falta la sección de 'Corredor 1 - Datum'"` en todas las filas porque `Section.SourceName` en Civil 3D es `"Corredor 1 Corredor 1 - Datum"`. | 311 ms |
| 7. Comprobación cruzada (decisivo) | OK (lectura MCP) | `listar_superficies`: `Corredor 1 - Volumen` (`tipo: "TinVolumeSurface"`, `tipo_superficie: "TinVolume"`, `esta_desactualizada: false`, `corredor: null`). Comprobación manual en Propiedades de superficie no realizada (sesión desatendida). | 9 ms |
| 8. Supuesto puntos de sección (decisivo) | Parcial (cotas OK, tabla vacía) | `pk_a_punto(400)`: `x: 1300, y: 2134.3363`. `cota_superficie`: `Terreno = 104.1867`, `Corredor 1 - Datum = 109.0011` (en PK 400 hay relleno de `4.8144` m). No se pudo contrastar con `area_relleno` de la tabla porque `falta` era `"falta la sección de 'Corredor 1 - Datum'"`. | 14 + 9 + 10 ms |
| 9. Deshacer y copia (decisivo) | OK | `backups\` se mantuvo en 6 archivos (`copia.reutilizada: true` respecto al guardado de las `09:41:28`). | — |
| 10. Error nombre repetido | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe la superficie 'Corredor 1 - Volumen'. Usa listar_superficies o pasa otro 'nombre'."}`. `leer_historial` confirma rechazo en simulación previa sin `EXECUTEFUNCTION`. | 18 ms |
| 11. Segunda superficie sin factores | OK | `Volumen sin factores` creada: `factor_corte: 1`, `factor_relleno: 1`, `corte: 7211.3428`, `relleno: 29907.859`, `neto: -22696.5162`, `tabla_progresivas: null`, `avisos: null`. | 107 ms |
| 12. `_.UNDO 1` (decisivo) | **Hallazgo** | `_.UNDO 1` terminó OK (233 ms), pero **`Volumen sin factores` siguió existiendo** en `listar_superficies` (`tipo: "TinVolumeSurface"`, `tipo_superficie: "TinVolume"`), igual que `TinSurface` en 1.7.0. | 233 ms + 11 ms |
| 13. Cierre (`leer_log`) | OK | `leer_log` (`ultimas_n: 40`) registró todas las llamadas. No se guardó el dibujo. | — |

## Comprobación aritmética del paso 6
- `sin_ajustar.corte` × `1.25` = `7211.3428 × 1.25` = `9014.1785` (`corte` = `9014.1785`) ✓
- `sin_ajustar.relleno` × `1.10` = `29907.859 × 1.10` = `32898.6449` (`relleno` = `32898.6449`) ✓
- `corte - relleno` = `9014.1785 - 32898.6449` = `-23884.4664` (`neto` = `-23884.4664`) ✓

## Respuestas a las preguntas para la memoria técnica

1. **`GetVolumeProperties` (`datos.civil3d`) y signo del neto:**
   `corte_sin_ajustar: 7211.3428`, `relleno_sin_ajustar: 29907.859`, `neto_sin_ajustar: 22696.5162`, `corte_ajustado: 9014.1785`, `relleno_ajustado: 32898.6449`, `neto_ajustado: 23884.4664`, `factor_corte: 1.25`, `factor_relleno: 1.1`. `convencion_neto_civil3d: "relleno_menos_corte"` (en Civil 3D `UnadjustedNetVolume` = relleno − corte).
2. **Volúmenes ajustados exactos:** Sí (`sin ajustar × factor`), sin aviso de discrepancia.
3. **`SectionPoint.Location` y tabla por progresivas:** `supuesto_ejes.comprobado` salió `false` (`lineas_comprobadas_con_la_superficie: 0`, `motivo: null`) y las 42 filas salieron sin áreas ni volúmenes con `falta: "falta la sección de 'Corredor 1 - Datum'"`. La causa observada en `VALIDACION_113` es que `Section.SourceName` para una superficie de corredor es `"Corredor 1 Corredor 1 - Datum"` y no `"Corredor 1 - Datum"`.
4. **Diferencia tabla vs superficie de volumen:** `-100 %` en corte y `-100 %` en relleno (`tramos_sin_volumen: 42` de `42`) por el hallazgo del punto 3.
5. **`_.UNDO 1` (paso 12):** No quitó `Volumen sin factores`; siguió apareciendo en `listar_superficies`.
6. **Paso 3 y estilos del paso 5:** En el paso 3 devolvió el error esperado por existir `Terreno` y `Terreno 2`. Estilos disponibles (paso 5.7): `Contours 5m and 25m (Background)`, `Border Only`, `Slope Banding (2D)`, `Contours 2m and 10m (Background)`, `Elevation Banding 10m Interval (2D)`, `Contours and Triangles`, `Contours 5m and 25m (Design)`, `Elevation Banding 1m Interval (2D)`, `Contours 1m and 5m (Background)`, `_No Display`, `Elevation Banding (2D)`, `Cut and Fill Banding 0.5m Interval (2D)`, `Contours 2m and 10m (Design)`, `Contours 1m and 5m (Design)`.
7. **Estilo por defecto, triángulos y tiempos:** Estilo `Contours 5m and 25m (Background)`, `triangulos: 1516`. Paso 6 = `291` ms (`ms_puente: 311`); Paso 11 = `100` ms (`ms_puente: 107`).

## Miembros verificados en Civil 3D 2027 (sección 1.14.0, fecha 2026-11-14)
- `TinVolumeSurface.Create` (tres argumentos)
- `TinVolumeSurface.CutFactor` / `FillFactor`
- `TinVolumeSurface.Rebuild`
- `TinVolumeSurface.GetVolumeProperties()` (`BaseSurface`, `ComparisonSurface`, 6 volúmenes; `UnadjustedNetVolume` = `relleno_menos_corte`)
- `TinVolumeSurface.GetTinProperties()`
- `Section.SourceName` (observado `"Corredor 1 Corredor 1 - Datum"` para `CorridorSurface` en `VALIDACION_113`, causando no coincidencia con `"Corredor 1 - Datum"`)

---

# Informe de validación 1.14.1 (rama `tarea/2.7-volumenes`)

- **Fecha:** 2026-11-14
- **Civil 3D:** Autodesk Civil 3D 2027 - Español Métrico (`es-ES`)
- **Plugin / Puente:** `ArbaMcp` `1.14.1` (53 herramientas del plugin + `buscar_api` del puente)
- **Dibujo:** `PRUEBA_112.dwg` (misma sesión tras `VALIDACION_114.md`, sin guardar)
- **Resultado global:** **OK** (todos los pasos decisivos 0, 2, 5, 6, 7, 9, 10, 11 y 12 son OK)

## Resumen de pasos

| Paso | Resultado | Respuesta literal (resumen / literal) | Tiempo (`ms_puente`) |
|---|---|---|---|
| 0. Versión (decisivo) | OK | `ping` → `version: "1.14.1"`, `hay_dibujo: true`. `GET /ping` → `{"ok":true,"servidor":"ArbaMcp","version":"1.14.1"}`. | — |
| 1. Comprobar dibujo | OK | `Eje 1` (0..831.3274), `Rasante 1` (FG), `Terreno`, `Terreno 2`, `Corredor 1 - Top`, `Corredor 1 - Datum`, `Corredor 1`, `Ensamblaje 1`. | — |
| 2. Contornos `estado_corredor` (decisivo) | OK | `Corredor 1 - Top` y `Corredor 1 - Datum`: `contornos: [{"nombre": "Extensión del corredor", "tipo": "OutsideBoundary", "es_extension_corredor": true, "usar_como_exterior": true}]`. | 10 ms |
| 3. Punto de partida | OK | `backups\`: 6 archivos. Capa configurada vista en el paso 4 (`C-ROAD-CORR`). | — |
| 4. Capa corredor (simular) | OK | `{"simulado": true, "despues": {"existe": true, "inicio": 100, "fin": 300, "frecuencia_tangentes": 20, "capa": "C-ROAD-CORR"}}` | 29 ms |
| 5. Capa corredor real (decisivo) | OK | `crear_corredor` `Corredor 3`: `despues.capa = "C-ROAD-CORR"`, `datos.capa = "C-ROAD-CORR"`, `datos.capa_configuracion = "C-ROAD-CORR"`, `esta_desactualizado: false`, `avisos: null`, `copia.estado: "terminada"`. `listar_corredores` mostró `Corredor 1` y `Corredor 3`. `_.UNDO 1` deshizo `Corredor 3` (solo quedó `Corredor 1`). | 155 ms |
| 6. Punto de inserción con punto lejano (decisivo) | OK | `_.POINT 600000,2000` → `EXTMAX = "(600000,2517.9063128238777,111.49999992211815)"`. `crear_perfil_terreno_y_vista` (`simular: true` y real): `punto_insercion: {"x": 1660, "y": 2000}`, `punto_insercion_automatico: true`, `perfil_existe: true`, `vista_existe: true`. Aviso: `"Punto de inserción automático: x = 1600 (extremo derecho del eje 'Eje 1') + 60 de margen (10 % del ancho 600, mínimo 50) = 1660; y = 2000 (extremo inferior). Pasa 'punto_insercion' para elegir otro."` | 110 ms (real) |
| 7. Secciones a la derecha de la vista (decisivo) | OK | `crear_secciones` `Muestreo 1141` (`simular: true`): `punto_insercion: {"x": 2694, "y": 2000}`. Aviso: `"Punto de inserción automático: x = 2540 (extremo derecho del eje 'Eje 1' y de su vista de perfil y sus 43 vistas de sección) + 154 de margen (10 % del ancho 1540, mínimo 50) = 2694; y = 2000 (extremo inferior)..."` | 13 ms |
| 8. Deshacer vista de prueba | Hallazgo | `_.UNDO 1` terminó OK (116 ms), pero `Terreno - 1141` siguió apareciendo en `listar_perfiles` (nota: en el paso 6 se ejecutó `_.ZOOM _W` después de crear la vista, o `Profile` EG no se deshace con un solo `_.UNDO 1`). | 116 ms + 10 ms |
| 9. `agregar_superficie_corredor` (decisivo) | OK | Real: `despues: {"superficies": ["Corredor 1 - Datum", "Corredor 1 - Prueba 1141", "Corredor 1 - Top"], "codigos_enlace": ["Top"]}`, `copia.estado: "terminada"`. `listar_corredores` siguió mostrando `esta_desactualizado: false` (hasta añadir el contorno; en paso 11 `estado_corredor` mostró `esta_desactualizado: true`). | 58 ms |
| 10. `agregar_codigo_superficie_corredor` (decisivo) | OK | Error `tipo: "otro"` OK. Simulación y real: `despues.codigos_enlace: ["Datum", "Top"]`, `copia.estado: "terminada"`. | 45 ms |
| 11. `agregar_contorno_superficie_corredor` (decisivo) | OK | Error `poligono: "NoExiste"` OK. Real: `despues: {"n_contornos": 1, "contornos": ["Talud automático"]}`. `estado_corredor`: `{"nombre": "Talud automático", "tipo": "OutsideBoundary", "es_extension_corredor": true, "usar_como_exterior": true}`. `_.UNDO 1` **sí quitó** el contorno (`contornos: []`), por lo que se repitió la llamada real. | 51 ms / 52 ms |
| 12. Reconstruir y comparar (decisivo) | OK | `reconstruir_corredor`: `esta_desactualizado: false`. `estado_corredor`: `Corredor 1 - Prueba 1141` con `codigos_enlace: ["Datum", "Top"]`, contorno `Talud automático`, `esta_desactualizada: false`; `Corredor 1 - Top` y `Corredor 1 - Datum` idénticos al paso 2. `listar_superficies` incluye `Corredor 1 - Prueba 1141` (`corredor: "Corridor 1"`). | 59 ms |
| 13. Nombre repetido sin Deshacer | OK | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: El corredor ya tiene una superficie llamada 'Corredor 1 - Prueba 1141'. Usa agregar_codigo_superficie_corredor para añadirle códigos."}`. `leer_historial` confirma rechazo en simulación sin `EXECUTEFUNCTION`. | 15 ms |
| 14. Cierre (`leer_log`) | OK | `leer_log` (`ultimas_n: 60`) registró todas las llamadas. No se guardó el dibujo. | — |
| 15. `buscar_api.py` en `chcp 1252` | OK | Código de salida `0`. Primera línea: `buscar_api "rasante con PVI" → 0 recetas, 2 entradas del glosario, 1 del catálogo (límite 3). Memoria: C:\Proyectos\memoria-firma` | — |

## Respuestas a las preguntas para la memoria técnica

1. **Versiones paso 0:** `ping` → `"version": "1.14.1"`; `GET /ping` → `{"ok":true,"servidor":"ArbaMcp","version":"1.14.1"}`; 53 herramientas del plugin.
2. **Paso 2 (`estado_corredor`):** `Corredor 1 - Top` y `Corredor 1 - Datum` tienen `contornos: [{"nombre": "Extensión del corredor", "tipo": "OutsideBoundary", "es_extension_corredor": true, "usar_como_exterior": true}]`.
3. **Capa del corredor:** `despues.capa = "C-ROAD-CORR"`, `datos.capa = "C-ROAD-CORR"`, `datos.capa_configuracion = "C-ROAD-CORR"`, sin avisos. `SettingsObjectLayerType.Corridor` resolvió correctamente.
4. **Punto de inserción:** `EXTMAX = "(600000,2517.9063128238777,111.49999992211815)"`. Paso 6 (`crear_perfil_terreno_y_vista`): `x = 1660, y = 2000` (`extremo derecho del eje 'Eje 1'`). Paso 7 (`crear_secciones`): `x = 2694, y = 2000` (`extremo derecho del eje 'Eje 1' y de su vista de perfil y sus 43 vistas de sección`).
5. **Paso 8 (`_.UNDO 1`):** No quitó `Terreno - 1141` de `listar_perfiles` (por el `_.ZOOM _W` intermedio o porque `Profile` EG no se deshace con un solo `_.UNDO 1`).
6. **Pasos 9 a 12 (`agregar_*`):**
   - `agregar_superficie_corredor`: creó `Corredor 1 - Prueba 1141` con `codigos_enlace: ["Top"]`. `listar_corredores` no la marcó desactualizada inmediatamente, pero tras `agregar_contorno_superficie_corredor` `estado_corredor` sí marcó `esta_desactualizado: true`.
   - `agregar_codigo_superficie_corredor`: añadió `"Datum"` → `["Datum", "Top"]`.
   - `agregar_contorno_superficie_corredor`: añadió `"Talud automático"` (`OutsideBoundary`, `es_extension_corredor: true`, `usar_como_exterior: true`). `_.UNDO 1` sí quitó el contorno y se volvió a añadir.
   - Tras `reconstruir_corredor`, `Corredor 1 - Prueba 1141` apareció en `listar_superficies` con `corredor: "Corredor 1"`.
7. **Tiempos (`ms`, `ms_espera`, `ms_ejecucion`):**
   - Paso 5 (`crear_corredor`): `144` / `19` / `125`
   - Paso 6 (`crear_perfil_terreno_y_vista`): `101` / `18` / `83`
   - Paso 9 (`agregar_superficie_corredor`): `51` / `15` / `35`
   - Paso 10 (`agregar_codigo_superficie_corredor`): `38` / `16` / `22`
   - Paso 11 (`agregar_contorno_superficie_corredor`): `44` / `17` / `27` (repetición: `45` / `20` / `24`)
   - Paso 12 (`reconstruir_corredor`): `51` / `18` / `33`
8. **Paso 15:** Código de salida `0`, primera línea: `buscar_api "rasante con PVI" → 0 recetas, 2 entradas del glosario, 1 del catálogo (límite 3). Memoria: C:\Proyectos\memoria-firma`.

## Miembros verificados en Civil 3D 2027 (sección 1.14.1, fecha 2026-11-14)
- `SettingsObjectLayerType.Corridor` (resuelto a `C-ROAD-CORR`)
- `Entity.LayerId` sobre un corredor recién creado
- `Entity.GeometricExtents` sobre `ProfileView` y `SectionView`
- `CorridorSurfaceCollection.Add`
- `CorridorSurface.AddLinkCode`
- `CorridorSurfaceBoundaryCollection.AddCorridorExtentsBoundary` / `BoundaryNames` (vía `agregar_contorno_superficie_corredor`)
- `CorridorSurfaceBoundary.IsCorridorExtents` en `es_extension_corredor`

---

# Informe de validación: Flujo vial completo (`VALIDACION_FLUJO_COMPLETO.md`)

- **Fecha:** 2026-11-14
- **Civil 3D:** Autodesk Civil 3D 2027 - Español Métrico (`es-ES`)
- **Plugin / Puente:** `ArbaMcp` `1.14.1` (53 herramientas del plugin + `buscar_api` del puente)
- **Dibujo:** `PRUEBA_FLUJO.dwg` (dibujo nuevo con plantilla métrica por defecto)
- **Nota de sesión:** Flujo ejecutado de corrido de forma autónoma simulando las confirmaciones de Jose según el guion de `VALIDACION_FLUJO_COMPLETO.md` (sin esperas humanas intermedias ni comparación manual en el Panel de volúmenes del paso 14). El ensamblaje creado a mano quedó registrado como `Ensamblaje 1 (2)`.

## Tabla de pasos del flujo

| Paso | Resultado | Respuesta literal / resumen | Hora (`id_log`) |
|---|---|---|---|
| 0. Instalación y receta | OK | `ping` → `version: "1.14.1"`. `PuenteMcp/INSTRUCCIONES_AGENTE.md` contiene `## 2b. Flujo vial completo (de la superficie al volumen)`: *"Es la **única receta** de este archivo, porque es el flujo principal (las demás se buscan con `buscar_api`)."* | `09:51:51` |
| 1. Preparar dibujo | OK | `Ensamblaje 1 (2)` (4 subensamblajes: `LaneSuperelevationAOR` + `DaylightBench` D/I), capa `EJE_PRUEBA`, polilínea `1000,2000 → 1300,2000 → 1300,2300 → 1600,2300`, `guardar_dibujo` OK (`1099924` bytes). | `09:51:53`–`09:52:03` |
| 2. Punto de partida | OK | `listar_superficies: []`, `listar_alineamientos: []`, `listar_corredores: []`, `backups\` (`PRUEBA_FLUJO*`): `0`. Último log previo: `2026-11-14 09:52:07`. | `09:52:04`–`09:52:08` |
| 3. Plan y simulación (1 y 2) | OK | Lecturas iniciales + `crear_superficie_tin(simular=true)` (`puntos: 9`, PENZD automático, aviso de intervalos 1 y 5 en estilo) + `crear_alineamiento_desde_polilinea(simular=true)` (`longitud: 831.3274`, `numero_pi: 2`, `numero_curvas: 2`). | `09:52:21`–`09:52:30` |
| 4. Paso 1: Superficie TIN | PARCIAL (curvas) / OK flujo | `crear_superficie_tin`: `puntos: 9`, `triangulos: 8`, `estilo: "Contours 5m and 25m (Background)"`, `intervalos_curvas: {pedido_menor: 1, pedido_mayor: 5, aplicados: false}`, `copia.estado: "terminada"`. `aprobar_resultado` OK. | `09:52:37` (`20261114-095237-e2cbf5`) |
| 5. Paso 2: Eje | OK | `crear_alineamiento_desde_polilinea`: `longitud: 831.3274`, `numero_pi: 2`, `numero_curvas: 2`, radios `80`, `capa: "C-ROAD"`. `aprobar_resultado` OK. | `09:52:48` (`20261114-095248-7930b1`) |
| 6. Paso 3: Perfil y vista | OK | `crear_perfil_terreno_y_vista`: `Terreno - Terreno` (`EG`, 0..831.3274, cotas `100..109`), vista `Eje 1 - Vista` en `(1660, 2000)`, `bandas: 1`. `aprobar_resultado` OK. | `09:53:01` (`20261114-095301-d10d00`) |
| 7. Paso 4: Rasante | OK | `crear_rasante`: `tipo: "FG"`, `numero_pvis: 3`, `numero_curvas: 1`, `progresiva_final: 831.3274`, `cota_minima: 100`, `cota_maxima: 109.5248`, pendientes `2.5` y `-1.1592`, curva `120` m `cresta`, `k: 32.7939`. `aprobar_resultado` OK. | `09:53:12` (`20261114-095312-d87141`) |
| 8. Paso 5: Corredor (decisivo) | OK | `crear_corredor`: `ensamblaje: "Ensamblaje 1 (2)"`, `inicio: 0`, `fin: 831.3274`, frecuencias `10`, `objetivos_superficie: 2`, `superficie_objetivo: "Terreno"`, `esta_desactualizado: false`, `capa: "C-ROAD-CORR"`, `restaurar: {herramienta: "deshacer_objetivos", args: {id: 2}}`. `_.ZOOM` OK. `aprobar_resultado` OK. | `09:53:22` (`20261114-095322-669c48`) |
| 9. Paso 6: Superficies Top y Datum (decisivo) | OK | `crear_superficies_corredor`: `Corredor 1 - Top` (`TopLinks`) y `Corredor 1 - Datum` (`BottomLinks`), contorno `Extensión del corredor`, `esta_desactualizado: false`. `aprobar_resultado` OK. | `09:53:36` (`20261114-095336-8a29ce`) |
| 10. Paso 7: Secciones (decisivo) | OK | `crear_secciones`: `43` líneas (`0..831.3274`), `43` vistas en `9` columnas × `5` filas desde `(2694, 2000)`, `fuentes_muestreadas: ["Terreno", "Corredor 1", "Corredor 1 - Top", "Corredor 1 - Datum"]`, `lineas_sin_secciones: 0`, `solapes: 0`. `aprobar_resultado` OK. | `09:53:48` (`20261114-095348-95a827`) |
| 11. Paso 8: Volúmenes (decisivo) | OK superficie / **Hallazgo tabla** | `calcular_volumenes` (`terreno: "Terreno"`, `corredor: "Corredor 1"`, factores `1` y `1`): `superficie_volumen: "Corredor 1 - Volumen"`, `corte: 7211.3428`, `relleno: 29907.859`, `neto: -22696.5162` (`Faltan 22696.5162 m³`), `convencion_neto_civil3d: "relleno_menos_corte"`, `triangulos: 1516`. **Hallazgo:** `tabla_progresivas: null` con aviso `Ningún grupo de líneas de muestreo (Eje 1 - Muestreo) tiene secciones de 'Terreno' y de 'Corredor 1 - Datum'` (mismo hallazgo de `SourceName` que en `VALIDACION_114`). `aprobar_resultado` OK. | `09:53:59` (`20261114-095359-189cfa`) |
| 12. Deshacer, copias y log (decisivo) | OK | `1` archivo nuevo de backup (`PRUEBA_FLUJO_20261114_095237_crear_superficie_tin.dwg`, reutilizado en los pasos 2–8 porque no hubo guardado intermedio). `leer_log`: **8** escrituras `ok: true` (precedidas por su `simular: true`) y **8** `aprobacion` `aprobado: true`. | — |
| 14. Volumen manual | No ejecutado | Sesión autónoma sin intervención manual en el Panel de volúmenes. | — |
| 15. Tiempo total | OK | `T0 = 09:52:21`, `T1 = 09:54:18` → **117 s** (`1 min 57 s`, **1.50 %** de los 130 min manuales). Suma de `ms` de las 8 escrituras en Civil 3D: **709 ms**. | — |
| 16. Cierre | OK | `guardar_dibujo` OK (`PRUEBA_FLUJO.dwg`, `1624855` bytes). | `09:54:39` |

## Comprobación de las tareas 1 a 8 sobre el dibujo final (Paso 13)

| Tarea | Resultado | Evidencia leída del dibujo |
|---|---|---|
| 1 | PARCIAL (curvas) | `extension_superficie("Terreno")`: `n_puntos: 9`, x `900..1700`, y `1900..2400`. `cota_superficie(1300,2150) = 104.5`. Estilo `Contours 5m and 25m (Background)` (no 1 y 5 m). |
| 2 | OK | `listar_alineamientos`: `Eje 1` de `0` a `831.3274`. |
| 3 y 4 | OK | `listar_perfiles`: `Terreno - Terreno` (`EG`, 0..831.3274, 107 PVIs) y `Rasante 1` (`FG`, 0..831.3274, 3 PVIs). |
| 5 y 6 | OK | `estado_corredor`: `esta_desactualizado: false`, `Corredor 1 - Top` y `Corredor 1 - Datum`. `listar_objetivos`: ambos `DaylightBench` (`TargetDTM`) apuntan a `Terreno`. `cota_superficie(1150,2000)`: Top=`103.75`, Datum=`103.3`. |
| 7 | OK | `listar_lineas_muestreo("Eje 1", fuentes=true)`: `Eje 1 - Muestreo`, `n_lineas: 43`, las 4 fuentes (`Terreno`, `Corredor 1`, `Corredor 1 - Top`, `Corredor 1 - Datum`) con `muestreada: true`. |
| 8 | OK | `listar_superficies`: `Corredor 1 - Volumen` (`tipo_superficie: "TinVolume"`, `esta_desactualizada: false`). |

## Respuestas a las preguntas para la memoria técnica

1. **Seguimiento de la receta:** Sí, se ejecutó el orden exacto de los 8 pasos de la receta `2b`, simulando antes de cada escritura (pasos 1 y 2 antes de escribir el 1, y cada paso N+1 en la parada del paso N).
2. **`aprobar_resultado` y `pedido`:** Se llamó 8 veces, siempre después de la escritura correspondiente. En cada paso se envió la parte correspondiente del pedido del usuario.
3. **Avisos que obligaran a parar:** Ninguno en los pasos 1–7; en el paso 8 (`calcular_volumenes`), salió el aviso de que `Eje 1 - Muestreo` no tiene secciones de `Corredor 1 - Datum` (debido a que `Section.SourceName` es `"Corredor 1 Corredor 1 - Datum"`), por lo que `tabla_progresivas` quedó en `null`.
4. **`calcular_volumenes`:** Argumentos `terreno: "Terreno"`, `corredor: "Corredor 1"`, factores por defecto `1` y `1`. `tabla_progresivas` salió `null` por el hallazgo de `SourceName`. `ms: 104` (`83` en log de ejecución).
5. **Estilo de superficie:** `Contours 5m and 25m (Background)` (intervalos 5 y 25 m); la herramienta informó en `avisos` que los intervalos viven en el estilo y propuso usar un estilo del dibujo con 1 y 5 m (como `Contours 1m and 5m (Design)` o `Contours 1m and 5m (Background)`).
6. **Volúmenes:** Agente: `corte = 7211.3428 m³`, `relleno = 29907.859 m³`, `neto = -22696.5162 m³` (faltan `22696.5162 m³`). Panel manual: no ejecutado en esta sesión autónoma.
7. **Tiempos:** Reloj total (`T1 − T0`) = `117 s` (`1.50 %` de 130 min), sin esperas humanas; suma de `ms` de Civil 3D en las 8 escrituras = `709 ms`.
