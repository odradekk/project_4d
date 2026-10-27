namespace FourD;

public sealed partial class World
{
    public PreviewResult GetRotationPreview(RotationPlan plan, SectionId section)
    {
        if (UnusablePlan(plan) is { } unusable) return new PreviewResult.Rejected(unusable);
        var target = sections.FirstOrDefault(candidate => candidate.Id == section);
        if (target is null) return new PreviewResult.Rejected(Fail(FailureCode.UnknownId, "Unknown section.", plan.Entity));
        var item = dynamicEntities[plan.Entity];
        var samples = plan.Samples
            .Select((progress, i) =>
            {
                var pose = At(plan, progress);
                var contours = Contours(DynamicShapes.Boxes(item.Definition, pose), DynamicShapes.Rounds(item.Definition, pose), target.W);
                return new PreviewSample(progress, contours, plan.Blocked >= 0 && i >= plan.Blocked);
            })
            .ToArray();
        return new PreviewResult.Preview(new(target.W, Array.AsReadOnly(samples), FoldPlane(plan, item, target.W), plan.Contacts));
    }

    // The outline of the ring where standing up will leave it, whether or not the way there is clear.
    public PreviewResult GetHiddenStructurePreview(RotationPlan plan, SectionId section)
    {
        if (UnusablePlan(plan) is { } unusable) return new PreviewResult.Rejected(unusable);
        var item = dynamicEntities[plan.Entity];
        if (!plan.StandsUp || item.Definition is not ItemDefinition { Shape: HiddenRingItem })
            return new PreviewResult.Rejected(Fail(FailureCode.WrongEntityKind, "Only standing up an item with a hidden ring has hidden structure.", plan.Entity));
        var target = sections.FirstOrDefault(candidate => candidate.Id == section);
        if (target is null) return new PreviewResult.Rejected(Fail(FailureCode.UnknownId, "Unknown section.", plan.Entity));
        var rings = DynamicShapes.Rounds(item.Definition, At(plan, 1));
        var final = new PreviewSample(1, Contours([], rings, target.W), plan.Blocked >= 0);
        return new PreviewResult.Preview(new(target.W, [final], [], plan.Contacts));
    }

    // The outline of the box parts, which have edges, followed by that of the rounds, which have none.
    private static IReadOnlyList<Polyline> Contours(IEnumerable<Box4> boxes, IEnumerable<Round4> rounds, double w) =>
        Array.AsReadOnly([.. BoxUnionOutline.At(boxes, w), .. rounds.SelectMany(round => RoundSlice.Outline(round, w))]);

    // The fold plane of a flip as a vertical rectangle through the pivot, as wide and tall as the moving item.
    // It is empty when the section lies outside the W range the item sweeps.
    private static IReadOnlyList<Polyline> FoldPlane(RotationPlan plan, Dynamic item, double w)
    {
        if (!plan.Flip) return Array.Empty<Polyline>();
        var tangent = new Vec4(-plan.Axis.Z, 0, plan.Axis.X, 0);
        var tangentAxis = Box4.ToArray(tangent);
        var pivot = Box4.ToArray(plan.Pivot);
        var solids = plan.Samples.SelectMany(progress => DynamicShapes.Solids(item.Definition, At(plan, progress))).ToArray();
        if (w < solids.Min(solid => solid.WSpan.Min) || w > solids.Max(solid => solid.WSpan.Max)) return Array.Empty<Polyline>();

        double Along(Solid4 solid) => Box4.Dot(Box4.Sub(solid.Center, pivot), tangentAxis);
        var min = solids.Min(solid => Along(solid) - solid.Radius(tangentAxis));
        var max = solids.Max(solid => Along(solid) + solid.Radius(tangentAxis));
        var low = solids.Min(solid => solid.Center[1] - solid.Extent(1));
        var high = solids.Max(solid => solid.Center[1] + solid.Extent(1));
        double[] Corner(double along, double y) => [plan.Pivot.X + along * tangent.X, y, plan.Pivot.Z + along * tangent.Z];
        double[] points = [.. Corner(min, low), .. Corner(max, low), .. Corner(max, high), .. Corner(min, high)];
        return Array.AsReadOnly(new[] { new Polyline(Array.AsReadOnly(points), true) });
    }
}
