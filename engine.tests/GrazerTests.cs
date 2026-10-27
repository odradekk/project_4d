using static FourD.Tests.Fixtures;
using static FourD.Tests.GrazerScenes;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// The slices of a grazer's metaball body (docs/frame/contract-views.md) and the frames the host feeds it
// (docs/frame/contract-actions.md). The body of one lump is a four-dimensional ball of radius .3, so its slice a
// distance d from the center is a sphere of radius sqrt(.3² - d²); the numbers below use the triples (.18, .24, .3).
public class GrazerTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");

    // The mesh follows the field along the edges of cubes of 2 cm, so its vertices lie within a millimetre of the
    // sphere, and within a few where the sphere is only a few cells across.
    private const double MeshError = .001;

    private static void AssertSphere(SliceGeometry slice, Vec3 center, double radius, double tolerance = MeshError)
    {
        var (shortest, longest) = DistancesFrom(slice, center);
        Assert.InRange(shortest, radius - tolerance, radius + tolerance);
        Assert.InRange(longest, radius - tolerance, radius + tolerance);
        Assert.Equal(0, slice.Parts.Count % 3);
        Assert.All(Enumerable.Range(0, slice.Parts.Count / 3).Select(t => 3 * t), first => Assert.True(slice.IsWoundOutward(first)));
        Assert.All(Enumerable.Range(0, slice.Parts.Count), i => Assert.Equal(1, slice.Normals.Vertex(i).Length(), 9));
        // Wound outward, the closed surface encloses the volume of the sphere, to within the 3 × tolerance / radius
        // that a radial error of the tolerance makes of it.
        var ratio = slice.EnclosedVolume(center) / (4.0 / 3 * Math.PI * Math.Pow(radius, 3));
        Assert.InRange(ratio, 1 - 3 * tolerance / radius, 1 + 3 * tolerance / radius);
    }

    [Fact]
    public void ASliceOfALumpIsASphereOfTheRadiusItsDistanceFromTheBodyLeaves()
    {
        var world = GrazerScene(At(0, 1, 0, w: .18), Lump(default));

        var inI = world.SliceOf(GrazerId, I)!;
        var inII = world.SliceOf(GrazerId, II)!;

        AssertSphere(inI, new(0, 1, 0), .24);
        AssertSphere(inII, new(0, 1, 0), .18);
        Assert.All(new[] { inI, inII }, slice =>
        {
            Assert.True(slice.ThroughObject);
            Assert.All(slice.Parts, part => Assert.Equal(new PartId("body"), part));
            for (var i = 0; i < slice.Parts.Count; i++)
            {
                var position = slice.Positions.Vertex(i);
                Assert.Equal(position.X, slice.Uvs[2 * i]);
                Assert.Equal(position.Z, slice.Uvs[2 * i + 1]);
            }
        });
    }

    [Fact]
    public void ASliceGrowsToTheBodyRadiusAtItsCenterAndVanishesAtItsPoles()
    {
        var world = GrazerScene(At(0, 1, 0, w: .18), Lump(default));

        AssertSphere(world.SlicesAt(.18).Single(slice => slice.Entity == GrazerId), new(0, 1, 0), LumpRadius);
        // sqrt(.3² - .29²) = .0768: a small lump has appeared out of nothing.
        AssertSphere(world.SlicesAt(.18 + .29).Single(slice => slice.Entity == GrazerId), new(0, 1, 0), .0768, 3 * MeshError);
        AssertSphere(world.SlicesAt(.18 - .29).Single(slice => slice.Entity == GrazerId), new(0, 1, 0), .0768, 3 * MeshError);
        Assert.DoesNotContain(world.SlicesAt(.18 + LumpRadius), slice => slice.Entity == GrazerId);
        Assert.DoesNotContain(world.SlicesAt(.18 - LumpRadius - .01), slice => slice.Entity == GrazerId);
    }

    [Fact]
    public void AHeavierBallNeedsAHigherThresholdForTheSameBody()
    {
        var world = GrazerScene(At(0, 1, 0, w: .18), new([Ball("lump", default, weight: 2)], 2 * LumpThreshold));

        AssertSphere(world.SliceOf(GrazerId, I)!, new(0, 1, 0), .24);
    }

    [Fact]
    public void TheMeshLiesOnTheWorldsLatticeOfTwoCentimetreCubes()
    {
        // A ball of radius .005 whose center is the lattice node (0, 1, 0) has only that node in its body: the field
        // is 1 there and 0 at its neighbours. With the threshold .5 the surface cuts each edge from the node halfway,
        // 1 cm out, whichever way the edge runs.
        var tiny = new MetaballShape([Ball("tiny", default, radius: .005)], .5);
        var world = GrazerScene(At(0, 1, 0), tiny);

        var slice = world.SliceOf(GrazerId, I)!;

        foreach (var (axis, middle) in new[] { (Axis3.X, 0d), (Axis3.Y, 1d), (Axis3.Z, 0d) })
        {
            var (min, max) = slice.Extent(axis);
            Assert.Equal(middle - .01, min, 9);
            Assert.Equal(middle + .01, max, 9);
        }
        // Its field reaches only 1 cm in W, less than the layer of an ordinary object.
        Assert.False(slice.ThroughObject);

        // Half a cube along, there is no node in the body and so nothing to mesh.
        world.Frame(At(.01, 1, 0), tiny);
        Assert.Null(world.SliceOf(GrazerId, I));
    }

    [Fact]
    public void BallsFuseWhereTheirFieldsAddUp()
    {
        // Balls .3 m either side of the middle. On the plane between them each gives (1 - (.3² + .2²) / .5²)² = .48²,
        // so a threshold of 2 × .48² = .4608 puts the neck at radius .2. Each lump alone reaches .5 × sqrt(1 - sqrt(.4608)) from its center.
        var shape = new MetaballShape([Ball("left", new(-.3, 0, 0, 0)), Ball("right", new(.3, 0, 0, 0))], .4608);
        var world = GrazerScene(At(0, 1, 0), shape);

        var slice = world.SliceOf(GrazerId, I)!;

        var neck = Enumerable.Range(0, slice.Parts.Count)
            .Select(slice.Positions.Vertex)
            .Where(vertex => Math.Abs(vertex.X) < .01)
            .Select(vertex => Math.Sqrt(Math.Pow(vertex.Y - 1, 2) + vertex.Z * vertex.Z))
            .ToArray();
        Assert.NotEmpty(neck);
        Assert.All(neck, radius => Assert.InRange(radius, .2 - 2 * MeshError, .2 + 2 * MeshError));
        var (left, right) = slice.Extent(Axis3.X);
        var reach = .3 + .5 * Math.Sqrt(1 - Math.Sqrt(.4608));
        Assert.Equal(-reach, left, 2);
        Assert.Equal(reach, right, 2);
    }

    [Fact]
    public void BallsTooFarApartMakeSeparateLumps()
    {
        var shape = new MetaballShape([Ball("left", new(-.6, 0, 0, 0)), Ball("right", new(.6, 0, 0, 0))], LumpThreshold);
        var world = GrazerScene(At(0, 1, 0), shape);

        var slice = world.SliceOf(GrazerId, I)!;

        Assert.DoesNotContain(slice.Coordinates(Axis3.X), x => Math.Abs(x) < .25);
        Assert.Equal(-.6 - LumpRadius, slice.Extent(Axis3.X).Min, 2);
    }

    [Fact]
    public void ATurnedBodyBringsItsBallsIntoAnotherSection()
    {
        // A quarter turn in XW takes the ball .42 along local X to W = .42, which is section II.
        var world = GrazerScene(At(0, 1, 0, rotation: XwQuarterTurn), Lump(new(.42, 0, 0, 0)));

        AssertSphere(world.SliceOf(GrazerId, II)!, new(0, 1, 0), LumpRadius);
        Assert.False(world.HasSliceVolume(GrazerId, I));
    }

    [Fact]
    public void AnUnturnedBodyKeepsItsBallsWhereTheyAreOffset()
    {
        var world = GrazerScene(At(0, 1, 0), Lump(new(.42, 0, 0, 0)));

        AssertSphere(world.SliceOf(GrazerId, I)!, new(.42, 1, 0), LumpRadius);
        Assert.False(world.HasSliceVolume(GrazerId, II));
    }

    [Fact]
    public void ANewFrameMovesTheSliceAtOnce()
    {
        var world = GrazerScene(At(0, 1, 0, w: .18), Lump(default));
        var before = world.SliceOf(GrazerId, I)!;

        world.Frame(At(.1, 1, -.2, w: .18), Lump(default));

        var after = world.SliceOf(GrazerId, I)!;
        // A move of whole cells, so the mesh moves with it.
        Assert.Equal(before.Extent(Axis3.X).Min + .1, after.Extent(Axis3.X).Min, 9);
        Assert.Equal(before.Extent(Axis3.X).Max + .1, after.Extent(Axis3.X).Max, 9);
        Assert.Equal(before.Extent(Axis3.Z).Min - .2, after.Extent(Axis3.Z).Min, 9);
        Assert.Equal(before.Extent(Axis3.Z).Max - .2, after.Extent(Axis3.Z).Max, 9);
    }

    [Fact]
    public void ANewShapeChangesTheSliceAtOnce()
    {
        var world = GrazerScene(At(0, 1, 0, w: .18), Lump(default));

        // The mouth opens: the ball grows to radius .6 at the same weight, so its field reaches the threshold at
        // .6 × sqrt(1 - sqrt(.4096)) = .6 × sqrt(.36) = .36, and the section .18 from the center cuts a sphere of
        // radius sqrt(.36² - .18²) = .3118.
        world.Frame(At(0, 1, 0, w: .18), new([Ball("lump", default, radius: .6)], LumpThreshold));

        AssertSphere(world.SliceOf(GrazerId, I)!, new(0, 1, 0), Math.Sqrt(.36 * .36 - .18 * .18));
    }

    [Fact]
    public void BothViewsReadTheSameFrameAndTheSameSectionTwiceGivesTheSameMesh()
    {
        var world = GrazerScene(At(0, 1, 0, w: .18), Lump(default));
        var again = GrazerScene(At(0, 1, 0, w: .18), Lump(default));

        var main = world.SliceOf(GrazerId, I)!;
        var lens = world.SliceOf(GrazerId, I)!;

        Assert.Equal(main.Positions, lens.Positions);
        Assert.Equal(main.Positions, again.SliceOf(GrazerId, I)!.Positions);
        Assert.Equal(main.Normals, lens.Normals);
    }
}
