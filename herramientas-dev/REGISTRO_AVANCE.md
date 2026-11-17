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

## Estado actual (17/11/2026, ronda D en marcha)

Sesión orquestadora: `session_01CdHU39Fo1j8s6nx1s6ZJWQ` (rama `claude/lucid-keller-ldugr4`, solo registro). En `main`
sigue la 1.19.1. El dueño pidió hacer todas las «Propuestas pendientes» y añadir **peralte** y **bordes de calzada con
sobreancho** (hoy los hace a mano con un XML de criterios de Civil 3D). Sesiones hijas (Opus, esfuerzo extra alto, cada
una presenta su plan y la orquestadora lo aprueba por `send_message`):

| Tarea | Sesión | Rama / repo | Versión | Estado |
|---|---|---|---|---|
| 6.1 `ejecutar_codigo` protegido sin `solo_lectura` | `session_01CdHU3589GnkBnGhy7K9ugg` | `tarea/6.1-codigo-escritura-segura` | 1.19.2 | **terminada**, PR #54 (borrador), sesión archivada. Propuesta: `Close()`/`Cancel()` sobre objetos abiertos para escritura siguen compilando |
| 6.2 Documentos pendientes | `session_01CdHU39JEMaAg6NgzUWPSRn` | `tarea/6.2-documentos-pendientes` | — | **terminada**, PR #50 (borrador) |
| 6.3 `crear_secciones`: grupos y superficie | `session_01CdHU36NsaMFpqqxZwcY9Qd` | `tarea/6.3-secciones-grupos` | 1.19.3 | **terminada**, PR #53 (borrador), sesión archivada; la cadena de ramas la junta la orquestadora |
| 6.4 Tablas DG-2018 de peralte y sobreancho | `session_01CdHU35F8CtvPhdpJWdzCMn` | `memoria-firma` (main) | — | **terminada** (commits 5d4f7d9 a 13776b1), sesión archivada: tablas `bombeo`, `vehiculos_diseno`, `sobreancho_holguras`, `sobreancho_reduccion`, `figuras_peralte`, fórmulas F46-F49, 35 reglas, casos 6 a 9 y P1-P9 en DECISIONES.md. Esquema enviado a 6.5 y 6.6 |
| 6.5 Peralte (`listar_peralte`, `aplicar_peralte`) | `session_01CdHU35tiuKgqTBqsCUGeTE` | `tarea/6.5-peralte` | 1.20.0 | **terminada** (3344f05), PR #51 (borrador), sesión archivada. Casos reales: 109 OK, 0 fallos. No escribe peralte con la norma hasta rellenar CORRESPONDENCIA con los nombres reales de los enumerados (paso 2 de VALIDACION_120). La 6.6 la fusiona en su rama |
| 6.6 Bordes de calzada (`crear_bordes_calzada`, `listar_bordes_calzada`) | `session_01CdHU37Cy3fgHeSz8Jv7MJf` | `tarea/6.6-bordes-sobreancho` | 1.21.0 | **terminada**, PR #52 (borrador, contiene el #51): merge de la 6.5 en 553926a; 59 herramientas del plugin, 63 en total; casos reales con la base real: 112 OK, 0 fallos (orquestadora). Sesión archivada |
| 6.11 Integración (rama para la PC) | `session_01CdHU35Yv2uAL4QFZWHEia6` | `tarea/ronda-d` | 1.21.1 | **terminada** (30e12f2), PR #56 (borrador, contiene #50 a #55), sesión archivada. Nube: xUnit 713/713, puente 376, CompilarSinCivil 0 errores, evaluador 21. Orquestadora: casos reales con la base real 112 OK, 0 fallos; pytest del puente 376 |
| 6.7 Evaluación honesta (comprobaciones manuales) | `session_01CdHU38kGV8ado1NVu2nujn` | `memoria-firma` (main) | — | **terminada** (commit `8f35eea`): banco versión 2, las tareas 1, 2, 3, 5, 8, 9 y 10 solo dan OK con lo que se lee por el MCP; lo visual es revisión de la persona. Ojo: el eje de prueba mide 848,4956 y la tarea 5 pedía fin 1000 (el OK viejo no podía ser cierto). Quedan frases manuales menores en las tareas 4 y 6. La próxima corrida puede pasar de OK a FALLO por el criterio nuevo |
| 6.9 `verificar_norma` y propuesta de radios con las decisiones del dueño | `session_01CdHU36FFBPjbfjEgijDCci` | `tarea/6.9-norma-decisiones` | 1.21.1 | **terminada**, PR #55 (borrador), sesión archivada. La orquestadora corrió `casos_norma_reales.py` con la base real: 69 OK, 0 fallos; T302.11A a 60 km/h = 325 m |
| 6.10 Digitalizar las Figuras 302.02 a 302.05 (peralte) | `session_01CdHU34ytgX6VkmhiAmnegy` | `memoria-firma` (main) | — | **terminada** (c0e4d03, d4cf543, 6e8eb54), sesión archivada: tabla `peralte_figura` (1295 nodos, 40 curvas) en la base 1.3, Decisión 6 A CONFIRMAR, casos 10 a 12 |
| 6.8 XML DG-2018 corregido desde la base | `session_01CdHU36zSy3u8D4NojARtyj` | `memoria-firma` (main) | — | **terminada** (2094490, 202dc97), sesión archivada: `normas/civil3d/DG2018_desde_base.xml` (SHA 081a4116…) generado con `generar_xml_criterios.py`; guía `VALIDACION_XML_DG2018.md` (decisivos: que cargue y que salgan los métodos DG-2018). Comprobado por la orquestadora: radios y peraltes ordenados, NC a 30-50 km/h, sobreancho DG-2018 de 30 a 80 km/h. Límites: la T302.13 solo cubre 30-90 km/h; la tabla del 10 % sigue sin NC a 30-50 km/h (sin figura). Enviado al dueño |

**Decisión del dueño (17/11/2026)**: las sesiones hijas se **archivan** en cuanto terminan, después de comprobar que su rama o su commit están en GitHub (el archivo se puede deshacer con `unarchive_session`). Archivadas: todas (6.1 a 6.11).

Los informes del verificador independiente están en `memoria-firma/normas/tablas/VERIFICACION_PERALTE_SOBREANCHO_20261117_A.md`
y `VERIFICACION_CRITERIOS_PENDIENTES_20261117_B.md` (commit `b2dc3e4`).

Hechos de la ronda en la nube:
- Catálogo de Civil 3D 2027 (consultado por la orquestadora): la API permite escribir el peralte
  (`SuperelevationCurveCollection.AddUserDefinedCurve` → `SuperelevationCurve.CriticalStations.Add` + `SetSlope`) y crear
  bordes con ensanchamiento (`Alignment.CreateOffsetAlignment` + `OffsetAlignmentInfo.AddWidening`). **No hay método para
  quitar una curva de peralte**: `aplicar_peralte` rechaza un eje que ya tiene peralte. Los valores de los enumerados de
  peralte no están en el catálogo: se leen en Civil 3D con `Enum.GetNames` (paso 1 de VALIDACION_120).
- **La DG-2018 no trae tabla peralte–radio**: solo las Figuras 302.02 a 302.05 (gráficas). Lo confirmaron por separado
  la 6.4 y un verificador independiente (Opus; Fable sin cupo hasta el 24/11/2026 11:00 UTC). Las dos extracciones
  coinciden (L del B2 = 10,55 m, VL = 4,30 m, articulados sin L, T302.20 400 → 0,07 y 450 → 0,08, 30 m frente a V/3,6).
- XML de criterios del dueño (no se sube a ningún repositorio). Versión original: sobreancho por tabla AASHTO con
  vehículos de EE. UU. y solo de 50 a 100 km/h (por eso no funciona a 30 km/h), fórmula «simple» con 0,05·V/√R (la
  DG-2018 dice 0,1·V/√R), radios que no decrecen en varias filas y tablas que no llegan al radio mínimo. Versión
  «Corregido_2»: corrige radios, extremos, nombres y la fórmula, pero sigue sin sobreancho por tabla a 30-40 km/h, sin
  fila NC a 30-50 km/h (R ≥ 3500 m da RC en vez de sin peralte), con el método SU-12 mal escrito (`Vehicle` en vez de
  `VehicleType`), longitudes de transición de la T302.13 (Tercera Clase) para todas las clases y ×1,5 para 4 carriles
  (no es DG-2018), y peraltes de tipo AASHTO (cerca de las figuras: 60 km/h, R 150 m, 8 % → 7,6 % frente a ≈ 7,5 %).

**Decisiones del dueño (17/11/2026)**: de acuerdo con las recomendaciones del verificador en los criterios pendientes
(a: curva sin espiral bajo la T302.11 = «No cumple», sin «pendiente de confirmar», más aviso por 302.05.06; b: PI sin
curva a más de 80 km/h = «No cumple» y, si Δ < 59′, pedir quitar el PI; c: 500/400 m como aviso, 0°59′ = «No cumple» con
curva y aviso sin curva; d: −1 % y luego +1 %; e: lomo quebrado como aviso, pendiente media con ventanas ≥ 2000 m,
descansos solo si suman ≥ 500 m; f: no escribir un radio bajo el mínimo sin confirmación y decir qué velocidad admite;
g: 215 m mientras solo haya arcos, ofreciendo 125 m con clotoide). **Peralte por radio: digitalizar las Figuras 302.02 a
302.05** de la DG-2018. Se añaden la **6.8** (XML DG-2018 corregido, generado desde la base) y la **6.9** (corrección de
`verificar_norma` y la propuesta de radios con esas decisiones).

**Preguntas abiertas para Jose** (no bloquean): (1) el peralte mínimo del 2 %, ¿solo donde lo dice la T304.06 o para todo R por debajo del radio de la T304.04? (con R < 2500 m la figura da menos del 2 %); (2) R entre el radio mínimo de la tabla y el extremo de la curva dibujada (p. ej. 302.03 a 80 km/h: tabla 230 m, curva hasta 288 m): ¿p máx con aviso?; (3) los P2 a P9 de la 6.4 en `memoria-firma/normas/DECISIONES.md` (0,7p frente a T304.07, 30 m frente a V/3,6, segundo término de Sa, factor T302.20, L de los articulados, Sa < 0,40 m, desarrollo en 40 m o en la transición del peralte, tramo de 0 a 2 % en la espiral).

**Cadena de ramas prevista** (merge, nunca rebase ni force): 6.1 (1.19.2) → 6.3 (1.19.3) → 6.5 (1.20.0) → 6.6 (1.21.0) → 6.9 (1.21.1). 6.5 y 6.6 generalizaron `AMPLIACIONES_NORMA` de main.py de dos formas distintas: la 6.6 unifica al fusionar la 6.5. La 6.2 (solo documentos) se fusiona aparte.

**Siguiente**: el dueño corre en la PC el Prompt L.1 (rama `tarea/ronda-d`) y luego el Prompt P con `herramientas-dev/VALIDACION_RONDA_D.md` (están en el PR #56). Primero el paso 2 de VALIDACION_120: con los nombres reales de los enumerados se rellena CORRESPONDENCIA en la misma rama (regla 11f). Después: sesión de hallazgos, decisiones de Jose (Decisión 6, P2 a P9, 2 % mínimo) y fusión de los PR por el dueño. Aparte: probar `memoria-firma/normas/civil3d/DG2018_desde_base.xml` con `VALIDACION_XML_DG2018.md`.
digitalización (6.10, en `memoria-firma`); después 6.8; juntar las ramas en una para la ronda D en la PC.

## Estado anterior (17/11/2026, ronda C cerrada: hoja de ruta programada completa)

**En `main`: versión 1.19.1, 59 herramientas (55 del plugin + 4 del puente), validada en Civil 3D 2027.** El dueño
escribió «Verificación OK» y el PR #48 (`tarea/ronda-c`) se fusionó el 17/11 (commit `dba04e3`; L.1 con `main` OK en la
PC: 609/609 xUnit, 261 pytest, `ping` 1.19.1). Ojo: la fusión la hizo el agente instalador con `gh pr merge`, y según
la regla 11g la hace el dueño desde GitHub; no repetirlo.

Cómo fue la prueba (informes en `herramientas-dev/informes/`, del 17/11/2026):
- Primera pasada con la 1.19.0 (`informe_validacion_1176_20261117.md`): pasos 0 a 8 OK; en el **paso 9** `Curve.Extend`
  con `solo_lectura=true` **cerró Civil 3D**. Corrección **1.19.1** en la misma rama (con `solo_lectura`, solo métodos de
  lectura en los objetos del dibujo), aprobada por el dueño y validada en la segunda pasada.
- Segunda pasada con la 1.19.1: parte 1 repetida entera (`..._1176_20261117_b.md`), 12 pasos decisivos OK, el paso 9
  rechazado al compilar con Civil 3D abierto; partes 2 (`..._118_...`), 3 (`..._119_...`) y 4
  (`..._deshacer_...`) OK. `detalle_validacion_ronda_c_20261117.md` trae las líneas literales del `mcp_log.jsonl`.
- El primer informe corto del agente traía cifras inventadas («25/25», «64/64», «TypeScript») y un texto de error
  reconstruido; con las líneas literales del `mcp_log.jsonl` todo cuadró. **Pedir siempre el `error` literal del log**.
- Parte 4: sin zoom, **un `_.UNDO 1` quita** la TIN, el eje, el perfil y su vista, las superficies Top y Datum y la
  superficie de volumen: se descarta el deshacer propio de superficies.

Hallazgos pasados a la documentación en la tarea **4.4** (`tarea/4.4-hallazgos-ronda-c`): `CLAUDE.md` (versión y hechos
validados), tabla de miembros, hoja de ruta (Etapa 4 hecha) y este registro.

**Siguiente**: nada programado en la hoja de ruta. Quedan la 3.3 (cada mes), F, L.2 y 5.2 (continuo), los puntos de la
norma que Jose debe confirmar (lista en «Estado anterior», abajo) y las «Propuestas pendientes».

## Estado anterior (17/11/2026, ronda C lista para la PC)

**Rama para la PC: `tarea/ronda-c`, versión 1.19.0, 59 herramientas (55 del plugin + 4 del puente).** Junta:
- `tarea/3.6-correcciones-1176` (hija B, `session_01GF7ZdydzsaLV7WfrxKwhVU`): 1.17.6 + correcciones de la auditoría
  en `ejecutar_codigo` (I3-I6, N1).
- `tarea/4.2-verificar-norma` (hija A, `session_01GF7Zdz8LwM4QbJw4N79ebj`): `verificar_norma`, 1.18.0.
- `tarea/4.3-norma-alineamiento` (hija C, `session_01GF7Ze1Sj1yRE7XUNRKaQkR`): `velocidad_diseno` en
  `crear_alineamiento_desde_polilinea`, 1.19.0, más las correcciones de la auditoría en la norma (I1, I2, M1, M2, M4,
  M5).
- Índice de la prueba: `herramientas-dev/VALIDACION_RONDA_C.md` (partes 1176, 118, 119 y `_.UNDO 1` sin zoom).

Hechos de la ronda en la nube:
- `memoria-firma` (directo en `main`): base DG-2018 ampliada (T302.01, T302.07/08, cuadros de deflexión y 3V/6V,
  reglas, equivalencias, F34-F45), `normas/DECISIONES.md` y casos con «pavimentada»; verificación independiente contra
  el PDF con 0 errores (`518fa70`).
- `casos_norma_reales.py` contra la base real: 69 OK, 0 fallos (casos 2 y 3 fuera de alcance).
- Auditoría independiente con Fable (0 bloqueantes, 6 importantes, 9 menores) y segunda revisión de las correcciones
  (1 importante, N1, corregido). Quedan documentados M3 (Tercera pavimentada sin fila de Segunda) y M6 (unidades
  leídas de dos sitios); M7 (`CLAUDE.md` en 1.17.5) se hace en la sesión de hallazgos.
- Puntos para que Jose confirme (no bloquean la prueba): E7 sin espiral = No cumple; PI sin curva a más de 80 km/h =
  No cumple; 500/400 m y 0°59′ como aviso; orden −1 %/+1 %; criterios propios donde la norma no define (lomo
  quebrado, ventanas de pendiente media, descansos…); radio menor que el mínimo cuando la polilínea no deja sitio;
  columna `tipo` de `reglas`; esquema e ids de la base en el repositorio público (M9).
- **Siguiente**: PR en borrador de `tarea/ronda-c`, L.1 y P en la PC con `VALIDACION_RONDA_C.md`, sesión de hallazgos.

## Estado anterior (17/11/2026, ronda C en marcha)

**4.1 cerrada en `memoria-firma` el 17/11** (commit `0f8ca14`): verificación independiente de `dg2018.sqlite` contra el
PDF por cuatro agentes, 1679 valores, **0 errores** (`normas/tablas/VERIFICACION_INDEPENDIENTE_20261117.md`). Decisión de
Jose sobre la Tabla 303.02: **manda la tabla**, con un aviso cuando la fórmula del texto pediría más. El PDF se lee del
repositorio público `Andy-rba30/normas` (`normas/fuentes/FUENTE.md`, con su SHA-256).

**Plan de la ronda C aprobado por el dueño (17/11)** en la sesión orquestadora `session_01GF7ZdyGvbtPBDHk9EKj2jX`
(rama `claude/charming-cori-611983`):
1. Agente de investigación (solo lectura): revisar los 5 casos de `casos_dg2018.md` contra el PDF (los hizo una sesión
   de Claude, no Jose), mapa de comprobaciones de `verificar_norma` con lo que leen `listar_pis` y `listar_pvis`, y
   cómo elegir el radio mínimo por PI para la 4.3.
2. En paralelo: **hija A**, 4.2 `verificar_norma` en el puente (Python), 1.18.0, `tarea/4.2-verificar-norma`; **hija
   B**, corrección 1.17.6 del plugin (aviso de `Entity.StyleId` en `crear_superficie_tin` y `ejecutar_codigo` de solo
   lectura sin entrada de Deshacer ni copia), `tarea/3.6-correcciones-1176` (`session_01GF7ZdydzsaLV7WfrxKwhVU`).
3. Después de A: **hija C**, 4.3 (`velocidad_diseno` en `crear_alineamiento_desde_polilinea`), 1.19.0, partiendo de A.
4. Una sola rama para la PC, con su `VALIDACION`, L.1 y P; en la misma prueba, repetir **sin zoom** el `_.UNDO 1` de
   la TIN, las superficies de corredor, el perfil de terreno y el eje.
5. Auditoría final independiente (un agente con Fable) antes de avisar al dueño para probar.
6. Si da OK: sesión de hallazgos, fusión de los PR y registro al día.
Fuera de la ronda: el deshacer propio de superficies (choca con la regla 4; se decide tras repetir la prueba sin zoom).

## Estado anterior (16/11/2026, Etapa 3 cerrada)

**En `main`: versión 1.17.5, 55 herramientas del plugin, validada en Civil 3D 2027.** El dueño fusionó el 16/11 el
**#43** (ronda B, 1.17.4), el **#45** (hallazgos de la ronda B) y el **#46** (`tarea/3.4-codigo-objetos-deshacer`,
1.17.5). No queda ningún PR abierto y todas las ramas están fusionadas en `main`.
- **L.1 con `main` (`ae1edfb`) OK en la PC** (16/11): Release sin errores ni avisos, `dotnet test` 578/578, pytest del
  puente 166/166. `ARBA_MCP_CODIGO` estaba vacía: la puso L.1 y la quitó el paso 11 de la validación.
- **VALIDACION_1175 OK** (16/11, `informes/informe_validacion_1175_20261116.md`, subido por el agente instalador en la
  rama `informes/1175`): pasos decisivos 0, 4, 5, 6, 8 y 10 OK. `objetos_creados: 1` en simulación y en real, solo por la
  fuente `fachada`; un solo `_.UNDO 1` quita la línea sin tocar la vista; el fallo de la 116 era el zoom (paso 9); el
  alias `TransactionManager` compila. El «hallazgo» del 9d es del guion: el conteo es otra simulación de
  `ejecutar_codigo` y deja su propia `Executefunction`. Hallazgos pasados a la tabla de miembros, a `CLAUDE.md` y a la
  sección 0 de la hoja de ruta (sesión `claude/sharp-planck-e8miv2`).
- **Siguiente**: que Jose revise en memoria-firma `normas/tablas/VERIFICAR.md` y `normas/casos_prueba/casos_dg2018.md`
  y decida la duda de la Tabla 303.02 (sección «Ronda B en la PC», 04:14 y 04:24 del 15/11). Con las tablas críticas al
  100 %, se lanza la **4.2 `verificar_norma`** y luego la 4.3 (ronda C).

## Estado anterior (14/11/2026, ronda A cerrada)

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

### Ronda B en la PC: primer intento (14/11/2026, noche) y corrección 1.17.3
- **VALIDACION_116 detenida en el paso 4a** (`id_log` `20261114-194520-06c83b`). Pasos 0 a 3 OK (`ping` 1.17.2,
  `ARBA_MCP_CODIGO=1`). El 4a (contar líneas, simulación) dio 282 errores **CS0009** «La imagen PE no contiene metadatos
  administrados» (`slidelib.exe`, `QtWebEngineProcess.exe`, `w3dtk_wt.dll`...): no se ejecutó nada, sin copia
  (`backups\` siguió en 12) ni entrada en Deshacer. El informe completo se pidió como texto; aún no hay archivo.
- **Diagnóstico**: en acad.exe, `TRUSTED_PLATFORM_ASSEMBLIES` trae todos los `.dll` y `.exe` de la carpeta de AutoCAD
  (también nativos) y `ReferenciasCodigo()` los pasaba todos a Roslyn. En Linux no sale: el TPA del testhost solo trae
  ensamblados .NET.
- **15/11 01:37, corrección 1.17.3 aprobada por el dueño** y encargada a la hija `session_01XnJWqafDo6REoPEUhS9Tds`
  (Opus, esfuerzo extra alto) en la **misma rama `tarea/ronda-b`** (PR #43): `ArbaMcp.Nucleo/CodigoReferencias.cs`
  (comprobación de metadatos con `PEReader` y filtro por carpeta: de AutoCAD solo lo ya cargado), uso en
  `ReferenciasCodigo()`, pruebas `CodigoReferenciasPruebas.cs`, versión 1.17.3 y `ping` esperado en VALIDACION_116, 117,
  1171 y 1172. Sin VALIDACION_1173: en la PC se repite el paso 0 de la 116 y se sigue **desde el paso 4**.
- **Revisión previa en paralelo** (solo lectura, la orquestadora): buscar otros fallos que solo saldrían en acad.exe
  (contexto aislado de Roslyn, carga del ensamblado del script, CS0012, biblioteca falsa frente a la API real) antes de
  volver a la PC. El dueño pidió (15/11) **probar en su PC solo cuando sea necesario**: no se le avisa hasta tener la
  1.17.3 subida, con la CI en verde y la revisión resuelta.
- **01:48, 1.17.3 subida** por la hija (`4830c75`; código y pruebas `e2d0c3a`, versión `5a89e79`). Su contenedor no tenía
  red para .NET ni PyPI: no corrió ninguna prueba; la **CI del PR #43 dio verde** en los tres trabajos (xUnit +
  CompilarSinCivil, puente, evaluador). `uv.lock` cambiado a mano en la línea de la versión (como en las subidas
  anteriores; `uv lock --check` lo acepta).
- **Resultado de la revisión previa**: **H1** (medio) AeccDbMgd (`C3D\`) y AecBaseMgd (`ACA\`) solo entran en las
  referencias si ya están cargadas (nunca estuvieron en el TPA) → CS0234 con el alias `Civ`, y la caché se congela sin
  AecBaseMgd (CS0012); **H2** (bajo, impacto alto) con un script que falla, `t1` no recibe su propio Abort/Dispose (se
  aborta otro envoltorio de `TopTransaction`) → posible cierre de Civil 3D por el finalizador. Menores, como propuestas:
  **H3** `TransactionManager` y `Surface` ambiguos con los using por defecto en la API real (CS0104 en scripts del
  agente); **H4** `DBObject` hereda de `GraphicsInterface.Drawable` en la API real: un miembro heredado y no
  sobrescrito podría salir bloqueado por la lista blanca (comprobar en Civil 3D).
- **El dueño aprobó incluir H1 y H2 en la 1.17.3** (sin subir versión: aún no se instaló). Encargado a la misma hija con
  el plan aprobado: las cinco DLL del plugin por ruta (esquema de los HintPath) y caché solo con las cinco; `_t1` con su
  propio Abort/Dispose en `AbortarEncimaDe`; en VALIDACION_116, aviso de CS0234/CS0012 en el 4a y `ping` 30 s después
  de los pasos con error o tiempo agotado.
- **02:49, H1 y H2 subidos** por la hija (`18a747b`; núcleo y pruebas `ce4d4df`, plugin `715d3e4`), versión 1.17.3.
  Revisados por la orquestadora. **CI del PR #43 en verde**: xUnit 558/558, CompilarSinCivil 0 errores, puente 166,
  evaluador OK. La hija tocó además una frase de `README.md` (la de la 1.17.3, que con H1 quedaba falsa): aceptado.
  Descripción del PR #43 actualizada a 1.17.3 (título, tabla, pruebas, «Para el agente instalador» y «Para Jose»).
- **Aviso al dueño enviado (15/11): ronda B lista para retomar en la PC**, con L.1 (`tarea/ronda-b`, `18a747b`) y P:
  VALIDACION_116 desde el paso 0 (`ping` 1.17.3) y luego desde el **paso 4** sobre el mismo `PRUEBA_116.dwg`, después
  117 → 1171 → 1172 → evaluación.
- **L.1 OK en la PC** (15/11): `tarea/ronda-b` en `18a747b`, versión 1.17.3, compilación Release con las DLL reales
  sin errores ni avisos, `dotnet test` 558/558, pytest del puente 166, puente reiniciado y MCP `civil3d` reconectado,
  `ARBA_MCP_CODIGO=1`, `ESTADO_PC.md` al día. Falta el Prompt P.
- **Prompt P con la 1.17.3: VALIDACION_116 detenida otra vez en el paso 4a** (`id_log` `20261114-220719-440eae`,
  1399 ms). Paso 0 OK (`ping` 1.17.3, 58 herramientas en el MCP). El 4a da **un solo error**: `CS0234 'Civil' no
  existe en el espacio de nombres 'Autodesk'`. `backups\` siguió en 12. El agente guardó
  `informe_validacion_116_20261114.md` en la PC (todavía no está en el repositorio).
- **Diagnóstico confirmado con el catálogo** (`memoria-firma`, tabla `ensamblados`): en Civil 3D 2027 el **nombre de
  ensamblado** de `acdbmgd.dll` es **`Acdbmgd`** (y el de `acmgd.dll`, `Acmgd`). `ReferenciasCodigo()` buscaba la carpeta
  de AutoCAD con `GetName().Name == "acdbmgd"`, que distingue mayúsculas, así que `dirAcad` era siempre `null`. Eso
  explica los dos informes: con la 1.17.2, sin filtro por carpeta, salieron los 282 CS0009 y el CS0234 quedó escondido;
  con la 1.17.3 el filtro de metadatos quitó los CS0009, pero sin `dirAcad` no entraron las cinco DLL ni lo cargado de
  AutoCAD (`C3D\` y `ACA\` nunca están en el TPA). Es la única comparación así: el resto usa `OrdinalIgnoreCase`.
- **Corrección 1.17.4** encargada a la misma hija, con el plan aprobado por la orquestadora (decisión vigente: correcciones
  pequeñas tras un informe):
  - `dirAcad` sale de `typeof(Database).Assembly.Location`, y si no, de `CodigoReferencias.CarpetaDeAutoCAD`, que busca
    el nombre sin distinguir mayúsculas.
  - Si la compilación falla sin las cinco DLL, el error dice qué falta. La primera vez que se arma la lista, deja una
    línea en el historial.
  - Pruebas con `Acdbmgd`, versión 1.17.4 y `ping` esperado en las cuatro VALIDACION.
- **03:14, 1.17.4 subida** por la hija (`b0287f8`; núcleo y pruebas `3cb047b`, plugin `c5a2ad3`, versión `0262240`).
  Revisada por la orquestadora: `dirAcad` sale de `typeof(Database).Assembly`, con `CarpetaDeAutoCAD` como respaldo;
  `DiagnosticoReferencias` se añade al error si la compilación falla, y `ResumenReferencias` va al historial la primera
  vez. Hay 6 pruebas nuevas.
- **CI del PR #43 en verde sobre `b0287f8`**: xUnit 564/564, CompilarSinCivil 0 errores, puente y evaluador OK.
  Descripción del PR actualizada a 1.17.4. **Aviso al dueño enviado**: L.1 con `b0287f8` (`ping` 1.17.4) y P (116 desde
  el paso 4, luego 117 → 1171 → 1172 → evaluación).
- **L.1 de la 1.17.4 OK en la PC** (15/11): `b0287f8`, compilación Release sin errores ni avisos, `dotnet test` 564/564,
  pytest del puente 166, puente reiniciado y MCP `civil3d` reconectado, `ARBA_MCP_CODIGO=1`. Falta el Prompt P.
- **Prompt P con la 1.17.4: VALIDACION_116 OK** (pasos decisivos 0, 4, 6, 8 y 9). Paso 9: `TimeoutException` a los
  60 s (`ms_puente` 60086) y Civil 3D estable (`ping` justo después y 30 s después). Paso 10: error del script en la
  línea 1 y `ping` estable 30 s después. Informe en la PC: `informe_validacion_116_20261114.md`.
  **VALIDACION_117 en curso**: pasos 0 a 6 OK; el agente espera a Jose en el paso 2 (capa `MCP Receta A`, color 3,
  para `aprobar_resultado`) y en el paso 7 (frase literal «Sí, guárdala como receta»).
- **VALIDACION_117 partes A y B OK.** Parte A (pasos 0 a 10): receta `crear_capa_con_color` guardada con la frase
  literal de Jose y subida con L.2 (commit `5456524` de memoria-firma). Parte B, en una conversación **nueva** del agente
  (paso 11, decisivo): `buscar_api` («crear capa color») da la receta la primera; luego `ejecutar_receta` simulada
  (`veces_usada` 0, `sumada` false), Jose confirma y se ejecuta de verdad (`veces_usada` 1). Paso 13: `MCP Receta C`
  con el color por defecto 7, `veces_usada` 2. Paso 14: `colour` rechazado sin llamar al plugin. Pasos 15 y 16: log
  con los pedidos y YAML con `veces_usada: 2`. Falta la parte C (17 a 20: comando `buscar_api`, quitar
  `ARBA_MCP_CODIGO`, comprobar que queda desactivada y L.2), y después 1171, 1172 y la evaluación.
- **Ronda B terminada en la PC (14/11, ~22:52 hora de la PC) con la 1.17.4 (`b0287f8`)**:
  - **116 OK** en los pasos decisivos. **Hallazgos**: en 4b y 5a, `despues.objetos_creados: 0` aunque la línea se creó
    (se esperaba 1: la detección de objetos creados no funcionó); 5c: tras `_.UNDO 1` el conteo da N0 + 1 (la línea
    **no** se quitó).
  - **117 OK global** (partes A, B y C; L.2 final con el commit `7083d1e` de memoria-firma; `ARBA_MCP_CODIGO` quitada y
    la herramienta desactivada otra vez).
  - **1171 OK**: diferencia de la tabla con la superficie de volumen de **0,31 % en corte y 0,05 % en relleno** (antes,
    16,58 % y 4,13 %).
  - **1172 OK**: elige `Contours 1m and 5m (Background)` (`aplicados: true`). Aviso menor: «No se pudo releer el estilo
    de la superficie creada (Entity.StyleId)».
  - **Evaluación: 10 de 10 OK** en 284 s; la tarea 1 aplica las curvas de 1 y 5 m y la tarea 8 da 0,21 % y 0,06 %.
  - Informes (116, 117, 1171, 1172 y dos YAML de la evaluación) subidos por el agente instalador en `informes/ronda-b`
    (`875f11b`), revisados (solo rutas genéricas de prueba, sin token) y llevados a `tarea/ronda-b` (avance directo), así
    que entran en `main` con el PR #43.
  - Recomendado al dueño: «Verificación OK» y fusión del #43; los dos hallazgos de la 116 van como corrección aparte, que
    se probará con la ronda C.
- **15/11 ~04:00: el dueño dio «Verificación OK» de la ronda B.** El PR #43 pasó a «listo»; el **Merge lo hace el
  dueño**, y después L.1 con `main` (`ping` 1.17.4). Sesiones hijas lanzadas para la noche (Opus, esfuerzo extra alto):
  - `session_01XnJWqbZr8U8ayr9SDd4PUP`: **corrección 1.17.5** en `tarea/3.4-codigo-objetos-deshacer`, que parte de
    `tarea/ronda-b`: objetos creados en 0, `_.UNDO 1` tras `ejecutar_codigo` y el alias H3, con `VALIDACION_1175.md`.
    Necesita la PC, así que se prueba en la próxima instalación.
  - `session_01XnJWqbXgf2DcUmEDfvbkhM`: **hallazgos de la ronda B**, solo documentación, en
    `tarea/3.5-hallazgos-ronda-b`: la tabla de miembros, los hechos validados de `CLAUDE.md` y la sección 0 de la hoja
    de ruta. Se fusiona sin la PC.
- **15/11 ~04:05, Etapa 4 desbloqueada**: el dueño indicó el PDF de su repositorio público `Andy-rba30/Normas`
  (`normas/Manual.de.Carreteras.DG-2018.pdf`: 285 páginas, con texto) y pidió que los 5 casos los prepare Claude con la
  norma; él los revisa. Dos sesiones sobre memoria-firma, directo en `main`:
  - `session_01XnJWqYUqvDdqAqByaDyacY`: **4.1** (prompt literal, con notas). Sin copiar el PDF: un `FUENTE.md` con su
    ubicación y el SHA-256. Produce `dg2018.sqlite`, `VERSION.md`, el script, `LEEME.md` y `VERIFICAR.md` con imágenes.
  - `session_01XnJWqaBA6s29sWjXdYLDjf`: **5 casos de prueba** en `normas/casos_prueba/`, sacados directamente del PDF y
    sin usar lo de la 4.1, para que sirvan de comprobación independiente.
  - Aviso al dueño: el repositorio `Normas` es público y contiene normas de AASHTO y ASTM con derechos de autor.
- **04:12, hallazgos de la ronda B terminados**: **PR #45** en borrador (`tarea/3.5-hallazgos-ronda-b`, `c20a5d2`), solo
  documentación, revisado por la orquestadora. Se fusiona **después del #43**, porque depende de él. Plan de la 1.17.5
  aprobado a las 04:10: cinco fuentes de detección con `datos.objetos.fuente_deteccion` y `datos.deshacer` (`UNDOCTL`,
  `DBMOD`, `CMDNAMES`), y el alias H3 confirmado con el catálogo (la `TransactionManager` de ApplicationServices deriva
  de la de DatabaseServices). Hipótesis del Deshacer: el `ZOOM` que pedía el paso 5a. VALIDACION_1175 pide no hacer
  ZOOM y anotar el menú Deshacer.
- **04:14, 5 casos DG-2018 terminados** (memoria-firma `main`, `d082973`, en `normas/casos_prueba/casos_dg2018.md` y
  `.yaml`, con 30 ejemplos de «Cumple» y «No cumple»):
  1. radio mínimo a 40, 60 y 80 km/h: 50, 125 y 230 m (Tabla 302.02, pág. 129);
  2. peralte con R 125 m: 8 %, y con R 3000 m: 2 %;
  3. visibilidad de parada a 80 km/h: 130, 144 y 118 m (Tabla 205.01 y 205.01-A);
  4. pendiente máxima: 8 % (Tabla 303.01);
  5. curva vertical: convexa 77 m (K 11) y cóncava 144 m (K 18).
  **Duda para Jose**: la Tabla 303.02 (convexa) no sale de la fórmula del texto (h2 = 0,15 m): la fórmula da 125 m y
  la tabla 77 m, con una h2 implícita de unos 0,60 m. El caso sigue la tabla; el ejemplo de borde queda
  `pendiente_de_decision`. Otras observaciones: 302.02 frente a 302.04 a 50 km/h (85 m y 80 m); erratas en 304.05 y en
  205.01 a 120 km/h.
- **04:22, 1.17.5 terminada**: **PR #46** en borrador (`tarea/3.4-codigo-objetos-deshacer`, `14a3c08`; depende del #43).
  Incluye `CodigoCambios.cs` y `CodigoDeshacer` con 14 pruebas, las cinco fuentes de detección, `datos.deshacer` y el
  alias H3, con `VALIDACION_1175.md`. Nada compilado en la nube: lo cubre la CI. Revisión previa de solo lectura en
  marcha, por el riesgo nuevo de que la línea entre en Creados y salte el aviso «ATENCIÓN» tras abortar.
- **04:24, 4.1 terminada** (memoria-firma `main`, `026a363`; Actions en verde). Se extrajeron 33 tablas y figuras, con
  1483 valores, 33 fórmulas con su sección y página y 33 observaciones. Contenido: `dg2018.sqlite`,
  `extraer_dg2018.py` (repetible; comprueba el SHA-256 del PDF), `LEEME.md`, `VERSION.md` (R.D. 03-2018-MTC/14, «a
  confirmar por Jose»), `VERIFICAR.md` con 34 imágenes y `normas/fuentes/FUENTE.md`. Los 5 casos coinciden con el
  sqlite. **Jose debe revisar primero**: la 204.01, leída por el color de las celdas; posibles erratas del PDF
  guardadas tal cual; la discrepancia entre 205.05 y 302.22; los peraltes de 302.02 frente a 304.05; y el alcance de
  Tercera Clase. Regla del PDF: **no seguir a la 4.2 sin el 100 % de las tablas críticas comprobado**.
- **04:25, CI del #46**: xUnit 577/578. Falla la prueba nueva del alias H3, que ejecutaba el script con un tipo de la
  biblioteca falsa, que solo existe para compilar (`FileNotFoundException 'AutodeskFalsa'`). Encargado a la hija que
  esa prueba solo compile. Es un fallo de la prueba, no del plugin.
- **04:27, prueba H3 corregida** (`790f951`). **Revisión previa de la 1.17.5**: nada de confianza alta; los textos de
  VALIDACION_1175 coinciden con el código, y la fuente `fachada` siempre ve la línea. Se encargan dos arreglos baratos
  en la misma rama: soltar los manejadores de `eventos_al_cerrar` antes de `t0.Dispose()` y desechar las
  `DBObjectCollection` de `GetAllObjects`.
- **04:28, 1.17.5 lista para la PC**: `b2656da` (manejadores en un `finally` anidado y `Dispose` de las colecciones;
  sustituto `DBObjectCollection` con `IDisposable`, que según el catálogo hereda de `DisposableWrapper`). **CI del #46
  en verde**: xUnit 578/578, CompilarSinCivil con 0 errores, puente y evaluador correctos.
- **Plan para la mañana del 15/11 (dueño)**:
  1. Merge del #43 y L.1 con `main` (`ping` 1.17.4).
  2. L.1 con `tarea/3.4-codigo-objetos-deshacer` (`b2656da`, `ping` 1.17.5) y P con `VALIDACION_1175.md`. Pone
     `ARBA_MCP_CODIGO=1` en `PRUEBA_1175.dwg`, y **sin ZOOM** entre los pasos 6 y 8.
  3. Revisar en memoria-firma `normas/tablas/VERIFICAR.md` (prioridad 1) y `normas/casos_prueba/casos_dg2018.md`, y
     decidir la duda de la Tabla 303.02.
  4. Fusionar el #45 (documentación) después del #43.
  Después, si 1175 da OK, fusionar el #46, y si las tablas están al 100 %, lanzar la **4.2 `verificar_norma`**.
- Etapa 4 sigue bloqueada (15/11): `memoria-firma/normas/fuentes/` solo tiene `.gitkeep`. Los datos de la evaluación
  **ya están** en `memoria-firma/evaluacion/datos_prueba/` (sincronización del 14/11 16:03).

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

- `ejecutar_codigo` **sin** `solo_lectura`: un script que cambie un objeto abierto para lectura (olvidando
  `F.Abrir(id, true)`) puede cerrar Civil 3D, como `Curve.Extend` en la ronda C (17/11). Valorar una protección también
  en ese modo (por ejemplo, avisar al compilar si se llama a un método que no es de lectura sobre un objeto abierto con
  `F.Abrir` sin `escribir`). Es la propuesta más importante de esta lista.
- Informe 118, paso 2: el agente dice que `_.-LAYER _M EJE_NORMA  ` (dos espacios) necesitó un Enter más para cerrar;
  en las partes 1 y 4 el mismo patrón terminó bien. Comprobar en otra prueba antes de cambiar los `VALIDACION_*.md`.
- El agente instalador fusionó el PR #48 con `gh pr merge` (17/11): anotar en `ESTADO_PC.md` de la PC que la fusión la
  hace el dueño (regla 11g).
- El caso "sin `frecuencia`" de VALIDACION_111: lo corta antes la validación del puente (Pydantic), no el plugin.
  Decidir si se cambia el guion.
- Tabla por progresivas frente a la superficie de volumen: corte 16,6 %, relleno 4,1 % (informe 1143). Manda la
  superficie; investigar si limitar las áreas al tramo donde existe la Datum (fuera de la TIN de la Datum la sección
  sigue teniendo puntos hasta ±30 m).
- La evaluación marca OK comprobaciones manuales que no hace (curvas de la tarea 1, cálculo manual de la 8, informe de
  Civil 3D de la 9): aclararlo en `tareas.yaml` o en el prompt de evaluación.
- `ejecutar_codigo` (revisión previa del 15/11): comprobar en Civil 3D si algún miembro heredado de
  `GraphicsInterface.Drawable` sale bloqueado por la lista blanca (H4). El alias H3 quedó resuelto y validado en la
  1.17.5 (16/11).
- `ejecutar_codigo` (informe 1175): la fuente `transaccion_script` trae objetos pero no marca la línea como nueva
  (`IsNewObject`): sin aclarar, no bloquea. (Lo de que un conteo dejaba entrada en Deshacer lo resolvió
  `solo_lectura`, 1.17.6, validado el 17/11.)
- `crear_secciones`: comprobar si los nombres de **grupo** también deben ser únicos en todo el dibujo (propuesta de la
  hija de la 1.14.2) y añadir en `datos.lineas[].secciones` el nombre de la superficie del dibujo junto a `SourceName`.
