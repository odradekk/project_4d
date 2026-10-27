using FourD;
namespace FourD.Tests;

public class WorldValidationTests
{
    private static readonly Source Source = new("/room", "value");
    private static readonly Rotation3 R3 = new(new(1, 0, 0), new(0, 1, 0), new(0, 0, 1));
    private static readonly Rotation4 R4 = new(new(1, 0, 0, 0), new(0, 1, 0, 0), new(0, 0, 1, 0), new(0, 0, 0, 1));
    private static FaceUv[] Uvs() => Enum.GetValues<Axis3>().SelectMany(a => new[] { false, true }.Select(p => new FaceUv(a, p, new(1, 1), new(0, 0)))).ToArray();
    private static BoxPart Part() => new(new("body"), default, new(1, 1, 1), R3, Uvs());
    private static BuildingElementDefinition Wall() => new(new("wall"), new(default, R4), Source, new("b"), new(-1, 1), [], new([Part()]));
    private static WorldDescription Description(EntityDefinition? entity = null) => new([new(new("I"), 0, Source)], [new(new("b"), new(-2, 2), Source)], entity is null ? [] : [entity], [], [], []);
    public static IEnumerable<object[]> InvalidDescriptions()
    {
        var d = Description(Wall()); var wall = Wall();
        yield return [d with { Sections = [] }, LoadErrorCode.InvalidSectionOrder];
        yield return [d with { Sections = [new(new("I"), double.NaN, Source)] }, LoadErrorCode.InvalidNumber];
        yield return [d with { Sections = [new(new("I"), 0, Source), new(new("II"), 0, Source)] }, LoadErrorCode.InvalidSectionOrder];
        yield return [d with { Sections = [new(new("II"), 1, Source), new(new("I"), 0, Source)] }, LoadErrorCode.InvalidSectionOrder];
        yield return [d with { Sections = [new(new("I"), 0, Source), new(new("I"), 1, Source)] }, LoadErrorCode.DuplicateId];
        yield return [d with { Sections = [new(new(""), 0, Source)] }, LoadErrorCode.InvalidId];
        yield return [d with { Buildings = [new(new("b"), new(2, -2), Source)] }, LoadErrorCode.InvalidInterval];
        yield return [d with { Entities = [wall, wall] }, LoadErrorCode.DuplicateId];
        yield return [Description(wall with { Id = new("") }), LoadErrorCode.InvalidId];
        yield return [Description(wall with { Building = new("missing") }), LoadErrorCode.MissingReference];
        yield return [Description(wall with { WRange = new(-3, 1) }), LoadErrorCode.BuildingCoverage];
        yield return [Description(wall with { WRange = new(0, 0) }), LoadErrorCode.InvalidInterval];
        yield return [Description(wall with { WRange = new(double.NegativeInfinity, 1) }), LoadErrorCode.InvalidNumber];
        yield return [Description(wall with { Gaps = [new(-2, 0)] }), LoadErrorCode.InvalidInterval];
        yield return [Description(wall with { Gaps = [new(0, .5), new(.3, .6)] }), LoadErrorCode.InvalidInterval];
        yield return [Description(wall with { Gaps = [new(.3, .6), new(-.6, -.3)] }), LoadErrorCode.InvalidInterval];
        yield return [Description(wall with { Shape = new([]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([Part(), Part()]) }), LoadErrorCode.DuplicateId];
        yield return [Description(wall with { Shape = new([Part() with { HalfSize = new(0, 1, 1) }]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([Part() with { HalfSize = new(.005, 1, 1) }]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { WRange = new(0, .01) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Gaps = [new(-.995, 1)] }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([Part() with { Center = new(double.NaN, 0, 0) }]) }), LoadErrorCode.InvalidNumber];
        yield return [Description(wall with { Shape = new([Part() with { Rotation = new(new(-1, 0, 0), R3.Y, R3.Z) }]) }), LoadErrorCode.InvalidPose];
        yield return [Description(wall with { Shape = new([Part() with { FaceUvs = Uvs()[..5] }]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([Part() with { FaceUvs = Enumerable.Repeat(Uvs()[0], 6).ToArray() }]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([Part() with { FaceUvs = Uvs().Select(f => f with { Axis = (Axis3)99 }).ToArray() }]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([Part() with { FaceUvs = Uvs().Select(f => f with { Scale = new(0, 1) }).ToArray() }]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Pose = new(new(0, 0, 0, 1), R4) }), LoadErrorCode.InvalidPose];
        yield return [Description(wall with { Pose = new(default, new(new(-1, 0, 0, 0), R4.Y, R4.Z, R4.W)) }), LoadErrorCode.InvalidPose];
        yield return [Description(wall with { Pose = new(default, new(new(0, 0, 0, 1), R4.Y, R4.Z, new(-1, 0, 0, 0))) }), LoadErrorCode.InvalidPose];
        yield return [Description(new FixedDefinition(new("fixed"), new(new(0, 0, 0, .1), R4), Source, new("I"), new([Part()]))), LoadErrorCode.InvalidPose];
        yield return [Description(new FixedDefinition(new("fixed"), new(default, R4), Source, new("missing"), new([Part()]))), LoadErrorCode.MissingReference];
        yield return [d with { Entities = [new UnknownDefinition(new("unknown"), new(default, R4), Source)] }, LoadErrorCode.UnsupportedDefinition];
        yield return [d with { SharedValues = [new(new("water"), new(0, 1), 0, Source)] }, LoadErrorCode.UnsupportedDefinition];
        yield return [d with { Seams = [new(new("seam"), default, new(1, 0, 0), [new("I")], false, true, false, Source)] }, LoadErrorCode.UnsupportedDefinition];
        yield return [d with { ExpectedOperations = [new(new("operation"), Source, new([], [], [], []), [])] }, LoadErrorCode.UnsupportedDefinition];
        yield return [Description(wall with { Pose = new(default, new(new(1.00000001, 0, 0, 0), R4.Y, R4.Z, R4.W)) }), LoadErrorCode.InvalidPose];
        yield return [Description(wall with { Shape = new([Part() with { Rotation = new(new(1, 0, 0), new(1e-8, 1, 0), R3.Z) }]) }), LoadErrorCode.InvalidPose];
        yield return [Description(wall with { Shape = new([Part() with { FaceUvs = Uvs().Select(f => f with { Offset = new(double.PositiveInfinity, 0) }).ToArray() }]) }), LoadErrorCode.InvalidNumber];
        yield return [Description(wall with { Shape = new([Part() with { Id = new("") }]) }), LoadErrorCode.InvalidId];
        yield return [Description(wall with { Pose = new(new(double.NaN, 0, 0, 0), R4) }), LoadErrorCode.InvalidNumber];
        yield return [Description(wall with { Pose = new(default, new(new(double.NaN, 0, 0, 0), R4.Y, R4.Z, R4.W)) }), LoadErrorCode.InvalidNumber];
        yield return [Description(wall with { Shape = new([Part() with { Rotation = new(new(double.NaN, 0, 0), R3.Y, R3.Z) }]) }), LoadErrorCode.InvalidNumber];
        yield return [Description(wall with { Shape = new([Part() with { HalfSize = new(double.PositiveInfinity, 1, 1) }]) }), LoadErrorCode.InvalidNumber];
        yield return [Description(wall with { Shape = new(null!) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([null!]) }), LoadErrorCode.InvalidGeometry];
        yield return [d with { Buildings = null! }, LoadErrorCode.InvalidGeometry];
        yield return [d with { Buildings = [null!] }, LoadErrorCode.InvalidGeometry];
        yield return [d with { Sections = [null!] }, LoadErrorCode.InvalidSectionOrder];
        yield return [d with { Entities = null! }, LoadErrorCode.InvalidGeometry];
        yield return [d with { Seams = null! }, LoadErrorCode.InvalidGeometry];
        yield return [d with { SharedValues = null! }, LoadErrorCode.InvalidGeometry];
        yield return [d with { ExpectedOperations = null! }, LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([Part() with { FaceUvs = Uvs().Select((f, i) => i == 0 ? f with { Axis = (Axis3)99 } : f).ToArray() }]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(new FixedDefinition(new("fixed"), new(default, new(new(0, 0, 0, 1), R4.Y, R4.Z, new(-1, 0, 0, 0))), Source, new("I"), new([Part()]))), LoadErrorCode.InvalidPose];
        yield return [Description(new FixedDefinition(new("fixed"), new(default, new(R4.X, new(0, 0, 0, 1), R4.Z, new(0, -1, 0, 0))), Source, new("I"), new([Part()]))), LoadErrorCode.InvalidPose];
        yield return [Description(new FixedDefinition(new("fixed"), new(default, new(R4.X, R4.Y, new(0, 0, 0, 1), new(0, 0, -1, 0))), Source, new("I"), new([Part()]))), LoadErrorCode.InvalidPose];
        yield return [d with { Sections = null! }, LoadErrorCode.InvalidSectionOrder];
        yield return [d with { Entities = [null!] }, LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = null! }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Gaps = null! }), LoadErrorCode.InvalidInterval];
        yield return [Description(wall with { Shape = new([Part() with { FaceUvs = null! }]) }), LoadErrorCode.InvalidGeometry];
    }
    private sealed record UnknownDefinition(EntityId Id, Pose4 Pose, Source Source) : EntityDefinition(Id, Pose, Source);
    [Theory, MemberData(nameof(InvalidDescriptions))]
    public void LoadRejectsInvalidInputsWithDiagnostics(WorldDescription description, LoadErrorCode expected)
    {
        var invalid = Assert.IsType<LoadResult.Invalid>(World.Load(description));
        Assert.Contains(invalid.Errors, e => e.Code == expected);
        Assert.All(invalid.Errors, e => Assert.False(string.IsNullOrWhiteSpace(e.Detail)));
    }
    [Fact]
    public void LoadCollectsIndependentErrorsInSourceOrder()
    {
        var d = Description(Wall() with { Id = new(""), Source = new("/z", "entity"), Pose = new(new(0, 0, 0, 1), R4), Shape = new([]) });
        d = d with { Sections = [new(new(""), 0, new("/a", "id"))], Buildings = [new(new("b"), new(1, -1), new("/m", "range"))] };
        var errors = Assert.IsType<LoadResult.Invalid>(World.Load(d)).Errors;
        Assert.Contains(errors, e => e.Code == LoadErrorCode.InvalidId && e.Source.NodePath == "/a");
        Assert.Contains(errors, e => e.Code == LoadErrorCode.InvalidInterval && e.Source.NodePath == "/m");
        Assert.Contains(errors, e => e.Code == LoadErrorCode.InvalidId && e.Source.NodePath == "/z");
        Assert.Equal(errors.OrderBy(e => e.Source.NodePath, StringComparer.Ordinal).ThenBy(e => e.Source.Property, StringComparer.Ordinal).ThenBy(e => e.Code.ToString(), StringComparer.Ordinal), errors);
    }
    public static IEnumerable<object[]> InvalidFieldSources()
    {
        yield return [Description(Wall() with { WRange = new(1, 1) }), LoadErrorCode.InvalidInterval, "value.WRange"];
        yield return [Description(Wall() with { WRange = new(-1, double.PositiveInfinity) }), LoadErrorCode.InvalidNumber, "value.WRange.Max"];
        yield return [Description(Wall() with { Gaps = [new(-2, 0)] }), LoadErrorCode.InvalidInterval, "value.Gaps"];
        yield return [Description(Wall() with { Building = new("missing") }), LoadErrorCode.MissingReference, "value.Building"];
        yield return [Description(Wall() with { WRange = new(-1, 3) }), LoadErrorCode.BuildingCoverage, "value.WRange.Max"];
        yield return [Description(Wall() with { Shape = new([Part() with { FaceUvs = [] }]) }), LoadErrorCode.InvalidGeometry, "value.Parts[0].FaceUvs"];
        yield return [Description(Wall() with { Shape = new([Part() with { HalfSize = new(.001, 1, 1) }]) }), LoadErrorCode.InvalidGeometry, "value.Parts[0].HalfSize"];
        yield return [Description(Wall() with { Shape = new([Part() with { Id = new("") }]) }), LoadErrorCode.InvalidId, "value.Parts[0].Id"];
        yield return [Description(Wall() with { Shape = new([Part() with { Center = new(double.NaN, 0, 0) }]) }), LoadErrorCode.InvalidNumber, "value.Parts[0].Center"];
    }
    [Theory, MemberData(nameof(InvalidFieldSources))]
    public void LoadReportsTheFieldResponsibleForInvalidInput(WorldDescription description, LoadErrorCode code, string property)
    {
        var errors = Assert.IsType<LoadResult.Invalid>(World.Load(description)).Errors;
        Assert.Contains(errors, e => e.Code == code && e.Source == Source with { Property = property });
    }
    [Fact]
    public void RightHandedWParallelHalfTurnAndThicknessJustAboveSamplingAreValid()
    {
        var rotation = new Rotation4(new(-1, 0, 0, 0), R4.Y, R4.Z, new(0, 0, 0, -1));
        var f = new FixedDefinition(new("fixed"), new(default, rotation), Source, new("I"), new([Part() with { HalfSize = new(.00500001, 1, 1) }]));
        Assert.IsType<LoadResult.Loaded>(World.Load(Description(f)));
        Assert.IsType<LoadResult.Loaded>(World.Load(Description(Wall() with { WRange = new(0, .01000001) })));
    }
    public static IEnumerable<object[]> UnrepresentableGeometry()
    {
        yield return [Part() with { Center = new(double.MaxValue, 0, 0), HalfSize = new(double.MaxValue, 1, 1) }, new Pose4(default, R4), LoadErrorCode.InvalidNumber, "value.Parts[0]"];
        yield return [Part() with { Center = new(1e16, 0, 0), HalfSize = new(.1, 1, 1) }, new Pose4(default, R4), LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        yield return [Part() with { HalfSize = new(.1, 1, 1) }, new Pose4(new(1e16, 0, 0, 0), R4), LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        var rational = new Rotation3(new(1d / 3, 2d / 3, 2d / 3), new(2d / 3, 1d / 3, -2d / 3), new(-2d / 3, 2d / 3, -1d / 3));
        yield return [Part() with { Center = new(1e16, 0, 0), HalfSize = new(.1, .2, .3), Rotation = rational }, new Pose4(default, R4), LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        yield return [Part() with { Center = new(1e16, 1e16, 1e16), HalfSize = new(.4, .6, 5), Rotation = rational }, new Pose4(default, R4), LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        var diagonal = Math.Sqrt(.5);
        yield return [Part() with { Center = new(1e16, 0, 0), HalfSize = new(.1, .2, .3), Rotation = new(new(diagonal, diagonal, 0), new(-diagonal, diagonal, 0), new(0, 0, 1)) }, new Pose4(default, R4), LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        yield return [Part() with { FaceUvs = Uvs().Select(f => f with { Scale = new(double.MaxValue, 1) }).ToArray(), HalfSize = new(2, 2, 2) }, new Pose4(default, R4), LoadErrorCode.InvalidNumber, "value.Parts[0].FaceUvs"];
        yield return [Part() with { FaceUvs = Uvs().Select(f => f with { Offset = new(double.MaxValue, 0) }).ToArray(), HalfSize = new(double.MaxValue, 1, 1) }, new Pose4(default, R4), LoadErrorCode.InvalidNumber, "value.Parts[0].FaceUvs"];
    }
    [Theory, MemberData(nameof(UnrepresentableGeometry))]
    public void LoadRejectsDerivedNonfiniteOrDegenerateGeometry(BoxPart part, Pose4 pose, LoadErrorCode code, string property)
    {
        var entity = new FixedDefinition(new("fixed"), pose, Source, new("I"), new([part]));
        var invalid = Assert.IsType<LoadResult.Invalid>(World.Load(Description(entity)));
        Assert.Contains(invalid.Errors, e => e.Code == code && e.Source == Source with { Property = property } && e.Entity == entity.Id);
    }
    [Fact]
    public void LoadRejectsFixedWThicknessLostToFloatingPointPrecision()
    {
        var fixedObject = new FixedDefinition(new("fixed"), new(new(0, 0, 0, 1e16), R4), Source, new("I"), new([Part()]));
        var description = Description(fixedObject) with { Sections = [new(new("I"), 1e16, Source)] };
        var invalid = Assert.IsType<LoadResult.Invalid>(World.Load(description));
        Assert.Contains(invalid.Errors, e => e.Code == LoadErrorCode.InvalidGeometry && e.Entity == fixedObject.Id && e.Source == Source with { Property = "value.Pose.Position.W" });
    }
    [Theory]
    [InlineData(1e200)]
    [InlineData(double.MaxValue)]
    public void LargeButRepresentableBoxesDoNotRequireACoordinateLimit(double halfSize)
    {
        var entity = new FixedDefinition(new("fixed"), new(default, R4), Source, new("I"), new([Part() with { HalfSize = new(halfSize, halfSize, halfSize) }]));
        var world = Assert.IsType<LoadResult.Loaded>(World.Load(Description(entity))).World;
        var slice = Assert.Single(Assert.IsType<SectionGeometryResult.Geometry>(world.GetGeometryAtW(0)).Entities);
        Assert.All(slice.Positions.Concat(slice.Normals).Concat(slice.Uvs), v => Assert.True(double.IsFinite(v)));
    }
}
