using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public class TurnableTests
{
    // A 1 m rod in section I, hinged 2 cm inside its left end at the bottom face.
    private static TurnableDefinition Rod() => new(
        new("object"), Origin, TestSource, new("I"), new("b"), Box(new(.5, .1, .1)),
        new(Holdable: true, Packable: false, HorizontalRadius: .5, LongAxis: Axis3.X, Hinge: new(-.48, -.1, 0), Reach: .98));

    // Sections I (W = 0) and II (W = .42) and a building "b" for W in [-2, 2], without any entity.
    private static World EmptyWorld() => Load(new(
        [new(new("I"), 0, TestSource), new(new("II"), .42, TestSource)],
        [new(new("b"), new(-2, 2), TestSource)],
        [], [], [], []));

    private static World WorldWithRod(TurnableDefinition rod)
    {
        var world = EmptyWorld();
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(rod));
        return world;
    }

    [Fact]
    public void AddedObjectIsImmediatelyReadableAndVisible()
    {
        var world = EmptyWorld();
        var rod = Rod();

        var added = Assert.IsType<AddEntityResult.Added>(world.AddEntity(rod));

        Assert.Equal(rod.Id, added.Entity.Id);
        Assert.Equal(rod.Pose, world.State(rod.Id).Pose);
        Assert.Single(world.SlicesIn(new("I")));
    }

    [Fact]
    public void PlacementTiltsFourDimensionalBoxAndPreservesOrigin()
    {
        var rod = Rod();
        var world = WorldWithRod(rod);

        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(rod.Id, At(0, 0, 0, w: .3, rotation: XwQuarterTurn)));

        var state = world.State(rod.Id);
        Assert.Null(state.Section);
        Assert.Equal(new SectionId("I"), state.UprightOrigin);
        var slice = Assert.Single(world.SlicesIn(new("I")));
        Assert.Equal(-Epsilon, slice.Coordinates(Axis3.X).Min(), 10);
        Assert.Equal(Epsilon, slice.Coordinates(Axis3.X).Max(), 10);
        Assert.True(world.HasSliceVolume(rod.Id, new("I")));
    }

    [Fact]
    public void ObliqueCutHasHandCalculatedBoundsAndOutwardTriangles()
    {
        var rod = Rod();
        var world = WorldWithRod(rod);
        var c = Math.Sqrt(.5);
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(rod.Id, At(0, 0, 0, w: .21, rotation: XwRotation(c, c))));

        var slice = Assert.Single(world.SlicesIn(new("I")));

        Assert.Equal(-.21 - Epsilon * Math.Sqrt(2), slice.Coordinates(Axis3.X).Min(), 10);
        Assert.Equal(-.21 + Epsilon * Math.Sqrt(2), slice.Coordinates(Axis3.X).Max(), 10);
        Assert.Equal(-.1, slice.Coordinates(Axis3.Y).Min(), 10);
        Assert.Equal(.1, slice.Coordinates(Axis3.Z).Max(), 10);
        for (var i = 0; i < slice.Parts.Count; i += 3)
        {
            Assert.True(slice.IsWoundOutward(i));
        }
        var farEnd = .21 + c * (.5 + Epsilon);
        Assert.Empty(world.SlicesAt(farEnd + 1e-8));
    }

    [Fact]
    public void RemoveRetiresIdAndDeliversEventsOnce()
    {
        var rod = Rod();
        var world = WorldWithRod(rod);

        Assert.IsType<RemoveEntityResult.Removed>(world.RemoveEntity(rod.Id));

        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(rod.Id));
        Assert.Empty(world.SlicesIn(new("I")));
        Assert.Equal(FailureCode.DuplicateId, Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(rod)).Failure.Code);
        Assert.Equal(FailureCode.InvalidInput, Assert.IsType<StepResult.Rejected>(world.Step(0)).Failure.Code);
        var events = world.StepEvents(.1);
        Assert.Equal(2, events.Count);
        Assert.IsType<EntityAdded>(events[0].Event);
        Assert.IsType<EntityRemoved>(events[1].Event);
        Assert.True(events[0].Sequence < events[1].Sequence);
        Assert.Equal(0, events[0].Time);
        Assert.Empty(world.StepEvents(.1));
    }

    [Fact]
    public void SourceSectionCannotChangeByFlatPlacementAndFailureIsAtomic()
    {
        var rod = Rod();
        var world = WorldWithRod(rod);

        var rejected = Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(rod.Id, At(0, 0, 0, w: .42)));

        Assert.Equal(FailureCode.WrongSection, rejected.Failure.Code);
        Assert.Equal(rod.Pose, world.State(rod.Id).Pose);
    }

    [Fact]
    public void ObliqueSideAndEndCapUseTheirDocumentedLocalUv()
    {
        var plain = Rod();
        var scaledFaces = plain.Shape.Parts[0].FaceUvs.Select(f => f with { Scale = new(2, 3), Offset = new(.25, .5) }).ToArray();
        var rod = plain with { Shape = Shape(plain.Shape.Parts[0] with { FaceUvs = scaledFaces }) };
        var world = WorldWithRod(rod);
        var c = Math.Sqrt(.5);
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(rod.Id, At(0, 0, 0, w: .21, rotation: XwRotation(c, c))));
        var w = .21 + .5 * c;

        var slice = Assert.Single(world.SlicesAt(w));

        var sideVertices = Enumerable.Range(0, slice.Parts.Count).Where(i => slice.Normals.Vertex(i).X > .9).ToArray();
        var capVertices = Enumerable.Range(0, slice.Parts.Count).Where(i => slice.Normals.Vertex(i).X < -.9).ToArray();
        Assert.NotEmpty(sideVertices);
        Assert.NotEmpty(capVertices);
        Assert.All(sideVertices, i =>
        {
            var p = slice.Positions.Vertex(i);
            Assert.Equal(p.Z * 2 + .25, slice.Uvs[i * 2], 9);
            Assert.Equal(p.Y * 3 + .5, slice.Uvs[i * 2 + 1], 9);
        });
        Assert.All(capVertices, i =>
        {
            var p = slice.Positions.Vertex(i);
            Assert.Equal((p.X + w - .21) / (2 * c), slice.Uvs[i * 2], 9);
            Assert.Equal(p.Z, slice.Uvs[i * 2 + 1], 9);
        });
    }

    [Fact]
    public void UprightEndTooCloseToSectionIsRejected()
    {
        var rod = Rod();
        var world = WorldWithRod(rod);

        var tooCloseToSection = At(0, 0, 0, w: -.08, rotation: XwQuarterTurn);

        var rejected = Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(rod.Id, tooCloseToSection));

        Assert.Equal(FailureCode.InvalidInput, rejected.Failure.Code);
        Assert.Equal(rod.Pose, world.State(rod.Id).Pose);
    }

    [Fact]
    public void PlacementRejectsUnrepresentableGeometryWithoutMutatingState()
    {
        var rod = Rod();
        var world = WorldWithRod(rod);

        var rejected = Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(rod.Id, At(1e30, 0, 0)));

        Assert.Equal(FailureCode.InvalidInput, rejected.Failure.Code);
        Assert.Equal(rod.Pose, world.State(rod.Id).Pose);
    }

    [Fact]
    public void LoadRejectsTurnableWhoseWThicknessCollapsesAtCoordinatePrecision()
    {
        var rod = Rod() with { Pose = At(0, 0, 0, 1e20) };

        var errors = LoadErrors(new(
            [new(new("I"), 1e20, TestSource)], [new(new("b"), new(-1e21, 1e21), TestSource)], [rod], [], [], []));

        Assert.Contains(errors, e => e.Code == LoadErrorCode.InvalidGeometry && e.Source.Property.EndsWith("Pose.Position.W"));
    }

    [Fact]
    public void ReadFixedEntityReportsHandednessFromItsPose()
    {
        var mirrored = new FixedDefinition(new("fixed"), new(default, XwHalfTurn), TestSource, new("I"), Rod().Shape);
        var world = Load(new([new(new("I"), 0, TestSource)], [], [mirrored], [], [], []));

        Assert.Equal(Handedness.Mirrored, world.State(mirrored.Id).Handedness);
    }
}
