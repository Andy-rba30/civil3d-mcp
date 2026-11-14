# Prompt de validación de la 1.17.2 (civil3d-mcp) para el agente local: estilo con las curvas pedidas

Copia este texto tal cual en el agente que tiene conectado el MCP `civil3d`. Requisitos previos:

- Civil 3D 2027 abierto con **una copia nueva** de `C:\Proyectos\dibujos_prueba\PRUEBA_EVALUACION_BASE.dwg` (dibujo
  limpio, con la plantilla métrica), guardada en disco como `C:\Proyectos\dibujos_prueba\PRUEBA_1172.dwg`. Nunca el
  original.
- El plugin instalado desde la **rama integrada de la ronda B** (la que indique el dueño; esta mejora, rama
  `tarea/2.12-curvas-estilo-tin`, se integra en ella). Puente y cliente MCP reiniciados después de instalar.
- El archivo de puntos `C:\Proyectos\dibujos_prueba\terreno_corredor.csv` de `VALIDACION_111.md`. Si no está, créalo
  con exactamente estas 10 líneas (9 puntos PENZD de un plano inclinado genérico, cotas de 97,5 a 111,5):

  ```
  # terreno de prueba del corredor, 9 puntos PENZD
  1,900.0,1900.0,97.5,TN
  2,1300.0,1900.0,99.5,TN
  3,1700.0,1900.0,101.5,TN
  4,900.0,2150.0,102.5,TN
  5,1300.0,2150.0,104.5,TN
  6,1700.0,2150.0,106.5,TN
  7,900.0,2400.0,107.5,TN
  8,1300.0,2400.0,109.5,TN
  9,1700.0,2400.0,111.5,TN
  ```

- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.17.2**: `crear_superficie_tin` con `intervalo_curvas_menor`/`intervalo_curvas_mayor` y **sin**
  `estilo` crea la superficie con un estilo del dibujo que **ya** tiene esos intervalos (±0,001) y dibuja esas curvas en
  planta. Si hay varios, elige con una regla fija: primero los que se sabe que dibujan las curvas, luego el propio
  estilo por defecto, luego su misma familia («Background», «Design») y por último el orden alfabético. Con `estilo`, el
  estilo se respeta siempre y solo se avisa. **Ningún estilo del dibujo se modifica ni se crea.** La respuesta lo explica
  en `intervalos_curvas` y en `avisos`. **Los pasos 1 y 2 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección
  1.17.2): `GetSettings<SettingsSurface>().Styles` (`SurfaceStyleId`, `Surface`), `SurfaceStyle.GetDisplayStylePlan`
  con `SurfaceDisplayStyleType.MinorContour`/`MajorContour` → `DisplayStyle.Visible`, y
  `TinSurface.Create(string, ObjectId styleId)`, que nunca se había ejecutado.

---

Eres el agente de validación de la versión 1.17.2 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`). Ejecuta los pasos en orden, anota la respuesta **literal** (JSON completo o, si es muy largo, las primeras 40
líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada, y **no cambies ningún estilo** del dibujo a mano: si algo no
  funciona, es un hallazgo.
- Abreviatura: `CSV` = `"C:\\Proyectos\\dibujos_prueba\\terreno_corredor.csv"`.

## Pasos

0. **Comprobar la instalación.** `ping` → `version` debe ser `1.17.2` si se instaló la rama integrada de la ronda B
   (o una versión superior, si el dueño indicó otra rama). `listar_superficies` → lista vacía (el dibujo base no tiene
   superficies). Mira la carpeta `C:\Proyectos\dibujos_prueba\backups\` y anota cuántos archivos tiene. Si `ping` no
   responde, para aquí.

1. **Simulación con curvas cada 1 y 5 m, sin estilo (decisivo).** `crear_superficie_tin` con
   `args={"nombre":"Terreno","archivo":CSV,"intervalo_curvas_menor":1,"intervalo_curvas_mayor":5,"simular":true,"pedido":"crea la superficie Terreno con curvas cada 1 y 5 m"}`.
   Esperado:
   - `simulado: true`; `despues.estilo: "Contours 1m and 5m (Background)"`; `despues.puntos: 9`.
   - `despues.intervalos_curvas`: `pedido_menor: 1`, `pedido_mayor: 5`, `aplicados: true`, `eleccion: "automatica"`,
     `estilo: "Contours 1m and 5m (Background)"`, `estilo_menor: 1`, `estilo_mayor: 5`, `curvas_visibles: true`,
     `tolerancia: 0.001`, y `otros_que_coinciden` **incluye** `"Contours 1m and 5m (Design)"` (puede traer más, por
     ejemplo `"Contours and Triangles"`, si también tiene 1 y 5). Anota la lista completa de `otros_que_coinciden` y de
     `descartados_sin_curvas_visibles`.
   - En `avisos`, un texto que empieza por `Intervalos de curvas pedidos (menor 1 y mayor 5) aplicados sin 'estilo': se
     usa el estilo 'Contours 1m and 5m (Background)'` y contiene `de la misma familia («Background») que el estilo por
     defecto del dibujo 'Contours 5m and 25m (Background)'` y `No se modificó ni se creó ningún estilo`.
   - Sin archivo nuevo en `backups\` y sin entrada nueva en el menú Deshacer.

   Cómo leerlo: si el aviso dice `no se pudo leer el estilo por defecto del dibujo`, falló la lectura de
   `SettingsSurface.Styles`; si `curvas_visibles` sale `null` y el aviso dice `compruébalo en pantalla`, falló
   `GetDisplayStylePlan`. En los dos casos la herramienta sigue funcionando: anota el texto literal y ejecuta
   `leer_historial` para copiar las líneas que empiezan por `crear_superficie_tin:`. Si `aplicados` es `false` o el
   estilo es otro, para aquí.

2. **Creación real (decisivo).** El mismo `crear_superficie_tin` sin `simular`:
   `args={"nombre":"Terreno","archivo":CSV,"intervalo_curvas_menor":1,"intervalo_curvas_mayor":5,"pedido":"crea la superficie Terreno con curvas cada 1 y 5 m"}`.
   Esperado: `ok: true`, `simulado: false`, `despues.estilo: "Contours 1m and 5m (Background)"` (releído de la
   superficie), `despues.puntos: 9`, `despues.triangulos: 8`, `copia.estado: "terminada"`, y
   `datos.intervalos_curvas` igual que en el paso 1 (`aplicados: true`, `eleccion: "automatica"`, `estilo_menor: 1`,
   `estilo_mayor: 5`). En `backups\` aparece **un** archivo nuevo `PRUEBA_1172_<fecha>_crear_superficie_tin.dwg` (o se
   reutiliza la copia si el dibujo no cambió). El menú Deshacer muestra **una** entrada `Executefunction` nueva. Si
   responde `El dibujo no refleja el cambio pedido en 'estilo'`, copia el mensaje entero: es un hallazgo de
   `TinSurface.Create(string, ObjectId)`.

3. **Comprobar el estilo y las curvas (Jose, en pantalla).** `listar_superficies` → `Terreno`, tipo TIN (esta
   herramienta no devuelve el estilo). En Civil 3D: Espacio de herramientas → Prospector → Superficies → `Terreno` →
   botón derecho → Propiedades de superficie → Información → **Estilo de superficie** debe ser
   `Contours 1m and 5m (Background)`. Con `ZOOM` → `Extensión`, en planta deben verse curvas de nivel **cada 1 m**
   (de 98 a 111) y curvas mayores **cada 5 m** (100, 105 y 110), con las mayores más marcadas. Pide a Jose que lo
   confirme y anota lo que dice. Si no se ven curvas, anota qué se ve (borde, triángulos, nada).

4. **Estilo dado que no coincide: se respeta.** `crear_superficie_tin` con
   `args={"nombre":"Terreno 2","archivo":CSV,"estilo":"Contours 5m and 25m (Background)","intervalo_curvas_menor":1,"intervalo_curvas_mayor":5}`.
   Esperado: `ok: true`, `despues.estilo: "Contours 5m and 25m (Background)"` (el estilo **no** se cambia),
   `datos.intervalos_curvas`: `aplicados: false`, `eleccion: "indicada"`, `estilo_menor: 5`, `estilo_mayor: 25`. En
   `avisos`, un texto que contiene `Intervalos de curvas pedidos (menor 1 y mayor 5) sin aplicar`,
   `El estilo 'Contours 5m and 25m (Background)' tiene menor 5 y mayor 25`,
   `Estilos del dibujo con esos intervalos: 'Contours 1m and 5m (Background)', 'Contours 1m and 5m (Design)'` (puede
   seguir la lista) y `El estilo indicado se respeta y no se cambia`. En el Prospector, `Terreno 2` tiene el estilo
   `Contours 5m and 25m (Background)` y `Terreno` **sigue** con `Contours 1m and 5m (Background)`.

5. **Intervalos que ningún estilo tiene.** `crear_superficie_tin` con
   `args={"nombre":"Terreno 3","archivo":CSV,"intervalo_curvas_menor":0.37,"intervalo_curvas_mayor":3.7,"simular":true}`.
   Esperado: `simulado: true`, `despues.estilo: "(estilo por defecto del dibujo)"`, `despues.intervalos_curvas`:
   `aplicados: false`, `eleccion: "por_defecto"`, `estilo: "Contours 5m and 25m (Background)"`, `estilo_menor: 5`,
   `estilo_mayor: 25`, `otros_que_coinciden: []`. En `avisos`, un texto que contiene
   `Intervalos de curvas pedidos (menor 0.37 y mayor 3.7) sin aplicar` y
   `Ningún estilo de superficie del dibujo tiene esos intervalos con las curvas visibles en planta (tolerancia 0.001)`.
   Sin archivo nuevo en `backups\` y sin entrada nueva en el menú Deshacer.

6. **Ningún estilo quedó cambiado.** Repite la simulación del paso 1 con otro nombre:
   `args={"nombre":"Terreno 3","archivo":CSV,"intervalo_curvas_menor":1,"intervalo_curvas_mayor":5,"simular":true}`.
   Esperado: la misma elección que en el paso 1 (`Contours 1m and 5m (Background)`, `estilo_menor: 1`,
   `estilo_mayor: 5`, las mismas listas `otros_que_coinciden` y `descartados_sin_curvas_visibles`). Luego
   `leer_historial`: copia las líneas que empiezan por `crear_superficie_tin:` (lo normal es que no haya ninguna con
   `lectura parcial` ni con `no se pudo leer el estilo de superficie por defecto`).

## Informe

**Guarda el informe como archivo** (no basta con mostrarlo en el chat) en
`herramientas-dev/informes/informe_validacion_1172_<AAAAMMDD>.md`, con: versión de Civil 3D, rama instalada y
`version` de `ping`, dibujo usado (solo el nombre de la copia), una tabla paso / resultado (OK, FALLO) / respuesta
literal / tiempo, lo que Jose vio en pantalla en el paso 3, las listas `otros_que_coinciden` y
`descartados_sin_curvas_visibles` del paso 1 y una lista final de los miembros de la sección 1.17.2 de
`miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D 2027`, con la fecha. Si el paso 1 o el 2
fallan, incluye el JSON completo de la respuesta y las líneas de `leer_historial`.
