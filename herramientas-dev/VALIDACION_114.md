# Prompt de validación de la 1.14.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Se ejecuta después de `VALIDACION_113.md`**, en la misma instalación y, si puede ser, **en la misma sesión de Civil
  3D, con el mismo dibujo abierto** (`C:\Proyectos\dibujos_prueba\PRUEBA_112.dwg` o la copia que usó la 113; anota cuál).
  La 113 no guarda el dibujo: si cerraste Civil 3D entre medias, el grupo de líneas de muestreo de la 113 no estará y el
  paso 1 lo vuelve a crear. Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`. Ningún dato real.
- **Lo que debe tener el dibujo** (lo dejaron las validaciones 1.10.0 a 1.13.0; el paso 1 lo comprueba):
  - `Eje 1` (de `0` a `831.3274`), `Rasante 1`, `Ensamblaje 1` y `Corredor 1` (de 0 a 831.3274 cada 10 m, taludes a
    `Terreno`).
  - `Terreno`: superficie TIN de 9 puntos que cubre x 900..1700, y 1900..2400. Puede estar también `Terreno 2` (la creó
    el paso 12 de la 1.11.0): no es fallo, anótalo (el paso 3 lo usa).
  - Las superficies de corredor **`Corredor 1 - Top`** y **`Corredor 1 - Datum`** (las creó la 1.12.0).
  - El grupo de líneas de muestreo **`Eje 1 - Muestreo`** en `Eje 1`: 43 líneas cada 20 m con 30 m a cada lado y
    secciones de `Terreno` y de las dos superficies del corredor (lo creó el paso 5 de la 1.13.0; si allí usaste
    `Muestreo MCP 113`, usa ese nombre en todos los pasos y anótalo). Puede quedar también `Muestreo 100` (paso 9 de la
    1.13.0) si el `_.UNDO 1` de su paso 10 no lo quitó: anótalo (el paso 3 lo usa).
  - **No** debe haber ninguna superficie llamada `Corredor 1 - Volumen` ni `Volumen sin factores`. Si la hay, **para** y
    anótalo.
- El plugin **1.14.0** se instala en el paso 0 desde la rama `tarea/2.7-volumenes` (trae fusionadas la 1.12.0 y la
  1.13.0, así que es la misma instalación en la que se pasaron `VALIDACION_112.md` y `VALIDACION_113.md`); el bundle
  queda con `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.14.0 (`uv sync` antes) y el
  cliente MCP reiniciado para que cargue la lista de **53** herramientas del plugin (más `buscar_api` del puente). El
  puente le da a `calcular_volumenes` hasta **300 s** (si llamas por `curl`, pasa `"timeout_s":300`).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.14.0**: la herramienta de escritura `calcular_volumenes` (paso 11 del flujo vial). En **una**
  transacción y **una** entrada de Deshacer crea una **superficie de volumen TIN** (`TinVolumeSurface.Create`) con base
  el terreno y comparación la superficie Datum del corredor, le pone los factores (`factor_esponjamiento` → `CutFactor`,
  `factor_compactacion` → `FillFactor`) y la reconstruye; relee `GetVolumeProperties` y devuelve **corte, relleno y
  neto** (corte ajustado = corte × esponjamiento; relleno ajustado = relleno × compactación; **neto = corte − relleno**:
  positivo sobra material, negativo falta). Si no se dan los nombres, elige el único terreno y la `<corredor> - Datum`.
  Además devuelve la **tabla por progresivas**: lee las secciones del terreno y del Datum de cada línea del grupo de
  muestreo y calcula, en el plugin, las áreas de corte y relleno de cada sección y los volúmenes entre líneas por
  **áreas medias** (no crea listas de materiales ni usa `ejecutar_comando`). Es la séptima herramienta del camino
  `Escritura.CrearObjeto`. **Los pasos 4, 6, 7, 8, 9 y 12 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.14.0):
  `TinVolumeSurface.Create` (tres argumentos), `CutFactor`/`FillFactor`, `Rebuild` sobre volumen,
  `GetVolumeProperties` (los seis volúmenes, `BaseSurface`, `ComparisonSurface`), `GetTinProperties`,
  `Section.SectionPoints`/`SectionPoint.Location` (si X es el desplazamiento e Y la cota), `Section.LeftOffset`/
  `RightOffset`/`MinmumElevation`/`MaximumElevation`, el signo de `UnadjustedNetVolume` y si `_.UNDO 1` quita la
  superficie de volumen.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 6): **"calcula los volúmenes de corte
  y relleno entre el terreno y la rasante del corredor 1, con esponjamiento 1,25 y compactación 1,10, y dame la tabla
  por progresivas"**.

---

Eres el agente de validación de la versión 1.14.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell o clics en Civil 3D. Ejecuta los pasos en orden, anota la respuesta
**literal** (JSON completo o, si es muy largo, las primeras 40 líneas; de `datos.tabla_progresivas.filas` basta con la
primera, la de la progresiva 400 y la última) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo. En este prompt solo se usa
  para `_.ZOOM` y `_.UNDO`, que son del propio Civil 3D.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- **`listar_lineas_muestreo` deja una entrada en el menú Deshacer** (es así desde la 1.2.2, no es fallo): solo se usa en
  el paso 1, antes del recuento del paso 2.
- Los volúmenes salen en unidades del dibujo al cubo (m³). Una diferencia menor de 0,01 m³ (o de una millonésima del
  valor) es OK. Los números exactos de corte y relleno no se conocen de antemano: lo que se comprueba es que **cuadren
  entre sí** y con lo que muestra Civil 3D.
- Al final hay una lista de **preguntas para el informe**: respóndelas con lo que viste.

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `tarea/2.7-volumenes`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`, `uv sync`. Reinicia el puente y el
   cliente MCP. Abre Civil 3D con el dibujo (ver requisitos). `ping` con
   `args={"pedido":"comprueba que el plugin responde"}` → `version` debe ser exactamente `1.14.0` (si la rama trae ya
   fusionada una corrección posterior, la versión de esa corrección: anótala). Pide la lista de herramientas: deben ser
   **53** del plugin (las 52 de la 1.13.0 y `calcular_volumenes`) y `calcular_volumenes` debe tener los parámetros
   `terreno`, `datum`, `corredor`, `nombre`, `factor_esponjamiento`, `factor_compactacion`, `estilo`,
   `grupo_muestreo`, `tabla_progresivas`, `simular` y `pedido`. Si no, para aquí. (Si cerraste Civil 3D después de la
   1.13.0, este paso deja el dibujo recién abierto: el paso 1 dirá qué falta.)

1. **Comprobar el dibujo** (no decisivo; solo lecturas).
   - `listar_superficies` con `args={"pedido":"lista las superficies"}`: deben estar `Terreno` (tipo `TIN`, sin
     corredor), `Corredor 1 - Top` y `Corredor 1 - Datum` (marcadas como superficies de `Corredor 1`). **Anota la lista
     completa** y si está `Terreno 2`. No debe estar `Corredor 1 - Volumen`.
   - `listar_corredores`: `Corredor 1` con una línea base sobre `Eje 1` y `esta_desactualizado: false`. Anota si hay más
     corredores.
   - `listar_lineas_muestreo` con `args={"alineamiento":"Eje 1","pedido":"lista las líneas de muestreo del Eje 1"}`:
     debe estar `Eje 1 - Muestreo` con `n_lineas: 43`, `inicio: 0`, `fin: 831.3274`. Anota si está también `Muestreo 100`.
   - **Si falta `Eje 1 - Muestreo`** (Civil 3D se cerró después de la 1.13.0): créalo repitiendo el paso 5 de
     `VALIDACION_113.md` (`crear_secciones` con
     `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"filas_por_columna":5,"pedido":"líneas de muestreo cada 20 m con 30 m a cada lado y las vistas de sección en cuadrícula de 5 filas, a la derecha del dibujo"}`)
     y anótalo. Si faltan el corredor o sus superficies, **para** (ver requisitos).

2. **Punto de partida.** Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene (o la última). Mira cuántos
   archivos hay en `backups\` junto al DWG.

3. **Simulación sin nombres** (no decisivo: comprueba la elección automática y sus mensajes). `calcular_volumenes` con
   `args={"simular":true,"pedido":"simula el cálculo de volúmenes sin decir las superficies"}`. Lo esperado depende del
   dibujo del paso 1 (anota cuál de los casos salió):
   - **Si `Terreno` es la única superficie TIN que no es de corredor**: `ok: true`, `simulado: true` y `accion` =
     `Crear la superficie de volumen TIN 'Corredor 1 - Volumen' con base 'Terreno' y comparación 'Corredor 1 - Datum', factor de esponjamiento (corte) 1 y de compactación (relleno) 1, y reconstruirla`
     seguida de `, con la tabla por progresivas del grupo 'Eje 1 - Muestreo'` si `Eje 1 - Muestreo` es el único grupo
     del eje con secciones del terreno y del Datum. Si también está `Muestreo 100`, la `accion` termina en
     `reconstruirla` y `avisos` trae `Hay varios grupos de líneas de muestreo con secciones de las dos superficies: ... Sin tabla por progresivas: pasa 'grupo_muestreo' con el que quieres usar.`
   - **Si está `Terreno 2`** (u otra TIN que no es de corredor): `ok: false` con `error` =
     `Hay varias superficies TIN que pueden ser el terreno: Terreno, Terreno 2. Pasa 'terreno' con el nombre de la que quieres usar como base (ver listar_superficies).`
     (el orden de la lista puede variar).
   OK si sale uno de los dos casos según el dibujo, sin `copia`, sin entrada nueva en Deshacer y sin archivo nuevo en
   `backups\`.

4. **Simulación con los datos del pedido (decisivo).** `calcular_volumenes` con
   `args={"corredor":"Corredor 1","terreno":"Terreno","factor_esponjamiento":1.25,"factor_compactacion":1.10,"grupo_muestreo":"Eje 1 - Muestreo","simular":true,"pedido":"simula los volúmenes con esponjamiento 1,25 y compactación 1,10"}`.
   Esperado: `simulado: true`; `accion` =
   `Crear la superficie de volumen TIN 'Corredor 1 - Volumen' con base 'Terreno' y comparación 'Corredor 1 - Datum', factor de esponjamiento (corte) 1.25 y de compactación (relleno) 1.1, y reconstruirla, con la tabla por progresivas del grupo 'Eje 1 - Muestreo'`;
   `antes: {"existe": false}`; `despues` con `existe: true`, `tipo: "TinVolume"`, `superficie_base: "Terreno"`,
   `superficie_comparacion: "Corredor 1 - Datum"`, `factor_corte: 1.25`, `factor_relleno: 1.1`,
   `estilo: "(estilo por defecto del dibujo)"`, `grupo_muestreo: "Eje 1 - Muestreo"` y `lineas_tabla: 43`; `avisos` con
   `Corte, relleno, neto y la tabla por progresivas se calculan al crear la superficie de volumen: la simulación solo dice qué superficies, factores y grupo se usarán.`
   (anota literal cualquier otro, por ejemplo uno de superficie o corredor desactualizado). OK solo si sale así, no hay
   `copia`, el menú Deshacer **no** cambió y no apareció ningún archivo nuevo en `backups\`.

5. **Errores controlados sin tocar nada.** Todos con `"simular":true`. Cada uno debe responder `ok: false` con el texto
   indicado, sin entrada nueva en Deshacer y sin archivo nuevo en `backups\`:
   - **Factor 0**: `args={"terreno":"Terreno","factor_esponjamiento":0,"simular":true}`
     → `'factor_esponjamiento' debe ser mayor que 0 (se recibió 0). Sin factor, pasa 1 o no lo pases.`
   - **Factor mal escrito**: `args={"terreno":"Terreno","factor_compactacion":125,"simular":true}`
     → `'factor_compactacion' = 125 es demasiado grande (máximo 5): un factor de 25 % se escribe 1.25, no 125 ni 25.`
   - **Terreno que no existe**: `args={"terreno":"NoExiste","simular":true}`
     → `No existe la superficie 'NoExiste' ('terreno'). Usa listar_superficies.`
   - **Corredor que no existe**: `args={"terreno":"Terreno","corredor":"NoExiste","simular":true}`
     → `No existe el corredor 'NoExiste'. Corredores del dibujo: Corredor 1. Usa listar_corredores.` (con los corredores
     del paso 1).
   - **Base y comparación iguales**: `args={"terreno":"Terreno","datum":"Terreno","simular":true}`
     → empieza por `La superficie base y la de comparación no pueden ser la misma ('Terreno')`.
   - **Datum que no es del corredor**: `args={"terreno":"Corredor 1 - Top","datum":"Terreno","corredor":"Corredor 1","simular":true}`
     → `La superficie 'Terreno' no es del corredor 'Corredor 1' (no la genera ningún corredor). Quita 'corredor' o pasa en 'datum' la superficie Datum de ese corredor (ver estado_corredor y listar_superficies).`
   - **Estilo que no existe**: `args={"terreno":"Terreno","estilo":"NoExiste","simular":true}`
     → `No existe el estilo de superficie 'NoExiste' en el dibujo. Estilos disponibles: <lista>. En Civil 3D se ven en Espacio de herramientas → Configuración → Superficie → Estilos de superficie.`
     (anota la lista; si sale `ninguno legible`, es un hallazgo).
   - **Grupo que no existe**: `args={"terreno":"Terreno","grupo_muestreo":"NoExiste","simular":true}`
     → empieza por `No existe el grupo de líneas de muestreo 'NoExiste'. Grupos del dibujo: Eje 1 - Muestreo (Eje 1)` y
     termina en `Usa listar_lineas_muestreo o créalo con crear_secciones.`
   - **Factor poco habitual** (este responde `ok: true`): `args={"terreno":"Terreno","factor_esponjamiento":3,"tabla_progresivas":false,"simular":true}`
     → `despues` con `factor_corte: 3` y `lineas_tabla: 0` (sin `grupo_muestreo`: sigue en `null` y `despues` solo
     muestra lo que cambia) y, en `avisos`,
     `'factor_esponjamiento' = 3 es poco habitual (lo normal está entre 0.5 y 2): revisa que no sea un error.`
   Si el puente rechaza alguno antes de llegar al plugin (error de validación de argumentos), anota el texto literal:
   no es fallo del plugin.

6. **Creación real (decisivo).** `calcular_volumenes` **sin** `simular` (el plugin la ensaya antes con `simular=true`;
   si el ensayo pasa, la escritura sigue igual y deja **una** entrada en Deshacer):
   `args={"corredor":"Corredor 1","terreno":"Terreno","factor_esponjamiento":1.25,"factor_compactacion":1.10,"grupo_muestreo":"Eje 1 - Muestreo","pedido":"calcula los volúmenes de corte y relleno entre el terreno y la rasante del corredor 1, con esponjamiento 1,25 y compactación 1,10, y dame la tabla por progresivas"}`.
   Esperado: `simulado: false`; `mensaje` igual a la `accion` del paso 4; `despues` **leído del dibujo** con
   `existe: true`, `tipo: "TinVolume"`, `superficie_base: "Terreno"`, `superficie_comparacion: "Corredor 1 - Datum"`,
   `factor_corte: 1.25`, `factor_relleno: 1.1`, `estilo` (anota el nombre), `corte`, `relleno` y `neto` (números:
   anótalos), `triangulos` (mayor que 0) y `esta_desactualizada: false`; `copia` con `ruta` dentro de `backups\`,
   `metodo: "copia_de_disco"` y `estado: "terminada"` (si `copia.reutilizada` es `true`, no hay archivo nuevo: anótalo,
   no es fallo); `datos` con:
   - `superficie_volumen: "Corredor 1 - Volumen"` (el nombre de la superficie creada), `superficie_base`,
     `superficie_comparacion`, `corredor: "Corredor 1"`, `elegidas_automaticamente` (`terreno: false`, `datum: true`
     porque sale de `Corredor 1`, `corredor: false` y `nombre: true`)
     y `factores` (`esponjamiento: 1.25`, `compactacion: 1.1` y la `convencion`);
   - **las cuentas tienen que cuadrar**: `corte` = `sin_ajustar.corte` × 1,25; `relleno` = `sin_ajustar.relleno` × 1,10;
     `neto` = `corte` − `relleno`; `sin_ajustar.neto` = `sin_ajustar.corte` − `sin_ajustar.relleno`; `balance` =
     `sobra` si `neto` es positivo, `falta` si es negativo; `texto` que empieza por `Sobran` o `Faltan` con el valor
     absoluto de `neto`. Haz las tres multiplicaciones y anótalas;
   - `civil3d` con los seis volúmenes y los dos factores tal como los da Civil 3D (anótalo entero) y
     **`convencion_neto_civil3d`** (anótalo literal: `corte_menos_relleno` o `relleno_menos_corte` dice el signo del neto
     de Civil 3D; `otra` es un hallazgo);
   - `unidades`, `triangulos`, `extension` (dentro de la del terreno: x 900..1700, y 1900..2400);
   - **`tabla_progresivas`**: `grupo: "Eje 1 - Muestreo"`, `alineamiento: "Eje 1"`, `metodo`, **`numeros: true`**,
     **`supuesto_ejes`** con `comprobado: true` y `lineas_comprobadas_con_la_superficie: 1` (anota `motivo` si no es
     `null`), `numero_lineas: 43`, `tramos: 42`, `tramos_sin_volumen` (lo esperado es `0`; si sale 1 o 2 en los extremos,
     anótalo: hallazgo menor), `totales` (anótalos: `corte_ajustado` = `corte` × 1,25 y `relleno_ajustado` = `relleno`
     × 1,10), **`diferencia_con_superficie_pct`** (anótala: menos de un 10 % en corte y en relleno es lo esperado; más
     es un hallazgo, no un fallo) y `filas`: la primera (`pk: 0`, `volumen_corte: null`), la de `pk: 400` y la última
     (`pk: 831.3274`, con `masa` igual a `totales.neto`); en ninguna debe salir `falta` salvo lo anotado en
     `tramos_sin_volumen`;
   - e `id_log`. `avisos`: anota literal cualquiera (`El corte ajustado de Civil 3D ... no es ...`, `La tabla por
     progresivas sale sin números: ...`, `... difiere más de un 10 % ...`, `... tramos no suman volumen ...`).
   OK solo si la superficie se creó con `tipo: "TinVolume"`, los factores releídos son 1,25 y 1,10, las cuentas cuadran y
   la tabla sale con `numeros: true`. Hallazgos posibles, anótalos literales y **para** en el primero de los dos
   primeros:
   - error `No se pudo crear la superficie de volumen 'Corredor 1 - Volumen' (TinVolumeSurface.Create: ...)`: no se pudo
     crear (la transacción se descartó; comprueba con `listar_superficies` que no quedó).
   - error que empieza por `La superficie de volumen 'Corredor 1 - Volumen' se creó pero ...` o `El dibujo no refleja el
     cambio pedido en ...`: la superficie existe pero no refleja lo pedido; anota `despues` (va en el texto del error).
   - `tabla_progresivas.numeros: false`: anota `supuesto_ejes.motivo` y el aviso; **sigue** (los totales no dependen de
     la tabla): es el hallazgo que decide el supuesto de `SectionPoint.Location`.

7. **Comprobación cruzada en Civil 3D (decisivo).** `listar_superficies` con
   `args={"pedido":"lista las superficies después de calcular volúmenes"}`: debe aparecer `Corredor 1 - Volumen` con
   `tipo_superficie: "TinVolume"` (y sin corredor). Luego, en Civil 3D: Espacio de herramientas → Prospector →
   Superficies → `Corredor 1 - Volumen` → clic derecho → **Propiedades de superficie** → pestaña **Estadísticas** →
   apartado **Volumen**. Anota los valores que muestra (desmonte/corte y terraplén/relleno sin ajustar y ajustados, neto,
   factores de corte y de relleno) y compáralos con `datos.civil3d` del paso 6: deben coincidir (±0,01). Anota también
   con qué signo o etiqueta muestra Civil 3D el neto (por ejemplo `<Corte>` / `<Relleno>`). En la pestaña
   **Definición** anota la superficie base y la de comparación (deben ser `Terreno` y `Corredor 1 - Datum`). Cierra el
   cuadro con **Cancelar** (no cambies nada). OK si los números y las superficies coinciden.

8. **Supuesto de los puntos de sección (decisivo).** Comprueba con lecturas que la fila de la progresiva 400 de la tabla
   tiene el signo correcto:
   - `pk_a_punto` con `args={"alineamiento":"Eje 1","pk":400}` → anota `x` e `y`.
   - `cota_superficie` con `args={"superficie":"Terreno","x":<x>,"y":<y>}` y con
     `args={"superficie":"Corredor 1 - Datum","x":<x>,"y":<y>}` → anota las dos cotas.
   - Si la cota del `Terreno` es **mayor** que la del Datum, en el eje hay que excavar: la fila `pk: 400` del paso 6 debe
     tener `area_corte` mayor que 0. Si es **menor**, hay que rellenar: debe tener `area_relleno` mayor que 0. (Las dos
     pueden ser mayores que 0 si el terreno corta el Datum dentro de la franja; lo que se comprueba es que la que
     corresponde al eje no sea 0.)
   - Si puedes, mira la vista de sección `Eje 1 - Muestreo 0+400.00` en pantalla (`_.ZOOM` a la vista) y anota si el
     terreno pasa por encima o por debajo de la calzada.
   OK si el signo de la fila coincide con las cotas. Si no coincide (o si en el paso 6 salió `numeros: false`), es el
   hallazgo principal de la tabla: anota las cotas, la fila y `supuesto_ejes`.

9. **Una sola entrada de Deshacer y una copia (decisivo).** Abre el menú Deshacer: respecto al paso 2 debe haber **una**
   entrada nueva (`Executefunction`), la del paso 6 (si en el paso 8 usaste `_.ZOOM`, añade las suyas, que son del propio
   Civil 3D: anótalas aparte); los pasos 3, 4, 5, 7 y 8 no añaden ninguna (`listar_superficies`, `pk_a_punto` y
   `cota_superficie` son lecturas). En `backups\` debe haber **un** archivo nuevo
   `<nombre del dwg>_<fecha>_calcular_volumenes.dwg` (el `copia.ruta` del paso 6; si `copia.reutilizada` es `true`, no
   hay archivo nuevo). En `mcp_log.jsonl` deben estar las líneas `llamada` de los pasos 3 a 6 (la del 6 con el `pedido`
   literal y `ok:true`).

10. **Error por nombre repetido.** Repite exactamente el `calcular_volumenes` del paso 6 (sin `simular`). Esperado:
    `ok: false` y `error` = `Ya existe la superficie 'Corredor 1 - Volumen'. Usa listar_superficies o pasa otro 'nombre'.`,
    con `id_log`. OK solo si **no** hay entrada nueva en el menú Deshacer y **no** aparece ningún archivo nuevo en
    `backups\`: la escritura se ensaya antes con `simular=true`, el ensayo da el error y el contexto de comando no se
    abre. `leer_historial` con `args={"ultimas_n":15}` debe mostrar
    `MCP ✗ calcular_volumenes no se ejecutó: falló la comprobación previa (simulación)...` y **ninguna** línea
    `Comando inicia: EXECUTEFUNCTION` desde la llamada; en `mcp_log.jsonl`, una sola línea de esta llamada con `ok:false`.

11. **Segunda superficie, sin factores ni tabla.** `calcular_volumenes` con
    `args={"terreno":"Terreno","datum":"Corredor 1 - Datum","nombre":"Volumen sin factores","tabla_progresivas":false,"pedido":"calcula otra vez los volúmenes sin factores y sin tabla"}`.
    Esperado: `ok: true`; `despues` con `factor_corte: 1` y `factor_relleno: 1`; `datos.corredor: "Corredor 1"`,
    `datos.corte` = `datos.sin_ajustar.corte` = el `sin_ajustar.corte` del paso 6 (±0,01) y lo mismo con el relleno;
    `datos.tabla_progresivas: null` y ningún aviso de la tabla. **No llames a nada más antes del paso 12.**

12. **Deshacer (decisivo).** En Civil 3D, `_.UNDO 1` **una vez** (con `ejecutar_comando` `args={"comando":"_.UNDO 1"}` o
    Ctrl+Z). Luego `listar_superficies`:
    - Si **`Volumen sin factores` desapareció** y `Corredor 1 - Volumen` sigue: `_.UNDO 1` deshizo la creación de la
      superficie de volumen.
    - Si **`Volumen sin factores` sigue** en la lista: es un hallazgo importante (como la superficie TIN de la 1.7.0, que
      tampoco se quitaba). Anota su tipo y si se ve en pantalla.
    Este paso responde la pregunta principal del informe.

13. **Cierre.** `leer_log` con `args={"ultimas_n":40}`: una línea `llamada` por cada paso, con `ok:false` en los errores
    controlados (paso 5 salvo el último, paso 10 y, si salió así, el paso 3) y `ok:true` en los demás. No guardes el
    dibujo (es una copia).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_114_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, y las respuestas a estas **preguntas para la memoria técnica**:

1. ¿Qué dio `GetVolumeProperties` (`datos.civil3d` del paso 6) y coincidió con Propiedades de superficie → Estadísticas
   (paso 7)? ¿Qué signo tiene el neto de Civil 3D (`convencion_neto_civil3d`) y cómo lo muestra la interfaz?
2. ¿Los volúmenes ajustados de Civil 3D son exactamente sin ajustar × factor (salió o no el aviso `El corte ajustado de
   Civil 3D ... no es ...`)?
3. ¿`SectionPoint.Location` dio X = desplazamiento e Y = cota (`tabla_progresivas.numeros`, `supuesto_ejes` y el paso 8)?
   Si no, ¿qué valores tenían los puntos (el `motivo`)?
4. ¿Cuánto difiere la tabla por progresivas de la superficie de volumen (`diferencia_con_superficie_pct`)? ¿Hubo tramos
   sin volumen y en qué progresivas?
5. ¿`_.UNDO 1` quitó la superficie de volumen `Volumen sin factores` (paso 12)?
6. El caso del paso 3 (elección automática o error de varias superficies) y la lista de estilos del paso 5.
7. Estilo por defecto con que quedó la superficie de volumen (`despues.estilo`), número de triángulos y tiempos de los
   pasos 6 y 11 (`ms`).

Y la lista de miembros de la sección 1.14.0 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
