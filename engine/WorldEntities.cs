namespace FourD;

public sealed partial class World
{
    // A turnable object or an item. `Holder` is whoever holds it in hand or has grabbed its slice, or carries it in a
    // backpack, where the item is `Packed`: it has no slice and is no obstacle.
    private sealed record Dynamic(
        EntityDefinition Definition, Pose4 Pose, SectionId? Section, SectionId? Origin, RotationPlan? Upright = null,
        EntityId? Holder = null, bool Packed = false);
    private sealed record Actor(Creature3Definition Definition, Pose4 Pose, double Yaw, SectionId Section);

    private readonly Dictionary<EntityId, Dynamic> dynamicEntities = [];
    private readonly Dictionary<EntityId, Actor> actors = [];
    private readonly HashSet<EntityId> retired = [];

    private void AddInitial(EntityDefinition definition)
    {
        var section = definition switch
        {
            TurnableDefinition turnable => turnable.Section,
            ItemDefinition { Shape: HiddenRingItem } => sections.Single(candidate => candidate.W == definition.Pose.Position.W).Id,
            _ => (SectionId?)null
        };
        dynamicEntities.Add(definition.Id, new(CopyOf(definition), definition.Pose, section, null));
    }

    private static EntityDefinition CopyOf(EntityDefinition definition) => definition switch
    {
        TurnableDefinition turnable => turnable with { Shape = CopyOf(turnable.Shape) },
        ItemDefinition { Shape: HiddenRingItem hidden } item => item with { Shape = hidden with { Body = CopyOf(hidden.Body) } },
        _ => definition
    };

    private static BoxAssembly CopyOf(BoxAssembly shape) => new(Array.AsReadOnly(CopyParts(shape)));

    private static Handedness PoseHandedness(Pose4 pose)
    {
        var rotation = pose.Rotation;
        var determinant = rotation.X.X * (rotation.Y.Y * rotation.Z.Z - rotation.Y.Z * rotation.Z.Y)
            - rotation.Y.X * (rotation.X.Y * rotation.Z.Z - rotation.X.Z * rotation.Z.Y)
            + rotation.Z.X * (rotation.X.Y * rotation.Y.Z - rotation.X.Z * rotation.Y.Y);
        // A determinant within the rounding margin of 0 is a degenerate XYZ projection with no handedness
        // (docs/frame/contract-views.md). It compares a computed value, so it uses the rounding margin and not
        // the tolerance for host matrices.
        return determinant switch
        {
            > RoundingMargin => Handedness.Normal,
            < -RoundingMargin => Handedness.Mirrored,
            _ => Handedness.NotComparable
        };
    }

    // Held items that have not been stood up are carried along; a stood-up one is only grabbed.
    private EntityState State(Actor actor) => new(
        actor.Definition.Id, actor.Definition, actor.Pose, actor.Section, actor.Yaw,
        UprightOrigin: null, Holder: null, InBackpack: false,
        CarriedItems: Array.AsReadOnly(dynamicEntities.Values
            .Where(item => item.Holder == actor.Definition.Id && item.Origin is null)
            .Select(item => item.Definition.Id)
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .ToArray()),
        DevicePosition: null, Rotating: false, Handedness: PoseHandedness(actor.Pose));

    private EntityState State(Dynamic item) => new(
        item.Definition.Id, item.Definition, item.Pose, item.Section, ActorYaw: null,
        item.Origin, item.Holder, item.Packed, CarriedItems: [], DevicePosition: null,
        Rotating: rotations.ContainsKey(item.Definition.Id),
        Handedness: DynamicShapes.IsBall(item.Definition) ? Handedness.NotComparable : PoseHandedness(item.Pose));

    // The slice of the entity at w, or of only its body, which is what an upright item must keep in its section.
    private SliceGeometry? Slice(Dynamic item, double w, bool bodyOnly = false)
    {
        if (item.Packed) return null;
        var definition = item.Definition;
        var pieces = new List<(PartId Part, SliceBuffer Buffer)>();
        foreach (var part in DynamicShapes.Parts(definition))
        {
            var buffer = new SliceBuffer();
            BoxSlice.Append(Box4.From(definition.Id, item.Pose, part), part, w, buffer.Positions, buffer.Normals, buffer.Uvs, buffer.Parts);
            pieces.Add((part.Id, buffer));
        }
        if (!bodyOnly)
            foreach (var round in DynamicShapes.Rounds(definition, item.Pose))
            {
                var buffer = new SliceBuffer();
                RoundSlice.Append(round, w, buffer);
                pieces.Add((round.Part, buffer));
            }
        var slice = new SliceBuffer();
        foreach (var (_, buffer) in pieces.OrderBy(piece => piece.Part.Value, StringComparer.Ordinal)) slice.AddRange(buffer);
        if (slice.Parts.Count == 0) return null;
        return new(definition.Id, slice.Positions.AsReadOnly(), slice.Normals.AsReadOnly(), slice.Uvs.AsReadOnly(), slice.Parts.AsReadOnly(),
            ExtendsInW(DynamicShapes.Solids(definition, item.Pose)));
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
            TurnableDefinition or ItemDefinition => AddDynamic(definition),
            _ => Reject(FailureCode.WrongEntityKind, "Only creatures, turnables and items can currently be added.")
        };
    }

    private bool IsUsed(EntityId id) =>
        entities.Any(entity => entity.Id == id) || dynamicEntities.ContainsKey(id) || actors.ContainsKey(id) || retired.Contains(id);

    private AddEntityResult AddActor(Creature3Definition actor)
    {
        var definitions = staticDefinitions.Values
            .Concat(dynamicEntities.Values.Select(item => item.Definition))
            .Concat(actors.Values.Select(other => (EntityDefinition)other.Definition))
            .Append(actor);
        if (ValidationFailure(definitions, actor.Id) is { } failure) return new AddEntityResult.Rejected(failure);
        actors.Add(actor.Id, new(actor, actor.Pose, 0, actor.Section));
        revision++;
        Enqueue(new EntityAdded(actor.Id), time);
        return new AddEntityResult.Added(State(actors[actor.Id]));
    }

    private AddEntityResult AddDynamic(EntityDefinition definition)
    {
        if (ValidationFailure(staticDefinitions.Values.Append(definition), definition.Id) is { } failure)
            return new AddEntityResult.Rejected(failure);
        if (IntersectsReservation(DynamicShapes.Solids(definition, definition.Pose), definition.Id))
            return new AddEntityResult.Rejected(Fail(FailureCode.TranslationBlocked, "Entity intersects a reserved rotation sweep.", definition.Id));
        AddInitial(definition);
        revision++;
        Enqueue(new EntityAdded(definition.Id), time);
        return new AddEntityResult.Added(State(dynamicEntities[definition.Id]));
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
        if (rotations.ContainsKey(entity) || IsHeld(entity) || HoldsItem(entity))
            return new RemoveEntityResult.Rejected(Fail(FailureCode.EntityInUse, "Entity is rotating, held, or holding an item.", entity));
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

    private bool IsHeld(EntityId entity) => dynamicEntities.TryGetValue(entity, out var item) && item.Holder is not null;

    private bool HoldsItem(EntityId actor) => dynamicEntities.Values.Any(item => item.Holder == actor);

    private bool HoldsInHand(EntityId actor) => dynamicEntities.Values.Any(item => item.Holder == actor && !item.Packed);

    public EntityReadResult ReadEntity(EntityId entity)
    {
        if (actors.TryGetValue(entity, out var actor)) return new EntityReadResult.Found(State(actor));
        if (dynamicEntities.TryGetValue(entity, out var item)) return new EntityReadResult.Found(State(item));
        if (!staticDefinitions.TryGetValue(entity, out var definition)) return new EntityReadResult.Missing(entity);
        var fixedObject = definition as FixedDefinition;
        return new EntityReadResult.Found(new(
            entity, definition, definition.Pose, fixedObject?.Section, ActorYaw: null, UprightOrigin: null, Holder: null, InBackpack: false,
            CarriedItems: [], DevicePosition: null, Rotating: false,
            Handedness: fixedObject is null ? Handedness.NotComparable : PoseHandedness(definition.Pose)));
    }

    public UpdateResult SynchronizeActor(EntityId actor, Vec3 position, double yaw)
    {
        if (!actors.TryGetValue(actor, out var state))
            return new UpdateResult.Rejected(Fail(KindOrUnknown(actor), "Unknown actor.", actor));
        if (!new[] { position.X, position.Y, position.Z, yaw }.All(double.IsFinite))
            return new UpdateResult.Rejected(Fail(FailureCode.InvalidInput, "Position and yaw must be finite.", actor));
        var moved = new Vec4(position.X, position.Y, position.Z, state.Pose.Position.W);
        var shift = Sub(moved, state.Pose.Position);
        actors[actor] = state with { Pose = state.Pose with { Position = moved }, Yaw = yaw };
        // The backpack goes where its owner goes; an item in the hand is moved by the host with MoveEntity.
        foreach (var (id, packed) in dynamicEntities.Where(entry => entry.Value.Holder == actor && entry.Value.Packed).ToArray())
            dynamicEntities[id] = packed with { Pose = packed.Pose with { Position = Add(packed.Pose.Position, shift) } };
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
