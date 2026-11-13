# Informe de validación 1.10.1 — `civil3d-mcp` (instalación 1.11.0)

- **Fecha:** 2026-11-13
- **Rama / Commit:** `tarea/2.5-corredor` (`4c8b6f0`) — incluye `1.10.1` (PR #22) + `1.11.0` (PR #23)
- **Civil 3D:** Autodesk Civil 3D 2027 (español)
- **Dibujo usado:** `VALIDACION_1101.dwg` (copia nueva y vacía de plantilla de Civil 3D)
- **Resultado global:** **OK** (todos los pasos decisivos `0, 3, 4, 7, 8, 10, 11` en **OK**)

> **Nota sobre la lista de herramientas en el cliente MCP:** El servidor MCP en Antigravity (`<ruta>/.gemini/antigravity/mcp/civil3d`) mantenía en caché 49 esquemas (sin `crear_rasante.json` ni `crear_corredor.json`), aunque en tiempo de ejecución `call_mcp_tool` invocó `crear_rasante` correctamente y el plugin respondió con versión `1.11.0`.

---

## Tabla de resultados por paso

| Paso | Resultado | Tiempo (`ms` / `ms_puente`) | Respuesta literal (resumen / fragmento) |
|---|---|---|---|
| **0** (decisivo) | **OK** | `6 ms` / `24 ms` | `ping`: `{"plugin": "ArbaMcp", "version": "1.11.0", "puerto": 8765, "dibujo": "<ruta>\\VALIDACION_1101.dwg", "hay_dibujo": true, ...}` · `GET /ping`: `{"ok":true,"servidor":"ArbaMcp","version":"1.11.0"}` |
| **1** | **OK** | `5 ms`, `6 ms`, `2 ms` | `listar_alineamientos`: `{"result": []}`; `listar_superficies`: `{"result": []}`; `CMDACTIVE`: `"0"`; `backups\` para `VALIDACION_1101`: `0` archivos. *(Menú Deshacer en UI no inspeccionado visualmente; `VALIDACION_1101.dwg` recién abierto y vacío).* |
| **2** | **OK** | `144 ms` / `154 ms` | `ejecutar_comando` (`_.-LAYER _M EJE_PRUEBA\n\n`): `{"result": "terminado", "ms": 144, "id_log": "20261113-165908-a8abbc", "ms_puente": 154}`; `CLAYER`: `"EJE_PRUEBA"`; `CMDACTIVE`: `"0"`. |
| **3** (decisivo) | **OK** | `105 ms` / `112 ms` | `ejecutar_comando` (`_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  `): `{"result": "terminado", "ms": 105, "id_log": "20261113-165924-30147c", "ms_puente": 112}`; `CMDACTIVE`: `"0"`; historial muestra una sola `PLINE`. |
| **4** (decisivo) | **OK** | `1222 ms` / `1234 ms` | `crear_alineamiento_desde_polilinea` (`simular: true`): `simulado: true`, `longitud: 831.3274`, `progresiva_inicial: 0`, `progresiva_final: 831.3274`, `numero_pi: 2`, `numero_curvas: 2`, `estilo: "Proposed"`, `conjunto_etiquetas: "All Labels"`, PI 1 `(300, 1300, 2000, 90°, izquierda, R=80)`, PI 2 `(565.6637, 1300, 2300, 90°, derecha, R=80)`. |
| **5** | **OK** | `115 ms` · `ms_puente: 9 ms` | `REGEN`: `{"result": "terminado", "ms": 115}`; `"   "`: `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Falta el parámetro obligatorio 'comando'."}`. |
| **6** | **OK** | `114 ms` (`Eje 1`) · `87 ms` (`Terreno`) | `Eje 1`: `simulado: false`, `longitud: 831.3274`, `numero_curvas: 2`, `copia.estado: "terminada"`; `Terreno`: `simulado: false`, `puntos: 8`, `triangulos: 7`, `copia.reutilizada: true`; `listar_alineamientos` (`0` a `831.3274`) y `listar_superficies` (`TIN`). |
| **7** (decisivo) | **OK** | `15–17 ms` (`ms_puente` c/u) | Las 7 llamadas devolvieron `ok: false` con nombres reales en `Disponibles:` / `Estilos disponibles:` (ninguna dijo `ninguno legible`). |
| **8** (decisivo) | **OK** | Sim `3 ms` · Real `46 ms` | `reconstruir_superficie`: `simulado: true` (`3 ms`) y `simulado: false` (`ms: 46, ms_espera: 16, ms_ejecucion: 29`, `cambios: []`, `esta_desactualizada: false`, `datos.ms: 19`, `copia.reutilizada: true`). Historial: una pareja `EXECUTEFUNCTION` y `MCP ✓`; `mcp_log.jsonl`: una línea por llamada. |
| **9** | **OK** | Sim `12 ms` · Real `46 ms` | `crear_rasante`: `simulado: true` (`12 ms`) y `simulado: false` (`ms: 46, ms_espera: 18, ms_ejecucion: 28`, `tipo: "FG"`, `numero_pvis: 3`, `numero_curvas: 1`, `progresiva_final: 831.3274`, `cota_maxima: 109.5248`). `listar_perfiles`: 1 `Rasante 1`; `backups\`: 1 archivo (`VALIDACION_1101_20261113_165948_crear_alineamiento_desde_polilinea.dwg`). |
| **10** (decisivo) | **OK** | `ms_puente: 14 ms` (`ms: 5`) | `crear_rasante` repetida sin `simular`: `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe el perfil 'Rasante 1' en el alineamiento 'Eje 1'. Usa listar_perfiles o pasa otro 'nombre'."}`. Sin `EXECUTEFUNCTION`, sin archivo nuevo en `backups\` (sigue en `1`), 1 sola línea en `mcp_log.jsonl`. |
| **11** (decisivo) | **OK** | `111 ms` / `117 ms` | 1 sola llamada `_.UNDO 1`: `{"result": "terminado"}`. `listar_perfiles` → `{"result": []}`; `listar_alineamientos` → `Eje 1` sigue de `0` a `831.3274`. |
| **12** | **OK** | `1 ms` / `8 ms` | `leer_log` (`ultimas_n: 40`): una línea `llamada` por cada llamada de los pasos 0–11, `ok: false` en pasos 5 (segundo), 7 (7 casos) y 10, `ok: true` en el resto. Dibujo no guardado. |

---

## Respuestas a las preguntas del informe

1. **Las dos versiones del paso 0 (`ping` y `GET /ping`), literales:**
   - `ping` (MCP): `"version": "1.11.0"`
   - `GET /ping` (`curl.exe -s http://127.0.0.1:8765/ping`): `{"ok":true,"servidor":"ArbaMcp","version":"1.11.0"}`

2. **Paso 3 (`_.PLINE ...  ` con dos espacios finales):**
   - Resultado: **`"result": "terminado"`** (`105 ms`).
   - `CMDACTIVE` justo después: **`"0"`**.
   - Líneas literales del historial con `PLINE`:
     - `16:59:24  MCP → ejecutar_comando [20261113-165924-30147c]`
     - `16:59:24  ejecutar_comando: '_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ' con tiempo máximo 20 s`
     - `16:59:24  Comando inicia: PLINE`
     - `16:59:24  Comando termina: PLINE`
     - `16:59:24  Registro 20261113-165924-30147c: ejecutar_comando OK (103 ms) pedido: "dibuja la polilínea del eje de prueba"`
     - `16:59:24  MCP ✓ ejecutar_comando OK (104 ms)`

3. **Paso 2 (`_.-LAYER _M EJE_PRUEBA\n\n`):**
   - Cerró directamente con **`"result": "terminado"`** (`144 ms`), sin `timeout con ESC`. `CLAYER = "EJE_PRUEBA"`, `CMDACTIVE = "0"`.

4. **Las siete listas de nombres del paso 7 (literales):**
   - **Estilos de superficie:** `Contours 5m and 25m (Background), Border Only, Slope Banding (2D), Contours 2m and 10m (Background), Elevation Banding 10m Interval (2D), Contours and Triangles, Contours 5m and 25m (Design), Elevation Banding 1m Interval (2D), Contours 1m and 5m (Background), _No Display, Elevation Banding (2D), Cut and Fill Banding 0.5m Interval (2D), Contours 2m and 10m (Design), Contours 1m and 5m (Design)`
   - **Estilos de alineamiento:** `Intersection Basic, Basic, Layout, Proposed, Existing, Offsets`
   - **Conjuntos de etiquetas de alineamiento:** `Major and Minor only, All Labels, Major Minor H+V Geometry Points, Major Minor H+V Geometry and Superelevation Points, Major Minor and Geometry Points, _No Labels`
   - **Estilos de perfil:** `Perfil del terreno existente, Intersection Basic, Basic, Design Profile, Layout, Right Sample Profile, Left Sample Profile`
   - **Conjuntos de etiquetas de perfil:** `Complete Label Set, _No Labels`
   - **Estilos de vista de perfil:** `First View, Intermediate View, Full Grid, Land Desktop Profile View, Last View, Profile View, Top Stacked View, Major Grids, Middle Stacked View, Bottom Stacked View`
   - **Conjuntos de bandas:** `Plan Profile Sheets - Elevations and Stations, Pipe Data, _No Bands, EG-FG Elevations and Stations, Land Desktop Full Band Set, Cut and Fill, Stations Only, Section Sample Lines, Land Desktop Standard Band Set, Profile Data with Geometry and Superelevation`

5. **Paso 10 (líneas literales del historial desde `MCP → crear_rasante [20261113-170104-819942]` y efecto en Deshacer / `backups\`):**
   - `17:01:04  MCP → crear_rasante [20261113-170104-819942]`
   - `17:01:04  MCP ✗ crear_rasante no se ejecutó: falló la comprobación previa (simulación); sin contexto de comando, sin copia y sin entrada en Deshacer: Ya existe el perfil 'Rasante 1' en el alineamiento 'Eje 1'. Usa listar_perfiles o pasa otro 'nombre'.`
   - `17:01:04  Registro 20261113-170104-819942: crear_rasante ERROR: Ya existe el perfil 'Rasante 1' en el alineamiento 'Eje 1'. Usa listar_perfiles o pasa otro 'nombre'. (5 ms) pedido: "repite la rasante Rasante 1 a propósito"`
   - `17:01:04  MCP ← crear_rasante ERROR: Ya existe el perfil 'Rasante 1' en el alineamiento 'Eje 1'. Usa listar_perfiles o pasa otro 'nombre'. ...` (sin ninguna línea `Comando inicia: EXECUTEFUNCTION`).
   - `backups\`: **no** cambió (siguió con 1 archivo de `VALIDACION_1101`). Menú Deshacer: **no** hubo entrada vacía (confirmado en el paso 11).

6. **Paso 11 (`_.UNDO 1`):**
   - Hizo falta **1** solo `_.UNDO 1` para quitar `Rasante 1` (dejando `Eje 1` intacto).

7. **Tiempos de la comprobación previa (`ms`, `ms_espera`, `ms_ejecucion` y `ms` de simulación):**
   - **Paso 4 / 6 (`crear_alineamiento_desde_polilinea`):** simulación = `1222 ms` (`ms_espera: 1186, ms_ejecucion: 35`); real = `ms: 114, ms_espera: 40, ms_ejecucion: 73`.
   - **Paso 6 (`crear_superficie_tin`):** real = `ms: 87, ms_espera: 28, ms_ejecucion: 58` (sin simulación previa separada en el guion).
   - **Paso 8 (`reconstruir_superficie`):** simulación = `3 ms` (`ms_espera: 0, ms_ejecucion: 3`); real = `ms: 46, ms_espera: 16, ms_ejecucion: 29`.
   - **Paso 9 (`crear_rasante`):** simulación = `12 ms` (`ms_espera: 0, ms_ejecucion: 11`); real = `ms: 46, ms_espera: 18, ms_ejecucion: 28`.

---

## Miembros de la sección `1.10.1` (`miembros_por_verificar_civil3d.md`)

Pasan a **`ejecutado en Civil 3D 2027`** con fecha **2026-11-13**:
- `Autodesk.Civil.DatabaseServices.DBObject.Name` (lectura) sobre `Styles.SurfaceStyle` — paso 7 (`crear_superficie_tin`: 14 estilos listados).
- `Autodesk.Civil.DatabaseServices.DBObject.Name` (lectura) sobre `Styles.AlignmentStyle` y `Styles.AlignmentLabelSetStyle` — paso 7 (`crear_alineamiento_desde_polilinea`: 6 estilos y 6 conjuntos listados).
- `Autodesk.Civil.DatabaseServices.DBObject.Name` (lectura) sobre `Styles.ProfileStyle`, `Styles.ProfileLabelSetStyle`, `Styles.ProfileViewStyle` y `Styles.ProfileViewBandSetStyle` — paso 7 (`crear_perfil_terreno_y_vista`: 7 estilos de perfil, 2 conjuntos de etiquetas, 10 estilos de vista y 10 conjuntos de bandas listados).
- `Document.SendStringToExecute(string, bool, bool, bool)` con texto terminado en espacios y sin `"\n"` añadido — paso 3 (`_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ` terminó con `"terminado"` y `CMDACTIVE = "0"`).

Sigue **`por verificar`**:
- `DBObject.OwnerId`, `DBDictionary.Contains(ObjectId)`, `DBDictionary.NameAt(ObjectId)` — no hizo falta ejecutar la segunda vía porque `DBObject.Name` devolvió todos los nombres.
