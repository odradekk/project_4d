using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// The nut of the size norms (docs/frame/content.md): an ordinary body with a ring that rests between sections
// I and II until the nut is stood up toward II. In these scenes the ring has a 20 cm radius and a 5 cm tube, its
// center is .41 m along the nut's long axis and .2 m deep, and the nut's hinge is at x = -.01. Standing up turns
// the point (x, W) = (.42, .2) about the hinge into (-.2, .42), so the ring's center ends at x = -.21, W = .42.
public class HiddenRingTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");
    private static readonly Creature3Definition Player = Actor();

    private static World Resting(params EntityDefinition[] more) => Scene([FloorOf(new(-.5, 1)), Nut(), Player, .. more]);

    // The block is where the ring passes at W = .3: there the turn has gone 14.7 degrees, which puts the ring's
    // center at x = .3454, so the block spans x = .3 to .4 and, in W, .28 to .32, over all of the ring's height.
    private static BuildingElementDefinition Block(double x) => ItemScenes.Block("block", new(x, 1, 0), new(.05, 1, .5), new(.28, .32));

    private static IEnumerable<Vec3> RingVertices(SliceGeometry slice) =>
        TrianglesOf(slice, RingPart).SelectMany(first => Enumerable.Range(first, 3).Select(slice.Positions.Vertex));

    // Distance from the ring's axis, which runs along X through y = .9, z = 0.
    private static double AroundAxis(Vec3 point) => Math.Sqrt(Math.Pow(point.Y - .9, 2) + point.Z * point.Z);

    [Fact]
    public void RingIsInNoSectionUntilTheNutIsStoodUp()
    {
        var world = Resting();

        Assert.Empty(TrianglesOf(world.SliceOf(NutId, I)!, RingPart));
        Assert.Null(world.SliceOf(NutId, II));
        Assert.False(world.HasSliceVolume(NutId, II));
    }

    [Fact]
    public void StandingTheNutUpTurnsTheRingIntoAHollowRingInTheTargetSection()
    {
        var world = Resting();

        world.Stand(Player.Id, NutId, II);

        var slice = world.SliceOf(NutId, II)!;
        var vertices = RingVertices(slice).ToArray();
        Assert.All(vertices, vertex =>
        {
            Assert.InRange(AroundAxis(vertex), .15 - .001, .25 + .001);
            Assert.InRange(vertex.X, -.21 - .053, -.21 + .053);
        });
        Assert.Equal(.65, vertices.Min(vertex => vertex.Y), 2);
        Assert.Equal(1.15, vertices.Max(vertex => vertex.Y), 2);
        Assert.Equal(-.25, vertices.Min(vertex => vertex.Z), 2);
        Assert.Equal(.25, vertices.Max(vertex => vertex.Z), 2);
        Assert.All(TrianglesOf(slice, RingPart), first => Assert.True(slice.IsWoundOutward(first)));
        // 64 segments along the circle and 24 around the tube make 1536 quads of two triangles.
        Assert.Equal(64 * 24 * 2, TrianglesOf(slice, RingPart).Count());
        Assert.True(slice.ThroughObject);
    }

    [Fact]
    public void RingShowsInTheTargetSectionOnlyOnceItHasTurnedFarEnough()
    {
        // The ring's tube first reaches W = .42 - .05 = .37 when its center is there: the center runs on a circle of
        // radius sqrt(.42^2 + .2^2) = .4652 about the hinge, so that is after asin(.37 / .4652) - atan(.2 / .42)
        // = 27.3 degrees. Half a second into the turn it has gone 17 degrees, and the whole turn takes 1.4 seconds.
        var world = Resting();
        var plan = world.AllowedPlan(new StandUp(Player.Id, NutId, II));
        world.StartRotation(plan);

        world.StepEvents(.5);
        Assert.Null(world.SliceOf(NutId, II));

        world.StepEvents(.9);
        Assert.NotEmpty(TrianglesOf(world.SliceOf(NutId, II)!, RingPart));
    }

    [Fact]
    public void RingPreviewOutlinesTheRingWhereStandingUpWillLeaveIt()
    {
        var world = Resting();
        var plan = world.AllowedPlan(new StandUp(Player.Id, NutId, II));

        var preview = Assert.IsType<PreviewResult.Preview>(world.GetHiddenStructurePreview(plan, II)).Value;

        var sample = Assert.Single(preview.Samples);
        Assert.Equal(1, sample.Progress);
        Assert.False(sample.Blocked);
        var points = sample.Contours.SelectMany(line => Enumerable.Range(0, line.Positions.Count / 3).Select(line.Positions.Vertex)).ToArray();
        // The outline is the ring cut through its middle: the circles of radius .2 - .05 and .2 + .05 at x = -.21.
        Assert.All(points, point => Assert.Equal(-.21, point.X, 9));
        Assert.Contains(points, point => Math.Abs(AroundAxis(point) - .15) < .003);
        Assert.Contains(points, point => Math.Abs(AroundAxis(point) - .25) < .003);
        Assert.All(points, point => Assert.True(Math.Abs(AroundAxis(point) - .15) < .003 || Math.Abs(AroundAxis(point) - .25) < .003));
    }

    [Fact]
    public void StandingUpIsSampledSoNoPointOfTheRingMovesMoreThanACentimeterBetweenSamples()
    {
        // The ring's farthest point from the hinge is .465 + .05 away: the center is (.42, .2) from the hinge in x
        // and W, and the tube reaches .05 further. A quarter turn takes it .5152 * pi / 2 = .809 m, in 81 steps.
        var world = Resting();
        var plan = world.AllowedPlan(new StandUp(Player.Id, NutId, II));

        var preview = Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan, II)).Value;

        Assert.Equal(82, preview.Samples.Count);
    }

    [Fact]
    public void RingPreviewIsEmptyInASectionTheRingDoesNotReachAndRefusesOtherPlans()
    {
        var world = Resting();
        var plan = world.AllowedPlan(new StandUp(Player.Id, NutId, II));

        var preview = Assert.IsType<PreviewResult.Preview>(world.GetHiddenStructurePreview(plan, I)).Value;
        Assert.Empty(Assert.Single(preview.Samples).Contours);

        var flip = world.AllowedPlan(new Flip(Player.Id, NutId));
        var wrongKind = Assert.IsType<PreviewResult.Rejected>(world.GetHiddenStructurePreview(flip, II));
        Assert.Equal(FailureCode.WrongEntityKind, wrongKind.Failure.Code);
        world.StartRotation(plan);
        var stale = Assert.IsType<PreviewResult.Rejected>(world.GetHiddenStructurePreview(plan, II));
        Assert.Equal(FailureCode.StalePlan, stale.Failure.Code);
    }

    [Fact]
    public void StandingTheNutUpAgainstABlockInTheRingsPathIsRefusedAndShowsRed()
    {
        var world = Resting(Block(.35));

        var blocked = Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new StandUp(Player.Id, NutId, II)));

        Assert.Equal(FailureCode.RotationBlocked, blocked.Failure.Code);
        var contact = blocked.Failure.Contacts[0];
        Assert.Equal(RingPart, contact.MovingPart);
        Assert.Equal(new EntityId("block"), contact.Obstacle);
        var start = Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(blocked.Plan));
        Assert.Equal(FailureCode.RotationBlocked, start.Failure.Code);
        var preview = Assert.IsType<PreviewResult.Preview>(world.GetHiddenStructurePreview(blocked.Plan, II)).Value;
        Assert.True(Assert.Single(preview.Samples).Blocked);
    }

    [Fact]
    public void LayingTheNutDownBringsTheRingBackBetweenTheSections()
    {
        var world = Resting();
        world.Stand(Player.Id, NutId, II);
        Assert.NotNull(world.SliceOf(NutId, II));

        var plan = world.AllowedPlan(new LayDown(Player.Id, NutId));
        world.StartRotation(plan);
        world.StepEvents(1);

        Assert.Null(world.SliceOf(NutId, II));
        Assert.Empty(TrianglesOf(world.SliceOf(NutId, I)!, RingPart));
        AssertNear(new(0, .03, 0, 0), world.State(NutId).Pose.Position);
        Assert.Equal(I, world.State(NutId).Section);
    }

    [Fact]
    public void NutCannotBePlacedWithItsRingInsideABlock()
    {
        // The block spans W .1 to .3 and x .3 to .4 over all heights: the ring at x = .41 (tube from .36) is in it.
        var world = Resting(ItemScenes.Block("block", new(.35, 1, 0), new(.05, 1, .5), new(.1, .3)));

        var rejected = Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(NutId, At(0, .03, 0)));
        Assert.Equal(FailureCode.TranslationBlocked, rejected.Failure.Code);
        Assert.Equal(RingPart, rejected.Failure.Contacts[0].MovingPart);

        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(NutId, At(-.5, .03, 0)));
    }

    [Fact]
    public void StandingTheNutUpBeyondTheBlockIsAllowed()
    {
        var world = Resting(Block(.8));

        Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(Player.Id, NutId, II)));
    }

    [Fact]
    public void RingHangingBelowTheBodyHoldsTheNutUpOnItsLowestPoint()
    {
        // The ring's center is .1 below the nut's origin, so the tube's lowest point is .1 + .2 + .05 = .35 below it.
        var world = Scene(FloorOf(new(-.5, 1)), Nut(At(0, 2, 0), ringY: -.1), Player);

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(NutId));

        Assert.Equal(.35, support.RestingOriginY, 10);
        var contact = Assert.Single(support.Contacts);
        Assert.Equal(Floor, contact.Support);
        AssertNear(new(.41, 0, 0, .2), contact.Point);
        var fell = Assert.IsType<EntityFell>(Assert.Single(world.StepEvents(.1)).Event);
        Assert.Equal(.35, fell.ToY, 10);
    }

    [Fact]
    public void NutLyingOnItsBodyKeepsItsRingAboveTheFloor()
    {
        var world = Resting();

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(NutId));

        Assert.Equal(.03, support.RestingOriginY, 10);
        Assert.Equal(Floor, Assert.Single(support.Contacts).Support);
        AssertNear(new(0, 0, 0, 0), Assert.Single(support.Contacts).Point with { X = 0, Z = 0, W = 0 });
    }

    [Theory]
    [InlineData(0, 0, false)] // the middle of the hole
    [InlineData(.2, 0, true)] // on the circle
    [InlineData(.2, .06, true)] // 6 cm deeper: the box's face is 4 cm from the circle, inside the 5 cm tube
    [InlineData(.2, .08, false)] // 8 cm deeper: its face is 6 cm away
    [InlineData(.264, 0, true)] // the box's face is 6 mm inside the tube's outer edge, at .2 + .05
    [InlineData(.266, 0, false)] // 4 mm inside it, within the 5 mm contact tolerance
    public void OnlyTheTubeOfTheRingIsOccupiedNotItsHole(double up, double deeper, bool occupied)
    {
        // A box 4 cm across, offset from the ring's center at (.41, .9, 0, .2).
        var world = Resting();
        var query = new OccupancyBox(At(.41, .9 + up, 0, .2 + deeper), new(.02, .02, .02, .02));

        var hits = world.OccupancyHits(query);

        Assert.Equal(occupied, hits.Any(hit => hit.Part == RingPart));
    }

    [Theory]
    [InlineData(.14, false)] // the ball in the ring's hole is .2 from the circle: .05 + .14 leaves a gap
    [InlineData(.154, false)] // .05 + .154 reaches 4 mm into the tube: within the tolerance
    [InlineData(.156, true)] // 6 mm into it
    public void BallInTheHoleOfTheRingIsOccupiedOnceItReachesIntoTheTube(double radius, bool occupied)
    {
        var world = Resting();
        var query = new OccupancySphere(new(.41, .9, 0, .2), radius);

        var hit = world.OccupancyHits(query).SingleOrDefault(hit => hit.Part == RingPart);

        Assert.Equal(occupied, hit is not null);
        if (hit is null) return;
        // The point lies inside both shapes: within the query sphere, and within the tube of the circle.
        var offset = Math.Sqrt(Math.Pow(hit.Point.X - .41, 2) + Math.Pow(hit.Point.Y - .9, 2) + Math.Pow(hit.Point.Z, 2) + Math.Pow(hit.Point.W - .2, 2));
        var aroundCircle = Math.Sqrt(Math.Pow(hit.Point.Y - .9, 2) + hit.Point.Z * hit.Point.Z) - .2;
        var tube = Math.Sqrt(aroundCircle * aroundCircle + Math.Pow(hit.Point.X - .41, 2) + Math.Pow(hit.Point.W - .2, 2));
        Assert.True(offset <= radius + 1e-9);
        Assert.True(tube <= .05 + 1e-9);
    }

    [Fact]
    public void RingTurnedWithinItsPlaneRestsOnItsLowestPointBetweenTheSamples()
    {
        // The circle lies in a plane turned so that its lowest point is at an angle that no coarse sample hits.
        var turned = new Rotation4(new(.6, .8, 0, 0), new(-.8, .6, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1));
        var nut = Nut(At(0, 2, 0), ringY: -.1);
        var hidden = (HiddenRingItem)nut.Shape;
        // Turning the ring about the nut's hinge now swings it farther in W, so the building is wider.
        var turnedNut = nut with { Shape = hidden with { Ring = hidden.Ring with { Rotation = turned } } };
        var world = Load(Description([FloorOf(new(-1, 1)), turnedNut, Player], buildingMin: -1));

        var support = Assert.IsType<SupportResult.Supported>(world.FindSupport(NutId));

        // Here the circle is vertical, so its lowest point is .2 below its center and the tube reaches .05 further.
        Assert.Equal(.35, support.RestingOriginY, 10);
    }

    [Fact]
    public void TiltedRingSliceLiesOnTheTubeAroundTheTiltedCircle()
    {
        // The player stands on the Z side, so flipping the nut turns it in the plane of Z and W, which tilts the ring's
        // plane (it contains Z) through W. The slice at section I picks up the ring while it passes through it.
        var player = Actor(x: 0) with { Pose = At(0, 0, -1) };
        var world = Scene(FloorOf(new(-.5, 1)), Nut(), player);
        world.StartRotation(world.AllowedPlan(new Flip(player.Id, NutId)));
        world.StepEvents(.7);

        var state = world.State(NutId);
        var slice = world.SliceOf(NutId, I)!;
        var vertices = RingVertices(slice).ToArray();
        Assert.NotEmpty(vertices);
        // Every vertex is as far from the ring's circle as the tube is thick: the circle is the ring's center
        // plus .2 times the turns of its two axes, all carried through the nut's current pose.
        var rotation = state.Pose.Rotation;
        Vec4 Carry(Vec4 local) => new(
            rotation.X.X * local.X + rotation.Y.X * local.Y + rotation.Z.X * local.Z + rotation.W.X * local.W,
            rotation.X.Y * local.X + rotation.Y.Y * local.Y + rotation.Z.Y * local.Z + rotation.W.Y * local.W,
            rotation.X.Z * local.X + rotation.Y.Z * local.Y + rotation.Z.Z * local.Z + rotation.W.Z * local.W,
            rotation.X.W * local.X + rotation.Y.W * local.Y + rotation.Z.W * local.Z + rotation.W.W * local.W);
        var ring = ((HiddenRingItem)Nut().Shape).Ring;
        var center = Carry(ring.Center);
        var u = Carry(ring.Rotation.X);
        var v = Carry(ring.Rotation.Y);
        var origin = new[] { state.Pose.Position.X + center.X, state.Pose.Position.Y + center.Y, state.Pose.Position.Z + center.Z, state.Pose.Position.W + center.W };
        Assert.All(vertices, vertex =>
        {
            double[] offset = [vertex.X - origin[0], vertex.Y - origin[1], vertex.Z - origin[2], 0 - origin[3]];
            double[] ua = [u.X, u.Y, u.Z, u.W], va = [v.X, v.Y, v.Z, v.W];
            var a = offset.Zip(ua, (p, q) => p * q).Sum();
            var b = offset.Zip(va, (p, q) => p * q).Sum();
            var across = offset.Select((p, i) => p - a * ua[i] - b * va[i]).Sum(p => p * p);
            var distance = Math.Sqrt(Math.Pow(Math.Sqrt(a * a + b * b) - ring.MajorRadius, 2) + across);
            Assert.InRange(distance, .0499, .0501);
        });
        Assert.All(TrianglesOf(slice, RingPart), first => Assert.True(slice.IsWoundOutward(first)));
    }
}
