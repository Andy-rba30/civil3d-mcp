# Informe de Validación ArbaMcp 1.3.2 en Autodesk Civil 3D 2027

- **Fecha:** 28 de septiembre de 2026
- **Entorno:** Autodesk Civil 3D 2027 (Español Métrico) sobre Windows 11
- **Versión de ArbaMcp:** 1.3.2 (`FileVersion`: `1.3.2.0` en `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`)
- **Versión de PuenteMcp:** 1.3.2 (`Puente MCP Civil 3D 1.3.2`)
- **Dibujo de prueba:** `D:\CURSO CIVIL 3D\Tarea 3\PRUEBA_AISLAMIENTO_validacion130.dwg` (70.9 MB)
- **Token:** `<oculto>`

---

## 1. Tabla Resumen de Pasos

| Paso | Herramienta | Tiempo total del paso | `ms` / `ms_espera` / `ms_ejecucion` / `ms_puente` | Resultado | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|---|
| **0** | Instalación 1.3.2 | ~35 s | N/A | **OK** | `Compilación correcta. 0 Advertencia(s), 0 Errores. FileVersion: 1.3.2.0, AppVersion: 1.3.2`. `Puente MCP Civil 3D 1.3.2 en http://127.0.0.1:8765`. Historial: `HiloPrincipal listo: despachador sí, ventana principal sí` (10:11:12). | Se lanzó Civil 3D 2027 con perfil Métrico (`/p "<<C3D_Metric>>" /product C3D /language es-ES`). |
| **1** | Lista de herramientas | ~150 ms | N/A | **OK** | 42 herramientas disponibles, incluidas `asignar_objetivos` y `establecer_frecuencias`. Parámetro `asignaciones` de tipo `json` con ejemplo en descripción. | Servidor y puente sincronizados con las 42 herramientas. |
| **2** | `ping` | ~20 ms | 0 / 0 / 0 / 7 | **OK** | `{"plugin": "ArbaMcp", "version": "1.3.2.0", "puerto": 8765, "dibujo": "D:\\CURSO CIVIL 3D\\Tarea 3\\PRUEBA_AISLAMIENTO_validacion130.dwg", "hay_dibujo": true, "hora": "2026-09-28 10:14:37", "ms": 0, "ms_espera": 0, "ms_ejecucion": 0, "ms_puente": 7}` | Conexión inmediata. Sobrecoste del puente: 7 ms. |
| **3** | Seguridad HTTP | ~150 ms | 1 / 0 / 1 / 14 (en `leer_historial`) | **OK** | `GET /ping` sin token: `{"ok":true,"servidor":"ArbaMcp","version":"1.3.2"}`.<br>`GET /tools`: `HTTP/1.1 401 Unauthorized`.<br>Token malo: `401 401 401`.<br>`leer_historial`: `"10:14:44  MCP 401 GET /tools: token ausente o distinto (los siguientes 401 de esta ruta en este minuto se agrupan)"`. | Agrupación por minuto de peticiones no autorizadas verificada. |
| **4** | `listar_corredores` y `listar_regiones` | ~100 ms | 14 / 0 / 14 / 27 | **OK** | `CORREDOR`: `Interseccion 3`. `LINEA_BASE`: `BL - Interseccion 3 (1) - SO - Quadrant - (80)`.<br>`REGION`: `RG - Acuerdo solo exterior - (99)` (`FT1`: 2).<br>`REGION2`: `RG - Acuerdo - (100)` (`FT2`: 2). | Sin errores de bloqueo de lectura ni excepciones. |
| **5** | `listar_objetivos` y `listar_superficies` | ~120 ms | 71 / 0 / 70 / 79 (objetivos)<br>15 / 0 / 15 / 30 (superficies) | **OK** | `SUB_A`: `Talud_derecha`, grupo `Derecha`, parámetro `TargetDTM`.<br>`ACTUAL`: `Topografia`.<br>`OTRA`: `Interseccion 3` (y `Subrasante` en paso 14). | Objetivos identificados en la primera región del corredor. |
| **6** | `asignar_objetivos` (simular=true) | ~60 ms | 33 / 0 / 32 / 45 | **OK** | `{"simulado": true, "herramienta": "asignar_objetivos", "accion": "Se aplicarían 3 asignaciones; 1 fallidas (de 4)", "plan": [{"indice": 0, ...}, ..., {"indice": 3, "error": "La línea base '...' no tiene una región 'region_que_no_existe'..."}], "fallidos": [{"indice": 3, ...}], "datos": {"total": 4, "aplicadas": 3, "fallidas": 1}}` | Sin copia de seguridad. El subpaso del menú previo no se realizó; comprobado posteriormente en diagnóstico exhaustivo. |
| **7** | `asignar_objetivos` (lote real) | ~230 ms | 207 / 15 / 192 / 219 | **OK** | `{"simulado": false, "herramienta": "asignar_objetivos", "mensaje": "3 asignaciones aplicadas, 1 fallidas (de 4)", "cambios": ["0.objetivos", "1.objetivos", "2.objetivos"], "copia": {"ruta": "D:\\...\\backups\\PRUEBA_AISLAMIENTO_validacion130_20260928_101624_asignar_objetivos.dwg", "metodo": "copia_de_disco", "reutilizada": false, "ms": 31, "espera_ms": 26, "refleja_guardado_de": "2026-09-27 10:59:58", "bytes": 70958262, "estado": "terminada"}}` | Exactamente 1 archivo nuevo en `backups\` y 1 línea nueva en `mcp_log.jsonl`. |
| **8** | `listar_objetivos` + menú Deshacer | ~80 ms | 5 / 0 / 4 / 12 | **OK** | `listar_objetivos`: `Talud_derecha` apunta a `Interseccion 3`.<br>Menú Deshacer en Civil 3D (captura literal de Andy):<br>1) `Executefunction`<br>2) `Grupo de comandos`<br>Al pie: `Deshacer 1 comando`. | Exactamente UNA sola entrada `Executefunction` para el lote del paso 7. Cero entradas por lecturas 4, 5 y 8, y cero por simulación 6.<br><br>*(Diagnóstico adicional de lecturas)*: Se ejecutaron sucesivamente `listar_superficies`, `listar_corredores`, `listar_regiones`, `listar_objetivos` y la simulación del paso 6; ninguna añadió entradas al menú Deshacer (permaneció invariable en 3 entradas en la sesión preliminar y 0 en la limpia). |
| **9** | `ejecutar_comando("_.UNDO 1")` + `listar_objetivos` | ~650 ms | 605 / null / null / 613 (comando)<br>4 / 0 / 4 / 11 (objetivos) | **FALLO** | `ejecutar_comando`: `"result": "terminado"`. Menú Deshacer: la entrada `Executefunction` se retiró. `listar_objetivos` devolvió `Interseccion 3`. | Antes de `_.UNDO 1` había cuatro entradas (`Executefunction` y tres `Grupo de comandos`); tras `_.UNDO 1` quedó una sola entrada `Grupo de comandos` y `Executefunction` desapareció. Sin embargo, al consultar con `listar_objetivos`, los objetivos se mantuvieron en `Interseccion 3` y no volvieron a `Topografia` (ACTUAL). |
| **9b** | Diagnóstico con la interfaz de Civil 3D | ~5 min | 6 / 0 / 5 / 15 (lectura)<br>169 / null / null / 177 (`_.UNDO 1`)<br>10 / 0 / 10 / 19 (lectura) | **Completado (Diagnóstico)** | Cambio manual desde Propiedades de corredor (`EDITCORRIDORPROPERTIES`): `Talud_derecha` cambiado a `Topografia`. Menú Deshacer: 1) `Editcorridorproperties`, 2) `Grupo de comandos`.<br>`listar_objetivos`: leyó inmediatamente `Topografia`.<br>`_.UNDO 1` retiró `Editcorridorproperties`, pero el objetivo permaneció en `Topografia`. | **Prueba A definitiva de Andy**: Desde la interfaz de Civil 3D puso `Talud_derecha` de `Topografia` a `Subrasante` (superficie válida no-corredor), pulsó `Ctrl+Z` varias veces en Civil 3D y el cuadro siguió mostrando `Subrasante`.<br><br>**Conclusión definitiva:** Civil 3D **no revierte por UNDO los cambios de objetivos**, ni por comando `_.UNDO` ni desde su propia interfaz gráfica con `Ctrl+Z`. La entrada de deshacer se retira pero el motor de Civil 3D no revierte el objetivo. El fallo de los pasos 9 y 14 no es del conector MCP ni del plugin, sino una limitación interna de Civil 3D.<br><br>**Hallazgo adicional:** `Interseccion 3` es la superficie generada por el propio corredor; el cuadro de Civil 3D no la ofrece como objetivo de superficie (para evitar dependencias circulares), pero la API la aceptó.<br><br>*(Restauración 1 a las 11:01:56)*: `asignar_objetivo` restauró `Topografia` (`ACTUAL`) tras quedar en `Subrasante` en la prueba manual. |
| **10** | `establecer_frecuencias` (simulado y real) | ~1.2 s | Simulado: 13 / 0 / 13 / 27<br>Real: 105 / 22 / 83 / 115<br>Lectura: 3 / 0 / 2 / 10 | **OK** | Simulado: plan de 2 con cambios `frecuencia_tangentes` de 2 a 3.<br>Real: `mensaje: "2 regiones aplicadas, 0 fallidas (de 2)"`, `copia.reutilizada: true`, `copia.ms: 0`, `copia.espera_ms: 0`. `listar_regiones` confirmó ambas regiones en 3. | Reutilización de copia confirmada (0 ms). Frecuencias de ambas regiones actualizadas correctamente. |
| **11** | `ejecutar_comando("_.UNDO 1")` + `listar_regiones` | ~150 ms | 117 / null / null / 125 (comando)<br>3 / 0 / 2 / 10 (regiones) | **OK** | `ejecutar_comando`: `"terminado"`. `listar_regiones` confirmó que ambas regiones volvieron a `frecuencia_tangentes: 2`. | `_.UNDO 1` revirtió el lote de frecuencias entero de una sola operación (las frecuencias sí se revierten por UNDO). |
| **12** | `establecer_frecuencias` (fallo parcial simular=true) | ~50 ms | 3 / 0 / 2 / 11 | **OK** | `{"simulado": true, "herramienta": "establecer_frecuencias", "accion": "Se aplicarían 1 regiones; 1 fallidas (de 2)", "plan": [{"indice": 0, "error": "La línea base '...' no tiene una región 'no_existe'..."}, {"indice": 1, ...}], "fallidos": [{"indice": 0, ...}], "datos": {"total": 2, "aplicadas": 1, "fallidas": 1}}` | Plan de 2 con índice 0 en fallidos e índice 1 válido. |
| **13** | Validación de errores sin tocar dibujo | ~1.5 s | 47 / 0 / 46 / 66 (en `leer_log`) | **OK** | 1. Sin tipo: `ArgumentException: asignaciones[0]: Falta el parámetro obligatorio 'tipo'.`<br>2. JSON inválido: `ArgumentException: El parámetro 'asignaciones' debe ser JSON válido...`<br>3. Lote 201: `ArgumentException: El lote tiene 201 elementos y el límite es 200; pásalo con forzar=true si es intencionado.`<br>4. Frecuencia 0: `ArgumentException: regiones[0]: La frecuencia 'frecuencia_tangentes' debe ser mayor que 0.`<br>`leer_log(ultimas_n=5)` registró las 4 llamadas con `ok: false`. | Rechazo inmediato de parámetros inválidos sin alterar el dibujo. |
| **14** | `asignar_objetivo` individual + `_.UNDO 1` | ~1.2 s | 58 / 20 / 38 / 88 (asignar)<br>114 / null / null / 120 (`_.UNDO 1`)<br>4 / 0 / 4 / 10 (listar) | **FALLO** | Siguiendo instrucción de Andy, se usó como OTRA la superficie `Subrasante` (no-corredor). `asignar_objetivo` OK (`despues.objetivos: ["superficie:Subrasante"]`, `copia.reutilizada: true`). Tras `_.UNDO 1`, `listar_objetivos` devolvió `Subrasante`. | `_.UNDO 1` no revirtió el objetivo individual por la limitación interna de Civil 3D demostrada en 9b.<br><br>*(Restauración 2 a las 11:10:33)*: `asignar_objetivo` restauró `Topografia` (`ACTUAL`) (`ms: 55`, `copia.reutilizada: true`). |
| **15** | `guardar_copia(sufijo="validacion_13")` | 3.88 s | 3872 / 20 / 3850 / 3882 | **OK** | `{"simulado": false, "herramienta": "guardar_copia", "copia": "D:\\CURSO CIVIL 3D\\Tarea 3\\backups\\PRUEBA_AISLAMIENTO_validacion130_20260928_111147_validacion_13.dwg", "bytes": 70989993, "copias_conservadas": 20}` | Guardado completo de copia de 71.0 MB en 3.85 s; rotación de 20 copias conservadas. |
| **16** | Copia de dibujo sin guardar | ~600 ms | 151 / 7 / 143 / 165 (copia)<br>444 / 0 / 443 / 458 (abrir) | **OK** | `_QNEW`: nuevo dibujo sin guardar.<br>`guardar_copia`: `copia: "...\\AppData\\Local\\Autodesk\\C3D 2027\\esp\\Template\\backups\\_Autodesk Civil 3D (Metric) NCS_20260928_111354_nuevo.dwg"`, `bytes: 954027`.<br>`abrir_dibujo`: `{"abierto": "D:\\...\\PRUEBA_AISLAMIENTO_validacion130.dwg", "ya_estaba_abierto": true}`. | Guardado bajo `%LOCALAPPDATA%` verificado. Ruta SaveAs de escrituras automáticas sobre dibujo sin guardar con corredor: no probado (dibujo vacío). |
| **17** | `probar_servidor.py --fase 13 --dwg` | 9.8 s | Script Python | **FALLO (1)** | `[FAL] tras _.UNDO 1 los 3 objetivos vuelven a su valor original (el lote es una sola entrada de deshacer)  -> [['Interseccion 3'], ['Interseccion 3'], ['Interseccion 3']]`<br>`=== Resultado: 1 fallo(s): tras _.UNDO 1 los 3 objetivos vuelven a su valor original (el lote es una sola entrada de deshacer) ===` | 28 de 29 pruebas superadas. La única comprobación fallida es el revertido de objetivos por `_.UNDO 1`.<br><br>*(Restauración 3 a las 11:17:47)*: `asignar_objetivo` restauró `Topografia` tras la ejecución del script (`ms: 71`). |
| **18** | `probar_servidor.py --dwg` (suite base) | 35 s | Script Python | **FALLO (1)** | `[FAL] tras _.UNDO 1 el objetivo vuelve al valor previo a la última escritura (la restauración se deshizo entera)  -> ['Topografia'] (esperado ['Interseccion 3'])`<br>`=== Resultado: 1 fallo(s): tras _.UNDO 1 el objetivo vuelve al valor previo a la última escritura (la restauración se deshizo entera) ===` | 33 de 34 pruebas superadas. En la 1.3.1 fallaban 3 pruebas; en la 1.3.2 las 2 comprobaciones de `/ping` público y reutilización de copia pasaron limpias `[OK]`. El único fallo restante es el revertido del objetivo por `_.UNDO 1`. |
| **19** | Menú Deshacer final (antes de cerrar) | ~1 min | Manual / Captura | **OK** | Captura literal de Andy: 1) `Intellizoom`, 2) `Grupo de comandos`, 3) `Viewcubeaction`, 4) `Intellizoom`, 5) `Grupo de comandos`, 6) `Regen`, 7) `Regen`, 8) `Executefunction`...<br>Al pie: `Deshacer 1 comando`. | Entradas `Executefunction` solo por escrituras reales y restauraciones. `Editcorridorproperties`/`Updatecorridor` por la prueba 9b. `Linea`/`Regen` por `probar_servidor.py`. Cero entradas espurias por lecturas o simulaciones. |
| **20** | `leer_historial(ultimas_n=60)` | ~50 ms | 1 / 0 / 0 / 12 | **OK** | Registros de `Copia de seguridad:` con tiempos (`SaveAs: 3848 ms`, `SaveAs: 141 ms`). Registros de `Escritura ...` con tiempos. Registros de `MCP 401 GET /tools` agrupados. Cero líneas de error en transacciones. | Trazabilidad completa y limpia. |
| **21** | Reconexión automática tras reinicio | ~15 s | Con Civil 3D cerrado: ms_puente=2046<br>Reabierto: 2 / 0 / 1 / 10 | **OK** | Con Civil 3D cerrado: `{"ok": false, "error": "Civil 3D no está abierto o ArbaMcp no cargó...", "ms_puente": 2046}`.<br>Con Civil 3D reabierto: `{"plugin": "ArbaMcp", "version": "1.3.2.0", ..., "ms": 2, "ms_espera": 0, "ms_ejecucion": 1, "ms_puente": 10}`. Salida del puente: sondeo `GET /ping "HTTP/1.1 200 OK"` y re-registro automático de herramientas. | Resiliencia y reconexión automática sin reiniciar el puente validada al 100%. |

---

## 2. Deshacer de Objetivos (Decisión de la 1.3.2)

### Resultados de los pasos 9 y 14
- **Paso 9 (`asignar_objetivos` lote + `_.UNDO 1`)**:
  - `ejecutar_comando("_.UNDO 1")`: `terminado` (`ms: 605`, `ms_puente: 613`).
  - La entrada `Executefunction` se retiró del menú Deshacer.
  - `listar_objetivos`: los objetivos permanecieron en `Interseccion 3` (`OTRA`) y **no volvieron** a `Topografia` (`ACTUAL`). Resultado: **FALLO**.
- **Paso 14 (`asignar_objetivo` individual + `_.UNDO 1`)**:
  - `asignar_objetivo` aplicó `Subrasante` (`OTRA`).
  - `ejecutar_comando("_.UNDO 1")`: `terminado` (`ms: 114`, `ms_puente: 120`).
  - `listar_objetivos`: el objetivo permaneció en `Subrasante` y **no volvió** a `Topografia` (`ACTUAL`). Resultado: **FALLO**.

### Diagnóstico 9b (con la interfaz gráfica de Civil 3D)
1. **Modificación desde la interfaz**: Se cambió `Talud_derecha` a `Topografia` desde **Propiedades de corredor** (`EDITCORRIDORPROPERTIES`).
2. **Entrada en Deshacer**: Apareció `Editcorridorproperties` y `Updatecorridor`.
3. **Comprobación de lectura**: `listar_objetivos` leyó inmediatamente `Topografia` (`ms: 6`, `ms_puente: 15`), confirmando que la lectura refleja con total fidelidad el estado del modelo.
4. **Deshacer por comando**: `_.UNDO 1` retiró `Editcorridorproperties`, pero `listar_objetivos` mantuvo `Topografia`.
5. **Prueba A definitiva de Andy (interfaz + `Ctrl+Z`)**:
   - Andy cambió manualmente `Talud_derecha` de `Topografia` a `Subrasante` desde Propiedades de corredor (un solo cambio, Aceptar).
   - Pulsó `Ctrl+Z` repetidas veces en Civil 3D.
   - El cuadro de Propiedades de corredor siguió mostrando `Subrasante`.

> [!IMPORTANT]
> **Conclusión definitiva del 9b:**
> Civil 3D **no deshace los cambios de objetivos ni desde su propia interfaz gráfica con Ctrl+Z**. La entrada del menú Deshacer se retira de la pila de AutoCAD, pero el motor de Civil 3D no revierte los objetivos de la región.
> **El fallo de los pasos 9 y 14 NO es del conector MCP ni del plugin `ArbaMcp`**, sino una limitación inherente del propio Autodesk Civil 3D.
> La escritura y restauración de `Description` en `Herramientas.RegistrarDeshacer` (1.3.2) no es suficiente porque la falta de revertido ocurre en el propio núcleo de Civil 3D.
> **Orientación para la versión 1.3.3:** La sesión de desarrollo implementará un mecanismo propio de deshacer de objetivos a nivel de conector.

### Hallazgo adicional
`Interseccion 3` es la superficie generada por el propio corredor. El cuadro interactivo de Civil 3D la excluye de la lista de objetivos para evitar referencias circulares; sin embargo, la API de Civil 3D y el plugin la aceptan y aplican sin error. En el paso 14 se utilizó `Subrasante` (superficie TIN independiente) confirmando idéntico comportamiento de no revertido.

---

## 3. Registro de Restauraciones

Se realizaron tres escrituras de restauración fuera del flujo de prueba automática para asegurar que el dibujo mantuviera su estado original (`Topografia` en `Talud_derecha`):

| Hora | Herramienta | Motivo |
|---|---|---|
| **11:01:56** | `asignar_objetivo` | Restauración de `Talud_derecha` a `Topografia` tras la prueba manual en la interfaz de Civil 3D del diagnóstico 9b (que lo había dejado en `Subrasante`). |
| **11:10:33** | `asignar_objetivo` | Restauración de `Talud_derecha` a `Topografia` tras el fallo de revertido de `_.UNDO 1` en el paso 14 (había quedado en `Subrasante`). |
| **11:17:47** | `asignar_objetivo` | Restauración de `Talud_derecha` a `Topografia` tras la ejecución de `probar_servidor.py --fase 13` (había quedado en `Interseccion 3`). |

Todas las restauraciones utilizaron `copia.reutilizada: true` (`copia.ms: 0`, `copia.espera_ms: 0`).

---

## 4. Análisis de Copia de Disco

| Parámetro | Paso 7 (`asignar_objetivos`) | Paso 10 (`establecer_frecuencias`) | Paso 14 (`asignar_objetivo`) |
|---|---|---|---|
| `copia.ms` | **31 ms** | **0 ms** | **0 ms** |
| `copia.espera_ms` | **26 ms** | **0 ms** | **0 ms** |
| `copia.reutilizada` | `false` (primera escritura) | `true` (reutilizada) | `true` (reutilizada) |
| `copia.refleja_guardado_de` | 2026-09-27 10:59:58 | 2026-09-27 10:59:58 | 2026-09-27 10:59:58 |
| `copia.estado` | `terminada` | `terminada` | `terminada` |
| `bytes` | 70,958,262 B (~70.9 MB) | 70,958,262 B (~70.9 MB) | 70,958,262 B (~70.9 MB) |

- Tamaño del archivo original en disco: **70,958,262 bytes** (70.9 MB).
- Tamaño del backup de disco: **70,958,262 bytes** (copia bit a bit exacta).
- Tamaño de `guardar_copia` (SaveAs en memoria, paso 15): **70,989,993 bytes**.
- En escrituras con `reutilizada: true`, el tiempo de copia de seguridad es de **0 ms**.

---

## 5. Menú Deshacer

- **Paso 8 (tras lote del paso 7)**:
  1) `Executefunction`
  2) `Grupo de comandos`
  Al pie: `Deshacer 1 comando`.
  Exactamente una entrada para el lote completo. Cero entradas por lecturas o simulaciones.
- **Diagnóstico del Paso 8 (5 llamadas de lectura/simulación)**:
  - `listar_superficies`: 0 entradas añadidas.
  - `listar_corredores`: 0 entradas añadidas.
  - `listar_regiones`: 0 entradas añadidas.
  - `listar_objetivos`: 0 entradas añadidas.
  - `asignar_objetivos(simular=true)`: 0 entradas añadidas.
  El bloqueo `DocumentLockMode.Read` funciona al 100% en 1.3.2.
- **Paso 10 (frecuencias)**:
  Se añadió exactamente una sola entrada `Executefunction`.
- **Paso 11 (`_.UNDO 1` en frecuencias)**:
  Revirtió el lote de frecuencias entero de ambas regiones de 3 a 2 de una sola operación.
- **Paso 19 (menú Deshacer final antes de cerrar Civil 3D)**:
  Entradas de arriba a abajo:
  1) `Intellizoom`
  2) `Grupo de comandos`
  3) `Viewcubeaction`
  4) `Intellizoom`
  5) `Grupo de comandos`
  6) `Regen`
  7) `Regen`
  8) `Executefunction`
  Seguidas de las entradas `Executefunction` de las escrituras reales y restauraciones, `Editcorridorproperties`/`Updatecorridor` de la prueba manual 9b, `Rnuevo` del paso 16 y `Linea`/`Regen` de los scripts de prueba. Cero entradas por lecturas o simulaciones.

---

## 6. Miembros de la API Evaluados

Para actualización en `herramientas-dev/miembros_por_verificar_civil3d.md`:

| Miembro / Característica | Estado Observado |
|---|---|
| `Herramientas.RegistrarDeshacer` (escritura y restauración de `Corridor.Description` antes de `SetTargets`) | **Ejecutado**: La técnica no provoca errores y se ejecuta dentro de la misma transacción, pero **no consigue que AutoCAD revierta los objetivos** tras `_.UNDO 1` ni `Ctrl+Z`, debido a que el motor interno de Civil 3D no revierte los targets de las regiones (demostrado en diagnóstico 9b). |
| `BaselineRegion.SetTargets` | **Ejecutado**: Aplica los cambios de objetivos correctamente (confirmado en lote e individual). La lectura los refleja inmediatamente. `_.UNDO 1` retira la entrada del menú pero Civil 3D no revierte los objetivos (comportamiento idéntico al de la propia interfaz gráfica). |
| `BaselineRegion.FrequencyAlongTangents` | **Ejecutado**: Se aplica en lote correctamente y **`_.UNDO 1` lo revierte al valor original** de forma íntegra. |
| `DocumentLockMode.Read` en lecturas y simulaciones | **Ejecutado**: Elimina por completo las entradas espurias (`Grupo de comandos` y `Executefunction`) en el menú Deshacer. |

---

## 7. Tiempos Comparados

### Espera frente a Ejecución
- **Lectura (`listar_corredores`, paso 4)**:
  - `ms_espera`: **0 ms**
  - `ms_ejecucion`: **14 ms**
  - `ms`: **14 ms**
- **Escritura en lote (`asignar_objetivos`, paso 7)**:
  - `ms_espera`: **15 ms** (bloqueo y preparación)
  - `ms_ejecucion`: **192 ms** (incluye copia de disco en segundo plano y aplicación de 3 objetivos)
  - `ms`: **207 ms**

### Sobrecoste del Puente (`ms_puente` vs `ms`)
| Llamada | `ms` (Civil 3D) | `ms_puente` | Sobrecoste (`ms_puente` - `ms`) |
|---|---|---|---|
| `ping` (Paso 2) | 0 ms | 7 ms | **+7 ms** |
| `listar_objetivos` (Paso 8) | 4 ms | 12 ms | **+8 ms** |
| `asignar_objetivos` (Paso 7) | 207 ms | 219 ms | **+12 ms** |

Sobrecoste promedio del puente MCP: **~7-10 ms**, completamente imperceptible para el usuario.

---

## 8. Resultados de la Suite Automatizada

- **`probar_servidor.py --fase 13 --dwg`**:
  - Resultado: **28 / 29 pruebas OK** (1 fallo).
  - Fallo:
    ```
    [FAL] tras _.UNDO 1 los 3 objetivos vuelven a su valor original (el lote es una sola entrada de deshacer)  -> [['Interseccion 3'], ['Interseccion 3'], ['Interseccion 3']]
    ```
- **`probar_servidor.py --dwg` (suite base)**:
  - Resultado: **33 / 34 pruebas OK** (1 fallo).
  - Fallo:
    ```
    [FAL] tras _.UNDO 1 el objetivo vuelve al valor previo a la última escritura (la restauración se deshizo entera)  -> ['Topografia'] (esperado ['Interseccion 3'])
    ```
  - Las comprobaciones de seguridad `/ping` público (200 OK) y reutilización de backup (`reutilizada: true`) pasaron al 100% limpias `[OK]`.
