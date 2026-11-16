# Informe de Validación 1.17.1 (en rama `tarea/ronda-b`, v1.17.4)

**Fecha:** 2026-11-14  
**Rama / Commit:** `tarea/ronda-b` (`b0287f8`)  
**Versión de `ping`:** `1.17.4`  
**Civil 3D:** 2027 (Español)  
**DWG usado:** `PRUEBA_1142.dwg`  
**Herramientas MCP `civil3d`:** 58  

---

## 1. Tabla de Resultados por Paso

| Paso | Herramienta | Resultado | Tiempo | Respuesta literal / Detalle |
|---|---|---|---|---|
| **0** | `ping` | **OK** | `7 ms` (`ms: 1`) | `{"plugin": "ArbaMcp", "version": "1.17.4", "dibujo": "<ruta>PRUEBA_1142.dwg", "hay_dibujo": true}` |
| **1a** | `listar_superficies` | **OK** | `29 ms` (`ms: 15`) | `Terreno`, `Corredor 1 - Top`, `Corredor 1 - Datum`, `Terreno 2`, `Corredor 1 - Volumen` (`TinVolume`); sin `Volumen 1171` |
| **1b** | `listar_lineas_muestreo` | **OK** | `30 ms` (`ms: 20`) | `Eje 1 - Muestreo` (`n_lineas: 43`, `0`..`831.3274`), `Muestreo 100` (`n_lineas: 10`) |
| **2** | Punto de partida | **OK** | `—` | `13` archivos en `backups\` |
| **3** | `calcular_volumenes` (`simular: true`) | **OK** | `101 ms` (`ms: 76`) | `simulado: true`, `supuesto_ejes.comprobado: true`, `motivo: null`, `lineas_tabla: 43`, `puntos_secciones.tramo_valido: {"desde_comun": -30, "hasta_comun": 30, "tramos": [{"desde": -3.9, "hasta": 3.6}], "ancho_valido": 7.5, "ancho_descartado": 52.5, "puntos_probados": 41}` |
| **4** | `calcular_volumenes` (real) | **OK** | `373 ms` (`ms: 350`) | `Volumen 1171` (`TinVolume`), `corte: 7211.3428`, `relleno: 29907.859`, `neto: -22696.5162`; `tabla_progresivas`: `tramos: 42`, `tramos_sin_volumen: 0`, `diferencia_con_superficie_pct: {"corte": 0.3058, "relleno": 0.0496}` |
| **5** | `backups\` | **OK** | `—` | `14` archivos en `backups\` (1 copia nueva, `reutilizada: false`), sin guardar el dibujo |
| **6** | `leer_log` | **OK** | `—` | Todas las llamadas de la validación registradas con `ok: true` |

---

## 2. Bloques Literales (`recorte` y `puntos_secciones`)

### `tabla_progresivas.recorte` (Paso 4)
```json
{
  "metodo": "Surface.FindElevationAtXY de la Datum y del terreno en Alignment.PointLocation(pk, desplazamiento), en los puntos de las dos secciones, en el punto medio de cada tramo y cada 5 m como mucho; los bordes se afinan por bisección hasta 0,01 m, del lado de dentro",
  "precision": 0.01,
  "paso_maximo": 5,
  "lineas": 43,
  "lineas_recortadas": 43,
  "lineas_sin_tramo_valido": 0,
  "lineas_con_varios_tramos": 0,
  "ancho_medio_descartado": 39.7228,
  "ancho_maximo_descartado": 52.5,
  "puntos_probados": 2192
}
```

### `puntos_secciones.tramo_valido` (Pasos 3 y 4)
```json
{
  "desde_comun": -30,
  "hasta_comun": 30,
  "tramos": [
    {
      "desde": -3.9,
      "hasta": 3.6
    }
  ],
  "ancho_valido": 7.5,
  "ancho_descartado": 52.5,
  "puntos_probados": 41
}
```

---

## 3. Preguntas para la Memoria Técnica

1. **`tabla_progresivas.recorte` (Paso 4):** Se recortaron **43** de 43 líneas, **0** quedaron sin tramo válido, ancho medio descartado **39.7228 m** y máximo descartado **52.5 m**.
2. **`puntos_secciones.tramo_valido` (Pasos 3 y 4):** Coinciden exactamente. La línea `0+000.00` tiene **1 tramo** válido: desde **-3.9 m** hasta **3.6 m** (`ancho_valido: 7.5 m`, `ancho_descartado: 52.5 m`).
3. **`diferencia_con_superficie_pct` frente a 1.14.3:**
   - **Corte:** **0.3058 %** (antes `16.5806 %`; bajó claramente). Tabla `7233.3954 m³` vs superficie `7211.3428 m³`.
   - **Relleno:** **0.0496 %** (antes `4.1347 %`; bajó claramente). Tabla `29922.7012 m³` vs superficie `29907.859 m³`.
   - **Neto:** Tabla `-22689.3058 m³` vs superficie `-22696.5162 m³`.
4. **Filas `0`, `400` y última (`0+831.33`, pk `831.3274`):**
   - `0+000.00`: `area_corte: 3.5208`, `area_relleno: 0`, `ancho_descartado: 52.5` (antes en 1.14.3: `5.1339` / `7.521`).
   - `0+400.00`: `area_corte: 0`, `area_relleno: 95.2254`, `ancho_descartado: 28.9073` (antes: `0` / `95.2254`).
   - `0+831.33`: `area_corte: 68.6451`, `area_relleno: 0`, `ancho_descartado: 33.5897`, `masa: -22689.3058` igual a `totales.neto` (antes: `72.4808` / `0`).
5. **Tiempos y puntos probados:** Paso 3 `76 ms` (`ms_puente: 101 ms`) vs `108 ms` en 1.14.3; Paso 4 `350 ms` (`ms_puente: 373 ms`) vs `421 ms` en 1.14.3. `recorte.puntos_probados: 2192`.

---

## 4. Miembros de la API (Sección 1.17.1)

- **Pasan a `ejecutado en Civil 3D 2027` (2026-11-14):**
  - `Surface.FindElevationAtXY` como límite de la superficie (`Corredor 1 - Datum` y `Terreno`) en todo el ancho de la línea.
  - `Alignment.PointLocation(pk, desplazamiento)` hasta ±30 m.
  - Hipótesis de líneas perpendiculares al eje y causa del exceso en la tabla (área fuera de la Datum) confirmadas.
