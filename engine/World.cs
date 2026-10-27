namespace FourD;

/// <summary>
/// Owns the region's four-dimensional geometry independently of the host.
/// Decision note: 2026-10-26-region-scene-authoring
/// Decision note: 2026-10-26-engine-rewrite
/// </summary>
public sealed partial class World
{
    // Slack for floating-point rounding in comparisons that are exact in real arithmetic.
    internal const double RoundingMargin = 1e-10;

    // Half the W thickness of an ordinary object (ε).
    internal const double Epsilon = .006;

    // Embedding depth up to which surfaces count as touching.
    internal const double ContactTolerance = .005;

    // Largest step between sampled poses of a motion; solids must be thicker than this.
    internal const double SamplingDistance = .01;

    // Smallest distance from a part end to a section: 0.02 m, less the rounding margin.
    internal const double SectionClearance = .02 - RoundingMargin;

    private readonly SectionDefinition[] sections;
    private readonly BuildingDefinition[] buildings;
    private readonly StaticEntity[] entities;
    private readonly Dictionary<EntityId, EntityDefinition> staticDefinitions = [];

    private World(WorldDescription description)
    {
        sections = description.Sections.ToArray();
        buildings = description.Buildings.ToArray();
        var solids = new List<StaticEntity>();
        foreach (var definition in description.Entities)
            switch (definition)
            {
                case TurnableDefinition turnable:
                    AddInitial(turnable);
                    break;
                case Creature3Definition actor:
                    actors.Add(actor.Id, new(actor, actor.Pose, 0));
                    break;
                default:
                    solids.Add(AddStatic(definition));
                    break;
            }
        entities = solids.ToArray();
    }

    /// <summary>Validates a region and takes independent copies of all accepted data.</summary>
    public static LoadResult Load(WorldDescription description)
    {
        var errors = WorldValidation.Check(description);
        if (errors.Length == 0) errors = CheckExpectedOperations(description);
        return errors.Length > 0
            ? new LoadResult.Invalid(Array.AsReadOnly(errors))
            : new LoadResult.Loaded(new World(description));
    }

    // Records a building element or fixed object, with its parts copied, and returns its geometry.
    private StaticEntity AddStatic(EntityDefinition definition)
    {
        switch (definition)
        {
            case BuildingElementDefinition element:
                var parts = CopyParts(element.Shape);
                var segments = RemainingSegments(element.WRange, element.Gaps);
                staticDefinitions.Add(element.Id, element with
                {
                    Shape = new(Array.AsReadOnly(parts)),
                    Gaps = Array.AsReadOnly(element.Gaps.ToArray())
                });
                var through = segments.Length > 0 && segments.Max(segment => segment.Max) - segments.Min(segment => segment.Min) > 2 * Epsilon;
                return new(element.Id, element.Pose, parts, segments, through);
            default:
                var fixedObject = (FixedDefinition)definition;
                var fixedParts = CopyParts(fixedObject.Shape);
                var w = fixedObject.Pose.Position.W;
                staticDefinitions.Add(fixedObject.Id, fixedObject with { Shape = new(Array.AsReadOnly(fixedParts)) });
                return new(fixedObject.Id, fixedObject.Pose, fixedParts, [new Interval(w - Epsilon, w + Epsilon)], false);
        }
    }

    private static BoxPart[] CopyParts(BoxAssembly shape) =>
        shape.Parts.Select(part => part with { FaceUvs = Array.AsReadOnly(part.FaceUvs.ToArray()) }).ToArray();

    internal static Failure Fail(FailureCode code, string detail, EntityId? entity = null, IReadOnlyList<Contact>? contacts = null) =>
        new(code, detail, entity, null, contacts ?? Array.Empty<Contact>());

    // The failure code for an ID that exists but has the wrong kind, or does not exist.
    private FailureCode KindOrUnknown(EntityId id) =>
        ReadEntity(id) is EntityReadResult.Found ? FailureCode.WrongEntityKind : FailureCode.UnknownId;

    // One contact per obstacle part and moving part, in a stable order.
    internal static IReadOnlyList<Contact> DistinctContacts(IEnumerable<Contact> contacts) =>
        Array.AsReadOnly(contacts
            .GroupBy(contact => (contact.Obstacle, contact.ObstaclePart, contact.MovingPart))
            .Select(group => group.First())
            .OrderBy(contact => contact.Obstacle.Value, StringComparer.Ordinal)
            .ThenBy(contact => contact.ObstaclePart.Value, StringComparer.Ordinal)
            .ThenBy(contact => contact.MovingPart.Value, StringComparer.Ordinal)
            .ToArray());

    /// <summary>Returns an independent snapshot, or Rejected(UnknownId) for an unknown section.</summary>
    public SectionGeometryResult GetSectionGeometry(SectionId section)
    {
        var found = sections.FirstOrDefault(candidate => candidate.Id == section);
        return found is null ? Reject(FailureCode.UnknownId, "Unknown section.") : GetGeometryAtW(found.W);
    }

    /// <summary>Returns an independent snapshot at any finite world W, or Rejected(InvalidInput) for nonfinite W.</summary>
    public SectionGeometryResult GetGeometryAtW(double w)
    {
        if (!double.IsFinite(w)) return Reject(FailureCode.InvalidInput, "W must be finite.");
        var slices = entities
            .Where(entity => entity.Segments.Any(segment => w >= segment.Min && w <= segment.Max))
            .Select(entity => entity.Slice())
            .Concat(DynamicSlices(w))
            .OrderBy(slice => slice.Entity.Value, StringComparer.Ordinal)
            .ToArray();
        return new SectionGeometryResult.Geometry(w, Array.AsReadOnly(slices));
    }

    private static SectionGeometryResult Reject(FailureCode code, string detail) =>
        new SectionGeometryResult.Rejected(Fail(code, detail));

    /// <summary>Returns Euclidean distance in four dimensions; nonfinite inputs throw ArgumentOutOfRangeException.</summary>
    public double Distance4(Vec4 a, Vec4 b)
    {
        double[] coordinates = [a.X, a.Y, a.Z, a.W, b.X, b.Y, b.Z, b.W];
        if (coordinates.Any(value => !double.IsFinite(value)))
            throw new ArgumentOutOfRangeException(nameof(a), "Points must be finite.");
        double[] differences = [a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W];
        var largest = differences.Max(value => Math.Abs(value));
        if (largest == 0 || double.IsPositiveInfinity(largest)) return largest;
        return largest * Math.Sqrt(differences.Sum(value => (value / largest) * (value / largest)));
    }

    // The parts of a W range left after removing its gaps.
    internal static Interval[] RemainingSegments(Interval range, IReadOnlyList<Interval> gaps)
    {
        var segments = new List<Interval>();
        var start = range.Min;
        foreach (var gap in gaps)
        {
            if (start < gap.Min) segments.Add(new(start, gap.Min));
            start = gap.Max;
        }
        if (start < range.Max) segments.Add(new(start, range.Max));
        return segments.ToArray();
    }
}
