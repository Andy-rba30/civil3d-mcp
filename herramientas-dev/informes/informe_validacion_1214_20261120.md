# Informe de Validación v1.21.4 (`crear_bordes_calzada` y Sobreancho DG-2018)

- **Fecha:** 2026-11-20
- **Versión de Civil 3D:** Autodesk Civil 3D 2027 (Español Métrico)
- **Rama instalada:** `tarea/ronda-d` (commit `b9e6212`)
- **Versión plugin / ping:** `1.21.4` (63 herramientas: 59 plugin C# + 4 puente Python)
- **DWG de prueba:** `PRUEBA_121.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)
- **Memoria de firmas (`memoria-firma`):** commit `202dc97` (`MEMORIA_FIRMA_DIR = C:\Proyectos\memoria-firma`)
- **Pruebas previas:** `dotnet test` (720 superadas, 0 errores) y `pytest` (377 superadas, 0 errores).

---

## 1. Tabla Resumen de Ejecución Paso a Paso

| Paso | Herramienta / Acción | Parámetros Clave | Resultado | Tiempo (`ms_puente`) | Observaciones / Respuestas Literales |
| :---: | :--- | :--- | :---: | :---: | :--- |
| **0** | `ping`<br>`leer_variable` | `INSUNITS` | **OK** | 20 ms<br>33 ms | `version: "1.21.4"`, `hay_dibujo: true`, `INSUNITS: "6"` (metros). 63 herramientas verificadas. |
| **1a** | `ejecutar_comando` | `_.-LAYER _M EJE_PRUEBA`<br>`_.PLINE` Eje 1 | **OK** | 227 ms<br>406 ms | `result: "terminado"` en ambos. |
| **1b** | `ejecutar_comando` | `_.-LAYER _M EJE_DESHACER`<br>`_.PLINE` Eje Deshacer | **OK** | 118 ms<br>126 ms | `result: "terminado"` en ambos. |
| **1c** | `crear_superficie_tin` | `Terreno` desde `terreno_corredor.csv` | **OK** | 1851 ms | Superficie TIN creada: 9 puntos, 8 triángulos. Backup: `PRUEBA_121_20261120_111014_crear_superficie_tin.dwg`. |
| **1d** | `crear_alineamiento_desde_polilinea` | `Eje 1`, R=80 | **OK** | 1301 ms | `despues.longitud: 831.3274`, 2 curvas (PI 1 izq PC 220–PT 345.6637, PI 2 der PC 485.6637–PT 611.3274). |
| **1e** | `crear_alineamiento_desde_polilinea` | `Eje Deshacer`, R=80 | **OK** | 273 ms | `despues.longitud: 831.3274`. |
| **1f** | `crear_rasante` | `Rasante 1`, 3 PVI | **OK** | 600 ms | Rasante `FG` de 0 a 831.3274 m, 1 curva vertical parabólica. |
| **1g** | `listar_ensamblajes` | Consulta de ensamblajes | **OK** | 145 ms | `<ensamblaje>` = `Ensamblaje 1 (3)` con `LaneSuperelevationAOR` y `DaylightBench` a ambos lados. |
| **1h** | `guardar_dibujo` | Guardar DWG | **OK** | 945 ms | Guardado en `PRUEBA_121.dwg` (1,134,833 bytes). |
| **2** | `crear_bordes_calzada` | `simular: true`, despl=3.6, ENS | **OK** | 40 ms | `simulado: true`. Cada ensanchamiento trae `rango_add_widening`: `[180, 345.6637]` y `[445.6637, 611.3274]`. Log: N2=1040→1041; Backups: C2=31 (sin cambios). |
| **3** | `crear_bordes_calzada` | `simular: false`, despl=3.6, ENS (**Decisivo**) | **OK** | 236 ms | **100% Exitoso.** `sobreancho_maximo: 0.6` en ambos bordes (`fallidas: 0`). Región `Specific` en `220–345.6637` (`-4.2`) y `485.6637–611.3274` (`4.2`), transiciones `180–220` y `345.6637–385.6637`. Log: 1042; Backups: 32 (`PRUEBA_121_20261120_111103_crear_bordes_calzada.dwg`). |
| **4** | Inspección visual y Prospector (**Decisivo**) | 4a, 4b (sin zoom) y 4c (post-paso 9 con zoom) | **OK** | - | **4a (Jose):** Valor de desfase nominal: `-3.600m`.<br>**4b:** Regiones en API: `180–220` transición, `220–345.6637` pleno `-4.2`, `345.6637–385.6637` transición.<br>**4c (Jose post-paso 9):** "se ve un poco brusca la transición". |
| **5a-c** | `listar_bordes_calzada` | `Eje 1`, `Eje 1 - Borde izq`, `Eje Deshacer` | **OK** | 36 / 37 / 16 ms | 2 bordes reportados para `Eje 1` (iguales a paso 3) y 0 bordes para `Eje Deshacer` con aviso literal. Log: +3 (1045). |
| **6a-c** | `crear_bordes_calzada` | Bordes existentes, borde como eje, solape | **OK** | 29 / 17 / 39 ms | Todos rechazados con `ok: false` y el mensaje esperado sin tocar el dibujo. Log: +3 (1048), Backups: 32. |
| **7a-c** | `crear_corredor`, `listar_regiones`, `listar_objetivos` | `Corredor 1` con `Ensamblaje 1 (3)` | **OK** | 1205 / 27 / 23 ms | Corredor creado y reconstruido (`esta_desactualizado: false`). Objetivos de desplazamiento `Width` vacíos en `Izquierda` y `Derecha`. |
| **7d** | `asignar_objetivos` | Simular y real con `opcion: "mas_cercano"` / sin `opcion` | **HALLAZGO (Fallo con `mas_cercano`) / OK sin `opcion`** | 44 ms (sim)<br>68 ms (err)<br>93 ms (ok) | Simulación OK. En real con `"opcion": "mas_cercano"`, Civil 3D falló con `InvalidOperationException: The count of TargetIds should be greater or equal to 2.` Sin `"opcion"`, asignó ambos bordes con éxito (0 fallidas). |
| **7e-f** | `reconstruir_corredor`, `listar_objetivos` | `Corredor 1` | **OK** | 1254 / 21 ms | `esta_desactualizado: false`. Jose confirma: **no** hay avisos de «Width not found» u «Offset target not found» en Visor de sucesos. |
| **8a-b** | `crear_secciones` y medición con `_.DIST` | Progresivas 100, 200, 300 (**Decisivo**) | **OK** | 433 ms | 3 líneas y 3 vistas de sección creadas. Jose confirma mediciones en vistas: **todo bien** (ensanchamiento visible en curva). |
| **9a-e** | `crear_bordes_calzada` + `_.UNDO 1` | `Eje Deshacer` (**Decisivo**) | **OK** | 238 / 857 / 15 ms | `_.UNDO 1` quitó **ambos bordes** (`Eje Deshacer - Borde izq` y `Eje Deshacer - Borde der`) de una sola vez (`numero_bordes: 0`). |
| **10** | Menú Deshacer y `backups\` | Inspección de pila | **OK** | - | Captura muestra entradas agrupadas (`Grupo de comandos`, equivalencia española de `Executefunction`) más `Intellizoom`/`Intellipan` posteriores. Backups totales: 32. |
| **11** | `casos_norma_reales.py` + `crear_bordes_calzada` | V=60 sin datos y con `T3S3` (**Decisivo**) | **OK** | 6 / 3 ms | `casos_norma_reales.py`: **112 OK, 0 fallos**. Ambas llamadas devolvieron `faltan` sin tocar Civil 3D. Log sin cambios: N11=1062. |
| **12** | `crear_bordes_calzada` | V=60, n=2, calzada=7.2, B2 (`simular: true`) | **OK** | 70 ms | `propuesta_norma` completo: `Sa = 1.303 m` (`sin_factor: 2.0682`, `factor: 0.63`), `pendiente_de_decision` P4–P8. |
| **13a-e** | `Eje 30` (R=25, R=30) + Comparación DG-2018 | V=30, n=2, calzada=7.2, B2 (`simular: true`) | **OK** | 100 / 15 / 48 ms | Coincidencia exacta con valores de control (`≤ 0.0044 m` de diferencia). Factores `0.86` y `0.83` idénticos. Log final: 1070. |

---

## 2. Paso 3 (Decisivo): Respuesta Completa y Línea de `mcp_log.jsonl`

### Bloques `izquierdo` y `derecho` completos (con `medicion` y `regiones_civil3d`)
```json
"izquierdo": {
  "nombre": "Eje 1 - Borde izq",
  "es_desplazado": true,
  "eje": "Eje 1",
  "desplazamiento_nominal": -3.6,
  "lado_civil3d": "Left",
  "lado": "izquierdo",
  "estilo": "Proposed",
  "capa": "C-ROAD",
  "longitud": 830.3939,
  "progresiva_inicial": 0,
  "progresiva_final": 830.3939,
  "medicion": {
    "lado_medido": "izquierdo",
    "desplazamiento_medido": 3.6,
    "muestreo": {"desde": 0, "hasta": 831.3274, "paso": 1, "muestras": 833, "fallidas": 0},
    "ensanchamientos_medidos": [{
      "inicio_transicion": 179.9994, "inicio_pleno": 219.9996, "fin_pleno": 345.6617, "fin_transicion": 385.6618,
      "longitud_entrada": 40.0002, "longitud_salida": 40.0002, "sobreancho_maximo": 0.6, "sobreancho_medio": 0.6,
      "cortada_al_inicio": false, "cortada_al_final": false,
      "puntos_entrada": [
        {"progresiva": 179.9994, "sobreancho": 0},
        {"progresiva": 189.9995, "sobreancho": 0.15},
        {"progresiva": 199.9995, "sobreancho": 0.3},
        {"progresiva": 209.9995, "sobreancho": 0.45},
        {"progresiva": 219.9996, "sobreancho": 0.6}
      ],
      "puntos_salida": [
        {"progresiva": 345.6617, "sobreancho": 0.6},
        {"progresiva": 355.6617, "sobreancho": 0.45},
        {"progresiva": 365.6617, "sobreancho": 0.3},
        {"progresiva": 375.6618, "sobreancho": 0.15},
        {"progresiva": 385.6618, "sobreancho": 0}
      ]
    }]
  },
  "regiones_civil3d": [
    {"tipo": "Norminal", "progresiva_inicial": 0, "progresiva_final": 180, "longitud": 180, "offset": -3.6, "offset_dist": -3.6, "increased_width": null, "entrada": null, "salida": {"tipo": "Linear", "descripcion": "LinearTransitionDescription", "progresiva_inicial": 180, "progresiva_final": 220, "longitud": 40}},
    {"tipo": "Specific", "progresiva_inicial": 220, "progresiva_final": 345.6637, "longitud": 125.6637, "offset": -4.2, "offset_dist": -4.2, "increased_width": null, "entrada": {"tipo": "Linear", "descripcion": "LinearTransitionDescription", "progresiva_inicial": 180, "progresiva_final": 220, "longitud": 40}, "salida": {"tipo": "Linear", "descripcion": "LinearTransitionDescription", "progresiva_inicial": 345.6637, "progresiva_final": 385.6637, "longitud": 40}},
    {"tipo": "Norminal", "progresiva_inicial": 385.6637, "progresiva_final": 831.3274, "longitud": 445.6637, "offset": -3.6, "offset_dist": -3.6, "increased_width": null, "entrada": {"tipo": "Linear", "descripcion": "LinearTransitionDescription", "progresiva_inicial": 345.6637, "progresiva_final": 385.6637, "longitud": 40}, "salida": null}
  ],
  "ensanchamientos": [{
    "numero": 1, "borde": "izquierdo", "progresiva_inicial": 220, "progresiva_final": 345.6637, "sobreancho": 0.6, "desplazamiento_total": 4.2, "longitud_transicion": 40,
    "rango_add_widening": [180, 345.6637],
    "medido": {"sobreancho_medido": 0.6, "transicion": "fuera", "explicacion_transicion": "Civil 3D dejó el sobreancho pleno en el rango pedido y las transiciones fuera de él: tramo pleno de 220 a 345.66, entrada de 40 m (desde 180) y salida de 40 m (hasta 385.66)."},
    "longitud_transicion_aplicada": true
  }]
},
"derecho": {
  "nombre": "Eje 1 - Borde der",
  "es_desplazado": true,
  "eje": "Eje 1",
  "desplazamiento_nominal": 3.6,
  "lado_civil3d": "Right",
  "lado": "derecho",
  "estilo": "Proposed",
  "capa": "C-ROAD",
  "longitud": 830.3939,
  "progresiva_inicial": 0,
  "progresiva_final": 830.3939,
  "medicion": {
    "lado_medido": "derecho",
    "desplazamiento_medido": 3.6,
    "muestreo": {"desde": 0, "hasta": 831.3274, "paso": 1, "muestras": 833, "fallidas": 0},
    "ensanchamientos_medidos": [{
      "inicio_transicion": 445.6634, "inicio_pleno": 485.6635, "fin_pleno": 611.3274, "fin_transicion": 651.3276,
      "longitud_entrada": 40.0002, "longitud_salida": 40.0002, "sobreancho_maximo": 0.6, "sobreancho_medio": 0.6,
      "cortada_al_inicio": false, "cortada_al_final": false,
      "puntos_entrada": [
        {"progresiva": 445.6634, "sobreancho": 0},
        {"progresiva": 455.6634, "sobreancho": 0.15},
        {"progresiva": 465.6635, "sobreancho": 0.3},
        {"progresiva": 475.6635, "sobreancho": 0.45},
        {"progresiva": 485.6635, "sobreancho": 0.6}
      ],
      "puntos_salida": [
        {"progresiva": 611.3274, "sobreancho": 0.6},
        {"progresiva": 621.3275, "sobreancho": 0.45},
        {"progresiva": 631.3275, "sobreancho": 0.3},
        {"progresiva": 641.3276, "sobreancho": 0.15},
        {"progresiva": 651.3276, "sobreancho": 0}
      ]
    }]
  },
  "regiones_civil3d": [
    {"tipo": "Norminal", "progresiva_inicial": 0, "progresiva_final": 445.6637, "longitud": 445.6637, "offset": 3.6, "offset_dist": 3.6, "increased_width": null, "entrada": null, "salida": {"tipo": "Linear", "descripcion": "LinearTransitionDescription", "progresiva_inicial": 445.6637, "progresiva_final": 485.6637, "longitud": 40}},
    {"tipo": "Specific", "progresiva_inicial": 485.6637, "progresiva_final": 611.3274, "longitud": 125.6637, "offset": 4.2, "offset_dist": 4.2, "increased_width": null, "entrada": {"tipo": "Linear", "descripcion": "LinearTransitionDescription", "progresiva_inicial": 445.6637, "progresiva_final": 485.6637, "longitud": 40}, "salida": {"tipo": "Linear", "descripcion": "LinearTransitionDescription", "progresiva_inicial": 611.3274, "progresiva_final": 651.3274, "longitud": 40}},
    {"tipo": "Norminal", "progresiva_inicial": 651.3274, "progresiva_final": 831.3274, "longitud": 180, "offset": 3.6, "offset_dist": 3.6, "increased_width": null, "entrada": {"tipo": "Linear", "descripcion": "LinearTransitionDescription", "progresiva_inicial": 611.3274, "progresiva_final": 651.3274, "longitud": 40}, "salida": null}
  ],
  "ensanchamientos": [{
    "numero": 2, "borde": "derecho", "progresiva_inicial": 485.6637, "progresiva_final": 611.3274, "sobreancho": 0.6, "desplazamiento_total": 4.2, "longitud_transicion": 40,
    "rango_add_widening": [445.6637, 611.3274],
    "medido": {"sobreancho_medido": 0.6, "transicion": "fuera", "explicacion_transicion": "Civil 3D dejó el sobreancho pleno en el rango pedido y las transiciones fuera de él: tramo pleno de 485.66 a 611.33, entrada de 40 m (desde 445.66) y salida de 40 m (hasta 651.33)."},
    "longitud_transicion_aplicada": true
  }]
}
```

### Línea literal de `mcp_log.jsonl` (UTF-8)
```json
{"tipo":"llamada","id":"20261120-111103-357611","hora":"2026-11-20 11:11:03","herramienta":"crear_bordes_calzada","pedido":"crea los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas","args":{"eje":"Eje 1","desplazamiento":3.6,"ensanchamientos":"[{\"borde\":\"izquierdo\",\"progresiva_inicial\":220,\"progresiva_final\":345.6637,\"sobreancho\":0.6,\"longitud_transicion\":40},{\"borde\":\"derecho\",\"progresiva_inicial\":485.6637,\"progresiva_final\":611.3274,\"sobreancho\":0.6,\"longitud_transicion\":40}]","pedido":"crea los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas"},"ok":true,"ms":179,"error":null,"version":"1.21.4"}
```

---

## 3. Paso 4 (Decisivo): Respuestas de Jose y Preguntas P1 a P4

- **4a (dictado por Jose desde Propiedades de alineación):** `-3.600m`
- **4c (dictado por Jose en planta con zoom):** *"se ve un poco brusca la transición"*

- **P1 (Rango y transición):** El rango pedido (`220` a `345.6637` m) quedó exactamente como **tramo pleno** (`Specific`), con la transición de entrada **fuera** (`180` a `220` m) y la transición de salida **fuera** (`345.6637` a `385.6637` m). `medido.transicion: "fuera"`.
- **P2 (Longitud y tipo de transición):** Mide exactamente **40.00 m** (`longitud_entrada: 40.0002`, `longitud_salida: 40.0002`, `longitud_transicion_aplicada: true`). Es de tipo **lineal** (`Linear` / `LinearTransitionDescription`) y en `puntos_entrada` al 50% (199.9995 m) da `0.3` exacto.
- **P3 (Signo y valores):** `desplazamiento_nominal` del borde izquierdo es negativo (**`-3.600m`**). El desplazamiento del ensanchamiento es negativo y muestra el **total** (**`-4.2`**), no el incremento (`0.6`).
- **P4 (Enumerados reales):**
  - `lado_civil3d` (`EntitySideType`): `"Left"` y `"Right"`.
  - `regiones_civil3d[].tipo` (`AlignmentRegionType`): `"Norminal"` (con el typo nativo de Autodesk) y `"Specific"`.

---

## 4. Comparación a 30 km/h (Paso 13) y Decisión P5

- **Decisión P5:** **SIN DECIDIR.** *"Jose no aplicaba el factor de la T302.20: el XML de Civil 3D no lo tenía."* La herramienta mantiene su comportamiento actual (`a_confirmar: true`).

| Curva | R (m) | L (m) | V (km/h) | n | Término Geométrico | Término Velocidad | Factor T302.20 | Sa sin factor (Herramienta) | Sa sin factor (Control) | Dif. sin factor | Sa con factor (Herramienta) | Sa con factor (Control) | Dif. con factor |
| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **PI 1 (izq)** | 25 | 10.55 | 30 | 2 | 4.6702 | 0.6000 | 0.86 | 5.2702 m | 5.27 m | 0.0002 m | 4.5324 m | 4.53 m | 0.0024 m |
| **PI 2 (der)** | 30 | 10.55 | 30 | 2 | 3.8325 | 0.5477 | 0.83 | 4.3802 m | 4.38 m | 0.0002 m | 3.6356 m | 3.64 m | 0.0044 m |

*(Todas las diferencias son menores de 0.01 m y los factores 0.86 y 0.83 coinciden exactamente).*

---

## 5. Hallazgos y Puntos Dudosos para Revisar

1. **Paso 7d (`asignar_objetivos` con `opcion: "mas_cercano"`):** Cuando se asigna un único objetivo de desplazamiento y se envía `"opcion": "mas_cercano"`, Civil 3D 2027 lanza:
   `Error de Civil 3D: InvalidOperationException: The count of TargetIds should be greater or equal to 2.`
   *(Sugerencia para el desarrollador: ignorar o no establecer `TargetToOption = NearestToOffset` cuando `targetIds.Count < 2`).*
2. **Paso 4c (Visualización de transición en corredor):** Al hacer zoom en planta, Jose observa que la transición del corredor se ve un poco brusca en los quiebres (causado por la frecuencia de muestreo del corredor cada 5 m en tangentes/curvas y transición lineal en planta).
3. **Paso 10 (Deshacer en Civil 3D en español):** Las entradas de `Executefunction` aparecen traducidas en la UI en español como `Grupo de comandos`.

---

## 6. Recuentos de Archivos (`mcp_log.jsonl` y `backups\`)

- **Paso 2:** Log N2 = 1040 → 1041 | Backups C2 = 31 → 31.
- **Paso 3:** Log N3 = 1041 → 1042 | Backups C3 = 31 → 32 (`PRUEBA_121_20261120_111103_crear_bordes_calzada.dwg`).
- **Paso 5:** Log N5 = 1042 → 1045 | Backups = 32.
- **Paso 6:** Log N6 = 1045 → 1048 | Backups = 32.
- **Paso 11:** Log N11 = 1062 → 1062 (no llamó a Civil 3D) | Backups = 32.
- **Cierre (post-Paso 13d):** Log final = 1070 líneas | Backups finales = 32 archivos.
