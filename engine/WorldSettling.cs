namespace FourD;

public sealed partial class World
{
    private sealed record SupportEdge(EntityId Support, EntityId Moving, double Gap);

    // Each initial contact limits how far an object can fall relative to its support.
    private Dictionary<EntityId,double> SettledHeights(IReadOnlySet<EntityId> held)
    {
        var ids=dynamicEntities.Keys.ToArray();
        var initial=ids.ToDictionary(id=>id,id=>dynamicEntities[id].Pose.Position.Y);
        var drops=ids.ToDictionary(id=>id,_=>double.PositiveInfinity);
        var edges=new List<SupportEdge>();
        var queue=new PriorityQueue<EntityId,double>();
        void Anchor(EntityId id,double drop)
        {
            if(drop>=drops[id]) return;
            drops[id]=drop; queue.Enqueue(id,drop);
        }
        foreach(var id in ids)
        {
            if(held.Contains(id) || rotations.ContainsKey(id)) { Anchor(id,0); continue; }
            var candidates=SupportCandidates(id).ToArray();
            if(candidates.Length==0) Anchor(id,0);
            foreach(var candidate in candidates)
            {
                var gap=Math.Max(candidate.Entry,0);
                var support=candidate.Contact.Support;
                if(initial.ContainsKey(support)) edges.Add(new(support,id,gap));
                else Anchor(id,gap);
            }
        }
        Propagate();

        // Only sink components without an external anchor are held in place.
        // A component above one of them remains free to fall onto it.
        var unresolved=ids.Where(id=>double.IsPositiveInfinity(drops[id])).ToHashSet();
        bool Reaches(EntityId from,EntityId target,HashSet<EntityId> visited)
        {
            if(from==target) return true;
            if(!visited.Add(from)) return false;
            return edges.Where(e=>e.Moving==from && unresolved.Contains(e.Support))
                .Any(e=>Reaches(e.Support,target,visited));
        }
        var examined=new HashSet<EntityId>();
        foreach(var id in unresolved)
        {
            if(examined.Contains(id)) continue;
            var component=unresolved.Where(other=>Reaches(id,other,[]) && Reaches(other,id,[])).ToHashSet();
            examined.UnionWith(component);
            if(edges.Any(e=>component.Contains(e.Moving) && unresolved.Contains(e.Support) && !component.Contains(e.Support))) continue;
            foreach(var member in component) Anchor(member,0);
        }
        Propagate();
        return ids.ToDictionary(id=>id,id=>initial[id]-drops[id]);

        void Propagate()
        {
            while(queue.TryDequeue(out var support,out var drop))
            {
                if(drop>drops[support]) continue;
                foreach(var edge in edges.Where(e=>e.Support==support)) Anchor(edge.Moving,drop+edge.Gap);
            }
        }
    }
}
