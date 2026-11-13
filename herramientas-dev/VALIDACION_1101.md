# Prompt de validación de la 1.10.1 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- Civil 3D 2027 abierto con **un DWG nuevo y vacío, guardado en disco** (por ejemplo `C:\Proyectos\dibujos_prueba\prueba_1101.dwg`),
  creado con una plantilla normal de Civil 3D (hacen falta estilos de superficie, alineación, perfil y vista de perfil,
  y conjuntos de etiquetas y de bandas: los de la plantilla). Sin alineamientos, sin superficies y sin polilíneas.
  Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`. Ningún dato real.
- **Archivo de puntos sintético** de `VALIDACION_17.md`: `C:\Proyectos\dibujos_prueba\puntos_prueba.csv` con exactamente
  estas 9 líneas (si no existe, créalo):

  ```
  # archivo de prueba, 8 puntos PENZD
  1,1000.0,2000.0,100.0,TN
  2,1010.0,2000.0,100.5,TN
  3,1020.0,2000.0,101.0,TN
  4,1000.0,2010.0,100.2,TN
  5,1010.0,2010.0,100.8,TN
  6,1020.0,2010.0,101.4,TN
  7,1000.0,2020.0,100.4,TN
  8,1020.0,2020.0,102.0,TN
  ```

- El plugin **1.10.1** se instala en el paso 0 desde la rama `tarea/1.10.1-correcciones`; el bundle queda con
  `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.10.1 (`uv sync` antes) y el cliente MCP
  reiniciado. La lista sigue siendo de **49** herramientas del plugin (más `buscar_api` del puente): esta versión no
  añade ninguna.
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.10.1** (cuatro correcciones, ninguna herramienta nueva):
  1. `ejecutar_comando` **conserva los espacios finales** del texto. En la línea de comandos un espacio final equivale a
     Enter, así que `_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ` (dos espacios al final) cierra la polilínea. Si
     el texto termina en espacio o salto de línea se envía tal cual; si no, se añade un Enter como antes. Hasta la
     1.10.0 se recortaban y la orden se quedaba esperando (informes 1.8.0 y 1.10.0, paso 0).
  2. **Comprobación previa**: antes de una escritura real, el plugin ensaya la misma herramienta con `simular=true`
     (sin tocar nada). Si el ensayo falla, responde el mismo error **sin** abrir el contexto de comando, así que no
     queda ninguna entrada `EXECUTEFUNCTION` vacía en el menú Deshacer ni se hace copia (informe 1.10.0, pasos 8 y 10:
     hizo falta un `_.UNDO 1` de más).
  3. `ping` devuelve la versión con **tres** números (`1.10.1`), igual que `GET /ping` (la 1.10.0 daba `1.10.0.0`).
  4. Los errores "No existe el estilo..." de `crear_superficie_tin`, `crear_alineamiento_desde_polilinea` y
     `crear_perfil_terreno_y_vista` listan **los nombres reales** de los estilos (como `crear_rasante` desde 1.10.0) en
     vez de `ninguno legible`.
  **Los pasos 0, 3, 4, 7, 8, 10 y 11 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.10.1):
  `Autodesk.Civil.DatabaseServices.DBObject.Name` sobre los estilos de superficie, alineación, vista de perfil y conjunto
  de bandas, y `Document.SendStringToExecute` con un texto que termina en espacios.

---

Eres el agente de validación de la versión 1.10.1 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` se usa **solo** donde este prompt lo indica (los pasos 2, 3, 5 y 11 lo prueban a propósito). Si
  algo no funciona, es un hallazgo: no lo arregles con otro comando ni con un archivo `.scr`.
- **Copia el texto de `comando` exactamente**, con sus espacios y saltos de línea finales: son lo que se prueba. En
  `args`, `\n` es un salto de línea dentro del texto JSON.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- El parámetro `pvis` es de tipo `json`: se pasa como **texto** con la lista JSON dentro, tal como está escrito aquí.
- Los números esperados son con unidades de metros y redondeo a 4 decimales; una diferencia menor de 0,001 es OK.
- Anota en **cada escritura real** (pasos 6, 8 y 9) los campos `ms`, `ms_espera` y `ms_ejecucion` de la respuesta, y el
  `ms` de su simulación cuando la haya: la comprobación previa hace una simulación antes de cada escritura real y el
  informe debe decir cuánto añade.
- Al final de este prompt hay una lista de **preguntas para el informe**: respóndelas con lo que viste.

## Pasos

0. **Instalar y versión (decisivo, corrección 3).** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la
   rama `tarea/1.10.1-correcciones`: `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`,
   `uv sync`. Reinicia el puente y el cliente MCP. Abre Civil 3D con el DWG vacío. `ping` con
   `args={"pedido":"comprueba que el plugin responde"}` → `"plugin": "ArbaMcp"`, **`"version": "1.10.1"`** (tres
   números; si sale `1.10.1.0` es FALLO), `"hay_dibujo": true`. En PowerShell: `curl.exe -s http://127.0.0.1:8765/ping`
   → `{"ok":true,"servidor":"ArbaMcp","version":"1.10.1"}`. OK solo si las dos versiones son exactamente `1.10.1`.
   Pide la lista de herramientas: deben ser **49** del plugin. Si no, para aquí.

1. **Punto de partida.** `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}` → `[]`.
   `listar_superficies` → `[]`. `leer_variable` con `args={"nombre":"CMDACTIVE"}` → `"valor": "0"`. Abre el menú
   Deshacer de Civil 3D y anota cuántas entradas tiene. Mira si existe la carpeta `backups\` junto al DWG y cuántos
   archivos tiene.

2. **Capa de prueba con saltos de línea finales.** `ejecutar_comando` con
   `args={"comando":"_.-LAYER _M EJE_PRUEBA\n\n","timeout_s":20,"pedido":"crea la capa EJE_PRUEBA"}` (el texto termina en
   **dos saltos de línea**: el primero acepta el nombre y el segundo cierra `-LAYER`). Esperado: `"result": "terminado"`.
   `leer_variable` con `args={"nombre":"CLAYER"}` → `"valor": "EJE_PRUEBA"`; `leer_variable` con
   `args={"nombre":"CMDACTIVE"}` → `"valor": "0"`. Si responde `timeout con ESC`, anótalo (la orden no se cerró) y, si
   `CLAYER` es `EJE_PRUEBA`, sigue; si no, usa en todo el prompt la capa que diga `CLAYER` en lugar de `EJE_PRUEBA`.

3. **Polilínea con dos espacios finales (decisivo, corrección 1).** `ejecutar_comando` con
   `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","timeout_s":20,"pedido":"dibuja la polilínea del eje de prueba"}`
   (**dos espacios** después de `1600,2300` y nada más). Esperado: `"result": "terminado"` en pocos segundos (no
   `timeout con ESC`). Justo después:
   - `leer_variable` con `args={"nombre":"CMDACTIVE"}` → `"valor": "0"` (no quedó otra `PLINE` esperando un punto).
   - `leer_historial` con `args={"ultimas_n":15}`: debe aparecer la línea
     `ejecutar_comando: '_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ' con tiempo máximo 20 s` (con los dos espacios
     antes de la comilla de cierre), **una** `Comando inicia: PLINE` y **una** `Comando termina: PLINE`, y ninguna
     segunda `Comando inicia: PLINE` después.
   OK solo si `result` es `terminado`, `CMDACTIVE` es `0` y el historial muestra una sola `PLINE`. Hallazgo posible: si
   `result` es `timeout con ESC`, la línea de comandos no tomó el espacio final como Enter; anota el historial literal.

4. **La polilínea tiene los cuatro vértices (decisivo).** `crear_alineamiento_desde_polilinea` con
   `args={"nombre":"Eje 1","capa":"EJE_PRUEBA","radio":80,"simular":true,"pedido":"simula el eje Eje 1 con radio 80"}`.
   Esperado (como en `VALIDACION_18.md`, paso 2): `simulado: true`, `accion` que empieza por `Crear el alineamiento 'Eje 1'
   desde la polilínea de la capa 'EJE_PRUEBA' con radio 80`, `despues` con `existe: true`, `longitud: 831.3274`,
   `progresiva_inicial: 0`, `progresiva_final: 831.3274`, `numero_pi: 2`, `numero_curvas: 2` y `pis` con dos elementos:
   PI 1 con `progresiva: 300`, `x: 1300`, `y: 2000`, `deflexion_grados: 90`, `giro: "izquierda"`, `radio_aplicado: 80`;
   PI 2 con `progresiva: 565.6637`, `x: 1300`, `y: 2300`, `deflexion_grados: 90`, `giro: "derecha"`, `radio_aplicado: 80`.
   Anota `estilo` y `conjunto_etiquetas` de `despues`: ahora deben ser **nombres** (no `null` ni `?`). OK solo si
   `longitud` es **831.3274** y `numero_curvas` es **2** (es la prueba de que el paso 3 dibujó los 4 vértices).

5. **Regresión de `ejecutar_comando`.**
   - `args={"comando":"REGEN","pedido":"regenera el dibujo"}` → `"result": "terminado"` (sin espacio final se sigue
     añadiendo el Enter, como antes).
   - `args={"comando":"   ","pedido":"comando vacío a propósito"}` → `ok: false` con `error` que contiene
     `Falta el parámetro obligatorio 'comando'.`
   OK si las dos responden así.

6. **Preparación: eje y superficie reales.**
   - `crear_alineamiento_desde_polilinea` con `args={"nombre":"Eje 1","capa":"EJE_PRUEBA","radio":80,"pedido":"crea el eje Eje 1 con radio 80"}`
     (sin `simular`) → `simulado: false`, `despues.longitud: 831.3274`, `despues.numero_curvas: 2`, `copia` con
     `estado: "terminada"`. Anota `ms`, `ms_espera`, `ms_ejecucion` y compáralos con el `ms` de la simulación del paso 4.
   - `crear_superficie_tin` con
     `args={"nombre":"Terreno","archivo":"C:\\Proyectos\\dibujos_prueba\\puntos_prueba.csv","pedido":"crea la superficie Terreno con los puntos del archivo"}`
     → `simulado: false`, `despues.existe: true`, `despues.puntos: 8`. Anota `ms`, `ms_espera` y `ms_ejecucion` (aquí la
     comprobación previa lee el archivo de puntos una vez más).
   `listar_alineamientos` → `Eje 1` de `0` a `831.3274`; `listar_superficies` → `Terreno` con `tipo_superficie: "TIN"`.

7. **Nombres de estilos en los errores (decisivo, corrección 4).** Cada llamada debe responder `ok: false` con la
   lista **literal** de nombres después de `Disponibles:` (o `Estilos disponibles:`); **anota cada lista**. Ninguna debe
   decir `ninguno legible`. Todas con `simular`, así que no tocan Deshacer ni `backups\`:
   - `crear_superficie_tin` con `args={"nombre":"Terreno 2","grupo_puntos":"NoExiste","estilo":"NoExiste","simular":true}`
     → `No existe el estilo de superficie 'NoExiste' en el dibujo. Estilos disponibles: <lista>. En Civil 3D se ven en
     Espacio de herramientas → Configuración → Superficie → Estilos de superficie.`
   - `crear_alineamiento_desde_polilinea` con `args={"nombre":"Eje 2","capa":"EJE_PRUEBA","radio":80,"estilo":"NoExiste","simular":true}`
     → `No existe el estilo de alineamiento 'NoExiste' en el dibujo. Disponibles: <lista>. ...`
   - `crear_alineamiento_desde_polilinea` con `args={"nombre":"Eje 2","capa":"EJE_PRUEBA","radio":80,"conjunto_etiquetas":"NoExiste","simular":true}`
     → `No existe el conjunto de etiquetas de alineamiento 'NoExiste' en el dibujo. Disponibles: <lista>. ...`
   - `crear_perfil_terreno_y_vista` con `args={"alineamiento":"Eje 1","superficie":"Terreno","estilo_perfil":"NoExiste","simular":true}`
     → `No existe el estilo de perfil 'NoExiste' en el dibujo. Disponibles: <lista>. ...` (en 1.10.0, con la misma
     plantilla, `crear_rasante` dio `Perfil del terreno existente, Intersection Basic, Basic, Design Profile, Layout,
     Right Sample Profile, Left Sample Profile`).
   - Igual con `"conjunto_etiquetas_perfil":"NoExiste"` → `No existe el conjunto de etiquetas de perfil 'NoExiste' ...`
     (en 1.10.0: `Complete Label Set, _No Labels`).
   - Igual con `"estilo_vista":"NoExiste"` → `No existe el estilo de vista de perfil 'NoExiste' ...`
   - Igual con `"conjunto_bandas":"NoExiste"` → `No existe el conjunto de bandas de vista de perfil 'NoExiste' ...`
   OK solo si las siete listas traen nombres. Si alguna dice `ninguno legible`, es el hallazgo principal de este paso:
   anota de qué colección es.

8. **Regresión de una escritura que no crea nada (decisivo).** Comprueba que la comprobación previa no cambia una
   escritura corriente sobre un objeto que ya existe (camino `CambiarSuperficie`). Primero `reconstruir_superficie` con
   `args={"superficie":"Terreno","simular":true,"pedido":"simula reconstruir Terreno"}` → `simulado: true`, `accion:
   "Reconstruir la superficie 'Terreno'"`; anota su `ms`. Después, **sin** `simular`:
   `args={"superficie":"Terreno","pedido":"reconstruye la superficie Terreno"}`. Esperado, igual que en versiones
   anteriores: `simulado: false`, `herramienta: "reconstruir_superficie"`, `mensaje: "Reconstruir la superficie
   'Terreno'"`, `antes` y `despues` con `superficie: "Terreno"` y `esta_desactualizada: false` (si no cambió nada,
   `cambios: []` y se muestran enteros), `copia` con `estado: "terminada"` (puede ser `reutilizada: true`), `datos` con
   `ms` del `Rebuild` e `id_log`. Anota `ms`, `ms_espera` y `ms_ejecucion`. Luego `leer_historial` con
   `args={"ultimas_n":15}`: tras `MCP → reconstruir_superficie [<id_log>]` debe haber **una** pareja
   `Comando inicia: EXECUTEFUNCTION` / `Comando termina: EXECUTEFUNCTION` y la línea `MCP ✓ reconstruir_superficie OK`,
   y **ninguna** línea `MCP ✗`. En el menú Deshacer, **una** entrada nueva respecto a antes de la llamada (la
   simulación no añade ninguna). En `mcp_log.jsonl`, **una** línea con ese `id` y `ok: true` (más la de la simulación,
   con su propio `id` y `"simular":true`). OK solo si la respuesta tiene esa forma, hay una sola entrada de Deshacer y
   una sola línea de log por llamada.

9. **Rasante real.** `crear_rasante` con
   `args={"alineamiento":"Eje 1","nombre":"Rasante 1","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":400,\"cota\":110,\"longitud_curva\":120},{\"progresiva\":831.33,\"cota\":105}]","simular":true,"pedido":"simula la rasante Rasante 1 del Eje 1"}`
   → `simulado: true`, `despues.numero_pvis: 3`, `despues.numero_curvas: 1`; anota su `ms`. Después la misma llamada
   **sin** `simular` y con `pedido` `crea la rasante Rasante 1 del Eje 1 con PVI en 0 a cota 100, 400 a cota 110 con
   curva de 120 m, y 831.33 a cota 105` → `simulado: false`, `despues` con `existe: true`, `tipo: "FG"`,
   `numero_pvis: 3`, `numero_curvas: 1`, `progresiva_final: 831.3274`, `cota_maxima: 109.5248` (como en la 1.10.0,
   paso 5). Anota `ms`, `ms_espera` y `ms_ejecucion`. `listar_perfiles` con `args={"alineamiento":"Eje 1"}` → un solo
   `Rasante 1` (`FG`, `pvis: 3`). Anota cuántos archivos hay ahora en `backups\` y cuántas entradas en el menú Deshacer.

10. **Error a propósito sin entrada vacía (decisivo, corrección 2).** Repite **exactamente** el `crear_rasante` real
    del paso 9 (sin `simular`, nombre `Rasante 1`, con `pedido` `repite la rasante Rasante 1 a propósito`). Esperado:
    `ok: false`, `error` igual que en la 1.10.0: `Error de Civil 3D: ArgumentException: Ya existe el perfil 'Rasante 1'
    en el alineamiento 'Eje 1'. Usa listar_perfiles o pasa otro 'nombre'.` y un `id_log`. Justo después:
    - `leer_historial` con `args={"ultimas_n":15}`: tras `MCP → crear_rasante [<id_log>]` deben aparecer, en este orden,
      `MCP ✗ crear_rasante no se ejecutó: falló la comprobación previa (simulación); sin contexto de comando, sin copia
      y sin entrada en Deshacer: Ya existe el perfil 'Rasante 1' en el alineamiento 'Eje 1'. Usa listar_perfiles o pasa
      otro 'nombre'.`, `Registro <id_log>: crear_rasante ERROR: Ya existe el perfil ...` y `MCP ← crear_rasante ERROR:
      ...`, y **ninguna** línea `Comando inicia: EXECUTEFUNCTION` desde `MCP → crear_rasante [<id_log>]` hasta el final.
      En la 1.10.0 sí aparecía `Comando inicia: EXECUTEFUNCTION` / `Comando termina: EXECUTEFUNCTION`: si aparece, es
      **FALLO**.
    - Menú Deshacer: **ninguna** entrada nueva respecto al final del paso 9.
    - `backups\`: **ningún** archivo nuevo (la copia ya no se empieza).
    - `leer_log` con `args={"ultimas_n":3}`: **una sola** línea con ese `id`, `herramienta: "crear_rasante"`,
      `ok: false`, `error` con `Ya existe el perfil 'Rasante 1'` y el `pedido` literal; ninguna otra línea de esa
      llamada (la simulación del ensayo no deja línea propia).
    - `listar_perfiles` con `args={"alineamiento":"Eje 1"}` → sigue un solo `Rasante 1`.
    OK solo si el error es el mismo, no hay `EXECUTEFUNCTION` para esta llamada, no hay entrada nueva en Deshacer y hay
    una sola línea de log.

11. **Un solo deshacer quita la rasante (decisivo).** `ejecutar_comando` con
    `args={"comando":"_.UNDO 1","timeout_s":30,"pedido":"deshaz la última operación"}` **una sola vez** → `"result":
    "terminado"`. `listar_perfiles` con `args={"alineamiento":"Eje 1"}` → **`[]`**: `Rasante 1` desapareció con un solo
    `_.UNDO 1` (en la 1.10.0 hizo falta uno más, porque el error dejaba una entrada vacía encima). `listar_alineamientos`
    → `Eje 1` sigue (`0` a `831.3274`). OK solo si `Rasante 1` ya no está tras **un** `_.UNDO 1`. Si sigue, no repitas
    el deshacer: anota el historial (`leer_historial` con `ultimas_n` 20) y el menú Deshacer; es el hallazgo principal.

12. **Cierre.** `leer_log` con `args={"ultimas_n":40}`: una línea `llamada` por cada llamada de los pasos 0 a 11, con
    `ok:false` en los errores a propósito (pasos 5, 7 y 10) y `ok:true` en los demás. No guardes el dibujo (es una
    copia).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_1101_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, los hallazgos, el resultado global (OK solo si los pasos decisivos 0, 3, 4, 7, 8, 10 y 11 son OK) y las
respuestas a estas preguntas para la memoria técnica:

1. Las dos versiones del paso 0 (`ping` y `GET /ping`), literales.
2. Paso 3: ¿`terminado` con los dos espacios finales? Valor de `CMDACTIVE` justo después y las líneas literales del
   historial con `PLINE`.
3. Paso 2: ¿cerró `-LAYER` con los dos saltos de línea finales (`terminado`) o hizo falta el ESC del tiempo agotado?
4. Las siete listas de nombres del paso 7, literales (estilos de superficie, de alineación, conjuntos de etiquetas de
   alineación, estilos de perfil, conjuntos de etiquetas de perfil, estilos de vista de perfil y conjuntos de bandas).
5. Paso 10: las líneas literales del historial desde `MCP → crear_rasante [<id_log>]`, y si cambió el menú Deshacer o
   `backups\`.
6. Paso 11: cuántos `_.UNDO 1` hicieron falta para quitar `Rasante 1`.
7. Tiempos de la comprobación previa: para cada escritura real de los pasos 6, 8 y 9, `ms`, `ms_espera` y
   `ms_ejecucion`, junto al `ms` de su simulación (pasos 4, 8 y 9). Desde la 1.10.1 `ms_espera` incluye el ensayo.

Y la lista de miembros de la sección 1.10.1 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
