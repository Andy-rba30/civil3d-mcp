# Prompt de validación de la 1.9.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- Civil 3D 2027 abierto con **una copia** de un DWG de prueba **guardado en disco**, con una plantilla normal de
  Civil 3D (hace falta que tenga al menos un estilo de perfil, un estilo de vista de perfil y, si puede ser, un conjunto
  de bandas y un conjunto de etiquetas de perfil). Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`.
- **Una superficie y un alineamiento que se crucen.** Valen los de la sub-tanda anterior: la superficie `Terreno`
  creada con `crear_superficie_tin` (VALIDACION_17) y el eje `Eje 1` creado con `crear_alineamiento_desde_polilinea`
  (VALIDACION_18; va de `1000,2000` a `1600,2300`). Si el DWG ya trae una superficie y un eje que pasa por encima de
  ella, úsalos y anota sus nombres (en este prompt se escriben `Terreno` y `Eje 1`). **La superficie debe cubrir todo
  el eje**: si no, el perfil saldrá más corto que el eje y la respuesta lo avisará (no es fallo de la herramienta, pero
  anótalo). Ningún dato real.
- El plugin **1.9.0** se instala en el paso 0 desde la rama `tarea/2.3-perfil-vista` (incluye lo de las ramas
  `tarea/2.1-superficie-tin` y `tarea/2.2-alineamiento`); el bundle queda con `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`. Puente
  reiniciado con el `main.py` de 1.9.0 (`uv sync` antes) y el cliente MCP reiniciado para que cargue la lista de **48**
  herramientas del plugin (más `buscar_api` del puente).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.9.0**: la herramienta de escritura `crear_perfil_terreno_y_vista` (perfil de terreno EG de un eje
  sobre una superficie **y** la vista de perfil de ese eje, los dos en **una sola** transacción y **una sola** entrada de
  Deshacer; estilos y conjuntos por defecto del dibujo o los pedidos; punto de inserción explícito o automático a la
  derecha de lo dibujado; asignación del perfil a las bandas de la vista; `simular`, copia de seguridad, relectura con
  progresivas y cotas). Es la tercera herramienta del camino `Escritura.CrearObjeto` (1.7.0). **Los pasos 2, 4, 5, 6 y 8
  son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.9.0):
  `Profile.CreateFromSurface(nombre, alignmentId, surfaceId, layerId, styleId, labelSetId)`, `Profile.ElevationMin/Max`,
  `Alignment.GetProfileViewIds`, `ProfileView.Create(alignmentId, Point3d, nombre, bandSetId, styleId)`,
  `ProfileView.Location/StationStart/StationEnd/Name`, `ProfileView.Bands` (`GetBottomBandItems`, `GetTopBandItems`,
  `SetBottomBandItems`, `SetTopBandItems`), `ProfileViewBandItem.Profile1Id/Profile2Id`, `Styles.ProfileStyles`,
  `Styles.ProfileViewStyles`, `Styles.ProfileViewBandSetStyles`, `Styles.LabelSetStyles.ProfileLabelSetStyles`,
  `Settings.GetSettings<SettingsProfile>().StyleSettings`, `Settings.GetSettings<SettingsProfileView>().Styles`
  (`ProfileViewStyleId`, `ProfileViewBandStyleId`), `ObjectLayerSettings.GetObjectLayerSetting(Profile)`,
  `Entity.GeometricExtents` y `Database.Extmin/Extmax`.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 4): **"crea el perfil de terreno del
  Eje 1 sobre la superficie Terreno y su vista a la derecha"**.

---

Eres el agente de validación de la versión 1.9.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo. En este prompt no hace falta
  en ningún paso (solo `_.ZOOM` y `_.UNDO`, que son del propio Civil 3D).
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Si el dibujo ya tiene un perfil llamado `Terreno - Terreno` en `Eje 1` o una vista `Eje 1 - Vista`, usa
  `nombre_perfil: "EG MCP 19"` y `nombre_vista: "Vista MCP 19"` en todos los pasos y anótalo.
- Los números esperados son con unidades de metros y redondeo a 4 decimales; una diferencia menor de 0,001 es OK.
- Donde dice `<inicio>` y `<fin>` se refiere a las progresivas del eje que devuelve `listar_alineamientos` en el paso 1
  (con el eje de VALIDACION_18 son `0` y `831.3274`).

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `tarea/2.3-perfil-vista`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`, `uv sync`. Reinicia el puente y el
   cliente MCP. Abre Civil 3D con la copia del DWG. `ping` con `args={"pedido":"comprueba que el plugin responde"}` →
   `version` debe ser `1.9.0`. Pide la lista de herramientas: deben ser **48** del plugin e incluir
   `crear_perfil_terreno_y_vista` con los parámetros `alineamiento`, `superficie`, `nombre_perfil`, `nombre_vista`,
   `estilo_perfil`, `conjunto_etiquetas_perfil`, `estilo_vista`, `conjunto_bandas`, `punto_insercion`, `simular` y
   `pedido`. Si no, para aquí.

1. **Punto de partida.** `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}`: anota `inicio`, `fin`
   y `perfiles` de `Eje 1` (debe tener `perfiles: []` o, al menos, ningún perfil llamado `Terreno - Terreno`).
   `listar_superficies`: `Terreno` debe estar y ser `TIN`. `extension_superficie` con `args={"superficie":"Terreno"}`:
   anota `min_x`, `max_x`, `min_y`, `max_y` (sirven para comprobar el punto automático del paso 2). Abre el menú
   Deshacer de Civil 3D y anota cuántas entradas tiene (o la última). Mira si existe la carpeta `backups\` junto al
   DWG y cuántos archivos tiene.

2. **Simulación (decisivo).** `crear_perfil_terreno_y_vista` con
   `args={"alineamiento":"Eje 1","superficie":"Terreno","simular":true,"pedido":"simula el perfil de terreno del Eje 1 y su vista"}`.
   Esperado: `simulado: true`, `accion` que empieza por `Crear el perfil de terreno 'Terreno - Terreno' del alineamiento
   'Eje 1' sobre la superficie 'Terreno' y su vista de perfil 'Eje 1 - Vista' a la derecha de lo dibujado`,
   `antes: {"perfil_existe": false, "vista_existe": false, ...}` y `despues` con: `perfil_existe: true`,
   `vista_existe: true`, `perfil: {"nombre": "Terreno - Terreno", "tipo": "EG", "progresiva_inicial": <inicio>,
   "progresiva_final": <fin>, "cota_minima": null, "cota_maxima": null, "estilo": "<estilo de perfil por defecto del
   dibujo>", "conjunto_etiquetas": "<el del dibujo o (sin etiquetas)>"}`, `vista: {"nombre": "Eje 1 - Vista",
   "punto_insercion": {"x": X, "y": Y}, "estilo": "<estilo de vista por defecto>", "conjunto_bandas": "<el del dibujo o
   (sin bandas)>", "progresiva_inicial": <inicio>, "progresiva_final": <fin>, "bandas": null}`,
   `punto_insercion_automatico: true` y `capa_perfil`. `avisos` debe incluir `Las cotas del perfil (cota_minima,
   cota_maxima) se calculan al crear.` y un texto que empieza por `Punto de inserción automático: x = ` con la regla
   (extremo derecho + 10 % del ancho, mínimo 50; y = extremo inferior). **Comprueba la regla**: `X` debe ser
   mayor o igual que el mayor `max_x` entre la superficie (paso 1) y el eje (1600) más el 10 % del ancho total, e `Y`
   igual al menor `min_y` entre la superficie y el eje (2000). Anota X e Y: se usan en el paso 4. OK solo si
   `perfil.tipo` es `EG`, las progresivas coinciden con las del eje, no hay `copia`, el menú Deshacer **no** cambió y no
   apareció ningún archivo nuevo en `backups\`. Hallazgos posibles: aviso `No se pudo leer el estilo ... por defecto`
   (anótalo: `Settings.GetSettings<SettingsProfile>` o `<SettingsProfileView>` falló), aviso `No se pudo leer el
   conjunto de bandas por defecto` (anótalo: `ProfileViewBandStyleId` no era un conjunto de bandas), error `No se pudo
   calcular el punto de inserción automático` (`GeometricExtents` falló en el eje y en la superficie).

3. **Errores controlados sin tocar nada.** Cada uno debe responder `ok: false` con el texto indicado, sin entrada nueva
   en Deshacer y sin archivo nuevo en `backups\`:
   - `args={"alineamiento":"NoExiste","superficie":"Terreno","simular":true}` → `No existe el alineamiento 'NoExiste'. Usa listar_alineamientos.`
   - `args={"alineamiento":"Eje 1","superficie":"NoExiste","simular":true}` → `No existe la superficie 'NoExiste'. Usa listar_superficies.`
   - `args={"alineamiento":"Eje 1","superficie":"Terreno","estilo_perfil":"NoExiste","simular":true}` → `No existe el
     estilo de perfil 'NoExiste' en el dibujo. Disponibles: ...` (anota la lista: sirve para la memoria técnica).
   - `args={"alineamiento":"Eje 1","superficie":"Terreno","estilo_vista":"NoExiste","simular":true}` → `No existe el
     estilo de vista de perfil 'NoExiste'` con la lista (anótala).
   - `args={"alineamiento":"Eje 1","superficie":"Terreno","conjunto_bandas":"NoExiste","simular":true}` → `No existe el
     conjunto de bandas de vista de perfil 'NoExiste'` con la lista (anótala).
   - `args={"alineamiento":"Eje 1","superficie":"Terreno","conjunto_etiquetas_perfil":"NoExiste","simular":true}` →
     `No existe el conjunto de etiquetas de perfil 'NoExiste'` con la lista (anótala).
   - `args={"alineamiento":"Eje 1","superficie":"Terreno","punto_insercion":"{\"x\":2000}","simular":true}` → `El
     parámetro 'punto_insercion' no es válido (faltan 'x' o 'y')`.
   Si alguna lista dice `ninguno legible`, es un hallazgo sobre `StyleBase.Name` de esa colección.

4. **Creación real con punto automático (decisivo).** `crear_perfil_terreno_y_vista` **sin** `simular`:
   `args={"alineamiento":"Eje 1","superficie":"Terreno","pedido":"crea el perfil de terreno del Eje 1 sobre la superficie Terreno y su vista a la derecha"}`.
   Esperado: `simulado: false`, `cambios` que incluye `perfil_existe`, `vista_existe`, `perfil` y `vista`;
   `antes: {"perfil_existe": false, "vista_existe": false, ...}`; `despues` con `perfil_existe: true`,
   `vista_existe: true`, `perfil` con `tipo: "EG"`, `progresiva_inicial: <inicio>`, `progresiva_final: <fin>`,
   `cota_minima` y `cota_maxima` **con números** (`cota_maxima >= cota_minima`, dentro del rango de cotas de la
   superficie: compara con `min_z`/`max_z` de `extension_superficie`), `estilo` y `conjunto_etiquetas`; `vista` con
   `nombre: "Eje 1 - Vista"`, `punto_insercion` **igual** al X, Y del paso 2 (leído del dibujo: `ProfileView.Location`),
   `progresiva_inicial: <inicio>`, `progresiva_final: <fin>` y `bandas` con el número de bandas del conjunto (por
   ejemplo 2 con el conjunto estándar); `copia` con `ruta` dentro de `backups\`, `metodo: "copia_de_disco"` (o
   `SaveAs` si el DWG no estaba guardado) y `estado: "terminada"`; `datos` con `bandas_asignadas` (el mismo número que
   `vista.bandas`), `capa_perfil` y las progresivas del eje; `avisos` con `Perfil asignado como fuente (Profile1 y
   Profile2) de N bandas de la vista.`; e `id_log`. En pantalla: haz `_.ZOOM _E`; a la derecha de la superficie y el eje
   debe aparecer la cuadrícula de la vista de perfil con la línea del terreno de principio a fin y sus bandas debajo
   con datos (progresivas y cotas), **sin** cuadros de diálogo abiertos. OK solo si `perfil.tipo` es `EG`,
   `cota_maxima >= cota_minima`, `vista.punto_insercion` coincide con el previsto y la vista se ve en pantalla.
   Hallazgos posibles, anótalos literales y **para** en los dos primeros:
   - error que contiene `Se creó el perfil pero ProfileView.Create falló` → la vista no se pudo crear; comprueba con
     `listar_perfiles` (`args={"alineamiento":"Eje 1"}`) que el perfil **tampoco** quedó (la transacción se descartó):
     si quedó, es el hallazgo más grave (anótalo así).
   - error `Profile.CreateFromSurface no devolvió el perfil` o cualquier excepción de la API con `CreateFromSurface`.
   - aviso `No se pudo asignar el perfil a las bandas de la vista (...)`: la vista y el perfil están, pero las bandas
     quedaron como en la plantilla (anota el mensaje literal: decide `Profile1Id/Profile2Id` y `Set*BandItems`).
   - aviso `El perfil se creó con tipo 'X' en vez de 'EG'` o `la superficie no cubre todo el eje`: anótalos con los números.
   - `vista.punto_insercion` distinto del previsto: `ProfileView.Location` no es el punto de inserción; anota ambos.
   - aviso `La capa de perfiles de la configuración ... no existe` o `No se pudo leer la capa de perfiles` (anótalo;
     puede decir `El enumerado SettingsObjectLayerType ... no tiene el valor 'Profile'` con los valores reales).

5. **Lectura cruzada (decisivo).** `listar_perfiles` con `args={"alineamiento":"Eje 1"}`: debe aparecer
   `Terreno - Terreno` con `tipo: "EG"`, `inicio: <inicio>` y `fin: <fin>`. Luego `pk_a_punto` con
   `args={"alineamiento":"Eje 1","pk":300,"perfil":"Terreno - Terreno"}` → el punto del eje en la progresiva 300 con una
   `cota` que debe coincidir (±0,01) con la que devuelve `cota_superficie` con
   `args={"superficie":"Terreno","x":<x>,"y":<y>}` en ese mismo punto. OK si el perfil está en la lista con tipo EG y la
   cota coincide con la superficie.

6. **Una sola entrada de Deshacer y una copia (decisivo).** Abre el menú Deshacer: respecto al paso 1 debe haber **una**
   entrada nueva (`Executefunction`), la del paso 4; los pasos 2, 3 y 5 no añaden ninguna. En `backups\` debe haber
   **un** archivo nuevo `<nombre del dwg>_<fecha>_crear_perfil_terreno_y_vista.dwg` (el `copia.ruta` del paso 4). En
   `mcp_log.jsonl` deben estar las líneas `llamada` de los pasos 2 y 3 (con `"simular":true` y `ok:true` u `ok:false`
   en los errores controlados), 4 (con el `pedido` literal y `ok:true`) y 5. **No deshagas todavía.**

7. **Error por nombre repetido.** Repite exactamente el `crear_perfil_terreno_y_vista` del paso 4 (sin `simular`).
   Esperado: `ok: false` y `error` que contiene `Ya existe el perfil 'Terreno - Terreno' en el alineamiento 'Eje 1'.
   Usa listar_perfiles`; con `id_log`. Luego
   `args={"alineamiento":"Eje 1","superficie":"Terreno","nombre_perfil":"Otro perfil","simular":true}` → `ok: false` con
   `Ya existe la vista de perfil 'Eje 1 - Vista' del alineamiento 'Eje 1'. Pasa otro 'nombre_vista'`. OK solo si además
   **no** hay entrada nueva en Deshacer y `listar_perfiles` sigue mostrando un solo `Terreno - Terreno`. Si aparece un
   archivo nuevo en `backups\` en la primera llamada, anótalo como hallazgo (la copia de disco se inicia al entrar; no
   es un fallo de seguridad, pero es una copia innecesaria, igual que en la 1.7.0 y la 1.8.0).

8. **Punto de inserción explícito y estilos pedidos (decisivo).** Con un estilo de perfil, un estilo de vista y un
   conjunto de bandas **de las listas del paso 3**, y un punto **debajo** de la vista anterior (la Y del paso 2 menos
   500): `crear_perfil_terreno_y_vista` con
   `args={"alineamiento":"Eje 1","superficie":"Terreno","nombre_perfil":"EG 2","nombre_vista":"Vista 2","estilo_perfil":"<uno>","estilo_vista":"<uno>","conjunto_bandas":"<uno>","punto_insercion":"{\"x\":<X del paso 2>,\"y\":<Y del paso 2 - 500>}","pedido":"crea otro perfil de terreno del Eje 1 con su vista debajo de la anterior"}`.
   Esperado: `despues.perfil.nombre: "EG 2"`, `despues.perfil.estilo` y `despues.vista.estilo` con los nombres pedidos,
   `despues.vista.conjunto_bandas` con el pedido, `vista.punto_insercion` **igual** al pasado,
   `punto_insercion_automatico: false`, `avisos` **sin** el texto `Punto de inserción automático`, y `copia` nueva. En
   pantalla, la segunda vista debajo de la primera. `listar_perfiles` muestra ahora `Terreno - Terreno` y `EG 2`, los dos
   `EG`. OK si los nombres, estilos y el punto coinciden.

9. **Deshacer.** En Civil 3D, `_.UNDO 1` (o Ctrl+Z) **una vez**. Esperado: desaparecen **a la vez** el perfil `EG 2` y la
   vista `Vista 2` (una sola entrada quita los dos objetos); `listar_perfiles` ya no muestra `EG 2` y la segunda vista
   no está en pantalla. Otro `_.UNDO 1`: desaparecen `Terreno - Terreno` y `Eje 1 - Vista`; `listar_perfiles` devuelve
   `[]` (o los perfiles que ya hubiera antes) y la primera vista tampoco está. El eje y la superficie siguen. Si tras
   deshacer queda el perfil sin la vista (o al revés), es un hallazgo: los dos objetos no quedaron en la misma entrada.

10. **Cierre.** `leer_log` con `args={"ultimas_n":25}`: una línea `llamada` por cada paso, con `ok:false` en los errores
    controlados (pasos 3 y 7) y `ok:true` en los demás. No guardes el dibujo (es una copia).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_19_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, las listas de estilos de perfil, estilos de vista de perfil, conjuntos de bandas y conjuntos de etiquetas de
perfil que devolvieron los errores del paso 3 (sirven para la memoria técnica: el nombre depende del idioma), el punto
automático previsto y el leído (`vista.punto_insercion` de los pasos 2 y 4), el número de bandas y si mostraban datos
del perfil, la capa en que quedó el perfil (`despues.capa_perfil`) y la lista de miembros de la sección 1.9.0 de
`miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D 2027` con la fecha (o a `no existe`, con el
error literal, en especial `ProfileView.Location`, `Profile1Id/Profile2Id`, `ProfileViewBandStyleId` y el valor
`SettingsObjectLayerType.Profile`). **No pegues en el informe líneas de `mcp_log.jsonl` con rutas reales**:
sustitúyelas por `<ruta>`.
