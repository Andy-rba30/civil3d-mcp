# Prompt de validación de la 1.8.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- Civil 3D 2027 abierto con **una copia** de un DWG de prueba **guardado en disco** (vale uno con una plantilla normal
  de Civil 3D: hace falta que tenga al menos un estilo de alineación y, si puede ser, un conjunto de etiquetas de
  alineación). No hace falta corredor ni superficie. Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y
  `backups\`.
- **Polilínea de prueba.** En la copia del DWG se dibuja (paso 0) una polilínea 2D de tangentes de **4 vértices** en
  coordenadas genéricas, en una capa propia `EJE_PRUEBA`: `1000,2000 → 1300,2000 → 1300,2300 → 1600,2300` (tres tramos
  de 300 m, un giro de 90° a la izquierda en el PI 1 y otro de 90° a la derecha en el PI 2). Ningún dato real.
- El plugin **1.8.0** se instala en el paso 0 desde la rama `tarea/2.2-alineamiento`; el bundle queda con `ArbaMcp.dll`
  y `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.8.0 (`uv sync` antes) y el cliente MCP reiniciado
  para que cargue la lista de **47** herramientas del plugin (más `buscar_api` del puente).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.8.0**: la herramienta de escritura `crear_alineamiento_desde_polilinea` (eje nuevo desde una
  polilínea 2D de tangentes indicada por `handle` o por `capa`, con una curva circular por PI: `radio` para todas y
  `radios_por_pi` para cambiar alguna; antes de crear calcula qué radios caben, el que no cabe queda sin curva con
  aviso del radio máximo; `simular`, copia de seguridad, una entrada de Deshacer, relectura con longitud, progresivas y
  lista de PI). Es la segunda herramienta del camino `Escritura.CrearObjeto` (1.7.0). **Los pasos 2, 4, 5 y 7 son los
  decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.8.0):
  `Alignment.Create(CivilDocument, PolylineOptions, ...)` con `siteId = ObjectId.Null`, `PolylineOptions`,
  `Alignment.Entities` (`Count`, `GetEntityByOrder`), `AlignmentEntityCollection.AddFreeCurve` con
  `CurveParamType.Radius` y `CurveType.Compound` (valores resueltos por nombre en tiempo de ejecución), `AlignmentEntity.EntityId`,
  `AlignmentArc.Radius`/`Delta`/`PIStation`, `AlignmentCurve.StartStation`/`EndStation`, `Alignment.SiteName`,
  `Styles.AlignmentStyles`, `Styles.LabelSetStyles.AlignmentLabelSetStyles`,
  `Settings.GetSettings<SettingsAlignment>().StyleSettings`, `ObjectLayerSettings.GetObjectLayerSetting(Alignment)`,
  `GetSiteIds`, `Polyline.GetBulgeAt`/`Normal`/`Closed` y `LayerTable.Has`.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 4): **"crea el eje Eje 1 desde la
  polilínea de la capa EJE_PRUEBA con radio 80"**.

---

Eres el agente de validación de la versión 1.8.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo. Solo se usa en el paso 0 para
  dibujar la polilínea de prueba y en el paso 9 para dibujar una con arco.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Si el dibujo ya tiene un alineamiento llamado `Eje 1`, usa `Eje MCP 18` en su lugar en todos los pasos y anótalo.
- Los números esperados son con unidades de metros y redondeo a 4 decimales; una diferencia menor de 0,001 es OK.

## Pasos

0. **Instalar y dibujar la polilínea.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama
   `tarea/2.2-alineamiento`: `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`,
   `uv sync`. Reinicia el puente y el cliente MCP. Abre Civil 3D con la copia del DWG. `ping` con
   `args={"pedido":"comprueba que el plugin responde"}` → `version` debe ser `1.8.0`. Pide la lista de herramientas:
   deben ser 47 del plugin e incluir `crear_alineamiento_desde_polilinea` con los parámetros `nombre`, `handle`,
   `capa`, `radio`, `radios_por_pi`, `estilo`, `conjunto_etiquetas`, `sitio`, `simular` y `pedido`. Si no, para aquí.
   Luego dibuja la polilínea con dos `ejecutar_comando` (el espacio doble final termina el comando):
   `args={"comando":"_.-LAYER _M EJE_PRUEBA  ","pedido":"crea la capa EJE_PRUEBA"}` y
   `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","pedido":"dibuja la polilínea de prueba"}`.
   Ambos deben responder `terminado`. Haz zoom a esa zona (`_.ZOOM _W 900,1900 1700,2400`) y comprueba en pantalla que
   hay una polilínea en "L invertida" de 3 tramos en la capa EJE_PRUEBA. Si en el dibujo ya existía algo en esa capa,
   bórralo antes o usa otra capa y anótalo.

1. **Punto de partida.** `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}`. Anota los nombres:
   `Eje 1` **no** debe estar (si está, usa `Eje MCP 18` desde aquí). Abre el menú Deshacer de Civil 3D y anota cuántas
   entradas tiene (o la última). Mira si existe la carpeta `backups\` junto al DWG y cuántos archivos tiene.

2. **Simulación con radio que cabe (decisivo).** `crear_alineamiento_desde_polilinea` con
   `args={"nombre":"Eje 1","capa":"EJE_PRUEBA","radio":80,"simular":true,"pedido":"simula el eje Eje 1 con radio 80"}`.
   Esperado: `simulado: true`, `accion` que empieza por `Crear el alineamiento 'Eje 1' desde la polilínea de la capa
   'EJE_PRUEBA' con radio 80`, `antes: {"existe": false, "longitud": 0, "numero_curvas": 0, ...}` y `despues` con:
   `existe: true`, `longitud: 831.3274`, `progresiva_inicial: 0`, `progresiva_final: 831.3274`, `numero_pi: 2`,
   `numero_curvas: 2`, `estilo` con el nombre del estilo de alineación por defecto del dibujo, `conjunto_etiquetas` con
   el del dibujo (o `(sin etiquetas)`), `sitio: null`, `capa` con la capa de alineaciones del dibujo (por ejemplo
   `C-ROAD`) o la capa actual, y `pis` con **dos** elementos:
   - PI 1: `numero: 1`, `progresiva: 300`, `x: 1300`, `y: 2000`, `deflexion_grados: 90`, `giro: "izquierda"`,
     `radio_pedido: 80`, `radio_aplicado: 80`, `tangente: 80`, `longitud_curva: 125.6637`, `progresiva_pc: 220`,
     `progresiva_pt: 345.6637`, `radio_maximo: 300`, `aviso: null`.
   - PI 2: `numero: 2`, `progresiva: 565.6637`, `x: 1300`, `y: 2300`, `deflexion_grados: 90`, `giro: "derecha"`,
     `radio_aplicado: 80`, `progresiva_pc: 485.6637`, `progresiva_pt: 611.3274`, `radio_maximo: 220`, `aviso: null`.
   `avisos` debe ser `null` o no aparecer (salvo un aviso sobre la capa o el estilo por defecto: anótalo, no es fallo).
   OK solo si `numero_curvas` es **2**, `longitud` es **831.3274**, no hay `copia`, el menú Deshacer **no** cambió y no
   apareció ningún archivo nuevo en `backups\`. Si responde error `No hay ninguna polilínea 2D en la capa`, revisa el
   paso 0 (la polilínea debe ser LWPOLYLINE, la que dibuja `_.PLINE`).

3. **Simulación con radio que no cabe y con dos curvas que compiten.** Primero
   `args={"nombre":"Eje 1","capa":"EJE_PRUEBA","radio":400,"simular":true,"pedido":"simula el eje con radio 400"}`:
   `despues.numero_curvas: 0`, `longitud: 900`, los dos PI con `radio_aplicado: null`, `radio_maximo: 300` y `aviso`
   que empieza por `PI 1: el radio 400 no cabe (tangente 400 m; libre 300 m antes y 300 m después); se deja sin curva.
   Radio máximo que cabría: 300.` (y lo mismo para el PI 2); `avisos` con esos dos textos. Después
   `args={"nombre":"Eje 1","capa":"EJE_PRUEBA","radio":160,"simular":true,"pedido":"simula el eje con radio 160"}`:
   PI 1 con `radio_aplicado: 160`, `progresiva_pc: 140`, `progresiva_pt: 391.3274`; PI 2 **sin** curva
   (`radio_aplicado: null`, `radio_maximo: 140`, `progresiva: 531.3274`) y `aviso` que contiene `libre 140 m antes` y
   `Radio máximo que cabría: 140.`; `numero_curvas: 1`, `longitud: 831.3274`. OK si los dos casos coinciden y sigue sin
   haber `copia`, entrada de Deshacer ni archivo nuevo en `backups\`.

4. **Creación real (decisivo).** `crear_alineamiento_desde_polilinea` **sin** `simular`:
   `args={"nombre":"Eje 1","capa":"EJE_PRUEBA","radio":80,"pedido":"crea el eje Eje 1 desde la polilínea de la capa EJE_PRUEBA con radio 80"}`.
   Esperado: `simulado: false`, `cambios` que incluye `existe`, `longitud`, `numero_curvas` y `pis`;
   `antes: {"existe": false, ...}`; `despues` con `existe: true`, `longitud: 831.3274` (leída del dibujo:
   `Alignment.Length`), `progresiva_inicial: 0`, `progresiva_final: 831.3274`, `numero_pi: 2`, `numero_curvas: 2`
   (curvas **leídas** del eje), `estilo`, `conjunto_etiquetas`, `sitio: null`, `capa`; `copia` con `ruta` dentro de
   `backups\`, `metodo: "copia_de_disco"` (o `SaveAs` si el DWG no estaba guardado) y `estado: "terminada"`; `datos`
   con `nombre`, `handle_polilinea` (anótalo: se usa en el paso 8), `capa_polilinea: "EJE_PRUEBA"`, `vertices: 4`,
   `longitud_polilinea: 900`, `longitud_total: 831.3274`, `longitud_prevista: 831.3274`, `numero_curvas: 2`, `pis` como
   en el paso 2 y `curvas` con dos elementos: `{"radio": 80, "longitud": 125.6637, "deflexion_grados": 90,
   "progresiva_pi": 300, "progresiva_pc": 220, "progresiva_pt": 345.6637, "horario": false}` y `{"radio": 80,
   "longitud": 125.6637, "deflexion_grados": 90, "progresiva_pi": 565.6637, "progresiva_pc": 485.6637,
   "progresiva_pt": 611.3274, "horario": true}`; e `id_log`. En pantalla debe aparecer el eje con sus dos curvas y sus
   etiquetas sobre la polilínea (que sigue existiendo, sin cambios). OK solo si `numero_curvas` es 2, los dos radios
   leídos son 80 y `longitud` es 831.3274 (±0,001). Hallazgos posibles, anótalos literales y **para** en los dos primeros:
   - error que contiene `El enumerado CurveParamType ... no tiene el valor 'Radius'` (o `CurveType ... 'Compound'`,
     o `SettingsObjectLayerType ... 'Alignment'`): el mensaje lista los valores reales; es el hallazgo principal.
   - error `No se pudo crear la curva del PI ...`: `AddFreeCurve` rechazó el radio; nada quedó escrito.
   - `deflexion_grados` de `curvas` ≈ 5156 en vez de 90: `AlignmentArc.Delta` viene en grados, no en radianes.
   - aviso `La longitud leída del eje ... difiere de la prevista`: anota ambas longitudes.

5. **Lectura cruzada (decisivo).** `listar_alineamientos`: debe aparecer `Eje 1` con `inicio: 0`, `fin: 831.3274`,
   `longitud: 831.3274` y `perfiles: []`. Luego `pk_a_punto` con `args={"alineamiento":"Eje 1","pk":300}` → el punto
   debe estar sobre la curva 1, en `x: 1276.57, y: 2023.43` (±0,01: es el punto medio de la curva, a 33,137 m del PI
   (1300, 2000) sobre la bisectriz hacia el centro (1220, 2080)); y `punto_a_pk` con `args={"alineamiento":"Eje 1","x":1600,"y":2300}` → `pk: 831.3274`,
   `desplazamiento: 0`. OK si los tres coinciden.

6. **Una sola entrada de Deshacer y una copia.** Abre el menú Deshacer: respecto al paso 1 debe haber **una** entrada
   nueva (`Executefunction`), la del paso 4; los pasos 2, 3 y 5 no añaden ninguna. En `backups\` debe haber **un**
   archivo nuevo `<nombre del dwg>_<fecha>_crear_alineamiento_desde_polilinea.dwg` (el `copia.ruta` del paso 4). En
   `mcp_log.jsonl` deben estar las líneas `llamada` de los pasos 2 y 3 (con `"simular":true` en `args` y `ok:true`), 4
   (con el `pedido` literal y `ok:true`) y 5. **No deshagas todavía.**

7. **Error por nombre repetido (decisivo).** Repite exactamente el `crear_alineamiento_desde_polilinea` del paso 4
   (sin `simular`). Esperado: `ok: false` y `error` que contiene `Ya existe el alineamiento 'Eje 1'. Usa
   listar_alineamientos.`; con `id_log`. OK solo si además **no** hay entrada nueva en Deshacer y `listar_alineamientos`
   sigue mostrando un solo `Eje 1`. Si aparece un archivo nuevo en `backups\`, anótalo como hallazgo (la copia de disco
   se inicia al entrar; no es un fallo de seguridad, pero es una copia innecesaria, igual que en la 1.7.0).

8. **Radios por PI y handle; errores controlados.** `crear_alineamiento_desde_polilinea` con
   `args={"nombre":"Eje 2","handle":"<handle_polilinea del paso 4>","radio":80,"radios_por_pi":"[{\"pi\":2,\"radio\":0}]","simular":true,"pedido":"simula el eje 2 sin curva en el PI 2"}`
   → `numero_curvas: 1`, PI 2 con `radio_pedido: 0`, `radio_aplicado: null`, `aviso: null`, `progresiva: 565.6637`
   (PT1 + 220: el PI sin curva está en su vértice) y `longitud: 865.6637`. Luego, cada uno debe responder `ok: false` con el texto indicado y sin entrada
   nueva en Deshacer:
   - `args={"nombre":"Eje 2","capa":"EJE_PRUEBA","radio":80,"radios_por_pi":"[10,20,30]","simular":true}` → error que
     contiene `nombra el PI 3 y la polilínea tiene 2 PI intermedios`.
   - `args={"nombre":"Eje 2","capa":"EJE_PRUEBA","radio":0,"simular":true}` → `El parámetro 'radio' debe ser un número
     mayor que 0`.
   - `args={"nombre":"Eje 2","capa":"NO_EXISTE","radio":80,"simular":true}` → `No existe la capa 'NO_EXISTE'`.
   - `args={"nombre":"Eje 2","radio":80,"simular":true}` (sin handle ni capa) → `Falta la polilínea: indica 'handle'`.
   - `args={"nombre":"Eje 2","capa":"EJE_PRUEBA","radio":80,"estilo":"NoExiste","simular":true}` → `No existe el estilo
     de alineamiento 'NoExiste' en el dibujo. Disponibles: ...` (anota la lista: sirve para la memoria técnica; si dice
     `ninguno legible`, es un hallazgo sobre `StyleBase.Name`).
   - `args={"nombre":"Eje 2","capa":"EJE_PRUEBA","radio":80,"conjunto_etiquetas":"NoExiste","simular":true}` → `No
     existe el conjunto de etiquetas de alineamiento 'NoExiste'` con la lista (anótala).
   - `args={"nombre":"Eje 2","capa":"EJE_PRUEBA","radio":80,"sitio":"NoExiste","simular":true}` → `No existe el sitio
     'NoExiste'. Sitios del dibujo: ...`.
   Por último, con un estilo y un conjunto **de las listas anteriores**:
   `args={"nombre":"Eje 2","capa":"EJE_PRUEBA","radio":80,"estilo":"<uno>","conjunto_etiquetas":"<uno>","simular":true}`
   → `despues.estilo` y `despues.conjunto_etiquetas` con esos nombres. **No crees** `Eje 2` de verdad.

9. **Error por polilínea con arcos.** Dibuja una segunda polilínea con un tramo de arco en otra capa:
   `ejecutar_comando` con `args={"comando":"_.-LAYER _M EJE_ARCO  "}` y después
   `args={"comando":"_.PLINE 2000,2000 2300,2000 _A 2300,2300 _L 2600,2300  "}` (ambos `terminado`). Luego
   `crear_alineamiento_desde_polilinea` con `args={"nombre":"Eje 3","capa":"EJE_ARCO","radio":80,"simular":true}` →
   `ok: false` y `error` que contiene `no sirve como eje: tiene 1 tramo de arco. Se espera una polilínea 2D abierta de
   tangentes` y la frase con `_.PLINE` de cómo dibujarla. Sin entrada en Deshacer (salvo las dos de los comandos de
   dibujo, que son de `ejecutar_comando`, no de la herramienta) y `Eje 3` no aparece en `listar_alineamientos`.

10. **Deshacer.** En Civil 3D, `_.UNDO 1` (o Ctrl+Z) las veces necesarias hasta deshacer la entrada del paso 4 (las
    del paso 9 son dos comandos de dibujo; deshazlas primero). Esperado: `Eje 1` desaparece de la pantalla y de
    `listar_alineamientos`; la polilínea de EJE_PRUEBA sigue. `punto_a_pk` con `args={"alineamiento":"Eje 1","x":1600,"y":2300}`
    responde error `No existe el alineamiento 'Eje 1'. Usa listar_alineamientos.`. Si el eje sigue tras deshacer, es un
    hallazgo (como pasó con los objetivos de corredor).

11. **Cierre.** `leer_log` con `args={"ultimas_n":25}`: una línea `llamada` por cada paso, con `ok:false` en los errores
    controlados (pasos 7, 8 y 9) y `ok:true` en los demás. No guardes el dibujo (es una copia).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_18_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, las listas de estilos de alineación, conjuntos de etiquetas y sitios que devolvieron los errores del paso 8
(sirven para la memoria técnica: el nombre depende del idioma), el nombre de la capa en que quedó el eje (`despues.capa`
del paso 4) y la lista de miembros de la sección 1.8.0 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en
Civil 3D 2027` con la fecha (o a `no existe`, con el error literal, en especial los valores de los enumerados
`CurveParamType`, `CurveType` y `SettingsObjectLayerType` si el error los listó). **No pegues en el informe líneas de
`mcp_log.jsonl` con rutas reales**: sustitúyelas por `<ruta>`.
