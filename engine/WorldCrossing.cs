namespace FourD;

// Decision note: 2026-10-26-prologue-greybox
public sealed partial class World
{
    // A crossing whose request names a creature, a seam and two sections that go together.
    private sealed record Crossing(Actor Actor, SeamDefinition Seam, SectionDefinition From, SectionDefinition To);

    /// <summary>
    /// Takes a three-dimensional creature through a seam to another section, with what it carries, or refuses
    /// and changes nothing. A mirror seam turns the creature and its things half a turn about the seam plane.
    /// What is in the hand goes into the backpack, unless it is long enough to reach the target section: that
    /// stands up in the seam, and the creature holds its slice there. Objects that stand upright, balls and
    /// items that are not packable cannot cross. The checks run in the order of docs/frame/contract-actions.md.
    /// </summary>
    public CrossSeamResult CrossSeam(CrossSeamRequest request)
    {
        if (request is null) return new CrossSeamResult.Rejected(Fail(FailureCode.InvalidInput, "Request is required."));
        CrossSeamResult Refuse(Failure failure) => new CrossSeamResult.Rejected(failure with { Seam = request.Seam });

        var (crossing, refusal) = ResolveCrossing(request);
        if (crossing is null) return Refuse(refusal!);
        var actor = crossing.Actor;
        var carried = dynamicEntities.Values.Where(item => item.Holder == actor.Definition.Id).ToArray();
        var hand = carried.SingleOrDefault(item => !item.Packed);
        RotationPlan? wedge = null;
        if (hand is not null)
        {
            (wedge, refusal) = PlanHandItem(crossing, hand, request.FreeBackpackSlots);
            if (refusal is not null) return Refuse(refusal);
        }

        var landing = AcrossSeam(actor.Pose, crossing);
        var carriedIds = carried.Select(item => item.Definition.Id).ToHashSet();
        var contacts = DistinctContacts(PassageContacts(actor.Definition, actor.Pose.Position, carriedIds)
            .Concat(PassageContacts(actor.Definition, landing.Position, carriedIds)));
        if (contacts.Count > 0)
            return Refuse(Fail(FailureCode.PassageOccupied, "There is no room for the creature at the seam.", actor.Definition.Id, contacts));

        actors[actor.Definition.Id] = actor with { Pose = landing, Section = crossing.To.Id };
        foreach (var item in carried)
            dynamicEntities[item.Definition.Id] = item == hand && wedge is not null
                ? item with { Pose = At(wedge, 1), Section = null, Origin = wedge.Origin, Upright = wedge }
                : item with { Pose = AcrossSeam(item.Pose, crossing), Section = crossing.To.Id, Packed = true };
        revision++;
        Enqueue(new SeamCrossed(actor.Definition.Id, crossing.Seam.Id, crossing.From.Id, crossing.To.Id), time);

        var packed = hand is not null && wedge is null ? new[] { hand.Definition.Id } : Array.Empty<EntityId>();
        return new CrossSeamResult.Crossed(
            crossing.From.Id, crossing.To.Id, PoseHandedness(landing), Array.AsReadOnly(packed), wedge is null ? null : hand?.Definition.Id);
    }

    // Finds the creature, the seam and the sections of the request, or why the crossing cannot go ahead from them.
    private (Crossing?, Failure?) ResolveCrossing(CrossSeamRequest request)
    {
        (Crossing?, Failure?) Refuse(FailureCode code, string detail, EntityId? entity = null) => (null, Fail(code, detail, entity));
        if (request.FreeBackpackSlots < 0) return Refuse(FailureCode.InvalidInput, "Free slots cannot be negative.");
        if (!actors.TryGetValue(request.Actor, out var actor))
            return Refuse(KindOrUnknown(request.Actor), "Actor is unknown or not a three-dimensional creature.", request.Actor);
        if (!seams.TryGetValue(request.Seam, out var seam)) return Refuse(FailureCode.UnknownId, "Unknown seam.");
        var to = sections.FirstOrDefault(candidate => candidate.Id == request.Target);
        if (to is null) return Refuse(FailureCode.UnknownId, "Unknown target section.");
        var from = sections.Single(candidate => candidate.Id == actor.Section);
        if (from.Id == to.Id) return Refuse(FailureCode.InvalidInput, "Target must differ from the actor's section.");
        if (!seam.Sections.Contains(from.Id) || !seam.Sections.Contains(to.Id))
            return Refuse(FailureCode.NotConnected, "The seam does not join these two sections.");
        if (Distance4(OnFloorPlan(actor.Pose.Position.X, actor.Pose.Position.Z), OnFloorPlan(seam.Position.X, seam.Position.Z))
            > actor.Definition.PassageRadius + RoundingMargin)
            return Refuse(FailureCode.WrongSection, "The creature is too far from the seam.", request.Actor);
        if (seam.Severed) return Refuse(FailureCode.SeamSevered, "The seam has been severed.");
        if (!seam.Open) return Refuse(FailureCode.SeamClosed, "The seam is closed.");
        if (!request.HasPrism && !actor.Definition.CanUseSeamsWithoutPrism)
            return Refuse(FailureCode.RequiresPrism, "Crossing needs the prism.", request.Actor);
        return (new Crossing(actor, seam, from, to), null);
    }

    // A point at height 0 and W 0, to measure distances on the floor plan.
    private static Vec4 OnFloorPlan(double x, double z) => new(x, 0, z, 0);

    // What the item in the hand does when its holder crosses: the plan of its stand-up when it is long enough to reach
    // the target section; no plan and no failure when it goes into the backpack; otherwise why it cannot go.
    private (RotationPlan?, Failure?) PlanHandItem(Crossing crossing, Dynamic hand, int freeSlots)
    {
        var id = hand.Definition.Id;
        (RotationPlan?, Failure?) Refuse(FailureCode code, string detail) => (null, Fail(code, detail, id));
        if (rotations.ContainsKey(id)) return Refuse(FailureCode.Busy, "The item in hand is rotating.");
        if (hand.Origin is not null || DynamicShapes.IsBall(hand.Definition))
            return Refuse(FailureCode.NotPackable, "An upright object or a ball cannot be taken through a seam.");

        var interaction = DynamicShapes.Interaction(hand.Definition);
        if (interaction.Reach < Math.Abs(crossing.To.W - crossing.From.W))
        {
            if (!interaction.Packable) return Refuse(FailureCode.NotPackable, "The item is too big for the backpack.");
            return freeSlots < 1 ? Refuse(FailureCode.CapacityExceeded, "The backpack is full.") : (null, null);
        }

        var planned = PlanRotation(new StandUp(crossing.Actor.Definition.Id, id, crossing.To.Id));
        if (planned is RotationPlanResult.Allowed allowed) return (allowed.Plan, null);
        return (null, planned is RotationPlanResult.Blocked blocked ? blocked.Failure : ((RotationPlanResult.Rejected)planned).Failure);
    }

    // The pose of something that crosses with the creature: the same place in the target section. A mirror seam also
    // reflects its position in the seam plane and turns its axes half way about that plane (and W).
    private static Pose4 AcrossSeam(Pose4 pose, Crossing crossing)
    {
        var seam = crossing.Seam;
        var crossed = pose;
        if (seam.Mirrored)
        {
            var plane = new Vec4(seam.Position.X, seam.Position.Y, seam.Position.Z, 0);
            var normal = new Vec4(seam.HorizontalNormal.X, seam.HorizontalNormal.Y, seam.HorizontalNormal.Z, 0);
            crossed = RotatedPose(pose, plane, normal, Math.PI);
        }
        return crossed with { Position = crossed.Position with { W = crossing.To.W } };
    }

    // Where a creature standing at `position` would embed in something. Its body is the box that holds it: the passage
    // radius to each side, the passage height up from its feet, and ε each way in W. What it carries is no obstacle.
    private IReadOnlyList<Contact> PassageContacts(Creature3Definition creature, Vec4 position, IReadOnlySet<EntityId> carried)
    {
        var body = new Box4(creature.Id, new("passage"),
            [position.X, position.Y + creature.PassageHeight / 2, position.Z, position.W],
            [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]],
            [creature.PassageRadius, creature.PassageHeight / 2, creature.PassageRadius, Epsilon]);
        var obstacles = Solids().Where(solid => !carried.Contains(solid.Entity)).Concat(ReservedSolids(creature.Id));
        return DistinctContacts(obstacles
            .Where(body.BoundsMayOverlap)
            .Select(obstacle => Collision.Touch(body, obstacle))
            .OfType<Contact>()
            .Select(WithSection));
    }
}
