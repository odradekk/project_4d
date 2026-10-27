namespace FourD;

public sealed partial class World
{
    /// <summary>Returns the intersected solid parts, excluding contact within five millimetres.</summary>
    public OccupancyResult QueryOccupancy(OccupancyShape shape, IReadOnlyList<EntityId> excluded)
    {
        OccupancyResult Rejected(FailureCode code, string detail) => new OccupancyResult.Rejected(new(code, detail, null, null, Array.Empty<Contact>()));
        if (excluded is null || excluded.Distinct().Count() != excluded.Count) return Rejected(FailureCode.InvalidInput, "Excluded IDs must be unique.");
        if (excluded.Any(id => ReadEntity(id) is EntityReadResult.Missing)) return Rejected(FailureCode.UnknownId, "Unknown excluded ID.");
        Box4? query = null;
        if (shape is OccupancyBox b)
        {
            var axes = new[] { Box4.ToArray(b.Pose.Rotation.X), Box4.ToArray(b.Pose.Rotation.Y), Box4.ToArray(b.Pose.Rotation.Z), Box4.ToArray(b.Pose.Rotation.W) };
            if (Box4.ToArray(b.Pose.Position).Concat(axes.SelectMany(x => x)).Concat(Box4.ToArray(b.HalfSize)).Any(v => !double.IsFinite(v)) || Box4.ToArray(b.HalfSize).Any(v => v <= 0) || !WorldValidation.Rotation(axes))
                return Rejected(FailureCode.InvalidInput, "Invalid occupancy box.");
            query = Box4.From(b);
            if (!query.Representable()) return Rejected(FailureCode.InvalidInput, "Occupancy box cannot be represented.");
        }
        else if (shape is not OccupancySphere s || Box4.ToArray(s.Center).Any(v => !double.IsFinite(v)) || !double.IsFinite(s.Radius) || s.Radius <= 0)
            return Rejected(FailureCode.InvalidInput, "Invalid occupancy shape.");
        var hits = new List<OccupancyHit>();
        foreach (var group in Boxes().Where(b => !excluded.Contains(b.Entity)).OrderBy(b => b.Entity.Value, StringComparer.Ordinal).ThenBy(b => b.Part.Value, StringComparer.Ordinal).GroupBy(b => (b.Entity, b.Part)))
        {
            foreach (var box in group)
            {
                double[]? point;
                if (query is not null)
                {
                    if (!Box4.Overlaps(box, query)) continue;
                    point = Box4.IntersectionPoint(box, query);
                }
                else
                {
                    var sphere = (OccupancySphere)shape; var center = Box4.ToArray(sphere.Center); point = box.Closest(center);
                    var difference = Box4.Sub(center, point); var distance = Math.Sqrt(Box4.Dot(difference, difference));
                    var inside = distance < 1e-12;
                    var depth = inside ? sphere.Radius + Enumerable.Range(0, 4).Min(i => box.Half[i] - Math.Abs(Box4.Dot(Box4.Sub(center, box.Center), box.Axes[i]))) : sphere.Radius - distance;
                    if (depth <= .005) continue;
                }
                if (point is not null) { hits.Add(new(box.Entity, box.Part, Box4.ToVec4(point))); break; }
            }
        }
        return new OccupancyResult.Occupancy(hits.AsReadOnly());
    }
}
