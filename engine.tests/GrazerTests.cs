using static FourD.Tests.Fixtures;
using static FourD.Tests.GrazerScenes;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// The slices of a grazer's metaball body (docs/frame/contract-views.md) and the frames the host feeds it
// (docs/frame/contract-actions.md). The body of one lump is a four-dimensional ball of radius 3, so its slice a
// distance d from the center is a sphere of radius sqrt(3² - d²); the numbers below use the triple (1.8, 2.4, 3).
public class GrazerTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");

    // The mesh follows the field along the edges of cubes of .2 m, which puts its vertices within a tenth of a cube
    // of the sphere, and a little more where the sphere is only a few cubes across.
    private const double MeshError = .02;

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
        // Section I is 1.8 from the center in W, and section II, .42 nearer, is 1.38 from it: sqrt(9 - 1.38²) = 2.66376.
        var world = GrazerScene(At(0, 4, 0, w: 1.8), Lump(default));

        var inI = world.SliceOf(GrazerId, I)!;
        var inII = world.SliceOf(GrazerId, II)!;

        AssertSphere(inI, new(0, 4, 0), 2.4);
        AssertSphere(inII, new(0, 4, 0), 2.66376);
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
        var world = GrazerScene(At(0, 4, 0, w: 1.8), Lump(default));

        AssertSphere(world.SlicesAt(1.8).Single(slice => slice.Entity == GrazerId), new(0, 4, 0), LumpRadius);
        // A lump has appeared out of nothing, sqrt(3² - 2.4²) = 1.8 across.
        AssertSphere(world.SlicesAt(1.8 + 2.4).Single(slice => slice.Entity == GrazerId), new(0, 4, 0), 1.8);
        AssertSphere(world.SlicesAt(1.8 - 2.4).Single(slice => slice.Entity == GrazerId), new(0, 4, 0), 1.8);
        Assert.DoesNotContain(world.SlicesAt(1.8 + LumpRadius), slice => slice.Entity == GrazerId);
        Assert.DoesNotContain(world.SlicesAt(1.8 - LumpRadius - .01), slice => slice.Entity == GrazerId);
    }

    [Fact]
    public void AHeavierBallNeedsAHigherThresholdForTheSameBody()
    {
        var world = GrazerScene(At(0, 4, 0, w: 1.8), new([LumpBall(default, weight: 2)], 2 * LumpThreshold));

        AssertSphere(world.SliceOf(GrazerId, I)!, new(0, 4, 0), 2.4);
    }

    [Fact]
    public void TheMeshLiesOnTheLatticeThatStartsAtTheGrazersPosition()
    {
        // A ball of radius .005 at the grazer's position has only the node there in its body: the field is 1 at the
        // node and 0 at its neighbours, .2 m away. With the threshold .5 the surface cuts each edge from the node
        // halfway, .1 m out, whichever way the edge runs.
        var tiny = new MetaballShape([Ball("tiny", default, radius: .005)], .5);
        var world = GrazerScene(At(.03, 1.07, -.02), tiny);

        var slice = world.SliceOf(GrazerId, I)!;

        foreach (var (axis, middle) in new[] { (Axis3.X, .03), (Axis3.Y, 1.07), (Axis3.Z, -.02) })
        {
            var (min, max) = slice.Extent(axis);
            Assert.Equal(middle - .1, min, 9);
            Assert.Equal(middle + .1, max, 9);
        }
        // Its field reaches only 1 cm in W, less than the layer of an ordinary object.
        Assert.False(slice.ThroughObject);

        // Half a cube along from the grazer's position, there is no node in the body and so nothing to mesh.
        world.Frame(At(.03, 1.07, -.02), new([Ball("tiny", new(.1, 0, 0, 0), radius: .005)], .5));
        Assert.Null(world.SliceOf(GrazerId, I));
    }

    [Fact]
    public void AGrazerThatMovesAsAWholeKeepsItsMeshAndMovesItRigidly()
    {
        var world = GrazerScene(At(0, 4, 0, w: 1.8), Lump(default));
        var before = world.SliceOf(GrazerId, I)!;

        // Not a whole number of cubes in any direction.
        world.Frame(At(.07, 3.97, .11, w: 1.8), Lump(default));

        var after = world.SliceOf(GrazerId, I)!;
        Assert.Equal(before.Positions.Count, after.Positions.Count);
        var shift = new[] { .07, -.03, .11 };
        for (var i = 0; i < before.Positions.Count; i++)
            Assert.Equal(before.Positions[i] + shift[i % 3], after.Positions[i], 9);
    }

    [Fact]
    public void ASmallPartDoesNotFlickerWhileTheWholeGrazerMovesAHundredthOfAMetre()
    {
        // A lump and six small balls beside it, each making a body 12 cm in radius, less than a cube. Which of them
        // the lattice catches depends on where it lies from the grazer's position, which does not change.
        Metaball[] feelers = Enumerable.Range(0, 6)
            .Select(i => Ball($"feeler{i}", new(9, .13 * i - .3, .07 * i, 0), radius: .2))
            .ToArray();
        var shape = new MetaballShape([LumpBall(default), .. feelers], LumpThreshold);
        var world = GrazerScene(At(0, 4, 0), shape);
        var sizes = new List<int>();

        for (var step = 0; step < 30; step++)
        {
            world.Frame(At(.01 * step, 4 + .01 * step, -.01 * step), shape);
            sizes.Add(world.SliceOf(GrazerId, I)!.TrianglesLargerThan(1e-10));
        }

        Assert.Single(sizes.Distinct());
    }

    [Fact]
    public void ABodyOfExactlyTheMostNodesKeepsTheUsualCubes()
    {
        // Balls .005 in radius at the grazer's position and 25 m along X: the lattice runs from node -1 to node 126,
        // which is the 128 nodes it may have. Each ball has the cube of its node around it, .1 m out.
        var world = GrazerScene(At(0, 4, 0), new([Ball("near", default, radius: .005), Ball("far", new(25, 0, 0, 0), radius: .005)], .5));

        var (min, max) = world.SliceOf(GrazerId, I)!.Extent(Axis3.X);

        Assert.Equal(-.1, min, 9);
        Assert.Equal(25.1, max, 9);
    }

    [Fact]
    public void ABodyOneCubeOverTheMostNodesGetsCubesTwiceAsBig()
    {
        // The far ball is 25.4 m along, so the lattice would have 130 nodes. The cubes are .4 m, the near ball makes
        // a cube of .2 m out around its node, and the far ball lies 63.5 cubes along, with no node in its body.
        var world = GrazerScene(At(0, 4, 0), new([Ball("near", default, radius: .005), Ball("far", new(25.4, 0, 0, 0), radius: .005)], .5));

        var (min, max) = world.SliceOf(GrazerId, I)!.Extent(Axis3.X);

        Assert.Equal(-.2, min, 9);
        Assert.Equal(.2, max, 9);
    }

    [Fact]
    public void ABodyTooWideForTheLatticeIsMeshedInCoarserCubes()
    {
        // A lump of radius 240 has a field of radius 400, so it is 4000 cubes across: 6e10 nodes, more than an array
        // can hold. The lattice keeps to 128 nodes a side by taking cubes a whole number of times the usual, here
        // 32 × .2 = 6.4 m, so the mesh is coarser and still the sphere.
        var world = GrazerScene(At(0, 250, 0, w: .18), new([LumpBall(default, radius: 400)], LumpThreshold));

        var slice = world.SliceOf(GrazerId, I)!;

        // sqrt(240² - .18²) is 240 to within a millimetre. Errors of a tenth of a cube, 64 cm, would be too much.
        AssertSphere(slice, new(0, 250, 0), 240, tolerance: .2);
    }

    [Fact]
    public void BallsFuseWhereTheirFieldsAddUp()
    {
        // Balls 3 m either side of the middle. On the plane between them each gives (1 - (3² + 2²) / 5²)² = .48²,
        // so a threshold of 2 × .48² = .4608 puts the neck at radius 2. Each lump alone reaches 5 × sqrt(1 - sqrt(.4608))
        // from its center, which is 2.8336.
        var shape = new MetaballShape([LumpBall(new(-3, 0, 0, 0)) with { Id = new("left") }, LumpBall(new(3, 0, 0, 0)) with { Id = new("right") }], .4608);
        var world = GrazerScene(At(0, 4, 0), shape);

        var slice = world.SliceOf(GrazerId, I)!;

        // The plane x = 0 is a plane of the lattice, so the surface has vertices on it, all on the neck.
        var neck = Enumerable.Range(0, slice.Parts.Count)
            .Select(slice.Positions.Vertex)
            .Where(vertex => Math.Abs(vertex.X) < .1)
            .Select(vertex => Math.Sqrt(Math.Pow(vertex.Y - 4, 2) + vertex.Z * vertex.Z))
            .ToArray();
        Assert.NotEmpty(neck);
        Assert.All(neck, radius => Assert.InRange(radius, 2 - MeshError, 2 + 2 * MeshError));
        var (left, right) = slice.Extent(Axis3.X);
        Assert.Equal(-5.8336, left, 1.5 * MeshError);
        Assert.Equal(5.8336, right, 1.5 * MeshError);
    }

    [Fact]
    public void BallsTooFarApartMakeSeparateLumps()
    {
        var shape = new MetaballShape([LumpBall(new(-6, 0, 0, 0)) with { Id = new("left") }, LumpBall(new(6, 0, 0, 0)) with { Id = new("right") }], LumpThreshold);
        var world = GrazerScene(At(0, 4, 0), shape);

        var slice = world.SliceOf(GrazerId, I)!;

        Assert.DoesNotContain(slice.Coordinates(Axis3.X), x => Math.Abs(x) < 2.5);
        Assert.Equal(-9, slice.Extent(Axis3.X).Min, 1.5 * MeshError);
    }

    [Fact]
    public void ATurnedBodyBringsItsBallsIntoAnotherSection()
    {
        // A quarter turn in XW takes the ball 2 along local X to W = 2: section I is 2 from it, section II 1.58, and
        // sqrt(3² - 2²) = 2.23607, sqrt(3² - 1.58²) = 2.55021.
        var world = GrazerScene(At(0, 4, 0, rotation: XwQuarterTurn), Lump(new(2, 0, 0, 0)));

        AssertSphere(world.SliceOf(GrazerId, I)!, new(0, 4, 0), 2.23607);
        AssertSphere(world.SliceOf(GrazerId, II)!, new(0, 4, 0), 2.55021);

        // At 3.6 the ball has gone beyond the reach of both sections.
        world.Frame(At(0, 4, 0, rotation: XwQuarterTurn), Lump(new(3.6, 0, 0, 0)));
        Assert.False(world.HasSliceVolume(GrazerId, I));
        Assert.False(world.HasSliceVolume(GrazerId, II));
    }

    [Fact]
    public void AnUnturnedBodyKeepsItsBallsWhereTheyAreOffset()
    {
        // The ball 2 along X lies in section I, and section II is .42 from it: sqrt(3² - .42²) = 2.97045.
        var world = GrazerScene(At(0, 4, 0), Lump(new(2, 0, 0, 0)));

        AssertSphere(world.SliceOf(GrazerId, I)!, new(2, 4, 0), LumpRadius);
        AssertSphere(world.SliceOf(GrazerId, II)!, new(2, 4, 0), 2.97045);
    }

    [Fact]
    public void ANewFrameMovesTheSliceAtOnce()
    {
        var world = GrazerScene(At(0, 4, 0, w: 1.8), Lump(default));

        world.Frame(At(.4, 4, -.6, w: 1.8), Lump(default));

        var slice = world.SliceOf(GrazerId, I)!;
        var (xMin, xMax) = slice.Extent(Axis3.X);
        var (zMin, zMax) = slice.Extent(Axis3.Z);
        Assert.Equal(.4, (xMin + xMax) / 2, MeshError);
        Assert.Equal(-.6, (zMin + zMax) / 2, MeshError);
    }

    [Fact]
    public void ANewShapeChangesTheSliceAtOnce()
    {
        var world = GrazerScene(At(0, 4, 0, w: 1.8), Lump(default));

        // The mouth opens: the ball grows to radius 6 at the same weight, so its field reaches the threshold at
        // 6 × sqrt(1 - sqrt(.4096)) = 6 × sqrt(.36) = 3.6, and the section 1.8 from the center cuts a sphere of
        // radius sqrt(3.6² - 1.8²) = 3.11769.
        world.Frame(At(0, 4, 0, w: 1.8), new([LumpBall(default, radius: 6)], LumpThreshold));

        AssertSphere(world.SliceOf(GrazerId, I)!, new(0, 4, 0), 3.11769);
    }

    [Fact]
    public void BothViewsReadTheSameFrameAndTheSameSectionTwiceGivesTheSameMesh()
    {
        var world = GrazerScene(At(0, 4, 0, w: 1.8), Lump(default));
        var again = GrazerScene(At(0, 4, 0, w: 1.8), Lump(default));

        var main = world.SliceOf(GrazerId, I)!;
        var lens = world.SliceOf(GrazerId, I)!;

        Assert.Equal(main.Positions, lens.Positions);
        Assert.Equal(main.Positions, again.SliceOf(GrazerId, I)!.Positions);
        Assert.Equal(main.Normals, lens.Normals);
    }
}
