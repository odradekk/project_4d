namespace FourD;

public sealed partial class World
{
    /// <summary>Returns the intersected solid parts, excluding contact within five millimetres.</summary>
    public OccupancyResult QueryOccupancy(OccupancyShape shape, IReadOnlyList<EntityId> excluded)
    {
        OccupancyResult Reject(FailureCode code, string detail) => new OccupancyResult.Rejected(Fail(code, detail));
        if (excluded is null || excluded.Distinct().Count() != excluded.Count)
            return Reject(FailureCode.InvalidInput, "Excluded IDs must be unique.");
        if (excluded.Any(id => ReadEntity(id) is EntityReadResult.Missing))
            return Reject(FailureCode.UnknownId, "Unknown excluded ID.");

        Solid4 query;
        switch (shape)
        {
            case OccupancyBox box:
                if (!IsValid(box)) return Reject(FailureCode.InvalidInput, "Invalid occupancy box.");
                var boxQuery = Box4.From(box);
                if (!boxQuery.Representable()) return Reject(FailureCode.InvalidInput, "Occupancy box cannot be represented.");
                query = boxQuery;
                break;
            case OccupancySphere sphere when IsValid(sphere):
                query = Round4.Ball(default, default, Box4.ToArray(sphere.Center), sphere.Radius);
                break;
            default:
                return Reject(FailureCode.InvalidInput, "Invalid occupancy shape.");
        }

        var hits = new List<OccupancyHit>();
        var solids = Solids()
            .Where(solid => !excluded.Contains(solid.Entity))
            .OrderBy(solid => solid.Entity.Value, StringComparer.Ordinal)
            .ThenBy(solid => solid.Part.Value, StringComparer.Ordinal)
            .GroupBy(solid => (solid.Entity, solid.Part));
        foreach (var part in solids)
            foreach (var solid in part)
                if (solid.BoundsMayOverlap(query) && Collision.Touch(solid, query) is { } contact)
                {
                    hits.Add(new(solid.Entity, solid.Part, contact.Point));
                    break;
                }
        return new OccupancyResult.Occupancy(hits.AsReadOnly());
    }

    private static bool IsValid(OccupancySphere sphere) =>
        Box4.ToArray(sphere.Center).All(double.IsFinite) && double.IsFinite(sphere.Radius) && sphere.Radius > 0;

    private static bool IsValid(OccupancyBox box)
    {
        var halfSize = Box4.ToArray(box.HalfSize);
        return WorldValidation.IsValidPose(box.Pose) && halfSize.All(double.IsFinite) && halfSize.All(length => length > 0);
    }
}
