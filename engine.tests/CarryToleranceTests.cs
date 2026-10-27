using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Surfaces that touch within the 5 mm contact tolerance do not block a carried object (docs/frame/rules.md), whether
// the object starts embedded in them or meets them on the way, and an object may not sink deeper than that.
public class CarryToleranceTests
{
    private static readonly SectionId II = new("II");
    private static readonly Creature3Definition Player = Actor();

    // A 6 m deep slab that is 10 cm thick, from x = from to x = to, with its top face at the given height.
    private static BuildingElementDefinition FloorPiece(string id, double from, double to, double top = 0) => new(
        new(id), Origin, TestSource, new("home"), new(-.5, 1), [],
        Shape(Part("slab", new((to - from) / 2, .05, 3), center: new((from + to) / 2, top - .05, 0))));

    private static MoveEntityResult.Moved Move(World world, EntityId entity, Vec3 delta) =>
        Assert.IsType<MoveEntityResult.Moved>(world.MoveEntity(new(entity, delta)));

    private static World HeldBall(double y, params EntityDefinition[] floor)
    {
        var world = Scene([.. floor, Ball(y: y), Player]);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(Player.Id, BallId));
        return world;
    }

    [Fact]
    public void RodEmbeddedInTheFloorCrossesTheSeamBetweenTwoFlushPieces()
    {
        // The rod stands up with its lower face 2 mm below the floor top, and the floor is two pieces that meet at x = .5.
        var world = Scene(FloorPiece("left", -3, .5), FloorPiece("right", .5, 3), Rod() with { Pose = At(0, .028, 0) }, Player);
        world.Stand(Player.Id, RodId, II);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(Player.Id, RodId));

        var moved = Move(world, RodId, new(1, 0, 0));

        Assert.Equal(1, moved.Applied.X);
        Assert.Empty(moved.Contacts);
    }

    [Fact]
    public void BallEmbeddedInTheFloorCrossesTheSeamBetweenTwoFlushPieces()
    {
        var world = HeldBall(BallRadius - .003, FloorPiece("left", -3, .5), FloorPiece("right", .5, 3));

        var moved = Move(world, BallId, new(2, 0, 0));

        Assert.Equal(new Vec3(2, 0, 0), moved.Applied);
        Assert.Empty(moved.Contacts);
    }

    [Fact]
    public void BallClimbsATwoMillimeterStepBetweenTwoFloorPieces()
    {
        // The ball is 2 mm into the left piece and the right one is 2 mm higher, so it ends 4 mm into that.
        var world = HeldBall(BallRadius - .002, FloorPiece("left", -3, .5), FloorPiece("right", .5, 3, top: .002));

        var moved = Move(world, BallId, new(2, 0, 0));

        Assert.Equal(new Vec3(2, 0, 0), moved.Applied);
    }

    [Fact]
    public void BallEmbeddedInTheFloorStillStopsAtAWallItReachesFirst()
    {
        // The wall's face is at x = 1, so a ball of radius .25 touching it has its center at x = .75.
        var wall = Block("wall", new(1.5, 1, 0), new(.5, 1, 3), new(-.5, 1));
        var world = HeldBall(BallRadius - .002, FloorPiece("floor", -3, 3), wall);

        var moved = Move(world, BallId, new(2, 0, 0));

        Assert.Equal(.75, moved.Applied.X, 9);
        Assert.Equal(new EntityId("wall"), Assert.Single(moved.Contacts).Obstacle);
    }

    [Fact]
    public void BallRollsOverASmallBumpAndStopsAtTheWallBeyondIt()
    {
        // The bump is a strip 3 mm high at x = .3 to .4: the ball, sitting on the floor, sinks 3 mm into it, which is
        // allowed, and leaves it again before the wall's face at x = 1, where the ball's center is at x = .75.
        var bump = Block("bump", new(.35, -.0485, 0), new(.05, .0515, 3), new(-.5, 1));
        var wall = Block("wall", new(1.5, 1, 0), new(.5, 1, 3), new(-.5, 1));
        var world = HeldBall(BallRadius, FloorPiece("floor", -3, 3), bump, wall);

        var moved = Move(world, BallId, new(2, 0, 0));

        Assert.Equal(.75, moved.Applied.X, 9);
    }

    [Fact]
    public void BallEmbeddedInTheFloorStillSlidesAlongIt()
    {
        var world = HeldBall(BallRadius - .003, FloorOf(new(-.5, 1)));

        var moved = Move(world, BallId, new(1, 0, 0));

        Assert.Equal(new Vec3(1, 0, 0), moved.Applied);
        Assert.Empty(moved.Contacts);
    }

    [Fact]
    public void HeldNutMovesWhileItsRingIsEmbeddedInABarThroughItsHole()
    {
        // A bar 4 cm square runs along x through the ring's hole, with its -z face a ring-center-relative -.15169 (3 mm
        // inside the tube, so the corner of the bar is .153 from the circle's center). Moving the nut toward -z carries
        // the bar across the hole to the tube on the far side, which the bar's +z face reaches at the same depth after
        // 2 * .15169 - .04 = .26337 m; the nut is stopped there, not at the start.
        var a = Math.Sqrt(.153 * .153 - .02 * .02);
        var world = NutWithBar(zFace: -a);

        var moved = Move(world, NutId, new(0, 0, -.3));

        Assert.Equal(-(2 * a - .04), moved.Applied.Z, 9);
        Assert.Equal(RingPart, Assert.Single(moved.Contacts).MovingPart);
    }

    [Fact]
    public void HeldNutMovesWhileItsRingIsClearOfABarThroughItsHole()
    {
        // The bar is .9 mm from the tube: its corner is .1491 from the circle's center, which puts its -z face at -b, and
        // the +z face meets the far side of the tube (.15 from the center) after b + c - .04 metres.
        double b = Math.Sqrt(.1491 * .1491 - .02 * .02), c = Math.Sqrt(.15 * .15 - .02 * .02);
        var world = NutWithBar(zFace: -b);

        var moved = Move(world, NutId, new(0, 0, -.3));

        Assert.Equal(-(b + c - .04), moved.Applied.Z, 9);
    }

    // The nut in the hand, with a bar through the ring's hole whose -z face is at the given z.
    private static World NutWithBar(double zFace)
    {
        var bar = Block("bar", new(.41, .9, zFace + .02), new(.3, .02, .02), new(.1, .3));
        var world = Scene(Nut(), Player, bar);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(Player.Id, NutId));
        return world;
    }
}
