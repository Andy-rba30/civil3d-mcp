# Prompt de validación de la 1.11.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- Civil 3D 2027 (en español) abierto con **una copia** de un DWG de prueba **guardado en disco**, con una plantilla
  normal de Civil 3D. **Vale el dibujo de la validación 1.10.0** (`C:\Proyectos\dibujos_prueba\PRUEBA_110_VACIO.dwg`),
  que ya tiene la capa `EJE_PRUEBA`, la polilínea de prueba y `Eje 1`: trabaja sobre una copia suya (por ejemplo
  `C:\Proyectos\dibujos_prueba\PRUEBA_111.dwg`). Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`.
  Ningún dato real.
- **Eje.** `Eje 1` de VALIDACION_18: alineamiento creado con `crear_alineamiento_desde_polilinea` desde la polilínea
  `1000,2000 → 1300,2000 → 1300,2300 → 1600,2300` de la capa `EJE_PRUEBA` con radio 80 (va de la progresiva `0` a la
  `831.3274`). **Solo si partes de un dibujo nuevo** (sin `Eje 1`) hay que dibujar la polilínea. Con la 1.10.1 (que
  esta rama trae) ya se puede con `ejecutar_comando`, como en los pasos 2 y 3 de `VALIDACION_1101.md`:
  `args={"comando":"_.-LAYER _M EJE_PRUEBA\n\n","timeout_s":20}` (termina en **dos saltos de línea**) y
  `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","timeout_s":20}` (**dos espacios** al final);
  los dos deben responder `terminado`. Si no, dibújala a mano: en la línea de comandos de Civil 3D escribe `_-LAYER`,
  Intro, `_M`, Intro, `EJE_PRUEBA`, Intro, Intro (crea la capa y la deja actual); luego `_.PLINE`, Intro, y los cuatro
  puntos **con `#` delante** (coordenadas absolutas aunque esté activa la entrada dinámica): `#1000,2000` Intro,
  `#1300,2000` Intro, `#1300,2300` Intro, `#1600,2300` Intro, e Intro otra vez para terminar. El paso 1 crea el eje
  desde ella.
- **Archivo de puntos del terreno.** El `puntos_prueba.csv` de VALIDACION_17 **no** sirve: solo cubre x 1000..1020,
  y 2000..2020, y el eje va de x 1000 a 1600 e y 2000 a 2300. Crea en la PC `C:\Proyectos\dibujos_prueba\terreno_corredor.csv`
  con exactamente estas 10 líneas (9 puntos PENZD de un plano inclinado genérico que cubre x 900..1700, y 1900..2400;
  cota = 100 + 0,02·(y − 2000) + 0,005·(x − 1000)):

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

  Con la rasante del paso 1 (100 → 110 → 105) el corredor queda en terraplén en la primera mitad y en desmonte al final:
  hay taludes de los dos tipos.
- **Ensamblaje hecho a mano** (el plugin no crea ensamblajes). Se crea en el paso 1, con estos clics:
  1. En la cinta, pestaña **Inicio** → grupo **Crear diseño** → desplegable **Ensamblaje** → **Crear ensamblaje**
     (*Home → Create Design → Assembly → Create Assembly*). También vale escribir `_CreateAssembly` e Intro.
  2. En el cuadro **Crear ensamblaje**: **Nombre** `Ensamblaje 1`; deja el tipo y los estilos como vengan. **Aceptar**.
  3. La línea de comandos pide la ubicación de la línea base del ensamblaje: escribe `1000,1700` e Intro (queda fuera
     del terreno, abajo, y no estorba). Aparece una marca vertical: es el ensamblaje vacío.
  4. Abre las **Paletas de herramientas** con `Ctrl+3` (cinta **Inicio** → grupo **Paletas** → **Paletas de
     herramientas**; *Tool Palettes*). Busca la paleta **Subensamblajes métricos de Civil 3D** (*Civil Metric
     Subassemblies*) y su pestaña **Básico** (*Basic*). Si no la ves: haz clic derecho sobre la barra de título vertical
     de las paletas y elige en la lista el grupo de subensamblajes métricos; si la pestaña tiene otro nombre, recorre
     las pestañas pasando el ratón por encima de las herramientas: el nombre en inglés de cada una sale en la
     descripción emergente y es el mismo en todos los idiomas.
  5. Abre también la paleta **Propiedades** con `Ctrl+1`.
  6. Haz clic en **BasicLane** (carril básico). En Propiedades, sección **Parámetros avanzados** (*Advanced*), pon
     **Lado** (*Side*) = **Derecha** (*Right*) y haz clic sobre la marca vertical del ensamblaje (paso 3): se dibuja el
     carril derecho. Sin salir del comando, cambia **Lado** a **Izquierda** (*Left*) y vuelve a hacer clic sobre la
     misma marca: se dibuja el carril izquierdo. Pulsa `Esc`.
  7. Haz clic en **BasicSideSlopeCutDitch** (talud básico de desmonte con cuneta), **Lado** = **Derecha**, y haz clic en
     el **círculo pequeño del borde exterior** del carril derecho (el punto de arriba más alejado del centro). Cambia
     **Lado** a **Izquierda** y haz clic en el círculo del borde exterior del carril izquierdo. Pulsa `Esc`.
  8. Resultado: un ensamblaje en forma de "V" abierta con dos carriles y dos taludes. Los dos taludes tienen un
     objetivo de superficie (en inglés `TargetDTM`): es lo que `crear_corredor` rellena con el terreno.
  Si el ensamblaje ya existe con otro nombre, úsalo y sustituye `Ensamblaje 1` en todo el prompt (anótalo).
- El plugin **1.11.0** se instala en el paso 0 desde la rama `tarea/2.5-corredor`; el bundle queda con `ArbaMcp.dll` y
  `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.11.0 (`uv sync` antes) y el cliente MCP reiniciado para
  que cargue la lista de **50** herramientas del plugin (más `buscar_api` del puente).
- **Esta rama trae también la 1.10.1** (rama `tarea/1.10.1-correcciones`, fusionada): `ejecutar_comando` conserva los
  espacios finales, cada escritura real se ensaya antes con `simular=true` (si el ensayo falla, error sin entrada vacía
  en Deshacer ni copia), `ping` da la versión con tres números y los errores de estilo listan los nombres. Por eso
  `ping` debe responder **`1.11.0`** (no `1.11.0.0`). En la **misma instalación** se valida primero
  `herramientas-dev/VALIDACION_1101.md` y **después** este prompt; si VALIDACION_1101 falla en un paso decisivo, anótalo
  aquí también.
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.11.0**: la herramienta de escritura `crear_corredor`: crea un corredor con una línea base (eje y
  rasante) y una región con un ensamblaje que ya existe, entre dos progresivas (por defecto el tramo común del eje y la
  rasante) y con una frecuencia dada (la misma en tangentes, curvas, espirales y curvas del perfil); pone el terreno en
  todos los objetivos de superficie de la región (los taludes) y reconstruye el corredor, todo en **una** transacción y
  **una** entrada de Deshacer. Devuelve `estado_corredor` y, como cambia objetivos, el bloque `restaurar` para
  `deshacer_objetivos` (Civil 3D no deshace objetivos con Ctrl+Z). Es la quinta herramienta del camino
  `Escritura.CrearObjeto`. No crea superficies de corredor (Top, Datum: 1.12.0). **Los pasos 3, 5, 6, 7 y 9 son los
  decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.11.0):
  `CorridorCollection.Add(nombre, línea base, alignmentId, profileId, región, assemblyId)`, `BaselineRegion.StartStation/
  EndStation` sobre una región recién creada, `AppliedAssemblySetting.FrequencyAlong*` sobre esa región,
  `GetTargets`/`SetTargets` antes de la primera reconstrucción, `Corridor.Rebuild()` dentro de la transacción que crea
  el corredor y, si hiciera falta, `BaselineRegionCollection.Add(nombre, assemblyId, inicio, fin)`.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 5): **"crea el corredor Corredor 1
  con el Eje 1, la Rasante 1 y el Ensamblaje 1, cada 10 m, con los taludes al Terreno"**.

---

Eres el agente de validación de la versión 1.11.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell o clics en Civil 3D. Ejecuta los pasos en orden, anota la respuesta
**literal** (JSON completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo. En este prompt solo se usa
  para `_.ZOOM` y `_.UNDO`, que son del propio Civil 3D, y, si partes de un dibujo nuevo, para la capa y la polilínea
  de los requisitos.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Los parámetros de tipo `json` (`pvis`) se pasan como **texto** con la lista JSON dentro, tal como está escrito aquí.
- Si el dibujo ya tiene un corredor llamado `Corredor 1` o `Corredor 2`, usa `Corredor MCP 111` y `Corredor MCP 111 b`
  en su lugar en todos los pasos y anótalo.
- Los números esperados son en metros con redondeo a 4 decimales; una diferencia menor de 0,001 es OK.
- `crear_corredor` reconstruye el corredor: el puente le da hasta 300 s (si llamas por `curl`, pasa `"timeout_s":300`).
- Al final hay una lista de **preguntas para el informe** (qué rango deja `CorridorCollection.Add`, cuándo aparecen
  los objetivos, si `_.UNDO 1` quita el corredor y sus objetivos): respóndelas con lo que viste.

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `tarea/2.5-corredor`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`, `uv sync`. Reinicia el puente y el
   cliente MCP. Abre Civil 3D con la copia del DWG. `ping` con `args={"pedido":"comprueba que el plugin responde"}` →
   `version` debe ser exactamente `1.11.0` (con la 1.10.1 son tres números; `1.11.0.0` es FALLO). Pide la lista de
   herramientas: deben ser **50** del plugin e incluir `crear_corredor` con los parámetros
   `nombre`, `alineamiento`, `perfil`, `ensamblaje`, `frecuencia`, `inicio`, `fin`, `superficie_objetivo`,
   `linea_base`, `region`, `simular` y `pedido`. Si no, para aquí.

1. **Preparar el dibujo** (no decisivo; cada creación deja su entrada en Deshacer, es normal).
   - `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}`. Si **no** está `Eje 1`, créalo con
     `crear_alineamiento_desde_polilinea` y `args={"nombre":"Eje 1","capa":"EJE_PRUEBA","radio":80,"pedido":"crea el eje Eje 1 desde la polilínea de la capa EJE_PRUEBA con radio 80"}`
     (antes, la polilínea a mano de los requisitos). Anota `inicio` y `fin` de `Eje 1`: deben ser `0` y `831.3274`.
   - `listar_superficies`: si no hay ninguna llamada `Terreno`, `crear_superficie_tin` con
     `args={"nombre":"Terreno","archivo":"C:\\Proyectos\\dibujos_prueba\\terreno_corredor.csv","pedido":"crea la superficie Terreno con el archivo terreno_corredor.csv"}`.
     Esperado: `puntos: 9` y extensión `min_x: 900`, `max_x: 1700`, `min_y: 1900`, `max_y: 2400` (anota los
     triángulos: en una malla de 3 × 3 suelen ser 8). Comprueba el plano con `cota_superficie`
     `args={"superficie":"Terreno","x":1300,"y":2150}` → `104.5`. **Si `listar_superficies` muestra otras superficies
     además de `Terreno`, anótalas**: el paso 9 depende de que `Terreno` sea la única que no es de un corredor.
   - `listar_perfiles` con `args={"alineamiento":"Eje 1"}`. Si no está `Rasante 1`, créala con `crear_rasante` y
     `args={"alineamiento":"Eje 1","nombre":"Rasante 1","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":400,\"cota\":110,\"longitud_curva\":120},{\"progresiva\":831.33,\"cota\":105}]","pedido":"crea la rasante Rasante 1 del Eje 1"}`
     (la de VALIDACION_110: `numero_pvis: 3`, `numero_curvas: 1`, `progresiva_final: 831.3274`, tipo `FG`).
   - Crea `Ensamblaje 1` **a mano** con los clics de los requisitos y luego `listar_ensamblajes` con
     `args={"pedido":"lista los ensamblajes"}`: debe aparecer `Ensamblaje 1` con sus grupos y **cuatro**
     subensamblajes: dos `BasicLane` (lado `derecha` e `izquierda`) y dos `BasicSideSlopeCutDitch` (`derecha` e
     `izquierda`). **Anota los nombres literales** de los subensamblajes (pueden salir como `BasicLane - (Right)` o en
     español) y que `usado_en` está vacío.

2. **Punto de partida.** `listar_corredores` con `args={"pedido":"lista los corredores"}`: no debe estar `Corredor 1`
   (si está, aplica la regla del nombre alternativo). Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene
   (o la última). Mira cuántos archivos hay en `backups\` junto al DWG.

3. **Simulación (decisivo).** `crear_corredor` con
   `args={"nombre":"Corredor 1","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":10,"superficie_objetivo":"Terreno","simular":true,"pedido":"simula el corredor Corredor 1"}`.
   Esperado: `simulado: true`; `accion: "Crear el corredor 'Corredor 1' sobre el eje 'Eje 1' y la rasante 'Rasante 1'
   con el ensamblaje 'Ensamblaje 1', de 0 a 831.327 cada 10 m, con 'Terreno' como objetivo de los taludes, y
   reconstruirlo"`; `antes: {"existe": false, "numero_lineas_base": 0, "numero_regiones": 0}`; `despues` con
   `existe: true`, `numero_lineas_base: 1`, `linea_base: "Línea base 1"`, `alineamiento: "Eje 1"`,
   `perfil: "Rasante 1"`, `numero_regiones: 1`, `region: "Región 1"`, `ensamblaje: "Ensamblaje 1"`, `inicio: 0`,
   `fin: 831.3274`, `frecuencia_tangentes: 10`, `frecuencia_curvas: 10`, `frecuencia_espirales: 10`,
   `frecuencia_perfil: 10`, `superficie_objetivo: "Terreno"` y `esta_desactualizado: false` (`nombre` y
   `objetivos_superficie` no salen en `despues` porque no cambian respecto a `antes`: el número de objetivos no se sabe
   hasta crear la región); `avisos` con `Cuántos objetivos de superficie tiene el ensamblaje (uno por talud) se sabe al
   crear la región: la simulación no los puede contar ('objetivos_superficie' = null).` OK solo si `fin` es
   **831.3274**, las cuatro frecuencias son **10**, no hay `copia`, el menú Deshacer **no** cambió y no apareció ningún
   archivo nuevo en `backups\`.

4. **Errores controlados sin tocar nada.** Todos con `"simular":true`. Cada uno debe responder `ok: false` con el texto
   indicado, sin entrada nueva en Deshacer y sin archivo nuevo en `backups\`:
   - `args={"nombre":"C","alineamiento":"NoExiste","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":10,"simular":true}`
     → `No existe el alineamiento 'NoExiste'. Usa listar_alineamientos.`
   - `args={"nombre":"C","alineamiento":"Eje 1","perfil":"NoExiste","ensamblaje":"Ensamblaje 1","frecuencia":10,"simular":true}`
     → `El alineamiento 'Eje 1' no tiene un perfil llamado 'NoExiste'. Usa listar_perfiles.`
   - `args={"nombre":"C","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"NoExiste","frecuencia":10,"simular":true}`
     → `No existe el ensamblaje 'NoExiste'. Ensamblajes del dibujo: Ensamblaje 1. Usa listar_ensamblajes para ver sus
     subensamblajes. Si no hay ninguno, créalo en Civil 3D (Inicio → Ensamblaje → Crear ensamblaje ...)` (anota la
     lista literal).
   - `args={"nombre":"C","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":10,"superficie_objetivo":"NoExiste","simular":true}`
     → `No existe la superficie 'NoExiste'. Usa listar_superficies.`
   - **Fin fuera del tramo común**: `args={"nombre":"C","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":10,"fin":900,"simular":true}`
     → `El 'fin' (900) queda fuera del tramo común del eje 'Eje 1' (0 a 831.327) y la rasante 'Rasante 1' (0 a
     831.327): debe estar entre 0 y 831.327. Usa listar_alineamientos y listar_perfiles para ver los rangos.`
   - **Inicio mayor que el fin**: `args={"nombre":"C","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":10,"inicio":500,"fin":400,"simular":true}`
     → `El 'fin' (400) debe ser mayor que el 'inicio' (500).`
   - **Frecuencia 0**: `args={"nombre":"C","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":0,"simular":true}`
     → `La 'frecuencia' debe ser un número mayor que 0 (metros entre secciones del corredor); llegó 0.`
   - **Sin frecuencia**: `args={"nombre":"C","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","simular":true}`
     → `Falta el parámetro obligatorio 'frecuencia' (metros entre secciones del corredor, por ejemplo 10).`

5. **Creación real (decisivo).** `crear_corredor` **sin** `simular` (con la 1.10.1 el plugin la ensaya antes con
   `simular=true`; si el ensayo pasa, la escritura sigue igual y deja **una** entrada en Deshacer):
   `args={"nombre":"Corredor 1","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":10,"superficie_objetivo":"Terreno","pedido":"crea el corredor Corredor 1 con el Eje 1, la Rasante 1 y el Ensamblaje 1, cada 10 m, con los taludes al Terreno"}`.
   Esperado: `simulado: false`, `mensaje` igual a la `accion` del paso 3, `cambios` que incluye `existe`,
   `numero_lineas_base`, `numero_regiones`, `inicio`, `fin`, las frecuencias y `objetivos_superficie`; `antes` como en
   el paso 3; `despues` **leído del dibujo** con lo mismo que en el paso 3 más `objetivos_superficie: 2` (uno por
   talud) y `esta_desactualizado: false`; `copia` con `ruta` dentro de `backups\`, `metodo: "copia_de_disco"` (o
   `SaveAs` si el DWG no estaba guardado) y `estado: "terminada"` (si `copia.reutilizada` es `true`, no hay archivo
   nuevo: anótalo, no es fallo); `restaurar` con `herramienta: "deshacer_objetivos"`, `args: {"id": <n>}` (**anota el
   número**: se usa en el paso 8) y `antes` con **dos** elementos (los dos taludes: `tipo: "superficie"`,
   `objetivos: []`, porque el corredor es nuevo); `datos` con `linea_base: "Línea base 1"`, `region: "Región 1"`,
   `tipo_perfil: "FG"`, `subensamblajes` (los cuatro del paso 1), `rango` (`inicio: 0`, `fin: 831.3274`,
   `inicio_comun: 0`, `fin_comun: 831.3274`), `frecuencias` (las cuatro en 10), `objetivos` (todos los de la región,
   como `listar_objetivos`: los dos de superficie con `Terreno`; si los carriles traen objetivos de desplazamiento o
   elevación, salen vacíos: anota cuántos hay de cada tipo), **`camino_objetivos`** (anótalo: `antes_de_reconstruir`
   o `despues_de_reconstruir`), **`region_creada_por_add`** (anota `inicio` y `fin`: el rango que dejó
   `CorridorCollection.Add` antes del ajuste), `region_anadida_aparte: false`, `reconstrucciones` (1, o 2 si el camino
   fue `despues_de_reconstruir`), `ms_reconstruccion`, `capa` y **`estado_corredor`** con `nombre: "Corredor 1"`,
   `esta_desactualizado: false`, `ultima_reconstruccion` con la hora de ahora, `n_lineas_base: 1` y `superficies: []`;
   e `id_log`. `avisos`: ninguno, o el de `despues_de_reconstruir` (anótalo literal). En Civil 3D haz
   `ejecutar_comando` con `args={"comando":"_.ZOOM _W 900,1900 1700,2400"}`: debe verse el corredor a lo largo del eje,
   con los dos carriles y los taludes llegando al terreno a cada lado (en terraplén al principio y en desmonte al final),
   **sin** cuadros de diálogo abiertos. OK solo si `objetivos_superficie` es **2**, `inicio`/`fin` son `0`/`831.3274`,
   las cuatro frecuencias son 10 y `esta_desactualizado` es `false`. Hallazgos posibles, anótalos literales y **para**
   en los dos primeros:
   - error `La API no devolvió el objeto creado` o una excepción de `CorridorCollection.Add`: no se pudo crear.
   - error que empieza por `No se pudo reconstruir el corredor 'Corredor 1' (Corridor.Rebuild: ...)`: anota el
     mensaje completo y comprueba con `listar_corredores` que `Corredor 1` **no** quedó (la transacción se descartó):
     si quedó, es el hallazgo más grave.
   - error `El corredor 'Corredor 1' se creó pero la región empieza en ...` o `... pero el objetivo de ... tiene
     ninguno y se pidió 'Terreno'`: el corredor existe pero no refleja lo pedido; anota `despues` y **no** deshagas
     todavía (el paso 10 lo hace).
   - avisos `La línea base quedó con el nombre ...`, `La región quedó con el nombre ...`, `El corredor sigue
     desactualizado después de reconstruir ...` o `El ensamblaje 'Ensamblaje 1' no tiene objetivos de superficie ...`:
     anótalos literales.

6. **Lectura cruzada (decisivo).**
   - `listar_corredores`: `Corredor 1` con `esta_desactualizado: false` y una línea base `Línea base 1` con
     `alineamiento: "Eje 1"`, `perfil: "Rasante 1"` y `n_regiones: 1` (anota su `inicio` y `fin`).
   - `listar_regiones` con `args={"corredor":"Corredor 1"}`: una región, `indice: 0`, `nombre: "Región 1"`,
     `inicio: 0`, `fin: 831.3274`, `ensamblaje: "Ensamblaje 1"` y las cuatro frecuencias en `10`.
   - `listar_objetivos` con `args={"corredor":"Corredor 1","linea_base":"Línea base 1","region":"0"}`: los dos
     `BasicSideSlopeCutDitch` con `tipo: "superficie"` y `objetivos: [{"tipo": "superficie", "nombre": "Terreno", ...}]`.
   - `estado_corredor` con `args={"corredor":"Corredor 1"}`: igual que `datos.estado_corredor` del paso 5.
   OK si las cuatro lecturas coinciden con el paso 5.

7. **Una sola entrada de Deshacer y una copia (decisivo).** Abre el menú Deshacer: respecto al paso 2 debe haber **una**
   entrada nueva (`Executefunction`), la del paso 5; los pasos 3, 4 y 6 no añaden ninguna. En `backups\` debe haber
   **un** archivo nuevo `<nombre del dwg>_<fecha>_crear_corredor.dwg` (el `copia.ruta` del paso 5; si
   `copia.reutilizada` es `true`, no hay archivo nuevo: anótalo, no es fallo). En `mcp_log.jsonl` deben estar las líneas
   `llamada` de los pasos 3 (`"simular":true`, `ok:true`), 4 (`ok:false`), 5 (con el `pedido` literal y `ok:true`) y 6.
   **No deshagas todavía.**

8. **Restauración de objetivos en simulación.** `deshacer_objetivos` con `args={"id":<el restaurar.args.id del paso 5>,"simular":true,"pedido":"simula deshacer los objetivos del Corredor 1"}`.
   Esperado: `simulado: true` y un `plan` con **dos** elementos, uno por talud, cada uno con
   `accion: "Devolver el objetivo de <subensamblaje> [superficie, ...] en la región 'Región 1' de 'Línea base 1' a ninguno"`,
   `antes` con `objetivos: ["superficie:Terreno"]` y `despues` con `objetivos: []`; `deshacer.pendientes` incluye la
   entrada con `herramienta: "crear_corredor"`. **Sin** entrada nueva en Deshacer, sin copia. No lo ejecutes de verdad.

9. **Segundo corredor con rango parcial, nombres propios y superficie por defecto (decisivo).** `crear_corredor` con
   `args={"nombre":"Corredor 2","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":20,"inicio":100,"fin":600,"linea_base":"BL Eje 1","region":"Tramo 1","pedido":"crea el corredor Corredor 2 del Eje 1 entre 100 y 600 cada 20 m"}`
   (sin `superficie_objetivo`). Esperado: `mensaje` con `de 100 a 600 cada 20 m, con 'Terreno' como objetivo de los
   taludes`; `despues` con `linea_base: "BL Eje 1"`, `region: "Tramo 1"`, `inicio: 100`, `fin: 600`, las cuatro
   frecuencias en `20`, `superficie_objetivo: "Terreno"` y `objetivos_superficie: 2`; `avisos` con `No se indicó
   'superficie_objetivo': se usa 'Terreno', la única superficie del dibujo que no es de un corredor.`; `restaurar` con
   otro `args.id`. `listar_regiones` con `args={"corredor":"Corredor 2"}`: una región `Tramo 1` de `100` a `600` con
   frecuencias `20`. OK solo si el rango es **100–600** y los nombres son los pedidos. En pantalla, el segundo corredor
   cubre solo ese tramo del eje.

10. **Deshacer: corredor y objetivos.** En Civil 3D, `_.UNDO 1` **una vez** (con `ejecutar_comando`
    `args={"comando":"_.UNDO 1"}` o Ctrl+Z). Luego `listar_corredores`:
    - Si **`Corredor 2` desapareció**: `_.UNDO 1` deshizo la creación entera. Haz otro `_.UNDO 1` y vuelve a
      `listar_corredores`: anota si desaparece también `Corredor 1`. Después `deshacer_objetivos` con
      `args={"simular":true}`: lo esperado es un error que empieza por `No existe el corredor` (la pila de objetivos
      sigue en memoria, pero el corredor ya no está; no es fallo).
    - Si **`Corredor 2` sigue** en la lista: es un hallazgo importante (como la superficie TIN de la 1.7.0). Anota
      entonces `listar_regiones` y `listar_objetivos` de `Corredor 2` (`linea_base: "BL Eje 1"`, `region: "0"`): ¿siguen
      el rango 100–600, las frecuencias 20 y el objetivo `Terreno`? Haz otro `_.UNDO 1` y anota lo mismo de
      `Corredor 1`.
    Anota también si en pantalla los corredores dejan de verse. Este paso responde la pregunta principal del informe.

11. **Error por nombre repetido.** Si después del paso 10 ya no existe `Corredor 1`, créalo otra vez con el `args` del
    paso 5 (anota la respuesta). Luego repite exactamente el `crear_corredor` del paso 5 (sin `simular`). Esperado:
    `ok: false` y `error` que contiene `Ya existe el corredor 'Corredor 1'. Usa listar_corredores o pasa otro 'nombre'.`,
    con `id_log`. OK solo si `listar_corredores` sigue mostrando un solo `Corredor 1`, **no** hay entrada nueva en el
    menú Deshacer y **no** aparece ningún archivo nuevo en `backups\`: con la 1.10.1 la escritura se ensaya antes con
    `simular=true`, el ensayo da el error y el contexto de comando no se abre. `leer_historial` con
    `args={"ultimas_n":15}` debe mostrar `MCP ✗ crear_corredor no se ejecutó: falló la comprobación previa
    (simulación)...` y **ninguna** línea `Comando inicia: EXECUTEFUNCTION` desde la llamada; en `mcp_log.jsonl`, una
    sola línea de esta llamada con `ok:false`. Si aparece la entrada vacía o la copia, es FALLO de la corrección de la
    1.10.1 (anótalo también para VALIDACION_1101).

12. **Superficie por defecto con varias superficies.** Crea una segunda superficie genérica con `crear_superficie_tin` y
    `args={"nombre":"Terreno 2","archivo":"C:\\Proyectos\\dibujos_prueba\\terreno_corredor.csv"}`. Luego:
    - `crear_corredor` con `args={"nombre":"Corredor 3","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":10,"simular":true}`
      → `ok: false` con `Indica 'superficie_objetivo': el dibujo tiene 2 superficies (Terreno, Terreno 2) y no se sabe
      cuál es el terreno. Usa listar_superficies, o pasa "ninguna" para crear el corredor sin objetivos.` (el orden de
      la lista puede variar).
    - El mismo con `"superficie_objetivo":"ninguna"` (y `"simular":true`) → `ok: true`, `accion` que termina en
      `sin objetivos de superficie, y reconstruirlo`, `despues` **sin** `superficie_objetivo` (queda vacía, igual que en
      `antes`, así que no sale) y un aviso que empieza por
      `Se pidió 'superficie_objetivo': "ninguna": el corredor se crea sin objetivos de superficie`.
    Sin entrada nueva en Deshacer por estas dos simulaciones.

13. **Cierre.** `leer_log` con `args={"ultimas_n":40}`: una línea `llamada` por cada paso, con `ok:false` en los errores
    controlados (pasos 4, 11 y el primero del 12) y `ok:true` en los demás. No guardes el dibujo (es una copia).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_111_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, y las respuestas a estas **preguntas para la memoria técnica**:

1. ¿Qué rango dejó `CorridorCollection.Add` en la región antes del ajuste (`datos.region_creada_por_add` de los pasos 5
   y 9)? ¿El de la línea base entera, el de la rasante u otro? ¿Hizo falta `BaselineRegionCollection.Add`
   (`region_anadida_aparte`)?
2. ¿Cuándo aparecieron los objetivos de superficie (`datos.camino_objetivos`: `antes_de_reconstruir` o
   `despues_de_reconstruir`) y cuántas reconstrucciones hubo? ¿Cuántos objetivos tenía la región de cada tipo
   (superficie, desplazamiento, elevación) y con qué nombres de subensamblaje y parámetro (`LogicalName`)?
3. ¿`Corridor.Rebuild()` funcionó dentro de la transacción que crea el corredor (paso 5 sin error de reconstrucción) y
   cuánto tardó (`ms_reconstruccion`)?
4. ¿`_.UNDO 1` quitó el corredor del dibujo (paso 10)? Si no lo quitó, ¿qué quedó: rango, frecuencias, objetivos?
5. Los nombres literales de los subensamblajes del ensamblaje (paso 1) y la capa del corredor (`datos.capa`).
6. ¿Se vio el corredor en pantalla con los taludes llegando al terreno a los dos lados (paso 5)?

Y la lista de miembros de la sección 1.11.0 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
