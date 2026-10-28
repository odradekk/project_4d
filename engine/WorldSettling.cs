namespace FourD;

public sealed partial class World
{
    private double time;
    private long sequence;
    private readonly List<EventOccurrence> events = [];

    private void Enqueue(SpaceEvent value, double at) => events.Add(new(at, ++sequence, value));

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

    // `Moving` rests on `Support` after falling `Gap` more than `Support` does.
    private sealed record SupportEdge(EntityId Support, EntityId Moving, double Gap);

    // Where the solids of one entity land on those of another, kept while neither entity's record is replaced. A
    // settle keeps the pairs it used and drops the rest.
    //
    // This rests on the records of StaticEntity, Dynamic and Device being replaced and never edited in place: a record
    // that is still the same object is taken to have the same solids. An edit in place would leave the old landings
    // in use. A turnable would then hang in the air until some other write replaced a record, and FindSupport would
    // return a landing that is out of date. That is why these records hold read-only lists, and why a change to one
    // builds a new record.
    private sealed record PairLandings(object Moving, object Support, SupportCandidate[] Candidates);

    private Dictionary<(EntityId Moving, EntityId Support), PairLandings> pairLandings = [];

    // The Y at which each turnable comes to rest. One resting on a static box falls until it touches it; one
    // resting on another turnable falls as far as that one does, plus the gap between them.
    private Dictionary<EntityId, double> SettledHeights(IReadOnlySet<EntityId> held)
    {
        var ids = dynamicEntities.Keys.ToArray();
        var owners = SolidsByEntity().ToArray();
        var kept = new Dictionary<(EntityId, EntityId), PairLandings>();
        var startY = ids.ToDictionary(id => id, id => dynamicEntities[id].Pose.Position.Y);
        var drops = ids.ToDictionary(id => id, _ => double.PositiveInfinity);
        var edges = new List<SupportEdge>();
        var queue = new PriorityQueue<EntityId, double>();

        void LimitDrop(EntityId id, double drop)
        {
            if (drop >= drops[id]) return;
            drops[id] = drop;
            queue.Enqueue(id, drop);
        }

        void Propagate()
        {
            while (queue.TryDequeue(out var support, out var drop))
            {
                if (drop > drops[support]) continue;
                foreach (var edge in edges.Where(edge => edge.Support == support)) LimitDrop(edge.Moving, drop + edge.Gap);
            }
        }

        foreach (var id in ids)
        {
            if (held.Contains(id) || rotations.ContainsKey(id) || dynamicEntities[id].Holder is not null)
            {
                LimitDrop(id, 0);
                continue;
            }
            var candidates = SupportCandidates(id, owners, kept);
            if (candidates.Length == 0) LimitDrop(id, 0);
            foreach (var candidate in candidates)
            {
                var support = candidate.Contact.Support;
                if (startY.ContainsKey(support)) edges.Add(new(support, id, candidate.Drop));
                else LimitDrop(id, candidate.Drop);
            }
        }
        Propagate();

        // A group of turnables that rest on each other with no support outside the group stays in place.
        // A group above one of them remains free to fall onto it.
        var unresolved = ids.Where(id => double.IsPositiveInfinity(drops[id])).ToHashSet();
        bool Reaches(EntityId start, EntityId target, HashSet<EntityId> visited)
        {
            if (start == target) return true;
            if (!visited.Add(start)) return false;
            return edges.Where(edge => edge.Moving == start && unresolved.Contains(edge.Support))
                .Any(edge => Reaches(edge.Support, target, visited));
        }
        var examined = new HashSet<EntityId>();
        foreach (var id in unresolved)
        {
            if (examined.Contains(id)) continue;
            var group = unresolved.Where(other => Reaches(id, other, []) && Reaches(other, id, [])).ToHashSet();
            examined.UnionWith(group);
            var restsOnOthers = edges.Any(edge =>
                group.Contains(edge.Moving) && unresolved.Contains(edge.Support) && !group.Contains(edge.Support));
            if (restsOnOthers) continue;
            foreach (var member in group) LimitDrop(member, 0);
        }
        Propagate();
        pairLandings = kept;
        return ids.ToDictionary(id => id, id => startY[id] - drops[id]);
    }
}
