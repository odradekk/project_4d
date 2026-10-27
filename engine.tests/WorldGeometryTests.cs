using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public class WorldGeometryTests
{
    // A box of 2 x 4 x 6 m centred on the part's origin.
    private static BoxAssembly Block(string id = "body") => Shape(Part(id, new(1, 2, 3)));

    private static BuildingElementDefinition Wall(string id = "wall", Interval? range = null, Interval[]? gaps = null) =>
        new(new(id), Origin, TestSource, new("building"), range ?? new Interval(-1, 1), gaps ?? [], Block());

    private static WorldDescription Description(params EntityDefinition[] entities) => new(
        [new(new("I"), 0, TestSource), new(new("II"), 0.42, TestSource)],
        [new(new("building"), new(-2, 2), TestSource)],
        entities, [], [], []);

    [Fact]
    public void BuildingSlicesAtBothClosedRangeEndsButNotOutside()
    {
        var world = Load(Description(Wall()));

        foreach (var w in new[] { -1d, 0, 1 })
        {
            var slice = Assert.Single(world.SlicesAt(w));
            Assert.Equal(108, slice.Positions.Count);
            Assert.Equal(-1, slice.Coordinates(0).Min());
            Assert.Equal(1, slice.Coordinates(0).Max());
            Assert.True(slice.ThroughObject);
        }
        Assert.Empty(world.SlicesAt(-1.000001));
        Assert.Empty(world.SlicesAt(1.000001));
        Assert.Equal(0.42, Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(new("II"))).W);
    }

    [Fact]
    public void GapsKeepTheirClosedEndsAndRemoveOnlyPositiveRemainders()
    {
        var world = Load(Description(Wall(gaps: [new(.3, .6)])));
        Assert.Empty(world.SlicesAt(.42));
        Assert.Single(world.SlicesAt(.3));
        Assert.Single(world.SlicesAt(.6));

        var fullyGapped = Load(Description(Wall(gaps: [new(-1, 1)])));
        foreach (var w in new[] { -1d, 0, 1 })
        {
            Assert.Empty(fullyGapped.SlicesAt(w));
        }

        var gapToTheEnd = Load(Description(Wall(gaps: [new(-1, .6)])));
        Assert.Empty(gapToTheEnd.SlicesAt(-1));
        Assert.Single(gapToTheEnd.SlicesAt(.6));
    }

    [Fact]
    public void AdjacentShapeRangesHaveIndependentGeometryAndIdentity()
    {
        var first = Wall("a", new(-1, .2));
        var second = Wall("b", new(.2, 1)) with { Shape = Shape(Part("body", new(2, 2, 3))) };
        var world = Load(Description(first, second));

        Assert.Equal(new EntityId("a"), Assert.Single(world.SlicesAt(0)).Entity);
        Assert.Equal(2, Assert.Single(world.SlicesAt(.42)).Coordinates(0).Max());
        Assert.Equal(2, world.SlicesAt(.2).Count);
    }

    [Fact]
    public void FixedGeometryHasExactlyTwelveMillimetersOfClosedWThickness()
    {
        var cabinet = new FixedDefinition(new("cabinet"), At(0, 0, 0, .42), TestSource, new("II"), Block());
        var world = Load(Description(cabinet));

        foreach (var w in new[] { .414, .42, .426 })
        {
            Assert.False(Assert.Single(world.SlicesAt(w)).ThroughObject);
        }
        Assert.Empty(world.SlicesAt(.413999));
        Assert.Empty(world.SlicesAt(.426001));
        Assert.Empty(world.SlicesAt(0));
    }

    [Fact]
    public void WorldOwnsNestedInputAndOutputCollections()
    {
        var faces = FaceUvs();
        var parts = new[] { Part("body", new(1, 2, 3)) with { FaceUvs = faces } };
        var gaps = new[] { new Interval(.3, .6) };
        var entities = new EntityDefinition[] { Wall() with { Shape = new(parts), Gaps = gaps } };
        var sections = new[] { new SectionDefinition(new("I"), 0, TestSource) };
        var world = Load(new(sections, [new(new("building"), new(-2, 2), TestSource)], entities, [], [], []));
        var before = Assert.Single(world.SlicesAt(0));

        faces[0] = faces[0] with { Offset = new(100, 100) };
        parts[0] = parts[0] with { HalfSize = new(9, 9, 9) };
        gaps[0] = new(-1, 1);
        entities[0] = Wall("replacement");
        sections[0] = new(new("I"), .42, TestSource);

        var after = Assert.Single(world.SlicesIn(new("I")));
        // Lists compare by reference, so swap the original lists in and compare the remaining fields.
        Assert.Equal(before, after with
        {
            Positions = before.Positions,
            Normals = before.Normals,
            Uvs = before.Uvs,
            Parts = before.Parts,
        });
        Assert.Equal(before.Positions, after.Positions);
        Assert.Equal(before.Uvs, after.Uvs);
        Assert.Empty(world.SlicesAt(.42));
        Assert.NotSame(before.Positions, after.Positions);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)before.Positions)[0] = 77);
    }

    [Fact]
    public void MeshUsesOutwardUnitFlatNormalsAndFaceCoordinatesForUvs()
    {
        var scaledFaces = FaceUvs().Select(f => f with { Scale = new(2, 3), Offset = new(.25, .5) }).ToArray();
        var shape = Shape(Part("body", new(1, 2, 3)) with { FaceUvs = scaledFaces });
        var slice = Assert.Single(Load(Description(Wall() with { Shape = shape })).SlicesAt(0));

        var vertexCount = slice.Parts.Count;
        Assert.Equal(0, vertexCount % 3);
        Assert.Equal(3 * vertexCount, slice.Positions.Count);
        Assert.Equal(3 * vertexCount, slice.Normals.Count);
        Assert.Equal(2 * vertexCount, slice.Uvs.Count);
        for (var i = 0; i < vertexCount; i += 3)
        {
            var corner = slice.Positions.Vertex(i);
            var normal = slice.Normals.Vertex(i);
            Assert.Equal(1, normal.Dot(normal), 10);
            Assert.True(slice.IsWoundOutward(i));
            Assert.True(corner.Dot(normal) > 0);
            Assert.Equal(normal, slice.Normals.Vertex(i + 1));
            Assert.Equal(normal, slice.Normals.Vertex(i + 2));
            Assert.Equal(slice.Parts[i], slice.Parts[i + 1]);
            Assert.Equal(slice.Parts[i], slice.Parts[i + 2]);

            // Face coordinates: +-X faces use (z, y), +-Y faces (x, z) and +-Z faces (x, y); a negative face negates the first.
            var u = normal.X != 0 ? corner.Z : corner.X;
            var v = normal.Y != 0 ? corner.Z : corner.Y;
            if (normal.X + normal.Y + normal.Z < 0)
            {
                u = -u;
            }
            Assert.Equal(u * 2 + .25, slice.Uvs[i * 2], 10);
            Assert.Equal(v * 3 + .5, slice.Uvs[i * 2 + 1], 10);
        }
    }

    [Fact]
    public void EntityAndPartRotationsComposeBeforeTranslationAndSortByStableIds()
    {
        var part = Part("z", new(1, 2, 3), center: new(1, 0, 0), rotation: new(new(0, 1, 0), new(-1, 0, 0), new(0, 0, 1)));
        var entityRotation = new Rotation4(new(0, 0, -1, 0), new(0, 1, 0, 0), new(1, 0, 0, 0), new(0, 0, 0, 1));
        var composite = Wall("a") with
        {
            Pose = new(new(10, 20, 30, 0), entityRotation),
            Shape = Shape(part, part with { Id = new("a"), Center = new(2, 0, 0) }),
        };
        var world = Load(Description(Wall("z"), composite));

        var slices = world.SlicesAt(0);

        Assert.Equal(new[] { "a", "z" }, slices.Select(s => s.Entity.Value));
        var slice = slices[0];
        Assert.Equal(36, slice.Parts.Count(p => p.Value == "a"));
        Assert.All(slice.Parts.Take(36), p => Assert.Equal("a", p.Value));
        // Part "z" ends up centred at (10, 20, 29) and spans 6 x 2 x 4 m along X, Y and Z.
        var partZ = Enumerable.Range(36, 36).Select(i => slice.Positions.Vertex(i)).ToArray();
        Assert.Equal(7, partZ.Min(p => p.X));
        Assert.Equal(13, partZ.Max(p => p.X));
        Assert.Equal(19, partZ.Min(p => p.Y));
        Assert.Equal(21, partZ.Max(p => p.Y));
        Assert.Equal(27, partZ.Min(p => p.Z));
        Assert.Equal(31, partZ.Max(p => p.Z));
        for (var i = 36; i < 72; i += 3)
        {
            Assert.True(slice.IsWoundOutward(i));
            Assert.True(slice.Positions.Vertex(i).Minus(new(10, 20, 29)).Dot(slice.Normals.Vertex(i)) > 0);
        }
    }

    [Fact]
    public void InvalidReadQueriesReturnExplicitFailuresWithoutChangingSnapshots()
    {
        var world = Load(Description(Wall()));

        var unknown = Assert.IsType<SectionGeometryResult.Rejected>(world.GetSectionGeometry(new("missing")));
        Assert.Equal(FailureCode.UnknownId, unknown.Failure.Code);
        foreach (var w in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var rejected = Assert.IsType<SectionGeometryResult.Rejected>(world.GetGeometryAtW(w));
            Assert.Equal(FailureCode.InvalidInput, rejected.Failure.Code);
        }
        Assert.Single(world.SlicesAt(0));
    }

    [Fact]
    public void DistanceIncludesWAndAvoidsIntermediateSquaredOverflow()
    {
        var world = Load(Description());

        Assert.Equal(5, world.Distance4(new(0, 0, 0, 0), new(3, 0, 0, 4)));
        Assert.Equal(.42, world.Distance4(new(1, 2, 3, 0), new(1, 2, 3, .42)));
        Assert.Equal(0, world.Distance4(new(1, 2, 3, 4), new(1, 2, 3, 4)));
        Assert.Equal(13, world.Distance4(new(0, 0, 0, 0), new(3, 4, 0, 12)));
        Assert.InRange(world.Distance4(new(0, 0, 0, 0), new(3e200, 0, 0, 4e200)) / 5e200, 1 - 1e-14, 1 + 1e-14);
        Assert.Equal(5e-200, world.Distance4(new(0, 0, 0, 0), new(3e-200, 0, 0, 4e-200)));
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Distance4(new(double.NaN, 0, 0, 0), default));
    }

    [Fact]
    public void WParallelHalfTurnKeepsOutwardWindingAndMirrorsLocalTexture()
    {
        var fixedObject = new FixedDefinition(new("fixed"), new(default, XwHalfTurn), TestSource, new("I"), Block());
        var slice = Assert.Single(Load(Description(fixedObject)).SlicesAt(0));

        for (var i = 0; i < slice.Parts.Count; i += 3)
        {
            Assert.True(slice.IsWoundOutward(i));
        }
        var positiveZ = Enumerable.Range(0, slice.Parts.Count).Where(i => slice.Normals.Vertex(i) == new Vec3(0, 0, 1)).ToArray();
        Assert.All(positiveZ, i => Assert.Equal(-slice.Positions[i * 3], slice.Uvs[i * 2]));
    }

    [Fact]
    public void ThroughObjectMeasuresFullWorldWExtentRatherThanVisibleSectionCount()
    {
        var thin = Wall("thin", new(-.0055, .0055));
        var ordinary = Wall("ordinary", new(-.006, .006));
        var through = Wall("through", new(-.0061, .0061));

        var slices = Load(Description(thin, ordinary, through)).SlicesAt(0);

        Assert.False(slices.Single(s => s.Entity.Value == "thin").ThroughObject);
        Assert.False(slices.Single(s => s.Entity.Value == "ordinary").ThroughObject);
        Assert.True(slices.Single(s => s.Entity.Value == "through").ThroughObject);
    }

    [Fact]
    public void StableIdsAreCaseSensitiveAndUseOrdinalOrdering()
    {
        var world = Load(Description(Wall("ä"), Wall("z"), Wall("a"), Wall("A")));

        Assert.Equal(new[] { "A", "a", "z", "ä" }, world.SlicesAt(0).Select(s => s.Entity.Value));
    }

    [Fact]
    public void TouchingGapsDoNotLeaveAnIsolatedSliceAtTheirSharedEndpoint()
    {
        var world = Load(Description(Wall(gaps: [new(-.6, 0), new(0, .6)])));

        foreach (var w in new[] { -.3, 0, .3 })
        {
            Assert.Empty(world.SlicesAt(w));
        }
        Assert.Single(world.SlicesAt(-.6));
        Assert.Single(world.SlicesAt(.6));
    }
}
