namespace FourD;

public sealed class RotationPlan
{
    internal RotationPlan(
        World world, long revision, EntityId entity, Pose4 start, Vec4 pivot, Vec4 axis, double angle, double duration,
        SectionId? origin, double[] samples, int blocked, IReadOnlyList<Contact> contacts, bool laying = false, bool flip = false)
        => (World, Revision, Entity, Start, Pivot, Axis, Angle, Duration, Origin, Samples, Blocked, Contacts, Laying, Flip) =
            (world, revision, entity, start, pivot, axis, angle, duration, origin, samples, blocked, contacts, laying, flip);
    internal World World { get; }
    internal long Revision { get; }
    internal EntityId Entity { get; }
    internal Pose4 Start { get; }
    internal Vec4 Pivot { get; }
    internal Vec4 Axis { get; }
    internal double Angle { get; }
    internal double Duration { get; }
    internal SectionId? Origin { get; }
    internal double[] Samples { get; }
    internal int Blocked { get; }
    internal IReadOnlyList<Contact> Contacts { get; }
    internal bool Laying { get; }
    internal bool Flip { get; }
    internal bool StandsUp => !Laying && !Flip;
}
public abstract record RotationPlanResult
{
    public sealed record Allowed(RotationPlan Plan) : RotationPlanResult;
    public sealed record Blocked(RotationPlan Plan, Failure Failure) : RotationPlanResult;
    public sealed record Rejected(Failure Failure) : RotationPlanResult;
}
public abstract record StartRotationResult
{
    public sealed record Started(EntityId Entity, double Duration) : StartRotationResult;
    public sealed record Rejected(Failure Failure) : StartRotationResult;
}
public sealed record RotationStarted(EntityId Entity) : SpaceEvent;
public sealed record RotationEnded(EntityId Entity) : SpaceEvent;
public sealed record Polyline(IReadOnlyList<double> Positions, bool Closed);
public sealed record PreviewSample(double Progress, IReadOnlyList<Polyline> Contours, bool Blocked);
public sealed record RotationPreview(
    double W, IReadOnlyList<PreviewSample> Samples, IReadOnlyList<Polyline> FoldPlane, IReadOnlyList<Contact> Contacts);
public abstract record PreviewResult
{
    public sealed record Preview(RotationPreview Value) : PreviewResult;
    public sealed record Rejected(Failure Failure) : PreviewResult;
}
public enum HandednessComparison { Same, Different, NotComparable }
public abstract record HandednessComparisonResult
{
    public sealed record Comparison(HandednessComparison Value) : HandednessComparisonResult;
    public sealed record Rejected(Failure Failure) : HandednessComparisonResult;
}
public abstract record HandednessResult
{
    public sealed record Value(Handedness Handedness) : HandednessResult;
    public sealed record Rejected(Failure Failure) : HandednessResult;
}
