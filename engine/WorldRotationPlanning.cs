namespace FourD;

public sealed partial class World
{
    private const double StandUpDuration = 1.4;
    private const double LayDownDuration = 1.0;
    private const double FlipDuration = 1.2;
    private const double FlipAroundObstacleDuration = 1.4;

    // Where and how fast a rotation turns: the hinge it pivots about, its plane (the axis and W), and the angle.
    private sealed record Motion(Vec4 Hinge, Vec4 Axis, double Angle, double Duration, SectionId? Origin);

    public RotationPlanResult PlanRotation(RotationRequest request)
    {
        RotationPlanResult Reject(FailureCode code, string detail, EntityId? id = null) =>
            new RotationPlanResult.Rejected(Fail(code, detail, id));
        if (request is null) return Reject(FailureCode.InvalidInput, "Request is required.");
        if (!actors.TryGetValue(request.Actor, out var actor))
            return Reject(KindOrUnknown(request.Actor), "Actor is unknown or not a three-dimensional creature.", request.Actor);
        if (!dynamicEntities.TryGetValue(request.Entity, out var item))
            return Reject(KindOrUnknown(request.Entity), "Entity is unknown or not turnable.", request.Entity);
        if (rotations.ContainsKey(request.Entity)) return Reject(FailureCode.Busy, "Entity is rotating.", request.Entity);
        if (!DynamicShapes.Interaction(item.Definition).Holdable) return Reject(FailureCode.NotHoldable, "Entity is not holdable.", request.Entity);
        var section = sections.Single(candidate => candidate.Id == actor.Definition.Section);
        if (Slice(item, section.W) is null) return Reject(FailureCode.NoSlice, "Entity has no slice in actor section.", request.Entity);

        var (motion, failure) = request switch
        {
            StandUp stand => PlanStandUp(stand, item, section),
            LayDown => PlanLayDown(item, section),
            Flip => PlanFlip(actor, item, section),
            _ => (null, Fail(FailureCode.InvalidInput, "Request is not supported.", request.Entity))
        };
        if (motion is null) return new RotationPlanResult.Rejected(failure!);

        var steps = SamplingSteps(DynamicShapes.Solids(item.Definition, item.Pose), motion.Hinge, motion.Axis, motion.Angle);
        var samples = Enumerable.Range(0, steps + 1).Select(i => (double)i / steps).ToArray();
        // The way must stay inside the building, whose W range is the only space the world has around the item. A flip
        // can turn either way, so it takes the first way that is clear and inside the building, then the first inside it.
        var candidates = new List<(RotationPlan Plan, bool InBuilding)>();
        void Consider(Motion option)
        {
            var traced = TraceRotation(request, item, option, samples);
            var swept = traced.Samples.SelectMany(progress => DynamicShapes.Solids(item.Definition, At(traced, progress)));
            candidates.Add((traced, !LeavesBuilding(item.Definition, swept)));
        }
        Consider(motion);
        if (request is Flip && !(candidates[0].InBuilding && candidates[0].Plan.Blocked < 0))
            Consider(motion with { Angle = -motion.Angle });
        var chosen = candidates.FirstOrDefault(candidate => candidate.InBuilding && candidate.Plan.Blocked < 0);
        if (chosen.Plan is null) chosen = candidates.FirstOrDefault(candidate => candidate.InBuilding);
        if (chosen.Plan is not { } plan) return Reject(FailureCode.OutOfRange, "Rotation leaves the building.", request.Entity);
        return plan.Blocked < 0
            ? new RotationPlanResult.Allowed(plan)
            : new RotationPlanResult.Blocked(plan, Fail(FailureCode.RotationBlocked, "Rotation is obstructed.", request.Entity, plan.Contacts));
    }

    private static Vec4 HingePosition(Dynamic item) => DynamicShapes.Hinge(item.Definition, item.Pose);

    private (Motion?, Failure?) PlanStandUp(StandUp request, Dynamic item, SectionDefinition section)
    {
        (Motion?, Failure?) Refuse(FailureCode code, string detail) => (null, Fail(code, detail, request.Entity));
        if (item.Origin is not null) return Refuse(FailureCode.InvalidInput, "Entity is already upright.");
        var target = sections.FirstOrDefault(candidate => candidate.Id == request.Target);
        if (target is null) return Refuse(FailureCode.UnknownId, "Unknown target section.");
        if (target.Id == section.Id) return Refuse(FailureCode.InvalidInput, "Target must differ from actor section.");
        // A ball is the same in every orientation: it turns about its center and never leaves its place.
        var ball = DynamicShapes.IsBall(item.Definition);
        if (!ball && item.Section != section.Id) return Refuse(FailureCode.WrongSection, "Entity belongs to another section.");
        var axis = LocalAxis(item.Pose.Rotation, DynamicShapes.Interaction(item.Definition).LongAxis);
        var angle = (target.W > section.W ? 1 : -1) * QuarterTurn;
        return (new Motion(HingePosition(item), axis, angle, StandUpDuration, ball ? null : section.Id), null);
    }

    private (Motion?, Failure?) PlanLayDown(Dynamic item, SectionDefinition section)
    {
        (Motion?, Failure?) Refuse(FailureCode code, string detail) => (null, Fail(code, detail, item.Definition.Id));
        if (item.Origin is null) return Refuse(FailureCode.InvalidInput, "Entity is not upright.");
        if (item.Origin != section.Id) return Refuse(FailureCode.WrongSection, "Lay down must occur in the origin section.");
        var hinge = HingePosition(item);
        if (item.Upright is { } standUp)
            return (new Motion(hinge, standUp.Axis, -standUp.Angle, LayDownDuration, item.Origin), null);

        // A pose placed at a tilt has no stand-up to undo. Undo the tilt of its W axis about the hinge, provided
        // that returns the pose to the origin section.
        var tilt = item.Pose.Rotation.W;
        var length = Math.Sqrt(tilt.X * tilt.X + tilt.Y * tilt.Y + tilt.Z * tilt.Z);
        if (length <= RoundingMargin) return Refuse(FailureCode.InvalidInput, "Tilt is degenerate.");
        var axis = new Vec4(tilt.X / length, tilt.Y / length, tilt.Z / length, 0);
        var angle = Math.Atan2(length, tilt.W);
        var relative = Sub(item.Pose.Position, hinge);
        var horizontal = relative.X * axis.X + relative.Y * axis.Y + relative.Z * axis.Z;
        var finalW = hinge.W + Math.Sin(angle) * horizontal + Math.Cos(angle) * relative.W;
        if (Math.Abs(finalW - section.W) > RoundingMargin)
            return Refuse(FailureCode.InvalidInput, "No hinge rotation returns this pose to the origin section.");
        return (new Motion(hinge, axis, angle, LayDownDuration, item.Origin), null);
    }

    private (Motion?, Failure?) PlanFlip(Actor actor, Dynamic item, SectionDefinition section)
    {
        if (item.Origin is not null) return (null, Fail(FailureCode.InvalidInput, "Lay down before flipping.", item.Definition.Id));
        var plane = FindFoldPlane(actor, item, section.W);
        var hinge = DynamicShapes.IsBall(item.Definition) ? item.Pose.Position : plane.Pivot with { W = item.Pose.Position.W };
        var duration = plane.Obstacle ? FlipAroundObstacleDuration : FlipDuration;
        return (new Motion(hinge, plane.Axis, Math.PI, duration, item.Section), null);
    }

    // Turns the item through the sampled poses and stops at the first pose that touches an obstacle.
    private RotationPlan TraceRotation(RotationRequest request, Dynamic item, Motion motion, double[] samples)
    {
        var obstacles = Solids().Where(solid => solid.Entity != request.Entity).Concat(ReservedSolids(request.Entity)).ToArray();
        var contacts = new List<Contact>();
        var blocked = -1;
        for (var i = 0; i < samples.Length && blocked < 0; i++)
        {
            var pose = RotatedPose(item.Pose, motion.Hinge, motion.Axis, motion.Angle * samples[i]);
            foreach (var moving in DynamicShapes.Solids(item.Definition, pose))
                foreach (var obstacle in obstacles)
                    if (moving.BoundsMayOverlap(obstacle) && Collision.Touch(moving, obstacle) is { } contact)
                        contacts.Add(WithSection(contact));
            if (contacts.Count > 0) blocked = i;
        }
        return new RotationPlan(this, revision, request.Entity, item.Pose, motion.Hinge, motion.Axis, motion.Angle, motion.Duration, motion.Origin,
            samples, blocked, DistinctContacts(contacts), request is LayDown, request is Flip);
    }

    // The fold plane the item flips about, as a pivot and a horizontal axis. It is the nearest wall between
    // the actor and the item; with no wall, it faces the actor.
    private (Vec4 Pivot, Vec4 Axis, bool Obstacle) FindFoldPlane(Actor actor, Dynamic item, double w)
    {
        var solids = DynamicShapes.Solids(item.Definition, item.Pose);
        var center = Enumerable.Range(0, 3)
            .Select(i => (solids.Min(solid => solid.Center[i] - solid.Extent(i)) + solids.Max(solid => solid.Center[i] + solid.Extent(i))) / 2)
            .ToArray();
        var start = Box4.ToArray(actor.Pose.Position);
        double[] end = [center[0], center[1], center[2], w];
        var ray = Box4.Sub(end, start);
        if (NearestWall(start, ray, w) is { } wall)
        {
            var normal = Box4.Dot(wall.Normal, Box4.Sub(start, end)) < 0 ? Box4.Scale(wall.Normal, -1) : wall.Normal;
            return (new(start[0] + wall.T * ray[0], center[1], start[2] + wall.T * ray[2], w), Box4.ToVec4(normal), true);
        }
        var dx = start[0] - end[0];
        var dz = start[2] - end[2];
        var length = Math.Sqrt(dx * dx + dz * dz);
        if (length <= RoundingMargin)
        {
            var longAxis = LocalAxis(item.Pose.Rotation, DynamicShapes.Interaction(item.Definition).LongAxis);
            dx = longAxis.X;
            dz = longAxis.Z;
            length = Math.Sqrt(dx * dx + dz * dz);
            if (length <= RoundingMargin)
            {
                dx = 1;
                dz = 0;
                length = 1;
            }
        }
        return (new(center[0], center[1], center[2], w), new(dx / length, 0, dz / length, 0), false);
    }

    // The nearest vertical face of a static box that the ray crosses at W = w, as the fraction of the ray and the face normal.
    private (double T, double[] Normal)? NearestWall(double[] start, double[] ray, double w)
    {
        var hits = new List<(double T, Box4 Box, double[] Normal)>();
        foreach (var box in Solids().OfType<Box4>().Where(box => staticDefinitions.ContainsKey(box.Entity)))
            foreach (var axis in box.Axes.Where(axis => Math.Abs(axis[1]) <= RoundingMargin && Math.Abs(axis[3]) <= RoundingMargin))
                foreach (var sign in new[] { -1d, 1d })
                {
                    var normal = Box4.Scale(axis, sign);
                    var denominator = Box4.Dot(normal, ray);
                    if (Math.Abs(denominator) <= RoundingMargin) continue;
                    var t = (Box4.Dot(normal, box.Center) + box.Radius(normal) - Box4.Dot(normal, start)) / denominator;
                    if (t < 0 || t > 1) continue;
                    var point = Enumerable.Range(0, 4).Select(i => start[i] + t * ray[i]).ToArray();
                    point[3] = w;
                    var offset = Box4.Sub(point, box.Center);
                    if (box.Axes.Select((boxAxis, i) => Math.Abs(Box4.Dot(boxAxis, offset)) <= box.Half[i] + RoundingMargin).All(inside => inside))
                        hits.Add((t, box, normal));
                }
        if (hits.Count == 0) return null;
        var nearest = hits.OrderBy(hit => hit.T)
            .ThenBy(hit => hit.Box.Entity.Value, StringComparer.Ordinal)
            .ThenBy(hit => hit.Box.Part.Value, StringComparer.Ordinal)
            .First();
        return (nearest.T, nearest.Normal);
    }
}
