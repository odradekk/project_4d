using static FourD.Tests.Fixtures;

namespace FourD.Tests;

// Scenes with a ball and a nut for the tests of four-dimensional items. Lengths are in metres, Y is up.
internal static class ItemScenes
{
    public static readonly SectionDefinition SectionI = new(new("I"), 0, TestSource);
    public static readonly SectionDefinition SectionII = new(new("II"), .42, TestSource);

    // The ball of the size norms: radius .25 m with its center .21 m deep, midway between the sections.
    public const double BallRadius = .25;
    public const double BallW = .21;

    public static readonly EntityId Floor = new("floor");
    public static readonly EntityId BallId = new("ball");
    public static readonly EntityId NutId = new("nut");
    public static readonly PartId RingPart = new("ring");

    public static TurnableProperties BallProperties() => new(
        Holdable: true, Packable: false, HorizontalRadius: BallRadius, LongAxis: Axis3.X, Hinge: default, Reach: BallRadius);

    public static ItemDefinition Ball(double x = 0, double y = BallRadius, double z = 0, double w = BallW, double radius = BallRadius) =>
        new(BallId, At(x, y, z, w), TestSource, new("home"), new Sphere4(new("ball"), radius), BallProperties());

    // The ring lies in the YZ plane of the nut, so standing the nut up about the X axis keeps it in a single section.
    public static readonly Rotation4 RingInYz = new(new(0, 1, 0, 0), new(0, 0, 1, 0), new(1, 0, 0, 0), new(0, 0, 0, 1));

    // A 6 cm nut that stands up about a hinge 1 cm from its middle, so the hinge is 2 cm from its lower end. Its
    // ring has a 20 cm radius and a 5 cm tube, and rests .41 m along the long axis, so that standing the nut up
    // toward section II (.42 m deep) turns the ring's center to W = .42. Lying on the floor, the nut's origin is
    // .03 m up, and the ring's center is .87 m above it, at chest height (.9 m).
    public static ItemDefinition Nut(Pose4? pose = null, double ringW = .2, double ringY = .87) => new(
        NutId, pose ?? At(0, .03, 0), TestSource, new("home"),
        new HiddenRingItem(Box(new(.03, .03, .03)), new Ring4(RingPart, new(.41, ringY, 0, ringW), RingInYz, .2, .05)),
        new TurnableProperties(Holdable: true, Packable: true, HorizontalRadius: .03, LongAxis: Axis3.X, Hinge: new(-.01, 0, 0), Reach: .04));

    public static readonly EntityId RodId = new("rod");

    // The .48 m rod of the size norms in section I: its hinge is 2 cm inside its left end, leaving .46 m of reach.
    // Stood up toward section II it is a thin plate at x = -.22, 1.2 cm thick and 6 cm wide and high, that spans W
    // from -.02 to .46.
    public static TurnableDefinition Rod(bool holdable = true) => new(
        RodId, Origin, TestSource, new("I"), new("home"), Box(new(.24, .03, .03)),
        new TurnableProperties(Holdable: holdable, Packable: false, HorizontalRadius: .24, LongAxis: Axis3.X, Hinge: new(-.22, 0, 0), Reach: .46));

    public static Creature3Definition Actor(string id = "actor", string section = "I", double x = -1) =>
        new(new(id), At(x, 0, 0, section == "I" ? 0 : .42), TestSource, new(section), PassageRadius: .2, PassageHeight: 1.6, CanUseSeamsWithoutPrism: false);

    // A 6 m x 6 m floor with its top at y = 0, present for the whole W range of the building unless it has gaps.
    public static BuildingElementDefinition FloorOf(Interval wRange, params Interval[] gaps) => new(
        Floor, Origin, TestSource, new("home"), wRange, gaps, Shape(Part("slab", new(3, .05, 3), center: new(0, -.05, 0))));

    // A block with the given center and half size, present only for the given W range.
    public static BuildingElementDefinition Block(string id, Vec3 center, Vec3 half, Interval wRange) => new(
        new(id), Origin, TestSource, new("home"), wRange, [], Shape(Part("part", half, center: center)));

    public static WorldDescription Description(EntityDefinition[] entities, double buildingMin = -.5, double buildingMax = 1) => new(
        [SectionI, SectionII], [new(new("home"), new(buildingMin, buildingMax), TestSource)], entities, [], [], []);

    public static World Scene(params EntityDefinition[] entities) => Load(Description(entities));

    public static void AssertNear(Vec4 expected, Vec4 actual, int digits = 9)
    {
        Assert.Equal(expected.X, actual.X, digits);
        Assert.Equal(expected.Y, actual.Y, digits);
        Assert.Equal(expected.Z, actual.Z, digits);
        Assert.Equal(expected.W, actual.W, digits);
    }

    // The shortest and the longest distance from a point to the vertices of a slice.
    public static (double Min, double Max) DistancesFrom(SliceGeometry slice, Vec3 center)
    {
        var distances = Enumerable.Range(0, slice.Parts.Count)
            .Select(i => slice.Positions.Vertex(i).Minus(center).Length())
            .ToArray();
        return (distances.Min(), distances.Max());
    }

    public static IEnumerable<int> TrianglesOf(SliceGeometry slice, PartId part) =>
        Enumerable.Range(0, slice.Parts.Count / 3).Select(t => 3 * t).Where(first => slice.Parts[first] == part);

    public static SliceGeometry? SliceOf(this World world, EntityId entity, SectionId section) =>
        world.SlicesIn(section).SingleOrDefault(slice => slice.Entity == entity);

    public static void Stand(this World world, EntityId actor, EntityId entity, SectionId target)
    {
        var plan = world.AllowedPlan(new StandUp(actor, entity, target));
        var started = Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan));
        world.StepEvents(started.Duration);
    }
}
