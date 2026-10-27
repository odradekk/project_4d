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
            .Select((progress, i) => new PreviewSample(
                progress,
                BoxUnionOutline.At(Box4.From(item.Definition, At(plan, progress)), target.W),
                plan.Blocked >= 0 && i >= plan.Blocked))
            .ToArray();
        return new PreviewResult.Preview(new(target.W, Array.AsReadOnly(samples), FoldPlane(plan, item, target.W), plan.Contacts));
    }

    public PreviewResult GetHiddenStructurePreview(RotationPlan plan, SectionId section) =>
        new PreviewResult.Rejected(UnusablePlan(plan) ?? Fail(FailureCode.WrongEntityKind, "Entity has no hidden structure.", plan.Entity));

    // The fold plane of a flip as a vertical rectangle through the pivot, as wide and tall as the moving item.
    // It is empty when the section lies outside the W range the item sweeps.
    private static IReadOnlyList<Polyline> FoldPlane(RotationPlan plan, Dynamic item, double w)
    {
        if (!plan.Flip) return Array.Empty<Polyline>();
        var tangent = new Vec4(-plan.Axis.Z, 0, plan.Axis.X, 0);
        var tangentAxis = Box4.ToArray(tangent);
        var pivot = Box4.ToArray(plan.Pivot);
        var boxes = plan.Samples.SelectMany(progress => Box4.From(item.Definition, At(plan, progress))).ToArray();
        if (w < boxes.Min(box => box.WSpan.Min) || w > boxes.Max(box => box.WSpan.Max)) return Array.Empty<Polyline>();

        double Along(Box4 box) => Box4.Dot(Box4.Sub(box.Center, pivot), tangentAxis);
        var min = boxes.Min(box => Along(box) - box.Radius(tangentAxis));
        var max = boxes.Max(box => Along(box) + box.Radius(tangentAxis));
        var low = boxes.Min(box => box.Center[1] - box.Extent(1));
        var high = boxes.Max(box => box.Center[1] + box.Extent(1));
        double[] Corner(double along, double y) => [plan.Pivot.X + along * tangent.X, y, plan.Pivot.Z + along * tangent.Z];
        double[] points = [.. Corner(min, low), .. Corner(max, low), .. Corner(max, high), .. Corner(min, high)];
        return Array.AsReadOnly(new[] { new Polyline(Array.AsReadOnly(points), true) });
    }
}
