namespace FourD;

// Decision note: 2026-10-26-prologue-greybox
public sealed partial class World
{
    /// <summary>
    /// Makes the actor hold the item in hand, or grab its slice when it is stood up. The item needs a slice in the
    /// actor's section, each actor holds at most one item in hand, and an item has at most one holder. An item in
    /// the actor's own backpack comes out into the hand.
    /// </summary>
    public CarryResult HoldEntity(EntityId actor, EntityId item)
    {
        CarryResult Reject(FailureCode code, string detail, EntityId id) => new CarryResult.Rejected(Fail(code, detail, id));
        if (!actors.TryGetValue(actor, out var holder))
            return Reject(KindOrUnknown(actor), "Actor is unknown or not a three-dimensional creature.", actor);
        if (!dynamicEntities.TryGetValue(item, out var held))
            return Reject(KindOrUnknown(item), "Entity is unknown or cannot be held.", item);
        if (rotations.ContainsKey(item)) return Reject(FailureCode.Busy, "Entity is rotating.", item);
        if (!DynamicShapes.Interaction(held.Definition).Holdable) return Reject(FailureCode.NotHoldable, "Entity is not holdable.", item);
        var inBackpack = held.Holder == actor && held.Packed;
        if ((held.Holder is not null && !inBackpack) || HoldsInHand(actor))
            return Reject(FailureCode.EntityInUse, "The item is already held or the actor already holds an item.", item);
        var (taken, refusal) = inBackpack ? TakeOut(holder, held) : (held, null);
        if (taken is null) return new CarryResult.Rejected(refusal!);
        var section = sections.Single(candidate => candidate.Id == holder.Section);
        if (Slice(taken, section.W) is null) return Reject(FailureCode.NoSlice, "Entity has no slice in actor section.", item);

        dynamicEntities[item] = taken with { Holder = actor };
        revision++;
        return new CarryResult.Changed(State(dynamicEntities[item]));
    }

    /// <summary>
    /// Puts a lying, packable item of the actor's section into the actor's backpack, from the world or from the
    /// hand. The host counts the free slots and passes them in: with none the item stays where it is.
    /// </summary>
    public CarryResult PackEntity(EntityId actor, EntityId item, int freeSlots)
    {
        CarryResult Reject(FailureCode code, string detail, EntityId id) => new CarryResult.Rejected(Fail(code, detail, id));
        if (freeSlots < 0) return Reject(FailureCode.InvalidInput, "Free slots cannot be negative.", item);
        if (!actors.TryGetValue(actor, out var owner))
            return Reject(KindOrUnknown(actor), "Actor is unknown or not a three-dimensional creature.", actor);
        if (!dynamicEntities.TryGetValue(item, out var candidate))
            return Reject(KindOrUnknown(item), "Entity is unknown or cannot be packed.", item);
        if (rotations.ContainsKey(item)) return Reject(FailureCode.Busy, "Entity is rotating.", item);
        if (candidate.Holder is not null && candidate.Holder != actor) return Reject(FailureCode.EntityInUse, "Another creature holds the item.", item);
        if (candidate.Packed) return Reject(FailureCode.InvalidInput, "The item is already in the backpack.", item);
        if (candidate.Origin is not null || DynamicShapes.IsBall(candidate.Definition) || !DynamicShapes.Interaction(candidate.Definition).Packable)
            return Reject(FailureCode.NotPackable, "Only a lying packable item goes into the backpack.", item);
        if (candidate.Section != owner.Section) return Reject(FailureCode.WrongSection, "The item lies in another section.", item);
        if (freeSlots == 0) return Reject(FailureCode.CapacityExceeded, "The backpack is full.", item);

        dynamicEntities[item] = candidate with { Holder = actor, Packed = true };
        revision++;
        return new CarryResult.Changed(State(dynamicEntities[item]));
    }

    /// <summary>Lets go of a held item where it is. An item in the backpack is put down where its owner stands.</summary>
    public CarryResult ReleaseEntity(EntityId item)
    {
        CarryResult Reject(FailureCode code, string detail) => new CarryResult.Rejected(Fail(code, detail, item));
        if (!dynamicEntities.TryGetValue(item, out var held)) return Reject(KindOrUnknown(item), "Entity is unknown or cannot be held.");
        if (held.Holder is null) return Reject(FailureCode.InvalidInput, "Entity is not held.");

        var (freed, refusal) = held.Packed ? TakeOut(actors[held.Holder.Value], held) : (held, null);
        if (freed is null) return new CarryResult.Rejected(refusal!);
        dynamicEntities[item] = freed with { Holder = null };
        revision++;
        return new CarryResult.Changed(State(dynamicEntities[item]));
    }

    // The item as it is once out of its owner's backpack, or why it cannot come out. It lies where its owner stands,
    // with its lowest point at the owner's feet and the rotation it had. It is in the building that holds the owner's
    // section, and it has to be fit to lie there: the checks PlaceEntity makes of a pose, because it was last checked
    // against the section it came from.
    private (Dynamic?, Failure?) TakeOut(Actor owner, Dynamic packed)
    {
        var id = packed.Definition.Id;
        var w = sections.Single(candidate => candidate.Id == owner.Section).W;
        var building = buildings.FirstOrDefault(candidate => candidate.WRange.Min <= w && w <= candidate.WRange.Max);
        if (building is null) return (null, Fail(FailureCode.OutOfRange, "The section is in no building.", id));
        var feet = owner.Pose.Position;
        var lowest = DynamicShapes.Solids(packed.Definition, packed.Pose).Min(solid => solid.Center[1] - solid.Extent(1));
        var position = packed.Pose.Position with { X = feet.X, Y = packed.Pose.Position.Y + feet.Y - lowest, Z = feet.Z };
        var taken = packed with { Packed = false, Building = building.Id, Pose = packed.Pose with { Position = position } };
        return PoseFailure(id, taken) is { } failure ? (null, failure) : (taken, null);
    }

    /// <summary>
    /// Moves a held item by the request's delta, a component at a time in the order X, Z, Y. An item that reaches
    /// into W is checked in every section: each component stops at the first contact, so the item slides along
    /// what blocks it. An ordinary item lying flat in the hand is carried by the host's own three-dimensional
    /// physics and is checked only against the sweeps of rotations in progress.
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
        if (item.Holder is null || item.Packed) return Reject(FailureCode.InvalidInput, "Entity is not held.");

        var solids = DynamicShapes.Solids(item.Definition, item.Pose);
        double[] delta = [request.Delta.X, request.Delta.Y, request.Delta.Z, 0];
        double[] applied = [0, 0, 0, 0];
        var contacts = new List<Contact>();
        if (ExtendsInW(solids))
        {
            // The order of the components is part of the contract, so that the same requests give the same result.
            foreach (var axis in new[] { 0, 2, 1 })
            {
                var moving = solids.Select(solid => solid.Moved(applied)).ToArray();
                var (distance, blocked) = Sweep(request.Entity, moving, axis, delta[axis]);
                applied[axis] = distance;
                contacts.AddRange(blocked);
            }
        }
        else
        {
            // The host passes the step of a single frame, so the end pose is the one to check.
            var reserved = ReservedContacts(solids.Select(solid => solid.Moved(delta)), request.Entity);
            if (reserved.Count > 0) return Reject(FailureCode.TranslationBlocked, "Target intersects a reserved rotation sweep.", reserved);
            applied = delta;
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

    // How far the solids can move along a coordinate axis by `delta` before they are blocked by an obstacle in any
    // section, and the contacts there. A pair is blocked where it would embed deeper than the start depth or the
    // contact tolerance, whichever is more, and the solids stop at the last place before that where it embeds no
    // deeper than the start depth or zero. So an obstacle met on the way stops them at its surface, but one they
    // already touch within the tolerance lets them slide along it and leave it.
    private (double Distance, IReadOnlyList<Contact> Contacts) Sweep(EntityId mover, Solid4[] moving, int axis, double delta)
    {
        if (delta == 0) return (0, []);
        double[] direction = [0, 0, 0, 0];
        direction[axis] = Math.Sign(delta);
        var length = Math.Abs(delta);
        var obstacles = Solids().Where(solid => solid.Entity != mover).Concat(ReservedSolids(mover)).ToArray();

        var stops = new List<(double Distance, Solid4 Moving, Solid4 Obstacle)>();
        foreach (var solid in moving)
        {
            foreach (var obstacle in obstacles.Where(candidate => SweepMayReach(solid, candidate, direction, length)))
            {
                var start = Collision.Depth(solid, obstacle);
                var blockLevel = Math.Max(start, ContactTolerance) + RoundingMargin;
                if (Collision.StopBefore(solid, obstacle, direction, Math.Max(start, 0), blockLevel, length) is { } stop)
                    stops.Add((stop, solid, obstacle));
            }
        }
        if (stops.Count == 0) return (delta, []);

        var nearest = stops.Min(candidate => candidate.Distance);
        var contacts = stops
            .Where(candidate => candidate.Distance <= nearest + RoundingMargin)
            .Select(candidate => Collision.ContactOf(candidate.Moving.Moved(Box4.Scale(direction, nearest)), candidate.Obstacle))
            .OfType<Contact>()
            .Select(WithSection)
            .ToArray();
        // A stop closer than the rounding margin is no movement.
        return (nearest < RoundingMargin ? 0 : Math.Sign(delta) * nearest, contacts);
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

    // Where the solids embed deeper than the contact tolerance into the sweeps of rotations in progress.
    private IReadOnlyList<Contact> ReservedContacts(IEnumerable<Solid4> solids, EntityId except)
    {
        var reserved = ReservedSolids(except).ToArray();
        return DistinctContacts(solids
            .SelectMany(solid => reserved.Where(solid.BoundsMayOverlap).Select(other => Collision.Touch(solid, other)))
            .OfType<Contact>()
            .Select(WithSection));
    }

    // Whether any part reaches out of the thin layer around the section that an ordinary object lies in.
    private static bool ExtendsInW(IEnumerable<Solid4> solids) =>
        solids.Max(solid => solid.WSpan.Max) - solids.Min(solid => solid.WSpan.Min) > 2 * Epsilon + RoundingMargin;

    // A contact point exactly on a section's W belongs to that section.
    private Contact WithSection(Contact contact) => contact with
    {
        Section = sections.FirstOrDefault(section => Math.Abs(section.W - contact.Point.W) <= RoundingMargin)?.Id
    };
}
