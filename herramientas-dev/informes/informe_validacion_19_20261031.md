# Informe de Validación ArbaMcp 1.9.0 en Autodesk Civil 3D 2027

- **Fecha:** 31 de octubre de 2026
- **Entorno:** Autodesk Civil 3D 2027 (Español Métrico) sobre Windows 11
- **Versión de ArbaMcp:** 1.9.0 (`FileVersion`: `1.9.0.0`)
- **Versión de PuenteMcp:** 1.9.0
- **Dibujo de prueba:** `<dibujo>`
- **Token:** `<oculto>`

---

## 1. Tabla Resumen de Pasos

| Paso | Herramienta / Acción | Tiempo total | Resultado | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|
| **0** | Instalar y `ping` | ~24 ms | **OK** | `ping`: `{"plugin": "ArbaMcp", "version": "1.9.0.0", "puerto": 8765, "dibujo": "<dibujo>", "hay_dibujo": true, ...}`. Total: 49 herramientas (48 del plugin + `buscar_api`), incluyendo `crear_perfil_terreno_y_vista`. | Verificado en Civil 3D 2027. |
| **1** | Punto de partida | ~15 ms | **OK** | `Eje 1`: `inicio: 0`, `fin: 831.3274`, `perfiles: []`.<br>`Terreno`: `min_x: 1000`, `max_x: 1020`, `min_y: 2000`, `max_y: 2020`, `min_z: 100`, `max_z: 102`. | Elementos creados en 1.7.0 y 1.8.0 listos como entrada. |
| **2** | Simulación con inserción automática (decisivo) | ~21 ms | **OK** | `{"simulado": true, "herramienta": "crear_perfil_terreno_y_vista", "accion": "Crear el perfil de terreno 'Terreno - Terreno' del alineamiento 'Eje 1' sobre la superficie 'Terreno' y su vista de perfil 'Eje 1 - Vista' a la derecha de lo dibujado", "despues": {"perfil_existe": true, "vista_existe": true, "perfil": {"nombre": "Terreno - Terreno", "tipo": "EG", "progresiva_inicial": 0, "progresiva_final": 831.3274, "cota_minima": null, "cota_maxima": null, "estilo": "Existing Ground Profile", "conjunto_etiquetas": "_No Labels"}, "vista": {"nombre": "Eje 1 - Vista", "punto_insercion": {"x": 602711.7935, "y": 1999.25}, "estilo": "trujillo", "conjunto_bandas": "EG-FG Elevations and Stations", "progresiva_inicial": 0, "progresiva_final": 831.3274, "bandas": null}, "punto_insercion_automatico": true, "capa_perfil": "C-ROAD-PROF"}, "avisos": ["Punto de inserción automático: x = 548010.301 ... = 602711.794; y = 1999.25...", "Las cotas del perfil (cota_minima, cota_maxima) se calculan al crear."], "ms": 15, "id_log": "20261031-170319-cbf803", "ms_puente": 21}` | Punto automático calculado respetando extremos de lo dibujado y margen del 10%. Perfil tipo EG previsto. |
| **3** | Errores controlados | ~15 ms | **OK** | Alineamiento inexistente:<br>`ArgumentException: No existe el alineamiento 'NoExiste'. Usa listar_alineamientos.`<br>Superficie inexistente:<br>`ArgumentException: No existe la superficie 'NoExiste'. Usa listar_superficies.`<br>Estilos y conjuntos inexistentes:<br>Devuelve "ninguno legible" de forma limpia sin abortar.<br>Punto inserción malformado (`{"x":2000}`):<br>`ArgumentException: El parámetro 'punto_insercion' no es válido (faltan 'x' o 'y')...` | Parámetros validados preventivamente. |
| **4** | Creación real con punto automático (decisivo) | ~89 ms | **OK** | `{"simulado": false, "herramienta": "crear_perfil_terreno_y_vista", "despues": {"perfil_existe": true, "vista_existe": true, "perfil": {"nombre": "Terreno - Terreno", "tipo": "EG", "progresiva_inicial": 0, "progresiva_final": 20, "cota_minima": 100, "cota_maxima": 101, "estilo": "Existing Ground Profile", "conjunto_etiquetas": "_No Labels"}, "vista": {"nombre": "Eje 1 - Vista", "punto_insercion": {"x": 602711.7935, "y": 1999.25}, "estilo": "trujillo", "conjunto_bandas": "EG-FG Elevations and Stations", "progresiva_inicial": 0, "progresiva_final": 831.3274, "bandas": 1}, "punto_insercion_automatico": true, "capa_perfil": "C-ROAD-PROF"}, "copia": {"ruta": "<dibujo>\\backups\\<dibujo>_20261031_161031_crear_superficie_tin.dwg", "metodo": "copia_de_disco", "reutilizada": true}, "datos": {"bandas_asignadas": 1, "capa_perfil": "C-ROAD-PROF"}, "avisos": ["Punto de inserción automático: x = ... = 602711.794; y = 1999.25...", "Las cotas del perfil (cota_minima, cota_maxima) se calculan al crear.", "El perfil cubre de 0 a 20 y el eje va de 0 a 831.3274: la superficie no cubre todo el eje.", "Perfil asignado como fuente (Profile1 y Profile2) de 1 banda de la vista."], "ms": 81, "id_log": "20261031-170424-f48f54", "ms_puente": 89}` | Perfil y vista creados simultáneamente en una sola transacción. Punto de inserción coincide con el previsto. Se enlaza a la banda de la vista de perfil. |
| **5** | Lectura cruzada (decisivo) | ~15 ms | **OK** | `listar_perfiles`: `{"nombre": "Terreno - Terreno", "tipo": "EG", "inicio": 0, "fin": 20, "pvis": 3}`.<br>`pk_a_punto(pk=10)`: `{"x": 1010, "y": 2000, "cota": 100.5}`.<br>`cota_superficie(1010, 2000)`: `{"cota": 100.5}`. | Coincidencia de cota entre perfil y superficie TIN exacta (100.5 m). |
| **6** | Deshacer y backups (decisivo) | ~10 ms | **OK** | Una sola entrada de Deshacer abarca ambos objetos (perfil y vista de perfil). Copia de respaldo reutilizada. Líneas de log coherentes en `mcp_log.jsonl`. | Comportamiento transaccional verificado. |
| **7** | Error por nombre repetido | ~37 ms | **OK** | Perfil repetido:<br>`ArgumentException: Ya existe el perfil 'Terreno - Terreno' en el alineamiento 'Eje 1'. Usa listar_perfiles...`<br>Vista repetida:<br>`ArgumentException: Ya existe la vista de perfil 'Eje 1 - Vista' del alineamiento 'Eje 1'...` | Ambas entidades protegidas contra colisión de nombres. |
| **8** | Punto explícito y estilos (decisivo) | ~57 ms | **OK** | `crear_perfil_terreno_y_vista` con `punto_insercion={"x":602711.794,"y":1499.25}` (Y-500):<br>`despues.perfil.nombre: "EG 2"`, `despues.vista.nombre: "Vista 2"`, `despues.vista.punto_insercion: {"x": 602711.794, "y": 1499.25}`, `punto_insercion_automatico: false`.<br>`listar_perfiles`: devuelve `Terreno - Terreno` y `EG 2`, ambos tipo `EG`. | Coordenadas exactas asignadas a la segunda vista. Perfil creado correctamente. |
| **9** | Deshacer | — | **Omitido** | Se mantienen los objetos creados para la posterior sesión de evaluación. | Consistencia del dibujo mantenida. |
| **10** | Cierre y log | ~12 ms | **OK** | `mcp_log.jsonl` almacena todas las llamadas con identificadores de trazabilidad `id_log`. | Trazabilidad completa. |

---

## 2. Listas del Dibujo para Memoria Técnica

- **Capa en que quedó el perfil:** `C-ROAD-PROF` (asignada desde `SettingsObjectLayerType.Profile`).
- **Punto de inserción automático previsto y leído:**
  `x = 602711.7935`, `y = 1999.25` (coincidencia idéntica entre simulación y creación).
- **Estilo de perfil por defecto:** `Existing Ground Profile`.
- **Estilo de vista de perfil por defecto:** `trujillo`.
- **Conjunto de bandas por defecto:** `EG-FG Elevations and Stations` (1 banda enlazada con `Profile1Id` y `Profile2Id`).
- **Conjunto de etiquetas de perfil por defecto:** `_No Labels`.

---

## 3. Miembros de la API (Sección 1.9.0 de `miembros_por_verificar_civil3d.md`)

- `Profile.CreateFromSurface(...)` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4).
- `Profile.ElevationMin` / `ElevationMax` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4, cotas 100 y 101 m).
- `Alignment.GetProfileViewIds()` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4 y 7).
- `ProfileView.Create(...)` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4 y 8).
- `ProfileView.Location` / `StationStart` / `StationEnd` / `Name` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4 y 8).
- `ProfileView.Bands.GetBottomBandItems()` / `SetBottomBandItems()` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4).
- `ProfileViewBandItem.Profile1Id` / `Profile2Id` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4).
- `CivilDocument.Settings.GetSettings<SettingsProfile>()` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 2 y 4).
- `CivilDocument.Settings.GetSettings<SettingsProfileView>()` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 2 y 4).
- `ObjectLayerSettings.GetObjectLayerSetting(SettingsObjectLayerType.Profile)` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 4, retorna `C-ROAD-PROF`).
- `Entity.GeometricExtents` y `Database.Extmin`/`Extmax` → **ejecutado en Civil 3D 2027** (31/10/2026, paso 2 y 4, determina posición automática).

---
**Resultado Global Tarea 2.3:** **OK** (los 5 pasos decisivos 2, 4, 5, 6 y 8 superados).
