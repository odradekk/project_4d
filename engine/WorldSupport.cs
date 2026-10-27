namespace FourD;

public sealed partial class World
{
    // Supports whose fall distances differ by no more than this count as equally high.
    private const double SupportTie = 1e-9;

    private double time;
    private long sequence;
    private readonly List<EventOccurrence> events = [];

    private void Enqueue(SpaceEvent value, double at) => events.Add(new(at, ++sequence, value));

    private IEnumerable<Solid4> Solids()
    {
        foreach (var entity in entities)
            foreach (var part in entity.Parts)
                foreach (var segment in entity.Segments)
                    yield return Box4.From(entity, part, segment);
        foreach (var item in dynamicEntities.Values.Where(item => !item.Packed))
            foreach (var solid in DynamicShapes.Solids(item.Definition, item.Pose))
                yield return solid;
        foreach (var solid in DeviceSolids())
            yield return solid;
    }

    /// <summary>Finds the highest actual contact while translating the entity downward.</summary>
    public SupportResult FindSupport(EntityId entity)
    {
        if (ReadEntity(entity) is not EntityReadResult.Found found)
            return new SupportResult.Rejected(Fail(FailureCode.UnknownId, "Unknown entity.", entity));
        var candidates = SupportCandidates(entity).ToArray();
        if (candidates.Length == 0) return new SupportResult.Unsupported();
        var best = candidates.Min(candidate => candidate.Drop);
        var contacts = candidates
            .Where(candidate => candidate.Drop <= best + SupportTie)
            .Select(candidate => candidate.Contact)
            .OrderBy(contact => contact.Support.Value, StringComparer.Ordinal)
            .ThenBy(contact => contact.Part.Value, StringComparer.Ordinal)
            .ToArray();
        return new SupportResult.Supported(found.Entity.Pose.Position.Y - best, Array.AsReadOnly(contacts));
    }

    // A box the entity lands on, and how far the entity falls to reach it.
    private sealed record SupportCandidate(double Drop, SupportContact Contact);

    private IEnumerable<SupportCandidate> SupportCandidates(EntityId entity)
    {
        var solids = Solids().ToArray();
        foreach (var moving in solids.Where(solid => solid.Entity == entity))
            foreach (var support in solids.Where(solid => solid.Entity != entity))
                if (Landing(moving, support) is { } candidate)
                    yield return candidate;
    }

    private static SupportCandidate? Landing(Solid4 moving, Solid4 support) =>
        moving is Box4 box && support is Box4 other ? BoxLanding(box, other) : RoundLanding(moving, support);

    // Where `moving`, translated straight down, comes to rest on `support` when one of them is round; null when it
    // never lands. It lands where the solids first touch, or stays where it is when it already touches.
    private static SupportCandidate? RoundLanding(Solid4 moving, Solid4 support)
    {
        // Falling straight down never meets a solid that lies to the side in X, Z or W.
        if (new[] { 0, 2, 3 }.Any(i => Math.Abs(moving.Center[i] - support.Center[i]) > moving.Extent(i) + support.Extent(i)))
            return null;
        var drop = 0d;
        var embedded = Collision.Depth(moving, support);
        if (embedded > ContactTolerance + RoundingMargin) return null;
        if (embedded < -RoundingMargin)
        {
            var reach = moving.Center[1] - moving.Extent(1) - (support.Center[1] - support.Extent(1));
            if (Collision.StopBefore(moving, support, [0, -1, 0, 0], 0, 0, reach) is not { } fall) return null;
            drop = fall;
        }
        if (Collision.ContactOf(moving.Moved([0, -drop, 0, 0]), support) is not { } contact || contact.Normal.Y <= RoundingMargin) return null;
        return new(drop, new(contact.Obstacle, contact.ObstaclePart, contact.Point, contact.Normal));
    }

    // Where `moving`, translated straight down, comes to rest on `support`; null when it never lands.
    private static SupportCandidate? BoxLanding(Box4 moving, Box4 support)
    {
        var delta = Box4.Sub(moving.Center, support.Center);
        var entry = double.NegativeInfinity; // fall distance at which the boxes start to overlap
        var exit = double.PositiveInfinity; // fall distance at which they separate again
        double[]? normal = null;
        var minDepth = double.PositiveInfinity;
        foreach (var axis in Box4.SeparatingAxes(moving, support))
        {
            var radius = moving.Radius(axis) + support.Radius(axis);
            var offset = Box4.Dot(axis, delta);
            var depth = radius - Math.Abs(offset);
            if (depth < minDepth) minDepth = depth;
            if (Math.Abs(axis[1]) < RoundingMargin)
            {
                // A purely vertical edge cannot support an object sliding past it.
                if (depth <= RoundingMargin) return null;
                continue;
            }
            var first = (offset - radius) / axis[1];
            var second = (offset + radius) / axis[1];
            var lower = Math.Min(first, second);
            var upper = Math.Max(first, second);
            if (lower > entry)
            {
                entry = lower;
                normal = axis[1] > 0 ? axis : Box4.Scale(axis, -1);
            }
            exit = Math.Min(exit, upper);
        }
        if (normal is null || exit <= Math.Max(entry, 0) + RoundingMargin) return null;
        // Already in contact: it rests where it is only if embedded no deeper than the contact tolerance, with the support below.
        if (entry < 0 && (minDepth < -RoundingMargin || minDepth > ContactTolerance + RoundingMargin || Box4.Dot(normal, delta) <= 0))
            return null;
        var drop = Math.Max(entry, 0);
        var center = moving.Center.ToArray();
        center[1] -= drop;
        if (Box4.IntersectionPoint(moving with { Center = center }, support) is not { } point) return null;
        return new(drop, new(support.Entity, support.Part, Box4.ToVec4(point), Box4.ToVec4(normal)));
    }

    /// <summary>Advances time and places unsupported turnables on their highest support.</summary>
    public StepResult Step(double delta)
    {
        if (!double.IsFinite(delta) || delta <= 0 || !double.IsFinite(time + delta))
            return new StepResult.Rejected(Fail(FailureCode.InvalidInput, "Delta must advance finite time."));
        var target = time + delta;
        var rotating = rotations.Values
            .OrderBy(active => active.Started + active.Plan.Duration)
            .ThenBy(active => active.Plan.Entity.Value, StringComparer.Ordinal)
            .ToArray();
        foreach (var active in rotating) AdvanceRotation(active, target);

        var resting = RestingHeights();
        foreach (var id in dynamicEntities.Keys.OrderBy(key => key.Value, StringComparer.Ordinal).ToArray())
        {
            if (rotations.ContainsKey(id)) continue;
            var item = dynamicEntities[id];
            var finalY = resting[id];
            if (finalY >= item.Pose.Position.Y - RoundingMargin) continue;
            var startY = item.Pose.Position.Y;
            dynamicEntities[id] = item with { Pose = item.Pose with { Position = item.Pose.Position with { Y = finalY } } };
            Enqueue(new EntityFell(id, startY, finalY), target);
        }
        time = target;
        revision++;
        var occurred = events.OrderBy(occurrence => occurrence.Time).ThenBy(occurrence => occurrence.Sequence).ToArray();
        events.Clear();
        return new StepResult.Advanced(time, Array.AsReadOnly(occurred));
    }

    // Where each turnable settles. One whose fall would cross a reserved rotation sweep stays where it is.
    private Dictionary<EntityId, double> RestingHeights()
    {
        var held = new HashSet<EntityId>();
        while (true)
        {
            var resting = SettledHeights(held);
            var blocked = dynamicEntities.Keys
                .Where(id => !held.Contains(id) && !rotations.ContainsKey(id) && FallCrossesReservation(id, resting[id]))
                .ToArray();
            if (blocked.Length == 0) return resting;
            held.UnionWith(blocked);
        }
    }

    private bool FallCrossesReservation(EntityId id, double finalY)
    {
        var item = dynamicEntities[id];
        var start = item.Pose.Position;
        var distance = start.Y - finalY;
        if (distance <= RoundingMargin) return false;
        var steps = Math.Max(1, (int)Math.Ceiling(distance / SamplingDistance));
        return Enumerable.Range(1, steps).Any(step =>
        {
            var falling = item.Pose with { Position = start with { Y = start.Y - distance * step / steps } };
            return IntersectsReservation(DynamicShapes.Solids(item.Definition, falling), id);
        });
    }
}
