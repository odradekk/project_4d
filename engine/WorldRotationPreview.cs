namespace FourD;

public sealed partial class World
{
    public PreviewResult GetRotationPreview(RotationPlan plan, SectionId section)
    {
        PreviewResult Reject(FailureCode code, string detail) => new PreviewResult.Rejected(RotationFailure(code, detail, plan?.Entity));
        if (plan is null) return Reject(FailureCode.InvalidInput, "Plan is required.");
        if (plan.World != this) return Reject(FailureCode.ForeignPlan, "Plan belongs to another world.");
        if (plan.Revision != revision) return Reject(FailureCode.StalePlan, "World has changed since planning.");
        var target = sections.FirstOrDefault(s => s.Id == section);
        if (target is null) return Reject(FailureCode.UnknownId, "Unknown section.");
        var item = dynamicEntities[plan.Entity];
        var samples = plan.Samples.Select((progress, i) => new PreviewSample(progress, Contours(item with { Pose = At(plan, progress) }, target.W), plan.Blocked >= 0 && i >= plan.Blocked)).ToArray();
        return new PreviewResult.Preview(new(target.W, Array.AsReadOnly(samples), FoldPlane(plan, item, target.W), plan.Contacts));
    }
    public PreviewResult GetHiddenStructurePreview(RotationPlan plan, SectionId section)
    {
        if (plan is null) return new PreviewResult.Rejected(RotationFailure(FailureCode.InvalidInput, "Plan is required."));
        if (plan.World != this) return new PreviewResult.Rejected(RotationFailure(FailureCode.ForeignPlan, "Plan belongs to another world.", plan.Entity));
        if (plan.Revision != revision) return new PreviewResult.Rejected(RotationFailure(FailureCode.StalePlan, "World has changed since planning.", plan.Entity));
        return new PreviewResult.Rejected(RotationFailure(FailureCode.WrongEntityKind, "Entity has no hidden structure.", plan.Entity));
    }
    private static IReadOnlyList<Polyline> Contours(Dynamic item, double w) =>
        BoxUnionOutline.At(item.Definition.Shape.Parts.Select(p => Box4.From(item.Definition, item.Pose, p)), w);
    private IReadOnlyList<Polyline> FoldPlane(RotationPlan plan, Dynamic item, double w)
    {
        if (!plan.Flip) return Array.Empty<Polyline>();
        var tangent = new Vec4(-plan.Axis.Z, 0, plan.Axis.X, 0); var min = double.PositiveInfinity; var max = double.NegativeInfinity; var low = double.PositiveInfinity; var high = double.NegativeInfinity; var minW = double.PositiveInfinity; var maxW = double.NegativeInfinity;
        foreach (var progress in plan.Samples) foreach (var part in item.Definition.Shape.Parts)
            {
                var box = Box4.From(item.Definition, At(plan, progress), part); var center = Box4.Dot(Box4.Sub(box.Center, Box4.ToArray(plan.Pivot)), Box4.ToArray(tangent)); var radius = box.Radius(Box4.ToArray(tangent));
                minW = Math.Min(minW, box.Center[3] - box.Radius([0, 0, 0, 1])); maxW = Math.Max(maxW, box.Center[3] + box.Radius([0, 0, 0, 1]));
                min = Math.Min(min, center - radius); max = Math.Max(max, center + radius); low = Math.Min(low, box.Center[1] - box.Radius([0, 1, 0, 0])); high = Math.Max(high, box.Center[1] + box.Radius([0, 1, 0, 0]));
            }
        if (w < minW || w > maxW) return Array.Empty<Polyline>();
        double[] points = [plan.Pivot.X + min * tangent.X, low, plan.Pivot.Z + min * tangent.Z, plan.Pivot.X + max * tangent.X, low, plan.Pivot.Z + max * tangent.Z, plan.Pivot.X + max * tangent.X, high, plan.Pivot.Z + max * tangent.Z, plan.Pivot.X + min * tangent.X, high, plan.Pivot.Z + min * tangent.Z];
        return Array.AsReadOnly(new[] { new Polyline(Array.AsReadOnly(points), true) });
    }
}
