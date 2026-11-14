# Prompt de validación de la 1.17.1 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Validación muy corta: solo la tabla por progresivas de `calcular_volumenes`.** En `VALIDACION_1143.md` (informe del
  14/11/2026) la tabla ya salió con números, pero distinta de la superficie de volumen: corte `8407.0288` frente a
  `7211.3428` sin ajustar (**+16,58 %**) y relleno `31144.4647` frente a `29907.859` (**+4,13 %**). La causa probable: la
  sección de la Datum llega a ±30 m aunque su superficie TIN termine antes, y la tabla contaba también esa parte. No se
  repite `crear_secciones`.
- Civil 3D 2027 (en español) abierto con **`C:\Proyectos\dibujos_prueba\PRUEBA_1142.dwg` tal como está guardado en
  disco** (si prefieres no tocarlo, abre una copia y anótalo). Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y
  `backups\`. Ningún dato real.
- **Lo que debe tener el dibujo** (el paso 1 lo comprueba): `Terreno`, `Corredor 1` con `Corredor 1 - Datum`, el grupo de
  líneas de muestreo **`Eje 1 - Muestreo`** (43 líneas) y `Muestreo 100` en `Eje 1`, y la superficie de volumen
  `Corredor 1 - Volumen`. **No** debe haber ninguna superficie `Volumen 1171`. Si falta algo, **para** y anótalo.
- **Instalación.** El plugin lo instala el agente instalador (Prompt L.1) antes de este prompt: lo normal es la **rama
  integrada de la ronda B** (con `ejecutar_codigo` y las recetas), que lleva esta corrección. El puente le da a
  `calcular_volumenes` hasta **300 s** (si llamas por `curl`, pasa `"timeout_s":300`).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.17.1** (una corrección, ninguna herramienta nueva): el área de cada línea de la tabla se cuenta
  **solo en los tramos de desplazamiento en que existen la Datum y el terreno**, como hace la superficie de volumen. En
  cada línea el plugin pregunta la cota de las dos superficies (`FindElevationAtXY`) en unos 40 desplazamientos y afina
  cada borde hasta 0,01 m. La respuesta trae dos bloques nuevos: **`tabla_progresivas.recorte`** (cuántas líneas se
  recortaron y cuánto ancho se descartó) y **`puntos_secciones.tramo_valido`** (los tramos válidos de la línea
  comprobada), y cada fila de la tabla trae `ancho_descartado`. La comprobación del supuesto de ejes de la 1.14.3 no
  cambia. **Los pasos 0, 3 y 4 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.17.1):
  `Surface.FindElevationAtXY` como límite de la superficie en todo el ancho de la línea, `Alignment.PointLocation` hasta
  ±30 m, el supuesto de líneas perpendiculares al eje y la hipótesis de que el exceso de la tabla era el área fuera de la
  Datum.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 4): **"calcula los volúmenes de corte
  y relleno entre el terreno y la rasante del corredor 1, sin factores, y dame la tabla por progresivas"**.

---

Eres el agente de validación de la versión 1.17.1 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Usa las herramientas MCP **por su nombre** (los argumentos van dentro de `args`). Ejecuta los pasos en orden, anota la
respuesta **literal** (JSON completo o, si es muy largo, las primeras 40 líneas; de `datos.tabla_progresivas.filas` basta
con la primera, la de la progresiva 400 y la última; **`tabla_progresivas.recorte` y `puntos_secciones` van enteros**) y
**el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` ni `ejecutar_codigo` para "arreglar" nada: si algo no funciona, es un hallazgo.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- **`listar_lineas_muestreo` deja una entrada en el menú Deshacer** (es así desde la 1.2.2, no es fallo): solo se usa en
  el paso 1, antes del recuento del paso 2.
- Los volúmenes salen en m³; una diferencia menor de 0,01 m³ es OK. Las cotas y los anchos, en metros con 4 decimales.

## Pasos

0. **Comprobar la instalación (decisivo).** `ping` con `args={"pedido":"comprueba que el plugin responde"}` y la lista
   de herramientas: `ping` da **`1.17.2`** si se instaló la rama integrada de la ronda B (lo normal), o **`1.17.1` o
   superior** en cualquier caso; una versión menor es FALLO. Anota la versión y el número de herramientas (debe incluir
   `calcular_volumenes`).

1. **Comprobar el dibujo** (no decisivo; solo lecturas).
   - `listar_superficies` con `args={"pedido":"lista las superficies"}`: `Terreno`, `Corredor 1 - Datum` y
     `Corredor 1 - Volumen` (tipo `TinVolume`); **no** `Volumen 1171`.
   - `listar_lineas_muestreo` con `args={"alineamiento":"Eje 1","pedido":"lista las líneas de muestreo del Eje 1"}`:
     `Eje 1 - Muestreo` con `n_lineas: 43`.
   Si falta algo, **para** (ver requisitos).

2. **Punto de partida.** Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene (o la última). Mira cuántos
   archivos hay en `backups\` junto al DWG.

3. **Simulación (decisivo).** `calcular_volumenes` con
   `args={"nombre":"Volumen 1171","terreno":"Terreno","corredor":"Corredor 1","grupo_muestreo":"Eje 1 - Muestreo","factor_esponjamiento":1,"factor_compactacion":1,"simular":true,"pedido":"simula los volúmenes sin factores"}`.
   Esperado: `ok: true`, `simulado: true`; `accion` =
   `Crear la superficie de volumen TIN 'Volumen 1171' con base 'Terreno' y comparación 'Corredor 1 - Datum', factor de esponjamiento (corte) 1 y de compactación (relleno) 1, y reconstruirla, con la tabla por progresivas del grupo 'Eje 1 - Muestreo'`;
   `despues` con `grupo_muestreo: "Eje 1 - Muestreo"`, `lineas_tabla: 43`, **`supuesto_ejes`** con
   **`comprobado: true`** (`motivo: null`, como en la 1.14.3), y **`puntos_secciones`** (anótalo **entero**) con su
   campo nuevo **`tramo_valido`**: `desde_comun` (`-30`), `hasta_comun` (`30`), `tramos` (lista de `{desde, hasta}`),
   `ancho_valido`, `ancho_descartado` y `puntos_probados`. Lo probable es un tramo estrecho alrededor del eje: en la
   1.14.3 la Datum tenía cota en el eje (`99.55`) y no a ±5 m. Sin `copia`, sin entrada nueva en Deshacer y sin archivo
   nuevo en `backups\`. OK solo si `supuesto_ejes.comprobado` es `true` y `puntos_secciones.tramo_valido` está.

4. **Creación real (decisivo).** `calcular_volumenes` **sin** `simular`:
   `args={"nombre":"Volumen 1171","terreno":"Terreno","corredor":"Corredor 1","grupo_muestreo":"Eje 1 - Muestreo","factor_esponjamiento":1,"factor_compactacion":1,"pedido":"calcula los volúmenes de corte y relleno entre el terreno y la rasante del corredor 1, sin factores, y dame la tabla por progresivas"}`.
   Esperado: `ok: true`; `despues` con `superficie: "Volumen 1171"`, `tipo: "TinVolume"`, `factor_corte: 1`,
   `factor_relleno: 1` y `corte`, `relleno` y `neto` iguales a los sin ajustar de la 1.14.3 (`7211.3428`, `29907.859`,
   `-22696.5162`; si difieren más de 0,01, anótalo como dato); `copia` dentro de `backups\`; y en `datos`:
   - **`tabla_progresivas`** con `numeros: true`, **`supuesto_ejes.comprobado: true`**, `numero_lineas: 43`,
     `tramos: 42`, **`tramos_sin_volumen: 0`**, el bloque nuevo **`recorte`** (anótalo **entero**: `metodo`,
     `precision`, `paso_maximo`, `lineas`, `lineas_recortadas`, `lineas_sin_tramo_valido`, `lineas_con_varios_tramos`,
     `ancho_medio_descartado`, `ancho_maximo_descartado`, `puntos_probados`), `totales` (anótalos),
     **`diferencia_con_superficie_pct`** (anótala y compárala con la de la 1.14.3: corte `16.5806`, relleno `4.1347`;
     el porcentaje no depende de los factores, así que la comparación vale aunque aquí sean 1) y `filas` (la primera,
     la de `pk: 400` y la última, con `area_corte`, `area_relleno`, `ancho_descartado` y `masa`; la última `masa` debe
     ser igual a `totales.neto`);
   - **`puntos_secciones`** (anótalo **entero**; su `tramo_valido` debe coincidir con el del paso 3);
   - `avisos`: anota literal cualquiera (el de `LeftOffset`/`RightOffset` de la 1.14.3 seguirá saliendo; si sale
     `La tabla por progresivas difiere más de un 10 % ...` o una línea sin tramo válido, es un dato).
   OK solo si `tramos_sin_volumen` es `0`, `supuesto_ejes.comprobado` es `true` y el bloque `recorte` está.
   **Lo esperado es que `diferencia_con_superficie_pct` sea claramente menor** que la de la 1.14.3 (en corte y en
   relleno). No hay un número de OK estricto, pero **si no baja, dilo expresamente en el informe** con los dos valores.
   Si `numeros` sale `false`, es FALLO: anota `supuesto_ejes.motivo`, `puntos_secciones` y los avisos.

5. **Deshacer y copias.** Respecto al paso 2, **una** entrada nueva `Executefunction` (la del paso 4) en el menú
   Deshacer, y en `backups\` un archivo nuevo `..._calcular_volumenes.dwg` (si `copia.reutilizada` es `true`, no hay
   archivo nuevo: anótalo, no es fallo). No hagas `_.UNDO` y **no guardes el dibujo**.

6. **Cierre.** `leer_log` con `args={"ultimas_n":15}`: una línea `llamada` por cada paso, todas con `ok:true`.

## Informe

**Guarda el informe como archivo** en `herramientas-dev/informes/informe_validacion_1171_<AAAAMMDD>.md` (no basta con
escribirlo en el chat). Debe llevar: versión e idioma de Civil 3D, rama instalada y versión de `ping`, DWG usado (solo el
nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal / tiempo, y las
respuestas a estas **preguntas para la memoria técnica**:

1. `tabla_progresivas.recorte` del paso 4, entero: ¿cuántas líneas se recortaron, cuántas quedaron sin tramo válido y
   cuál es el ancho medio y el máximo descartado?
2. `puntos_secciones.tramo_valido` de los pasos 3 y 4: ¿coinciden?, ¿cuántos tramos tiene la línea 0+000.00 y dónde
   empiezan y terminan?
3. `diferencia_con_superficie_pct` nueva frente a la de la 1.14.3 (corte `16.5806`, relleno `4.1347`), y `totales` de la
   tabla frente a `corte` y `relleno` de la superficie. ¿Bajó claramente? Si no bajó, dilo.
4. Filas `0`, `400` y última: `area_corte`, `area_relleno` y `ancho_descartado`, frente a las de la 1.14.3 (`5.1339` /
   `7.521`, `0` / `95.2254` y `72.4808` / `0`). En la fila `400` `area_relleno` debe seguir siendo mayor que 0.
5. Tiempos de los pasos 3 y 4 (`ms`), frente a los de la 1.14.3 (`108 ms` y `421 ms`), y `recorte.puntos_probados`.

Y la lista de miembros de la sección 1.17.1 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con rutas
reales**: sustitúyelas por `<ruta>`.
