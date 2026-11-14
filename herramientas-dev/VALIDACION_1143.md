# Prompt de validación de la 1.14.3 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Validación muy corta: solo lo que falló en `VALIDACION_1142.md`** (paso 8, 14/11/2026): con la sección de la Datum
  ya encontrada, la tabla por progresivas de `calcular_volumenes` salió con `numeros: false` y `motivo: "un punto de la
  sección de 'Corredor 1 - Datum' tiene X = -30, fuera de los desplazamientos de la sección (-3.9 a 3.6)"`. No repite
  `crear_secciones`.
- Civil 3D 2027 (en español) abierto con **`C:\Proyectos\dibujos_prueba\PRUEBA_1142.dwg` tal como lo dejó guardado la
  1.14.2** (si prefieres no tocarlo, abre una copia y anótalo). Anota la carpeta del DWG: ahí aparecerán
  `mcp_log.jsonl` y `backups\`. Ningún dato real.
- **Lo que debe tener el dibujo** (el paso 1 lo comprueba): `Eje 1` (de `0` a `831.3274`), `Terreno`, `Terreno 2`,
  `Corredor 1` con `Corredor 1 - Top` y `Corredor 1 - Datum`, los grupos de líneas de muestreo **`Eje 1 - Muestreo`**
  (43 líneas cada 20 m, 30/30) y `Muestreo 100` (10 líneas), la superficie de volumen `Corredor 1 - Volumen` y el
  `Eje 115` de `VALIDACION_115.md`. **No** debe haber ninguna superficie `Volumen 1143`. Si falta algo, **para** y
  anótalo.
- **Instalación.** El plugin lo instala el agente instalador (Prompt L.1) antes de este prompt: lo normal en esta ronda
  es la rama **`tarea/2.9-tabla-pi`** (1.15.1, con `listar_pis`); también vale `tarea/2.7-volumenes` sola (1.14.3). El
  puente le da a `calcular_volumenes` hasta **300 s** (si llamas por `curl`, pasa `"timeout_s":300`).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.14.3** (una corrección, ninguna herramienta nueva): la tabla por progresivas ya no comprueba los
  puntos de cada sección con `Section.LeftOffset`/`RightOffset` (en la Datum no son sus extremos). Bloquean la tabla
  solo dos cosas:
  1. un punto fuera del **ancho real de la línea de muestreo** (30 m a cada lado en `Eje 1 - Muestreo`);
  2. en la primera línea en que se pueda, la **cota de la sección** del terreno o de la Datum **distinta (±0,01) de la
     de su superficie** en el eje y a 5 m a cada lado (así se comprueba también el signo izquierda/derecha).

  `LeftOffset`/`RightOffset` y las cotas mínima y máxima pasan a un aviso y al bloque nuevo **`puntos_secciones`**
  (en `despues` al simular, en `datos` al escribir), que deja escrito qué devuelve Civil 3D. **Los pasos 0, 3, 4 y 5
  son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.14.3):
  `Section.LeftOffset`/`RightOffset`, `LeftSwathWidth`/`RightSwathWidth` y `MinmumElevation`/`MaximumElevation` (qué
  miden), y el supuesto de `SectionPoint.Location` (X = desplazamiento, negativo a la izquierda; Y = cota).
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 4): **"calcula otra vez los volúmenes
  de corte y relleno entre el terreno y la rasante del corredor 1, con esponjamiento 1,25 y compactación 1,10, y dame la
  tabla por progresivas"**.

---

Eres el agente de validación de la versión 1.14.3 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Usa las herramientas MCP **por su nombre** (los argumentos van dentro de `args`). Ejecuta los pasos en orden, anota la
respuesta **literal** (JSON completo o, si es muy largo, las primeras 40 líneas; de `datos.tabla_progresivas.filas`
basta con la primera, la de la progresiva 400 y la última; **`puntos_secciones` va entero**) y **el tiempo total de cada
paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- **`listar_lineas_muestreo` deja una entrada en el menú Deshacer** (es así desde la 1.2.2, no es fallo): solo se usa en
  el paso 1, antes del recuento del paso 2.
- Los volúmenes salen en m³; una diferencia menor de 0,01 m³ es OK. Las cotas, en metros con 4 decimales.

## Pasos

0. **Comprobar la instalación (decisivo).** `ping` con `args={"pedido":"comprueba que el plugin responde"}` y la lista
   de herramientas: `ping` responde `1.15.1` y 54 herramientas si se instaló `tarea/2.9-tabla-pi` (lo normal en esta
   ronda), o `1.14.3` y 53 si se instaló `tarea/2.7-volumenes` sola; cualquier otra cosa es FALLO. Anota cuál salió.

1. **Comprobar el dibujo** (no decisivo; solo lecturas).
   - `listar_superficies` con `args={"pedido":"lista las superficies"}`: `Terreno`, `Terreno 2`, `Corredor 1 - Top`,
     `Corredor 1 - Datum` y `Corredor 1 - Volumen` (tipo `TinVolume`); **no** `Volumen 1143`.
   - `listar_lineas_muestreo` con `args={"alineamiento":"Eje 1","pedido":"lista las líneas de muestreo del Eje 1"}`:
     `Eje 1 - Muestreo` con `n_lineas: 43` y `Muestreo 100` con `n_lineas: 10`.
   Si falta algo, **para** (ver requisitos).

2. **Punto de partida.** Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene (o la última). Mira cuántos
   archivos hay en `backups\` junto al DWG.

3. **Simulación (decisivo).** `calcular_volumenes` con
   `args={"nombre":"Volumen 1143","terreno":"Terreno","corredor":"Corredor 1","grupo_muestreo":"Eje 1 - Muestreo","factor_esponjamiento":1.25,"factor_compactacion":1.10,"simular":true,"pedido":"simula otra vez los volúmenes con esponjamiento 1,25 y compactación 1,10"}`.
   Esperado: `ok: true`, `simulado: true`; `accion` =
   `Crear la superficie de volumen TIN 'Volumen 1143' con base 'Terreno' y comparación 'Corredor 1 - Datum', factor de esponjamiento (corte) 1.25 y de compactación (relleno) 1.1, y reconstruirla, con la tabla por progresivas del grupo 'Eje 1 - Muestreo'`;
   `despues` con `grupo_muestreo: "Eje 1 - Muestreo"`, `lineas_tabla: 43`, `identificacion_secciones` (43 de 43 en el
   terreno y en la Datum, como en la 1.14.2), **`supuesto_ejes`** con **`comprobado: true`**,
   `lineas_comprobadas_con_la_superficie: 1` y `motivo: null`, y **`puntos_secciones`** (anótalo **entero**): `linea`,
   `pk`, `ancho_linea` (`izquierdo: 30`, `derecho: 30`), `terreno` y `datum` (cada uno con `left_offset`,
   `right_offset`, `left_swath_width`, `right_swath_width`, `minmum_elevation`, `maximum_elevation`, `numero_puntos`,
   `primeros` y `ultimos`), `cotas_comparadas` (desplazamientos `0`, `-5` y `5` para las dos superficies, con
   `diferencia` en ±0,01) y `comprobacion_completa: true`. En `avisos`: `Corte, relleno, neto y la tabla por progresivas
   se calculan al crear ...` y, probablemente, uno que empieza por `En la línea '` y termina en `no bloquea la tabla, se
   anota en puntos_secciones para miembros_por_verificar_civil3d.md.` (anótalo literal: es lo esperado). **No** debe salir
   `La tabla por progresivas saldrá sin números`. Sin `copia`, sin entrada nueva en Deshacer y sin archivo nuevo en
   `backups\`. OK solo si `supuesto_ejes.comprobado` es `true` y `puntos_secciones` está.

4. **Creación real (decisivo).** `calcular_volumenes` **sin** `simular`:
   `args={"nombre":"Volumen 1143","terreno":"Terreno","corredor":"Corredor 1","grupo_muestreo":"Eje 1 - Muestreo","factor_esponjamiento":1.25,"factor_compactacion":1.10,"pedido":"calcula otra vez los volúmenes de corte y relleno entre el terreno y la rasante del corredor 1, con esponjamiento 1,25 y compactación 1,10, y dame la tabla por progresivas"}`.
   Esperado: `ok: true`; `despues` con `superficie: "Volumen 1143"`, `tipo: "TinVolume"`, `factor_corte: 1.25`,
   `factor_relleno: 1.1` y `corte`, `relleno` y `neto` iguales a los de la 1.14.2 (`9014.1785`, `32898.6449`,
   `-23884.4664`; si difieren más de 0,01, anótalo como dato); `copia` dentro de `backups\`; y en `datos`:
   - **`tabla_progresivas`** con `numeros: true`, **`supuesto_ejes.comprobado: true`** (`motivo: null`),
     `numero_lineas: 43`, `tramos: 42`, **`tramos_sin_volumen: 0`** (o solo tramos cuya fila tenga un `falta` que diga el
     motivo: anótalos), `totales` (anótalos), `diferencia_con_superficie_pct` (anótala: es un **dato**, no un OK/FALLO) y
     `filas` (la primera, la de `pk: 400` y la última, con `masa` igual a `totales.neto`);
   - **`puntos_secciones`** (anótalo **entero**; debe coincidir con el del paso 3) e `identificacion_secciones`.
   - `avisos`: anota literal cualquiera (el de `LeftOffset`/`RightOffset` del paso 3; `La tabla por progresivas difiere
     más de un 10 % ...` es un dato).
   OK solo si `tramos_sin_volumen` es `0` (o solo los que tengan su motivo escrito), `supuesto_ejes.comprobado` es
   `true` y `puntos_secciones` está. Si `numeros` sale `false`, es FALLO: anota `supuesto_ejes.motivo`,
   `puntos_secciones` y el aviso.

5. **Contraste en la progresiva 400 (decisivo).** `pk_a_punto` con `args={"alineamiento":"Eje 1","pk":400}` →
   `x: 1300`, `y: 2134.3363`; `cota_superficie` con `args={"superficie":"Terreno","x":1300,"y":2134.3363}` → `104.1867`
   y con `args={"superficie":"Corredor 1 - Datum","x":1300,"y":2134.3363}` → `109.0011` (en el eje la Datum está
   `4.8144` m por encima del terreno: hay que **rellenar**). La fila `pk: 400` del paso 4 debe tener **`area_relleno`
   mayor que 0** (anota `area_corte` y `area_relleno`). OK si es así. Si `area_relleno` es 0 y `area_corte` mayor que 0,
   el signo está cambiado: es el hallazgo principal.

6. **Deshacer y copias.** Respecto al paso 2, **una** entrada nueva `Executefunction` (la del paso 4) en el menú
   Deshacer, y en `backups\` un archivo nuevo `..._calcular_volumenes.dwg` (si `copia.reutilizada` es `true`, no hay
   archivo nuevo: anótalo, no es fallo). No hagas `_.UNDO` y **no guardes el dibujo**.

7. **Cierre.** `leer_log` con `args={"ultimas_n":15}`: una línea `llamada` por cada paso, todas con `ok:true`.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_1143_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, rama
instalada y versión de `ping`, DWG usado (solo el nombre genérico, sin ruta ni datos de obra), una tabla paso /
resultado (OK, FALLO) / respuesta literal / tiempo, y las respuestas a estas **preguntas para la memoria técnica**:

1. `puntos_secciones` del paso 4, entero: de la sección del terreno y de la de la Datum, ¿qué valen `left_offset`,
   `right_offset`, `left_swath_width`, `right_swath_width`, `minmum_elevation` y `maximum_elevation`, cuántos puntos
   tienen y cuáles son los 3 primeros y los 3 últimos? ¿Qué parecen medir `LeftOffset`/`RightOffset` y
   `LeftSwathWidth`/`RightSwathWidth` (el ancho de la línea, el de la calzada…)?
2. `cotas_comparadas`: ¿la cota de las secciones coincidió con la de las superficies en el eje y a 5 m a cada lado (sí
   o no, con las diferencias)? ¿Confirma que X es el desplazamiento con la izquierda negativa e Y la cota?
3. Tabla por progresivas: `totales`, `diferencia_con_superficie_pct` (corte y relleno) y, si los hubo, los tramos sin
   volumen y su `falta`.
4. Paso 5: `area_corte` y `area_relleno` de la fila `pk: 400` frente a las cotas (Terreno `104.1867`, Datum `109.0011`).
5. Tiempos de los pasos 3 y 4 (`ms`).

Y la lista de miembros de la sección 1.14.3 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
