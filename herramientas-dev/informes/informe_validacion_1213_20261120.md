# Informe de Validación v1.21.3 (`crear_bordes_calzada` en Civil 3D 2027)

- **Fecha:** 2026-11-20
- **Versión de Civil 3D:** Autodesk Civil 3D 2027 (Español)
- **Rama probada:** `tarea/ronda-d` (commit `46a6647`)
- **Versión plugin / ping:** `1.21.3` (63 herramientas: 59 plugin + 4 puente)
- **DWG de prueba:** `PRUEBA_121.dwg` (copia nueva y limpia de `PRUEBA_EVALUACION_BASE.dwg`)
- **Memoria de firmas (`memoria-firma`):** commit `202dc97` (`MEMORIA_FIRMA_DIR = C:\Proyectos\memoria-firma`)

---

## Parte A. Pruebas del Fallo del 20/11 (Copia Literal)

### A.1. Líneas de `crear_bordes_calzada` en `mcp_log.jsonl` del 20/11
```json
{"tipo":"llamada","id":"20261120-005041-67580a","hora":"2026-11-20 00:50:41","herramienta":"crear_bordes_calzada","pedido":"simula los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas","args":{"eje":"Eje 1","desplazamiento":3.6,"ensanchamientos":"[{\"borde\":\"izquierdo\",\"progresiva_inicial\":220,\"progresiva_final\":345.6637,\"sobreancho\":0.6,\"longitud_transicion\":40},{\"borde\":\"derecho\",\"progresiva_inicial\":485.6637,\"progresiva_final\":611.3274,\"sobreancho\":0.6,\"longitud_transicion\":40}]","simular":true,"pedido":"simula los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas"},"ok":true,"ms":10,"error":null,"version":"1.21.2"}
{"tipo":"llamada","id":"20261120-005056-859330","hora":"2026-11-20 00:50:57","herramienta":"crear_bordes_calzada","pedido":"crea los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas","args":{"eje":"Eje 1","desplazamiento":3.6,"ensanchamientos":"[{\"borde\":\"izquierdo\",\"progresiva_inicial\":220,\"progresiva_final\":345.6637,\"sobreancho\":0.6,\"longitud_transicion\":40},{\"borde\":\"derecho\",\"progresiva_inicial\":485.6637,\"progresiva_final\":611.3274,\"sobreancho\":0.6,\"longitud_transicion\":40}]","simular":false,"pedido":"crea los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas"},"ok":false,"ms":28,"error":"The offsetDistance value must greater than 0.","version":"1.21.2"}
```

### A.2. Historial de Civil 3D
NO HECHO: Civil 3D se cerró y el historial se perdió.

### A.3. Argumentos Exactos de la Llamada del Paso 3
Los argumentos con los que se llamó a `crear_bordes_calzada` en el paso 3 de la prueba del 20/11 fueron **exactamente** los del prompt:
```json
{
  "eje": "Eje 1",
  "desplazamiento": 3.6,
  "ensanchamientos": "[{\"borde\":\"izquierdo\",\"progresiva_inicial\":220,\"progresiva_final\":345.6637,\"sobreancho\":0.6,\"longitud_transicion\":40},{\"borde\":\"derecho\",\"progresiva_inicial\":485.6637,\"progresiva_final\":611.3274,\"sobreancho\":0.6,\"longitud_transicion\":40}]",
  "simular": false,
  "pedido": "crea los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas"
}
```

---

## Parte B. Instalación y Verificación de la Versión 1.21.3

- **Rama:** `tarea/ronda-d`
- **Commit:** `46a6647` (*"Bordes 1.21.3: AddWidening recibe la distancia sin signo y cada llamada a Civil 3D tiene su error"*)
- **Compilación C# (.NET 10 Release):** 0 Errores, 2 Advertencias (obsoletos de API). Desplegado en `%APPDATA%\Autodesk\ApplicationPlugins\ArbaMcp.bundle`.
- **Pruebas Unitarias (`dotnet test`):** 719 superadas, 0 errores, 0 omitidas.
- **Pruebas Python (`pytest`):** 377 superadas en 126.13 s.
- **Verificación `ping` en Civil 3D 2027:**
  ```json
  {"plugin": "ArbaMcp", "version": "1.21.3", "puerto": 8765, "dibujo": "C:\\Proyectos\\dibujos_prueba\\PRUEBA_121.dwg", "hay_dibujo": true}
  ```

---

## Parte C. Ejecución Paso a Paso de `VALIDACION_121.md` (v1.21.3)

| Paso | Herramienta | Parámetros Clave | Resultado | Tiempo | Observaciones / Respuestas Literales |
| :---: | :--- | :--- | :---: | :---: | :--- |
| **0** | `ping`<br>`leer_variable` | `INSUNITS` | **OK** | 17 ms<br>34 ms | `version: "1.21.3"`, `hay_dibujo: true`, `INSUNITS: "6"` (metros). 63 herramientas verificadas. |
| **1a** | `ejecutar_comando` | `_.-LAYER _M EJE_PRUEBA`<br>`_.PLINE` Eje 1 | **OK** | 432 ms<br>115 ms | `result: "terminado"` en ambos. |
| **1b** | `ejecutar_comando` | `_.-LAYER _M EJE_DESHACER`<br>`_.PLINE` Eje Deshacer | **OK** | 217 ms<br>117 ms | `result: "terminado"` en ambos. |
| **1c** | `crear_superficie_tin` | `Terreno` desde `terreno_corredor.csv` | **OK** | 825 ms | Superficie TIN creada con 9 puntos y 8 triángulos. Backup generado: `PRUEBA_121_20261120_104706_crear_superficie_tin.dwg`. |
| **1d** | `crear_alineamiento_desde_polilinea` | `Eje 1`, R=80 | **OK** | 224 ms | Eje creado: longitud 831.3274 m, 2 curvas (PI 1 izq, PI 2 der). |
| **1e** | `crear_alineamiento_desde_polilinea` | `Eje Deshacer`, R=80 | **OK** | 82 ms | Eje creado: longitud 831.3274 m. |
| **1f** | `crear_rasante` | `Rasante 1`, 3 PVI | **OK** | 272 ms | Rasante creada: 0 a 831.3274 m, 1 curva vertical parabólica. |
| **1g** | `listar_ensamblajes` | Consulta de ensamblajes | **OK** | 95 ms | `<ensamblaje>` = `Ensamblaje 1 (3)` con subensamblajes `LaneSuperelevationAOR` y `DaylightBench` a ambos lados. |
| **1h** | `guardar_dibujo` | Guardar DWG de prueba | **OK** | 1573 ms | Dibujo guardado en `PRUEBA_121.dwg`. |
| **2** | `crear_bordes_calzada` | `simular: true`, despl=3.6, ENS | **OK** | 56 ms | `simulado: true`. N2=1024 → 1025 líneas log; C2=29 backups (sin cambios). |
| **3** | `crear_bordes_calzada` | `simular: false`, despl=3.6, ENS (**Decisivo**) | **FALLO (Validación interna de sobreancho medido)** | 174 ms | **Civil 3D creó exitosamente los bordes sin excepción de API.** Sin embargo, el análisis posterior de medición del plugin rechazó el resultado porque el sobreancho medido dio 1.0518 m en vez de 0.6 m. Detenido en paso 3 según protocolo. |
| **4** | Inspección Parámetros Desplazamiento (**Decisivo**) | Preguntas P1 a P4 | **OK** | - | Respondidas en detalle técnico a partir de los objetos reales creados en Civil 3D. |
| **5 a 13** | - | Pasos siguientes | **NO HECHO** | - | Se detuvo en el paso 3 conforme a la instrucción de `VALIDACION_121.md`. |

---

## Detalle Técnico del Paso 3 (Decisivo)

### Respuesta Completa del Plugin
```json
{
  "ok": false,
  "error": "Error de Civil 3D: InvalidOperationException: El ensanchamiento 1 del borde 'Eje 1 - Borde izq' (220–345.6637) quedó con un sobreancho medido de 1.0518 m y se pidió 0.6 m (desplazamiento total pasado a AddWidening: 4.2 m). El ensanchamiento 2 del borde 'Eje 1 - Borde der' (485.6637–611.3274) quedó con un sobreancho medido de 1.0522 m y se pidió 0.6 m (desplazamiento total pasado a AddWidening: 4.2 m). No se reintenta; deshaz con Ctrl+Z (_.UNDO 1) si no quieres los bordes. despues={\"eje\":\"Eje 1\",\"existe_izquierdo\":true,\"existe_derecho\":true,\"bordes_existentes\":[],\"izquierdo\":{\"nombre\":\"Eje 1 - Borde izq\",\"es_desplazado\":true,\"eje\":\"Eje 1\",\"desplazamiento_nominal\":-3.6,\"lado_civil3d\":\"Left\",\"lado\":\"izquierdo\",\"estilo\":\"Proposed\",\"capa\":\"C-ROAD\",\"longitud\":830.4451,\"progresiva_inicial\":0,\"progresiva_final\":830.4451,\"medicion\":{\"lado_medido\":\"izquierdo\",\"desplazamiento_medido\":3.6,\"muestreo\":{\"desde\":0,\"hasta\":831.3274,\"paso\":1,\"muestras\":833,\"fallidas\":1},\"ensanchamientos_medidos\":[{\"inicio_transicion\":217.5314,\"inicio_pleno\":245.1979,\"fin_pleno\":245.1979,\"fin_transicion\":394.0451,\"longitud_entrada\":27.6665,\"longitud_salida\":148.8472,\"sobreancho_maximo\":1.0518,\"sobreancho_medio\":1.0518,\"cortada_al_inicio\":false,\"cortada_al_final\":false,\"puntos_entrada\":[{\"progresiva\":217.5314,\"sobreancho\":0},{\"progresiva\":224.448,\"sobreancho\":0.48},{\"progresiva\":231.3646,\"sobreancho\":0.7544},{\"progresiva\":238.2813,\"sobreancho\":0.4616},{\"progresiva\":245.1979,\"sobreancho\":0.8355}],\"puntos_salida\":[{\"progresiva\":245.1979,\"sobreancho\":0.8355},{\"progresiva\":282.4097,\"sobreancho\":0.6},{\"progresiva\":319.6215,\"sobreancho\":0.6},{\"progresiva\":356.8333,\"sobreancho\":0.4325},{\"progresiva\":394.0451,\"sobreancho\":0}]}]},\"regiones_civil3d\":[{\"tipo\":\"Norminal\",\"progresiva_inicial\":0,\"progresiva_final\":220,\"longitud\":220,\"offset\":-3.6,\"offset_dist\":-3.6,\"increased_width\":null,\"entrada\":null,\"salida\":{\"tipo\":\"Linear\",\"descripcion\":\"LinearTransitionDescription\",\"progresiva_inicial\":220,\"progresiva_final\":260,\"longitud\":40}},{\"tipo\":\"Specific\",\"progresiva_inicial\":260,\"progresiva_final\":345.6637,\"longitud\":85.6637,\"offset\":-4.2,\"offset_dist\":-4.2,\"increased_width\":null,\"entrada\":{\"tipo\":\"Linear\",\"descripcion\":\"LinearTransitionDescription\",\"progresiva_inicial\":220,\"progresiva_final\":260,\"longitud\":40},\"salida\":{\"tipo\":\"Linear\",\"descripcion\":\"LinearTransitionDescription\",\"progresiva_inicial\":345.6637,\"progresiva_final\":385.6637,\"longitud\":40}},{\"tipo\":\"Norminal\",\"progresiva_inicial\":385.6637,\"progresiva_final\":831.3274,\"longitud\":445.6637,\"offset\":-3.6,\"offset_dist\":-3.6,\"increased_width\":null,\"entrada\":{\"tipo\":\"Linear\",\"descripcion\":\"LinearTransitionDescription\",\"progresiva_inicial\":345.6637,\"progresiva_final\":385.6637,\"longitud\":40},\"salida\":null}],\"ensanchamientos\":[{\"numero\":1,\"borde\":\"izquierdo\",\"progresiva_inicial\":220,\"progresiva_final\":345.6637,\"sobreancho\":0.6,\"desplazamiento_total\":4.2,\"longitud_transicion\":40,\"transicion_prevista\":{\"entrada\":[180,220],\"salida\":[345.6637,385.6637],\"supuesto\":\"Se supone que el rango pedido es el tramo pleno y que la transición queda fuera de él; al crear se mide y se avisa si Civil 3D la puso de otra forma.\"},\"medido\":{\"sobreancho_medido\":1.0518,\"transicion\":null,\"explicacion_transicion\":null,\"zona_medida\":null},\"longitud_transicion_aplicada\":true}]},\"derecho\":{\"nombre\":\"Eje 1 - Borde der\",\"es_desplazado\":true,\"eje\":\"Eje 1\",\"desplazamiento_nominal\":3.6,\"lado_civil3d\":\"Right\",\"lado\":\"derecho\",\"estilo\":\"Proposed\",\"capa\":\"C-ROAD\",\"longitud\":830.4451,\"progresiva_inicial\":0,\"progresiva_final\":830.4451,\"medicion\":{\"lado_medido\":\"derecho\",\"desplazamiento_medido\":3.6,\"muestreo\":{\"desde\":0,\"hasta\":831.3274,\"paso\":1,\"muestras\":833,\"fallidas\":0},\"ensanchamientos_medidos\":[{\"inicio_transicion\":483.1936,\"inicio_pleno\":510.8609,\"fin_pleno\":510.8609,\"fin_transicion\":659.7103,\"longitud_entrada\":27.6673,\"longitud_salida\":148.8494,\"sobreancho_maximo\":1.0522,\"sobreancho_medio\":1.0522,\"cortada_al_inicio\":false,\"cortada_al_final\":false,\"puntos_entrada\":[{\"progresiva\":483.1936,\"sobreancho\":0},{\"progresiva\":490.1104,\"sobreancho\":0.4799},{\"progresiva\":497.0273,\"sobreancho\":0.7544},{\"progresiva\":503.9441,\"sobreancho\":0.4616},{\"progresiva\":510.8609,\"sobreancho\":0.8355}],\"puntos_salida\":[{\"progresiva\":510.8609,\"sobreancho\":0.8355},{\"progresiva\":548.0732,\"sobreancho\":0.6},{\"progresiva\":585.2856,\"sobreancho\":0.6},{\"progresiva\":622.4979,\"sobreancho\":0.4324},{\"progresiva\":659.7103,\"sobreancho\":0}]}]},\"regiones_civil3d\":[{\"tipo\":\"Norminal\",\"progresiva_inicial\":0,\"progresiva_final\":485.6637,\"longitud\":485.6637,\"offset\":3.6,\"offset_dist\":3.6,\"increased_width\":null,\"entrada\":null,\"salida\":{\"tipo\":\"Linear\",\"descripcion\":\"LinearTransitionDescription\",\"progresiva_inicial\":485.6637,\"progresiva_final\":525.6637,\"longitud\":40}},{\"tipo\":\"Specific\",\"progresiva_inicial\":525.6637,\"progresiva_final\":611.3274,\"longitud\":85.6637,\"offset\":4.2,\"offset_dist\":4.2,\"increased_width\":null,\"entrada\":{\"tipo\":\"Linear\",\"descripcion\":\"LinearTransitionDescription\",\"progresiva_inicial\":485.6637,\"progresiva_final\":525.6637,\"longitud\":40},\"salida\":{\"tipo\":\"Linear\",\"descripcion\":\"LinearTransitionDescription\",\"progresiva_inicial\":611.3274,\"progresiva_final\":651.3274,\"longitud\":40}},{\"tipo\":\"Norminal\",\"progresiva_inicial\":651.3274,\"progresiva_final\":831.3274,\"longitud\":180,\"offset\":3.6,\"offset_dist\":3.6,\"increased_width\":null,\"entrada\":{\"tipo\":\"Linear\",\"descripcion\":\"LinearTransitionDescription\",\"progresiva_inicial\":611.3274,\"progresiva_final\":651.3274,\"longitud\":40},\"salida\":null}],\"ensanchamientos\":[{\"numero\":2,\"borde\":\"derecho\",\"progresiva_inicial\":485.6637,\"progresiva_final\":611.3274,\"sobreancho\":0.6,\"desplazamiento_total\":4.2,\"longitud_transicion\":40,\"transicion_prevista\":{\"entrada\":[445.6637,485.6637],\"salida\":[611.3274,651.3274],\"supuesto\":\"Se supone que el rango pedido es el tramo pleno y que la transición queda fuera de él; al crear se mide y se avisa si Civil 3D la puso de otra forma.\"},\"medido\":{\"sobreancho_medido\":1.0522,\"transicion\":null,\"explicacion_transicion\":null,\"zona_medida\":null},\"longitud_transicion_aplicada\":true}]}}",
  "ms_puente": 174,
  "id_log": "20261120-104804-d8e40c"
}
```

### Línea Literal de `mcp_log.jsonl`
La línea se leyó sin `-Encoding UTF8`, por eso los acentos salen rotos (`quedÃ³`); el campo `error` repite el texto de la respuesta de arriba, con `"version":"1.21.3"` e `"id":"20261120-104804-d8e40c"`, `"hora":"2026-11-20 10:48:04"`, `"ok":false`, `"ms":102`. Se omite aquí la repetición del bloque `despues` (es el mismo de la respuesta completa).

```json
{"tipo":"llamada","id":"20261120-104804-d8e40c","hora":"2026-11-20 10:48:04","herramienta":"crear_bordes_calzada","pedido":"crea los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas","args":{"eje":"Eje 1","desplazamiento":3.6,"ensanchamientos":"[{\"borde\":\"izquierdo\",\"progresiva_inicial\":220,\"progresiva_final\":345.6637,\"sobreancho\":0.6,\"longitud_transicion\":40},{\"borde\":\"derecho\",\"progresiva_inicial\":485.6637,\"progresiva_final\":611.3274,\"sobreancho\":0.6,\"longitud_transicion\":40}]","pedido":"crea los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas"},"ok":false,"ms":102,"error":"El ensanchamiento 1 del borde 'Eje 1 - Borde izq' (220â€“345.6637) quedÃ³ con un sobreancho medido de 1.0518 m y se pidiÃ³ 0.6 m (desplazamiento total pasado a AddWidening: 4.2 m). El ensanchamiento 2 del borde 'Eje 1 - Borde der' (485.6637â€“611.3274) quedÃ³ con un sobreancho medido de 1.0522 m y se pidiÃ³ 0.6 m (desplazamiento total pasado a AddWidening: 4.2 m). No se reintenta; deshaz con Ctrl+Z (_.UNDO 1) si no quieres los bordes. despues={...}","version":"1.21.3"}
```

### Verificación de Existencia en Civil 3D
Llamada a `listar_alineamientos`:
```json
[
  {"nombre": "Eje 1", "inicio": 0, "fin": 831.3274, "longitud": 831.3274, "perfiles": ["Rasante 1"]},
  {"nombre": "Eje Deshacer", "inicio": 0, "fin": 831.3274, "longitud": 831.3274, "perfiles": []},
  {"nombre": "Eje 1 - Borde izq", "inicio": 0, "fin": 830.4451, "longitud": 830.4451, "perfiles": []},
  {"nombre": "Eje 1 - Borde der", "inicio": 0, "fin": 830.4451, "longitud": 830.4451, "perfiles": []}
]
```
Ambos bordes **sí quedaron creados en el dibujo de Civil 3D**.

---

## Parte D. Respuestas a las Preguntas Decisivas (P1 a P4)

A partir de la inspección de los objetos reales en la base de datos de Civil 3D (`OffsetAlignmentInfo`, `AlignmentRegionCollection`):

### P1. Tramo Pleno vs. Posición de Transición
- Al llamar a `OffsetAlignmentInfo.AddWidening(startStation: 220, endStation: 345.6637, offset: 4.2)` con longitud de transición `40`:
  - **Transición de entrada:** Civil 3D la colocó de **220 a 260** (longitud 40 m). Por tanto, la transición de entrada quedó **DENTRO** del rango inicial pedido.
  - **Tramo pleno (`Specific`):** Quedó restringido de **260 a 345.6637** (longitud 85.6637 m).
  - **Transición de salida:** Civil 3D la colocó de **345.6637 a 385.6637** (longitud 40 m), es decir, **FUERA** del rango final pedido.
- **Conclusión:** Civil 3D toma `startStation` como el inicio de la transición de entrada y `endStation` como el final del tramo pleno (inicio de la transición de salida). Para lograr que el tramo pleno comience en 220 m con 40 m de transición, se debe pasar a `AddWidening` la progresiva `startStation = 180`.

### P2. Longitud y Tipo de Transición
- **Longitud:** Exactamente **40.00 m** tanto en entrada como en salida (`longitud: 40`).
- **Tipo:** **`Linear`** (`LinearTransitionDescription`).

### P3. Signos y Valores de Desplazamiento
- **Desplazamiento nominal borde izquierdo:** Civil 3D muestra y guarda signo negativo: **`-3.600`** (`offset: -3.6`).
- **Desplazamiento de la región ensanchada (`Specific`):** Civil 3D muestra y guarda signo negativo y corresponde al **desplazamiento total**: **`-4.200`** (`offset: -4.2`), no al incremento (no muestra 0.600).

### P4. Nombres de Enumerados en la API Real
- **`lado_civil3d` (`EntitySideType`):** `"Left"` para el borde izquierdo y `"Right"` para el borde derecho.
- **`regiones_civil3d[].tipo` (`AlignmentRegionType`):**
  - **`Norminal`** (con la errata tipográfica literal propia de Autodesk en la API .NET) para las regiones estándar nominales.
  - **`Specific`** para las regiones de ensanchamiento con sobreancho.
