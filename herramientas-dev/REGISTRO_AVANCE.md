# Registro de avance (para retomar si se corta la sesión)

Este archivo lo mantiene la **sesión orquestadora** de Claude Code web: la que crea una sesión por tarea, aprueba sus
planes, revisa sus PR y prepara las pruebas en la PC. Si la sesión se corta, una sesión nueva lee este archivo y sigue
desde el último estado. El plan completo está en `HOJA_DE_RUTA_v1.3.md`, con su sección 0 "Avance", y en el PDF
original, `HOJA_DE_RUTA_v1.3.pdf`. Repositorio público: aquí no van rutas reales ni datos de obra.

## Cómo retomar (prompt para una sesión nueva de Claude Code web)

Abre una sesión nueva con los repositorios `civil3d-mcp` y `memoria-firma` y pega:

```
Eres la sesión orquestadora de civil3d-mcp. Lee CLAUDE.md, herramientas-dev/REGISTRO_AVANCE.md y la sección 0 de
herramientas-dev/HOJA_DE_RUTA_v1.3.md. Retoma desde "Estado actual": comprueba en GitHub el estado de las ramas y PR
abiertos que lista el registro, crea una sesión hija por tarea pendiente (con el prompt del PDF literal más notas
aparte), aprueba sus planes, revisa sus PR, júntalos en una sola rama para la prueba en mi PC y avísame solo cuando
haya que probar. Usa Opus con esfuerzo extra alto en las sesiones hijas. Actualiza este registro en cada paso.
```

## Decisiones del dueño (vigentes)

- La orquestadora **aprueba los planes** de las sesiones hijas y **fusiona los PR**: los de solo documentación,
  directamente; los de código, cuando la prueba en Civil 3D da OK.
- **Solo 3 rondas de prueba en la PC** para lo que queda (13/11/2026):
  - **Ronda A, Etapa 2 completa**: 1.12.0 (superficies Top/Datum), 1.13.0 (secciones), 1.14.0 (volúmenes),
    1.14.1 (arreglos), 2.8 (receta del flujo completo) y la **evaluación del plan** (5.2-D y 5.2-PC).
  - **Ronda B, Etapa 3**: 3.1 `ejecutar_codigo` y 3.2 recetas.
  - **Ronda C, Etapa 4**: 4.2 `verificar_norma` y 4.3 la norma dentro del eje. La 4.1 (tablas) no necesita la PC:
    solo la revisión del dueño. Hace falta antes el PDF de la DG-2018 en `memoria-firma/normas/fuentes/` y 5 casos
    calculados a mano.
- Sesiones hijas con Opus y esfuerzo extra alto. Prompts del PDF **literales**, más "Notas de la orquestadora" aparte.
- Avisar al dueño **solo** cuando haya que probar en su PC. Un aviso por ronda, con L.1 y P listos para pegar.

## Cómo se trabaja (lo aprendido)

- En las sesiones hijas, `add_repo` de `memoria-firma` lo deniega el clasificador de permisos: la orquestadora
  consulta el catálogo (`memoria-firma/catalogo/catalogo_c3d2027.sqlite`) y les pasa los miembros confirmados.
- Las ramas paralelas se juntan en cadena: cada rama nueva parte de la anterior o la fusiona al final (merge, nunca
  rebase ni force). La última rama de la cadena es la que instala el dueño, y los PR se fusionan en orden cuando la
  prueba da OK.
- Antes de cada prueba: recargar el MCP `civil3d` en Antigravity (su caché no ve las herramientas nuevas) y guardar el
  dibujo de la prueba anterior si se va a reutilizar.
- Cada informe de validación se sube a `herramientas-dev/informes/`. Una sesión de "hallazgos" (solo documentación)
  pasa lo aprendido a la tabla de miembros y a los "Hechos ya validados" de `CLAUDE.md`.
- La conexión de GitHub del dueño **puede caducar** a mitad de sesión (14/11/2026, 15:37: `push` con 403 «GitHub
  authentication required» en la orquestadora y en las hijas). Se le pide reconectar en claude.ai/connect-github; las
  hijas guardan sus commits en local y suben después.
- Las hijas se crean con `create_session` y `outcome_branch` (su rama `tarea/...`); presentan el plan y esperan el
  mensaje de la orquestadora. Para una corrección pequeña tras un informe, la orquestadora la manda con el plan ya
  aprobado en el mismo mensaje.
- En el Prompt P, pedir **expresamente** que el agente guarde cada informe como archivo en
  `herramientas-dev\informes\` (el 14/11 solo entregó resúmenes de la 1142 y la 115; se subieron como transcripción).
- La evaluación parte siempre de una **copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg`
  (plantilla métrica + ensamblaje, sin nada más; anotado en `ESTADO_PC.md`). Una copia reutilizada arrastra líneas de
  muestreo y falla la tarea 7.

## Estado actual (14/11/2026, ronda A cerrada)

**En `main`: versión 1.15.1, 54 herramientas, validada en Civil 3D 2027 el 14/11/2026.** La Etapa 2 está hecha: la
evaluación dio **10 de 10 OK** (hito: la tarea 10, flujo completo, en 176 s frente a 130 min) y las validaciones 1142
(salvo la tabla, corregida en la 1143), 1143 y 115 dieron OK. Fusionados el **#31** (`tarea/2.7-volumenes`, 1.14.3) y
el **#35** (`tarea/2.9-tabla-pi`, 1.15.1); GitHub cerró como fusionados el #26, #27, #29 y #30, que iban dentro. Los
ocho informes de la ronda A (112, 113, 114, 1141, flujo completo, 1142, 115 y 1143) están en
`herramientas-dev/informes/`; el de la evaluación, en `memoria-firma/evaluacion/informes/informe_evaluacion_20261114_1150.yaml`.
La hoja de ruta (sección 0) marca la Etapa 2 como hecha.

### En curso (14/11/2026, 17:46): hallazgos y ronda B lanzados
Sesiones hijas (Opus, esfuerzo extra alto), cada una presenta su plan y espera a la orquestadora:
- `session_011fq2pFR18Uv6gB9YEB9yQQ`: **hallazgos de la ronda A** (solo documentación) en
  `tarea/2.10-hallazgos-ronda-a`: estados de la tabla de miembros 1.12.0 a 1.15.0 y «Hechos ya validados» de
  `CLAUDE.md`. Se fusiona directamente (sin PC).
- `session_011fq2pG9v3qYhNzDMFmn2n2`: **3.1 `ejecutar_codigo`** (1.16.0) en `tarea/3.1-ejecutar-codigo`, con
  `VALIDACION_116.md`. Primero explica los riesgos.
- `session_011fq2pGwzj6ddGgrG2wGrk3`: **3.2 recetas** (`guardar_receta`, `ejecutar_receta`, `buscar_api` con recetas
  primero; 1.17.0) en `tarea/3.2-recetas`, con `VALIDACION_117.md` (después de la 116; quita `ARBA_MCP_CODIGO` al final).
- **Interfaz fijada entre las dos** (para trabajar en paralelo): `ejecutar_codigo(codigo_csharp, simular=true,
  parametros?, pedido)`; el script lee `Parametros` y su `return` llega en `resultado`; desactivada → error que empieza
  por «ejecutar_codigo está desactivada».
- Al terminar: la orquestadora fusiona `tarea/3.1-ejecutar-codigo` en `tarea/3.2-recetas` (versión 1.17.0), que será la
  rama de la **única instalación de la ronda B**: VALIDACION_116 → VALIDACION_117 → evaluación 5.2-D/PC (sobre una copia
  nueva de `PRUEBA_EVALUACION_BASE.dwg`).
- **18:00, el dueño sale unas horas y pide adelantar todo lo que no necesite su PC.** Se lanzan además dos mejoras
  pequeñas que entran en la misma instalación de la ronda B (sin tocar versión: la pone la orquestadora al integrar):
  - `session_011fq2pHySa8d4fvCQEix3Gm`: **tabla por progresivas solo donde existe la Datum** (`calcular_volumenes`,
    será la 1.17.1) en `tarea/2.11-tabla-volumen-datum`, con `VALIDACION_1171.md` sobre `PRUEBA_1142.dwg`.
  - `session_011fq2pFZ4ZspM3F9GeCq9e2`: **`crear_superficie_tin` elige un estilo con las curvas pedidas** (1 y 5 m; será
    la 1.17.2) en `tarea/2.12-curvas-estilo-tin`, con `VALIDACION_1172.md`.
  - Planes aprobados a las 17:58: hallazgos (sin cambiar títulos de las secciones: párrafo «Validada el 14/11/2026») y
    3.2 (herramientas del puente; `parametros` con la misma forma que los `json` del plugin). La 3.1 aún prepara el suyo.
- **Datos de la evaluación**: `add_repo` de memoria-firma con permiso de escritura lo deniega el clasificador. Se
  prepararon `puntos_terreno.csv`, `eje_polilinea.dxf`, `pvi_rasante.csv` y `LEEME.md` (sintéticos) para que el agente
  instalador los cree en `C:\Proyectos\memoria-firma\evaluacion\datos_prueba\` y los suba con L.2.
- Etapa 4 bloqueada hasta que el dueño suba el PDF de la DG-2018 a `memoria-firma/normas/fuentes/` (hoy solo `.gitkeep`)
  y prepare los 5 casos calculados a mano.
- **18:07 hallazgos terminados** (`tarea/2.10-hallazgos-ronda-a`, `5b88796`): revisados y fusionados en
  `claude/charming-brown-70s7g3` (sin PR: el conector de GitHub vuelve a pedir autorización). Pasan a `main` con el
  próximo PR de documentación.
- **18:12 plan de la 3.1 aprobado.** Riesgos explicados (salir de Civil 3D, saltarse la transacción, estropear el
  dibujo, simulación engañosa, colgar o cerrar Civil 3D, hilos, choque de versiones de Roslyn, memoria, atajo para
  saltarse las reglas) con sus mitigaciones; no se puede detener un script a la fuerza (sin `Thread.Abort` en .NET 10;
  el hilo es el de AutoCAD). Decisiones: copia también al simular y entrada vacía en Deshacer al simular (dos
  excepciones documentadas), fuera de la comprobación previa (asíncrona), Roslyn 5.0.0 en un contexto aislado, 60 s de
  tiempo límite. Catálogo: confirmados `GetAllObjects`, `IsNewObject`/`IsModified`/`IsErased`, `TopTransaction` y los
  argumentos de eventos; los eventos de `Database` no constan (el catálogo no registra eventos): solo de apoyo.
- **18:14 la 3.2 terminó**: PR #38, `tarea/3.2-recetas` en `08906ed`, versión 1.17.0 (puente 166 pruebas). `parametros`
  va a `ejecutar_codigo` como texto JSON (así pasa el puente todos los `json`); la 3.1 aceptará objeto y texto.
- **18:16 planes aprobados** de la tabla de volúmenes (recorte por `FindElevationAtXY` en las dos superficies,
  bisección 0,01 m, sin miembros nuevos) y de las curvas TIN (elige entre los estilos que dibujan curvas; catálogo:
  `SettingsSurface.Styles.SurfaceStyleId` y `SurfaceStyle.GetDisplayStylePlan(...)`.`Visible`; cambia una frase de
  `INSTRUCCIONES_AGENTE.md`).
- **18:25-18:54 terminaron** la tabla de volúmenes (PR #39, `fea71e5`, 412 pruebas), las curvas TIN (PR #40, `4816b83`)
  y la 3.1 `ejecutar_codigo` (PR #41, `57e5313`, 521 pruebas; Roslyn 5.0.0; bloquea además rutas asignadas a propiedades,
  propiedades de `Database`, `UserConfigurationManager` y `Dispose` de documento/editor).
- **Rama integrada `tarea/ronda-b`** (commit `977e80c`, versión **1.17.2**, 55 herramientas del plugin + 3 del puente):
  `tarea/3.2-recetas` + `2.11` + `2.12` + `3.1`. Cuatro pruebas OK: dotnet 546/546, puente 166, CompilarSinCivil 0
  errores, evaluador 21. `VALIDACION_116` y `117` ajustadas a `ping` 1.17.2. `INSTRUCCIONES_AGENTE.md` junta las reglas
  de recetas (paso 2) y de `ejecutar_codigo` (pasos 3 y 4).
- **19:00 PR #42** (hallazgos + registro) fusionado en `main`; `main` fusionado en `tarea/ronda-b` (`470f722`, limpio).
  **PR #43** en borrador: `tarea/ronda-b` → `main`, con «Para el agente instalador» y «Para Jose» de toda la ronda B.
  **Aviso al dueño enviado: ronda B lista para probar** (L.1, P y la evaluación, más los datos de la evaluación).
- **Integración prevista de la ronda B**: `tarea/3.1` → `tarea/3.2` → + `tarea/2.11` + `tarea/2.12` en una sola rama
  (versión **1.17.2**). Orden en la PC: VALIDACION_116 → 117 → 1171 → 1172 → evaluación (5.2-D/PC).

## Historial de la ronda A (14/11/2026)

**En `main`**: versión **1.11.0**, validada. **Ronda A probada en la PC el 14/11/2026** sobre la rama
**`tarea/2.7-volumenes`** (versión 1.14.1, 53 herramientas, commit `a502066`). Los PR #26, #27, #29, #30 y #31
siguen **en borrador y sin fusionar**: quedan dos fallos que se corrigen en esa misma rama.

### Primera prueba de la ronda A (resumen del agente instalador; los informes completos están en la PC, en
`C:\Proyectos\civil3d-mcp\herramientas-dev\informes\informe_validacion_{112,113,114,1141,flujo_completo}_20261114.md`,
y todavía **no están en el repositorio**: hay que pedírselos al dueño o al agente y subirlos)

| Validación | Resultado | Hallazgos |
|---|---|---|
| 112 `crear_superficies_corredor` | OK | Top (`TopLinks`) y Datum (`BottomLinks`), contorno `OutsideBoundary`. **`_.UNDO 1` no quita las superficies de corredor** (como la TIN). `guardar_dibujo` funciona |
| 113 `crear_secciones` | OK, con 1 fallo | 43 líneas y 43 vistas en 9×5, sin solapes; fuentes muestreadas correctas. **FALLO paso 9**: un segundo grupo en el mismo eje da `ArgumentException: Sample line name should not duplicate.` (los nombres de línea, "0+020.00", chocan con los del primer grupo) |
| 114 `calcular_volumenes` | OK, con 1 fallo | Totales correctos (corte 7211,34; relleno 29907,86; neto de Civil 3D = relleno − corte). **FALLO tabla por progresivas**: 42 de 42 tramos sin volumen porque `Section.SourceName` de la Datum es `"Corredor 1 Corredor 1 - Datum"` (corredor + superficie), no `"Corredor 1 - Datum"`. **`_.UNDO 1` no quita la superficie de volumen** |
| 1141 arreglos | **OK** | Capa `C-ROAD-CORR`; punto de inserción junto al eje con contenido lejano; las tres `agregar_*` funcionan; buscar_api en cp1252 OK |
| Flujo completo | OK salvo la tabla | 8 pasos con simulación previa y `aprobar_resultado`; **117 s frente a 130 min manuales**. `tabla_progresivas: null` por el mismo motivo de `SourceName` |
| Evaluación (memoria-firma, 14/11 09:59) | **6 de 10 OK** (antes 3) | 4, 5 y 6 pasan a OK. Falla la 7 por el nombre de línea duplicado; la 8 por la tabla (el total sí sale); la 9 porque no hay herramienta para la tabla de PI del eje; la 10 por la 7 y la 8 |

### Tarde del 14/11/2026: correcciones 1.14.2 y 1.14.3, `listar_pis` 1.15.0 y prueba corta (sesión orquestadora nueva)
- **Informes de la ronda A**: el dueño pegó los cinco en la sesión (112, 113, 114, 1141 y flujo completo). Se suben a
  `herramientas-dev/informes/` junto con los de la prueba corta (lo pidió así); mientras, quedan en la rama
  `claude/charming-brown-70s7g3`. El de la evaluación ya está en `memoria-firma/evaluacion/informes/`.
- **Diagnóstico de la orquestadora** (catálogo consultado el 14/11/2026): `SeccionEsDe` ya comparaba
  `Section.SourceId` con la TIN de la Datum, así que para una sección de superficie de corredor `SourceId` no es la
  TIN; el catálogo tiene `SampleLine.GetSectionId(ObjectId)`, `Section.CorridorSurfaceId` y
  `SectionSource.GetSectionIds()`, que se usan ahora en ese orden. La unicidad de nombres de línea de muestreo parece
  ser de todo el dibujo (tarea 7 de la evaluación: el eje no tenía grupos y falló igual).
- **Sesiones hijas lanzadas** (Opus, esfuerzo extra alto), en paralelo:
  - `session_011fq2pFW6xpNx7GkwSxLBXA`: **corrección 1.14.2** en `tarea/2.7-volumenes` (PR #31), con
    `VALIDACION_1142.md` corta (solo lo que falló).
  - `session_011fq2pHJPZdM53xHeQdQSZ9`: **1.15.0 `listar_pis`** (tabla de PI del eje, tarea 9 de la evaluación) en
    `tarea/2.9-tabla-pi`, que parte de `tarea/2.7-volumenes`; con `VALIDACION_115.md`. La orquestadora fusionará
    `tarea/2.7-volumenes` (con la 1.14.2) en ella al final: **`tarea/2.9-tabla-pi` será la rama que instala el dueño**.
- **Planes aprobados** por la orquestadora a las 15:26. La hija de `listar_pis` terminó a las 15:37 (commit local
  `7f2b298`, 4 pruebas OK: dotnet 375/375, puente 84, CompilarSinCivil 0 errores, evaluador 21), pero **no pudo hacer
  push**: GitHub responde 403 «GitHub authentication required» también a la orquestadora. **Bloqueado hasta que el
  dueño reconecte GitHub** (claude.ai/connect-github); después, push y PR de cada hija.
- **15:42, GitHub reconectado por el dueño.** La 1.14.2 subió a `tarea/2.7-volumenes` (`402b728`, PR #31 actualizado:
  370 pruebas xUnit) y `listar_pis` a `tarea/2.9-tabla-pi` (`7f2b298`, **PR #35** en borrador).
- **La orquestadora fusionó `tarea/2.7-volumenes` en `tarea/2.9-tabla-pi`** (commit `31ef86b`, versión **1.15.0**, 54
  herramientas). Conflictos solo de versión, README y tabla de miembros (quedan las dos secciones, 1.14.2 y 1.15.0).
  Cuatro pruebas OK: dotnet 396/396, puente 84, CompilarSinCivil 0 errores y 0 avisos, evaluador 21. El PR #35 dice
  "Para el agente instalador" y "Para Jose" de toda la prueba corta. **Orden de fusión cuando dé OK: #31 y luego #35.**
- **Aviso al dueño enviado (15:46): prueba corta lista**, con L.1 y P sobre `tarea/2.9-tabla-pi`.
- **L.1 OK en la PC**: rama `tarea/2.9-tabla-pi`, commit `31ef86b`, compilación Release sin errores ni avisos,
  bundle 1.15.0 instalado, `dotnet test` 396/396, pytest del puente 84/84, puente reiniciado. Falta el Prompt P
  (VALIDACION_1142 → VALIDACION_115) y la evaluación (5.2-D y 5.2-PC).
- **Prompt P (14/11, ~11:20 hora de la PC)**: **VALIDACION_115 OK** en todo (`listar_pis`: 2 PI con
  `diferencia_pi_civil3d: 0`; `Eje 115` con el PI sin curva bien). **VALIDACION_1142**: `crear_secciones` **OK** (segundo
  grupo con prefijo "Muestreo 100 "; las líneas y vistas son entidades del espacio modelo: 43/43) y la identificación de
  secciones **OK** (terreno y Datum 43/43 por `SampleLine.GetSectionId`), pero la tabla por progresivas sigue sin números
  por otro control: `supuesto_ejes.motivo` = un punto de la Datum con X = -30 fuera de `LeftOffset`/`RightOffset`
  (-3.9 a 3.6). X = -30 es el ancho de la línea: `LeftOffset`/`RightOffset` no son los extremos de la sección de una
  superficie de corredor. Totales correctos (corte 9014.1785, relleno 32898.6449). `PRUEBA_1142.dwg` quedó guardado.
- **Corrección 1.14.3** pedida a la hija `session_011fq2pFW6xpNx7GkwSxLBXA` en `tarea/2.7-volumenes`: límite de X por el
  ancho real de la línea; la comprobación decisiva pasa a ser la cota de la sección frente a la de la superficie (terreno
  y Datum, en el eje y a cada lado); diagnóstico de los puntos; `VALIDACION_1143.md` muy corta. Después, la orquestadora
  la fusiona en `tarea/2.9-tabla-pi` como **1.15.1**.
- Se propuso al dueño correr **ya** la evaluación con la 1.15.0 instalada (la tarea 8 mide los totales, que ya salen
  bien), para descubrir a la vez cualquier otro fallo; luego una instalación más, solo con VALIDACION_1143.
- **16:30, 1.14.3 subida** por la hija a `tarea/2.7-volumenes` (`0526c6a`, CI del PR #31 en verde): bloquean solo el
  ancho real de la línea (±0,01) y las cotas de terreno y Datum frente a sus superficies en el eje y a ±5 m;
  `LeftOffset`/`RightOffset` y las cotas mín./máx. pasan a aviso; bloque `puntos_secciones`; `VALIDACION_1143.md`.
- **La orquestadora la fusionó en `tarea/2.9-tabla-pi`** (commit `b37b21a`, versión **1.15.1**, 54 herramientas). Cuatro
  pruebas OK: dotnet 400/400, puente 84, CompilarSinCivil 0 errores y 0 avisos, evaluador 21. PR #35 actualizado
  ("Para Jose" → VALIDACION_1143 sobre `PRUEBA_1142.dwg` guardado).
- El dueño no recordaba el dibujo base de la evaluación: se le dio un prompt para que el agente instalador encuentre o
  prepare `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (plantilla métrica + ensamblaje, sin nada más) y lo
  anote en `ESTADO_PC.md`. En `memoria-firma/evaluacion/datos_prueba/` solo hay `.gitkeep`: los datos de la evaluación
  viven en la PC (pendiente: ¿subirlos a memoria-firma?).
- **Siguiente en la PC**: evaluación con la 1.15.0 ya instalada (en curso) y después L.1 con `tarea/2.9-tabla-pi`
  (`b37b21a`) + VALIDACION_1143.
- **Evaluación 5.2-D con la 1.15.0 (14/11, 11:50 hora de la PC): 10 de 10 OK** (antes 6 de 10). Hito de la Etapa 2
  (tarea 10, flujo completo) alcanzado: 176 s frente a 130 min manuales. Dibujo base nuevo y limpio:
  `PRUEBA_EVALUACION_BASE.dwg` (anotado en `ESTADO_PC.md`); corrida sobre la copia `PRUEBA_EVALUACION_20261114B.dwg` con
  los mismos datos genéricos de la mañana (`puntos_prueba.csv`, polilínea 1000,2000 → 1600,2300 con R 60, PVI 0/100,
  400/110 con curva de 40 m y final/105). Tarea 8: corte 7420.8346, relleno 32065.2314; tarea 9: `listar_pis` con
  `diferencia_pi_civil3d: 0`. **Ojo**: el agente marcó OK la 1 (curvas 1 y 5 m), la 8 (comparación con un cálculo manual)
  y la 9 (comparación con el informe de Civil 3D) sin esas comprobaciones manuales; anotarlo en la sesión de hallazgos.
  Pendiente en la PC: 5.2-PC (guardar el YAML con `comparar` y `sincronizar_memoria.ps1`).
- **Pendiente propuesto**: crear de verdad `memoria-firma/evaluacion/datos_prueba/` (`puntos_terreno.csv`,
  `eje_polilinea.dxf`, `pvi_rasante.csv`): hoy solo tiene `.gitkeep` y el agente improvisa con datos equivalentes.
- **Prueba corta prevista** (una instalación): VALIDACION_1142 → VALIDACION_115 → evaluación 5.2-D y 5.2-PC (su
  tarea 10 repite el flujo completo, así que no se repite VALIDACION_FLUJO_COMPLETO).

## Propuestas pendientes (no están en la hoja de ruta)

- Deshacer propio para las superficies que Ctrl+Z no quita: TIN (1.7.0), superficies de corredor (1.12.0) y de volumen (1.14.0).
- El caso "sin `frecuencia`" de VALIDACION_111: lo corta antes la validación del puente (Pydantic), no el plugin.
  Decidir si se cambia el guion.
- Tabla por progresivas frente a la superficie de volumen: corte 16,6 %, relleno 4,1 % (informe 1143). Manda la
  superficie; investigar si limitar las áreas al tramo donde existe la Datum (fuera de la TIN de la Datum la sección
  sigue teniendo puntos hasta ±30 m).
- Crear de verdad `memoria-firma/evaluacion/datos_prueba/` (`puntos_terreno.csv`, `eje_polilinea.dxf`,
  `pvi_rasante.csv`): hoy solo tiene `.gitkeep`.
- La evaluación marca OK comprobaciones manuales que no hace (curvas de la tarea 1, cálculo manual de la 8, informe de
  Civil 3D de la 9): aclararlo en `tareas.yaml` o en el prompt de evaluación.
- `crear_secciones`: comprobar si los nombres de **grupo** también deben ser únicos en todo el dibujo (propuesta de la
  hija de la 1.14.2) y añadir en `datos.lineas[].secciones` el nombre de la superficie del dibujo junto a `SourceName`.
