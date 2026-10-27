using FourD;
namespace FourD.Tests;

public class WorldGeometryTests
{
    private static readonly Source Source = new("/room", "shape");
    private static readonly Rotation3 R3 = new(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
    private static readonly Rotation4 R4 = new(new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1));
    private static readonly Pose4 Pose = new(new(0, 0, 0, 0), R4);
    private static FaceUv[] Uvs() => Enum.GetValues<Axis3>().SelectMany(axis => new[] { false, true }.Select(p => new FaceUv(axis, p, new(1, 1), new(0, 0)))).ToArray();
    private static BoxAssembly Box(string id = "body") => new([new(new(id), new(0, 0, 0), new(1, 2, 3), R3, Uvs())]);
    private static BuildingElementDefinition Wall(string id = "wall", Interval? range = null, Interval[]? gaps = null) => new(new(id), Pose, Source, new("building"), range ?? new(-1, 1), gaps ?? [], Box());
    private static WorldDescription Description(params EntityDefinition[] entities) => new([new(new("I"), 0, Source), new(new("II"), 0.42, Source)], [new(new("building"), new(-2, 2), Source)], entities, [], [], []);
    private static World Load(WorldDescription description) => Assert.IsType<LoadResult.Loaded>(World.Load(description)).World;
    private static SectionGeometryResult.Geometry Geometry(World world, double w) => Assert.IsType<SectionGeometryResult.Geometry>(world.GetGeometryAtW(w));

    [Fact]
    public void BuildingSlicesAtBothClosedRangeEndsButNotOutside()
    {
        var world = Load(Description(Wall()));
        foreach (var w in new[] { -1d, 0, 1 })
        {
            var slice = Assert.Single(Geometry(world, w).Entities);
            Assert.Equal(108, slice.Positions.Count);
            Assert.Equal(-1, slice.Positions.Where((_, i) => i % 3 == 0).Min());
            Assert.Equal(1, slice.Positions.Where((_, i) => i % 3 == 0).Max());
            Assert.True(slice.ThroughObject);
        }
        Assert.Empty(Geometry(world, -1.000001).Entities);
        Assert.Empty(Geometry(world, 1.000001).Entities);
        Assert.Equal(0.42, Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(new("II"))).W);
    }

    [Fact]
    public void GapsKeepTheirClosedEndsAndRemoveOnlyPositiveRemainders()
    {
        var world = Load(Description(Wall(gaps: [new(.3, .6)])));
        Assert.Empty(Geometry(world, .42).Entities);
        Assert.Single(Geometry(world, .3).Entities);
        Assert.Single(Geometry(world, .6).Entities);
        foreach (var w in new[] { -1d, 0, 1 }) Assert.Empty(Geometry(Load(Description(Wall(gaps: [new(-1, 1)]))), w).Entities);
        var edge = Load(Description(Wall(gaps: [new(-1, .6)])));
        Assert.Empty(Geometry(edge, -1).Entities);
        Assert.Single(Geometry(edge, .6).Entities);
    }

    [Fact]
    public void AdjacentShapeRangesHaveIndependentGeometryAndIdentity()
    {
        var first = Wall("a", new(-1, .2));
        var second = Wall("b", new(.2, 1)) with { Shape = new([Box().Parts[0] with { HalfSize = new(2, 2, 3) }]) };
        var world = Load(Description(first, second));
        Assert.Equal(new EntityId("a"), Assert.Single(Geometry(world, 0).Entities).Entity);
        Assert.Equal(2, Assert.Single(Geometry(world, .42).Entities).Positions.Where((_, i) => i % 3 == 0).Max());
        Assert.Equal(2, Geometry(world, .2).Entities.Count);
    }

    [Fact]
    public void FixedGeometryHasExactlyTwelveMillimetersOfClosedWThickness()
    {
        var fixedObject = new FixedDefinition(new("cabinet"), Pose with { Position = new(0, 0, 0, .42) }, Source, new("II"), Box());
        var world = Load(Description(fixedObject));
        foreach (var w in new[] { .414, .42, .426 }) Assert.False(Assert.Single(Geometry(world, w).Entities).ThroughObject);
        Assert.Empty(Geometry(world, .413999).Entities);
        Assert.Empty(Geometry(world, .426001).Entities);
        Assert.Empty(Geometry(world, 0).Entities);
    }

    [Fact]
    public void WorldOwnsNestedInputAndOutputCollections()
    {
        var faces = Uvs();
        var parts = new[] { Box().Parts[0] with { FaceUvs = faces } };
        var gaps = new[] { new Interval(.3, .6) };
        var entities = new EntityDefinition[] { Wall() with { Shape = new(parts), Gaps = gaps } };
        var sections = new[] { new SectionDefinition(new("I"), 0, Source) };
        var world = Load(new(sections, [new(new("building"), new(-2, 2), Source)], entities, [], [], []));
        var before = Assert.Single(Geometry(world, 0).Entities);
        faces[0] = faces[0] with { Offset = new(100, 100) };
        parts[0] = parts[0] with { HalfSize = new(9, 9, 9) };
        gaps[0] = new(-1, 1);
        entities[0] = Wall("replacement");
        sections[0] = new(new("I"), .42, Source);
        var after = Assert.Single(Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(new("I"))).Entities);
        Assert.Equal(before, after with { Positions = before.Positions, Normals = before.Normals, Uvs = before.Uvs, Parts = before.Parts });
        Assert.Equal(before.Positions, after.Positions);
        Assert.Equal(before.Uvs, after.Uvs);
        Assert.Empty(Geometry(world, .42).Entities);
        Assert.NotSame(before.Positions, after.Positions);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)before.Positions)[0] = 77);
    }

    [Fact]
    public void MeshUsesOutwardUnitFlatNormalsAndFaceCoordinatesForUvs()
    {
        var shape = Box() with { Parts = [Box().Parts[0] with { FaceUvs = Uvs().Select(f => f with { Scale = new(2, 3), Offset = new(.25, .5) }).ToArray() }] };
        var slice = Assert.Single(Geometry(Load(Description(Wall() with { Shape = shape })), 0).Entities);
        var n = slice.Parts.Count;
        Assert.Equal(0, n % 3);
        Assert.Equal(3 * n, slice.Positions.Count);
        Assert.Equal(3 * n, slice.Normals.Count);
        Assert.Equal(2 * n, slice.Uvs.Count);
        for (var i = 0; i < n; i += 3)
        {
            var a = At(slice.Positions, i); var b = At(slice.Positions, i + 1); var c = At(slice.Positions, i + 2);
            var normal = At(slice.Normals, i);
            Assert.Equal(1, Dot(normal, normal), 10);
            Assert.True(Dot(Cross(Sub(b, a), Sub(c, a)), normal) > 0);
            Assert.True(Dot(a, normal) > 0);
            Assert.Equal(normal, At(slice.Normals, i + 1)); Assert.Equal(normal, At(slice.Normals, i + 2));
            Assert.Equal(slice.Parts[i], slice.Parts[i + 1]); Assert.Equal(slice.Parts[i], slice.Parts[i + 2]);
            var u = normal.X != 0 ? a.Z : a.X;
            var v = normal.Y != 0 ? a.Z : a.Y;
            if (normal.X + normal.Y + normal.Z < 0) u = -u;
            Assert.Equal(u * 2 + .25, slice.Uvs[i * 2], 10);
            Assert.Equal(v * 3 + .5, slice.Uvs[i * 2 + 1], 10);
        }
    }
    private static Vec3 At(IReadOnlyList<double> values, int i) => new(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
    private static Vec3 Sub(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    private static double Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private static Vec3 Cross(Vec3 a, Vec3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    [Fact]
    public void EntityAndPartRotationsComposeBeforeTranslationAndSortByStableIds()
    {
        var part = Box("z").Parts[0] with { Center = new(1, 0, 0), HalfSize = new(1, 2, 3), Rotation = new(new(0, 1, 0), new(-1, 0, 0), new(0, 0, 1)) };
        var pose = Pose with { Position = new(10, 20, 30, 0), Rotation = new(new(0, 0, -1, 0), new(0, 1, 0, 0), new(1, 0, 0, 0), new(0, 0, 0, 1)) };
        var world = Load(Description(Wall("z"), Wall("a") with { Pose = pose, Shape = new([part, part with { Id = new("a"), Center = new(2, 0, 0) }]) }));
        var output = Geometry(world, 0).Entities;
        Assert.Equal(new[] { "a", "z" }, output.Select(s => s.Entity.Value));
        var slice = output[0];
        Assert.Equal(36, slice.Parts.Count(p => p.Value == "a"));
        Assert.All(slice.Parts.Take(36), p => Assert.Equal("a", p.Value));
        var z = Enumerable.Range(36, 36).Select(i => At(slice.Positions, i)).ToArray();
        Assert.Equal(7, z.Min(p => p.X)); Assert.Equal(13, z.Max(p => p.X));
        Assert.Equal(19, z.Min(p => p.Y)); Assert.Equal(21, z.Max(p => p.Y));
        Assert.Equal(27, z.Min(p => p.Z)); Assert.Equal(31, z.Max(p => p.Z));
        for (var i = 36; i < 72; i += 3)
        {
            var a = At(slice.Positions, i); var b = At(slice.Positions, i + 1); var c = At(slice.Positions, i + 2);
            var normal = At(slice.Normals, i);
            Assert.True(Dot(Cross(Sub(b, a), Sub(c, a)), normal) > 0);
            Assert.True(Dot(Sub(a, new(10, 20, 29)), normal) > 0);
        }
    }

    [Fact]
    public void InvalidReadQueriesReturnExplicitFailuresWithoutChangingSnapshots()
    {
        var world = Load(Description(Wall()));
        Assert.Equal(FailureCode.UnknownId, Assert.IsType<SectionGeometryResult.Rejected>(world.GetSectionGeometry(new("missing"))).Failure.Code);
        foreach (var w in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            Assert.Equal(FailureCode.InvalidInput, Assert.IsType<SectionGeometryResult.Rejected>(world.GetGeometryAtW(w)).Failure.Code);
        Assert.Single(Geometry(world, 0).Entities);
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
        var rotation = new Rotation4(new(-1, 0, 0, 0), R4.Y, R4.Z, new(0, 0, 0, -1));
        var fixedObject = new FixedDefinition(new("fixed"), new(default, rotation), Source, new("I"), Box());
        var slice = Assert.Single(Geometry(Load(Description(fixedObject)), 0).Entities);
        for (var i = 0; i < slice.Parts.Count; i += 3)
        {
            var a = At(slice.Positions, i); var b = At(slice.Positions, i + 1); var c = At(slice.Positions, i + 2);
            Assert.True(Dot(Cross(Sub(b, a), Sub(c, a)), At(slice.Normals, i)) > 0);
        }
        var positiveZ = Enumerable.Range(0, slice.Parts.Count).Where(i => At(slice.Normals, i) == new Vec3(0, 0, 1)).ToArray();
        Assert.All(positiveZ, i => Assert.Equal(-slice.Positions[i * 3], slice.Uvs[i * 2]));
    }
    [Fact]
    public void ThroughObjectMeasuresFullWorldWExtentRatherThanVisibleSectionCount()
    {
        var thin = Wall("thin", new(-.0055, .0055));
        var ordinary = Wall("ordinary", new(-.006, .006));
        var through = Wall("through", new(-.0061, .0061));
        var slices = Geometry(Load(Description(thin, ordinary, through)), 0).Entities;
        Assert.False(slices.Single(s => s.Entity.Value == "thin").ThroughObject);
        Assert.False(slices.Single(s => s.Entity.Value == "ordinary").ThroughObject);
        Assert.True(slices.Single(s => s.Entity.Value == "through").ThroughObject);
    }
    [Fact]
    public void StableIdsAreCaseSensitiveAndUseOrdinalOrdering()
    {
        var world = Load(Description(Wall("ä"), Wall("z"), Wall("a"), Wall("A")));
        Assert.Equal(new[] { "A", "a", "z", "ä" }, Geometry(world, 0).Entities.Select(s => s.Entity.Value));
    }
    [Fact]
    public void TouchingGapsDoNotLeaveAnIsolatedSliceAtTheirSharedEndpoint()
    {
        var world = Load(Description(Wall(gaps: [new(-.6, 0), new(0, .6)])));
        foreach (var w in new[] { -.3, 0, .3 }) Assert.Empty(Geometry(world, w).Entities);
        Assert.Single(Geometry(world, -.6).Entities);
        Assert.Single(Geometry(world, .6).Entities);
    }
}
