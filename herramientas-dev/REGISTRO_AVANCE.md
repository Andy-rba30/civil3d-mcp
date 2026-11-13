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

## Estado actual (13/11/2026, 23:40)

**En `main`**: versión **1.11.0**, validada (1.10.1 y 1.11.0 el 13/11/2026; PR #22 a #25 y #28 fusionados).

**Ronda A LISTA PARA PROBAR en la PC** (aviso enviado al dueño). Todo va en la rama **`tarea/2.7-volumenes`**
(commit `a502066`, versión **1.14.1**, 53 herramientas; pruebas en la nube 349/84/0 errores/21), que contiene:

| Rama | PR (borrador) | Versión | Qué |
|---|---|---|---|
| `tarea/2.5b-superficies-corredor` | #26 | 1.12.0 | `crear_superficies_corredor` (Top y Datum, voladizo, contorno) |
| `tarea/2.6-secciones` | #27 | 1.13.0 | `crear_secciones` (líneas de muestreo y vistas en cuadrícula) |
| `tarea/2.7-volumenes` | #31 | 1.14.0 | `calcular_volumenes` (superficie de volumen TIN y tabla por áreas medias) |
| `tarea/1.14.1-arreglos` | #30 | 1.14.1 | Capa del corredor, buscar_api cp1252, `es_extension_corredor`, punto de inserción junto al eje, validar las `agregar_*` |
| `tarea/2.8-flujo` | #29 | — | Receta 2b "Flujo vial completo" en INSTRUCCIONES_AGENTE y VALIDACION_FLUJO_COMPLETO |

Orden de la prueba en una sola instalación: VALIDACION_112 → 113 → 114 → 1141 → FLUJO_COMPLETO → evaluación
(PROMPT_EVALUACION de memoria-firma + `comparar` + `sincronizar_memoria.ps1`). Dibujo: `PRUEBA_112.dwg`, copia de
`PRUEBA_111.dwg`.

**Si da OK**: fusionar en orden #26, #27, #31, #30 y #29 (o solo #31, que lo contiene todo, cerrando los demás como
incluidos), subir los informes, lanzar la sesión de hallazgos y actualizar la hoja de ruta (Etapa 2 hecha). Después,
la **Ronda B** (Etapa 3: 3.1 `ejecutar_codigo` y 3.2 recetas).
**Si falla**: pasar cada fallo a la sesión de su rama (o a una nueva con el prompt del fallo), corregir en esa rama,
volver a fusionar en `tarea/2.7-volumenes` y repetir solo lo que falló.

## Propuestas pendientes (no están en la hoja de ruta)

- Deshacer propio para la superficie TIN (Ctrl+Z no la quita; informe 1.7.0).
- El caso "sin `frecuencia`" de VALIDACION_111: lo corta antes la validación del puente (Pydantic), no el plugin.
  Decidir si se cambia el guion.
