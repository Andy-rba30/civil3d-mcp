# Prompt de validación de la 1.19.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Dibujo**: una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica, en
  metros) guardada como `C:\Proyectos\dibujos_prueba\PRUEBA_119.dwg` y abierta en Civil 3D 2027. Nunca el archivo base
  ni un dibujo real. Anota la carpeta del DWG: ahí aparecen `mcp_log.jsonl` y `backups\`.
- **Plugin y puente 1.19.0** desde la rama `tarea/4.3-norma-alineamiento` (o la rama conjunta de la ronda que indique
  el PR). El plugin no trae herramientas nuevas (solo sube de versión): hay **59** herramientas, **55** del plugin más
  **4** del puente (`buscar_api`, `guardar_receta`, `ejecutar_receta` y `verificar_norma`).
- `memoria-firma` clonado y al día (`git pull`) en `C:\Proyectos\memoria-firma`, con `MEMORIA_FIRMA_DIR` apuntando ahí
  (variable de usuario) y con la base de la norma en `C:\Proyectos\memoria-firma\normas\tablas\dg2018.sqlite`.
- `ARBA_MCP_CODIGO` no hace falta: esta validación no usa `ejecutar_codigo`.
- Reparto de papeles: el agente local **instala y verifica**; el código y este prompt los mantiene la sesión de
  desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.19.0**: `crear_alineamiento_desde_polilinea` acepta, **dentro de `args`**, seis parámetros
  opcionales nuevos que añade el **puente**: `velocidad_diseno`, `clasificacion`, `orografia`, `ubicacion`,
  `peralte_maximo` y `pavimentada` (los mismos valores que en `verificar_norma`). Con ellos, el puente:
  1. comprueba los datos del proyecto; si falta alguno, responde qué falta **sin llamar a Civil 3D**;
  2. lee `INSUNITS` (debe ser 6, metros);
  3. simula una vez para leer los PI de la polilínea;
  4. propone en cada PI el **radio mínimo de la DG-2018**: el mayor entre el radio mínimo (T302.02/T302.04), el radio
     sin curva de transición (T302.11) y L > 30 (10 − Δ) con deflexión pequeña, redondeado hacia arriba a 5 m;
  5. comprueba que cabe dejando la tangente mínima entre curvas; si no cabe, avisa y pide el mayor radio que cabe;
  6. crea el eje con esos radios (la escritura la sigue haciendo el plugin: copia, **una** entrada de Deshacer, log);
  7. adjunta `verificacion`, la de `verificar_norma` (prevista si se simula).
  Sin ninguno de los seis parámetros, la herramienta funciona igual que en la 1.18.0. **Los pasos 0, 3, 4, 5, 6 y 7
  son los decisivos.**
- Miembros de la API que esta validación decide: **ninguno**. Todo lo nuevo es Python; el plugin no cambia y
  `herramientas-dev/miembros_por_verificar_civil3d.md` no cambia en 1.19.0.
- Datos del proyecto de esta validación (no inventes otros): **Carretera de tercera clase, Ondulado, rural, no
  pavimentada, peralte máximo 8 %, 60 km/h**. Con la base real: radio mínimo 125 m (T302.02 y T302.04), radio sin
  transición 210 m (T302.11B, «superior»: hace falta más de 210, así que se propone **215**), tangente mínima entre
  curvas de sentido contrario 83 m (T302.01).

---

Eres el agente de validación de la versión 1.19.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (las del plugin, también
`crear_alineamiento_desde_polilinea` con los parámetros nuevos, con los argumentos dentro de `args`; `verificar_norma`
con los argumentos directos), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta
**literal** (JSON completo o, si es muy largo, las primeras 60 líneas; de `crear_alineamiento_desde_polilinea` con la
norma, siempre `propuesta_norma.radios_por_pi`, `propuesta_norma.pis`, `propuesta_norma.avisos`,
`verificacion.resumen` y `verificacion.tabla`) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` solo para lo que dicen los pasos 0 y 1, con el texto exacto. Si algo no funciona, es un hallazgo:
  no lo arregles con comandos.
- **Los datos del proyecto los da este prompt.** Si la herramienta responde `faltan` fuera del paso 3, anota la lista y
  para ese paso.
- Para contar las líneas de `mcp_log.jsonl`, en PowerShell: `(Get-Content <carpeta del DWG>\mcp_log.jsonl).Count`.
  Para contar las copias: `(Get-ChildItem <carpeta del DWG>\backups).Count`.
- No pegues en el informe rutas con nombres de obra (`<ruta>`).

En los pasos siguientes, `DATOS` significa estos seis pares, que se añaden **dentro de `args`**:
`"velocidad_diseno":60,"clasificacion":"Carretera de tercera clase","orografia":"Ondulado","ubicacion":"rural","pavimentada":false,"peralte_maximo":8`

## Pasos

0. **Versión, instalación y unidades (decisivo).** Si `ping` ya da `1.19.0`, no reinstales. Si no, con Civil 3D
   cerrado, en PowerShell desde la raíz del repo en la rama del PR:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`; en `PuenteMcp`, `uv sync`; reinicia el puente y
   el cliente MCP y abre Civil 3D con `PRUEBA_119.dwg`. Después:
   - `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.19.0"`, `"hay_dibujo": true`.
   - La lista de herramientas: **59**. En `crear_alineamiento_desde_polilinea`, dentro de `args`, aparecen además
     `velocidad_diseno`, `clasificacion`, `orografia`, `ubicacion`, `peralte_maximo` y `pavimentada`; el único
     obligatorio es `nombre` (`radio` ya no lo es) y la descripción termina con el párrafo «CON LA NORMA DG-2018
     (puente, desde 1.19.0)...».
   - PowerShell: `[Environment]::GetEnvironmentVariable('MEMORIA_FIRMA_DIR','User')` → `C:\Proyectos\memoria-firma` y
     `Test-Path C:\Proyectos\memoria-firma\normas\tablas\dg2018.sqlite` → `True`.
   - `leer_variable` con `args={"nombre":"INSUNITS"}` → `"valor": "6"` (metros). Si no es `"6"`, anótalo como hallazgo
     y, **solo en esta copia**, `ejecutar_comando` con
     `args={"comando":"INSUNITS 6 ","timeout_s":20,"pedido":"pone INSUNITS en metros en la copia de prueba"}` (con un
     espacio al final) y vuelve a leerla; si sigue sin ser `"6"`, **para aquí**.
   Si la versión no es exactamente `1.19.0`, faltan los parámetros nuevos o la base no está, **para aquí**.

1. **Polilíneas de prueba.** `ejecutar_comando`, en este orden y con **dos espacios** al final de cada texto, antes de
   la comilla (cada uno → `"result": "terminado"`):
   - `args={"comando":"_.-LAYER _M EJE_NORMA_A  ","timeout_s":20,"pedido":"crea la capa EJE_NORMA_A y déjala actual"}`
   - `args={"comando":"_.PLINE 1000,2000 1500,2000 1500,2700 2100,2700  ","timeout_s":20,"pedido":"dibuja la polilínea A"}`
   - `args={"comando":"_.-LAYER _M EJE_NORMA_B  ","timeout_s":20,"pedido":"crea la capa EJE_NORMA_B y déjala actual"}`
   - `args={"comando":"_.PLINE 1000,3500 1300,3500 1300,3800 1600,3800  ","timeout_s":20,"pedido":"dibuja la polilínea B"}`
   Luego `leer_variable` con `args={"nombre":"CMDACTIVE"}` → `"valor": "0"`. La **A** va al este 500 m, al norte 700 m
   y al este 600 m (dos giros de 90°: izquierda y derecha). La **B** va al este, al norte y al este con tramos de
   300 m (la de la 1.18.0, desplazada).

2. **Sin `velocidad_diseno`, todo igual que en la 1.18.0.** Cuenta las líneas de `mcp_log.jsonl` (N0). Luego
   `crear_alineamiento_desde_polilinea` con
   `args={"nombre":"Eje Sin Norma 119","capa":"EJE_NORMA_A","radio":80,"simular":true,"pedido":"simula el eje A con radio 80"}`.
   Esperado: `simulado: true`, `despues.numero_curvas: 2`, los dos PI con `radio_aplicado: 80`, **sin** las claves
   `propuesta_norma` ni `verificacion`, y `mcp_log.jsonl` con **N0 + 1** líneas (solo esta llamada).

3. **Sin un dato obligatorio, pregunta sin llamar a Civil 3D (decisivo).** Cuenta las líneas de `mcp_log.jsonl` (N1).
   a. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje Norma A","capa":"EJE_NORMA_A","simular":true,"velocidad_diseno":60,"clasificacion":"Carretera de tercera clase","orografia":"Ondulado","ubicacion":"rural"}`.
      Esperado: `ok: false`; `error` que empieza por «Faltan datos del proyecto para proponer los radios de la DG-2018:
      pavimentada, peralte_maximo» y dice «No se llamó a Civil 3D: ni copia, ni Deshacer, ni línea en mcp_log.jsonl.»;
      `faltan` con `pavimentada` (`valores_validos: [true, false]`) y `peralte_maximo` (con los valores de la base).
   b. Lo mismo con `args={"nombre":"Eje Norma A","capa":"EJE_NORMA_A","radio":80,"clasificacion":"Carretera de tercera clase"}`
      (un dato del proyecto sin velocidad). Esperado: `ok: false` y `faltan` con `velocidad_diseno` entre otros.
   c. Lo mismo con `DATOS` pero `"velocidad_diseno":65`. Esperado: `faltan` solo con `velocidad_diseno`, el motivo dice
      que la norma no permite interpolar y `valores_validos` incluye 60 y no 65.
   d. Vuelve a contar: **N1** (ninguna línea nueva) y ninguna entrada nueva en el menú Deshacer.

4. **Polilínea A simulada: propone el mínimo de la norma (decisivo).** Cuenta las líneas de `mcp_log.jsonl` (N2) y las
   copias de `backups\` (C2). `crear_alineamiento_desde_polilinea` con
   `args={"nombre":"Eje Norma A","capa":"EJE_NORMA_A","simular":true,"pedido":"simula el eje A con los radios de la norma a 60 km/h", DATOS}`.
   Esperado:
   - La respuesta del plugin: `simulado: true`, `despues.numero_curvas: 2`, los dos PI con `radio_aplicado: 215`, y
     `avisos` del plugin vacío o `null`.
   - `propuesta_norma.unidades.INSUNITS: "6"`; `radios_por_pi: [215, 215]`; `radio: 215`; `tramos: [500, 700, 600]`;
     `tangentes_minimas` con una fila `tramo: "PI 1–PI 2"`, `sentido: "sentido contrario"`, `valor: 83` y `referencia`
     que empieza por `T302.01`; `avisos: []`.
   - `propuesta_norma.pis[0]` (PI 1): `progresiva: 500`, `deflexion_grados: 90`, `giro: "izquierda"`,
     `origen: "norma"`, `radio_minimo_norma: 215`, `radio_pedido: 215`, `cumple_minimo: true`,
     `radio_maximo_que_cabe: 500`, `aviso: null`. En `manda`: `comprobacion: "Radio para prescindir de la curva de
     transición"`, `referencia` que empieza por `T302.11B (302.05.07, p. 146; PDF 147)` y `explicacion` con «210 m
     redondeado hacia arriba a un múltiplo de 5 m (desigualdad estricta)». En `minimos`, el «Radio mínimo» con
     `valor_exigido: 125` y `referencia.id` `T302.02` (o `T302.04`) con sus páginas, y la transición con
     `valor_exigido: 210` y `radio_necesario: 215`.
   - `propuesta_norma.pis[1]` (PI 2): `giro: "derecha"`, `radio_pedido: 215`, `radio_maximo_que_cabe: 402`,
     `progresiva` ≈ 1107.721.
   - `verificacion`: `ok: true`, `verificado: true`, **`prevista: true`**, la `nota` «Verificación prevista sobre la
     tabla de PI de la simulación...», y **`resumen` con 0 «No cumple»**. En `tabla`: «Radio mínimo» de las dos curvas
     `Cumple` (215 frente a 125); «Radio para prescindir de la curva de transición» `Cumple` (215 frente a 210,
     T302.11B); «Longitud mínima de curva (3 V)» `Cumple` (337.721 frente a 180); `Tangente PI 1–PI 2`, «Tangente mínima
     entre curvas de sentido contrario (L mín.s)», `Cumple` con 270 frente a 83. Como la tangente es de 270 m (más de
     200), no hay fila de relación de radios.
   - `mcp_log.jsonl` con **N2 + 3** líneas: `leer_variable` y dos de `crear_alineamiento_desde_polilinea` (la
     simulación de ensayo y la final), las tres con el `pedido` y las dos de crear con `"simular": true`. La primera de
     crear trae `"radio": 1` (o `1.0`, provisional); la segunda, `"radios_por_pi": "[215, 215]"`.
   - **Ninguna** entrada nueva en el menú Deshacer y `backups\` con **C2** archivos.
   Pega `propuesta_norma.pis` y `verificacion.tabla` completos. Si los radios no son 215 y 215 o hay algún «No
   cumple», **para aquí**.

5. **Polilínea A de verdad: una entrada de Deshacer y la verificación adjunta (decisivo).** Cuenta las líneas de
   `mcp_log.jsonl` (N3). `crear_alineamiento_desde_polilinea` con
   `args={"nombre":"Eje Norma A","capa":"EJE_NORMA_A","pedido":"crea el eje A con los radios de la norma a 60 km/h", DATOS}`
   (sin `simular`). Esperado:
   - `simulado: false`, `copia.ruta` con una copia en `backups\`, `despues.numero_curvas: 2`, y en `datos.curvas` dos
     curvas con `radio: 215`.
   - `propuesta_norma.radios_por_pi: [215, 215]`, igual que en el paso 4.
   - `verificacion`: `verificado: true`, **sin** `prevista`, `lecturas.listar_pis` con un `id_log` y **`resumen` con 0
     «No cumple»** (la misma `tabla` que en el paso 4).
   - `mcp_log.jsonl` con **N3 + 4** líneas: `leer_variable`, crear (simulación de ensayo), crear (escritura real) y
     `listar_pis` del `Eje Norma A`; el `id` de la última es el de `verificacion.lecturas.listar_pis`.
   - El menú Deshacer con **una** entrada nueva (`Executefunction` o `EXECUTEFUNCTION`) para este paso. No deshagas
     nada.
   Después, `listar_pis` con `args={"alineamiento":"Eje Norma A"}` → `unidades: "Meters"`, `numero_curvas: 2`; PI 1
   con `radio: 215`, `giro: "izquierda"`, `progresiva_pc: 285`, `progresiva_pt` ≈ 622.721; PI 2 con `radio: 215`,
   `giro: "derecha"`, `progresiva_pc` ≈ 892.721; `fin.progresiva` ≈ 1615.442. Y `verificar_norma` con
   `{"norma": "DG-2018", "tipo_elemento": "alineamiento", "nombre": "Eje Norma A", "velocidad_diseno": 60,
   "clasificacion": "Carretera de tercera clase", "orografia": "Ondulado", "ubicacion": "rural", "pavimentada": false,
   "peralte_maximo": 8}` → el mismo `resumen` (0 «No cumple»). Si el eje no se creó con 215 y 215 o la verificación
   tiene algún «No cumple», **para aquí**.

6. **Polilínea B: el mínimo no cabe en el PI 2 y no se fuerza (decisivo).** `crear_alineamiento_desde_polilinea` con
   `args={"nombre":"Eje Norma B","capa":"EJE_NORMA_B","simular":true,"pedido":"simula el eje B con los radios de la norma a 60 km/h", DATOS}`.
   Esperado:
   - `propuesta_norma.radios_por_pi: [215, 80]`, `tramos: [300, 300, 300]`.
   - PI 1: `radio_pedido: 215`, `cumple_minimo: true`, `radio_maximo_que_cabe: 217`.
   - PI 2: `radio_minimo_norma: 215`, `radio_maximo_que_cabe: 2`, `radio_pedido: 80`, `cumple_minimo: false` y el
     `aviso` «La polilínea no permite cumplir la norma en el PI 2 (mínimo 215 m; cabe hasta 2 m dejando la tangente
     mínima): mueve los vértices o baja la velocidad. Se pide 80 m, el mayor que cabe sin dejar la tangente mínima con
     la curva anterior (lo que admite el plugin), redondeado hacia abajo a 5 m; la verificación lo marcará «No
     cumple».» (también en `propuesta_norma.avisos`); `nota_3v` con «con R ≥ 115 m ... ese radio no cabe en la
     polilínea».
   - **La respuesta del plugin con `despues.numero_curvas: 2`**: el PI 2 con `radio_aplicado: 80`, no `null` (no queda
     sin curva por sorpresa).
   - `verificacion` (prevista): «Radio mínimo» de `Curva PI 2` `No cumple` (80 frente a 125); «Radio para prescindir de
     la curva de transición» de `Curva PI 2` `No cumple` (80 frente a 210); `Tangente PI 1–PI 2`, «Tangente mínima
     entre curvas de sentido contrario (L mín.s)», `No cumple` (5 frente a 83); «Longitud mínima de curva (3 V)» de
     `Curva PI 2` `Aviso` (125.664 frente a 180). Anota también las dos filas de «Relación entre radios consecutivos»
     (`PI 1 → PI 2` y `PI 2 → PI 1`, T302.08) con su `valor_exigido`.
   Pega `propuesta_norma.pis` y `verificacion.tabla`. Si el PI 2 sale sin curva o sin el aviso, **para aquí**.

7. **Radios del usuario: mandan y se avisa (decisivo).** `crear_alineamiento_desde_polilinea` con
   `args={"nombre":"Eje Usuario 119","capa":"EJE_NORMA_A","simular":true,"radios_por_pi":"[{\"pi\": 2, \"radio\": 150}]","pedido":"simula el eje A con 150 en el PI 2", DATOS}`.
   Esperado: `propuesta_norma.radios_por_pi: [215, 150]`; PI 1 con `origen: "norma"` y `radio_pedido: 215`; PI 2 con
   `origen: "usuario"`, `radio_pedido: 150`, `cumple_minimo: false` y el `aviso` «PI 2: tu radio 150 m queda por
   debajo del mínimo de la norma (215 m, T302.11B (302.05.07, p. 146; PDF 147)): la verificación lo marcará «No
   cumple».». En `verificacion`: «Radio mínimo» de `Curva PI 2` `Cumple` (150 frente a 125) y «Radio para prescindir
   de la curva de transición» `No cumple` (150 frente a 210); `resumen` con 1 «No cumple».

8. **Errores claros.**
   a. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje Norma A","capa":"EJE_NORMA_A","simular":true, DATOS}` (el eje ya existe) → `ok: false`, un
      `error` que contiene «Ya existe el alineamiento 'Eje Norma A'. Usa listar_alineamientos.», con `id_log`, y sin
      `propuesta_norma` (para en la simulación de ensayo).
   b. Lo mismo con `"capa":"NO_EXISTE"` y `"nombre":"Eje 8b"` → `ok: false` con un `error` del plugin que contiene
      «No existe la capa 'NO_EXISTE' en el dibujo».

9. **Deshacer y copias.** Abre el menú Deshacer de Civil 3D: las entradas nuevas son las del paso 1 (capas y
   polilíneas) y **una** del paso 5 (el eje `Eje Norma A`); **ninguna** de los pasos 2, 3, 4, 6, 7 u 8 (simulaciones,
   lecturas y errores). En `backups\`, solo la copia del paso 5 (o ninguna nueva si se reutilizó la del último
   guardado). No deshagas nada.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_119_<AAAAMMDD>.md` con: versión de Civil 3D, DWG usado (nombre,
sin rutas de obra), commit de `memoria-firma` (`git -C C:\Proyectos\memoria-firma log -1 --format=%h`), el valor de
`INSUNITS` del paso 0, una tabla paso / resultado (OK, FALLO) / respuesta literal / tiempo, `propuesta_norma.pis` y
`verificacion.tabla` de los pasos 4, 5, 6 y 7, los recuentos de `mcp_log.jsonl` y de `backups\` de los pasos 2 a 5 y,
al final, la lista de los radios propuestos, «No cumple», «Aviso» y «Sin valor en la norma» que te parezcan dudosos
frente a la norma, con su referencia, para que el dueño los revise.
