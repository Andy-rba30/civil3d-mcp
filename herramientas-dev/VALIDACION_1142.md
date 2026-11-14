# Prompt de validación de la 1.14.2 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Validación corta: solo lo que falló en la ronda del 14/11/2026.** Paso 9 de `VALIDACION_113.md` (segundo grupo de
  líneas de muestreo en el mismo eje: `ArgumentException: Sample line name should not duplicate.`) y la tabla por
  progresivas de `calcular_volumenes` (pasos 6 y 8 de `VALIDACION_114.md` y paso 11 del flujo completo: sin volúmenes
  porque no se encontraba la sección de `Corredor 1 - Datum`). No repite los errores controlados de la 113 ni de la 114.
- Civil 3D 2027 (en español) abierto con **una copia nueva** del dibujo que dejó guardado `VALIDACION_112.md`,
  `C:\Proyectos\dibujos_prueba\PRUEBA_112.dwg`: cópialo como `C:\Proyectos\dibujos_prueba\PRUEBA_1142.dwg` y abre la
  copia. Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`. Ningún dato real.
- **Lo que debe tener el dibujo** (lo dejó guardado la 1.12.0; las validaciones 113, 114 y 1141 **no** se guardaron, así
  que en disco no hay líneas de muestreo ni superficies de volumen; el paso 1 lo comprueba):
  - `Eje 1`: alineamiento de la progresiva `0` a la `831.3274`.
  - `Terreno` (superficie TIN que cubre x 900..1700, y 1900..2400) y `Terreno 2`.
  - `Rasante 1`, `Ensamblaje 1` y `Corredor 1` (de 0 a 831.3274 cada 10 m) con sus superficies `Corredor 1 - Top` y
    `Corredor 1 - Datum`.
  - **Ningún** grupo de líneas de muestreo y **ninguna** superficie `Corredor 1 - Volumen`.
  Si falta algo o sobra un grupo o la superficie de volumen, **para** y anótalo.
- **Instalación.** El plugin lo instala el agente instalador (Prompt L.1) antes de este prompt. En esta ronda lo normal
  es la rama **`tarea/2.9-tabla-pi`**, que trae esta corrección y además `listar_pis` (1.15.0); también vale
  `tarea/2.7-volumenes` sola (1.14.2). El paso 0 comprueba cuál quedó. El puente le da a `crear_secciones` y a
  `calcular_volumenes` hasta **300 s** (si llamas por `curl`, pasa `"timeout_s":300`).
- **Detrás de esta validación va `VALIDACION_115.md` sobre el mismo dibujo**: el paso 11 guarda `PRUEBA_1142.dwg` para
  que la 115 lo reutilice.
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.14.2** (dos correcciones, ninguna herramienta nueva):
  1. **`crear_secciones`, nombres de línea únicos en el dibujo.** Civil 3D no deja repetir el nombre de una línea de
     muestreo en todo el dibujo. Antes de crear (también con `simular=true`) la herramienta lee los nombres de todas las
     líneas y vistas del dibujo (de los grupos de todos los ejes y del espacio modelo). Si ningún nombre por progresiva
     choca, las líneas se llaman como siempre (`0+020.00`); si choca alguno, **todas** las líneas del grupo nuevo llevan
     delante el nombre del grupo (`Muestreo 100 0+100.00`), con un aviso. Las vistas se siguen llamando
     `<grupo> <progresiva>` (`Muestreo 100 0+100.00`), sin repetir el grupo. La respuesta trae `despues.nombres_lineas`,
     `despues.prefijo_lineas` y `datos.nombres`. Si aun así Civil 3D rechazara un nombre, el error ya no es el texto
     crudo de Civil 3D, sino uno que empieza por `Civil 3D no deja crear la línea de muestreo`.
  2. **`calcular_volumenes`, la sección de la Datum.** Civil 3D nombra las secciones de una superficie de corredor
     `"<corredor> <superficie>"` (`Corredor 1 Corredor 1 - Datum`) y su `SourceId` no es la superficie del dibujo, así
     que la 1.14.1 no las encontraba. Ahora se buscan por intentos, en este orden: `SampleLine.GetSectionId`,
     `Section.CorridorSurfaceId`, `Section.SourceId`, el nombre de la superficie y `"<corredor> <superficie>"`. La
     respuesta trae el bloque **`identificacion_secciones`** (en `despues` al simular, en `datos` al escribir) con el
     intento que funcionó en cada línea y los valores crudos de las secciones de la primera línea, para la memoria
     técnica.
  **Los pasos 0, 4, 6, 7, 8 y 9 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.14.2):
  `SampleLine.GetSectionId`, `Section.CorridorSurfaceId`, `Section.SourceId` y `Section.SourceType` en las secciones de
  una superficie de corredor, la unicidad de los nombres de línea en todo el dibujo, el recorrido de todos los ejes y si
  las líneas y las vistas son entidades del espacio modelo.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 8): **"calcula los volúmenes de corte
  y relleno entre el terreno y la rasante del corredor 1, con esponjamiento 1,25 y compactación 1,10, y dame la tabla
  por progresivas"**.

---

Eres el agente de validación de la versión 1.14.2 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell o clics en Civil 3D. Ejecuta los pasos en orden, anota la respuesta
**literal** (JSON completo o, si es muy largo, las primeras 40 líneas; de `datos.tabla_progresivas.filas` basta con la
primera, la de la progresiva 400 y la última; `datos.identificacion_secciones` va **entero**) y **el tiempo total de cada
paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo. En este prompt solo se usa
  para `_.ZOOM`, que es del propio Civil 3D.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Los parámetros de tipo `json` (`punto_insercion`) se pasan como **texto** con el JSON dentro, tal como está escrito.
- **`listar_lineas_muestreo` deja una entrada en el menú Deshacer** (es así desde la 1.2.2, no es fallo): solo se usa en
  el paso 1, antes del recuento del paso 2.
- Los números esperados van en metros con redondeo a 4 decimales; una diferencia menor de 0,001 (o de 0,01 m³ en los
  volúmenes) es OK. El orden de las listas de fuentes puede variar.
- El texto `Sample line name should not duplicate.` **no debe aparecer** en ninguna respuesta: si aparece, es FALLO del
  paso en que salga.

## Pasos

0. **Comprobar la instalación (decisivo).** Con Civil 3D abierto con `PRUEBA_1142.dwg`, `ping` con
   `args={"pedido":"comprueba que el plugin responde"}` y pide la lista de herramientas:
   `ping` responde `1.15.0` y 54 herramientas si se instaló `tarea/2.9-tabla-pi` (lo normal en esta ronda), o `1.14.2`
   y 53 si se instaló `tarea/2.7-volumenes` sola; cualquier otra cosa es FALLO. Anota cuál de los dos casos salió.
   `crear_secciones` y `calcular_volumenes` deben estar en la lista. Si no, para aquí.

1. **Comprobar el dibujo** (no decisivo; solo lecturas).
   - `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}`: `Eje 1` de `0` a `831.3274`.
   - `listar_superficies`: `Terreno`, `Terreno 2`, `Corredor 1 - Top` y `Corredor 1 - Datum` (las dos últimas
     marcadas como de `Corredor 1`); **no** debe estar `Corredor 1 - Volumen`. Anota la lista completa.
   - `listar_corredores`: `Corredor 1` sobre `Eje 1` y `esta_desactualizado: false`.
   - `listar_lineas_muestreo` con `args={"pedido":"lista las líneas de muestreo del dibujo"}` (sin `alineamiento`):
     debe responder `[]`. Si hay algún grupo, **para** y anótalo (la copia no es la de `PRUEBA_112.dwg`).

2. **Punto de partida.** Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene (o la última). Mira cuántos
   archivos hay en `backups\` junto al DWG.

3. **Primer grupo, simulación** (no decisivo). `crear_secciones` con
   `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"filas_por_columna":5,"simular":true,"pedido":"simula las líneas de muestreo cada 20 m con 30 m a cada lado"}`.
   Esperado: `simulado: true`; `despues` con `numero_lineas: 43`, `progresivas` de `0` a `831.3274`, **`nombres_lineas`**
   con 43 nombres sin prefijo (`"0+000.00"`, `"0+020.00"`, …, `"0+820.00"`, `"0+831.33"`), **sin** `prefijo_lineas`
   (no sale porque es `null`) y `numero_vistas: 43`; en `avisos`, **ninguno** que empiece por `Ya hay líneas de
   muestreo`. Sin `copia`, sin entrada nueva en Deshacer y sin archivo nuevo en `backups\`.

4. **Primer grupo, creación real (decisivo).** `crear_secciones` **sin** `simular`:
   `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"filas_por_columna":5,"pedido":"líneas de muestreo cada 20 m con 30 m a cada lado y las vistas de sección en cuadrícula de 5 filas, a la derecha del dibujo"}`.
   Esperado: `ok: true`, `simulado: false`; `despues` **leído del dibujo** con `numero_lineas: 43`, `numero_vistas: 43`
   y `nombres_lineas` igual que en el paso 3; `copia` dentro de `backups\`; `datos.lineas` con `nombre` `0+000.00` …
   `0+831.33` y `secciones` (anota la lista de una línea: deben ser 5, `Terreno`, `Terreno 2`, `Corredor 1`,
   `Corredor 1 Corredor 1 - Top` y `Corredor 1 Corredor 1 - Datum`); `datos.vistas` con `nombre`
   `Eje 1 - Muestreo 0+000.00` …; y **`datos.nombres`** con `prefijo_lineas: null`, `numero_que_chocaban: 0` y `leidos`
   (anótalo **entero**: lo esperado es `lineas: 0` y `vistas: 0`, porque el dibujo no tenía líneas). OK solo si se crean
   las 43 líneas con sus nombres de siempre.

5. **Lista de estilos de vista** (no decisivo; sin tocar nada). `crear_secciones` con
   `args={"alineamiento":"Eje 1","intervalo":100,"ancho_izquierdo":10,"ancho_derecho":25,"nombre":"Muestreo 100","estilo_vista":"NoExiste","simular":true}`
   → `ok: false` y `No existe el estilo de vista de sección 'NoExiste' en el dibujo. Disponibles: <lista>. ...`. Anota
   la lista y elige un estilo **distinto** del `estilo_vista` del paso 3 (si solo hay uno, ese). Luego, la simulación
   del segundo grupo: `crear_secciones` con
   `args={"alineamiento":"Eje 1","nombre":"Muestreo 100","intervalo":100,"ancho_izquierdo":10,"ancho_derecho":25,"filas_por_columna":2,"estilo_vista":"<el estilo elegido>","punto_insercion":"{\"x\":1000,\"y\":500}","simular":true,"pedido":"simula otro grupo de líneas cada 100 m con 10 m a la izquierda y 25 a la derecha"}`.
   Esperado: `ok: true`, `simulado: true`; `despues` con `numero_lineas: 10`, **`prefijo_lineas: "Muestreo 100 "`** y
   **`nombres_lineas`** = `"Muestreo 100 0+000.00"`, `"Muestreo 100 0+100.00"`, …, `"Muestreo 100 0+800.00"`,
   `"Muestreo 100 0+831.33"`; en `avisos`, el de la última línea (`La última línea va en el final del tramo (831.327), a
   31.327 m de la anterior (el intervalo es 100 m).`) y este (literal):
   `Ya hay líneas de muestreo con los nombres '0+000.00', '0+100.00', '0+200.00', '0+300.00', '0+400.00' y 5 más en el dibujo (grupo 'Eje 1 - Muestreo' del eje 'Eje 1') y Civil 3D no deja repetir el nombre de una línea en todo el dibujo: las 10 líneas del grupo 'Muestreo 100' llevan delante el nombre del grupo ('Muestreo 100 0+000.00' … 'Muestreo 100 0+831.33'). Las vistas se siguen llamando '<grupo> <progresiva>'.`
   Si el paréntesis dice `(espacio modelo)` en vez del grupo, anótalo (las líneas se leyeron del espacio modelo y no de
   los ejes). Sin entrada nueva en Deshacer y sin archivo nuevo en `backups\`.

6. **Segundo grupo, creación real (decisivo).** Repite la última llamada del paso 5 **sin** `simular` y con
   `"pedido":"crea otro grupo de líneas cada 100 m con 10 m a la izquierda y 25 a la derecha, vistas en 2 filas debajo del dibujo"`.
   Esperado: `ok: true` (**no** `Sample line name should not duplicate.`); `despues` leído del dibujo con
   `numero_lineas: 10`, `numero_vistas: 10`, `columnas: 5`, `nombres_lineas` igual que en el paso 5, `estilo_vista` = el
   elegido y `punto_insercion` ≈ `{"x": 1000, "y": 500}`; el aviso de nombres del paso 5; `datos.lineas` con `nombre`
   `Muestreo 100 0+000.00` … y **`ancho_izquierdo: 10` y `ancho_derecho: 25`** en todas; `datos.vistas` con `nombre`
   **`Muestreo 100 0+000.00`** … `Muestreo 100 0+831.33` (si alguna sale `Muestreo 100 Muestreo 100 ...`, es FALLO);
   `datos.vistas_colocadas` con `0+000.00` en fila 1, columna 1, `0+100.00` en fila 2, columna 1 y `0+831.33` en fila
   2, columna 5; y **`datos.nombres`** con `prefijo_lineas: "Muestreo 100 "`, `numero_que_chocaban: 10` y `leidos`
   (anótalo **entero**: `lineas: 43`, `lineas_por_ejes: 43`, y cuántas salieron en `lineas_en_espacio_modelo` y
   `vistas_en_espacio_modelo`). Sin `Las vistas quedaron con el estilo ...` en `avisos`.
   **Comprobación del lado** (la pregunta 6 de la 113): con `ejecutar_comando`
   `args={"comando":"_.ZOOM _W 960,1950 1040,2050"}` mira la línea de la progresiva 0 de este grupo (en x = 1000): el eje
   va hacia el este, así que su **izquierda es el norte**: la línea debe ir de y = 2010 (10 m al norte) a y = 1975 (25 m
   al sur). Si va al revés, anótalo (con lo que dice `datos.lineas[0]`). Si puedes, abre en Prospector → Alineaciones →
   `Eje 1` → Grupos de líneas de muestreo → `Muestreo 100` y anota cómo se llaman sus líneas y los anchos de franja
   izquierdo y derecho que muestra Civil 3D. OK si el grupo se crea con las 10 líneas prefijadas y las vistas sin el
   grupo repetido. Si sale un error que empieza por `Civil 3D no deja crear la línea de muestreo`, anótalo entero y
   `leer_historial` con `args={"ultimas_n":10}`: es el hallazgo (el plugin no vio la línea que chocaba).

7. **Volúmenes, simulación (decisivo).** `calcular_volumenes` con
   `args={"corredor":"Corredor 1","terreno":"Terreno","factor_esponjamiento":1.25,"factor_compactacion":1.10,"grupo_muestreo":"Eje 1 - Muestreo","simular":true,"pedido":"simula los volúmenes con esponjamiento 1,25 y compactación 1,10"}`.
   Esperado: `ok: true`, `simulado: true`; `accion` =
   `Crear la superficie de volumen TIN 'Corredor 1 - Volumen' con base 'Terreno' y comparación 'Corredor 1 - Datum', factor de esponjamiento (corte) 1.25 y de compactación (relleno) 1.1, y reconstruirla, con la tabla por progresivas del grupo 'Eje 1 - Muestreo'`;
   `despues` con `grupo_muestreo: "Eje 1 - Muestreo"`, `lineas_tabla: 43` y **`identificacion_secciones`** (anótalo
   **entero**) con `grupo: "Eje 1 - Muestreo"`, `terreno.lineas_sin_seccion: 0` y **`datum.lineas_sin_seccion: 0`**;
   en `avisos`, `Corte, relleno, neto y la tabla por progresivas se calculan al crear la superficie de volumen: ...` y
   **ninguno** que diga `no tiene ninguna línea con secciones de 'Terreno' y de 'Corredor 1 - Datum'`. OK solo si sale
   así, sin `copia`, sin entrada nueva en Deshacer y sin archivo nuevo en `backups\`.

8. **Volúmenes, creación real (decisivo).** `calcular_volumenes` **sin** `simular`:
   `args={"corredor":"Corredor 1","terreno":"Terreno","factor_esponjamiento":1.25,"factor_compactacion":1.10,"grupo_muestreo":"Eje 1 - Muestreo","pedido":"calcula los volúmenes de corte y relleno entre el terreno y la rasante del corredor 1, con esponjamiento 1,25 y compactación 1,10, y dame la tabla por progresivas"}`.
   Esperado: `ok: true`; `despues` con `tipo: "TinVolume"`, `factor_corte: 1.25`, `factor_relleno: 1.1`, `corte`,
   `relleno` y `neto` (anótalos: el dibujo es el mismo que el de la 114, que dio `corte: 9014.1785`,
   `relleno: 32898.6449`, `neto: -23884.4664`; si difieren más de 0,01, anótalo como dato, no como fallo); `copia`
   dentro de `backups\`; y en `datos`:
   - **`tabla_progresivas`** con `grupo: "Eje 1 - Muestreo"`, `numeros: true`, **`supuesto_ejes.comprobado: true`**
     (con `lineas_comprobadas_con_la_superficie: 1` y `motivo: null`), `numero_lineas: 43`, `tramos: 42` y
     **`tramos_sin_volumen: 0`** (o solo tramos cuya fila tenga un `falta` que diga el motivo: anótalos); `totales`
     (anótalos); `diferencia_con_superficie_pct` (anótala: es un **dato** para la memoria técnica, no un OK/FALLO); y
     `filas`: la primera (`pk: 0`), la de `pk: 400` y la última (`pk: 831.3274`), **sin** `falta: "falta la sección de
     'Corredor 1 - Datum'"` en ninguna;
   - **`identificacion_secciones`** (anótalo **entero**): `terreno.metodos` y `datum.metodos` (con qué intento se
     encontró cada sección y en cuántas líneas), `lineas_sin_seccion` y `primera_linea` con `get_section_id` y las 5
     `secciones` (`source_name`, `source_type`, `source_id`, `corridor_surface_id`, `es`).
   - `avisos`: anota literal cualquiera (por ejemplo `La tabla por progresivas difiere más de un 10 % ...`, que aquí es
     un dato).
   OK solo si `tramos_sin_volumen` es `0` (o solo los que tengan su motivo escrito), `supuesto_ejes.comprobado` es
   `true` y el bloque `identificacion_secciones` está presente. Si `tabla_progresivas` sale `null` o con
   `numeros: false`, es FALLO: anota el aviso, `supuesto_ejes.motivo` e `identificacion_secciones`.

9. **Contraste en la progresiva 400 (decisivo).** Lecturas:
   - `pk_a_punto` con `args={"alineamiento":"Eje 1","pk":400}` → `x: 1300`, `y: 2134.3363`.
   - `cota_superficie` con `args={"superficie":"Terreno","x":1300,"y":2134.3363}` → `104.1867`, y con
     `args={"superficie":"Corredor 1 - Datum","x":1300,"y":2134.3363}` → `109.0011` (en el eje la Datum está
     `4.8144` m por encima del terreno: hay que **rellenar**).
   - La fila `pk: 400` del paso 8 debe tener **`area_relleno` mayor que 0** (anota `area_corte` y `area_relleno`).
   OK si `area_relleno` es mayor que 0. Si es 0 y `area_corte` mayor que 0, el signo está cambiado: es el hallazgo
   principal de la tabla.

10. **Deshacer y copias.** Abre el menú Deshacer: respecto al paso 2 debe haber **tres** entradas nuevas
    (`Executefunction`), las de los pasos 4, 6 y 8 (el `_.ZOOM` del paso 6 añade la suya, que es del propio Civil 3D:
    anótala aparte); los pasos 3, 5, 7 y 9 no añaden ninguna. En `backups\`, un archivo nuevo por escritura
    (`..._crear_secciones.dwg` y `..._calcular_volumenes.dwg`; si `copia.reutilizada` es `true`, no hay archivo nuevo:
    anótalo, no es fallo). No hagas `_.UNDO`.

11. **Guardar para la 115** (no decisivo, pero **obligatorio**). `guardar_dibujo` con
    `args={"pedido":"guarda el dibujo para la validación siguiente"}`. Esperado: `mensaje: "Dibujo guardado en <ruta de PRUEBA_1142.dwg>"`.
    Si da error, anota el error literal y **pide a Jose que guarde con Ctrl+S**. Anota en el informe que
    `PRUEBA_1142.dwg` queda guardado con `Eje 1 - Muestreo`, `Muestreo 100` y `Corredor 1 - Volumen`: es el punto de
    partida de `VALIDACION_115.md`.

12. **Cierre.** `leer_log` con `args={"ultimas_n":30}`: una línea `llamada` por cada paso, con `ok:false` solo en la
    primera llamada del paso 5 (estilo `NoExiste`) y `ok:true` en las demás.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_1142_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, rama
instalada (`tarea/2.9-tabla-pi` o `tarea/2.7-volumenes`) y versión de `ping`, DWG usado (solo el nombre genérico, sin
ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal / tiempo, y las respuestas a estas
**preguntas para la memoria técnica**:

1. ¿Qué nombres quedaron en las líneas de los dos grupos (pasos 4 y 6) y cómo los muestra Prospector? ¿Salió el aviso de
   nombres del paso 5 literal? ¿Qué dio `datos.nombres.leidos` en el paso 6: cuántas líneas y vistas se leyeron por los
   ejes y cuántas en el espacio modelo (¿son las líneas y las vistas entidades del espacio modelo?)?
2. ¿Los anchos quedaron en el lado correcto (paso 6: 10 m al norte y 25 m al sur en la progresiva 0)? ¿Qué anchos
   mostró Civil 3D en las propiedades de la línea? (pregunta 6 de la 113)
3. El estilo de vista de sección por defecto (`despues.estilo_vista` del paso 3), la lista de estilos del paso 5 y si
   `estilo_vista` se aplicó en el paso 6. (pregunta 7 de la 113)
4. `identificacion_secciones` del paso 8: ¿con qué intento se encontró la sección del terreno y la de la Datum
   (`metodos`)? ¿Qué devolvió `SampleLine.GetSectionId` en la primera línea? Para cada una de sus 5 secciones:
   `source_name`, `source_type` (texto de `SectionSourceType`), el objeto de `source_id` y el de
   `corridor_surface_id` (nombre y tipo). ¿Coincidió con la simulación del paso 7?
5. Tabla por progresivas del paso 8: `totales`, `diferencia_con_superficie_pct` (corte y relleno) y, si los hubo, los
   tramos sin volumen y su `falta`.
6. Paso 9: `area_corte` y `area_relleno` de la fila `pk: 400` frente a las cotas (Terreno `104.1867`, Datum `109.0011`).
7. Tiempos de los pasos 4, 6 y 8 (`ms`).

Y la lista de miembros de la sección 1.14.2 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
