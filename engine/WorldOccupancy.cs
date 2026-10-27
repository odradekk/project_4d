namespace FourD;

public sealed partial class World
{
    // A sphere center this close to a box counts as inside it.
    private const double InsideDistance = 1e-12;

    /// <summary>Returns the intersected solid parts, excluding contact within five millimetres.</summary>
    public OccupancyResult QueryOccupancy(OccupancyShape shape, IReadOnlyList<EntityId> excluded)
    {
        OccupancyResult Reject(FailureCode code, string detail) => new OccupancyResult.Rejected(Fail(code, detail));
        if (excluded is null || excluded.Distinct().Count() != excluded.Count)
            return Reject(FailureCode.InvalidInput, "Excluded IDs must be unique.");
        if (excluded.Any(id => ReadEntity(id) is EntityReadResult.Missing))
            return Reject(FailureCode.UnknownId, "Unknown excluded ID.");

        Func<Box4, double[]?> intersection;
        switch (shape)
        {
            case OccupancyBox box:
                if (!IsValid(box)) return Reject(FailureCode.InvalidInput, "Invalid occupancy box.");
                var query = Box4.From(box);
                if (!query.Representable()) return Reject(FailureCode.InvalidInput, "Occupancy box cannot be represented.");
                intersection = solid => Box4.Overlaps(solid, query) ? Box4.IntersectionPoint(solid, query) : null;
                break;
            case OccupancySphere sphere when IsValid(sphere):
                intersection = solid => SphereContact(solid, sphere);
                break;
            default:
                return Reject(FailureCode.InvalidInput, "Invalid occupancy shape.");
        }

        var hits = new List<OccupancyHit>();
        var solids = Boxes()
            .Where(box => !excluded.Contains(box.Entity))
            .OrderBy(box => box.Entity.Value, StringComparer.Ordinal)
            .ThenBy(box => box.Part.Value, StringComparer.Ordinal)
            .GroupBy(box => (box.Entity, box.Part));
        foreach (var part in solids)
            foreach (var box in part)
                if (intersection(box) is { } point)
                {
                    hits.Add(new(box.Entity, box.Part, Box4.ToVec4(point)));
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

    // The nearest point of the box when the sphere embeds deeper than the contact tolerance, otherwise null.
    private static double[]? SphereContact(Box4 box, OccupancySphere sphere)
    {
        var center = Box4.ToArray(sphere.Center);
        var nearest = box.Closest(center);
        var offset = Box4.Sub(center, nearest);
        var distance = Math.Sqrt(Box4.Dot(offset, offset));
        var depth = distance < InsideDistance
            ? sphere.Radius + Enumerable.Range(0, 4).Min(i => box.Half[i] - Math.Abs(Box4.Dot(Box4.Sub(center, box.Center), box.Axes[i])))
            : sphere.Radius - distance;
        return depth > ContactTolerance ? nearest : null;
    }
}
