# Prompt de validación de la 1.17.5 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Validación corta: solo lo que salió mal en `VALIDACION_116.md` con la 1.17.4** (informe
  `informe_validacion_116_20261114.md`). Todo lo demás de la 116 pasó y no se repite.
  1. En los pasos 4b (simulación) y 5a (escritura real) la línea creada con `F.Agregar` salió con
     `despues.objetos_creados: 0` y `datos.objetos.creados: []` (y el `mensaje` de 5a decía `0 objeto(s) creado(s)`),
     aunque la línea sí se creó.
  2. Un `_.UNDO 1` después de 5a **no** quitó la línea (5c contó N0 + 1). El informe no anotó el menú Deshacer, y el
     paso 5a pedía a Jose un `ZOOM` → `Extensión` entre 5a y 5b: un zoom deja **su propia entrada** en Deshacer, así que
     ese `_.UNDO 1` pudo deshacer el zoom y no la línea.
- **Dibujo**: una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica y un
  ensamblaje, nada más) guardada como `C:\Proyectos\dibujos_prueba\PRUEBA_1175.dwg`. Tiene que estar **guardada en
  disco** (así la copia de seguridad es `copia_de_disco`). En esa carpeta aparecerán `mcp_log.jsonl` y `backups\`.
  Ningún dato real.
- **Instalación.** El plugin lo instala el agente instalador (Prompt L.1) antes de este prompt, con la rama
  **`tarea/3.4-codigo-objetos-deshacer`** (1.17.5). En `PuenteMcp`, `uv sync`, reinicia el puente y recarga el MCP
  `civil3d`.
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.17.5** (correcciones, ninguna herramienta nueva):
  1. `ejecutar_codigo` mira lo que creó el script por **cinco fuentes** y la respuesta dice qué vio cada una en
     **`datos.objetos.fuente_deteccion`**: `fachada` (lo que añade `F.Agregar`), `transaccion_script` (la transacción
     del script, antes de confirmarla), `transaccion_plugin` (la del plugin, después: el camino que falló en la 116),
     `eventos` (eventos de la base de datos mientras corre el script: también falló) y `eventos_al_cerrar` (los mismos
     eventos mientras se confirma o se aborta, solo como diagnóstico). `creados_por` dice qué fuentes vieron lo creado.
  2. **`datos.deshacer`**: `comando` (la variable `CMDNAMES` mientras corre el script: el pseudocomando que recoge la
     entrada de Deshacer), `undoctl_antes`, `undoctl_despues`, `undoctl_texto` (`UNDOCTL` en palabras) y
     `dbmod_antes`/`dbmod_despues`.
  3. Un script puede escribir `TransactionManager` sin el error CS0104 (nombre ambiguo).
- **Los pasos 0, 4, 5, 6, 8 y 10 son los decisivos.** Los pasos 3 a 9 repiten los pasos 4 y 5 de la 116 (y su
  numeración entre paréntesis).
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.17.5):
  `Transaction.GetAllObjects()` antes y después de confirmar la transacción del script, cuándo llegan los eventos
  `ObjectAppended`/`ObjectModified`/`ObjectErased` en una transacción, la relectura de lo creado tras abortar, las
  variables `CMDNAMES`, `UNDOCTL` y `DBMOD` y el alias `TransactionManager`.

---

Eres el agente de validación de la versión 1.17.5 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo; `datos.objetos.fuente_deteccion` y `datos.deshacer` van **enteros** siempre que salgan) y **el tiempo total de
cada paso** (`ms_puente`).

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` se usa **solo** donde este prompt lo indica (los `_.UNDO 1` de los pasos 7, 8b y 9, y el
  `_.ZOOM _E` del paso 9). Si algo no funciona, es un hallazgo: no lo arregles con otro comando ni con otro script.
- En `codigo_csharp` y `pedido` usa **exactamente** el texto que se indica (los `\n` son saltos de línea dentro del texto).
- **Menú Deshacer, con su texto literal**: es la lista desplegable que se abre con la **flecha junto al botón Deshacer**
  de la barra de acceso rápido. Anota **antes y después de cada paso** todas sus entradas, **de arriba abajo, tal como
  aparecen escritas** (por ejemplo `Executefunction`, `Zoom`...), y cierra la lista sin pulsar ninguna (pulsar una
  entrada deshace hasta ella). Si la lista es larga, basta con las cinco de arriba y cuántas hay en total.
- **Jose no debe tocar la vista entre los pasos 6 y 8**: ni `ZOOM`, ni la rueda del ratón, ni encuadre, ni ningún otro
  comando. Un zoom deja su propia entrada en Deshacer y el `_.UNDO 1` del paso 7 lo desharía a él en vez de la línea.
  Si quiere ver la línea, que lo haga **después** del paso 8 (y que lo anotes).
- **`backups\`**: anota cuántos archivos hay antes del paso 3 y después de los pasos 3, 6 y 9.
- **Contar las líneas** es siempre esta llamada (la del paso 4a de la 116): `ejecutar_codigo` con
  `args={"codigo_csharp":"int n = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line) n++;\nreturn n;","pedido":"cuenta las líneas del espacio modelo"}`.
  Lo que importa es `resultado`.

## Pasos

0. **Comprobar la instalación (decisivo).** `ping` con `args={"pedido":"comprueba que el plugin responde"}` →
   `"version": "1.17.5"` y `"hay_dibujo": true`. En PowerShell: `curl.exe -s http://127.0.0.1:8765/ping` →
   `{"ok":true,"servidor":"ArbaMcp","version":"1.17.5"}`. La lista de herramientas: **58** (55 del plugin más
   `buscar_api`, `guardar_receta` y `ejecutar_receta`). Si la versión no es `1.17.5`, **para aquí**.

1. **Activar `ejecutar_codigo`** (no decide). En PowerShell:
   `[Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')`. Anota lo que sale. Si no es `1` (la validación de
   la 3.2 la quita al final): cierra Civil 3D,
   `[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO','1','User')`, comprueba que la lectura da `1` y abre Civil
   3D **desde el menú Inicio o el acceso directo** con `PRUEBA_1175.dwg`; recarga el MCP `civil3d` y repite `ping`
   (`"hay_dibujo": true`).

2. **Punto de partida** (no decide). Con `PRUEBA_1175.dwg` recién abierto: anota el **menú Deshacer** (lo normal es que
   esté vacío) y cuántos archivos hay en `backups\` (puede no existir todavía).

3. **(116, 4a) Contar las líneas en simulación** (no decide). La llamada de contar. Esperado: `ok: true`,
   `simulado: true`, `resultado` = **N0** (en el dibujo base, `0`), `copia` con una copia nueva en `backups\`.
   Anota `datos.objetos.fuente_deteccion` y `datos.deshacer` enteros, y el menú Deshacer después (lo normal: **una**
   entrada nueva, la de la simulación, que no deshace nada).

4. **(116, 4b) La línea en simulación (decisivo).** `ejecutar_codigo` con
   `args={"codigo_csharp":"var l = new Line(new Point3d(1000, 2000, 0), new Point3d(1100, 2000, 0));\nF.Agregar(l);\nF.Escribir(\"línea de prueba\");\nreturn new { largo = l.Length, capa = l.Layer };","pedido":"dibuja una línea de prueba de 100 m"}`.
   Esperado:
   - `ok: true`, `simulado: true`, `accion` empieza por `Ejecutar un script de 4 línea(s) en simulación`;
   - **`antes.objetos_creados: 0` y `despues.objetos_creados: 1`** (en la 116 salió `0`: es lo que decide el paso);
   - `cambios` incluye `objetos_creados` (si incluye también `objetos_modificados`, anota qué objeto sale en
     `datos.objetos.modificados`; lo normal es el espacio modelo, `BlockTableRecord`);
   - **`datos.objetos.creados` con un elemento con `tipo: "Line"`** (anota su `handle` y su `capa`);
   - `datos.objetos.fuente_deteccion.creados_por` contiene **`fachada`**; anota si contiene también
     `transaccion_script`, `transaccion_plugin` o `eventos`;
   - `resultado: {"largo":100,"capa":"0"}` y `datos.mensajes: ["línea de prueba"]`;
   - `copia` con `ruta` en `backups\` (`reutilizada: true` es correcto: el `.dwg` no cambió);
   - `avisos`: el primero empieza por `Simulación: el script se ejecutó de verdad dentro de una transacción que se abortó
     al final`; **ningún** aviso empieza por `ATENCIÓN` (si sale, cópialo entero: diría que la línea siguió en el
     dibujo tras abortar).
   Anota `datos.objetos.fuente_deteccion` y `datos.deshacer` **enteros** (en `eventos_al_cerrar`, `momento` debe ser
   `al abortar`) y el menú Deshacer después.

5. **(116, 4c) Contar otra vez (decisivo).** La llamada de contar. Esperado: `resultado` = **N0** (la simulación no dejó
   la línea) y `datos.compilacion.de_cache: true`. Menú Deshacer después.

6. **(116, 5a) La línea de verdad (decisivo).** **Desde aquí hasta el final del paso 8, nadie toca la vista de Civil
   3D.** Antes de llamar, anota el menú Deshacer. La llamada del paso 4 con `"simular": false` y
   `"pedido":"dibuja de verdad la línea de prueba de 100 m"`. Esperado:
   - `simulado: false`; **`mensaje` empieza por `Script de 4 línea(s) ejecutado y confirmado: 1 objeto(s) creado(s)`**;
   - **`despues.objetos_creados: 1`**; `datos.objetos.creados` con un elemento `tipo: "Line"`;
   - `resultado.largo: 100`; `copia` presente;
   - `datos.objetos.fuente_deteccion` y `datos.deshacer` **enteros** (en `eventos_al_cerrar`, `momento` debe ser
     `al confirmar`; anota `creados` y `errores` de esa fuente: dicen si los eventos llegan al confirmar);
   - si sale un aviso que empieza por `Los eventos de la base de datos vieron`, cópialo entero.
   Menú Deshacer **justo después**: debe haber **una sola entrada nueva** arriba del todo (anota su texto literal).
   `leer_log` con `args={"ultimas_n":1}`: herramienta `ejecutar_codigo`, `ok: true`, en `args` el `codigo_csharp`,
   `"simular": false` y el `pedido` (`leer_log` no deja entrada en Deshacer: compruébalo en el menú).

7. **(116, 5b) Deshacer una vez** (no decide). Anota el menú Deshacer. `ejecutar_comando` con
   `args={"comando":"_.UNDO 1"}` → `"terminado"`. Menú Deshacer después: la entrada de arriba del paso 6 ya no está.

8. **(116, 5c) Contar (decisivo).** La llamada de contar. Esperado: `resultado` = **N0**: un solo `_.UNDO 1` quitó la
   línea. **Si da N0 + 1**, es un hallazgo; sigue con el 8b:
   - **8b**: anota el menú Deshacer literal; `ejecutar_comando` con `args={"comando":"_.UNDO 1"}` → `"terminado"`;
     menú Deshacer otra vez; contar otra vez y anota el `resultado`. (Si ahora da N0, entre la escritura y el primer
     `_.UNDO 1` hubo otra entrada: el menú del paso 6 debería decir cuál.)

9. **Comprobar la sospecha del zoom** (no decide; explica el paso 5c de la 116). Ahora sí se toca la vista, pero solo
   con este comando.
   - **9a**: la llamada del paso 6 con `"pedido":"dibuja otra vez la línea de prueba para comprobar el deshacer"`
     (`simular: false`) → `1 objeto(s) creado(s)`. Menú Deshacer.
   - **9b**: `ejecutar_comando` con `args={"comando":"_.ZOOM _E"}` → `"terminado"`. Menú Deshacer: anota si apareció
     una entrada nueva encima de la del 9a y su texto literal.
   - **9c**: `ejecutar_comando` con `args={"comando":"_.UNDO 1"}` → `"terminado"`; menú Deshacer; contar. Se espera
     **N0 + 1**: ese `_.UNDO 1` deshizo el zoom, no la línea (como en la 116).
   - **9d**: `ejecutar_comando` con `args={"comando":"_.UNDO 1"}` → `"terminado"`; menú Deshacer; contar. Se espera
     **N0**.
   Si en 9c ya sale N0 (el zoom no dejó entrada), la causa del paso 5c de la 116 es otra: anótalo con los menús.

10. **`TransactionManager` sin nombre ambiguo (decisivo).** `ejecutar_codigo` con
    `args={"codigo_csharp":"TransactionManager a = F.Db.TransactionManager;\nTransactionManager b = F.Doc.TransactionManager;\nreturn a != null && b != null;","pedido":"comprueba que TransactionManager no es ambiguo"}`
    (simulación). Esperado: `ok: true`, `simulado: true`, `resultado: true` y `cambios: []` (el script no cambia
    nada). Con la 1.17.4 daba `ok: false` con un error `CS0104` (referencia ambigua entre
    `Autodesk.AutoCAD.ApplicationServices.TransactionManager` y `Autodesk.AutoCAD.DatabaseServices.TransactionManager`).
    Si sale `CS0104`, copia el error literal.

11. **Cierre** (no decide). `leer_historial` con `args={"ultimas_n":20}`: anota las líneas de `ejecutar_codigo` (las de
    simulación y aplicado dicen ahora `creados N según <fuentes>`). **No guardes el dibujo.** Deja la variable
    `ARBA_MCP_CODIGO` como la encontraste en el paso 1: si estaba vacía y la pusiste tú, cierra Civil 3D y quítala con
    `[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO',$null,'User')`; anota cómo quedó.

## Informe

**Guarda el informe como archivo** (no basta con el resumen en el chat) en
`herramientas-dev/informes/informe_validacion_1175_<AAAAMMDD>.md`, con:

- Versión de Civil 3D, rama y commit instalados, versión de `ping`, DWG usado (nombre y carpeta) y el valor de
  `ARBA_MCP_CODIGO` al empezar y al terminar.
- Una tabla paso / resultado (OK, FALLO, HALLAZGO) / respuesta literal / tiempo (`ms_puente`).
- **El menú Deshacer antes y después de cada paso, con el texto literal de cada entrada**, y los archivos de
  `backups\` antes y después de los pasos 3, 6 y 9.
- `datos.objetos.fuente_deteccion` y `datos.deshacer` **enteros** de los pasos 3, 4 y 6, y una frase que diga qué
  fuentes vieron la línea en la simulación (paso 4) y en la escritura real (paso 6), y qué dijo `eventos_al_cerrar`
  en cada uno.
- Si Jose miró la línea en pantalla, en qué paso (debe ser después del 8).
- La conclusión del paso 9: si el `_.UNDO 1` del paso 5c de la 116 deshizo un zoom.
- Una lista final de los miembros de la sección 1.17.5 de `miembros_por_verificar_civil3d.md` que pasan a
  `ejecutado en Civil 3D 2027` con la fecha, y los que fallaron con su error literal.
