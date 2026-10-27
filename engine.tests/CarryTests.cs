using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// Holding items and moving them: the slice of a stood-up object or a ball in the hand moves in every section at once,
// and any section's obstacle stops it. Distances are worked out by hand from the faces of the scene's boxes.
public class CarryTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");
    private static readonly Creature3Definition Player = Actor();

    // A wall 4 cm thick, 1 m high and wide, in one section, centered at x.
    private static FixedDefinition Wall(double x, string section = "I") =>
        new(new("wall"), At(x, 0, 0, section == "I" ? 0 : .42), TestSource, new(section), Shape(Part("part", new(.02, .5, .5))));

    // The rod stood up toward section II, held by the player in section I.
    private static World HeldUprightRod(params EntityDefinition[] more)
    {
        var world = Scene([Rod(), Player, .. more]);
        world.Stand(Player.Id, RodId, II);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(Player.Id, RodId));
        return world;
    }

    private static MoveEntityResult.Moved Move(World world, EntityId entity, Vec3 delta) =>
        Assert.IsType<MoveEntityResult.Moved>(world.MoveEntity(new(entity, delta)));

    private static double LeftEdge(World world, EntityId entity, SectionId section) =>
        world.SliceOf(entity, section)!.Coordinates(Axis3.X).Min();

    [Fact]
    public void MovingAGrabbedSliceMovesTheObjectInEverySection()
    {
        var world = HeldUprightRod();
        Assert.Equal(-.226, LeftEdge(world, RodId, I), 9);
        Assert.Equal(-.226, LeftEdge(world, RodId, II), 9);

        var moved = Move(world, RodId, new(.1, 0, 0));

        Assert.Equal(new Vec3(.1, 0, 0), moved.Applied);
        Assert.Empty(moved.Contacts);
        Assert.Equal(-.126, LeftEdge(world, RodId, I), 9);
        Assert.Equal(-.126, LeftEdge(world, RodId, II), 9);
    }

    [Fact]
    public void AnObstacleInAnotherSectionStopsTheObjectAtItsSurface()
    {
        // The rod's right face is at x = -.214 and the wall in II has its left face at x = -.02: .194 apart.
        var world = HeldUprightRod(Wall(0, "II"));

        var moved = Move(world, RodId, new(.3, 0, 0));

        Assert.Equal(.194, moved.Applied.X, 9);
        var contact = Assert.Single(moved.Contacts);
        Assert.Equal(new EntityId("wall"), contact.Obstacle);
        Assert.Equal(RodId, contact.Moving);
        Assert.Equal(II, contact.Section);
        Assert.Equal(-.02, contact.Point.X, 9);
        Assert.True(contact.Normal.X < 0);
    }

    [Fact]
    public void ObjectSlidesAlongTheObstacleWhenOnlyOneDirectionIsBlocked()
    {
        var world = HeldUprightRod(Wall(0, "II"));

        var moved = Move(world, RodId, new(.3, 0, .2));

        Assert.Equal(.194, moved.Applied.X, 9);
        Assert.Equal(0, moved.Applied.Y);
        Assert.Equal(.2, moved.Applied.Z);
        Assert.Equal(new EntityId("wall"), Assert.Single(moved.Contacts).Obstacle);
        Assert.Equal(.2, world.State(RodId).Pose.Position.Z, 9);
    }

    [Fact]
    public void ObjectAlreadyTouchingAWallStillSlidesAlongIt()
    {
        // The wall's right face is at x = -.226, against the rod's left face.
        var world = HeldUprightRod(Wall(-.246, "II"));

        var moved = Move(world, RodId, new(-.1, 0, .2));

        Assert.Equal(0, moved.Applied.X, 9);
        Assert.Equal(.2, moved.Applied.Z, 9);
        Assert.Single(moved.Contacts);
    }

    [Fact]
    public void ObjectThatCannotMoveAtAllIsRefusedAndNothingChanges()
    {
        var world = HeldUprightRod(Wall(-.246, "II"));
        var before = world.State(RodId);

        var rejected = Assert.IsType<MoveEntityResult.Rejected>(world.MoveEntity(new(RodId, new(-.1, 0, 0))));

        Assert.Equal(FailureCode.TranslationBlocked, rejected.Failure.Code);
        Assert.Equal(new EntityId("wall"), Assert.Single(rejected.Failure.Contacts).Obstacle);
        Assert.Equal(before, world.State(RodId));
        Assert.DoesNotContain(world.StepEvents(.1), occurrence => occurrence.Event is MovementBlocked);
    }

    [Fact]
    public void SlidingIsReportedAsABlockedMovementOnTheNextStep()
    {
        var world = HeldUprightRod(Wall(0, "II"));
        var moved = Move(world, RodId, new(.3, 0, 0));

        var blocked = Assert.Single(world.StepEvents(.1), occurrence => occurrence.Event is MovementBlocked).Event;

        var movement = Assert.IsType<MovementBlocked>(blocked);
        Assert.Equal(RodId, movement.Entity);
        Assert.Equal(moved.Contacts, movement.Contacts);
    }

    [Fact]
    public void FlatObjectInTheHandIsCarriedWithoutAFourDimensionalCheck()
    {
        var world = Scene(Rod(), Player, Wall(.6));
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(Player.Id, RodId));

        var moved = Move(world, RodId, new(1, .2, 0));

        Assert.Equal(new Vec3(1, .2, 0), moved.Applied);
        Assert.Empty(moved.Contacts);
        Assert.Equal(new Vec4(1, .2, 0, 0), world.State(RodId).Pose.Position);
    }

    [Fact]
    public void HoldingAFlatObjectCarriesItAndAllowsOnlyOneItemPerHandAndOneHolderPerItem()
    {
        var other = Actor("other");
        var world = Scene(Rod(), Ball(), Player, other);

        var held = Assert.IsType<CarryResult.Changed>(world.HoldEntity(Player.Id, RodId));
        Assert.Equal(Player.Id, held.Item.Holder);
        Assert.Equal([RodId], world.State(Player.Id).CarriedItems);

        Assert.Equal(FailureCode.EntityInUse, Assert.IsType<CarryResult.Rejected>(world.HoldEntity(Player.Id, BallId)).Failure.Code);
        Assert.Equal(FailureCode.EntityInUse, Assert.IsType<CarryResult.Rejected>(world.HoldEntity(other.Id, RodId)).Failure.Code);

        var released = Assert.IsType<CarryResult.Changed>(world.ReleaseEntity(RodId));
        Assert.Null(released.Item.Holder);
        Assert.Empty(world.State(Player.Id).CarriedItems);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(other.Id, RodId));
    }

    [Fact]
    public void GrabbingNeedsASliceInTheActorsSectionAndAStoodUpObjectCanBeGrabbedWhereverItHasOne()
    {
        var other = Actor("other", "II");
        var world = Scene(Rod(), Player, other);

        Assert.Equal(FailureCode.NoSlice, Assert.IsType<CarryResult.Rejected>(world.HoldEntity(other.Id, RodId)).Failure.Code);
        world.Stand(Player.Id, RodId, II);

        var grabbed = Assert.IsType<CarryResult.Changed>(world.HoldEntity(other.Id, RodId));
        Assert.Equal(other.Id, grabbed.Item.Holder);
        Assert.Empty(world.State(other.Id).CarriedItems);
    }

    [Fact]
    public void HoldingAndReleasingRefuseWhatTheyCannotDo()
    {
        var world = Scene(Rod(), Rod(holdable: false) with { Id = new("brick"), Pose = At(0, 0, 2) }, Wall(5), Player);
        var rejected = (Func<EntityId, EntityId, FailureCode>)((actor, item) => Assert.IsType<CarryResult.Rejected>(world.HoldEntity(actor, item)).Failure.Code);

        Assert.Equal(FailureCode.UnknownId, rejected(Player.Id, new("missing")));
        Assert.Equal(FailureCode.UnknownId, rejected(new("missing"), RodId));
        Assert.Equal(FailureCode.WrongEntityKind, rejected(Player.Id, new("wall")));
        Assert.Equal(FailureCode.WrongEntityKind, rejected(RodId, RodId));
        Assert.Equal(FailureCode.NotHoldable, rejected(Player.Id, new("brick")));
        world.StartRotation(world.AllowedPlan(new StandUp(Player.Id, RodId, II)));
        Assert.Equal(FailureCode.Busy, rejected(Player.Id, RodId));
        Assert.Equal(FailureCode.InvalidInput, Assert.IsType<CarryResult.Rejected>(world.ReleaseEntity(new("brick"))).Failure.Code);
        Assert.Equal(FailureCode.UnknownId, Assert.IsType<CarryResult.Rejected>(world.ReleaseEntity(new("missing"))).Failure.Code);
    }

    [Fact]
    public void MovingNeedsAHeldItemAndAFiniteDelta()
    {
        var world = Scene(Rod(), Wall(5), Player);
        var code = (Func<EntityId, Vec3, FailureCode>)((entity, delta) => Assert.IsType<MoveEntityResult.Rejected>(world.MoveEntity(new(entity, delta))).Failure.Code);

        Assert.Equal(FailureCode.InvalidInput, code(RodId, new(.1, 0, 0)));
        Assert.Equal(FailureCode.WrongEntityKind, code(new("wall"), new(.1, 0, 0)));
        Assert.Equal(FailureCode.WrongEntityKind, code(Player.Id, new(.1, 0, 0)));
        Assert.Equal(FailureCode.UnknownId, code(new("missing"), new(.1, 0, 0)));
        world.HoldEntity(Player.Id, RodId);
        Assert.Equal(FailureCode.InvalidInput, code(RodId, new(double.NaN, 0, 0)));
        Assert.Equal(FailureCode.InvalidInput, code(RodId, new(0, double.PositiveInfinity, 0)));
        world.ReleaseEntity(RodId);
        world.HoldEntity(Player.Id, RodId);
        world.StartRotation(world.AllowedPlan(new StandUp(Player.Id, RodId, II)));
        Assert.Equal(FailureCode.Busy, code(RodId, new(.1, 0, 0)));
    }

    [Fact]
    public void HeldItemStaysInTheAirUntilItIsReleased()
    {
        var world = Scene(FloorOf(new(-.5, 1)), Rod() with { Pose = At(0, 2, 0) }, Player);
        world.HoldEntity(Player.Id, RodId);

        Assert.Empty(world.StepEvents(.1));
        Assert.Equal(2, world.State(RodId).Pose.Position.Y);
        Assert.Equal(FailureCode.EntityInUse, Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(RodId, At(0, 1, 0))).Failure.Code);
        Assert.Equal(FailureCode.EntityInUse, Assert.IsType<RemoveEntityResult.Rejected>(world.RemoveEntity(RodId)).Failure.Code);
        Assert.Equal(FailureCode.EntityInUse, Assert.IsType<RemoveEntityResult.Rejected>(world.RemoveEntity(Player.Id)).Failure.Code);

        world.ReleaseEntity(RodId);
        Assert.IsType<EntityFell>(Assert.Single(world.StepEvents(.1)).Event);
    }

    private static World HeldBall(params EntityDefinition[] more)
    {
        var world = Scene([FloorOf(new(-.5, 1)), Ball(y: .5), Player, .. more]);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(Player.Id, BallId));
        return world;
    }

    [Fact]
    public void HeldBallStopsWhereItsSliceInAnotherSectionMeetsAWall()
    {
        // The wall in II is 1 m away at x = .98 and spans W .414 to .426. The ball's center is .204 from it in W, so
        // there the ball is a sphere of radius sqrt(.25^2 - .204^2) = .1445 and stops that far from the wall's face.
        var world = HeldBall(Wall(1, "II"));

        var moved = Move(world, BallId, new(2, 0, 0));

        Assert.Equal(.98 - Math.Sqrt(.25 * .25 - .204 * .204), moved.Applied.X, 9);
        Assert.Equal(new EntityId("wall"), Assert.Single(moved.Contacts).Obstacle);
    }

    [Fact]
    public void ComponentsAreTriedInTheOrderXThenZ()
    {
        // The wall stands at x = 1 but only for z from .2 on. Going 2 m along x first passes it, since the ball,
        // .14 wide in section I, is clear of it at z = 0; moving along z first would have put the ball in its way.
        var wall = new FixedDefinition(new("wall"), At(1, 0, .6), TestSource, I, Shape(Part("part", new(.02, .5, .4))));
        var world = HeldBall(wall);

        var moved = Move(world, BallId, new(2, 0, .2));

        Assert.Equal(new Vec3(2, 0, .2), moved.Applied);
        Assert.Empty(moved.Contacts);
    }

    [Fact]
    public void HeldBallStopsOnTheFloorWhenLowered()
    {
        var world = HeldBall();

        var moved = Move(world, BallId, new(0, -1, 0));

        Assert.Equal(-.25, moved.Applied.Y, 9);
        var contact = Assert.Single(moved.Contacts);
        Assert.Equal(Floor, contact.Obstacle);
        Assert.Equal(1, contact.Normal.Y, 9);
        Assert.Equal(.25, world.State(BallId).Pose.Position.Y, 9);
    }

    [Fact]
    public void HeldBallMovesFreelyAboveTheFloor()
    {
        var world = HeldBall();

        var moved = Move(world, BallId, new(.5, 0, -.4));

        Assert.Equal(new Vec3(.5, 0, -.4), moved.Applied);
        Assert.Empty(moved.Contacts);
    }

    [Fact]
    public void HeldNutPushesItsHiddenRingIntoAPillarBeforeItsBodyGetsNear()
    {
        // The ring's center is at x = .41 and its tube reaches .05 further, to .46. The pillar exists only at the
        // ring's depth, W .1 to .3, and has its near face at x = .9: .44 away.
        var pillar = Block("pillar", new(.95, 1, 0), new(.05, 1, .5), new(.1, .3));
        var world = Scene(FloorOf(new(-.5, 1)), Nut(), Player, pillar);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(Player.Id, NutId));

        var moved = Move(world, NutId, new(1, 0, 0));

        Assert.Equal(.44, moved.Applied.X, 9);
        var contact = Assert.Single(moved.Contacts);
        Assert.Equal(RingPart, contact.MovingPart);
        Assert.Equal(new EntityId("pillar"), contact.Obstacle);
    }

    [Fact]
    public void StoodUpNutCarriesItsRingAlongInTheTargetSection()
    {
        var world = Scene(FloorOf(new(-.5, 1)), Nut(), Player);
        world.Stand(Player.Id, NutId, II);
        Assert.IsType<CarryResult.Changed>(world.HoldEntity(Player.Id, NutId));
        var before = world.SliceOf(NutId, II)!.Coordinates(Axis3.X).Min();

        var moved = Move(world, NutId, new(.1, 0, 0));

        Assert.Equal(new Vec3(.1, 0, 0), moved.Applied);
        Assert.Equal(before + .1, world.SliceOf(NutId, II)!.Coordinates(Axis3.X).Min(), 9);
    }
}
