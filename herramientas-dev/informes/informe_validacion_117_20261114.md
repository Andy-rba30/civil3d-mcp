# Informe de Validación 1.17.0 (en rama `tarea/ronda-b`, v1.17.4)

**Fecha:** 2026-11-14  
**Rama / Commit:** `tarea/ronda-b` (`b0287f8`)  
**Versión de `ping`:** `1.17.4`  
**Civil 3D:** 2027 (Español)  
**DWG usado:** `PRUEBA_116.dwg` (copia de `PRUEBA_EVALUACION_BASE.dwg`)  
**Herramientas MCP `civil3d`:** 58 (55 del plugin + `buscar_api`, `guardar_receta`, `ejecutar_receta`)  

---

## Parte A (Pasos 0 a 9)

| Paso | Herramienta | Resultado | Tiempo | Respuesta literal / Detalle |
|---|---|---|---|---|
| **0** | `ping` + env | **OK** | `23 ms` | `version: "1.17.4"`, `hay_dibujo: true`; 58 herramientas; `ARBA_MCP_CODIGO=1`, `MEMORIA_FIRMA_DIR=C:\Proyectos\memoria-firma` |
| **1** | `Test-Path` + `buscar_api` | **OK** | `—` | `False`, `False`; `buscar_api "crear una capa con color" → 0 recetas, 0 entradas del glosario, 10 del catálogo (límite 10).` |
| **2 (sim)** | `ejecutar_codigo` | **OK** | `402 ms` | 1 intento; `simulado: true`, `resultado: {"capa": "MCP Receta A", "color": 3, "creada": true}`, `id_log: 20261114-223415-37840f` |
| **2 (real)** | `ejecutar_codigo` + `aprobar_resultado` | **OK** | `37 ms` / `14 ms` | `simulado: false`, `resultado: {"capa": "MCP Receta A", "color": 3, "creada": true}`, `id_log: 20261114-223422-a2d29e`; aprobado por Jose (`"si, la capa quedó bien"`) |
| **3** | `guardar_receta` (vista previa) | **OK** | `—` | `{"ok": true, "guardada": false, "reemplazaria": false, ...}`; `Test-Path` → `False`, `False` |
| **4** | `guardar_receta` (sin palabras) | **OK** | `—` | `{"ok": false, "error": "Falta 'aprobacion_del_dueno': ...", "guardada": false}`; `Test-Path` → `False`, `False` |
| **5** | `guardar_receta` (`..\fuera`) | **OK** | `—` | `{"ok": false, "error": "El nombre de receta '..\\fuera' no vale: ...", "guardada": false}`; `Test-Path` → `False`, `False` |
| **6a** | `guardar_receta` (token 64 `a`) | **OK** | `—` | `{"ok": false, "error": "'codigo' contiene un texto de 64 caracteres hexadecimales que parece un token (<oculto>). ...", "guardada": false}` |
| **6b** | `guardar_receta` (ruta en notas) | **OK** | `—` | `{"ok": false, "error": "'notas' lleva una ruta absoluta (C:\\..., \\\\servidor\\...). ...", "guardada": false}`; `Test-Path prueba_rechazos.yaml` → `False` |
| **7** | `guardar_receta` (con aprobación) | **OK** | `—` | Aprobación pedida a Jose (`"si, guardala como receta"`); `{"ok": true, "guardada": true, "reemplazada": false, "nombre": "crear_capa_con_color", "veces_usada": 0, ...}`; `LastWriteTime`: `2026-11-14T22:37:49.4297773-05:00`; recordatorio L.2 entregado |
| **8** | `guardar_receta` (no sobrescribe) | **OK** | `—` | `{"ok": false, "error": "Ya existe la receta 'crear_capa_con_color' (..., veces_usada 0). No se sobrescribe sin decirlo: ...", "guardada": false}`; `LastWriteTime` sin cambios |
| **9** | `buscar_api` | **OK** | `—` | `buscar_api "crear una capa con color" → 1 recetas, 0 entradas del glosario, 9 del catálogo (límite 10).` Primera línea de RECETAS: `crear_capa_con_color.yaml + crear_capa_con_color.cs` |

### Script del Paso 2 (`crear_capa_con_color.cs`)

Lectura de parámetros: `F.Texto("nombre_capa")` y `(short)Math.Round(F.Numero("color"))`.

```csharp
var nombre = F.Texto("nombre_capa");
var color = (short)Math.Round(F.Numero("color"));
var lt = F.Abrir<LayerTable>(F.Db.LayerTableId, true);
bool creada = !lt.Has(nombre);
if (creada)
{
    var ltr = new LayerTableRecord();
    ltr.Name = nombre;
    ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, color);
    lt.Add(ltr);
    F.Tr.AddNewlyCreatedDBObject(ltr, true);
}
return new { capa = nombre, color = (int)color, creada };
```

---

## L.2 (Paso 10)

- **Resultado:** **OK**
- **Commit en `C:\Proyectos\memoria-firma`:** `5456524 2026-11-14 Sincronización 2026-11-14 22:38`
- **Archivos subidos:** `recetas/crear_capa_con_color.cs`, `recetas/crear_capa_con_color.yaml`

```text
==> Buscando cambios en: catalogo, recetas, normas, casos, evaluacion, dataset
    Archivos con cambios: 2
      - recetas/crear_capa_con_color.cs
      - recetas/crear_capa_con_color.yaml
    To https://github.com/Andy-rba30/memoria-firma
       828bccf..5456524  main -> main
Sincronización terminada correctamente.
5456524 2026-11-14 Sincronización 2026-11-14 22:38
```

---

## Parte B (Pasos 11 a 16)

| Paso | Herramienta | Resultado | Tiempo | Respuesta literal / Detalle |
|---|---|---|---|---|
| **11** | `ping` + `buscar_api` + `ejecutar_receta` (sim y real) | **OK** | `7 ms` / `—` / `39 ms` / `43 ms` | `buscar_api "crear capa color"` → 1 recetas (`crear_capa_con_color` en posición 1). Simulación (`id_log: 20261114-223945-f3bfdc`): `resultado: {"capa": "MCP Receta B", "color": 3, "creada": true}`, `receta: {"veces_usada": 0, "sumada": false, "nota": "Simulación: veces_usada no cambia; ..."}`. Confirmación pedida a Jose (`"si"`). Real (`id_log: 20261114-224010-ee4718`): `resultado: {"capa": "MCP Receta B", "color": 3, "creada": true}`, `receta: {"veces_usada": 1, "sumada": true, "nota": "Ejecución real correcta: veces_usada sube a 1. El contador está en esta PC; L.2 lo sube a GitHub."}` |
| **12** | Registro de herramientas (Paso 11) | **OK** | `—` | Orden: 1) `ping` (`args={"pedido": "Necesito una capa nueva que se llame MCP Receta B, en verde."}`, `id_log: 20261114-223929-043608`); 2) `buscar_api` (`{"consulta": "crear capa color", "limite": 10}`); 3) `ejecutar_receta` (`{"nombre": "crear_capa_con_color", "parametros": {"color": 3, "nombre_capa": "MCP Receta B"}, "pedido": "Necesito una capa nueva que se llame MCP Receta B, en verde.", "simular": true}`, `id_log: 20261114-223945-f3bfdc`); 4) `ejecutar_receta` (`{"nombre": "crear_capa_con_color", "parametros": {"color": 3, "nombre_capa": "MCP Receta B"}, "pedido": "Necesito una capa nueva que se llame MCP Receta B, en verde.", "simular": false}`, `id_log: 20261114-224010-ee4718`) |
| **13** | `ejecutar_receta` (color por defecto) | **OK** | `52 ms` | `resultado: {"capa": "MCP Receta C", "color": 7, "creada": true}`, `receta: {"nombre": "crear_capa_con_color", "parametros": {"nombre_capa": "MCP Receta C", "color": 7}, "veces_usada": 2, "sumada": true, "nota": "Ejecución real correcta: veces_usada sube a 2. El contador está en esta PC; L.2 lo sube a GitHub."}`, `id_log: 20261114-224221-603d24` |
| **14** | `ejecutar_receta` (`colour`) + `leer_log` (`ultimas_n: 3`) | **OK** | `7 ms` / `9 ms` | `{"ok": false, "error": "La receta no tiene los parámetros: colour. Parámetros de la receta: nombre_capa (string, obligatorio), color (number, por defecto 7).", "ms_puente": 7}` (sin `id_log`). `leer_log` (`id_log: 20261114-224230-92f452`, `ruta: "<ruta>"`) devuelve las 3 líneas `20261114-223945-f3bfdc`, `20261114-224010-ee4718`, `20261114-224221-603d24`; ninguna menciona `MCP Receta D`. |
| **15** | `leer_log` (`ultimas_n: 8`) | **OK** | `7 ms` | `id_log: 20261114-224232-44b729` (`ruta: "<ruta>"`). Las ejecuciones reales de los pasos 11 (`20261114-224010-ee4718`) y 13 (`20261114-224221-603d24`) aparecen como `ejecutar_codigo` con `ok: true`, primera línea `var nombre = F.Texto("nombre_capa");` y sus `pedido` literales (`"Necesito una capa nueva que se llame MCP Receta B, en verde."` y `"Crea también la capa MCP Receta C con el color por defecto"`). |
| **16** | `Select-String` en `crear_capa_con_color.yaml` | **OK** | `—` | `veces_usada: 2` y `ultima_vez: '2026-11-14'` |

### Notas de la Parte B (Memoria técnica)

- **Paso 11 (`buscar_api`):** consulta `"crear capa color"`; la receta `crear_capa_con_color` salió en posición 1 de `RECETAS`; se pidió confirmación a Jose entre la simulación (`simular=true`) y la ejecución real (`simular=false`).
- **Comparativa de tiempos (`ms_puente`):** `ejecutar_receta` simulada (`39 ms`) y real (`43 ms` en paso 11; `52 ms` en paso 13) frente a `ejecutar_codigo` directo en paso 2 (`402 ms` sim / `37 ms` real).

---

## Parte C (Pasos 17 a 20)

| Paso | Herramienta | Resultado | Tiempo | Respuesta literal / Detalle |
|---|---|---|---|---|
| **17** | `buscar_api.py "capa con color"` | **OK** | `—` | Código salida `0`; sección `RECETAS (1)` primero con `crear_capa_con_color.yaml + crear_capa_con_color.cs` y `veces usada 2` |
| **18** | PowerShell + reinicio Civil 3D | **OK** | `22 ms` (`ping`) | `ARBA_MCP_CODIGO` eliminada (`Test-Path Env:ARBA_MCP_CODIGO` → `False`); Civil 3D reabierto desde menú Inicio, `ping` `version: "1.17.4"`, `hay_dibujo: true` |
| **19a** | `ejecutar_codigo` (desactivada) | **OK** | `10 ms` | `{"ok": false, "error": "Error de Civil 3D: InvalidOperationException: ejecutar_codigo está desactivada: se activa con la variable de entorno de usuario ARBA_MCP_CODIGO=1 y reiniciando Civil 3D, y eso lo decide el dueño del equipo. No se compiló ni se ejecutó nada: sin copia de seguridad y sin entrada en Deshacer. Usa las herramientas específicas o dile al usuario que la herramienta está desactivada."}` |
| **19b** | `ejecutar_receta` (desactivada) | **OK** | `13 ms` | `{"ok": false, "error": "Error de Civil 3D: InvalidOperationException: ejecutar_codigo está desactivada: ...", "receta": {"nombre": "crear_capa_con_color", "parametros": {"nombre_capa": "MCP Receta E", "color": 7}, "veces_usada": 2, "sumada": false, "nota": "La ejecución falló: veces_usada no cambia."}}`; `veces_usada` sigue en `2` |
| **20** | `sincronizar_memoria.ps1` (L.2 final) | **OK** | `—` | Commit `7083d1e 2026-11-14 Sincronización 2026-11-14 22:47`, archivo `recetas/crear_capa_con_color.yaml` |

**Resultado global VALIDACION_117:** **OK** (todos los pasos decisivos `0, 3, 4, 7, 10, 11, 13, 14, 17, 19` superados).
