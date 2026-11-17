# Prueba de la ronda C en Civil 3D (1.17.6 + 1.18.0 + 1.19.0, con la corrección 1.19.1): índice y orden

Este archivo junta en **una sola sesión de pruebas** las tres validaciones de la ronda C y añade una cuarta parte, la
repetición **sin zoom** de los `_.UNDO 1` que quedaron en duda. Lo usa el agente instalador con el **Prompt P**,
después de instalar con el **Prompt L.1**. Copia este texto tal cual en el agente que tiene conectado el MCP de
Civil 3D.

## Requisitos (una sola instalación para todo)

- **Rama**: `tarea/ronda-c`. Lleva las tres versiones juntas: la corrección 1.17.6, `verificar_norma`
  (1.18.0), la norma dentro del eje (1.19.0) y, desde el 17/11/2026, la corrección **1.19.1** (ver «Estado tras la
  primera prueba», abajo). Se instala **una vez**. Con Civil 3D cerrado, en PowerShell desde la
  raíz del repo: `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. Después, en `PuenteMcp`, `uv sync`.
  Reinicia el puente y el cliente MCP.
- **Versión instalada**: `1.19.1`. Herramientas: **59** (55 del plugin y 4 del puente: `buscar_api`,
  `guardar_receta`, `ejecutar_receta` y `verificar_norma`).
  - En el paso 0 de `VALIDACION_1176.md` y de `VALIDACION_118.md` se espera **`1.19.1`**, no el número de cada archivo,
    tanto en `ping` como en el `/ping` de PowerShell, y **59** herramientas (`VALIDACION_1176.md` dice 58 porque se
    escribió antes de `verificar_norma`). Todo lo demás de esos pasos 0 vale igual.
  - No reinstales entre partes, aunque el paso 0 de cada archivo nombre su propia rama: basta con abrir el dibujo de
    cada una.
- **`memoria-firma` al día**: en `C:\Proyectos\memoria-firma`, `git pull`, con `MEMORIA_FIRMA_DIR` apuntando ahí.
  Hacen falta `normas\tablas\dg2018.sqlite` y `normas\casos_prueba\casos_dg2018.yaml` (commit `518fa70` o posterior).
- **`ARBA_MCP_CODIGO=1`** (variable de usuario) **solo** para la parte 1, que prueba `ejecutar_codigo` y
  `ejecutar_receta`. Si no está puesta, ponla antes de abrir Civil 3D. Al terminar la parte 1 no hace falta quitarla;
  el paso final de `VALIDACION_1176.md` dice qué hacer.
- **Un dibujo nuevo por parte**. Cada uno es una **copia nueva** de
  `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica + ensamblaje, sin nada más), con el nombre
  que dice cada parte. Nunca el archivo base ni un dibujo real. Una copia reutilizada arrastra objetos de otra prueba.
- Reparto de papeles: el agente **instala y verifica**. No edita el repositorio, salvo los informes en
  `herramientas-dev\informes\`, y no hace commits. Usa **solo** el MCP `civil3d`: nada de curl, scripts ni el puerto
  8765, salvo los pasos que piden PowerShell.

## Estado tras la primera prueba (17/11/2026, con la 1.19.0)

- **Parte 1**: pasos 0 a 8 OK; en el **paso 9** (`Curve.Extend` con `solo_lectura=true`) **Civil 3D se cerró**
  (informe `informe_validacion_1176_20261117.md`). La 1.19.1 lo corrige: con `solo_lectura=true` ese script ya no compila.
  **Repite la parte 1 entera** desde el paso 0, sobre una **copia nueva** (`PRUEBA_1176.dwg` quedó a medias por la
  caída; no lo recuperes). Guarda el informe nuevo con otro nombre, `informe_validacion_1176_<AAAAMMDD>_b.md`, sin
  borrar el primero.
- **Partes 2, 3 y 4**: si ya se hicieron con la 1.19.0, **no se repiten** (la 1.19.1 solo cambia `ejecutar_codigo` con
  `solo_lectura`); basta con el `ping` del paso 0 de la parte 1 (`1.19.1`). Si no se hicieron, van después de la parte 1,
  como dice el orden.

## Orden

| Parte | Archivo | Dibujo | Qué prueba | Pasos decisivos |
|---|---|---|---|---|
| 1 | `herramientas-dev/VALIDACION_1176.md` | `PRUEBA_1176.dwg` | Relectura del estilo en las herramientas que crean objetos; `solo_lectura` en `ejecutar_codigo` y `ejecutar_receta`; métodos de `Database` bloqueados | Los que marca el archivo |
| 2 | `herramientas-dev/VALIDACION_118.md` | `PRUEBA_118.dwg` | `verificar_norma`: eje y rasante contra la DG-2018, datos que faltan, base ausente | Los que marca el archivo |
| 3 | `herramientas-dev/VALIDACION_119.md` | `PRUEBA_119.dwg` | `crear_alineamiento_desde_polilinea` con `velocidad_diseno`: radio de la norma por PI y verificación adjunta | Los que marca el archivo |
| 4 | Este archivo, sección «Parte 4» | `PRUEBA_DESHACER.dwg` | Qué quita de verdad un `_.UNDO 1`, sin zoom de por medio | Todos sus pasos son de observación |

Cada parte guarda su informe en `herramientas-dev\informes\` con el nombre que pide su archivo. La parte 4 lo guarda en
`informe_validacion_deshacer_<AAAAMMDD>.md`. Al final, el informe corto para pegar en Claude Code web dice OK o FALLO
por parte y por paso decisivo, con los errores completos.

## Parte 4: qué quita `_.UNDO 1` sin zoom

**Por qué**: en validaciones anteriores, `_.UNDO 1` no quitó la superficie TIN (1.7.0), las superficies Top y Datum
(1.12.0), el perfil de terreno (1.14.1) ni la superficie de volumen (1.14.0). En varios de esos casos hubo un `_.ZOOM`
entre la creación y el `_.UNDO 1`. El zoom deja su propia entrada en Deshacer, y el `_.UNDO 1` pudo deshacer el zoom y
no el objeto, como pasó con `ejecutar_codigo` en la 1.16.0 (validado el 16/11/2026). El eje nunca se probó. Esta parte
lo repite todo sin zoom.

**Reglas de esta parte**:

- Entre cada creación y su `_.UNDO 1`: **ningún** zoom, encuadre ni regeneración, ni comandos, ni `capturar_pantalla`,
  ni `listar_lineas_muestreo` (deja su propia entrada en Deshacer). Solo las lecturas que pide el paso.
- **Antes** de cada `_.UNDO 1`, Jose abre la flecha del botón Deshacer de Civil 3D, **sin** tocar la vista, y dicta la
  entrada de arriba. Se espera `Executefunction` (o como la muestre Civil 3D en español). Anótala literal.
- El `_.UNDO 1` va siempre con `ejecutar_comando` y `args={"comando":"_.UNDO 1","timeout_s":20}`. Respuesta esperada:
  `"result": "terminado"`.
- Ningún resultado de esta parte es FALLO en sí: lo que se busca es **lo que pasa**. Anota en cada paso «se quitó» o
  «sigue», con la lectura literal de antes y de después.

**Pasos**:

- **D0. Dibujo.** Copia `PRUEBA_EVALUACION_BASE.dwg` como `C:\Proyectos\dibujos_prueba\PRUEBA_DESHACER.dwg` y ábrela.
  Llama a `listar_ensamblajes` con `args={"pedido":"lista los ensamblajes"}` y anota el nombre del ensamblaje que trae
  la base (en los pasos de abajo, `<ensamblaje>`). Comprueba que `C:\Proyectos\dibujos_prueba\terreno_corredor.csv`
  existe. Si no, créalo con las 10 líneas de los requisitos de `VALIDACION_FLUJO_COMPLETO.md`.
- **D1. Polilínea** (deja entradas en Deshacer, es normal).
  1. `ejecutar_comando` con `args={"comando":"_.-LAYER _M EJE_PRUEBA  ","timeout_s":20}`.
  2. `ejecutar_comando` con `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","timeout_s":20}`,
     con **dos espacios** al final.
  3. `guardar_dibujo` con `args={"pedido":"guarda el dibujo de prueba"}`.
- **D2. Superficie TIN.**
  1. `crear_superficie_tin` con
     `args={"nombre":"Terreno","archivo":"C:\\Proyectos\\dibujos_prueba\\terreno_corredor.csv","intervalo_curvas_menor":1,"intervalo_curvas_mayor":5,"pedido":"crea la superficie Terreno"}`.
  2. `listar_superficies` → debe estar `Terreno`.
  3. Jose dicta la entrada de arriba del menú Deshacer y se ejecuta `_.UNDO 1`.
  4. `listar_superficies` → ¿sigue `Terreno`? Anota «se quitó» o «sigue».
  5. Si se quitó, vuelve a crearla con los mismos `args`; la necesitan los pasos siguientes.
- **D3. Eje.**
  1. `crear_alineamiento_desde_polilinea` con
     `args={"nombre":"Eje 1","capa":"EJE_PRUEBA","radio":80,"pedido":"crea el Eje 1"}` → `despues.longitud` = `831.3274`.
  2. `listar_alineamientos` → `Eje 1`.
  3. Menú Deshacer y `_.UNDO 1`.
  4. `listar_alineamientos` → ¿sigue `Eje 1`? Anota.
  5. Si se quitó, vuelve a crearlo.
- **D4. Perfil de terreno y vista.**
  1. `crear_perfil_terreno_y_vista` con
     `args={"alineamiento":"Eje 1","superficie":"Terreno","pedido":"crea el perfil de terreno y su vista"}`.
  2. `listar_perfiles` con `args={"alineamiento":"Eje 1"}` → el perfil de terreno (tipo EG); anota su nombre.
  3. Menú Deshacer y `_.UNDO 1`.
  4. `listar_perfiles` → ¿sigue el perfil? Pregunta a Jose si la vista de perfil sigue en pantalla, **sin** que haga
     zoom: si no se ve sin zoom, «no se sabe». Anota las dos cosas.
  5. Si se quitó, vuelve a crearlo.
- **D5. Rasante y corredor** (ya se sabe que `_.UNDO 1` los quita; aquí solo se crean).
  1. `crear_rasante` con
     `args={"alineamiento":"Eje 1","nombre":"Rasante 1","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":400,\"cota\":110,\"longitud_curva\":120},{\"progresiva\":831.33,\"cota\":105}]","pedido":"crea la Rasante 1"}`.
  2. `crear_corredor` con
     `args={"nombre":"Corredor 1","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"<ensamblaje>","frecuencia":10,"superficie_objetivo":"Terreno","pedido":"crea el Corredor 1"}`.
- **D6. Superficies Top y Datum.**
  1. `crear_superficies_corredor` con `args={"corredor":"Corredor 1","pedido":"crea las superficies Top y Datum"}`.
  2. `listar_superficies` → `Corredor 1 - Top` y `Corredor 1 - Datum`.
  3. Menú Deshacer y `_.UNDO 1`.
  4. `listar_superficies` → ¿siguen las dos? Anota.
  5. Si se quitaron, vuelve a crearlas.
- **D7. Superficie de volumen.**
  1. `calcular_volumenes` con
     `args={"terreno":"Terreno","datum":"Corredor 1 - Datum","corredor":"Corredor 1","pedido":"calcula los volúmenes"}`.
  2. `listar_superficies` → la superficie de volumen; anota su nombre.
  3. Menú Deshacer y `_.UNDO 1`.
  4. `listar_superficies` → ¿sigue? Anota.

**Informe de la parte 4**: una tabla con una fila por objeto (TIN, eje, perfil de terreno y vista, Top y Datum,
volumen). Columnas: entrada de arriba del menú Deshacer antes del `_.UNDO 1`, respuesta del `_.UNDO 1`, «se quitó» o
«sigue», y la lectura literal de antes y de después. Añade cualquier mensaje que saliera en la línea de comandos de
Civil 3D.
