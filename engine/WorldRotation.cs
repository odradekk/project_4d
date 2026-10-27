namespace FourD;

public sealed partial class World
{
    private long revision;
    private sealed record ActiveRotation(RotationPlan Plan, double Started);
    private readonly Dictionary<EntityId, ActiveRotation> rotations = [];
    private static Failure RotationFailure(FailureCode code, string detail, EntityId? id = null, IReadOnlyList<Contact>? contacts = null) => new(code, detail, id, null, contacts ?? Array.Empty<Contact>());
    internal static Vec4 Rotate(Vec4 value, Vec4 axis, double angle)
    {
        var a = Box4.ToArray(axis); var v = Box4.ToArray(value); var dot = Box4.Dot(a, v);
        var halfTurn = angle == Math.PI || angle == -Math.PI;
        var c = halfTurn ? -1 : Math.Cos(angle); var s = halfTurn ? 0 : Math.Sin(angle);
        for (var i = 0; i < 3; i++) v[i] += a[i] * ((c - 1) * dot - s * value.W);
        v[3] = s * dot + c * value.W;
        return Box4.ToVec4(v);
    }
    internal static Vec4 Add(Vec4 a, Vec4 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);
    internal static Vec4 Sub(Vec4 a, Vec4 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);
    internal static Vec4 Transform(Rotation4 r, Vec3 v) => new(r.X.X * v.X + r.Y.X * v.Y + r.Z.X * v.Z, r.X.Y * v.X + r.Y.Y * v.Y + r.Z.Y * v.Z, r.X.Z * v.X + r.Y.Z * v.Y + r.Z.Z * v.Z, r.X.W * v.X + r.Y.W * v.Y + r.Z.W * v.Z);
    private static Pose4 At(RotationPlan plan, double progress)
    {
        var angle = plan.Angle * progress; var r = plan.Start.Rotation;
        return new(Add(plan.Pivot, Rotate(Sub(plan.Start.Position, plan.Pivot), plan.Axis, angle)), new(Rotate(r.X, plan.Axis, angle), Rotate(r.Y, plan.Axis, angle), Rotate(r.Z, plan.Axis, angle), Rotate(r.W, plan.Axis, angle)));
    }
    private (Vec4 Pivot, Vec4 Axis, bool Obstacle) FindFlipPlane(Actor actor, Dynamic item, double w)
    {
        var boxes = item.Definition.Shape.Parts.Select(p => Box4.From(item.Definition, item.Pose, p)).ToArray();
        var center = Enumerable.Range(0, 3).Select(i => (boxes.Min(b => b.Center[i] - b.Radius(Unit(i))) + boxes.Max(b => b.Center[i] + b.Radius(Unit(i)))) / 2).ToArray();
        var from = Box4.ToArray(actor.Pose.Position); double[] to = [center[0], center[1], center[2], w]; var ray = Box4.Sub(to, from);
        var hits = new List<(double T, Box4 Box, double[] Normal)>();
        foreach (var box in Boxes().Where(b => staticDefinitions.ContainsKey(b.Entity)))
        {
            foreach (var normal in box.Axes.Where(a => Math.Abs(a[1]) <= 1e-10 && Math.Abs(a[3]) <= 1e-10)) foreach (var sign in new[] { -1d, 1d })
                {
                    var n = normal.Select(x => x * sign).ToArray(); var denominator = Box4.Dot(n, ray);
                    if (Math.Abs(denominator) <= 1e-10) continue;
                    var t = (Box4.Dot(n, box.Center) + box.Radius(n) - Box4.Dot(n, from)) / denominator;
                    if (t < 0 || t > 1) continue;
                    var p = Enumerable.Range(0, 4).Select(i => from[i] + t * ray[i]).ToArray(); p[3] = w;
                    if (box.Axes.Select((a, i) => Math.Abs(Box4.Dot(a, Box4.Sub(p, box.Center))) <= box.Half[i] + 1e-10).All(x => x)) hits.Add((t, box, n));
                }
        }
        var hit = hits.OrderBy(h => h.T).ThenBy(h => h.Box.Entity.Value, StringComparer.Ordinal).ThenBy(h => h.Box.Part.Value, StringComparer.Ordinal).FirstOrDefault();
        if (hit.Box is not null)
        {
            var n = hit.Normal; if (Box4.Dot(n, Box4.Sub(from, to)) < 0) n = n.Select(v => -v).ToArray();
            return (new(from[0] + hit.T * ray[0], center[1], from[2] + hit.T * ray[2], w), Box4.ToVec4(n), true);
        }
        var dx = from[0] - to[0]; var dz = from[2] - to[2]; var length = Math.Sqrt(dx * dx + dz * dz);
        if (length <= 1e-10)
        {
            var a = new[] { item.Pose.Rotation.X, item.Pose.Rotation.Y, item.Pose.Rotation.Z }[(int)item.Definition.Interaction.LongAxis];
            dx = a.X; dz = a.Z; length = Math.Sqrt(dx * dx + dz * dz);
            if (length <= 1e-10) { dx = 1; dz = 0; length = 1; }
        }
        return (new(center[0], center[1], center[2], w), new(dx / length, 0, dz / length, 0), false);
    }
    public UpdateResult SynchronizeActor(EntityId actor, Vec3 position, double yaw)
    {
        if (!actors.TryGetValue(actor, out var state)) return new UpdateResult.Rejected(RotationFailure(ReadEntity(actor) is EntityReadResult.Found ? FailureCode.WrongEntityKind : FailureCode.UnknownId, "Unknown actor.", actor));
        if (!double.IsFinite(position.X) || !double.IsFinite(position.Y) || !double.IsFinite(position.Z) || !double.IsFinite(yaw)) return new UpdateResult.Rejected(RotationFailure(FailureCode.InvalidInput, "Position and yaw must be finite.", actor));
        actors[actor] = state with { Pose = state.Pose with { Position = new(position.X, position.Y, position.Z, state.Pose.Position.W) }, Yaw = yaw }; revision++;
        return new UpdateResult.Updated();
    }
    public HandednessResult ReadHandedness(EntityId entity) => ReadEntity(entity) switch
    {
        EntityReadResult.Found found => new HandednessResult.Value(found.Entity.Handedness),
        _ => new HandednessResult.Rejected(RotationFailure(FailureCode.UnknownId, "Unknown entity.", entity))
    };
    public HandednessComparisonResult CompareHandedness(EntityId a, EntityId b)
    {
        if (ReadHandedness(a) is not HandednessResult.Value left) return new HandednessComparisonResult.Rejected(RotationFailure(FailureCode.UnknownId, "Unknown entity.", a));
        if (ReadHandedness(b) is not HandednessResult.Value right) return new HandednessComparisonResult.Rejected(RotationFailure(FailureCode.UnknownId, "Unknown entity.", b));
        var result = left.Handedness == Handedness.NotComparable || right.Handedness == Handedness.NotComparable ? HandednessComparison.NotComparable : left.Handedness == right.Handedness ? HandednessComparison.Same : HandednessComparison.Different;
        return new HandednessComparisonResult.Comparison(result);
    }
    public RotationPlanResult PlanRotation(RotationRequest request)
    {
        RotationPlanResult Reject(FailureCode code, string detail, EntityId? id = null) => new RotationPlanResult.Rejected(RotationFailure(code, detail, id));
        if (request is null) return Reject(FailureCode.InvalidInput, "Request is required.");
        if (!actors.TryGetValue(request.Actor, out var actor)) return Reject(ReadEntity(request.Actor) is EntityReadResult.Found ? FailureCode.WrongEntityKind : FailureCode.UnknownId, "Actor is unknown or not a three-dimensional creature.", request.Actor);
        if (!dynamicEntities.TryGetValue(request.Entity, out var item)) return Reject(ReadEntity(request.Entity) is EntityReadResult.Found ? FailureCode.WrongEntityKind : FailureCode.UnknownId, "Entity is unknown or not turnable.", request.Entity);
        if (rotations.ContainsKey(request.Entity)) return Reject(FailureCode.Busy, "Entity is rotating.", request.Entity);
        if (!item.Definition.Interaction.Holdable) return Reject(FailureCode.NotHoldable, "Entity is not holdable.", request.Entity);
        var section = sections.Single(s => s.Id == actor.Definition.Section);
        if (Slice(item, section.W) is null) return Reject(FailureCode.NoSlice, "Entity has no slice in actor section.", request.Entity);
        var hinge = Add(item.Pose.Position, Transform(item.Pose.Rotation, item.Definition.Interaction.Hinge));
        var longAxis = new[] { item.Pose.Rotation.X, item.Pose.Rotation.Y, item.Pose.Rotation.Z }[(int)item.Definition.Interaction.LongAxis];
        var axis = longAxis; double angle; double duration; SectionId? origin;
        if (request is StandUp stand)
        {
            if (item.Origin is not null) return Reject(FailureCode.InvalidInput, "Entity is already upright.", request.Entity);
            var target = sections.FirstOrDefault(s => s.Id == stand.Target);
            if (target is null) return Reject(FailureCode.UnknownId, "Unknown target section.", request.Entity);
            if (target.Id == section.Id) return Reject(FailureCode.InvalidInput, "Target must differ from actor section.", request.Entity);
            if (item.Section != section.Id) return Reject(FailureCode.WrongSection, "Entity belongs to another section.", request.Entity);
            angle = (target.W > section.W ? 1 : -1) * Math.PI / 2; duration = 1.4; origin = section.Id;
        }
        else if (request is LayDown)
        {
            if (item.Origin is null) return Reject(FailureCode.InvalidInput, "Entity is not upright.", request.Entity);
            if (item.Origin != section.Id) return Reject(FailureCode.WrongSection, "Lay down must occur in the origin section.", request.Entity);
            origin = item.Origin; duration = 1.0;
            if (item.Upright is not null) { axis = item.Upright.Axis; angle = -item.Upright.Angle; }
            else
            {
                var w = item.Pose.Rotation.W; var length = Math.Sqrt(w.X * w.X + w.Y * w.Y + w.Z * w.Z);
                if (length <= 1e-10) return Reject(FailureCode.InvalidInput, "Tilt is degenerate.", request.Entity);
                axis = new(w.X / length, w.Y / length, w.Z / length, 0); angle = Math.Atan2(length, w.W);
                var relative = Sub(item.Pose.Position, hinge); var c = Math.Cos(angle); var sn = Math.Sin(angle);
                var horizontal = relative.X * axis.X + relative.Y * axis.Y + relative.Z * axis.Z;
                var finalW = hinge.W + sn * horizontal + c * relative.W;
                if (Math.Abs(finalW - section.W) > 1e-10) return Reject(FailureCode.InvalidInput, "No hinge rotation returns this pose to the origin section.", request.Entity);
            }
        }
        else if (request is Flip)
        {
            if (item.Origin is not null) return Reject(FailureCode.InvalidInput, "Lay down before flipping.", request.Entity);
            var plane = FindFlipPlane(actor, item, section.W);
            hinge = plane.Pivot with { W = item.Pose.Position.W }; axis = plane.Axis; angle = Math.PI; duration = plane.Obstacle ? 1.4 : 1.2; origin = item.Section;
        }
        else return Reject(FailureCode.InvalidInput, "Request is not supported.", request.Entity);
        var count = SamplingSteps(item.Definition, item.Pose, hinge, axis, angle);
        var samples = Enumerable.Range(0, count + 1).Select(i => (double)i / count).ToArray();
        var plan = CheckPath(angle);
        if (request is Flip && plan.Blocked >= 0)
        {
            var opposite = CheckPath(-angle);
            if (opposite.Blocked < 0) plan = opposite;
        }
        return plan.Blocked < 0 ? new RotationPlanResult.Allowed(plan) : new RotationPlanResult.Blocked(plan, RotationFailure(FailureCode.RotationBlocked, "Rotation is obstructed.", request.Entity, plan.Contacts));
        RotationPlan CheckPath(double pathAngle)
        {
            var provisional = new RotationPlan(this, revision, request.Entity, item.Pose, hinge, axis, pathAngle, duration, origin, samples, -1, [], request is LayDown, request is Flip);
            var contacts = new List<Contact>(); var blocked = -1; var obstacles = Boxes().Where(b => b.Entity != request.Entity).Concat(ReservedBoxes(request.Entity)).ToArray();
            for (int i = 0; i < samples.Length; i++)
            {
                var pose = At(provisional, samples[i]);
                foreach (var part in item.Definition.Shape.Parts) foreach (var obstacle in obstacles)
                    {
                        var moving = Box4.From(item.Definition, pose, part);
                        if (Enumerable.Range(0, 4).Any(j => Math.Abs(moving.Center[j] - obstacle.Center[j]) >= moving.Radius(Unit(j)) + obstacle.Radius(Unit(j)) - .005)) continue;
                        if (!Box4.Overlaps(moving, obstacle)) continue;
                        var point = Box4.IntersectionPoint(moving, obstacle); if (point is null) continue;
                        var delta = Box4.Sub(moving.Center, obstacle.Center);
                        var normal = Box4.SeparatingAxes(moving, obstacle).MinBy(n => moving.Radius(n) + obstacle.Radius(n) - Math.Abs(Box4.Dot(n, delta)))!;
                        if (Box4.Dot(normal, delta) < 0) normal = normal.Select(x => -x).ToArray();
                        var contactSection = sections.FirstOrDefault(s => Math.Abs(s.W - point[3]) <= 1e-10)?.Id;
                        contacts.Add(new(request.Entity, part.Id, obstacle.Entity, obstacle.Part, Box4.ToVec4(point), Box4.ToVec4(normal), contactSection));
                    }
                if (contacts.Count > 0) { blocked = i; break; }
            }
            var sorted = contacts.GroupBy(c => (c.Obstacle, c.ObstaclePart, c.MovingPart)).Select(g => g.First()).OrderBy(c => c.Obstacle.Value, StringComparer.Ordinal).ThenBy(c => c.ObstaclePart.Value, StringComparer.Ordinal).ThenBy(c => c.MovingPart.Value, StringComparer.Ordinal).ToArray();
            return new RotationPlan(this, revision, request.Entity, item.Pose, hinge, axis, pathAngle, duration, origin, samples, blocked, Array.AsReadOnly(sorted), request is LayDown, request is Flip);
        }
    }

    internal static int SamplingSteps(TurnableDefinition item, Pose4 pose, Vec4 pivot, Vec4 axis, double angle)
    {
        var radius = 0d;
        foreach (var part in item.Shape.Parts)
        {
            var box = Box4.From(item, pose, part);
            for (var mask = 0; mask < 16; mask++)
            {
                var point = box.Center.ToArray();
                for (var j = 0; j < 4; j++) for (var k = 0; k < 4; k++) point[k] += ((mask & (1 << j)) == 0 ? -1 : 1) * box.Half[j] * box.Axes[j][k];
                var offset = Box4.Sub(point, Box4.ToArray(pivot)); var along = Box4.Dot(offset, Box4.ToArray(axis));
                radius = Math.Max(radius, Math.Sqrt(along * along + offset[3] * offset[3]));
            }
        }
        return Math.Max(1, (int)Math.Ceiling(radius * Math.Abs(angle) / SamplingDistance));
    }
    private IEnumerable<Box4> ReservedBoxes(EntityId except)
    {
        foreach (var active in rotations.Values.Where(a => a.Plan.Entity != except))
        {
            var plan = active.Plan; var item = dynamicEntities[plan.Entity];
            foreach (var progress in plan.Samples) foreach (var part in item.Definition.Shape.Parts)
                    yield return Box4.From(item.Definition, At(plan, progress), part);
        }
    }
    private bool IntersectsReservation(IEnumerable<Box4> boxes, EntityId except)
    {
        var reserved = ReservedBoxes(except).ToArray();
        foreach (var box in boxes) foreach (var other in reserved)
            {
                if (Enumerable.Range(0, 4).Any(i => Math.Abs(box.Center[i] - other.Center[i]) >= box.Radius(Unit(i)) + other.Radius(Unit(i)) - .005)) continue;
                if (Box4.Overlaps(box, other)) return true;
            }
        return false;
    }
    private static double[] Unit(int i) { var axis = new double[4]; axis[i] = 1; return axis; }
    public StartRotationResult StartRotation(RotationPlan plan)
    {
        StartRotationResult Reject(FailureCode code, string message) => new StartRotationResult.Rejected(RotationFailure(code, message, plan?.Entity));
        if (plan is null) return Reject(FailureCode.InvalidInput, "Plan is required.");
        if (plan.World != this) return Reject(FailureCode.ForeignPlan, "Plan belongs to another world.");
        if (plan.Revision != revision) return Reject(FailureCode.StalePlan, "World has changed since planning.");
        if (plan.Blocked >= 0) return new StartRotationResult.Rejected(RotationFailure(FailureCode.RotationBlocked, "Rotation is obstructed.", plan.Entity, plan.Contacts));
        rotations.Add(plan.Entity, new(plan, time));
        if (!plan.Laying && !plan.Flip)
            dynamicEntities[plan.Entity] = dynamicEntities[plan.Entity] with { Section = null, Origin = plan.Origin };
        Enqueue(new RotationStarted(plan.Entity), time); revision++;
        return new StartRotationResult.Started(plan.Entity, plan.Duration);
    }
}
