# Prompt de validación de la 1.3.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D (Antigravity, Claude Desktop, Claude
Code, Cursor...). Requisitos previos:

- Civil 3D 2027 abierto con **una copia** del DWG de prueba (nunca la entrega original): un dibujo guardado en disco
  con al menos un corredor cuya primera región tenga objetivos de superficie, y con al menos dos superficies.
- Plugin **1.3.0** instalado (`instalar.ps1` con Civil 3D cerrado; el bundle lleva `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`).
  `VALIDACION_122.md` debe haberse ejecutado antes (o al menos su paso 5: `HiloPrincipal listo`).
- Puente reiniciado con el `main.py` de 1.3.0 (`.venv\Scripts\python main.py` en `PuenteMcp`) y el cliente MCP
  reiniciado para que cargue la lista nueva de 42 herramientas.
- El informe de este prompt es el que actualiza `herramientas-dev/miembros_por_verificar_civil3d.md`.

---

Eres el agente de validación de la versión 1.3.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso** (desde que decides llamar
hasta que tienes la respuesta; anota también `ms`, `ms_espera`, `ms_ejecucion` y `ms_puente` de la respuesta).

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- **No deduzcas causas que no comprobaste.** Si un paso falla, pega el error tal cual y sigue con el siguiente.
- **No muestres el token** (`%LOCALAPPDATA%\ArbaMcp\token`) en ningún sitio; en las salidas sustitúyelo por `<oculto>`.
- No corrijas nada por tu cuenta, no reintentes una escritura que devolvió "no refleja el cambio" y no ejecutes
  Deshacer en Civil 3D salvo donde el paso lo diga.
- Toma los nombres reales (corredor, línea base, región, subensamblajes, superficies) de las respuestas de los pasos
  4 y 5, no los de los ejemplos.

## Pasos

1. **(1 min)** Lista las herramientas que te ofrece el servidor → esperado: **42** nombres, entre ellos
   `asignar_objetivos` y `establecer_frecuencias` (nuevas) y todas las de la 1.2.2 (`asignar_objetivo`,
   `establecer_frecuencia`, `ping`, `leer_historial`...). En `asignar_objetivos` el parámetro `asignaciones` es de
   tipo texto (`string`) y su descripción incluye un ejemplo JSON. Pega la lista de nombres.
2. **(1 min)** `ping` sin argumentos → esperado: `plugin: "ArbaMcp"`, `version: "1.3.0.0"`, `hay_dibujo: true`, y en
   la misma respuesta `ms`, `ms_espera`, `ms_ejecucion` y `ms_puente` (enteros). Anota los cuatro.
3. **(2 min)** En PowerShell:
   ```powershell
   curl.exe -s http://127.0.0.1:8765/ping
   curl.exe -s -i http://127.0.0.1:8765/tools | Select-Object -First 1
   1..3 | ForEach-Object { curl.exe -s -o NUL -w "%{http_code} " -H "X-Arba-Token: malo" http://127.0.0.1:8765/tools }
   ```
   → esperado, literal: `{"ok":true,"servidor":"ArbaMcp","version":"1.3.0"}` (sin token y sin datos del dibujo);
   `HTTP/1.1 401 Unauthorized`; `401 401 401`. Después `leer_historial(ultimas_n=30)` → esperado: **una sola** línea
   `MCP 401 GET /tools: token ausente o distinto (los siguientes 401 de esta ruta en este minuto se agrupan)` para
   ese minuto, no tres. Pega las líneas `MCP 401`.
4. **(1 min)** `listar_corredores` → guarda como `CORREDOR` el nombre del primero y como `LINEA_BASE` su primera
   línea base. `listar_regiones(corredor=CORREDOR)` → guarda `REGION` (el `nombre` de la primera región), `REGION2`
   (la segunda, si existe) y las `frecuencia_tangentes` de ambas como `FT1` y `FT2`.
5. **(1 min)** `listar_objetivos(corredor=CORREDOR, linea_base=LINEA_BASE, region=REGION)` → guarda los tres primeros
   objetivos de `tipo: "superficie"` como `SUB_A`, `SUB_B`, `SUB_C` (`subensamblaje`, y `grupo`/`parametro` si vienen;
   si hay menos de tres, repite el primero) y el nombre de su superficie actual como `ACTUAL`.
   `listar_superficies` → guarda como `OTRA` una superficie distinta de `ACTUAL`.
6. **(1 min)** `asignar_objetivos(corredor=CORREDOR, linea_base=LINEA_BASE, simular=true, asignaciones="[...]")`
   con cuatro elementos en el texto JSON: los tres de `SUB_A`, `SUB_B`, `SUB_C` con
   `{"region": REGION, "subensamblaje": ..., "tipo": "superficie", "objetivo": OTRA}` (añade `grupo` y `parametro`
   si el paso 5 los dio) y un cuarto igual al primero pero con `"region": "region_que_no_existe"` → esperado:
   `simulado: true`, `plan` con 4 entradas (índices 0 a 3; la 3 con `error` que nombra la región inexistente y lista
   las regiones), `fallidos` con solo el índice 3, `datos: {total: 4, aplicadas: 3, fallidas: 1}`, sin `copia`,
   `antes`/`despues` por índice con `objetivos`. Anota `ms` y `ms_puente`.
7. **(2 min)** El paso 6 sin `simular` → esperado: `simulado: false`, `mensaje: "3 asignaciones aplicadas, 1 fallidas
   (de 4)"`, `datos: {total: 4, aplicadas: 3, fallidas: 1}`, `fallidos` con el índice 3, `cambios` con `0.objetivos`,
   `1.objetivos`, `2.objetivos`, y `copia` como **objeto** con `ruta` (en `backups\`), `metodo: "copia_de_disco"`,
   `reutilizada` (false si es la primera escritura sobre este archivo desde que se abrió; true si ya hubo una),
   `ms`, `espera_ms`, `refleja_guardado_de` (fecha del último guardado del .dwg), `estado: "terminada"` y `nota`
   con "refleja el último guardado en disco". Comprueba en PowerShell que `backups\` tiene **como máximo un archivo
   nuevo** y que `mcp_log.jsonl` tiene **una** línea nueva (`herramienta: "asignar_objetivos"`). Anota `ms`,
   `ms_espera`, `ms_ejecucion`, `ms_puente`, `copia.ms` y `copia.espera_ms`.
8. **(1 min)** `listar_objetivos(...)` como en el paso 5 → esperado: `SUB_A`, `SUB_B` y `SUB_C` apuntan a `OTRA`.
   En Civil 3D, despliega la flecha del botón **Deshacer** de la barra de acceso rápido y anota literalmente las 3
   últimas entradas → esperado: **una sola** entrada para el lote del paso 7 (no una por asignación ni por
   transacción). No ejecutes Deshacer desde el menú.
9. **(1 min)** `ejecutar_comando(comando="_.UNDO 1", timeout_s=30)` → esperado: `terminado`. Después
   `listar_objetivos(...)` → esperado: los tres objetivos vuelven a `ACTUAL` (el lote entero se deshizo con una
   sola operación). Si solo vuelve uno, anótalo: significa que el lote no quedó como una entrada.
10. **(2 min)** `establecer_frecuencias(corredor=CORREDOR, linea_base=LINEA_BASE, simular=true, regiones="[...]")` con
    dos elementos: `{"region": REGION, "tangentes": FT1+1}` y `{"region": REGION2, "tangentes": FT2+1}` (si no hay
    `REGION2`, repite `REGION`) → esperado: `simulado: true`, `plan` de 2 con `cambios: ["frecuencia_tangentes"]` y
    `antes`/`despues`. Sin `simular` → esperado: `datos.aplicadas: 2`, `copia.reutilizada: true` (segunda escritura
    sin guardar el dibujo entre medias), `copia.espera_ms` entero (normalmente 0), `copia.ms: 0`.
    `listar_regiones(corredor=CORREDOR)` → esperado: las dos regiones con `frecuencia_tangentes` = valor +1. Anota la
    entrada nueva del menú Deshacer (una sola).
11. **(1 min)** `ejecutar_comando(comando="_.UNDO 1", timeout_s=30)` → `terminado`; `listar_regiones` → las
    frecuencias vuelven a `FT1` y `FT2`.
12. **(1 min)** `establecer_frecuencias(corredor=CORREDOR, regiones="[{\"region\": \"no_existe\", \"tangentes\": 10},
    {\"region\": REGION, \"linea_base\": LINEA_BASE, \"tangentes\": FT1}]", simular=true)` → esperado: `plan` de 2 con
    el índice 0 en `fallidos` (región inexistente) y el 1 válido; `datos: {total: 2, aplicadas: 1, fallidas: 1}`.
13. **(1 min)** Errores de validación, todos **sin** tocar el dibujo:
    - `asignar_objetivos(corredor=CORREDOR, linea_base=LINEA_BASE, asignaciones="[{\"region\": REGION,
      \"subensamblaje\": \"x\"}]")` → esperado: error que empieza por `asignaciones[0]:` y nombra `'tipo'`.
    - `asignar_objetivos(..., asignaciones="[{")` → esperado: error con `debe ser JSON válido`.
    - `asignar_objetivos(..., simular=true, asignaciones=<201 copias del elemento de SUB_A>)` → esperado: error
      `El lote tiene 201 elementos y el límite es 200; pásalo con forzar=true si es intencionado`.
    - `establecer_frecuencias(corredor=CORREDOR, regiones="[{\"region\": REGION, \"linea_base\": LINEA_BASE,
      \"tangentes\": 0}]")` → esperado: error `regiones[0]: La frecuencia 'frecuencia_tangentes' debe ser mayor que 0.`
    Comprueba con `leer_log(ultimas_n=5)` que cada una dejó una línea con `ok: false`.
14. **(2 min)** `asignar_objetivo(corredor=CORREDOR, linea_base=LINEA_BASE, region=REGION, subensamblaje=SUB_A,
    tipo="superficie", objetivo=OTRA)` (herramienta individual de la 1.2.x) → esperado: `simulado: false`,
    `despues.objetivos` con `OTRA`, `copia` como objeto (`reutilizada: true`), `avisos: null` (si hay un aviso
    "Sin marca de deshacer" o "No se pudo abrir la marca de deshacer", pégalo: es el dato clave para
    `Document.StartUndoMark`). `ejecutar_comando("_.UNDO 1")` → `listar_objetivos` vuelve a `ACTUAL`.
15. **(1 min)** `guardar_copia(sufijo="validacion_13")` → esperado: `copia` en `backups\` con `_validacion_13.dwg`
    (sin cambios respecto a 1.2.x) y `copias_conservadas: 20`.
16. **(3 min)** Copia de un dibujo **sin guardar**: en Civil 3D crea un dibujo nuevo (`_.NEW`, plantilla por defecto) y
    déjalo activo; `guardar_copia(sufijo="nuevo")` → esperado: `copia` bajo `%LOCALAPPDATA%\ArbaMcp\backups\`.
    Vuelve a activar la copia del DWG de prueba (`abrir_dibujo(ruta=...)` → `ya_estaba_abierto: true`). (La ruta
    SaveAs de las escrituras automáticas sobre un dibujo sin guardar no se puede probar sin un corredor: anótalo
    como "no probado" salvo que tengas un dibujo sin guardar con corredor.)
17. **(4 min)** En una consola, desde el clon: `.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py
    --fase 13 --dwg "<ruta de la copia>"` → esperado: `=== Resultado: TODO OK ===` (salida 0). Pega la salida entera
    con el token sustituido. Las líneas `[FAL]` van al informe con su `->`.
18. **(3 min)** `.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py --dwg "<ruta de la copia>"`
    (las pruebas de la 1.2.2, que no deben dejar de pasar) → esperado: `TODO OK`, incluida la prueba 6
    (`_.UNDO 1 revierte la última escritura entera`) y la de "Civil 3D ocupado".
19. **(4 min)** Arranque de Civil 3D con el puente en marcha: sin cerrar el puente, cierra Civil 3D, espera 10 s,
    llama a `ping` → esperado: `ok: false` con `Civil 3D no está abierto o ArbaMcp no cargó` y `ms_puente`. Abre
    Civil 3D de nuevo (menú Inicio), espera a que cargue el dibujo, llama a `ping` **sin reiniciar el puente** →
    esperado: `version: "1.3.0.0"` (el puente sondeó `/ping`, releyó el token y volvió a registrar las herramientas).
    En el archivo de salida del puente (o su consola) pega las líneas que mencionen `/ping` o el token.
20. **(1 min)** `leer_historial(ultimas_n=60)` → pega las líneas `Copia de seguridad:` (con `ms`), `Escritura ...`,
    `MCP 401` y cualquier `StartUndoMark`/`EndUndoMark falló`.
21. **(2 min)** Menú Deshacer final: despliega la lista y pega literalmente las 8 últimas entradas. Esperado: una
    entrada por cada escritura real de los pasos 7, 10 y 14 (no varias por lote) y ninguna entrada suelta por
    `listar_*`.

## Informe

Entrega una tabla con una fila por paso:

| Paso | Herramienta | Tiempo total del paso | `ms` / `ms_espera` / `ms_ejecucion` / `ms_puente` | Resultado (OK / FALLO / no probado) | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|---|

Y debajo:

- **Copia de disco**: `copia.ms`, `copia.espera_ms`, `copia.reutilizada` y `copia.refleja_guardado_de` de los pasos 7,
  10 y 14; si `copia.estado` fue `error`, el mensaje literal. Tamaño del `.dwg` y del archivo de `backups\`.
- **Deshacer**: las entradas literales de los pasos 8, 10 y 21 y si `_.UNDO 1` revirtió el lote entero (pasos 9 y 11).
  Cualquier aviso `Sin marca de deshacer` / `No se pudo abrir la marca de deshacer` de las respuestas.
- **Miembros de la API que fallaron** (nombre del miembro, mensaje de error literal), para actualizar
  `herramientas-dev/miembros_por_verificar_civil3d.md`. En 1.3.0 están `por verificar`: `Document.StartUndoMark` /
  `EndUndoMark` (por reflexión), `File.Copy` del .dwg desde un hilo aparte mientras Civil 3D tiene el dibujo abierto,
  `FileInfo.LastWriteTimeUtc`/`Length` del .dwg abierto, `DocumentCollection.ExecuteInCommandContextAsync`,
  `Dispatcher` del hilo principal, `Application.MainWindow.Handle` + `IsWindowEnabled`, `GetSystemVariable("CMDACTIVE")`,
  `Document.SendStringToExecute` por la cola Inmediato, `BaselineRegion.GetTargets/SetTargets` en lote,
  `AppliedAssemblySetting.FrequencyAlongTangents` en lote.
- **Tiempos comparados**: `ms_espera` frente a `ms_ejecucion` en una lectura y en el lote del paso 7; `ms_puente`
  menos `ms` (sobrecoste del puente) en tres llamadas.
- Versión de Civil 3D e idioma, versión de `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll` instaladas (`FileVersion`), y el
  resultado de `probar_servidor.py --fase 13` y del `--dwg` de la 1.2.2 (`TODO OK` o la lista de fallos).
