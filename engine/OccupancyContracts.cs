namespace FourD;

public abstract record OccupancyShape;
public sealed record OccupancyBox(Pose4 Pose, Vec4 HalfSize) : OccupancyShape;
public sealed record OccupancySphere(Vec4 Center, double Radius) : OccupancyShape;
public sealed record OccupancyHit(EntityId Entity, PartId Part, Vec4 Point);
public abstract record OccupancyResult
{
    public sealed record Occupancy(IReadOnlyList<OccupancyHit> Hits) : OccupancyResult;
    public sealed record Rejected(Failure Failure) : OccupancyResult;
}
