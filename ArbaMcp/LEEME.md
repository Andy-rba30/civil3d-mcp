# ArbaMcp: conector entre Civil 3D 2027 y un agente de IA

Plugin independiente de las herramientas de cálculo. Al cargarse en Civil 3D abre un servidor HTTP local en `http://127.0.0.1:8765/` (solo accesible desde tu máquina) y agrega el botón **Conexión IA** en la pestaña ARBA. Un puente MCP externo, en Python, traduce las herramientas del agente a peticiones a ese servidor, igual que el conector de Revit.

## Instalar

Con Civil 3D cerrado, en PowerShell dentro de esta carpeta:

```powershell
powershell -ExecutionPolicy Bypass -File .\instalar.ps1
```

Compila e instala el paquete en `%APPDATA%\Autodesk\ApplicationPlugins\ArbaMcp.bundle`. Requiere Visual Studio 2026 con .NET 10 y Civil 3D 2027 en la ruta indicada en `C3DPath` del `.csproj`. Compilar desde Visual Studio también instala.

## Seguridad

El servidor solo acepta peticiones con la cabecera `X-Arba-Token`. El token se genera en cada arranque de Civil 3D y se guarda en `%LOCALAPPDATA%\ArbaMcp\token`, legible solo por tu usuario; el puente Python lo lee de ahí. Además rechaza peticiones con cabecera `Origin`, con `Host` distinto de 127.0.0.1 o localhost, y `POST` que no sean JSON. Detalle en `CONTRATO.md`.

## Seguridad de escritura

Las herramientas que modifican el dibujo (`asignar_*`, `establecer_*`, `agregar_*`, `dividir_region`, `reconstruir_*`, `pegar_superficie`, `guardar_*`) comparten estas reglas (`Escritura.cs`):

- **Antes de escribir** comprueban que no hay un comando activo (`CMDACTIVE`), que el dibujo no es de solo lectura y que el mismo archivo no está abierto dos veces; si algo falla, responden error sin tocar nada.
- **Copias**: antes del primer cambio de cada minuto guardan `<carpeta del dwg>\backups\<nombre>_<fecha>_<herramienta>.dwg` (sin renombrar el dibujo activo) y conservan las últimas 20 copias. `guardar_copia` hace una copia a mano con el sufijo que indiques.
- **Log**: cada llamada de escritura añade una línea JSON a `<carpeta del dwg>\mcp_log.jsonl` (hora, herramienta, args, ok, ms, error); `leer_log` la devuelve. El historial del plugin se guarda además en `%LOCALAPPDATA%\ArbaMcp\historial.log`.
- **`simular`**: todas aceptan `simular=true` y entonces devuelven lo que harían (`antes`/`despues` previstos) sin tocar el dibujo.
- **Verificación**: después de escribir vuelven a leer el objeto y devuelven `antes`/`despues` con los campos que cambiaron; si el dibujo no refleja el cambio, responden error y no reintentan.
- **Límites**: ninguna herramienta borra ni recrea regiones, líneas base, ensamblajes ni superficies. `ejecutar_comando` es el último recurso y **ya no envuelve la orden en UNDO por defecto**: pásale `undo=true` si quieres un grupo de deshacer.

## Hilos y contextos de ejecución

Desde la versión 1.2.2 el servidor HTTP nunca llama a la API de AutoCAD: cada herramienta se lleva al hilo principal
por el Dispatcher de WPF y se ejecuta en el contexto de comando del dibujo activo (`ExecuteInCommandContextAsync`),
esperando a que Civil 3D esté libre (sin comando activo ni cuadro de diálogo). `ping`, `leer_historial`, `leer_log`,
`leer_variable` y `capturar_pantalla` responden siempre. Si Civil 3D se cierra con errores o traza en blanco con el
plugin cargado, sigue `ESTABILIDAD.md`.

## Comprobar

Abre Civil 3D y pulsa **Conexión IA** en la pestaña ARBA, o escribe `ARBAMCP`. Verás un aviso con el estado y el puerto. Desde PowerShell:

```powershell
$t = Get-Content "$env:LOCALAPPDATA\ArbaMcp\token" -Raw
curl.exe -H "X-Arba-Token: $t" http://127.0.0.1:8765/ping
curl.exe -H "X-Arba-Token: $t" http://127.0.0.1:8765/tools
curl.exe -X POST http://127.0.0.1:8765/execute -H "X-Arba-Token: $t" -H "Content-Type: application/json" -d "{\"tool\":\"listar_alineamientos\"}"
```

## Icono del botón

Copia el PNG de tu icono a `Recursos\ARBA_BTN_MCP.png` (32×32) y recompila. Ver `Recursos\LEEME.md`.

## Configurar

- `ARBA_MCP_PORT`: puerto (por defecto 8765).
- `ARBA_MCP=0`: desactiva el servidor.

## Herramientas

- Generales: `ping`, `listar_alineamientos`, `listar_perfiles`, `listar_pvis`, `listar_superficies`, `abrir_dibujo`, `ejecutar_comando` (último recurso; espera a que el comando termine, con tiempo límite y `undo` opcional), `leer_historial`, `leer_log`, `leer_variable`, `capturar_pantalla`.
- Lectura por API .NET: `listar_corredores`, `listar_regiones`, `listar_objetivos`, `listar_ensamblajes`, `listar_intersecciones`, `listar_lineas_muestreo`, `estado_corredor`, `punto_a_pk`, `pk_a_punto`, `cota_superficie`, `interseccion_ejes`.
- Escritura por API .NET (todas con `simular`): `asignar_ensamblaje_region`, `asignar_objetivo`, `asignar_objetivos_superficie`, `dividir_region`, `establecer_rango_region`, `establecer_frecuencia`, `agregar_estacion_region`, `reconstruir_corredor`, `agregar_superficie_corredor`, `agregar_codigo_superficie_corredor`, `agregar_contorno_superficie_corredor`, `agregar_linea_rotura_superficie`, `pegar_superficie`, `reconstruir_superficie`, `guardar_dibujo`, `guardar_copia`.
- Exportación por comando (no hay API): `exportar_landxml`, `exportar_imx`.

El contrato completo, con parámetros, respuestas y ejemplos `curl`, está en `CONTRATO.md`. Otros plugins pueden registrar sus propias herramientas (ver el final de ese archivo).

## Estructura

```
Cinta.cs          Arranque del plugin, botón Conexión IA, comando ARBAMCP, pestaña ARBA compartida
Servidor.cs       HTTP mínimo en 127.0.0.1 (GET /ping, GET /tools, POST /execute) e historial
HiloPrincipal.cs  Cola que ejecuta cada herramienta en el hilo principal de AutoCAD, en su contexto (Documento, Aplicacion, Inmediato) y con Civil 3D libre
ESTABILIDAD.md    Qué fallaba en los hilos, qué cambió en 1.2.2 y cómo comprobar en tu equipo si el plugin causaba cierres o trazados en blanco
Herramientas.cs   Registro, ayudantes y herramientas generales
Herramientas.Corredores.cs   Corredores, regiones, objetivos, ensamblajes, intersecciones, líneas de muestreo
Herramientas.Superficies.cs  Geometría de ejes, superficies, líneas de rotura, pegado y exportación
Herramientas.Seguridad.cs    leer_log, guardar_dibujo, guardar_copia
Escritura.cs      Reglas de seguridad de escritura: comprobaciones, copias, mcp_log.jsonl, simular, antes/después
Api.cs            Acceso por reflexión a los miembros de la API que cambian de nombre entre versiones
Bundle/           PackageContents.xml para la carga automática
instalar.ps1      Compila e instala
CONTRATO.md       Contrato para escribir el puente MCP
Recursos/         Iconos de los botones
pruebas/          probar_servidor.py: seguridad, ejecutar_comando y, con --dwg, lectura/escritura sobre un dibujo con corredor
```

El puente Python está en la carpeta `PuenteMcp` del repositorio (escucha en `localhost:8001/mcp`; Antigravity se conecta ahí).
