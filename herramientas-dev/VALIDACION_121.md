# Prompt de validación de la 1.21.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Dibujo**: una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica, en
  metros) guardada como `C:\Proyectos\dibujos_prueba\PRUEBA_121.dwg` y abierta en Civil 3D 2027. Nunca el archivo base
  ni un dibujo real. Anota la carpeta del DWG: ahí aparecen `mcp_log.jsonl` y `backups\`.
- **Archivo de puntos**: `C:\Proyectos\dibujos_prueba\terreno_corredor.csv`, el de los requisitos de
  `VALIDACION_111.md` (10 líneas, plano inclinado genérico). Si no existe, créalo con esas 10 líneas.
- **Plugin y puente 1.21.0** desde la rama `tarea/6.6-bordes-sobreancho` (o la rama conjunta que indique el PR). Hay
  **61** herramientas: **57** del plugin (las 55 de antes más `crear_bordes_calzada` y `listar_bordes_calzada`) más
  **4** del puente (`buscar_api`, `guardar_receta`, `ejecutar_receta` y `verificar_norma`). Si la rama conjunta trae
  herramientas de otras tareas, el número que diga su PR.
- `memoria-firma` clonado y al día (`git pull`) en `C:\Proyectos\memoria-firma`, con `MEMORIA_FIRMA_DIR` apuntando ahí
  (variable de usuario). Solo la necesitan los pasos 11 a 13 (el sobreancho de la norma).
- `ARBA_MCP_CODIGO` no hace falta: esta validación no usa `ejecutar_codigo`.
- Reparto de papeles: el agente local **instala y verifica**; el código y este prompt los mantiene la sesión de
  desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.21.0**:
  1. `crear_bordes_calzada` (escritura): crea los **dos bordes de calzada** de un eje, dos alineamientos desplazados a
     media calzada (izquierdo y derecho), en **una** entrada de Deshacer, y, si se le pasan, sus **ensanchamientos**
     (sobreanchos) por tramo. Después de crear **mide** cada borde a lo largo del eje y responde error si un borde no
     queda en su lado o si el sobreancho medido no es el pedido.
  2. `listar_bordes_calzada` (lectura): los bordes de un eje con su desplazamiento, su lado, las regiones que da Civil
     3D y lo **medido** (zonas ensanchadas con sus transiciones).
  3. En el **puente**, `crear_bordes_calzada` con `velocidad_diseno`, `numero_carriles`, `ancho_calzada` y
     `vehiculo_diseno` calcula el sobreancho de cada curva con la DG-2018 (302.09) y lo pone en el borde interior. **Las
     tablas del sobreancho las añade la tarea 6.4 a `dg2018.sqlite`**: si tu base aún no las tiene, los pasos 11 y 12
     solo comprueban el mensaje que lo dice. La herramienta debe funcionar también a **30 y 40 km/h** (el XML de criterios
     que se usa hoy en Civil 3D no funciona a 30 km/h): el paso 13 lo compara con un cálculo a mano.
- **Lo que esta validación decide** (no se sabe todavía; por eso hay preguntas): cómo modela Civil 3D la transición de
  un ensanchamiento (`OffsetAlignmentInfo.AddWidening`), el signo del desplazamiento de `CreateOffsetAlignment` y de
  `AddWidening`, y si `_.UNDO 1` quita los dos bordes. **Los pasos 0, 3, 4, 8, 9 y 10 son los decisivos.**

---

Eres el agente de validación de la versión 1.21.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre**, con los argumentos dentro de
`args`, salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 80 líneas; de `crear_bordes_calzada` real y de `listar_bordes_calzada`,
siempre los bloques `izquierdo`/`derecho` o `bordes` completos, con `medicion` y `regiones_civil3d`) y **el tiempo
total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` solo para lo que dicen los pasos, con el texto exacto. Si algo no funciona, es un hallazgo: no lo
  arregles con comandos.
- **Entre una escritura y su `_.UNDO 1` (paso 9) no hagas zoom, encuadre ni regeneración**, ni le pidas a Jose que
  los haga: un zoom deja su propia entrada en el menú Deshacer y el `_.UNDO 1` desharía esa.
- Para contar las líneas de `mcp_log.jsonl`, en PowerShell: `(Get-Content <carpeta del DWG>\mcp_log.jsonl).Count`.
  Para contar las copias: `(Get-ChildItem <carpeta del DWG>\backups).Count`.
- No pegues en el informe rutas con nombres de obra (`<ruta>`).

En los pasos siguientes, `ENS` es este valor de `ensanchamientos` (un sobreancho de 0,6 m en el borde **interior** de
cada curva del `Eje 1`, en el tramo del PC al PT, con 40 m de transición):
`"[{\"borde\":\"izquierdo\",\"progresiva_inicial\":220,\"progresiva_final\":345.6637,\"sobreancho\":0.6,\"longitud_transicion\":40},{\"borde\":\"derecho\",\"progresiva_inicial\":485.6637,\"progresiva_final\":611.3274,\"sobreancho\":0.6,\"longitud_transicion\":40}]"`

## Pasos

0. **Versión e instalación (decisivo).** Si `ping` ya da `1.21.0`, no reinstales. Si no, con Civil 3D cerrado, en
   PowerShell desde la raíz del repo en la rama del PR:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`; en `PuenteMcp`, `uv sync`; reinicia el puente y
   el cliente MCP y abre Civil 3D con `PRUEBA_121.dwg`. Después:
   - `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.21.0"`, `"hay_dibujo": true`.
   - La lista de herramientas: **61** (o la del PR conjunto). Están `crear_bordes_calzada` (parámetros `eje`,
     `desplazamiento`, `nombre_izquierdo`, `nombre_derecho`, `estilo`, `ensanchamientos`, `simular`, `pedido` y, del
     puente, `velocidad_diseno`, `numero_carriles`, `ancho_calzada`, `vehiculo_diseno`, `longitud_transicion`; el único
     obligatorio es `eje` y la descripción termina con «CON LA NORMA DG-2018 (puente, desde 1.21.0)...») y
     `listar_bordes_calzada` (`alineamiento`).
   - `leer_variable` con `args={"nombre":"INSUNITS"}` → `"valor": "6"` (metros).
   Si la versión no es exactamente `1.21.0` o falta alguna de las dos herramientas, **para aquí**.

1. **Preparar el dibujo** (deja entradas en Deshacer, es normal). En este orden:
   a. `ejecutar_comando` con `args={"comando":"_.-LAYER _M EJE_PRUEBA  ","timeout_s":20,"pedido":"crea la capa EJE_PRUEBA"}`
      y con `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","timeout_s":20,"pedido":"dibuja la polilínea del Eje 1"}`
      (**dos espacios** al final de cada texto; cada uno → `"result": "terminado"`).
   b. `ejecutar_comando` con `args={"comando":"_.-LAYER _M EJE_DESHACER  ","timeout_s":20,"pedido":"crea la capa EJE_DESHACER"}`
      y con `args={"comando":"_.PLINE 1000,3500 1300,3500 1300,3800 1600,3800  ","timeout_s":20,"pedido":"dibuja la polilínea del eje de deshacer"}`.
   c. `crear_superficie_tin` con
      `args={"nombre":"Terreno","archivo":"C:\\Proyectos\\dibujos_prueba\\terreno_corredor.csv","intervalo_curvas_menor":1,"intervalo_curvas_mayor":5,"pedido":"crea la superficie Terreno"}`.
   d. `crear_alineamiento_desde_polilinea` con `args={"nombre":"Eje 1","capa":"EJE_PRUEBA","radio":80,"pedido":"crea el Eje 1"}`
      → `despues.longitud: 831.3274`; dos curvas de radio 80: PI 1 a la **izquierda** (PC 220, PT 345.6637) y PI 2 a la
      **derecha** (PC 485.6637, PT 611.3274).
   e. `crear_alineamiento_desde_polilinea` con `args={"nombre":"Eje Deshacer","capa":"EJE_DESHACER","radio":80,"pedido":"crea el eje para probar el deshacer"}`.
   f. `crear_rasante` con
      `args={"alineamiento":"Eje 1","nombre":"Rasante 1","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":400,\"cota\":110,\"longitud_curva\":120},{\"progresiva\":831.3274,\"cota\":105}]","pedido":"crea la Rasante 1"}`.
   g. **Ensamblaje con carriles que admiten objetivo de desplazamiento.** `listar_ensamblajes` con
      `args={"pedido":"lista los ensamblajes"}`. Si un ensamblaje tiene carriles `LaneSuperelevationAOR` (o
      `BasicLaneTransition`) a los dos lados, anota su nombre: es `<ensamblaje>`. Si no (por ejemplo, si sus carriles
      son `BasicLane`, que no admite objetivos), pide a Jose que cree **a mano** `Ensamblaje Bordes` con los clics de
      los requisitos de `VALIDACION_111.md` (pasos 1 a 7 de «Ensamblaje hecho a mano»), con dos cambios: el nombre es
      `Ensamblaje Bordes`, la línea base en `1000,1600`, y en el paso 6 el carril es **LaneSuperelevationAOR** (paleta
      **Subensamblajes métricos de Civil 3D**, pestaña **Carriles** / *Lanes*; si no está, **BasicLaneTransition**) en
      vez de BasicLane. Después, `listar_ensamblajes` otra vez: `<ensamblaje>` = `Ensamblaje Bordes` con los dos
      carriles y los dos taludes. Anota los nombres literales de los subensamblajes (por ejemplo
      `LaneSuperelevationAOR - (Left)`).
   h. `guardar_dibujo` con `args={"pedido":"guarda el dibujo de prueba"}`.

2. **Simulación (sin tocar el dibujo).** Cuenta las líneas de `mcp_log.jsonl` (N2) y las copias de `backups\` (C2).
   `crear_bordes_calzada` con
   `args={"eje":"Eje 1","desplazamiento":3.6,"ensanchamientos":ENS,"simular":true,"pedido":"simula los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas"}`.
   Esperado: `simulado: true`; `antes`: `existe_izquierdo: false`, `existe_derecho: false`, `bordes_existentes: []`;
   `despues.izquierdo`: `nombre: "Eje 1 - Borde izq"`, `lado: "izquierdo"`, `desplazamiento_nominal: -3.6`, `estilo`
   y `capa` los de alineaciones de la configuración, y un ensanchamiento con `progresiva_inicial: 220`,
   `progresiva_final: 345.6637`, `sobreancho: 0.6`, `desplazamiento_total: 4.2`, `longitud_transicion: 40` y
   `transicion_prevista.entrada: [180, 220]`, `.salida: [345.6637, 385.6637]`; `despues.derecho` igual con
   `nombre: "Eje 1 - Borde der"`, `desplazamiento_nominal: 3.6` y su ensanchamiento de 485.6637 a 611.3274. Vuelve a
   contar: **N2 + 1** líneas y **C2** copias; ninguna entrada nueva en el menú Deshacer.

3. **Bordes de verdad: una entrada de Deshacer y lo medido (decisivo).** Cuenta N3 y C3.
   `crear_bordes_calzada` con
   `args={"eje":"Eje 1","desplazamiento":3.6,"ensanchamientos":ENS,"pedido":"crea los bordes del Eje 1 a 3,6 m con 0,6 m de sobreancho en las curvas"}`.
   Esperado:
   - `simulado: false`, `copia.ruta` con una copia en `backups\`, `despues.existe_izquierdo: true`,
     `despues.existe_derecho: true`, y el menú Deshacer con **una** entrada nueva (`Executefunction` o
     `EXECUTEFUNCTION`). `mcp_log.jsonl` con N3 + 1 líneas.
   - `despues.izquierdo`: `es_desplazado: true`, `eje: "Eje 1"`, `lado: "izquierdo"` (medido),
     `medicion.desplazamiento_medido: 3.6`, `medicion.muestreo.paso: 1`, `muestreo.fallidas` (anótalo) y
     **un** elemento en `medicion.ensanchamientos_medidos` con `sobreancho_maximo` ≈ 0.6. Su ensanchamiento pedido con
     `medido.sobreancho_medido` ≈ 0.6 y `medido.transicion` (`fuera`, `dentro` u `otra`).
   - `despues.derecho` igual, a la derecha, con su zona de 485.6637 a 611.3274.
   - **Anota literal**: `desplazamiento_nominal` y `lado_civil3d` de cada borde, `regiones_civil3d` completas de cada
     borde, `medido.zona_medida` y `medido.explicacion_transicion` de cada ensanchamiento,
     `longitud_transicion_aplicada` y todos los `avisos`.
   - Si responde **error** (por ejemplo «quedó a la derecha del eje», «sobreancho medido de ... y se pidió 0.6» o
     «Parece que AddWidening suma el valor al nominal»), **cópialo entero**: es la respuesta a una pregunta decisiva.
     Comprueba con `listar_alineamientos` si los bordes quedaron creados, anota el resultado del paso 4 igualmente y
     **para aquí** (salvo el paso 4).

4. **La transición del ensanchamiento en Civil 3D (decisivo; preguntas para Jose).** Pide a Jose que, **sin hacer
   zoom** (no lo necesitas; si hace falta para ver algo, que sea después del paso 9), seleccione `Eje 1 - Borde izq`
   en el Prospector (**Alineaciones** → **Alineaciones de desplazamiento** o dentro de `Eje 1`), clic derecho →
   **Propiedades de alineación** → pestaña **Parámetros de desplazamiento** (*Offset Parameters*), y te dicte:
   a. El **valor de desplazamiento nominal** (*Nominal offset value*) que muestra: ¿`-3.600` o `3.600`?
   b. Las **regiones de ensanchamiento** (*Widening regions*): progresiva inicial y final, desplazamiento del
      ensanchamiento (¿`4.200`, `-4.200` o `0.600`?) y las **transiciones de entrada y salida**: tipo y longitud, y su
      progresiva inicial y final.
   Con eso y lo del paso 3 responde en el informe:
   - **P1.** ¿El rango que se pasó (220 a 345.6637) quedó como **tramo pleno**, con la transición **fuera** (de 180 a
     220 y de 345.6637 a 385.6637), o la transición quedó **dentro** del rango? (compara `medido.transicion` y
     `zona_medida` con lo que dicta Jose).
   - **P2.** ¿La transición mide 40 m (`longitud_transicion_aplicada: true` y `longitud_entrada`/`longitud_salida` ≈ 40)
     o tiene otra longitud? ¿De qué tipo es (lineal u otra) según Civil 3D y según `puntos_entrada` (al 50 % debería
     dar ≈ 0.3 si es lineal)?
   - **P3.** El signo: ¿`desplazamiento_nominal` del borde izquierdo es negativo? ¿El desplazamiento del
     ensanchamiento que muestra Civil 3D lleva signo y es el total (4.2) o el incremento (0.6)?
   - **P4.** ¿Qué textos dan `lado_civil3d` (valores de `EntitySideType`) y `regiones_civil3d[].tipo`
     (`AlignmentRegionType`)?

5. **Lectura de los bordes.** Cuenta N5.
   a. `listar_bordes_calzada` con `args={"alineamiento":"Eje 1","pedido":"lista los bordes del Eje 1"}` →
      `eje: "Eje 1"`, `es_borde: false`, `numero_bordes: 2`, y los dos bordes con los mismos datos y mediciones que en
      el paso 3.
   b. `listar_bordes_calzada` con `args={"alineamiento":"Eje 1 - Borde izq"}` → `eje: "Eje 1"`,
      `consultado: "Eje 1 - Borde izq"`, `es_borde: true`, `numero_bordes: 2`.
   c. `listar_bordes_calzada` con `args={"alineamiento":"Eje Deshacer"}` → `numero_bordes: 0`, `bordes: []` y el aviso
      «El eje 'Eje Deshacer' no tiene bordes...».
   Vuelve a contar: **N5 + 3** líneas y ninguna entrada nueva en el menú Deshacer.

6. **Errores sin tocar nada.** Cuenta N6 y C6.
   a. Repite el paso 3 tal cual → `ok: false` y un `error` que contiene «Ya existe el alineamiento 'Eje 1 - Borde
      izq'. Alineamientos del dibujo:» con la lista (`Eje 1`, `Eje Deshacer`, `Eje 1 - Borde izq`, `Eje 1 - Borde der`).
   b. `crear_bordes_calzada` con `args={"eje":"Eje 1 - Borde izq","desplazamiento":3.6,"simular":true}` → error
      «El alineamiento 'Eje 1 - Borde izq' ya es un borde (desplazado de 'Eje 1')...».
   c. `crear_bordes_calzada` con
      `args={"eje":"Eje Deshacer","desplazamiento":3.6,"simular":true,"ensanchamientos":"[{\"borde\":\"izquierdo\",\"progresiva_inicial\":100,\"progresiva_final\":200,\"sobreancho\":0.5,\"longitud_transicion\":40},{\"borde\":\"izquierdo\",\"progresiva_inicial\":260,\"progresiva_final\":300,\"sobreancho\":0.5,\"longitud_transicion\":40}]"}`
      → error «Los ensanchamientos 1 y 2 del borde izquierdo se solapan (contando las transiciones)...».
   Vuelve a contar: **N6 + 3** líneas (una por llamada, con `ok: false`), **C6** copias y **ninguna** entrada nueva en
   el menú Deshacer (la comprobación previa por simulación responde antes de abrir el comando).

7. **Corredor con los bordes asignados.**
   a. `crear_corredor` con
      `args={"nombre":"Corredor 1","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"<ensamblaje>","frecuencia":5,"superficie_objetivo":"Terreno","pedido":"crea el Corredor 1"}`
      → `estado_corredor.esta_desactualizado: false`.
   b. `listar_regiones` con `args={"corredor":"Corredor 1"}` → anota la línea base y el índice de la región (`0`).
   c. `listar_objetivos` con `args={"corredor":"Corredor 1","linea_base":"<línea base>","region":"0"}` → anota los
      objetivos de tipo `desplazamiento` de los dos carriles (subensamblaje, grupo, lado, `parametro`) y que hoy están
      vacíos.
   d. `asignar_objetivos` con
      `args={"corredor":"Corredor 1","linea_base":"<línea base>","simular":true,"asignaciones":"[{\"region\":\"0\",\"subensamblaje\":\"<carril izquierdo>\",\"tipo\":\"desplazamiento\",\"objetivo\":\"Eje 1 - Borde izq\",\"opcion\":\"mas_cercano\"},{\"region\":\"0\",\"subensamblaje\":\"<carril derecho>\",\"tipo\":\"desplazamiento\",\"objetivo\":\"Eje 1 - Borde der\",\"opcion\":\"mas_cercano\"}]","pedido":"asigna los bordes a los carriles del Corredor 1"}`
      (con los nombres literales del paso 7c; si un carril tiene varios objetivos de desplazamiento, añade su
      `parametro`) → el `plan` con los dos. Repite sin `simular` → los dos aplicados, `restaurar` y **una** entrada nueva
      en el menú Deshacer.
   e. `reconstruir_corredor` con `args={"corredor":"Corredor 1"}` y `estado_corredor` → `esta_desactualizado: false`.
      Pregunta a Jose si en el **Visor de sucesos** de Civil 3D (*Event Viewer*) sale «Width not found» u «Offset
      target not found»: anota la respuesta.
   f. `listar_objetivos` como en 7c → los dos carriles con su borde.

8. **Sección en curva: la calzada ensanchada (decisivo).**
   a. `crear_secciones` con
      `args={"alineamiento":"Eje 1","intervalo":100,"ancho_izquierdo":20,"ancho_derecho":20,"inicio":100,"fin":300,"pedido":"secciones en 100, 200 y 300 del Eje 1"}`
      → tres líneas (0+100, 0+200 y 0+300) con el corredor entre sus fuentes y tres vistas de sección.
   b. Pide a Jose que, en cada vista de sección, mida con `_.DIST` (o con la rejilla de la vista) la distancia
      **horizontal** del eje (desplazamiento 0) al **borde exterior del carril izquierdo** y al del derecho, y te la
      dicte. Esperado, con la transición fuera del rango (P1 = «fuera»): en **0+100** (tangente), 3.60 a cada lado; en
      **0+200** (a mitad de la transición de entrada, de 180 a 220), ≈ 3.90 a la izquierda y 3.60 a la derecha; en
      **0+300** (dentro de la curva 1, que gira a la izquierda), **4.20 a la izquierda** y 3.60 a la derecha. Si P1 fue
      «dentro», en 0+200 la izquierda es 3.60. Pega lo que dicte Jose y di si cuadra con `listar_bordes_calzada`.
   c. Si el carril no llega al borde ensanchado en 0+300 (3.60 en vez de 4.20), anota los objetivos del paso 7f y el
      mensaje del Visor de sucesos: es un hallazgo.

9. **`_.UNDO 1` sin zoom quita los dos bordes (decisivo).** **Sin zoom, encuadre ni regeneración** desde aquí hasta el
   final del paso.
   a. `crear_bordes_calzada` con `args={"eje":"Eje Deshacer","desplazamiento":3.6,"pedido":"crea los bordes del eje de deshacer"}`
      → `existe_izquierdo: true`, `existe_derecho: true`.
   b. `listar_alineamientos` → están `Eje Deshacer - Borde izq` y `Eje Deshacer - Borde der`.
   c. Jose dicta la entrada de arriba del menú Deshacer (debe ser `Executefunction`).
   d. `ejecutar_comando` con `args={"comando":"_.UNDO 1 ","timeout_s":20,"pedido":"deshace la creación de los bordes"}`.
   e. `listar_alineamientos` y `listar_bordes_calzada` con `args={"alineamiento":"Eje Deshacer"}` → ¿se quitaron
      **los dos** bordes? Anota «se quitaron los dos», «se quitó uno» o «siguen», con las listas literales de antes y de
      después.

10. **Deshacer y copias.** Abre el menú Deshacer de Civil 3D: las entradas nuevas de esta validación son las del paso 1,
    **una** del paso 3, **una** del 7a, **una** del 7d (real), las de 7e y 8a y la del paso 9 (ya deshecha); **ninguna**
    de los pasos 2, 5 y 6. En `backups\`, las copias de las escrituras reales (o reutilizadas si el archivo no cambió).
    No deshagas nada más.

11. **Sobreancho de la norma: sin datos, sin llamar a Civil 3D (decisivo).** Cuenta N11.
    `crear_bordes_calzada` con `args={"eje":"Eje 1","nombre_izquierdo":"Eje 1 - Norma izq","nombre_derecho":"Eje 1 - Norma der","simular":true,"velocidad_diseno":60}`.
    Esperado, según tu base:
    - **Si la base aún no tiene las tablas del sobreancho** (la 6.4 sin terminar): `ok: false` y un `error` que
      contiene «no tiene todavía las tablas del sobreancho» y «No se llamó a Civil 3D». Anótalo y **salta los pasos 12 y
      13**.
    - **Si las tiene**: `ok: false`, `error` que empieza por «Faltan datos del proyecto para calcular el sobreancho de
      la DG-2018: numero_carriles, ancho_calzada, vehiculo_diseno» y `faltan` con `vehiculo_diseno` y sus
      `valores_validos` (anótalos).
    En los dos casos, vuelve a contar: **N11** (ninguna línea nueva) y ninguna entrada nueva en el menú Deshacer.

12. **Sobreancho de la norma simulado** (solo si el paso 11 dio `faltan`). Pide a Jose el vehículo de diseño de la lista
    `valores_validos` (anota cuál). `crear_bordes_calzada` con
    `args={"eje":"Eje 1","nombre_izquierdo":"Eje 1 - Norma izq","nombre_derecho":"Eje 1 - Norma der","simular":true,"velocidad_diseno":60,"numero_carriles":2,"ancho_calzada":7.2,"vehiculo_diseno":"<vehículo>","pedido":"simula los bordes del Eje 1 con el sobreancho de la norma"}`.
    Esperado: la respuesta del plugin (`simulado: true`) y `propuesta_norma` con `seccion: "302.09"`,
    `desplazamiento: 3.6` (`ancho_calzada / 2`), `longitud_transicion: 40`, `proyecto.l_m` (anótalo) y dos `curvas`:
    PI 1 con `borde: "izquierdo"` y PI 2 con `borde: "derecho"`, cada una con `termino_geometrico`,
    `termino_velocidad`, `sobreancho_formula` (compruébalo a mano: 2 (80 − √(6400 − L²)) + 60 / (10 √80)),
    `factor_reduccion` o `nota_factor`, `sobreancho`, `se_aplica` y `manda`; `ensanchamientos` del PC al PT con
    `longitud_transicion: 40` (o ninguno si Sa < 0,40) y `pendiente_de_decision` con la transición del peralte y el
    redondeo. Pega `propuesta_norma` completo.

13. **Comparación a 30 km/h con curvas cerradas** (solo si el paso 11 dio `faltan`). Se compara lo que propone la
    herramienta con lo que Jose calcula **a mano con la DG-2018** (302.09: Sa = n (R − √(R² − L²)) + V / (10 √R) y la
    Tabla 302.20). **No uses ni copies valores del XML de criterios de Civil 3D**: no son de la norma.
    a. `ejecutar_comando` con `args={"comando":"_.-LAYER _M EJE_30  ","timeout_s":20,"pedido":"crea la capa EJE_30"}` y con
       `args={"comando":"_.PLINE 1000,4500 1300,4500 1300,4800 1600,4800  ","timeout_s":20,"pedido":"dibuja la polilínea del eje de 30 km/h"}`
       (**dos espacios** al final).
    b. `crear_alineamiento_desde_polilinea` con
       `args={"nombre":"Eje 30","capa":"EJE_30","radio":25,"radios_por_pi":"[25, 30]","pedido":"crea el eje de 30 km/h con radios 25 y 30"}`
       → dos curvas: PI 1 a la izquierda con R = 25 y PI 2 a la derecha con R = 30. `listar_pis` con
       `args={"alineamiento":"Eje 30"}`: anota el PC y el PT de cada curva.
    c. Pide a Jose los datos de esta vía: **número de carriles**, **ancho de calzada** y **vehículo de diseño** (de la
       lista del paso 11). No los supongas.
    d. `crear_bordes_calzada` con
       `args={"eje":"Eje 30","simular":true,"velocidad_diseno":30,"numero_carriles":<n>,"ancho_calzada":<ancho>,"vehiculo_diseno":"<vehículo>","pedido":"simula los bordes del eje de 30 km/h con el sobreancho de la norma"}`.
       Esperado: **ningún** aviso de «fuera del rango de la figura 302.18B» (30 km/h está dentro); por curva,
       `termino_velocidad` = 30 / (10 √R) (**0.6** con R = 25 y **0.5477** con R = 30), `termino_geometrico` =
       n (R − √(R² − L²)), el `factor_reduccion` de la T302.20 o su `nota_factor`, `sobreancho`, `se_aplica` y, si se
       aplica, el ensanchamiento en el borde **interior** (izquierdo en el PI 1, derecho en el PI 2) del PC al PT, con 40 m
       de transición (o recortada, con aviso, si no cabe).
    e. Jose calcula a mano cada curva con la DG-2018 y te dicta su resultado. Pon en el informe una tabla: curva, R, L,
       V, n, término geométrico, término de velocidad, factor de la T302.20, Sa a mano, Sa de la herramienta y diferencia.
       Una diferencia de más de 0,01 m, o un factor distinto, es un hallazgo: anota cómo lo calculó Jose.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_121_<AAAAMMDD>.md` con: versión de Civil 3D, DWG usado (nombre,
sin rutas de obra), commit de `memoria-firma` (`git -C C:\Proyectos\memoria-firma log -1 --format=%h`), el nombre del
ensamblaje y de sus subensamblajes (paso 1g), una tabla paso / resultado (OK, FALLO) / respuesta literal / tiempo, los
bloques `izquierdo` y `derecho` del paso 3 completos (con `medicion` y `regiones_civil3d`), **las respuestas P1 a P4
del paso 4** con lo que dictó Jose, las medidas del paso 8, el resultado del `_.UNDO 1` del paso 9 y los recuentos de
`mcp_log.jsonl` y de `backups\` de los pasos 2, 3, 5, 6 y 11 y, si se hizo, la tabla de comparación a 30 km/h del
paso 13. Al final, la lista de lo que te parezca dudoso (una
transición que no cuadra, un borde que no llega, un sobreancho de la norma que no te salga a mano) para que el dueño lo
revise.
