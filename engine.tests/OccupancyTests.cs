using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public class OccupancyTests
{
    // A rotation within 3D space that mixes the x, y, and z axes. It leaves W alone.
    private static Rotation4 MixedXyzRotation()
    {
        var s = Math.Sqrt(.5);
        return new(new(s, s, 0, 0), new(-.5, .5, s, 0), new(.5, -.5, s, 0), Identity4.W);
    }

    // A 2 m cube wall around the origin, present for W in [-1, 1].
    private static World WallWorld()
    {
        var wall = new BuildingElementDefinition(
            new("wall"), Origin, TestSource, new("b"), new(-1, 1), [], Shape(Part("part", new(1, 1, 1))));
        return Load(new([new(new("I"), 0, TestSource)], [new(new("b"), new(-2, 2), TestSource)], [wall], [], [], []));
    }

    private static World FixedBoxWorld()
    {
        // A 2 x 2 x 0.5 m box around the origin, present only in section I.
        var part = Part("part", new(1, 1, .25));
        var fixedBox = new FixedDefinition(new("fixed"), Origin, TestSource, new("I"), Shape(part));
        return Load(new([new(new("I"), 0, TestSource)], [], [fixedBox], [], [], []));
    }

    [Fact]
    public void BoxVolumeIntersectionReturnsRealPoint()
    {
        var query = new OccupancyBox(At(.5, 0, 0), new(.5, .5, .5, .5));

        var hit = Assert.Single(WallWorld().OccupancyHits(query));

        Assert.Equal(new EntityId("wall"), hit.Entity);
        Assert.Equal(new PartId("part"), hit.Part);
        Assert.InRange(hit.Point.X, 0, 1);
        Assert.InRange(hit.Point.Y, -.5, .5);
        Assert.InRange(hit.Point.Z, -.5, .5);
        Assert.InRange(hit.Point.W, -.5, .5);
    }

    [Theory]
    [InlineData(1.494, true)]
    [InlineData(1.495, false)]
    [InlineData(1.496, false)]
    public void FiveMillimeterContactIsNotOccupied(double x, bool occupied)
    {
        var query = new OccupancyBox(At(x, 0, 0), new(.5, .5, .5, .5));

        var hits = WallWorld().OccupancyHits(query);

        Assert.Equal(occupied, hits.Count != 0);
    }

    [Fact]
    public void SphereQueryReturnsPointInsideBothShapes()
    {
        var world = WallWorld();

        var hit = Assert.Single(world.OccupancyHits(new OccupancySphere(new(1.2, 0, 0, .2), .3)));

        Assert.InRange(hit.Point.X, -1, 1);
        Assert.InRange(hit.Point.W, -1, 1);
        Assert.True(Math.Pow(hit.Point.X - 1.2, 2) + Math.Pow(hit.Point.W - .2, 2) <= .09);
        Assert.Empty(world.OccupancyHits(new OccupancySphere(new(1.296, 0, 0, 0), .3)));
    }

    [Fact]
    public void ExclusionsAndInvalidShapesAreChecked()
    {
        var world = WallWorld();
        var sphere = new OccupancySphere(default, .5);

        Assert.Empty(world.OccupancyHits(sphere, new EntityId("wall")));
        var unknown = Assert.IsType<OccupancyResult.Rejected>(world.QueryOccupancy(sphere, [new("missing")]));
        Assert.Equal(FailureCode.UnknownId, unknown.Failure.Code);
        var duplicate = Assert.IsType<OccupancyResult.Rejected>(world.QueryOccupancy(sphere, [new("wall"), new("wall")]));
        Assert.Equal(FailureCode.InvalidInput, duplicate.Failure.Code);
        var noRadius = Assert.IsType<OccupancyResult.Rejected>(world.QueryOccupancy(new OccupancySphere(default, 0), []));
        Assert.Equal(FailureCode.InvalidInput, noRadius.Failure.Code);
    }

    [Fact]
    public void MixedFourDimensionalAxisSeparatesBoxesEvenWhenFacesOverlap()
    {
        var query = new OccupancyBox(new(new(0, 2.25, 0, 0), MixedXyzRotation()), new(1, 1, .25, Epsilon));

        Assert.Empty(FixedBoxWorld().OccupancyHits(query));
    }

    [Fact]
    public void MixedAxisPenetrationWithinToleranceIsNotOccupied()
    {
        // The boxes are .0948 m apart along the direction (0, -sqrt(2/3), 1/sqrt(3)). Moving the query box
        // this far along it buries it 3 mm, within the 5 mm contact tolerance.
        const double shift = .0978087444407;
        var center = new Vec4(0, 2.25 - shift * Math.Sqrt(2d / 3d), shift / Math.Sqrt(3), 0);
        var query = new OccupancyBox(new(center, MixedXyzRotation()), new(1, 1, .25, Epsilon));

        Assert.Empty(FixedBoxWorld().OccupancyHits(query));
    }
}
