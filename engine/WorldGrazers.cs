namespace FourD;

// Decision note: 2026-10-26-prologue-greybox
public sealed partial class World
{
    // A grazer: `Definition` is as it was added, and the pose and shape are the frame the host set last. A grazer is
    // neither an obstacle nor a support, so none of the solids of the world are made from it.
    private sealed record Grazer(GrazerDefinition Definition, Pose4 Pose, MetaballShape Shape);

    private readonly Dictionary<EntityId, Grazer> grazers = [];

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
    /// </summary>
    public UpdateResult SetGrazerFrame(EntityId grazer, Pose4 pose, MetaballShape shape)
    {
        if (!grazers.TryGetValue(grazer, out var found))
            return new UpdateResult.Rejected(Fail(KindOrUnknown(grazer), "Unknown entity or not a grazer.", grazer));
        var frame = found.Definition with { Pose = pose, Shape = shape };
        if (FirstFailure(WorldValidation.CheckGrazer(frame), grazer) is { } failure) return new UpdateResult.Rejected(failure);
        grazers[grazer] = found with { Pose = pose, Shape = CopyOf(shape) };
        revision++;
        return new UpdateResult.Updated();
    }
}
