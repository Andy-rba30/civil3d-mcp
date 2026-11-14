# Prompt de validación de la 1.15.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Se ejecuta después de `VALIDACION_1142.md`**, en la misma ronda y **sobre el mismo dibujo**: la copia
  `C:\Proyectos\dibujos_prueba\PRUEBA_1142.dwg` que la 1142 deja guardada (si usas otra copia de `PRUEBA_112.dwg`,
  anótalo). Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`. Ningún dato real.
- **Lo que debe tener el dibujo** (el paso 1 lo comprueba): `Eje 1`, el alineamiento de la progresiva `0` a la
  `831.3274` que creó la 1.8.0 desde la polilínea `1000,2000 → 1300,2000 → 1300,2300 → 1600,2300` de la capa
  `EJE_PRUEBA` con radio 80 en los dos PI. Lo demás que haya (rasante, corredor, superficies, secciones) no molesta. Si
  falta `Eje 1`, **para** y anótalo: esta validación no lo crea.
- **Plugin 1.15.0** desde la rama `tarea/2.9-tabla-pi` (junta la 1.14.2 de `tarea/2.7-volumenes` y la 1.15.0), con
  **54** herramientas del plugin (más `buscar_api` del puente). Si la 1142 se validó con otra instalación (1.14.2), el
  paso 0 reinstala desde `tarea/2.9-tabla-pi`.
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.15.0**: la herramienta de **lectura** `listar_pis`, la tabla de PI de un eje. Recibe
  `alineamiento` y devuelve una fila por PI, en orden: número, progresiva del PI (número y texto `0+300.000`),
  coordenadas `x`, `y`, deflexión (grados, grados-minutos-segundos y giro), y, si el PI tiene curva, radio, longitud,
  tangente y progresivas del PC y del PT (vacíos, `null`, si el PI no tiene curva). El inicio y el fin del eje van
  aparte (`inicio`, `fin`). No cambia el dibujo: **sin copia, sin `simular` y sin entrada en Deshacer**.
  **Los pasos 0, 2, 3, 4 y 6 son los decisivos.** El paso 7 es manual, lo hace Jose y no decide.
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.15.0):
  `AlignmentEntity.SubEntityCount`, el indexador `AlignmentEntity[int]` y `AlignmentSubEntityArc.PIPoint` (campo
  `diferencia_pi_civil3d`), y `Alignment.PointLocation` en el final exacto de una entidad y del eje.

---

Eres el agente de validación de la versión 1.15.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` se usa **solo** donde este prompt lo indica (dibujar la polilínea del paso 4 si falta). Si algo no
  funciona, es un hallazgo: no lo arregles con otro comando.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Si ya existe un alineamiento `Eje 115`, usa `Eje 115 b` en el paso 4 y anótalo.
- Los números esperados son en metros con redondeo a 4 decimales; **una diferencia menor de 0,001 es OK**.
- Anota el menú Deshacer (la flecha junto al botón Deshacer) **antes y después** de los pasos 2, 3, 4 y 5:
  `listar_pis` no debe añadir **ninguna** entrada; solo la creación real del paso 4 (con
  `crear_alineamiento_desde_polilinea`) añade **una** (`Executefunction`).

## Pasos

0. **Versión (decisivo).** Si `ping` ya da `1.15.0`, no reinstales. Si no, con Civil 3D cerrado, en PowerShell desde
   la raíz del repo en la rama `tarea/2.9-tabla-pi`: `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`;
   en `PuenteMcp`, `uv sync`; reinicia el puente y el cliente MCP y abre Civil 3D con la copia del DWG. Después:
   `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.15.0"`, `"hay_dibujo": true`. En
   PowerShell: `curl.exe -s http://127.0.0.1:8765/ping` → `{"ok":true,"servidor":"ArbaMcp","version":"1.15.0"}`. Pide
   la lista de herramientas: **54** del plugin, con `listar_pis` y sus parámetros `alineamiento` (obligatorio) y
   `pedido`, **sin** `simular`. Si la versión no es exactamente `1.15.0`, para aquí.

1. **Comprobar el dibujo** (solo lectura, no decisivo). `listar_alineamientos` con
   `args={"pedido":"lista los alineamientos"}`: debe estar `Eje 1` con `inicio: 0`, `fin: 831.3274`,
   `longitud: 831.3274`. **Anota la lista completa** (si hay más ejes, por ejemplo `Eje 115` de una ronda anterior).
   Si falta `Eje 1`, **para**.

2. **El pedido de Jose (decisivo).** Pide al agente, con estas palabras: **"Dame la tabla de PI del eje con radio,
   longitud de curva y progresiva"**. El agente debe elegir `listar_pis` con
   `args={"alineamiento":"Eje 1","pedido":"Dame la tabla de PI del eje con radio, longitud de curva y progresiva"}`
   (si pregunta qué eje, responde `Eje 1`). Anota qué herramienta eligió y con qué `args`. Respuesta esperada
   (`result`, literal salvo el orden de las claves; `unidades` es la del dibujo, `Meters` en el dibujo de prueba):

   ```json
   {"alineamiento":"Eje 1","unidades":"Meters","longitud":831.3274,
    "inicio":{"progresiva":0,"progresiva_texto":"0+000.000","x":1000,"y":2000},
    "fin":{"progresiva":831.3274,"progresiva_texto":"0+831.327","x":1600,"y":2300},
    "numero_pi":2,"numero_curvas":2,
    "pis":[{"numero":1,"tipo":"curva","tiene_curva":true,"progresiva":300,"progresiva_texto":"0+300.000","x":1300,"y":2000,
            "deflexion_grados":90,"deflexion_gms":"90°00'00.00\"","giro":"izquierda","radio":80,"longitud_curva":125.6637,
            "tangente":80,"progresiva_pc":220,"progresiva_pc_texto":"0+220.000","progresiva_pt":345.6637,
            "progresiva_pt_texto":"0+345.664","diferencia_pi_civil3d":0,"aviso":null},
           {"numero":2,"tipo":"curva","tiene_curva":true,"progresiva":565.6637,"progresiva_texto":"0+565.664","x":1300,"y":2300,
            "deflexion_grados":90,"deflexion_gms":"90°00'00.00\"","giro":"derecha","radio":80,"longitud_curva":125.6637,
            "tangente":80,"progresiva_pc":485.6637,"progresiva_pc_texto":"0+485.664","progresiva_pt":611.3274,
            "progresiva_pt_texto":"0+611.327","diferencia_pi_civil3d":0,"aviso":null}],
    "avisos":[]}
   ```

   OK si `numero_pi` es 2, `avisos` está vacío y todos los números coinciden con ±0,001. Si `diferencia_pi_civil3d`
   sale `null` en vez de `0`, **no es FALLO** de este paso: anótalo (la lectura de `PIPoint` falló) y llama a
   `leer_historial` con `args={"ultimas_n":10}` para copiar el mensaje `listar_pis: no se pudo leer el PI de Civil 3D`.
   El menú Deshacer debe seguir **igual** que antes del paso.

3. **Fila a fila contra la polilínea y la 1.8.0 (decisivo).** Con la respuesta del paso 2, comprueba (±0,001):

   | PI | Vértice de la polilínea (`x`, `y`) | `progresiva` | `radio` | `longitud_curva` | `progresiva_pc` | `progresiva_pt` | `giro` |
   |---|---|---|---|---|---|---|---|
   | 1 | 1300, 2000 | 300 | 80 | 125.6637 | 220 | 345.6637 | izquierda |
   | 2 | 1300, 2300 | 565.6637 | 80 | 125.6637 | 485.6637 | 611.3274 | derecha |

   Son los valores que validó la 1.8.0 con `crear_alineamiento_desde_polilinea` (informe 1.8.0, paso 4: `PIStation`
   300 y 565,6637, PC/PT 220/345,6637 y 485,6637/611,3274, radio 80 y deflexión 90° en los dos PI). Además, `inicio`
   debe estar en el primer vértice (1000, 2000) y `fin` en el último (1600, 2300), con `fin.progresiva` igual al `fin`
   de `listar_alineamientos` del paso 1. OK solo si todo coincide.

4. **PI sin curva (decisivo).** Se crea un segundo eje desde la misma polilínea, sin curva en el primer PI.
   1. **Simular.** `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje 115","capa":"EJE_PRUEBA","radio":80,"radios_por_pi":"[0, 80]","simular":true,"pedido":"simula el eje 115 sin curva en el primer PI"}`.
      Esperado: `simulado: true`, `despues.longitud: 865.6637`, `despues.numero_curvas: 1` y `despues.pis` con el PI 1
      sin curva (`radio_pedido: 0`, `radio_aplicado: null`, `progresiva: 300`, `x: 1300`, `y: 2000`) y el PI 2 con
      curva (`radio_aplicado: 80`, `progresiva: 600`, `progresiva_pc: 520`, `progresiva_pt: 645.6637`). Ningún archivo
      nuevo en `backups\` y el menú Deshacer igual.
      - Si responde `No existe la capa 'EJE_PRUEBA'` o `No hay ninguna polilínea 2D en la capa 'EJE_PRUEBA'`, dibuja la
        polilínea con `ejecutar_comando` y
        `args={"comando":"_.-LAYER _M EJE_PRUEBA  ","pedido":"crea la capa EJE_PRUEBA"}` y luego
        `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","pedido":"dibuja la polilínea de prueba"}`
        (con **dos espacios al final**: cierran la orden; validado en la 1.10.1). Las dos deben responder
        `terminado`. Repite la simulación y anota que hubo que dibujarla.
      - Si responde `La capa 'EJE_PRUEBA' tiene N polilíneas; indica cuál con 'handle': ...`, repite la simulación con
        `"handle":"<el de 4 vértices>"` en lugar de `"capa"` y anota la lista literal.
   2. **Crear.** Los mismos `args` sin `simular` y con
      `"pedido":"crea el eje 115 sin curva en el primer PI"`. Esperado: `ok: true`, `despues.existe: true`,
      `despues.numero_curvas: 1`, una entrada **nueva** en el menú Deshacer y una copia en `backups\` (o la reutilizada
      si el archivo no cambió; anota `copia.ruta` con `<ruta>`).
   3. **Tabla.** `listar_pis` con
      `args={"alineamiento":"Eje 115","pedido":"tabla de PI del eje 115"}`. Respuesta esperada (`result`):

      ```json
      {"alineamiento":"Eje 115","unidades":"Meters","longitud":865.6637,
       "inicio":{"progresiva":0,"progresiva_texto":"0+000.000","x":1000,"y":2000},
       "fin":{"progresiva":865.6637,"progresiva_texto":"0+865.664","x":1600,"y":2300},
       "numero_pi":2,"numero_curvas":1,
       "pis":[{"numero":1,"tipo":"sin_curva","tiene_curva":false,"progresiva":300,"progresiva_texto":"0+300.000","x":1300,"y":2000,
               "deflexion_grados":90,"deflexion_gms":"90°00'00.00\"","giro":"izquierda","radio":null,"longitud_curva":null,
               "tangente":null,"progresiva_pc":null,"progresiva_pc_texto":null,"progresiva_pt":null,"progresiva_pt_texto":null,
               "diferencia_pi_civil3d":null,"aviso":null},
              {"numero":2,"tipo":"curva","tiene_curva":true,"progresiva":600,"progresiva_texto":"0+600.000","x":1300,"y":2300,
               "deflexion_grados":90,"deflexion_gms":"90°00'00.00\"","giro":"derecha","radio":80,"longitud_curva":125.6637,
               "tangente":80,"progresiva_pc":520,"progresiva_pc_texto":"0+520.000","progresiva_pt":645.6637,
               "progresiva_pt_texto":"0+645.664","diferencia_pi_civil3d":0,"aviso":null}],
       "avisos":[]}
      ```

      OK si el PI 1 sale **sin radio** (`tipo: "sin_curva"`, `radio`, `longitud_curva`, `progresiva_pc` y
      `progresiva_pt` en `null`), en el vértice 1300, 2000, con `progresiva: 300` y `deflexion_grados: 90`, y el PI 2
      coincide con ±0,001. Como en el paso 2, `diferencia_pi_civil3d: null` en el PI 2 se anota pero no es FALLO.
      El menú Deshacer debe quedar como tras el paso 4.2. **No borres** `Eje 115`: no molesta a las validaciones
      siguientes.

5. **Errores controlados (no decisivo).** Cada uno debe responder error **sin** entrada en Deshacer:
   - `listar_pis` con `args={"alineamiento":"NoExiste","pedido":"tabla de PI de un eje que no existe"}` →
     `No existe el alineamiento 'NoExiste'. Usa listar_alineamientos.`
   - `listar_pis` con `args={"pedido":"tabla de PI sin decir el eje"}` → `Falta el parámetro obligatorio 'alineamiento'.`
     (si el cliente MCP no deja llamar sin el parámetro obligatorio, anota su mensaje: no es FALLO).

6. **Sin rastro de escritura (decisivo).** Menú Deshacer: entre el principio del paso 2 y el final del paso 5 solo debe
   haber **una** entrada nueva, la del paso 4.2. En `backups\`: ningún archivo nuevo salvo el del paso 4.2 (si no se
   reutilizó). `leer_log` con `args={"ultimas_n":8}`: deben aparecer las llamadas a `listar_pis` de los pasos 2, 4.3 y
   5 con su `pedido` literal, `ok: true` en las dos primeras y `ok: false` con el error en las de error. Copia las
   líneas con `<ruta>` en lugar de las rutas.

7. **Informe de alineamiento de Civil 3D (manual, Jose, recomendado, no decisivo).** El agente no puede generar este
   informe con el MCP: lo hace Jose a mano en Civil 3D, y el agente solo copia lo que Jose le dicte.
   1. En Civil 3D: **Espacio de herramientas** (ficha Inicio → grupo Paletas → Espacio de herramientas) → ficha **Caja de herramientas** → **Administrador de informes** → **Alineamiento** (los
      nombres en español pueden variar un poco; anota los reales).
   2. Abre el informe de **progresivas y curvas** (en la instalación en inglés, *Station and Curve Report*). Si no
      está, usa el de **PI** (*PI Station Report*). Anota el nombre exacto que muestra Civil 3D.
   3. En el cuadro que se abre, marca **solo** `Eje 1`, deja el intervalo de progresivas completo, elige dónde guardar
      el archivo (por ejemplo `C:\Proyectos\dibujos_prueba\informe_eje1.html`) y pulsa **Crear informe**.
   4. Compara, para cada curva del informe, con la fila del paso 2: **progresiva del PI** con `progresiva`, **progresiva
      del PC** con `progresiva_pc`, **progresiva del PT** con `progresiva_pt`, **radio** con `radio`, **longitud** con
      `longitud_curva`, **tangente** con `tangente` y **delta** (o deflexión) con `deflexion_gms`. Si el informe trae
      coordenadas del PI, **Norte** (Northing) con `y` y **Este** (Easting) con `x`. La tolerancia es 0,001; si el
      informe da solo 2 decimales, vale la diferencia de su redondeo (hasta 0,005).
   5. Repite con `Eje 115` (paso 4): el PI 1 no tiene curva, así que en el informe de curvas solo sale la del PI 2; el
      de PI (si lo usas) debe dar el PI 1 en 0+300 con 1300, 2000.
   Anota en el informe una tabla con las columnas del informe de Civil 3D, los valores de `listar_pis` y la diferencia.
   **No pegues** el archivo del informe de Civil 3D en el repositorio.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_115_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (**OK** o **FALLO**) / respuesta
literal / tiempo, los hallazgos y el **resultado global: OK solo si los pasos decisivos 0, 2, 3, 4 y 6 son OK** (el
paso 7, manual, se informa aparte como OK, FALLO o «no hecho»). Responde además a estas preguntas para la memoria
técnica:

1. Las dos versiones del paso 0 (`ping` y `GET /ping`), literales, y el número de herramientas (54).
2. Paso 2: ¿qué herramienta y qué `args` eligió el agente con el pedido de Jose? ¿Hizo falta decirle el eje?
3. ¿Salió `diferencia_pi_civil3d` con un número (cuál) o `null` en los pasos 2 y 4.3? Si salió `null`, el mensaje
   literal de `leer_historial`.
4. Paso 4: ¿hubo que dibujar la polilínea o usar `handle`? Las respuestas literales de la simulación y de la creación.
5. Tiempos (`ms`) de `listar_pis` en los pasos 2 y 4.3.
6. Paso 7 (si Jose lo hizo): el nombre exacto del informe y de la ruta en el Administrador de informes en español, sus
   columnas y la tabla de diferencias.

Y la lista de miembros de la sección 1.15.0 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
