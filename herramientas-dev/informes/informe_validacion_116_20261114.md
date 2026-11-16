# Informe de Validación 1.16.0 (en rama `tarea/ronda-b`, v1.17.4)

**Fecha:** 2026-11-14  
**Rama / Commit:** `tarea/ronda-b` (`b0287f8`)  
**Versión de `ping`:** `1.17.4`  
**Civil 3D:** 2027 (`C:\Program Files\Autodesk\AutoCAD 2027`)  
**DWG usado:** `C:\Proyectos\dibujos_prueba\PRUEBA_116.dwg`  
**Variable `ARBA_MCP_CODIGO`:** `1` (User, conservada en `1` al finalizar)  

---

## 1. Bundle y DLLs de Roslyn (Paso 0)

- Bundle en `%APPDATA%\Autodesk\ApplicationPlugins\ArbaMcp.bundle\Contents`: `ArbaMcp.dll`, `ArbaMcp.Nucleo.dll`, sus `.pdb`, y `roslyn\Microsoft.CodeAnalysis.dll`, `roslyn\Microsoft.CodeAnalysis.CSharp.dll` (`5.0.25.56712`), `roslyn\es\*.resources.dll`. Ninguna `Microsoft.CodeAnalysis*.dll` suelta en `Contents\`.
- Herramientas MCP `civil3d`: **58** (55 del plugin + `buscar_api`, `guardar_receta`, `ejecutar_receta`).

---

## 2. Tabla de Resultados por Paso (Pasos 0 y 4–12)

*(Los pasos 1–3 ya pasaron en `1.17.2` y `ARBA_MCP_CODIGO=1` ya estaba activa).*

| Paso | Herramienta | Resultado | `ms_puente` | Resumen / Respuesta literal |
|---|---|---|---|---|
| **0** | `ping` | **OK** | `23 ms` | `{"plugin":"ArbaMcp","version":"1.17.4","dibujo":"...PRUEBA_116.dwg","hay_dibujo":true}` |
| **4a** | `ejecutar_codigo` (sim) | **OK** | `2787 ms` | `simulado: true`, `resultado: 0`, `copia.reutilizada: false` (`PRUEBA_116_20261114_222811_ejecutar_codigo.dwg`), `compilacion: {ms:2541, de_cache:false, roslyn:"5.0.0.0"}`, `eventos_bd: "vigilados"` |
| **4b** | `ejecutar_codigo` (sim) | **OK (con hallazgo)** | `536 ms` | `simulado: true`, `resultado: {"largo":100,"capa":"0"}`, `mensajes: ["línea de prueba"]`, `compilacion: {ms:471, de_cache:false, roslyn:"5.0.0.0"}`, `eventos_bd: "vigilados"`. **Hallazgo:** `despues.objetos_creados: 0` y `datos.objetos.creados: []` |
| **4c** | `ejecutar_codigo` (sim) | **OK** | `60 ms` | `simulado: true`, `resultado: 0`, `compilacion: {ms:0, de_cache:true, roslyn:"5.0.0.0"}` |
| **5a** | `ejecutar_codigo` (real) | **OK (con hallazgo)** | `56 ms` | `simulado: false`, `resultado: {"largo":100,"capa":"0"}`, `mensajes: ["línea de prueba"]`, `leer_log` confirma `ok: true, simular: false`. **Hallazgo:** `mensaje` dice `0 objeto(s) creado(s)` y `despues.objetos_creados: 0` |
| **5b** | `ejecutar_comando` | **OK** | `272 ms` | `_.UNDO 1` → `{"result": "terminado"}` |
| **5c** | `ejecutar_codigo` (sim) | **HALLAZGO** | `835 ms` | `resultado: 1` (N0 + 1; un solo `_.UNDO 1` no revirtió la línea de 5a — probablemente por la `Executefunction` de la simulación o contexto de comando) |
| **6** | `ejecutar_codigo` (red) | **OK** | `426 ms` | `ok: false`, `3 usos no permitidos` (`[bloqueado: red]` x2, `[bloqueado: hilos]` x1). `leer_historial` muestra `ejecutar_codigo: no compila (2 líneas, 415 ms)` |
| **7a** | `ejecutar_codigo` (proc) | **OK** | `329 ms` | `ok: false`, `[bloqueado: procesos] System.Diagnostics.Process` |
| **7b** | `ejecutar_codigo` (file out) | **OK** | `295 ms` | `ok: false`, `[bloqueado: archivos] System.IO.File`, `Test-Path C:\Proyectos\fuera.txt` → `False` |
| **7c** | `ejecutar_codigo` (file sim) | **OK** | `389 ms` | `no_escritos_por_simulacion: ["prueba116_sim.txt"]`, `Test-Path` → `False` |
| **7d** | `ejecutar_codigo` (file real) | **OK** | `406 ms` | `escritos: ["prueba116.txt"]`, `resultado: ["prueba116.txt"]`, `Get-Content` → `hola` |
| **8** | `ejecutar_codigo` (sintaxis) | **OK** | `35 ms` | `ok: false`, `2 error(es) de compilación`: `CS1002 Se esperaba ;` y `CS0103 El nombre 'y' no existe en el contexto actual.` con `^` y `Pista:` |
| **9** | `ejecutar_codigo` (timeout) | **OK** | `60086 ms` | `ok: false`, `TimeoutException: El script superó el tiempo límite de 60 s y se detuvo en la línea 4... No se aplicó nada: la transacción se abortó (era una simulación).` |
| **9+** | `ping`, `listar_superficies`, conteo, `ping` (+30 s) | **OK** | `7`, `12`, `33`, `7 ms` | Todos respondieron rápido; a los +30 s `ping` dio `hay_dibujo: true`. Civil 3D abierto y estable |
| **10** | `ejecutar_codigo` (runtime) | **OK** | `396 ms` | `ok: false`, `InvalidOperationException: El script falló en la línea 1: ArgumentException: No existe el alineamiento 'NO_EXISTE'. Usa listar_alineamientos. No se aplicó nada: la transacción se abortó (era una simulación).` |
| **10+** | `ping` (+30 s) | **OK** | `8 ms` | `hay_dibujo: true`. Civil 3D abierto y estable |
| **11a** | `ejecutar_codigo` (params) | **OK** | `525 ms` | `resultado: {"capa": "0", "doble": 100, "directo": "0"}` |
| **11b** | `ejecutar_codigo` (params err) | **OK** | `7 ms` | `ok: false`, `ArgumentException: El parámetro 'parametros' debe ser un objeto JSON... llegó un valor de tipo Array.` |
| **12** | `leer_log` / `leer_historial` | **OK** | `—` | `20` llamadas a `ejecutar_codigo` en las últimas 40 líneas de `mcp_log.jsonl`; `ARBA_MCP_CODIGO=1` |

---

## 3. `backups\`, Compilación y Eventos

- **`backups\`:** `12` archivos antes del paso 4 → `13` archivos después del paso 4a (se creó `PRUEBA_116_20261114_222811_ejecutar_codigo.dwg` y se reutilizó en los pasos siguientes; pasos 6, 7a, 7b y 8 no crearon copias).
- **Primera ejecución (`4a`):** `roslyn: "5.0.0.0"`, `compilacion.ms: 2541`, `de_cache: false`, `eventos_bd: "vigilados"`.
- **Ejecución repetida (`4c`):** `roslyn: "5.0.0.0"`, `compilacion.ms: 0`, `de_cache: true`, `eventos_bd: "vigilados"`.

---

## 4. Miembros de la API (Sección 1.16.0 de `miembros_por_verificar_civil3d.md`)

- **Pasan a `ejecutado en Civil 3D 2027` (2026-11-14):**
  - Roslyn (`Microsoft.CodeAnalysis.CSharp 5.0.0.0`) dentro de Civil 3D 2027 con referencias de `CodigoReferencias` (`1.17.4`): compilación y caché correctas.
  - `TransactionManager.TopTransaction` y aborto/cierre de transacción en simulación, error en tiempo de ejecución y timeout.
  - Eventos `Database.ObjectAppended`, `ObjectModified`, `ObjectErased`: reportados como `eventos_bd: "vigilados"`.
- **Pendiente de revisión (hallazgo):**
  - `DBObject.IsNewObject` / `Transaction.GetAllObjects()` / captura de `objetos.creados`: tanto en simulación (`4b`) como en ejecución real (`5a`), `despues.objetos_creados` devolvió `0` aunque `Line` sí se añadió al espacio modelo (verificado en `5c` con `resultado: 1`).
  - `_.UNDO 1` tras `5a` dejó `resultado: 1` en `5c`.
