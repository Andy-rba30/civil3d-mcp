# Comprobaciones pendientes para la prueba de la ronda D en la PC

Tres comprobaciones cortas que salieron de validaciones anteriores y que solo se pueden hacer con Civil 3D. **Ninguna
decide la ronda**: si una falla, se anota el resultado literal y se sigue. La sesión orquestadora las mete en el
`VALIDACION_*.md` de la ronda D. Los pasos de cada una están listos para pegar tal cual.

| Comprobación | De dónde sale | Qué se quiere saber |
|---|---|---|
| **C1** `-LAYER` con dos espacios | Informe 118, paso 2 (17/11/2026) | Si `_.-LAYER _M <capa>  ` (dos espacios al final) termina solo o necesita un Enter más |
| **C2** H4: miembros heredados de `Drawable` | Revisión previa de `ejecutar_codigo` del 15/11/2026 (`REGISTRO_AVANCE.md`, hallazgo H4) | Si la lista blanca de `ejecutar_codigo` rechaza la lectura de alguna propiedad que los objetos del dibujo heredan de `Autodesk.AutoCAD.GraphicsInterface.Drawable` |
| **C3** `IsNewObject` de la fuente `transaccion_script` | Informe 1175, pasos 3, 4 y 6 (16/11/2026) | Por qué `transaccion_script` no marca como nueva la línea que crea un script |

## Requisitos

- **Qué abrir**: Civil 3D 2027 con el plugin y el puente de la ronda D (1.19.1 o posterior) y el agente con el MCP
  `civil3d` conectado, sobre el dibujo de la prueba de la ronda D o sobre una copia nueva de
  `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` guardada como
  `C:\Proyectos\dibujos_prueba\PRUEBA_RONDA_D.dwg`. Nunca el archivo base ni un dibujo real. Para C2a (opcional), además,
  PowerShell en la raíz del repositorio en la rama de la ronda.
- `ARBA_MCP_CODIGO=1` para C2 y C3, que usan `ejecutar_codigo` (sin ella responde «ejecutar_codigo está desactivada»).
- Las herramientas del plugin llevan los argumentos dentro de `args`.
- Reglas: no se marca OK lo que no se vio, cada resultado va con la respuesta literal y no se pegan rutas con nombres
  de obra (`<ruta>`). `ejecutar_comando` solo con el texto exacto de C1: si algo no funciona, es el hallazgo, no se
  arregla con otros comandos.

---

## C1. `_.-LAYER` con dos espacios al final

**Por qué.** `ejecutar_comando` manda el texto tal cual, y cada espacio final equivale a pulsar Enter. Con
`_.-LAYER _M EJE_NORMA  `, el primer espacio final cierra el nombre de la capa y el segundo cierra la orden. En el
informe 118 (paso 2), el agente anotó que hizo falta un Enter más (`\n\n`). Sin embargo, el mismo patrón con dos
espacios terminó bien en la validación 119 (paso 1, capas `EJE_NORMA_A` y `EJE_NORMA_B`). Antes de cambiar los
`VALIDACION_*.md` hay que repetirlo una vez mirando con cuidado qué texto llegó al plugin.

**Paso listo para pegar:**

C1. **`-LAYER` con dos espacios** (no decide).
   1. `leer_variable` con `args={"nombre":"CMDACTIVE"}` → `"valor": "0"`. Después, `leer_variable` con
      `args={"nombre":"CLAYER"}` → anota la capa actual (normalmente `"0"`).
   2. `ejecutar_comando` con
      `args={"comando":"_.-LAYER _M EJE_COMPROBACION  ","timeout_s":20,"pedido":"crea la capa EJE_COMPROBACION y déjala actual"}`
      con **exactamente dos espacios** al final, antes de la comilla: sin `\n` y sin reintentar. Anota `result` y el
      tiempo literales. Esperado: `"result": "terminado"` en menos de un segundo (en la validación 1176, paso 12, la
      misma orden con `\n\n` tardó 111 ms).
   3. `leer_variable` con `args={"nombre":"CMDACTIVE"}` → `"valor": "0"`, y con `args={"nombre":"CLAYER"}` →
      `"valor": "EJE_COMPROBACION"`.
   4. Abre `mcp_log.jsonl` (carpeta del DWG) y copia **literal** la última línea de `ejecutar_comando`. En
      `args.comando` el texto debe terminar en `EJE_COMPROBACION` seguido de **dos** espacios. Cuenta los espacios
      que hay entre `EJE_COMPROBACION` y la comilla de cierre y anota el número.
   5. **Solo si** el 2 no dio `"terminado"` o el 3 no dio `CMDACTIVE` `"0"` y `CLAYER` `"EJE_COMPROBACION"`:
      - Si `CMDACTIVE` sigue en `"1"`, Jose pulsa Esc en Civil 3D y lo anotas.
      - Luego `ejecutar_comando` con
        `args={"comando":"_.-LAYER _M EJE_COMPROBACION_B\n\n","timeout_s":20,"pedido":"crea la capa EJE_COMPROBACION_B con dos Enter"}`.
        Esperado: `"result": "terminado"`.
      - Al final, `leer_variable` de `CMDACTIVE` (`"0"`) y de `CLAYER` (`"EJE_COMPROBACION_B"`). Anota las tres
        respuestas literales.

**Qué debería ver**: `terminado` en el 2, `CMDACTIVE` `0` y `CLAYER` `EJE_COMPROBACION` en el 3 y dos espacios en el 4.
Esta orden no crea objetos con el plugin, así que no aparece ninguna copia nueva en `backups\`.

**Cómo leer el resultado:**

| Paso 2 | Espacios en el paso 4 | Qué significa | Qué se hace después |
|---|---|---|---|
| `terminado` y `CLAYER` correcta | 2 | Los guiones con dos espacios son correctos; lo del 118 fue un caso suelto (probablemente el texto perdió un espacio por el camino) | Se cierra el punto sin cambiar los `VALIDACION_*.md` |
| `timeout con ESC` (y el 5 termina) | 2 | `-LAYER` necesita un Enter más después del nombre. Una explicación posible: su pregunta del nombre no toma el espacio como Enter | En otra sesión, los guiones con `-LAYER` pasan a `\n\n`, como el paso 12 de `VALIDACION_1176.md` |
| cualquiera | 0 o 1 | El texto llegó recortado al plugin: lo cortó el agente o el cliente MCP, no Civil 3D | Se anota; otra sesión mira el camino del texto en el puente |

---

## C2. H4: propiedades heredadas de `GraphicsInterface.Drawable`

**Por qué.** En la biblioteca real de Civil 3D, todos los objetos del dibujo (`DBObject` y, por debajo, `Entity`)
heredan de una clase de la parte gráfica de AutoCAD, `Autodesk.AutoCAD.GraphicsInterface.Drawable`. La lista blanca
de `ejecutar_codigo` bloquea entero el espacio de nombres `Autodesk.AutoCAD.GraphicsInterface` (categoría «ventanas y
diálogos»). Si un script lee una propiedad escrita solo en `Drawable` (que `DBObject` y `Entity` no vuelven a
declarar), el plugin la rechazaría al compilar aunque solo sea una lectura. Es el hallazgo H4 de la revisión del
15/11/2026. En la nube no se puede ver: los sustitutos de `CompilarSinCivil` y las pruebas no tienen esa herencia.

Según el catálogo de la API de Civil 3D 2027 (`memoria-firma`, consulta de la orquestadora del 17/11/2026):
- `Drawable` está en `acdbmgd.dll`, como `DBObject` y `Entity`.
- `Id` (`ObjectId`) e `IsPersistent` (`bool`) están declaradas en `Drawable` y `DBObject` las vuelve a declarar: **no**
  son candidatas a H4. Sirven de **control**: deben compilar.
- `Bounds` (`Nullable<Extents3d>`) y `DrawableType` (de tipo `GraphicsInterface.DrawableType`) solo las declara
  `Drawable`: ni `DBObject` ni `Entity` las vuelven a declarar. Son la **prueba de H4**. `Bounds` es la importante,
  porque un script la usaría para medir la extensión de una entidad.

**Paso listo para pegar:**

C2. **Propiedades heredadas de `Drawable` en `ejecutar_codigo`** (no decide; necesita `ARBA_MCP_CODIGO=1`).
   a. **Opcional, confirmación del catálogo** (PowerShell, desde la raíz del repo; Civil 3D puede estar abierto o
      cerrado):
      `dotnet run --project herramientas-dev\InspectC3D -- Autodesk.AutoCAD.GraphicsInterface.Drawable Autodesk.AutoCAD.DatabaseServices.DBObject Autodesk.AutoCAD.DatabaseServices.Entity`.
      - Esperado: el bloque de `DBObject` con `Base: Autodesk.AutoCAD.GraphicsInterface.Drawable`.
      - `Bounds`, `DrawableType`, `Id` e `IsPersistent` entre las propiedades de `Drawable`.
      - `Id` e `IsPersistent` también en `DBObject`, y `Bounds` y `DrawableType` ni en `DBObject` ni en `Entity`.
      - Anota solo «coincide» o qué no coincide.
      - **La salida de InspectC3D no se pega en el informe ni se guarda en el repositorio** (regla 7 de
        `CLAUDE.md`: ningún volcado de las DLL de Autodesk).
   b. **Control: `Id` e `IsPersistent`** con `ejecutar_codigo` y
      `args={"codigo_csharp":"foreach (var id in F.EspacioModelo())\n{\n    var e = F.Abrir<Entity>(id);\n    var a = e.Id;\n    var b = e.IsPersistent;\n    return new { tipo = e.GetType().Name, id_igual = a == id, persistente = b };\n}\nreturn \"sin entidades\";","solo_lectura":true,"pedido":"lee Id e IsPersistent de la primera entidad sin tocar el dibujo"}`.
      Esperado:
      - `ok: true`, `copia: null`, `datos.deshacer.comando: ""` y `datos.compilacion.solo_lectura: true`, sin entrada
        nueva en el menú Deshacer.
      - `resultado` con `tipo` (el de la primera entidad, por ejemplo `Polyline`), `id_igual: true` y
        `persistente: true`.
      - Si `resultado` es `"sin entidades"`: `ejecutar_comando` con
        `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","timeout_s":20}` (dos espacios al
        final) y repite el b.
      - Si el error nombra `GetType`, quita `tipo = e.GetType().Name, ` y repite.
   c. **Prueba de H4: `Bounds`** con `ejecutar_codigo` y
      `args={"codigo_csharp":"foreach (var id in F.EspacioModelo())\n{\n    var e = F.Abrir<Entity>(id);\n    var c = e.Bounds;\n    return new { tipo = e.GetType().Name, limites = \"\" + c };\n}\nreturn \"sin entidades\";","solo_lectura":true,"pedido":"lee Bounds de la primera entidad sin tocar el dibujo"}`.
      `Bounds` está en la línea 4.
      - Lo previsto, por cómo está escrita la lista blanca: `ok: false` con `No permitido:` y
        `Línea 4, columna N: [bloqueado: ventanas y diálogos] Autodesk.AutoCAD.GraphicsInterface.Drawable: ejecutar_codigo no admite ventanas, diálogos ni entrada interactiva...`,
        sin copia y sin entrada en Deshacer (**H4 confirmado**).
      - Si en cambio sale `ok: true` con `limites` (las dos esquinas de la entidad), H4 no pasa con `Bounds`. Anota el
        texto de `limites` tal cual.
   d. **Prueba de H4: `DrawableType`** con `ejecutar_codigo` y
      `args={"codigo_csharp":"foreach (var id in F.EspacioModelo())\n{\n    var e = F.Abrir<Entity>(id);\n    var d = e.DrawableType;\n    return new { tipo = e.GetType().Name, tipo_dibujable = \"\" + d };\n}\nreturn \"sin entidades\";","solo_lectura":true,"pedido":"lee DrawableType de la primera entidad sin tocar el dibujo"}`.
      `DrawableType` está en la línea 4.
      - Lo previsto: `ok: false` con `[bloqueado: ventanas y diálogos]` en la línea 4, que nombra
        `Autodesk.AutoCAD.GraphicsInterface.Drawable`, el tipo del valor
        (`Autodesk.AutoCAD.GraphicsInterface.DrawableType`) o los dos. Anota qué nombra.
      - Si sale `ok: true`, anota `tipo_dibujable`.

**Qué debería ver**: el b compila (`ok: true`). El c y el d, previsiblemente, no compilan, con el bloqueo en la línea
4. Ninguno deja entrada nueva en el menú Deshacer ni copia nueva en `backups\`: todos van con `solo_lectura`.

**Cómo leer el resultado:**

| Lo que sale | Qué significa | Qué se hace después |
|---|---|---|
| b `ok: true` y c `ok: false` que nombra `GraphicsInterface.Drawable` | **H4 confirmado**: las propiedades que `DBObject` vuelve a declarar pasan, pero `Bounds`, que solo declara `Drawable`, se rechaza aunque sea una lectura | Propuesta para otra sesión: permitir leer las propiedades de `Drawable` sobre objetos del dibujo, al menos `Bounds` |
| b y c `ok: true` | H4 no pasa: el plugin no ve `Drawable` como declarante de `Bounds` | Se cierra el punto |
| b `ok: false` que nombra `GraphicsInterface.Drawable` | H4 es más amplio de lo previsto: también bloquea `Id` e `IsPersistent`, que `DBObject` vuelve a declarar | Propuesta para otra sesión, con más prioridad (`Id` es de uso común) |
| d `ok: false` que solo nombra `GraphicsInterface.DrawableType` | El bloqueo viene del tipo del valor, que es de la parte gráfica y se bloquea a propósito: no es H4 | Se anota aparte |
| `CS1061` («no contiene una definición») en cualquiera | La propiedad no existe en esta instalación, en contra del catálogo | Se anota como hallazgo |

---

## C3. `IsNewObject` de la fuente `transaccion_script`

**Por qué.** Desde la 1.17.5, `ejecutar_codigo` cuenta lo que crea un script por varias fuentes y dice cuál lo vio
en `datos.objetos.fuente_deteccion`. En la validación 1175 (pasos 3, 4 y 6), con una línea creada por el script:
- La fuente `fachada` (lo que añade `F.Agregar`) contó la línea.
- La fuente `transaccion_script` (`F.Tr.GetAllObjects()` antes de confirmar, con `IsNewObject` de cada objeto) trajo
  3 objetos, pero `creados: 0` y `modificados: 2`.

No bloquea nada, porque `fachada` basta, pero no se sabe por qué. Pudo pasar una de dos cosas: la colección no trae la
línea, o la trae sin la marca de nueva. Esta comprobación lo mira **desde dentro del script**, en una simulación: la
transacción se aborta al final y la línea no queda en el dibujo.

**Paso listo para pegar:**

C3. **Qué marca `IsNewObject` en la línea creada** (no decide; necesita `ARBA_MCP_CODIGO=1`).
   1. Cuenta las líneas del espacio modelo con `ejecutar_codigo` y
      `args={"codigo_csharp":"int n = 0;\nforeach (var id in F.EspacioModelo())\n    if (F.Abrir<Entity>(id) is Line) n++;\nreturn n;","solo_lectura":true,"pedido":"cuenta las líneas del espacio modelo sin tocar el dibujo"}`
      → `ok: true`, `copia: null`. Anota `resultado` (N0).
   2. `ejecutar_codigo` con
      `args={"codigo_csharp":"var l = new Line(new Point3d(1000, 2000, 0), new Point3d(1100, 2000, 0));\nvar id = F.Agregar(l);\nbool lineaNueva = l.IsNewObject;\nbool lineaModificada = l.IsModified;\nvar objetos = new List<object>();\nbool vista = false;\nusing (var todos = F.Tr.GetAllObjects())\n{\n    foreach (DBObject o in todos)\n    {\n        bool esLinea = o.ObjectId == id;\n        if (esLinea) vista = true;\n        objetos.Add(new { tipo = o.GetType().Name, handle = o.ObjectId.Handle.ToString(), es_la_linea = esLinea, misma_instancia = object.ReferenceEquals(o, l), nuevo = o.IsNewObject, modificado = o.IsModified, borrado = o.IsErased });\n    }\n}\nreturn new { linea_nueva = lineaNueva, linea_modificada = lineaModificada, linea_en_get_all_objects = vista, objetos };","simular":true,"pedido":"comprueba qué marca IsNewObject en la línea que crea el script, sin dejarla en el dibujo"}`.
      Esperado:
      - `ok: true`, `simulado: true`, `despues.objetos_creados: 1` y `datos.objetos.creados` con un `Line`.
      - En `datos.objetos.fuente_deteccion`, `fachada` con `creados: 1`.
      - `copia` con `estado: "terminada"`: es una simulación sin `solo_lectura` y hace copia, como todas las de esta
        herramienta.
      - En `resultado`: `linea_nueva`, `linea_modificada`, `linea_en_get_all_objects` y la lista `objetos`, con
        `tipo`, `handle`, `es_la_linea`, `misma_instancia`, `nuevo`, `modificado` y `borrado` de cada objeto.
      - Copia **literal** todo `resultado` y el bloque `datos.objetos.fuente_deteccion.transaccion_script` (`objetos`,
        `creados`, `modificados`, `borrados`, `errores`), y anota también `datos.deshacer.comando`.
      - Si el error nombra `GetType`, quita `tipo = o.GetType().Name, ` y repite.
   3. Repite el conteo del 1 → `resultado` igual a N0 (la línea de la simulación no quedó en el dibujo).

**Qué debería ver**: la simulación responde `ok: true` y el conteo del 3 vuelve a dar N0. En `backups\` puede haber
una copia nueva o reutilizada (`copia.reutilizada`); el dibujo no cambia.

**Cómo leer el resultado** (del paso 2):

| Lo que sale | Qué significa | Qué se hace después |
|---|---|---|
| `linea_en_get_all_objects: true` y la fila con `es_la_linea: true` tiene `nuevo: false` | `GetAllObjects` de `F.Tr` sí trae la línea, pero `IsNewObject` no la marca como nueva mientras la transacción sigue abierta. Si además sale `modificado: true`, Civil 3D la cuenta como modificada, lo que explicaría el `modificados: 2` del informe 1175 | Se anota en `miembros_por_verificar_civil3d.md` (fila de `IsNewObject`, sección 1.17.5) y se cierra el punto: basta la fuente `fachada` |
| `linea_en_get_all_objects: false` | `GetAllObjects` de `F.Tr` no trae la línea; los objetos de la lista son otros (anota sus `tipo`) | Igual: se anota y se cierra el punto |
| La fila de la línea con `nuevo: true`, pero `fuente_deteccion.transaccion_script.creados: 0` en la misma respuesta | El script ve la línea nueva y el plugin no: el fallo está en cómo la lee el plugin, después del script | Propuesta de corrección para otra sesión |
| `linea_nueva` distinta del `nuevo` de su fila, con `misma_instancia: false` | Civil 3D devuelve otro objeto .NET para la misma línea, con otra marca | Se anota junto a la fila que toque |

---

## Qué va al informe

En el informe de la ronda D, una sección **«Comprobaciones de la ronda D (C1, C2, C3)»** con:

- **C1**: las respuestas de los pasos 1 a 3 (y 5 si se hizo), la línea literal de `mcp_log.jsonl` del paso 4 y el
  número de espacios.
- **C2**: si se hizo el a, «coincide» o qué no coincide (sin la salida de InspectC3D), y la respuesta literal de b, c y
  d.
- **C3**: N0, el `resultado` y el bloque `transaccion_script` del paso 2 y el conteo del paso 3.
- Para cada una, la fila de su tabla «Cómo leer el resultado» que corresponde.

La sesión de hallazgos de la ronda D pasa lo aprendido a `miembros_por_verificar_civil3d.md`, a `CLAUDE.md` y a
`REGISTRO_AVANCE.md` y quita estos puntos de «Propuestas pendientes».
