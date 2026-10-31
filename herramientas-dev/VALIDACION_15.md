# Prompt de validación de la 1.5.0 (civil3d-mcp) para el agente local

Copia este texto tal cual en el agente que tiene conectado el MCP de Civil 3D (`civil3d`). Requisitos previos:

- El repositorio privado `memoria-firma` clonado en la PC (por ejemplo en `C:\Proyectos\memoria-firma`) y al día
  (`git pull`): debe contener `catalogo\catalogo_c3d2027.sqlite`, `catalogo\glosario.yaml` y la carpeta `recetas\`.
- La variable de entorno de usuario `MEMORIA_FIRMA_DIR` apuntando a esa carpeta, puesta con PowerShell:
  `[Environment]::SetEnvironmentVariable('MEMORIA_FIRMA_DIR','C:\Proyectos\memoria-firma','User')`.
- El plugin **1.5.0** instalado en el paso 0 desde la rama `tarea/1.3-buscar-api` (solo cambia el número de versión;
  el bundle sigue con `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`). Puente con las dependencias nuevas (`uv sync` en
  `PuenteMcp`, o `pip install pyyaml` en el `.venv`) y **reiniciado después de poner la variable** (la lee al
  arrancar). Cliente MCP reiniciado para que cargue la lista de **45** herramientas (44 del plugin + `buscar_api`).
- Reparto de papeles: el agente local **instala y verifica**; el código y este prompt los mantiene la sesión de
  desarrollo a partir del informe. El agente no edita ningún archivo del repositorio salvo su informe en
  `herramientas-dev/informes/` y no hace commits.
- **Lo que trae la 1.5.0**: la herramienta `buscar_api(consulta, limite=10)` del **puente** (Python, no del plugin),
  que busca en `memoria-firma` (recetas, glosario español → API y catálogo de la API de Civil 3D 2027) y funciona con
  Civil 3D cerrado. Sus argumentos van directos (no dentro de `args`) y responde texto, no JSON. **Los pasos 2 y 5 son
  los decisivos.**
- Miembros de la API que esta validación decide: **ninguno**. `buscar_api` es Python y no llama a ninguna DLL de
  Autodesk; `herramientas-dev/miembros_por_verificar_civil3d.md` no cambia en 1.5.0.

---

Eres el agente de validación de la versión 1.5.0 del conector Civil 3D MCP (plugin `ArbaMcp` + puente `PuenteMcp`).
Usa las herramientas MCP **por su nombre**, salvo en los pasos que indican PowerShell. Ejecuta los pasos en orden,
anota la respuesta **literal** (el texto completo o, si es muy largo, las primeras 40 líneas) y **el tiempo total de
cada paso**.

Reglas:

- **No marques OK lo que no viste.** Cada OK va con la respuesta literal.
- Si un paso falla, anota el error literal y sigue con el siguiente salvo que el prompt diga que pares.
- No uses `ejecutar_comando` para "arreglar" nada: si algo no funciona, es un hallazgo.
- No copies en el informe el contenido de las recetas ni del glosario más allá de las líneas que devuelve la
  herramienta: `memoria-firma` es privado y el informe vive en un repositorio público.

## Pasos

0. **Instalar.** Con Civil 3D cerrado, en PowerShell desde la raíz del repo en la rama `tarea/1.3-buscar-api`:
   `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`. En `PuenteMcp`, `uv sync` (o
   `.\.venv\Scripts\pip install pyyaml`). Comprueba la variable: `[Environment]::GetEnvironmentVariable('MEMORIA_FIRMA_DIR','User')`
   debe imprimir la carpeta de `memoria-firma`. Reinicia el puente (está arrancado por el `.vbs` de la carpeta Inicio:
   cierra el proceso `python.exe` de `main.py` y vuelve a lanzar el `.vbs`, o cierra y abre la sesión de Windows) y
   reinicia el cliente MCP. **Civil 3D sigue cerrado.**

1. **Lista de herramientas con Civil 3D cerrado.** Pide al cliente la lista de herramientas del servidor `civil3d`.
   Esperado: aparece `buscar_api` con los parámetros `consulta` (obligatorio) y `limite` (por defecto 10), aunque las
   44 herramientas del plugin no estén (Civil 3D cerrado). Si `buscar_api` no aparece, el puente no se reinició con el
   `main.py` de 1.5.0: para aquí.

2. **`buscar_api` con Civil 3D cerrado (decisivo).** Pide al agente: "usa buscar_api: cómo crear una rasante con PVI"
   (llamada esperada: `buscar_api` con `consulta="rasante con PVI"`). Esperado, literal en la primera línea:
   `buscar_api "rasante con PVI" → 0 recetas, 2 entradas del glosario, 8 del catálogo (límite 10). Memoria: <carpeta>`
   (el número de recetas puede ser mayor si `recetas\` ya tiene archivos que hablen de rasante o PVI). Después:
   `Consulta ampliada con el glosario: Profile, CreateByLayout, ProfileType, FG, ...`, un bloque `GLOSARIO (2, de
   glosario.yaml):` con los términos `rasante` y `PIV` (cada concepto con `[confirmado]` o `[por confirmar]`), y un
   bloque `CATÁLOGO (...)` con **pocas firmas relevantes**: `Profile.CreateByLayout` (dos firmas, con `firma:` y
   `parámetros:`), `ProfileType`, `Profile.CreateStaticFGFromProfile`, `ProfilePVI` y `ProfilePVI.RawStation`. OK solo
   si la respuesta es texto (no JSON), tiene esos tres bloques y **no** es un volcado largo de la API (menos de 60
   líneas con `limite` 10).

3. **Nombre exacto y obsoleto.** `buscar_api` con `consulta="ProfilePVI Station"` y `limite=20`. Esperado: en
   `CATÁLOGO` la primera fila es `- clase Autodesk.Civil.DatabaseServices.ProfilePVI (...)` y poco después
   `- propiedad Autodesk.Civil.DatabaseServices.ProfilePVI.Station: Double (...)` seguida de una línea `OBSOLETO`
   (en 2027 `ProfilePVI.Station` está obsoleto y se usa `RawStation`, hecho ya validado en
   `miembros_por_verificar_civil3d.md`).

4. **Límite.** `buscar_api` con `consulta="rasante con PVI"` y `limite=3`. Esperado: la cabecera dice `(límite 3)` y
   la suma de recetas + entradas del glosario + filas del catálogo es 3, con prioridad recetas > glosario > catálogo.

5. **Error claro sin `MEMORIA_FIRMA_DIR` (decisivo).** En PowerShell, desde `PuenteMcp`, sin la variable en esa
   consola: `$env:MEMORIA_FIRMA_DIR=''; uv run --frozen python buscar_api.py "rasante"`. Esperado, literal:
   `Error: Falta la variable de entorno MEMORIA_FIRMA_DIR: debe apuntar a la carpeta del repositorio privado memoria-firma (la que contiene catalogo\ y recetas\). Ponla con PowerShell (y reinicia el puente): [Environment]::SetEnvironmentVariable('MEMORIA_FIRMA_DIR','C:\Proyectos\memoria-firma','User')`
   y `$LASTEXITCODE` igual a 2. Luego con una carpeta inexistente:
   `$env:MEMORIA_FIRMA_DIR='C:\no_existe'; uv run --frozen python buscar_api.py "rasante"` → `Error: La carpeta de
   MEMORIA_FIRMA_DIR no existe: C:\no_existe. ...`. Ninguna de las dos debe mostrar una traza de Python.

6. **Uso como comando, con la memoria.** En PowerShell desde `PuenteMcp` (cierra y abre la consola para que tenga la
   variable de usuario, o pásala con `--memoria`): `uv run --frozen python buscar_api.py "superficie TIN" --limite 5` y
   `python -m buscar_api "superficie TIN" --limite 5` (con el `.venv` activado o `.\.venv\Scripts\python -m buscar_api ...`).
   Esperado: las dos imprimen el mismo texto, que empieza por `buscar_api "superficie TIN" → ` y trae el término
   `superficie` del glosario y `TinSurface` en el catálogo; `$LASTEXITCODE` igual a 0.

7. **Civil 3D abierto: versión 1.5.0.** Abre Civil 3D con una copia de cualquier DWG de prueba. Espera a que el
   puente registre las herramientas (unos segundos). `ping` → `version` debe ser `1.5.0`. Pide la lista de
   herramientas: deben ser 45 e incluir `buscar_api` y las 44 del plugin (por ejemplo `extension_superficie` y
   `deshacer_objetivos`). También en PowerShell: `curl.exe http://127.0.0.1:8765/ping` → `{"ok":true,"servidor":"ArbaMcp","version":"1.5.0"}`.

8. **`buscar_api` con Civil 3D abierto.** Repite la llamada del paso 2. Esperado: la misma respuesta que con Civil 3D
   cerrado, y **sin** nueva línea en `mcp_log.jsonl` (`leer_log` con `ultimas_n=3` no debe mostrar `buscar_api`) ni
   entrada nueva en el menú Deshacer de Civil 3D: `buscar_api` no toca el dibujo ni pasa por el plugin.

9. **Una herramienta del plugin sigue funcionando.** `listar_superficies`. Esperado: la lista de superficies del
   dibujo con `ms`, `ms_espera`, `ms_ejecucion` y `ms_puente`, como en 1.4.0. Confirma que la 1.5.0 no cambió nada del
   plugin salvo el número de versión.

## Informe

Guarda `herramientas-dev/informes/informe_validacion_15_<AAAAMMDD>.md` con: versión de Civil 3D, carpeta de
`memoria-firma` usada (solo la ruta; sin copiar recetas ni glosario), fecha del `git pull` de `memoria-firma`, una
tabla paso / resultado (OK, FALLO) / respuesta literal (recortada a las primeras 40 líneas) / tiempo, y al final la
frase "Miembros de la API nuevos en 1.5.0: ninguno" (no hay nada que mover en `miembros_por_verificar_civil3d.md`).
Si el paso 2 o el 5 fallan, incluye el texto completo de la respuesta y la salida de
`[Environment]::GetEnvironmentVariable('MEMORIA_FIRMA_DIR','User')`.
