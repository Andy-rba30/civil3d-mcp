# Prompt de validación de la 1.3.2 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D (Antigravity, Claude Desktop, Claude
Code, Cursor...). Requisitos previos:

- Civil 3D 2027 abierto con **una copia** del DWG de prueba (nunca la entrega original): un dibujo guardado en disco
  con al menos un corredor cuya primera región tenga objetivos de superficie, y con al menos dos superficies.
- `VALIDACION_122.md` ejecutada (informe del 28/09/2026: 1.2.2 validada, `HiloPrincipal listo`, 13/13 y 31/31) y la
  1.3.1 validada con este mismo prompt (informe `herramientas-dev/informes/informe_validacion_131_20260928.md`).
- El plugin **1.3.2** se instala en el paso 0 de este prompt desde la rama `claude/vibrant-ptolemy-79gan9` (PR hacia
  `feature/proceso-1.3`; si el PR ya está fusionado, sirve `feature/proceso-1.3`); el bundle queda con `ArbaMcp.dll` y
  `ArbaMcp.Nucleo.dll`.
- Puente reiniciado con el `main.py` de 1.3.2 y el cliente MCP reiniciado para que cargue la lista de 42 herramientas
  (también en el paso 0).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- Lo que ya se sabe: cada herramienta de escritura deja **una** entrada `Executefunction` en el menú Deshacer (1.2.2);
  las lecturas y las simulaciones no dejan ninguna (1.3.1, bloqueo de lectura en contexto de aplicación); los lotes son
  una sola entrada; la copia de disco se reutiliza; el puente se reconecta solo tras reabrir Civil 3D (todo validado el
  28/09/2026 con la 1.3.1). **Lo que falló en la 1.3.1**: `_.UNDO 1` revertía las frecuencias, pero **no los
  objetivos** de `asignar_objetivos` ni de `asignar_objetivo` (pasos 9, 14, 17 y 18: el comando terminaba, la entrada
  `Executefunction` desaparecía y `listar_objetivos` seguía en el valor nuevo). La 1.3.2 graba el estado del corredor en
  la pila de deshacer antes de tocarlo (escritura y restauración de `Description`), y la suite base de
  `probar_servidor.py` ya no exige 401 en `GET /ping` (público desde 1.3.0) ni un archivo nuevo en `backups\` cuando la
  copia se reutiliza. **Los pasos 9 y 14 son los decisivos**; el 9b es el diagnóstico con la interfaz si el 9 falla.
- Si ya ejecutaste este prompt con la 1.3.1, vuelve a empezar desde el paso 0 con la 1.3.2.

---

Eres el agente de validación de la versión 1.3.2 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
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
  lo restauras donde el paso lo indique, y cada restauración se anota en Observaciones (hora y herramienta): son
  escrituras que dejan su propia entrada `Executefunction` y su línea de log.
- Cuando un paso pide mirar el menú Deshacer, pídeselo a la persona que tiene Civil 3D delante y anota literalmente
  lo que te diga. No edites archivos del repositorio salvo tu informe y no hagas commits.
- Toma los nombres reales (corredor, línea base, región, subensamblajes, superficies) de las respuestas de los pasos
  4 y 5, no los de los ejemplos.

## Pasos

0. **(6 min) Instalar la 1.3.2.** Reglas de `VALIDACION_122.md` (no muestres el token, sin commits, sin tocar código).
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
   `herramientas-dev/miembros_por_verificar_civil3d.md` de una validación anterior, descártalo con
   `git checkout -- herramientas-dev/miembros_por_verificar_civil3d.md`, la tabla la mantiene la sesión de desarrollo),
   la compilación con `0 Errores` (compila dos proyectos: `ArbaMcp.Nucleo` y `ArbaMcp`),
   `Instalado en: ... (ArbaMcp.dll + ArbaMcp.Nucleo.dll)`, `FileVersion` **1.3.2.0** en las dos DLL y
   `AppVersion="1.3.2"`. Si la compilación falla, pega todas las líneas con `error` y **para aquí** (informe con solo
   este paso): la 1.3.1 sigue instalada.
   Después reinicia el puente (detén el proceso `main.py` de `PuenteMcp` que haya y arranca
   `.\PuenteMcp\.venv\Scripts\python .\PuenteMcp\main.py` con la salida redirigida a un archivo; esperado en esa salida:
   `Puente MCP Civil 3D 1.3.2: plugin en http://127.0.0.1:8765, agente en http://127.0.0.1:8001/mcp` y
   `Uvicorn running on http://127.0.0.1:8001`). Abre Civil 3D desde el menú Inicio con la **copia** del DWG de prueba,
   comprueba en `historial.log` la línea `HiloPrincipal listo: despachador sí, ventana principal sí` de este arranque y
   reconecta el cliente MCP. Si `ARBAMCP` muestra un cuadro de diálogo, ciérralo con Aceptar antes de seguir: mientras
   está abierto, Civil 3D cuenta como ocupado.
1. **(1 min)** Lista las herramientas que te ofrece el servidor → esperado: **42** nombres, entre ellos
   `asignar_objetivos` y `establecer_frecuencias` y todas las de la 1.2.2 (`asignar_objetivo`,
   `establecer_frecuencia`, `ping`, `leer_historial`...). En `asignar_objetivos` el parámetro `asignaciones` es de
   tipo texto (`string`) y su descripción incluye un ejemplo JSON. Pega la lista de nombres.
2. **(1 min)** `ping` sin argumentos → esperado: `plugin: "ArbaMcp"`, `version: "1.3.2.0"`, `hay_dibujo: true`, y en
   la misma respuesta `ms`, `ms_espera`, `ms_ejecucion` y `ms_puente` (enteros). Anota los cuatro.
3. **(2 min)** En PowerShell:
   ```powershell
   curl.exe -s http://127.0.0.1:8765/ping
   curl.exe -s -i http://127.0.0.1:8765/tools | Select-Object -First 1
   1..3 | ForEach-Object { curl.exe -s -o NUL -w "%{http_code} " -H "X-Arba-Token: malo" http://127.0.0.1:8765/tools }
   ```
   → esperado, literal: `{"ok":true,"servidor":"ArbaMcp","version":"1.3.2"}` (sin token y sin datos del dibujo);
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
   `listar_superficies` → guarda como `OTRA` una superficie distinta de `ACTUAL`.
6. **(1 min)** `asignar_objetivos(corredor=CORREDOR, linea_base=LINEA_BASE, simular=true, asignaciones="[...]")`
   con cuatro elementos en el texto JSON: los tres de `SUB_A`, `SUB_B`, `SUB_C` con
   `{"region": REGION, "subensamblaje": ..., "tipo": "superficie", "objetivo": OTRA}` (añade `grupo` y `parametro`
   si el paso 5 los dio) y un cuarto igual al primero pero con `"region": "region_que_no_existe"` → esperado:
   `simulado: true`, `plan` con 4 entradas (índices 0 a 3; la 3 con `error` que nombra la región inexistente y lista
   las regiones), `fallidos` con solo el índice 3, `datos: {total: 4, aplicadas: 3, fallidas: 1}`, sin `copia`,
   `antes`/`despues` por índice con `objetivos`. Anota `ms` y `ms_puente`. Antes de llamar, anota la primera
   entrada del menú Deshacer (flecha del botón **Deshacer** de la barra de acceso rápido, sin ejecutar nada); después
   de la simulación vuelve a mirar → esperado: la misma primera entrada (la simulación no añade ninguna).
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
   primeras entradas → esperado: **una sola** entrada `Executefunction` para el lote del paso 7 (no una por asignación
   ni por transacción; la escritura y restauración de `Description` de la 1.3.2 va dentro de la misma entrada) y
   **ninguna** entrada por los `listar_*` de los pasos 4, 5 y 8 ni por la simulación del paso 6. Si aparecen entradas
   `Grupo de comandos` o más de un `Executefunction`, anótalo con el recuento. No ejecutes Deshacer desde el menú.
9. **(2 min) Paso decisivo.** `ejecutar_comando(comando="_.UNDO 1", timeout_s=30)` → esperado: `terminado`. Pide la
   primera entrada del menú Deshacer → esperado: el `Executefunction` del paso 7 ya no está. Después
   `listar_objetivos(...)` → esperado: los tres objetivos vuelven a `ACTUAL` (el lote entero se deshizo con una
   sola operación). Si vuelven: OK, y sigue con el paso 10. Si solo vuelve uno, anótalo (el lote no quedó como una
   entrada). Si los tres siguen en `OTRA` (como en la 1.3.1): **FALLO**, pega la respuesta literal y haz el paso 9b.
9b. **(5 min, solo si el paso 9 falló) Diagnóstico con la interfaz.** Sirve para saber si el problema está en la
   escritura del plugin o en Civil 3D. Pide a la persona que tiene Civil 3D que, **sin usar el MCP**, cambie el objetivo
   de superficie de `SUB_A` en la región `REGION` al valor contrario del actual (si está en `OTRA`, a `ACTUAL`; si en
   `ACTUAL`, a `OTRA`): seleccionar el corredor, botón derecho, **Propiedades de corredor**, pestaña **Parámetros**,
   en la fila de `REGION` el botón de la columna **Objetivo**, en el cuadro **Asignación de objetivos** cambiar la
   superficie de `SUB_A`, Aceptar y Aceptar (si pregunta por reconstruir, que acepte). Anota qué entrada nueva
   aparece en el menú Deshacer (su nombre literal). Después:
   - `listar_objetivos(...)` → esperado: `SUB_A` en el valor nuevo (confirma que la lectura ve los cambios de la
     interfaz).
   - `ejecutar_comando(comando="_.UNDO 1", timeout_s=30)` → `terminado`; `listar_objetivos(...)`: si `SUB_A` volvió al
     valor anterior, **la interfaz sí se revierte**: el fallo está en la escritura del plugin. Si sigue en el valor
     nuevo, **Civil 3D tampoco revierte por UNDO** los objetivos: es una limitación de Civil 3D (o de la lectura), no
     del plugin. Anota cuál de las dos.
   - Restauración: si los objetivos no están todos en `ACTUAL`, ponlos con `asignar_objetivos` (los tres elementos
     del paso 6, con `"objetivo": ACTUAL`) y anótalo como restauración. A partir de aquí los pasos siguen con los
     objetivos en `ACTUAL`.
10. **(2 min)** `establecer_frecuencias(corredor=CORREDOR, linea_base=LINEA_BASE, simular=true, regiones="[...]")` con
    dos elementos: `{"region": REGION, "tangentes": FT1+1}` y `{"region": REGION2, "tangentes": FT2+1}` (si no hay
    `REGION2`, repite `REGION`) → esperado: `simulado: true`, `plan` de 2 con `cambios: ["frecuencia_tangentes"]` y
    `antes`/`despues`. Sin `simular` → esperado: `datos.aplicadas: 2`, `copia.reutilizada: true` (segunda escritura
    sin guardar el dibujo entre medias), `copia.espera_ms` entero (normalmente 0), `copia.ms: 0`.
    `listar_regiones(corredor=CORREDOR)` → esperado: las dos regiones con `frecuencia_tangentes` = valor +1. Anota la
    entrada nueva del menú Deshacer (una sola).
11. **(1 min)** `ejecutar_comando(comando="_.UNDO 1", timeout_s=30)` → `terminado`; `listar_regiones` → las
    frecuencias vuelven a `FT1` y `FT2` (en la 1.3.1 esto ya funcionaba).
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
14. **(2 min) Segundo paso decisivo.** Antes de llamar, confirma con `listar_objetivos` que `SUB_A` está en `ACTUAL`
    (si no, anótalo y restáuralo con `asignar_objetivo(..., objetivo=ACTUAL)` como restauración). Después
    `asignar_objetivo(corredor=CORREDOR, linea_base=LINEA_BASE, region=REGION, subensamblaje=SUB_A,
    tipo="superficie", objetivo=OTRA)` (herramienta individual de la 1.2.x) → esperado: `simulado: false`,
    `despues.objetivos` con `OTRA`, `copia` como objeto (`reutilizada: true`), `avisos: null`, y en el menú Deshacer
    una entrada `Executefunction` nueva. `ejecutar_comando("_.UNDO 1")` → `terminado`; `listar_objetivos` → esperado:
    `SUB_A` vuelve a `ACTUAL`. Si sigue en `OTRA`: **FALLO**, pega la respuesta y restáuralo con
    `asignar_objetivo(..., objetivo=ACTUAL)` anotándolo como restauración.
15. **(1 min)** `guardar_copia(sufijo="validacion_13")` → esperado: `copia` en `backups\` con `_validacion_13.dwg`
    (sin cambios respecto a 1.2.x) y `copias_conservadas: 20`.
16. **(3 min)** Copia de un dibujo **sin guardar**: en Civil 3D crea un dibujo nuevo (`_.NEW`, plantilla por defecto) y
    déjalo activo; `guardar_copia(sufijo="nuevo")` → esperado: `copia` bajo `%LOCALAPPDATA%\ArbaMcp\backups\`.
    Vuelve a activar la copia del DWG de prueba (`abrir_dibujo(ruta=...)` → `ya_estaba_abierto: true`). (La ruta
    SaveAs de las escrituras automáticas sobre un dibujo sin guardar no se puede probar sin un corredor: anótalo
    como "no probado" salvo que tengas un dibujo sin guardar con corredor.)
17. **(4 min)** En una consola, desde el clon: `.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py
    --fase 13 --dwg "<ruta de la copia>"` → esperado: `=== Resultado: TODO OK ===` (salida 0), incluida
    `tras _.UNDO 1 los 3 objetivos vuelven a su valor original` (en la 1.3.1 fue el único `[FAL]`). Pega la salida
    entera con el token sustituido. Las líneas `[FAL]` van al informe con su `->`.
18. **(3 min)** `.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py --dwg "<ruta de la copia>"`
    (las pruebas de la 1.2.2, que no deben dejar de pasar) → esperado: `TODO OK`, incluida la prueba 6
    (`_.UNDO 1 revierte la última escritura entera`) y la de "Civil 3D ocupado". Desde 1.3.2 la suite base comprueba
    `GET /ping` sin token → 200 y `GET /tools` sin token → 401, y admite que la copia se reutilice (en la 1.3.1 esas
    dos comprobaciones estaban desfasadas y daban `[FAL]`).
19. **(2 min)** Menú Deshacer final, **antes de cerrar Civil 3D** (cerrarlo borra la lista): despliega la lista y pega
    literalmente las 8 primeras entradas. Esperado: entradas `Executefunction` solo por las escrituras reales de los
    pasos 7, 10, 14 y 15 y por las restauraciones que hayas anotado (una por herramienta, no varias por lote), las
    `Linea`/`Regen` de `probar_servidor.py`, y **ninguna** `Grupo de comandos` ni `Executefunction` por las decenas de
    `listar_*`, las simulaciones y los `ping` de esta sesión. Si aparecen `Grupo de comandos` o tantas
    `Executefunction` como lecturas, anótalo con el recuento.
20. **(1 min)** `leer_historial(ultimas_n=60)` → pega las líneas `Copia de seguridad:` (con `ms`), `Escritura ...` y
    `MCP 401`.
21. **(4 min)** Arranque de Civil 3D con el puente en marcha: sin cerrar el puente, cierra Civil 3D (a la pregunta de
    guardar cambios, **No**), espera 10 s, llama a `ping` → esperado: `ok: false` con
    `Civil 3D no está abierto o ArbaMcp no cargó` y `ms_puente`. Abre Civil 3D de nuevo (menú Inicio) con la copia,
    espera a que cargue el dibujo (si ejecutas `ARBAMCP`, cierra su cuadro con Aceptar), llama a `ping` **sin
    reiniciar el puente** → esperado: `version: "1.3.2.0"` (el puente sondeó `/ping`, releyó el token y volvió a
    registrar las herramientas). En el archivo de salida del puente pega las líneas que mencionen `/ping` o el token.

## Informe

Guárdalo como `herramientas-dev/informes/informe_validacion_132_<fecha>.md` (sin el token) y entrega una tabla con
una fila por paso:

| Paso | Herramienta | Tiempo total del paso | `ms` / `ms_espera` / `ms_ejecucion` / `ms_puente` | Resultado (OK / FALLO / no probado) | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|---|

Y debajo:

- **Deshacer de objetivos** (lo que decide la 1.3.2): resultado literal de los pasos 9 y 14 (y 9b si hizo falta, con la
  entrada del menú que dejó el cambio desde la interfaz y cuál de las dos conclusiones aplica).
- **Restauraciones**: lista de escrituras de restauración hechas (hora, herramienta, motivo), o "ninguna".
- **Copia de disco**: `copia.ms`, `copia.espera_ms`, `copia.reutilizada` y `copia.refleja_guardado_de` de los pasos 7,
  10 y 14; si `copia.estado` fue `error`, el mensaje literal. Tamaño del `.dwg` y del archivo de `backups\`.
- **Deshacer**: las entradas literales de los pasos 8, 10 y 19, si `_.UNDO 1` revirtió el lote entero (pasos 9 y 11) y
  si las lecturas y simulaciones dejaron o no entradas.
- **Miembros de la API que fallaron** (nombre del miembro, mensaje de error literal), para que la sesión de desarrollo
  actualice `herramientas-dev/miembros_por_verificar_civil3d.md`. En 1.3.2 están `por verificar`: la escritura y
  restauración de `Description` del corredor como registro de deshacer antes de `SetTargets` (pasos 9 y 14) y, si hizo
  falta, el diagnóstico 9b.
- **Tiempos comparados**: `ms_espera` frente a `ms_ejecucion` en una lectura y en el lote del paso 7; `ms_puente`
  menos `ms` (sobrecoste del puente) en tres llamadas.
- Versión de Civil 3D e idioma, versión de `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll` instaladas (`FileVersion`), y el
  resultado de `probar_servidor.py --fase 13` y del `--dwg` (`TODO OK` o la lista de fallos).
