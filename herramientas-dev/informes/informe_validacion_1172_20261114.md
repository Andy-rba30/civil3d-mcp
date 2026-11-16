# Informe de Validación 1.17.2 (en rama `tarea/ronda-b`, v1.17.4)

**Fecha:** 2026-11-14  
**Rama / Commit:** `tarea/ronda-b` (`b0287f8`)  
**Versión de `ping`:** `1.17.4`  
**Civil 3D:** 2027 (Español)  
**DWG usado:** `PRUEBA_1172.dwg` (copia nueva de `PRUEBA_EVALUACION_BASE.dwg`)  
**Herramientas MCP `civil3d`:** 58  

---

## 1. Tabla de Resultados por Paso

| Paso | Herramienta | Resultado | Tiempo | Respuesta literal / Detalle |
|---|---|---|---|---|
| **0** | `ping` + `listar_superficies` | **OK** | `7 ms` / `11 ms` | `version: "1.17.4"`, `listar_superficies` → `[]`; `14` archivos en `backups\` |
| **1** | `crear_superficie_tin` (`simular: true`, 1 y 5, sin estilo) | **OK** | `50 ms` (`ms: 41`) | `simulado: true`, `despues.estilo: "Contours 1m and 5m (Background)"`, `puntos: 9`, `aplicados: true`, `eleccion: "automatica"`, `curvas_visibles: true`, `estilo_menor: 1`, `estilo_mayor: 5`, `tolerancia: 0.001` |
| **2** | `crear_superficie_tin` (real, 1 y 5, sin estilo) | **OK** | `65 ms` (`ms: 56`) | `simulado: false`, `despues.estilo: "Contours 1m and 5m (Background)"`, `puntos: 9`, `triangulos: 8`, `copia.estado: "terminada"` (`PRUEBA_1172_20261114_224849_crear_superficie_tin.dwg`). **Aviso:** `No se pudo releer el estilo de la superficie creada (Entity.StyleId): 'intervalos_curvas' usa el estilo previsto.` |
| **3** | `listar_superficies` | **OK** | `9 ms` (`ms: 2`) | `Terreno` (`TinSurface`, `TIN`). *(Nota: pendiente confirmación visual de Jose en pantalla en Civil 3D, aunque la sesión continuó de corrido por indicación del usuario)* |
| **4** | `crear_superficie_tin` (`estilo: "Contours 5m and 25m (Background)"`) | **OK** | `81 ms` (`ms: 75`) | `despues.estilo: "Contours 5m and 25m (Background)"`, `aplicados: false`, `eleccion: "indicada"`, `estilo_menor: 5`, `estilo_mayor: 25`, `copia.reutilizada: true` |
| **5** | `crear_superficie_tin` (0.37 y 3.7, `simular: true`) | **OK** | `10 ms` (`ms: 4`) | `despues.estilo: "(estilo por defecto del dibujo)"`, `aplicados: false`, `eleccion: "por_defecto"`, `estilo: "Contours 2m and 10m (Background)"`, `estilo_menor: 2`, `estilo_mayor: 10`, `otros_que_coinciden: []` |
| **6** | `crear_superficie_tin` (1 y 5, `simular: true`) + `leer_historial` | **OK** | `10 ms` / `7 ms` | Misma elección que Paso 1 (`Contours 1m and 5m (Background)`); `leer_historial` sin líneas de `lectura parcial` ni `no se pudo leer el estilo de superficie por defecto` |

---

## 2. Listas del Paso 1

- **Estilo por defecto del dibujo:** `Contours 2m and 10m (Background)` (familia `Background`).
- **`otros_que_coinciden`:** `["Contours 1m and 5m (Background)", "Contours 1m and 5m (Design)"]` → en Paso 1: `["Contours 1m and 5m (Design)"]`.
- **`descartados_sin_curvas_visibles`:** `[]`.

---

## 3. Miembros de la API (Sección 1.17.2)

- **Pasan a `ejecutado en Civil 3D 2027` (2026-11-14):**
  - `GetSettings<SettingsSurface>().Styles` (`SurfaceStyleId`, `Surface`).
  - `SurfaceStyle.GetDisplayStylePlan(SurfaceDisplayStyleType.MinorContour / MajorContour)` → `DisplayStyle.Visible`.
  - `TinSurface.Create(string, ObjectId styleId)`.
- **Nota / Hallazgo menor (Paso 2 y Paso 4):**
  - Al releer la superficie creada tras `TinSurface.Create(nombre, idEstilo)`, salió el aviso: `No se pudo releer el estilo de la superficie creada (Entity.StyleId): 'intervalos_curvas' usa el estilo previsto.`
