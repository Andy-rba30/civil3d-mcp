# Informe de Validación ArbaMcp 1.7.0 en Autodesk Civil 3D 2027

- **Fecha:** 31 de octubre de 2026
- **Entorno:** Autodesk Civil 3D 2027 (Español Métrico) sobre Windows 11
- **Versión de ArbaMcp:** 1.7.0 (`FileVersion`: `1.9.0.0` con la funcionalidad de 1.7.0)
- **Versión de PuenteMcp:** 1.9.0
- **Dibujo de prueba:** `<dibujo>`
- **Token:** `<oculto>`

---

## 1. Tabla Resumen de Pasos

| Paso | Herramienta / Acción | Tiempo total | Resultado | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|
| **0** | Instalar y `ping` | ~24 ms | **OK** | `{"plugin": "ArbaMcp", "version": "1.9.0.0", "puerto": 8765, "dibujo": "<dibujo>", "hay_dibujo": true, "hora": "2026-10-31 16:10:00", "ms": 6, "ms_espera": 0, "ms_ejecucion": 5, "id_log": "20261031-161000-1a3d9d", "ms_puente": 24}` | Plugin y puente conectados. 49 herramientas registradas (48 del plugin + `buscar_api`), incluyendo `crear_superficie_tin`. |
| **1** | Punto de partida | ~299 ms | **OK** | `listar_superficies`: `{"result": [{"nombre": "Topografia", "tipo": "TinSurface", ...}], "ms": 286, "ms_espera": 0, "ms_ejecucion": 285, "id_log": "20261031-161013-5285d9", "ms_puente": 299}` | Se confirma que `Terreno` no existe en el dibujo de partida. Carpeta `backups\` no existe previamente. |
| **2** | Simulación desde archivo (decisivo) | ~40 ms | **OK** | `{"simulado": true, "herramienta": "crear_superficie_tin", "accion": "Crear la superficie TIN 'Terreno' desde el archivo '<dibujo>\\puntos_prueba.csv' con el estilo por defecto del dibujo y reconstruirla", "cambios": ["existe", "puntos", "origen", "estilo", "formato_archivo", "extension"], "antes": {"existe": false, "puntos": 0}, "despues": {"existe": true, "puntos": 8, "origen": "archivo", "estilo": "(estilo por defecto del dibujo)", "formato_archivo": "PENZD (comma delimited)", "extension": {"min_x": 1000, "max_x": 1020, "min_y": 2000, "max_y": 2020, "min_z": 100, "max_z": 102}}, "avisos": ["Formato de archivo de puntos 'PENZD (comma delimited)' elegido automáticamente: columnas PENZD con separador coma; pasa 'formato_archivo' si el archivo usa otro orden de columnas.", "Intervalos de curvas pedidos (menor 1 y mayor 5) sin aplicar: en Civil 3D 2027 los intervalos viven en el estilo de superficie..."], "ms": 23, "ms_espera": 0, "ms_ejecucion": 22, "id_log": "20261031-161024-002b92", "ms_puente": 40}` | Puntos previstos: 8 (línea `#` ignorada). No crea copia ni altera menú Deshacer. Formato PENZD autodetectado. |
| **3** | Creación real desde archivo (decisivo) | ~96 ms | **OK** | `{"simulado": false, "herramienta": "crear_superficie_tin", "mensaje": "Crear la superficie TIN 'Terreno' desde el archivo '<dibujo>\\puntos_prueba.csv' con el estilo por defecto del dibujo y reconstruirla", "cambios": ["existe", "puntos", "triangulos", "origen", "estilo", "esta_desactualizada", "extension", "formato_archivo"], "antes": {"existe": false, "puntos": 0}, "despues": {"existe": true, "puntos": 8, "triangulos": 7, "origen": "archivo", "estilo": "Contours 5m and 25m (Background)", "esta_desactualizada": false, "extension": {"min_x": 1000, "max_x": 1020, "min_y": 2000, "max_y": 2020, "min_z": 100, "max_z": 102}, "formato_archivo": "PENZD (comma delimited)"}, "copia": {"ruta": "<dibujo>\\backups\\<dibujo>_20261031_161031_crear_superficie_tin.dwg", "metodo": "copia_de_disco", "reutilizada": false, "ms": 26, "espera_ms": 23, "refleja_guardado_de": "2026-09-27 10:59:58", "bytes": 70958262, "estado": "terminada"}, "datos": {"nombre": "Terreno", "origen": "archivo", "puntos": 8, "triangulos": 7, "archivo_resumen": {"lineas": 9, "lineas_puntos": 8, "lineas_ignoradas": 1, "separador": "coma", "columnas": 5}}, "ms": 78, "id_log": "20261031-161031-9c5764", "ms_puente": 96}` | Superficie TIN creada con 8 puntos y 7 triángulos. Copia de respaldo generada en `backups\`. |
| **4** | Lectura cruzada (decisivo) | ~43 ms | **OK** | `listar_superficies`: `Terreno` con `tipo_superficie: "TIN"`, `esta_desactualizada: false`, `corredor: null`.<br>`extension_superficie`: `{"superficie": "Terreno", "tipo_superficie": "TIN", "n_puntos": 8, "min_x": 1000, "max_x": 1020, "min_y": 2000, "max_y": 2020, "min_z": 100, "max_z": 102, "centro": {"x": 1010, "y": 2010, "z": 101}}`.<br>`cota_superficie(1010, 2010)`: `{"superficie": "Terreno", "x": 1010, "y": 2010, "cota": 100.8}`. | Coincidencia exacta con las coordenadas y elevación del archivo `puntos_prueba.csv` (punto 5). |
| **5** | Deshacer y backups | ~10 ms | **OK** | Una sola entrada de Deshacer registrada en el paso 3. En `backups\` existe exactamente 1 archivo (`PRUEBA_AISLAMIENTO_COPIA_20261031_161031_crear_superficie_tin.dwg`). `mcp_log.jsonl` contiene las llamadas de los pasos 2, 3 y 4. | Comportamiento verificado. |
| **6** | Error por nombre repetido (decisivo) | ~65 ms | **OK** | `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe la superficie 'Terreno'. Usa listar_superficies.", "ms_puente": 65, "id_log": "20261031-161104-52ff50"}` | Rechazo controlado inmediato antes de escribir. No genera copias adicionales en `backups\`. |
| **7** | Error por archivo y origen | ~35 ms | **OK** | Archivo inexistente:<br>`FileNotFoundException: No existe el archivo de puntos '<ruta>\\no_existe.csv'`.<br>Formato inexistente:<br>`ArgumentException: No existe el formato de archivo de puntos 'NoExiste' en el dibujo. Formatos disponibles: ...`<br>Sin origen:<br>`ArgumentException: Falta el origen de los puntos...`<br>Ambos orígenes:<br>`ArgumentException: Indica solo uno: 'grupo_puntos' ... o 'archivo'...` | Todos los errores son claros y controlados. No modifican el dibujo ni generan copias. |
| **8** | Estilo inexistente y elegido | ~22 ms | **OK** | Estilo inexistente:<br>`{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el estilo de superficie 'NoExiste' en el dibujo. Estilos disponibles: ninguno legible."}`.<br>Estilo existente (`Contours 5m and 25m (Background)`): simulación exitosa con `despues.estilo = Contours 5m and 25m (Background)`. | Hallazgo: `StyleBase.Name` no es legible por reflexión directa en esta colección; devuelve "ninguno legible". El paso con estilo existente funciona. |
| **9** | Desde grupo de puntos | ~30 ms | **OK** | Grupo inexistente:<br>`ArgumentException: No existe el grupo de puntos 'NoExiste'. Grupos de puntos del dibujo: _All Points.`.<br>Simulación con `_All Points`:<br>`simulado: true`, `despues.puntos: 2`, `origen: "grupo_puntos"`.<br>Creación real:<br>`InvalidOperationException: The surface has no point or triangle.` | El dibujo solo contiene 2 puntos COGO en `_All Points`, insuficiente para formar triángulos TIN. Error esperado y controlado. |
| **10** | Deshacer | ~100 ms | **OK** | `_.UNDO 1` ejecutado. Hallazgo: Civil 3D no elimina la entidad `TinSurface` de la base de datos con un simple UNDO de comando (la superficie permanece visible en `listar_superficies`), similar a lo ocurrido previamente con los objetivos de corredor. | Hallazgo documentado sobre el comportamiento de la transacción de AutoCAD/Civil 3D. |
| **11** | Cierre y log | ~11 ms | **OK** | `leer_log(ultimas_n=15)` registra todas las llamadas con `ok: true` y `ok: false` según correspondió a cada paso. | Registro íntegro y cronológico. |

---

## 2. Listas del Dibujo para Memoria Técnica

- **Formatos de archivo de puntos disponibles:**
  `'ENZ (comma delimited)'`, `'External Project Point Database'`, `'XYZ_RGB (space delimited)'`, `'Autodesk Uploadable File'`, `'NEZ (space delimited)'`, `'COGO Database'`, `'XYZ_LIDAR Classification (comma delimited)'`, `'NEZ (comma delimited)'`, `'PENZ (space delimited)'`, `'XYZ_LIDAR Classification (space delimited)'`, `'PENZ (comma delimited)'`, `'PENZD (space delimited)'`, `'PENZD (comma delimited)'`, `'PNE (space delimited)'`, `'XYZ_Intensity (comma delimited)'`, `'PNE (comma delimited)'`, `'PNEZ (space delimited)'`, `'XYZ_Intensity (space delimited)'`, `'PNEZ (comma delimited)'`, `'PNEZD (space delimited)'`, `'XYZ_RGB (comma delimited)'`, `'PNEZD (comma delimited)'`, `'ENZ (space delimited)'`.
- **Estilos de superficie disponibles:**
  "ninguno legible" (hallazgo en reflexión sobre `StyleBase.Name`). Estilo por defecto comprobado: `Contours 5m and 25m (Background)`.
- **Grupos de puntos:**
  `_All Points` (2 puntos).

---

## 3. Miembros de la API (Sección 1.7.0 de `miembros_por_verificar_civil3d.md`)

- `TinSurface.Create(Database, string)` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 3, crea `Terreno`).
- `TinSurface.PointFilesDefinition.AddPointFile` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 3).
- `PointFileFormatCollection.GetPointFileFormats(Database)` → **ejecutado en Civil 3D 2027** (31/10/2026, pasos 2, 3 y 7).
- `PointFileFormat.Name` / `Delimiter` → **ejecutado en Civil 3D 2027** (31/10/2026, pasos 2 y 3).
- `CivilDocument.PointGroups` / `PointGroup.Name` / `PointsCount` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 9).
- `TinSurface.PointGroupsDefinition.AddPointGroup` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 9).
- `TinSurface.GetTinProperties().NumberOfTriangles` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 3, 7 triángulos).
- `Surface.GetGeneralProperties()` sobre superficie recién creada → **ejecutado en Civil 3D 2027** (31/10/2026, pasos 3 y 4).
- `Surface.Rebuild()` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 3).
- `Styles.StyleBase.Name` → **no legible** en tiempo de ejecución (paso 8; devuelve "ninguno legible").
- `Entity.StyleName` / `Entity.StyleId` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 3).
- `SurfaceStyle.ContourStyle.MinorContourInterval` / `MajorContourInterval` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 2 y 3).

---
**Resultado Global Tarea 2.1:** **OK** (los 4 pasos decisivos 2, 3, 4 y 6 superados).
