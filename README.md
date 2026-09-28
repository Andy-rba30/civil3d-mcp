# civil3d-mcp: conector entre Civil 3D 2027 y un agente de IA

Dos piezas, con la misma estructura que el conector de Revit:

| Carpeta | Qué es | Puerto |
|---|---|---|
| [`ArbaMcp`](ArbaMcp/) | Plugin .NET 10 para Civil 3D 2027. Abre un servidor HTTP local y pone el botón **Conexión IA** en la pestaña ARBA. | 8765 |
| [`ArbaMcp.Nucleo`](ArbaMcp.Nucleo/) | La lógica del plugin que no toca AutoCAD (HTTP, argumentos, verificación, copias, cola, lotes, reflexión). Se compila y se prueba sin Civil 3D. | — |
| [`ArbaMcp.Pruebas`](ArbaMcp.Pruebas/) | Pruebas xUnit del núcleo. | — |
| [`PuenteMcp`](PuenteMcp/) | Puente MCP en Python. Lee las herramientas del plugin y las expone al agente por `streamable-http`. Con `tests/` (pytest). | 8001 |
| [`herramientas-dev`](herramientas-dev/) | Prompts de validación para el agente local, tabla de miembros de la API por verificar, `InspectC3D` y `CompilarSinCivil`. | — |

Versión actual: **1.3.0** (plugin, bundle y puente).

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

Con `--dwg` abre ese dibujo (debe tener al menos un corredor) y comprueba las herramientas de lectura, `simular`, la escritura real con copia de seguridad, `ejecutar_comando` sin UNDO, `mcp_log.jsonl` y que `_.UNDO 1` revierte la última escritura entera; `--sin-escritura` omite las pruebas que modifican el dibujo. `--fase 13` prueba lo nuevo de la 1.3.0 (`/ping` sin token, 401 agrupados, tiempos, `ms_puente` por el puente, lotes, deshacer del lote, copia reutilizada); `--fase todo` ejecuta ambas. Los prompts de validación para el agente local, con informe OK/FALLO, están en `herramientas-dev/VALIDACION_122.md` (1.2.2) y `VALIDACION_13.md` (1.3.x); los informes, en `herramientas-dev/informes/`.
