# Informe de Validación v1.21.5 (`asignar_objetivos` con Un Solo Objetivo)

- **Fecha:** 2026-11-20
- **Versión de Civil 3D:** Autodesk Civil 3D 2027 (Español Métrico)
- **Rama instalada:** `tarea/ronda-d` (commit `0afd25d`)
- **Versión plugin / ping:** `1.21.5`
- **DWG de prueba:** `PRUEBA_1215.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)
- **Pruebas previas:** `dotnet test` (721 superadas, 0 errores) y `pytest` (377 superadas, 0 errores).

---

## Parte A y B. Instalación y Preparación del Dibujo

| Paso | Herramienta | Parámetros / Resumen | Resultado | Tiempo (`ms_puente`) |
| :---: | :--- | :--- | :---: | :---: |
| **A** | `ping` | Comprobación inicial `1.21.5` en `PRUEBA_1215.dwg` | **OK** | 12 ms |
| **1a** | `ejecutar_comando` | Capa `EJE_PRUEBA` y polilínea de Eje 1 | **OK** | 172 ms / 443 ms |
| **1c** | `crear_superficie_tin` | `Terreno` desde `terreno_corredor.csv` | **OK** | 556 ms |
| **1d** | `crear_alineamiento_desde_polilinea` | `Eje 1`, R=80 (L=831.3274 m) | **OK** | 312 ms |
| **1f** | `crear_rasante` | `Rasante 1` (3 PVI) | **OK** | 1973 ms |
| **1g** | `listar_ensamblajes` | `Ensamblaje 1 (3)` (`LaneSuperelevationAOR` + `DaylightBench`) | **OK** | 71 ms |
| **3** | `crear_bordes_calzada` | `Eje 1 - Borde izq` y `Eje 1 - Borde der` (despl=3.6, ENS) | **OK** | - |
| **7a-c** | `crear_corredor`, `listar_regiones`, `listar_objetivos` | `Corredor 1` con `Línea base 1`, `Región 1` (índice 0) | **OK** | 506 / 15 / 13 ms |

- **Nombres literales identificados en 7b-7c:**
  - **Línea base:** `"Línea base 1"`
  - **Carril izquierdo:** `subensamblaje: "LaneSuperelevationAOR"`, `grupo: "Izquierda"`, `parametro: "Width"`
  - **Carril derecho:** `subensamblaje: "LaneSuperelevationAOR"`, `grupo: "Derecha"`, `parametro: "Width"`

---

## Parte C. Resultados de la Prueba (Puntos 1 a 6)

### Punto 1: `asignar_objetivos` SIMULADO (con `"opcion": "mas_cercano"`) — **OK** (`ms_puente: 115`)
- **Respuesta completa:**
```json
{
  "simulado": true,
  "herramienta": "asignar_objetivos",
  "accion": "Se aplicarían 2 asignaciones; 0 fallidas (de 2)",
  "plan": [
    {
      "indice": 0,
      "accion": "Asignar 'Eje 1 - Borde izq' como objetivo de desplazamiento del subensamblaje 'LaneSuperelevationAOR' en la región '0' de 'Línea base 1'",
      "cambios": ["objetivos"],
      "antes": {"objetivos": []},
      "despues": {"objetivos": ["eje:Eje 1 - Borde izq"]}
    },
    {
      "indice": 1,
      "accion": "Asignar 'Eje 1 - Borde der' como objetivo de desplazamiento del subensamblaje 'LaneSuperelevationAOR' en la región '0' de 'Línea base 1'",
      "cambios": ["objetivos"],
      "antes": {"objetivos": []},
      "despues": {"objetivos": ["eje:Eje 1 - Borde der"]}
    }
  ],
  "cambios": ["0.objetivos", "1.objetivos"],
  "antes": [{"objetivos": []}, {"objetivos": []}],
  "despues": [{"objetivos": ["eje:Eje 1 - Borde izq"]}, {"objetivos": ["eje:Eje 1 - Borde der"]}],
  "fallidos": [],
  "datos": {"total": 2, "aplicadas": 2, "fallidas": 0},
  "avisos": [
    "asignaciones[0]: La opción 'mas_cercano' no se aplica en 'LaneSuperelevationAOR' (región '0'): hay 1 objetivo y Civil 3D exige al menos 2 para elegir entre ellos («The count of TargetIds should be greater or equal to 2»). El objetivo se asigna sin opción.",
    "asignaciones[1]: La opción 'mas_cercano' no se aplica en 'LaneSuperelevationAOR' (región '0'): hay 1 objetivo y Civil 3D exige al menos 2 para elegir entre ellos («The count of TargetIds should be greater or equal to 2»). El objetivo se asigna sin opción."
  ],
  "ms": 98,
  "ms_espera": 0,
  "ms_ejecucion": 96,
  "id_log": "20261120-114920-2de16b",
  "ms_puente": 115
}
```

### Punto 2 (DECISIVO): `asignar_objetivos` REAL (con `"opcion": "mas_cercano"`) — **OK** (`ms_puente: 60`)
- **Respuesta completa:**
```json
{
  "simulado": false,
  "herramienta": "asignar_objetivos",
  "mensaje": "2 asignaciones aplicadas, 0 fallidas (de 2)",
  "cambios": ["0.objetivos", "1.objetivos"],
  "antes": [{"objetivos": []}, {"objetivos": []}],
  "despues": [{"objetivos": ["eje:Eje 1 - Borde izq"]}, {"objetivos": ["eje:Eje 1 - Borde der"]}],
  "copia": {"ruta": "C:\\Proyectos\\dibujos_prueba\\backups\\PRUEBA_1215_20261120_114835_crear_superficie_tin.dwg", "metodo": "copia_de_disco", "reutilizada": true, "ms": 0, "espera_ms": 0, "refleja_guardado_de": "2026-11-14 11:46:22", "bytes": 1120488, "estado": "terminada", "nota": "La copia refleja el último guardado en disco, no el estado en memoria."},
  "datos": {"total": 2, "aplicadas": 2, "fallidas": 0},
  "fallidos": [],
  "avisos": [
    "asignaciones[0]: La opción 'mas_cercano' no se aplica en 'LaneSuperelevationAOR' (región '0'): hay 1 objetivo y Civil 3D exige al menos 2 para elegir entre ellos («The count of TargetIds should be greater or equal to 2»). El objetivo se asigna sin opción.",
    "asignaciones[1]: La opción 'mas_cercano' no se aplica en 'LaneSuperelevationAOR' (región '0'): hay 1 objetivo y Civil 3D exige al menos 2 para elegir entre ellos («The count of TargetIds should be greater or equal to 2»). El objetivo se asigna sin opción."
  ],
  "restaurar": {
    "nota": "Civil 3D no revierte los objetivos con _.UNDO ni con Ctrl+Z (validado el 28/09/2026): para volver atrás usa deshacer_objetivos, que reaplica lo que había antes. La pila vive en memoria hasta cerrar Civil 3D; 'antes' sirve para reasignar a mano con asignar_objetivos si hiciera falta.",
    "herramienta": "deshacer_objetivos",
    "args": {"id": 2},
    "antes": [
      {"linea_base": "Línea base 1", "region": "Región 1", "subensamblaje": "LaneSuperelevationAOR", "grupo": "Izquierda", "parametro": "Width", "tipo": "desplazamiento", "objetivos": [], "opcion": null, "mismo_lado": null},
      {"linea_base": "Línea base 1", "region": "Región 1", "subensamblaje": "LaneSuperelevationAOR", "grupo": "Derecha", "parametro": "Width", "tipo": "desplazamiento", "objetivos": [], "opcion": null, "mismo_lado": null}
    ]
  },
  "ms": 51,
  "ms_espera": 28,
  "ms_ejecucion": 22,
  "id_log": "20261120-114931-c0567b",
  "ms_puente": 60
}
```
- **Línea literal de `mcp_log.jsonl`:**
```json
{"tipo":"llamada","id":"20261120-114931-c0567b","hora":"2026-11-20 11:49:31","herramienta":"asignar_objetivos","pedido":"asigna los bordes a los carriles del Corredor 1","args":{"corredor":"Corredor 1","linea_base":"Línea base 1","asignaciones":"[{\"region\":\"0\",\"subensamblaje\":\"LaneSuperelevationAOR\",\"grupo\":\"Izquierda\",\"tipo\":\"desplazamiento\",\"objetivo\":\"Eje 1 - Borde izq\",\"opcion\":\"mas_cercano\"},{\"region\":\"0\",\"subensamblaje\":\"LaneSuperelevationAOR\",\"grupo\":\"Derecha\",\"tipo\":\"desplazamiento\",\"objetivo\":\"Eje 1 - Borde der\",\"opcion\":\"mas_cercano\"}]","pedido":"asigna los bordes a los carriles del Corredor 1"},"ok":true,"ms":21,"error":null,"version":"1.21.5"}
```
- **Menú Deshacer:** Registra `Executefunction` (una entrada por escritura en el menú Deshacer de Civil 3D).

### Punto 3: `listar_objetivos` — **OK** (`ms_puente: 18`)
- Carril derecho (`LaneSuperelevationAOR`, `Derecha`, `Width`): `[{"tipo": "eje", "nombre": "Eje 1 - Borde der", "handle": "7482"}]`.
- Carril izquierdo (`LaneSuperelevationAOR`, `Izquierda`, `Width`): `[{"tipo": "eje", "nombre": "Eje 1 - Borde izq", "handle": "7476"}]`.

### Punto 4 (DECISIVO): `deshacer_objetivos` (`id: "2"`) — **OK** (`ms_puente: 23` simulado / `68` real)
- **Simulación (`simular: true`):**
```json
{"simulado": true, "herramienta": "deshacer_objetivos", "accion": "Se aplicarían 2 objetivos; 0 fallidas (de 2)", "plan": [{"indice": 0, "accion": "Devolver el objetivo de LaneSuperelevationAOR [desplazamiento, Width] en la región 'Región 1' de 'Línea base 1' a ninguno", "cambios": ["objetivos"], "antes": {"objetivos": ["eje:Eje 1 - Borde izq"]}, "despues": {"objetivos": []}}, {"indice": 1, "accion": "Devolver el objetivo de LaneSuperelevationAOR [desplazamiento, Width] en la región 'Región 1' de 'Línea base 1' a ninguno", "cambios": ["objetivos"], "antes": {"objetivos": ["eje:Eje 1 - Borde der"]}, "despues": {"objetivos": []}}], "cambios": ["0.objetivos", "1.objetivos"], "antes": [{"objetivos": ["eje:Eje 1 - Borde izq"]}, {"objetivos": ["eje:Eje 1 - Borde der"]}], "despues": [{"objetivos": []}, {"objetivos": []}], "fallidos": [], "datos": {"total": 2, "aplicadas": 2, "fallidas": 0}, "avisos": null, "ms": 10, "id_log": "20261120-115000-53475c", "ms_puente": 23}
```
- **Ejecución real (`simular: false`):**
```json
{"simulado": false, "herramienta": "deshacer_objetivos", "mensaje": "2 objetivos aplicadas, 0 fallidas (de 2)", "cambios": ["0.objetivos", "1.objetivos"], "antes": [{"objetivos": ["eje:Eje 1 - Borde izq"]}, {"objetivos": ["eje:Eje 1 - Borde der"]}], "despues": [{"objetivos": []}, {"objetivos": []}], "datos": {"total": 2, "aplicadas": 2, "fallidas": 0}, "fallidos": [], "avisos": null, "ms": 58, "id_log": "20261120-115010-b17ec5", "ms_puente": 68}
```
- **Línea literal de `mcp_log.jsonl`:**
```json
{"tipo":"llamada","id":"20261120-115010-b17ec5","hora":"2026-11-20 11:50:10","herramienta":"deshacer_objetivos","pedido":"devuelve los objetivos de los carriles","args":{"id":2.0,"pedido":"devuelve los objetivos de los carriles"},"ok":true,"ms":26,"error":null,"version":"1.21.5"}
```

### Punto 5: `listar_objetivos` post-deshacer — **OK** (`ms_puente: 15`)
- Ambos carriles volvieron a `objetivos: []` y `opcion_objetivo: null`.

### Punto 6 (DECISIVO): Reasignación + Reconstrucción + Estado + Visor de Sucesos — **OK** (`ms_puente: 49 / 604 / 10`)
- **`asignar_objetivos` real:** `2 asignaciones aplicadas, 0 fallidas (de 2)` (`id_log: "20261120-115040-867b28"`, `restaurar.args.id: 3`).
- **`reconstruir_corredor`:** `esta_desactualizado: false` (`ms: 585`).
- **`estado_corredor`:** `esta_desactualizado: false` (`ms: 3`).
- **Visor de sucesos (captura de Jose):** **"Visor de sucesos: 0 suceso(s)"** — limpio, sin errores de «Width not found» ni «Offset target not found».
- **Líneas literales de `mcp_log.jsonl`:**
```json
{"tipo":"llamada","id":"20261120-115040-867b28","hora":"2026-11-20 11:50:40","herramienta":"asignar_objetivos","pedido":"asigna los bordes a los carriles del Corredor 1 (punto 6)","args":{"corredor":"Corredor 1","linea_base":"Línea base 1","asignaciones":"[{\"region\":\"0\",\"subensamblaje\":\"LaneSuperelevationAOR\",\"grupo\":\"Izquierda\",\"tipo\":\"desplazamiento\",\"objetivo\":\"Eje 1 - Borde izq\",\"opcion\":\"mas_cercano\"},{\"region\":\"0\",\"subensamblaje\":\"LaneSuperelevationAOR\",\"grupo\":\"Derecha\",\"tipo\":\"desplazamiento\",\"objetivo\":\"Eje 1 - Borde der\",\"opcion\":\"mas_cercano\"}]","pedido":"asigna los bordes a los carriles del Corredor 1 (punto 6)"},"ok":true,"ms":11,"error":null,"version":"1.21.5"}
{"tipo":"llamada","id":"20261120-115050-a6167f","hora":"2026-11-20 11:50:50","herramienta":"reconstruir_corredor","pedido":null,"args":{"corredor":"Corredor 1"},"ok":true,"ms":548,"error":null,"version":"1.21.5"}
{"tipo":"llamada","id":"20261120-115052-684732","hora":"2026-11-20 11:50:52","herramienta":"estado_corredor","pedido":null,"args":{"corredor":"Corredor 1"},"ok":true,"ms":1,"error":null,"version":"1.21.5"}
```
