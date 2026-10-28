namespace FourD;

public sealed partial class World
{
    // Supports whose fall distances differ by no more than this count as equally high.
    private const double SupportTie = 1e-9;

    // The solids of one entity that blocks or supports, and the record they are made from: they change only when that
    // record is replaced. The solids are made when they are asked for.
    private sealed record EntitySolids(EntityId Id, object Record, IEnumerable<Solid4> Solids);

    private IEnumerable<EntitySolids> SolidsByEntity()
    {
        foreach (var entity in entities)
            yield return new(entity.Id, entity, entity.Parts.SelectMany(part => entity.Segments.Select(segment => Box4.From(entity, part, segment))));
        foreach (var item in dynamicEntities.Values.Where(item => !item.Packed))
            yield return new(item.Definition.Id, item, DynamicShapes.Solids(item.Definition, item.Pose));
        foreach (var device in devices.Values)
            yield return new(device.Definition.Id, device, Solids(device, device.Position));
    }

    // The solids of the world; a static entity that never reaches into `within` can be left out of a question about it.
    private IEnumerable<Solid4> Solids(Interval? within = null) => SolidsByEntity()
        .Where(owner => within is not { } span || owner.Record is not StaticEntity entity || entity.Segments.Any(segment => segment.Max >= span.Min && segment.Min <= span.Max))
        .SelectMany(owner => owner.Solids);

    /// <summary>Finds the highest actual contact while translating the entity downward.</summary>
    public SupportResult FindSupport(EntityId entity)
    {
        if (ReadEntity(entity) is not EntityReadResult.Found found)
            return new SupportResult.Rejected(Fail(FailureCode.UnknownId, "Unknown entity.", entity));
        var candidates = SupportCandidates(entity, SolidsByEntity().ToArray(), pairLandings);
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

    // The landing of each pair is taken from `pairLandings` while neither of its records has been replaced; the pairs
    // used go into `kept`. The candidates of one pair come moving solid first, then supporting solid. FindSupport
    // sorts its contacts by support entity and part, and the sort is stable, so contacts on one part of one entity
    // come back in that order.
    private SupportCandidate[] SupportCandidates(EntityId entity, EntitySolids[] owners, Dictionary<(EntityId, EntityId), PairLandings> kept)
    {
        if (owners.SingleOrDefault(owner => owner.Id == entity) is not { } moving) return [];
        var movingSolids = moving.Solids.ToArray();
        var candidates = new List<SupportCandidate>();
        foreach (var support in owners.Where(owner => owner.Id != entity))
        {
            var key = (entity, support.Id);
            if (!pairLandings.TryGetValue(key, out var pair) || !ReferenceEquals(pair.Moving, moving.Record) || !ReferenceEquals(pair.Support, support.Record))
            {
                var supportSolids = support.Solids.ToArray();
                var landings = movingSolids.SelectMany(solid => supportSolids.Select(other => Landing(solid, other))).OfType<SupportCandidate>();
                pair = new(moving.Record, support.Record, [.. landings]);
            }
            kept[key] = pair;
            candidates.AddRange(pair.Candidates);
        }
        return [.. candidates];
    }

    private static SupportCandidate? Landing(Solid4 moving, Solid4 support) =>
        moving is Box4 box && support is Box4 other ? BoxLanding(box, other) : RoundLanding(moving, support);

    // Where `moving`, translated straight down, comes to rest on `support` when they are not both boxes; null when it
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
}
