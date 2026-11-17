# Prueba de la ronda D en Civil 3D (1.19.2 + 1.19.3 + 1.20.0 + 1.21.0 + 1.21.1): índice y orden

Este archivo junta en **una sola sesión de pruebas** las cinco validaciones de la ronda D y las tres comprobaciones
pendientes de `COMPROBACIONES_RONDA_D.md`. Lo usa el agente instalador con el **Prompt P**, después de instalar con el
**Prompt L.1**. Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D.

## Requisitos (una sola instalación para todo)

- **Rama**: `tarea/ronda-d`. Junta los PR #50 a #55: los documentos pendientes (#50), la corrección de `ejecutar_codigo`
  (1.19.2, #54), la de `crear_secciones` (1.19.3, #53), el peralte (1.20.0, #51, dentro del #52), los bordes de calzada
  con sobreancho (1.21.0, #52) y las decisiones del dueño sobre la norma (1.21.1, #55). Se instala **una vez**: con
  Civil 3D cerrado, en PowerShell desde la raíz del repo, `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`;
  después, en `PuenteMcp`, `uv sync`. Reinicia el puente (`startup_c3d_mcp.vbs`) y **recarga el MCP `civil3d` en
  Antigravity** (su caché no ve las herramientas nuevas).
- **Versión instalada**: `1.21.1`. Herramientas: **63**, **59** del plugin (las 55 de la 1.19.1 más `listar_peralte`,
  `aplicar_peralte`, `crear_bordes_calzada` y `listar_bordes_calzada`) y **4** del puente (`buscar_api`,
  `guardar_receta`, `ejecutar_receta` y `verificar_norma`).
  - En el **paso 0 de cada parte** se espera **`1.21.1`** y **63** herramientas, no el número de cada archivo (la 1192
    dice 1.19.2 y 59; la 1193, 1.19.3 y 55 del plugin; la 120, 1.20.0 y 61; la 121, 1.21.0 y 63; la 1211, 59). El
    `curl.exe -s http://127.0.0.1:8765/ping` de la 1192 debe dar `{"ok":true,"servidor":"ArbaMcp","version":"1.21.1"}`.
    Todo lo demás de esos pasos 0 vale igual.
  - **No reinstales entre partes**, aunque el paso 0 de cada archivo nombre su propia rama o diga «si no, instala».
- **`memoria-firma` al día**: en `C:\Proyectos\memoria-firma`, `git pull`, con `MEMORIA_FIRMA_DIR` apuntando ahí
  (variable de usuario). Hace falta, **como mínimo, el commit `6e8eb54`**, que trae la tabla `peralte_figura` (las
  Figuras 302.02 a 302.05 digitalizadas) y, antes, las tablas de peralte y sobreancho de la base 1.2. Compruébalo en
  PowerShell: `git -C C:\Proyectos\memoria-firma merge-base --is-ancestor 6e8eb54 HEAD; $LASTEXITCODE` → `0`. Si da
  `1`, **para aquí** y avisa. Anota el commit: `git -C C:\Proyectos\memoria-firma log -1 --format=%h`. Se usan
  `normas\tablas\dg2018.sqlite` (partes 1, 2 y 3) y `recetas\crear_capa_con_color.cs` (parte 5, paso 9).
- **`ARBA_MCP_CODIGO=1`**, **solo** para la parte 5 (`VALIDACION_1192.md`) y la parte 6 (comprobaciones C2 y C3), que
  usan `ejecutar_codigo`. Para no reiniciar Civil 3D a mitad de la prueba, se pone **una vez, al principio, antes de
  abrir Civil 3D**: en PowerShell, `[Environment]::GetEnvironmentVariable('ARBA_MCP_CODIGO','User')` y anota lo que sale
  (es **V0**; puede salir vacío). Si no es `1`, `[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO','1','User')` y
  vuelve a leerla (`1`). Las partes 1 a 4 no usan `ejecutar_codigo`: la variable no les afecta. Se deja como estaba
  (V0) **al final de la parte 6**, no en el paso 10 de la 1192.
- **Archivo de puntos**: `C:\Proyectos\dibujos_prueba\terreno_corredor.csv`, el de los requisitos de
  `VALIDACION_111.md` (partes 1 y 2). Si no existe, créalo con las 10 líneas que da ese archivo.
- **Un dibujo por parte**, con el nombre que dice la tabla «Orden». Nunca el archivo base ni un dibujo real. Una copia
  reutilizada arrastra objetos de otra prueba. La parte 4 parte de `C:\Proyectos\dibujos_prueba\PRUEBA_112.dwg` (el que
  dejó la validación 1.12.0 el 14/11/2026): si no existe, **salta la parte 4** y anótalo como «sin dibujo», sin
  recrearlo.
- **Civil 3D se abre una sola vez**, después de poner la variable, desde el **menú Inicio o el acceso directo** (así lee
  `ARBA_MCP_CODIGO`), y se pulsa **Conexión IA**. Para pasar de una parte a la siguiente, cierra el dibujo de la parte
  anterior (sin guardar, salvo que su archivo diga otra cosa) y abre el de la siguiente con **Abrir**, sin cerrar Civil
  3D; después, `ping` debe dar en `dibujo` el de la parte nueva. Si Civil 3D se cierra solo, anótalo con la hora, vuelve
  a abrirlo desde el menú Inicio, pulsa Conexión IA y sigue con la parte siguiente (la que se cortó se repite al final,
  sobre una **copia nueva**).
- Reparto de papeles: el agente **instala y verifica**. No edita el repositorio, salvo los informes en
  `herramientas-dev\informes\`, y no hace commits. Usa **solo** el MCP `civil3d`: nada de curl, scripts ni el puerto
  8765, salvo los pasos que piden PowerShell.

## Lo primero de la ronda: el paso 2 de la parte 1

**El paso 2 de `VALIDACION_120.md` es el más importante de la ronda.** `listar_peralte` devuelve en `enumerados` los
**nombres reales** que usa Civil 3D 2027 para los tipos de progresiva crítica, las regiones y los segmentos del peralte.
El catálogo de la API no los trae, y sin ellos no se puede completar la tabla `CORRESPONDENCIA` de
`PuenteMcp/norma_peralte.py` (qué nombre de Civil 3D corresponde a cada concepto de la DG-2018). **Mientras falte,
`aplicar_peralte` con la norma no escribe**: calcula el peralte, lo devuelve en `propuesta_norma` y responde «Falta la
correspondencia entre la DG-2018 y los nombres de Civil 3D para ...» (paso 10d). Con `enumerados` y la tabla
«concepto → nombre elegido → por qué» del paso 3, la sesión de desarrollo rellena la correspondencia en esta misma rama.

Por eso la parte 1 va la primera y, **en cuanto termine el paso 2**, guarda ya el informe de la parte 1 como archivo con
`enumerados` completo (lo completas al final de la parte). Si la prueba se corta después, ese dato ya está guardado.

## Orden

| Parte | Archivo | Dibujo | Qué prueba | Pasos decisivos |
|---|---|---|---|---|
| 1 | `herramientas-dev/VALIDACION_120.md` | `PRUEBA_120.dwg` (dibujo **Nuevo** con la plantilla métrica, como dice el archivo) | Peralte: `listar_peralte`, `aplicar_peralte` con valores explícitos y el peralte de la DG-2018 calculado en el puente, sin escribir | 0, **2** (el de la `CORRESPONDENCIA`), 4, 5, 7, 9 y 10 |
| 2 | `herramientas-dev/VALIDACION_121.md` | `PRUEBA_121.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`) | Bordes de calzada con ensanchamientos y el sobreancho de la DG-2018 | 0, 3, 4, 8, 9 y 10 |
| 3 | `herramientas-dev/VALIDACION_1211.md` | `PRUEBA_1211.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`) | Las decisiones del dueño en `verificar_norma` y en la propuesta de radios (sin radio bajo el mínimo sin confirmación) | 0, 2, 3, 4, 5, 6, 7 y 8 |
| 4 | `herramientas-dev/VALIDACION_1193.md` | `PRUEBA_1193.dwg` (copia nueva de `PRUEBA_112.dwg`) | `crear_secciones`: nombre de grupo que ya usa otro eje y superficie del dibujo de cada sección | 0, 3, 6, 7 y 10 |
| 5 | `herramientas-dev/VALIDACION_1192.md` | `PRUEBA_1192.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`, sin líneas) | `ejecutar_codigo` sin `solo_lectura`: un cambio sobre un objeto abierto para lectura no compila | 0, 3, 4b, 5, 7, 8 y 9 |
| 6 | `herramientas-dev/COMPROBACIONES_RONDA_D.md` (C1, C2 y C3) | El mismo `PRUEBA_1192.dwg` de la parte 5, sin cerrarlo | `-LAYER` con dos espacios, propiedades heredadas de `Drawable` e `IsNewObject` | Ninguno: no deciden la ronda |

Por qué este orden:

- **Parte 1 primero**, por el paso 2 (ver arriba).
- **Partes 2 y 3** usan, como la 1, la base de la norma de `memoria-firma`; cada una con su copia nueva de la base.
- **Parte 4** necesita su dibujo de partida (`PRUEBA_112.dwg`); no depende de las otras.
- **Partes 5 y 6 al final**: son las únicas que usan `ejecutar_codigo`. La 1192 está pensada para no cerrar Civil 3D
  (los rechazos se prueban con el dibujo sin líneas), pero si algo fallara sería la única que podría cerrarlo, y al final
  no se pierde nada más. La parte 6 reutiliza el dibujo y la variable de la parte 5, sin abrir otro dibujo ni reiniciar.
- **Reinicios**: uno al principio (Civil 3D se abre después de instalar y de poner la variable) y uno al final (Civil 3D
  se cierra para dejar la variable como estaba).

## Ajustes de cada parte por ir juntas

- **Parte 1 (`VALIDACION_120.md`)**:
  - Paso 0: `1.21.1` y 63 herramientas; además de `listar_peralte` y `aplicar_peralte` están `crear_bordes_calzada` y
    `listar_bordes_calzada`.
  - Paso 2: es el decisivo para la `CORRESPONDENCIA`. Pega `enumerados` **completo** (`tipo_estacion`, `region`,
    `region_transicion`, `segmento` y `tipo_peralte_eje`), sin resumir ni reordenar, y guarda el informe enseguida.
  - Paso 10d: con `memoria-firma` en `6e8eb54` o después, lo esperado es la **segunda** respuesta del archivo: `ok:
    false` con «Falta la correspondencia entre la DG-2018 y los nombres de Civil 3D para ...» y `propuesta_norma`. La
    primera («La base de la norma no tiene la tabla peralte_figura ...» o la de las tablas de la sesión 6.4) significa
    que `memoria-firma` no está al día: anótalo como FALLO de requisitos.
  - Paso 10f: el número de comprobaciones de `casos_norma_reales.py` puede no ser 109 con la 1.21.1; lo que decide es
    «**0 fallos**» y que los casos 10 a 12 y la sección «Peralte según el radio» salgan con `[OK   ]`.
  - Paso 11 (solo referencia): necesita el archivo XML de criterios del dueño. Si no está a mano, se salta y se anota.
- **Parte 2 (`VALIDACION_121.md`)**: paso 0 con `1.21.1` (el archivo dice `1.21.0`); el resto, igual.
- **Parte 3 (`VALIDACION_1211.md`)**: paso 0 con **63** herramientas (el archivo dice 59 «o el número de la rama
  conjunta»). Si ya corriste `casos_norma_reales.py` en la parte 1 con el mismo commit de `memoria-firma`, córrelo otra
  vez de todos modos: este paso pide sus líneas «Umbrales del código» y «Referencias que cita el código».
- **Parte 4 (`VALIDACION_1193.md`)**: paso 0 con `1.21.1` y 59 herramientas del plugin (63 en total).
- **Parte 5 (`VALIDACION_1192.md`)**:
  - Paso 0: `1.21.1` y 63 herramientas, también en el `curl.exe` de `/ping`.
  - Paso 1: la variable ya está en `1` desde los requisitos. Solo anota el valor; no cierres Civil 3D.
  - Paso 10: haz el recuento y `leer_historial`, **no guardes el dibujo** y **no** toques la variable ni cierres Civil
    3D: pasa directamente a la parte 6 con el mismo dibujo.
- **Parte 6 (`COMPROBACIONES_RONDA_D.md`)**, sobre `PRUEBA_1192.dwg` todavía abierto:
  - C1, C2 y C3 en ese orden, con los pasos listos para pegar del archivo. C2a (InspectC3D) es opcional y su salida
    **no** se pega en el informe ni se guarda en el repositorio (regla 7 de `CLAUDE.md`).
  - En C3, el conteo N0 saldrá **1** (la línea que dejó la parte 5): es normal; lo que se compara es que el paso 3 dé
    el mismo N0.
  - **Cierre de la ronda**: cierra Civil 3D **sin guardar** y deja `ARBA_MCP_CODIGO` como estaba (V0): si V0 estaba
    vacía, `[Environment]::SetEnvironmentVariable('ARBA_MCP_CODIGO',$null,'User')`; si era `1`, déjala. Vuelve a leerla
    y anota cómo quedó.

## Informes (lo aprendido en la ronda C)

- **Cada parte guarda su informe como archivo** en `herramientas-dev\informes\`, con el nombre que pide su archivo:
  `informe_validacion_120_<AAAAMMDD>.md`, `informe_validacion_121_<AAAAMMDD>.md`,
  `informe_validacion_1211_<AAAAMMDD>.md`, `informe_validacion_1193_<AAAAMMDD>.md` y
  `informe_validacion_1192_<AAAAMMDD>.md`. No basta con el resumen en el chat.
- **Cada error se copia literal del `mcp_log.jsonl`** (carpeta del DWG de la parte): en cada FALLO, y en cada paso que
  espera un error, copia el campo `error` de la línea de esa llamada tal como está, con su hora y su `id_log`, sin
  resumirlo ni rehacerlo de memoria. En la ronda C, el primer informe corto traía cifras inventadas y un texto de error
  reconstruido; con las líneas literales del log todo cuadró.
- **Ninguna cifra sin su respuesta**: versiones, recuentos de herramientas, de pruebas, de líneas del log y de copias se
  copian de lo que salió, no de lo que se esperaba.
- Al final, un informe de la ronda, `herramientas-dev\informes\informe_validacion_ronda_d_<AAAAMMDD>.md`, con:
  - rama y commit instalados, `ping` (versión y número de herramientas), commit de `memoria-firma`, V0 y el valor final
    de `ARBA_MCP_CODIGO`;
  - una tabla parte / paso decisivo / OK o FALLO / informe donde está el detalle;
  - `enumerados` completo del paso 2 de la parte 1 y la tabla del paso 3 (concepto → nombre elegido → por qué);
  - la sección **«Comprobaciones de la ronda D (C1, C2, C3)»**, con lo que pide «Qué va al informe» de
    `COMPROBACIONES_RONDA_D.md`;
  - las líneas literales de `mcp_log.jsonl` de cada FALLO (sin el token: si aparece, `<oculto>`).
- Para pegar en Claude Code web: un informe corto con OK o FALLO por parte y por paso decisivo, y los errores
  completos, copiados del log.
- Regla 7 de `CLAUDE.md`: sin rutas con nombres de obra, sin `.dwg`, sin copias de `backups\` y sin el archivo XML de
  criterios del dueño (de la parte 1, paso 11, solo los valores leídos).
