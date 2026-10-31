# Prompt de validación de la 1.10.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- Civil 3D 2027 abierto con **una copia** de un DWG de prueba **guardado en disco**, con una plantilla normal de
  Civil 3D (hace falta al menos un estilo de perfil; si puede ser, también un conjunto de etiquetas de perfil). Anota la
  carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`.
- **Un alineamiento de longitud conocida.** Vale el eje `Eje 1` de VALIDACION_18, creado con
  `crear_alineamiento_desde_polilinea` desde la polilínea `1000,2000 → 1300,2000 → 1300,2300 → 1600,2300` con radio 80
  (va de la progresiva `0` a la `831.3274`). Si el DWG no lo tiene, recréalo con el paso 0 de VALIDACION_18 (polilínea en
  la capa `EJE_PRUEBA` y `crear_alineamiento_desde_polilinea` con `nombre: "Eje 1"`, `capa: "EJE_PRUEBA"`, `radio: 80`).
  Si usas otro eje, anota su nombre y sus progresivas `<inicio>` y `<fin>` y sustituye en este prompt `0` y `831.3274`
  (y `831.33`) por ellas. **No hace falta superficie**: la rasante no depende del terreno. Si el eje ya tiene una vista
  de perfil (la de VALIDACION_19), la rasante se verá dibujada en ella; si no, el perfil existe igual y se comprueba con
  las herramientas de lectura. Ningún dato real.
- El plugin **1.10.0** se instala en el paso 0 desde la rama `tarea/2.4-rasante`; el bundle queda con `ArbaMcp.dll` y
  `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.10.0 (`uv sync` antes) y el cliente MCP reiniciado para
  que cargue la lista de **49** herramientas del plugin (más `buscar_api` del puente).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.10.0**: la herramienta de escritura `crear_rasante`: crea la rasante (perfil de diseño, tipo FG)
  de un eje a partir de una lista ordenada de PVI (progresiva, cota) con sus longitudes de curva vertical. Antes de
  tocar el dibujo calcula, sin Civil 3D, la pendiente de cada tramo y, en cada PVI intermedio, el cambio de pendiente
  A, K = L/|A|, si es cresta o valle, las progresivas y cotas de inicio y fin de la curva y si la curva **cabe** (la
  que no cabe queda como quiebre de pendiente y `avisos` dice la longitud máxima). Crea el perfil con
  `Profile.CreateByLayout`, los PVI con `ProfilePVICollection.AddPVI` y una parábola simétrica por PVI intermedio con
  `AddFreeSymmetricParabolaByPVIAndCurveLength` (o una curva circular con `AddFreeCircularCurveByPVIAndLength`), todo en
  **una** transacción y **una** entrada de Deshacer; relee el perfil y comprueba PVI, cotas y rango. Es la cuarta
  herramienta del camino `Escritura.CrearObjeto` (1.7.0). **Los pasos 2, 3, 5, 6, 7 y 9 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.10.0):
  `Profile.CreateByLayout(nombre, alignmentId, layerId, styleId, labelSetId)`, `ProfilePVICollection.AddPVI(station,
  elevation)`, `ProfileEntityCollection.AddFreeSymmetricParabolaByPVIAndCurveLength(pvi, length)`,
  `AddFreeCircularCurveByPVIAndLength(pvi, length)`, `ProfilePVI.GradeIn/GradeOut/PVIType`, `ProfileEntity.Length/
  StartElevation/EndElevation`, `ProfileTangent.Grade`, `ProfileParabolaSymmetric.K/PVIStation/PVIElevation/CurveType/
  HighLowPointStation`, `ProfileCircular.K/Radius`, `Profile.ElevationMin/Max` sobre un perfil de composición,
  `Autodesk.Civil.DatabaseServices.DBObject.Name` (nombres de los estilos en los errores) y `DBDictionary.NameAt`.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 5): **"crea la rasante Rasante 1 del
  Eje 1 con PVI en 0 a cota 100, 400 a cota 110 con curva de 120 m, y 831.33 a cota 105"**.

---

Eres el agente de validación de la versión 1.10.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo. En este prompt no hace falta
  en ningún paso (solo `_.ZOOM` y `_.UNDO`, que son del propio Civil 3D).
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- El parámetro `pvis` es de tipo `json`: se pasa como **texto** con la lista JSON dentro, tal como está escrito aquí.
- Si el dibujo ya tiene un perfil llamado `Rasante 1` o `Rasante 2` en `Eje 1`, usa `Rasante MCP 110` y
  `Rasante MCP 110 b` en su lugar en todos los pasos y anótalo.
- Los números esperados son con unidades de metros y redondeo a 4 decimales; una diferencia menor de 0,001 es OK.
- Al final de este prompt hay una lista de **preguntas para el informe** (unidad de las pendientes, texto de cresta y
  valle, cota máxima leída, nombres de los estilos, si Deshacer quita el perfil): respóndelas con lo que viste.

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `tarea/2.4-rasante`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`, `uv sync`. Reinicia el puente y el
   cliente MCP. Abre Civil 3D con la copia del DWG. `ping` con `args={"pedido":"comprueba que el plugin responde"}` →
   `version` debe ser `1.10.0`. Pide la lista de herramientas: deben ser **49** del plugin e incluir `crear_rasante` con
   los parámetros `alineamiento`, `nombre`, `pvis`, `longitud_curva`, `tipo_curva`, `estilo`, `conjunto_etiquetas`,
   `vista_perfil`, `simular` y `pedido`. Si no, para aquí.

1. **Punto de partida.** `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}`: anota `inicio`, `fin` y
   `perfiles` de `Eje 1` (`0`, `831.3274`; los perfiles que haya, por ejemplo `Terreno - Terreno` de VALIDACION_19).
   `listar_perfiles` con `args={"alineamiento":"Eje 1"}`: no debe haber ningún `Rasante 1` (si lo hay, aplica la regla
   del nombre alternativo). Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene (o la última). Mira si
   existe la carpeta `backups\` junto al DWG y cuántos archivos tiene.

2. **Simulación con 3 PVI y una curva que cabe (decisivo).** `crear_rasante` con
   `args={"alineamiento":"Eje 1","nombre":"Rasante 1","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":400,\"cota\":110,\"longitud_curva\":120},{\"progresiva\":831.33,\"cota\":105}]","simular":true,"pedido":"simula la rasante Rasante 1 del Eje 1"}`.
   Esperado: `simulado: true`, `accion: "Crear la rasante 'Rasante 1' (perfil de diseño FG) del alineamiento 'Eje 1'
   con 3 PVI y 1 curva vertical (parábola simétrica), de 0 a 831.327"`, `antes: {"existe": false, "numero_pvis": 0,
   "numero_curvas": 0, ...}` y `despues` con: `existe: true`, `tipo: "FG"`, `estilo: "<estilo de perfil por defecto
   del dibujo>"`, `conjunto_etiquetas: "<el del dibujo o (sin etiquetas)>"`, `progresiva_inicial: 0`,
   `progresiva_final: 831.3274`, `cota_minima: 100`, `cota_maxima: 109.5248` (la cota más alta está en el vértice de la
   parábola, en la progresiva 421,98, no en el PVI de cota 110), `numero_pvis: 3`, `numero_curvas: 1`,
   `tipo_curva: "parabola_simetrica"`, `tramos` con dos elementos:
   - `{"numero": 1, "progresiva_inicio": 0, "progresiva_fin": 400, "longitud": 400, "cota_inicio": 100, "cota_fin": 110,
     "desnivel": 10, "pendiente_pct": 2.5, "aviso": null}`
   - `{"numero": 2, "progresiva_inicio": 400, "progresiva_fin": 831.3274, "longitud": 431.3274, "cota_inicio": 110,
     "cota_fin": 105, "desnivel": -5, "pendiente_pct": -1.1592, "aviso": null}`
   y `curvas` con un elemento: `{"pvi": 2, "progresiva_pvi": 400, "cota_pvi": 110, "pendiente_entrada_pct": 2.5,
   "pendiente_salida_pct": -1.1592, "a_pct": -3.6592, "longitud_pedida": 120, "longitud": 120, "k": 32.7939,
   "tipo": "cresta", "progresiva_inicio": 340, "progresiva_fin": 460, "cota_inicio": 108.5, "cota_fin": 109.3045,
   "longitud_maxima": 800, "aviso": null}`; `capa` (la capa de perfiles de la configuración, por ejemplo `C-ROAD-PROF`)
   y `vista_perfil: null`. `avisos` debe traer exactamente `PVI 3: la progresiva 831.33 se ajusta al final del eje
   (831.327).` (el dueño dijo 831.33 y el eje termina en 831.3274: se ajusta con aviso, no es error). OK solo si
   `numero_curvas` es **1**, `k` es **32.7939**, `tipo` es `cresta`, `progresiva_final` es **831.3274**, no hay `copia`,
   el menú Deshacer **no** cambió y no apareció ningún archivo nuevo en `backups\`. Hallazgos posibles: aviso `No se pudo
   leer el estilo de perfil por defecto` (anótalo: `SettingsProfile.StyleSettings` falló), `estilo: null` (ni la
   configuración ni `DBObject.Name` dieron el nombre: anótalo).

3. **Simulación con una curva que no cabe (decisivo).** `crear_rasante` con
   `args={"alineamiento":"Eje 1","nombre":"Rasante prueba","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":100,\"cota\":102,\"longitud_curva\":300},{\"progresiva\":500,\"cota\":100}]","simular":true,"pedido":"simula una rasante con una curva que no cabe"}`.
   Esperado: `simulado: true`, `accion` que termina en `con 3 PVI y 0 curvas verticales, de 0 a 500`, `despues` con
   `numero_curvas: 0`, `cota_maxima: 102` (sin curva, la cota más alta es la del PVI), `tramos` con `pendiente_pct: 2`
   y `pendiente_pct: -0.5`, y `curvas` con un elemento `{"pvi": 2, "progresiva_pvi": 100, "cota_pvi": 102,
   "a_pct": -2.5, "longitud_pedida": 300, "longitud": null, "k": null, "tipo": "cresta", "progresiva_inicio": null,
   "progresiva_fin": null, "longitud_maxima": 200, "aviso": "PVI 2 (100): la curva de 300 m no cabe (necesita 150 m a
   cada lado; libre 100 m antes y 400 m después); se deja sin curva, como quiebre de pendiente. Longitud máxima que
   cabría: 200 m."}`; `avisos` con ese mismo texto. OK solo si `numero_curvas` es **0**, `longitud_maxima` es **200** y
   el aviso dice la longitud máxima. Sin `copia`, sin entrada nueva en Deshacer, sin archivo nuevo en `backups\`.

4. **Errores controlados sin tocar nada.** Cada uno debe responder `ok: false` con el texto indicado, sin entrada nueva
   en Deshacer y sin archivo nuevo en `backups\`:
   - `args={"alineamiento":"NoExiste","nombre":"R","pvis":"[[0,100],[400,105]]","simular":true}` →
     `No existe el alineamiento 'NoExiste'. Usa listar_alineamientos.`
   - `args={"alineamiento":"Eje 1","nombre":"<un perfil que ya exista en Eje 1, por ejemplo Terreno - Terreno>","pvis":"[[0,100],[400,105]]","simular":true}`
     → `Ya existe el perfil 'Terreno - Terreno' en el alineamiento 'Eje 1'. Usa listar_perfiles o pasa otro 'nombre'.`
     (si `Eje 1` no tiene ningún perfil, salta este punto y anótalo).
   - **Progresivas desordenadas**: `args={"alineamiento":"Eje 1","nombre":"R","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":400,\"cota\":110},{\"progresiva\":300,\"cota\":105}]","simular":true}`
     → `Las progresivas de 'pvis' deben ir en orden estrictamente creciente: el PVI 3 (300) no es mayor que el PVI 2
     (400). Ordena la lista por progresiva y quita los repetidos.`
   - **PVI fuera del eje**: `args={"alineamiento":"Eje 1","nombre":"R","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":900,\"cota\":105}]","simular":true}`
     → `El PVI 2 está en la progresiva 900, fuera del alineamiento (va de 0 a 831.327). Usa listar_alineamientos para
     ver el rango del eje.`
   - `args={"alineamiento":"Eje 1","nombre":"R","pvis":"[[0,100]]","simular":true}` → `'pvis' tiene 1 PVI; una rasante
     necesita al menos 2 (inicio y fin).`
   - `args={"alineamiento":"Eje 1","nombre":"R","pvis":"[[0,100],[400,105]]","tipo_curva":"asimetrica","simular":true}`
     → `El parámetro 'tipo_curva' debe ser 'parabola_simetrica' (por defecto) o 'circular'; llegó 'asimetrica'...`
   - `args={"alineamiento":"Eje 1","nombre":"R","pvis":"[[0,100],[400,105]]","estilo":"NoExiste","simular":true}` →
     `No existe el estilo de perfil 'NoExiste' en el dibujo. Disponibles: ...` **Anota la lista literal**: en 1.7.0 a
     1.9.0 salía `ninguno legible`; en 1.10.0 el nombre se lee por otra vía (`DBObject.Name` o la clave del
     diccionario). Si sigue diciendo `ninguno legible`, es el hallazgo principal de este paso.
   - `args={"alineamiento":"Eje 1","nombre":"R","pvis":"[[0,100],[400,105]]","conjunto_etiquetas":"NoExiste","simular":true}`
     → `No existe el conjunto de etiquetas de perfil 'NoExiste' en el dibujo. Disponibles: ...` (anota la lista).
   - `args={"alineamiento":"Eje 1","nombre":"R","pvis":"[[0,100],[400,105]]","vista_perfil":"NoExiste","simular":true}`
     → `No existe la vista de perfil 'NoExiste' en el alineamiento 'Eje 1'...`

5. **Creación real (decisivo).** `crear_rasante` **sin** `simular`:
   `args={"alineamiento":"Eje 1","nombre":"Rasante 1","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":400,\"cota\":110,\"longitud_curva\":120},{\"progresiva\":831.33,\"cota\":105}]","pedido":"crea la rasante Rasante 1 del Eje 1 con PVI en 0 a cota 100, 400 a cota 110 con curva de 120 m, y 831.33 a cota 105"}`.
   Esperado: `simulado: false`, `mensaje` igual a la `accion` del paso 2, `cambios` que incluye `existe`,
   `numero_pvis`, `numero_curvas`, `tramos` y `curvas`; `antes: {"existe": false, "numero_pvis": 0,
   "numero_curvas": 0, ...}`; `despues` con `existe: true`, `tipo: "FG"` (leído del dibujo: `Profile.ProfileType`),
   `progresiva_inicial: 0`, `progresiva_final: 831.3274` (leídas: `StartingStation`/`EndingStation`), `cota_minima` y
   `cota_maxima` **leídas** (`ElevationMin`/`ElevationMax`; anota los valores: lo previsto es `100` y `109.5248`, pero si
   la API da la cota del PVI saldrá `110` y un aviso `Las cotas extremas leídas ... difieren de las previstas`: no es
   fallo, es un hallazgo sobre `ElevationMax`), `numero_pvis: 3` y `numero_curvas: 1` (**contados en el dibujo**),
   `tramos` y `curvas` como en el paso 2, `estilo`, `conjunto_etiquetas`, `capa`; `copia` con `ruta` dentro de
   `backups\`, `metodo: "copia_de_disco"` (o `SaveAs` si el DWG no estaba guardado) y `estado: "terminada"`; `datos` con
   `pvis_creados` (3 elementos leídos del perfil: `{"numero": 1, "progresiva": 0, "cota": 100, "pendiente_entrada_api":
   ..., "pendiente_salida_api": ..., "tipo": "..."}` etc.), `entidades` (3 elementos: tangente de 0 a 340, parábola de 340
   a 460 con `longitud: 120`, `k`, `progresiva_pvi: 400`, `cota_pvi: 110`, `tipo_curva_api` y punto alto, y tangente de
   460 a 831.3274), `cota_minima_prevista: 100`, `cota_maxima_prevista: 109.5248`, `progresiva_final_eje: 831.3274`;
   `avisos` con `PVI 3: la progresiva 831.33 se ajusta al final del eje (831.327).` y una línea que empieza por `Las
   pendientes de la API (ProfileTangent.Grade, ProfilePVI.GradeIn/GradeOut) vienen en` (anota si dice `tanto por uno`
   o `porcentaje`); e `id_log`. Si el eje tiene vista de perfil, haz `_.ZOOM _E`: en la vista debe verse la rasante
   (línea recta de 0 a 340, curva hasta 460 y recta hasta el final), **sin** cuadros de diálogo abiertos. OK solo si
   `tipo` es `FG`, `numero_pvis` es 3, `numero_curvas` es 1 y las progresivas coinciden. Hallazgos posibles, anótalos
   literales y **para** en los dos primeros:
   - error `Profile.CreateByLayout` o `La API no devolvió el objeto creado`: no se pudo crear el perfil vacío.
   - error que empieza por `No se pudo añadir el PVI` o `No se pudo crear la curva vertical del PVI 2`: anota el
     mensaje completo (decide `AddPVI` y `AddFreeSymmetricParabolaByPVIAndCurveLength`); comprueba con
     `listar_perfiles` que `Rasante 1` **no** quedó (la transacción se descartó): si quedó, es el hallazgo más grave.
   - error `La rasante 'Rasante 1' se creó pero tiene N PVI y se pidieron 3` o `su PVI 2 quedó en la progresiva ...`:
     el perfil existe pero no refleja lo pedido; anota `despues` y deshaz con Ctrl+Z.
   - aviso `El perfil se creó con tipo 'X' en vez de 'FG'`, `La curva del PVI 2 quedó con longitud ...`, `K leída de la
     API ...` o `El perfil tiene N tangentes ...`: anótalos con los números.

6. **Lectura cruzada (decisivo).** `listar_perfiles` con `args={"alineamiento":"Eje 1"}`: debe aparecer `Rasante 1`
   con `tipo: "FG"`, `inicio: 0`, `fin: 831.3274` y `pvis: 3`. `listar_pvis` con
   `args={"alineamiento":"Eje 1","perfil":"Rasante 1"}`: 3 PVI; el 1 con `ps_pct: 2.5`, el 2 con `pe_pct: 2.5`,
   `ps_pct: -1.1592`, `a_pct: 3.6592`, `tiene_curva: true`, `longitud_curva: 120` (anota `tipo_curva`: es el texto de
   `ProfileEntityType`), el 3 con `pe_pct: -1.1592`. Luego `pk_a_punto` con `perfil: "Rasante 1"`:
   - `args={"alineamiento":"Eje 1","pk":200,"perfil":"Rasante 1"}` → `cota: 105` (mitad del primer tramo, cota
     interpolada en la tangente).
   - `args={"alineamiento":"Eje 1","pk":400,"perfil":"Rasante 1"}` → `cota: 109.4511` (bajo el PVI, sobre la parábola:
     110 menos la externa 0,5489).
   - `args={"alineamiento":"Eje 1","pk":600,"perfil":"Rasante 1"}` → `cota: 107.6816` (segundo tramo).
   - `args={"alineamiento":"Eje 1","pk":0,"perfil":"Rasante 1"}` → `cota: 100` y `pk: 831.3274` → `cota: 105`.
   OK si el perfil está en la lista con tipo FG y 3 PVI y las cinco cotas coinciden (±0,001). Si la cota en 400 sale
   `110`, la curva no se creó aunque la respuesta lo diga: hallazgo grave.

7. **Una sola entrada de Deshacer y una copia (decisivo).** Abre el menú Deshacer: respecto al paso 1 debe haber **una**
   entrada nueva (`Executefunction`), la del paso 5; los pasos 2, 3, 4 y 6 no añaden ninguna. En `backups\` debe haber
   **un** archivo nuevo `<nombre del dwg>_<fecha>_crear_rasante.dwg` (el `copia.ruta` del paso 5; si `copia.reutilizada`
   es `true` porque el DWG no cambió desde la copia anterior, no hay archivo nuevo y `copia.ruta` apunta al anterior:
   anótalo, no es fallo). En `mcp_log.jsonl` deben estar las líneas `llamada` de los pasos 2 y 3 (`"simular":true`,
   `ok:true`), 4 (`ok:false`), 5 (con el `pedido` literal y `ok:true`) y 6. **No deshagas todavía.**

8. **Error por nombre repetido.** Repite exactamente el `crear_rasante` del paso 5 (sin `simular`). Esperado:
   `ok: false` y `error` que contiene `Ya existe el perfil 'Rasante 1' en el alineamiento 'Eje 1'. Usa listar_perfiles`;
   con `id_log`. OK solo si además **no** hay entrada nueva en Deshacer y `listar_perfiles` sigue mostrando un solo
   `Rasante 1`. Si aparece un archivo nuevo en `backups\`, anótalo como hallazgo (la copia de disco se inicia al
   entrar; no es un fallo de seguridad, pero es una copia innecesaria, igual que en 1.7.0 a 1.9.0).

9. **Segunda rasante con dos curvas circulares, longitud por defecto y estilo pedido (decisivo).** Con un estilo de
   perfil **de la lista del paso 4** (si la lista salió `ninguno legible`, no pases `estilo`): `crear_rasante` con
   `args={"alineamiento":"Eje 1","nombre":"Rasante 2","pvis":"[[0,100],[300,106],[400,104],[831.3274,110]]","longitud_curva":60,"tipo_curva":"circular","estilo":"<uno>","pedido":"crea la rasante Rasante 2 del Eje 1 con curvas circulares de 60 m"}`.
   Esperado: `mensaje` que termina en `con 4 PVI y 2 curvas verticales (curva circular), de 0 a 831.327`; `despues`
   con `numero_pvis: 4`, `numero_curvas: 2`, `tipo_curva: "circular"`, `estilo` con el nombre pedido, `tramos` con
   `pendiente_pct` `2`, `-2` y `1.3911`, y `curvas` con dos elementos:
   - `{"pvi": 2, "progresiva_pvi": 300, "cota_pvi": 106, "a_pct": -4, "longitud": 60, "k": 15, "tipo": "cresta",
     "progresiva_inicio": 270, "progresiva_fin": 330, "cota_inicio": 105.4, "cota_fin": 105.4, "longitud_maxima": 200,
     "aviso": null}`
   - `{"pvi": 3, "progresiva_pvi": 400, "cota_pvi": 104, "a_pct": 3.3911, "longitud": 60, "k": 17.6936, "tipo": "valle",
     "progresiva_inicio": 370, "progresiva_fin": 430, "cota_inicio": 104.6, "cota_fin": 104.4173, "longitud_maxima": 140,
     "aviso": null}` (140 = 2 × (100 − 30): la mitad de la primera curva ya ocupa 30 m del tramo de 100 m);
   `cota_minima_prevista: 100`, `cota_maxima_prevista: 110`; `avisos` con `Las cotas de inicio y fin de cada curva y las
   cotas extremas se calculan como parábola; con curvas circulares la diferencia es de milímetros.`; `datos.entidades`
   con 5 elementos (tangente, circular con `radio` y `k`, tangente, circular, tangente). `listar_perfiles` muestra
   ahora `Rasante 1` y `Rasante 2`, los dos `FG`. OK si `numero_curvas` es 2, las dos curvas van de 270 a 330 y de 370
   a 430 y el estilo coincide. Hallazgo posible: error `No se pudo crear la curva vertical del PVI 2 (... curva circular
   ...)`: `AddFreeCircularCurveByPVIAndLength` rechazó la longitud; anota el mensaje; `Rasante 2` no debe quedar.

10. **Deshacer.** En Civil 3D, `_.UNDO 1` (o Ctrl+Z) **una vez**. Esperado: desaparece `Rasante 2` (`listar_perfiles`
    ya no la muestra y, si hay vista, deja de verse). Otro `_.UNDO 1`: desaparece `Rasante 1`. El eje sigue. **Hallazgo
    conocido**: con la 1.7.0 `_.UNDO` dejó la entrada de Deshacer pero la superficie TIN siguió en el dibujo; anota si
    con los perfiles pasa lo mismo (perfil que sigue en `listar_perfiles` tras deshacer) o si sí se quitan: es una de
    las preguntas del informe.

11. **Cierre.** `leer_log` con `args={"ultimas_n":30}`: una línea `llamada` por cada paso, con `ok:false` en los
    errores controlados (pasos 4 y 8) y `ok:true` en los demás. No guardes el dibujo (es una copia).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_110_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, y las respuestas a estas **preguntas para la memoria técnica**:

1. ¿En qué unidad vienen las pendientes de la API? (el aviso del paso 5: `tanto por uno` o `porcentaje`, y los valores
   literales de `pendiente_api` de la primera tangente y `pendiente_entrada_api`/`pendiente_salida_api` del PVI 2).
2. ¿Qué texto devuelve `tipo_curva_api` (`VerticalCurveType`) para la cresta (paso 5) y para el valle (paso 9)? ¿Y
   `tipo` en `pvis_creados` (`ProfilePVI.PVIType`) y `tipo_curva` en `listar_pvis` (`ProfileEntityType`)?
3. `cota_maxima` leída en el paso 5: ¿`109.5248` (vértice de la parábola) o `110` (PVI)? ¿Y la `k` leída frente a
   `32.7939`?
4. Las listas de estilos de perfil y de conjuntos de etiquetas que devolvieron los errores del paso 4 (¿nombres o
   `ninguno legible`?). Sirven para decidir `Autodesk.Civil.DatabaseServices.DBObject.Name` y `DBDictionary.NameAt`.
5. ¿`_.UNDO 1` quitó los perfiles del dibujo (paso 10)?
6. La capa en que quedaron los perfiles (`despues.capa`) y si la rasante se vio en la vista de perfil (si había).

Y la lista de miembros de la sección 1.10.0 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
