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

## 3. Reglas de dominio

- Los **taludes** (`DaylightGeneral`, `DaylightBasin`, `BasicSideSlopeCutDitch`, `LinkSlopeToSurface`, "Talud") **siempre necesitan un objetivo de superficie**; sin él el corredor avisa "TargetDTM not found" y el talud no se dibuja.
- En **regiones de intersección** usa **ensamblajes de media sección** (un solo grupo lateral): el carril y la acera de cada rama se construyen por separado con líneas base sobre los ejes de borde de calzada y los acuerdos de esquina.
- **Nunca "volver a crear regiones"** (`recrear regiones`, "Recreate Corridor Regions") en una intersección que el usuario editó a mano: se perderían los objetivos y las frecuencias ajustados. Ninguna herramienta del puente lo hace; tampoco lo pidas con `ejecutar_comando`.
- Los **objetivos de anchura** (desplazamiento) de un carril apuntan a un **eje de borde de calzada** (alineamiento de desplazamiento o de acuerdo) y los **objetivos de elevación** de ese carril apuntan al **perfil de ese mismo eje**. Un objetivo de elevación de un eje distinto produce escalones.
- Cambia el ensamblaje de una región con `asignar_ensamblaje_region`; después revisa `listar_objetivos`, porque los objetivos se reasignan por nombre de subensamblaje y pueden quedar vacíos.
- Para dividir una región usa `dividir_region`; para moverla, `establecer_rango_region` (falla si solapa). Ninguna herramienta borra regiones, líneas base, ensamblajes ni superficies: si el usuario lo necesita, que lo haga en Civil 3D.
- Las superficies de corredor se crean con `agregar_superficie_corredor` (código `Top` o `Datum`), se completan con `agregar_codigo_superficie_corredor` y se cierran con `agregar_contorno_superficie_corredor`; si el contorno automático no cierra, usa `exterior_poligono` con una polilínea cerrada.
- La rasante final que se pega en una superficie compuesta se actualiza con `reconstruir_corredor` → `pegar_superficie` (o `reconstruir_superficie` si ya estaba pegada).
- Superficie de terreno nueva → `crear_superficie_tin` (desde un grupo de puntos o un archivo de puntos; `simular=true` primero). Los intervalos de curvas de nivel viven en el **estilo** de superficie, no en la superficie: la herramienta no cambia estilos; si el usuario pide intervalos, elige con `estilo` uno del dibujo que ya los tenga y explícale lo que diga `avisos`.
- Eje nuevo desde una polilínea → `crear_alineamiento_desde_polilinea` (`simular=true` primero: devuelve los PI y qué radios caben). La polilínea debe ser **2D, abierta y solo de tangentes** (sus vértices son los PI; sin arcos ni 3D): las curvas las pone la herramienta con `radio` y `radios_por_pi`; un radio que no cabe deja el PI sin curva y `avisos` dice el radio máximo.
- Perfil de terreno y vista de perfil de un eje → `crear_perfil_terreno_y_vista` (`simular=true` primero: devuelve los nombres, el punto de inserción previsto y las progresivas). Crea los dos objetos en una sola entrada de Deshacer; sin `punto_insercion` coloca la vista a la derecha de lo dibujado y devuelve el punto usado; estilos y conjuntos por defecto del dibujo si no se pasan.
- Rasante (perfil de diseño FG) → `crear_rasante` (`simular=true` primero: devuelve `tramos` con las pendientes, `curvas` con A, K y si cada curva cabe, y `avisos`). La lista `pvis` va **ordenada por progresiva**, con al menos 2 PVI dentro del eje; el primero y el último no llevan curva; `longitud_curva` es la longitud por defecto de los PVI intermedios que no traigan la suya. Una curva que no cabe queda como quiebre y `avisos` dice la longitud máxima; una pendiente mayor del 12 % solo avisa (no hay norma cargada).
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
| Rasante | Perfil de diseño (FG, `Profile` tipo `FG`) |
| Terreno natural | Perfil de superficie (EG) / superficie TIN |

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

