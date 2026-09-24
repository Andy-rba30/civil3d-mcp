# Miembros de la API de Civil 3D 2027 Verificados

Inspección de metadatos realizada sobre `C:\Program Files\Autodesk\AutoCAD 2027\C3D\AeccDbMgd.dll` y `C:\Program Files\Autodesk\AutoCAD 2027\acdbmgd.dll` mediante la utilidad `InspectC3D` (.NET 10).

| Tipo | Miembro pedido / esperado | Miembro real en 2027 | Estado en 2027 | Notas / Alternativa |
|---|---|---|---|---|
| `SubassemblyTargetInfo` | `SubassemblyId` | `AssemblyGroupName`, `SubassemblyName`, `LogicalName` | **No existe** | Identificación resuelta por grupo y nombre buscando en `Assembly.Groups → GetSubassemblyIds()`. |
| `SubassemblyTargetInfo` | `AssemblyGroupName` | `String AssemblyGroupName { get; }` | **Verificado** | Propiedad tipada directa. |
| `SubassemblyTargetInfo` | `LogicalName` | `String LogicalName { get; }` | **Verificado** | Propiedad tipada directa. |
| `SubassemblyTargetInfo` | `TargetIds` (setter) | `ObjectIdCollection TargetIds { get; set; }` | **Verificado** | Propiedad tipada con getter y setter. |
| `SubassemblyTargetInfo` | `UseSameSideTarget` | `Boolean UseSameSideTarget { get; set; }` | **Verificado** | Propiedad tipada con getter y setter (reemplaza `UseSameSide`/`SameSide`). |
| `CorridorSurface` | `AddPointCode` | `AddFeatureLineCode(String codeName)` | **No existe** | En 2027 las superficies de corredor se definen por enlaces (`AddLinkCode`) o líneas características (`AddFeatureLineCode`). Se usa `AddFeatureLineCode` y `tipo=punto` se mantiene como alias. |
| `CorridorSurface` | `LinkCodes` | `String[] LinkCodes()` | **Verificado** | Método tipado directo. |
| `CorridorSurface` | `PointCodes` | `String[] PointCodes()` | **Verificado** | Método tipado directo (solo lectura). |
| `CorridorSurface` | `FeatureLineCodes` | `String[] FeatureLineCodes()` | **Verificado** | Método tipado directo. |
| `CorridorSurface` | `AddLinkCode` | `Void AddLinkCode(String codeName, Boolean addAsBreakLine)` | **Verificado** | Método tipado directo. |
| `CorridorSurface` | `Boundaries` | `CorridorSurfaceBoundaryCollection Boundaries { get; }` | **Verificado** | Colección tipada directa. |
| `CorridorSurfaceBoundaryCollection` | `AddCorridorExtentsBoundary` | `CorridorSurfaceBoundary AddCorridorExtentsBoundary(String boundaryName)` | **Verificado** | Método tipado directo. |
| `CorridorSurfaceBoundaryCollection` | `Add` (polígono) | `CorridorSurfaceBoundary Add(String boundaryName, ObjectId polylineId)` | **Verificado** | Método tipado directo. |
| `BaselineRegion` | Frecuencias (`FrequencyAlongTangents`, etc.) | `AppliedAssemblySetting.FrequencyAlongTangents`, `FrequencyAlongCurves`, `FrequencyAlongSpirals`, `FrequencyAlongProfileCurves` | **Verificado en propiedad anidada** | No están en la raíz de `BaselineRegion`, sino en su propiedad tipada `AppliedAssemblySetting`. |
| `BaselineRegion` | Estaciones adicionales (lectura) | `Double[] AdditionalStations()` | **Verificado** | Método tipado directo. |
| `BaselineRegion` | Estaciones adicionales (adición) | `Void AddStation(Double rawStation, String description)` | **Verificado** | Método tipado directo. |
| `BaselineRegionCollection` | `Add` | `BaselineRegion Add(String regionName, ObjectId assemblyId, Double startStation, Double endStation)` | **Verificado** | Método tipado directo. |
| `ProfilePVI` | `Station` | `Double RawStation { get; set; }` | **Obsoleto** | `Station` genera advertencia CS0618; sustituido por `RawStation`. |
| `Surface` | `IsOutOfDate` | `Boolean IsOutOfDate { get; }` | **Verificado** | Propiedad tipada directa. |
| `Surface` | `Rebuild` | `Void Rebuild()` | **Verificado** | Método tipado directo. |
| `TinSurface` | `PasteSurface` | `SurfaceOperationPasteSurface PasteSurface(ObjectId surfaceId)` | **Verificado** | Método tipado directo. |
| `TinSurface` | `BreaklinesDefinition.AddStandardBreaklines` | `SurfaceOperationAddBreakline AddStandardBreaklines(ObjectIdCollection, Double, Double, Double, Double)` | **Verificado** | Método tipado directo en `tinSurface.BreaklinesDefinition`. |
| `SampleLineGroup` | `GetSectionSources` | `SectionSourceCollection GetSectionSources()` | **Verificado** | Método tipado directo. |
| `Assembly` | `Type` | `AssemblyType Type { get; set; }` | **Verificado** | Propiedad tipada directa. |
| `Intersection` | `Location`, `CorridorId` | `Point3d Location { get; }`, `ObjectId CorridorId { get; }` | **Verificado** | Propiedades tipadas directas. |
| `Intersection` | `IntersectionRoads` | `IntersectionRoadCollection IntersectionRoads { get; }` (`CenterlineAlignmentId`, etc.) | **Verificado** | Vías principal y secundaria obtenidas tipadamente de la colección. |
| `Intersection` | `GradeRuleType` | `IntersectionCorridorType GradeRuleType { get; }` | **Verificado** | Sustituye a `IntersectionType`. |
| `Corridor` | Líneas características (`MainBaselineFeatureLines`) | `bl.MainBaselineFeatureLines.FeatureLineCollectionMap` → `CorridorFeatureLine` (`CodeName`, `FeatureLinePoints`) | **Verificado** | Propiedades tipadas directas. |
| `Database` | `SaveAs` (4 argumentos) | `Void SaveAs(String, Boolean, DwgVersion, SecurityParameters)` | **Verificado** | Método tipado directo en `acdbmgd.dll`. |
