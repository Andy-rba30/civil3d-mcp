# civil3d-mcp: conector entre Civil 3D 2027 y un agente de IA

Dos piezas, con la misma estructura que el conector de Revit:

| Carpeta | Qué es | Puerto |
|---|---|---|
| [`ArbaMcp`](ArbaMcp/) | Plugin .NET 10 para Civil 3D 2027. Abre un servidor HTTP local y pone el botón **Conexión IA** en la pestaña ARBA. | 8765 |
| [`ArbaMcp.Nucleo`](ArbaMcp.Nucleo/) | La lógica del plugin que no toca AutoCAD (HTTP, argumentos, verificación, copias, cola, lotes, reflexión). Se compila y se prueba sin Civil 3D. | — |
| [`ArbaMcp.Pruebas`](ArbaMcp.Pruebas/) | Pruebas xUnit del núcleo. | — |
| [`PuenteMcp`](PuenteMcp/) | Puente MCP en Python. Lee las herramientas del plugin y las expone al agente por `streamable-http`. Con `tests/` (pytest). | 8001 |
| [`herramientas-dev`](herramientas-dev/) | Prompts de validación para el agente local, tabla de miembros de la API por verificar, `InspectC3D` y `CompilarSinCivil`. | — |

Versión actual: **1.9.0** (plugin, bundle y puente): herramienta nueva de escritura `crear_perfil_terreno_y_vista`, los pasos 3 y 4 del flujo vial: crea el perfil de terreno (EG) de un alineamiento sobre una superficie y la vista de perfil de ese alineamiento, los dos en una sola transacción y una sola entrada de Deshacer (si la vista falla, el perfil tampoco queda), con estilos y conjuntos por defecto del dibujo o los pedidos, punto de inserción explícito o automático a la derecha de lo dibujado, asignación del perfil a las bandas de la vista, `simular`, copia de seguridad y relectura (progresivas y cotas del perfil, nombre y punto de la vista; validación en `herramientas-dev/VALIDACION_19.md`). La 1.8.0 añadió `crear_alineamiento_desde_polilinea`, el paso 2 del flujo vial: crea un alineamiento (eje) desde una polilínea 2D de tangentes, con una curva circular por PI (radio por defecto y radios por PI), comprobando antes, sin Civil 3D, qué radios caben entre las tangentes (el que no cabe queda sin curva y el aviso dice el radio máximo), y devuelve la lista de PI con progresiva, radio y longitud de curva; con `simular`, copia de seguridad, una entrada de Deshacer y relectura (validación en `herramientas-dev/VALIDACION_18.md`). La 1.7.0 añadió `crear_superficie_tin`, la primera del flujo vial que **crea** un objeto: una superficie TIN a partir de un grupo de puntos del dibujo o de un archivo de puntos de texto (PENZD, ENZ...), con `simular`, copia de seguridad, una entrada de Deshacer y relectura (puntos, triángulos y extensión). Estrena `Escritura.CrearObjeto`, el camino común para las herramientas de creación siguientes (validación en `herramientas-dev/VALIDACION_17.md`). La 1.6.0 hizo que el registro `mcp_log.jsonl` sirviera como **dataset**. Todas las herramientas aceptan el parámetro `pedido` (las palabras del usuario, solo se guardan), toda llamada queda registrada con un `id` que la respuesta devuelve como `id_log`, la herramienta nueva `aprobar_resultado` anota que el usuario dio por bueno un resultado y el script `exportar_dataset` del puente convierte las llamadas aprobadas en pares `pedido → herramientas` anonimizados (validación en `herramientas-dev/VALIDACION_16.md`). La 1.5.0 añadió `buscar_api`, una herramienta del **puente** (no del plugin) que busca en la memoria técnica privada `memoria-firma` (recetas, glosario español → API y catálogo de la API de Civil 3D 2027) y funciona con Civil 3D cerrado (`herramientas-dev/VALIDACION_15.md`); la 1.4.0, `extension_superficie` y el flujo de superficies a Revit por coordenadas compartidas (`PuenteMcp/INSTRUCCIONES_AGENTE.md`, sección 7).

## Instalar el plugin

Con Civil 3D cerrado, en PowerShell desde la raíz del repo:

```powershell
powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1
```

Compila e instala el bundle (`ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`) en `%APPDATA%\Autodesk\ApplicationPlugins\ArbaMcp.bundle`. Seguridad, herramientas y contrato en [`ArbaMcp/LEEME.md`](ArbaMcp/LEEME.md) y [`ArbaMcp/CONTRATO.md`](ArbaMcp/CONTRATO.md). Si Civil 3D se cierra con errores o traza en blanco con el plugin cargado, lee [`ArbaMcp/ESTABILIDAD.md`](ArbaMcp/ESTABILIDAD.md). Para validar una versión recién instalada, los prompts [`herramientas-dev/VALIDACION_122.md`](herramientas-dev/VALIDACION_122.md) y [`herramientas-dev/VALIDACION_13.md`](herramientas-dev/VALIDACION_13.md).

## Instalar el puente

```powershell
cd PuenteMcp
python -m venv .venv
.\.venv\Scripts\pip install -r requirements.txt
.\.venv\Scripts\python main.py
```

O con `uv` (mismo `pyproject.toml` y `uv.lock` que usa la integración continua): `cd PuenteMcp; uv sync; uv run python main.py`.

Escucha en `http://127.0.0.1:8001/mcp`. En la configuración MCP del agente, la entrada `civil3d` apunta a esa URL.

### `buscar_api`: la memoria técnica desde el puente (desde 1.5.0)

El puente tiene una herramienta propia, `buscar_api(consulta, limite=10)`, que no necesita Civil 3D: busca en el
repositorio privado `memoria-firma` (primero `recetas/*.md`, luego `catalogo/glosario.yaml` y después el catálogo
`catalogo/catalogo_c3d2027.sqlite`) y devuelve un texto corto con las recetas, los términos del glosario (con su
estado `confirmado` o `por confirmar`) y las clases, métodos (con firma y parámetros) y propiedades de la API. La
carpeta se indica con la variable de entorno de usuario `MEMORIA_FIRMA_DIR`, en PowerShell:

```powershell
[Environment]::SetEnvironmentVariable('MEMORIA_FIRMA_DIR','C:\Proyectos\memoria-firma','User')
```

Después hay que reiniciar el puente (lee la variable al arrancar). Si falta la variable o un archivo, la herramienta
responde un texto que empieza por `Error:` y dice qué falta y cómo ponerlo. Necesita la dependencia `pyyaml`
(`uv sync`, o `pip install pyyaml` en el `.venv`). También se usa como comando, sin MCP, desde `PuenteMcp`:

```powershell
uv run --frozen python buscar_api.py "rasante con PVI" --limite 10
python -m buscar_api "rasante con PVI"
```

### `exportar_dataset`: el registro como dataset (desde 1.6.0)

Cada llamada queda en `mcp_log.jsonl` con el `pedido` del usuario y un `id` (el `id_log` de la respuesta); cuando el
usuario confirma un resultado, el agente llama a `aprobar_resultado`. El script `exportar_dataset` del puente toma
solo las llamadas aprobadas, las agrupa por pedido y escribe `pares.jsonl` sin rutas, sin nombres de dibujos ni de
obras (lista `casos\PROHIBIDOS.txt` de `memoria-firma`), por defecto en `%MEMORIA_FIRMA_DIR%\dataset\pares.jsonl`:

```powershell
cd PuenteMcp
uv run --frozen python exportar_dataset.py --logs C:\Proyectos\dibujos_prueba
```

Detalle de campos, opciones y respuesta esperada en `ArbaMcp/CONTRATO.md` (*Herramientas del puente*).

El puente lee el token de `%LOCALAPPDATA%\ArbaMcp\token`, que Civil 3D genera en cada arranque. Puede arrancar antes que Civil 3D: sondea `GET /ping` (sin token) y registra las herramientas en cuanto el plugin responde; si Civil 3D se reinicia, relee el token solo. Las respuestas al agente son JSON (`json.dumps`) con `ms` (Civil 3D), `ms_espera`, `ms_ejecucion` y `ms_puente`; el tiempo máximo depende de la herramienta (30 s lectura, 120 s escritura, 300 s reconstrucciones y exportaciones) y el archivo `PuenteMcp/INSTRUCCIONES_AGENTE.md` se entrega al agente como `instructions` del servidor MCP (precedencia API > comando, lotes, flujo obligatorio para corredores, reglas de dominio y glosario).

## Arranque automático

Un `.vbs` en la carpeta Inicio de Windows lanza el puente sin ventana al iniciar sesión:

```vb
Set WshShell = CreateObject("WScript.Shell")
WshShell.CurrentDirectory = "<ruta del repo>\PuenteMcp"
WshShell.Run """<ruta del repo>\PuenteMcp\.venv\Scripts\python.exe"" main.py", 0, False
```

Se guarda como `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\startup_c3d_mcp.vbs`. Si cambia `main.py` hay que reiniciar el puente: un proceso en marcha no se entera de que el archivo cambió.

## Pruebas

Sin Civil 3D (lo mismo que ejecuta la integración continua en cada PR y push a `main`, `.github/workflows/pruebas.yml`):

```powershell
dotnet test ArbaMcp.Pruebas                                   # núcleo del plugin, xUnit
cd PuenteMcp; uv run --frozen pytest -q; cd ..                 # puente, pytest contra un servidor falso
dotnet build herramientas-dev\CompilarSinCivil                 # el plugin contra sustitutos de la API (solo nombres y tipos)
```

El plugin `ArbaMcp` no se compila en CI: referencia las DLL de una instalación local de Civil 3D 2027 (`C3DPath`), que
no existe en GitHub Actions ni se puede redistribuir. Lo que depende de ellas queda `por verificar` en
[`herramientas-dev/miembros_por_verificar_civil3d.md`](herramientas-dev/miembros_por_verificar_civil3d.md) hasta la
validación en el programa real.

Con Civil 3D abierto y el plugin cargado:

```powershell
.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py
.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py --dwg C:\Proyectos\prueba_corredor.dwg
.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py --fase 13 --dwg C:\Proyectos\prueba_corredor.dwg
```

Con `--dwg` abre ese dibujo (debe tener al menos un corredor) y comprueba las herramientas de lectura, `simular`, la escritura real con copia de seguridad, `ejecutar_comando` sin UNDO, `mcp_log.jsonl` y que `deshacer_objetivos` devuelve la última escritura de objetivos entera (Civil 3D no revierte objetivos con `_.UNDO`); `--sin-escritura` omite las pruebas que modifican el dibujo. `--fase 13` prueba lo nuevo de la 1.3.0 (`/ping` sin token, 401 agrupados, tiempos, `ms_puente` por el puente, lotes, `deshacer_objetivos` sobre el lote, `_.UNDO 1` sobre las frecuencias, copia reutilizada, rechazo de la superficie del propio corredor); `--fase todo` ejecuta ambas. Los prompts de validación para el agente local, con informe OK/FALLO, están en `herramientas-dev/VALIDACION_122.md` (1.2.2) y `VALIDACION_13.md` (1.3.x); los informes, en `herramientas-dev/informes/`.
