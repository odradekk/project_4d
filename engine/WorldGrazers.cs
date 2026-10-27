namespace FourD;

// Decision note: 2026-10-26-prologue-greybox
public sealed partial class World
{
    // A grazer: `Definition` is as it was added, and the pose and shape are the frame the host set last. A grazer is
    // neither an obstacle nor a support, so none of the solids of the world are made from it.
    private sealed record Grazer(GrazerDefinition Definition, Pose4 Pose, MetaballShape Shape);

    private readonly Dictionary<EntityId, Grazer> grazers = [];

    // The building elements that grazers have consumed, whose IDs stay taken.
    private readonly HashSet<EntityId> consumed = [];

    private static Grazer NewGrazer(GrazerDefinition definition)
    {
        var shape = CopyOf(definition.Shape);
        return new(definition with { Shape = shape }, definition.Pose, shape);
    }

    private static MetaballShape CopyOf(MetaballShape shape) => shape with { Balls = Array.AsReadOnly(shape.Balls.ToArray()) };

    private static EntityState State(Grazer grazer) => new(
        grazer.Definition.Id, grazer.Definition, grazer.Pose, Section: null, ActorYaw: null, UprightOrigin: null, Holder: null, InBackpack: false,
        CarriedItems: [], DevicePosition: null, Rotating: false, Handedness: Handedness.NotComparable);

    private AddEntityResult AddGrazer(GrazerDefinition definition)
    {
        if (FirstFailure(WorldValidation.CheckGrazer(definition), definition.Id) is { } failure)
            return new AddEntityResult.Rejected(failure);
        var grazer = NewGrazer(definition);
        grazers.Add(definition.Id, grazer);
        revision++;
        Enqueue(new EntityAdded(definition.Id), time);
        return new AddEntityResult.Added(State(grazer));
    }

    // The slices of the grazers in the section at w; those the section does not cut have none.
    private IEnumerable<SliceGeometry> GrazerSlices(double w)
    {
        foreach (var grazer in grazers.Values)
        {
            var buffer = new SliceBuffer();
            MetaballSlice.Append(grazer.Shape, grazer.Pose, w, buffer);
            if (buffer.Parts.Count == 0) continue;
            var span = MetaballSlice.WSpan(grazer.Shape, grazer.Pose);
            yield return buffer.ToSlice(grazer.Definition.Id, span.Max - span.Min > 2 * Epsilon + RoundingMargin);
        }
    }

    /// <summary>
    /// Replaces a grazer's pose and shape with the host's frame; the next slice shows it. The pose must be a valid
    /// four-dimensional pose, and the shape the same as one that loads. Rejected(InvalidInput) for either that is not,
    /// Rejected(UnknownId) or Rejected(WrongEntityKind) for an ID that is not a grazer; a rejection changes nothing.
    /// A grazer blocks and supports nothing, so a frame changes no plan, and plans made before it stay valid.
    /// </summary>
    public UpdateResult SetGrazerFrame(EntityId grazer, Pose4 pose, MetaballShape shape)
    {
        if (!grazers.TryGetValue(grazer, out var found))
            return new UpdateResult.Rejected(Fail(KindOrUnknown(grazer), "Unknown entity or not a grazer.", grazer));
        var frame = found.Definition with { Pose = pose, Shape = shape };
        if (FirstFailure(WorldValidation.CheckGrazer(frame), grazer) is { } failure) return new UpdateResult.Rejected(failure);
        grazers[grazer] = found with { Pose = pose, Shape = CopyOf(shape) };
        return new UpdateResult.Updated();
    }

    /// <summary>
    /// Removes whole building elements and severs seams, as the scripted bite of a grazer. The elements leave the
    /// slices, the collision and the support, and the next Step settles what stood on them. Each element that is
    /// gone is reported as StructureConsumed, in ID order, and then each seam that is cut as SeamSevered, in ID order;
    /// elements already consumed and seams already severed succeed without an event. Rejected(UnknownId) for an
    /// unknown grazer, element or seam, and Rejected(WrongEntityKind) for an ID of another kind or a grazer that is
    /// not one; a rejection changes nothing.
    /// </summary>
    public UpdateResult ConsumeStructure(EntityId grazer, IReadOnlyList<EntityId> elements, IReadOnlyList<SeamId> seams)
    {
        UpdateResult Reject(FailureCode code, string detail, EntityId? entity = null, SeamId? seam = null) =>
            new UpdateResult.Rejected(Fail(code, detail, entity) with { Seam = seam });
        if (elements is null || seams is null) return Reject(FailureCode.InvalidInput, "Elements and seams are required.", grazer);
        if (!grazers.ContainsKey(grazer)) return Reject(KindOrUnknown(grazer), "Unknown entity or not a grazer.", grazer);
        foreach (var element in elements)
            if (!consumed.Contains(element) && staticDefinitions.GetValueOrDefault(element) is not BuildingElementDefinition)
                return Reject(KindOrUnknown(element), "Unknown entity or not a building element.", element);
        foreach (var seam in seams)
            if (ReadSeam(seam) is SeamReadResult.Missing) return Reject(FailureCode.UnknownId, "Unknown seam.", seam: seam);

        var gone = elements.Except(consumed).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
        foreach (var element in gone)
        {
            Enqueue(new StructureConsumed(grazer, element), time);
            staticDefinitions.Remove(element);
            consumed.Add(element);
        }
        entities = entities.Where(entity => !consumed.Contains(entity.Id)).ToArray();
        foreach (var seam in seams.OrderBy(id => id.Value, StringComparer.Ordinal)) Sever(seam);
        revision++;
        return new UpdateResult.Updated();
    }
}
