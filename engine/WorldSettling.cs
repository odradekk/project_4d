namespace FourD;

public sealed partial class World
{
    // `Moving` rests on `Support` after falling `Gap` more than `Support` does.
    private sealed record SupportEdge(EntityId Support, EntityId Moving, double Gap);

    // Where the solids of one entity land on those of another, kept while neither entity's record is replaced. A
    // settle keeps the pairs it used and drops the rest.
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
