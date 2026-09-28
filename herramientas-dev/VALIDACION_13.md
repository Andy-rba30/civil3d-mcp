# Prompt de validación de la 1.3.3 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D (Antigravity, Claude Desktop, Claude
Code, Cursor...). Requisitos previos:

- Civil 3D 2027 abierto con **una copia** del DWG de prueba (nunca la entrega original): un dibujo guardado en disco
  con al menos un corredor cuya primera región tenga objetivos de superficie, y con al menos dos superficies que **no**
  genere ningún corredor.
- `VALIDACION_122.md` ejecutada (informe del 28/09/2026: 1.2.2 validada) y la 1.3.1 y la 1.3.2 validadas con este
  mismo prompt (informes en `herramientas-dev/informes/`).
- El plugin **1.3.3** se instala en el paso 0 desde la rama `claude/vibrant-ptolemy-79gan9` (PR hacia
  `feature/proceso-1.3`; si el PR ya está fusionado, sirve `feature/proceso-1.3`); el bundle queda con `ArbaMcp.dll` y
  `ArbaMcp.Nucleo.dll`.
- Puente reiniciado con el `main.py` de 1.3.3 y el cliente MCP reiniciado para que cargue la lista de **43**
  herramientas (también en el paso 0).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- Lo que ya se sabe (validado el 28/09/2026 con la 1.3.1 y la 1.3.2): cada escritura deja **una** entrada
  `Executefunction` en el menú Deshacer; las lecturas y las simulaciones no dejan ninguna; los lotes son una sola
  entrada; la copia de disco se reutiliza; el puente se reconecta solo tras reabrir Civil 3D; `_.UNDO 1` revierte las
  frecuencias. **Y Civil 3D no deshace los cambios de objetivos**: ni tras una escritura del plugin ni tras cambiarlos
  en Propiedades de corredor, `_.UNDO 1` y Ctrl+Z retiran la entrada del menú y el objetivo sigue en el valor nuevo
  (1.3.2, pasos 9 y 9b). También se vio que el plugin aceptaba como objetivo la superficie generada por el propio
  corredor, que Civil 3D no ofrece.
- **Lo que trae la 1.3.3**: cada escritura de objetivos guarda en memoria lo que había antes y devuelve un bloque
  `restaurar`; la herramienta nueva **`deshacer_objetivos`** lo reaplica (comprobando que nadie cambió el objetivo
  después); el plugin rechaza como objetivo una superficie generada por el mismo corredor; `probar_servidor.py` ya no
  espera que `_.UNDO 1` devuelva objetivos y nunca elige una superficie de corredor. **Los pasos 9, 9b y 14 son los
  decisivos.**
- Si ya ejecutaste este prompt con la 1.3.2, vuelve a empezar desde el paso 0 con la 1.3.3.

---

Eres el agente de validación de la versión 1.3.3 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso** (desde que decides llamar
hasta que tienes la respuesta; anota también `ms`, `ms_espera`, `ms_ejecucion` y `ms_puente` de la respuesta).

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- **No deduzcas causas que no comprobaste.** Si un paso falla, pega el error tal cual y sigue con el siguiente.
- **No muestres el token** (`%LOCALAPPDATA%\ArbaMcp\token`) en ningún sitio; en las salidas sustitúyelo por `<oculto>`.
- No corrijas nada por tu cuenta, no reintentes una escritura que devolvió "no refleja el cambio" y no ejecutes
  Deshacer en Civil 3D salvo donde el paso lo diga. Si un paso deja el dibujo en un estado distinto del esperado, solo
  lo restauras donde el paso lo indique, y cada restauración se anota en Observaciones (hora y herramienta).
- Cuando un paso pide mirar el menú Deshacer, pídeselo a la persona que tiene Civil 3D delante y anota literalmente
  lo que te diga. No edites archivos del repositorio salvo tu informe y no hagas commits.
- Toma los nombres reales (corredor, línea base, región, subensamblajes, superficies) de las respuestas de los pasos
  4 y 5, no los de los ejemplos.

## Pasos

0. **(6 min) Instalar la 1.3.3.** Reglas de `VALIDACION_122.md` (no muestres el token, sin commits, sin tocar código).
   Civil 3D **cerrado** (`Get-Process acad -ErrorAction SilentlyContinue` no devuelve nada). En PowerShell:
   ```powershell
   cd C:\IA\civil3d-mcp
   git status --short
   git fetch origin
   git checkout claude/vibrant-ptolemy-79gan9
   git pull origin claude/vibrant-ptolemy-79gan9
   git log -1 --oneline
   powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1
   $c = "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\Contents"
   (Get-Item "$c\ArbaMcp.dll").VersionInfo.FileVersion
   (Get-Item "$c\ArbaMcp.Nucleo.dll").VersionInfo.FileVersion
   Select-String -Path "$env:APPDATA\Autodesk\ApplicationPlugins\ArbaMcp.bundle\PackageContents.xml" -Pattern 'AppVersion'
   ```
   → esperado: `git status` sin archivos modificados (los `??` no rastreados no importan; si aparece modificado
   `herramientas-dev/miembros_por_verificar_civil3d.md`, descártalo con `git checkout -- <archivo>`), la compilación
   con `0 Errores` (compila `ArbaMcp.Nucleo` y `ArbaMcp`), `Instalado en: ... (ArbaMcp.dll + ArbaMcp.Nucleo.dll)`,
   `FileVersion` **1.3.3.0** en las dos DLL y `AppVersion="1.3.3"`. Si la compilación falla, pega todas las líneas con
   `error` y **para aquí** (informe con solo este paso): la 1.3.2 sigue instalada.
   Después reinicia el puente (detén el proceso `main.py` de `PuenteMcp` que haya y arranca
   `.\PuenteMcp\.venv\Scripts\python .\PuenteMcp\main.py` con la salida redirigida a un archivo; esperado en esa salida:
   `Puente MCP Civil 3D 1.3.3: plugin en http://127.0.0.1:8765, agente en http://127.0.0.1:8001/mcp` y
   `Uvicorn running on http://127.0.0.1:8001`). Abre Civil 3D desde el menú Inicio con la **copia** del DWG de prueba,
   comprueba en `historial.log` la línea `HiloPrincipal listo: despachador sí, ventana principal sí` de este arranque y
   reconecta el cliente MCP. Si `ARBAMCP` muestra un cuadro de diálogo, ciérralo con Aceptar antes de seguir: mientras
   está abierto, Civil 3D cuenta como ocupado.
1. **(1 min)** Lista las herramientas que te ofrece el servidor → esperado: **43** nombres, entre ellos
   `deshacer_objetivos` (nueva), `asignar_objetivos`, `establecer_frecuencias` y todas las de la 1.2.2. En
   `deshacer_objetivos` los parámetros son `id` (number), `forzar` (boolean) y `simular` (boolean), y su descripción
   dice que Civil 3D no revierte objetivos con `_.UNDO` ni Ctrl+Z. Pega la lista de nombres.
2. **(1 min)** `ping` sin argumentos → esperado: `plugin: "ArbaMcp"`, `version: "1.3.3.0"`, `hay_dibujo: true`, y en
   la misma respuesta `ms`, `ms_espera`, `ms_ejecucion` y `ms_puente` (enteros). Anota los cuatro.
3. **(2 min)** En PowerShell:
   ```powershell
   curl.exe -s http://127.0.0.1:8765/ping
   curl.exe -s -i http://127.0.0.1:8765/tools | Select-Object -First 1
   1..3 | ForEach-Object { curl.exe -s -o NUL -w "%{http_code} " -H "X-Arba-Token: malo" http://127.0.0.1:8765/tools }
   ```
   → esperado, literal: `{"ok":true,"servidor":"ArbaMcp","version":"1.3.3"}` (sin token y sin datos del dibujo);
   `HTTP/1.1 401 Unauthorized`; `401 401 401`. Después `leer_historial(ultimas_n=30)` → esperado: **una sola** línea
   `MCP 401 GET /tools: token ausente o distinto (los siguientes 401 de esta ruta en este minuto se agrupan)` para
   ese minuto, no tres. Pega las líneas `MCP 401`.
4. **(1 min)** `listar_corredores` → guarda como `CORREDOR` el nombre del primero y como `LINEA_BASE` su primera
   línea base. Si en vez de la lista devuelve un error que mencione `LockViolation`, `eLockViolation` o
   `eNotOpenForWrite`, pega el error literal y **para aquí** (informe con los pasos 0 a 4).
   `listar_regiones(corredor=CORREDOR)` → guarda `REGION` (el `nombre` de la primera región), `REGION2` (la segunda,
   si existe) y las `frecuencia_tangentes` de ambas como `FT1` y `FT2`.
5. **(1 min)** `listar_objetivos(corredor=CORREDOR, linea_base=LINEA_BASE, region=REGION)` → guarda los tres primeros
   objetivos de `tipo: "superficie"` como `SUB_A`, `SUB_B`, `SUB_C` (`subensamblaje`, y `grupo`/`parametro` si vienen;
   si hay menos de tres, repite el primero) y el nombre de su superficie actual como `ACTUAL`.
   `listar_superficies` → guarda como `OTRA` una superficie distinta de `ACTUAL` **cuyo campo `corredor` sea null**
   (`tipo_superficie` distinto de `Corridor`): las superficies generadas por un corredor no valen como objetivo. Guarda
   además como `PROPIA` una superficie con `corredor: CORREDOR`, si la hay.
6. **(1 min)** `asignar_objetivos(corredor=CORREDOR, linea_base=LINEA_BASE, simular=true, asignaciones="[...]")`
   con cuatro elementos en el texto JSON: los tres de `SUB_A`, `SUB_B`, `SUB_C` con
   `{"region": REGION, "subensamblaje": ..., "tipo": "superficie", "objetivo": OTRA}` (añade `grupo` y `parametro`
   si el paso 5 los dio) y un cuarto igual al primero pero con `"region": "region_que_no_existe"` → esperado:
   `simulado: true`, `plan` con 4 entradas (índices 0 a 3; la 3 con `error` que nombra la región inexistente y lista
   las regiones), `fallidos` con solo el índice 3, `datos: {total: 4, aplicadas: 3, fallidas: 1}`, sin `copia` y sin
   `restaurar`, `antes`/`despues` por índice con `objetivos`. Anota `ms` y `ms_puente`. Antes de llamar, anota la primera
   entrada del menú Deshacer (flecha del botón **Deshacer** de la barra de acceso rápido, sin ejecutar nada); después
   de la simulación vuelve a mirar → esperado: la misma primera entrada.
7. **(2 min)** El paso 6 sin `simular` → esperado: `simulado: false`, `mensaje: "3 asignaciones aplicadas, 1 fallidas
   (de 4)"`, `datos: {total: 4, aplicadas: 3, fallidas: 1}`, `fallidos` con el índice 3, `cambios` con `0.objetivos`,
   `1.objetivos`, `2.objetivos`, `copia` como **objeto** (`ruta` en `backups\`, `metodo: "copia_de_disco"`,
   `reutilizada`, `ms`, `espera_ms`, `refleja_guardado_de`, `estado: "terminada"`, `nota`) y **`restaurar`** como objeto
   con `herramienta: "deshacer_objetivos"`, `args: {id: N}` (guarda `N` como `ID7`), `nota` que dice que Civil 3D no
   revierte objetivos, y `antes` con **un elemento por objetivo distinto** (uno solo si `SUB_A`, `SUB_B` y `SUB_C` son
   el mismo) con `objetivos: ["superficie:ACTUAL"]`. Comprueba en PowerShell que `backups\` tiene **como máximo un
   archivo nuevo** y que `mcp_log.jsonl` tiene **una** línea nueva. Anota `ms`, `ms_espera`, `ms_ejecucion`,
   `ms_puente`, `copia.ms` y `copia.espera_ms`.
8. **(1 min)** `listar_objetivos(...)` como en el paso 5 → esperado: `SUB_A`, `SUB_B` y `SUB_C` apuntan a `OTRA`.
   Despliega la flecha del botón **Deshacer** y anota literalmente las 3 primeras entradas → esperado: **una sola**
   entrada `Executefunction` para el lote del paso 7 y **ninguna** por las lecturas de los pasos 4, 5 y 8 ni por la
   simulación del paso 6. Si aparecen `Grupo de comandos`, anota cuántos y sigue. No ejecutes Deshacer desde el menú.
9. **(2 min) Paso decisivo.** `deshacer_objetivos()` sin argumentos → esperado: `simulado: false`, `herramienta:
   "deshacer_objetivos"`, `datos: {total: K, aplicadas: K, fallidas: 0}` (K = número de objetivos distintos del paso
   7), `cambios` con `objetivos` por índice, `antes`/`despues` por índice (de `OTRA` a `ACTUAL`), `copia` como objeto,
   y `deshacer: {entrada: {id: ID7, herramienta: "asignar_objetivos", corredor: CORREDOR, objetivos: K}, pendientes:
   [...]}` **sin** `ID7` en `pendientes`. Después `listar_objetivos(...)` → esperado: los tres objetivos vuelven a
   `ACTUAL`. Pide la primera entrada del menú Deshacer → esperado: un `Executefunction` nuevo (el deshacer es una
   escritura). Después `deshacer_objetivos()` otra vez → esperado: error `No hay escrituras de objetivos que deshacer
   en este dibujo...` (la pila quedó vacía). Si los objetivos **no** vuelven a `ACTUAL`: FALLO, pega la respuesta
   literal y restáuralos con `asignar_objetivos` (los tres elementos con `"objetivo": ACTUAL`), anotándolo como
   restauración.
9b. **(1 min) Superficie del propio corredor.** Si el paso 5 dio `PROPIA`:
   `asignar_objetivo(corredor=CORREDOR, linea_base=LINEA_BASE, region=REGION, subensamblaje=SUB_A, tipo="superficie",
   objetivo=PROPIA, simular=true)` → esperado: error que dice `la genera el propio corredor` y nombra `CORREDOR`; sin
   `simular` → el mismo error y **nada cambia** (`listar_objetivos` sigue en `ACTUAL`, `mcp_log.jsonl` con una línea
   `ok: false`). Y `asignar_objetivos(..., simular=true, asignaciones="[<el elemento de SUB_A con objetivo PROPIA>]")` →
   esperado: `fallidos` con el índice 0 y ese mismo motivo, `datos: {total: 1, aplicadas: 0, fallidas: 1}`. Si no hay
   `PROPIA`, anota "no probado".
10. **(2 min)** `establecer_frecuencias(corredor=CORREDOR, linea_base=LINEA_BASE, simular=true, regiones="[...]")` con
    dos elementos: `{"region": REGION, "tangentes": FT1+1}` y `{"region": REGION2, "tangentes": FT2+1}` (si no hay
    `REGION2`, repite `REGION`) → esperado: `simulado: true`, `plan` de 2 con `cambios: ["frecuencia_tangentes"]` y
    `antes`/`despues`. Sin `simular` → esperado: `datos.aplicadas: 2`, `copia.reutilizada: true`, `copia.espera_ms`
    entero, `copia.ms: 0`, y **sin** `restaurar` (solo lo llevan las escrituras de objetivos).
    `listar_regiones(corredor=CORREDOR)` → esperado: las dos regiones con `frecuencia_tangentes` = valor +1. Anota la
    entrada nueva del menú Deshacer (una sola).
11. **(1 min)** `ejecutar_comando(comando="_.UNDO 1", timeout_s=30)` → `terminado`; `listar_regiones` → las
    frecuencias vuelven a `FT1` y `FT2` (las frecuencias sí se deshacen con `_.UNDO 1`).
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
    - `deshacer_objetivos(id=999999)` → esperado: error `No hay ninguna entrada 999999 en la pila de este dibujo`
      con la lista de pendientes.
    Comprueba con `leer_log(ultimas_n=6)` que cada una dejó una línea con `ok: false`.
14. **(3 min) Segundo paso decisivo: deshacer por id, comprobación de "nadie lo cambió después" y `forzar`.**
    Confirma con `listar_objetivos` que `SUB_A` está en `ACTUAL`. Después:
    1. `asignar_objetivo(corredor=CORREDOR, linea_base=LINEA_BASE, region=REGION, subensamblaje=SUB_A,
       tipo="superficie", objetivo=OTRA)` → esperado: `despues.objetivos` con `OTRA`, `copia.reutilizada: true`,
       `restaurar.args.id` (guárdalo como `X`) y `restaurar.antes[0].objetivos: ["superficie:ACTUAL"]`.
    2. La misma llamada con `objetivo=ACTUAL` → esperado: `despues.objetivos` con `ACTUAL` y `restaurar.args.id`
       (guárdalo como `Y`).
    3. `deshacer_objetivos(id=X)` → esperado: **no toca nada**: `datos: {total: 1, aplicadas: 0, fallidas: 1}`,
       `fallidos[0].motivo` con `ya no está como lo dejó la escritura` y `forzar=true`, `avisos` con "sigue en la
       pila", y `listar_objetivos` sigue en `ACTUAL`.
    4. `deshacer_objetivos(id=Y)` → esperado: `aplicadas: 1`, `SUB_A` vuelve a `OTRA`, `deshacer.pendientes` con `X`
       y sin `Y`.
    5. `deshacer_objetivos(id=X)` → esperado: ahora sí, `aplicadas: 1`, `SUB_A` vuelve a `ACTUAL`, `pendientes: []`.
    6. `deshacer_objetivos(id=X, forzar=true)` → esperado: error `No hay ninguna entrada X` (ya se consumió).
    Menú Deshacer: pide las 5 primeras entradas → esperado: cinco `Executefunction` (dos escrituras y tres
    `deshacer_objetivos`, el fallido del punto 3 no escribe) y ninguna otra entrada. Si al final `SUB_A` no está en
    `ACTUAL`, restáuralo con `asignar_objetivo(..., objetivo=ACTUAL)` anotándolo como restauración.
15. **(1 min)** `guardar_copia(sufijo="validacion_13")` → esperado: `copia` en `backups\` con `_validacion_13.dwg`
    y `copias_conservadas: 20`.
16. **(3 min)** Copia de un dibujo **sin guardar**: en Civil 3D crea un dibujo nuevo (`_.NEW`, plantilla por defecto) y
    déjalo activo; `guardar_copia(sufijo="nuevo")` → esperado: `copia` bajo `%LOCALAPPDATA%\ArbaMcp\backups\`.
    Vuelve a activar la copia del DWG de prueba (`abrir_dibujo(ruta=...)` → `ya_estaba_abierto: true`).
17. **(4 min)** En una consola, desde el clon: `.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py
    --fase 13 --dwg "<ruta de la copia>"` → esperado: `=== Resultado: TODO OK ===` (salida 0), incluidas
    `tras deshacer_objetivos los 3 objetivos vuelven a su valor original`, `_.UNDO 1 revierte el lote de frecuencias
    entero` y `la superficie del propio corredor (...) va a fallidos` (si el corredor genera alguna). Pega la salida
    entera con el token sustituido. Las líneas `[FAL]` van al informe con su `->`.
18. **(3 min)** `.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py --dwg "<ruta de la copia>"`
    → esperado: `TODO OK`, incluida la prueba 6 (`deshacer_objetivos devuelve la última escritura entera`) y la de
    "Civil 3D ocupado".
19. **(2 min)** Menú Deshacer final, **antes de cerrar Civil 3D** (cerrarlo borra la lista): despliega la lista y pega
    literalmente las 8 primeras entradas. Esperado: entradas `Executefunction` solo por escrituras reales (las de los
    pasos 7, 9, 10, 14, 15, las de `probar_servidor.py` y las restauraciones anotadas; `deshacer_objetivos` cuenta como
    escritura), las `Linea`/`Regen` de `probar_servidor.py`, y **ninguna** `Grupo de comandos` ni `Executefunction` por
    las lecturas, las simulaciones y los `ping`. Si aparecen `Grupo de comandos`, anótalo con el recuento.
20. **(1 min)** `leer_historial(ultimas_n=60)` → pega las líneas `Copia de seguridad:` (con `ms`), `Escritura ...` y
    `MCP 401`.
21. **(4 min)** Arranque de Civil 3D con el puente en marcha: sin cerrar el puente, cierra Civil 3D (a la pregunta de
    guardar cambios, **No**), espera 10 s, llama a `ping` → esperado: `ok: false` con
    `Civil 3D no está abierto o ArbaMcp no cargó` y `ms_puente`. Abre Civil 3D de nuevo (menú Inicio) con la copia,
    espera a que cargue el dibujo (si ejecutas `ARBAMCP`, cierra su cuadro con Aceptar), llama a `ping` **sin
    reiniciar el puente** → esperado: `version: "1.3.3.0"`. Después `deshacer_objetivos(simular=true)` → esperado:
    error `No hay escrituras de objetivos que deshacer...` (la pila vive en memoria y se perdió con el reinicio). En
    el archivo de salida del puente pega las líneas que mencionen `/ping` o el token.

## Informe

Guárdalo como `herramientas-dev/informes/informe_validacion_133_<fecha>.md` (sin el token) y entrega una tabla con
una fila por paso:

| Paso | Herramienta | Tiempo total del paso | `ms` / `ms_espera` / `ms_ejecucion` / `ms_puente` | Resultado (OK / FALLO / no probado) | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|---|

Y debajo:

- **Deshacer de objetivos** (lo que decide la 1.3.3): respuestas literales de `deshacer_objetivos` en los pasos 9 y 14
  (los seis puntos) y si los objetivos volvieron; el bloque `restaurar` literal del paso 7.
- **Superficie del propio corredor**: el error literal del 9b (o "no probado").
- **Restauraciones**: lista de escrituras de restauración hechas (hora, herramienta, motivo), o "ninguna".
- **Copia de disco**: `copia.ms`, `copia.espera_ms`, `copia.reutilizada` y `copia.refleja_guardado_de` de los pasos 7,
  10 y 14; si `copia.estado` fue `error`, el mensaje literal.
- **Deshacer**: las entradas literales de los pasos 8, 14 y 19, y si las lecturas y simulaciones dejaron o no entradas.
- **Miembros de la API que fallaron** (nombre del miembro, mensaje de error literal), para que la sesión de desarrollo
  actualice `herramientas-dev/miembros_por_verificar_civil3d.md`. En 1.3.3 están `por verificar`: la pila
  `Restauraciones` con `deshacer_objetivos` (`TryGetObjectId` de los handles anteriores y `SetTargets` con ellos) y el
  rechazo de la superficie del propio corredor (`Corridor.CorridorSurfaces`).
- **Tiempos comparados**: `ms_espera` frente a `ms_ejecucion` en una lectura y en el lote del paso 7; `ms_puente`
  menos `ms` (sobrecoste del puente) en tres llamadas.
- Versión de Civil 3D e idioma, versión de `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll` instaladas (`FileVersion`), y el
  resultado de `probar_servidor.py --fase 13` y del `--dwg` (`TODO OK` o la lista de fallos).
