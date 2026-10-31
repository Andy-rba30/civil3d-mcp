# ExtractorCatalogo - Catálogo de la API de Civil 3D en SQLite

Herramienta de desarrollo en .NET 10 que lee las DLL de AutoCAD y Civil 3D 2027 **solo como metadatos** (con
`MetadataLoadContext`, el mismo enfoque que `InspectC3D`): no las carga en memoria como código ni ejecuta nada de
ellas, así que no necesita Civil 3D abierto. Guarda todos los tipos y miembros **públicos** en un archivo SQLite con
un índice de texto (FTS5) para que el agente pueda buscar la API por nombre o por descripción sin adivinar.

El archivo resultante es la base del catálogo de `memoria-firma`. **Nunca se sube a este repositorio** (regla 7 de
`CLAUDE.md`): el `.gitignore` excluye cualquier `.sqlite` dentro de `herramientas-dev/`.

## Requisitos
- .NET 10 SDK.
- Para el catálogo real: Autodesk Civil 3D 2027 instalado (las DLL están en la carpeta `C3DPath` de
  `ArbaMcp/ArbaMcp.csproj`, con `AecBaseMgd.dll` en la subcarpeta `ACA\` y `AeccDbMgd.dll` en `C3D\`). Aquí no hace
  falta ninguna DLL de Autodesk para compilar ni para probar.

## Uso
```powershell
dotnet run --project herramientas-dev\ExtractorCatalogo -- --dll "<carpeta de Civil 3D>" --salida <archivo.sqlite> [--incluir <patrón>]...
```
Parámetros:
- `--dll <carpeta>`: carpeta donde buscar las DLL; se puede repetir y se miran también sus subcarpetas. Todo lo que
  haya en esas carpetas sirve además para resolver las referencias entre ensamblados.
- `--salida <archivo>`: archivo `.sqlite` que se crea (si ya existe, se sobrescribe).
- `--incluir <patrón>`: añade otras DLL por patrón, por ejemplo `"Aecc*.dll"`; se puede repetir.
- `--solo-incluir`: procesa únicamente lo que indiquen los patrones, sin las DLL por defecto (lo usan las pruebas).

Por defecto procesa solo las DLL que referencia el plugin: `acdbmgd.dll`, `acmgd.dll`, `accoremgd.dll`,
`AeccDbMgd.dll`, `AecBaseMgd.dll` y `AdWindows.dll`. Los archivos `.dll` que no sean ensamblados .NET (por ejemplo
DLL nativas de C++) se saltan sin error y se cuentan. Al terminar imprime cuántos ensamblados leyó, cuántos tipos,
métodos y propiedades extrajo, cuántos archivos saltó y el tamaño del archivo de salida.

## Qué guarda
| Tabla | Contenido |
|---|---|
| `ensamblados` | nombre, archivo, versión y el `.xml` de documentación usado (si lo había) |
| `espacios` | espacios de nombres |
| `tipos` | nombre, nombre completo, espacio, ensamblado, clase (`clase`, `struct`, `interface`, `enum`, `delegado`), tipo base, obsoleto y su mensaje, descripción y fuente |
| `metodos` | tipo, nombre, estático, tipo de retorno, si es constructor, firma, obsoleto y mensaje, descripción y fuente |
| `propiedades` | tipo, nombre, tipo de dato, estático, lectura, escritura, obsoleto y mensaje, descripción y fuente |
| `parametros` | método, posición, nombre y tipo |
| `busqueda` | índice FTS5 (una fila por tipo, método o propiedad: `clase`, `ref_id`, `nombre`, `nombre_completo`, `descripcion`) |
| `glosario` | `termino`, `sinonimos`, `concepto_api`, `nota`; se crea **vacía** para rellenarla en tareas posteriores |
| `catalogo_info` | fecha de generación, versión del extractor y los conteos |

La descripción sale del `<summary>` del archivo `<nombre>.xml` que esté junto a la DLL, y `fuente_descripcion` guarda
el nombre de ese archivo. Si no hay `.xml` (lo habitual con las DLL de Autodesk), descripción y fuente quedan vacías.

Ejemplo de consulta (con el `sqlite3` de la biblioteca estándar de Python, sin instalar nada):
```powershell
python -c "import sqlite3; c=sqlite3.connect('catalogo.sqlite'); print(c.execute(\"SELECT clase, nombre_completo FROM busqueda WHERE busqueda MATCH 'TinSurface' ORDER BY rank LIMIT 10\").fetchall())"
```

## Pruebas
`ArbaMcp.Pruebas/ExtractorCatalogoPruebas.cs` extrae la biblioteca falsa `herramientas-dev/ExtractorCatalogo.Ejemplo`
(clases, propiedades, un método estático `Create`, un enum, una interfaz y miembros `[Obsolete]`) y comprueba los
conteos, el método estático, los obsoletos, las descripciones del `.xml`, una búsqueda FTS5 y que `glosario` existe
vacía. Corren con `dotnet test ArbaMcp.Pruebas`, sin Civil 3D.
