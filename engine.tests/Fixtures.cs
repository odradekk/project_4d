namespace FourD.Tests;

// Builders and result unwrapping shared by the engine tests. Lengths are in metres, Y is up.
internal static class Fixtures
{
    // Creatures, turnable objects, and fixed objects all extend this far to each side in W
    // (docs/frame/principles.md and content.md).
    public const double Epsilon = .006;

    public static readonly Source TestSource = new("/test", "definition");
    public static readonly Rotation3 Identity3 = new(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
    public static readonly Rotation4 Identity4 = new(new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1));
    public static readonly Pose4 Origin = At(0, 0, 0);

    // Turns +X toward +W. A quarter turn lays an object that lies along X along W instead.
    public static readonly Rotation4 XwQuarterTurn = XwRotation(cos: 0, sin: 1);
    public static readonly Rotation4 XwHalfTurn = new(new(-1, 0, 0, 0), Identity4.Y, Identity4.Z, new(0, 0, 0, -1));

    public static Pose4 At(double x, double y, double z, double w = 0, Rotation4? rotation = null) =>
        new(new(x, y, z, w), rotation ?? Identity4);

    public static Rotation4 XwRotation(double cos, double sin) =>
        new(new(cos, 0, 0, sin), Identity4.Y, Identity4.Z, new(-sin, 0, 0, cos));

    // Unit scale and no offset on all six faces.
    public static FaceUv[] FaceUvs() => Enum.GetValues<Axis3>()
        .SelectMany(axis => new[] { false, true }, (axis, positive) => new FaceUv(axis, positive, new(1, 1), new(0, 0)))
        .ToArray();

    public static BoxPart Part(string id, Vec3 half, Vec3 center = default, Rotation3? rotation = null) =>
        new(new(id), center, half, rotation ?? Identity3, FaceUvs());

    public static BoxAssembly Shape(params BoxPart[] parts) => new(parts);

    public static BoxAssembly Box(Vec3 half) => Shape(Part("body", half));

    public static World Load(WorldDescription description) =>
        Assert.IsType<LoadResult.Loaded>(World.Load(description)).World;

    public static IReadOnlyList<LoadError> LoadErrors(WorldDescription description) =>
        Assert.IsType<LoadResult.Invalid>(World.Load(description)).Errors;

    public static EntityState State(this World world, EntityId id) =>
        Assert.IsType<EntityReadResult.Found>(world.ReadEntity(id)).Entity;

    public static IReadOnlyList<EventOccurrence> StepEvents(this World world, double delta) =>
        Assert.IsType<StepResult.Advanced>(world.Step(delta)).Events;

    public static RotationPlan AllowedPlan(this World world, RotationRequest request) =>
        Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(request)).Plan;

    public static IReadOnlyList<OccupancyHit> OccupancyHits(this World world, OccupancyShape shape, params EntityId[] excluded) =>
        Assert.IsType<OccupancyResult.Occupancy>(world.QueryOccupancy(shape, excluded)).Hits;

    public static bool HasSliceVolume(this World world, EntityId entity, SectionId section) =>
        Assert.IsType<SlicePresenceResult.Present>(world.HasSlice(entity, section)).HasVolume;

    public static IReadOnlyList<SliceGeometry> SlicesAt(this World world, double w) =>
        Assert.IsType<SectionGeometryResult.Geometry>(world.GetGeometryAtW(w)).Entities;

    public static IReadOnlyList<SliceGeometry> SlicesIn(this World world, SectionId section) =>
        Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(section)).Entities;

    // Slice vertices and normals are flat lists of x, y, z triples.
    public static Vec3 Vertex(this IReadOnlyList<double> triples, int index) =>
        new(triples[index * 3], triples[index * 3 + 1], triples[index * 3 + 2]);

    // All values along one axis of the slice's vertices.
    public static IEnumerable<double> Coordinates(this SliceGeometry slice, Axis3 axis) =>
        slice.Positions.Where((_, i) => i % 3 == (int)axis);

    // The triangle that starts at vertex `first` is counter-clockwise seen from the side its normal points to.
    public static bool IsWoundOutward(this SliceGeometry slice, int first)
    {
        var a = slice.Positions.Vertex(first);
        var b = slice.Positions.Vertex(first + 1);
        var c = slice.Positions.Vertex(first + 2);
        return b.Minus(a).Cross(c.Minus(a)).Dot(slice.Normals.Vertex(first)) > 0;
    }

    public static Vec3 Plus(this Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3 Minus(this Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3 Times(this Vec3 a, double factor) => new(a.X * factor, a.Y * factor, a.Z * factor);

    public static double Dot(this Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vec3 Cross(this Vec3 a, Vec3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    public static double Length(this Vec3 a) => Math.Sqrt(a.Dot(a));
}
