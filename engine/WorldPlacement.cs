namespace FourD;
public sealed partial class World
{
    /// <summary>Atomically places an unheld turnable at a validated full four-dimensional pose.</summary>
    public UpdateResult PlaceEntity(EntityId entity,Pose4 pose)
    {
        UpdateResult Reject(FailureCode code,string detail,IReadOnlyList<Contact>? contacts=null) => new UpdateResult.Rejected(new(code,detail,entity,null,contacts ?? Array.Empty<Contact>()));
        if(!dynamicEntities.TryGetValue(entity,out var item)) return Reject(entities.Any(e=>e.Id==entity)?FailureCode.WrongEntityKind:FailureCode.UnknownId,"Unknown or unsupported entity.");
        var axes=new[] {Box4.V(pose.Rotation.X),Box4.V(pose.Rotation.Y),Box4.V(pose.Rotation.Z),Box4.V(pose.Rotation.W)};
        if(Box4.V(pose.Position).Concat(axes.SelectMany(x=>x)).Any(v=>!double.IsFinite(v)) || !WorldValidation.Rotation(axes)) return Reject(FailureCode.InvalidInput,"Invalid pose.");
        var origin=item.Origin ?? item.Section!.Value;
        var section=sections.Single(s=>s.Id==origin);
        var flat=Math.Abs(pose.Rotation.W.X)<=1e-10 && Math.Abs(pose.Rotation.W.Y)<=1e-10 && Math.Abs(pose.Rotation.W.Z)<=1e-10;
        if(flat && pose.Position.W!=section.W) return Reject(FailureCode.WrongSection,"Flat object must return to its origin section.");
        var proposed=item with {Pose=pose,Section=flat?origin:null,Origin=flat?null:origin};
        if(!flat && Slice(proposed,section.W) is null) return Reject(FailureCode.NoSlice,"Object must retain a slice in its origin section.");
        var owner=buildings.Single(b=>b.Id==item.Definition.Building);
        var boxes=item.Definition.Shape.Parts.Select(p=>Box4.From(item.Definition,pose,p)).ToArray();
        if(boxes.Any(b=>!b.Representable())) return Reject(FailureCode.InvalidInput,"Pose cannot represent nondegenerate box geometry.");
        var longAxis=axes[(int)item.Definition.Interaction.LongAxis];
        if(Math.Abs(Math.Abs(longAxis[3])-1)<=1e-10 && boxes.Any(b=>sections.Any(s=>
            Math.Abs(s.W-(b.Center[3]-b.Radius([0,0,0,1])))<.02-1e-10 || Math.Abs(s.W-(b.Center[3]+b.Radius([0,0,0,1])))<.02-1e-10)))
            return Reject(FailureCode.InvalidInput,"Upright part end is too close to a section.");
        if(boxes.Any(b=>b.Center[3]-b.Radius([0,0,0,1])<owner.WRange.Min || b.Center[3]+b.Radius([0,0,0,1])>owner.WRange.Max)) return Reject(FailureCode.OutOfRange,"Object extends outside its building.");
        var contacts=new List<Contact>();
        foreach(var a in boxes) foreach(var b in Boxes().Where(b=>b.Entity!=entity))
        {
            if(!Box4.Overlaps(a,b,.005)) continue;
            var point=Box4.IntersectionPoint(a,b); if(point is null) continue;
            var axis=Box4.SeparatingAxes(a,b).MinBy(n=>a.Radius(n)+b.Radius(n)-Math.Abs(Box4.Dot(n,Box4.Sub(a.Center,b.Center))))!;
            if(Box4.Dot(axis,Box4.Sub(a.Center,b.Center))<0) axis=axis.Select(x=>-x).ToArray();
            contacts.Add(new(entity,a.Part,b.Entity,b.Part,Box4.Vec(point),Box4.Vec(axis),null));
        }
        if(contacts.Count>0) return Reject(FailureCode.TranslationBlocked,"Target pose is occupied.",contacts.AsReadOnly());
        dynamicEntities[entity]=proposed;
        return new UpdateResult.Updated();
    }
    /// <summary>Reports whether a given entity has a three-dimensional slice at the section.</summary>
    public SlicePresenceResult HasSlice(EntityId entity,SectionId section)
    {
        var target=sections.FirstOrDefault(s=>s.Id==section);
        if(target is null || (!dynamicEntities.ContainsKey(entity) && entities.All(e=>e.Id!=entity)))
            return new SlicePresenceResult.Rejected(new(FailureCode.UnknownId,"Unknown entity or section.",entity,null,Array.Empty<Contact>()));
        var geometry=(SectionGeometryResult.Geometry)GetGeometryAtW(target.W);
        return new SlicePresenceResult.Present(geometry.Entities.Any(e=>e.Entity==entity));
    }
}
