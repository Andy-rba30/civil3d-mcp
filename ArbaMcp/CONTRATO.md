# Contrato del servidor local para el puente MCP

El plugin **ArbaMcp** abre, al cargarse en Civil 3D, un servidor HTTP mínimo que escucha **solo en 127.0.0.1**.
No usa http.sys ni necesita permisos de administrador.

- Puerto: variable de entorno `ARBA_MCP_PORT` (por defecto **8765**). `ARBA_MCP=0` desactiva el servidor.
- Comando `ARBAMCP` (botón *Conexión IA* de la pestaña ARBA): muestra si está activo, el puerto y las últimas líneas del historial.
- Desde la versión **1.3.0** el plugin son dos DLL: `ArbaMcp.dll` (lo que toca AutoCAD y Civil 3D) y `ArbaMcp.Nucleo.dll`
  (análisis y autorización HTTP, argumentos, verificación antes/después, copias de seguridad, cola de trabajos,
  reflexión), que se prueba sin Civil 3D con `ArbaMcp.Pruebas` (xUnit). Ver *Pruebas* al final.
- Todas las llamadas a la API de Civil 3D se ejecutan en el hilo principal, en el contexto que declara cada
  herramienta (ver *Contextos de ejecución*). Si Civil 3D está ocupado (comando activo o cuadro de diálogo modal
  abierto) la petición espera; pasado `timeout_s` responde error. Si la acción seguía esperando se descarta (no se
  ejecutará al liberarse Civil 3D) y el mensaje lo indica.

## Contextos de ejecución (desde la versión 1.2.2)

El servidor HTTP corre en hilos del ThreadPool y nunca llama a la API de AutoCAD. Cada herramienta se encola en
`HiloPrincipal`, que la lleva al hilo principal por el Dispatcher de WPF de ese hilo (con `Idle` de respaldo) y la
ejecuta en el contexto que la herramienta declara en `Herramienta.Contexto`:

| Contexto | Qué hace | Espera a que Civil 3D esté libre | Herramientas |
|---|---|---|---|
| `Documento` (por defecto) | Contexto de comando del dibujo activo (`DocumentManager.ExecuteInCommandContextAsync`): la herramienta corre como un comando más, con el documento bloqueado y los gráficos actualizados al terminar. AutoCAD la anota como **una entrada `Executefunction` del menú Deshacer** (validado el 28/09/2026) | Sí | Todas las de escritura del dibujo, y `listar_lineas_muestreo` (abre el grupo para escritura con `fuentes=true`) |
| `Aplicacion` | Contexto de aplicación en el hilo principal. Las lecturas y las simulaciones bloquean el dibujo **en modo lectura** (`DocumentLockMode.Read`): un bloqueo de escritura fuera de un comando deja un `Grupo de comandos` en el menú Deshacer aunque no cambie nada (validado el 28/09/2026 con la 1.3.0); el de lectura no lo deja (validado el 28/09/2026 con la 1.3.1: ni las lecturas ni las simulaciones dejan entrada) | Sí | Desde 1.3.0, todas las de solo lectura (`listar_*`, `estado_corredor`, `punto_a_pk`, `pk_a_punto`, `cota_superficie`, `interseccion_ejes`); desde 1.3.1, también cualquier herramienta de escritura llamada con `simular=true`; `abrir_dibujo`, el envío de `ejecutar_comando` y de las exportaciones |
| `Inmediato` | Contexto de aplicación, sin esperar y por delante de los demás trabajos | No | `ping`, `leer_historial`, `leer_log`, `leer_variable`, `capturar_pantalla`, los ESC de `ejecutar_comando` |

"Libre" significa `CMDACTIVE = 0` y ventana principal habilitada (sin cuadro de diálogo modal). Los trabajos
`Documento` y `Aplicacion` se ejecutan de uno en uno; mientras uno espera, el historial anota
`MCP ⏳ <herramienta> espera: <motivo>`. Detalle y comprobaciones en `ESTABILIDAD.md`.

## Seguridad (desde la versión 1.1.0)

Toda petición debe cumplir estas reglas; si no, se rechaza antes de procesar nada:

| Regla | Si falla |
|---|---|
| Cabecera `X-Arba-Token` con el token de la sesión (32 bytes aleatorios del generador criptográfico, en hexadecimal; se compara en tiempo constante). El plugin lo genera al iniciar y lo guarda en `%LOCALAPPDATA%\ArbaMcp\token` (permisos solo para tu usuario). Cambia en cada arranque de Civil 3D. Aplica a todas las rutas **salvo `GET /ping`**, pública desde 1.3.0 (solo responde `ok`, `servidor` y `version`, nada del dibujo; es lo que sondea el puente mientras Civil 3D arranca) | 401 |
| Sin cabecera `Origin` (evita llamadas desde navegadores) | 403 |
| Cabecera `Host` exactamente `127.0.0.1:<puerto>` o `localhost:<puerto>` | 400 |
| En `POST`, `Content-Type: application/json` | 415 |

El puente Python lee el token del archivo al arrancar y lo vuelve a leer si recibe 401 o cuando `/ping` vuelve a responder
tras un arranque de Civil 3D. Los 401 se anotan en el historial **una vez por ruta y minuto** (el primero en el acto y,
si hubo más, una línea con el recuento al cerrarse el minuto), no uno por petición.

## Rutas

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/ping` | **Sin token.** `{"ok":true,"servidor":"ArbaMcp","version":"1.3.0"}`; sin datos del dibujo (para eso, la herramienta `ping`) |
| GET | `/tools` | Lista de herramientas con nombre, descripción y parámetros (nombre, tipo, descripción, requerido) |
| POST | `/execute` | Ejecuta una herramienta. Cuerpo: `{"tool":"nombre","args":{...},"timeout_s":120}` |

Respuesta de `/execute`:

```json
{"ok": true,  "tool": "ping", "ms": 3, "ms_espera": 0, "ms_ejecucion": 2, "result": { ... }}
{"ok": false, "error": "mensaje"}
```

`ms` es el tiempo total visto por el servidor; `ms_espera`, lo que el trabajo esperó en cola a que Civil 3D estuviera
libre; `ms_ejecucion`, la herramienta en el hilo principal (los dos últimos son `null` en las herramientas asíncronas,
`ejecutar_comando` y las exportaciones, que gestionan su propia espera). El puente añade `ms_puente` (total visto
desde Python) a lo que entrega al agente.

Los tipos de parámetro son `string`, `number`, `boolean` o, desde 1.3.0, `json`: un texto que contiene un valor JSON
(los lotes lo usan para sus listas). El puente expone `json` como `str` y su descripción lleva un ejemplo; si un cliente
habla HTTP directamente puede enviar el valor ya estructurado. El puente debe leer `/tools` al arrancar y registrar
cada herramienta de forma dinámica, de modo que al añadir herramientas en C# no haya que tocar Python. El puente pasa
`timeout_s` según la herramienta: 30 s en lectura, 120 s en escritura y 300 s en `reconstruir_corredor`,
`reconstruir_superficie`, `exportar_landxml` y `exportar_imx`. Si un cliente llama a un nombre retirado, el puente
responde con la sustituta (tabla `HERRAMIENTAS_RETIRADAS` de `main.py`; vacía en 1.3.0).

## Seguridad de escritura (desde la versión 1.2.0)

Toda herramienta de escritura pasa por `Escritura.Ejecutar` (`Escritura.cs`), que aplica estas reglas antes y después del cambio:

1. **Comprobaciones previas**: responde error, sin tocar nada, si `CMDACTIVE > 0` (hay un comando activo), si el dibujo está en solo lectura o si el mismo archivo está abierto más de una vez en Civil 3D.
2. **Copia de seguridad antes de cada escritura real (desde 1.3.0, como en Revit)**: si el dibujo está guardado en disco, se copia el `.dwg` **de disco** con `File.Copy` en un hilo aparte, iniciado al entrar en la herramienta y esperado justo antes de tocar el dibujo, a `<carpeta del dwg>\backups\<nombre>_<yyyyMMdd_HHmmss>_<herramienta>.dwg`. Si el `.dwg` no cambió de fecha ni de tamaño desde la copia anterior (y esa copia sigue en disco) se reutiliza sin copiar. **La copia refleja el último guardado en disco, no el estado en memoria** (la respuesta lo dice en `copia.nota`; para conservar el estado actual, `guardar_dibujo` o `guardar_copia` antes). Un dibujo sin guardar en disco sigue usando `SaveAs(ruta, false, DwgVersion.Current, SecurityParameters)` en `%LOCALAPPDATA%\ArbaMcp\backups` y la respuesta lo indica (`copia.metodo: "SaveAs"`). Se conservan las últimas 20 copias de cada dibujo. **Nunca se escribe sin copia terminada**: si la copia falla, la herramienta responde error sin tocar el dibujo. `guardar_copia` no cambia (SaveAs a mano con el sufijo que indiques).
3. **Registro por llamada**: cada escritura añade una línea a `<carpeta del dwg>\mcp_log.jsonl` con `{"hora","herramienta","args","ok","ms","error"}` y otra al historial (`leer_historial`). `leer_log` devuelve las últimas líneas ya interpretadas. Un lote es una sola línea.
4. **Verificación antes/después**: tras escribir, la herramienta vuelve a leer el objeto y responde `{"simulado":false,"cambios":[...],"antes":{...},"despues":{...},"copia":{...},"datos":...,"avisos":...}` con los campos que cambiaron. `copia` es un objeto: `ruta`, `metodo` (`copia_de_disco` | `SaveAs`), `reutilizada`, `ms` (lo que tardó la copia; 0 si se reutilizó), `espera_ms` (lo que la escritura esperó a que terminara), `refleja_guardado_de` (fecha del último guardado del `.dwg`; `null` con SaveAs), `bytes`, `estado` y `nota`. Si el dibujo no refleja el cambio pedido, responde `ok=false` con la explicación (y el estado leído) y **no reintenta**.
5. **Nada se borra ni se recrea**: ninguna herramienta elimina regiones, líneas base, ensamblajes ni superficies, ni usa `Erase()`.
6. **`simular`** (boolean, por defecto `false`): con `true` responde `{"simulado":true,"accion":"...","antes":{...},"despues":{...}}` con lo que haría, sin tocar el dibujo (tampoco hace copia; sí registra la llamada en el log).
7. **Una entrada de Deshacer por herramienta, y deshacer propio para los objetivos**: cada escritura (también un lote) corre como un pseudocomando y AutoCAD la anota como una sola entrada `Executefunction` del menú Deshacer, con todas sus transacciones (validado en Civil 3D 2027 el 28/09/2026; no hacen falta marcas de deshacer). Las lecturas y las simulaciones no dejan entrada (contexto `Aplicacion` con bloqueo de lectura, validado con la 1.3.1). `_.UNDO 1` revierte las frecuencias, pero **Civil 3D no deshace los cambios de objetivos**: ni tras `SetTargets` ni tras cambiarlos en Propiedades de corredor, `_.UNDO 1` y Ctrl+Z retiran la entrada del menú y el objetivo sigue en el valor nuevo (validado con la 1.3.2, pasos 9 y 9b, con un cambio hecho desde el propio cuadro de Civil 3D). Por eso, desde 1.3.3, cada escritura de objetivos (`asignar_objetivo`, `asignar_objetivos`, `asignar_objetivos_superficie`) guarda en memoria lo que había antes y devuelve un bloque `restaurar` (`herramienta: deshacer_objetivos`, `args.id`, `antes` por objetivo); **`deshacer_objetivos`** reaplica ese estado tras comprobar que cada objetivo sigue como lo dejó la escritura (si no, va a `fallidos` salvo `forzar=true`). Validado en Civil 3D 2027 el 28/09/2026 (`VALIDACION_13` 1.3.3, pasos 9, 9b y 14; las dos suites de `probar_servidor.py` en `TODO OK`). La pila (50 entradas por dibujo, `Restauraciones.cs`) vive hasta cerrar Civil 3D; si se perdió, `antes` sirve para reasignar a mano.

El historial del plugin (`leer_historial`) también se escribe en `%LOCALAPPDATA%\ArbaMcp\historial.log` (rota a 5 MB en `historial.1.log`).

## Herramientas generales

| Herramienta | Parámetros | Devuelve |
|---|---|---|
| `ping` | — | plugin, versión, puerto, dibujo activo, hora |
| `listar_alineamientos` | — | nombre, inicio, fin, longitud, perfiles[] |
| `listar_perfiles` | alineamiento* | nombre, tipo (EG/FG), inicio, fin, pvis |
| `listar_pvis` | alineamiento*, perfil* | n, progresiva, cota, pe_pct, ps_pct, a_pct, tiene_curva, tipo_curva, longitud_curva |
| `listar_superficies` | — | nombre, tipo (clase .NET), `tipo_superficie` (TIN, Grid, TinVolume, GridVolume o Corridor), `esta_desactualizada`, `n_lineas_rotura`, `n_contornos`, `corredor` (si es superficie de corredor) |
| `abrir_dibujo` | ruta* | abierto, `ya_estaba_abierto`. Si el archivo ya está abierto, activa esa pestaña en vez de abrirlo otra vez |
| `ejecutar_comando` | comando*, timeout_s, undo | **Último recurso; usa primero las herramientas de API.** Espera a que el comando termine y devuelve `terminado`, `cancelado`, `fallido`, `el comando no se inició en N s (¿nombre incorrecto o Civil 3D ocupado?)` o `timeout con ESC`. Si hay un comando activo responde error sin enviar nada. **Solo con `undo=true`** la orden va entre `_.UNDO _BE` y `_.UNDO _E`; por defecto no se toca UNDO. Si se agota `timeout_s` (60 por defecto) envía dos ESC para cancelar |
| `leer_historial` | ultimas_n | líneas con hora: comandos iniciados/terminados, llamadas MCP, mensajes, copias y escrituras |
| `leer_log` | ultimas_n | `ruta` de `mcp_log.jsonl` y `lineas[]` (hora, herramienta, args, ok, ms, error) |
| `leer_variable` | nombre* | variable, valor, tipo |
| `capturar_pantalla` | ruta | ruta del PNG, ancho, alto (ventana principal de Civil 3D, con diálogos) |

`*` = obligatorio.

## Herramientas de lectura de corredores y geometría (API .NET, desde 1.2.0)

| Herramienta | Parámetros | Devuelve |
|---|---|---|
| `listar_corredores` | — | nombre, esta_desactualizado, reconstruir_automatico, lineas_base[] (nombre, alineamiento, perfil, inicio, fin, n_regiones) |
| `listar_regiones` | corredor*, linea_base | por región: linea_base, indice, nombre, inicio, fin, ensamblaje, frecuencia_tangentes, frecuencia_curvas, frecuencia_espirales, frecuencia_perfil, estaciones_adicionales[] |
| `listar_objetivos` | corredor*, linea_base*, region* | por subensamblaje: subensamblaje, grupo, lado (izquierda/derecha/ninguno), tipo (superficie/elevacion/desplazamiento), parametro, objetivos[] (tipo: eje, perfil, superficie, polilinea, linea_caracteristica; nombre; handle), opcion_objetivo (mas_cercano/exterior/interior) |
| `listar_ensamblajes` | — | nombre, tipo, grupos[] (nombre, lado, subensamblajes[]: nombre, tipo, lado, parametros{}), usado_en[] (corredor, linea_base, region) |
| `listar_intersecciones` | — | nombre, eje_principal, eje_secundario, pk_principal, pk_secundaria, x, y, corredor, tipo, regiones_generadas[] |
| `listar_lineas_muestreo` | alineamiento, fuentes (false) | grupos: nombre, alineamiento, n_lineas, inicio, fin; con `fuentes=true`, fuentes[] (nombre, tipo, muestreada). Consultar las fuentes abre el grupo para escritura (lo exige la API) y puede tardar |
| `punto_a_pk` | alineamiento*, x*, y* | pk, desplazamiento, lado (`Alignment.StationOffset`) |
| `pk_a_punto` | alineamiento*, pk*, desplazamiento, perfil | x, y (`Alignment.PointLocation`) y `cota` si se pasa `perfil` (`Profile.ElevationAt`) |
| `cota_superficie` | superficie*, x*, y* | cota (`Surface.FindElevationAtXY`) o error si el punto está fuera |
| `interseccion_ejes` | alineamiento_a*, alineamiento_b* | metodo, n, cruces[] (x, y, pk_a, pk_b). Usa `Entity.IntersectWith`; si no devuelve puntos, muestrea cada 0.5 m |
| `estado_corredor` | corredor* | esta_desactualizado, reconstruir_automatico, ultima_reconstruccion (de las hechas por MCP), n_lineas_base, superficies[] (nombre, codigos_enlace[], codigos_punto[], codigos_lineas_caracteristicas[], contornos[] (nombre, tipo, usar_como_exterior), esta_desactualizada) |

`region` admite el nombre o el `indice` que devuelve `listar_regiones`.

## Herramientas de escritura (todas con `simular`)

Todas devuelven la respuesta estándar de escritura (`simulado`, `cambios`, `antes`, `despues`, `copia`, `avisos`, `datos`).

| Herramienta | Parámetros | Acción |
|---|---|---|
| `asignar_ensamblaje_region` | corredor*, linea_base*, region*, ensamblaje* | `region.AssemblyId = idEnsamblaje` |
| `asignar_objetivo` | corredor*, linea_base*, region*, subensamblaje*, tipo* (superficie/elevacion/desplazamiento), objetivo* (nombre del objeto o `ninguno`), alineamiento_del_perfil, opcion (mas_cercano/exterior/interior), mismo_lado, grupo, parametro | `region.GetTargets()` → modifica el `SubassemblyTargetInfo` que coincide por `SubassemblyName` y tipo → `region.SetTargets(...)`. Con varios subensamblajes del mismo nombre exige `grupo` (y lista las opciones); con varios objetivos del mismo tipo exige `parametro`. El objetivo se resuelve como superficie, eje, perfil (con `alineamiento_del_perfil`), línea característica o handle de polilínea. Desde 1.3.3 rechaza una superficie generada por el propio corredor y la respuesta lleva `restaurar` (regla 7) |
| `asignar_objetivos_superficie` | corredor*, superficie*, linea_base, region | Pone la superficie en **todos** los objetivos de tipo superficie del ámbito. Desde 1.3.3 rechaza la superficie del propio corredor y la respuesta lleva `restaurar` |
| `deshacer_objetivos` (1.3.3) | id, forzar, simular | Devuelve la última escritura de objetivos del dibujo (o la entrada `id`, el `restaurar.args.id` de la respuesta) a lo que había antes, con la pila en memoria de `Restauraciones`. Comprueba antes que cada objetivo sigue como lo dejó esa escritura (si no, ese objetivo va a `fallidos` salvo `forzar=true`) y que los objetos anteriores siguen existiendo; responde como un lote (`antes`/`despues` por objetivo, `fallidos`, `datos`) más `deshacer` (`entrada`, `pendientes`). Con `simular=true`, el plan y las pendientes. Es el único deshacer que funciona para objetivos: Civil 3D no los revierte con `_.UNDO` ni Ctrl+Z |
| `dividir_region` | corredor*, linea_base*, region*, pk* | Si la API tiene `Split` lo usa; si no, recorta la región hasta `pk` y añade otra (`BaselineRegions.Add`) con el mismo ensamblaje desde `pk` hasta el fin anterior, copiando frecuencias y objetivos |
| `establecer_rango_region` | corredor*, linea_base*, region*, inicio, fin | Cambia `StartStation`/`EndStation`; falla si solapa con otra región o sale de la línea base |
| `establecer_frecuencia` | corredor*, linea_base*, region*, tangentes, curvas, espirales, perfil | Cambia las frecuencias indicadas |
| `asignar_objetivos` (lote, 1.3.0) | corredor*, linea_base, asignaciones* (`json`), forzar, simular | Varias asignaciones de objetivos en **un solo contexto de comando, una copia, una línea de log y una entrada de Deshacer**. `asignaciones` es un arreglo JSON (texto) donde cada elemento lleva los argumentos de `asignar_objetivo` (`linea_base`, `region`, `subensamblaje`, `tipo`, `objetivo` y los opcionales); `corredor` y `linea_base` del nivel superior son valores por defecto. Valida todos antes de tocar nada (error `asignaciones[i]: ...`), aplica en orden; un objeto inexistente va a `fallidos[]` con su motivo sin abortar el lote. Máximo 200 elementos salvo `forzar=true`. Desde 1.3.3 un elemento con la superficie del propio corredor va a `fallidos` y la respuesta lleva `restaurar` con un elemento por objetivo distinto |
| `establecer_frecuencias` (lote, 1.3.0) | corredor*, linea_base, regiones* (`json`), forzar, simular | Igual, para `establecer_frecuencia`: cada elemento lleva `region` (nombre o índice), `linea_base` opcional y las frecuencias a cambiar |
| `agregar_estacion_region` | corredor*, linea_base*, region*, pk* | Añade la estación adicional (`AdditionalStations.Add` o el método equivalente) |
| `reconstruir_corredor` | corredor* | `corridor.Rebuild()`; `datos.ms` y `esta_desactualizado` después |
| `agregar_superficie_corredor` | corredor*, nombre*, codigo_enlace (Top) | `corridor.CorridorSurfaces.Add(nombre)` + `AddLinkCode(codigo, true)` |
| `agregar_codigo_superficie_corredor` | corredor*, superficie*, codigo*, tipo (enlace/linea_caracteristica, acepta punto como alias), como_linea_rotura | `AddLinkCode` / `AddFeatureLineCode` (en Civil 3D 2027 las superficies se definen por líneas características; `punto` se acepta como alias de compatibilidad devolviendo aviso en la respuesta) |
| `agregar_contorno_superficie_corredor` | corredor*, superficie*, tipo (talud_automatico/talud_por_linea_base/exterior_poligono), usar_como_exterior, poligono, nombre | `Boundaries.AddCorridorExtentsBoundary(...)` / `Boundaries.Add(nombre, idPolilinea)`. Si el talud no cierra: "El talud no cierra; usa exterior_poligono con una polilínea cerrada" |
| `agregar_linea_rotura_superficie` | superficie*, corredor*, superficie_corredor o codigo | Crea polilíneas 3D (capa `MCP_LINEAS_ROTURA`) con las líneas características del corredor de esos códigos de punto y las añade con `BreaklinesDefinition.AddStandardBreaklines` |
| `pegar_superficie` | superficie_destino*, superficie_origen* | `TinSurface.PasteSurface` + `Rebuild`; si el origen ya estaba pegado, solo reconstruye |
| `reconstruir_superficie` | superficie* | `surface.Rebuild()` |
| `guardar_dibujo` | como | `SaveAs(ruta actual, true, ...)` (QSAVE por API) o `SaveAs(como, true, ...)`: el nuevo archivo pasa a ser el activo |
| `guardar_copia` | sufijo* | Copia en `backups\` sin renombrar el activo (misma rutina que las copias automáticas) |

Respuesta de un lote: la de escritura, con `antes` y `despues` como **arreglos por índice** (`null` en los fallidos),
`cambios` como `"<índice>.<campo>"`, `datos: {total, aplicadas, fallidas}` y `fallidos: [{indice, accion, motivo}]`;
simulado, además `plan: [{indice, accion, cambios, antes, despues | error}]`. Si algún elemento aplicado no refleja el
cambio, responde error con el índice y el campo (`'[2].objetivos'`) y no reintenta.

### Ejemplos `curl` (PowerShell, con `$t` = token)

```powershell
$t = Get-Content "$env:LOCALAPPDATA\ArbaMcp\token" -Raw
$h = @("-H", "X-Arba-Token: $t", "-H", "Content-Type: application/json")
$u = "http://127.0.0.1:8765/execute"
curl.exe -X POST $u @h -d '{"tool":"asignar_ensamblaje_region","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","region":"0","ensamblaje":"Calzada 7 m","simular":true}}'
curl.exe -X POST $u @h -d '{"tool":"asignar_objetivo","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","region":"0","subensamblaje":"DaylightGeneral - (Right)","tipo":"superficie","objetivo":"Terreno"}}'
curl.exe -X POST $u @h -d '{"tool":"asignar_objetivo","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","region":"0","subensamblaje":"LaneSuperelevationAOR - (Left)","tipo":"desplazamiento","objetivo":"Borde izq","opcion":"mas_cercano"}}'
curl.exe -X POST $u @h -d '{"tool":"asignar_objetivo","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","region":"0","subensamblaje":"LaneSuperelevationAOR - (Left)","tipo":"elevacion","objetivo":"Rasante borde","alineamiento_del_perfil":"Borde izq"}}'
curl.exe -X POST $u @h -d '{"tool":"asignar_objetivos_superficie","args":{"corredor":"Corredor 1","superficie":"Terreno"}}'
curl.exe -X POST $u @h -d '{"tool":"dividir_region","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","region":"0","pk":250}}'
curl.exe -X POST $u @h -d '{"tool":"establecer_rango_region","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","region":"1","inicio":250,"fin":480.5}}'
curl.exe -X POST $u @h -d '{"tool":"establecer_frecuencia","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","region":"0","tangentes":10,"curvas":5}}'
curl.exe -X POST $u @h -d '{"tool":"asignar_objetivos","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","simular":true,"asignaciones":"[{\"region\":\"0\",\"subensamblaje\":\"DaylightGeneral - (Right)\",\"tipo\":\"superficie\",\"objetivo\":\"Terreno\"},{\"region\":\"0\",\"subensamblaje\":\"LaneSuperelevationAOR - (Left)\",\"tipo\":\"desplazamiento\",\"objetivo\":\"Borde izq\",\"opcion\":\"mas_cercano\"}]"}}'
curl.exe -X POST $u @h -d '{"tool":"establecer_frecuencias","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","regiones":"[{\"region\":\"0\",\"tangentes\":10,\"curvas\":5},{\"region\":\"1\",\"perfil\":20}]"}}'
curl.exe -X POST $u @h -d '{"tool":"agregar_estacion_region","args":{"corredor":"Corredor 1","linea_base":"BL - Eje","region":"0","pk":123.45}}'
curl.exe -X POST $u @h -d '{"tool":"reconstruir_corredor","args":{"corredor":"Corredor 1"},"timeout_s":300}'
curl.exe -X POST $u @h -d '{"tool":"agregar_superficie_corredor","args":{"corredor":"Corredor 1","nombre":"Corredor 1 - Top","codigo_enlace":"Top"}}'
curl.exe -X POST $u @h -d '{"tool":"agregar_codigo_superficie_corredor","args":{"corredor":"Corredor 1","superficie":"Corredor 1 - Top","codigo":"Datum","tipo":"enlace","como_linea_rotura":true}}'
curl.exe -X POST $u @h -d '{"tool":"agregar_contorno_superficie_corredor","args":{"corredor":"Corredor 1","superficie":"Corredor 1 - Top","tipo":"talud_automatico","usar_como_exterior":true}}'
curl.exe -X POST $u @h -d '{"tool":"agregar_contorno_superficie_corredor","args":{"corredor":"Corredor 1","superficie":"Corredor 1 - Top","tipo":"exterior_poligono","poligono":"2F3A"}}'
curl.exe -X POST $u @h -d '{"tool":"agregar_linea_rotura_superficie","args":{"superficie":"Rasante","corredor":"Corredor 1","codigo":"Crown;ETW;Daylight"}}'
curl.exe -X POST $u @h -d '{"tool":"pegar_superficie","args":{"superficie_destino":"Rasante","superficie_origen":"Corredor 1 - Top"}}'
curl.exe -X POST $u @h -d '{"tool":"reconstruir_superficie","args":{"superficie":"Rasante"},"timeout_s":300}'
curl.exe -X POST $u @h -d '{"tool":"guardar_dibujo","args":{}}'
curl.exe -X POST $u @h -d '{"tool":"guardar_dibujo","args":{"como":"C:\\Proyectos\\obra_v2.dwg"}}'
curl.exe -X POST $u @h -d '{"tool":"guardar_copia","args":{"sufijo":"antes_de_objetivos"}}'
```

## Exportación (excepción: por comando, no por API)

Civil 3D no expone en `Autodesk.Civil.DatabaseServices` una API de exportación a LandXML ni a IMX. Estas dos
herramientas son la **excepción documentada**: usan el mismo núcleo que `ejecutar_comando` (sin UNDO), ponen
`FILEDIA=0` durante la orden y envían el comando seguido de la ruta. El cuadro de selección de objetos de
`LANDXMLOUT` lo tiene que completar el usuario en Civil 3D; los parámetros `alineamientos` y `superficies` son
informativos para que el agente le indique qué marcar. Ambas registran la llamada en `mcp_log.jsonl` y admiten `simular`.

| Herramienta | Parámetros | Devuelve |
|---|---|---|
| `exportar_landxml` | ruta*, alineamientos (lista `;`), superficies (lista `;`), timeout_s (300), comando (`LANDXMLOUT`) | estado del comando, ruta, `existe` (el archivo apareció o se actualizó), bytes, nota |
| `exportar_imx` | ruta*, timeout_s (300), comando (`EXPORTIMX`) | igual |

Si en tu instalación el comando de IMX tiene otro nombre, pásalo en `comando`.

## Miembros de la API en Civil 3D 2027 (.NET 10) y Reflexión

En la versión 1.2.1, todos los miembros verificados contra `AeccDbMgd.dll` y `acdbmgd.dll` de Civil 3D 2027 se llaman
de forma **directa y tipada**. La utilidad interna `herramientas-dev/InspectC3D` (.NET 10 con `MetadataLoadContext`)
permite auditar los metadatos de las DLLs sin requerir la ejecución de AutoCAD. Esa comprobación dice que el miembro
**existe**; que además **funciona** lo dice la validación en Civil 3D: el estado de cada miembro (`existe en la DLL`,
`ejecutado en Civil 3D 2027`, `por verificar`, `no existe`) está en `herramientas-dev/miembros_por_verificar_civil3d.md`
y se actualiza con el informe de cada `VALIDACION_*.md`.

`Api.cs` (reflexión) se mantiene exclusivamente como mecanismo de tolerancia en puntos donde la API pública no
expone una propiedad directa (por ejemplo, identificación del catálogo/componente de un `Subassembly`, o deducción
de lado en `AssemblyGroup`).

### Miembros verificados tipados en Civil 3D 2027

| Tipo / Elemento | Miembro en Civil 3D 2027 | Modo de llamada | Notas |
|---|---|---|---|
| `SubassemblyTargetInfo` | `AssemblyGroupName`, `SubassemblyName`, `LogicalName` | Tipado | Identificación por grupo + nombre recorriendo `Assembly.Groups → GetSubassemblyIds()` con desempate por `LogicalName`. |
| `SubassemblyTargetInfo` | `TargetIds`, `UseSameSideTarget`, `TargetToOption` | Tipado | Propiedades tipadas con getter y setter directo. |
| `CorridorSurface` | `AddLinkCode(codigo, rotura)`, `AddFeatureLineCode(codigo)` | Tipado | Sustituye a `AddPointCode` inexistente. `tipo=punto` se admite como alias. |
| `CorridorSurface` | `LinkCodes()`, `PointCodes()`, `FeatureLineCodes()` | Tipado | Métodos tipados directos para lectura de códigos. |
| `CorridorSurfaceBoundaryCollection` | `AddCorridorExtentsBoundary(nombre)`, `Add(nombre, polylineId)` | Tipado | Adición de contornos tipada; lectura con `BoundaryNames()`. |
| `BaselineRegion` | `AppliedAssemblySetting.FrequencyAlongTangents`, etc. | Tipado | Frecuencias anidadas en `AppliedAssemblySetting`. |
| `BaselineRegion` | `AdditionalStations()`, `AddStation(pk, desc)`, `Split(pk)` | Tipado | Métodos tipados directos de estación y división. |
| `TinSurface` / `Surface` | `IsOutOfDate`, `Rebuild()`, `PasteSurface(id)` | Tipado | Métodos y propiedades tipadas directas. |
| `TinSurface` | `BreaklinesDefinition.AddStandardBreaklines(...)` | Tipado | Adición tipada de líneas de rotura. |
| `Intersection` | `IntersectionRoads[i].CenterlineAlignmentId`, `Location`, `CorridorId`, `GradeRuleType` | Tipado | Acceso directo a propiedades de intersección. |
| `Assembly` | `Type.ToString()` | Tipado | Propiedad tipada `AssemblyType Type`. |
| `ProfilePVI` | `RawStation` | Tipado | Sustituye a `Station` (obsoleto CS0618 en 2027). |

## Pruebas rápidas desde PowerShell (con Civil 3D abierto)

```powershell
$t = Get-Content "$env:LOCALAPPDATA\ArbaMcp\token" -Raw
curl.exe -H "X-Arba-Token: $t" http://127.0.0.1:8765/tools
curl.exe -X POST http://127.0.0.1:8765/execute -H "X-Arba-Token: $t" -H "Content-Type: application/json" -d "{\"tool\":\"ping\"}"
curl.exe -X POST http://127.0.0.1:8765/execute -H "X-Arba-Token: $t" -H "Content-Type: application/json" -d "{\"tool\":\"listar_corredores\"}"
curl.exe -X POST http://127.0.0.1:8765/execute -H "X-Arba-Token: $t" -H "Content-Type: application/json" -d "{\"tool\":\"listar_regiones\",\"args\":{\"corredor\":\"Corredor 1\"}}"
curl.exe -X POST http://127.0.0.1:8765/execute -H "X-Arba-Token: $t" -H "Content-Type: application/json" -d "{\"tool\":\"listar_pvis\",\"args\":{\"alineamiento\":\"Eje principal\",\"perfil\":\"Rasante\"}}"
curl.exe -X POST http://127.0.0.1:8765/execute -H "X-Arba-Token: $t" -H "Content-Type: application/json" -d "{\"tool\":\"ejecutar_comando\",\"args\":{\"comando\":\"REGEN\"}}"
```

Sin token responde 401 (salvo `GET /ping`); con cabecera `Origin`, 403; `POST` sin `Content-Type: application/json`, 415.

## Pruebas

| Qué | Dónde | Cómo | Necesita Civil 3D |
|---|---|---|---|
| Núcleo del plugin (autorización, argumentos, verificación, copias, cola, lotes, reflexión) | `ArbaMcp.Pruebas` (xUnit) | `dotnet test ArbaMcp.Pruebas` | No |
| Puente (registro dinámico, timeouts, 401, JSON, `ms_puente`, `/ping`, retiradas) | `PuenteMcp/tests` (pytest) | `cd PuenteMcp; uv run --frozen pytest -q` (o `.venv\Scripts\python -m pytest`) | No (servidor falso) |
| Compilación del plugin contra sustitutos de la API | `herramientas-dev/CompilarSinCivil` | `dotnet build herramientas-dev/CompilarSinCivil` | No (solo nombres y tipos) |
| Humo en el programa real | `ArbaMcp/pruebas/probar_servidor.py` | sin argumentos (seguridad, `ejecutar_comando`, "Civil 3D ocupado"); `--dwg RUTA.dwg` (lectura, `simular`, copia nueva o reutilizada, `mcp_log.jsonl`, `deshacer_objetivos`; desde 1.3.2 la suite base sabe que `GET /ping` es público y que la copia puede reutilizarse; desde 1.3.3 la prueba 6 usa `deshacer_objetivos` y nunca una superficie de corredor); `--fase 13` (lo nuevo de 1.3.x: `/ping` sin token, 401 agrupados, `ms_espera`, `ms_puente`, lotes, `deshacer_objetivos` sobre el lote, `_.UNDO 1` sobre las frecuencias, `copia.reutilizada`, rechazo de la superficie del propio corredor); `--fase todo` | Sí |
| Validación documentada | `herramientas-dev/VALIDACION_122.md`, `VALIDACION_13.md` | prompts para el agente local, con informe OK/FALLO | Sí |

La integración continua (`.github/workflows/pruebas.yml`) ejecuta las tres primeras en cada PR y push a `main`. El
plugin `ArbaMcp` no se compila en CI porque referencia las DLL de una instalación local de Civil 3D 2027.

## Añadir herramientas desde otro plugin de la pestaña ARBA

Cualquier plugin que referencie `ArbaMcp.dll` **y** `ArbaMcp.Nucleo.dll` puede registrar sus propias herramientas
desde su `Initialize` (desde 1.3.0 `ContextoEjecucion` vive en `ArbaMcp.Nucleo`; `ArbaMcp.Parametro` se mantiene como
alias de `ArbaMcp.Nucleo.Parametro`; los tipos válidos son `string`, `number`, `boolean` y `json`):

```csharp
ArbaMcp.Herramientas.Registrar(new ArbaMcp.Herramienta
{
    Nombre = "mi_herramienta",
    Descripcion = "...",
    Parametros = { new ArbaMcp.Parametro { name = "x", type = "number", description = "...", required = true } },
    Ejecutar = args => new { resultado = 1 },  // se ejecuta en el hilo principal, en el contexto de comando del dibujo activo
    // Contexto = ArbaMcp.Nucleo.ContextoEjecucion.Inmediato   // solo si no toca el dibujo y debe responder aunque Civil 3D esté ocupado
});
```

El puente las verá en `GET /tools` sin cambios en Python.
