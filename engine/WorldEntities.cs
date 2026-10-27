namespace FourD;

public sealed partial class World
{
    private sealed record Dynamic(TurnableDefinition Definition, Pose4 Pose, SectionId? Section, SectionId? Origin, RotationPlan? Upright = null);
    private readonly Dictionary<EntityId, Dynamic> dynamicEntities = [];
    private sealed record Actor(Creature3Definition Definition, Pose4 Pose, double Yaw);
    private readonly Dictionary<EntityId, Actor> actors = [];
    private static EntityState State(Actor a) => new(a.Definition.Id, a.Definition, a.Pose, a.Definition.Section, a.Yaw, null, null, Array.Empty<EntityId>(), null, false, PoseHandedness(a.Pose));
    private readonly HashSet<EntityId> retired = [];
    private void AddInitial(TurnableDefinition d)
    {
        var parts = d.Shape.Parts.Select(p => p with { FaceUvs = Array.AsReadOnly(p.FaceUvs.ToArray()) }).ToArray();
        var copy = d with { Shape = new(Array.AsReadOnly(parts)) };
        dynamicEntities.Add(d.Id, new(copy, d.Pose, d.Section, null));
    }
    private static Handedness PoseHandedness(Pose4 pose)
    {
        var r = pose.Rotation;
        var determinant = r.X.X * (r.Y.Y * r.Z.Z - r.Y.Z * r.Z.Y) - r.Y.X * (r.X.Y * r.Z.Z - r.X.Z * r.Z.Y) + r.Z.X * (r.X.Y * r.Y.Z - r.X.Z * r.Y.Y);
        return determinant > 1e-10 ? Handedness.Normal : determinant < -1e-10 ? Handedness.Mirrored : Handedness.NotComparable;
    }
    private EntityState State(Dynamic d) => new(d.Definition.Id, d.Definition, d.Pose, d.Section, null, d.Origin, null, Array.Empty<EntityId>(), null, rotations.ContainsKey(d.Definition.Id), PoseHandedness(d.Pose));
    private SliceGeometry? Slice(Dynamic d, double w)
    {
        var positions = new List<double>(); var normals = new List<double>(); var uvs = new List<double>(); var ids = new List<PartId>();
        var boxes = d.Definition.Shape.Parts.OrderBy(p => p.Id.Value, StringComparer.Ordinal).Select(p => (Part: p, Box: Box4.From(d.Definition, d.Pose, p))).ToArray();
        foreach (var (part, box) in boxes) BoxSlice.Append(box, part, w, positions, normals, uvs, ids);
        if (ids.Count == 0) return null;
        var min = boxes.Min(x => x.Box.Center[3] - x.Box.Radius([0, 0, 0, 1]));
        var max = boxes.Max(x => x.Box.Center[3] + x.Box.Radius([0, 0, 0, 1]));
        return new(d.Definition.Id, positions.AsReadOnly(), normals.AsReadOnly(), uvs.AsReadOnly(), ids.AsReadOnly(), max - min > 2 * Epsilon + 1e-10);
    }
    private IEnumerable<SliceGeometry> DynamicSlices(double w) => dynamicEntities.Values.Select(d => Slice(d, w)).OfType<SliceGeometry>();
    public AddEntityResult AddEntity(EntityDefinition definition)
    {
        Failure Error(FailureCode code, string detail) => new(code, detail, definition?.Id, null, Array.Empty<Contact>());
        if (definition is null) return new AddEntityResult.Rejected(Error(FailureCode.InvalidInput, "Definition is required."));
        if (entities.Any(e => e.Id == definition.Id) || dynamicEntities.ContainsKey(definition.Id) || actors.ContainsKey(definition.Id) || retired.Contains(definition.Id)) return new AddEntityResult.Rejected(Error(FailureCode.DuplicateId, "ID is already used."));
        if (definition is Creature3Definition actor)
        {
            var actorErrors = WorldValidation.Check(new(sections, buildings, staticDefinitions.Values.Concat(dynamicEntities.Values.Select(d => (EntityDefinition)d.Definition)).Concat(actors.Values.Select(a => (EntityDefinition)a.Definition)).Append(actor).ToArray(), [], [], []));
            if (actorErrors.Length > 0) return new AddEntityResult.Rejected(new(FailureCode.InvalidInput, $"{actorErrors[0].Source.NodePath}:{actorErrors[0].Source.Property}: {actorErrors[0].Detail}", actor.Id, null, actorErrors[0].Contacts));
            actors.Add(actor.Id, new(actor, actor.Pose, 0)); revision++; Enqueue(new EntityAdded(actor.Id), time);
            return new AddEntityResult.Added(State(actors[actor.Id]));
        }
        if (definition is not TurnableDefinition t) return new AddEntityResult.Rejected(Error(FailureCode.WrongEntityKind, "Only turnables can currently be added."));
        var errors = WorldValidation.Check(new(sections, buildings, staticDefinitions.Values.Append(t).ToArray(), [], [], []));
        if (errors.Length > 0) return new AddEntityResult.Rejected(new(FailureCode.InvalidInput, $"{errors[0].Source.NodePath}:{errors[0].Source.Property}: {errors[0].Detail}", t.Id, null, errors[0].Contacts));
        var candidate = t.Shape.Parts.Select(p => Box4.From(t, t.Pose, p));
        if (IntersectsReservation(candidate, t.Id)) return new AddEntityResult.Rejected(Error(FailureCode.TranslationBlocked, "Entity intersects a reserved rotation sweep."));
        AddInitial(t); revision++;
        Enqueue(new EntityAdded(t.Id), time);
        return new AddEntityResult.Added(State(dynamicEntities[t.Id]));
    }
    private static Failure ErrorInUse(EntityId entity) => new(FailureCode.EntityInUse, "Entity is rotating.", entity, null, Array.Empty<Contact>());
    public RemoveEntityResult RemoveEntity(EntityId entity)
    {
        if (rotations.ContainsKey(entity)) return new RemoveEntityResult.Rejected(ErrorInUse(entity));
        if (actors.Remove(entity)) { retired.Add(entity); revision++; Enqueue(new EntityRemoved(entity), time); return new RemoveEntityResult.Removed(entity); }
        if (dynamicEntities.Remove(entity)) { retired.Add(entity); revision++; Enqueue(new EntityRemoved(entity), time); return new RemoveEntityResult.Removed(entity); }
        return new RemoveEntityResult.Rejected(new(entities.Any(e => e.Id == entity) ? FailureCode.WrongEntityKind : FailureCode.UnknownId, "Cannot remove entity.", entity, null, Array.Empty<Contact>()));
    }
    public EntityReadResult ReadEntity(EntityId entity)
    {
        if (actors.TryGetValue(entity, out var actor)) return new EntityReadResult.Found(State(actor));
        if (dynamicEntities.TryGetValue(entity, out var d)) return new EntityReadResult.Found(State(d));
        if (!staticDefinitions.TryGetValue(entity, out var definition)) return new EntityReadResult.Missing(entity);
        var section = definition is FixedDefinition fixedItem ? fixedItem.Section : (SectionId?)null;
        return new EntityReadResult.Found(new(entity, definition, definition.Pose, section, null, null, null, Array.Empty<EntityId>(), null, false, definition is FixedDefinition ? PoseHandedness(definition.Pose) : Handedness.NotComparable));
    }
}
