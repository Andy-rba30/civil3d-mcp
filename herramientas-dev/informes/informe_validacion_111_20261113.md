# Informe de validación 1.11.0 — `civil3d-mcp` (`crear_corredor`)

- **Fecha:** 2026-11-13
- **Rama / Commit:** `tarea/2.5-corredor` (`4c8b6f0`) — incluye `1.10.1` + `1.11.0`
- **Civil 3D:** Autodesk Civil 3D 2027 (español)
- **Dibujo usado:** `PRUEBA_111.dwg` (copia de `PRUEBA_110_VACIO.dwg`)
- **Validación previa `1.10.1`:** [informe_validacion_1101_20261113.md](informe_validacion_1101_20261113.md) — **OK** (todos los pasos decisivos en OK)
- **Resultado global:** **OK** (todos los pasos decisivos `3, 5, 6, 7, 9` en **OK**)

> **Notas:**
> 1. El caché estático del cliente MCP en Antigravity (`<ruta>/.gemini/antigravity/mcp/civil3d`) mostraba 49 esquemas (sin `crear_corredor.json`), aunque en tiempo de ejecución `call_mcp_tool` invocó `crear_corredor` correctamente.
> 2. En el paso 1, el ensamblaje manual `Ensamblaje 1` se creó con `LaneSuperelevationAOR` (derecha e izquierda) y `DaylightBench` (derecha e izquierda) en lugar de `BasicLane` + `BasicSideSlopeCutDitch`, manteniendo igualmente 2 carriles y 2 taludes con objetivo de superficie (`TargetDTM`).

---

## Tabla de resultados por paso

| Paso | Resultado | Tiempo (`ms` / `ms_puente`) | Respuesta literal (resumen / fragmento) |
|---|---|---|---|
| **0** | **OK** | `1 ms` / `7 ms` | `ping`: `{"plugin": "ArbaMcp", "version": "1.11.0", "puerto": 8765, "dibujo": "<ruta>\\PRUEBA_111.dwg", "hay_dibujo": true, ...}` |
| **1** | **OK** | `104 ms` (`Eje 1`) · `53 ms` (`Terreno`) · `25 ms` (`Rasante 1`) | `Eje 1`: `0` a `831.3274`; `Terreno`: `9` puntos, `8` triángulos (`900..1700`, `1900..2400`), `cota_superficie(1300, 2150) = 104.5`; `Rasante 1`: `FG`, `3` PVI, `1` curva de `120 m`, `0` a `831.3274`; `Ensamblaje 1`: 4 subensamblajes (`LaneSuperelevationAOR` y `DaylightBench` en `Derecha` e `Izquierda`), `usado_en: []`. |
| **2** | **OK** | `3 ms` / `8 ms` | `listar_corredores`: `{"result": []}`; `backups\` para `PRUEBA_111`: 1 archivo (`PRUEBA_111_20261113_170233_crear_alineamiento_desde_polilinea.dwg`). |
| **3** (decisivo) | **OK** | `13 ms` / `22 ms` | `crear_corredor` (`simular: true`): `simulado: true`, `fin: 831.3274`, las cuatro frecuencias `10`, `superficie_objetivo: "Terreno"`, `esta_desactualizado: false`, `avisos`: `["Cuántos objetivos de superficie tiene el ensamblaje (uno por talud) se sabe al crear la región: la simulación no los puede contar ('objetivos_superficie' = null)."]`. |
| **4** | **OK** | `8–17 ms` (`ms_puente`) | 7 errores de Civil 3D con el texto esperado (ensamblajes listados: `Ensamblaje 1`). El 8.º caso (sin `frecuencia`) fue rechazado por validación Pydantic del puente: `1 validation error for crear_corredorArguments: args.frecuencia Field required`. |
| **5** (decisivo) | **OK** | `213 ms` / `224 ms` (`ms_espera: 19, ms_ejecucion: 194`) | `simulado: false`, `despues.objetivos_superficie: 2`, `inicio: 0`, `fin: 831.3274`, frecuencias en `10`, `esta_desactualizado: false`, `camino_objetivos: "antes_de_reconstruir"`, `region_creada_por_add: {"inicio": 0, "fin": 831.3274}`, `region_anadida_aparte: false`, `reconstrucciones: 1`, `ms_reconstruccion: 136`, `capa: "0"`, `restaurar.args.id: 1`, `copia.reutilizada: true`. `_.ZOOM _W 900,1900 1700,2400`: `"result": "terminado"` (`101 ms`), sin cuadros de diálogo. |
| **6** (decisivo) | **OK** | `4`, `6`, `5`, `2 ms` | `listar_corredores`, `listar_regiones` (`Región 1`, `0` a `831.3274`, frecuencias `10`), `listar_objetivos` (ambos `DaylightBench` con `TargetDTM` → `Terreno`) y `estado_corredor` coinciden exactamente con el paso 5. |
| **7** (decisivo) | **OK** | `1 ms` / `7 ms` | En `backups\` siguió el único archivo previo (porque `copia.reutilizada` fue `true` en el paso 5); sin llamadas de escritura extra ni cambios en `mcp_log.jsonl` fuera de lo esperado. |
| **8** | **OK** | `17 ms` / `26 ms` | `deshacer_objetivos` (`id: 1, simular: true`): `simulado: true`, plan de 2 elementos devolviendo `DaylightBench [superficie, TargetDTM]` a `[]`, `deshacer.pendientes` con entrada `id: 1` (`crear_corredor`). |
| **9** (decisivo) | **OK** | `95 ms` / `101 ms` (`ms_reconstruccion: 57`) | `Corredor 2`: `linea_base: "BL Eje 1"`, `region: "Tramo 1"`, `inicio: 100`, `fin: 600`, frecuencias `20`, `superficie_objetivo: "Terreno"`, `objetivos_superficie: 2`, `region_creada_por_add: {"inicio": 0, "fin": 831.3274}`, `restaurar.args.id: 2`, aviso por superficie única. `listar_regiones` confirma `Tramo 1` de `100` a `600` con frecuencias `20`. |
| **10** | **OK** | `126 ms`, `105 ms`, `14 ms` | Primer `_.UNDO 1`: **`Corredor 2` desapareció** de `listar_corredores` (deshizo la creación entera). Segundo `_.UNDO 1`: deshizo el `ZOOM` del paso 5 y `Corredor 1` siguió en `listar_corredores`. `deshacer_objetivos` (`simular: true`): `{"ok": false, "error": "Error de Civil 3D: ArgumentException: No existe el corredor 'Corredor 2'. Corredores del dibujo: Corredor 1."}`. |
| **11** | **OK** | `ms_puente: 15 ms` (`ms: 6`) | Repetición de `Corredor 1` sin `simular`: `{"ok": false, "error": "Error de Civil 3D: ArgumentException: Ya existe el corredor 'Corredor 1'. Usa listar_corredores o pasa otro 'nombre'."}`. Historial muestra `MCP ✗ crear_corredor no se ejecutó: falló la comprobación previa (simulación)...` sin `EXECUTEFUNCTION`; `backups\` sigue con 1 archivo; 1 sola línea `ok: false` en `mcp_log.jsonl`. |
| **12** | **OK** | `50 ms`, `16 ms`, `12 ms` | `Terreno 2` creada (`9` puntos); `Corredor 3` sin `superficie_objetivo` → `ok: false` (`el dibujo tiene 2 superficies (Terreno, Terreno 2)`); `Corredor 3` con `superficie_objetivo: "ninguna"` (`simular: true`) → `ok: true` con aviso y sin `superficie_objetivo` en `despues`. |
| **13** | **OK** | `1 ms` / `9 ms` | `leer_log` (`ultimas_n: 40`): registra todas las llamadas con `ok: false` en los errores controlados y `ok: true` en el resto. Dibujo no guardado. |

---

## Respuestas a las preguntas para la memoria técnica

1. **Rango de `CorridorCollection.Add` y `BaselineRegionCollection.Add`:**
   - En ambos pasos (5 y 9), `datos.region_creada_por_add` fue `{"inicio": 0, "fin": 831.3274}`: `CorridorCollection.Add` de 6 argumentos crea la región cubriendo **el rango completo de la línea base / rasante** (`0` a `831.3274`), y el ajuste posterior deja `100` a `600` en el paso 9.
   - `datos.region_anadida_aparte` fue **`false`** en ambos casos: **no** hizo falta `BaselineRegionCollection.Add`.

2. **Aparición de objetivos, reconstrucciones y tipos de objetivos:**
   - `datos.camino_objetivos`: **`"antes_de_reconstruir"`** (los objetivos ya aparecieron al llamar a `GetTargets()` antes de la primera reconstrucción).
   - `datos.reconstrucciones`: **`1`**.
   - Objetivos en la región (6 entradas totales):
     - **2 de superficie** (`tipo: "superficie"`, `parametro: "TargetDTM"`): `DaylightBench` (`Derecha` e `Izquierda`), asignados a `Terreno`.
     - **2 de elevación** (`tipo: "elevacion"`, `parametro: "Outside Elevation"`): `LaneSuperelevationAOR` (`Derecha` e `Izquierda`), vacíos (`[]`).
     - **2 de desplazamiento** (`tipo: "desplazamiento"`, `parametro: "Width"`): `LaneSuperelevationAOR` (`Derecha` e `Izquierda`), vacíos (`[]`).

3. **`Corridor.Rebuild()` dentro de la transacción de creación:**
   - **Sí** funcionó dentro de la misma transacción (sin error de reconstrucción), tardando `ms_reconstruccion: 136` ms en el paso 5 (`Corredor 1`, 0–831.3274 cada 10 m) y `ms_reconstruccion: 57` ms en el paso 9 (`Corredor 2`, 100–600 cada 20 m).

4. **`_.UNDO 1` sobre el corredor (paso 10):**
   - **Sí quitó el corredor del dibujo:** un solo `_.UNDO 1` eliminó por completo `Corredor 2` de `listar_corredores`. (El segundo `_.UNDO 1` revirtió el `_.ZOOM` del paso 5 y `Corredor 1` permaneció). `deshacer_objetivos` sobre la pila en memoria dio el error esperado `No existe el corredor 'Corredor 2'. Corredores del dibujo: Corredor 1.`.

5. **Subensamblajes del ensamblaje y capa del corredor:**
   - Subensamblajes literales (`Ensamblaje 1`): `LaneSuperelevationAOR` (`Derecha`), `DaylightBench` (`Derecha`), `LaneSuperelevationAOR` (`Izquierda`), `DaylightBench` (`Izquierda`).
   - Capa del corredor (`datos.capa`): **`"0"`**.

6. **Visualización en pantalla (paso 5):**
   - `_.ZOOM _W 900,1900 1700,2400` terminó con `"result": "terminado"` sin cuadros de diálogo abiertos; en la captura `capturar_pantalla` las paletas abiertas en Civil 3D cubrían el lienzo del dibujo, por lo que no se inspeccionaron visualmente los taludes en la imagen.

---

## Miembros de la sección `1.11.0` (`miembros_por_verificar_civil3d.md`)

Pasan a **`ejecutado en Civil 3D 2027`** con fecha **2026-11-13**:
- `CorridorCollection.Add(string, string, ObjectId, ObjectId, string, ObjectId)` — pasos 5 y 9 (creó `Corredor 1` y `Corredor 2` con su línea base y región de `0` a `831.3274`).
- `CivilDocument.CorridorCollection` y `CivilDocument.AssemblyCollection` — pasos 3, 4, 5, 9, 11 y 12.
- `BaselineRegion.StartStation`, `.EndStation`, `.AssemblyId`, `.Name` — pasos 5 y 9 (incluyendo el ajuste de `0..831.3274` a `100..600` sobre región recién creada en `Corredor 2`).
- `BaselineRegion.AppliedAssemblySetting` → `FrequencyAlongTangents`, `FrequencyAlongCurves`, `FrequencyAlongSpirals`, `FrequencyAlongProfileCurves` — pasos 5 y 9 (frecuencias `10` y `20` sobre regiones recién creadas).
- `BaselineRegion.GetTargets()` / `SetTargets(SubassemblyTargetInfoCollection)`, `SubassemblyTargetInfo.TargetIds`, `.TargetType` — pasos 5 y 9 antes de la primera reconstrucción (`camino_objetivos = "antes_de_reconstruir"`).
- `Corridor.Rebuild()` dentro de la transacción que crea el corredor — pasos 5 y 9 (`reconstrucciones = 1`, `esta_desactualizado = false`).
- `Corridor.Baselines`, `CorridorSurfaces`, `IsOutOfDate`, `RebuildAutomatic`, `Baseline.Name`, `AlignmentId`, `ProfileId`, `BaselineRegions` — pasos 5, 6 y 9.
- `Entity.Layer` sobre el corredor (`datos.capa = "0"`) — pasos 5 y 9.
- `Assembly.Groups`, `AssemblyGroup.Name`, `GetSubassemblyIds()`, `Subassembly.Name`, `Subassembly.Side` — pasos 1, 5 y 9.
- `Alignment.StartingStation`, `EndingStation`; `Profile.StartingStation`, `EndingStation`, `ProfileType` — pasos 3, 4, 5, 9 y 12.

Sigue **`por verificar`**:
- `BaselineRegionCollection.Add(string, ObjectId, double, double)` (respaldo) — no se ejecutó porque `CorridorCollection.Add` ya creó la región (`region_anadida_aparte = false`).
