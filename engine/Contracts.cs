namespace FourD;

public readonly record struct EntityId(string Value);
public readonly record struct PartId(string Value);
public readonly record struct SectionId(string Value);
public readonly record struct BuildingId(string Value);
public readonly record struct SeamId(string Value);
public readonly record struct OperationId(string Value);
public readonly record struct Vec2(double X, double Y);
public readonly record struct Vec3(double X, double Y, double Z);
public readonly record struct Vec4(double X, double Y, double Z, double W);
public readonly record struct Rotation3(Vec3 X, Vec3 Y, Vec3 Z);
public readonly record struct Rotation4(Vec4 X, Vec4 Y, Vec4 Z, Vec4 W);
public readonly record struct Pose4(Vec4 Position, Rotation4 Rotation);
public readonly record struct Interval(double Min, double Max);
public readonly record struct Source(string NodePath, string Property);
public enum Axis3 { X, Y, Z }

public sealed record SectionDefinition(SectionId Id, double W, Source Source);
public sealed record BuildingDefinition(BuildingId Id, Interval WRange, Source Source);
public sealed record WorldDescription(
    IReadOnlyList<SectionDefinition> Sections,
    IReadOnlyList<BuildingDefinition> Buildings,
    IReadOnlyList<EntityDefinition> Entities,
    IReadOnlyList<SeamDefinition> Seams,
    IReadOnlyList<SharedValueDefinition> SharedValues,
    IReadOnlyList<ExpectedOperation> ExpectedOperations);
public abstract record EntityDefinition(EntityId Id, Pose4 Pose, Source Source);
public sealed record BuildingElementDefinition(
    EntityId Id, Pose4 Pose, Source Source, BuildingId Building,
    Interval WRange, IReadOnlyList<Interval> Gaps, BoxAssembly Shape)
    : EntityDefinition(Id, Pose, Source);
public sealed record FixedDefinition(
    EntityId Id, Pose4 Pose, Source Source, SectionId Section, BoxAssembly Shape)
    : EntityDefinition(Id, Pose, Source);
public sealed record BoxAssembly(IReadOnlyList<BoxPart> Parts);
public sealed record BoxPart(
    PartId Id, Vec3 Center, Vec3 HalfSize, Rotation3 Rotation,
    IReadOnlyList<FaceUv> FaceUvs);
public sealed record FaceUv(Axis3 Axis, bool Positive, Vec2 Scale, Vec2 Offset);
public abstract record LoadResult
{
    public sealed record Loaded(World World) : LoadResult;
    public sealed record Invalid(IReadOnlyList<LoadError> Errors) : LoadResult;
}
public enum LoadErrorCode
{
    InvalidNumber, InvalidId, DuplicateId, MissingReference,
    InvalidSectionOrder, InvalidInterval, InvalidPose, InvalidGeometry,
    InvalidInteraction, InvalidMotion, InvalidCarry,
    PartBoundaryTooClose, HiddenStructureTooClose,
    BuildingCoverage, BuildingInterference, SeamTooClose, ExpectedOperationFailed,
    UnsupportedDefinition
}
public sealed record Contact(
    EntityId Moving, PartId MovingPart, EntityId Obstacle, PartId ObstaclePart,
    Vec4 Point, Vec4 Normal, SectionId? Section);
public sealed record LoadError(
    LoadErrorCode Code, Source Source, EntityId? Entity,
    OperationId? Operation, int? StepIndex, string Detail,
    IReadOnlyList<Contact> Contacts);
public enum FailureCode
{
    InvalidInput, UnknownId, DuplicateId, WrongEntityKind, Busy,
    NoSlice, WrongSection, NotHoldable, NotPackable, CapacityExceeded,
    RequiresPrism, SeamClosed, SeamSevered, NotConnected,
    PassageOccupied, RotationBlocked, TranslationBlocked,
    OutOfRange, StalePlan, ForeignPlan, EntityInUse
}
public sealed record Failure(
    FailureCode Code, string Detail,
    EntityId? Entity, SeamId? Seam, IReadOnlyList<Contact> Contacts);
public sealed record SliceGeometry(
    EntityId Entity, IReadOnlyList<double> Positions,
    IReadOnlyList<double> Normals, IReadOnlyList<double> Uvs,
    IReadOnlyList<PartId> Parts, bool ThroughObject);
public abstract record SectionGeometryResult
{
    public sealed record Geometry(double W, IReadOnlyList<SliceGeometry> Entities)
        : SectionGeometryResult;
    public sealed record Rejected(Failure Failure) : SectionGeometryResult;
}
