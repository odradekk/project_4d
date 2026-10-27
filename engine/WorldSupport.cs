namespace FourD;
public sealed partial class World
{
    private double time;
    private long sequence;
    private readonly List<EventOccurrence> events = [];
    private void Enqueue(SpaceEvent value,double at) => events.Add(new(at,++sequence,value));
    private IEnumerable<Box4> Boxes()
    {
        foreach(var entity in entities) foreach(var part in entity.Parts) foreach(var segment in entity.Segments) yield return Box4.From(entity,part,segment);
        foreach(var d in dynamicEntities.Values) foreach(var part in d.Definition.Shape.Parts) yield return Box4.From(d.Definition,d.Pose,part);
    }
    /// <summary>Finds the highest actual contact while translating the entity downward.</summary>
    public SupportResult FindSupport(EntityId entity)
    {
        if(ReadEntity(entity) is not EntityReadResult.Found found)
            return new SupportResult.Rejected(new(FailureCode.UnknownId,"Unknown entity.",entity,null,Array.Empty<Contact>()));
        var candidates=SupportCandidates(entity).ToArray();
        if(candidates.Length==0) return new SupportResult.Unsupported();
        var best=candidates.Min(c=>Math.Max(c.Entry,0));
        var contacts=candidates.Where(c=>Math.Max(c.Entry,0)<=best+1e-9).Select(c=>c.Contact)
            .OrderBy(c=>c.Support.Value,StringComparer.Ordinal).ThenBy(c=>c.Part.Value,StringComparer.Ordinal).ToArray();
        return new SupportResult.Supported(found.Entity.Pose.Position.Y-best,Array.AsReadOnly(contacts));
    }
    private sealed record SupportCandidate(double Entry, SupportContact Contact);
    private IEnumerable<SupportCandidate> SupportCandidates(EntityId entity)
    {
        var boxes=Boxes().ToArray();
        foreach(var a in boxes.Where(b=>b.Entity==entity)) foreach(var b in boxes.Where(b=>b.Entity!=entity))
        {
            var delta=Box4.Sub(a.Center,b.Center); var entry=double.NegativeInfinity; var exit=double.PositiveInfinity;
            double[]? normal=null; var minDepth=double.PositiveInfinity; var blocked=false;
            foreach(var axis in Box4.SeparatingAxes(a,b))
            {
                var radius=a.Radius(axis)+b.Radius(axis); var d=Box4.Dot(axis,delta); var depth=radius-Math.Abs(d);
                if(depth<minDepth) minDepth=depth;
                if(Math.Abs(axis[1])<1e-10)
                {
                    // A purely vertical edge cannot support an object sliding past it.
                    if(depth<=1e-10) {blocked=true;break;}
                    continue;
                }
                var t1=(d-radius)/axis[1]; var t2=(d+radius)/axis[1]; var lower=Math.Min(t1,t2); var upper=Math.Max(t1,t2);
                if(lower>entry) {entry=lower;normal=axis[1]>0?axis:axis.Select(v=>-v).ToArray();}
                exit=Math.Min(exit,upper);
            }
            if(blocked || normal is null || exit<=Math.Max(entry,0)+1e-10) continue;
            if(entry<0 && (minDepth< -1e-10 || minDepth>.005+1e-10 || Box4.Dot(normal,delta)<=0)) continue;
            var center=a.Center.ToArray(); center[1]-=Math.Max(entry,0); var landed=a with {Center=center};
            var point=Box4.IntersectionPoint(landed,b);
            if(point is not null) yield return new(entry,new(b.Entity,b.Part,Box4.Vec(point),Box4.Vec(normal)));
        }
    }
    /// <summary>Advances time and places unsupported turnables on their highest support.</summary>
    public StepResult Step(double delta)
    {
        if(!double.IsFinite(delta) || delta<=0 || !double.IsFinite(time+delta))
            return new StepResult.Rejected(new(FailureCode.InvalidInput,"Delta must advance finite time.",null,null,Array.Empty<Contact>()));
        var target=time+delta;
        foreach(var active in rotations.Values.OrderBy(a=>a.Started+a.Plan.Duration).ThenBy(a=>a.Plan.Entity.Value,StringComparer.Ordinal).ToArray())
        {
            var plan=active.Plan; var end=active.Started+plan.Duration; var done=target>=end;
            var elapsed=done ? plan.Duration : Math.Clamp(target-active.Started,0,plan.Duration);
            var slow=plan.Duration*.25; var first=(Math.PI/36)/Math.Abs(plan.Angle);
            var progress=done ? 1 : first>1 ? elapsed/plan.Duration : elapsed<=slow ? first*elapsed/slow : first+(1-first)*(elapsed-slow)/(plan.Duration-slow);
            progress=Math.Clamp(progress,0,1);
            var item=dynamicEntities[plan.Entity];
            dynamicEntities[plan.Entity]=item with {Pose=At(plan,progress)};
            if(done)
            {
                var finished=dynamicEntities[plan.Entity];
                if(plan.Laying && item.Upright is RotationPlan upright)
                    finished=finished with {Pose=upright.Start with {Position=Add(upright.Start.Position,Sub(plan.Pivot,upright.Pivot))}};
                else if(plan.Laying)
                    finished=finished with {Pose=finished.Pose with {Position=finished.Pose.Position with {W=sections.Single(s=>s.Id==plan.Origin).W}}};
                dynamicEntities[plan.Entity]=finished with {Section=plan.Laying ? plan.Origin : plan.Flip ? item.Section : null, Origin=plan.Laying || plan.Flip ? null : plan.Origin, Upright=plan.Laying || plan.Flip ? null : plan};
                rotations.Remove(plan.Entity); Enqueue(new RotationEnded(plan.Entity),end);
            }
        }
        var held=new HashSet<EntityId>(); Dictionary<EntityId,double> resting;
        while(true)
        {
            resting=SettledHeights(held); var added=false;
            foreach(var id in dynamicEntities.Keys.Where(id=>!held.Contains(id) && !rotations.ContainsKey(id)))
            {
                var item=dynamicEntities[id]; var old=item.Pose.Position; var distance=old.Y-resting[id];
                if(distance<=1e-10) continue;
                var steps=Math.Max(1,(int)Math.Ceiling(distance/SamplingDistance));
                if(Enumerable.Range(1,steps).Any(i=>IntersectsReservation(item.Definition.Shape.Parts.Select(p=>Box4.From(item.Definition,item.Pose with {Position=old with {Y=old.Y-distance*i/steps}},p)),id))) {held.Add(id);added=true;}
            }
            if(!added) break;
        }
        foreach(var id in dynamicEntities.Keys.OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray())
        {
            if(rotations.ContainsKey(id)) continue;
            var item=dynamicEntities[id]; var finalY=resting[id];
            if(finalY>=item.Pose.Position.Y-1e-10) continue;
            var old=item.Pose.Position; var updated=item.Pose with {Position=old with {Y=finalY}};
            dynamicEntities[id]=item with {Pose=updated}; Enqueue(new EntityFell(id,old.Y,finalY),target);
        }
        time=target; revision++; var result=events.OrderBy(e=>e.Time).ThenBy(e=>e.Sequence).ToArray(); events.Clear();
        return new StepResult.Advanced(time,Array.AsReadOnly(result));
    }
}
