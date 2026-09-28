# Informe de Validación ArbaMcp 1.3.3 en Autodesk Civil 3D 2027

- **Fecha:** 28 de septiembre de 2026
- **Entorno:** Autodesk Civil 3D 2027 (Español Métrico) sobre Windows 11
- **Rama:** `claude/vibrant-ptolemy-79gan9` (commit `edbff11`)
- **Estado de instalación:** **FALLO EN COMPILACIÓN (Paso 0)**. La versión 1.3.2 permanece instalada en el sistema.

---

## 1. Tabla de Pasos

| Paso | Herramienta | Tiempo total del paso | `ms` / `ms_espera` / `ms_ejecucion` / `ms_puente` | Resultado | Respuesta literal (recortada a 40 líneas) | Observaciones |
|---|---|---|---|---|---|---|
| **0** | Instalación de la 1.3.3 (`instalar.ps1`) | ~6 s | N/A | **FALLO** | `C:\IA\civil3d-mcp\ArbaMcp\Herramientas.Corredores.cs(1301,109): error CS0234: El tipo o el nombre del espacio de nombres 'Serializar' no existe en el espacio de nombres 'Json' (¿falta alguna referencia de ensamblado?)`<br>`C:\IA\civil3d-mcp\ArbaMcp\Herramientas.cs(99,49): error CS0234: El tipo o el nombre del espacio de nombres 'Serializar' no existe en el espacio de nombres 'Json' (¿falta alguna referencia de ensamblado?)` | Error de compilación en `ArbaMcp.csproj`. `ArbaMcp.Nucleo` compila correctamente. La validación se detiene aquí según la directiva del prompt: la 1.3.2 sigue instalada en el bundle. |
| **1-21** | Pasos restantes | N/A | N/A | **no probado** | N/A | Detenido en paso 0 por fallo de compilación. |

---

## 2. Detalle del Error de Compilación (Paso 0)

Salida literal del comando `powershell -ExecutionPolicy Bypass -File .\ArbaMcp\instalar.ps1`:

```
Compilando (Release)...
  Determinando los proyectos que se van a restaurar...
  Se ha restaurado C:\IA\civil3d-mcp\ArbaMcp.Nucleo\ArbaMcp.Nucleo.csproj (en 134 ms).
  Se ha restaurado C:\IA\civil3d-mcp\ArbaMcp\ArbaMcp.csproj (en 142 ms).
  ArbaMcp.Nucleo -> C:\IA\civil3d-mcp\ArbaMcp.Nucleo\bin\Release\net10.0\ArbaMcp.Nucleo.dll
C:\IA\civil3d-mcp\ArbaMcp\Herramientas.Corredores.cs(1301,109): error CS0234: El tipo o el nombre del espacio de nombres 'Serializar' no existe en el espacio de nombres 'Json' (¿falta alguna referencia de ensamblado?) [C:\IA\civil3d-mcp\ArbaMcp\ArbaMcp.csproj]
C:\IA\civil3d-mcp\ArbaMcp\Herramientas.cs(99,49): error CS0234: El tipo o el nombre del espacio de nombres 'Serializar' no existe en el espacio de nombres 'Json' (¿falta alguna referencia de ensamblado?) [C:\IA\civil3d-mcp\ArbaMcp\ArbaMcp.csproj]

ERROR al compilar.

C:\IA\civil3d-mcp\ArbaMcp\Herramientas.Corredores.cs(1301,109): error CS0234: El tipo o el nombre del espacio de nombres 'Serializar' no existe en el espacio de nombres 'Json' (¿falta alguna referencia de ensamblado?) [C:\IA\civil3d-mcp\ArbaMcp\ArbaMcp.csproj]
C:\IA\civil3d-mcp\ArbaMcp\Herramientas.cs(99,49): error CS0234: El tipo o el nombre del espacio de nombres 'Serializar' no existe en el espacio de nombres 'Json' (¿falta alguna referencia de ensamblado?) [C:\IA\civil3d-mcp\ArbaMcp\ArbaMcp.csproj]
    0 Advertencia(s)
    2 Errores

Tiempo transcurrido 00:00:05.56
```

### Causa técnica identificada
En `ArbaMcp\Herramientas.cs` (línea 99) y `ArbaMcp\Herramientas.Corredores.cs` (línea 1301) se invoca `Json.Serializar(...)`. Al estar presente `using System.Text.Json;` en ambos archivos, el compilador C# resuelve `Json` como el espacio de nombres `System.Text.Json` en lugar de la clase `ArbaMcp.Nucleo.Json`. La referencia calificada debe ser `Nucleo.Json.Serializar(...)` (igual a como se utiliza en `ArbaMcp\Servidor.cs:167`).

---

## 3. Estado de la Instalación
- Bundle en `%APPDATA%\Autodesk\ApplicationPlugins\ArbaMcp.bundle\Contents`:
  - `ArbaMcp.dll`: `FileVersion 1.3.2.0` (sin modificar)
  - `ArbaMcp.Nucleo.dll`: `FileVersion 1.3.2.0` (sin modificar)
  - `PackageContents.xml`: `AppVersion="1.3.2"` (sin modificar)
