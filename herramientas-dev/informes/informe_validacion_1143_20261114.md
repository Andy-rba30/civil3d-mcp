# Informe de validación 1.14.3 (`civil3d-mcp`)

- **Fecha:** 2026-11-14
- **Civil 3D:** 2027 (español)
- **Rama instalada:** `tarea/2.9-tabla-pi` (commit `b37b21a`, PR #35)
- **Versión `ping`:** `1.15.1`
- **DWG usado:** `PRUEBA_1142.dwg` (tal como quedó guardado tras `VALIDACION_1142.md` y `VALIDACION_115.md`)
- **Resultado global:** **OK** (todos los pasos decisivos 0, 3, 4 y 5 en **OK**)

---

## Tabla de pasos

| Paso | Herramienta(s) | Resultado | Respuesta literal (resumen) | Tiempo (`ms`) |
|---|---|---|---|---|
| 0 | `ping` + lista de herramientas | **OK** | `{"plugin": "ArbaMcp", "version": "1.15.1", "dibujo": "<ruta>PRUEBA_1142.dwg", "hay_dibujo": true}` — 54 herramientas del plugin (55 con `buscar_api`), incluida `listar_pis`. | `12 ms` (`ms_puente: 298`) |
| 1 | `listar_superficies`, `listar_lineas_muestreo` | **OK** | Superficies: `Terreno`, `Corredor 1 - Top`, `Corredor 1 - Datum`, `Terreno 2`, `Corredor 1 - Volumen` (`TinVolume`); sin `Volumen 1143`. Grupos en `Eje 1`: `Eje 1 - Muestreo` (`43` líneas), `Muestreo 100` (`10` líneas). | `51 ms` + `136 ms` |
| 2 | `backups\` | **OK** | `11` archivos previos en `backups\`. | N/A |
| 3 | `calcular_volumenes` (`simular: true`) | **OK** | `simulado: true`, `lineas_tabla: 43`, `supuesto_ejes: {"comprobado": true, "lineas_comprobadas_con_la_superficie": 1, "motivo": null}`, `puntos_secciones.comprobacion_completa: true`, aviso literal de `LeftOffset/RightOffset` (`-3.9` a `3.6`). | `108 ms` (`ms_puente: 168`) |
| 4 | `calcular_volumenes` (real, `Volumen 1143`) | **OK** | `corte: 9014.1785`, `relleno: 32898.6449`, `neto: -23884.4664`, `1516` triángulos. `tabla_progresivas`: `numeros: true`, `supuesto_ejes.comprobado: true`, `tramos: 42`, `tramos_sin_volumen: 0`, `totales.neto: -23750.1252` (= `masa` última fila). | `421 ms` (`ms_puente: 520`) |
| 5 | `pk_a_punto`, `cota_superficie` | **OK** | PK `400` (`x: 1300, y: 2134.3363`): `Terreno = 104.1867`, `Corredor 1 - Datum = 109.0011`. Fila `0+400.00`: `area_corte: 0`, `area_relleno: 95.2254`. | `7 ms` + `5 ms` + `3 ms` |
| 6 | `backups\` | **OK** | `1` archivo nuevo (`<ruta>PRUEBA_1142_20261114_123752_calcular_volumenes.dwg`, total `12`). Dibujo no guardado. | N/A |
| 7 | `leer_log` (`ultimas_n: 15`) | **OK** | Todas las llamadas registradas con `ok: true` y `version: "1.15.1"`. | `3 ms` |

---

## Respuestas literales clave (Paso 4)

- **`despues`:**
  ```json
  {"existe": true, "tipo": "TinVolume", "superficie_base": "Terreno", "superficie_comparacion": "Corredor 1 - Datum", "factor_corte": 1.25, "factor_relleno": 1.1, "estilo": "Contours 5m and 25m (Background)", "corte": 9014.1785, "relleno": 32898.6449, "neto": -23884.4664, "triangulos": 1516, "esta_desactualizada": false, "grupo_muestreo": "Eje 1 - Muestreo"}
  ```
- **`tabla_progresivas` (resumen y filas `0`, `400`, `831.3274`):**
  ```json
  {"grupo": "Eje 1 - Muestreo", "alineamiento": "Eje 1", "numeros": true, "supuesto_ejes": {"comprobado": true, "lineas_comprobadas_con_la_superficie": 1, "motivo": null}, "numero_lineas": 43, "tramos": 42, "tramos_sin_volumen": 0, "totales": {"corte": 8407.0288, "relleno": 31144.4647, "corte_ajustado": 10508.786, "relleno_ajustado": 34258.9112, "neto": -23750.1252}, "diferencia_con_superficie_pct": {"corte": 16.5806, "relleno": 4.1347}}
  ```
  - Primera fila (`0`): `{"linea": "0+000.00", "pk": 0, "area_corte": 5.1339, "area_relleno": 7.521, "corte_acumulado": 0, "relleno_acumulado": 0, "masa": 0, "falta": null}`
  - Fila `400`: `{"linea": "0+400.00", "pk": 400, "area_corte": 0, "area_relleno": 95.2254, "volumen_corte": 0, "volumen_relleno": 1969.0668, "volumen_corte_ajustado": 0, "volumen_relleno_ajustado": 2165.9734, "corte_acumulado": 146.2534, "relleno_acumulado": 26077.8839, "masa": -25931.6304, "falta": null}`
  - Última fila (`831.3274`): `{"linea": "0+831.33", "pk": 831.3274, "area_corte": 72.4808, "area_relleno": 0, "volumen_corte": 794.3422, "volumen_relleno": 0, "volumen_corte_ajustado": 992.9278, "volumen_relleno_ajustado": 0, "corte_acumulado": 10508.786, "relleno_acumulado": 34258.9112, "masa": -23750.1252, "falta": null}`
- **`avisos`:**
  - `"En la línea '0+000.00' un punto de la sección de 'Corredor 1 - Datum' tiene X = -30, fuera de los desplazamientos de la sección (-3.9 a 3.6) (Section.LeftOffset/RightOffset y las cotas mínima y máxima): no bloquea la tabla, se anota en puntos_secciones para miembros_por_verificar_civil3d.md."`
  - `"La tabla por progresivas difiere más de un 10 % de la superficie de volumen (corte 16.5806 %, relleno 4.1347 %): es normal si el grupo no cubre todo el corredor o las líneas están muy separadas (áreas medias). Manda la superficie de volumen."`

---

## Preguntas para la memoria técnica

1. **`puntos_secciones` entero (Paso 4):**
   ```json
   {
     "linea": "0+000.00",
     "pk": 0,
     "ancho_linea": {"izquierdo": 30, "derecho": 30},
     "terreno": {
       "superficie": "Terreno",
       "left_offset": -30,
       "right_offset": 30,
       "left_swath_width": 30,
       "right_swath_width": 30,
       "minmum_elevation": 99.4,
       "maximum_elevation": 100.6,
       "numero_puntos": 3,
       "primeros": [{"x": -30, "y": 100.6}, {"x": 0, "y": 100}, {"x": 30, "y": 99.4}],
       "ultimos": [{"x": -30, "y": 100.6}, {"x": 0, "y": 100}, {"x": 30, "y": 99.4}]
     },
     "datum": {
       "superficie": "Corredor 1 - Datum",
       "left_offset": -3.9,
       "right_offset": 3.6,
       "left_swath_width": 30,
       "right_swath_width": 30,
       "minmum_elevation": 99.478,
       "maximum_elevation": 100.078,
       "numero_puntos": 9,
       "primeros": [{"x": -30, "y": 100.7066}, {"x": -3.9, "y": 100.078}, {"x": -3.6002, "y": 99.9281}],
       "ultimos": [{"x": 14.0508, "y": 99.925}, {"x": 22.7597, "y": 99.9224}, {"x": 30, "y": 99.9203}]
     },
     "cotas_comparadas": [
       {"superficie": "Terreno", "desplazamiento": 0, "cota_seccion": 100, "cota_superficie": 100, "diferencia": 0},
       {"superficie": "Corredor 1 - Datum", "desplazamiento": 0, "cota_seccion": 99.55, "cota_superficie": 99.55, "diferencia": 0},
       {"superficie": "Terreno", "desplazamiento": -5, "cota_seccion": 100.1, "cota_superficie": 100.1, "diferencia": 0},
       {"superficie": "Corredor 1 - Datum", "desplazamiento": -5, "cota_seccion": 100.1045, "cota_superficie": null, "diferencia": null},
       {"superficie": "Terreno", "desplazamiento": 5, "cota_seccion": 99.9, "cota_superficie": 99.9, "diferencia": 0},
       {"superficie": "Corredor 1 - Datum", "desplazamiento": 5, "cota_seccion": 99.5379, "cota_superficie": null, "diferencia": null}
     ],
     "comprobacion_completa": true
   }
   ```
   - `LeftSwathWidth` / `RightSwathWidth` valen **`30` y `30`** en ambas secciones (miden el ancho de franja de la línea de muestreo).
   - `LeftOffset` / `RightOffset` en `Terreno` valen **`-30` y `30`**, pero en `Corredor 1 - Datum` valen **`-3.9` y `3.6`** (el ancho del corredor/calzada en esa estación, no los extremos de los `SectionPoints`, que llegan de `-30` a `30`).
   - `MinmumElevation` / `MaximumElevation` en `Terreno` valen `99.4` y `100.6`; en `Corredor 1 - Datum` valen `99.478` y `100.078` (mientras que el primer punto en `X = -30` tiene `Y = 100.7066`, mayor que `100.078`).

2. **`cotas_comparadas`:**
   - **Sí** coincidió con `diferencia: 0` en todos los puntos comparables (`Terreno` en `0`, `-5`, `5`; `Corredor 1 - Datum` en `0`). En `-5` y `5` de la Datum `cota_superficie` es `null` porque la superficie del corredor termina antes de ±5 m. Confirma que `X` es el desplazamiento (negativo a la izquierda) e `Y` es la cota.

3. **Tabla por progresivas:**
   - `totales`: `corte = 8407.0288`, `relleno = 31144.4647`, `corte_ajustado = 10508.786`, `relleno_ajustado = 34258.9112`, `neto = -23750.1252`.
   - `diferencia_con_superficie_pct`: `corte = 16.5806 %`, `relleno = 4.1347 %`.
   - Tramos sin volumen: `0` (`tramos_sin_volumen: 0`).

4. **Paso 5 (PK `400`):**
   - `Terreno = 104.1867`, `Corredor 1 - Datum = 109.0011` (hay que rellenar `4.8144` m en el eje).
   - Fila `pk: 400`: `area_corte = 0`, `area_relleno = 95.2254` (> 0). Signo verificado.

5. **Tiempos (`ms`):**
   - Paso 3 (simulación): `108 ms` (`ms_puente: 168`).
   - Paso 4 (creación real): `421 ms` (`ms_puente: 520`).

---

## Miembros de la sección 1.14.3 verificados (`2026-11-14`)

- `Section.LeftOffset`, `Section.RightOffset` en sección de superficie de corredor → **ejecutado en Civil 3D 2027** (`-3.9` y `3.6` en `Corredor 1 - Datum`, menor que el rango `-30..30` de los `SectionPoints`).
- `Section.LeftSwathWidth`, `Section.RightSwathWidth` → **ejecutado en Civil 3D 2027** (`30` y `30`, ancho de la línea de muestreo).
- `Section.MinmumElevation`, `Section.MaximumElevation` → **ejecutado en Civil 3D 2027** (`99.4..100.6` en Terreno; `99.478..100.078` en Datum).
- `SampleLine.Vertices` + `Alignment.StationOffset` como ancho real de la línea en `calcular_volumenes` → **ejecutado en Civil 3D 2027** (`izquierdo: 30, derecho: 30`).
- `Surface.FindElevationAtXY` sobre `Corredor 1 - Datum` en `Alignment.PointLocation` dentro de `calcular_volumenes` → **ejecutado en Civil 3D 2027** (`99.55`, `diferencia: 0` en desplazamiento `0`).
- Supuesto `SectionPoint.Location` (`X` = desplazamiento negativo a la izquierda, `Y` = cota) → **ejecutado en Civil 3D 2027** (`supuesto_ejes.comprobado: true`, `cotas_comparadas` con `diferencia: 0`).
