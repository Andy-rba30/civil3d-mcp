# Instrucciones para el agente que trabaja con Civil 3D por MCP

Estas herramientas actúan sobre el dibujo activo de Civil 3D 2027 a través del plugin ArbaMcp. Léelas antes de tocar nada.

## 1. Precedencia de herramientas

1. **Herramienta existente primero**: usa siempre la herramienta específica (`listar_*`, `asignar_*`, `establecer_*`, `agregar_*`, `crear_*`, `reconstruir_*`, `pegar_superficie`, `guardar_*`, `punto_a_pk`, `pk_a_punto`, `cota_superficie`, `extension_superficie`, `interseccion_ejes`).
2. **Si ninguna cubre el caso, busca una receta con `buscar_api`** (argumentos directos: `consulta`, `limite`; devuelve texto; funciona con Civil 3D cerrado). Las recetas de la memoria técnica salen primero en su respuesta; síguelas tal cual.
3. **Si no hay receta, `buscar_api` + código**: busca la clase o el miembro de la API (en español o en inglés; el glosario amplía la consulta y marca cada concepto `[confirmado]` o `[por confirmar]`) y escribe el código con lo que esté **confirmado** en el catálogo; nunca inventes un miembro. Si responde `Error:`, di al usuario qué falta (la variable `MEMORIA_FIRMA_DIR` o un archivo) y cómo ponerlo; no sigas a ciegas.
4. **`ejecutar_comando` solo como último recurso**, cuando ni herramienta, ni receta, ni API lo cubren. Envía texto a la línea de comandos, no sabe qué ha cambiado y no verifica nada. Por defecto ya no envuelve la orden en UNDO; pásale `undo=true` si quieres un grupo de deshacer.
5. **`capturar_pantalla` solo para diagnosticar**: úsala cuando una llamada devuelve "tiempo agotado" o sospechas que hay un cuadro de diálogo abierto. No la uses para "ver" el modelo: los datos se leen con las herramientas de lectura.
6. `leer_historial` y `leer_log` sirven para saber qué se ejecutó y con qué resultado; `leer_log` devuelve una línea JSON por cada llamada (id, hora, herramienta, pedido, args, ok, ms, error) y por cada aprobación.
6b. **Envía `pedido` siempre**: toda herramienta del plugin acepta `pedido` (string) con el pedido original del usuario, **con sus palabras** (no lo resumas ni lo traduzcas a nombres de herramientas); no cambia lo que hace la herramienta, solo queda en el log. Cada respuesta trae `id_log`. **Cuando el usuario confirme que un resultado fue correcto**, llama a `aprobar_resultado` con ese `id_log`, `aprobado=true` y, en `comentario`, sus palabras; si dice que estuvo mal, `aprobado=false`. No apruebes nada por tu cuenta.
7. **Civil 3D ocupado**: las herramientas que tocan el dibujo esperan a que Civil 3D esté libre (sin comando activo ni cuadro de diálogo) y, si el tiempo se agota mientras esperan, se descartan sin ejecutarse. `ping`, `leer_historial`, `leer_log`, `leer_variable` y `capturar_pantalla` responden siempre. Si una llamada devuelve "tiempo agotado" o "Civil 3D siguió ocupado", pide al usuario que termine el comando o cierre el diálogo antes de repetirla; `leer_historial` muestra `MCP ⏳ <herramienta> espera: <motivo>`.
8. **Antes de encadenar varias llamadas iguales, usa el lote**: varios `asignar_objetivo` son un `asignar_objetivos` (parámetro `asignaciones`, arreglo JSON como texto con los argumentos de cada asignación) y varios `establecer_frecuencia` son un `establecer_frecuencias` (parámetro `regiones`). Un lote es un solo contexto de comando, una copia de seguridad, una línea de log y **una** entrada de Deshacer (en Civil 3D se llama `Executefunction`; las lecturas no dejan entrada); con `simular=true` devuelve el `plan` por índice; un elemento cuyo objeto no existe va a `fallidos` con su motivo y el resto se aplica. Límite de 200 elementos salvo `forzar=true`. Los parámetros de tipo `json` se pasan como texto con el JSON dentro. Para deshacer una asignación de objetivos usa **`deshacer_objetivos`** (por defecto deshace la última; `id` para una concreta, el `restaurar.args.id` de la respuesta): Civil 3D no revierte objetivos con `_.UNDO 1` ni con Ctrl+Z, ni siquiera los cambiados desde su propio cuadro. Si la pila se perdió (Civil 3D reiniciado), reasigna con `restaurar.antes`. Nunca pongas como objetivo una superficie generada por el mismo corredor (`listar_superficies` la marca con `corredor`): el plugin la rechaza, igual que Civil 3D.
9. **Civil 3D arrancando**: el puente sondea `GET /ping` del plugin (sin token) y registra las herramientas en cuanto responde; si tu lista de herramientas está vacía o incompleta, espera unos segundos y vuelve a pedirla (o reconecta el servidor MCP). No llames a `ejecutar_comando` para "despertar" a Civil 3D.

## 2. Flujo obligatorio para cambios en corredores

1. `listar_corredores` → identifica el corredor y sus líneas base.
2. `listar_regiones` (corredor, línea base) → índice, rango, ensamblaje y frecuencias de cada región.
3. `listar_objetivos` (corredor, línea base, región) → subensamblajes, tipo de objetivo y objetos asignados.
4. **Propón el plan al usuario en una tabla** con columnas: región, ensamblaje, objetivo antes → objetivo después (y opción). No ejecutes nada antes de mostrarla.
5. Ejecuta cada escritura con **`simular=true`** y muestra el resultado (`antes`/`despues` previstos).
6. **Confirma con el usuario**.
7. Ejecuta sin `simular`. Cada escritura hace una copia de seguridad (`backups\`), escribe en `mcp_log.jsonl` y devuelve `antes`/`despues` leídos del dibujo; si el dibujo no refleja el cambio, la herramienta falla y **no debes reintentar sin avisar al usuario**. La copia (`copia.ruta`) es una copia del `.dwg` de disco: **refleja el último guardado en disco, no el estado en memoria** (`copia.refleja_guardado_de` dice de cuándo; `copia.reutilizada=true` si el archivo no cambió desde la copia anterior). Si el usuario quiere conservar el estado actual antes de una serie de cambios, `guardar_dibujo` o `guardar_copia` (esta sí guarda lo que hay en memoria) primero.
8. `reconstruir_corredor`.
9. `estado_corredor` → comprueba `esta_desactualizado=false` y el estado de las superficies del corredor.

Después de cada escritura comprueba `esta_desactualizado` (corredor) o `esta_desactualizada` (superficie): un objeto desactualizado no refleja aún el cambio en pantalla.

## 2b. Flujo vial completo (de la superficie al volumen)

Es la **única receta** de este archivo, porque es el flujo principal (las demás se buscan con `buscar_api`). Úsala cuando el usuario pida el flujo entero o varios de sus pasos seguidos. Las reglas de cada herramienta están en la sección 3; aquí van el orden y las paradas.

| Paso | Herramienta | Necesita antes |
|---|---|---|
| 1 | `crear_superficie_tin` | el archivo o el grupo de puntos del usuario |
| 2 | `crear_alineamiento_desde_polilinea` | la polilínea 2D de tangentes del usuario |
| 3 | `crear_perfil_terreno_y_vista` | el eje (2) y el terreno (1) |
| 4 | `crear_rasante` | el eje (2) y los PVI del usuario |
| 5 | `crear_corredor` | la rasante (4), el ensamblaje y el terreno (1) como `superficie_objetivo` |
| 6 | `crear_superficies_corredor` | el corredor (5): crea la Top y la Datum |
| 7 | `crear_secciones` | el corredor y sus superficies (5 y 6), para que las secciones los muestren |
| 8 | `calcular_volumenes` | el terreno (1) y la Datum (6); para la tabla por progresivas, las secciones (7). Pregunta los factores (por defecto 1) |

1. **Lo manual va antes.** El **ensamblaje** lo crea la persona en Civil 3D (no hay herramienta para eso): compruébalo con `listar_ensamblajes` antes de empezar y, si no hay, pídeselo y espera. La polilínea, el archivo de puntos y los PVI los da el usuario: no los inventes.
2. **Plan y simulación, sin escribir nada.** Lee el dibujo (`listar_superficies`, `listar_alineamientos`, `listar_perfiles`, `listar_ensamblajes`, `listar_corredores`), simula con `simular=true` todos los pasos cuyos datos ya existen (en un dibujo vacío, el 1 y el 2: los demás responderían "No existe ..." porque usan el objeto del paso anterior) y muestra una tabla con los 8 pasos: herramienta, argumentos, qué crea y si ya está simulado. **Espera la confirmación.**
3. **Paso a paso.** En cada paso, en orden: simúlalo si aún no lo está con el dibujo actual, ejecútalo sin `simular` y muestra su resultado (nombres creados, `avisos`, `copia.ruta`) junto con la simulación del paso siguiente; **espera la confirmación antes de seguir**. Cuando el usuario dé el paso por bueno, `aprobar_resultado` con el `id_log` de esa escritura.
4. **Si un paso falla, o un aviso cambia lo pedido** (un radio o una curva que no cabe, un corredor desactualizado, líneas sin secciones), para, explícalo y espera; no reintentes ni pases al siguiente, y no completes nada con `ejecutar_comando`. Si una herramienta dice que la persona tiene que completar un cuadro de diálogo, dile qué marcar, espera a que lo cierre y comprueba el resultado leyendo el dibujo.
5. **Al terminar**, resume los objetos creados, el `id_log` de cada paso y el corte, el relleno y el neto. Para volver atrás: cada paso es una entrada de Deshacer, pero `_.UNDO` no quita la superficie TIN ni revierte objetivos (`deshacer_objetivos`); la copia previa de cada paso está en su `copia.ruta`.

## 3. Reglas de dominio

- Los **taludes** (`DaylightGeneral`, `DaylightBasin`, `BasicSideSlopeCutDitch`, `LinkSlopeToSurface`, "Talud") **siempre necesitan un objetivo de superficie**; sin él el corredor avisa "TargetDTM not found" y el talud no se dibuja.
- En **regiones de intersección** usa **ensamblajes de media sección** (un solo grupo lateral): el carril y la acera de cada rama se construyen por separado con líneas base sobre los ejes de borde de calzada y los acuerdos de esquina.
- **Nunca "volver a crear regiones"** (`recrear regiones`, "Recreate Corridor Regions") en una intersección que el usuario editó a mano: se perderían los objetivos y las frecuencias ajustados. Ninguna herramienta del puente lo hace; tampoco lo pidas con `ejecutar_comando`.
- Los **objetivos de anchura** (desplazamiento) de un carril apuntan a un **eje de borde de calzada** (alineamiento de desplazamiento o de acuerdo) y los **objetivos de elevación** de ese carril apuntan al **perfil de ese mismo eje**. Un objetivo de elevación de un eje distinto produce escalones.
- Cambia el ensamblaje de una región con `asignar_ensamblaje_region`; después revisa `listar_objetivos`, porque los objetivos se reasignan por nombre de subensamblaje y pueden quedar vacíos.
- Para dividir una región usa `dividir_region`; para moverla, `establecer_rango_region` (falla si solapa). Ninguna herramienta borra regiones, líneas base, ensamblajes ni superficies: si el usuario lo necesita, que lo haga en Civil 3D.
- Las superficies **Top y Datum** de un corredor (las que usa el movimiento de tierras) se crean de una vez con `crear_superficies_corredor` (`simular=true` primero; nombres por defecto `<corredor> - Top` y `<corredor> - Datum`; deja los códigos como líneas de rotura, el voladizo, el contorno de la extensión del corredor y el corredor reconstruido). Para otras superficies de corredor: `agregar_superficie_corredor`, `agregar_codigo_superficie_corredor` y `agregar_contorno_superficie_corredor`; si el contorno automático no cierra, usa `exterior_poligono` con una polilínea cerrada.
- La rasante final que se pega en una superficie compuesta se actualiza con `reconstruir_corredor` → `pegar_superficie` (o `reconstruir_superficie` si ya estaba pegada).
- Superficie de terreno nueva → `crear_superficie_tin` (desde un grupo de puntos o un archivo de puntos; `simular=true` primero). Los intervalos de curvas de nivel viven en el **estilo** de superficie, no en la superficie: la herramienta no cambia estilos; si el usuario pide intervalos, elige con `estilo` uno del dibujo que ya los tenga y explícale lo que diga `avisos`.
- Eje nuevo desde una polilínea → `crear_alineamiento_desde_polilinea` (`simular=true` primero: devuelve los PI y qué radios caben). La polilínea debe ser **2D, abierta y solo de tangentes** (sus vértices son los PI; sin arcos ni 3D): las curvas las pone la herramienta con `radio` y `radios_por_pi`; un radio que no cabe deja el PI sin curva y `avisos` dice el radio máximo.
- Perfil de terreno y vista de perfil de un eje → `crear_perfil_terreno_y_vista` (`simular=true` primero: devuelve los nombres, el punto de inserción previsto y las progresivas). Crea los dos objetos en una sola entrada de Deshacer; sin `punto_insercion` coloca la vista a la derecha de lo dibujado y devuelve el punto usado; estilos y conjuntos por defecto del dibujo si no se pasan.
- Rasante (perfil de diseño FG) → `crear_rasante` (`simular=true` primero: devuelve `tramos` con las pendientes, `curvas` con A, K y si cada curva cabe, y `avisos`). La lista `pvis` va **ordenada por progresiva**, con al menos 2 PVI dentro del eje; el primero y el último no llevan curva; `longitud_curva` es la longitud por defecto de los PVI intermedios que no traigan la suya. Una curva que no cabe queda como quiebre y `avisos` dice la longitud máxima; una pendiente mayor del 12 % solo avisa (no hay norma cargada).
- Corredor nuevo → `crear_corredor`, después de la rasante y con un **ensamblaje que ya exista** en el dibujo (`listar_ensamblajes`; el plugin no crea ensamblajes: si no hay, pide al usuario que lo cree en Civil 3D). `simular=true` primero; luego `estado_corredor`. Pasa `superficie_objetivo` (el terreno) cuando haya más de una superficie; la herramienta pone esa superficie en todos los objetivos de superficie (taludes) y reconstruye. Para deshacer los objetivos usa `deshacer_objetivos` con `restaurar.args.id`; las superficies Top y Datum se añaden después con `crear_superficies_corredor`.
- Líneas de muestreo y vistas de sección → `crear_secciones`, **después del corredor y de sus superficies** si las secciones deben mostrarlos (las fuentes se eligen al crear el grupo). `simular=true` primero: devuelve las progresivas, las fuentes previstas y el punto de la primera vista. Por defecto muestrea todas las superficies, los corredores del eje y sus superficies; para otra selección pasa `fuentes`. Comprueba en la respuesta `fuentes_muestreadas`, `numero_vistas` y los `avisos` (líneas sin secciones, vistas solapadas); revisa después con `listar_lineas_muestreo` (`fuentes=true`).
- Volúmenes de corte y relleno → `calcular_volumenes`, **después de `crear_superficies_corredor`** (compara el terreno con la superficie Datum) y, si se quiere la tabla por progresivas, **después de `crear_secciones`** (la tabla sale de las secciones del terreno y del Datum del grupo). `simular=true` primero: dice qué terreno, Datum, factores y grupo usará. El **esponjamiento** va al corte (`factor_esponjamiento`, CutFactor) y la **compactación** al relleno (`factor_compactacion`, FillFactor), los dos como multiplicadores (1,25 se pasa `1.25`; un coeficiente menor que 1 se pasa como su inverso); **neto = corte − relleno** (positivo: sobra material; negativo: falta). Si hay varias superficies de terreno o varios corredores, pasa `terreno` o `corredor`. Muestra al usuario `datos.texto`, `corte`, `relleno`, `neto` y los `avisos`.
- `guardar_dibujo` guarda por API (QSAVE). Antes de una serie de cambios grandes, `guardar_copia` con un sufijo descriptivo.

## 4. Glosario español ↔ API de Civil 3D

| Español | API / interfaz inglesa |
|---|---|
| Objetivo de anchura (desplazamiento) | Offset target (`SubassemblyLogicalNameType.Offset`) |
| Objetivo de elevación (cota) | Elevation target (`SubassemblyLogicalNameType.Elevation`) |
| Objetivo de superficie | Surface target (`SubassemblyLogicalNameType.Surface`) |
| Media sección | Ensamblaje con un solo grupo lateral (half assembly) |
| Línea base | `Baseline` |
| Región | `BaselineRegion` |
| Frecuencia | `Frequency` (tangentes, curvas, espirales, curvas del perfil) |
| Contorno | `Boundary` (`CorridorSurfaceBoundary`) |
| Línea de rotura | `Breakline` |
| Envolvente / contorno exterior | `Outer boundary` (`UseAsOuterBoundary`) |
| Reconstruir | `Rebuild` |
| Estación adicional | `Additional station` |
| Opción de objetivo más cercano / exterior / interior | `TargetToOption` Nearest / Outside / Inside |
| Código de enlace / de punto | Link code / Point code |
| Línea característica | `FeatureLine` (o `CorridorFeatureLine` dentro del corredor) |
| Eje / alineamiento | `Alignment` |
| Tabla de PI (progresiva, radio, longitud de curva, PC y PT de cada PI) | `listar_pis` |
| Rasante | Perfil de diseño (FG, `Profile` tipo `FG`) |
| Terreno natural | Perfil de superficie (EG) / superficie TIN |
| Superficie de volumen, corte, relleno, neto | `TinVolumeSurface` (Cut, Fill, Net volume) |
| Esponjamiento / compactación | Cut factor / Fill factor |

## 5. Mensajes típicos de Civil 3D y su causa

| Mensaje | Causa probable | Qué hacer |
|---|---|---|
| `Width not found` / `Offset target not found` | La perpendicular en esa estación no corta el objetivo de desplazamiento (el eje objetivo termina antes o está al otro lado) | Revisa `mismo_lado`/`opcion` en `asignar_objetivo`, cambia el objetivo por uno que cubra la región o ajusta el rango de la región |
| `Outside Elevation not found` / `Elevation target not found` | El perfil objetivo no cubre el rango de la región | Amplía el perfil o recorta la región con `establecer_rango_region` |
| `TargetDTM not found` / `Target surface not found` | Un talud sin objetivo de superficie | `asignar_objetivo` con `tipo=superficie` o `asignar_objetivos_superficie` |
| `máscara 0 no añadida` / `mask 0 not added` / `boundary not closed` | El contorno automático (talud) no cierra porque el talud no intersecta el terreno en algún tramo | `agregar_contorno_superficie_corredor` con `tipo=exterior_poligono` y una polilínea cerrada |
| `Assembly not found` | La región apunta a un ensamblaje borrado | `asignar_ensamblaje_region` |
| `Corridor is out of date` / `esta_desactualizado=true` | Faltan reconstrucciones | `reconstruir_corredor` y luego `estado_corredor` |
| `Tiempo agotado` / `Civil 3D siguió ocupado` | Civil 3D está en medio de un comando, un trazado o un cuadro de diálogo; la herramienta esperó y se descartó sin ejecutarse | `leer_historial` (línea `MCP ⏳`) o `capturar_pantalla`, pide al usuario que termine o cierre, no reintentes a ciegas |

## 6. Exportaciones

`exportar_landxml` y `exportar_imx` no tienen API .NET: envían el comando correspondiente y el usuario debe completar el cuadro de diálogo en Civil 3D. Indícale qué alineamientos y superficies marcar y comprueba `existe=true` en la respuesta.

## 7. Llevar una superficie a Revit (coordenadas compartidas)

Revit tiene un origen interno que no se mueve y toda su geometría debe quedar a menos de 16 km de él (a partir de 33 km avisa y cambia la posición a "centro con centro" por su cuenta). Una superficie en UTM está a cientos de kilómetros del 0,0, así que **nunca se vincula "origen con origen"**: el aviso "muy lejos del origen" sale siempre, por muy georreferenciados que estén los dos archivos. El flujo correcto, con el MCP de Revit conectado:

1. `listar_superficies` y `extension_superficie` (superficie): anota `origen_sugerido` (este, norte, cota), `sistema_coordenadas.codigo` y `avisos`. Si hay un aviso de más de 33 km, recorta la superficie o el DWG antes de seguir; no hay posición que lo arregle.
2. En Revit, `get_project_coordinates`: si `shared_coordinates_set` es false, el modelo nunca se georreferenció.
3. En Revit, `set_project_coordinates` con `east`, `north`, `elevation` = `origen_sugerido` (en metros, `units="m"`) y `angle_degrees=0` si el DWG está alineado al norte de cuadrícula. Se hace una sola vez por modelo; si ya tenía coordenadas compartidas (por ejemplo adquiridas de otro modelo), no las cambies sin confirmar con el usuario.
4. `guardar_dibujo` en Civil 3D si hay cambios sin guardar: Revit lee el DWG de disco.
5. En Revit, `link_file` con la ruta del DWG y `placement="shared"`. Comprueba en la respuesta `extents.far_from_origin=false` y que `center_shared_m` coincide con el centro de la superficie; si `beyond_revit_limit` es true, Revit forzará "centro con centro" y hay que recortar.
6. En Revit, el Toposolid se crea desde el vínculo ("Crear a partir de importación"); si el usuario lo pide por herramienta, `execute_revit_code`.

Si Revit ya tenía el DWG vinculado "origen con origen", vuelve a vincularlo con `placement="shared"` después del paso 3: el vínculo viejo no se recoloca solo.

## 8. Qué lleva cada respuesta

- Los argumentos de cada herramienta del plugin van dentro de `args` (`{"args": {"corredor": "Corredor 1"}}`). `buscar_api` es del puente: sus argumentos van directos (`{"consulta": "rasante con PVI", "limite": 10}`) y responde texto, no JSON.
- Toda respuesta trae `id_log` (la línea de esta llamada en `mcp_log.jsonl`; sirve para `aprobar_resultado`) y los tiempos: `ms` (lo que tardó Civil 3D en la herramienta, medido por el plugin), `ms_espera` (lo que esperó en cola a que Civil 3D estuviera libre), `ms_ejecucion` (la herramienta en el hilo principal) y `ms_puente` (total visto desde el puente). Si el resultado es un objeto, van como claves suyas; si es una lista o un valor, la respuesta es `{"result": ..., "ms": ..., "ms_puente": ...}`.
- Un error es `{"ok": false, "error": "...", "id_log": "...", "ms_puente": ...}`. Los mensajes "Civil 3D no está abierto o ArbaMcp no cargó" y "Civil 3D no respondió en N s" vienen del puente; el resto, del plugin.
- Las escrituras devuelven `simulado`, `cambios`, `antes`, `despues`, `copia` (objeto: `ruta`, `metodo`, `reutilizada`, `ms`, `espera_ms`, `refleja_guardado_de`, `nota`), `datos` y `avisos`; los lotes añaden `fallidos` y, simulados, `plan`.
- Si una herramienta que conocías ya no existe, el servidor responde con la sustituta (tabla de nombres retirados del puente).

