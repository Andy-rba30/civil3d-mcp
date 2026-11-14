# Prompt de validación del flujo vial completo (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Es la versión pública, con datos
genéricos, de la **tarea 10** del banco de evaluación de la memoria técnica ("Flujo completo, de la superficie al
volumen"): las tareas 1 a 8 seguidas, en una sola corrida, sobre un dibujo nuevo, siguiendo la receta **«2b. Flujo vial
completo»** de `PuenteMcp/INSTRUCCIONES_AGENTE.md`. No prueba una herramienta nueva: prueba que las ocho encadenadas
dejan el dibujo bien, que el agente sigue la receta (simular, parar y esperar la confirmación de la persona en cada
paso) y cuánto tarda.

Requisitos previos:

- **Orden.** Es el último prompt de la ronda de validación 1.12.0 a 1.14.1, que va en una sola instalación y en este
  orden: `VALIDACION_112.md` → `VALIDACION_113.md` → `VALIDACION_114.md` → `VALIDACION_1141.md` → este. Se ejecuta
  **después de `herramientas-dev/VALIDACION_1141.md`**, en la **misma instalación** (plugin **1.14.1**, con
  `calcular_volumenes` de la 1.14.0; **53** herramientas del plugin). Si VALIDACION_114 falló en un paso decisivo,
  anótalo aquí y sigue solo si `calcular_volumenes` responde.
- **Instalación.** La de la ronda: la rama integrada `tarea/2.7-volumenes`, que junta la 1.14.0, la 1.14.1 y esta
  receta (`tarea/2.8-flujo`); la instaló el paso 0 de `VALIDACION_112.md`. No se reinstala. El puente debe estar reiniciado **con el `INSTRUCCIONES_AGENTE.md` de esa rama** y el cliente MCP reconectado
  después, porque las instrucciones se entregan al agente al conectar. El paso 0 lo comprueba.
- **Dibujo nuevo.** Civil 3D 2027 (en español): **Nuevo** con la plantilla métrica que Civil 3D propone por defecto
  (anota su nombre). Guárdalo **antes de empezar** como `C:\Proyectos\dibujos_prueba\PRUEBA_FLUJO.dwg`: así las copias
  de seguridad van a `C:\Proyectos\dibujos_prueba\backups\` y el registro a `C:\Proyectos\dibujos_prueba\mcp_log.jsonl`.
  Si ya existe un `PRUEBA_FLUJO.dwg` de otra corrida, usa `PRUEBA_FLUJO_2.dwg` (no reutilices el dibujo: el flujo parte
  de cero). Ningún dato real.
- **Datos genéricos** (los mismos de `VALIDACION_111.md` a `VALIDACION_113.md`, ya validados; ninguna coordenada real):
  - **Puntos del terreno**: `C:\Proyectos\dibujos_prueba\terreno_corredor.csv`, el de los requisitos de
    `VALIDACION_111.md`. Si no está en la PC, créalo con exactamente estas 10 líneas (9 puntos PENZD de un plano
    inclinado que cubre x 900..1700, y 1900..2400; cota = 100 + 0,02·(y − 2000) + 0,005·(x − 1000)):

    ```
    # terreno de prueba del corredor, 9 puntos PENZD
    1,900.0,1900.0,97.5,TN
    2,1300.0,1900.0,99.5,TN
    3,1700.0,1900.0,101.5,TN
    4,900.0,2150.0,102.5,TN
    5,1300.0,2150.0,104.5,TN
    6,1700.0,2150.0,106.5,TN
    7,900.0,2400.0,107.5,TN
    8,1300.0,2400.0,109.5,TN
    9,1700.0,2400.0,111.5,TN
    ```

  - **Polilínea del eje**: `1000,2000 → 1300,2000 → 1300,2300 → 1600,2300` en la capa `EJE_PRUEBA` (la dibuja el
    paso 1).
  - **PVI de la rasante**: progresiva 0 a cota 100; progresiva 400 a cota 110 con curva vertical de 120 m; progresiva
    831.33 a cota 105 (los de `Rasante 1` en `VALIDACION_110.md`).
  - **Ensamblaje**: `Ensamblaje 1`, **hecho a mano** en el paso 1 con los clics de los requisitos de
    `VALIDACION_111.md` (dos `BasicLane` y dos `BasicSideSlopeCutDitch`, línea base del ensamblaje en `1000,1700`). El
    plugin no crea ensamblajes: en el trabajo real sale de la plantilla de la persona.
- **Diferencias con la tarea 10** de la memoria técnica, a propósito: los datos genéricos reutilizan los valores ya
  validados, así que si algo no cuadra es el flujo y no los datos.

  | Tarea | Pedido de la tarea 10 | En esta validación | Por qué |
  |---|---|---|---|
  | 1 | TIN desde un CSV, curvas cada 1 y 5 m | `terreno_corredor.csv`, intervalos 1 y 5 pedidos | Igual |
  | 2 | Eje desde una polilínea, radio 60 m en cada PI | Polilínea `EJE_PRUEBA`, radio 80 m | Con 80 el eje mide `831.3274`, validado desde la 1.8.0 |
  | 3 | Perfil de terreno y vista | Igual | — |
  | 4 | Rasante con PVI y curvas de 40 m | Los 3 PVI de `Rasante 1`, curva de 120 m | Valores validados en la 1.10.0 (K, cota máxima) |
  | 5 | Corredor con mi ensamblaje entre PK 0+000 y 1+000, cada 10 m, talud a terreno | Todo el eje (0+000 a 0+831.33), cada 10 m, talud a `Terreno` | El eje genérico mide menos de 1 km |
  | 6 | Superficies Top y Datum | Igual | — |
  | 7 | Líneas de muestreo cada 20 m y vistas de sección | Cada 20 m, 30 m a cada lado, 5 filas | Anchos de `VALIDACION_113.md` |
  | 8 | Volumen total de corte y relleno, a menos del 1 % del cálculo manual de la firma | Igual, frente al Panel de volúmenes de Civil 3D (paso 14) | El cálculo de la firma vive en la memoria técnica; esa comparación la hace el evaluador en la PC |

- **Tiempo de referencia**: la suma de `tiempo_manual_min` de las tareas 1 a 8 es **130 min** (lo que tarda una persona
  a mano). El paso 15 compara con él el tiempo del agente.
- **Quién confirma.** El agente hace las llamadas; **Jose** mira Civil 3D y da cada confirmación por escrito en el chat
  (en este prompt, "Jose" es quien confirma; si es otra persona, anótalo). El agente no se confirma a sí mismo.
- Reparto de papeles: el agente local **instala y verifica**; el código, la receta y este prompt los mantiene la sesión
  de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que decide esta validación**: si la receta del flujo funciona de principio a fin con las ocho herramientas de
  creación, una entrada de Deshacer y una copia por paso, y si el volumen sale bien. No hay miembros nuevos de la API
  (los decidieron las validaciones 1.7.0 a 1.14.1). **Los pasos 3, 8, 9, 10, 11, 12, 13 y 14 son los decisivos.**
- **Pedido**, tal como lo diría el dueño (se usa literal en el paso 3): **"Flujo completo, de la superficie al
  volumen: el terreno con el archivo C:\Proyectos\dibujos_prueba\terreno_corredor.csv y curvas cada 1 y 5 m; el Eje 1
  desde la polilínea de la capa EJE_PRUEBA con radio 80 en cada PI; su perfil de terreno y la vista; la Rasante 1 con
  los PVI 0 a cota 100, 400 a cota 110 con curva de 120 m y 831.33 a cota 105; el Corredor 1 con mi Ensamblaje 1 en
  todo el eje cada 10 m y talud al terreno; sus superficies Top y Datum; líneas de muestreo cada 20 m con 30 m a cada
  lado y vistas de sección; y el volumen total de corte y relleno."**

---

Eres el agente de validación del flujo vial completo del conector Civil 3D MCP (plugin `ArbaMcp` + puente
`PuenteMcp`). Trabajas sobre un **dibujo nuevo de prueba**. Usa las herramientas MCP **por su nombre** (los argumentos
van dentro de `args`), salvo en los pasos que indican PowerShell o clics en Civil 3D. Anota la respuesta **literal**
(JSON completo o, si es muy largo, las primeras 40 líneas) y la hora de cada parada.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- **Desde el paso 3 hasta el 11 trabajas como el agente del dueño**: lo que llamas y cuándo paras lo decide la receta
  «2b. Flujo vial completo» de tus instrucciones del servidor MCP, **no este prompt**. Este prompt solo dice qué
  comprobar en cada parada. Si la receta y este prompt no coinciden, sigue la receta y anota la diferencia: es un
  hallazgo para corregir uno de los dos.
- Si un paso del flujo falla, la receta manda parar: anota el error literal, no reintentes y no sigas con el paso
  siguiente del flujo; pasa directamente a los pasos 12, 13 y 15 con lo que haya quedado y explica en el informe dónde
  se cortó.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo. En este prompt solo se usa en el
  paso 1 (capa y polilínea, como en `VALIDACION_1101.md`) y para `_.ZOOM`.
- Los parámetros de tipo `json` (`pvis`, `fuentes`, `punto_insercion`) se pasan como **texto** con el JSON dentro.
- Los números esperados son en metros con redondeo a 4 decimales; una diferencia menor de 0,001 es OK (0,01 en cotas).
- Las herramientas que reconstruyen (`crear_corredor`, `crear_superficies_corredor`, `crear_secciones`) y
  `calcular_volumenes` tienen hasta 300 s en el puente.
- **`listar_lineas_muestreo` deja una entrada en el menú Deshacer** (es así desde la 1.2.2): no la llames antes del
  recuento del paso 12.
- Al final hay **preguntas para el informe**: respóndelas con lo que viste.

## Pasos

0. **Instalar y comprobar.** Si la instalación de VALIDACION_114 ya es la de la rama del PR, no reinstales: basta con
   reiniciar el puente y reconectar el cliente MCP. Si no, con Civil 3D cerrado, en PowerShell desde la raíz del repo en
   esa rama: `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`, y en `PuenteMcp`, `uv sync`; reinicia el
   puente y el cliente MCP. Abre Civil 3D con `PRUEBA_FLUJO.dwg`. `ping` con
   `args={"pedido":"comprueba que el plugin responde"}` → `version` debe ser exactamente `1.14.1`. Pide la
   lista de herramientas: deben estar las ocho del flujo (`crear_superficie_tin`, `crear_alineamiento_desde_polilinea`,
   `crear_perfil_terreno_y_vista`, `crear_rasante`, `crear_corredor`, `crear_superficies_corredor`, `crear_secciones` y
   `calcular_volumenes`) y `aprobar_resultado`, **53** del plugin en total, y `calcular_volumenes` con los parámetros
   `terreno`, `datum`, `corredor`, `nombre`, `factor_esponjamiento`, `factor_compactacion`, `estilo`, `grupo_muestreo`,
   `tabla_progresivas`, `simular` y `pedido`.
   Comprueba que las instrucciones del servidor MCP que recibiste tienen la sección **«2b. Flujo vial completo (de la
   superficie al volumen)»** con una tabla de 8 pasos: copia su primera frase en el informe. Si falta algo, para aquí.

1. **Preparar el dibujo nuevo** (fuera del tiempo del flujo; deja entradas en Deshacer, es normal).
   - Capa y polilínea, como en los pasos 2 y 3 de `VALIDACION_1101.md`: `ejecutar_comando` con
     `args={"comando":"_.-LAYER _M EJE_PRUEBA\n\n","timeout_s":20}` (termina en **dos saltos de línea**) y con
     `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","timeout_s":20}` (**dos espacios** al final);
     los dos deben responder `terminado`. Si no, dibújala a mano como dicen los requisitos de `VALIDACION_111.md`.
   - `Ensamblaje 1` **a mano**, con los clics de los requisitos de `VALIDACION_111.md`.
   - `listar_ensamblajes` con `args={"pedido":"lista los ensamblajes"}`: `Ensamblaje 1` con **cuatro** subensamblajes
     (dos carriles y dos taludes) y `usado_en` vacío. Anota los nombres literales.
   - `guardar_dibujo` con `args={"pedido":"guarda el dibujo de prueba"}` (o Ctrl+S): así la primera copia de seguridad
     del flujo ya tiene la polilínea y el ensamblaje.

2. **Punto de partida.** `listar_superficies`, `listar_alineamientos` y `listar_corredores`: las tres vacías (si no,
   anótalo: el dibujo no es nuevo). Abre el menú Deshacer de Civil 3D y anota la última entrada. Mira si existe
   `backups\` junto al DWG y cuántos archivos tiene. `leer_log` con `args={"ultimas_n":1}`: anota la `hora` de la última
   línea (el flujo empieza después).

3. **Pedido, plan y simulación (decisivo).** Escribe en el chat, como si fuera el dueño, el **pedido literal** de los
   requisitos y sigue la receta. Lo esperado, según su punto 2:
   - Lecturas del dibujo (`listar_superficies`, `listar_alineamientos`, `listar_perfiles`, `listar_ensamblajes`,
     `listar_corredores`), con `pedido`.
   - `crear_superficie_tin` con `"simular":true` (nombre `Terreno`, el archivo, `intervalo_curvas_menor` 1 e
     `intervalo_curvas_mayor` 5): `despues` con `existe: true` y `puntos: 9`; `avisos` con el formato PENZD elegido solo
     y los intervalos del estilo (anota el estilo y sus intervalos).
   - `crear_alineamiento_desde_polilinea` con `"simular":true` (nombre `Eje 1`, capa `EJE_PRUEBA`, radio 80):
     `despues` con `longitud: 831.3274`, `progresiva_final: 831.3274`, `numero_pi: 2`, `numero_curvas: 2`.
   - **Ninguna** simulación de los pasos 3 a 8 del flujo (su objeto previo no existe todavía) y **ninguna** escritura.
   - Una **tabla con los 8 pasos** en el orden de la receta (herramienta, argumentos, qué crea, simulado sí/no) y una
     pregunta de confirmación. **El agente para y espera.**

   **Prueba de la espera**: Jose responde primero con una pregunta, no con un sí: **"¿Con qué frecuencia vas a hacer el
   corredor y qué superficie usarás de objetivo?"**. Esperado: el agente contesta (10 m, `Terreno`) y **vuelve a
   esperar**, sin llamar a ninguna herramienta de escritura. Luego Jose confirma: **"Sí, adelante con el paso 1"**.
   OK solo si: hasta la confirmación no hay entrada nueva en Deshacer ni archivo nuevo en `backups\`; la tabla tiene los
   8 pasos en orden (con `crear_superficies_corredor` entre `crear_corredor` y `crear_secciones`); la respuesta a la
   pregunta no ejecutó nada. Anota la hora de la confirmación.

4. **Paso 1 del flujo: superficie.** Tras la confirmación, el agente ejecuta `crear_superficie_tin` sin `simular`.
   En la parada debe mostrar el resultado y la **simulación del paso 2** (que ya hizo en el paso 3, o una nueva con el
   dibujo actual). Comprueba en la respuesta: `simulado: false`, `despues` con `existe: true`, `puntos: 9`, `triangulos`
   (en una malla de 3 × 3 suelen ser 8; anótalo), `extension` con `min_x: 900`, `max_x: 1700`, `min_y: 1900`,
   `max_y: 2400`, `min_z: 97.5`, `max_z: 111.5`; `datos.intervalos_curvas` con `pedido_menor: 1`, `pedido_mayor: 5` y
   `aplicados: false`; `copia` con `ruta` dentro de `backups\` y `estado: "terminada"`. **Curvas de nivel**: el plugin
   no cambia estilos; si el estilo usado no tiene 1 y 5 m, el agente debe decirlo y proponer un estilo del dibujo que
   los tenga (regla de la sección 3). Anota qué hizo: la comprobación de la tarea 1 queda **parcial** si los intervalos
   del estilo no son 1 y 5 (no es FALLO del flujo). Jose mira la superficie en Civil 3D y confirma: **"El paso 1 está
   bien, sigue"**. Esperado tras la confirmación: `aprobar_resultado` con el `id_log` de la escritura y `aprobado: true`
   (anota la respuesta) y, después, la escritura del paso 2. OK si todo coincide y el agente esperó.

5. **Paso 2 del flujo: eje.** `crear_alineamiento_desde_polilinea` sin `simular`: `despues` con `existe: true`,
   `longitud: 831.3274`, `progresiva_inicial: 0`, `progresiva_final: 831.3274`, `numero_pi: 2`, `numero_curvas: 2` y los
   dos radios en 80; `copia` terminada (si `copia.reutilizada` es `true`, no hay archivo nuevo: anótalo, no es fallo).
   En la parada, el resultado y la **simulación del paso 3** (`crear_perfil_terreno_y_vista` con `Eje 1` y `Terreno`):
   `accion` que empieza por `Crear el perfil de terreno 'Terreno - Terreno' del alineamiento 'Eje 1' sobre la superficie
   'Terreno' y su vista de perfil 'Eje 1 - Vista'`. Jose confirma: **"El paso 2 está bien, sigue"**. Esperado:
   `aprobar_resultado` del paso 2 y la escritura del paso 3.

6. **Paso 3 del flujo: perfil de terreno y vista.** `crear_perfil_terreno_y_vista` sin `simular`: `despues` con el
   perfil `Terreno - Terreno` de tipo `EG`, progresivas `0` a `831.3274` y cotas mínima y máxima leídas (anótalas;
   quedan dentro de 97.5..111.5), la vista `Eje 1 - Vista` con su `punto_insercion` (anótalo) y `bandas` mayor que 0
   (o el aviso de bandas, anótalo); `copia` terminada. En la parada, el resultado y la **simulación del paso 4**
   (`crear_rasante` con los 3 PVI): `numero_pvis: 3`, `numero_curvas: 1`, `progresiva_final: 831.3274`,
   `cota_maxima: 109.5248`, `tramos` con `pendiente_pct` 2.5 y −1.1592 y una curva de 120 m tipo `cresta` con
   `k: 32.7939`. Jose mira la vista de perfil en Civil 3D y confirma: **"El paso 3 está bien, sigue"**.

7. **Paso 4 del flujo: rasante.** `crear_rasante` sin `simular`: `despues` releído con `tipo: "FG"`, `numero_pvis: 3`,
   `numero_curvas: 1`, `progresiva_final: 831.3274`, `cota_minima: 100` y `cota_maxima: 109.5248`; `copia` terminada.
   Jose comprueba que la rasante se ve en `Eje 1 - Vista`. En la parada, el resultado y la **simulación del paso 5**
   (`crear_corredor` con `Eje 1`, `Rasante 1`, `Ensamblaje 1`, frecuencia 10 y `superficie_objetivo` `Terreno`):
   `despues` con `inicio: 0`, `fin: 831.3274`, las cuatro frecuencias en 10 y `superficie_objetivo: "Terreno"`. Jose
   confirma: **"El paso 4 está bien, sigue"**.

8. **Paso 5 del flujo: corredor (decisivo).** `crear_corredor` sin `simular`: `despues` releído con `existe: true`,
   `linea_base: "Línea base 1"`, `region: "Región 1"`, `ensamblaje: "Ensamblaje 1"`, `inicio: 0`, `fin: 831.3274`, las
   cuatro frecuencias en `10`, `superficie_objetivo: "Terreno"`, **`objetivos_superficie: 2`** y
   **`esta_desactualizado: false`**; `restaurar` con `herramienta: "deshacer_objetivos"` (anota `args.id`);
   `datos.estado_corredor` con `superficies: []`; `copia` terminada. Con `ejecutar_comando`
   `args={"comando":"_.ZOOM _W 900,1900 1700,2400"}`, Jose comprueba que el corredor se ve a lo largo del eje con los
   taludes llegando al terreno a los dos lados (terraplén al principio, desmonte al final). En la parada, el resultado
   y la **simulación del paso 6** (`crear_superficies_corredor` con `Corredor 1`): `despues` con
   `superficies: ["Corredor 1 - Datum", "Corredor 1 - Top"]`, `top_voladizo: "TopLinks"` y
   `datum_voladizo: "BottomLinks"`. Jose confirma: **"El paso 5 está bien, sigue"**. OK solo si
   `objetivos_superficie` es **2**, `fin` es **831.3274** y `esta_desactualizado` es `false`.

9. **Paso 6 del flujo: superficies Top y Datum (decisivo).** `crear_superficies_corredor` sin `simular`: `despues`
   releído igual que la simulación; `datos` con `superficie_top: "Corredor 1 - Top"`, `superficie_datum: "Corredor 1 -
   Datum"`, `voladizo_top: "TopLinks"`, `voladizo_datum: "BottomLinks"`, `contorno: "Extensión del corredor"` y
   `estado_corredor` con **dos** superficies y `esta_desactualizado: false`; `copia` terminada. En la parada, el resultado
   y la **simulación del paso 7** (`crear_secciones` con `Eje 1`, intervalo 20, 30 m a cada lado, 5 filas): `despues`
   con `numero_lineas: 43`, `progresiva_ultima: 831.3274`, `numero_vistas: 43`, `columnas: 9` y `fuentes_muestreadas`
   con `Terreno`, `Corredor 1`, `Corredor 1 - Top` y `Corredor 1 - Datum`. Jose confirma: **"El paso 6 está bien,
   sigue"**. OK solo si las dos superficies existen y el corredor no quedó desactualizado.

10. **Paso 7 del flujo: líneas de muestreo y vistas de sección (decisivo).** `crear_secciones` sin `simular`: `despues`
    releído con **43** líneas (`progresivas` de `0` a `831.3274`), **43** vistas en 9 columnas de 5 filas,
    `fuentes_muestreadas` **reales** con el terreno, el corredor y sus dos superficies, y `datos.lineas_sin_secciones: 0`;
    `copia` terminada. Jose mira dos o tres vistas de sección en Civil 3D: se ven el terreno, el corredor y las dos
    superficies. En la parada, el resultado y la **simulación del paso 8**: `calcular_volumenes` con `simular=true` y
    `terreno: "Terreno"` y `corredor: "Corredor 1"` (o `datum: "Corredor 1 - Datum"`; sin ellos también vale, porque en
    este dibujo hay un solo terreno y un solo corredor). Anota los argumentos literales. El agente debe **preguntar** los
    factores (`factor_esponjamiento` va al corte y `factor_compactacion` al relleno; por defecto `1`), no inventarlos.
    Esperado: `accion` = `Crear la superficie de volumen TIN 'Corredor 1 - Volumen' con base 'Terreno' y comparación
    'Corredor 1 - Datum', factor de esponjamiento (corte) 1 y de compactación (relleno) 1, y reconstruirla, con la tabla
    por progresivas del grupo 'Eje 1 - Muestreo'` y `despues` con `lineas_tabla: 43`. Jose confirma: **"El paso 7 está
    bien, sigue; sin factores"** (factores `1` en los dos, o sin pasarlos, para que el paso 14 pueda comparar). OK si
    las 43 líneas tienen secciones de las cuatro fuentes.

11. **Paso 8 del flujo: volúmenes (decisivo).** `calcular_volumenes` sin `simular`, con los argumentos de la simulación
    del paso 10. No usa comandos ni cuadros de diálogo. Anota la respuesta completa. Esperado: `despues` con
    `tipo: "TinVolume"`, `factor_corte: 1` y `factor_relleno: 1`; `datos.superficie_volumen: "Corredor 1 - Volumen"` (el
    nombre de la superficie creada); `datos.corte` **mayor que 0** y `datos.relleno` **mayor que 0** (la rasante va en
    terraplén al principio y en desmonte al final); `datos.neto` = `corte` − `relleno` (positivo: sobra material); con
    factores 1, `datos.sin_ajustar` igual a `corte`/`relleno`/`neto`; `datos.balance` y `datos.texto` de acuerdo con el
    signo del neto; `datos.convencion_neto_civil3d` (anótalo); y `datos.tabla_progresivas` con `numeros: true`,
    `numero_lineas: 43` y `tramos: 42` (anota `diferencia_con_superficie_pct`). `copia` terminada. En la parada final, el **resumen** del punto 5 de la receta: objetos creados, `id_log` de
    cada paso, corte, relleno y neto. Jose confirma: **"El paso 8 está bien, terminamos"**; esperado:
    `aprobar_resultado` del paso 8. Anota la hora de esta última confirmación.

12. **Una entrada de Deshacer y una copia por paso (decisivo).** Abre el menú Deshacer: respecto al paso 2 debe haber
    **ocho** entradas nuevas (`Executefunction`), una por paso del flujo. Las lecturas, simulaciones y `aprobar_resultado` no dejan
    ninguna. En `backups\` debe haber un archivo por paso del flujo (`<nombre del dwg>_<fecha>_<herramienta>.dwg`), salvo
    los pasos con `copia.reutilizada: true` (anótalos: no es fallo). `leer_log` con `args={"ultimas_n":80}`: por cada
    paso del flujo, una línea `llamada` con `"simular":true` antes de la real (las de los pasos 1 y 2, en el paso 3), la
    línea de la escritura real con `ok:true` y una línea `aprobacion` con `aprobado:true` que apunta a ella (**ocho**
    aprobaciones). OK solo si hay ocho escrituras con `ok:true` y ocho aprobaciones, y ninguna escritura real va antes
    de la confirmación de su paso.

13. **Las tareas 1 a 8 sobre el dibujo final (decisivo).** Solo lecturas, en este orden:
    - **Tarea 1**: `extension_superficie` con `args={"superficie":"Terreno"}` → `n_puntos: 9`, x 900..1700, y 1900..2400;
      `cota_superficie` con `args={"superficie":"Terreno","x":1300,"y":2150}` → `104.5`.
    - **Tarea 2**: `listar_alineamientos` → `Eje 1` de `0` a `831.3274`.
    - **Tareas 3 y 4**: `listar_perfiles` con `args={"alineamiento":"Eje 1"}` → `Terreno - Terreno` (EG) y `Rasante 1`
      (FG), los dos hasta `831.3274`; la vista `Eje 1 - Vista` se ve en pantalla con los dos perfiles.
    - **Tareas 5 y 6**: `estado_corredor` con `args={"corredor":"Corredor 1"}` → `esta_desactualizado: false` y las
      superficies `Corredor 1 - Top` y `Corredor 1 - Datum`; `listar_objetivos` con
      `args={"corredor":"Corredor 1","linea_base":"Línea base 1","region":"0"}` → los dos taludes con objetivo `Terreno`;
      `cota_superficie` con `args={"superficie":"Corredor 1 - Top","x":1150,"y":2000}` → **≈ 103.75** (la rasante en la
      progresiva 150: 100 + 2,5 % · 150; anota la cifra si difiere) y, en el mismo punto, `Corredor 1 - Datum` → una
      cota **menor** que la de la Top.
    - **Tarea 7**: `listar_lineas_muestreo` con `args={"alineamiento":"Eje 1","fuentes":true}` (deja una entrada en
      Deshacer, es normal) → el grupo `Eje 1 - Muestreo` con **43** líneas y las cuatro fuentes muestreadas.
    - **Tarea 8**: `listar_superficies` → `Corredor 1 - Volumen`, la superficie de volumen del paso 11, sigue en el
      dibujo con `tipo_superficie: "TinVolume"`.
    OK solo si las ocho comprobaciones dan OK (la tarea 1 puede quedar **parcial** por los intervalos de curvas, ver
    paso 4: anótalo aparte).

14. **Volumen a mano con el Panel de volúmenes (decisivo).** Es el "cálculo manual" de esta validación: Jose lo hace en
    Civil 3D sin el agente. Pestaña **Analizar** → grupo **Volúmenes y materiales** → **Panel de volúmenes** (*Analyze
    → Volumes and Materials → Volumes Dashboard*; si los nombres en español son otros, anótalos). En el panel, **Crear
    superficie de volumen** (*Create new volume surface*): nombre `Volumen manual`, superficie base `Terreno`, superficie
    de comparación `Corredor 1 - Datum`, factores de corte y de relleno `1`. Anota el corte, el relleno y el neto que
    muestra el panel. Compáralos con los del paso 11: diferencia = |agente − manual| / manual × 100. OK solo si el corte
    y el relleno difieren **menos del 1 %** (si el paso 11 aplicó factores distintos de 1, compara con
    `datos.sin_ajustar` del paso 11). Este paso añade una superficie al dibujo y una entrada
    en Deshacer: va después de los recuentos, no cuenta en el tiempo del flujo.

15. **Tiempo total.** Con `leer_log` (`args={"ultimas_n":120}`):
    - `T0` = `hora` de la primera llamada después de la anotada en el paso 2 (la primera lectura del paso 3).
    - `T1` = `hora` de la última `aprobacion` (la del paso 11).
    - **Esperas**: por cada parada, desde la última llamada antes de parar hasta la `aprobacion` que sigue a la
      confirmación de Jose (la del paso 3 va hasta la primera escritura). Suma las nueve esperas.
    - **Tiempo del agente** = (T1 − T0) − esperas. Anota también la suma de `ms` de las ocho escrituras (lo que tardó
      Civil 3D).
    Da el tiempo total de reloj (T1 − T0), el tiempo del agente y el porcentaje frente a **130 min**. No es decisivo:
    es el dato que pide la tarea 10.

16. **Cierre.** `guardar_dibujo` con `args={"pedido":"guarda el dibujo del flujo completo"}`: el dibujo es de prueba y
    sirve de punto de partida para validaciones siguientes. No borres nada.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_flujo_completo_<AAAAMMDD>.md` con: versión e idioma de Civil 3D,
versión del plugin (`ping`), plantilla usada, DWG (solo el nombre genérico, sin ruta ni datos de obra), una tabla paso /
resultado (OK, PARCIAL, FALLO) / respuesta literal / hora, la tabla de las tareas 1 a 8 del paso 13 (OK, PARCIAL o
FALLO), la comparación de volúmenes del paso 14 (agente, manual y diferencia en %) y los tiempos del paso 15, y las
respuestas a estas **preguntas para la memoria técnica**:

1. ¿El agente siguió la receta sin que este prompt le dijera qué llamar? ¿Simuló los pasos 1 y 2 al principio, cada
   paso siguiente antes de escribirlo, y paró y esperó en las **nueve** paradas (también ante la pregunta del paso 3)?
   Anota cualquier diferencia entre la receta y lo que hizo.
2. ¿Llamó a `aprobar_resultado` después de cada confirmación (ocho veces) y nunca antes? ¿Qué puso en `pedido` en cada
   llamada (el pedido entero o la parte de cada paso)?
3. ¿Hubo algún paso con un aviso que obligara a parar (radio, curva, corredor desactualizado, líneas sin secciones)?
4. ¿Qué argumentos pasó el agente a `calcular_volumenes` y qué preguntó sobre los factores? ¿Salió la tabla por
   progresivas con números y cuánto difiere de la superficie (`diferencia_con_superficie_pct`)? ¿Cuánto tardó (`ms`)?
5. ¿Qué estilo de superficie se usó y qué intervalos de curvas tiene? ¿Qué propuso el agente para las curvas de 1 y 5 m?
6. Corte, relleno y neto del agente y del Panel de volúmenes, con la diferencia en %.
7. Tiempo de reloj, tiempo del agente y porcentaje frente a los 130 min manuales.

**No pegues en el informe líneas de `mcp_log.jsonl` con rutas reales**: sustitúyelas por `<ruta>`. Sin capturas con
datos de obra.
