# Prompt de validación de la 1.13.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Se ejecuta después de `VALIDACION_112.md`**, en la misma instalación. Civil 3D 2027 (en español) abierto con **una
  copia** del dibujo que deja guardado esa validación, `C:\Proyectos\dibujos_prueba\PRUEBA_112.dwg` (por ejemplo
  `C:\Proyectos\dibujos_prueba\PRUEBA_113.dwg`; si trabajas directamente sobre `PRUEBA_112.dwg`, anótalo). Anota la
  carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`. Ningún dato real.
- **Lo que debe tener el dibujo** (lo dejaron las validaciones 1.10.0, 1.11.0 y 1.12.0; el paso 1 lo comprueba):
  - `Eje 1`: alineamiento de la progresiva `0` a la `831.3274` (polilínea `1000,2000 → 1300,2000 → 1300,2300 →
    1600,2300` con radio 80).
  - `Terreno`: superficie TIN de 9 puntos que cubre x 900..1700, y 1900..2400 (`terreno_corredor.csv` de
    `VALIDACION_111.md`). Puede estar también `Terreno 2` (la creó el paso 12 de la 1.11.0): no es fallo, anótalo.
  - `Rasante 1` (perfil FG de `Eje 1`), `Ensamblaje 1` y `Corredor 1` (de 0 a 831.3274 cada 10 m, taludes a `Terreno`).
  - Las superficies de corredor **`Corredor 1 - Top`** y **`Corredor 1 - Datum`** (las creó la 1.12.0).
  Si falta algo de esto, **para** y anótalo: esta validación no lo crea.
- El plugin **1.13.0** se instala en el paso 0 desde la rama `tarea/2.6-secciones` (que trae fusionada la 1.12.0, así
  que es la misma instalación en la que se pasó `VALIDACION_112.md`); el
  bundle queda con `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.13.0 (`uv sync`
  antes) y el cliente MCP reiniciado para que cargue la lista de **52** herramientas del plugin (más `buscar_api` del
  puente). El puente le da a `crear_secciones` hasta **300 s** (si llamas por `curl`, pasa `"timeout_s":300`).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.13.0**: la herramienta de escritura `crear_secciones` (pasos 9 y 10 del flujo vial). En **una**
  transacción y **una** entrada de Deshacer: crea un grupo de líneas de muestreo en el eje (`SampleLineGroup.Create`),
  marca como muestreadas las fuentes (por defecto todas las superficies, los corredores del eje y sus superficies:
  `GetSectionSources` + `IsSampled`), crea una línea cada `intervalo` m (y otra en el final del tramo) con los anchos
  izquierdo y derecho pedidos (`SampleLine.Create` con dos puntos) y una vista de sección por línea
  (`SectionView.Create`) en cuadrícula de `filas_por_columna` filas: columnas de izquierda a derecha y, en cada
  columna, la progresiva crece **de abajo arriba**; cada vista se coloca encima de la anterior según su tamaño real.
  Sin `punto_insercion`, la primera vista va a la derecha de lo dibujado (la regla de `crear_perfil_terreno_y_vista`).
  Es la sexta herramienta del camino `Escritura.CrearObjeto`. **Los pasos 3, 5, 6, 7, 9 y 10 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.13.0):
  `SampleLineGroup.Create`, `GetSectionSources`/`IsSampled` sobre un grupo recién creado, `SampleLine.Create(nombre,
  grupo, Point2dCollection)`, el signo del desplazamiento de `Alignment.PointLocation` (izquierda negativa),
  `SampleLine.Vertices`, `GetSectionIds`/`Section.SourceName`, `SectionView.Create`, `SectionView.StyleId`,
  `Entity.GeometricExtents` de una vista recién creada, `SectionViewGroups`/`IndividualSectionViewGroup` y
  `SettingsSectionView`.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 5): **"líneas de muestreo cada 20 m
  con 30 m a cada lado y las vistas de sección en cuadrícula de 5 filas, a la derecha del dibujo"**.

---

Eres el agente de validación de la versión 1.13.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell o clics en Civil 3D. Ejecuta los pasos en orden, anota la respuesta
**literal** (JSON completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo. En este prompt solo se usa
  para `_.ZOOM` y `_.UNDO`, que son del propio Civil 3D.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Los parámetros de tipo `json` (`fuentes`, `punto_insercion`) se pasan como **texto** con el JSON dentro, tal como
  está escrito aquí.
- Si el eje ya tiene un grupo de líneas de muestreo llamado `Eje 1 - Muestreo` o `Muestreo 100`, usa
  `Muestreo MCP 113` y `Muestreo MCP 113 b` (pasando `"nombre"`) en todos los pasos y anótalo.
- **`listar_lineas_muestreo` deja una entrada en el menú Deshacer** (corre en contexto de comando porque, con
  `fuentes=true`, abre el grupo para escritura; es así desde la 1.2.2, no es fallo). Por eso el recuento de Deshacer del
  paso 7 se hace **antes** de la lectura cruzada del paso 8, y entre el paso 9 y el `_.UNDO 1` del paso 10 no se llama
  a nada más.
- Los números esperados son en metros con redondeo a 4 decimales; una diferencia menor de 0,001 es OK. El orden de las
  listas de fuentes puede variar.
- Al final hay una lista de **preguntas para el informe**: respóndelas con lo que viste.

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `tarea/2.6-secciones`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`, `uv sync`. Reinicia el puente y el
   cliente MCP. Abre Civil 3D con la copia del DWG. `ping` con `args={"pedido":"comprueba que el plugin responde"}` →
   `version` debe ser exactamente `1.13.0`. Pide la lista de herramientas: deben ser **52** del plugin (las 50 de la
   1.11.0, `crear_superficies_corredor` de la 1.12.0 y `crear_secciones`) y `crear_secciones` debe tener los parámetros
   `alineamiento`, `intervalo`, `ancho_izquierdo`, `ancho_derecho`, `nombre`, `inicio`, `fin`, `fuentes`,
   `crear_vistas`, `filas_por_columna`, `estilo_vista`, `punto_insercion`, `simular` y `pedido`. Si no, para aquí.

1. **Comprobar el dibujo** (no decisivo; solo lecturas).
   - `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}`: `Eje 1` de `0` a `831.3274`.
   - `listar_superficies`: deben estar `Terreno`, `Corredor 1 - Top` y `Corredor 1 - Datum` (las dos últimas marcadas
     como superficies de `Corredor 1`). **Anota la lista completa** (si está `Terreno 2`, también se muestreará).
   - `listar_corredores`: `Corredor 1` con una línea base sobre `Eje 1` y `esta_desactualizado: false`.
   - `estado_corredor` con `args={"corredor":"Corredor 1"}`: las dos superficies de corredor, sin desactualizar.
   Si falta algo, **para** (ver requisitos).

2. **Punto de partida.** Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene (o la última). Mira cuántos
   archivos hay en `backups\` junto al DWG. **No llames todavía a `listar_lineas_muestreo`** (deja entrada en Deshacer).

3. **Simulación (decisivo).** `crear_secciones` con
   `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"filas_por_columna":5,"simular":true,"pedido":"simula las líneas de muestreo cada 20 m con 30 m a cada lado"}`.
   Esperado: `simulado: true`; `accion` que empieza por `Crear el grupo de líneas de muestreo 'Eje 1 - Muestreo' en el
   eje 'Eje 1' con 43 líneas de 0 a 831.327 cada 20 m (30 m a la izquierda y 30 m a la derecha), muestrear ` y termina
   en ` y crear 43 vistas de sección en 9 columnas de 5 filas desde (<x>, <y>)` (anota la lista de fuentes y el punto);
   `antes: {"existe": false, "numero_lineas": 0, "numero_vistas": 0}`; `despues` con `existe: true`,
   `numero_lineas: 43`, `progresiva_primera: 0`, `progresiva_ultima: 831.3274`, `progresivas` (43 números: `0, 20, 40,
   …, 820, 831.3274`), `intervalo: 20`, `ancho_izquierdo: 30`, `ancho_derecho: 30`, `fuentes_muestreadas` con
   `Terreno`, `Corredor 1 - Top`, `Corredor 1 - Datum` y `Corredor 1` (y `Terreno 2` si existe), `numero_vistas: 43`,
   `filas_por_columna: 5`, `columnas: 9`, `estilo_vista` (anota el nombre: es el estilo por defecto del dibujo; si no
   sale, hay un aviso que lo dice) y `punto_insercion` (el mismo punto de la `accion`); `avisos` con:
   `La última línea va en el final del tramo (831.327), a 11.327 m de la anterior (el intervalo es 20 m).`, el de
   `Punto de inserción automático: x = ...`, el de `'fuentes_muestreadas' es la lista prevista ...` y
   `La posición de cada vista se calcula al crearla, con el tamaño real de la anterior (separación 6): la simulación
   solo da el punto de la primera.` OK solo si salen **43** líneas, **9** columnas, las fuentes incluyen el terreno y las
   dos superficies del corredor, no hay `copia`, el menú Deshacer **no** cambió y no apareció ningún archivo nuevo en
   `backups\`.

4. **Errores controlados sin tocar nada.** Todos con `"simular":true`. Cada uno debe responder `ok: false` con el texto
   indicado, sin entrada nueva en Deshacer y sin archivo nuevo en `backups\`:
   - `args={"alineamiento":"NoExiste","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"simular":true}`
     → `No existe el alineamiento 'NoExiste'. Usa listar_alineamientos.`
   - `args={"alineamiento":"Eje 1","intervalo":0,"ancho_izquierdo":30,"ancho_derecho":30,"simular":true}`
     → `El 'intervalo' debe ser un número mayor que 0 (metros entre líneas de muestreo); llegó 0.`
   - `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":-5,"ancho_derecho":30,"simular":true}`
     → `El 'ancho_izquierdo' debe ser un número mayor que 0 (metros de franja a la izquierda del eje); llegó -5.`
   - **Fin fuera del eje**: `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"fin":900,"simular":true}`
     → `El 'fin' (900) queda fuera del eje 'Eje 1' (0 a 831.327): debe estar entre 0 y 831.327. Usa listar_alineamientos para ver el rango.`
   - **Demasiadas líneas**: `args={"alineamiento":"Eje 1","intervalo":1,"ancho_izquierdo":30,"ancho_derecho":30,"simular":true}`
     → `Con un 'intervalo' de 1 m entre 0 y 831.327 saldrían unas 832 líneas de muestreo; el máximo por llamada es 500. Usa un intervalo mayor o parte el tramo con 'inicio' y 'fin'.`
   - **Filas no enteras**: `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"filas_por_columna":2.5,"simular":true}`
     → `'filas_por_columna' debe ser un número entero mayor o igual que 1 (vistas de sección por columna de la cuadrícula); llegó 2.5.`
   - **Fuente que no existe**: `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"fuentes":"[\"NoExiste\"]","simular":true}`
     → empieza por `No existe la fuente 'NoExiste' para las líneas de muestreo. Fuentes disponibles: ` y termina en
     `Usa listar_superficies y listar_corredores.` (anota la lista: cada fuente con su clase entre paréntesis).
   - **Estilo que no existe**: `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"estilo_vista":"NoExiste","simular":true}`
     → `No existe el estilo de vista de sección 'NoExiste' en el dibujo. Disponibles: <lista>. En Civil 3D se ven en
     Espacio de herramientas → Configuración → Visualización de sección → Estilos de visualización de sección.`
     **Anota la lista literal de estilos** (se usa en el paso 9; si sale `ninguno legible`, anótalo: es un hallazgo).
   - **Solo terreno y sin vistas** (este responde `ok: true`): `args={"alineamiento":"Eje 1","intervalo":100,"ancho_izquierdo":30,"ancho_derecho":30,"fuentes":"[\"Terreno\"]","crear_vistas":false,"simular":true}`
     → `accion` que termina en `muestrear Terreno, sin vistas de sección`, `despues` con `numero_lineas: 10` (`0, 100, …, 800, 831.3274`),
     `fuentes_muestreadas: ["Terreno"]` y `numero_vistas: 0` (no sale en `despues` porque no cambia).
   Si el puente rechaza alguno antes de llegar al plugin (error de validación de argumentos), anota el texto literal:
   no es fallo del plugin.

5. **Creación real (decisivo).** `crear_secciones` **sin** `simular` (el plugin la ensaya antes con `simular=true`; si
   el ensayo pasa, la escritura sigue igual y deja **una** entrada en Deshacer):
   `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"filas_por_columna":5,"pedido":"líneas de muestreo cada 20 m con 30 m a cada lado y las vistas de sección en cuadrícula de 5 filas, a la derecha del dibujo"}`.
   Esperado: `simulado: false`; `mensaje` igual a la `accion` del paso 3; `cambios` que incluye `existe`,
   `numero_lineas` y `numero_vistas`; `despues` **leído del dibujo** con lo mismo que en el paso 3 (43 líneas,
   `progresivas` de `0` a `831.3274`, 43 vistas), `fuentes_muestreadas` **reales** (anótalas) y `punto_insercion`
   leído de la primera vista (anota si coincide con el del paso 3); `copia` con `ruta` dentro de `backups\`,
   `metodo: "copia_de_disco"` y `estado: "terminada"` (si `copia.reutilizada` es `true`, no hay archivo nuevo: anótalo,
   no es fallo); `datos` con:
   - `lineas`: 43 elementos con `nombre` (`0+000.00`, `0+020.00`, …, `0+820.00`, `0+831.33`), `progresiva`,
     **`ancho_izquierdo: 30` y `ancho_derecho: 30`** (anótalos si salen `null`: no se pudieron leer los vértices) y
     **`secciones`** (la lista de fuentes de las secciones de cada línea: anota la de la primera, la del medio y la
     última); `lineas_sin_secciones` (lo esperado es `0`);
   - `fuentes`: todas las fuentes del grupo con `nombre`, `clase` (`superficie`, `corredor`, `superficie_corredor` u
     `otra`), **`tipo`** (el texto de `SectionSourceType`: **anótalo literal** para cada una) y `muestreada`;
     `fuentes_previstas` (las del paso 3);
   - `vistas`: 43 con `nombre` (`Eje 1 - Muestreo 0+000.00`, …), `x`, `y`, `ancho`, `alto` y `estilo`;
     `vistas_colocadas` con `fila` y `columna` (la `0+000.00` en fila 1, columna 1; la `0+080.00` en fila 5, columna 1;
     la `0+100.00` en fila 1, columna 2; la `0+831.33` en fila 3, columna 9); `columnas: 9`, `separacion: 6`,
     `punto_insercion_automatico: true`, **`extensiones_leidas`** (lo esperado es `43`), **`solapes: 0`** y
     **`grupos_vistas`** (anótalo entero: cuántos grupos de vistas hay y cuántas vistas en cada uno);
   - e `id_log`. `avisos`: el de la última línea y el del punto automático; anota literal cualquier otro (`Las fuentes
     reales del grupo no son las previstas ...`, `... líneas no tienen secciones ...`, `... se solapan ...`, `No se pudo
     leer la extensión ...`).
   En Civil 3D haz `ejecutar_comando` con `args={"comando":"_.ZOOM _E"}`: a la derecha del terreno deben verse las
   **43 vistas de sección en 9 columnas** (8 de 5 vistas y la última de 3), con la progresiva creciendo de abajo arriba
   en cada columna y cada vista con la línea del terreno y la del corredor (calzada y taludes), **sin** cuadros de
   diálogo abiertos; y sobre el eje, las 43 líneas de muestreo cruzándolo. Anota si las vistas se tocan o se solapan.
   OK solo si `numero_lineas` y `numero_vistas` son **43**, las `progresivas` van de `0` a `831.3274` y entre las
   `fuentes_muestreadas` están `Terreno` y las dos superficies de `Corredor 1`. Hallazgos posibles, anótalos literales y
   **para** en el primero:
   - error `La API no devolvió el objeto creado` o una excepción de `SampleLineGroup.Create`: no se pudo crear.
   - error que empieza por `No se pudo crear la línea de muestreo '...'` o `No se pudo crear la vista de sección '...'`:
     anota el mensaje completo y comprueba con `listar_lineas_muestreo` `args={"alineamiento":"Eje 1"}` que el grupo
     **no** quedó (la transacción se descartó): si quedó, es el hallazgo más grave.
   - error `El grupo 'Eje 1 - Muestreo' se creó pero ...`: el grupo existe pero no refleja lo pedido; anota `despues`.

6. **Vistas en pantalla (decisivo).** Haz `ejecutar_comando` con `args={"comando":"_.ZOOM _W <x1>,<y1> <x2>,<y2>"}`
   usando como esquinas el `x`, `y` de la primera vista de `datos.vistas` menos 20 y el de la última vista más su `ancho`
   y `alto` más 20 (anota el comando exacto) y, si puedes, `capturar_pantalla` (cierra antes las paletas que tapen el
   dibujo). Comprueba y anota: 9 columnas; en la primera columna, de abajo arriba, `0+000.00` … `0+080.00`; el estilo de
   las vistas (el de `despues.estilo_vista`); y que cada vista muestra el terreno y el corredor. OK si la cuadrícula es
   la descrita y no hay vistas superpuestas.

7. **Una sola entrada de Deshacer y una copia (decisivo).** Abre el menú Deshacer: respecto al paso 2 debe haber **una**
   entrada nueva (`Executefunction`), la del paso 5 (el `_.ZOOM` de los pasos 5 y 6 añade las suyas, que son del propio
   Civil 3D: anótalas aparte); los pasos 3 y 4 no añaden ninguna. En `backups\` debe haber **un** archivo nuevo
   `<nombre del dwg>_<fecha>_crear_secciones.dwg` (el `copia.ruta` del paso 5; si `copia.reutilizada` es `true`, no hay
   archivo nuevo). En `mcp_log.jsonl` deben estar las líneas `llamada` de los pasos 3 (`"simular":true`, `ok:true`), 4
   (`ok:false`, salvo la última, `ok:true`) y 5 (con el `pedido` literal y `ok:true`).

8. **Lectura cruzada.** `listar_lineas_muestreo` con `args={"alineamiento":"Eje 1","fuentes":true,"pedido":"lista las líneas de muestreo del Eje 1 con sus fuentes"}`
   (deja una entrada en Deshacer, es lo normal): debe aparecer `Eje 1 - Muestreo` con `n_lineas: 43`, `inicio: 0`,
   `fin: 831.3274` y `fuentes` con `muestreada: true` en las mismas que `datos.fuentes` del paso 5 (y `false` en las
   demás). OK si coincide con el paso 5.

9. **Segundo grupo con anchos distintos, estilo pedido y punto propio (decisivo).** Elige de la lista del paso 4 un
   estilo de vista de sección **distinto** del `estilo_vista` del paso 3 (si solo hay uno, usa ese) y anótalo. Luego
   `crear_secciones` con
   `args={"alineamiento":"Eje 1","nombre":"Muestreo 100","intervalo":100,"ancho_izquierdo":10,"ancho_derecho":25,"filas_por_columna":2,"estilo_vista":"<el estilo elegido>","punto_insercion":"{\"x\":1000,\"y\":500}","pedido":"crea otro grupo de líneas cada 100 m con 10 m a la izquierda y 25 a la derecha, vistas en 2 filas debajo del dibujo"}`.
   Esperado: `despues` con `numero_lineas: 10` (`progresivas` `0, 100, …, 800, 831.3274`), `numero_vistas: 10`,
   `columnas: 5`, `estilo_vista` = el elegido y `punto_insercion` ≈ `{"x": 1000, "y": 500}`; `datos.lineas` con
   **`ancho_izquierdo: 10` y `ancho_derecho: 25`** en todas; `datos.vistas_colocadas` con `0+000.00` en fila 1,
   columna 1, `0+100.00` en fila 2, columna 1 y `0+831.33` en fila 2, columna 5; `avisos` con
   `La última línea va en el final del tramo (831.327), a 31.327 m de la anterior (el intervalo es 100 m).` y **sin**
   `Las vistas quedaron con el estilo ...`. **Comprobación del lado** (anótala): con
   `ejecutar_comando` `args={"comando":"_.ZOOM _W 960,1950 1040,2050"}` mira la línea de la progresiva 0 de este grupo
   (en x = 1000): el eje va hacia el este, así que su **izquierda es el norte**: la línea debe ir de y = 2010 (10 m al
   norte) a y = 1975 (25 m al sur). Si va al revés (25 al norte), el signo de los desplazamientos está cambiado: es un
   hallazgo importante (anota también lo que dice `datos.lineas[0]`). Si puedes, abre las propiedades de esa línea de
   muestreo en Civil 3D (Prospector → Alineaciones → `Eje 1` → Grupos de líneas de muestreo → `Muestreo 100`) y anota los
   anchos de franja izquierdo y derecho que muestra. **No llames a nada más antes del paso 10.**

10. **Deshacer (decisivo).** En Civil 3D, `_.UNDO 1` **una vez** (con `ejecutar_comando` `args={"comando":"_.UNDO 1"}` o
    Ctrl+Z). Luego `listar_lineas_muestreo` con `args={"alineamiento":"Eje 1"}`:
    - Si **`Muestreo 100` desapareció** y `Eje 1 - Muestreo` sigue: `_.UNDO 1` deshizo la creación entera. Mira con
      `_.ZOOM _W 950,450 1500,800` que las 10 vistas de `Muestreo 100` ya no se ven.
    - Si **`Muestreo 100` sigue** en la lista: es un hallazgo importante (como la superficie TIN de la 1.7.0). Anota su
      `n_lineas` y mira en pantalla si siguen sus líneas y sus vistas.
    Anota también si, después del `_.UNDO 1`, quedó algo suelto en pantalla (vistas sin grupo, líneas). Este paso
    responde la pregunta principal del informe.

11. **Error por nombre repetido.** Repite exactamente el `crear_secciones` del paso 5 (sin `simular`). Esperado:
    `ok: false` y `error` que contiene `Ya existe el grupo de líneas de muestreo 'Eje 1 - Muestreo' en el alineamiento
    'Eje 1'. Usa listar_lineas_muestreo o pasa otro 'nombre'.`, con `id_log`. OK solo si **no** hay entrada nueva en el
    menú Deshacer y **no** aparece ningún archivo nuevo en `backups\`: la escritura se ensaya antes con `simular=true`,
    el ensayo da el error y el contexto de comando no se abre. `leer_historial` con `args={"ultimas_n":15}` debe
    mostrar `MCP ✗ crear_secciones no se ejecutó: falló la comprobación previa (simulación)...` y **ninguna** línea
    `Comando inicia: EXECUTEFUNCTION` desde la llamada; en `mcp_log.jsonl`, una sola línea de esta llamada con `ok:false`.

12. **Cierre.** `leer_log` con `args={"ultimas_n":40}`: una línea `llamada` por cada paso, con `ok:false` en los errores
    controlados (pasos 4 y 11) y `ok:true` en los demás. No guardes el dibujo (es una copia).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_113_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, y las respuestas a estas **preguntas para la memoria técnica**:

1. ¿Qué fuentes ofreció Civil 3D en el grupo nuevo (`datos.fuentes` del paso 5): nombre, clase y **texto de `tipo`**
   (`SectionSourceType`) de cada una? ¿Coincidieron con las previstas de la simulación (`fuentes_previstas`)? ¿Aparecieron
   fuentes de otra clase?
2. ¿Se crearon las secciones al crear las líneas, dentro de la misma transacción (`datos.lineas[].secciones`,
   `lineas_sin_secciones`)? ¿De qué fuentes salió cada sección (la lista de una línea cualquiera)?
3. ¿`Entity.GeometricExtents` dio el tamaño real de cada vista recién creada (`datos.extensiones_leidas` = 43)? ¿Qué
   `ancho` y `alto` tienen las vistas y cuántos `solapes` hubo? ¿La cuadrícula en pantalla fue la descrita?
4. ¿En qué grupo de vistas dejó `SectionView.Create` las vistas (`datos.grupos_vistas`: número de grupos, nombres,
   vistas de cada uno, `individuales`)?
5. ¿`_.UNDO 1` quitó del dibujo el grupo, las líneas y las vistas (paso 10)? Si no, ¿qué quedó?
6. ¿Los anchos quedaron en el lado correcto (paso 9: 10 m al norte y 25 m al sur en la progresiva 0)? ¿Qué anchos
   mostró Civil 3D en las propiedades de la línea?
7. El estilo de vista de sección por defecto (`despues.estilo_vista` del paso 3), la lista de estilos del paso 4 y si
   `estilo_vista` se aplicó en el paso 9.
8. Tiempos de los pasos 5 y 9 (`ms`).

Y la lista de miembros de la sección 1.13.0 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
