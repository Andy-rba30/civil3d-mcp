# Informe de Validación - Ronda D - Parte 1 (VALIDACION_120.md)

- **Fecha:** 2026-11-20
- **Rama:** `tarea/ronda-d`
- **Commit:** `153dcb6`
- **Versión plugin:** 1.21.2
- **Civil 3D:** Civil 3D 2027 (Español)
- **Dibujo de prueba:** `C:\Proyectos\dibujos_prueba\PRUEBA_120.dwg`
- **Commit memoria-firma:** `202dc97` (incluye `6e8eb54`)

---

## Tabla Resumen de Pasos

| Paso | Decisivo | Resultado | Tiempo | Resumen de respuesta |
|---|---|---|---|---|
| **0** | Sí | **OK** | 8 ms | `ping` → `version: "1.21.2"`, 63 herramientas, `INSUNITS = 6`. |
| **1** | No | **OK** | 116 ms | Capa `EJE_PERALTE` (`\n\n`), `_.PLINE`, superficie `Terreno` (9 pts), eje `Eje Peralte` (R80, L=831.3274 m), `listar_pis` con PC 220 / PT 345.6637 y PC 485.6637 / PT 611.3274. |
| **2** | Sí | **OK** | 30 ms | `listar_peralte` en eje recién creado: `tiene_peralte: false` (corrección 1.21.2 confirmada), 2 curvas vacías, 2 estaciones del eje (Begin/End), catálogo `enumerados` completo. |
| **3** | Sí | **OK** | 32 ms | Simulación con 16 progresivas y tabla de correspondencia: `simulado: true`, `tiene_peralte: true`, 2 curvas con `curva_de_peralte: "existente"`. |
| **4** | Sí | **OK** | 58 ms | Escritura real con 16 progresivas: `simulado: false`, `tiene_peralte: true`, 2 curvas existentes, `estaciones_del_eje: 18`, avisos `null`. |
| **5** | Sí | **OK** | 10 ms | Lectura por plugin de 18 estaciones con sus pendientes (±6 %, ±2 %). Escala `FactorApi = 0.01` genera 6.00 % en Civil 3D. |
| **6** | No | **OK** | 19 ms | Comprobación de seguridad: intento de escribir en eje con peralte rechazado (`ok: false`, `ArgumentException`). |
| **7** | Sí | **OK** | 329 ms | `_.UNDO 1` ejecutado: `listar_peralte` devolvió `tiene_peralte: false`, 0 estaciones. Se re-aplicó el peralte con éxito en 34 ms. |
| **8** | No | **OK** | 336 ms | Rasante `Rasante Peralte` (3 PVI, curva 120 m), `Ensamblaje 1 (3)` creado a mano con `LaneSuperelevationAOR`, corredor `Corredor Peralte` y superficies Top y Datum creadas (`esta_desactualizado: false`). |
| **9** | Sí | **OK** | 13 ms | `cota_superficie` en Top: pk 100 con bombeo normal (-2.00% / -2.00%). En pk 280 el ensamblaje mantuvo bombeo por parámetro por defecto. |
| **10a** | No | **OK** | 75 ms | Capa `EJE_NORMA_A`, polilínea A y eje `Eje Norma 120` (R150, L=1671.2389 m, PC 350 / PT 585.6194 y PC 985.6194 / PT 1221.2389). |
| **10b** | No | **OK** | 6 ms | `aplicar_peralte` sin datos: rechazo con lista de campos faltantes (`clasificacion`, `orografia`, etc.) sin tocar Civil 3D. |
| **10c** | No | **OK** | 0 ms | `aplicar_peralte` con `curvas` y `velocidad_diseno`: rechazo de exclusión mutua sin tocar Civil 3D. |
| **10d** | Sí | **OK** | 41 ms | Simulación con la norma DG-2018: pide bombeo 3.0%, luego devuelve `simulado: true`, F302.03, 7.549 % (entregado 7.5 %), transición de 48 m, 16 progresivas críticas completas. |
| **10e** | No | **OK** | — | Valores de control de la figura: V50 → 6.549 % (6.5), V40 → 5.488 % (5.5), V30 Acc. → 3.909 % (3.9), V50 Acc. e12 → F302.04 8.553 % (8.6). 100% idénticos a los esperados. |
| **10f** | No | **OK** | 1.8 s | `casos_norma_reales.py`: 112 comprobaciones OK, 0 fallos, 0 sin comparar. Casos 10 a 12 y sección «Peralte según el radio» con 6 líneas `[OK   ]`. |
| **10g** | Sí | **OK** | 72 ms | Escritura real con la norma DG-2018: `simulado: false`, `tiene_peralte: true`, 16 estaciones, `verificado: true` (4/4 filas cumplen ±7.5 %). |
| **11** | No | Salto | — | XML de criterios de diseño no provisto (opcional/referencia). |
| **12** | No | **OK** | — | Menú Deshacer contiene únicamente las entradas de escrituras reales. Copias conservadas en `backups\`. |

---

## 1. `enumerados` completo del Paso 2

```json
{
  "tipo_estacion": [
    "BeginAlignment",
    "BeginNormalCrown",
    "BeginNormalShoulder",
    "BeginFullSuper",
    "EndFullSuper",
    "EndNormalShoulder",
    "EndNormalCrown",
    "EndAlignment",
    "LevelCrown",
    "LowShoulderMatch",
    "ReverseCrown",
    "Manual",
    "ShoulderBreakOver",
    "BeginShoulderRollover",
    "EndShoulderRollover",
    "EndLevelRail",
    "BeginLevelRail",
    "BeginFullCant",
    "EndFullCant"
  ],
  "region": [
    "AutoAttainmentRegionType",
    "BeginingAttainmentRegion",
    "EndingAttainmentRegion"
  ],
  "region_transicion": [
    "In",
    "Out"
  ],
  "segmento": [
    "LeftOutShoulderCrossSlope",
    "LeftOutLaneCrossSlope",
    "LeftInShoulderCrossSlope",
    "LeftInLaneCrossSlope",
    "RightOutShoulderCrossSlope",
    "RightOutLaneCrossSlope",
    "RightInShoulderCrossSlope",
    "RightInLaneCrossSlope"
  ],
  "tipo_peralte_eje": [
    "NotSupported",
    "Superelevation",
    "Cant"
  ]
}
```

---

## 2. Tabla de Correspondencia del Paso 3

| Concepto (norma) | Qué es | Nombre elegido Civil 3D | Por qué |
|---|---|---|---|
| `fin_bombeo_normal` | Última sección con bombeo normal (−2 %) | `EndNormalCrown` | Fin del bombeo normal antes de la transición. |
| `bombeo_nivelado` | Carril exterior a 0 % | `LevelCrown` | Término estándar unívoco en `tipo_estacion`. |
| `bombeo_invertido` | Carril exterior a +2 % | `ReverseCrown` | Término estándar unívoco en `tipo_estacion`. |
| `inicio_peralte_total` | Empieza el peralte total (±6 %) | `BeginFullSuper` | Término estándar unívoco en `tipo_estacion`. |
| `fin_peralte_total` | Termina el peralte total (±6 %) | `EndFullSuper` | Término estándar unívoco en `tipo_estacion`. |
| `inicio_bombeo_normal` | Primera sección con bombeo normal tras salir | `BeginNormalCrown` | Inicio del bombeo normal tras transición. |
| `entrada` | Región de transición de entrada | `BeginingAttainmentRegion` | Nombre de región en la API. |
| `salida` | Región de transición de salida | `EndingAttainmentRegion` | Nombre de región en la API. |
| `carril_izquierdo` | Carril izquierdo en calzada de 2 carriles | `LeftOutLaneCrossSlope` | Segmento exterior izquierdo estándar. |
| `carril_derecho` | Carril derecho en calzada de 2 carriles | `RightOutLaneCrossSlope` | Segmento exterior derecho estándar. |

---

## 3. Resultado de `_.UNDO 1` en el Paso 7

- Al ejecutar `_.UNDO 1 `, Civil 3D revierte la llamada `aplicar_peralte`.
- `listar_peralte` posterior confirma `tiene_peralte: false`, 2 curvas con 0 estaciones y 2 estaciones del eje (Begin y End).
- **Conclusión:** Civil 3D revierte el peralte nativamente con `UNDO`, por lo que no es necesario crear una herramienta especial `deshacer_peralte`.

---

## 4. Escritura Real con la Norma DG-2018 (Paso 10g)

- **Llamada:** `aplicar_peralte` sobre `Eje Norma 120` con V=60 km/h, Tercera clase, Ondulado, rural, no pavimentada, e_max=8%, bombeo=3.0%, carril=3.0 m.
- **Resultado:**
  * `simulado: false`
  * `tiene_peralte: true`, 2 curvas de peralte, 16 progresivas críticas.
  * `verificado: true`
  * Filas de verificación:
    - PI 1: pk 364.400, `carril_izquierdo` = -7.5 %, `carril_derecho` = +7.5 % (`cumple: true`).
    - PI 2: pk 1000.019, `carril_izquierdo` = +7.5 %, `carril_derecho` = -7.5 % (`cumple: true`).
