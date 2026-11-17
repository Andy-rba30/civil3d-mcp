# Prompt de validación de la 1.18.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Dibujo**: una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica, en
  metros) guardada como `C:\Proyectos\dibujos_prueba\PRUEBA_118.dwg` y abierta en Civil 3D 2027. Nunca el archivo base
  ni un dibujo real. Anota la carpeta del DWG: ahí aparece `mcp_log.jsonl`.
- **Plugin y puente 1.18.0** desde la rama `tarea/4.2-verificar-norma`. El plugin no trae herramientas nuevas (solo
  sube de versión): hay **59** herramientas, **55** del plugin más **4** del puente: `buscar_api`, `guardar_receta`,
  `ejecutar_receta` y `verificar_norma`.
- `memoria-firma` clonado y al día (`git pull`) en `C:\Proyectos\memoria-firma`, con `MEMORIA_FIRMA_DIR` apuntando ahí
  (variable de usuario, puesta desde la 1.5.0) y con la base de la norma en
  `C:\Proyectos\memoria-firma\normas\tablas\dg2018.sqlite` y los casos en
  `C:\Proyectos\memoria-firma\normas\casos_prueba\casos_dg2018.yaml` (tarea 4.1).
- `ARBA_MCP_CODIGO` no hace falta: esta validación no usa `ejecutar_codigo`.
- Reparto de papeles: el agente local **instala y verifica**; el código y este prompt los mantiene la sesión de
  desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.18.0**: `verificar_norma`, una herramienta del **puente** (Python; argumentos directos, no dentro
  de `args`; responde JSON). Verifica un eje (lo lee con `listar_pis`) o una rasante (con `listar_pvis`) contra la
  norma DG-2018 con la base de `memoria-firma`, y devuelve una fila por comprobación (elemento, progresiva, valor de
  diseño, valor exigido, resultado y referencia de la norma con tabla, sección y páginas), un `resumen`, lo que no se
  pudo comprobar (`no_comprobado`) y la tabla en markdown (`tabla`). Si falta un dato del proyecto, **no lee el
  dibujo** y responde `faltan` con los valores válidos. Manda la tabla de la norma: si la fórmula pide más, el
  resultado es «Cumple con aviso». **Los pasos 0, 3, 5, 6 y 8 son los decisivos.**
- Miembros de la API que esta validación decide: **ninguno**. `verificar_norma` es Python y solo usa `listar_pis` y
  `listar_pvis`, ya validadas; `herramientas-dev/miembros_por_verificar_civil3d.md` no cambia en 1.18.0.

---

Eres el agente de validación de la versión 1.18.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (las del plugin con los argumentos
dentro de `args`; `verificar_norma` con los argumentos directos), salvo en los pasos que indican PowerShell. Ejecuta
los pasos en orden, anota la respuesta **literal** (JSON completo o, si es muy largo, las primeras 40 líneas; de
`verificar_norma`, siempre `resumen`, `tabla` y las filas que pide cada paso) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` solo para dibujar la polilínea del paso 2, con el texto exacto del paso. Si algo no funciona, es
  un hallazgo: no lo arregles con comandos.
- **Los datos del proyecto los da este prompt**: no inventes otros. Si `verificar_norma` responde `faltan` fuera del
  paso 5, anota la lista y para ese paso.
- No pegues en el informe rutas con nombres de obra (`<ruta>`).

## Pasos

0. **Versión e instalación (decisivo).** Si `ping` ya da `1.18.0`, no reinstales. Si no, con Civil 3D cerrado, en
   PowerShell desde la raíz del repo en la rama `tarea/4.2-verificar-norma`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`; en `PuenteMcp`, `uv sync`; reinicia el puente y
   el cliente MCP y abre Civil 3D con `PRUEBA_118.dwg`. Después:
   - `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.18.0"`, `"hay_dibujo": true`.
   - La lista de herramientas: **59**, con `verificar_norma`, que tiene `norma`, `tipo_elemento` y `nombre`
     obligatorios y, entre otros, `alineamiento`, `velocidad_diseno`, `clasificacion`, `orografia`, `ubicacion`,
     `peralte_maximo`, `pavimentada`, `altitud_msnm` y `cotas_msnm`.
   - PowerShell: `[Environment]::GetEnvironmentVariable('MEMORIA_FIRMA_DIR','User')` → `C:\Proyectos\memoria-firma` y
     `Test-Path C:\Proyectos\memoria-firma\normas\tablas\dg2018.sqlite` → `True`.
   Si la versión no es exactamente `1.18.0`, falta `verificar_norma` o la base no está, **para aquí**.

1. **Casos calculados a mano contra la base real (sin Civil 3D).** PowerShell, desde `PuenteMcp`:
   `uv run --frozen python casos_norma_reales.py; $LASTEXITCODE`. Esperado: una línea `[OK   ]` por cada valor de los
   casos 1, 4 y 5 (radio mínimo, pendiente máxima, curva vertical), los casos 2 y 3 como «fuera de alcance de
   verificar_norma», las secciones de fórmulas, umbrales y referencias sin `[FALLO]`, la última línea
   `Resultado: N comprobaciones OK, 0 fallos, ...` y `$LASTEXITCODE` = `0`. Pega la salida completa en el informe.
   Las líneas `[??   ]` (sin comparar) no son fallo, pero anótalas.

2. **Polilínea del eje de prueba.** `ejecutar_comando` con
   `args={"comando":"_.-LAYER _M EJE_NORMA  ","timeout_s":20,"pedido":"crea la capa EJE_NORMA y déjala actual"}` y
   después con
   `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","timeout_s":20,"pedido":"dibuja la polilínea del eje de prueba de la norma"}`
   (los dos con **dos espacios** al final, antes de la comilla). Cada uno → `"result": "terminado"`; luego
   `leer_variable` con `args={"nombre":"CMDACTIVE"}` → `"valor": "0"`. La polilínea va de este a norte y otra vez a
   este, con dos giros de 90° (el primero a la izquierda, el segundo a la derecha) y tramos de 300 m.

3. **Eje con una curva de radio menor al mínimo (decisivo).**
   a. `crear_alineamiento_desde_polilinea` con
      `args={"nombre":"Eje Norma 118","capa":"EJE_NORMA","radio":80,"radios_por_pi":"[80, 130]","simular":true}` y,
      si `avisos` está vacío, lo mismo sin `simular`, con `"pedido":"crea el eje de prueba de la norma con radios 80 y 130"`.
      Esperado: dos curvas, radios 80 y 130.
   b. `listar_pis` con `args={"alineamiento":"Eje Norma 118"}` → `unidades: "Meters"`, `numero_curvas: 2`; el PI 1 con
      `radio: 80`, `giro: "izquierda"`, `progresiva_pc: 220`, `progresiva_pt` ≈ 345.664; el PI 2 con `radio: 130`,
      `giro: "derecha"`, `progresiva_pc` ≈ 435.664; `fin.progresiva` ≈ 809.867.
   c. `verificar_norma` con
      `{"norma": "DG-2018", "tipo_elemento": "alineamiento", "nombre": "Eje Norma 118", "velocidad_diseno": 60,
      "clasificacion": "Carretera de tercera clase", "orografia": "Ondulado", "ubicacion": "rural",
      "pavimentada": false, "peralte_maximo": 8, "pedido": "verifica el eje de prueba con la DG-2018"}`.
      Esperado:
      - `ok: true`, `verificado: true`, `proyecto.clase: "Tercera clase"`, `proyecto.aplica_101_05: false`,
        `lecturas.listar_pis` con un `id_log`.
      - La fila con `elemento: "Curva PI 1"` y `comprobacion: "Radio mínimo"`: **`resultado: "No cumple"`**,
        `valor_diseno: 80`, `valor_exigido: 125`, `referencia.id: "T302.02"` (o `"T302.04"`), `referencia.pagina_pdf:
        "130"` (o `"133"`) y `referencia.pagina_impresa`; en `detalle`, `valor_formula` ≈ 123.245 (fórmula F08) y
        `otra_tabla` con la otra de las dos tablas (125).
      - `Curva PI 1`, «Longitud mínima de curva (3 V)»: `Aviso` (125.664 frente a 180). «Radio para prescindir de la
        curva de transición»: `No cumple` (80 frente a 210, `T302.11B`), con la nota «Requiere curva de transición...».
      - `Curva PI 2`, «Radio mínimo»: `Cumple` (130 frente a 125); «Longitud mínima de curva (3 V)»: `Cumple`.
      - `Tangente PI 1–PI 2` (90 m, curvas de sentido contrario): «Tangente mínima entre curvas de sentido contrario
        (L mín.s)»: `Cumple`, con `referencia.id: "T302.01"`.
      - `PI 1 → PI 2`, «Relación entre radios consecutivos»: `No cumple` (130 fuera de 53 a 120, `T302.08`). Anota
        también el resultado de `PI 2 → PI 1`.
      - `resumen` con al menos 3 `No cumple`, y la suma de `resumen` igual al número de `filas`.
      - `no_comprobado` con el peralte de diseño, la visibilidad disponible y el sobreancho.
      Pega `tabla` completa en el informe. Si el radio de `Curva PI 1` no sale «No cumple», **para aquí**.
   d. En la carpeta del DWG, `mcp_log.jsonl`: la última línea es de `listar_pis` con
      `"pedido":"verifica el eje de prueba con la DG-2018"`, y su `id` es el de `lecturas.listar_pis`.

4. **Tercera Clase pavimentada (artículo 101.05).** Repite el paso 3c con `"pavimentada": true`. Esperado:
   `proyecto.aplica_101_05: true`, `proyecto.clase: "Segunda clase"`, un aviso en `avisos` que cita `R101.05-1`; la
   fila de radio mínimo de `Curva PI 1` sin `otra_tabla` (solo la T302.02) y la de curva de transición con
   `referencia.id: "T302.11A"`. Anota los resultados de esas dos filas.

5. **Rasante con un K corto (decisivo).**
   a. `crear_rasante` con
      `args={"alineamiento":"Eje Norma 118","nombre":"Rasante Norma 118","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":300,\"cota\":112,\"longitud_curva\":50},{\"progresiva\":600,\"cota\":103,\"longitud_curva\":120},{\"progresiva\":800,\"cota\":107}]","simular":true}`
      y, si las dos curvas caben (`avisos` vacío), lo mismo sin `simular`, con
      `"pedido":"crea la rasante de prueba de la norma"`.
   b. `listar_pvis` con `args={"alineamiento":"Eje Norma 118","perfil":"Rasante Norma 118"}` → el PVI 2 con
      `pe_pct: 4`, `ps_pct: -3`, `a_pct: 7`, `tiene_curva: true`, `longitud_curva: 50`; el PVI 3 con `pe_pct: -3`,
      `ps_pct: 2`, `longitud_curva: 120`.
   c. `verificar_norma` con
      `{"norma": "DG-2018", "tipo_elemento": "rasante", "nombre": "Rasante Norma 118", "alineamiento": "Eje Norma 118",
      "velocidad_diseno": 60, "clasificacion": "Carretera de tercera clase", "orografia": "Ondulado",
      "pavimentada": false, "pedido": "verifica la rasante de prueba con la DG-2018"}`. Esperado:
      - `ok: true`, `verificado: true`, `lecturas` con `listar_pis` y `listar_pvis`.
      - La fila `PVI 2`, «Longitud de curva vertical convexa (visibilidad de parada)»: **`resultado: "No cumple"`**,
        `valor_diseno: 50`, `valor_exigido: 77`, `referencia.id: "T303.02"`, `referencia.pagina_pdf: "181"`; en
        `detalle`, `k_tabla: 11`, `k_diseno` ≈ 7.143, `a_pct: 7` y `valor_formula` ≈ 125.11 con `formula.id: "F26"`.
      - `PVI 3`, «Longitud de curva vertical cóncava»: `Cumple` (120 frente a 90 = 18 × 5, `T303.03`).
      - `PVI 2` y `PVI 3`, «Curva vertical obligatoria si A > 2 %»: `Cumple` (sin pavimentar el umbral es 2 %).
      - Las tres filas «Pendiente máxima» (4 %, 3 % y 2 %) con `referencia.id: "T303.01"`: anota su `valor_exigido` y
        su resultado (`Cumple` si la tabla da 4 % o más).
      - `no_comprobado` con la visibilidad de paso.
      Pega `tabla` completa. Si la curva del PVI 2 no sale «No cumple», **para aquí**.

6. **Sin un dato obligatorio, pregunta sin leer el dibujo (decisivo).** PowerShell: cuenta las líneas de
   `mcp_log.jsonl` (`(Get-Content <carpeta del DWG>\mcp_log.jsonl).Count`). Luego:
   a. `verificar_norma` con
      `{"norma": "DG-2018", "tipo_elemento": "alineamiento", "nombre": "Eje Norma 118", "velocidad_diseno": 60,
      "clasificacion": "Carretera de tercera clase", "orografia": "Ondulado", "ubicacion": "rural"}`. Esperado:
      `ok: false`, `verificado: false`, `error` que empieza por «Faltan datos del proyecto: pavimentada, peralte_maximo»
      y termina con «No se leyó el dibujo.», y `faltan` con `pavimentada` (`valores_validos: [true, false]`) y
      `peralte_maximo` (`valores_validos` con los peraltes de la T302.04 hasta 8 %, el máximo de la T304.05 en zona rural
      plana u ondulada: `[4, 6, 8]` si la base tiene esos; desde la corrección de la auditoría de la ronda C ya no salen
      10 ni 12).
   b. Lo mismo con todos los datos del paso 3c pero `"velocidad_diseno": 65`. Esperado: `faltan` solo con
      `velocidad_diseno`, el motivo dice que la norma no permite interpolar y `valores_validos` es la lista de la
      T204.01 para Tercera Clase en terreno ondulado (con 60, sin 65).
   c. Vuelve a contar las líneas de `mcp_log.jsonl`: **igual** que antes (a y b no llamaron al plugin).

7. **Errores claros.**
   a. `verificar_norma` con `"norma": "AASHTO"` (y los datos del paso 3c) → `ok: false`, «Norma 'AASHTO' no
      soportada. Normas disponibles: DG-2018.»
   b. `tipo_elemento: "rasante"` sin `alineamiento` → `ok: false`, «Para una rasante falta 'alineamiento'...».
   c. `nombre: "NO_EXISTE"` con los datos del paso 3c → `ok: false`, «Error de Civil 3D: No existe el alineamiento
      'NO_EXISTE'. Usa listar_alineamientos.», con `id_log`.

8. **MEMORIA_FIRMA_DIR sin la base (decisivo).** PowerShell: `New-Item -ItemType Directory -Force
   C:\Proyectos\memoria-vacia`. Cierra la ventana del puente y arráncalo en una PowerShell **nueva** con la variable
   solo para esa ventana (no cambies la variable de usuario):
   `cd <repo>\PuenteMcp; $env:MEMORIA_FIRMA_DIR = 'C:\Proyectos\memoria-vacia'; uv run --frozen python main.py`.
   Reconecta el MCP en el agente y repite el paso 3c. Esperado: `ok: false` y un `error` que empieza por «Falta la base
   de la norma C:\Proyectos\memoria-vacia\normas\tablas\dg2018.sqlite. Actualiza memoria-firma (git pull)» y dice «No
   se verificó nada.»; `mcp_log.jsonl` sin línea nueva. Además, en otra PowerShell desde `PuenteMcp`:
   `uv run --frozen python casos_norma_reales.py --memoria C:\Proyectos\memoria-vacia; $LASTEXITCODE` → una línea
   `Error: Falta la base de la norma ...` y `2`. Después cierra esa ventana del puente, arráncalo como siempre (con la
   variable de usuario) y reconecta el MCP; `verificar_norma` del paso 3c vuelve a responder `verificado: true`.

9. **Sin entrada en Deshacer.** Abre el menú Deshacer de Civil 3D: las entradas son las de los pasos 2, 3a y 5a
   (polilínea, eje y rasante); ninguna de los pasos 3c, 4, 5c, 6, 7 u 8 (`verificar_norma` solo lee). No deshagas
   nada.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_118_<AAAAMMDD>.md` con: versión de Civil 3D, DWG usado (nombre,
sin rutas de obra), commit de `memoria-firma` (`git -C C:\Proyectos\memoria-firma log -1 --format=%h`), una tabla
paso / resultado (OK, FALLO) / respuesta literal / tiempo, la salida completa del paso 1, la `tabla` de los pasos 3c,
4 y 5c y, al final, la lista de los «No cumple», «Aviso» y «Sin valor en la norma» que te parezcan dudosos frente a
la norma, con su referencia, para que el dueño los revise.
