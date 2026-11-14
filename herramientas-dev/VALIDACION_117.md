# Prompt de validación de la 1.17.0 (civil3d-mcp) para el agente local

Esta validación tiene **tres partes** y **dos conversaciones** con el agente: la parte A (conversación 1, con Jose
presente), la parte B (una conversación **nueva**, para comprobar que el agente encuentra la receta él solo) y la parte C
(el agente instalador, en PowerShell). Entre la A y la B se corre L.2. Requisitos previos:

- **Se ejecuta DESPUÉS de `VALIDACION_116.md`** (la de `ejecutar_codigo`), en la **misma instalación** y con
  `ARBA_MCP_CODIGO=1` **todavía puesta** (la 116 la deja activada; esta validación la quita al final, en la parte C).
- **Dibujo**: la misma copia de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` que usó la 116 (anota su
  nombre). Si no la tienes, haz una copia nueva de `PRUEBA_EVALUACION_BASE.dwg` como
  `C:\Proyectos\dibujos_prueba\PRUEBA_117.dwg` y ábrela. Nunca el archivo base ni un dibujo real. Anota la carpeta
  del DWG: ahí aparecen `mcp_log.jsonl` y `backups\`.
- **Plugin y puente 1.17.0** desde la rama `tarea/3.2-recetas` (que ya trae fusionada la 3.1, `ejecutar_codigo`). Hay
  **58** herramientas: **55** del plugin (con `ejecutar_codigo`) más **3** del puente: `buscar_api`, `guardar_receta` y
  `ejecutar_receta`.
- `memoria-firma` clonado y al día en `C:\Proyectos\memoria-firma`, con `MEMORIA_FIRMA_DIR` apuntando ahí (variable de
  usuario, puesta desde la 1.5.0) y `sincronizar_memoria.ps1` en esa carpeta (es el Prompt L.2).
- **Jose tiene que estar delante en las partes A y B**: los pasos 7 y 11 piden **su** respuesta. El agente no puede
  contestar por él.
- Reparto de papeles: el agente local **instala y verifica**; el código y este prompt los mantiene la sesión de
  desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits en `civil3d-mcp` (L.2 sí hace su commit en `memoria-firma`: es lo que
  se prueba).
- **Lo que trae la 1.17.0**: dos herramientas del **puente** (Python, no del plugin), con los argumentos directos (no
  dentro de `args`) y respuesta JSON:
  - `guardar_receta`: guarda en `memoria-firma\recetas\` un script de `ejecutar_codigo` que ya funcionó, como
    `<nombre>.cs` (el código) y `<nombre>.yaml` (pedido de ejemplo, parámetros, versión de Civil 3D, fecha,
    `veces_usada` y la aprobación del dueño). **Solo con la aprobación explícita del dueño**: sin
    `aprobada_por_el_dueno=true` y sus palabras literales en `aprobacion_del_dueno` no escribe nada y devuelve la vista
    previa. Recuerda correr L.2.
  - `ejecutar_receta`: reutiliza la receta con otros parámetros a través de `ejecutar_codigo` (`simular=true` por
    defecto) y suma 1 a `veces_usada` solo en una ejecución real que sale bien.
  - Además, `buscar_api` (herramienta y comando) muestra primero las recetas, también las de código.
  **Los pasos 0, 3, 4, 7, 10, 11, 13, 14, 17 y 19 son los decisivos.**
- Miembros de la API que esta validación decide: **ninguno**. Las herramientas nuevas son Python; el script de la capa
  lo escribe el agente en el paso 2 con lo que ya validó la 116. `herramientas-dev/miembros_por_verificar_civil3d.md` no
  cambia en 1.17.0.

---

## Parte A: conversación 1 (copia desde aquí hasta «Fin de la parte A» en el agente)

Eres el agente de validación de la versión 1.17.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (las del plugin con los argumentos
dentro de `args`; `buscar_api`, `guardar_receta` y `ejecutar_receta` con los argumentos directos), salvo en los pasos
que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON completo o, si es muy largo,
las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para nada en esta validación. Si algo no funciona, es un hallazgo.
- **La aprobación de la receta es de Jose**: en el paso 7 se la pides y esperas su respuesta. Las llamadas de prueba
  de los pasos 4, 5 y 6 llevan `aprobada_por_el_dueno=true` porque **este prompt las autoriza** para comprobar que se
  rechazan; fuera de esta validación, nunca pongas esa marca sin el sí del dueño.
- En el informe sustituye el token del plugin por `<oculto>` y no pegues rutas con nombres de obra (`<ruta>`).
- Nombre de la receta: `crear_capa_con_color`. Si en el paso 1 ya existe (de una ronda anterior), usa
  `crear_capa_con_color_2` en todos los pasos y anótalo.

### Pasos

0. **Versión e instalación (decisivo).** Si `ping` ya da `1.17.0`, no reinstales. Si no, con Civil 3D cerrado, en
   PowerShell desde la raíz del repo en la rama `tarea/3.2-recetas`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`; en `PuenteMcp`, `uv sync`; reinicia el puente y
   el cliente MCP y abre Civil 3D con la copia del DWG. Después:
   - `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.17.0"`, `"hay_dibujo": true`. En
     PowerShell: `curl.exe -s http://127.0.0.1:8765/ping` → `{"ok":true,"servidor":"ArbaMcp","version":"1.17.0"}`.
   - La lista de herramientas: **58**, con `ejecutar_codigo`, `buscar_api`, `guardar_receta` y `ejecutar_receta`.
     `guardar_receta` tiene `nombre`, `pedido` y `codigo` obligatorios y `aprobada_por_el_dueno` (por defecto `false`);
     `ejecutar_receta` tiene `nombre` obligatorio y `simular` (por defecto `true`).
   - PowerShell: `[Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')` → `1` y
     `[Environment]::GetEnvironmentVariable('MEMORIA_FIRMA_DIR','User')` → `C:\Proyectos\memoria-firma`.
   Si la versión no es exactamente `1.17.0`, faltan herramientas o `ARBA_MCP_CODIGO` no es `1`, **para aquí**.

1. **Estado inicial (no decisivo).** PowerShell:
   `Test-Path C:\Proyectos\memoria-firma\recetas\crear_capa_con_color.cs, C:\Proyectos\memoria-firma\recetas\crear_capa_con_color.yaml`
   → `False` y `False`. `buscar_api` con `{"consulta": "crear una capa con color"}`: anota la cabecera literal
   (`buscar_api "crear una capa con color" → N recetas, ...`) y comprueba que **no** sale `crear_capa_con_color`.

2. **Resolver la tarea con código (prerrequisito, no decisivo).** Jose te pide, con estas palabras: **"Crea la capa MCP
   Receta A con el color 3 (verde)"**. Ninguna herramienta crea capas, así que escribe un script para `ejecutar_codigo`
   (como en la 116) que:
   - lea `nombre_capa` (texto) y `color` (número, índice de color ACI) de `Parametros`;
   - cree la capa con ese color si no existe y, si ya existe, no falle;
   - devuelva con `return` un objeto con `capa`, `color` y `creada` (true si la creó, false si ya estaba).
   Ejecútalo con `ejecutar_codigo`, `parametros` = `{"nombre_capa":"MCP Receta A","color":3}`, primero con
   `simular=true` (debe responder `ok` y no dejar la capa: Jose lo comprueba en la lista de capas) y después con
   `simular=false` (`ok`; la capa `MCP Receta A` aparece con color 3). Pide a Jose que lo confirme y, si dice que está
   bien, `aprobar_resultado` con el `id_log` de la ejecución real. **Anota el script completo** (es genérico). Si el
   script no funciona después de dos intentos, **para**: sin un script que funcione no hay receta que guardar.

3. **Vista previa sin aprobación (decisivo).** `guardar_receta` **sin** `aprobada_por_el_dueno` ni
   `aprobacion_del_dueno`:

   ```json
   {"nombre": "crear_capa_con_color",
    "pedido": "Crea la capa MCP Receta A con el color 3 (verde)",
    "codigo": "<el script del paso 2, tal cual>",
    "parametros": "[{\"nombre\":\"nombre_capa\",\"tipo\":\"string\",\"descripcion\":\"Nombre de la capa\",\"obligatorio\":true},{\"nombre\":\"color\",\"tipo\":\"number\",\"descripcion\":\"Color ACI de la capa\",\"por_defecto\":7}]",
    "notas": "Crea la capa con el color pedido; si ya existe, no falla y devuelve creada=false."}
   ```

   Esperado: `"ok": true`, `"guardada": false`, `"reemplazaria": false`, `archivos_previstos` con
   `C:\Proyectos\memoria-firma\recetas\crear_capa_con_color.cs` y `.yaml`, `yaml` con `nombre`, `pedido`,
   `parametros`, `version_civil3d: '2027'`, `version_conector: 1.17.0`, `fecha` de hoy y `veces_usada: 0`, `avisos: []`
   y `siguiente_paso` que empieza por «No se guardó nada. Enséñale al dueño esta vista previa». PowerShell: el
   `Test-Path` del paso 1 sigue dando `False` y `False`. OK solo si no se escribió nada.

4. **Aprobación sin las palabras del dueño (decisivo).** La misma llamada con `"aprobada_por_el_dueno": true` y sin
   `aprobacion_del_dueno`. Esperado: `"ok": false`, `"guardada": false` y `error` que empieza por
   «Falta 'aprobacion_del_dueno': escribe las palabras literales con las que el dueño aprobó guardar la receta». El
   `Test-Path` sigue dando `False` y `False`.

5. **Nombre inseguro (no decisivo).** La misma llamada con `"nombre": "..\\fuera"`, `"aprobada_por_el_dueno": true` y
   `"aprobacion_del_dueno": "prueba de nombre"`. Esperado: `"ok": false` y `error` «El nombre de receta '..\fuera' no
   vale: usa de 3 a 64 caracteres en minúsculas, números y guiones bajos, sin rutas, puntos ni espacios (por ejemplo
   'fuera').». PowerShell: `Test-Path C:\Proyectos\memoria-firma\fuera.cs, C:\Proyectos\memoria-firma\recetas\fuera.cs`
   → `False` y `False`.

6. **Token y datos de obra (no decisivo).** Dos llamadas como la del paso 5 pero con `"nombre": "prueba_rechazos"`:
   - `codigo` = el script del paso 2 con una línea más al final: `// ` seguida de 64 letras `a` seguidas. Esperado:
     `"ok": false`, `error` «'codigo' contiene un texto de 64 caracteres hexadecimales que parece un token (<oculto>).
     ...» y **sin** las 64 letras en la respuesta.
   - `notas` = `Probado en C:\Proyectos\dibujos_prueba\PRUEBA_117.dwg`. Esperado: `"ok": false`, `error` «'notas' lleva
     una ruta absoluta (C:\..., \\servidor\...). Las rutas son datos de obra: pásala como parámetro ...».
   PowerShell: `Test-Path C:\Proyectos\memoria-firma\recetas\prueba_rechazos.yaml` → `False`.

7. **Guardar con la aprobación de Jose (decisivo).** Pregunta a Jose, con tus palabras, si quiere guardar el script
   del paso 2 como la receta `crear_capa_con_color` (enséñale la vista previa del paso 3). Jose responde:
   **"Sí, guárdala como receta"**. Solo entonces, la llamada del paso 3 con `"aprobada_por_el_dueno": true` y
   `"aprobacion_del_dueno": "Sí, guárdala como receta"`. Esperado:

   ```json
   {"ok": true, "guardada": true, "reemplazada": false, "nombre": "crear_capa_con_color",
    "archivos": {"codigo": "C:\\Proyectos\\memoria-firma\\recetas\\crear_capa_con_color.cs",
                 "datos": "C:\\Proyectos\\memoria-firma\\recetas\\crear_capa_con_color.yaml"},
    "veces_usada": 0, "avisos": [],
    "recordatorio": "Recuerda correr L.2 para subir la receta a GitHub: en C:\\Proyectos\\memoria-firma ejecuta sincronizar_memoria.ps1 (Prompt L.2 del agente instalador). Hasta entonces la receta solo está en esta PC."}
   ```

   **Díselo a Jose** (el recordatorio de L.2). PowerShell:
   `Get-Content C:\Proyectos\memoria-firma\recetas\crear_capa_con_color.yaml -Encoding UTF8` → empieza por
   `# Receta de código de civil3d-mcp` y trae `veces_usada: 0`, `ultima_vez: null`, `version_civil3d: '2027'`,
   `version_conector: 1.17.0`, `fecha: '<hoy, AAAA-MM-DD>'` y `aprobacion:` con
   `palabras_del_dueno: Sí, guárdala como receta`. `Get-Content ...\crear_capa_con_color.cs -Encoding UTF8` → el script
   del paso 2, idéntico. Anota `(Get-Item ...\crear_capa_con_color.yaml).LastWriteTime`. OK solo si están los dos
   archivos con esos campos.

8. **No sobrescribe (no decisivo).** Repite la llamada del paso 7 tal cual. Esperado: `"ok": false` y `error` que
   empieza por «Ya existe la receta 'crear_capa_con_color' (C:\Proyectos\memoria-firma\recetas\crear_capa_con_color.yaml,
   veces_usada 0). No se sobrescribe sin decirlo». El `LastWriteTime` del `.yaml` es el mismo que en el paso 7.

9. **La búsqueda ya la ve (no decisivo).** `buscar_api` con `{"consulta": "crear una capa con color"}`. Esperado: la
   cabecera dice al menos `1 recetas` y la primera línea de **RECETAS** es
   `- crear_capa_con_color.yaml + crear_capa_con_color.cs: Crea la capa MCP Receta A con el color 3 (verde)`, seguida
   de `receta de código: ejecutar_receta nombre=crear_capa_con_color (simular=true primero) · veces usada 0 · Civil 3D
   2027 · <hoy>` y `parámetros: nombre_capa (string, obligatorio), color (number, por defecto 7)`.

**GUARDA AHORA el informe como archivo** (no basta con mostrarlo en el chat):
`herramientas-dev/informes/informe_validacion_117_<AAAAMMDD>.md`, con la sección «Parte A» (ver *Informe* al final:
la tabla de los pasos 0 a 9 y el script del paso 2).

Fin de la parte A.

---

## L.2 entre las dos conversaciones (agente instalador o Jose, en PowerShell)

10. **Subir la receta (decisivo).** En `C:\Proyectos\memoria-firma`:
    `powershell -ExecutionPolicy Bypass -File .\sincronizar_memoria.ps1` (si `validar_fichas.py` falla, anota qué ficha y
    por qué, y no subas nada). Después:
    `git -C C:\Proyectos\memoria-firma log -1 --name-only --format="%h %ad %s" --date=short` → un commit con la fecha de
    hoy que incluye `recetas/crear_capa_con_color.cs` y `recetas/crear_capa_con_color.yaml`. En GitHub, `memoria-firma`
    muestra ese commit. Añade al informe la sección «L.2 (paso 10)» con la salida literal y **guárdalo**.

---

## Parte B: conversación NUEVA

Jose abre una **conversación nueva** con el agente (mismo MCP `civil3d`, sin pegarle nada de este prompt) y escribe
**solo** esto:

11. **El mismo pedido con otras palabras (decisivo).** **"Necesito una capa nueva que se llame MCP Receta B, en
    verde."** Lo esperado, sin que nadie se lo diga al agente:
    - llama a `buscar_api` (con la consulta que él elija) y la primera receta de **RECETAS** es `crear_capa_con_color`;
    - llama a `ejecutar_receta` con `nombre: crear_capa_con_color`, `parametros` `{"nombre_capa": "MCP Receta B",
      "color": 3}` (verde es el 3) y `simular: true` (o sin `simular`), con `pedido` = las palabras de Jose. Respuesta
      con `ok`, lo mismo que devolvió `ejecutar_codigo` simulado en el paso 2 (el `resultado` del script) y el bloque
      `receta`: `"veces_usada": 0`,
      `"sumada": false` y `nota` que empieza por «Simulación: veces_usada no cambia»;
    - te enseña el resultado y te pide confirmación. Jose responde **"Sí, créala"**;
    - repite `ejecutar_receta` con `simular: false`: `ok`, bloque `receta` con `"veces_usada": 1`, `"sumada": true` y
      `nota` «Ejecución real correcta: veces_usada sube a 1. ...». La capa `MCP Receta B` aparece con color 3.
    Es **FALLO** si el agente escribe un script nuevo con `ejecutar_codigo` en vez de usar la receta, si ejecuta sin
    simular primero o si no pide confirmación. Si pregunta qué color es «verde», Jose responde «el 3».

Cuando el agente termine, Jose pega en **la misma conversación nueva** el texto siguiente (desde «Ahora actúa» hasta
«Fin de la parte B»):

> Ahora actúa como agente de validación de la versión 1.17.0 del conector Civil 3D MCP. Sigue los pasos 12 a 16, anota
> la respuesta literal y el tiempo de cada uno, no uses `ejecutar_comando` y sustituye el token por `<oculto>`.

12. **Qué hiciste (no decisivo).** Anota, en orden, las herramientas que usaste en el paso 11 con sus argumentos
    literales (la consulta de `buscar_api` y los dos `ejecutar_receta`) y el `id_log` de cada `ejecutar_receta`.

13. **Segunda reutilización real, con el valor por defecto (decisivo).** `ejecutar_receta` con
    `{"nombre": "crear_capa_con_color", "parametros": {"nombre_capa": "MCP Receta C"}, "simular": false,
    "pedido": "Crea también la capa MCP Receta C con el color por defecto"}`. Esperado: `ok`, el bloque `receta` con
    `"parametros": {"nombre_capa": "MCP Receta C", "color": 7}` (el `color` sale del `por_defecto`), `"veces_usada": 2`
    y `"sumada": true`. La capa `MCP Receta C` aparece con color 7.

14. **Un parámetro mal escrito no toca nada (decisivo).** `ejecutar_receta` con
    `{"nombre": "crear_capa_con_color", "parametros": {"nombre_capa": "MCP Receta D", "colour": 3}, "simular": false}`.
    Esperado: `"ok": false` y `error` «La receta no tiene los parámetros: colour. Parámetros de la receta: nombre_capa
    (string, obligatorio), color (number, por defecto 7).», sin `id_log` (el puente no llamó al plugin). Después
    `leer_log` con `args={"ultimas_n": 3}`: **ninguna** línea menciona `MCP Receta D`. No hay capa `MCP Receta D` y el
    menú Deshacer no tiene entrada nueva.

15. **El log guarda el pedido (no decisivo).** `leer_log` con `args={"ultimas_n": 8}`: las ejecuciones reales de los
    pasos 11 y 13 aparecen como líneas de `ejecutar_codigo` con `ok: true` y el `pedido` literal de cada una
    (en `args` va el código de la receta: no lo pegues entero en el informe, basta con su primera línea).

16. **El contador en el archivo (no decisivo).** PowerShell:
    `Select-String -Path C:\Proyectos\memoria-firma\recetas\crear_capa_con_color.yaml -Pattern 'veces_usada|ultima_vez' -Encoding UTF8`
    → `veces_usada: 2` y `ultima_vez: '<hoy>'`.

Abre el informe `herramientas-dev/informes/informe_validacion_117_<AAAAMMDD>.md` de la parte A, **añade** la sección
«Parte B» (tabla de los pasos 11 a 16) sin borrar lo anterior y **guarda el archivo**.

Fin de la parte B.

---

## Parte C: agente instalador (PowerShell y MCP)

17. **El comando también la encuentra primero (decisivo).** Desde la raíz del repo:
    `cd PuenteMcp; uv run --frozen python buscar_api.py "capa con color"; cd ..` → código de salida 0; la sección
    **RECETAS** es la primera después de la cabecera y su primera línea es
    `- crear_capa_con_color.yaml + crear_capa_con_color.cs: Crea la capa MCP Receta A con el color 3 (verde)`, con
    `veces usada 2` en la línea siguiente.

18. **Quitar `ARBA_MCP_CODIGO` (no decisivo, pero obligatorio).** Cierra Civil 3D. En PowerShell:
    ```powershell
    [Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO', $null, 'User')
    [Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO', 'User')    # no imprime nada
    ```
    Abre una ventana **nueva** de PowerShell y comprueba `Test-Path Env:ARBA_MCP_CODIGO` → `False`. Abre Civil 3D desde
    el menú Inicio o el escritorio (no desde una consola que tuviera la variable) con la copia del DWG y espera a que
    `ping` responda.

19. **`ejecutar_codigo` vuelve a estar desactivada (decisivo).**
    - `ejecutar_codigo` con `args={"codigo_csharp": "return 1;", "simular": true, "pedido": "comprueba que está desactivada"}`
      → `"ok": false` y `error` que contiene «ejecutar_codigo está desactivada».
    - `ejecutar_receta` con `{"nombre": "crear_capa_con_color", "parametros": {"nombre_capa": "MCP Receta E"}}` →
      `"ok": false`, `error` que contiene «ejecutar_codigo está desactivada» y el bloque `receta` con `"sumada": false`
      y `nota` «La ejecución falló: veces_usada no cambia.».
    - El `Select-String` del paso 16 sigue dando `veces_usada: 2`.

20. **L.2 final (no decisivo).** Corre otra vez `sincronizar_memoria.ps1` en `C:\Proyectos\memoria-firma` para subir el
    contador. `git -C C:\Proyectos\memoria-firma log -1 --name-only --format="%h %ad %s" --date=short` → incluye
    `recetas/crear_capa_con_color.yaml`.

Abre el mismo informe, **añade** la sección «Parte C» (pasos 17 a 20) y el resultado global, y **guarda el archivo**.

## Informe

Un solo archivo, **guardado en disco** (no basta con mostrarlo en el chat):
`herramientas-dev/informes/informe_validacion_117_<AAAAMMDD>.md`. Cada parte añade su sección sin borrar las
anteriores. Lleva: versión e idioma de Civil 3D, DWG usado (solo el nombre genérico, sin ruta ni datos de obra), una
tabla paso / resultado (**OK** o **FALLO**) / respuesta literal / tiempo por cada parte, los hallazgos y el **resultado
global: OK solo si los pasos decisivos 0, 3, 4, 7, 10, 11, 13, 14, 17 y 19 son OK**. Responde además a estas preguntas
para la memoria técnica:

1. Paso 0: `ping` y `GET /ping` literales, el número de herramientas (58) y los nombres de las 3 del puente.
2. Paso 2: el script completo y la expresión exacta con la que lee `nombre_capa` y `color` de `Parametros` (es lo que
   reutilizará la receta). ¿Cuántos intentos hicieron falta?
3. Paso 7: ¿el agente pidió la aprobación a Jose antes de llamar con `aprobada_por_el_dueno=true`? ¿Le dijo lo de L.2?
4. Paso 11: la consulta exacta de `buscar_api`, en qué posición salió la receta y si el agente pidió confirmación entre
   la simulación y la ejecución real.
5. Tiempos: `ms_puente` de `ejecutar_receta` simulada y real (paso 11) frente a `ejecutar_codigo` directo (paso 2).
6. Paso 19: el texto literal del error de `ejecutar_codigo` desactivada.
7. ¿Alguna entrada de Deshacer de más? (Anota el menú Deshacer antes y después de los pasos 2, 11 y 13: cada ejecución
   real debe dejar **una** entrada `Executefunction`; las simulaciones, los rechazos de `guardar_receta` y el paso 14,
   ninguna.)

Puedes copiar en el informe el YAML y el código de la receta de prueba `crear_capa_con_color` (es genérica, sin datos
de obra); **nada de otras recetas** ni del glosario de `memoria-firma`, que es privado. **No pegues líneas de
`mcp_log.jsonl` con rutas reales**: sustitúyelas por `<ruta>`.
