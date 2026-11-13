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

## Estado actual (13/11/2026, noche)

**En `main`**: versión **1.11.0**, validada (1.10.1 y 1.11.0 el 13/11/2026; PR #22 a #25 fusionados).

**Ronda A en preparación (todavía no probada en la PC):**

| Rama | PR | Versión | Qué | Estado |
|---|---|---|---|---|
| `tarea/2.5b-superficies-corredor` | #26 (borrador) | 1.12.0 | `crear_superficies_corredor`: Top y Datum con voladizo y contorno | Terminada, pruebas en verde, al día con main |
| `tarea/2.6-secciones` | #27 (borrador) | 1.13.0 | `crear_secciones`: líneas de muestreo y vistas en cuadrícula. **Contiene la 1.12.0** | Terminada, pruebas en verde (294/82/0/21) |
| `tarea/2.7-volumenes` | — | 1.14.0 | `calcular_volumenes` (superficie de volumen TIN; tabla por progresivas si la API alcanza) | En marcha (parte de 2.6) |
| `tarea/2.8-flujo` | — | — | Receta "flujo vial completo" en INSTRUCCIONES_AGENTE y VALIDACION_FLUJO_COMPLETO | En marcha (parte de 2.6) |
| `tarea/1.14.1-arreglos` | — | 1.14.1 | Capa del corredor, buscar_api en cp1252, campo `usar_como_exterior`, punto de inserción de la vista de perfil, validar las `agregar_*` | En marcha (parte de 2.6) |

**Siguiente paso**: cuando terminen 2.7, 2.8 y 1.14.1, la sesión de la 2.7 fusiona las otras dos en
`tarea/2.7-volumenes` (versión final 1.14.1). La orquestadora revisa, corre las cuatro pruebas y manda al dueño una
sola prueba con este orden: VALIDACION_112 → 113 → 114 → 1141 → FLUJO_COMPLETO → evaluación (5.2-D y 5.2-PC). Si da
OK, se fusionan los PR #26, #27 y los de 2.7, 1.14.1 y 2.8 en ese orden, se suben los informes y se lanza la sesión
de hallazgos.

**Después**: Ronda B (Etapa 3) y Ronda C (Etapa 4).

## Propuestas pendientes (no están en la hoja de ruta)

- Deshacer propio para la superficie TIN (Ctrl+Z no la quita; informe 1.7.0).
- El caso "sin `frecuencia`" de VALIDACION_111: lo corta antes la validación del puente (Pydantic), no el plugin.
  Decidir si se cambia el guion.
