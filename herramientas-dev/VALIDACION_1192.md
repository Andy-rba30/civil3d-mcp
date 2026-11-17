# Prompt de validación de la 1.19.2 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Validación corta: solo lo que cambia en la 1.19.2** (una corrección de `ejecutar_codigo`, ninguna herramienta nueva).
  En la ronda C (17/11/2026, `VALIDACION_1176.md`, paso 9, con la 1.19.0) un script que alargaba con `Curve.Extend` una
  línea **abierta para lectura** cerró Civil 3D en vez de dar un error. La 1.19.1 lo paró **con** `solo_lectura=true`.
  La 1.19.2 lo para también **sin** `solo_lectura`: si el compilador ve que el script cambia un objeto que abrió para
  lectura (`F.Abrir(id)` sin `true`, o `GetObject` con `OpenMode.ForRead`), el script **no compila** y el error dice la
  línea, de dónde viene el objeto y cómo corregirlo («abre el objeto con F.Abrir(id, true) ... o llama a UpgradeOpen()»).
  Lo que el compilador no puede seguir (un parámetro, una colección que pasa a otro método) se deja pasar, como antes. Con
  `solo_lectura=true` no cambia nada.
- **Cómo está pensada para no cerrar Civil 3D**: los pasos que esperan el rechazo (3, 4 y 5) se hacen con el dibujo **sin
  ninguna línea**. Si por un fallo el script compilara, no encontraría ninguna línea que cambiar (devuelve `"sin líneas"` o
  `0`): el fallo se ve en la respuesta sin arriesgar Civil 3D. La línea se dibuja después (paso 6) para probar los scripts
  corregidos (pasos 7 y 8), que abren la línea para escritura.
- **Dibujo**: una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica y un
  ensamblaje, sin líneas) guardada como `C:\Proyectos\dibujos_prueba\PRUEBA_1192.dwg`. Tiene que estar **guardada en
  disco** (así la copia de seguridad es `copia_de_disco`). En esa carpeta aparecerán `mcp_log.jsonl` y `backups\`.
  Ningún dato real.
- **Instalación.** El plugin lo instala el agente instalador (Prompt L.1) antes de este prompt, con la rama
  **`tarea/6.1-codigo-escritura-segura`** (la 1.19.2). En `PuenteMcp`, `uv sync`, reinicia el puente y recarga el MCP
  `civil3d`.
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Los pasos 0, 3, 4b, 5, 7, 8 y 9 son los decisivos.** El 3 es el caso de la ronda C sin `solo_lectura`; el 4b, el
  mismo rechazo con `F.Tr.GetObject` de la API real; el 5, que con `solo_lectura` sigue el texto de la 1.19.1; el 7 y el
  8, que las dos correcciones que propone el error funcionan en Civil 3D; el 9, que la receta guardada sigue compilando.
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.19.2):
  el análisis con las DLL reales (`F.Abrir`, `Transaction.GetObject`), `Curve.Extend` sobre una línea abierta para
  escritura con `F.Abrir<Entity>(id, true)` y `DBObject.UpgradeOpen()` dentro de `F.Tr`.

---

Eres el agente de validación de la versión 1.19.2 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo `ejecutar_receta`, que es del puente (sus argumentos van directos), y los pasos que indican PowerShell.
Ejecuta los pasos en orden, anota la respuesta **literal** (JSON completo; `avisos`, `datos.compilacion` y
`datos.objetos` van **enteros** siempre que salgan) y **el tiempo total de cada paso** (`ms_puente`).

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- **Si Civil 3D se cierra** en cualquier paso, o la respuesta es «Error de conexión»: es un **FALLO grave**. **Para
  aquí**, anota el paso, el tiempo y lo que haya en `%LOCALAPPDATA%\Autodesk\CER\`, y no sigas.
- No uses `ejecutar_comando` en esta validación. Si algo no funciona, es un hallazgo: no lo arregles con otro comando ni
  con otro script.
- En `codigo_csharp` y `pedido` usa **exactamente** el texto que se indica (los `\n` son saltos de línea dentro del
  texto).
- **Menú Deshacer, con su texto literal**: es la lista desplegable que se abre con la **flecha junto al botón Deshacer**
  de la barra de acceso rápido. Anota **antes y después de cada paso** sus entradas, **de arriba abajo, tal como
  aparecen escritas** (por ejemplo `Executefunction`), y cierra la lista sin pulsar ninguna (pulsar una entrada deshace
  hasta ella). Si la lista es larga, basta con las cinco de arriba y cuántas hay en total.
- **`backups\`**: anota cuántos archivos hay (y el nombre del más reciente) antes del paso 3 y después de los pasos 3,
  4, 5, 6, 7, 8 y 9.
- Abreviaturas:
  - **Contar las líneas** es siempre esta llamada: `ejecutar_codigo` con
    `args={"codigo_csharp":"int n = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line) n++;\nreturn n;","solo_lectura":true,"pedido":"cuenta las líneas del espacio modelo sin tocar el dibujo"}`.
    Lo que importa es `resultado`.
  - **Medir la línea** es siempre esta llamada: `ejecutar_codigo` con
    `args={"codigo_csharp":"double largo = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) largo = x.Length;\nreturn largo;","solo_lectura":true,"pedido":"mide la línea de prueba"}`.
    Lo que importa es `resultado`.
  - **El error de compilación** de los pasos 3 y 4 empieza siempre por `Error de Civil 3D: ArgumentException:
    ejecutar_codigo: el código no se puede ejecutar (1 uso no permitido). No se ejecutó nada: sin copia de seguridad, sin
    contexto de comando y sin entrada en Deshacer.` (anota si el prefijo `Error de Civil 3D: ArgumentException: ` sale
    distinto: lo que decide es el resto).

## Pasos

0. **Comprobar la instalación (decisivo).** `ping` con `args={"pedido":"comprueba que el plugin responde"}` →
   `"version": "1.19.2"` y `"hay_dibujo": true`. En PowerShell: `curl.exe -s http://127.0.0.1:8765/ping` →
   `{"ok":true,"servidor":"ArbaMcp","version":"1.19.2"}`. La lista de herramientas: **59** (55 del plugin más
   `buscar_api`, `guardar_receta`, `ejecutar_receta` y `verificar_norma`). Si la versión no es `1.19.2`, **para aquí**:
   los pasos siguientes cuentan con el rechazo nuevo.

1. **Activar `ejecutar_codigo`** (no decide). En PowerShell:
   `[Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')`. Anota lo que sale. Si no es `1`: cierra Civil
   3D, `[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO','1','User')`, comprueba que la lectura da `1` y abre
   Civil 3D **desde el menú Inicio o el acceso directo** con `PRUEBA_1192.dwg`; recarga el MCP `civil3d` y repite
   `ping` (`"hay_dibujo": true`).

2. **Punto de partida** (no decide). Con `PRUEBA_1192.dwg` recién abierto: anota el **menú Deshacer** (lo normal es que
   esté vacío) y cuántos archivos hay en `backups\` (puede no existir todavía). **Contar las líneas** → **`resultado:
   0`**. Si da otra cosa, **para aquí**: los pasos 3 a 5 necesitan el dibujo sin líneas.

### A. Sin `solo_lectura`, un cambio sobre un objeto abierto para lectura no compila

3. **El script de la ronda C, sin `solo_lectura` (decisivo).** Es el del paso 9 de `VALIDACION_1176.md` (que con la 1.19.0
   cerró Civil 3D), ahora **sin** `solo_lectura` (simulación por defecto). `ejecutar_codigo` con
   `args={"codigo_csharp":"Line l = null;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) l = x;\nif (l == null) return \"sin líneas\";\nl.Extend(false, new Point3d(l.EndPoint.X + 50, l.EndPoint.Y, 0));\nreturn l.Length;","pedido":"intenta alargar la línea sin abrirla para escritura"}`.
   Esperado:
   - `ok: false`, la respuesta llega en menos de 5 s y **Civil 3D sigue abierto**;
   - el error empieza por **el error de compilación** y contiene, literal: `Línea 5, columna 3: [bloqueado: abierto para
     lectura] Autodesk.AutoCAD.DatabaseServices.Curve.Extend sobre 'l', abierto para lectura en la línea 3
     (F.Abrir<Entity>(id)): cambiar un objeto abierto para lectura no siempre da un error en Civil 3D: con Curve.Extend,
     Civil 3D 2027 se cerró (17/11/2026). Para cambiarlo, abre el objeto con F.Abrir(id, true) (o con OpenMode.ForWrite
     en GetObject), o llama a UpgradeOpen() sobre él antes de cambiarlo.` Copia el error **entero**;
   - menú Deshacer **igual** que en el paso 2 y **ningún archivo nuevo** en `backups\` (no compiló: sin copia, sin
     contexto de comando y sin entrada en Deshacer).
   Si sale `ok: true` con `resultado: "sin líneas"`, es un **FALLO** (el script compiló: el rechazo nuevo no funcionó en
   Civil 3D); anótalo con la respuesta entera y sigue.

4. **Otras dos formas de olvidar el `true`.** Las dos sin `solo_lectura`:
   - **4a** (no decide; una propiedad): `ejecutar_codigo` con
     `args={"codigo_csharp":"foreach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) x.Layer = \"0\";\nreturn 0;","pedido":"intenta cambiar la capa sin abrir para escritura"}`.
     Esperado: `ok: false`, **el error de compilación**, y contiene `Línea 2, columna 42: [bloqueado: abierto para
     lectura] Autodesk.AutoCAD.DatabaseServices.Entity.Layer (asignación) sobre 'x', abierto para lectura en la línea 2
     (F.Abrir<Entity>(id))`.
   - **4b** (decisivo; `GetObject` de la API real, con sus sobrecargas): `ejecutar_codigo` con
     `args={"codigo_csharp":"foreach (var id in F.EspacioModelo())\n{\n    var e = (Entity)F.Tr.GetObject(id, OpenMode.ForRead);\n    if (e is Line) e.Erase();\n}\nreturn 0;","pedido":"intenta borrar la línea sin abrirla para escritura"}`.
     Esperado: `ok: false`, **el error de compilación**, y contiene `Línea 4, columna 22: [bloqueado: abierto para
     lectura] Autodesk.AutoCAD.DatabaseServices.DBObject.Erase sobre 'e', abierto para lectura en la línea 3
     (F.Tr.GetObject(id, OpenMode.ForRead))`. Si además sale un error `CS`, anótalo entero.
   Después de las dos: menú Deshacer igual que en el paso 2 y ningún archivo nuevo en `backups\`. Si alguna sale con
   `ok: true` (y `resultado: 0`), es un **FALLO**: anótalo y sigue.

5. **Con `solo_lectura`, como en la 1.19.1 (decisivo).** El script del paso 3 con `"solo_lectura":true` y
   `"pedido":"intenta alargar la línea con solo lectura"`. Esperado, igual que el paso 9 del informe 1176_b:
   `ok: false`, **el error de compilación**, y contiene `Línea 5, columna 3: [bloqueado: solo lectura]
   Autodesk.AutoCAD.DatabaseServices.Curve.Extend: con solo_lectura=true el script solo puede leer` y `solo se pueden
   llamar métodos de lectura`. **No** debe contener `abierto para lectura`. Menú Deshacer igual y ningún archivo nuevo
   en `backups\`.

### B. Las correcciones que propone el error funcionan

6. **La línea de verdad** (no decide). `ejecutar_codigo` con
   `args={"codigo_csharp":"var l = new Line(new Point3d(1000, 2000, 0), new Point3d(1100, 2000, 0));\nF.Agregar(l);\nF.Escribir(\"línea de prueba\");\nreturn new { largo = l.Length, capa = l.Layer };","simular":false,"pedido":"dibuja de verdad la línea de prueba de 100 m"}`.
   Esperado, como el paso 6 de `VALIDACION_1176.md`: `ok: true`, `simulado: false`, `mensaje` empieza por `Script de 4
   línea(s) ejecutado y confirmado: 1 objeto(s) creado(s)`, `resultado: {"largo":100,"capa":"0"}`, `copia` presente,
   `datos.compilacion.escrituras_reconocidas: 1` y una entrada nueva en el menú Deshacer (anota su texto literal).
   Después, **medir la línea** → **`resultado: 100`**.

7. **Abriendo para escritura (decisivo).** El script del paso 3 con `F.Abrir<Entity>(id, true)`, sin `solo_lectura`
   (simulación por defecto): `ejecutar_codigo` con
   `args={"codigo_csharp":"Line l = null;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id, true) is Line x) l = x;\nif (l == null) return \"sin líneas\";\nl.Extend(false, new Point3d(l.EndPoint.X + 50, l.EndPoint.Y, 0));\nreturn l.Length;","pedido":"simula alargar la línea 50 m"}`.
   Esperado:
   - `ok: true`, `simulado: true`, **`resultado: 150`** (100 m más los 50 m del `Extend`), Civil 3D sigue abierto;
   - `datos.compilacion.escrituras_reconocidas: 2` (`F.Abrir` con `true` y `Extend`); `copia` presente;
   - anota `antes`, `despues`, `datos.objetos` (se espera la línea en `modificados`) y `avisos` **enteros**;
   - menú Deshacer: **una** entrada `Executefunction` nueva (la de la simulación, como siempre en `ejecutar_codigo` sin
     `solo_lectura`).
   Después, **medir la línea** → **`resultado: 100`** (la simulación se abortó). Si el paso da `ok: false` con un error
   de compilación, cópialo entero: es un **FALLO** (el script corregido debía compilar).

8. **Con `UpgradeOpen()` antes del cambio (decisivo).** El script del paso 3 con `l.UpgradeOpen();` antes del `Extend`,
   sin `solo_lectura`: `ejecutar_codigo` con
   `args={"codigo_csharp":"Line l = null;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) l = x;\nif (l == null) return \"sin líneas\";\nl.UpgradeOpen();\nl.Extend(false, new Point3d(l.EndPoint.X + 50, l.EndPoint.Y, 0));\nreturn l.Length;","pedido":"simula alargar la línea 50 m con UpgradeOpen"}`.
   Esperado: `ok: true`, `simulado: true`, **`resultado: 150`**, `datos.compilacion.escrituras_reconocidas: 2`
   (`UpgradeOpen` y `Extend`), Civil 3D sigue abierto y una entrada `Executefunction` nueva en el menú Deshacer. Anota
   `datos.objetos` y `avisos` enteros. Después, **medir la línea** → **`resultado: 100`**. Si da `ok: false`, copia el
   error entero (dice si `UpgradeOpen` falló dentro de `F.Tr`).

### C. Las recetas guardadas siguen compilando

9. **`crear_capa_con_color`, en simulación (decisivo).** Necesita `MEMORIA_FIRMA_DIR` y la receta de `VALIDACION_117.md`
   (en PowerShell: `Test-Path "$env:MEMORIA_FIRMA_DIR\recetas\crear_capa_con_color.cs"` → `True`; si no está, anótalo y
   salta al paso 10). Anota `veces_usada` de su `.yaml`. `ejecutar_receta` con
   `{"nombre":"crear_capa_con_color","parametros":{"nombre_capa":"MCP Receta 1192","color":3},"pedido":"simula la capa MCP Receta 1192 con la receta"}`
   (simulación por defecto). Esperado: `ok: true`, `simulado: true`, `resultado: {"capa":"MCP Receta 1192","color":3,"creada":true}`,
   `datos.compilacion.escrituras_reconocidas: 5`, `receta.sumada: false` y `veces_usada` **igual** que antes (es una
   simulación). Si responde un error de compilación con `abierto para lectura`, es un **FALLO**: cópialo entero (la
   receta abre la tabla de capas con `true` y no debía rechazarse).

10. **Cierre** (no decide). **Contar las líneas** → `resultado: 1`. `leer_historial` con `args={"ultimas_n":20}`: anota
    las líneas de `ejecutar_codigo`. **No guardes el dibujo.** Deja la variable `ARBA_MCP_CODIGO` como la encontraste en
    el paso 1: si estaba vacía y la pusiste tú, cierra Civil 3D y quítala con
    `[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO',$null,'User')`; anota cómo quedó.

## Informe

**Guarda el informe como archivo** (no basta con el resumen en el chat) en
`herramientas-dev/informes/informe_validacion_1192_<AAAAMMDD>.md`, con:

- Versión de Civil 3D, rama y commit instalados, versión de `ping`, DWG usado (solo el nombre de la copia) y el valor de
  `ARBA_MCP_CODIGO` al empezar y al terminar.
- Una tabla paso / resultado (OK, FALLO, HALLAZGO) / respuesta literal / tiempo (`ms_puente`).
- **Los errores literales y enteros de los pasos 3, 4a, 4b y 5**, y si Civil 3D siguió abierto en cada uno.
- `datos.compilacion`, `datos.objetos`, `antes`, `despues` y `avisos` **enteros** de los pasos 6, 7 y 8, y el
  `resultado` de cada **medir la línea**.
- La respuesta literal del paso 9 (y si se saltó, por qué) y `veces_usada` antes y después.
- **El menú Deshacer antes y después de cada paso, con el texto literal de cada entrada**, y los archivos de
  `backups\` (número y el más reciente) antes del paso 3 y después de los pasos 3, 4, 5, 6, 7, 8 y 9.
- Una lista final de los miembros de la sección 1.19.2 de `miembros_por_verificar_civil3d.md` que pasan a
  `ejecutado en Civil 3D 2027` con la fecha, y los que fallaron con su error literal.
