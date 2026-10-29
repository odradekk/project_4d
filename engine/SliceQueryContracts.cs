namespace FourD;

public abstract record SliceGeometryResult
{
    public sealed record Geometry(SliceGeometry Slice) : SliceGeometryResult;
    public sealed record Empty : SliceGeometryResult;
    public sealed record Rejected(Failure Failure) : SliceGeometryResult;
}
public abstract record SliceBoundsResult
{
    public sealed record Bounds(Vec3 Min, Vec3 Max) : SliceBoundsResult;
    public sealed record Empty : SliceBoundsResult;
    public sealed record Rejected(Failure Failure) : SliceBoundsResult;
}
public abstract record SliceDirectionResult
{
    public sealed record Direction(Vec3 UnitDirection) : SliceDirectionResult;
    public sealed record Degenerate : SliceDirectionResult;
    public sealed record Rejected(Failure Failure) : SliceDirectionResult;
}
