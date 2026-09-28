// Sustitutos mínimos de la API de Civil 3D (AeccDbMgd) SOLO para compilar el plugin sin Civil 3D. Ver LEEME.md.
using System;
using System.Collections;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace Autodesk.Civil.ApplicationServices
{
    using Autodesk.Civil.DatabaseServices;
    public class CivilDocument
    {
        public ObjectIdCollection GetAlignmentIds() => new ObjectIdCollection();
        public ObjectIdCollection GetSurfaceIds() => new ObjectIdCollection();
        public ObjectIdCollection GetSampleLineGroupIds() => new ObjectIdCollection();
        public ObjectIdCollection GetIntersectionIds() => new ObjectIdCollection();
        public CorridorCollection CorridorCollection => new CorridorCollection();
        public AssemblyCollection AssemblyCollection => new AssemblyCollection();
        public IntersectionCollection IntersectionCollection => new IntersectionCollection();
    }
    public static class CivilApplication { public static CivilDocument ActiveDocument => new CivilDocument(); }
}

namespace Autodesk.Civil.DatabaseServices
{
    public class Entity : Autodesk.AutoCAD.DatabaseServices.Entity { public string Name { get; set; } public string Description { get; set; } }

    public class ColeccionIds : IEnumerable { public int Count => 0; public IEnumerator GetEnumerator() => new List<ObjectId>().GetEnumerator(); }
    public class CorridorCollection : ColeccionIds { }
    public class AssemblyCollection : ColeccionIds { }
    public class IntersectionCollection : ColeccionIds { }

    // ---- alineamientos y perfiles
    public enum ProfileType { EG, FG, Other }
    public enum ProfileEntityType { Tangent, Circular, Parabolic, Asymmetric, ParabolaSymmetric, ParabolaAsymmetric }
    public class ProfileEntity { public double StartStation => 0; public double EndStation => 0; public ProfileEntityType EntityType => ProfileEntityType.Tangent; }
    public class ProfileEntityCollection : IEnumerable { public int Count => 0; public IEnumerator GetEnumerator() => new List<ProfileEntity>().GetEnumerator(); }
    public class ProfilePVI { public double RawStation { get; set; } public double Elevation { get; set; } public double Station { get; set; } }
    public class ProfilePVICollection : IEnumerable { public int Count => 0; public IEnumerator GetEnumerator() => new List<ProfilePVI>().GetEnumerator(); }
    public class Profile : Entity
    {
        public ProfileType ProfileType => ProfileType.FG;
        public double StartingStation => 0; public double EndingStation => 0;
        public ProfilePVICollection PVIs => new ProfilePVICollection();
        public ProfileEntityCollection Entities => new ProfileEntityCollection();
        public ObjectId AlignmentId => ObjectId.Null;
        public double ElevationAt(double pk) => 0;
    }
    public class Alignment : Entity
    {
        public double StartingStation => 0; public double EndingStation => 0; public double Length => 0;
        public ObjectIdCollection GetProfileIds() => new ObjectIdCollection();
        public ObjectIdCollection GetSampleLineGroupIds() => new ObjectIdCollection();
        public void StationOffset(double x, double y, ref double pk, ref double desplazamiento) { }
        public void PointLocation(double pk, double desplazamiento, ref double x, ref double y) { }
        public void PointLocation(double pk, double desplazamiento, double tol, ref double x, ref double y, ref double rumbo) { }
    }

    // ---- superficies
    public class Surface : Entity
    {
        public bool IsOutOfDate => false;
        public void Rebuild() { }
        public double FindElevationAtXY(double x, double y) => 0;
    }
    public class DefinicionConCuenta : IEnumerable { public int Count => 0; public IEnumerator GetEnumerator() => new List<object>().GetEnumerator(); }
    public class SurfaceOperationAddBreakline { }
    public class SurfaceOperationPasteSurface { }
    public class SurfaceDefinitionBreaklines : DefinicionConCuenta
    {
        public SurfaceOperationAddBreakline AddStandardBreaklines(ObjectIdCollection ids, double midOrdinate, double maxDist, double weeding, double weedingAngle) => new SurfaceOperationAddBreakline();
    }
    public class SurfaceDefinitionBoundaries : DefinicionConCuenta { }
    public class SurfaceOperationCollection : DefinicionConCuenta { }
    public class TinSurface : Surface
    {
        public SurfaceDefinitionBreaklines BreaklinesDefinition => new SurfaceDefinitionBreaklines();
        public SurfaceDefinitionBoundaries BoundariesDefinition => new SurfaceDefinitionBoundaries();
        public SurfaceOperationCollection Operations => new SurfaceOperationCollection();
        public SurfaceOperationPasteSurface PasteSurface(ObjectId id) => new SurfaceOperationPasteSurface();
    }
    public class GridSurface : Surface { public SurfaceDefinitionBoundaries BoundariesDefinition => new SurfaceDefinitionBoundaries(); }
    public class TinVolumeSurface : Surface { }
    public class GridVolumeSurface : Surface { }

    // ---- corredores
    public enum SubassemblyTargetType { Surface, Elevation, Offset }
    public enum SubassemblyTargetToOption { Nearest, Farthest, Inside, Outside }
    public class SubassemblyTargetInfo
    {
        public string SubassemblyName => ""; public string AssemblyGroupName => ""; public string LogicalName => "";
        public SubassemblyTargetType TargetType => SubassemblyTargetType.Surface;
        public ObjectIdCollection TargetIds { get; set; }
        public SubassemblyTargetToOption TargetToOption { get; set; }
        public bool UseSameSideTarget { get; set; }
    }
    public class SubassemblyTargetInfoCollection : IEnumerable { public int Count => 0; public SubassemblyTargetInfo this[int i] => new SubassemblyTargetInfo(); public IEnumerator GetEnumerator() => new List<SubassemblyTargetInfo>().GetEnumerator(); }
    public class AppliedAssemblySetting
    {
        public double FrequencyAlongTangents { get; set; } public double FrequencyAlongCurves { get; set; }
        public double FrequencyAlongSpirals { get; set; } public double FrequencyAlongProfileCurves { get; set; }
    }
    public class BaselineRegion
    {
        public string Name { get; set; } public double StartStation { get; set; } public double EndStation { get; set; }
        public ObjectId AssemblyId { get; set; }
        public AppliedAssemblySetting AppliedAssemblySetting => new AppliedAssemblySetting();
        public SubassemblyTargetInfoCollection GetTargets() => new SubassemblyTargetInfoCollection();
        public void SetTargets(SubassemblyTargetInfoCollection c) { }
        public double[] AdditionalStations() => new double[0];
        public void AddStation(double pk, string descripcion) { }
        public BaselineRegion Split(double pk) => new BaselineRegion();
    }
    public class BaselineRegionCollection : IEnumerable
    {
        public int Count => 0;
        public BaselineRegion this[int i] => new BaselineRegion();
        public BaselineRegion Add(string nombre, ObjectId ensamblaje, double inicio, double fin) => new BaselineRegion();
        public IEnumerator GetEnumerator() => new List<BaselineRegion>().GetEnumerator();
    }
    public class CorridorFeatureLine : IEnumerable { public string CodeName => ""; public Point3dCollection FeatureLinePoints => new Point3dCollection(); public IEnumerator GetEnumerator() => new List<object>().GetEnumerator(); }
    public class FeatureLineCollection : IEnumerable { public string CodeName => ""; public IEnumerator GetEnumerator() => new List<CorridorFeatureLine>().GetEnumerator(); }
    public class BaselineFeatureLines { public IEnumerable FeatureLineCollectionMap => new List<FeatureLineCollection>(); }
    public class Baseline
    {
        public string Name { get; set; } public double StartStation => 0; public double EndStation => 0;
        public ObjectId AlignmentId => ObjectId.Null; public ObjectId ProfileId => ObjectId.Null;
        public BaselineRegionCollection BaselineRegions => new BaselineRegionCollection();
        public BaselineFeatureLines MainBaselineFeatureLines => new BaselineFeatureLines();
        public IEnumerable OffsetBaselineFeatureLinesCol => new List<BaselineFeatureLines>();
    }
    public class BaselineCollection : IEnumerable { public int Count => 0; public IEnumerator GetEnumerator() => new List<Baseline>().GetEnumerator(); }
    public enum CorridorSurfaceBoundaryType { OutsideBoundary, InsideBoundary, RenderOnly }
    public class CorridorSurfaceBoundary { public string Name => ""; public CorridorSurfaceBoundaryType BoundaryType { get; set; } public bool IsCorridorExtents => false; public bool UseAsOuterBoundary { get; set; } }
    public class CorridorSurfaceBoundaryCollection : IEnumerable
    {
        public int Count => 0;
        public CorridorSurfaceBoundary AddCorridorExtentsBoundary(string nombre) => new CorridorSurfaceBoundary();
        public CorridorSurfaceBoundary Add(string nombre, ObjectId polilinea) => new CorridorSurfaceBoundary();
        public string[] BoundaryNames() => new string[0];
        public IEnumerator GetEnumerator() => new List<CorridorSurfaceBoundary>().GetEnumerator();
    }
    public class CorridorSurface
    {
        public string Name => ""; public ObjectId SurfaceId => ObjectId.Null;
        public string[] LinkCodes() => new string[0]; public string[] PointCodes() => new string[0]; public string[] FeatureLineCodes() => new string[0];
        public void AddLinkCode(string codigo, bool comoLineaRotura) { }
        public void AddFeatureLineCode(string codigo) { }
        public CorridorSurfaceBoundaryCollection Boundaries => new CorridorSurfaceBoundaryCollection();
    }
    public class CorridorSurfaceCollection : IEnumerable { public int Count => 0; public CorridorSurface Add(string nombre) => new CorridorSurface(); public IEnumerator GetEnumerator() => new List<CorridorSurface>().GetEnumerator(); }
    public class Corridor : Entity
    {
        public bool IsOutOfDate => false; public bool RebuildAutomatic { get; set; }
        public BaselineCollection Baselines => new BaselineCollection();
        public CorridorSurfaceCollection CorridorSurfaces => new CorridorSurfaceCollection();
        public void Rebuild() { }
    }

    // ---- ensamblajes
    public enum AssemblyType { Other, UndividedCrownedRoad, DividedCrownedRoad }
    public enum SubassemblySideType { Left, Right, None }
    public class Subassembly : Entity
    {
        public SubassemblySideType Side => SubassemblySideType.Left;
        public IEnumerable ParamsString => new List<object>(); public IEnumerable ParamsDouble => new List<object>();
        public IEnumerable ParamsLong => new List<object>(); public IEnumerable ParamsBool => new List<object>();
    }
    public class AssemblyGroup { public string Name => ""; public ObjectIdCollection GetSubassemblyIds() => new ObjectIdCollection(); }
    public class AssemblyGroupCollection : IEnumerable { public int Count => 0; public IEnumerator GetEnumerator() => new List<AssemblyGroup>().GetEnumerator(); }
    public class Assembly : Entity { public AssemblyType Type { get; set; } public AssemblyGroupCollection Groups => new AssemblyGroupCollection(); }

    // ---- intersecciones, líneas de muestreo, líneas características
    public enum IntersectionCorridorType { PrimaryRoadCrownMaintained, AllCrownsMaintained }
    public class IntersectionRoad { public ObjectId CenterlineAlignmentId => ObjectId.Null; public double Station => 0; }
    public class IntersectionRoadCollection : IEnumerable { public int Count => 0; public IntersectionRoad this[int i] => new IntersectionRoad(); public IEnumerator GetEnumerator() => new List<IntersectionRoad>().GetEnumerator(); }
    public class Intersection : Entity
    {
        public Point3d Location => new Point3d(); public ObjectId CorridorId => ObjectId.Null;
        public IntersectionCorridorType GradeRuleType => IntersectionCorridorType.PrimaryRoadCrownMaintained;
        public IntersectionRoadCollection IntersectionRoads => new IntersectionRoadCollection();
    }
    public class SectionSource { public string SourceName => ""; public ObjectId SourceId => ObjectId.Null; public bool IsSampled { get; set; } public object SourceType => null; }
    public class SectionSourceCollection : IEnumerable { public int Count => 0; public IEnumerator GetEnumerator() => new List<SectionSource>().GetEnumerator(); }
    public class SampleLine : Entity { public double Station => 0; }
    public class SampleLineGroup : Entity
    {
        public ObjectId ParentAlignmentId => ObjectId.Null;
        public ObjectIdCollection GetSampleLineIds() => new ObjectIdCollection();
        public SectionSourceCollection GetSectionSources() => new SectionSourceCollection();
    }
    public class FeatureLine : Entity { public Point3dCollection GetPoints(object tipo) => new Point3dCollection(); }
}
