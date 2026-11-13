# Prompt de validación de la 1.14.1 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- **Se ejecuta después de `VALIDACION_114.md`**, en la misma instalación. Civil 3D 2027 (en español) abierto con **una
  copia** del dibujo `C:\Proyectos\dibujos_prueba\PRUEBA_112.dwg` (la misma copia que usó la 1.14.0 o una nueva, por
  ejemplo `C:\Proyectos\dibujos_prueba\PRUEBA_1141.dwg`; si trabajas directamente sobre `PRUEBA_112.dwg`, anótalo).
  Anota la carpeta del DWG: ahí aparecerán `mcp_log.jsonl` y `backups\`. Ningún dato real.
- **Lo que debe tener el dibujo** (lo dejaron las validaciones 1.10.0, 1.11.0 y 1.12.0; el paso 1 lo comprueba):
  - `Eje 1`: alineamiento de la progresiva `0` a la `831.3274` (polilínea `1000,2000 → 1300,2000 → 1300,2300 →
    1600,2300` con radio 80).
  - `Terreno`: superficie TIN que cubre x 900..1700, y 1900..2400. Puede estar también `Terreno 2`: no es fallo.
  - `Rasante 1` (perfil FG de `Eje 1`), `Ensamblaje 1` y `Corredor 1` (de 0 a 831.3274 cada 10 m, taludes a `Terreno`).
  - Las superficies de corredor `Corredor 1 - Top` y `Corredor 1 - Datum` (las creó la 1.12.0).
  Lo que hayan dejado las validaciones 1.13.0 y 1.14.0 (grupos de líneas de muestreo, vistas de sección, volúmenes) no
  molesta: anótalo en el paso 1. Si falta algo de la lista, **para** y anótalo: esta validación no lo crea.
- El plugin **1.14.1** se instala en el paso 0 desde la rama `tarea/1.14.1-arreglos` (o desde la rama integrada que
  indique el PR, si la orquestadora la juntó con la 1.14.0); el bundle queda con `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`.
  Puente reiniciado con el `main.py` de 1.14.1 (`uv sync` antes) y el cliente MCP reiniciado. **Esta versión no añade
  herramientas**: la lista es de **52** del plugin si es solo esta rama, o **53** si la instalación trae también
  `calcular_volumenes` (1.14.0), más `buscar_api` del puente.
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.14.1** (cinco correcciones, ninguna herramienta nueva):
  1. **Capa del corredor**: `crear_corredor` pone el corredor en la capa de corredores de la configuración del dibujo
     (Espacio de herramientas → Configuración → clic derecho en el nombre del dibujo → Editar configuración de dibujo
     → pestaña Capas de objeto → fila Corredor, que en español puede llamarse «Obra lineal»). Hasta la 1.14.0 quedaba en
     la capa `0` (informe 1.11.0, pregunta 5). Si esa capa no existe o no se puede poner, avisa y el corredor se queda
     en la capa que le pone Civil 3D.
  2. **`buscar_api.py` como comando** escribe en UTF-8 aunque la consola de Windows esté en cp1252 (no se prueba con
     Civil 3D; ver el paso 15, opcional).
  3. **`estado_corredor`**: cada contorno trae el campo nuevo **`es_extension_corredor`** (lee `IsCorridorExtents`); el
     campo antiguo `usar_como_exterior`, que leía lo mismo con un nombre engañoso, **se conserva con el mismo valor**
     (obsoleto).
  4. **Punto de inserción automático**: sin `punto_insercion`, `crear_perfil_terreno_y_vista` coloca la vista a la
     derecha **del eje y de sus vistas de perfil ya existentes**, no de todo el dibujo (informe 1.9.0, paso 2: con
     contenido lejano la vista quedó en x ≈ 602 712); `crear_secciones` hace lo mismo y tiene en cuenta además las vistas
     de sección de los grupos del eje, así que sus vistas quedan a la derecha de la vista de perfil.
  5. **Tres herramientas que nunca se habían ejecutado**: `agregar_superficie_corredor`,
     `agregar_codigo_superficie_corredor` y `agregar_contorno_superficie_corredor` (`AUDITORIA_FLUJO_VIAL.md`). Su
     código no cambia: esta validación las ejecuta de verdad sobre `Corredor 1` con una superficie de prueba aparte,
     `Corredor 1 - Prueba 1141`, **sin tocar** `Corredor 1 - Top` ni `Corredor 1 - Datum`.
  **Los pasos 0, 2, 5, 6, 7, 9, 10, 11 y 12 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.14.1):
  `SettingsObjectLayerType.Corridor` (resuelto por nombre), `Entity.LayerId` sobre un corredor recién creado,
  `Entity.GeometricExtents` sobre vistas de perfil, `CorridorSurfaceCollection.Add`, `CorridorSurface.AddLinkCode`,
  `CorridorSurfaceBoundaryCollection.AddCorridorExtentsBoundary`/`BoundaryNames` por el camino de las tres herramientas
  `agregar_*`, y `IsCorridorExtents` en `es_extension_corredor`.

---

Eres el agente de validación de la versión 1.14.1 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell o clics en Civil 3D. Ejecuta los pasos en orden, anota la respuesta
**literal** (JSON completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- `ejecutar_comando` se usa **solo** donde este prompt lo indica (`_.POINT`, `_.ZOOM` y `_.UNDO`). Si algo no
  funciona, es un hallazgo: no lo arregles con otro comando.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Si ya existe un corredor `Corredor 3`, usa `Corredor 1141` en los pasos 4 y 5; si ya existe la superficie de
  corredor `Corredor 1 - Prueba 1141`, usa `Corredor 1 - Prueba 1141 b` en los pasos 9 a 13; si ya existe el perfil
  `Terreno - 1141` o la vista `Eje 1 - Vista 1141`, añade ` b` a los dos nombres. Anota el cambio.
- Los números esperados son en metros con redondeo a 4 decimales; una diferencia menor de 0,001 es OK. Las listas de
  nombres van ordenadas sin distinguir mayúsculas.
- Anota el menú Deshacer **antes y después de cada escritura real** (pasos 5, 6, 9, 10, 11 y 12): cada una debe añadir
  **una** entrada (`Executefunction`); las simulaciones y los errores a propósito, ninguna.
- Al final hay una lista de **preguntas para el informe**: respóndelas con lo que viste.

## Pasos

0. **Instalar y versión (decisivo).** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama
   `tarea/1.14.1-arreglos` (o la integrada): `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En
   `PuenteMcp`, `uv sync`. Reinicia el puente y el cliente MCP. Abre Civil 3D con la copia del DWG. `ping` con
   `args={"pedido":"comprueba que el plugin responde"}` → `"version": "1.14.1"`, `"hay_dibujo": true`. En PowerShell:
   `curl.exe -s http://127.0.0.1:8765/ping` → `{"ok":true,"servidor":"ArbaMcp","version":"1.14.1"}`. Pide la lista de
   herramientas: **52** del plugin (o **53** con `calcular_volumenes`); anota cuál. Si la versión no es exactamente
   `1.14.1`, para aquí.

1. **Comprobar el dibujo** (solo lecturas, no decisivo).
   - `listar_alineamientos` con `args={"pedido":"lista los alineamientos"}`: `Eje 1` de `0` a `831.3274`.
   - `listar_perfiles` con `args={"alineamiento":"Eje 1"}`: debe estar `Rasante 1` (FG). **Anota la lista completa**
     (los perfiles de terreno que haya, por ejemplo `Terreno - Terreno`).
   - `listar_superficies`: deben estar `Terreno`, `Corredor 1 - Top` y `Corredor 1 - Datum` (las dos últimas como
     superficies de `Corredor 1`). **Anota la lista completa.**
   - `listar_corredores`: `Corredor 1` con una línea base sobre `Eje 1`. Anota si hay otros.
   - `listar_ensamblajes`: `Ensamblaje 1`.
   - `listar_lineas_muestreo` **no** se llama (deja entrada en Deshacer).
   Si falta algo de los requisitos, **para**.

2. **Contornos de `estado_corredor` (decisivo, corrección 3).** `estado_corredor` con
   `args={"corredor":"Corredor 1","pedido":"estado del corredor 1 con sus contornos"}`. Esperado: `superficies` con
   `Corredor 1 - Top` y `Corredor 1 - Datum`; en cada una, `contornos` con **un** elemento (el que creó la 1.12.0) con
   las claves **`nombre`** (`Extensión del corredor`), **`tipo`** (anótalo; lo esperado es `OutsideBoundary`),
   **`es_extension_corredor`** y **`usar_como_exterior`**, y las dos últimas con **el mismo valor** (lo esperado es
   `true`: el contorno se creó con `AddCorridorExtentsBoundary`). **Guarda literal el bloque de cada una de las dos
   superficies** (códigos, contornos, `correccion_voladizo`, `construir`, `codigos_enlace_rotura`): el paso 12 lo
   compara. OK solo si todos los contornos traen `es_extension_corredor` y `usar_como_exterior` con el mismo valor. Si
   falta `es_extension_corredor`, el plugin instalado no es el de 1.14.1.

3. **Punto de partida.** Abre el menú Deshacer de Civil 3D y anota cuántas entradas tiene (o la última). Mira cuántos
   archivos hay en `backups\` junto al DWG. **Capa de corredores de la configuración**: abre Espacio de herramientas →
   Configuración → clic derecho en el nombre del dibujo → Editar configuración de dibujo → pestaña Capas de objeto y
   **anota la capa de la fila Corredor** (en español puede decir «Obra lineal») y si su modificador está en «Ninguno»;
   cierra el cuadro con **Cancelar** (no cambies nada). Si no puedes abrirlo, anótalo y sigue: el paso 4 la dice.

4. **Capa del corredor, simulación (no decisivo).** `crear_corredor` con
   `args={"nombre":"Corredor 3","alineamiento":"Eje 1","perfil":"Rasante 1","ensamblaje":"Ensamblaje 1","frecuencia":20,"inicio":100,"fin":300,"superficie_objetivo":"Terreno","simular":true,"pedido":"simula el corredor de prueba Corredor 3 para ver su capa"}`.
   Esperado: `simulado: true`; `accion: "Crear el corredor 'Corredor 3' sobre el eje 'Eje 1' y la rasante 'Rasante 1'
   con el ensamblaje 'Ensamblaje 1', de 100 a 300 cada 20 m, con 'Terreno' como objetivo de los taludes, y
   reconstruirlo"`; `despues` con `existe: true`, `inicio: 100`, `fin: 300`, las cuatro frecuencias en `20` y
   **`capa`** = la capa del paso 3 (anótala literal; con la plantilla métrica NCS suele ser `C-ROAD-CORR`). Si `capa`
   sale `null`, `avisos` debe explicar por qué; anota el aviso literal:
   - `La capa de corredores de la configuración del dibujo ('X') no existe en el dibujo; el corredor se queda en la
     capa que le pone Civil 3D ...`: la capa configurada no está en el dibujo (no es fallo del plugin).
   - `No se pudo leer la capa de corredores de la configuración del dibujo (El enumerado SettingsObjectLayerType de la
     API de Civil 3D no tiene el valor 'Corridor'. Valores disponibles: ...)`: **hallazgo importante**; anota la lista
     de valores completa.
   Sin copia, sin entrada en Deshacer y sin archivo nuevo en `backups\`.

5. **Capa del corredor, creación real (decisivo, corrección 1).** La misma llamada **sin** `simular` y con `pedido`
   `crea el corredor de prueba Corredor 3 de 100 a 300 cada 20 m`. Esperado: `simulado: false`; `despues.capa` =
   `datos.capa` = `datos.capa_configuracion` = la capa del paso 4; `despues.esta_desactualizado: false`; `copia` con
   `estado: "terminada"`; `restaurar` presente; y en `avisos` **ninguno** que empiece por `El corredor quedó en la capa`
   ni por `No se pudo poner el corredor en la capa`. Anota `ms`, `ms_espera` y `ms_ejecucion`. En Civil 3D, selecciona
   el corredor nuevo (el de 100 a 300) y mira en Propiedades (Ctrl+1) su **Capa**: debe ser la misma. Mira también la
   capa de `Corredor 1`: sigue en la que tenía (lo normal es `0`; esta versión no cambia los corredores existentes).
   `listar_corredores`: `Corredor 1` y `Corredor 3`. El menú Deshacer tiene **una** entrada nueva. OK solo si la capa
   releída es la de la configuración (o, si el paso 4 explicó que no existe, si el corredor se creó igual con el aviso
   y `datos.capa_configuracion: null`). Después, para dejar el dibujo como estaba, `ejecutar_comando` con
   `args={"comando":"_.UNDO 1","timeout_s":30,"pedido":"deshaz el corredor de prueba"}` **una vez** y `listar_corredores`
   → solo `Corredor 1` (como en la 1.11.0, paso 10). Si `Corredor 3` sigue, anótalo y sigue.

6. **Punto de inserción con contenido lejano (decisivo, corrección 4).**
   - `ejecutar_comando` con `args={"comando":"_.POINT 600000,2000","timeout_s":20,"pedido":"dibuja un punto lejano a propósito"}`
     → `"result": "terminado"`. `leer_variable` con `args={"nombre":"EXTMAX"}`: anota el valor; su x debe ser al menos
     `600000` (si no lo es, `ejecutar_comando` con `args={"comando":"_.ZOOM _E"}` y vuelve a leerla; anótalo).
   - `crear_perfil_terreno_y_vista` con
     `args={"alineamiento":"Eje 1","superficie":"Terreno","nombre_perfil":"Terreno - 1141","nombre_vista":"Eje 1 - Vista 1141","simular":true,"pedido":"simula el perfil de terreno y la vista de perfil de prueba sin punto de inserción"}`.
     Esperado: `simulado: true`; `accion` que termina en `y su vista de perfil 'Eje 1 - Vista 1141' a la derecha del
     eje`; `despues.vista.punto_insercion` con **x menor que 10000** (lejos de 600000) y `punto_insercion_automatico:
     true`; en `avisos`, una línea que empieza por `Punto de inserción automático: x = ` seguida de
     `(extremo derecho del eje 'Eje 1')` si el eje no tiene vistas de perfil, o de `(extremo derecho del eje 'Eje 1' y
     de su vista de perfil)` / `... y de sus N vistas de perfil)` si ya tiene (anota el aviso entero). Sin vistas
     previas, x ≈ `1660` e y ≈ `2000` (el eje va de x 1000 a 1600; algo más si sus etiquetas amplían su extensión). Si
     el aviso dice `extremo derecho del dibujo entero` o x es ≈ 600000, es **FALLO**.
   - La misma llamada **sin** `simular` y con `pedido` `crea el perfil de terreno y la vista de perfil de prueba a la
     derecha del eje` → `simulado: false`, `despues.perfil_existe: true`, `despues.vista_existe: true`,
     `despues.vista.punto_insercion` igual que en la simulación (±0,001). Anota `ms`. Luego `ejecutar_comando` con
     `args={"comando":"_.ZOOM _W 900,1800 3200,2800"}` (y, si puedes, `capturar_pantalla`): la vista nueva debe verse a
     la derecha del eje, sin montarse sobre él ni sobre otra vista de perfil del eje.
   OK solo si x es menor que 10000 en la simulación y en la escritura real y el aviso nombra el eje.

7. **Las secciones van a la derecha de la vista de perfil (decisivo).** `crear_secciones` con
   `args={"alineamiento":"Eje 1","nombre":"Muestreo 1141","intervalo":100,"ancho_izquierdo":30,"ancho_derecho":30,"simular":true,"pedido":"simula líneas de muestreo de prueba para ver dónde irían las vistas"}`.
   Esperado: `simulado: true`; en `avisos`, `Punto de inserción automático: x = <X> (extremo derecho del eje 'Eje 1' y
   de ...)` que cuenta **al menos una** vista de perfil (la del paso 6) y, si quedaron grupos con vistas de la 1.13.0 o
   la 1.14.0, también vistas de sección (anota el aviso entero); `despues.punto_insercion.x` **mayor** que el x de la
   vista del paso 6 más `831` (la vista muestra el eje entero) y **menor que 10000**. No se crea nada (simulación).
   Si el grupo `Muestreo 1141` ya existe, usa `Muestreo 1141 b`. OK solo si el punto queda a la derecha de la vista del
   paso 6 y lejos de 600000.

8. **Deshacer la vista de prueba (no decisivo; responde un pendiente).** `ejecutar_comando` con
   `args={"comando":"_.UNDO 1","timeout_s":30,"pedido":"deshaz el perfil y la vista de prueba"}` **una vez** (entre el
   paso 6 y este no hubo ninguna escritura: el paso 7 fue una simulación). Luego `listar_perfiles` con
   `args={"alineamiento":"Eje 1"}` y `_.ZOOM _W 900,1800 3200,2800`: anota si **`Terreno - 1141` desapareció** de la
   lista y si **la vista** desapareció de la pantalla. Hasta ahora no se sabe si `_.UNDO 1` quita el perfil y la vista
   creados por API (con la TIN no pasa; con la rasante y el corredor sí). Si siguen, no repitas el deshacer: anótalo y
   sigue (no molestan a los pasos siguientes). El punto lejano del paso 6 se queda (no molesta).

9. **`agregar_superficie_corredor` (decisivo, corrección 5).** Primero la simulación:
   `args={"corredor":"Corredor 1","nombre":"Corredor 1 - Prueba 1141","codigo_enlace":"Top","simular":true,"pedido":"simula una superficie de corredor de prueba con el código Top"}`.
   Esperado: `simulado: true`; `accion: "Crear la superficie de corredor 'Corredor 1 - Prueba 1141' con el código de
   enlace 'Top'"`; `antes` con `superficies: ["Corredor 1 - Datum", "Corredor 1 - Top"]` y `codigos_enlace: null`;
   `despues` con `superficies: ["Corredor 1 - Datum", "Corredor 1 - Prueba 1141", "Corredor 1 - Top"]` y
   `codigos_enlace: ["Top"]` (si sale el aviso `La API no expone LinkCodes ...`, anótalo). Después la misma llamada
   **sin** `simular` y con `pedido` `crea la superficie de corredor de prueba Corredor 1 - Prueba 1141 con el código Top`
   → `simulado: false`, `mensaje` igual a la `accion`, `despues` **releído** igual al previsto, `cambios` con
   `superficies` y `codigos_enlace`, `copia` con `estado: "terminada"` (puede ser `reutilizada: true`). Anota `ms`,
   `ms_espera` y `ms_ejecucion`. El menú Deshacer tiene **una** entrada nueva. Esta herramienta **no reconstruye**: anota
   si después `listar_corredores` dice `Corredor 1` desactualizado. OK solo si la superficie existe con el código `Top`.

10. **`agregar_codigo_superficie_corredor` (decisivo).**
    - Error a propósito: `args={"corredor":"Corredor 1","superficie":"Corredor 1 - Prueba 1141","codigo":"Datum","tipo":"otro","simular":true}`
      → `ok: false` con `error` que contiene `El parámetro 'tipo' debe ser 'enlace' o 'linea_caracteristica' (o 'punto'
      como alias).`
    - Simulación: `args={"corredor":"Corredor 1","superficie":"Corredor 1 - Prueba 1141","codigo":"Datum","simular":true,"pedido":"simula añadir el código Datum a la superficie de prueba"}`
      → `simulado: true`; `accion: "Añadir el código de enlace 'Datum' a la superficie 'Corredor 1 - Prueba 1141'"`;
      `antes` con `superficie: "Corredor 1 - Prueba 1141"`, `codigos_enlace: ["Top"]` y
      `codigos_lineas_caracteristicas` (anótalo; lo esperado es `[]`); `despues` con `codigos_enlace: ["Datum", "Top"]`.
    - Real: la misma **sin** `simular` y con `pedido` `añade el código Datum a la superficie de prueba` → `simulado:
      false`, `despues.codigos_enlace: ["Datum", "Top"]` releído, `copia` terminada, **una** entrada nueva en Deshacer.
      Anota `ms`.
    OK solo si la escritura real deja `["Datum", "Top"]`.

11. **`agregar_contorno_superficie_corredor` (decisivo).**
    - Error a propósito: `args={"corredor":"Corredor 1","superficie":"Corredor 1 - Prueba 1141","tipo":"exterior_poligono","poligono":"NoExiste","simular":true}`
      → `ok: false` con `error` que contiene `No se encontró la polilínea 'NoExiste' (usa el handle hexadecimal o el
      nombre de una línea característica).`
    - Simulación: `args={"corredor":"Corredor 1","superficie":"Corredor 1 - Prueba 1141","tipo":"talud_automatico","simular":true,"pedido":"simula el contorno automático de la superficie de prueba"}`
      → `simulado: true`; `accion: "Añadir el contorno 'Talud automático' (talud_automatico) a la superficie
      'Corredor 1 - Prueba 1141'"`; `antes` con `n_contornos: 0`, `contornos: []`; `despues` con `n_contornos: 1`,
      `contornos: ["Talud automático"]`.
    - Real: la misma **sin** `simular` y con `pedido` `añade el contorno automático a la superficie de prueba` →
      `simulado: false`, `despues` releído igual al previsto, `copia` terminada, **una** entrada nueva en Deshacer. Anota
      `ms`. Si responde `El talud no cierra; usa 'exterior_poligono' con una polilínea cerrada. Detalle: ...`, anota el
      detalle literal (es la respuesta prevista cuando la API no cierra el contorno) y salta al paso 12.
    - `estado_corredor` con `args={"corredor":"Corredor 1"}`: la superficie `Corredor 1 - Prueba 1141` con `contornos`
      `[{"nombre": "Talud automático", "tipo": ..., "es_extension_corredor": ..., "usar_como_exterior": ...}]` (anota los
      tres valores; lo esperado es `OutsideBoundary`, `true`, `true`).
    - **Deshacer de prueba**: `ejecutar_comando` con `args={"comando":"_.UNDO 1","timeout_s":30,"pedido":"deshaz el contorno de prueba"}`
      **una vez** y `estado_corredor` otra vez: anota si el contorno `Talud automático` desapareció. Si desapareció,
      repite la llamada **real** de este paso (mismo `pedido`) para volver a ponerlo; si sigue, **no** la repitas.
    OK solo si la escritura real deja el contorno `Talud automático` en la superficie de prueba.

12. **Reconstruir y comparar (decisivo).** `reconstruir_corredor` con
    `args={"corredor":"Corredor 1","pedido":"reconstruye el corredor 1 con la superficie de prueba"}` → `ok: true`,
    `despues.esta_desactualizado: false`; anota `ms`. Luego `estado_corredor` con `args={"corredor":"Corredor 1"}`:
    - `Corredor 1 - Prueba 1141`: `codigos_enlace: ["Datum", "Top"]`, el contorno del paso 11 y `esta_desactualizada`
      (anótalo).
    - `Corredor 1 - Top` y `Corredor 1 - Datum`: **iguales** que en el paso 2 (códigos, contornos, voladizo,
      `construir`, códigos de rotura). Cualquier diferencia es **FALLO**: anota cuál.
    - `listar_superficies`: anota si aparece `Corredor 1 - Prueba 1141` como superficie de `Corredor 1` (si no
      aparece, la superficie de corredor aún no tiene su superficie del dibujo: anótalo, es un dato para la memoria
      técnica).
    OK solo si la superficie de prueba tiene los dos códigos y su contorno, y Top y Datum no cambiaron.

13. **Nombre repetido sin entrada de Deshacer.** Repite **exactamente** la llamada real del paso 9 (sin `simular`, con
    `pedido` `repite la superficie de prueba a propósito`). Esperado: `ok: false`, `error` que contiene `El corredor ya
    tiene una superficie llamada 'Corredor 1 - Prueba 1141'. Usa agregar_codigo_superficie_corredor para añadirle
    códigos.` y un `id_log`. **Ninguna** entrada nueva en Deshacer y **ningún** archivo nuevo en `backups\`;
    `leer_historial` con `args={"ultimas_n":15}` muestra `MCP ✗ agregar_superficie_corredor no se ejecutó: falló la
    comprobación previa (simulación); sin contexto de comando, sin copia y sin entrada en Deshacer: ...` y **ninguna**
    `Comando inicia: EXECUTEFUNCTION` desde la llamada; en `mcp_log.jsonl`, una sola línea de esta llamada con
    `ok:false`.

14. **Cierre.** `leer_log` con `args={"ultimas_n":60}`: una línea `llamada` por cada llamada de los pasos 0 a 13, con
    `ok:false` en los errores a propósito (pasos 10, 11 y 13) y `ok:true` en los demás. **No guardes el dibujo** (es una
    copia; se quedan la superficie de prueba y el punto lejano).

15. **`buscar_api` en una consola cp1252 (opcional, sin Civil 3D, corrección 2).** Solo si en la PC está
    `MEMORIA_FIRMA_DIR`. En una consola de Windows nueva (cmd o PowerShell), desde `PuenteMcp`: `chcp 1252`, luego
    `uv run --frozen python buscar_api.py "rasante con PVI" --limite 3`. Esperado: código de salida `0` y una primera
    línea que empieza por `buscar_api "rasante con PVI" →` (la flecha puede verse como otro carácter en la consola; lo
    importante es que **no** salga `UnicodeEncodeError`). Hasta la 1.14.0 el programa moría con ese error. Anota la
    primera línea literal y el código de salida (`echo %ERRORLEVEL%` en cmd, `$LASTEXITCODE` en PowerShell).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_1141_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, los hallazgos, el resultado global (OK solo si los pasos decisivos 0, 2, 5, 6, 7, 9, 10, 11 y 12 son OK) y las
respuestas a estas preguntas para la memoria técnica:

1. Las dos versiones del paso 0 (`ping` y `GET /ping`), literales, y el número de herramientas (52 o 53).
2. Paso 2: los contornos de `Corredor 1 - Top` y `Corredor 1 - Datum`, literales (`nombre`, `tipo`,
   `es_extension_corredor`, `usar_como_exterior`).
3. Capa del corredor: la capa de la configuración (paso 3, y si el modificador era «Ninguno»), `despues.capa` de la
   simulación (paso 4), `datos.capa` y `datos.capa_configuracion` de la creación real (paso 5), la capa que mostró
   Propiedades y los avisos de capa literales si los hubo. ¿Resolvió `SettingsObjectLayerType` el valor `Corridor`?
4. Punto de inserción: el valor de `EXTMAX` del paso 6, los avisos literales `Punto de inserción automático: ...` de
   los pasos 6 y 7, los puntos (`x`, `y`) de los dos pasos y dónde se vio la vista en pantalla.
5. Paso 8: ¿quitó `_.UNDO 1` el perfil `Terreno - 1141`? ¿Y la vista?
6. Pasos 9 a 12: ¿qué dejaron las tres herramientas `agregar_*` (respuestas literales de las escrituras reales)?
   ¿Quedó `Corredor 1` desactualizado tras añadir la superficie (paso 9)? ¿Quitó `_.UNDO 1` el contorno (paso 11)?
   ¿Apareció `Corredor 1 - Prueba 1141` en `listar_superficies` tras reconstruir (paso 12)?
7. Tiempos (`ms`, `ms_espera`, `ms_ejecucion`) de las escrituras reales de los pasos 5, 6, 9, 10, 11 y 12.
8. Paso 15 (si se hizo): la primera línea y el código de salida.

Y la lista de miembros de la sección 1.14.1 de `miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D
2027` con la fecha (o a `no existe`, con el error literal). **No pegues en el informe líneas de `mcp_log.jsonl` con
rutas reales**: sustitúyelas por `<ruta>`.
