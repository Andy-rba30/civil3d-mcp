# Prompt de validación de la 1.19.3 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Validación corta: solo lo que cambia en la 1.19.3** (una corrección de `crear_secciones`, ninguna herramienta nueva):
  1. **Nombre del grupo en otro eje.** Civil 3D no deja repetir el nombre de una **línea** de muestreo en todo el dibujo
     (validado el 14/11/2026). No se sabe si deja repetir el nombre de un **grupo** de líneas de muestreo que ya existe
     en **otro** eje: **esa es la pregunta de esta validación**. Desde la 1.19.3, `crear_secciones` lee los grupos de
     todos los ejes (también al simular). Si el nombre pedido ya lo usa un grupo de otro eje, avisa y prueba con ese
     nombre:
     - si Civil 3D lo acepta, el grupo se llama como se pidió;
     - si Civil 3D lo rechaza por nombre repetido **sin haber creado nada**, el grupo se crea en la misma operación como
       `<eje> - <nombre>` (`Eje 2 - Muestreo 100`), con sus líneas, y con **una sola** entrada en Deshacer;
     - en cualquier otro caso, error sin tocar nada.
     La respuesta lo dice en el bloque nuevo `datos.nombre_grupo` (`rechazado_por_civil3d` y `texto_civil3d`).
  2. **Superficie del dibujo de cada sección.** Cada línea de `datos.lineas` trae, además de `secciones` (la lista de
     textos de siempre, que no cambia), la lista nueva **`secciones_detalle`**: por cada sección, `source_name` (lo que
     dice Civil 3D: en las superficies de corredor es `"<corredor> <superficie>"`), `superficie` (el nombre de la
     superficie del dibujo), `clase` y `metodo` (cómo se reconoció; lo normal es `SampleLine.GetSectionId`, la vía
     validada en `calcular_volumenes`). Y el resumen nuevo `datos.secciones_identificadas`.
  **Los pasos 0, 3, 6, 7 y 10 son los decisivos.**
- Civil 3D 2027 (en español) abierto con **una copia nueva** del dibujo que dejó guardado `VALIDACION_112.md`,
  `C:\Proyectos\dibujos_prueba\PRUEBA_112.dwg`: cópialo como `C:\Proyectos\dibujos_prueba\PRUEBA_1193.dwg` y abre la
  copia. Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`. Ningún dato real.
- **Lo que debe tener el dibujo** (el paso 1 lo comprueba):
  - `Eje 1`: alineamiento de la progresiva `0` a la `831.3274`, creado desde la polilínea
    `1000,2000 → 1300,2000 → 1300,2300 → 1600,2300` de la capa `EJE_PRUEBA` con radio 80.
  - `Terreno` (superficie TIN que cubre x 900..1700, y 1900..2400) y `Terreno 2`.
  - `Corredor 1` sobre `Eje 1`, con sus superficies `Corredor 1 - Top` y `Corredor 1 - Datum`.
  - **Ningún** grupo de líneas de muestreo, **ningún** alineamiento `Eje 2` y **ninguna** superficie `Corredor 1 - Volumen`.
  Si falta algo o sobra un grupo, `Eje 2` o la superficie de volumen, **para** y anótalo.
- **Instalación.** El plugin lo instala el agente instalador (Prompt L.1) antes de este prompt, desde la rama
  **`tarea/6.3-secciones-grupos`** (1.19.3), o desde la rama de la ronda que junte esta con otras si así lo indica el
  PR. El paso 0 comprueba cuál quedó. El puente le da a `crear_secciones` hasta **300 s** (si llamas por `curl`, pasa
  `"timeout_s":300`).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.19.3):
  `SampleLineGroup.Create` con un nombre que ya usa un grupo de otro eje (lo acepta o lo rechaza, y con qué texto); si
  un `SampleLineGroup.Create` rechazado deja algo en el eje; `SampleLine.GetSectionId` con todas las superficies
  muestreadas en `crear_secciones`; y `Section.CorridorSurfaceId`.
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 7): **"crea en el eje 2 un grupo de
  líneas de muestreo llamado Muestreo 100, cada 100 m con 20 m a cada lado, solo del terreno y sin vistas"**.

---

Eres el agente de validación de la versión 1.19.3 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican clics en Civil 3D. Ejecuta los pasos en orden, anota la respuesta **literal**
(JSON completo o, si es muy largo, las primeras 40 líneas; `datos.nombre_grupo`, `datos.secciones_identificadas` y la
**primera línea** de `datos.lineas` van **enteros**) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` se usa **solo** donde este prompt lo indica (dibujar la polilínea del paso 5 si falta y el
  `_.UNDO 2` del paso 11). **Ningún zoom, encuadre ni regeneración** entre el paso 7 y el paso 11: el zoom deja su
  propia entrada en Deshacer y el `_.UNDO` desharía esa.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Los parámetros de tipo `json` (`fuentes`) se pasan como **texto** con el JSON dentro, tal como está escrito.
- **`listar_lineas_muestreo` deja una entrada `Executefunction` en el menú Deshacer** (corre como un comando; es así desde
  la 1.2.2, no es fallo): se usa en el paso 1, antes del recuento del paso 2, y en los pasos 10 y 12, que cuentan con
  ella.
- Los números esperados van en metros con redondeo a 4 decimales; una diferencia menor de 0,001 es OK. El orden de las
  listas de fuentes y de secciones puede variar.

## Pasos

0. **Comprobar la instalación (decisivo).** Con Civil 3D abierto con `PRUEBA_1193.dwg`, `ping` con
   `args={"pedido":"comprueba que el plugin responde"}` y pide la lista de herramientas: `ping` responde
   **`"version": "1.19.3"`** con 55 herramientas del plugin (si se instaló la rama de una ronda que junta esta con
   otras, la versión de esa rama: anótala). `crear_secciones`, `crear_alineamiento_desde_polilinea` y
   `listar_lineas_muestreo` deben estar en la lista. Si no, para aquí.

1. **Comprobar el dibujo** (no decisivo; solo lecturas).
   - `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}`: **solo** `Eje 1`, de `0` a `831.3274`.
   - `listar_superficies`: `Terreno`, `Terreno 2`, `Corredor 1 - Top` y `Corredor 1 - Datum` (las dos últimas
     marcadas como de `Corredor 1`); **no** debe estar `Corredor 1 - Volumen`. Anota la lista completa.
   - `listar_corredores`: `Corredor 1` sobre `Eje 1`.
   - `listar_lineas_muestreo` con `args={"pedido":"lista las líneas de muestreo del dibujo"}` (sin `alineamiento`):
     debe responder `[]`. Si hay algún grupo, **para** y anótalo (la copia no es la de `PRUEBA_112.dwg`).

2. **Punto de partida.** Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene (o la última). Mira cuántos
   archivos hay en `backups\` junto al DWG.

3. **Primer grupo en el Eje 1: superficie de cada sección (decisivo).** `crear_secciones` con
   `args={"alineamiento":"Eje 1","intervalo":20,"ancho_izquierdo":30,"ancho_derecho":30,"crear_vistas":false,"pedido":"líneas de muestreo cada 20 m con 30 m a cada lado en el eje 1, sin vistas"}`.
   Esperado: `ok: true`, `simulado: false`; `despues` leído del dibujo con `nombre: "Eje 1 - Muestreo"`,
   `numero_lineas: 43`, `numero_vistas: 0`, `nombres_lineas` de `"0+000.00"` a `"0+831.33"` sin prefijo y
   `fuentes_muestreadas` con las cinco fuentes (`Terreno`, `Terreno 2`, `Corredor 1`, `Corredor 1 - Top` y
   `Corredor 1 - Datum`, en cualquier orden); `copia` dentro de `backups\`; y en `datos`:
   - **`lineas[0]`** (anótala **entera**) con `nombre: "0+000.00"`, **`secciones`** como hasta ahora, una lista de 5
     **textos**: `"Terreno"`, `"Terreno 2"`, `"Corredor 1"`, `"Corredor 1 Corredor 1 - Top"` y
     `"Corredor 1 Corredor 1 - Datum"`, y **`secciones_detalle`**, una lista de 5 objetos **en el mismo orden** que
     `secciones`:

     | `source_name` | `superficie` | `clase` | `metodo` |
     |---|---|---|---|
     | `Terreno` | `Terreno` | `superficie` | `SampleLine.GetSectionId` |
     | `Terreno 2` | `Terreno 2` | `superficie` | `SampleLine.GetSectionId` |
     | `Corredor 1` | `null` | `corredor` | `Section.SourceId` o `Section.SourceName` (anota cuál) |
     | `Corredor 1 Corredor 1 - Top` | **`Corredor 1 - Top`** | `superficie_corredor` | `SampleLine.GetSectionId` |
     | `Corredor 1 Corredor 1 - Datum` | **`Corredor 1 - Datum`** | `superficie_corredor` | `SampleLine.GetSectionId` |

   - **`secciones_identificadas`** (entero): `secciones: 215` (43 líneas × 5), `sin_identificar: 0` y `metodos` con
     `"SampleLine.GetSectionId": 172` y el método del corredor con `43`.
   - **`nombre_grupo`** (entero): `{"pedido": "Eje 1 - Muestreo", "usado": "Eje 1 - Muestreo", "en_otros_ejes": [],
     "alternativo": null, "rechazado_por_civil3d": false, "texto_civil3d": null, "grupos_leidos": 0}`.
   - `nombres.leidos` con `grupos: 0`.
   OK solo si `secciones` sigue siendo una lista de textos y cada sección de superficie de `secciones_detalle` dice su
   `superficie` del dibujo. Si alguna sale con `superficie: null` y `metodo: null`, anota la línea entera: es el hallazgo.

4. **Segundo grupo en el Eje 1** (no decisivo; ya validado en la 1.14.2). `crear_secciones` con
   `args={"alineamiento":"Eje 1","nombre":"Muestreo 100","intervalo":100,"ancho_izquierdo":20,"ancho_derecho":20,"fuentes":"[\"Terreno\"]","crear_vistas":false,"pedido":"otro grupo en el eje 1 cada 100 m, solo del terreno, sin vistas"}`.
   Esperado: `ok: true`; `despues` con `nombre: "Muestreo 100"`, `numero_lineas: 10`, `prefijo_lineas: "Muestreo 100 "`
   y `nombres_lineas` de `"Muestreo 100 0+000.00"` a `"Muestreo 100 0+831.33"`; en `datos.lineas[0]`,
   `secciones: ["Terreno"]` y `secciones_detalle` con un objeto (`superficie: "Terreno"`,
   `metodo: "SampleLine.GetSectionId"`); `datos.nombre_grupo.en_otros_ejes: []` (el `Eje 1 - Muestreo` es del mismo eje,
   no de otro) y `grupos_leidos: 1`.

5. **Segundo eje** (no decisivo; prepara el paso 6). Se crea `Eje 2` desde la misma polilínea que `Eje 1`.
   1. **Simular.** `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje 2","capa":"EJE_PRUEBA","radio":80,"simular":true,"pedido":"simula el eje 2 desde la polilínea de prueba"}`.
      Esperado: `simulado: true` y `despues.longitud: 831.3274`.
      - Si responde `No existe la capa 'EJE_PRUEBA'` o `No hay ninguna polilínea 2D en la capa 'EJE_PRUEBA'`, dibuja la
        polilínea con `ejecutar_comando` y
        `args={"comando":"_.-LAYER _M EJE_PRUEBA\n\n","pedido":"crea la capa EJE_PRUEBA"}` y luego
        `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","pedido":"dibuja la polilínea de prueba"}`
        (la polilínea con **dos espacios al final**: cierran la orden, validado en la 1.10.1; la capa con `\n\n`, porque `-LAYER` no toma el espacio como Enter, comprobación C1 del 20/11/2026). Las dos deben responder `terminado`.
        Repite la simulación y anota que hubo que dibujarla.
      - Si responde `La capa 'EJE_PRUEBA' tiene N polilíneas; indica cuál con 'handle': ...`, repite la simulación con
        `"handle":"<el de 4 vértices>"` en lugar de `"capa"` y anota la lista literal.
   2. **Crear.** Los mismos `args` sin `simular` y con `"pedido":"crea el eje 2 desde la polilínea de prueba"`.
      Esperado: `ok: true`, `despues.existe: true` y `despues.longitud: 831.3274`.

6. **El mismo nombre de grupo en el otro eje, simulación (decisivo).** `crear_secciones` con
   `args={"alineamiento":"Eje 2","nombre":"Muestreo 100","intervalo":100,"ancho_izquierdo":20,"ancho_derecho":20,"fuentes":"[\"Terreno\"]","crear_vistas":false,"simular":true,"pedido":"simula en el eje 2 un grupo llamado Muestreo 100"}`.
   Esperado: `ok: true` (**no** el error `Ya existe el grupo de líneas de muestreo 'Muestreo 100' en el alineamiento ...`:
   ese error es solo para el mismo eje), `simulado: true`; `despues` con `nombre: "Muestreo 100"`, `numero_lineas: 10`,
   `prefijo_lineas: "Muestreo 100 "` y `nombres_lineas` de `"Muestreo 100 0+000.00 (2)"` a
   `"Muestreo 100 0+831.33 (2)"` (los nombres `Muestreo 100 0+000.00`... ya los tienen las líneas del paso 4); y en
   `avisos`, entre otros, este (literal):
   `Ya hay un grupo de líneas de muestreo llamado 'Muestreo 100' en el eje 'Eje 1'. No se sabe todavía si Civil 3D deja repetir el nombre de un grupo en otro eje (por verificar, VALIDACION_1193): se intenta con 'Muestreo 100' y, si Civil 3D lo rechaza por repetido, el grupo se crea como 'Eje 2 - Muestreo 100' (con sus líneas y vistas) en la misma transacción, con otro aviso.`
   Sin `copia`, sin entrada nueva en Deshacer y sin archivo nuevo en `backups\`. OK solo si sale el aviso literal.

7. **El mismo nombre de grupo en el otro eje, creación real (decisivo: la pregunta de esta validación).** Repite la
   llamada del paso 6 **sin** `simular` y con
   `"pedido":"crea en el eje 2 un grupo de líneas de muestreo llamado Muestreo 100, cada 100 m con 20 m a cada lado, solo del terreno y sin vistas"`.
   Anota **entero** `datos.nombre_grupo` y los `avisos`. Hay dos resultados correctos; anota cuál salió:
   - **A. Civil 3D acepta el nombre.** `ok: true`; `despues.nombre: "Muestreo 100"`, `numero_lineas: 10` y
     `nombres_lineas` como en el paso 6; `datos.nombre_grupo` =
     `{"pedido": "Muestreo 100", "usado": "Muestreo 100", "en_otros_ejes": ["Eje 1"], "alternativo": "Eje 2 - Muestreo 100", "rechazado_por_civil3d": false, "texto_civil3d": null, "grupos_leidos": 2}`;
     y en `avisos`, **en lugar** del aviso del paso 6, este (literal):
     `Civil 3D dejó crear el grupo 'Muestreo 100' en el eje 'Eje 2' aunque el eje 'Eje 1' ya tiene uno con ese nombre (datos.nombre_grupo.rechazado_por_civil3d: false): el nombre del grupo se dejó tal cual.`
   - **B. Civil 3D rechaza el nombre y el grupo se crea con el alternativo.** `ok: true`;
     `despues.nombre: "Eje 2 - Muestreo 100"`, `numero_lineas: 10`, `prefijo_lineas: "Eje 2 - Muestreo 100 "` y
     `nombres_lineas` de `"Eje 2 - Muestreo 100 0+000.00"` a `"Eje 2 - Muestreo 100 0+831.33"`; `cambios` con el
     nombre de `Muestreo 100` a `Eje 2 - Muestreo 100`; `datos.nombre_grupo` con `usado: "Eje 2 - Muestreo 100"`,
     `rechazado_por_civil3d: true` y **`texto_civil3d`**: el texto de Civil 3D (anótalo literal: es la respuesta a la
     pregunta); y en `avisos`, **en lugar** del aviso del paso 6, este (literal):
     `Civil 3D no dejó crear el grupo 'Muestreo 100' en el eje 'Eje 2' porque ya hay uno con ese nombre en el eje 'Eje 1' (rechazo por nombre repetido, en datos.nombre_grupo.texto_civil3d): se creó como 'Eje 2 - Muestreo 100', y sus líneas y vistas llevan ese nombre. El grupo del otro eje no se tocó.`
   En los dos casos, `datos.lineas[0].secciones_detalle` con un objeto (`superficie: "Terreno"`,
   `metodo: "SampleLine.GetSectionId"`) y `copia` dentro de `backups\` (o reutilizada).
   **Si sale un error** (`ok: false`): si empieza por `Civil 3D no deja crear el grupo de líneas de muestreo 'Muestreo 100'
   en el eje 'Eje 2' porque ya hay uno con ese nombre en el eje 'Eje 1', y no se prueba con 'Eje 2 - Muestreo 100'
   porque el eje pasó de ...`, Civil 3D rechazó el nombre **y el intento dejó algo**: no es OK, pero es el hallazgo;
   anótalo entero, `leer_historial` con `args={"ultimas_n":10}` (el texto de Civil 3D está ahí) y sigue. Cualquier otro
   error es FALLO.

8. **El mismo nombre en el mismo eje sigue siendo error** (no decisivo; sin tocar nada). `crear_secciones` **sin**
   `simular` con
   `args={"alineamiento":"Eje 1","nombre":"Muestreo 100","intervalo":100,"ancho_izquierdo":20,"ancho_derecho":20,"fuentes":"[\"Terreno\"]","crear_vistas":false,"pedido":"repite el grupo Muestreo 100 en el eje 1"}`.
   Esperado: `ok: false` y
   `Ya existe el grupo de líneas de muestreo 'Muestreo 100' en el alineamiento 'Eje 1'. Usa listar_lineas_muestreo o pasa otro 'nombre'.`
   Sin copia y sin entrada en Deshacer (lo corta la comprobación previa por simulación).

9. **Deshacer y copias.** Abre el menú Deshacer: respecto al paso 2 debe haber **cuatro** entradas nuevas
   (`Executefunction`), las de los pasos 3, 4, 5.2 y 7 (si en el paso 5 hubo que dibujar la polilínea, `ejecutar_comando`
   añade las suyas: anótalas aparte); los pasos 5.1, 6 y 8 no añaden ninguna. En `backups\`, un archivo nuevo por
   escritura o la copia reutilizada (`copia.reutilizada: true`, no es fallo). **No** hagas zoom.

10. **El grupo del Eje 1 sigue intacto (decisivo).** `listar_lineas_muestreo` con
    `args={"pedido":"lista las líneas de muestreo del dibujo"}` (sin `alineamiento`). Esperado (anota la lista entera):
    - en `Eje 1`, **los dos grupos de antes, sin cambios**: `Eje 1 - Muestreo` con `n_lineas: 43`, `inicio: 0`,
      `fin: 831.3274`, y `Muestreo 100` con `n_lineas: 10`, `inicio: 0`, `fin: 831.3274`;
    - en `Eje 2`, **un solo** grupo, con el nombre que salió en el paso 7 (`Muestreo 100` en el caso A,
      `Eje 2 - Muestreo 100` en el B) y `n_lineas: 10`.
    Si en el paso 7 salió error, en `Eje 2` no debe haber **ningún** grupo. OK solo si el `Eje 1` tiene sus dos grupos
    con las mismas líneas. Si puedes, abre en Prospector → Alineaciones → `Eje 2` → Grupos de líneas de muestreo y anota
    el nombre del grupo y el de sus líneas.

11. **Deshacer la creación del paso 7.** Abre el menú Deshacer: arriba debe haber **dos** `Executefunction`, la del
    paso 10 (`listar_lineas_muestreo`) y, debajo, la del paso 7. Con `ejecutar_comando`
    `args={"comando":"_.UNDO 2","pedido":"deshaz la lista de líneas de muestreo y el grupo del eje 2"}` (si hubiera
    otra cosa encima, anótala y ajusta el número). Esperado: `"result": "terminado"`.

12. **Comprobar el deshacer.** `listar_lineas_muestreo` con
    `args={"pedido":"lista las líneas de muestreo del dibujo"}`: en `Eje 2` **ningún** grupo; en `Eje 1`, los dos
    grupos del paso 10, iguales. Si el grupo del `Eje 2` sigue ahí, anótalo con lo que muestra el menú Deshacer.

13. **Cierre.** `leer_log` con `args={"ultimas_n":30}`: una línea `llamada` por cada paso, con `ok:false` solo en el
    paso 8 (y en el 7 si salió error) y `ok:true` en las demás. No guardes el dibujo.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_1193_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, rama
instalada y versión de `ping`, DWG usado (solo el nombre genérico, sin ruta ni datos de obra), una tabla paso /
resultado (OK, FALLO) / respuesta literal / tiempo, y las respuestas a estas **preguntas para la memoria técnica**:

1. **La pregunta de esta validación**: ¿Civil 3D deja crear un grupo de líneas de muestreo con el nombre de un grupo que
   ya existe en **otro** eje (paso 7, caso A o B)? Si lo rechazó, el texto literal (`datos.nombre_grupo.texto_civil3d`
   y la línea de `leer_historial`). ¿El intento rechazado dejó algo (el error «el eje pasó de ...»)?
2. ¿El grupo del `Eje 1` quedó intacto (paso 10)? ¿Qué nombres muestra Prospector para el grupo del `Eje 2` y sus
   líneas?
3. `secciones_detalle` del paso 3: con qué `metodo` se reconoció cada sección (en especial `Corredor 1 - Top` y
   `Corredor 1 - Datum`, y el corredor: ¿`Section.SourceId` o `Section.SourceName`?) y `secciones_identificadas`
   entero. ¿Hubo alguna sección sin identificar?
4. ¿Un solo `_.UNDO 2` (la lista del paso 10 y la creación del paso 7) quitó el grupo del `Eje 2` (paso 12)?
5. Tiempos de los pasos 3, 4 y 7 (`ms`).

Y la lista de miembros de la sección 1.19.3 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
