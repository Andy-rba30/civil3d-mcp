# Instrucciones para el agente que trabaja con Civil 3D por MCP

Estas herramientas actúan sobre el dibujo activo de Civil 3D 2027 a través del plugin ArbaMcp. Léelas antes de tocar nada.

## 1. Precedencia de herramientas

1. **API primero**: usa siempre la herramienta específica (`listar_*`, `asignar_*`, `establecer_*`, `agregar_*`, `reconstruir_*`, `pegar_superficie`, `guardar_*`, `punto_a_pk`, `pk_a_punto`, `cota_superficie`, `interseccion_ejes`).
2. **`ejecutar_comando` solo si ninguna herramienta cubre la acción**. Es el último recurso: envía texto a la línea de comandos, no sabe qué ha cambiado y no verifica nada. Por defecto ya no envuelve la orden en UNDO; pásale `undo=true` si quieres un grupo de deshacer.
3. **`capturar_pantalla` solo para diagnosticar**: úsala cuando una llamada devuelve "tiempo agotado" o sospechas que hay un cuadro de diálogo abierto. No la uses para "ver" el modelo: los datos se leen con las herramientas de lectura.
4. `leer_historial` y `leer_log` sirven para saber qué se ejecutó y con qué resultado; `leer_log` devuelve una línea JSON por cada escritura (hora, herramienta, args, ok, ms, error).
5. **Civil 3D ocupado**: las herramientas que tocan el dibujo esperan a que Civil 3D esté libre (sin comando activo ni cuadro de diálogo) y, si el tiempo se agota mientras esperan, se descartan sin ejecutarse. `ping`, `leer_historial`, `leer_log`, `leer_variable` y `capturar_pantalla` responden siempre. Si una llamada devuelve "tiempo agotado" o "Civil 3D siguió ocupado", pide al usuario que termine el comando o cierre el diálogo antes de repetirla; `leer_historial` muestra `MCP ⏳ <herramienta> espera: <motivo>`.
6. **Antes de encadenar varias llamadas iguales, usa el lote**: varios `asignar_objetivo` son un `asignar_objetivos` (parámetro `asignaciones`, arreglo JSON como texto con los argumentos de cada asignación) y varios `establecer_frecuencia` son un `establecer_frecuencias` (parámetro `regiones`). Un lote es un solo contexto de comando, una copia de seguridad, una línea de log y **una** entrada de Deshacer; con `simular=true` devuelve el `plan` por índice; un elemento cuyo objeto no existe va a `fallidos` con su motivo y el resto se aplica. Límite de 200 elementos salvo `forzar=true`. Los parámetros de tipo `json` se pasan como texto con el JSON dentro.
7. **Civil 3D arrancando**: el puente sondea `GET /ping` del plugin (sin token) y registra las herramientas en cuanto responde; si tu lista de herramientas está vacía o incompleta, espera unos segundos y vuelve a pedirla (o reconecta el servidor MCP). No llames a `ejecutar_comando` para "despertar" a Civil 3D.

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

## 7. Qué lleva cada respuesta

- Los argumentos de cada herramienta van dentro de `args` (`{"args": {"corredor": "Corredor 1"}}`).
- Toda respuesta trae los tiempos: `ms` (lo que tardó Civil 3D en la herramienta, medido por el plugin), `ms_espera` (lo que esperó en cola a que Civil 3D estuviera libre), `ms_ejecucion` (la herramienta en el hilo principal) y `ms_puente` (total visto desde el puente). Si el resultado es un objeto, van como claves suyas; si es una lista o un valor, la respuesta es `{"result": ..., "ms": ..., "ms_puente": ...}`.
- Un error es `{"ok": false, "error": "...", "ms_puente": ...}`. Los mensajes "Civil 3D no está abierto o ArbaMcp no cargó" y "Civil 3D no respondió en N s" vienen del puente; el resto, del plugin.
- Las escrituras devuelven `simulado`, `cambios`, `antes`, `despues`, `copia` (objeto: `ruta`, `metodo`, `reutilizada`, `ms`, `espera_ms`, `refleja_guardado_de`, `nota`), `datos` y `avisos`; los lotes añaden `fallidos` y, simulados, `plan`.
- Si una herramienta que conocías ya no existe, el servidor responde con la sustituta (tabla de nombres retirados del puente).

