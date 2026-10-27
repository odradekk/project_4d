namespace FourD;

// Decision note: 2026-10-26-prologue-greybox
public sealed partial class World
{
    /// <summary>
    /// Makes the actor hold the item in hand, or grab its slice when it is stood up. The item needs a slice in the
    /// actor's section, each actor holds at most one item, and an item has at most one holder.
    /// </summary>
    public CarryResult HoldEntity(EntityId actorId, EntityId itemId)
    {
        CarryResult Reject(FailureCode code, string detail, EntityId id) => new CarryResult.Rejected(Fail(code, detail, id));
        if (!actors.TryGetValue(actorId, out var actor))
            return Reject(KindOrUnknown(actorId), "Actor is unknown or not a three-dimensional creature.", actorId);
        if (!dynamicEntities.TryGetValue(itemId, out var item))
            return Reject(KindOrUnknown(itemId), "Entity is unknown or cannot be held.", itemId);
        if (rotations.ContainsKey(itemId)) return Reject(FailureCode.Busy, "Entity is rotating.", itemId);
        if (!DynamicShapes.Interaction(item.Definition).Holdable) return Reject(FailureCode.NotHoldable, "Entity is not holdable.", itemId);
        if (item.Holder is not null || HoldsItem(actorId))
            return Reject(FailureCode.EntityInUse, "The item is already held or the actor already holds an item.", itemId);
        var section = sections.Single(candidate => candidate.Id == actor.Definition.Section);
        if (Slice(item, section.W) is null) return Reject(FailureCode.NoSlice, "Entity has no slice in actor section.", itemId);

        dynamicEntities[itemId] = item with { Holder = actorId };
        revision++;
        return new CarryResult.Changed(State(dynamicEntities[itemId]));
    }

    /// <summary>Lets go of a held item where it is.</summary>
    public CarryResult ReleaseEntity(EntityId itemId)
    {
        CarryResult Reject(FailureCode code, string detail) => new CarryResult.Rejected(Fail(code, detail, itemId));
        if (!dynamicEntities.TryGetValue(itemId, out var item)) return Reject(KindOrUnknown(itemId), "Entity is unknown or cannot be held.");
        if (item.Holder is null) return Reject(FailureCode.InvalidInput, "Entity is not held.");

        dynamicEntities[itemId] = item with { Holder = null };
        revision++;
        return new CarryResult.Changed(State(dynamicEntities[itemId]));
    }

    /// <summary>
    /// Moves a held item by the request's delta, a component at a time in the order X, Z, Y. An item that reaches
    /// into W is checked in every section: each component stops at the first contact, so the item slides along
    /// what blocks it. Nothing is checked for an ordinary item lying flat in the hand, which the host's own
    /// three-dimensional physics carries.
    /// </summary>
    public MoveEntityResult MoveEntity(MoveRequest request)
    {
        MoveEntityResult Reject(FailureCode code, string detail, IReadOnlyList<Contact>? contacts = null) =>
            new MoveEntityResult.Rejected(Fail(code, detail, request?.Entity, contacts));
        if (request is null) return Reject(FailureCode.InvalidInput, "Request is required.");
        if (!new[] { request.Delta.X, request.Delta.Y, request.Delta.Z }.All(double.IsFinite))
            return Reject(FailureCode.InvalidInput, "Delta must be finite.");
        if (!dynamicEntities.TryGetValue(request.Entity, out var item))
            return Reject(KindOrUnknown(request.Entity), "Entity is unknown or cannot be moved.");
        if (rotations.ContainsKey(request.Entity)) return Reject(FailureCode.Busy, "Entity is rotating.");
        if (item.Holder is null) return Reject(FailureCode.InvalidInput, "Entity is not held.");

        var solids = DynamicShapes.Solids(item.Definition, item.Pose);
        double[] delta = [request.Delta.X, request.Delta.Y, request.Delta.Z, 0];
        double[] applied = [0, 0, 0, 0];
        var contacts = new List<Contact>();
        if (!ExtendsInW(solids)) applied = delta;
        else
            // The order of the components is fixed so that the result does not depend on the host's frame rate.
            foreach (var axis in new[] { 0, 2, 1 })
            {
                var moving = solids.Select(solid => solid.Moved(applied)).ToArray();
                var (distance, blocked) = Sweep(request.Entity, moving, axis, delta[axis]);
                applied[axis] = distance;
                contacts.AddRange(blocked);
            }

        var all = DistinctContacts(contacts);
        if (applied.All(value => value == 0) && all.Count > 0) return Reject(FailureCode.TranslationBlocked, "Movement is blocked.", all);
        dynamicEntities[request.Entity] = item with
        {
            Pose = item.Pose with { Position = Add(item.Pose.Position, new(applied[0], applied[1], applied[2], 0)) }
        };
        revision++;
        if (all.Count > 0) Enqueue(new MovementBlocked(request.Entity, all), time);
        return new MoveEntityResult.Moved(new(applied[0], applied[1], applied[2]), all);
    }

    // How far the solids can move along a coordinate axis by `delta` before they reach an obstacle in any
    // section, and the contacts there. Solids that already touch or overlap an obstacle may not sink deeper
    // into it, but can still slide along it or leave it.
    private (double Distance, IReadOnlyList<Contact> Contacts) Sweep(EntityId mover, Solid4[] moving, int axis, double delta)
    {
        if (delta == 0) return (0, []);
        double[] direction = [0, 0, 0, 0];
        direction[axis] = Math.Sign(delta);
        var length = Math.Abs(delta);
        var obstacles = Solids().Where(solid => solid.Entity != mover).Concat(ReservedSolids(mover)).ToArray();

        var stops = new List<(double Distance, Solid4 Moving, Solid4 Obstacle)>();
        foreach (var solid in moving)
            foreach (var obstacle in obstacles.Where(candidate => SweepMayReach(solid, candidate, direction, length)))
            {
                var allowed = Math.Max(Collision.Depth(solid, obstacle), 0);
                if (Collision.FirstReach(solid, obstacle, direction, allowed + RoundingMargin, length) is null) continue;
                stops.Add((Collision.FirstReach(solid, obstacle, direction, allowed, length)!.Value, solid, obstacle));
            }
        if (stops.Count == 0) return (delta, []);

        var stop = stops.Min(candidate => candidate.Distance);
        var contacts = stops
            .Where(candidate => candidate.Distance <= stop + RoundingMargin)
            .Select(candidate => Collision.ContactOf(candidate.Moving.Moved(Box4.Scale(direction, stop)), candidate.Obstacle))
            .OfType<Contact>()
            .Select(WithSection)
            .ToArray();
        // A stop closer than the rounding margin is no movement.
        return (stop < RoundingMargin ? 0 : Math.Sign(delta) * stop, contacts);
    }

    // Whether the bounds of the solid, swept along the direction, meet those of the obstacle.
    private static bool SweepMayReach(Solid4 moving, Solid4 obstacle, double[] direction, double length) =>
        Enumerable.Range(0, 4).All(i =>
        {
            var sweep = direction[i] * length;
            var low = moving.Center[i] - moving.Extent(i) + Math.Min(0, sweep);
            var high = moving.Center[i] + moving.Extent(i) + Math.Max(0, sweep);
            return low <= obstacle.Center[i] + obstacle.Extent(i) && obstacle.Center[i] - obstacle.Extent(i) <= high;
        });

    // Whether any part reaches out of the thin layer around the section that an ordinary object lies in.
    private static bool ExtendsInW(IEnumerable<Solid4> solids) =>
        solids.Max(solid => solid.WSpan.Max) - solids.Min(solid => solid.WSpan.Min) > 2 * Epsilon + RoundingMargin;

    // A contact point exactly on a section's W belongs to that section.
    private Contact WithSection(Contact contact) => contact with
    {
        Section = sections.FirstOrDefault(section => Math.Abs(section.W - contact.Point.W) <= RoundingMargin)?.Id
    };
}
