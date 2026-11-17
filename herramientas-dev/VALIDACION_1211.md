# Prompt de validación de la 1.21.1 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Dibujo**: una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica, en
  metros) guardada como `C:\Proyectos\dibujos_prueba\PRUEBA_1211.dwg` y abierta en Civil 3D 2027. Nunca el archivo
  base ni un dibujo real. Anota la carpeta del DWG: ahí aparecen `mcp_log.jsonl` y `backups\`.
- **Plugin y puente 1.21.1** desde la rama `tarea/6.9-norma-decisiones` (o la rama conjunta que indique el PR). El
  plugin no trae herramientas nuevas (solo sube de versión): **59** herramientas, **55** del plugin más **4** del
  puente (`buscar_api`, `guardar_receta`, `ejecutar_receta` y `verificar_norma`). Si la rama conjunta trae otras
  herramientas nuevas, anota el número que salga.
- `memoria-firma` clonado y al día (`git pull`) en `C:\Proyectos\memoria-firma`, con `MEMORIA_FIRMA_DIR` apuntando ahí
  (variable de usuario) y con la base de la norma en `C:\Proyectos\memoria-firma\normas\tablas\dg2018.sqlite`.
- `ARBA_MCP_CODIGO` no hace falta: esta validación no usa `ejecutar_codigo`.
- Reparto de papeles: el agente local **instala y verifica**; el código y este prompt los mantiene la sesión de
  desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.21.1**: las decisiones del dueño (17/11/2026) sobre los criterios de la DG-2018 que estaban
  «pendientes», todas en el puente (`PuenteMcp/norma.py` y `PuenteMcp/norma_alineamiento.py`):
  a. **Curva de transición**: la T302.11 A también pide un radio **superior** al de la tabla (igual no basta), como la
     B; en Tercera Clase manda la B. La nota del «No cumple» cita 302.05.07 y 302.05.06 («Si R requiere p>3%. Se debe
     usar curva de transición.») y ya no dice «pendiente de confirmar». Si alguna curva cumple, **un solo** aviso en
     `avisos`: «302.05.06 podría pedir clotoide aunque el radio supere la T302.11 si el peralte pasa del 3 % ...».
  b. **PI sin curva a más de 80 km/h con deflexión menor de 0°59′**: sigue «No cumple», pero la nota pide **quitar el
     PI** (no poner curva), y la propuesta de radios no propone curva: 0 con el aviso «elimina el PI».
  c. **0°59′**: «No cumple» si el PI tiene curva y «Aviso» si va sin curva, con la cita de 302.02. Los avisos de 500 m
     y 400 m siguen como aviso, con el texto de la norma.
  d. Pendiente máxima: igual (−1 % por altitud y después +1 % excepcional); solo cambia la nota.
  e. **Pendiente media**: el peor tramo de **2000 m o más** (antes, solo ventanas de 2000 m justos). **Descansos**:
     solo corta una subida un descanso de tramos de 2 % o menos que **seguidos sumen 500 m o más**.
  f. **Propuesta de radios** (`crear_alineamiento_desde_polilinea` con `velocidad_diseno`): con `simular=true` se
     sigue mostrando el mayor radio que cabe; en la **escritura real**, un PI bajo el mínimo (o con «elimina el PI») **no
     se crea sin confirmación**: responde error sin la llamada de escritura, con `radios_por_pi_para_confirmar`; la
     confirmación es repetir con esos radios en `radios_por_pi`. El aviso dice la velocidad que admite el radio, «baja
     la velocidad de diseño del tramo homogéneo (T204.01)» y la Sección 301 (excepciones solo con autorización previa
     del MTC); el nivel 2 no pasa del mínimo; y se avisa si otro reparto de radios haría cumplir a un PI.
  g. En Tercera Clase, cada PI donde manda la T302.11B dice la alternativa con clotoide de 302.05.06.
  **Los pasos 0, 2, 3, 4, 5, 6, 7 y 8 son los decisivos.**
- Miembros de la API que esta validación decide: **ninguno**. Todo lo nuevo es Python;
  `herramientas-dev/miembros_por_verificar_civil3d.md` no cambia en 1.21.1.
- Datos del proyecto (no inventes otros). `DATOS` significa, **dentro de `args`** (crear) o como argumentos directos
  (`verificar_norma`): `"velocidad_diseno":60,"clasificacion":"Carretera de tercera clase","orografia":"Ondulado","ubicacion":"rural","pavimentada":false,"peralte_maximo":8`.
  Con la base real: radio mínimo 125 m (T302.02 y T302.04), radio sin transición 210 m (T302.11B, «superior» → se
  propone **215**), tangente mínima entre curvas de sentido contrario 83 m y del mismo sentido 167 m (T302.01).
  `DATOS_RAPIDA` significa: `"velocidad_diseno":100,"clasificacion":"Autopista de primera clase","orografia":"Plano","ubicacion":"rural"`.
  Si con `DATOS_RAPIDA` responde `faltan` con `velocidad_diseno`, usa el **mayor valor de 90 o más** de sus
  `valores_validos` y anótalo; si no hay ninguno mayor de 80, **para el paso 4** y anótalo.

---

Eres el agente de validación de la versión 1.21.1 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (las del plugin con los argumentos
dentro de `args`; `verificar_norma` con los argumentos directos), salvo en los pasos que indican PowerShell. Ejecuta
los pasos en orden, anota la respuesta **literal** (JSON completo o, si es muy largo, las primeras 60 líneas; de
`crear_alineamiento_desde_polilinea` con la norma, siempre `propuesta_norma.radios_por_pi`, `propuesta_norma.pis`,
`propuesta_norma.avisos`, `verificacion.resumen`, `verificacion.avisos` y `verificacion.tabla`; de `verificar_norma`,
`resumen`, `avisos` y `tabla`) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` solo para lo que dicen los pasos 0 y 1, con el texto exacto. Si algo no funciona, es un hallazgo:
  no lo arregles con comandos.
- **Los datos del proyecto los da este prompt.** Si una herramienta responde `faltan` donde no se espera, anota la
  lista y para ese paso.
- **Nunca confirmes un radio bajo el mínimo por tu cuenta**: solo en el paso 7c, porque este prompt lo pide.
- Para contar las líneas de `mcp_log.jsonl`, en PowerShell: `(Get-Content <carpeta del DWG>\mcp_log.jsonl).Count`.
  Para contar las copias: `(Get-ChildItem <carpeta del DWG>\backups).Count`.
- No pegues en el informe rutas con nombres de obra (`<ruta>`).

## Pasos

0. **Versión, instalación, unidades y casos reales (decisivo).** Si `ping` ya da `1.21.1`, no reinstales. Si no, con
   Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama del PR:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`; en `PuenteMcp`, `uv sync`; reinicia el puente y
   el cliente MCP y abre Civil 3D con `PRUEBA_1211.dwg`. Después:
   - `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.21.1"`, `"hay_dibujo": true`.
   - La lista de herramientas: **59** (o el número de la rama conjunta). La descripción de
     `crear_alineamiento_desde_polilinea` contiene «Con la escritura real y algún PI así NO crea el eje: responde
     'sin_confirmar' y 'radios_por_pi_para_confirmar'».
   - PowerShell: `Test-Path C:\Proyectos\memoria-firma\normas\tablas\dg2018.sqlite` → `True`.
   - PowerShell, en `PuenteMcp`: `uv run --frozen python casos_norma_reales.py`. Esperado: la última línea
     «Resultado: N comprobaciones OK, **0 fallos**, ...» y código de salida 0 (`$LASTEXITCODE`). Pega las líneas de
     «Umbrales del código» y «Referencias que cita el código».
   - `leer_variable` con `args={"nombre":"INSUNITS"}` → `"valor": "6"` (metros). Si no es `"6"`, anótalo como hallazgo
     y, **solo en esta copia**, `ejecutar_comando` con
     `args={"comando":"INSUNITS 6 ","timeout_s":20,"pedido":"pone INSUNITS en metros en la copia de prueba"}` (con un
     espacio al final) y vuelve a leerla; si sigue sin ser `"6"`, **para aquí**.
   Si la versión no es `1.21.1`, `casos_norma_reales.py` da algún fallo o la base no está, **para aquí**.

1. **Polilíneas de prueba.** `ejecutar_comando`, en este orden y con **dos espacios** al final de cada texto, antes de
   la comilla (cada uno → `"result": "terminado"`):
   - `args={"comando":"_.-LAYER _M EJE_NORMA_A  ","timeout_s":20,"pedido":"crea la capa EJE_NORMA_A"}`
   - `args={"comando":"_.PLINE 1000,2000 1500,2000 1500,2700 2100,2700  ","timeout_s":20,"pedido":"dibuja la polilínea A"}`
   - `args={"comando":"_.-LAYER _M EJE_NORMA_B  ","timeout_s":20,"pedido":"crea la capa EJE_NORMA_B"}`
   - `args={"comando":"_.PLINE 1000,3500 1300,3500 1300,3800 1600,3800  ","timeout_s":20,"pedido":"dibuja la polilínea B"}`
   - `args={"comando":"_.-LAYER _M EJE_05  ","timeout_s":20,"pedido":"crea la capa EJE_05"}`
   - `args={"comando":"_.PLINE 1000,6000 2000,6000 4499.905,6021.816  ","timeout_s":20,"pedido":"dibuja la polilínea de 0,5°"}`
   - `args={"comando":"_.-LAYER _M EJE_U  ","timeout_s":20,"pedido":"crea la capa EJE_U"}`
   - `args={"comando":"_.PLINE 1000,8000 1600,8000 1600,8600 1000,8600  ","timeout_s":20,"pedido":"dibuja la polilínea en U"}`
   - `args={"comando":"_.-LAYER _M EJE_LARGO  ","timeout_s":20,"pedido":"crea la capa EJE_LARGO"}`
   - `args={"comando":"_.PLINE 0,12000 6500,12000  ","timeout_s":20,"pedido":"dibuja la recta de 6500 m"}`
   - `args={"comando":"_.-LAYER _M EJE_REPARTO  ","timeout_s":20,"pedido":"crea la capa EJE_REPARTO"}`
   - `args={"comando":"_.PLINE 1000,14000 1600,14000 1450,14259.808 1345.811,14850.693  ","timeout_s":20,"pedido":"dibuja la polilínea del reparto"}`
   Luego `leer_variable` con `args={"nombre":"CMDACTIVE"}` → `"valor": "0"`. La **A** gira 90° a la izquierda y 90° a
   la derecha (tramos 500, 700 y 600 m); la **B**, igual con tramos de 300 m; la de **0,5°** tiene un PI que desvía
   0,5° (tramos 1000 y 2500 m); la **U** gira dos veces 90° a la izquierda (tramos de 600 m); la **larga** es una recta
   de 6500 m; la del **reparto** gira 120° a la izquierda y 20° a la derecha (tramos 600, 300 y 600 m).

2. **a) La T302.11 sin «pendiente de confirmar»; la A estricta; un solo aviso de 302.05.06 (decisivo).**
   a. `crear_alineamiento_desde_polilinea` **sin** datos de la norma con
      `args={"nombre":"Eje 325","capa":"EJE_NORMA_A","radio":325,"pedido":"crea el eje A con radio 325"}` → `simulado:
      false`, `despues.numero_curvas: 2`, los dos PI con `radio_aplicado: 325`; **una** entrada nueva en Deshacer.
   b. `verificar_norma` con `{"norma":"DG-2018","tipo_elemento":"alineamiento","nombre":"Eje 325", DATOS,
      "pedido":"verifica el Eje 325 en Tercera Clase"}`. Esperado: «Radio para prescindir de la curva de transición»
      de `Curva PI 1` y `Curva PI 2`: **`Cumple`** (325 frente a 210, `criterio: "mayor_estricto"`, `referencia.id:
      "T302.11B"`), con `detalle.cita` que contiene «superior al señalado en la Tabla 302.11 B»; y en `avisos` **una
      sola vez** el texto que empieza por «302.05.06 podría pedir clotoide aunque el radio supere la T302.11 si el
      peralte pasa del 3 % (302.05.06, p. 145; PDF 146: «Si R requiere p>3%. Se debe usar curva de transición.»)».
      Ninguna fila ni aviso dice «pendiente de confirmar» ni «pendiente del dueño».
   c. Lo mismo con `"pavimentada": true` (101.05: se verifica como Segunda Clase, T302.11 A). Esperado: la fila de
      transición de las dos curvas con **`referencia.id: "T302.11A"`, `valor_exigido: 325`, `resultado: "No cumple"`**
      (325 = 325: igual no basta), `criterio: "mayor_estricto"`, y la `nota` «Requiere curva de transición: el radio no
      supera el de la T302.11A (igual no basta) y 302.05.06, p. 145; PDF 146: «Si R requiere p>3%. Se debe usar curva
      de transición.». El plugin solo crea arcos.»; `detalle.cita` con «La anterior tabla no significa que para radios
      superiores ...». Si el `valor_exigido` no es 325, anota el que salga: es el de la base.
   Si 2b sale «No cumple» o 2c sale «Cumple», **para aquí**.

3. **c) 0°59′ con y sin curva; textos de 500 y 400 m (decisivo).**
   a. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje 05 Curva","capa":"EJE_05","radio":40000,"pedido":"crea el eje de 0,5° con radio 40000"}` y
      con `args={"nombre":"Eje 05 Recto","capa":"EJE_05","radio":1,"radios_por_pi":"[0]","pedido":"crea el eje de 0,5° sin curva"}`.
      Esperado: el primero con `numero_curvas: 1` (radio 40000) y el segundo con `numero_curvas: 0`.
   b. `verificar_norma` del `Eje 05 Curva` con `DATOS` → «Deflexión no menor de 0°59′» de `Curva PI 1`: **`No
      cumple`** (≈ 0,5 frente a 0,983), `referencia.id: "R302.02-2"`, `detalle.cita` «No se usará nunca ángulos de
      deflexión menores de 59' (minutos).» y `nota` «Curva con una deflexión menor de 0°59′: la norma no la admite.
      Quita el PI y realinea el eje.».
   c. `verificar_norma` del `Eje 05 Recto` con `DATOS` → «Deflexión máxima sin curva» de `PI 1 (sin curva)`: `Cumple`
      (60 km/h: el cuadro permite hasta 1°30′) y «Deflexión no menor de 0°59′»: **`Aviso`** con la `nota` «Solo aviso:
      PI sin curva con una deflexión menor de 0°59′; valora quitar el PI.».
   d. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje U","capa":"EJE_U","radio":215,"pedido":"crea el eje en U con radio 215"}` y `verificar_norma`
      del `Eje U` con `DATOS`. Esperado en `Tangente PI 1–PI 2` (≈ 170 m, las dos curvas a la izquierda): «Tangente
      mínima entre curvas del mismo sentido (L mín.o)» con `valor_exigido: 167`; «Tangente entre curvas del mismo
      sentido mayor de 500 m»: **`Aviso`**, `criterio: "mayor_estricto"`, `nota` «Solo aviso: la norma pide más de 500
      m salvo que sea imposible usar una curva única o compuesta; en Tercera Clase puede ser menor (302.02, p. 126; PDF
      127).» y `detalle.cita` que contiene «deberá ser superior a 500 m»; «Evitar menos de 400 m entre curvas del mismo
      sentido»: `Aviso` con la `nota` «Solo aviso: recomendación de la norma.».
   Si 3b no sale «No cumple» o 3c no sale «Aviso», **para aquí**.

4. **b) A más de 80 km/h, un PI de 0,5°: «elimina el PI» (decisivo).** Cuenta las líneas de `mcp_log.jsonl` (N4) y
   las copias de `backups\` (C4).
   a. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje 05 Rapido","capa":"EJE_05","simular":true,"pedido":"simula el eje de 0,5° a 100 km/h", DATOS_RAPIDA}`.
      Esperado: `propuesta_norma.radios_por_pi: [0]`; PI 1 con `origen: "sin_curva"`, `radio_pedido: 0`,
      **`eliminar_pi: true`**, `cumple_minimo: false`, `requiere_confirmacion: true` y el `aviso` que empieza por «PI 1:
      elimina el PI (realinea la polilínea). Su deflexión (0.5°) es menor de 0°59′ y la norma no admite curva (302.02,
      p. 125; PDF 126: «No se usará nunca ángulos de deflexión menores de 59' (minutos).»); sin curva tampoco cumple,
      porque el cuadro de deflexión sin curva (C302.02-DEFLEX ...) solo cubre de 30 a 80 km/h. Una curva pediría además
      R > 3265x m ...» (con 0,5° exactos, R > 32658.594 m). **No** propone ninguna curva. En `verificacion` (prevista):
      `PI 1 (sin curva)`, «Deflexión máxima sin curva»: **`No cumple`**, `detalle.quitar_pi: true` y la `nota` con
      «quita el PI y realinea el eje» (sin «el PI necesita curva circular»); «Deflexión no menor de 0°59′»: `Aviso`.
   b. La misma llamada **sin** `simular` y con `"pedido":"crea el eje de 0,5° a 100 km/h"`. Esperado: **`ok: false`**,
      `error` que empieza por «No se creó el eje: la propuesta de la DG-2018 deja 1 PI bajo la norma (PI 1: elimina el
      PI (sin curva, 0))», `radios_por_pi_para_confirmar: "[{\"pi\": 1, \"radio\": 0}]"`, `sin_confirmar` con el PI 1
      y `eliminar_pi: true`. `listar_alineamientos` **no** tiene `Eje 05 Rapido`.
   c. `mcp_log.jsonl` con **N4 + 5** líneas (en 4a: `leer_variable` y dos simulaciones; en 4b: `leer_variable` y la
      simulación de ensayo; **ninguna** escritura), **ninguna** entrada nueva en Deshacer y `backups\` con **C4**
      archivos.
   Si 4a propone una curva o 4b crea el eje, **para aquí**.

5. **e) Pendiente media en todo tramo de 2000 m o más (decisivo).**
   a. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje Largo","capa":"EJE_LARGO","radio":100,"pedido":"crea el eje recto de 6500 m"}` →
      `numero_pi: 0`, `longitud` 6500.
   b. `crear_rasante` con
      `args={"alineamiento":"Eje Largo","nombre":"Rasante Media","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":1000,\"cota\":170},{\"progresiva\":2000,\"cota\":220},{\"progresiva\":3000,\"cota\":290}]","simular":true}`
      y, si no hay errores, lo mismo sin `simular`, con `"pedido":"crea la rasante 7-5-7"` (los PVI intermedios quedan
      como quiebres: 7 %, 5 % y 7 %).
   c. `verificar_norma` con `{"norma":"DG-2018","tipo_elemento":"rasante","nombre":"Rasante Media","alineamiento":"Eje Largo",
      "velocidad_diseno":60,"clasificacion":"Carretera de tercera clase","orografia":"Ondulado","pavimentada":false,
      "pedido":"verifica la rasante 7-5-7"}`. Esperado: la fila `Rasante (tramos de 2000 m o más)`, «Pendiente media en
      más de 2000 m»: **`No cumple`**, `valor_diseno: 6.333`, `valor_exigido: 6`, `progresiva: "0+000.000 a
      3+000.000"`, `detalle.longitud_tramo: 3000` y `detalle.cita` «La máxima pendiente promedio en tramos de longitud
      mayor a 2,000 m, no debe superar el 6%.». (Con la 1.19 salía `Cumple` con 6,00 %: todas las ventanas de 2000 m
      justos dan 6 %.) Anota también las filas de «Pendiente máxima».
   Si la pendiente media no sale «No cumple» con 6.333, **para aquí**.

6. **e) Un rellano corto no corta la subida (decisivo).**
   a. `crear_rasante` con
      `args={"alineamiento":"Eje Largo","nombre":"Rasante Descanso","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":3000,\"cota\":280,\"longitud_curva\":100},{\"progresiva\":3200,\"cota\":282,\"longitud_curva\":100},{\"progresiva\":6200,\"cota\":462}]","simular":true}`
      y, si las dos curvas caben, lo mismo sin `simular`, con `"pedido":"crea la rasante 6-1-6"` (6 % en 3000 m, 1 % en
      200 m y 6 % en 3000 m).
   b. `verificar_norma` como en 5c con `"nombre":"Rasante Descanso"`. Esperado: la fila `Tramos PVI 1–PVI 4`,
      «Descanso en subidas continuas de más del 5 %»: **`Aviso`**, `valor_diseno: 6200`, `valor_exigido: 3000`,
      `detalle.rellanos_cortos_m: 200`, `detalle.cita` con «un tramo de descanso de una longitud no menor de 500 m con
      pendiente no mayor de 2%» y la `nota` «Solo aviso: más de 3 km sin un descanso de 500 m o más seguidos con 2 % o
      menos (los rellanos más cortos no cortan la subida); revísalo a mano.». (Con la 1.19 no salía: el rellano de
      200 m cortaba la subida.)
   Si no sale el aviso, **para aquí**.

7. **f) La escritura real no crea un radio bajo el mínimo sin confirmación (decisivo).**
   a. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje Norma B","capa":"EJE_NORMA_B","simular":true,"pedido":"simula el eje B con la norma", DATOS}`.
      Esperado: `propuesta_norma.radios_por_pi: [215, 80]`; PI 2 con `radio_pedido: 80`, `nivel: 2`,
      `cumple_minimo: false`, **`requiere_confirmacion: true`** y un `aviso` que contiene «La polilínea no permite
      cumplir la norma en el PI 2 (mínimo 215 m», «mueve los vértices o baja la velocidad de diseño del tramo homogéneo
      (T204.01», «dos incumplimientos a la vez, el radio bajo el mínimo (radio mínimo (T302.02 ...: 125 m), radio para
      prescindir de la curva de transición (T302.11B ...: más de 210 m)) y la tangente bajo la mínima», «Ese radio
      admite V = √(127·R·(pmáx + fmáx)) = 48.3 km/h» y «Sección 301, p. 124; PDF 125». Anota
      `velocidad_que_admite.tabla_t20401` (la mayor velocidad de la T204.01 con la que 80 m cumpliría, o las que no
      tienen fila). La respuesta del plugin con el PI 2 `radio_aplicado: 80` (simulación).
   b. Cuenta las líneas de `mcp_log.jsonl` (N7) y las copias de `backups\` (C7). La misma llamada **sin** `simular` y
      con `"pedido":"crea el eje B con la norma"`. Esperado: **`ok: false`**, `error` que empieza por «No se creó el
      eje: la propuesta de la DG-2018 deja 1 PI bajo la norma (PI 2: 80 m con mínimo 215 m) y en la escritura real eso
      necesita la confirmación explícita del usuario. Solo se leyó INSUNITS y se hizo la simulación de ensayo: sin copia
      ni entrada en Deshacer.», `radios_por_pi_para_confirmar: "[{\"pi\": 2, \"radio\": 80}]"`, `seccion_301` con la
      cita «... con la condición de obtener previamente la autorización del Ministerio de Transportes y
      Comunicaciones.» y `propuesta_norma` presente. `mcp_log.jsonl` con **N7 + 2** líneas (`leer_variable` y la
      simulación de ensayo), **ninguna** entrada nueva en Deshacer, `backups\` con **C7** archivos y
      `listar_alineamientos` **sin** `Eje Norma B`.
   c. **Confirmación** (este prompt la da en nombre del dueño): la misma llamada sin `simular`, con
      `"radios_por_pi":"[{\"pi\": 2, \"radio\": 80}]"` y `"pedido":"crea el eje B con 80 m en el PI 2, confirmado"`.
      Esperado: `simulado: false`, `copia` presente, `despues.numero_curvas: 2`; en `propuesta_norma.pis[1]`:
      `origen: "usuario"`, `radio_pedido: 80`, `requiere_confirmacion: false` y el `aviso` «PI 2: tu radio 80 m queda por
      debajo del mínimo de la norma (215 m, T302.11B ...): la verificación lo marcará «No cumple».»; `verificacion`
      (con `listar_pis`) con «Radio mínimo» de `Curva PI 2` `No cumple`. **Una** entrada nueva en Deshacer.
   Si 7b crea el eje o 7c no lo crea, **para aquí**.

8. **f) y g) Reparto, nivel 2 sin pasar del mínimo y alternativa con clotoide (decisivo).**
   a. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje Reparto","capa":"EJE_REPARTO","simular":true,"pedido":"simula el eje del reparto", DATOS}`.
      Esperado: `propuesta_norma.radios_por_pi: [125, 215]`; PI 1 (≈ 120°) con `radio_pedido: 125`, `nivel: 1`; PI 2
      (≈ 20°) con `radio_pedido: 215` (no 470), `nivel: 2`, `incumple: []`, el `aviso` con «y sin pasar del mínimo: un
      incumplimiento, la tangente bajo la mínima» y **`reparto`** con `pi_anterior: 1`, `radio_anterior_maximo: 100` y
      el aviso «Reparto: el PI 2 queda bajo la norma porque la curva del PI 1 ocupa la recta entre los dos (...). Con R ≤
      100 m en el PI 1 (que ya queda bajo la norma), el PI 2 tendría su mínimo (215 m) dejando la tangente mínima (83
      m). ...». Si los números de 100 o 125 cambian por la base real, anótalos.
   b. La misma simulación con `"radios_por_pi":"[{\"pi\": 1, \"radio\": 100}]"` → el PI 2 con `cumple_minimo: true` y
      `radio_pedido: 215`.
   c. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje Norma A","capa":"EJE_NORMA_A","simular":true,"pedido":"simula el eje A con la norma", DATOS}`.
      Esperado: `radios_por_pi: [215, 215]` como en la 1.19; en cada PI, **`alternativa_con_clotoide`** con `radio:
      125`, `le_minimo: 30.758`, `le_maximo_24r: 54.772`, `le_maximo_1_5_le_minimo: 46.138`, **`le_maximo: 46.138`**
      (manda el más exigente), `referencias` con 302.05.06 p. 145 / PDF 146 y p. 146 / PDF 147, y la `nota` que dice que
      la norma usa la velocidad específica y aquí la de diseño; `verificacion.resumen` con 0 «No cumple» y
      `verificacion.avisos` con el aviso de 302.05.06 **una sola vez**.

9. **Deshacer y copias.** Abre el menú Deshacer de Civil 3D: las entradas nuevas son las del paso 1 (capas y
   polilíneas), una por cada eje y rasante creados de verdad (pasos 2a, 3a ×2, 3d, 5a, 5b, 6a y 7c) y **ninguna** de las
   simulaciones, las lecturas, `verificar_norma` ni los pasos 4 y 7b. No deshagas nada.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_1211_<AAAAMMDD>.md` con: versión de Civil 3D, DWG usado (nombre,
sin rutas de obra), commit de `memoria-firma` (`git -C C:\Proyectos\memoria-firma log -1 --format=%h`), la última línea
de `casos_norma_reales.py`, el valor de `INSUNITS` del paso 0, una tabla paso / resultado (OK, FALLO) / respuesta
literal / tiempo, las `tabla` de `verificar_norma` de los pasos 2b, 2c, 3b, 3c, 3d, 5c y 6b, `propuesta_norma.pis` de
los pasos 4a, 7a, 8a y 8c, los recuentos de `mcp_log.jsonl` y de `backups\` de los pasos 4 y 7 y, al final, la lista de
avisos, «No cumple» y textos que te parezcan dudosos frente a la norma, con su referencia, para que el dueño los revise.
