# InspectC3D - Utilidad de inspección de la API de Civil 3D

Herramienta de desarrollo basada en .NET 10 y `MetadataLoadContext` para inspeccionar los tipos, propiedades, métodos y eventos reales de la API de Autodesk Civil 3D (`AeccDbMgd.dll` y `acdbmgd.dll`) sin requerir la ejecución activa de Civil 3D ni depender de inicializaciones nativas de C++.

## Requisitos
- .NET 10 SDK
- Autodesk Civil 3D 2027 instalado en `C:\Program Files\Autodesk\AutoCAD 2027`

## Uso

### Inspeccionar uno o varios tipos
```powershell
dotnet run --project herramientas-dev\InspectC3D -- <NombreDeTipo> [<NombreDeTipo> ...]
```
Ejemplo:
```powershell
dotnet run --project herramientas-dev\InspectC3D -- Corridor BaselineRegion CorridorSurface Intersection
```
Imprime para cada tipo:
- Nombre completo y tipo base
- Propiedades públicas (nombre, tipo y si cuenta con setter `{ get; set; }` o `{ get; }`)
- Métodos públicos (firma completa con tipos de retorno y parámetros)
- Eventos públicos

### Buscar tipos por texto
```powershell
dotnet run --project herramientas-dev\InspectC3D -- --buscar <texto>
```
Ejemplo:
```powershell
dotnet run --project herramientas-dev\InspectC3D -- --buscar Target
```
Lista todos los tipos presentes en el ensamblado que contengan la cadena especificada.
