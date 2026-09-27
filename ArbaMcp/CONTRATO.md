# Contrato del servidor local para el puente MCP

El plugin **ArbaMcp** abre, al cargarse en Civil 3D, un servidor HTTP mínimo que escucha **solo en 127.0.0.1**.
No usa http.sys ni necesita permisos de administrador.

- Puerto: variable de entorno `ARBA_MCP_PORT` (por defecto **8765**). `ARBA_MCP=0` desactiva el servidor.
- Comando `ARBAMCP` (botón *Conexión IA* de la pestaña ARBA): muestra si está activo, el puerto y las últimas líneas del historial.
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
| `Documento` (por defecto) | Contexto de comando del dibujo activo (`DocumentManager.ExecuteInCommandContextAsync`): la herramienta corre como un comando más, con el documento bloqueado y los gráficos actualizados al terminar | Sí | Todas las de lectura y escritura del dibujo |
| `Aplicacion` | Contexto de aplicación en el hilo principal | Sí | `abrir_dibujo`, el envío de `ejecutar_comando` y de las exportaciones |
| `Inmediato` | Contexto de aplicación, sin esperar y por delante de los demás trabajos | No | `ping`, `leer_historial`, `leer_log`, `leer_variable`, `capturar_pantalla`, los ESC de `ejecutar_comando` |

"Libre" significa `CMDACTIVE = 0` y ventana principal habilitada (sin cuadro de diálogo modal). Los trabajos
`Documento` y `Aplicacion` se ejecutan de uno en uno; mientras uno espera, el historial anota
`MCP ⏳ <herramienta> espera: <motivo>`. Detalle y comprobaciones en `ESTABILIDAD.md`.

## Seguridad (desde la versión 1.1.0)

Toda petición debe cumplir estas reglas; si no, se rechaza antes de procesar nada:

| Regla | Si falla |
|---|---|
| Cabecera `X-Arba-Token` con el token de la sesión (32 bytes aleatorios del generador criptográfico, en hexadecimal). El plugin lo genera al iniciar y lo guarda en `%LOCALAPPDATA%\ArbaMcp\token` (permisos solo para tu usuario). Cambia en cada arranque de Civil 3D. Aplica a todas las rutas, `/ping` incluida | 401 |
| Sin cabecera `Origin` (evita llamadas desde navegadores) | 403 |
| Cabecera `Host` exactamente `127.0.0.1:<puerto>` o `localhost:<puerto>` | 400 |
| En `POST`, `Content-Type: application/json` | 415 |

El puente Python lee el token del archivo al arrancar y lo vuelve a leer si recibe 401.

## Rutas

| Método | Ruta | Descripción |
|---|---|---|
| GET | `/ping` | `{"ok":true,"servidor":"ArbaMcp","puerto":8765}` |
| GET | `/tools` | Lista de herramientas con nombre, descripción y parámetros (nombre, tipo, descripción, requerido) |
| POST | `/execute` | Ejecuta una herramienta. Cuerpo: `{"tool":"nombre","args":{...},"timeout_s":120}` |

Respuesta de `/execute`:

```json
{"ok": true,  "tool": "ping", "ms": 3, "result": { ... }}
{"ok": false, "error": "mensaje"}
```

Los tipos de parámetro son `string`, `number` o `boolean`. El puente debe leer `/tools` al arrancar y
registrar cada herramienta de forma dinámica, de modo que al añadir herramientas en C# no haya que tocar Python.
El puente pasa `timeout_s` según la herramienta: 30 s en lectura, 120 s en escritura y 300 s en
`reconstruir_corredor`, `reconstruir_superficie`, `exportar_landxml` y `exportar_imx`.

## Seguridad de escritura (desde la versión 1.2.0)

Toda herramienta de escritura pasa por `Escritura.Ejecutar` (`Escritura.cs`), que aplica estas reglas antes y después del cambio:

1. **Comprobaciones previas**: responde error, sin tocar nada, si `CMDACTIVE > 0` (hay un comando activo), si el dibujo está en solo lectura o si el mismo archivo está abierto más de una vez en Civil 3D.
2. **Copia de seguridad por minuto**: antes del primer cambio de cada minuto guarda `<carpeta del dwg>\backups\<nombre>_<yyyyMMdd_HHmmss>_<herramienta>.dwg` con `SaveAs(ruta, false, DwgVersion.Current, SecurityParameters)`: el dibujo activo no cambia de nombre. Conserva las últimas 20 copias de cada dibujo y borra las anteriores. Si el dibujo nunca se guardó, la carpeta es `%LOCALAPPDATA%\ArbaMcp\backups`.
3. **Registro por llamada**: cada escritura añade una línea a `<carpeta del dwg>\mcp_log.jsonl` con `{"hora","herramienta","args","ok","ms","error"}` y otra al historial (`leer_historial`). `leer_log` devuelve las últimas líneas ya interpretadas.
4. **Verificación antes/después**: tras escribir, la herramienta vuelve a leer el objeto y responde `{"simulado":false,"cambios":[...],"antes":{...},"despues":{...},"copia":"..."}` con los campos que cambiaron. Si el dibujo no refleja el cambio pedido, responde `ok=false` con la explicación (y el estado leído) y **no reintenta**.
5. **Nada se borra ni se recrea**: ninguna herramienta elimina regiones, líneas base, ensamblajes ni superficies, ni usa `Erase()`.
6. **`simular`** (boolean, por defecto `false`): con `true` responde `{"simulado":true,"accion":"...","antes":{...},"despues":{...}}` con lo que haría, sin tocar el dibujo (tampoco hace copia; sí registra la llamada en el log).

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
| `asignar_objetivo` | corredor*, linea_base*, region*, subensamblaje*, tipo* (superficie/elevacion/desplazamiento), objetivo* (nombre del objeto o `ninguno`), alineamiento_del_perfil, opcion (mas_cercano/exterior/interior), mismo_lado, grupo, parametro | `region.GetTargets()` → modifica el `SubassemblyTargetInfo` que coincide por `SubassemblyName` y tipo → `region.SetTargets(...)`. Con varios subensamblajes del mismo nombre exige `grupo` (y lista las opciones); con varios objetivos del mismo tipo exige `parametro`. El objetivo se resuelve como superficie, eje, perfil (con `alineamiento_del_perfil`), línea característica o handle de polilínea |
| `asignar_objetivos_superficie` | corredor*, superficie*, linea_base, region | Pone la superficie en **todos** los objetivos de tipo superficie del ámbito |
| `dividir_region` | corredor*, linea_base*, region*, pk* | Si la API tiene `Split` lo usa; si no, recorta la región hasta `pk` y añade otra (`BaselineRegions.Add`) con el mismo ensamblaje desde `pk` hasta el fin anterior, copiando frecuencias y objetivos |
| `establecer_rango_region` | corredor*, linea_base*, region*, inicio, fin | Cambia `StartStation`/`EndStation`; falla si solapa con otra región o sale de la línea base |
| `establecer_frecuencia` | corredor*, linea_base*, region*, tangentes, curvas, espirales, perfil | Cambia las frecuencias indicadas |
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
permite auditar los metadatos de las DLLs sin requerir la ejecución de AutoCAD.

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

Sin token responde 401; con cabecera `Origin`, 403; `POST` sin `Content-Type: application/json`, 415.
El script `pruebas\probar_servidor.py` ejecuta estas comprobaciones de una vez; con `--dwg RUTA.dwg` abre ese
dibujo (con al menos un corredor) y comprueba las herramientas de lectura, `simular`, la copia de seguridad,
`ejecutar_comando` sin UNDO y `mcp_log.jsonl`.

## Añadir herramientas desde otro plugin de la pestaña ARBA

Cualquier plugin que referencie `ArbaMcp.dll` puede registrar sus propias herramientas desde su `Initialize`:

```csharp
ArbaMcp.Herramientas.Registrar(new ArbaMcp.Herramienta
{
    Nombre = "mi_herramienta",
    Descripcion = "...",
    Parametros = { new ArbaMcp.Parametro { name = "x", type = "number", description = "...", required = true } },
    Ejecutar = args => new { resultado = 1 },  // se ejecuta en el hilo principal, en el contexto de comando del dibujo activo
    // Contexto = ArbaMcp.ContextoEjecucion.Inmediato   // solo si no toca el dibujo y debe responder aunque Civil 3D esté ocupado
});
```

El puente las verá en `GET /tools` sin cambios en Python.
