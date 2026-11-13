# Prompt de validación de la 1.12.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Orden con la 1.13.0.** Este prompt va **antes** que `herramientas-dev/VALIDACION_113.md` en la **misma
  instalación**: la 1.13.0 (secciones) parte del dibujo que deja guardado el paso 11 de este prompt, con `Corredor 1` y
  sus superficies Top y Datum. No empieces VALIDACION_113 sin haber terminado el paso 11.
- Civil 3D 2027 (en español) abierto con **una copia** del dibujo de la validación 1.11.0, **guardada en disco**: copia
  `C:\Proyectos\dibujos_prueba\PRUEBA_111.dwg` como `C:\Proyectos\dibujos_prueba\PRUEBA_112.dwg` y trabaja sobre
  `PRUEBA_112.dwg`. Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`. Ningún dato real.
- **Lo que debe tener el dibujo** (lo comprueba el paso 1): la superficie `Terreno` (9 puntos del archivo
  `C:\Proyectos\dibujos_prueba\terreno_corredor.csv` de VALIDACION_111), el eje `Eje 1` (de `0` a `831.3274`), la
  rasante `Rasante 1`, el ensamblaje hecho a mano `Ensamblaje 1` (en la validación 1.11.0 se hizo con
  `LaneSuperelevationAOR` y `DaylightBench` a cada lado; también vale `BasicLane` + `BasicSideSlopeCutDitch`) y el
  corredor `Corredor 1` (de `0` a `831.3274`, cada 10 m, con los taludes al `Terreno`). **Ojo**: el informe 1.11.0 dice
  en su paso 13 que el dibujo **no se guardó**, así que es posible que `PRUEBA_111.dwg` no tenga todo. El paso 1 crea lo
  que falte con los mismos `args` de `VALIDACION_111.md` (requisitos y pasos 1 y 5); el ensamblaje, si falta, se hace a
  mano con los clics de los requisitos de VALIDACION_111.
- El plugin **1.12.0** se instala en el paso 0 desde la rama `tarea/2.5b-superficies-corredor`; el bundle queda con
  `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.12.0 (`uv sync` antes) y el cliente MCP
  reiniciado para que cargue la lista de **51** herramientas del plugin (más `buscar_api` del puente). Si la
  instalación es de una rama que junta esta con la 1.13.0, `ping` dará la versión de esa rama y habrá más herramientas:
  anótalo, no es fallo, siempre que estén `crear_superficies_corredor` y `estado_corredor`.
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.12.0**: la herramienta de escritura `crear_superficies_corredor`. En un corredor que ya existe crea
  sus dos superficies de corredor para el movimiento de tierras: `<corredor> - Top` (código de vínculo `Top`) y
  `<corredor> - Datum` (código `Datum`), las dos **como líneas de rotura**, con **corrección de voladizo** (la Top por
  los vínculos superiores, `TopLinks`; la Datum por los inferiores, `BottomLinks`) y un **contorno exterior** con la
  extensión del corredor (`Extensión del corredor`), y reconstruye el corredor, todo en **una** transacción y **una**
  entrada de Deshacer. Antes de tocar nada (también al simular) comprueba que los dos nombres están libres, que el
  corredor tiene los códigos `Top` y `Datum` y que el enumerado del voladizo de esta instalación tiene esos dos valores
  (si no, error con los valores reales: no adivina). Además, `estado_corredor` dice desde esta versión, por cada
  superficie, `codigos_enlace_rotura`, `correccion_voladizo` y `construir`. **Los pasos 3, 5, 6, 7 y 8 son los
  decisivos**; el paso 11 deja el dibujo listo para VALIDACION_113.
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.12.0):
  `Corridor.GetLinkCodes()`, `CorridorSurfaceCollection.Add(nombre)`, `CorridorSurface.AddLinkCode(código, true)`,
  `LinkCodes()`, `IsLinkCodeAsBreakLine(código)`, `OverhangCorrection` y los valores `TopLinks`/`BottomLinks`,
  `IsBuild` (solo se lee), `SurfaceId`, `Boundaries.AddCorridorExtentsBoundary(nombre)`, `BoundaryNames()`,
  `CorridorSurfaceBoundary.BoundaryType`/`IsCorridorExtents`/`Name` y `Corridor.Rebuild()` con superficies nuevas.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 5): **"crea las superficies Top y
  Datum del Corredor 1 para calcular los volúmenes"**.

---

Eres el agente de validación de la versión 1.12.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell o clics en Civil 3D. Ejecuta los pasos en orden, anota la respuesta
**literal** (JSON completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo. En este prompt solo se usa
  para `_.ZOOM` y `_.UNDO`, que son del propio Civil 3D (y, si el paso 1 tiene que rehacer el eje en un dibujo sin
  polilínea, para la capa y la polilínea de los requisitos de VALIDACION_111).
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Si el dibujo ya tiene superficies llamadas `Corredor 1 - Top` o `Corredor 1 - Datum` antes del paso 3, añade en
  todos los pasos `"nombre_top":"Corredor 1 - Top MCP 112"` y `"nombre_datum":"Corredor 1 - Datum MCP 112"`, adapta
  los textos esperados a esos nombres y anótalo.
- Los números esperados son en metros con redondeo a 4 decimales; una diferencia menor de 0,001 es OK (en las cotas del
  paso 6, 0,01).
- `crear_superficies_corredor` reconstruye el corredor: el puente le da hasta 300 s (si llamas por `curl`, pasa
  `"timeout_s":300`).
- En los errores, el plugin antepone `Error de Civil 3D: ArgumentException: ` (o `InvalidOperationException: `) al
  texto: un `error` que **contiene** el texto indicado es OK.
- Al final hay una lista de **preguntas para el informe** (valores reales del enumerado del voladizo, `IsBuild`, si
  `_.UNDO 1` quita las superficies, si `guardar_dibujo` funciona): respóndelas con lo que viste.

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `tarea/2.5b-superficies-corredor`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`, `uv sync`. Reinicia el puente y el
   cliente MCP. Abre Civil 3D con `PRUEBA_112.dwg`. `ping` con `args={"pedido":"comprueba que el plugin responde"}` →
   `version` debe ser exactamente `1.12.0` (ver requisitos si la rama junta 1.12.0 y 1.13.0). Pide la lista de
   herramientas: deben ser **51** del plugin e incluir `crear_superficies_corredor` con los parámetros `corredor`,
   `nombre_top`, `nombre_datum`, `simular` y `pedido`. Si no, para aquí.

1. **Preparar el dibujo** (no decisivo; cada creación deja su entrada en Deshacer, es normal).
   - `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}`: `Eje 1` de `0` a `831.3274`. Si falta,
     créalo como en el paso 1 de VALIDACION_111 (polilínea de la capa `EJE_PRUEBA`, radio 80).
   - `listar_superficies` con `args={"pedido":"lista las superficies"}`: debe estar `Terreno`. Si falta, créalo como en
     el paso 1 de VALIDACION_111 (`crear_superficie_tin` con `terreno_corredor.csv`). **Anota todas las superficies** que
     salgan y su `tipo_superficie` (si hay superficies de corredor previas, anota de qué corredor son).
   - `listar_perfiles` con `args={"alineamiento":"Eje 1"}`: debe estar `Rasante 1` (tipo `FG`). Si falta, créala con el
     `crear_rasante` del paso 1 de VALIDACION_111.
   - `listar_ensamblajes` con `args={"pedido":"lista los ensamblajes"}`: debe estar `Ensamblaje 1` con **cuatro**
     subensamblajes (dos carriles y dos taludes). Si falta, créalo a mano con los clics de los requisitos de
     VALIDACION_111 (anota qué subensamblajes usaste).
   - `listar_corredores` con `args={"pedido":"lista los corredores"}`: debe estar `Corredor 1`. Si falta, créalo con el
     `crear_corredor` del paso 5 de VALIDACION_111:
     `args={"nombre":"Corredor 1","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":10,"superficie_objetivo":"Terreno","pedido":"crea el corredor Corredor 1 con el Eje 1, la Rasante 1 y el Ensamblaje 1, cada 10 m, con los taludes al Terreno"}`
     (debe responder `objetivos_superficie: 2` y `esta_desactualizado: false`). Anota si había otros corredores
     (`Corredor 2`...).

2. **Punto de partida.** `estado_corredor` con `args={"corredor":"Corredor 1","pedido":"estado del Corredor 1"}`:
   `esta_desactualizado: false`, `n_lineas_base: 1` y `superficies: []` (si no está vacía, anota las superficies y
   aplica la regla de los nombres alternativos). Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene (o la
   última). Mira cuántos archivos hay en `backups\` junto al DWG.

3. **Simulación (decisivo).** `crear_superficies_corredor` con
   `args={"corredor":"Corredor 1","simular":true,"pedido":"simula las superficies Top y Datum del Corredor 1"}`.
   Esperado: `simulado: true`;
   `accion: "Crear en el corredor 'Corredor 1' las superficies 'Corredor 1 - Top' (código de vínculo Top, voladizo por los vínculos superiores) y 'Corredor 1 - Datum' (código de vínculo Datum, voladizo por los vínculos inferiores), como líneas de rotura y con el contorno de la extensión del corredor, y reconstruirlo"`;
   `cambios: ["superficies", "top_existe", "top_codigos_enlace", "top_codigos_rotura", "top_voladizo", "top_contornos", "datum_existe", "datum_codigos_enlace", "datum_codigos_rotura", "datum_voladizo", "datum_contornos"]`;
   `antes` con `superficies: []`, `top_existe: false`, `datum_existe: false` y los demás campos en `null`; `despues` con
   `superficies: ["Corredor 1 - Datum", "Corredor 1 - Top"]`, `top_existe: true`, `top_codigos_enlace: ["Top"]`,
   `top_codigos_rotura: ["Top"]`, `top_voladizo: "TopLinks"`, `top_contornos: ["Extensión del corredor"]`,
   `datum_existe: true`, `datum_codigos_enlace: ["Datum"]`, `datum_codigos_rotura: ["Datum"]`,
   `datum_voladizo: "BottomLinks"` y `datum_contornos: ["Extensión del corredor"]` (`esta_desactualizado` no sale porque
   no cambia); `avisos` con `Si cada superficie de corredor tiene ya su superficie en el dibujo (la que ve
   listar_superficies y usará calcular_volumenes) se sabe al reconstruir: la simulación no lo puede comprobar.` OK solo si
   `top_voladizo` es **TopLinks**, `datum_voladizo` es **BottomLinks**, no hay `copia`, el menú Deshacer **no** cambió y
   no apareció ningún archivo nuevo en `backups\`. Hallazgos posibles, anótalos literales y **para** en los dos primeros:
   - error `El enumerado OverhangCorrectionType de esta instalación de Civil 3D no tiene el valor 'TopLinks' (...).
     Valores: ...` (o `'BottomLinks'`): los nombres reales del voladizo son otros. **Copia la lista de valores entera**:
     con ella se corrige la herramienta. Es FALLO del paso decisivo.
   - error `El corredor 'Corredor 1' no tiene el código de vínculo 'Datum' ... Códigos de vínculo del corredor: ...`:
     copia la lista de códigos y los subensamblajes del paso 1.
   - error `El corredor 'Corredor 1' no devuelve ningún código de vínculo (Corridor.GetLinkCodes) ...`: haz
     `reconstruir_corredor` con `args={"corredor":"Corredor 1"}` y repite este paso **una** vez; anota los dos
     resultados.

4. **Errores controlados sin tocar nada.** Todos con `"simular":true`. Cada uno debe responder `ok: false` con el texto
   indicado, sin entrada nueva en Deshacer y sin archivo nuevo en `backups\`:
   - `args={"corredor":"NoExiste","simular":true}` → `No existe el corredor 'NoExiste'. Corredores del dibujo:
     Corredor 1.` (anota la lista literal).
   - **Nombres iguales**: `args={"corredor":"Corredor 1","nombre_top":"Superficie C1","nombre_datum":"Superficie C1","simular":true}`
     → `'nombre_top' y 'nombre_datum' no pueden ser iguales ('Superficie C1'): pasa dos nombres distintos.`
   - **Nombre de la Top ya usado** por el terreno: `args={"corredor":"Corredor 1","nombre_top":"Terreno","simular":true}`
     → `Ya existe la superficie 'Terreno'. Usa listar_superficies o estado_corredor, o pasa otro 'nombre_top'.`
   - **Nombre de la Datum ya usado**: `args={"corredor":"Corredor 1","nombre_datum":"Terreno","simular":true}`
     → `Ya existe la superficie 'Terreno'. Usa listar_superficies o estado_corredor, o pasa otro 'nombre_datum'.`
   - **Sin corredor**: `args={"simular":true}` → `Falta el parámetro obligatorio 'corredor'.` del plugin o, si el
     puente lo rechaza antes, un error de validación `args.corredor Field required` (anota cuál; los dos son OK).

5. **Creación real (decisivo).** `crear_superficies_corredor` **sin** `simular` (con la 1.10.1 el plugin la ensaya antes
   con `simular=true`; si el ensayo pasa, la escritura sigue igual y deja **una** entrada en Deshacer):
   `args={"corredor":"Corredor 1","pedido":"crea las superficies Top y Datum del Corredor 1 para calcular los volúmenes"}`.
   Esperado: `simulado: false`; `mensaje` igual a la `accion` del paso 3; `cambios`, `antes` y `despues` como en el
   paso 3, pero `despues` **leído del dibujo**; `copia` con `ruta` dentro de `backups\`, `metodo: "copia_de_disco"` (o
   `SaveAs` si el DWG no estaba guardado) y `estado: "terminada"` (si `copia.reutilizada` es `true`, no hay archivo
   nuevo: anótalo, no es fallo); `datos` con `corredor: "Corredor 1"`, `superficie_top: "Corredor 1 - Top"`,
   `superficie_datum: "Corredor 1 - Datum"`, `codigo_top: "Top"`, `codigo_datum: "Datum"`, **`codigos_corredor`** (anota
   la lista literal), `voladizo_top: "TopLinks"`, `voladizo_datum: "BottomLinks"`, **`valores_voladizo`** (anota la
   lista literal: son los nombres del enumerado en esta instalación), `contorno: "Extensión del corredor"`, `top` y
   `datum` con `nombre`, **`construir`** (anótalo), `tipo_contorno: "OutsideBoundary"`, **`contorno_es_extension`**
   (anótalo; lo esperable es `true`) y **`superficie_dibujo`** (anótalo; lo esperable es el mismo nombre que la
   superficie), `reconstrucciones: 1`, **`ms_reconstruccion`** y `estado_corredor` con `nombre: "Corredor 1"`,
   `esta_desactualizado: false`, `ultima_reconstruccion` con la hora de ahora, `n_lineas_base: 1` y **dos**
   `superficies` (las del paso 6); e `id_log`. `avisos`: ninguno; si sale alguno (`IsBuild = false`, `SurfaceId vacío
   después de reconstruir`, contorno que no quedó `OutsideBoundary`, `estaba desactualizado`), anótalo literal. En
   Civil 3D haz `ejecutar_comando` con `args={"comando":"_.ZOOM _W 900,1900 1700,2400"}` y anota si se ven las
   superficies del corredor (depende de su estilo; no es decisivo), **sin** cuadros de diálogo abiertos. OK solo si
   `despues` coincide con el del paso 3 y `estado_corredor.esta_desactualizado` es `false`. Hallazgos posibles, anótalos
   literales y **para** en los dos primeros:
   - error `No se pudo añadir a 'Corredor 1 - Top' el contorno de la extensión del corredor (...)` o `No se pudo
     reconstruir el corredor 'Corredor 1' (Corridor.Rebuild: ...)`: anota el mensaje completo y comprueba con
     `estado_corredor` que **no** quedó ninguna superficie (la transacción se descartó): si quedó alguna, es el hallazgo
     más grave.
   - una excepción de `CorridorSurfaces.Add`, `AddLinkCode` u `OverhangCorrection` (por ejemplo `MissingMethodException`
     o `eInvalidInput`): anótala literal y comprueba lo mismo con `estado_corredor`.
   - error `El dibujo no refleja el cambio pedido en '<campo>': se pidió ... y después de escribir tiene ...`: las
     superficies existen pero no como se pidió; anota el campo, `despues` y **no** deshagas todavía (el paso 10 lo hace).

6. **Lectura cruzada (decisivo).**
   - `listar_superficies` con `args={"pedido":"lista las superficies"}`: además de las del paso 1, `Corredor 1 - Top` y
     `Corredor 1 - Datum` con `tipo_superficie: "Corridor"` y `corredor: "Corredor 1"` (anota `tipo` y
     `esta_desactualizada` de cada una). Si **no** aparecen, es FALLO: anótalo junto con `datos.top.superficie_dibujo`
     y `construir` del paso 5.
   - `estado_corredor` con `args={"corredor":"Corredor 1"}`: igual que `datos.estado_corredor` del paso 5. Por cada
     superficie: `codigos_enlace: ["Top"]` (o `["Datum"]`), `codigos_enlace_rotura` igual, `correccion_voladizo:
     "TopLinks"` (o `"BottomLinks"`), `contornos: [{"nombre": "Extensión del corredor", "tipo": "OutsideBoundary",
     "usar_como_exterior": ...}]` (anota `usar_como_exterior`, que en realidad lee `IsCorridorExtents`),
     `esta_desactualizada: false` y `construir` (anótalo); anota también `codigos_punto` y
     `codigos_lineas_caracteristicas`.
   - `cota_superficie` con `args={"superficie":"Corredor 1 - Top","x":1150,"y":2000}` (el eje en la progresiva 150) →
     **≈ 103.75** (la rasante en esa progresiva: 100 + 2,5 % · 150). Con `"superficie":"Corredor 1 - Datum"` en el mismo
     punto → una cota **menor** que la de la Top (anótala: la diferencia es el espesor del firme en el eje). Con
     `"superficie":"Terreno"` → `100.75` (allí el corredor va en terraplén). Si la Top da otra cifra, anótala (no es
     fallo de esta herramienta si el ensamblaje tiene peralte o el carril no arranca en la rasante).
   - `extension_superficie` con `args={"superficie":"Corredor 1 - Top"}` y con `"Corredor 1 - Datum"`: anota `min_x`,
     `max_x`, `min_y`, `max_y` y `n_puntos` (deben ser mayores que 0 y quedar dentro de x 900..1700, y 1900..2400).
   OK si las superficies aparecen en `listar_superficies`, `estado_corredor` coincide con el paso 5 y las dos tienen
   cota en (1150, 2000) y `n_puntos` mayor que 0.

7. **Una sola entrada de Deshacer y una copia (decisivo).** Abre el menú Deshacer: respecto al paso 2 debe haber **una**
   entrada nueva (`Executefunction`), la del paso 5; los pasos 3, 4 y 6 no añaden ninguna. En `backups\` debe haber
   **un** archivo nuevo `<nombre del dwg>_<fecha>_crear_superficies_corredor.dwg` (el `copia.ruta` del paso 5; si
   `copia.reutilizada` es `true`, no hay archivo nuevo: anótalo, no es fallo). En `mcp_log.jsonl` deben estar las líneas
   `llamada` de los pasos 3 (`"simular":true`, `ok:true`), 4 (`ok:false`), 5 (con el `pedido` literal y `ok:true`) y 6.
   **No deshagas todavía.**

8. **Error por nombre repetido sin entrada en Deshacer (decisivo).** Repite exactamente el `crear_superficies_corredor`
   del paso 5 (sin `simular`). Esperado: `ok: false` y `error` que contiene `Ya existe la superficie 'Corredor 1 - Top'
   (la genera el corredor 'Corredor 1'). Usa listar_superficies o estado_corredor, o pasa otro 'nombre_top'.`, con
   `id_log`. OK solo si `estado_corredor` sigue mostrando **dos** superficies, **no** hay entrada nueva en el menú
   Deshacer y **no** aparece ningún archivo nuevo en `backups\`: la escritura se ensaya antes con `simular=true`, el
   ensayo da el error y el contexto de comando no se abre. `leer_historial` con `args={"ultimas_n":15}` debe mostrar
   `MCP ✗ crear_superficies_corredor no se ejecutó: falló la comprobación previa (simulación)...` y **ninguna** línea
   `Comando inicia: EXECUTEFUNCTION` desde la llamada; en `mcp_log.jsonl`, una sola línea de esta llamada con
   `ok:false`.

9. **Nombres propios en simulación.** `crear_superficies_corredor` con
   `args={"corredor":"Corredor 1","nombre_top":"Rasante C1","nombre_datum":"Subrasante C1","simular":true,"pedido":"simula las superficies con nombres propios"}`.
   Esperado: `ok: true`, `simulado: true`, `accion` con `las superficies 'Rasante C1' (...) y 'Subrasante C1' (...)`,
   `antes` con `superficies: ["Corredor 1 - Datum", "Corredor 1 - Top"]` y `despues` con
   `superficies: ["Corredor 1 - Datum", "Corredor 1 - Top", "Rasante C1", "Subrasante C1"]`, `top_voladizo: "TopLinks"`
   y `datum_voladizo: "BottomLinks"`. Sin entrada nueva en Deshacer ni copia. No la ejecutes de verdad.

10. **Deshacer: superficies del corredor.** En Civil 3D, `_.UNDO 1` **una sola vez** (con `ejecutar_comando`
    `args={"comando":"_.UNDO 1"}` o Ctrl+Z). La última entrada debe ser la del paso 5 (los pasos 6 a 9 no dejan
    ninguna). Luego `estado_corredor` con `args={"corredor":"Corredor 1"}` y `listar_superficies`:
    - Si **las dos superficies desaparecieron** de las dos lecturas: `_.UNDO 1` deshizo la creación entera. Anota si
      `Corredor 1` sigue existiendo y su `esta_desactualizado`.
    - Si **siguen** (en las dos lecturas o solo en una): es un hallazgo importante (como la superficie TIN de la 1.7.0).
      Anota qué quedó de cada una: `codigos_enlace`, `correccion_voladizo`, `contornos` y si sale en `listar_superficies`.
    **No** hagas un segundo `_.UNDO 1` (podría deshacer `Corredor 1`, que el paso 11 necesita). Anota también si en
    pantalla las superficies dejan de verse. Este paso responde una de las preguntas principales del informe.

11. **Dejar el dibujo listo para VALIDACION_113** (no decisivo para la 1.12.0, pero **obligatorio**: sin él no se puede
    empezar la 1.13.0).
    - Si en el paso 10 las superficies **desaparecieron**, vuelve a crearlas con `crear_superficies_corredor` y
      `args={"corredor":"Corredor 1","pedido":"vuelve a crear las superficies Top y Datum del Corredor 1"}`: debe
      responder como el paso 5 (`ok: true`, `despues` igual). Si en el paso 10 **siguieron**, no las crees otra vez
      (daría el error del paso 8) y anótalo.
    - `estado_corredor` con `args={"corredor":"Corredor 1"}`: dos superficies, `Corredor 1 - Top` y
      `Corredor 1 - Datum`, con su voladizo y su contorno, y `esta_desactualizado: false`.
    - Guarda el dibujo con `guardar_dibujo` y `args={"pedido":"guarda el dibujo para la validación de secciones"}`
      (primera vez que se ejecuta esta herramienta en una validación). Esperado: `simulado: false`,
      `mensaje: "Dibujo guardado en <ruta de PRUEBA_112.dwg>"`, `despues` con `ruta` = esa ruta y `existe: true`, y en
      el Explorador de Windows la fecha de `PRUEBA_112.dwg` cambia a la hora de ahora. Si `guardar_dibujo` da error, anota
      el error literal y **pide a Jose que guarde con Ctrl+S** en Civil 3D; comprueba después la fecha del archivo.
    - Anota en el informe que `PRUEBA_112.dwg` queda guardado con `Corredor 1` y sus dos superficies: es el punto de
      partida de VALIDACION_113.

12. **Cierre.** `leer_log` con `args={"ultimas_n":40}`: una línea `llamada` por cada paso, con `ok:false` en los errores
    controlados (pasos 4 y 8) y `ok:true` en los demás (incluido `guardar_dibujo` del paso 11, si funcionó).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_112_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, y las respuestas a estas **preguntas para la memoria técnica**:

1. ¿Qué valores tiene el enumerado del voladizo en esta instalación (`datos.valores_voladizo` del paso 5)? ¿Existían
   `TopLinks` y `BottomLinks` y quedaron así en `estado_corredor` (`correccion_voladizo`)?
2. ¿Qué códigos de vínculo devolvió el corredor (`datos.codigos_corredor`) con los subensamblajes del paso 1?
3. ¿Qué valor trae `IsBuild` por defecto en una superficie recién creada (`datos.top.construir`,
   `datos.datum.construir`, `construir` de `estado_corredor`) y **aparecen las dos superficies en
   `listar_superficies`** (paso 6)?
4. ¿Funcionó `AddCorridorExtentsBoundary` antes de la reconstrucción? ¿Qué `tipo_contorno` y `contorno_es_extension`
   quedaron, y hubo algún aviso?
5. ¿Cada superficie de corredor tenía su superficie del dibujo después de reconstruir (`datos.top.superficie_dibujo`)?
   ¿Qué cotas dieron la Top y la Datum en (1150, 2000) y cuántos puntos tienen (`extension_superficie`)? ¿Cuánto tardó
   la reconstrucción (`ms_reconstruccion`)?
6. ¿`_.UNDO 1` quitó las dos superficies (paso 10)? Si no, ¿qué quedó?
7. ¿Funcionó `guardar_dibujo` (paso 11) o hubo que guardar con Ctrl+S? ¿Quedó `PRUEBA_112.dwg` guardado con las dos
   superficies?
8. ¿Se vieron las superficies en pantalla tras el `_.ZOOM` del paso 5?

Y la lista de miembros de la sección 1.12.0 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
