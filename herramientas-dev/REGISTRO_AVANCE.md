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

## Estado actual (14/11/2026, tras la prueba de la ronda A)

**En `main`**: versión **1.11.0**, validada. **Ronda A probada en la PC el 14/11/2026** sobre la rama
**`tarea/2.7-volumenes`** (versión 1.14.1, 53 herramientas, commit `a502066`). Los PR #26, #27, #29, #30 y #31
siguen **en borrador y sin fusionar**: quedan dos fallos que se corrigen en esa misma rama.

### Resultado de la ronda A (resumen del agente instalador; los informes completos están en la PC, en
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

### En curso (14/11/2026, tarde; sesión orquestadora nueva)
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
- **Prueba corta prevista** (una instalación): VALIDACION_1142 → VALIDACION_115 → evaluación 5.2-D y 5.2-PC (su
  tarea 10 repite el flujo completo, así que no se repite VALIDACION_FLUJO_COMPLETO).

### Siguiente paso (al retomar)
1. **Corrección 1.14.2 en `tarea/2.7-volumenes`** (una sesión hija, plan aprobado por la orquestadora):
   a) `crear_secciones`: nombres de línea únicos aunque ya haya otros grupos en el eje (por ejemplo, con el nombre del
   grupo delante) y comprobación previa de nombres;
   b) `calcular_volumenes`: identificar las secciones por `Section.SourceId` (o `CorridorSurfaceId`), no por
   `SourceName`, y documentar el formato real de `SourceName`.
   Opcional y propuesto (decidirlo al retomar): una herramienta de lectura de la **tabla de PI del eje** (tarea 9 de
   la evaluación), como 1.15.0.
2. **Prueba corta en la PC**: solo lo que falló (VALIDACION_113 paso 9, VALIDACION_114 tabla, paso 8 del flujo
   completo) y repetir la evaluación (tareas 7, 8 y 10, y la 9 si se hace la herramienta).
3. Si da OK: fusionar #31 (contiene #26, #27, #29 y #30; cerrar esos como incluidos), subir los seis informes,
   sesión de hallazgos (la tabla de miembros y los "Hechos ya validados": los `_.UNDO 1` que no quitan superficies de
   corredor ni de volumen, los valores `TopLinks`/`BottomLinks`, el formato de `SourceName` y el signo del neto) y
   marcar la Etapa 2 como hecha en la hoja de ruta.
4. Después: **Ronda B** (Etapa 3: 3.1 `ejecutar_codigo` y 3.2 recetas).

## Propuestas pendientes (no están en la hoja de ruta)

- Deshacer propio para las superficies que Ctrl+Z no quita: TIN (1.7.0), superficies de corredor (1.12.0) y de volumen (1.14.0).
- El caso "sin `frecuencia`" de VALIDACION_111: lo corta antes la validación del puente (Pydantic), no el plugin.
  Decidir si se cambia el guion.
