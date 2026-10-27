namespace FourD;

public sealed partial class World
{
    private sealed record Dynamic(TurnableDefinition Definition, Pose4 Pose, SectionId? Section, SectionId? Origin, RotationPlan? Upright = null);
    private sealed record Actor(Creature3Definition Definition, Pose4 Pose, double Yaw);

    private readonly Dictionary<EntityId, Dynamic> dynamicEntities = [];
    private readonly Dictionary<EntityId, Actor> actors = [];
    private readonly HashSet<EntityId> retired = [];

    private void AddInitial(TurnableDefinition turnable)
    {
        var copy = turnable with { Shape = new(Array.AsReadOnly(CopyParts(turnable.Shape))) };
        dynamicEntities.Add(turnable.Id, new(copy, turnable.Pose, turnable.Section, null));
    }

    private static Handedness PoseHandedness(Pose4 pose)
    {
        var rotation = pose.Rotation;
        var determinant = rotation.X.X * (rotation.Y.Y * rotation.Z.Z - rotation.Y.Z * rotation.Z.Y)
            - rotation.Y.X * (rotation.X.Y * rotation.Z.Z - rotation.X.Z * rotation.Z.Y)
            + rotation.Z.X * (rotation.X.Y * rotation.Y.Z - rotation.X.Z * rotation.Y.Y);
        return determinant switch
        {
            > RoundingMargin => Handedness.Normal,
            < -RoundingMargin => Handedness.Mirrored,
            _ => Handedness.NotComparable
        };
    }

    private static EntityState State(Actor actor) => new(
        actor.Definition.Id, actor.Definition, actor.Pose, actor.Definition.Section, actor.Yaw,
        UprightOrigin: null, Holder: null, CarriedItems: [], DevicePosition: null, Rotating: false,
        Handedness: PoseHandedness(actor.Pose));

    private EntityState State(Dynamic item) => new(
        item.Definition.Id, item.Definition, item.Pose, item.Section, ActorYaw: null,
        item.Origin, Holder: null, CarriedItems: [], DevicePosition: null,
        Rotating: rotations.ContainsKey(item.Definition.Id), Handedness: PoseHandedness(item.Pose));

    private SliceGeometry? Slice(Dynamic item, double w)
    {
        var positions = new List<double>();
        var normals = new List<double>();
        var uvs = new List<double>();
        var ids = new List<PartId>();
        var boxes = item.Definition.Shape.Parts
            .OrderBy(part => part.Id.Value, StringComparer.Ordinal)
            .Select(part => (Part: part, Box: Box4.From(item.Definition, item.Pose, part)))
            .ToArray();
        foreach (var (part, box) in boxes) BoxSlice.Append(box, part, w, positions, normals, uvs, ids);
        if (ids.Count == 0) return null;
        var min = boxes.Min(entry => entry.Box.WSpan.Min);
        var max = boxes.Max(entry => entry.Box.WSpan.Max);
        return new(item.Definition.Id, positions.AsReadOnly(), normals.AsReadOnly(), uvs.AsReadOnly(), ids.AsReadOnly(),
            max - min > 2 * Epsilon + RoundingMargin);
    }

    private IEnumerable<SliceGeometry> DynamicSlices(double w) =>
        dynamicEntities.Values.Select(item => Slice(item, w)).OfType<SliceGeometry>();

    public AddEntityResult AddEntity(EntityDefinition definition)
    {
        AddEntityResult Reject(FailureCode code, string detail) =>
            new AddEntityResult.Rejected(Fail(code, detail, definition?.Id));
        if (definition is null) return Reject(FailureCode.InvalidInput, "Definition is required.");
        if (IsUsed(definition.Id)) return Reject(FailureCode.DuplicateId, "ID is already used.");
        return definition switch
        {
            Creature3Definition actor => AddActor(actor),
            TurnableDefinition turnable => AddTurnable(turnable),
            _ => Reject(FailureCode.WrongEntityKind, "Only turnables can currently be added.")
        };
    }

    private bool IsUsed(EntityId id) =>
        entities.Any(entity => entity.Id == id) || dynamicEntities.ContainsKey(id) || actors.ContainsKey(id) || retired.Contains(id);

    private AddEntityResult AddActor(Creature3Definition actor)
    {
        var definitions = staticDefinitions.Values
            .Concat(dynamicEntities.Values.Select(item => (EntityDefinition)item.Definition))
            .Concat(actors.Values.Select(other => (EntityDefinition)other.Definition))
            .Append(actor);
        if (ValidationFailure(definitions, actor.Id) is { } failure) return new AddEntityResult.Rejected(failure);
        actors.Add(actor.Id, new(actor, actor.Pose, 0));
        revision++;
        Enqueue(new EntityAdded(actor.Id), time);
        return new AddEntityResult.Added(State(actors[actor.Id]));
    }

    private AddEntityResult AddTurnable(TurnableDefinition turnable)
    {
        if (ValidationFailure(staticDefinitions.Values.Append(turnable), turnable.Id) is { } failure)
            return new AddEntityResult.Rejected(failure);
        if (IntersectsReservation(Box4.From(turnable, turnable.Pose), turnable.Id))
            return new AddEntityResult.Rejected(Fail(FailureCode.TranslationBlocked, "Entity intersects a reserved rotation sweep.", turnable.Id));
        AddInitial(turnable);
        revision++;
        Enqueue(new EntityAdded(turnable.Id), time);
        return new AddEntityResult.Added(State(dynamicEntities[turnable.Id]));
    }

    // The first validation error of the region with these entities as an InvalidInput failure, or null when it is valid.
    private Failure? ValidationFailure(IEnumerable<EntityDefinition> definitions, EntityId added)
    {
        var errors = WorldValidation.Check(new(sections, buildings, definitions.ToArray(), [], [], []));
        if (errors.Length == 0) return null;
        var first = errors[0];
        return Fail(FailureCode.InvalidInput, $"{first.Source.NodePath}:{first.Source.Property}: {first.Detail}", added, first.Contacts);
    }

    public RemoveEntityResult RemoveEntity(EntityId entity)
    {
        if (rotations.ContainsKey(entity))
            return new RemoveEntityResult.Rejected(Fail(FailureCode.EntityInUse, "Entity is rotating.", entity));
        if (actors.Remove(entity) || dynamicEntities.Remove(entity))
        {
            retired.Add(entity);
            revision++;
            Enqueue(new EntityRemoved(entity), time);
            return new RemoveEntityResult.Removed(entity);
        }
        var code = entities.Any(solid => solid.Id == entity) ? FailureCode.WrongEntityKind : FailureCode.UnknownId;
        return new RemoveEntityResult.Rejected(Fail(code, "Cannot remove entity.", entity));
    }

    public EntityReadResult ReadEntity(EntityId entity)
    {
        if (actors.TryGetValue(entity, out var actor)) return new EntityReadResult.Found(State(actor));
        if (dynamicEntities.TryGetValue(entity, out var item)) return new EntityReadResult.Found(State(item));
        if (!staticDefinitions.TryGetValue(entity, out var definition)) return new EntityReadResult.Missing(entity);
        var fixedObject = definition as FixedDefinition;
        return new EntityReadResult.Found(new(
            entity, definition, definition.Pose, fixedObject?.Section, ActorYaw: null, UprightOrigin: null, Holder: null,
            CarriedItems: [], DevicePosition: null, Rotating: false,
            Handedness: fixedObject is null ? Handedness.NotComparable : PoseHandedness(definition.Pose)));
    }

    public UpdateResult SynchronizeActor(EntityId actor, Vec3 position, double yaw)
    {
        if (!actors.TryGetValue(actor, out var state))
            return new UpdateResult.Rejected(Fail(KindOrUnknown(actor), "Unknown actor.", actor));
        if (!new[] { position.X, position.Y, position.Z, yaw }.All(double.IsFinite))
            return new UpdateResult.Rejected(Fail(FailureCode.InvalidInput, "Position and yaw must be finite.", actor));
        actors[actor] = state with
        {
            Pose = state.Pose with { Position = new(position.X, position.Y, position.Z, state.Pose.Position.W) },
            Yaw = yaw
        };
        revision++;
        return new UpdateResult.Updated();
    }

    public HandednessResult ReadHandedness(EntityId entity) => ReadEntity(entity) switch
    {
        EntityReadResult.Found found => new HandednessResult.Value(found.Entity.Handedness),
        _ => new HandednessResult.Rejected(Fail(FailureCode.UnknownId, "Unknown entity.", entity))
    };

    public HandednessComparisonResult CompareHandedness(EntityId a, EntityId b)
    {
        if (ReadHandedness(a) is not HandednessResult.Value left)
            return new HandednessComparisonResult.Rejected(Fail(FailureCode.UnknownId, "Unknown entity.", a));
        if (ReadHandedness(b) is not HandednessResult.Value right)
            return new HandednessComparisonResult.Rejected(Fail(FailureCode.UnknownId, "Unknown entity.", b));
        var comparison = left.Handedness == Handedness.NotComparable || right.Handedness == Handedness.NotComparable
            ? HandednessComparison.NotComparable
            : left.Handedness == right.Handedness ? HandednessComparison.Same : HandednessComparison.Different;
        return new HandednessComparisonResult.Comparison(comparison);
    }
}
