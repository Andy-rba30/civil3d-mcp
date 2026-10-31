# Prompt de validación de la 1.7.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D. Requisitos previos:

- Civil 3D 2027 abierto con **una copia** de un DWG de prueba **guardado en disco** (vale uno con una plantilla normal
  de Civil 3D: hace falta que tenga al menos un estilo de superficie y los formatos de archivo de puntos de serie,
  `PENZD (comma delimited)` o su nombre en español). No hace falta corredor. Anota la carpeta del DWG: ahí aparecerán
  `mcp_log.jsonl` y `backups\`.
- **Archivo de puntos sintético.** Crea en la PC la carpeta `C:\Proyectos\dibujos_prueba\` y dentro el archivo
  `puntos_prueba.csv` con exactamente estas 9 líneas (8 puntos PENZD en coordenadas genéricas, ningún dato real):

  ```
  # archivo de prueba, 8 puntos PENZD
  1,1000.0,2000.0,100.0,TN
  2,1010.0,2000.0,100.5,TN
  3,1020.0,2000.0,101.0,TN
  4,1000.0,2010.0,100.2,TN
  5,1010.0,2010.0,100.8,TN
  6,1020.0,2010.0,101.4,TN
  7,1000.0,2020.0,100.4,TN
  8,1020.0,2020.0,102.0,TN
  ```

- El plugin **1.7.0** se instala en el paso 0 desde la rama `tarea/2.1-superficie-tin`; el bundle queda con `ArbaMcp.dll`
  y `ArbaMcp.Nucleo.dll`. Puente reiniciado con el `main.py` de 1.7.0 (`uv sync` antes) y el cliente MCP reiniciado
  para que cargue la lista de **46** herramientas del plugin (más `buscar_api` del puente).
- Reparto de papeles: el agente local **instala y verifica**; el código, la tabla de miembros y este prompt los mantiene
  la sesión de desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.7.0**: la herramienta de escritura `crear_superficie_tin` (superficie TIN nueva desde un grupo de
  puntos o desde un archivo de puntos, con `simular`, copia de seguridad, una entrada de Deshacer, relectura con
  puntos, triángulos y extensión) y `Escritura.CrearObjeto`, el camino común que usarán las herramientas de creación
  siguientes. **Los pasos 2, 3, 4 y 6 son los decisivos.**
- Miembros de la API que esta validación decide (`herramientas-dev/miembros_por_verificar_civil3d.md`, sección 1.7.0):
  `TinSurface.Create` (las dos firmas), `PointFilesDefinition.AddPointFile`, `PointFileFormatCollection.GetPointFileFormats`,
  `PointFileFormat.Name`/`Delimiter`, `CivilDocument.PointGroups`, `PointGroup.Name`/`PointsCount`,
  `PointGroupsDefinition.AddPointGroup`, `GetTinProperties().NumberOfTriangles`, `Styles.SurfaceStyles`,
  `StyleBase.Name`, `Entity.StyleName`/`StyleId` y `SurfaceStyle.ContourStyle.Minor/MajorContourInterval` (solo lectura).
- Pedido de ejemplo, tal como lo diría el dueño (úsalo literal en `pedido` del paso 3): **"crea una superficie llamada
  Terreno con los puntos del archivo puntos_prueba.csv"**.

---

Eres el agente de validación de la versión 1.7.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Trabajas sobre una **copia** del dibujo. Usa las herramientas MCP **por su nombre** (los argumentos van dentro de
`args`), salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden, anota la respuesta **literal** (JSON
completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo.
- En este prompt, **`pedido` siempre lleva las palabras literales** que se indican; no las cambies.
- Si el dibujo ya tiene una superficie llamada `Terreno`, usa `Terreno MCP 17` en su lugar en todos los pasos y anótalo.

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `tarea/2.1-superficie-tin`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`, `uv sync`. Reinicia el puente y
   el cliente MCP. Crea el archivo `C:\Proyectos\dibujos_prueba\puntos_prueba.csv` de los requisitos. Abre Civil 3D
   con la copia del DWG. `ping` con `args={"pedido":"comprueba que el plugin responde"}` → `version` debe ser `1.7.0`.
   Pide la lista de herramientas: deben ser 46 del plugin e incluir `crear_superficie_tin` con los parámetros `nombre`,
   `grupo_puntos`, `archivo`, `formato_archivo`, `estilo`, `intervalo_curvas_menor`, `intervalo_curvas_mayor`,
   `simular` y `pedido`. Si no, para aquí.

1. **Punto de partida.** `listar_superficies` con `args={"pedido":"lista las superficies"}`. Anota los nombres: `Terreno`
   **no** debe estar (si está, usa `Terreno MCP 17` desde aquí). Abre el menú Deshacer de Civil 3D y anota cuántas
   entradas tiene (o la última). Mira si existe la carpeta `backups\` junto al DWG y cuántos archivos tiene.

2. **Simulación desde archivo (decisivo).** `crear_superficie_tin` con
   `args={"nombre":"Terreno","archivo":"C:\\Proyectos\\dibujos_prueba\\puntos_prueba.csv","intervalo_curvas_menor":1,"intervalo_curvas_mayor":5,"simular":true,"pedido":"simula la superficie Terreno con el archivo de puntos"}`.
   Esperado: `simulado: true`, `accion` que empieza por `Crear la superficie TIN 'Terreno' desde el archivo`,
   `antes: {"existe": false, "puntos": 0, ...}`, `despues: {"existe": true, "puntos": 8, "origen": "archivo",
   "formato_archivo": "PENZD (comma delimited)" (o el nombre equivalente en español del formato del dibujo), "estilo":
   "(estilo por defecto del dibujo)", "extension": {"min_x": 1000, "max_x": 1020, "min_y": 2000, "max_y": 2020,
   "min_z": 100, "max_z": 102}}` y `avisos` con **dos** textos: uno que empieza por `Formato de archivo de puntos
   '...' elegido automáticamente: columnas PENZD con separador coma` y otro que empieza por `Intervalos de curvas
   pedidos (menor 1 y mayor 5) sin aplicar` (o `ya coinciden` si el estilo por defecto ya tiene 1 y 5). OK solo si
   `puntos` es **8** (la línea `#` no cuenta), no hay `copia`, el menú Deshacer **no** cambió y no apareció ningún
   archivo nuevo en `backups\`. Si `avisos` dice que no se pudo elegir formato, anota el error literal (trae la lista de
   formatos del dibujo) y repite el paso añadiendo `"formato_archivo":"<uno de esa lista que empiece por PENZD y sea de
   comas>"`; anótalo como hallazgo (el nombre del formato depende del idioma de Civil 3D).

3. **Creación real desde archivo (decisivo).** El mismo `crear_superficie_tin` **sin** `simular` y con
   `pedido="crea una superficie llamada Terreno con los puntos del archivo puntos_prueba.csv"` (si en el paso 2 hizo
   falta `formato_archivo`, pásalo también aquí). Esperado: `simulado: false`, `cambios` que incluye `existe`,
   `puntos`, `triangulos`, `extension` y `estilo`; `antes: {"existe": false, "puntos": 0}`; `despues` con `existe: true`,
   `puntos: 8`, `triangulos` entre **6 y 8** (8 puntos en una malla de 3×3 sin el centro-derecha dan 8 triángulos si
   la triangulación es completa), `estilo` con el nombre del estilo por defecto del dibujo, `esta_desactualizada:
   false`, `extension` igual que la prevista en el paso 2; `copia` con `ruta` dentro de `backups\`, `metodo:
   "copia_de_disco"` (o `SaveAs` si el DWG no estaba guardado) y `estado: "terminada"`; `datos` con `nombre`,
   `origen: "archivo"`, `formato_archivo`, `formato_elegido_automaticamente: true`, `puntos: 8`, `triangulos`,
   `extension`, `intervalos_curvas: {"pedido_menor": 1, "pedido_mayor": 5, "aplicados": false, "donde_viven":
   "SurfaceStyle.ContourStyle (estilo de superficie)", "estilo_menor": <n>, "estilo_mayor": <n>}` y `archivo_resumen:
   {"lineas": 9, "lineas_puntos": 8, "lineas_ignoradas": 1, "separador": "coma", "columnas": 5, ...}`; `avisos` como
   en el paso 2; e `id_log`. En pantalla debe aparecer la superficie nueva (haz zoom a 1000,2000). OK solo si `puntos`
   es 8 y `despues.existe` es true. Si responde error "La superficie 'Terreno' se creó pero quedó sin puntos", anótalo
   literal y **para**: es el hallazgo principal (el formato o `AddPointFile` no importó nada).

4. **Lectura cruzada (decisivo).** `listar_superficies`: debe aparecer `Terreno` con `tipo_superficie: "TIN"`,
   `esta_desactualizada: false`, `n_lineas_rotura: 0`, `n_contornos: 0` y `corredor: null`. Luego
   `extension_superficie` con `args={"superficie":"Terreno"}`: `n_puntos: 8`, `min_x: 1000`, `max_x: 1020`, `min_y:
   2000`, `max_y: 2020`, `min_z: 100`, `max_z: 102`, `centro: {"x": 1010, "y": 2010, "z": 101}`. Y `cota_superficie`
   con `args={"superficie":"Terreno","x":1010,"y":2010}` → `cota: 100.8`. OK si los tres coinciden con el archivo.

5. **Una sola entrada de Deshacer y una copia.** Abre el menú Deshacer: respecto al paso 1 debe haber **una** entrada
   nueva (`Executefunction`), la del paso 3; los pasos 2 y 4 no añaden ninguna. En `backups\` debe haber **un** archivo
   nuevo `<nombre del dwg>_<fecha>_crear_superficie_tin.dwg` (el `copia.ruta` del paso 3). En `mcp_log.jsonl` deben
   estar las líneas `llamada` de los pasos 2 (con `"simular":true` en `args` y `ok:true`), 3 (con el `pedido` literal
   y `ok:true`) y 4. **No deshagas todavía.**

6. **Error por nombre repetido (decisivo).** Repite exactamente el `crear_superficie_tin` del paso 3 (sin `simular`).
   Esperado: `ok: false` y `error` que contiene `Ya existe la superficie 'Terreno'. Usa listar_superficies.`; con
   `id_log`. OK solo si además **no** hay entrada nueva en Deshacer y **no** hay archivo nuevo en `backups\` (la
   comprobación del nombre va antes de la copia... **atención**: la copia de disco se inicia al entrar y se espera antes
   de escribir; si aparece un archivo nuevo en `backups\`, anótalo como hallazgo: no es un fallo de seguridad, pero es
   una copia innecesaria). `listar_superficies` debe seguir mostrando una sola `Terreno` con `n_puntos` 8 en
   `extension_superficie`.

7. **Error por archivo inexistente.** `crear_superficie_tin` con
   `args={"nombre":"Terreno 2","archivo":"C:\\Proyectos\\dibujos_prueba\\no_existe.csv"}`. Esperado: `ok: false`, `error`
   que contiene `No existe el archivo de puntos 'C:\Proyectos\dibujos_prueba\no_existe.csv'`; sin entrada en Deshacer,
   sin copia nueva y `Terreno 2` no aparece en `listar_superficies`. Luego `crear_superficie_tin` con
   `args={"nombre":"Terreno 3","archivo":"C:\\Proyectos\\dibujos_prueba\\puntos_prueba.csv","formato_archivo":"NoExiste"}`
   → error `No existe el formato de archivo de puntos 'NoExiste' en el dibujo. Formatos disponibles: ...` (anota la
   lista: sirve para la memoria técnica). Y `crear_superficie_tin` con `args={"nombre":"Terreno 4"}` (sin origen) →
   error `Falta el origen de los puntos: indica 'grupo_puntos' ... o 'archivo' ...`; con los dos (`grupo_puntos` y
   `archivo`) → error `Indica solo uno`.

8. **Estilo inexistente y estilo elegido.** `crear_superficie_tin` con
   `args={"nombre":"Terreno 5","archivo":"C:\\Proyectos\\dibujos_prueba\\puntos_prueba.csv","estilo":"NoExiste","simular":true}`
   → error `No existe el estilo de superficie 'NoExiste' en el dibujo. Estilos disponibles: ...` (anota la lista; si
   dice `ninguno legible`, es un hallazgo sobre `StyleBase.Name`). Luego repite con `"estilo":"<uno de la lista>"` y
   `simular=true`: `despues.estilo` debe ser ese nombre y el aviso de intervalos debe decir qué intervalos tiene ese
   estilo (`El estilo '<nombre>' tiene menor <n> y mayor <n>`). **No crees** esta superficie de verdad.

9. **Desde un grupo de puntos (si el dibujo tiene puntos COGO; si no, anota "sin grupos" y salta).** Pide la lista de
   grupos con un error controlado: `crear_superficie_tin` con `args={"nombre":"Terreno 6","grupo_puntos":"NoExiste","simular":true}`
   → error `No existe el grupo de puntos 'NoExiste'. Grupos de puntos del dibujo: ...`. Si la lista trae un grupo con
   puntos (por ejemplo `_All Points`), `crear_superficie_tin` con `args={"nombre":"Terreno 6","grupo_puntos":"<ese grupo>","simular":true}`
   → `despues.puntos` igual al número de puntos del grupo (compáralo con Prospector) y `origen: "grupo_puntos"`. Luego
   sin `simular`: `despues.existe: true`, `puntos > 0`, `triangulos > 0`, una entrada nueva en Deshacer y una copia
   nueva (o reutilizada, `copia.reutilizada: true`, si el DWG no cambió en disco).

10. **Deshacer.** En Civil 3D, `_.UNDO 1` (o Ctrl+Z) una vez por cada superficie creada (pasos 3 y, si lo hiciste, 9).
    Esperado: la superficie desaparece de la pantalla y de `listar_superficies`; `extension_superficie` con
    `args={"superficie":"Terreno"}` responde error `No existe la superficie 'Terreno'. Usa listar_superficies.`. Si la
    superficie sigue tras deshacer, es un hallazgo (como pasó con los objetivos de corredor).

11. **Cierre.** `leer_log` con `args={"ultimas_n":15}`: una línea `llamada` por cada paso, con `ok:false` en los errores
    controlados (pasos 6, 7, 8 y el primero del 9) y `ok:true` en los demás. No guardes el dibujo (es una copia).

## Informe

Guarda `herramientas-dev/informes/informe_validacion_17_<AAAAMMDD>.md` con: versión e idioma de Civil 3D, DWG usado
(solo el nombre genérico, sin ruta ni datos de obra), una tabla paso / resultado (OK, FALLO) / respuesta literal /
tiempo, la lista de formatos de archivo de puntos y de estilos de superficie que devolvieron los errores de los pasos 7
y 8 (sirven para la memoria técnica: el nombre depende del idioma), y la lista de miembros de la sección 1.7.0 de
`miembros_por_verificar_civil3d.md` que pasan a `ejecutado en Civil 3D 2027` con la fecha (o a `no existe`, con el error
literal). **No pegues en el informe líneas de `mcp_log.jsonl` con rutas reales**: sustitúyelas por `<ruta>`.
