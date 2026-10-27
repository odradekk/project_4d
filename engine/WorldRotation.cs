namespace FourD;

public sealed partial class World
{
    // The first five degrees of a rotation take a quarter of its duration, so the player can watch the slice leave its section.
    private const double SlowStartAngle = Math.PI / 36;
    private const double SlowStartShare = .25;

    private long revision;
    private sealed record ActiveRotation(RotationPlan Plan, double Started);
    private readonly Dictionary<EntityId, ActiveRotation> rotations = [];

    // Turns a vector in the plane spanned by `axis` (a unit vector with no W component) and W.
    private static Vec4 Rotate(Vec4 value, Vec4 axis, double angle)
    {
        var direction = Box4.ToArray(axis);
        var result = Box4.ToArray(value);
        var along = Box4.Dot(direction, result);
        // Math.Sin(Math.PI) is not 0, so a half turn uses the exact cosine and sine.
        var halfTurn = angle == Math.PI || angle == -Math.PI;
        var cos = halfTurn ? -1 : Math.Cos(angle);
        var sin = halfTurn ? 0 : Math.Sin(angle);
        for (var i = 0; i < 3; i++) result[i] += direction[i] * ((cos - 1) * along - sin * value.W);
        result[3] = sin * along + cos * value.W;
        return Box4.ToVec4(result);
    }

    internal static Vec4 Add(Vec4 a, Vec4 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    private static Vec4 Sub(Vec4 a, Vec4 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

    internal static Vec4 Transform(Rotation4 rotation, Vec3 point) => new(
        rotation.X.X * point.X + rotation.Y.X * point.Y + rotation.Z.X * point.Z,
        rotation.X.Y * point.X + rotation.Y.Y * point.Y + rotation.Z.Y * point.Z,
        rotation.X.Z * point.X + rotation.Y.Z * point.Y + rotation.Z.Z * point.Z,
        rotation.X.W * point.X + rotation.Y.W * point.Y + rotation.Z.W * point.Z);

    internal static Vec4 Transform(Rotation4 rotation, Vec4 point) => new(
        rotation.X.X * point.X + rotation.Y.X * point.Y + rotation.Z.X * point.Z + rotation.W.X * point.W,
        rotation.X.Y * point.X + rotation.Y.Y * point.Y + rotation.Z.Y * point.Z + rotation.W.Y * point.W,
        rotation.X.Z * point.X + rotation.Y.Z * point.Y + rotation.Z.Z * point.Z + rotation.W.Z * point.W,
        rotation.X.W * point.X + rotation.Y.W * point.Y + rotation.Z.W * point.Z + rotation.W.W * point.W);

    internal static Vec4 LocalAxis(Rotation4 rotation, Axis3 axis) => axis switch
    {
        Axis3.X => rotation.X,
        Axis3.Y => rotation.Y,
        _ => rotation.Z
    };

    // The pose after turning the whole body about `pivot` by `angle` in the plane of `axis` and W.
    internal static Pose4 RotatedPose(Pose4 pose, Vec4 pivot, Vec4 axis, double angle)
    {
        var rotation = pose.Rotation;
        return new(
            Add(pivot, Rotate(Sub(pose.Position, pivot), axis, angle)),
            new(Rotate(rotation.X, axis, angle), Rotate(rotation.Y, axis, angle), Rotate(rotation.Z, axis, angle), Rotate(rotation.W, axis, angle)));
    }

    private static Pose4 At(RotationPlan plan, double progress) => RotatedPose(plan.Start, plan.Pivot, plan.Axis, plan.Angle * progress);

    // Samples so that no point of the item moves more than the sampling distance between poses.
    internal static int SamplingSteps(IEnumerable<Solid4> solids, Vec4 pivot, Vec4 axis, double angle)
    {
        var radius = solids.Max(solid => solid.Reach(Box4.ToArray(pivot), Box4.ToArray(axis)));
        return Math.Max(1, (int)Math.Ceiling(radius * Math.Abs(angle) / SamplingDistance));
    }

    // The boxes swept by every other rotation in progress, at each of its sampled poses.
    private IEnumerable<Solid4> ReservedSolids(EntityId except) =>
        rotations.Values.Where(active => active.Plan.Entity != except).SelectMany(active =>
        {
            var plan = active.Plan;
            var definition = dynamicEntities[plan.Entity].Definition;
            return plan.Samples.SelectMany(progress => DynamicShapes.Solids(definition, At(plan, progress)));
        });

    private bool IntersectsReservation(IEnumerable<Solid4> solids, EntityId except)
    {
        var reserved = ReservedSolids(except).ToArray();
        return solids.Any(solid => reserved.Any(other => solid.BoundsMayOverlap(other) && Collision.Overlaps(solid, other)));
    }

    // Why a plan cannot be used: it is missing, from another world, or out of date. Null when it can.
    private Failure? UnusablePlan(RotationPlan? plan)
    {
        if (plan is null) return Fail(FailureCode.InvalidInput, "Plan is required.");
        if (plan.World != this) return Fail(FailureCode.ForeignPlan, "Plan belongs to another world.", plan.Entity);
        if (plan.Revision != revision) return Fail(FailureCode.StalePlan, "World has changed since planning.", plan.Entity);
        return null;
    }

    public StartRotationResult StartRotation(RotationPlan plan)
    {
        if (UnusablePlan(plan) is { } unusable) return new StartRotationResult.Rejected(unusable);
        if (plan.Blocked >= 0)
            return new StartRotationResult.Rejected(Fail(FailureCode.RotationBlocked, "Rotation is obstructed.", plan.Entity, plan.Contacts));
        rotations.Add(plan.Entity, new(plan, time));
        if (plan.StandsUp)
            dynamicEntities[plan.Entity] = dynamicEntities[plan.Entity] with { Section = null, Origin = plan.Origin };
        Enqueue(new RotationStarted(plan.Entity), time);
        revision++;
        return new StartRotationResult.Started(plan.Entity, plan.Duration);
    }

    // Moves a rotation to its pose at the target time, and completes it once its duration has passed.
    private void AdvanceRotation(ActiveRotation active, double target)
    {
        var plan = active.Plan;
        var end = active.Started + plan.Duration;
        var done = target >= end;
        var progress = done ? 1 : Progress(plan, Math.Clamp(target - active.Started, 0, plan.Duration));
        var item = dynamicEntities[plan.Entity];
        dynamicEntities[plan.Entity] = item with { Pose = At(plan, progress) };
        if (done) FinishRotation(plan, item, end);
    }

    private static double Progress(RotationPlan plan, double elapsed)
    {
        var slowTime = plan.Duration * SlowStartShare;
        var slowProgress = SlowStartAngle / Math.Abs(plan.Angle);
        var progress = slowProgress > 1 ? elapsed / plan.Duration
            : elapsed <= slowTime ? slowProgress * elapsed / slowTime
            : slowProgress + (1 - slowProgress) * (elapsed - slowTime) / (plan.Duration - slowTime);
        return Math.Clamp(progress, 0, 1);
    }

    // `before` is the entity as it was before this step moved it.
    private void FinishRotation(RotationPlan plan, Dynamic before, double end)
    {
        var item = dynamicEntities[plan.Entity];
        if (plan.Laying && before.Upright is { } upright)
        {
            // Lying down retraces the stand-up, shifted by however far the hinge has moved since.
            var position = Add(upright.Start.Position, Sub(plan.Pivot, upright.Pivot));
            item = item with { Pose = upright.Start with { Position = position } };
        }
        else if (plan.Laying)
        {
            var originW = sections.Single(section => section.Id == plan.Origin).W;
            item = item with { Pose = item.Pose with { Position = item.Pose.Position with { W = originW } } };
        }
        dynamicEntities[plan.Entity] = item with
        {
            Section = plan.Laying ? plan.Origin : plan.Flip ? before.Section : null,
            Origin = plan.StandsUp ? plan.Origin : null,
            Upright = plan.StandsUp ? plan : null
        };
        rotations.Remove(plan.Entity);
        Enqueue(new RotationEnded(plan.Entity), end);
    }
}
