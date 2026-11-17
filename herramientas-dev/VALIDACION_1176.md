# Prompt de validación de la 1.17.6 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Validación corta: solo lo que cambia en la 1.17.6** (dos correcciones, ninguna herramienta nueva):
  1. `crear_superficie_tin` daba el aviso `No se pudo releer el estilo de la superficie creada (Entity.StyleId):
     'intervalos_curvas' usa el estilo previsto.` en cada creación con intervalos de curvas (informe 1.17.2, pasos 2
     y 4). La causa (confirmada en el catálogo): el plugin leía `StyleId` por reflexión y Civil 3D tiene en
     `Surface` otro `StyleId` **solo de escritura**, así que salía vacío sin error. Ahora se lee directamente.
  2. Cada simulación de `ejecutar_codigo`, también un simple conteo, dejaba una entrada `Executefunction` en el menú
     Deshacer y hacía copia de seguridad (informe 1.17.5). Ahora hay un parámetro nuevo, **`solo_lectura`** (por
     defecto `false`): con `true`, el script corre como una lectura, con el dibujo bloqueado para lectura, **sin copia
     de seguridad y sin entrada en Deshacer**, y no compila si usa algo que escribe (`F.Agregar`, `OpenMode.ForWrite`,
     `F.Abrir(id, true)`...). Si aun así intenta escribir, responde error sin tocar el dibujo. **Sin `solo_lectura`,
     todo sigue como en la 1.17.5.**
  3. Lo mismo que el punto 1 en `crear_alineamiento_desde_polilinea`, `crear_perfil_terreno_y_vista` (perfil y vista),
     `crear_rasante` y `calcular_volumenes`: el estilo se relee directamente. Hasta ahora, si la lectura fallaba, la
     respuesta repetía el estilo previsto **sin decirlo**; ahora lo dice en `avisos` («No se pudo releer el estilo...»)
     y, si el estilo releído no es el pedido, responde error.
  4. `ejecutar_receta` (del puente) acepta `solo_lectura`, con el mismo significado que en `ejecutar_codigo`.
  5. `ejecutar_codigo` no compila `Database.AddDBObject` (ni con `solo_lectura` ni sin ella): hace residente un objeto
     fuera de la transacción y, al abortar una simulación, quedaría en el dibujo.
  6. Huecos que encontró la auditoría de la ronda C en las reglas de `ejecutar_codigo`, cerrados: con `solo_lectura`, los
     gestores que cambian el dibujo sin ser `DBObject` (presentaciones, estados de capa) y el `OpenMode` escondido en
     tipos, genéricos o delegados; en todo modo, las asignaciones por desestructuración (`(a.b, _) = ...`) y los
     gemelos de `BindXrefs` (`XBindXrefs`, `DetachXref`...) y `DisableUndoRecording`.
- **Dibujo**: una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica y un
  ensamblaje, nada más) guardada como `C:\Proyectos\dibujos_prueba\PRUEBA_1176.dwg`. Tiene que estar **guardada en
  disco** (así la copia de seguridad es `copia_de_disco`). En esa carpeta aparecerán `mcp_log.jsonl` y `backups\`.
  Ningún dato real.
- El archivo de puntos `C:\Proyectos\dibujos_prueba\terreno_corredor.csv` de `VALIDACION_111.md` y
  `VALIDACION_1172.md`. Si no está, créalo con exactamente estas 10 líneas (9 puntos PENZD de un plano inclinado
  genérico, cotas de 97,5 a 111,5):

  ```
  # terreno de prueba del corredor, 9 puntos PENZD
  1,900.0,1900.0,97.5,TN
  2,1300.0,1900.0,99.5,TN
  3,1700.0,1900.0,101.5,TN
  4,900.0,2150.0,102.5,TN
  5,1300.0,2150.0,104.5,TN
  6,1700.0,2150.0,106.5,TN
  7,900.0,2400.0,107.5,TN
  8,1300.0,2400.0,109.5,TN
  9,1700.0,2400.0,111.5,TN
  ```

- **Instalación.** El plugin lo instala el agente instalador (Prompt L.1) antes de este prompt, con la rama que indique
  el dueño (esta corrección va en **`tarea/3.6-correcciones-1176`**, la 1.17.6). En `PuenteMcp`, `uv sync`, reinicia el
  puente y recarga el MCP `civil3d` (el parámetro nuevo `solo_lectura` solo aparece después de recargar).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Los pasos 0, 3, 5, 6, 7, 8, 9, 10, 13, 15, 16 y 17 son los decisivos.** Los pasos 5, 6 y 10 repiten la 1.17.5 (la escritura sigue
  igual); el 7 es el conteo de solo lectura; el 8 y el 9, los intentos de escribir con solo lectura.
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección
  1.17.6): `Autodesk.Civil.DatabaseServices.Entity.StyleId`/`StyleName` con enlace directo sobre una `TinSurface`,
  `Document.LockDocument(DocumentLockMode.Read, ...)` alrededor de un script en contexto de aplicación, qué hace AutoCAD
  si un script de solo lectura intenta cambiar un objeto (`Curve.Extend`), y `DBMOD` en contexto de aplicación, y la relectura del estilo de `Alignment`, `Profile` y `ProfileView`.

---

Eres el agente de validación de la versión 1.17.6 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo; `avisos`, `datos.deshacer` y `datos.compilacion` van **enteros** siempre que salgan) y **el tiempo total de
cada paso** (`ms_puente`).

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` se usa **solo** donde este prompt lo indica (el `_.UNDO 1` del paso 10 y la capa y la polilínea
  del paso 12). Si algo no funciona, es un
  hallazgo: no lo arregles con otro comando ni con otro script, y **no cambies ningún estilo** del dibujo a mano.
- En `codigo_csharp` y `pedido` usa **exactamente** el texto que se indica (los `\n` son saltos de línea dentro del
  texto).
- **Menú Deshacer, con su texto literal**: es la lista desplegable que se abre con la **flecha junto al botón Deshacer**
  de la barra de acceso rápido. Anota **antes y después de cada paso** todas sus entradas, **de arriba abajo, tal como
  aparecen escritas** (por ejemplo `Executefunction`), y cierra la lista sin pulsar ninguna (pulsar una entrada deshace
  hasta ella). Si la lista es larga, basta con las cinco de arriba y cuántas hay en total.
- **Nadie toca la vista de Civil 3D entre los pasos 6 y 10**: ni `ZOOM`, ni la rueda del ratón, ni encuadre, ni ningún
  otro comando. Un zoom deja su propia entrada en Deshacer y el `_.UNDO 1` del paso 10 lo desharía a él (informe
  1.17.5, paso 9). Si Jose quiere mirar el dibujo, que lo haga **después** del paso 10 (y que lo anotes).
- **`backups\`**: anota cuántos archivos hay (y el nombre del más reciente) antes del paso 3 y después de los pasos 3,
  5, 6, 7, 8, 9, 11, 16 y 17.
- Abreviaturas:
  - `CSV` = `"C:\\Proyectos\\dibujos_prueba\\terreno_corredor.csv"`.
  - **Contar con solo lectura** es siempre esta llamada: `ejecutar_codigo` con
    `args={"codigo_csharp":"int n = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line) n++;\nreturn n;","solo_lectura":true,"pedido":"cuenta las líneas del espacio modelo sin tocar el dibujo"}`.
    Lo que importa es `resultado`.

## Pasos

0. **Comprobar la instalación (decisivo).** `ping` con `args={"pedido":"comprueba que el plugin responde"}` →
   `"version": "1.17.6"` y `"hay_dibujo": true`. En PowerShell: `curl.exe -s http://127.0.0.1:8765/ping` →
   `{"ok":true,"servidor":"ArbaMcp","version":"1.17.6"}`. La lista de herramientas: **58** (55 del plugin más
   `buscar_api`, `guardar_receta` y `ejecutar_receta`). En la definición de `ejecutar_codigo` debe aparecer el
   parámetro **`solo_lectura`** (boolean); anota su descripción literal. Si la versión no es `1.17.6` o falta
   `solo_lectura`, **para aquí**.

1. **Activar `ejecutar_codigo`** (no decide). En PowerShell:
   `[Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')`. Anota lo que sale. Si no es `1`: cierra Civil
   3D, `[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO','1','User')`, comprueba que la lectura da `1` y abre
   Civil 3D **desde el menú Inicio o el acceso directo** con `PRUEBA_1176.dwg`; recarga el MCP `civil3d` y repite
   `ping` (`"hay_dibujo": true`).

2. **Punto de partida** (no decide). Con `PRUEBA_1176.dwg` recién abierto: anota el **menú Deshacer** (lo normal es que
   esté vacío), cuántos archivos hay en `backups\` (puede no existir todavía) y que `CSV` existe.
   `listar_superficies` → lista vacía.

### A. `crear_superficie_tin` sin el aviso de `Entity.StyleId`

3. **Superficie con curvas cada 1 y 5 m, sin estilo (decisivo).** `crear_superficie_tin` con
   `args={"nombre":"Terreno","archivo":CSV,"intervalo_curvas_menor":1,"intervalo_curvas_mayor":5,"pedido":"crea la superficie Terreno con curvas cada 1 y 5 m"}`.
   Esperado:
   - `ok: true`, `simulado: false`, `despues.puntos: 9`, `despues.triangulos: 8`;
   - **`despues.estilo: "Contours 1m and 5m (Background)"`**, releído de la superficie;
   - `datos.intervalos_curvas`: `aplicados: true`, `eleccion: "automatica"`, `estilo: "Contours 1m and 5m (Background)"`,
     **`estilo_menor: 1`, `estilo_mayor: 5`**, `curvas_visibles: true`;
   - **en `avisos` no hay ningún texto que contenga `No se pudo releer el estilo` ni `Se releyó el estilo de la
     superficie creada`** (con la 1.17.2 salía el primero: es lo que decide el paso). Copia `avisos` entero;
   - `copia` presente (nueva en `backups\` o `reutilizada: true`) y **una** entrada `Executefunction` nueva en el menú
     Deshacer.
   Después, `leer_historial` con `args={"ultimas_n":20}`: **ninguna** línea empieza por
   `crear_superficie_tin: relectura del estilo` (si hay alguna, cópiala entera: dice qué falló).
   Si responde `El dibujo no refleja el cambio pedido en 'estilo'`, copia el mensaje entero: el estilo releído (ahora de
   verdad) no es el elegido.

4. **Estilo dado que no coincide** (no decide). `crear_superficie_tin` con
   `args={"nombre":"Terreno 2","archivo":CSV,"estilo":"Contours 5m and 25m (Background)","intervalo_curvas_menor":1,"intervalo_curvas_mayor":5,"pedido":"crea Terreno 2 con el estilo de 5 y 25 m"}`.
   Esperado: `ok: true`, `despues.estilo: "Contours 5m and 25m (Background)"`, `datos.intervalos_curvas`:
   `aplicados: false`, `eleccion: "indicada"`, `estilo_menor: 5`, `estilo_mayor: 25`; en `avisos`, el texto que contiene
   `Intervalos de curvas pedidos (menor 1 y mayor 5) sin aplicar` y **ninguno** con `No se pudo releer el estilo`.
   (Si el dibujo no tiene ese estilo, el error lista los estilos: elige otro de la lista que no sea de 1 y 5 m, repite
   y anota cuál.)

### D. La escritura sigue como en la 1.17.5

5. **La línea en simulación, sin `solo_lectura` (decisivo).** Antes, anota el menú Deshacer. `ejecutar_codigo` con
   `args={"codigo_csharp":"var l = new Line(new Point3d(1000, 2000, 0), new Point3d(1100, 2000, 0));\nF.Agregar(l);\nF.Escribir(\"línea de prueba\");\nreturn new { largo = l.Length, capa = l.Layer };","pedido":"dibuja una línea de prueba de 100 m"}`.
   Esperado, igual que el paso 4 de `VALIDACION_1175.md`:
   - `ok: true`, `simulado: true`, `accion` empieza por `Ejecutar un script de 4 línea(s) en simulación`;
   - `antes.objetos_creados: 0` y `despues.objetos_creados: 1`; `datos.objetos.creados` con un elemento
     `tipo: "Line"`; `datos.objetos.fuente_deteccion.creados_por` contiene `fachada`;
   - `resultado: {"largo":100,"capa":"0"}`; `copia` presente;
   - `avisos`: el primero empieza por `Simulación: el script se ejecutó de verdad`; **ninguno** contiene
     `pásalo con solo_lectura=true` (el script escribe);
   - **nuevo**: `datos.compilacion.solo_lectura: false` y **`datos.compilacion.escrituras_reconocidas: 1`**
     (`F.Agregar`);
   - menú Deshacer después: **una** entrada `Executefunction` nueva (la de la simulación, como en la 1.17.5).

6. **La línea de verdad (decisivo).** **Desde aquí hasta el final del paso 10, nadie toca la vista de Civil 3D.**
   La llamada del paso 5 con `"simular": false` y `"pedido":"dibuja de verdad la línea de prueba de 100 m"`. Esperado:
   `simulado: false`, `mensaje` empieza por `Script de 4 línea(s) ejecutado y confirmado: 1 objeto(s) creado(s)`,
   `despues.objetos_creados: 1`, `datos.objetos.creados` con un elemento `tipo: "Line"` (anota su `handle`),
   `copia` presente. Menú Deshacer **justo después**: **una** entrada nueva arriba del todo (anota su texto literal y
   cuántas entradas hay en total: es la referencia de los pasos 7 a 10).

### B. Contar con solo lectura: sin entrada en Deshacer ni copia

7. **Contar con solo lectura (decisivo).** Antes, anota el menú Deshacer (debe ser el del final del paso 6), cuántos
   archivos hay en `backups\` y el más reciente. La llamada de **contar con solo lectura**. Esperado:
   - `ok: true`, **`solo_lectura: true`**, **`copia: null`**, `simulado: true`, `cambios: []`, `antes` y `despues`
     con `objetos_creados: 0`, `objetos_modificados: 0` y `objetos_borrados: 0`;
   - `accion` empieza por `Ejecutar un script de solo lectura de 4 línea(s): con el dibujo bloqueado para lectura y la
     transacción abortada al final, sin copia de seguridad y sin entrada en Deshacer.`;
   - **`resultado: 1`** (la línea del paso 6);
   - `datos.compilacion.solo_lectura: true`, `datos.compilacion.escrituras_reconocidas: 0`;
   - `datos.deshacer` **entero**: `comando` vacío (`""` o `null`: no hay pseudocomando) y **`dbmod_antes` igual a
     `dbmod_despues`**;
   - `avisos` sin `Simulación: el script se ejecutó de verdad` ni `La simulación puede dejar una entrada vacía`;
   - **el menú Deshacer exactamente igual que antes** (ninguna entrada nueva: es lo que decide el paso);
   - **ningún archivo nuevo en `backups\`** (mismo número y el mismo más reciente).
   `leer_log` con `args={"ultimas_n":1}`: herramienta `ejecutar_codigo`, `ok: true`, en `args`
   `"solo_lectura": true` y el `pedido`.

### C. Un script de solo lectura que intenta escribir

8. **Rechazos al compilar (decisivo).** Cinco llamadas a `ejecutar_codigo`, todas con `"solo_lectura":true`:
   - **8a**: `args={"codigo_csharp":"var l = new Line(new Point3d(0, 0, 0), new Point3d(10, 0, 0));\nF.Agregar(l);\nreturn 1;","solo_lectura":true,"pedido":"intenta dibujar una línea con solo lectura"}`.
     Esperado: `ok: false`, el error empieza por `ejecutar_codigo: el código no se puede ejecutar (1 uso no
     permitido). No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.` y
     contiene `Línea 2, columna 3: [bloqueado: solo lectura] F.Agregar: con solo_lectura=true el script solo puede leer`.
   - **8b**: `args={"codigo_csharp":"foreach (var id in F.EspacioModelo())\n{\n    var o = F.Tr.GetObject(id, OpenMode.ForWrite);\n}\nreturn 0;","solo_lectura":true,"pedido":"intenta abrir para escritura con solo lectura"}`.
     Esperado: `ok: false`, contiene `[bloqueado: solo lectura] OpenMode distinto de OpenMode.ForRead (OpenMode.ForWrite)`.
   - **8c**: `args={"codigo_csharp":"foreach (var id in F.EspacioModelo())\n    F.Abrir<Entity>(id, true);\nreturn 0;","solo_lectura":true,"pedido":"intenta abrir con escribir=true con solo lectura"}`.
     Esperado: `ok: false`, contiene `[bloqueado: solo lectura] F.Abrir con 'escribir' distinto de false (true)`.
   - **8d** (auditoría, una presentación no es un `DBObject`): `args={"codigo_csharp":"LayoutManager.Current.DeleteLayout(\"Presentación1\");\nreturn 1;","solo_lectura":true,"pedido":"intenta borrar una presentación con solo lectura"}`.
     Esperado: `ok: false`, contiene `[bloqueado: solo lectura] Autodesk.AutoCAD.DatabaseServices.LayoutManager.DeleteLayout`.
     (Con la 1.17.6 de antes de la auditoría compilaba y **borraba la presentación**: si sale `ok: true`, es un FALLO
     grave; anótalo y comprueba en Civil 3D que la pestaña `Presentación1` sigue.)
   - **8e** (auditoría, `OpenMode` escondido): `args={"codigo_csharp":"foreach (var id in F.EspacioModelo())\n{\n    var o = new[] { id }.Zip(Enum.GetValues<OpenMode>().Skip(1), F.Tr.GetObject).First();\n}\nreturn 0;","solo_lectura":true,"pedido":"intenta abrir para escritura sin escribir OpenMode"}`.
     Esperado: `ok: false`, contiene `[bloqueado: solo lectura]` con `OpenMode como argumento genérico de GetValues` y
     `grupo de métodos (delegado) con un parámetro OpenMode: GetObject` (si además sale un error `CS`, anótalo: en
     Civil 3D `Transaction.GetObject` tiene varias sobrecargas).
   Después de las cinco: menú Deshacer **igual** que al final del paso 6, **ningún archivo nuevo** en `backups\`, y
   contar con solo lectura → `resultado: 1`.

9. **Escritura sobre un objeto abierto para lectura: la rechaza Civil 3D al ejecutarse (decisivo).** `Curve.Extend`
   alarga una curva; las reglas de solo lectura no la conocen, así que el script compila y lo tiene que parar Civil 3D
   al ejecutarse porque la línea está abierta solo para lectura. **Este paso no prueba el bloqueo de lectura del
   dibujo**: cambiar un objeto abierto para lectura da `eNotOpenForWrite` con cualquier bloqueo. El bloqueo de lectura
   (`eLockViolation` al abrir para escritura) no tiene forma conocida de probarse sin saltarse las reglas de
   compilación: todas las maneras conocidas de abrir para escritura las rechaza el paso 8. `ejecutar_codigo` con
   `args={"codigo_csharp":"Line l = null;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) l = x;\nif (l == null) return \"sin líneas\";\nl.Extend(false, new Point3d(l.EndPoint.X + 50, l.EndPoint.Y, 0));\nreturn l.Length;","solo_lectura":true,"pedido":"intenta alargar la línea con solo lectura"}`.
   Esperado:
   - `ok: false`; el error empieza por `Con solo_lectura=true el script intentó escribir en la línea 5 y se impidió (`
     y sigue con el **texto literal de AutoCAD: por anotar** (se espera `eNotOpenForWrite`, algo como `Exception: eNotOpenForWrite`;
     cópialo tal cual: decide la tabla de miembros); termina con `No se tocó el dibujo: solo lectura (dibujo bloqueado
     para lectura y transacción abortada), sin copia de seguridad y sin entrada en Deshacer. Si el script necesita
     escribir, quita solo_lectura y simula primero (simular=true).`;
   - si en vez de eso el error empieza por `Con solo_lectura=true el script no puede cambiar el dibujo, pero se
     detectaron objetos` o por `ATENCIÓN: en un script de solo lectura`, cópialo entero: Civil 3D dejó cambiar la línea
     y la paró la comprobación de después (es un hallazgo, pero el dibujo no debe haber cambiado);
   - si sale `ok: true` con `resultado: 150`, es un **FALLO**: anótalo y sigue.
   Después: menú Deshacer **igual** que al final del paso 6, **ningún archivo nuevo** en `backups\`, y comprobar que la
   línea sigue midiendo 100: `ejecutar_codigo` con
   `args={"codigo_csharp":"double largo = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line x) largo = x.Length;\nreturn largo;","solo_lectura":true,"pedido":"mide la línea de prueba"}`
   → `resultado: 100`.

### D (fin). Deshacer

10. **Deshacer una vez (decisivo).** Anota el menú Deshacer: arriba debe estar la entrada del paso 6 (los pasos 7 a 9
    no dejaron ninguna). `ejecutar_comando` con `args={"comando":"_.UNDO 1"}` → `"terminado"`. Menú Deshacer
    después. Contar con solo lectura → **`resultado: 0`**: un solo `_.UNDO 1` quitó la línea del paso 6, así que entre
    el paso 6 y este no quedó **ninguna** entrada en Deshacer (si da `1`, los pasos 7 a 9 dejaron alguna: anota el menú
    literal antes y después del `_.UNDO 1`).

11. **La sugerencia sin `solo_lectura`** (no decide). La llamada de contar **sin** `solo_lectura`:
    `args={"codigo_csharp":"int n = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line) n++;\nreturn n;","pedido":"cuenta las líneas del espacio modelo"}`.
    Esperado, como en la 1.17.5: `simulado: true`, `resultado: 0`, `copia` presente, `datos.compilacion.solo_lectura:
    false`, `escrituras_reconocidas: 0`, y una entrada `Executefunction` nueva en el menú Deshacer. **Nuevo**: en
    `avisos`, un texto que empieza por `El script no cambió nada y no usa ninguna de las escrituras que el plugin
    reconoce: si solo lee, pásalo con solo_lectura=true la próxima vez`.

### E. Relectura del estilo en el eje, el perfil y la rasante

Desde aquí ya se puede mirar el dibujo en pantalla.

12. **Capa y polilínea del eje** (no decide). `ejecutar_comando` con
    `args={"comando":"_.-LAYER _M EJE_PRUEBA\n\n","timeout_s":20,"pedido":"crea la capa EJE_PRUEBA"}` → `"terminado"`;
    después `ejecutar_comando` con
    `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","timeout_s":20,"pedido":"dibuja la polilínea del eje de prueba"}`
    (**dos espacios** al final) → `"terminado"` (como en `VALIDACION_1101.md`, pasos 2 y 3).

13. **Eje con el estilo de la configuración (decisivo).** Primero la simulación:
    `crear_alineamiento_desde_polilinea` con
    `args={"nombre":"Eje 1176","capa":"EJE_PRUEBA","radio":80,"simular":true,"pedido":"simula el eje Eje 1176 con radio 80"}`
    → `simulado: true`, `despues.longitud: 831.3274`, `despues.numero_curvas: 2`; anota `despues.estilo` (el previsto,
    de la configuración del dibujo; en la 1.8.0 fue `Proposed`). Después la misma llamada **sin** `simular` y con
    `"pedido":"crea el eje Eje 1176 con radio 80"`. Esperado: `simulado: false`, `despues.longitud: 831.3274`,
    `despues.numero_curvas: 2`, **`despues.estilo` igual al de la simulación** (ahora releído de verdad) y **ningún**
    aviso que contenga `No se pudo releer el estilo` (si sale, cópialo entero: dice qué falló). Si responde
    `El estilo releído del alineamiento 'Eje 1176' es ...`, copia el error entero: el eje quedó con otro estilo.

14. **Perfil de terreno y vista** (no decide). Simulación y luego real: `crear_perfil_terreno_y_vista` con
    `args={"alineamiento":"Eje 1176","superficie":"Terreno","simular":true,"pedido":"simula el perfil de terreno del Eje 1176"}`;
    anota `despues.perfil.estilo` y `despues.vista.estilo`. Después sin `simular` y con
    `"pedido":"crea el perfil de terreno y la vista del Eje 1176"`. Esperado: `simulado: false`, `despues.perfil.estilo`
    y `despues.vista.estilo` iguales a los de la simulación y **ningún** aviso con `No se pudo releer el estilo`.
    Anota si alguno sale (dice si `ProfileView` se puede releer así).

15. **Rasante con estilo pedido (decisivo).** `crear_rasante` con
    `args={"alineamiento":"Eje 1176","nombre":"Rasante 1176","estilo":"Design Profile","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":400,\"cota\":110,\"longitud_curva\":120},{\"progresiva\":831.33,\"cota\":105}]","pedido":"crea la rasante Rasante 1176 con el estilo Design Profile"}`.
    Esperado: `simulado: false`, `despues.tipo: "FG"`, `despues.numero_pvis: 3`, `despues.numero_curvas: 1`,
    **`despues.estilo: "Design Profile"`** y **ningún** aviso con `No se pudo releer el estilo`. Si el dibujo no tiene
    `Design Profile`, el error lista los estilos de perfil: elige otro de la lista, repite y anota cuál (debe salir ese
    en `despues.estilo`). `calcular_volumenes` no se prueba aquí (necesita un corredor): lo cubre la próxima validación
    del flujo completo.

### F. `ejecutar_receta` con `solo_lectura`

16. **Una receta que escribe, con `solo_lectura` (decisivo).** Necesita `MEMORIA_FIRMA_DIR` y la receta
    `crear_capa_con_color` de `VALIDACION_117.md` (en PowerShell: `Test-Path "$env:MEMORIA_FIRMA_DIR\recetas\crear_capa_con_color.cs"`
    → `True`; si no está, anótalo y salta al paso 17). Anota `veces_usada` de su `.yaml`, el menú Deshacer y `backups\`.
    `ejecutar_receta` con
    `{"nombre":"crear_capa_con_color","parametros":{"nombre_capa":"MCP Receta 1176","color":3},"solo_lectura":true,"pedido":"crea la capa con la receta, en solo lectura"}`.
    Esperado: `ok: false`, el error contiene `[bloqueado: solo lectura]` (la receta crea una capa: escribe),
    `receta.sumada: false`, `receta.nota: "La ejecución falló: veces_usada no cambia."`; `veces_usada` igual que antes,
    el menú Deshacer igual y ningún archivo nuevo en `backups\`. (Con una receta que solo lea, la respuesta traería
    `solo_lectura: true` y `veces_usada` subiría 1; no hay ninguna guardada todavía.)

### G. `Database.AddDBObject` bloqueado también sin `solo_lectura`

17. **`AddDBObject` y otros bloqueos de todo modo (decisivo).** `ejecutar_codigo` (sin `solo_lectura`, simulación por defecto) con
    `args={"codigo_csharp":"var id = F.Db.AddDBObject(new Line());\nreturn id;","pedido":"intenta añadir un objeto fuera de la transacción"}`.
    Esperado: `ok: false`, el error empieza por `ejecutar_codigo: el código no se puede ejecutar (1 uso no permitido).
    No se ejecutó nada: sin copia de seguridad, sin contexto de comando y sin entrada en Deshacer.` y contiene
    `[bloqueado: transacción]` y `AddDBObject: usa F.Tr o una transacción anidada`. Después, dos llamadas más, también
    sin `solo_lectura` (auditoría de la ronda C):
    - **17b**: `args={"codigo_csharp":"F.Db.XBindXrefs(new ObjectIdCollection(), true);\nF.Db.DetachXref(ObjectId.Null);\nreturn null;","pedido":"intenta tocar las referencias externas"}`
      → `ok: false`, `(2 usos no permitidos)`, con `[bloqueado: documentos]` en `XBindXrefs` y en `DetachXref`.
    - **17c**: `args={"codigo_csharp":"(F.Db.Clayer, _) = (F.Db.Clayer, 0);\nreturn null;","pedido":"intenta cambiar la capa actual por desestructuración"}`
      → `ok: false`, contiene `[bloqueado: variables de sistema]` y `Clayer (asignación)` (igual que `F.Db.Clayer = ...`).
    El menú Deshacer igual y ningún archivo nuevo en `backups\` después de las tres.

18. **Cierre** (no decide). `leer_historial` con `args={"ultimas_n":30}`: anota las líneas de `ejecutar_codigo` (las de
    solo lectura dicen `compilado con solo_lectura` y `solo lectura terminada, sin cambios`) y las de
    `crear_superficie_tin`. **No guardes el dibujo.** Deja la variable `ARBA_MCP_CODIGO` como la encontraste en el
    paso 1: si estaba vacía y la pusiste tú, cierra Civil 3D y quítala con
    `[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO',$null,'User')`; anota cómo quedó.

## Informe

**Guarda el informe como archivo** (no basta con el resumen en el chat) en
`herramientas-dev/informes/informe_validacion_1176_<AAAAMMDD>.md`, con:

- Versión de Civil 3D, rama y commit instalados, versión de `ping`, DWG usado (solo el nombre de la copia) y el valor de
  `ARBA_MCP_CODIGO` al empezar y al terminar.
- Una tabla paso / resultado (OK, FALLO, HALLAZGO) / respuesta literal / tiempo (`ms_puente`).
- `avisos` enteros de los pasos 3 y 4, y las líneas de `leer_historial` que empiezan por `crear_superficie_tin:`.
- **El menú Deshacer antes y después de cada paso, con el texto literal de cada entrada**, y los archivos de
  `backups\` (número y el más reciente) antes del paso 3 y después de los pasos 3, 5, 6, 7, 8, 9 y 11.
- `datos.deshacer` y `datos.compilacion` **enteros** de los pasos 5, 7 y 11.
- El `despues.estilo` de la simulación y de la creación real de los pasos 13, 14 (perfil y vista) y 15, y los `avisos`
  enteros de esos pasos.
- Las respuestas literales de los pasos 16 y 17 (y si el 16 se saltó, por qué).
- **El texto literal de AutoCAD del paso 9** (lo que va entre paréntesis después de `se impidió`).
- Las respuestas literales de 8d, 8e, 17b y 17c y, en 8d, si la pestaña `Presentación1` sigue en el dibujo.
- Si Jose miró el dibujo en pantalla, en qué paso (debe ser después del 10).
- Una lista final de los miembros de la sección 1.17.6 de `miembros_por_verificar_civil3d.md` que pasan a
  `ejecutado en Civil 3D 2027` con la fecha, y los que fallaron con su error literal.
