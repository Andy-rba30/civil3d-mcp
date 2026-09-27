# civil3d-mcp: conector entre Civil 3D 2027 y un agente de IA

Dos piezas, con la misma estructura que el conector de Revit:

| Carpeta | Qué es | Puerto |
|---|---|---|
| [`ArbaMcp`](ArbaMcp/) | Plugin .NET 10 para Civil 3D 2027. Abre un servidor HTTP local y pone el botón **Conexión IA** en la pestaña ARBA. | 8765 |
| [`PuenteMcp`](PuenteMcp/) | Puente MCP en Python. Lee las herramientas del plugin y las expone al agente por `streamable-http`. | 8001 |

## Instalar el plugin

Con Civil 3D cerrado, en PowerShell desde la raíz del repo:

```powershell
powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1
```

Compila e instala el bundle en `%APPDATA%\Autodesk\ApplicationPlugins\ArbaMcp.bundle`. Seguridad, herramientas y contrato en [`ArbaMcp/LEEME.md`](ArbaMcp/LEEME.md) y [`ArbaMcp/CONTRATO.md`](ArbaMcp/CONTRATO.md). Si Civil 3D se cierra con errores o traza en blanco con el plugin cargado, lee [`ArbaMcp/ESTABILIDAD.md`](ArbaMcp/ESTABILIDAD.md).

## Instalar el puente

```powershell
cd PuenteMcp
python -m venv .venv
.\.venv\Scripts\pip install -r requirements.txt
.\.venv\Scripts\python main.py
```

Escucha en `http://127.0.0.1:8001/mcp`. En la configuración MCP del agente, la entrada `civil3d` apunta a esa URL.

El puente lee el token de `%LOCALAPPDATA%\ArbaMcp\token`, que Civil 3D genera en cada arranque. Puede arrancar antes que Civil 3D: registra las herramientas en cuanto el plugin responde. Las respuestas al agente son JSON (`json.dumps`), el tiempo máximo depende de la herramienta (30 s lectura, 120 s escritura, 300 s reconstrucciones y exportaciones) y el archivo `PuenteMcp/INSTRUCCIONES_AGENTE.md` se entrega al agente como `instructions` del servidor MCP (precedencia API > comando, flujo obligatorio para corredores, reglas de dominio y glosario).

## Arranque automático

Un `.vbs` en la carpeta Inicio de Windows lanza el puente sin ventana al iniciar sesión:

```vb
Set WshShell = CreateObject("WScript.Shell")
WshShell.CurrentDirectory = "<ruta del repo>\PuenteMcp"
WshShell.Run """<ruta del repo>\PuenteMcp\.venv\Scripts\python.exe"" main.py", 0, False
```

Se guarda como `%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\startup_c3d_mcp.vbs`. Si cambia `main.py` hay que reiniciar el puente: un proceso en marcha no se entera de que el archivo cambió.

## Pruebas

Con Civil 3D abierto y el plugin cargado:

```powershell
.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py
.\PuenteMcp\.venv\Scripts\python .\ArbaMcp\pruebas\probar_servidor.py --dwg C:\Proyectos\prueba_corredor.dwg
```

Con `--dwg` abre ese dibujo (debe tener al menos un corredor) y comprueba las herramientas de lectura, `simular`, la escritura real con copia de seguridad, `ejecutar_comando` sin UNDO y `mcp_log.jsonl`; `--sin-escritura` omite las pruebas que modifican el dibujo.
