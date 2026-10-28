namespace FourD;

// Decision note: 2026-10-26-device-translation
public sealed partial class World
{
    // A device, its lofted pieces, and how far along its rotation or track it stands. A record is replaced and never
    // edited in place, because pairLandings keeps results while it is the same object (see PairLandings).
    private sealed record Device(DeviceDefinition Definition, IReadOnlyList<LoftPiece> Pieces, double Position);

    private readonly Dictionary<EntityId, Device> devices = [];

    private static Device NewDevice(DeviceDefinition definition) =>
        new(CopyOf(definition), LoftPiece.Of(definition.Shape), DeviceKinematics.Initial(definition.Motion));

    private static DeviceDefinition CopyOf(DeviceDefinition device)
    {
        IReadOnlyList<T> Copy<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
        ConvexPart Part(ConvexPart part) => part with
        {
            Vertices = Copy(part.Vertices),
            Faces = Copy(part.Faces.Select(face => face with { Vertices = Copy(face.Vertices) }))
        };
        LoftKey Key(LoftKey key) => key with { Parts = Copy(key.Parts.Select(Part)) };
        return device with { Shape = new(Copy(device.Shape.Segments.Select(segment => new LoftSegment(Copy(segment.Keys.Select(Key)))))) };
    }

    private EntityState State(Device device) => new(
        device.Definition.Id, device.Definition, DeviceKinematics.PoseAt(device.Definition, device.Position), Section: null,
        ActorYaw: null, UprightOrigin: null, Holder: null, InBackpack: false, CarriedItems: [], DevicePosition: device.Position, Rotating: false,
        Handedness: Handedness.NotComparable);

    // The pieces of the device when it stands at `position`.
    private static IEnumerable<Loft4> Solids(Device device, double position)
    {
        var pose = DeviceKinematics.PoseAt(device.Definition, position);
        return device.Pieces.Select(piece => Loft4.At(device.Definition.Id, piece, pose));
    }

    private IEnumerable<SliceGeometry> DeviceSlices(double w) => devices.Values.Select(device => Slice(device, w)).OfType<SliceGeometry>();

    private static SliceGeometry? Slice(Device device, double w)
    {
        var pieces = Solids(device, device.Position).ToArray();
        var slice = new SliceBuffer();
        foreach (var part in pieces.GroupBy(piece => piece.Part).OrderBy(group => group.Key.Value, StringComparer.Ordinal))
            foreach (var piece in part)
                LoftSlice.Append(piece, w, slice);
        if (slice.Parts.Count == 0) return null;
        return new(device.Definition.Id, slice.Positions.AsReadOnly(), slice.Normals.AsReadOnly(), slice.Uvs.AsReadOnly(), slice.Parts.AsReadOnly(),
            ExtendsInW(pieces));
    }

    private AddEntityResult AddDeviceEntity(DeviceDefinition definition)
    {
        if (ValidationFailure(staticDefinitions.Values.Append(definition), definition.Id) is { } failure)
            return new AddEntityResult.Rejected(failure);
        var device = NewDevice(definition);
        if (IntersectsReservation(Solids(device, device.Position), definition.Id))
            return new AddEntityResult.Rejected(Fail(FailureCode.TranslationBlocked, "Entity intersects a reserved rotation sweep.", definition.Id));
        devices.Add(definition.Id, device);
        revision++;
        Enqueue(new EntityAdded(definition.Id), time);
        return new AddEntityResult.Added(State(device));
    }

    /// <summary>
    /// Moves a device to a position along its motion, relative to the shape and pose it was defined with: an angle in
    /// radians for a rotation, a distance in metres for a track. A rotation checks its whole sweep in every section
    /// and is refused entirely when anything is in the way. A translation stops at the first contact and reports it,
    /// by the rules of carrying an item, and is refused only when it cannot move at all. The motion is instant.
    /// </summary>
    public DriveDeviceResult DriveDevice(EntityId device, double target)
    {
        DriveDeviceResult Reject(FailureCode code, string detail, IReadOnlyList<Contact>? contacts = null) =>
            new DriveDeviceResult.Rejected(Fail(code, detail, device, contacts));
        if (!devices.TryGetValue(device, out var state)) return Reject(KindOrUnknown(device), "Entity is unknown or not a device.");
        if (!double.IsFinite(target)) return Reject(FailureCode.InvalidInput, "Target must be finite.");
        var limits = DeviceKinematics.Limits(state.Definition.Motion);
        if (target < limits.Min || target > limits.Max) return Reject(FailureCode.OutOfRange, "Target is outside the limits of the device.");

        if (state.Definition.Motion is DeviceRotation rotation)
        {
            var obstructed = TurnContacts(state, rotation, target);
            return obstructed.Count > 0
                ? Reject(FailureCode.RotationBlocked, "Rotation is obstructed.", obstructed)
                : Commit(state, target, target, []);
        }

        var (axis, sign) = DeviceKinematics.Track(state.Definition);
        var requested = sign * (target - state.Position);
        var (applied, contacts) = Sweep(device, Solids(state, state.Position).Cast<Solid4>().ToArray(), axis, requested);
        if (applied == 0 && requested != 0) return Reject(FailureCode.TranslationBlocked, "Translation is blocked.", contacts);
        return Commit(state, target, applied == requested ? target : state.Position + sign * applied, contacts);
    }

    private DriveDeviceResult.Driven Commit(Device device, double target, double position, IReadOnlyList<Contact> contacts)
    {
        var id = device.Definition.Id;
        devices[id] = device with { Position = position };
        revision++;
        Enqueue(new DeviceMotionStarted(id, device.Position, target), time);
        if (contacts.Count > 0) Enqueue(new MovementBlocked(id, contacts), time);
        Enqueue(new DeviceMotionEnded(id, position), time);
        return new(position, contacts);
    }

    // Where the device first touches an obstacle on its way from its position to the target, turning through poses
    // that no point of it moves more than the sampling distance between. A pair of solids is blocked where it embeds
    // deeper than the contact tolerance, or than it embedded at the start when that was deeper, as in a slide: an
    // obstacle the device already sits in does not stop it from turning out of it. Empty when the way is clear.
    private IReadOnlyList<Contact> TurnContacts(Device device, DeviceRotation rotation, double target)
    {
        var id = device.Definition.Id;
        var turn = target - device.Position;
        var steps = Math.Max(1, (int)Math.Ceiling(DeviceKinematics.Reach(device.Pieces, rotation) * Math.Abs(turn) / SamplingDistance));
        var obstacles = Solids().Where(solid => solid.Entity != id).Concat(ReservedSolids(id)).ToArray();
        var started = new Dictionary<(int, int), double>();
        for (var step = 0; step <= steps; step++)
        {
            var pieces = Solids(device, device.Position + turn * step / steps).ToArray();
            var contacts = new List<Contact>();
            for (var i = 0; i < pieces.Length; i++)
                for (var j = 0; j < obstacles.Length; j++)
                {
                    if (!pieces[i].BoundsMayOverlap(obstacles[j])) continue;
                    var depth = Collision.Depth(pieces[i], obstacles[j]);
                    if (step == 0) started[(i, j)] = depth;
                    var blockLevel = Math.Max(started.GetValueOrDefault((i, j)), ContactTolerance) + RoundingMargin;
                    if (depth > blockLevel && Collision.ContactOf(pieces[i], obstacles[j]) is { } contact) contacts.Add(WithSection(contact));
                }
            if (contacts.Count > 0) return DistinctContacts(contacts);
        }
        return [];
    }
}
