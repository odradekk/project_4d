using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Devices for the tests of lofted four-dimensional bodies. Lengths are in metres, Y is up. Sections I and II are at
// W = 0 and .42, and the building "home" spans W from -.5 to 1.
internal static class DeviceScenes
{
    public static readonly EntityId CabinetId = new("cabinet");
    public static readonly EntityId HangerId = new("hanger");
    public static readonly EntityId SashId = new("sash");
    public static readonly EntityId LiftId = new("lift");
    public static readonly EntityId BoardId = new("board");
    public static readonly EntityId CrateId = new("crate");

    // A box part of a loft with the given ranges of x, y and z. Vertex i has bit 0 set on the high x side, bit 1 on
    // the high y side and bit 2 on the high z side.
    public static ConvexPart LoftBox(string id, (double Min, double Max) x, (double Min, double Max) y, (double Min, double Max) z)
    {
        var vertices = Enumerable.Range(0, 8)
            .Select(i => new Vec3((i & 1) == 0 ? x.Min : x.Max, (i & 2) == 0 ? y.Min : y.Max, (i & 4) == 0 ? z.Min : z.Max))
            .ToArray();
        var faces = Enum.GetValues<Axis3>()
            .SelectMany(axis => new[] { false, true }, (axis, positive) => BoxFace(vertices, axis, positive))
            .ToArray();
        return new(new(id), vertices, faces);
    }

    private static ConvexFace BoxFace(Vec3[] vertices, Axis3 axis, bool positive)
    {
        var bit = 1 << (int)axis;
        var first = 1 << (((int)axis + 1) % 3);
        var second = 1 << (((int)axis + 2) % 3);
        var side = positive ? bit : 0;
        int[] loop = [side, side | first, side | first | second, side | second];
        var direction = axis switch { Axis3.X => new Vec3(1, 0, 0), Axis3.Y => new Vec3(0, 1, 0), _ => new Vec3(0, 0, 1) };
        var normal = vertices[loop[1]].Minus(vertices[loop[0]]).Cross(vertices[loop[2]].Minus(vertices[loop[0]]));
        if (normal.Dot(direction) * (positive ? 1 : -1) < 0) loop = [.. loop.Reverse()];
        return new(loop, new FaceUv(axis, positive, new(1, 1), new(0, 0)));
    }

    public static LoftKey Key(double w, params ConvexPart[] parts) => new(w, parts);

    public static LoftShape Loft(params LoftKey[] keys) => new([new LoftSegment(keys)]);

    public static LoftShape Loft(params LoftSegment[] segments) => new(segments);

    // A leaf that hangs from a hinge along Z at y = 2 and swings toward +x, and that grows with W: in section I it is
    // 40 cm long and 60 cm wide, in section II 1.2 m long and 20 cm wide, and it changes linearly between. Both are
    // 2 cm thick. The keys at W = 0 and .42 are the section shapes themselves, with keys at W = -.2 and .62 holding
    // them over a margin on each side.
    public static ConvexPart Leaf(double bottom, double halfWidth) => LoftBox("leaf", (-.01, .01), (bottom, 0), (-halfWidth, halfWidth));

    public static DeviceDefinition Hanger(double initial = 0, double max = Math.PI / 2) => new(
        HangerId, At(0, 2, 0), TestSource,
        Loft(Key(-.2, Leaf(-.4, .3)), Key(0, Leaf(-.4, .3)), Key(.42, Leaf(-1.2, .1)), Key(.62, Leaf(-1.2, .1))),
        new DeviceRotation(RotationPlane.XY, default, new(0, max), initial));

    // The cabinet of scene 8 in a room. The hinge is a line along Z at y = 2, the floor's top is at y = 0 and the
    // ceiling's underside at y = 3. In section I the device is a flap standing up from the hinge, 40 cm high, 60 cm wide
    // and 2 cm thick. In section II it is a plank 2.2 m long, 20 cm wide and 2 cm thick that leans out from the hinge
    // by the angle whose tangent is 2, so that its end is at x = 1.97, y = 2.98 under the ceiling. The two are two
    // segments of one loft that change shape at W = .21, between the sections, and they share one hinge and one turn.
    // A turn of -π/2 opens the flap to lie along +x and lays the plank down as a slope to the floor, its end at
    // x = .98, y = .03.
    public static readonly double Lean = Math.Atan2(2, 1);
    public const double PlankLength = 2.2;

    private static ConvexPart Tilted(ConvexPart part, double angle) => part with
    {
        Vertices = [.. part.Vertices.Select(v => new Vec3(v.X * Math.Cos(angle) - v.Y * Math.Sin(angle), v.X * Math.Sin(angle) + v.Y * Math.Cos(angle), v.Z))]
    };

    public static ConvexPart Flap() => LoftBox("flap", (-.01, .01), (0, .4), (-.3, .3));

    public static ConvexPart Plank() => Tilted(LoftBox("plank", (-.01, .01), (0, PlankLength), (-.1, .1)), -Lean);

    public static DeviceDefinition Cabinet(double initial = 0) => new(
        CabinetId, At(0, 2, 0), TestSource,
        Loft(
            new LoftSegment([Key(-.2, Flap()), Key(0, Flap()), Key(.21, Flap())]),
            new LoftSegment([Key(.21, Plank()), Key(.42, Plank()), Key(.62, Plank())])),
        new DeviceRotation(RotationPlane.XY, default, new(-Math.PI / 2, 0), initial));

    public static EntityDefinition[] CabinetRoom() =>
    [
        FloorOf(new(-.5, 1)),
        Block("ceiling", new(0, 3.1, 0), new(3, .1, 3), new Interval(-.5, 1))
    ];

    // A sliding sash that closes toward +x, 40 cm wide and 2 cm thick. Its lower edge dips with W, from y = .13 at
    // W = -.2 to -.19 at .6, so that at section I (y = .05) it clears a pencil lying on the sill, and deeper in W it
    // reaches the height the pencil stands at.
    public static ConvexPart Pane(double bottom) => LoftBox("pane", (-1, -.6), (bottom, .9), (-.02, .02));

    public static DeviceDefinition Sash(double initial = 0) => new(
        SashId, Origin, TestSource, Loft(Key(-.2, Pane(.13)), Key(.6, Pane(-.19))),
        new DeviceTranslation(Axis3.X, new(0, 1), initial));

    // A 1 m x 1 m platform, 10 cm thick, whose top is at y = 1 and that moves up and down by half a metre.
    public static DeviceDefinition Lift(double initial = 0) => new(
        LiftId, Origin, TestSource,
        Loft(Key(-.1, LoftBox("deck", (-.5, .5), (.9, 1), (-.5, .5))), Key(.1, LoftBox("deck", (-.5, .5), (.9, 1), (-.5, .5)))),
        new DeviceTranslation(Axis3.Y, new(-.5, .5), initial));

    // A board 30 cm long along its local x, hinged in the XW plane at its end.
    public static DeviceDefinition Board() => new(
        BoardId, At(0, 1, 0), TestSource,
        Loft(Key(-.1, LoftBox("slab", (0, .3), (0, .2), (0, .2))), Key(.1, LoftBox("slab", (0, .3), (0, .2), (0, .2)))),
        new DeviceRotation(RotationPlane.XW, default, new(0, Math.PI / 2), 0));

    public static TurnableDefinition Crate(double y) => new(
        CrateId, At(0, y, 0), TestSource, new("I"), new("home"), Box(new(.1, .1, .1)),
        new TurnableProperties(Holdable: true, Packable: false, HorizontalRadius: .1, LongAxis: Axis3.X, Hinge: new(-.08, 0, 0), Reach: .18));

    public static DriveDeviceResult.Driven Driven(this World world, EntityId device, double target) =>
        Assert.IsType<DriveDeviceResult.Driven>(world.DriveDevice(device, target));

    public static Failure Refused(this World world, EntityId device, double target) =>
        Assert.IsType<DriveDeviceResult.Rejected>(world.DriveDevice(device, target)).Failure;

    // The range of one coordinate over the vertices of a slice.
    public static (double Min, double Max) Range(this SliceGeometry slice, Axis3 axis)
    {
        var values = slice.Coordinates(axis).ToArray();
        return (values.Min(), values.Max());
    }

    public static void AssertRange((double Min, double Max) actual, double min, double max, int digits = 9)
    {
        Assert.Equal(min, actual.Min, digits);
        Assert.Equal(max, actual.Max, digits);
    }

    // The total area of the triangles of a slice.
    public static double Area(this SliceGeometry slice) => Enumerable.Range(0, slice.Parts.Count / 3)
        .Sum(t => slice.Positions.Vertex(3 * t + 1).Minus(slice.Positions.Vertex(3 * t))
            .Cross(slice.Positions.Vertex(3 * t + 2).Minus(slice.Positions.Vertex(3 * t))).Length() / 2);
}
