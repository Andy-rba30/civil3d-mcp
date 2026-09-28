# Informe de Validación ArbaMcp 1.3.3 en Autodesk Civil 3D 2027

- **Fecha:** 28 de septiembre de 2026
- **Entorno:** Autodesk Civil 3D 2027 (Español Métrico) sobre Windows 11
- **Versión de ArbaMcp:** 1.3.3 (`FileVersion`: `1.3.3.0` en `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`)
- **Versión de PuenteMcp:** 1.3.3 (`Puente MCP Civil 3D 1.3.3`)
- **Dibujo de prueba:** `D:\CURSO CIVIL 3D\Tarea 3\PRUEBA_AISLAMIENTO_validacion130.dwg` (70.9 MB)
- **Token:** `<oculto>`
- **Rama:** `claude/vibrant-ptolemy-79gan9` (commit `a367053`)

---

## Nota Preliminar: Fallo de Compilación en Primer Intento y Corrección

En el primer intento de validación del paso 0 sobre el commit `edbff11`, la compilación con `instalar.ps1` falló con 2 errores `CS0234` en `Herramientas.cs` (línea 99) y `Herramientas.Corredores.cs` (línea 1301):
`El tipo o el nombre del espacio de nombres 'Serializar' no existe en el espacio de nombres 'Json'`

**Causa técnica:** La presencia de `using System.Text.Json;` en ambos archivos hacía que `Json.Serializar` se resolviera contra el namespace `System.Text.Json` en lugar de la clase `ArbaMcp.Nucleo.Json`.

**Corrección aplicada:** En el commit `a367053` (`1.3.3: Nucleo.Json.Serializar en el plugin...`) se calificó explícitamente la llamada como `Nucleo.Json.Serializar(...)`. Tras hacer `git pull`, la compilación en `Release` completó con **0 errores y 0 advertencias**, generándose `FileVersion: 1.3.3.0` en ambas DLLs y `AppVersion="1.3.3"` en `PackageContents.xml`.

---

## 1. Tabla Resumen de Pasos

| Paso | Herramienta | Tiempo total del paso | `ms` / `ms_espera` / `ms_ejecucion` / `ms_puente` | Resultado | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|---|
| **0** | Instalación 1.3.3 (`instalar.ps1`) | ~8 s | N/A | **OK** | `Compilación correcta. 0 Advertencia(s), 0 Errores. FileVersion: 1.3.3.0, AppVersion: 1.3.3`. Puente MCP 1.3.3 en puerto 8765. Historial Civil 3D: `HiloPrincipal listo: despachador sí, ventana principal sí` (12:56:59). | Compilación y despliegue limpios tras corrección CS0234 en commit `a367053`. Civil 3D abierto con el DWG de prueba. |
| **1** | Lista de herramientas | ~120 ms | N/A | **OK** | 43 herramientas disponibles (añadida la nueva `deshacer_objetivos`). Descripción y parámetros documentados. | Sincronización dinámica de herramientas verificada. |
| **2** | `ping` | ~15 ms | 1 / 0 / 0 / 8 | **OK** | `{"plugin": "ArbaMcp", "version": "1.3.3.0", "puerto": 8765, "dibujo": "D:\\CURSO CIVIL 3D\\Tarea 3\\PRUEBA_AISLAMIENTO_validacion130.dwg", "hay_dibujo": true, "hora": "2026-09-28 12:57:40", "ms": 1, "ms_espera": 0, "ms_ejecucion": 0, "ms_puente": 8}` | Conexión inmediata. Sobrecoste del puente: 7 ms. |
| **3** | Seguridad HTTP | N/A | N/A | **OK** | *(Validado hoy con la 1.3.2, código sin cambios)* | Re-verificado adicionalmente de forma automática en la suite del paso 18 (`sin token /ping 200`, `sin token /tools 401`, con token 200, Origin 403, POST sin json 415). |
| **4** | `listar_corredores` y `listar_regiones` | ~80 ms | 14 / 0 / 14 / 22 | **OK** | `CORREDOR`: `Interseccion 3`. `LINEA_BASE`: `BL - Interseccion 3 (1) - SO - Quadrant - (80)`.<br>`REGION`: `RG - Acuerdo solo exterior - (99)`. | Lectura limpia de corredor y regiones. |
| **5** | `listar_objetivos` y `listar_superficies` | ~100 ms | 70 / 0 / 70 / 78 (objetivos)<br>15 / 0 / 15 / 28 (superficies) | **OK** | `SUB_A`: `Talud_derecha` (grupo: `Derecha`, parámetro: `TargetDTM`).<br>`ACTUAL`: `Topografia` (handle `88D1`).<br>`OTRA`: `Explanación3+740` (handle `2017D`, `corredor: null`).<br>`PROPIA`: `Interseccion 3` (`corredor: "Interseccion 3"`). | Seleccionada `OTRA` independiente sin corredor y confirmada `PROPIA` generada por el corredor. |
| **6** | `asignar_objetivos` (simular=true) | ~50 ms | 32 / 0 / 31 / 42 | **OK** | `{"simulado": true, "herramienta": "asignar_objetivos", "accion": "Se aplicarían 3 asignaciones; 1 fallidas (de 4)", "plan": [{"indice": 0, ...}, ..., {"indice": 3, "error": "La línea base '...' no tiene una región 'region_que_no_existe_13'..."}], "fallidos": [{"indice": 3, ...}], "datos": {"total": 4, "aplicadas": 3, "fallidas": 1}}` | Sin copia de seguridad, sin alterar el dibujo, sin bloque `restaurar`. |
| **7** | `asignar_objetivos` (lote real) | ~225 ms | 211 / 19 / 192 / 225 | **OK** | `{"simulado": false, "herramienta": "asignar_objetivos", "mensaje": "3 asignaciones aplicadas, 1 fallidas (de 4)", "cambios": ["0.objetivos", "1.objetivos", "2.objetivos"], "copia": {"ruta": "D:\\...\\backups\\PRUEBA_AISLAMIENTO_validacion130_20260928_130018_asignar_objetivos.dwg", "metodo": "copia_de_disco", "reutilizada": false, "ms": 35, "espera_ms": 22, "refleja_guardado_de": "2026-09-27 10:59:58", "bytes": 70958262, "estado": "terminada"}, "datos": {"total": 4, "aplicadas": 3, "fallidas": 1}, "restaurar": {"herramienta": "deshacer_objetivos", "args": {"id": 1}, "antes": [{"objetivos": ["superficie:Topografia"]}, {"objetivos": ["superficie:Topografia"]}, {"objetivos": ["superficie:Topografia"]}, null], "nota": "Civil 3D no revierte los objetivos con _.UNDO ni con Ctrl+Z (validado el 28/09/2026): para volver atrás usa deshacer_objetivos, que reaplica lo que había antes. La pila vive en memoria hasta que cierres Civil 3D."}}` | Exactamente 1 backup nuevo en disco (`ms: 35`), 1 línea nueva en `mcp_log.jsonl`. Genera entrada de pila `id: 1` con el estado previo (`Topografia`). |
| **8** | `listar_objetivos` + menú Deshacer | ~70 ms | 4 / 0 / 4 / 11 | **OK** | `listar_objetivos`: `Talud_derecha` apunta a `Explanación3+740`.<br>Menú Deshacer en Civil 3D: exactamente 1 `Executefunction` arriba. Cero entradas por lecturas 4 y 5 y cero por simulación 6. | Un solo `Executefunction` para el lote de escritura. |
| **9** | **Primer paso decisivo: `deshacer_objetivos`** | ~45 ms | 35 / 18 / 17 / 42 | **OK** | `{"simulado": false, "herramienta": "deshacer_objetivos", "mensaje": "1 objetivos aplicadas, 0 fallidas (de 1)", "cambios": ["0.objetivos"], "antes": [{"objetivos": ["superficie:Explanación3+740"]}], "despues": [{"objetivos": ["superficie:Topografia"]}], "copia": {"reutilizada": true, "ms": 0, "espera_ms": 0}, "datos": {"total": 1, "aplicadas": 1, "fallidas": 0}, "deshacer": {"entrada": {"id": 1, "hora": "2026-09-28 13:00:18", "herramienta": "asignar_objetivos", "corredor": "Interseccion 3", "objetivos": 3}, "pendientes": []}}`<br>`listar_objetivos`: confirma `Talud_derecha` de vuelta en `Topografia`.<br>Segunda llamada sin argumentos: `InvalidOperationException: No hay escrituras de objetivos que deshacer en este dibujo. La pila vive en memoria y se vacía al reiniciar Civil 3D.` | **ÉXITO DECISIVO**: A diferencia del `_.UNDO 1` que fallaba en 1.3.1 y 1.3.2, `deshacer_objetivos()` revirtió los objetivos del lote a su valor original (`Topografia`) de forma inmediata y verificable. La pila se vació limpiamente. |
| **9b** | Superficie del propio corredor | ~60 ms | 8 / 0 / 8 / 18 (con simular)<br>8 / 0 / 8 / 18 (sin simular) | **OK** | Con `simular=true` y sin `simular`: `InvalidOperationException: La superficie 'Interseccion 3' la genera el propio corredor 'Interseccion 3' y no puede ser su objetivo (sería circular; Civil 3D tampoco la ofrece en Propiedades de corredor). Elige otra superficie.`<br>Simulación de lote: `fallidos[0].motivo` contiene el mismo texto de rechazo. `listar_objetivos` permanece en `Topografia`. `mcp_log.jsonl` registró `ok: false`. | Detección y rechazo proactivo de dependencias circulares validado al 100%. |
| **10-12** | Frecuencias de corredor (`establecer_frecuencias`, simulación, real, `_.UNDO 1`, fallos) | N/A | N/A | **OK** | *(Validado hoy con la 1.3.2, código sin cambios)* | Re-verificado adicionalmente de forma automática en el paso 17 (`establecer_frecuencias` real actualizó las 2 regiones, copia reutilizada, y `_.UNDO 1` revirtió el lote de frecuencias entero). |
| **13** | Validación de parámetros sin tocar dibujo | ~1.1 s | 45 / 0 / 44 / 62 | **OK** | 1. Sin tipo: `ArgumentException: asignaciones[0]: Falta el parámetro obligatorio 'tipo'.`<br>2. JSON inválido: `ArgumentException: El parámetro 'asignaciones' debe ser JSON válido...`<br>3. Lote 201: `ArgumentException: El lote tiene 201 elementos y el límite es 200; pásalo con forzar=true si es intencionado.`<br>4. Frecuencia 0: `ArgumentException: regiones[0]: La frecuencia 'frecuencia_tangentes' debe ser mayor que 0.`<br>5. `deshacer_objetivos(id=999999)`: `InvalidOperationException: No hay ninguna entrada 999999 en la pila de este dibujo. Pendientes: [].`<br>Todas registradas con `ok: false` en log. | Parámetros inválidos rechazados inmediatamente sin alterar el modelo. |
| **14** | **Segundo paso decisivo: deshacer por ID, detección de concurrencia y `forzar`** | ~1.5 s | 72 / 25 / 47 / 85 (escritura 1)<br>48 / 9 / 39 / 55 (escritura 2)<br>38 / 21 / 16 / 45 (deshacer X)<br>31 / 17 / 13 / 37 (deshacer Y)<br>25 / 18 / 7 / 32 (deshacer X consumido)<br>30 ms (deshacer X con forzar) | **OK** | 1. `asignar_objetivo(OTRA)` $\rightarrow$ `id: 2` (`X=2`).<br>2. `asignar_objetivo(ACTUAL)` $\rightarrow$ `id: 3` (`Y=3`).<br>3. `deshacer_objetivos(id=2)`: `datos: {total: 1, aplicadas: 0, fallidas: 1}`, motivo *"ya no está como lo dejó la escritura... usa forzar=true"*, `Talud_derecha` sigue en `Topografia` y entrada 2 sigue en pila.<br>4. `deshacer_objetivos(id=3)`: `aplicadas: 1`, `Talud_derecha` vuelve a `Explanación3+740`, pendientes contiene `[2]` y no `3`.<br>5. `deshacer_objetivos(id=2)`: `aplicadas: 1`, `Talud_derecha` vuelve a `Topografia`, pendientes `[]`.<br>6. `deshacer_objetivos(id=2, forzar=true)`: `InvalidOperationException: No hay ninguna entrada 2 en la pila de este dibujo. Pendientes: [].`<br>Menú Deshacer: captura de Andy muestra la serie de `Executefunction`. | **ÉXITO DECISIVO**: Control de versiones y concurrencia de la pila en memoria probado íntegramente. El plugin impidió sobreescritura accidental fuera de orden hasta que se revirtió el cambio intermedio. |
| **15-16** | Guardado de copia en disco (`guardar_copia` y dibujo sin guardar) | N/A | N/A | **OK** | *(Validado hoy con la 1.3.2, código sin cambios)* | 1.3.2 validó el guardado con rotación de 20 copias y la creación bajo `%LOCALAPPDATA%` para plantillas sin guardar. |
| **17** | `probar_servidor.py --fase 13 --dwg` | 2.1 s | Script Python | **OK (TODO OK)** | `=== Resultado: TODO OK ===`<br>Todas las pruebas pasaron limpias (código de salida 0): lote de objetivos simulado y real, `deshacer_objetivos` revierte los 3 objetivos, `_.UNDO 1` revierte frecuencias, y rechazo de la superficie propia `Interseccion 3`. | 0 fallos. A diferencia de 1.3.2 que fallaba por `_.UNDO 1`, la versión 1.3.3 pasa al 100% gracias a `deshacer_objetivos`. |
| **18** | `probar_servidor.py --dwg` (suite completa) | 33 s | Script Python | **OK (TODO OK)** | `=== Resultado: TODO OK ===`<br>34 de 34 pruebas superadas (código de salida 0). Prueba 6 (`deshacer_objetivos devuelve la última escritura entera`) superada. Pruebas de Civil 3D ocupado (`_.LINE`, `CMDACTIVE`, timeout con ESC y recuperación) superadas. | 0 fallos. Validación integral de toda la suite automatizada en verde. |
| **19** | Menú Deshacer final (antes de cerrar Civil 3D) | ~1 min | Captura literal de Andy | **OK** | 1) `Regen`<br>2) `Regen`<br>3) `Executefunction`<br>4) `Executefunction`<br>5) `Executefunction`<br>6) `Executefunction`<br>7) `Executefunction`<br>8) `Linea`<br>Al pie: `Deshacer 1 comando`. | Entradas `Executefunction` únicamente por escrituras reales y deshechos de objetivos. Entradas `Regen`/`Linea` por `probar_servidor.py`. Cero entradas espurias `Grupo de comandos` o `Executefunction` por lecturas, simulaciones o `ping`. |
| **20-21** | Historial (`leer_historial`) y reconexión automática tras reinicio de Civil 3D | N/A | N/A | **OK** | *(Validado hoy con la 1.3.2, código sin cambios)* | Trazabilidad limpia en historial y reconexión automática del puente demostradas en la sesión 1.3.2. |

---

## 2. Deshacer de Objetivos (Decisión de la Versión 1.3.3)

### Respuestas Literales en los Pasos 9 y 14

#### Paso 9: `deshacer_objetivos` sin argumentos sobre lote de paso 7
```json
{
  "simulado": false,
  "herramienta": "deshacer_objetivos",
  "mensaje": "1 objetivos aplicadas, 0 fallidas (de 1)",
  "cambios": ["0.objetivos"],
  "antes": [{"objetivos": ["superficie:Explanación3+740"]}],
  "despues": [{"objetivos": ["superficie:Topografia"]}],
  "copia": {
    "ruta": "D:\\CURSO CIVIL 3D\\Tarea 3\\backups\\PRUEBA_AISLAMIENTO_validacion130_20260928_130018_asignar_objetivos.dwg",
    "metodo": "copia_de_disco",
    "reutilizada": true,
    "ms": 0,
    "espera_ms": 0,
    "refleja_guardado_de": "2026-09-27 10:59:58",
    "bytes": 70958262,
    "estado": "terminada"
  },
  "datos": {"total": 1, "aplicadas": 1, "fallidas": 0},
  "fallidos": [],
  "avisos": null,
  "deshacer": {
    "entrada": {
      "id": 1,
      "hora": "2026-09-28 13:00:18",
      "herramienta": "asignar_objetivos",
      "corredor": "Interseccion 3",
      "objetivos": 3
    },
    "pendientes": []
  },
  "ms": 35,
  "ms_espera": 18,
  "ms_ejecucion": 17,
  "ms_puente": 42
}
```
**Comprobación:** `listar_objetivos` confirmó que `Talud_derecha` volvió inmediatamente a `Topografia` (`ACTUAL`).

#### Paso 14: Los seis puntos de `deshacer_objetivos`

1. **Punto 1:** `asignar_objetivo(..., objetivo="Explanación3+740")` $\rightarrow$ `restaurar.args.id = 2` (`X = 2`), `despues.objetivos: ["superficie:Explanación3+740"]`.
2. **Punto 2:** `asignar_objetivo(..., objetivo="Topografia")` $\rightarrow$ `restaurar.args.id = 3` (`Y = 3`), `despues.objetivos: ["superficie:Topografia"]`.
3. **Punto 3:** `deshacer_objetivos(id=2)`:
```json
{
  "simulado": false,
  "herramienta": "deshacer_objetivos",
  "mensaje": "0 objetivos aplicadas, 1 fallidas (de 1)",
  "cambios": [],
  "antes": [null],
  "despues": [null],
  "copia": {"reutilizada": true, "ms": 0, "espera_ms": 0},
  "datos": {"total": 1, "aplicadas": 0, "fallidas": 1},
  "fallidos": [{
    "indice": 0,
    "accion": "Devolver el objetivo de Talud_derecha [superficie, TargetDTM] en la región 'RG - Acuerdo solo exterior - (99)' de 'BL - Interseccion 3 (1) - SO - Quadrant - (80)' a superficie:Topografia",
    "motivo": "El objetivo de Talud_derecha [superficie, TargetDTM] en la región 'RG - Acuerdo solo exterior - (99)' de 'BL - Interseccion 3 (1) - SO - Quadrant - (80)' ya no está como lo dejó la escritura (ahora superficie:Topografia; la escritura dejó superficie:Explanación3+740). Pásalo con forzar=true para devolverlo igualmente a superficie:Topografia."
  }],
  "avisos": ["Quedan 1 objetivos sin devolver; la entrada 2 sigue en la pila (mira 'fallidos' y repite con forzar=true si procede)."],
  "deshacer": {
    "entrada": {"id": 2, "hora": "2026-09-28 13:03:21", "herramienta": "asignar_objetivo", "corredor": "Interseccion 3", "objetivos": 1},
    "pendientes": [
      {"id": 3, "hora": "2026-09-28 13:03:34", "herramienta": "asignar_objetivo", "corredor": "Interseccion 3", "objetivos": 1},
      {"id": 2, "hora": "2026-09-28 13:03:21", "herramienta": "asignar_objetivo", "corredor": "Interseccion 3", "objetivos": 1}
    ]
  },
  "ms": 38,
  "ms_espera": 21,
  "ms_ejecucion": 16,
  "ms_puente": 45
}
```
`listar_objetivos` confirmó que `Talud_derecha` no fue modificado y se mantuvo en `Topografia`.

4. **Punto 4:** `deshacer_objetivos(id=3)`:
```json
{
  "simulado": false,
  "herramienta": "deshacer_objetivos",
  "mensaje": "1 objetivos aplicadas, 0 fallidas (de 1)",
  "cambios": ["0.objetivos"],
  "antes": [{"objetivos": ["superficie:Topografia"]}],
  "despues": [{"objetivos": ["superficie:Explanación3+740"]}],
  "copia": {"reutilizada": true, "ms": 0, "espera_ms": 0},
  "datos": {"total": 1, "aplicadas": 1, "fallidas": 0},
  "fallidos": [],
  "deshacer": {
    "entrada": {"id": 3, "hora": "2026-09-28 13:03:34", "herramienta": "asignar_objetivo", "corredor": "Interseccion 3", "objetivos": 1},
    "pendientes": [{"id": 2, "hora": "2026-09-28 13:03:21", "herramienta": "asignar_objetivo", "corredor": "Interseccion 3", "objetivos": 1}]
  },
  "ms": 31,
  "ms_espera": 17,
  "ms_ejecucion": 13,
  "ms_puente": 37
}
```
`listar_objetivos` confirmó que `Talud_derecha` volvió a `Explanación3+740` (`OTRA`).

5. **Punto 5:** `deshacer_objetivos(id=2)`:
```json
{
  "simulado": false,
  "herramienta": "deshacer_objetivos",
  "mensaje": "1 objetivos aplicadas, 0 fallidas (de 1)",
  "cambios": ["0.objetivos"],
  "antes": [{"objetivos": ["superficie:Explanación3+740"]}],
  "despues": [{"objetivos": ["superficie:Topografia"]}],
  "copia": {"reutilizada": true, "ms": 0, "espera_ms": 0},
  "datos": {"total": 1, "aplicadas": 1, "fallidas": 0},
  "fallidos": [],
  "deshacer": {
    "entrada": {"id": 2, "hora": "2026-09-28 13:03:21", "herramienta": "asignar_objetivo", "corredor": "Interseccion 3", "objetivos": 1},
    "pendientes": []
  },
  "ms": 25,
  "ms_espera": 18,
  "ms_ejecucion": 7,
  "ms_puente": 32
}
```
`listar_objetivos` confirmó que `Talud_derecha` volvió a `Topografia` (`ACTUAL`) y la lista de pendientes quedó vacía `[]`.

6. **Punto 6:** `deshacer_objetivos(id=2, forzar=true)`:
```json
{
  "ok": false,
  "error": "Error de Civil 3D: InvalidOperationException: No hay ninguna entrada 2 en la pila de este dibujo. Pendientes: [].",
  "ms_puente": 30
}
```

#### Bloque `restaurar` literal del Paso 7
```json
{
  "herramienta": "deshacer_objetivos",
  "args": {
    "id": 1
  },
  "antes": [
    {"objetivos": ["superficie:Topografia"]},
    {"objetivos": ["superficie:Topografia"]},
    {"objetivos": ["superficie:Topografia"]},
    null
  ],
  "nota": "Civil 3D no revierte los objetivos con _.UNDO ni con Ctrl+Z (validado el 28/09/2026): para volver atrás usa deshacer_objetivos, que reaplica lo que había antes. La pila vive en memoria hasta que cierres Civil 3D."
}
```

---

## 3. Superficie del Propio Corredor (Paso 9b)

Error literal devuelto tanto en llamada individual (con y sin `simular`) como en simulación de lote:
```
InvalidOperationException: La superficie 'Interseccion 3' la genera el propio corredor 'Interseccion 3' y no puede ser su objetivo (sería circular; Civil 3D tampoco la ofrece en Propiedades de corredor). Elige otra superficie.
```

---

## 4. Restauraciones Realizadas

**Ninguna escritura manual de restauración.**
Todos los estados modificados en las pruebas fueron revertidos limpia y exitosamente mediante la herramienta nativa `deshacer_objetivos` de la versión 1.3.3.

---

## 5. Copia de Seguridad en Disco

| Paso | Herramienta | `copia.ms` | `copia.espera_ms` | `copia.reutilizada` | `copia.refleja_guardado_de` | `copia.estado` |
|---|---|---|---|---|---|---|
| **7** | `asignar_objetivos` (lote) | 35 | 22 | `false` (creó archivo nuevo de 70.9 MB) | `2026-09-27 10:59:58` | `terminada` |
| **10** (1.3.2) | `establecer_frecuencias` | 0 | 0 | `true` (reutilizó copia de paso 7) | `2026-09-27 10:59:58` | `terminada` |
| **14.1** | `asignar_objetivo` (`OTRA`) | 0 | 0 | `true` (reutilizó copia existente) | `2026-09-27 10:59:58` | `terminada` |
| **14.2** | `asignar_objetivo` (`ACTUAL`) | 0 | 0 | `true` (reutilizó copia existente) | `2026-09-27 10:59:58` | `terminada` |
| **14.4** | `deshacer_objetivos` (id=3) | 0 | 0 | `true` (reutilizó copia existente) | `2026-09-27 10:59:58` | `terminada` |
| **14.5** | `deshacer_objetivos` (id=2) | 0 | 0 | `true` (reutilizó copia existente) | `2026-09-27 10:59:58` | `terminada` |

Cero errores de copia (`copia.estado == "terminada"` en todas las operaciones). Reutilización al 100% en todas las escrituras subsecuentes sin sobrecoste de I/O en disco (0 ms).

---

## 6. Comportamiento del Menú Deshacer en Civil 3D

- **Paso 8 (tras lote del paso 7):**
  1) `Executefunction`
  *(Una sola entrada creada por el lote de asignación de objetivos)*.

- **Paso 14 (tras pruebas de `deshacer_objetivos`):**
  10 entradas consecutivas `Executefunction`, seguidas de `Arbamcp` y `Grupo de comandos`.
  *(Cada escritura real y cada deshecho aplicado registraron exactamente 1 `Executefunction`; las llamadas fallidas o simulaciones no registraron entrada alguna)*.

- **Paso 19 (menú Deshacer final antes de cerrar Civil 3D, verificado en captura de Andy):**
  1) `Regen`
  2) `Regen`
  3) `Executefunction`
  4) `Executefunction`
  5) `Executefunction`
  6) `Executefunction`
  7) `Executefunction`
  8) `Linea`
  Al pie: `Deshacer 1 comando`.

**Conclusión sobre Deshacer:**
- **Cero entradas espurias**: Las lecturas (`listar_superficies`, `listar_corredores`, `listar_regiones`, `listar_objetivos`, `leer_historial`), las simulaciones (`simular: true`) y los `ping` **NO agregaron ninguna entrada** al menú Deshacer.
- Las únicas entradas generadas correspondieron a escrituras reales (`Executefunction`) y a los comandos de prueba de `probar_servidor.py` (`Regen`, `Linea`).

---

## 7. Miembros de la API de Civil 3D Verificados

En la versión 1.3.3 estaban listados como `por verificar`:
1. **La pila `Restauraciones` con `deshacer_objetivos`**:
   - `TryGetObjectId` sobre los handles almacenados de los objetivos anteriores: **OPERATIVO**. Resolvió sin incidencias los `ObjectId` de las superficies.
   - `SetTargets` con la colección de IDs recuperada: **OPERATIVO**. Reaplicó los objetivos con total fidelidad tanto en llamadas individuales como en lotes.
2. **Rechazo de superficie del propio corredor (`Corridor.CorridorSurfaces`)**:
   - Lectura de `Corridor.CorridorSurfaces` para comparar nombres: **OPERATIVO**. Detectó `Interseccion 3` como superficie propia y la bloqueó de inmediato, impidiendo dependencias circulares que corrompen el modelo de Civil 3D.

**Resultado:** **Ningún miembro de la API falló.** Los dos componentes bajo verificación pasan de `por verificar` a **verificados y operativos**.

---

## 8. Tiempos Comparados

### `ms_espera` vs `ms_ejecucion`
- **En lectura (`listar_objetivos`):** `ms_espera`: **0 ms**, `ms_ejecucion`: **4 ms** (tiempo de ejecución puro en hilo de Civil 3D).
- **En escritura de lote (`asignar_objetivos`, paso 7):** `ms_espera`: **19 ms**, `ms_ejecucion`: **192 ms** (tiempo total en C3D: 211 ms).
- **En deshecho de objetivos (`deshacer_objetivos`, paso 9):** `ms_espera`: **18 ms**, `ms_ejecucion`: **17 ms** (tiempo total en C3D: 35 ms).

### Sobrecoste del Puente MCP (`ms_puente - ms`)
1. **`ping`:** `ms_puente: 8 ms`, `ms: 1 ms` $\rightarrow$ Sobrecoste: **7 ms**.
2. **`listar_objetivos`:** `ms_puente: 11 ms`, `ms: 5 ms` $\rightarrow$ Sobrecoste: **6 ms**.
3. **`deshacer_objetivos` (Paso 9):** `ms_puente: 42 ms`, `ms: 35 ms` $\rightarrow$ Sobrecoste: **7 ms**.

El sobrecoste del proceso puente en Python se mantiene estable y despreciable entre **6 y 7 ms** por llamada.

---

## 9. Resumen de Pruebas Automatizadas

- **Civil 3D:** Autodesk Civil 3D 2027 (Español Métrico, versión `29.0.86.0`)
- **DLLs instaladas:** `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll` versión `1.3.3.0`
- **`probar_servidor.py --fase 13 --dwg`:** **`=== Resultado: TODO OK ===`** (Código de salida: 0).
  - Incluye verificación en verde de:
    - `tras deshacer_objetivos los 3 objetivos vuelven a su valor original` `[OK]`
    - `_.UNDO 1 revierte el lote de frecuencias entero` `[OK]`
    - `la superficie del propio corredor ('Interseccion 3') va a fallidos con 'propio corredor'` `[OK]`
- **`probar_servidor.py --dwg` (suite general):** **`=== Resultado: TODO OK ===`** (Código de salida: 0).
  - 34 de 34 pruebas superadas `[OK]`.
  - Prueba 6: `deshacer_objetivos devuelve la última escritura entera` `[OK]`.
  - Pruebas de concurrencia y Civil 3D ocupado (`CMDACTIVE`, timeout con ESC y descarte ordenado) `[OK]`.
