using static FourD.Tests.DeviceScenes;
using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;

namespace FourD.Tests;

// The slices of lofted devices: the key shapes, the shapes between them, topology changes and turned devices.
public class LoftSliceTests
{
    private static readonly SectionId I = new("I");
    private static readonly SectionId II = new("II");

    private static SliceGeometry Slice(World world, EntityId id, double w) =>
        Assert.Single(world.SlicesAt(w), slice => slice.Entity == id);

    [Fact]
    public void AKeyShapeIsTheSliceAtItsW()
    {
        var world = Scene(Hanger());

        var flap = world.SliceOf(HangerId, I)!;
        AssertRange(flap.Range(Axis3.X), -.01, .01);
        AssertRange(flap.Range(Axis3.Y), 1.6, 2);
        AssertRange(flap.Range(Axis3.Z), -.3, .3);
        var plank = world.SliceOf(HangerId, II)!;
        AssertRange(plank.Range(Axis3.X), -.01, .01);
        AssertRange(plank.Range(Axis3.Y), .8, 2);
        AssertRange(plank.Range(Axis3.Z), -.1, .1);
        Assert.All(new[] { flap, plank }, slice => Assert.All(slice.Parts, part => Assert.Equal(new PartId("leaf"), part)));
    }

    [Fact]
    public void BetweenKeysEveryVertexIsInterpolatedLinearly()
    {
        var world = Scene(Hanger());

        // Halfway between the flap at W = 0 and the plank at .42, and a quarter of the way.
        var half = Slice(world, HangerId, .21);
        AssertRange(half.Range(Axis3.Y), 1.2, 2);
        AssertRange(half.Range(Axis3.Z), -.2, .2);
        var quarter = Slice(world, HangerId, .105);
        AssertRange(quarter.Range(Axis3.Y), 1.4, 2);
        AssertRange(quarter.Range(Axis3.Z), -.25, .25);
    }

    [Fact]
    public void ASlopingFaceIsCutAtTheInterpolatedHeight()
    {
        var world = Scene(Sash());

        // The lower edge of the pane falls from y = .13 at W = -.2 to -.19 at .6, which is .05 at section I and -.118
        // at section II.
        AssertRange(world.SliceOf(SashId, I)!.Range(Axis3.Y), .05, .9);
        AssertRange(world.SliceOf(SashId, II)!.Range(Axis3.Y), -.118, .9);
    }

    [Fact]
    public void ALoftHasNoSliceOutsideItsKeys()
    {
        var world = Scene(Hanger());

        Assert.Empty(world.SlicesAt(-.3));
        Assert.Empty(world.SlicesAt(.7));
        Assert.Single(world.SlicesAt(-.2));
        Assert.Single(world.SlicesAt(.62));
    }

    [Fact]
    public void ASliceAtASharedKeyIsCutOnce()
    {
        var world = Scene(Hanger());

        // The flap is a 2 cm x 40 cm x 60 cm box: two triangles for each of its six faces, and its surface area.
        var flap = world.SliceOf(HangerId, I)!;
        Assert.Equal(36, flap.Parts.Count);
        Assert.Equal(2 * (.02 * .4 + .02 * .6 + .4 * .6), flap.Area(), 9);
    }

    [Fact]
    public void ADeviceThatSpansWIsAThroughObjectAndAThinOneIsNot()
    {
        var thin = new DeviceDefinition(
            new("thin"), Origin, TestSource,
            Loft(Key(-Epsilon, Pane(0)), Key(Epsilon, Pane(0))), new DeviceTranslation(Axis3.X, new(0, 1), 0));
        var world = Scene(Hanger(), thin);

        Assert.True(world.SliceOf(HangerId, I)!.ThroughObject);
        Assert.False(world.SliceOf(thin.Id, I)!.ThroughObject);
    }

    [Fact]
    public void ATopologyChangeSwitchesToAnotherSetOfPartsAtItsW()
    {
        // One slab below W = .2, split in two above it.
        var loft = Loft(
            new LoftSegment([Key(-.2, LoftBox("slab", (0, 1), (0, 1), (0, 1))), Key(.2, LoftBox("slab", (0, 1), (0, 1), (0, 1)))]),
            new LoftSegment([
                Key(.2, LoftBox("left", (0, .5), (0, 1), (0, 1)), LoftBox("right", (.5, 1), (0, 1), (0, 1))),
                Key(.6, LoftBox("left", (0, .5), (0, 1), (0, 1)), LoftBox("right", (.5, 1), (0, 1), (0, 1)))]));
        var world = Scene(new DeviceDefinition(new("split"), Origin, TestSource, loft, new DeviceTranslation(Axis3.X, new(0, 1), 0)));

        Assert.Equal(new[] { new PartId("slab") }, Slice(world, new("split"), 0).Parts.Distinct());
        Assert.Equal(new[] { new PartId("left"), new PartId("right") }, Slice(world, new("split"), .4).Parts.Distinct());
        // At the shared W both shapes are present.
        Assert.Equal(
            new[] { new PartId("left"), new PartId("right"), new PartId("slab") },
            Slice(world, new("split"), .2).Parts.Distinct().OrderBy(part => part.Value, StringComparer.Ordinal));
    }

    [Fact]
    public void ATurnedDeviceShowsEverySliceTurnedByTheSameAngle()
    {
        var world = Scene(Hanger());

        world.Driven(HangerId, Math.PI / 4);

        // A point (x, y) about the hinge at (0, 2) turns to ((x - y) / sqrt 2, (x + y) / sqrt 2). The flap hangs over
        // x of ±.01 and y from -.4 to 0, the plank to -1.2.
        var root = Math.Sqrt(2);
        var flap = world.SliceOf(HangerId, I)!;
        AssertRange(flap.Range(Axis3.X), -.01 / root, .41 / root);
        AssertRange(flap.Range(Axis3.Y), 2 - .41 / root, 2 + .01 / root);
        AssertRange(flap.Range(Axis3.Z), -.3, .3);
        var plank = world.SliceOf(HangerId, II)!;
        AssertRange(plank.Range(Axis3.X), -.01 / root, 1.21 / root);
        AssertRange(plank.Range(Axis3.Y), 2 - 1.21 / root, 2 + .01 / root);
        AssertRange(plank.Range(Axis3.Z), -.1, .1);
    }

    [Theory]
    [InlineData(Math.PI / 6, .2)]
    [InlineData(Math.PI / 4, .1414213562373095)]
    public void ATurnIntoWCutsTheBoardShorterAsItLeavesTheSection(double angle, double length)
    {
        var world = Scene(Board());

        world.Driven(BoardId, angle);

        // A point of the board at x' along it and w' across it is at W = x' sin + w' cos and X = x' cos - w' sin. At
        // W = 0 the board lies where w' = -x' tan, within ±.1, so x' runs from 0 to .1 / tan and X from 0 to .1 / sin.
        var slice = world.SliceOf(BoardId, I)!;
        AssertRange(slice.Range(Axis3.X), 0, length);
        AssertRange(slice.Range(Axis3.Y), 1, 1.2);
        AssertRange(slice.Range(Axis3.Z), 0, .2);
        Assert.Null(world.SliceOf(BoardId, II));
    }

    [Fact]
    public void FaceUvsFollowTheRuleOfTheFacesOfABox()
    {
        var slice = Slice(Scene(Lift()), LiftId, 0);

        // ±X faces use (z, y), ±Y faces (x, z) and ±Z faces (x, y), with the first coordinate negated on the negative
        // faces.
        for (var vertex = 0; vertex < slice.Parts.Count; vertex++)
        {
            var position = slice.Positions.Vertex(vertex);
            var normal = slice.Normals.Vertex(vertex);
            var (u, v) = normal switch
            {
                { X: > .5 } => (position.Z, position.Y),
                { X: < -.5 } => (-position.Z, position.Y),
                { Y: > .5 } => (position.X, position.Z),
                { Y: < -.5 } => (-position.X, position.Z),
                { Z: > .5 } => (position.X, position.Y),
                _ => (-position.X, position.Y)
            };
            Assert.Equal(u, slice.Uvs[2 * vertex], 9);
            Assert.Equal(v, slice.Uvs[2 * vertex + 1], 9);
        }
    }

    [Fact]
    public void AFaceUvScalesAndOffsetsItsCoordinates()
    {
        var deck = LoftBox("deck", (-.5, .5), (.9, 1), (-.5, .5));
        var scaled = deck with
        {
            Faces = [.. deck.Faces.Select(face => face.Uv is { Axis: Axis3.X, Positive: true } ? face with { Uv = face.Uv with { Scale = new(2, 3), Offset = new(.5, .25) } } : face)]
        };
        var device = Lift() with { Shape = Loft(Key(-.1, scaled), Key(.1, scaled)) };
        var slice = Slice(Scene(device), LiftId, 0);

        var onPositiveX = Enumerable.Range(0, slice.Parts.Count).Where(vertex => slice.Normals.Vertex(vertex).X > .5).ToArray();
        Assert.NotEmpty(onPositiveX);
        foreach (var vertex in onPositiveX)
        {
            var position = slice.Positions.Vertex(vertex);
            Assert.Equal(2 * position.Z + .5, slice.Uvs[2 * vertex], 9);
            Assert.Equal(3 * position.Y + .25, slice.Uvs[2 * vertex + 1], 9);
        }
    }

    [Fact]
    public void AHalfTurnKeepsTheKeysExactlyOnTheirSections()
    {
        var board = Board() with { Motion = new DeviceRotation(RotationPlane.XW, default, new(-Math.PI, 0), 0) };
        var world = Scene(board);

        world.Driven(BoardId, -Math.PI);

        // The board now lies mirrored, x from -.3 to 0, and its cap at W = .1 is the slice at that W.
        var cap = Slice(world, BoardId, .1);
        AssertRange(cap.Range(Axis3.X), -.3, 0);
        AssertRange(cap.Range(Axis3.Y), 1, 1.2);
        AssertRange(cap.Range(Axis3.Z), 0, .2);
    }

    // A slab with a key in its middle, which lies on section I. Its surface is 2 (.3 × .2 + .3 × .2 + .2 × .2).
    private static readonly ConvexPart Slab = LoftBox("slab", (0, .3), (0, .2), (0, .2));

    private static DeviceDefinition SlabWithMiddleKey(Pose4 pose, DeviceMotion motion) =>
        new(new("slab"), pose, TestSource, Loft(Key(-.1, Slab), Key(0, Slab), Key(.1, Slab)), motion);

    [Fact]
    public void AMiddleKeyOnASectionIsCutOnceWhenTheDeviceHasTurnedOver()
    {
        var world = Scene(SlabWithMiddleKey(At(0, 1, 0), new DeviceRotation(RotationPlane.XW, default, new(-Math.PI, 0), 0)));

        world.Driven(new("slab"), -Math.PI);

        var cut = Slice(world, new("slab"), 0);
        Assert.Equal(36, cut.Parts.Count);
        Assert.Equal(.32, cut.Area(), 9);
    }

    [Fact]
    public void AMiddleKeyOnASectionIsCutOnceWhenThePoseTurnsWIntoMinusW()
    {
        var world = Scene(SlabWithMiddleKey(At(0, 1, 0, rotation: XwHalfTurn), new DeviceTranslation(Axis3.Y, new(0, 1), 0)));

        var cut = Slice(world, new("slab"), 0);

        Assert.Equal(36, cut.Parts.Count);
        Assert.Equal(.32, cut.Area(), 9);
    }

    [Fact]
    public void ASectionThatOnlyTouchesADeviceCutsNothing()
    {
        var world = Scene(Board());
        world.Driven(BoardId, Math.PI / 4);

        // The board's lowest W is -.1 cos(π/4), reached by its whole inner face x' = 0, w' = -.1. A section there
        // touches that face and encloses no volume.
        var lowest = -.1 * Math.Cos(Math.PI / 4);
        Assert.Empty(world.SlicesAt(lowest));
        Assert.Single(world.SlicesAt(lowest + 1e-6));
    }

    [Fact]
    public void AllSlicesAreWoundOutwardWithUnitNormals()
    {
        var turned = Scene(Hanger(), Board());
        turned.Driven(HangerId, Math.PI / 4);
        turned.Driven(BoardId, Math.PI / 6);
        var slices = new[]
        {
            Scene(Hanger()).SliceOf(HangerId, II)!,
            Scene(Sash()).SliceOf(SashId, II)!,
            turned.SliceOf(HangerId, I)!,
            turned.SliceOf(HangerId, II)!,
            turned.SliceOf(BoardId, I)!,
            Slice(turned, BoardId, .1)
        };

        foreach (var slice in slices)
        {
            Assert.Equal(0, slice.Parts.Count % 3);
            for (var vertex = 0; vertex < slice.Parts.Count; vertex += 3)
            {
                Assert.True(slice.IsWoundOutward(vertex));
                var (a, b, c) = (slice.Positions.Vertex(vertex), slice.Positions.Vertex(vertex + 1), slice.Positions.Vertex(vertex + 2));
                Assert.True(b.Minus(a).Cross(c.Minus(a)).Length() > 1e-6, "A triangle of a slice has an area.");
            }
            for (var vertex = 0; vertex < slice.Parts.Count; vertex++) Assert.Equal(1, slice.Normals.Vertex(vertex).Length(), 9);
        }
    }

    [Fact]
    public void AnExtraKeyInTheMiddleOfAShapeDoesNotChangeItsSlice()
    {
        var slab = LoftBox("slab", (0, .3), (0, .2), (0, .2));
        DeviceDefinition BoardOf(params LoftKey[] keys) => new(
            new("board"), At(0, 1, 0), TestSource, Loft(keys), new DeviceRotation(RotationPlane.XW, default, new(0, 1), 0));
        var whole = Scene(BoardOf(Key(-.1, slab), Key(.1, slab)));
        var split = Scene(BoardOf(Key(-.1, slab), Key(0, slab), Key(.1, slab)));
        whole.Driven(new("board"), Math.PI / 6);
        split.Driven(new("board"), Math.PI / 6);

        // At W = .1 the turned board crosses its middle key, which a lone piece does not have.
        var expected = Slice(whole, new("board"), .1);
        var actual = Slice(split, new("board"), .1);
        Assert.Equal(expected.Area(), actual.Area(), 9);
    }
}
