namespace FourD;

public sealed partial class World
{
    private sealed record Dynamic(TurnableDefinition Definition, Pose4 Pose, SectionId? Section, SectionId? Origin);
    private readonly Dictionary<EntityId, Dynamic> dynamicEntities = [];
    private readonly HashSet<EntityId> retired = [];
    private void AddInitial(TurnableDefinition d)
    {
        var parts=d.Shape.Parts.Select(p=>p with { FaceUvs=Array.AsReadOnly(p.FaceUvs.ToArray()) }).ToArray();
        var copy=d with { Shape=new(Array.AsReadOnly(parts)) };
        dynamicEntities.Add(d.Id,new(copy,d.Pose,d.Section,null));
    }
    private static Handedness PoseHandedness(Pose4 pose)
    {
        var r=pose.Rotation;
        var determinant=r.X.X*(r.Y.Y*r.Z.Z-r.Y.Z*r.Z.Y)-r.Y.X*(r.X.Y*r.Z.Z-r.X.Z*r.Z.Y)+r.Z.X*(r.X.Y*r.Y.Z-r.X.Z*r.Y.Y);
        return determinant>1e-10 ? Handedness.Normal : determinant < -1e-10 ? Handedness.Mirrored : Handedness.NotComparable;
    }
    private EntityState State(Dynamic d) => new(d.Definition.Id,d.Definition,d.Pose,d.Section,null,d.Origin,null,Array.Empty<EntityId>(),null,false,PoseHandedness(d.Pose));
    private SliceGeometry? Slice(Dynamic d,double w)
    {
        var positions=new List<double>(); var normals=new List<double>(); var uvs=new List<double>(); var ids=new List<PartId>();
        var boxes=d.Definition.Shape.Parts.OrderBy(p=>p.Id.Value,StringComparer.Ordinal).Select(p=>(Part:p,Box:Box4.From(d.Definition,d.Pose,p))).ToArray();
        foreach(var (part,box) in boxes) BoxSlice.Append(box,part,w,positions,normals,uvs,ids);
        if(ids.Count==0) return null;
        var min=boxes.Min(x=>x.Box.Center[3]-x.Box.Radius([0,0,0,1]));
        var max=boxes.Max(x=>x.Box.Center[3]+x.Box.Radius([0,0,0,1]));
        return new(d.Definition.Id,positions.AsReadOnly(),normals.AsReadOnly(),uvs.AsReadOnly(),ids.AsReadOnly(),max-min>2*Epsilon+1e-10);
    }
    private IEnumerable<SliceGeometry> DynamicSlices(double w) => dynamicEntities.Values.Select(d=>Slice(d,w)).OfType<SliceGeometry>();
    public AddEntityResult AddEntity(EntityDefinition definition)
    {
        Failure Error(FailureCode code,string detail) => new(code,detail,definition?.Id,null,Array.Empty<Contact>());
        if(definition is null) return new AddEntityResult.Rejected(Error(FailureCode.InvalidInput,"Definition is required."));
        if(entities.Any(e=>e.Id==definition.Id) || dynamicEntities.ContainsKey(definition.Id) || retired.Contains(definition.Id)) return new AddEntityResult.Rejected(Error(FailureCode.DuplicateId,"ID is already used."));
        if(definition is not TurnableDefinition t) return new AddEntityResult.Rejected(Error(FailureCode.WrongEntityKind,"Only turnables can currently be added."));
        var errors=WorldValidation.Check(new(sections,buildings,staticDefinitions.Values.Append(t).ToArray(),[],[],[]));
        if(errors.Length>0) return new AddEntityResult.Rejected(new(FailureCode.InvalidInput,$"{errors[0].Source.NodePath}:{errors[0].Source.Property}: {errors[0].Detail}",t.Id,null,errors[0].Contacts));
        AddInitial(t);
        Enqueue(new EntityAdded(t.Id),time);
        return new AddEntityResult.Added(State(dynamicEntities[t.Id]));
    }
    public RemoveEntityResult RemoveEntity(EntityId entity)
    {
        if(dynamicEntities.Remove(entity)) {retired.Add(entity);Enqueue(new EntityRemoved(entity),time);return new RemoveEntityResult.Removed(entity);}
        return new RemoveEntityResult.Rejected(new(entities.Any(e=>e.Id==entity)?FailureCode.WrongEntityKind:FailureCode.UnknownId,"Cannot remove entity.",entity,null,Array.Empty<Contact>()));
    }
    public EntityReadResult ReadEntity(EntityId entity)
    {
        if(dynamicEntities.TryGetValue(entity,out var d)) return new EntityReadResult.Found(State(d));
        if(!staticDefinitions.TryGetValue(entity,out var definition)) return new EntityReadResult.Missing(entity);
        var section=definition is FixedDefinition fixedItem ? fixedItem.Section : (SectionId?)null;
        return new EntityReadResult.Found(new(entity,definition,definition.Pose,section,null,null,null,Array.Empty<EntityId>(),null,false,definition is FixedDefinition ? PoseHandedness(definition.Pose) : Handedness.NotComparable));
    }
}
