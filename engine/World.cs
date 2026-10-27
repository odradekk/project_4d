namespace FourD;

/// <summary>
/// Owns the region's four-dimensional geometry independently of the host.
/// Decision note: 2026-10-26-region-scene-authoring
/// Decision note: 2026-10-26-engine-rewrite
/// </summary>
public sealed class World
{
    internal const double Epsilon = .006;
    internal const double SamplingDistance = .01;
    private readonly SectionDefinition[] sections;
    private readonly StaticEntity[] entities;
    private World(SectionDefinition[] sections, StaticEntity[] entities)
    {
        this.sections = sections;
        this.entities = entities;
    }
    /// <summary>Validates a region and takes independent copies of all accepted data.</summary>
    public static LoadResult Load(WorldDescription description)
    {
        var errors = WorldValidation.Check(description);
        if (errors.Length > 0) return new LoadResult.Invalid(Array.AsReadOnly(errors));
        var entities = description.Entities.Select(e =>
        {
            var shape = e switch { BuildingElementDefinition b => b.Shape, FixedDefinition f => f.Shape, _ => throw new ArgumentException("Unsupported entity.") };
            var parts = shape.Parts.Select(p => p with { FaceUvs = Array.AsReadOnly(p.FaceUvs.ToArray()) }).ToArray();
            var segments = e switch
            {
                BuildingElementDefinition b => RemainingSegments(b.WRange, b.Gaps),
                FixedDefinition f => new[] { new Interval(f.Pose.Position.W - Epsilon, f.Pose.Position.W + Epsilon) },
                _ => throw new ArgumentException("Unsupported entity.")
            };
            var through = e is BuildingElementDefinition && segments.Length > 0 && segments.Max(s => s.Max) - segments.Min(s => s.Min) > 2 * Epsilon;
            return new StaticEntity(e.Id, e.Pose, parts, segments, through);
        }).ToArray();
        return new LoadResult.Loaded(new(description.Sections.ToArray(), entities));
    }
    /// <summary>Returns an independent snapshot, or Rejected(UnknownId) for an unknown section.</summary>
    public SectionGeometryResult GetSectionGeometry(SectionId section)
    {
        var found = sections.FirstOrDefault(s => s.Id == section);
        return found is null ? Reject(FailureCode.UnknownId, "Unknown section.") : GetGeometryAtW(found.W);
    }
    /// <summary>Returns an independent snapshot at any finite world W, or Rejected(InvalidInput) for nonfinite W.</summary>
    public SectionGeometryResult GetGeometryAtW(double w)
    {
        if (!double.IsFinite(w)) return Reject(FailureCode.InvalidInput, "W must be finite.");
        return new SectionGeometryResult.Geometry(w, Array.AsReadOnly(entities
            .Where(e => e.Segments.Any(s => w >= s.Min && w <= s.Max))
            .OrderBy(e => e.Id.Value, StringComparer.Ordinal).Select(e => e.Slice()).ToArray()));
    }
    private static SectionGeometryResult Reject(FailureCode code, string detail) =>
        new SectionGeometryResult.Rejected(new(code, detail, null, null, Array.Empty<Contact>()));
    /// <summary>Returns Euclidean distance in four dimensions; nonfinite inputs throw ArgumentOutOfRangeException.</summary>
    public double Distance4(Vec4 a, Vec4 b)
    {
        double[] coordinates = [a.X, a.Y, a.Z, a.W, b.X, b.Y, b.Z, b.W];
        if (coordinates.Any(v => !double.IsFinite(v))) throw new ArgumentOutOfRangeException(nameof(a), "Points must be finite.");
        double[] differences = [a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W];
        var max = differences.Max(v => Math.Abs(v));
        if (max == 0 || double.IsPositiveInfinity(max)) return max;
        return max * Math.Sqrt(differences.Sum(v => (v / max) * (v / max)));
    }
    private static Interval[] RemainingSegments(Interval range, IReadOnlyList<Interval> gaps)
    {
        var result = new List<Interval>();
        var start = range.Min;
        foreach (var gap in gaps)
        {
            if (start < gap.Min) result.Add(new(start, gap.Min));
            start = gap.Max;
        }
        if (start < range.Max) result.Add(new(start, range.Max));
        return result.ToArray();
    }
}
