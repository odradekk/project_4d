using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Scenes with seams between sections I (W = 0) and II (W = .42), and shorthand for crossing them. Lengths are in metres.
internal static class SeamScenes
{
    public static readonly SectionId I = new("I");
    public static readonly SectionId II = new("II");
    public static readonly EntityId ActorId = new("actor");

    // A seam in the YZ plane through the origin by default: its normal is +X, so a half turn about it reverses X and W.
    public static SeamDefinition Seam(
        bool mirrored = false, bool open = true, bool severed = false, string id = "seam",
        Vec3 position = default, Vec3? normal = null, string[]? sections = null) => new(
        new(id), position, normal ?? new(1, 0, 0), (sections ?? ["I", "II"]).Select(name => new SectionId(name)).ToArray(),
        mirrored, open, severed, TestSource);

    public static World SeamScene(SeamDefinition seam, params EntityDefinition[] entities) =>
        Load(Description(entities) with { Seams = [seam] });

    public static CrossSeamRequest Request(
        string actor = "actor", string seam = "seam", string target = "II", bool hasPrism = true, int slots = 4) =>
        new(new(actor), new(seam), new(target), hasPrism, slots);

    public static CrossSeamResult.Crossed Cross(this World world, CrossSeamRequest request) =>
        Assert.IsType<CrossSeamResult.Crossed>(world.CrossSeam(request));

    // Crosses, expecting a refusal with the given code, and checks that the watched entities and the events are as before.
    public static Failure Refuse(this World world, CrossSeamRequest request, FailureCode code, params EntityId[] watched)
    {
        var before = world.Snapshot(watched);
        var failure = Assert.IsType<CrossSeamResult.Rejected>(world.CrossSeam(request)).Failure;
        Assert.Equal(code, failure.Code);
        Assert.Equal(before, world.Snapshot(watched));
        Assert.Empty(world.StepEvents(.001));
        return failure;
    }

    private static string Snapshot(this World world, EntityId[] ids) => string.Join(" | ", ids.Select(id =>
    {
        var state = world.State(id);
        return $"{state.Pose} {state.Section} {state.Holder} {state.UprightOrigin} {string.Join(',', state.CarriedItems)}";
    }));

    // A wall of 2 m high and deep that fills everything from x = edge on (or up to it), present in one section.
    public static FixedDefinition WallFrom(double edge, string section) =>
        new(new("wall"), At(edge + 1, 1, 0, section == "I" ? 0 : .42), TestSource, new(section), Box(new(1, 1, 1)));

    public static FixedDefinition WallUpTo(double edge, string section) =>
        WallFrom(edge - 2, section);

    // A rod of the given reach, centered at the origin of section I, with its hinge 2 cm inside its left end.
    public static TurnableDefinition Stick(double reach, bool packable)
    {
        var half = (reach + .02) / 2;
        return Rod() with
        {
            Shape = Box(new(half, .03, .03)),
            Interaction = new TurnableProperties(Holdable: true, Packable: packable, HorizontalRadius: half, LongAxis: Axis3.X, Hinge: new(.02 - half, 0, 0), Reach: reach)
        };
    }

    // A short item that cannot go in the backpack.
    public static TurnableDefinition Chair() => new(
        new("chair"), At(.1, .03, 0), TestSource, I, new("home"), Box(new(.05, .03, .03)),
        new TurnableProperties(Holdable: true, Packable: false, HorizontalRadius: .05, LongAxis: Axis3.X, Hinge: new(-.03, 0, 0), Reach: .08));

    public static void AssertNear(Rotation4 expected, Rotation4 actual)
    {
        ItemScenes.AssertNear(expected.X, actual.X);
        ItemScenes.AssertNear(expected.Y, actual.Y);
        ItemScenes.AssertNear(expected.Z, actual.Z);
        ItemScenes.AssertNear(expected.W, actual.W);
    }
}
