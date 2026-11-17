# Prompt de validación de la 1.20.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Dibujo nuevo.** Civil 3D 2027 (en español): **Nuevo** con la plantilla métrica que propone por defecto (anota su
  nombre) y guárdalo **antes de empezar** como `C:\Proyectos\dibujos_prueba\PRUEBA_120.dwg` (las copias van a
  `C:\Proyectos\dibujos_prueba\backups\` y el registro a `C:\Proyectos\dibujos_prueba\mcp_log.jsonl`). Si ya existe,
  usa `PRUEBA_120_2.dwg`. Nunca un dibujo real.
- **Puntos del terreno**: `C:\Proyectos\dibujos_prueba\terreno_corredor.csv`, el de los requisitos de
  `VALIDACION_111.md` (9 puntos PENZD de un plano inclinado que cubre x 900..1700, y 1900..2400). Si no está en la PC,
  créalo con las 10 líneas que da ese prompt.
- **Plugin y puente 1.20.0** desde la rama `tarea/6.5-peralte` (o la rama conjunta de la ronda que indique el PR):
  **61** herramientas, **57** del plugin (las 55 de antes más `listar_peralte` y `aplicar_peralte`) y **4** del puente
  (`buscar_api`, `guardar_receta`, `ejecutar_receta` y `verificar_norma`).
- `memoria-firma` al día (`git pull`) en `C:\Proyectos\memoria-firma`, con `MEMORIA_FIRMA_DIR` apuntando ahí (variable
  de usuario) y la base `C:\Proyectos\memoria-firma\normas\tablas\dg2018.sqlite`.
- `ARBA_MCP_CODIGO` no hace falta: esta validación no usa `ejecutar_codigo`.
- Reparto de papeles: el agente local **instala y verifica**; el código y este prompt los mantiene la sesión de
  desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.20.0**, el peralte del eje desde el agente, sin el archivo XML de criterios de diseño ni el
  asistente «Calcular peralte» de Civil 3D:
  1. `listar_peralte` (plugin, lectura): las curvas de peralte del eje y sus progresivas críticas (progresiva, tipo,
     región, pendientes por segmento) y, en `enumerados`, **los nombres reales** que usa esta versión de Civil 3D para
     los tipos de progresiva crítica, las regiones y los segmentos. El catálogo de la API no trae esos nombres: **los
     da esta validación** (paso 2).
  2. `aplicar_peralte` (plugin, escritura): pone el peralte en las curvas circulares de un eje **sin peralte** con los
     valores que se le pasan (`curvas`: por PI, progresivas críticas con tipo, región y pendientes en %). Comprobación
     previa, copia, **una** entrada de Deshacer, relectura y verificación. Si el eje ya tiene peralte, error sin tocar
     nada (no lo borra).
  3. En el puente, `aplicar_peralte` con `velocidad_diseno` y los datos del proyecto: calcula el peralte con la
     DG-2018. En esta ronda **no escribe todavía**: la correspondencia entre los conceptos de la norma y los nombres de
     Civil 3D se rellena con lo que salga del paso 2 (paso 10).
  **Los pasos 0, 2, 4, 5, 7, 9 y 10 son los decisivos.**
- Miembros de la API que esta validación decide: los de la sección 1.20.0 de
  `herramientas-dev/miembros_por_verificar_civil3d.md` (`SuperelevationCurves`, `AddUserDefinedCurve`,
  `CriticalStations`, `Add`, `GetCriticalStationAt`, `GetSlope`/`SetSlope` y los enumerados del peralte).

---

Eres el agente de validación de la versión 1.20.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre un **dibujo nuevo de prueba**. Usa las herramientas MCP **por su nombre** (las del plugin con los
argumentos dentro de `args`; `verificar_norma` con los argumentos directos), salvo en los pasos que indican PowerShell o
la interfaz de Civil 3D. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON completo o, si es muy largo,
las primeras 80 líneas; de `listar_peralte`, siempre `enumerados` completo) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` solo para lo que dicen los pasos 1 y 7, con el texto exacto. Si algo no funciona, es un hallazgo:
  no lo arregles con comandos.
- Los valores de peralte de los pasos 3 a 9 son **de ejemplo** (6 % de peralte, 2 % de bombeo, transición de 30 m con
  el 60 % en la tangente): solo prueban que el plugin escribe y lee bien; **no son de la norma**.
- Para contar las líneas de `mcp_log.jsonl`, en PowerShell: `(Get-Content C:\Proyectos\dibujos_prueba\mcp_log.jsonl).Count`.
  Para contar las copias: `(Get-ChildItem C:\Proyectos\dibujos_prueba\backups).Count`.
- No pegues en el informe rutas con nombres de obra.

## Pasos

0. **Versión e instalación (decisivo).** Si `ping` ya da `1.20.0`, no reinstales. Si no, con Civil 3D cerrado, en
   PowerShell desde la raíz del repo en la rama del PR: `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`;
   en `PuenteMcp`, `uv sync`; reinicia el puente y el cliente MCP (las instrucciones del agente cambian: sección 2b y
   sección 3) y abre Civil 3D con `PRUEBA_120.dwg`. Después:
   - `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.20.0"`, `"hay_dibujo": true`.
   - La lista de herramientas: **61**. Están `listar_peralte` (parámetro `alineamiento`, obligatorio) y
     `aplicar_peralte`; en `aplicar_peralte`, dentro de `args`, `alineamiento` (el único obligatorio), `curvas`,
     `simular`, `pedido` y además `velocidad_diseno`, `clasificacion`, `orografia`, `ubicacion`, `peralte_maximo` y
     `pavimentada`; la descripción termina con el párrafo «CON LA NORMA DG-2018 (puente, desde 1.20.0)...».
     `crear_alineamiento_desde_polilinea` sigue con sus seis parámetros de la norma (1.19.0) y `radio` no obligatorio.
   - `leer_variable` con `args={"nombre":"INSUNITS"}` → `"valor": "6"` (metros). Si no es `"6"`, anótalo y para aquí.
   Si la versión no es exactamente `1.20.0` o faltan las dos herramientas, **para aquí**.

1. **Preparar el dibujo** (no decisivo; cada creación deja su entrada en Deshacer, es normal).
   - `ejecutar_comando` con `args={"comando":"_.-LAYER _M EJE_PERALTE  ","timeout_s":20,"pedido":"crea la capa EJE_PERALTE"}`
     y con `args={"comando":"_.PLINE 1000,2000 1300,2000 1300,2300 1600,2300  ","timeout_s":20,"pedido":"dibuja la polilínea del eje"}`
     (**dos espacios** al final de cada texto): los dos → `"result": "terminado"`.
   - `crear_superficie_tin` con `args={"nombre":"Terreno","archivo":"C:\\Proyectos\\dibujos_prueba\\terreno_corredor.csv","pedido":"crea la superficie Terreno"}`
     → `puntos: 9`.
   - `crear_alineamiento_desde_polilinea` con `args={"nombre":"Eje Peralte","capa":"EJE_PERALTE","radio":80,"pedido":"crea el eje Eje Peralte con radio 80"}`.
   - `listar_pis` con `args={"alineamiento":"Eje Peralte"}` → `numero_curvas: 2`; PI 1 con `radio: 80`,
     `giro: "izquierda"`, `progresiva_pc: 220`, `progresiva_pt: 345.6637`; PI 2 con `radio: 80`, `giro: "derecha"`,
     `progresiva_pc: 485.6637`, `progresiva_pt: 611.3274`; `fin.progresiva: 831.3274`. Si los PC y PT no son esos,
     **para aquí**: las progresivas de los pasos siguientes salen de ellos.

2. **`listar_peralte` en un eje recién creado (decisivo).** `listar_peralte` con
   `args={"alineamiento":"Eje Peralte","pedido":"muestra el peralte del Eje Peralte"}`. Anota y **pega completos**:
   - `tiene_peralte`, `numero_curvas`, `numero_estaciones`, `curvas` y `estaciones_del_eje`. Lo esperado es
     `tiene_peralte: false` y listas vacías; si un eje recién creado ya trae progresivas críticas (por ejemplo, de
     inicio y fin del eje), anótalas: es un hallazgo y `aplicar_peralte` lo rechazará (paso 3 lo dirá).
   - `tipo_peralte`, `usa_archivo_criterios`, `archivo_criterios`, `escala_pendientes` y `avisos`.
   - **`enumerados` completo**: `tipo_estacion`, `region`, `region_transicion`, `segmento` y `tipo_peralte_eje`. Es el
     dato que falta en el catálogo: **sin él no se puede seguir**. Si `enumerados` no sale o alguna lista está vacía,
     **para aquí**.
   - Comprueba en `mcp_log.jsonl` que hay una línea de `listar_peralte` y en el menú Deshacer que **no** hay entrada
     nueva.

3. **Elegir los nombres y simular (decisivo para la correspondencia).** Con las listas del paso 2, elige el nombre de
   Civil 3D de cada concepto y **anota la tabla en el informe** (concepto → nombre elegido → por qué). Si en Civil 3D
   tienes a mano el editor tabular de peralte (selecciona un eje → cinta contextual → grupo de modificar → desplegable
   de peralte → editor tabular), los nombres de sus filas te ayudan a decidir. Si dudas entre dos nombres, anota los
   dos y usa uno.

   | Concepto (norma) | Qué es | Lista de `enumerados` |
   |---|---|---|
   | `fin_bombeo_normal` | antes de la curva, la última sección con el bombeo normal (los dos carriles en −2 %) | `tipo_estacion` |
   | `bombeo_nivelado` | el carril exterior llega a 0 % (sección «a nivel» por ese lado) | `tipo_estacion` |
   | `bombeo_invertido` | el carril exterior llega a +2 % (la sección queda en un solo plano) | `tipo_estacion` |
   | `inicio_peralte_total` | empieza el peralte total (±6 %) | `tipo_estacion` |
   | `fin_peralte_total` | acaba el peralte total | `tipo_estacion` |
   | `inicio_bombeo_normal` | después de la curva, la primera sección con el bombeo normal de nuevo | `tipo_estacion` |
   | `entrada` | la transición antes de la curva (de bombeo a peralte) | `region` |
   | `salida` | la transición después de la curva (de peralte a bombeo) | `region` |
   | `carril_izquierdo` | el carril a la izquierda del eje en una vía de dos carriles sin mediana | `segmento` |
   | `carril_derecho` | el carril a la derecha del eje | `segmento` |

   Después, cuenta las líneas de `mcp_log.jsonl` (N3) y las copias de `backups\` (C3) y llama a `aplicar_peralte` con
   `simular: true` y estas `curvas` (sustituye cada `<concepto>` por el nombre elegido; pendientes en %, valores **de
   ejemplo**). La curva del PI 1 gira a la **izquierda**: su carril exterior es el **derecho** (sube) y el interior el
   izquierdo (baja). La del PI 2 gira a la **derecha**: al revés.

   ```json
   [{"pi": 1, "estaciones": [
      {"progresiva": 192, "tipo": "<fin_bombeo_normal>", "region": "<entrada>", "pendientes": {"<carril_izquierdo>": -2, "<carril_derecho>": -2}},
      {"progresiva": 202, "tipo": "<bombeo_nivelado>", "region": "<entrada>", "pendientes": {"<carril_izquierdo>": -2, "<carril_derecho>": 0}},
      {"progresiva": 212, "tipo": "<bombeo_invertido>", "region": "<entrada>", "pendientes": {"<carril_izquierdo>": -2, "<carril_derecho>": 2}},
      {"progresiva": 232, "tipo": "<inicio_peralte_total>", "region": "<entrada>", "pendientes": {"<carril_izquierdo>": -6, "<carril_derecho>": 6}},
      {"progresiva": 333.6637, "tipo": "<fin_peralte_total>", "region": "<salida>", "pendientes": {"<carril_izquierdo>": -6, "<carril_derecho>": 6}},
      {"progresiva": 353.6637, "tipo": "<bombeo_invertido>", "region": "<salida>", "pendientes": {"<carril_izquierdo>": -2, "<carril_derecho>": 2}},
      {"progresiva": 363.6637, "tipo": "<bombeo_nivelado>", "region": "<salida>", "pendientes": {"<carril_izquierdo>": -2, "<carril_derecho>": 0}},
      {"progresiva": 373.6637, "tipo": "<inicio_bombeo_normal>", "region": "<salida>", "pendientes": {"<carril_izquierdo>": -2, "<carril_derecho>": -2}}]},
    {"pi": 2, "estaciones": [
      {"progresiva": 457.6637, "tipo": "<fin_bombeo_normal>", "region": "<entrada>", "pendientes": {"<carril_izquierdo>": -2, "<carril_derecho>": -2}},
      {"progresiva": 467.6637, "tipo": "<bombeo_nivelado>", "region": "<entrada>", "pendientes": {"<carril_izquierdo>": 0, "<carril_derecho>": -2}},
      {"progresiva": 477.6637, "tipo": "<bombeo_invertido>", "region": "<entrada>", "pendientes": {"<carril_izquierdo>": 2, "<carril_derecho>": -2}},
      {"progresiva": 497.6637, "tipo": "<inicio_peralte_total>", "region": "<entrada>", "pendientes": {"<carril_izquierdo>": 6, "<carril_derecho>": -6}},
      {"progresiva": 599.3274, "tipo": "<fin_peralte_total>", "region": "<salida>", "pendientes": {"<carril_izquierdo>": 6, "<carril_derecho>": -6}},
      {"progresiva": 619.3274, "tipo": "<bombeo_invertido>", "region": "<salida>", "pendientes": {"<carril_izquierdo>": 2, "<carril_derecho>": -2}},
      {"progresiva": 629.3274, "tipo": "<bombeo_nivelado>", "region": "<salida>", "pendientes": {"<carril_izquierdo>": 0, "<carril_derecho>": -2}},
      {"progresiva": 639.3274, "tipo": "<inicio_bombeo_normal>", "region": "<salida>", "pendientes": {"<carril_izquierdo>": -2, "<carril_derecho>": -2}}]}]
   ```

   (Cómo salen: bombeo nivelado de entrada = PC − 0,6 · 30 = PC − 18; fin del bombeo normal = 10 m antes; bombeo
   invertido = 10 m después; inicio del peralte total = bombeo nivelado + 30. A la salida, simétrico respecto al PT.)

   Llamada: `aplicar_peralte` con `args={"alineamiento":"Eje Peralte","simular":true,"curvas":"<la lista de arriba en una línea>","pedido":"simula el peralte de ejemplo del Eje Peralte"}`.
   Esperado: `simulado: true`; `accion: "Aplicar peralte en el eje 'Eje Peralte': 2 curvas de peralte (PI 1, 2) con 16
   progresivas críticas"`; `antes` con `tiene_peralte: false`; `despues` con `tiene_peralte: true`, `numero_curvas: 2`,
   `numero_estaciones: 16` y `curvas` con el PI 1 (`radio: 80`, `giro: "izquierda"`, `progresiva_pc: 220`) y el PI 2;
   `avisos` vacío o `null`. **N3 + 1** líneas en `mcp_log.jsonl`, **C3** copias y ninguna entrada en Deshacer. Si
   responde que un nombre no existe («'X' no es un tipo de progresiva crítica de Civil 3D ... Valores: ...»), corrige
   la elección con esa lista y repite. Si responde «ya tiene peralte», **para aquí**.

4. **Escritura real (decisivo).** Cuenta (N4, C4). La misma llamada **sin** `simular` y con
   `"pedido":"aplica el peralte de ejemplo al Eje Peralte"`. Esperado:
   - `simulado: false`, `copia.ruta` con una copia en `backups\` (o reutilizada), `despues` con `tiene_peralte: true` y
     `numero_curvas: 2`, `curvas` con los nombres que les puso Civil 3D y sus estaciones.
   - `datos.estaciones_del_eje` (anótalo), `datos.escala_pendientes` y `datos.deshacer`.
   - `avisos`: **anótalos todos**. En especial, si sale «El eje tiene progresivas críticas que no se pidieron (Civil 3D
     pudo crearlas al añadir la curva de peralte): ...» (lo que hace `AddUserDefinedCurve`) o
     «Alignment.SuperelevationCriticalStations tiene N progresivas críticas y las curvas de peralte suman M».
   - **N4 + 1** líneas en `mcp_log.jsonl` (una sola, la de la escritura; la comprobación previa no deja otra) y **una**
     entrada nueva en el menú Deshacer (`Executefunction` o `EXECUTEFUNCTION`). No deshagas nada todavía.
   Si responde «El dibujo no refleja el peralte pedido ...», pega el mensaje entero (dice qué progresiva, tipo o
   pendiente no cuadra) y sigue en el paso 5 para ver qué quedó.

5. **Lo escrito, leído por el plugin y por Civil 3D (decisivo: escala de las pendientes).**
   a. `listar_peralte` con `args={"alineamiento":"Eje Peralte"}`. Pega `curvas` y `estaciones_del_eje` completos.
      Esperado: 2 curvas; en cada una, las 8 progresivas pedidas con su `tipo`, su `region` (el texto de
      `TransitionRegionType`: anota si coincide con el nombre de región elegido) y en `pendientes` los dos segmentos
      con los valores pedidos (−2, 0, 2, ±6). Anota **cualquier progresiva que no se pidió** y si
      `estaciones_del_eje` tiene las mismas 16 progresivas que las curvas, otras o ninguna (**son dos colecciones
      distintas**: `Alignment.SuperelevationCriticalStations` y `CriticalStations` de cada curva).
   b. En Civil 3D, abre el **editor tabular de peralte** de `Eje Peralte` (selecciona el eje → cinta contextual del
      alineamiento → desplegable de peralte → editor tabular). Anota, para las progresivas `0+232.00` y `0+497.66`, lo
      que muestra cada columna de carril. Esperado: **6,00 %** y **−6,00 %** (no 0,06 %): eso confirma que la API usa
      tanto por uno. Si muestra 0,06 % o 600 %, es un hallazgo **decisivo** (la escala `FactorApi` de
      `ArbaMcp.Nucleo/Peralte.cs` está mal). Anota también si el editor marca algún aviso o solape. Cierra el editor
      **sin cambiar nada**.

6. **Un eje con peralte se rechaza sin tocar nada.** Cuenta (N6, C6). Repite la llamada del paso 4 (sin `simular`).
   Esperado: `ok: false` con el error «El eje 'Eje Peralte' ya tiene peralte (2 curvas de peralte, 16 progresivas
   críticas): aplicar_peralte solo escribe en un eje sin peralte y no borra el que hay. Míralo con listar_peralte.» (el
   número de progresivas puede ser mayor si Civil 3D añadió alguna), con `id_log`; **N6 + 1** líneas, **C6** copias y
   **ninguna** entrada nueva en Deshacer (la comprobación previa lo detecta antes de abrir el comando).

7. **`_.UNDO 1` sin zoom (decisivo).** Entre el paso 4 y este no hagas zoom, encuadre ni regeneración. Mira la entrada
   de arriba del menú Deshacer: debe ser la de `aplicar_peralte` (`Executefunction`). Entonces `ejecutar_comando` con
   `args={"comando":"_.UNDO 1 ","timeout_s":20,"pedido":"deshace el peralte de ejemplo"}` y después `listar_peralte`
   con `args={"alineamiento":"Eje Peralte"}`. Anota `tiene_peralte`, `numero_curvas`, `numero_estaciones` y
   `estaciones_del_eje`.
   - Si `tiene_peralte: false`: `_.UNDO 1` **quita el peralte** (no hace falta `deshacer_peralte`). Vuelve a aplicarlo:
     la llamada del paso 4 otra vez → `simulado: false`, 2 curvas (también comprueba que un eje sin peralte se puede
     escribir de nuevo).
   - Si sigue con peralte: `_.UNDO 1` **no lo quita** (como los objetivos de corredor). Anota si quitó algo (por
     ejemplo, las pendientes pero no las curvas). No lo arregles; sigue con el peralte que haya.

8. **Corredor con carriles que usan el peralte.** Crea **a mano** `Ensamblaje Peralte` como en los requisitos de
   `VALIDACION_111.md` (pasos 1 a 5 de sus clics: `_CreateAssembly`, nombre `Ensamblaje Peralte`, línea base en
   `1000,1700`, paletas con `Ctrl+3` y Propiedades con `Ctrl+1`), pero con **dos `LaneSuperelevationAOR`** de la
   pestaña **Carriles** (*Lanes*) de los subensamblajes métricos: uno con **Lado** = **Derecha** y otro con **Lado** =
   **Izquierda**, los dos enganchados a la marca del ensamblaje. Sin taludes. Antes de hacer clic, en Propiedades →
   **Parámetros**, anota el parámetro de peralte del carril (en inglés *Use Superelevation* o parecido) y déjalo en el
   segmento del mismo lado que elegiste en el paso 3 para ese carril (izquierdo para el izquierdo, derecho para el
   derecho). Anota los valores que ofrece la lista y los que dejaste. Luego:
   - `listar_ensamblajes` → `Ensamblaje Peralte` con dos subensamblajes `LaneSuperelevationAOR` (anota sus nombres).
   - `crear_rasante` con `args={"alineamiento":"Eje Peralte","nombre":"Rasante Peralte","pvis":"[{\"progresiva\":0,\"cota\":100},{\"progresiva\":400,\"cota\":110,\"longitud_curva\":120},{\"progresiva\":831.33,\"cota\":105}]","pedido":"crea la rasante del Eje Peralte"}`.
   - `crear_corredor` con `args={"nombre":"Corredor Peralte","alineamiento":"Eje Peralte","perfil":"Rasante Peralte","ensamblaje":"Ensamblaje Peralte","frecuencia":5,"pedido":"crea el corredor con los carriles con peralte"}`
     y después `estado_corredor` → `esta_desactualizado: false`.
   - `crear_superficies_corredor` con `args={"corredor":"Corredor Peralte","pedido":"crea las superficies Top y Datum del corredor"}`
     → `Corredor Peralte - Top` y `Corredor Peralte - Datum`. Si falla por los códigos de los vínculos, anota el error
     y en el paso 9 usa solo la vista de sección.

9. **La sección en una curva muestra el peralte (decisivo: signo de las pendientes).** Si en el paso 7 el peralte se
   quitó y no se volvió a aplicar, **para aquí**. Para cada progresiva de la tabla, `pk_a_punto` con
   `args={"alineamiento":"Eje Peralte","pk":<pk>,"desplazamiento":<d>}` para `d` = −2, 0 y 2 (el negativo es la
   izquierda: compruébalo en el pk 100, donde el eje va hacia el este y la izquierda es el norte, `y` mayor), y
   `cota_superficie` con `args={"superficie":"Corredor Peralte - Top","x":<x>,"y":<y>}` en cada punto. Calcula
   `izquierda = (z(−2) − z(0)) / 2 × 100` y `derecha = (z(2) − z(0)) / 2 × 100` (en %, del eje hacia el borde).

   | pk | Dónde | izquierda esperada | derecha esperada |
   |---|---|---|---|
   | 100 | tangente, bombeo normal | −2 | −2 |
   | 280 | peralte total de la curva del PI 1 (gira a la izquierda) | −6 | +6 |
   | 550 | peralte total de la curva del PI 2 (gira a la derecha) | +6 | −6 |

   Tolerancia ±0,05 %. **El borde interior de cada curva debe quedar más bajo** (el izquierdo en la del PI 1, el
   derecho en la del PI 2). Si los signos salen al revés, es un hallazgo **decisivo**: anótalo literal. Después, en
   Civil 3D, mira una vista de sección del corredor en el pk 280 (por ejemplo con `crear_secciones` sobre `Eje Peralte`
   con `args={"alineamiento":"Eje Peralte","intervalo":10,"ancho_izquierdo":10,"ancho_derecho":10,"inicio":270,"fin":290,"pedido":"crea las secciones de los pk 270 a 290"}`)
   y describe la inclinación de los carriles.

10. **Peralte con la norma en el puente (decisivo).** Sobre un eje nuevo sin peralte:
    a. `crear_alineamiento_desde_polilinea` con
       `args={"nombre":"Eje Norma 120","capa":"EJE_PERALTE","radio":80,"pedido":"crea un segundo eje para el peralte de la norma"}`.
       Cuenta las líneas de `mcp_log.jsonl` (N10).
    b. `aplicar_peralte` con `args={"alineamiento":"Eje Norma 120","simular":true,"velocidad_diseno":60}` → `ok: false`,
       `error` que empieza por «Faltan datos del proyecto para calcular el peralte con la DG-2018:» y `faltan` con
       `clasificacion`, `orografia` y los demás que falten, con sus valores válidos. **N10** líneas (no llamó a Civil 3D).
    c. `aplicar_peralte` con `curvas` (cualquier texto) **y** `velocidad_diseno` → «Pasa 'curvas' ... o
       'velocidad_diseno' ..., no los dos.», sin llamar a Civil 3D.
    d. `aplicar_peralte` con
       `args={"alineamiento":"Eje Norma 120","simular":true,"velocidad_diseno":60,"clasificacion":"Carretera de tercera clase","orografia":"Ondulado","ubicacion":"rural","pavimentada":false,"peralte_maximo":8,"pedido":"simula el peralte de la norma"}`
       (más los datos del peralte que pida el `faltan`, si pide alguno: anótalos con sus valores válidos y usa los que
       el dueño te diga; no los inventes). En esta ronda lo esperado es **una** de estas respuestas, sin escribir nada:
       - «La base de la norma todavía no tiene las tablas de peralte ...»: `memoria-firma` no trae aún las tablas de la
         sesión 6.4 o esta rama no tiene sus consultas. Anótalo.
       - `ok: false` con «Falta la correspondencia entre la DG-2018 y los nombres de Civil 3D para ...» y
         `propuesta_norma`: pega `propuesta_norma.curvas` completo (peralte, bombeo, longitudes, proporción, `valores`
         con su referencia y las 8 `estaciones` de cada curva). La correspondencia se rellena con tu tabla del paso 3.
       En los dos casos, **no** debe haber llamada a `aplicar_peralte` en `mcp_log.jsonl` (sí `leer_variable` y
       `listar_pis` en el segundo).

11. **Deshacer y copias.** Abre el menú Deshacer: las entradas nuevas de `aplicar_peralte` son solo las de las
    escrituras reales (paso 4 y, si se repitió, paso 7); **ninguna** de los pasos 2, 3, 5, 6 ni 10. En `backups\`,
    las copias de las escrituras reales (o reutilizadas). No deshagas nada más.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_120_<AAAAMMDD>.md` con: versión de Civil 3D, plantilla y DWG
usados (nombre, sin rutas de obra), commit de `memoria-firma` (`git -C C:\Proyectos\memoria-firma log -1 --format=%h`),
una tabla paso / resultado (OK, FALLO) / respuesta literal / tiempo, y **en secciones aparte**:

1. `enumerados` completo del paso 2.
2. La tabla del paso 3: concepto → nombre elegido → por qué (y las dudas).
3. Lo que mostró el editor tabular en el paso 5b (escala de las pendientes).
4. La comparación de `estaciones_del_eje` con las estaciones de las curvas (pasos 4 y 5a) y las progresivas que Civil
   3D añadió por su cuenta.
5. El resultado del `_.UNDO 1` del paso 7.
6. La tabla de pendientes medidas del paso 9 y los valores del parámetro de peralte de `LaneSuperelevationAOR`.
7. La respuesta del paso 10d (y `propuesta_norma.curvas` si salió).
