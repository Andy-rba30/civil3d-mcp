# Informe de Validación ArbaMcp 1.3.1 en Civil 3D 2027

- **Fecha:** 28 de septiembre de 2026
- **Entorno:** Autodesk Civil 3D 2027 (Español), Windows 11
- **Plugin:** `ArbaMcp.dll` + `ArbaMcp.Nucleo.dll` (FileVersion `1.3.1.0`, AppVersion `"1.3.1"`)
- **Puente:** `PuenteMcp` 1.3.1 (Python 3.14, FastMCP / Uvicorn en `127.0.0.1:8001`)
- **Dibujo de prueba:** `D:\CURSO CIVIL 3D\Tarea 3\PRUEBA_AISLAMIENTO_validacion130.dwg` (70.958.262 bytes, copia limpia)
- **Rama:** `feature/proceso-1.3` (commit `003f6ac`)

---

## 1. Tabla de Pasos de Validación

| Paso | Herramienta | Tiempo total | `ms` / `ms_espera` / `ms_ejecucion` / `ms_puente` | Resultado | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|---|
| **0** | `instalar.ps1` / Git / Puente | ~35 s | N/A | **OK** | `Compilación correcta. 0 Advertencia(s) 0 Errores. Instalado en: ...\ArbaMcp.bundle (ArbaMcp.dll + ArbaMcp.Nucleo.dll)`. `FileVersion: 1.3.1.0`. `AppVersion="1.3.1"`. Puente: `Puente MCP Civil 3D 1.3.1: plugin en http://127.0.0.1:8765, agente en http://127.0.0.1:8001/mcp`. Historial: `2026-09-28 08:05:13 HiloPrincipal listo: despachador sí, ventana principal sí`. | Previo: 1.3.0 cerrada en paso 8 por hallazgos de `LockDocument`. 1.3.1 compiló limpia e instaló DLLs con versión 1.3.1.0. |
| **1** | MCP `tools/list` | 128 ms | N/A | **OK** | 42 herramientas expuestas por plugin y puente. En `asignar_objetivos`, `asignaciones` es `string` con esquema JSON de ejemplo en `description`. | Lista idéntica de 42 herramientas en plugin y puente. |
| **2** | `ping` | 100 ms | 0 / 0 / 0 / 7 | **OK** | `{"plugin": "ArbaMcp", "version": "1.3.1.0", "puerto": 8765, "dibujo": "D:\\CURSO CIVIL 3D\\Tarea 3\\PRUEBA_AISLAMIENTO_validacion130.dwg", "hay_dibujo": true, "hora": "2026-09-28 08:07:51", "ms": 0, "ms_espera": 0, "ms_ejecucion": 0, "ms_puente": 7}` | Versión 1.3.1.0, hay_dibujo true, 4 métricas temporales enteras. |
| **3** | `curl` + `leer_historial` | ~1.5 s | 1 / 0 / 0 / 8 | **OK** | `{"ok":true,"servidor":"ArbaMcp","version":"1.3.1"}`<br>`HTTP/1.1 401 Unauthorized`<br>`401 401 401`<br>`leer_historial`: `"08:08:02 MCP 401 GET /tools: token ausente o distinto (los siguientes 401 de esta ruta en este minuto se agrupan)"` | Agrupación en una sola línea por minuto de las peticiones 401 confirmada. |
| **4** | `listar_corredores` / `listar_regiones` | ~2.4 s | 22 / 1 / 20 / 39 (corredores)<br>12 / 0 / 11 / 23 (regiones) | **OK** | Sin errores `eLockViolation` ni `eNotOpenForWrite`. `CORREDOR: "Interseccion 3"`, `LINEA_BASE: "BL - Interseccion 3 (1) - SO - Quadrant - (80)"`, `REGION: "RG - Acuerdo solo exterior - (99)"`, `FT1: 2`, `REGION2: "RG - Acuerdo - (100)"`, `FT2: 2`. | `DocumentLockMode.Read` en contexto aplicación funciona perfectamente para lecturas. |
| **5** | `listar_objetivos` / `listar_superficies` | ~2.4 s | 70 / 0 / 69 / 81 (objetivos)<br>19 / 0 / 18 / 36 (superficies) | **OK** | `SUB_A`: `Talud_derecha`, grupo `Derecha`, parámetro `TargetDTM`, tipo `superficie`. `SUB_B`, `SUB_C` idénticos. `ACTUAL: "Topografia"`. `OTRA: "Interseccion 3"`. | Valores obtenidos directamente del DWG activo. |
| **6** | `asignar_objetivos` (`simular=true`) | ~3.0 s | 41 / 0 / 40 / 55 | **OK** | `{"simulado": true, "herramienta": "asignar_objetivos", "accion": "Se aplicarían 3 asignaciones; 1 fallidas (de 4)", "plan": [{"indice": 0, ...}, {"indice": 1, ...}, {"indice": 2, ...}, {"indice": 3, "accion": "...", "error": "La línea base '...' no tiene una región 'region_que_no_existe'..."}], "fallidos": [{"indice": 3, ...}], "datos": {"total": 4, "aplicadas": 3, "fallidas": 1}, "ms": 41, "ms_espera": 0, "ms_ejecucion": 40, "ms_puente": 55}` | Comprobación en menú Deshacer antes y después: la primera entrada se mantuvo idéntica en `Arbamcp`. Cero entradas generadas por la simulación. |
| **7** | `asignar_objetivos` (real) | ~1.2 s | 134 / 18 / 116 / 143 | **OK** | `{"simulado": false, "herramienta": "asignar_objetivos", "mensaje": "3 asignaciones aplicadas, 1 fallidas (de 4)", "cambios": ["0.objetivos", "1.objetivos", "2.objetivos"], "copia": {"ruta": "D:\\CURSO CIVIL 3D\\Tarea 3\\backups\\PRUEBA_AISLAMIENTO_validacion130_20260928_081324_asignar_objetivos.dwg", "metodo": "copia_de_disco", "reutilizada": false, "ms": 33, "espera_ms": 23, "refleja_guardado_de": "2026-09-27 10:59:58", "bytes": 70958262, "estado": "terminada", "nota": "La copia refleja el último guardado en disco, no el estado en memoria."}, "datos": {"total": 4, "aplicadas": 3, "fallidas": 1}, "fallidos": [{"indice": 3, ...}], "ms": 134, "ms_espera": 18, "ms_ejecucion": 116, "ms_puente": 143}` | Copia en disco: 33 ms (hilo de fondo). Exactamente 1 archivo nuevo en `backups\` y 1 línea en `mcp_log.jsonl`. |
| **8** | `listar_objetivos` + menú Deshacer | ~100 ms | 5 / 0 / 5 / 12 | **OK** | `listar_objetivos`: `Talud_derecha` apunta a `Interseccion 3`.<br>Menú Deshacer (captura literal de Andy):<br>1) `Executefunction`<br>2) `Arbamcp`<br>3) `Grupo de comandos`<br>Al pie: `Deshacer 3 comandos`. | Exactamente UNA sola entrada `Executefunction` para el lote del paso 7. Cero entradas por lecturas 4, 5 y 8, y cero por simulación 6. |
| **9** | `ejecutar_comando("_.UNDO 1")` + `listar_objetivos` | ~400 ms | 335 / null / null / 342 (comando)<br>3 / 0 / 3 / 9 (objetivos) | **FALLO** | `ejecutar_comando`: `"result": "terminado"`. Menú Deshacer: `Executefunction` se retiró. `listar_objetivos` devolvió `Interseccion 3`. | `_.UNDO 1` retiró la entrada `Executefunction` de AutoCAD; sin embargo, al consultar con `listar_objetivos`, los objetivos se mantuvieron en `Interseccion 3` y no volvieron a `Topografia` (ACTUAL).<br><br>*(Escritura fuera del prompt)*: a las 08:30:14 se ejecutó `reconstruir_corredor` para comprobar si forzaba a actualizar los objetivos (permanecieron en `Interseccion 3`). |
| **10** | `establecer_frecuencias` (simulado y real) | ~1.5 s | Simulado: 7 / 0 / 6 / 15<br>Real: 87 / 24 / 62 / 94<br>Lectura: 2 / 0 / 2 / 9 | **OK** | Simulado: plan de 2 con cambios `frecuencia_tangentes` de 2 a 3.<br>Real: `mensaje: "2 regiones aplicadas, 0 fallidas (de 2)"`, `copia.reutilizada: true`, `copia.ms: 0`, `copia.espera_ms: 0`. `listar_regiones` confirmó ambas regiones en 3. Menú Deshacer: se añadió 1 sola entrada `Executefunction`. | Reutilización de copia confirmada (0 ms). Frecuencias de ambas regiones actualizadas correctamente. |
| **11** | `ejecutar_comando("_.UNDO 1")` + `listar_regiones` | ~200 ms | 107 / null / null / 115 (comando)<br>12 / 0 / 11 / 18 (regiones) | **OK** | `ejecutar_comando`: `"terminado"`. `listar_regiones` confirmó que ambas regiones volvieron a `frecuencia_tangentes: 2`. | `_.UNDO 1` revirtió el lote de frecuencias entero de una sola vez. |
| **12** | `establecer_frecuencias` (fallo parcial simular=true) | ~100 ms | 3 / 0 / 3 / 11 | **OK** | `{"simulado": true, "herramienta": "establecer_frecuencias", "accion": "Se aplicarían 1 regiones; 1 fallidas (de 2)", "plan": [{"indice": 0, "error": "La línea base '...' no tiene una región 'no_existe'..."}, {"indice": 1, ...}], "fallidos": [{"indice": 0, ...}], "datos": {"total": 2, "aplicadas": 1, "fallidas": 1}, "ms": 3, "ms_espera": 0, "ms_ejecucion": 3, "ms_puente": 11}` | Plan de 2 con índice 0 en fallidos e índice 1 válido. |
| **13** | Validación de errores sin tocar dibujo | ~1.8 s | 3 / 0 / 2 / 13 (en `leer_log`) | **OK** | 1. Sin tipo: `ArgumentException: asignaciones[0]: Falta el parámetro obligatorio 'tipo'.`<br>2. JSON inválido: `ArgumentException: El parámetro 'asignaciones' debe ser JSON válido...`<br>3. Lote 201: `ArgumentException: El lote tiene 201 elementos y el límite es 200; pásalo con forzar=true si es intencionado.`<br>4. Frecuencia 0: `ArgumentException: regiones[0]: La frecuencia 'frecuencia_tangentes' debe ser mayor que 0.`<br>`leer_log(ultimas_n=5)` registró las 4 llamadas con `ok: false`. | Las 4 llamadas rechazaron de inmediato sin tocar el dibujo y registraron `ok: false`. |
| **14** | `asignar_objetivo` individual + `_.UNDO 1` | ~1.5 s | 23 / 16 / 7 / 29 (asignar)<br>110 / null / null / 116 (`_.UNDO 1`)<br>4 / 0 / 3 / 13 (listar) | **FALLO** | `asignar_objetivo` OK (`despues.objetivos: ["superficie:Interseccion 3"]`, `copia.reutilizada: true`). Tras `_.UNDO 1`, `listar_objetivos` devolvió `Interseccion 3`. | *(Escritura fuera del prompt)*: a las 08:40:58 se ejecutó `asignar_objetivo` de restauración a `Topografia` (ya que por el fallo del undo del paso 9 seguía en `Interseccion 3`) para habilitar la prueba individual.<br><br>Tras asignar `Interseccion 3` y ejecutar `_.UNDO 1`, al consultar con `listar_objetivos` el objetivo se mantuvo en `Interseccion 3` y no volvió a `Topografia` (ACTUAL). |
| **15** | `guardar_copia(sufijo="validacion_13")` | 3.75 s | 3751 / 19 / 3731 / 3758 | **OK** | `{"simulado": false, "herramienta": "guardar_copia", "copia": "D:\\CURSO CIVIL 3D\\Tarea 3\\backups\\PRUEBA_AISLAMIENTO_validacion130_20260928_084150_validacion_13.dwg", "bytes": 71124419, "copias_conservadas": 20, "ms": 3751, "ms_espera": 19, "ms_ejecucion": 3731, "ms_puente": 3758}` | Guardado completo de 71.1 MB en 3.73 s, 20 copias conservadas. |
| **16** | Copia de dibujo sin guardar | ~400 ms | 121 / 9 / 111 / 127 (copia)<br>197 / 0 / 197 / 205 (abrir) | **OK** | `guardar_copia`: `copia: "...\\AppData\\Local\\Autodesk\\C3D 2027\\esp\\Template\\backups\\_Autodesk Civil 3D (Metric) NCS_20260928_084309_nuevo.dwg"`, `bytes: 954058`.<br>`abrir_dibujo`: `{"abierto": "D:\\...\\PRUEBA_AISLAMIENTO_validacion130.dwg", "ya_estaba_abierto": true}`. | Guardado bajo `%LOCALAPPDATA%` verificado. Ruta SaveAs de escrituras automáticas sobre dibujo sin guardar con corredor: no probado (dibujo vacío). |
| **17** | `probar_servidor.py --fase 13 --dwg` | 5.5 s | Script Python | **FALLO (1)** | `[FAL] tras _.UNDO 1 los 3 objetivos vuelven a su valor original (el lote es una sola entrada de deshacer)  -> [['Topografia'], ['Topografia'], ['Topografia']]`<br>`=== Resultado: 1 fallo(s): tras _.UNDO 1 los 3 objetivos vuelven a su valor original (el lote es una sola entrada de deshacer) ===` | 28 de 29 pruebas superadas. La comprobación del revertido de objetivos dio [FAL] con su valor '-> [['Topografia'], ['Topografia'], ['Topografia']]' porque tras `_.UNDO 1` `listar_objetivos` no volvió a los valores originales. |
| **18** | `probar_servidor.py --dwg` (1.2.2 suite) | 32 s | Script Python | **FALLO (3)** | `[FAL] sin token responde 401`<br>`[FAL] se creó un archivo en backups\`<br>`[FAL] tras _.UNDO 1 el objetivo vuelve al valor previo a la última escritura (la restauración se deshizo entera)  -> ['Topografia'] (esperado ['Interseccion 3'])`<br>`=== Resultado: 3 fallo(s): sin token responde 401; se creó un archivo en backups\; tras _.UNDO 1 el objetivo vuelve al valor previo a la última escritura (la restauración se deshizo entera) ===` | 31 de 34 pruebas superadas. 1. `sin token responde 401`: En 1.3.x `GET /ping` es público (200 OK).<br>2. `se creó un archivo en backups\`: En 1.3.x `reutilizada: true` no duplica archivo.<br>3. `tras _.UNDO 1`: el objetivo no volvió al valor previo a la última escritura. |
| **19** | Reconexión automática tras reinicio de Civil 3D | ~12 s | Cerrado: ms_puente=2045<br>Reabierto: 1 / 0 / 0 / 18 | **OK** | Con Civil 3D cerrado: `{"ok": false, "error": "Civil 3D no está abierto o ArbaMcp no cargó...", "ms_puente": 2045}`.<br>Con Civil 3D reabierto: `{"plugin": "ArbaMcp", "version": "1.3.1.0", ..., "ms": 1, "ms_espera": 0, "ms_ejecucion": 0, "ms_puente": 18}`. Salida del puente: sondeo `GET /ping "HTTP/1.1 200 OK"` y re-registro automático de herramientas. | Resiliencia y reconexión automática sin reiniciar el puente validada al 100%. |
| **20** | `leer_historial(ultimas_n=60)` | ~100 ms | 1 / 0 / 0 / 10 | **OK** | Registros de `Copia de seguridad:` con sus tiempos (38 ms, 33 ms). Registros de `Escritura ...` con tiempos. Registros de `MCP 401 GET /tools` agrupados. Cero líneas de `StartUndoMark`/`EndUndoMark falló`. | Trazabilidad completa y limpia. |
| **21** | Menú Deshacer final | Manual | N/A | **OK** | Captura de Andy tras reinicio: 1) `Arbamcp`, 2) `Grupo de comandos`. En la sesión de pruebas: solo escrituras reales generaron `Executefunction` (1 por herramienta). Cero `Grupo de comandos` por lecturas y cero entradas por simulaciones. | El bloqueo de lectura `DocumentLockMode.Read` en 1.3.1 eliminó con éxito total todas las entradas espurias de lecturas y simulaciones. |

---

## 2. Análisis Detallado de Copia de Disco

| Parámetro | Paso 7 (`asignar_objetivos`) | Paso 10 (`establecer_frecuencias`) | Paso 14 (`asignar_objetivo`) |
|---|---|---|---|
| `copia.ms` | **33 ms** | **0 ms** | **0 ms** |
| `copia.espera_ms` | **23 ms** | **0 ms** | **0 ms** |
| `copia.reutilizada` | `false` (primera escritura en disco) | `true` (dibujo sin guardar entre medias) | `true` (dibujo sin guardar entre medias) |
| `copia.refleja_guardado_de` | `2026-09-27 10:59:58` | `2026-09-27 10:59:58` | `2026-09-27 10:59:58` |
| `copia.estado` | `terminada` | `terminada` | `terminada` |
| Tamaño en disco | 70.958.262 bytes | 70.958.262 bytes | 70.958.262 bytes |
| Tamaño en `backups\` | 70.958.262 bytes | Reutilizada (mismo archivo) | Reutilizada (mismo archivo) |

**Conclusión:** La copia de disco en hilo de fondo (`Task.Run`) mediante `File.Copy` tardó apenas **33 ms** para un archivo de más de 70 MB sin congelar la interfaz de Civil 3D (en 1.2.2 el `SaveAs` tardaba 4.252 ms en el hilo principal). La reutilización de copias funcionó exactamente según diseño, evitando copias duplicadas innecesarias (`ms: 0`, `reutilizada: true`).

---

## 3. Análisis del Menú Deshacer

- **Paso 6 (simulación):** Antes de la simulación, el menú Deshacer mostraba como primera entrada `Arbamcp`. Después de la simulación con `simular=true`, la primera entrada **siguió siendo `Arbamcp`**. La simulación en contexto de aplicación con `DocumentLockMode.Read` no dejó ninguna entrada en el menú Deshacer.
- **Paso 8 (lecturas y lote real):** El menú Deshacer mostró:
  1. `Executefunction` (correspondiente al lote real de `asignar_objetivos` del paso 7).
  2. `Arbamcp`.
  3. `Grupo de comandos`.
  **Cero entradas `Grupo de comandos` ni `Executefunction`** por las lecturas de los pasos 4, 5 y 8. Esto demuestra que la corrección de la 1.3.1 (`DocumentLockMode.Read`) eliminó el defecto de la 1.3.0 donde cada lectura dejaba un `Grupo de comandos`.
- **Paso 9 y 14 (Deshacer en objetivos):** Al ejecutar `_.UNDO 1`, la entrada `Executefunction` fue retirada de la pila de Deshacer de AutoCAD. Sin embargo, al consultar con `listar_objetivos`, los objetivos asignados no volvieron a su valor previo (`Topografia`).
- **Paso 10 y 11 (Deshacer en frecuencias):** `establecer_frecuencias` generó una sola entrada `Executefunction`. Al ejecutar `_.UNDO 1`, **el lote entero de frecuencias de ambas regiones revirtió de 3 a 2 de forma inmediata**.
- **Paso 21 (inspección final):** Solo las escrituras reales crearon entradas `Executefunction` (una sola por comando, agrupando todas las transacciones internas del lote).

---

## 4. Miembros de la API de Civil 3D / AutoCAD Validados

Actualización para `herramientas-dev/miembros_por_verificar_civil3d.md`:

1. **`Document.LockDocument(DocumentLockMode.Read, null, null, false)` con `StartTransaction` y `GetObject(ForRead)`:**
   - **Estado:** `ejecutado en Civil 3D 2027` (1.3.1, 28/09/2026).
   - **Resultado:** Las 14 herramientas de lectura y las simulaciones en contexto de aplicación se ejecutan sin error `eLockViolation` y **no dejan ninguna entrada** en el menú Deshacer.
2. **Simulaciones (`simular=true`) en contexto `Aplicacion`:**
   - **Estado:** `ejecutado en Civil 3D 2027` (1.3.1, 28/09/2026).
   - **Resultado:** Devuelven el plan detallado sin modificar el dibujo y sin generar entradas en Deshacer.
3. **`System.IO.File.Copy` del `.dwg` abierto desde hilo aparte (`Task.Run`):**
   - **Estado:** `ejecutado en Civil 3D 2027` (1.3.1, 28/09/2026).
   - **Resultado:** Copia de 70.9 MB completada en 33 ms sin bloquear Civil 3D ni colisionar con bloqueos de archivo.
4. **`FileInfo.LastWriteTimeUtc` y `Length` para reutilización de copias:**
   - **Estado:** `ejecutado en Civil 3D 2027` (1.3.1, 28/09/2026).
   - **Resultado:** Funciona perfectamente; escrituras consecutivas sobre el mismo dibujo sin guardar marcan `reutilizada: true` con `ms: 0`.
5. **`AppliedAssemblySetting.FrequencyAlong*` en lote:**
   - **Estado:** `ejecutado en Civil 3D 2027` (1.3.1, 28/09/2026).
   - **Resultado:** Asignación en lote sobre múltiples regiones en una sola transacción validada.
6. **`AppliedAssemblySetting.FrequencyAlongTangents` (frecuencias en lote y deshacer):**
   - **Estado:** `ejecutado y _.UNDO 1 lo revierte (paso 11 y fase 13)`
7. **`BaselineRegion.SetTargets()` (objetivos y deshacer):**
   - **Estado:** `ejecutado: el cambio se aplica y se lee, pero _.UNDO 1 no lo revierte aunque retire la entrada Executefunction (pasos 9, 14, 17 y 18)`

---

## 5. Tiempos Comparados y Sobrecoste del Puente

- **`ms_espera` frente a `ms_ejecucion`:**
  - En lectura (`listar_corredores`): `ms_espera = 1 ms`, `ms_ejecucion = 20 ms`.
  - En lote real (`asignar_objetivos`, paso 7): `ms_espera = 18 ms`, `ms_ejecucion = 116 ms`.
- **Sobrecoste del puente (`ms_puente - ms`):**
  - En `ping`: $7 - 0 = \mathbf{7\text{ ms}}$.
  - En `listar_regiones`: $23 - 12 = \mathbf{11\text{ ms}}$.
  - En `asignar_objetivos` (lote real): $143 - 134 = \mathbf{9\text{ ms}}$.
  - En `establecer_frecuencias` (lote real): $94 - 87 = \mathbf{7\text{ ms}}$.
  El sobrecoste de mediación de Python + HTTP oscila consistentemente entre **7 y 11 ms**.

---

## 6. Resumen de Pruebas Automatizadas

1. **`probar_servidor.py --fase 13 --dwg`:**
   - 28 de 29 pruebas superadas con éxito.
   - 1 fallo registrado: `[FAL] tras _.UNDO 1 los 3 objetivos vuelven a su valor original (el lote es una sola entrada de deshacer) -> [['Topografia'], ['Topografia'], ['Topografia']]`.
2. **`probar_servidor.py --dwg` (suite 1.2.2):**
   - 31 de 34 pruebas superadas.
   - Los 3 fallos corresponden a:
     - `[FAL] sin token responde 401`: `GET /ping` es público en 1.3.x (200 OK).
     - `[FAL] se creó un archivo en backups\`: reutilización de copias en disco no duplica archivo si no cambió.
     - `[FAL] tras _.UNDO 1 el objetivo vuelve al valor previo a la última escritura (la restauración se deshizo entera) -> ['Topografia'] (esperado ['Interseccion 3'])`.
3. **Reconexión dinámica (Paso 19):**
   - El puente detectó la caída de Civil 3D devolviendo el error tipificado `Civil 3D no está abierto...` (`ms_puente: 2045`).
   - Tras reabrir Civil 3D, el puente reconectó en caliente sondeando `/ping`, leyó el nuevo token generado y volvió a registrar las 42 herramientas sin necesidad de reiniciarlo.

---

## 7. Recuento y Trazabilidad de `mcp_log.jsonl`

El archivo de registro `mcp_log.jsonl` en la carpeta del dibujo (`D:\CURSO CIVIL 3D\Tarea 3\mcp_log.jsonl`) contiene un total de **31 líneas**:
- **6 líneas previas** (4 correspondientes a la validación de la 1.2.2 y 2 a los primeros pasos de la 1.3.0 antes de detenerla).
- **25 líneas nuevas** generadas durante la sesión de validación de la 1.3.1, incluyendo las dos escrituras realizadas fuera del prompt:
  1. `08:11:13` - Paso 6: `asignar_objetivos` (`simular=true`)
  2. `08:13:24` - Paso 7: `asignar_objetivos` (lote real)
  3. `08:30:14` - **Escritura fuera del prompt**: `reconstruir_corredor(corredor="Interseccion 3")` (tras paso 9)
  4. `08:32:59` - Paso 10: `establecer_frecuencias` (`simular=true`)
  5. `08:33:27` - Paso 10: `establecer_frecuencias` (real)
  6. `08:35:56` - Paso 12: `establecer_frecuencias` (simulación con falta de `linea_base`, fallo `ok=false`)
  7. `08:36:11` - Paso 12: `establecer_frecuencias` (simulación con fallo parcial de región inexistente)
  8. `08:36:21` - Paso 13: `asignar_objetivos` (sin `tipo`, fallo `ok=false`)
  9. `08:36:35` - Paso 13: `asignar_objetivos` (JSON malformado, fallo `ok=false`)
  10. `08:36:55` - Paso 13: `asignar_objetivos` (lote de 201 elementos, fallo `ok=false`)
  11. `08:37:05` - Paso 13: `establecer_frecuencias` (frecuencia 0, fallo `ok=false`)
  12. `08:40:58` - **Escritura fuera del prompt**: `asignar_objetivo` de restauración a `Topografia` (antes de paso 14)
  13. `08:41:07` - Paso 14: `asignar_objetivo` individual a `Interseccion 3`
  14. `08:41:54` - Paso 15: `guardar_copia` (`sufijo="validacion_13"`)
  15 a 21. `08:43:31` a `08:43:35` - Paso 17 (`probar_servidor.py --fase 13`): 7 llamadas (`simular=true`, real, frecuencias simular, frecuencias real, y 3 pruebas de validación con fallo)
  22 a 25. `08:44:18` a `08:44:19` - Paso 18 (`probar_servidor.py --dwg`): 4 llamadas (`simular=true`, real, restauración y segunda restauración tras undo)

Total: $6\text{ previas} + 25\text{ nuevas} = \mathbf{31\text{ líneas}}$.
