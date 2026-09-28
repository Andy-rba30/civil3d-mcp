# ArbaMcp: conector entre Civil 3D 2027 y un agente de IA

Plugin independiente de las herramientas de cálculo. Al cargarse en Civil 3D abre un servidor HTTP local en `http://127.0.0.1:8765/` (solo accesible desde tu máquina) y agrega el botón **Conexión IA** en la pestaña ARBA. Un puente MCP externo, en Python, traduce las herramientas del agente a peticiones a ese servidor, igual que el conector de Revit.

## Instalar

Con Civil 3D cerrado, en PowerShell dentro de esta carpeta:

```powershell
powershell -ExecutionPolicy Bypass -File .\instalar.ps1
```

Compila e instala el paquete en `%APPDATA%\Autodesk\ApplicationPlugins\ArbaMcp.bundle` (desde 1.3.0, dos DLL: `ArbaMcp.dll` y `ArbaMcp.Nucleo.dll`). Requiere Visual Studio 2026 con .NET 10 y Civil 3D 2027 en la ruta indicada en `C3DPath` del `.csproj`. Compilar desde Visual Studio también instala.

## Seguridad

El servidor solo acepta peticiones con la cabecera `X-Arba-Token` (salvo `GET /ping`, que desde 1.3.0 es público y solo devuelve `ok`, `servidor` y `version`, para que el puente sepa cuándo ha arrancado Civil 3D). El token se genera en cada arranque de Civil 3D y se guarda en `%LOCALAPPDATA%\ArbaMcp\token`, legible solo por tu usuario; el puente Python lo lee de ahí. Además rechaza peticiones con cabecera `Origin`, con `Host` distinto de 127.0.0.1 o localhost, y `POST` que no sean JSON. Los 401 se anotan en el historial una vez por ruta y minuto. Detalle en `CONTRATO.md`.

## Seguridad de escritura

Las herramientas que modifican el dibujo (`asignar_*`, `establecer_*`, `agregar_*`, `dividir_region`, `reconstruir_*`, `pegar_superficie`, `guardar_*`) comparten estas reglas (`Escritura.cs`):

- **Antes de escribir** comprueban que no hay un comando activo (`CMDACTIVE`), que el dibujo no es de solo lectura y que el mismo archivo no está abierto dos veces; si algo falla, responden error sin tocar nada.
- **Copias**: antes de cada escritura real copian el `.dwg` **de disco** a `<carpeta del dwg>\backups\<nombre>_<fecha>_<herramienta>.dwg` en un hilo aparte (se espera justo antes de tocar el dibujo; se reutiliza si el archivo no cambió) y conservan las últimas 20. La copia refleja el último guardado en disco, no el estado en memoria; la respuesta lo dice (`copia.nota`, `copia.refleja_guardado_de`). Un dibujo sin guardar usa `SaveAs` en `%LOCALAPPDATA%\ArbaMcp\backups`. Si la copia falla no se escribe. `guardar_copia` hace una copia a mano (SaveAs, estado en memoria) con el sufijo que indiques.
- **Deshacer**: cada herramienta de escritura (también un lote) es una sola entrada `Executefunction` del menú Deshacer y `_.UNDO 1` la revierte entera (validado en Civil 3D 2027, 28/09/2026). Las lecturas corren en contexto de aplicación y no dejan entrada.
- **Lotes**: `asignar_objetivos` y `establecer_frecuencias` aplican varias asignaciones o regiones en un solo contexto de comando, con una copia, una línea de log y una entrada de Deshacer; validan todo antes de tocar nada y devuelven `fallidos[]` sin abortar el lote. Antes de encadenar varias llamadas iguales, el agente debe usar el lote.
- **Log**: cada llamada de escritura añade una línea JSON a `<carpeta del dwg>\mcp_log.jsonl` (hora, herramienta, args, ok, ms, error); `leer_log` la devuelve. El historial del plugin se guarda además en `%LOCALAPPDATA%\ArbaMcp\historial.log`.
- **`simular`**: todas aceptan `simular=true` y entonces devuelven lo que harían (`antes`/`despues` previstos) sin tocar el dibujo.
- **Verificación**: después de escribir vuelven a leer el objeto y devuelven `antes`/`despues` con los campos que cambiaron; si el dibujo no refleja el cambio, responden error y no reintentan.
- **Límites**: ninguna herramienta borra ni recrea regiones, líneas base, ensamblajes ni superficies. `ejecutar_comando` es el último recurso y **ya no envuelve la orden en UNDO por defecto**: pásale `undo=true` si quieres un grupo de deshacer.

## Hilos y contextos de ejecución

Desde la versión 1.2.2 el servidor HTTP nunca llama a la API de AutoCAD: cada herramienta se lleva al hilo principal
por el Dispatcher de WPF esperando a que Civil 3D esté libre (sin comando activo ni cuadro de diálogo). Las de
escritura se ejecutan en el contexto de comando del dibujo activo (`ExecuteInCommandContextAsync`, una entrada de
Deshacer por herramienta); desde 1.3.0 las de solo lectura corren en contexto de aplicación con el dibujo bloqueado,
para no llenar el menú Deshacer de entradas vacías. `ping`, `leer_historial`, `leer_log`,
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
- Escritura por API .NET (todas con `simular`): `asignar_ensamblaje_region`, `asignar_objetivo`, `asignar_objetivos` (lote), `asignar_objetivos_superficie`, `dividir_region`, `establecer_rango_region`, `establecer_frecuencia`, `establecer_frecuencias` (lote), `agregar_estacion_region`, `reconstruir_corredor`, `agregar_superficie_corredor`, `agregar_codigo_superficie_corredor`, `agregar_contorno_superficie_corredor`, `agregar_linea_rotura_superficie`, `pegar_superficie`, `reconstruir_superficie`, `guardar_dibujo`, `guardar_copia`.
- Exportación por comando (no hay API): `exportar_landxml`, `exportar_imx`.

El contrato completo, con parámetros, respuestas y ejemplos `curl`, está en `CONTRATO.md`. Otros plugins pueden registrar sus propias herramientas (ver el final de ese archivo).

## Estructura

```
Cinta.cs          Arranque del plugin, botón Conexión IA, comando ARBAMCP, pestaña ARBA compartida
Servidor.cs       HTTP mínimo en 127.0.0.1 (GET /ping sin token, GET /tools, POST /execute) e historial; el análisis y la autorización están en el núcleo
HiloPrincipal.cs  Lleva cada herramienta al hilo principal de AutoCAD, en su contexto (Documento, Aplicacion, Inmediato) y con Civil 3D libre; la política de cola está en el núcleo (Planificador)
ESTABILIDAD.md    Qué fallaba en los hilos, qué cambió en 1.2.2 y cómo comprobar en tu equipo si el plugin causaba cierres o trazados en blanco
Herramientas.cs   Registro, ayudantes y herramientas generales
Herramientas.Corredores.cs   Corredores, regiones, objetivos, ensamblajes, intersecciones, líneas de muestreo, lotes
Herramientas.Superficies.cs  Geometría de ejes, superficies, líneas de rotura, pegado y exportación
Herramientas.Seguridad.cs    leer_log, guardar_dibujo, guardar_copia
Escritura.cs      Reglas de seguridad de escritura: comprobaciones, copia de disco en hilo aparte, marca de deshacer, mcp_log.jsonl, simular, antes/después
Bundle/           PackageContents.xml para la carga automática
instalar.ps1      Compila e instala las dos DLL
CONTRATO.md       Contrato para escribir el puente MCP
Recursos/         Iconos de los botones
pruebas/          probar_servidor.py: seguridad, ejecutar_comando, "Civil 3D ocupado" y, con --dwg, lectura/escritura; --fase 13 para lo nuevo de 1.3.0

../ArbaMcp.Nucleo/     Biblioteca .NET 10 sin AutoCAD: Http y Autorizacion, Argumentos, Catalogo, Verificacion, Copias y CopiaSeguridad,
                       RegistroEscritura, Registro401, Lotes, Pendiente y Planificador, Api (reflexión). Se prueba sin Civil 3D.
../ArbaMcp.Pruebas/    xUnit sobre el núcleo (dotnet test ArbaMcp.Pruebas)
../herramientas-dev/CompilarSinCivil/  Compila los .cs del plugin contra sustitutos de la API para cazar errores de nombres sin Civil 3D
```

## Pruebas

- Sin Civil 3D: `dotnet test ArbaMcp.Pruebas` (núcleo, xUnit), `cd PuenteMcp; uv run --frozen pytest -q` (puente, con un
  servidor falso) y `dotnet build herramientas-dev/CompilarSinCivil`. Las tres corren en la integración continua
  (`.github/workflows/pruebas.yml`); el plugin no se compila en CI porque necesita las DLL de Civil 3D.
- Con Civil 3D: `pruebas\probar_servidor.py` (ver README) y los prompts `herramientas-dev/VALIDACION_122.md` y
  `VALIDACION_13.md` para el agente local. Lo que cada miembro de la API tiene comprobado está en
  `herramientas-dev/miembros_por_verificar_civil3d.md`.

El puente Python está en la carpeta `PuenteMcp` del repositorio (escucha en `localhost:8001/mcp`; Antigravity se conecta ahí).
