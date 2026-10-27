using static FourD.Tests.Fixtures;

namespace FourD.Tests;

public class WorldValidationTests
{
    // Reported field paths start with this property, for example "value.WRange".
    private static readonly Source Source = new("/room", "value");

    private static SectionDefinition SectionAt(string id, double w) => new(new(id), w, Source);

    private static BoxPart Cube() => Part("body", new(1, 1, 1));

    private static BuildingElementDefinition Wall() =>
        new(new("wall"), Origin, Source, new("b"), new(-1, 1), [], Shape(Cube()));

    private static FixedDefinition FixedCube(Pose4 pose, string section = "I") =>
        new(new("fixed"), pose, Source, new(section), Shape(Cube()));

    private static WorldDescription Description(EntityDefinition? entity = null) => new(
        [SectionAt("I", 0)],
        [new(new("b"), new(-2, 2), Source)],
        entity is null ? [] : [entity],
        [], [], []);

    private static WorldDescription WithPart(BoxPart part) => Description(Wall() with { Shape = Shape(part) });

    // The wall's single part gets all six faces, each changed by `change`.
    private static WorldDescription WithFaces(Func<FaceUv, FaceUv> change) =>
        WithPart(Cube() with { FaceUvs = FaceUvs().Select(change).ToArray() });

    private sealed record UnknownDefinition(EntityId Id, Pose4 Pose, Source Source) : EntityDefinition(Id, Pose, Source);

    public static IEnumerable<object[]> InvalidDescriptions()
    {
        var valid = Description(Wall());
        var wall = Wall();

        // Sections
        yield return [valid with { Sections = [] }, LoadErrorCode.InvalidSectionOrder];
        yield return [valid with { Sections = [SectionAt("I", double.NaN)] }, LoadErrorCode.InvalidNumber];
        yield return [valid with { Sections = [SectionAt("I", 0), SectionAt("II", 0)] }, LoadErrorCode.InvalidSectionOrder];
        yield return [valid with { Sections = [SectionAt("II", 1), SectionAt("I", 0)] }, LoadErrorCode.InvalidSectionOrder];
        yield return [valid with { Sections = [SectionAt("I", 0), SectionAt("I", 1)] }, LoadErrorCode.DuplicateId];
        yield return [valid with { Sections = [SectionAt("", 0)] }, LoadErrorCode.InvalidId];
        yield return [valid with { Sections = [null!] }, LoadErrorCode.InvalidSectionOrder];
        yield return [valid with { Sections = null! }, LoadErrorCode.InvalidSectionOrder];

        // Buildings
        yield return [valid with { Buildings = [new(new("b"), new(2, -2), Source)] }, LoadErrorCode.InvalidInterval];
        yield return [valid with { Buildings = [null!] }, LoadErrorCode.InvalidGeometry];
        yield return [valid with { Buildings = null! }, LoadErrorCode.InvalidGeometry];

        // Entities, ids and references
        yield return [valid with { Entities = [wall, wall] }, LoadErrorCode.DuplicateId];
        yield return [valid with { Entities = [null!] }, LoadErrorCode.InvalidGeometry];
        yield return [valid with { Entities = null! }, LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Id = new("") }), LoadErrorCode.InvalidId];
        yield return [Description(wall with { Building = new("missing") }), LoadErrorCode.MissingReference];
        yield return [Description(FixedCube(Origin, section: "missing")), LoadErrorCode.MissingReference];

        // Building W range and gaps
        yield return [Description(wall with { WRange = new(-3, 1) }), LoadErrorCode.BuildingCoverage];
        yield return [Description(wall with { WRange = new(0, 0) }), LoadErrorCode.InvalidInterval];
        yield return [Description(wall with { WRange = new(double.NegativeInfinity, 1) }), LoadErrorCode.InvalidNumber];
        yield return [Description(wall with { WRange = new(0, .01) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Gaps = [new(-2, 0)] }), LoadErrorCode.InvalidInterval];
        yield return [Description(wall with { Gaps = [new(0, .5), new(.3, .6)] }), LoadErrorCode.InvalidInterval];
        yield return [Description(wall with { Gaps = [new(.3, .6), new(-.6, -.3)] }), LoadErrorCode.InvalidInterval];
        yield return [Description(wall with { Gaps = [new(-.995, 1)] }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Gaps = null! }), LoadErrorCode.InvalidInterval];

        // Shape and parts
        yield return [Description(wall with { Shape = null! }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new(null!) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([null!]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = new([]) }), LoadErrorCode.InvalidGeometry];
        yield return [Description(wall with { Shape = Shape(Cube(), Cube()) }), LoadErrorCode.DuplicateId];
        yield return [WithPart(Cube() with { Id = new("") }), LoadErrorCode.InvalidId];
        yield return [WithPart(Cube() with { HalfSize = new(0, 1, 1) }), LoadErrorCode.InvalidGeometry];
        yield return [WithPart(Cube() with { HalfSize = new(.005, 1, 1) }), LoadErrorCode.InvalidGeometry];
        yield return [WithPart(Cube() with { HalfSize = new(double.PositiveInfinity, 1, 1) }), LoadErrorCode.InvalidNumber];
        yield return [WithPart(Cube() with { Center = new(double.NaN, 0, 0) }), LoadErrorCode.InvalidNumber];
        yield return [WithPart(Cube() with { Rotation = Identity3 with { X = new(-1, 0, 0) } }), LoadErrorCode.InvalidPose];
        yield return [WithPart(Cube() with { Rotation = Identity3 with { Y = new(1e-8, 1, 0) } }), LoadErrorCode.InvalidPose];
        yield return [WithPart(Cube() with { Rotation = Identity3 with { X = new(double.NaN, 0, 0) } }), LoadErrorCode.InvalidNumber];

        // Face coordinates
        yield return [WithPart(Cube() with { FaceUvs = null! }), LoadErrorCode.InvalidGeometry];
        yield return [WithPart(Cube() with { FaceUvs = FaceUvs()[..5] }), LoadErrorCode.InvalidGeometry];
        yield return [WithPart(Cube() with { FaceUvs = Enumerable.Repeat(FaceUvs()[0], 6).ToArray() }), LoadErrorCode.InvalidGeometry];
        yield return [WithFaces(f => f with { Axis = (Axis3)99 }), LoadErrorCode.InvalidGeometry];
        yield return [WithPart(Cube() with { FaceUvs = [FaceUvs()[0] with { Axis = (Axis3)99 }, .. FaceUvs()[1..]] }), LoadErrorCode.InvalidGeometry];
        yield return [WithFaces(f => f with { Scale = new(0, 1) }), LoadErrorCode.InvalidGeometry];
        yield return [WithFaces(f => f with { Offset = new(double.PositiveInfinity, 0) }), LoadErrorCode.InvalidNumber];

        // Poses
        yield return [Description(wall with { Pose = At(0, 0, 0, 1) }), LoadErrorCode.InvalidPose];
        yield return [Description(wall with { Pose = new(default, Identity4 with { X = new(-1, 0, 0, 0) }) }), LoadErrorCode.InvalidPose];
        yield return [Description(wall with { Pose = new(default, XwQuarterTurn) }), LoadErrorCode.InvalidPose];
        yield return [Description(wall with { Pose = new(default, Identity4 with { X = new(1.00000001, 0, 0, 0) }) }), LoadErrorCode.InvalidPose];
        yield return [Description(wall with { Pose = At(double.NaN, 0, 0) }), LoadErrorCode.InvalidNumber];
        yield return [Description(wall with { Pose = new(default, Identity4 with { X = new(double.NaN, 0, 0, 0) }) }), LoadErrorCode.InvalidNumber];
        yield return [Description(FixedCube(At(0, 0, 0, .1))), LoadErrorCode.InvalidPose];
        yield return [Description(FixedCube(new(default, XwQuarterTurn))), LoadErrorCode.InvalidPose];
        var quarterTurnInYw = Identity4 with { Y = new(0, 0, 0, 1), W = new(0, -1, 0, 0) };
        var quarterTurnInZw = Identity4 with { Z = new(0, 0, 0, 1), W = new(0, 0, -1, 0) };
        yield return [Description(FixedCube(new(default, quarterTurnInYw))), LoadErrorCode.InvalidPose];
        yield return [Description(FixedCube(new(default, quarterTurnInZw))), LoadErrorCode.InvalidPose];

        // Definitions that are not supported yet
        var unknown = new UnknownDefinition(new("unknown"), Origin, Source);
        var water = new SharedValueDefinition(new("water"), new(0, 1), 0, Source);
        var move = new ExpectedMove(new(new("item"), default), default, null);
        var operation = new ExpectedOperation(new("operation"), Source, new([], [], [], []), [move]);
        yield return [valid with { Entities = [unknown] }, LoadErrorCode.UnsupportedDefinition];
        yield return [valid with { SharedValues = [water] }, LoadErrorCode.UnsupportedDefinition];
        yield return [valid with { ExpectedOperations = [operation] }, LoadErrorCode.UnsupportedDefinition];
        yield return [valid with { Seams = null! }, LoadErrorCode.InvalidGeometry];
        yield return [valid with { SharedValues = null! }, LoadErrorCode.InvalidGeometry];
        yield return [valid with { ExpectedOperations = null! }, LoadErrorCode.InvalidGeometry];
    }

    [Theory, MemberData(nameof(InvalidDescriptions))]
    public void LoadRejectsInvalidInputsWithDiagnostics(WorldDescription description, LoadErrorCode expected)
    {
        var errors = LoadErrors(description);

        Assert.Contains(errors, e => e.Code == expected);
        Assert.All(errors, e => Assert.False(string.IsNullOrWhiteSpace(e.Detail)));
    }

    [Fact]
    public void LoadCollectsIndependentErrorsInSourceOrder()
    {
        var brokenWall = Wall() with { Id = new(""), Source = new("/z", "entity"), Pose = At(0, 0, 0, 1), Shape = new([]) };
        var description = Description(brokenWall) with
        {
            Sections = [new(new(""), 0, new("/a", "id"))],
            Buildings = [new(new("b"), new(1, -1), new("/m", "range"))],
        };

        var errors = LoadErrors(description);

        Assert.Contains(errors, e => e.Code == LoadErrorCode.InvalidId && e.Source.NodePath == "/a");
        Assert.Contains(errors, e => e.Code == LoadErrorCode.InvalidInterval && e.Source.NodePath == "/m");
        Assert.Contains(errors, e => e.Code == LoadErrorCode.InvalidId && e.Source.NodePath == "/z");
        var sorted = errors
            .OrderBy(e => e.Source.NodePath, StringComparer.Ordinal)
            .ThenBy(e => e.Source.Property, StringComparer.Ordinal)
            .ThenBy(e => e.Code.ToString(), StringComparer.Ordinal);
        Assert.Equal(sorted, errors);
    }

    public static IEnumerable<object[]> InvalidFieldSources()
    {
        yield return [Description(Wall() with { WRange = new(1, 1) }), LoadErrorCode.InvalidInterval, "value.WRange"];
        yield return [
            Description(Wall() with { WRange = new(-1, double.PositiveInfinity) }),
            LoadErrorCode.InvalidNumber, "value.WRange.Max"];
        yield return [Description(Wall() with { Gaps = [new(-2, 0)] }), LoadErrorCode.InvalidInterval, "value.Gaps"];
        yield return [Description(Wall() with { Building = new("missing") }), LoadErrorCode.MissingReference, "value.Building"];
        yield return [Description(Wall() with { WRange = new(-1, 3) }), LoadErrorCode.BuildingCoverage, "value.WRange.Max"];
        yield return [WithPart(Cube() with { FaceUvs = [] }), LoadErrorCode.InvalidGeometry, "value.Parts[0].FaceUvs"];
        yield return [
            WithPart(Cube() with { HalfSize = new(.001, 1, 1) }),
            LoadErrorCode.InvalidGeometry, "value.Parts[0].HalfSize"];
        yield return [WithPart(Cube() with { Id = new("") }), LoadErrorCode.InvalidId, "value.Parts[0].Id"];
        yield return [
            WithPart(Cube() with { Center = new(double.NaN, 0, 0) }),
            LoadErrorCode.InvalidNumber, "value.Parts[0].Center"];
    }

    [Theory, MemberData(nameof(InvalidFieldSources))]
    public void LoadReportsTheFieldResponsibleForInvalidInput(WorldDescription description, LoadErrorCode code, string property)
    {
        var errors = LoadErrors(description);

        Assert.Contains(errors, e => e.Code == code && e.Source == Source with { Property = property });
    }

    [Fact]
    public void RightHandedWParallelHalfTurnAndThicknessJustAboveSamplingAreValid()
    {
        var thinPart = Cube() with { HalfSize = new(.00500001, 1, 1) };
        var thinFixed = FixedCube(new(default, XwHalfTurn)) with { Shape = Shape(thinPart) };
        Assert.IsType<LoadResult.Loaded>(World.Load(Description(thinFixed)));

        Assert.IsType<LoadResult.Loaded>(World.Load(Description(Wall() with { WRange = new(0, .01000001) })));
    }

    public static IEnumerable<object[]> UnrepresentableGeometry()
    {
        var rational = new Rotation3(new(1d / 3, 2d / 3, 2d / 3), new(2d / 3, 1d / 3, -2d / 3), new(-2d / 3, 2d / 3, -1d / 3));
        var diagonal = Math.Sqrt(.5);
        var turnedAboutZ = new Rotation3(new(diagonal, diagonal, 0), new(-diagonal, diagonal, 0), new(0, 0, 1));
        FaceUv[] ScaledFaces(Vec2 scale) => FaceUvs().Select(f => f with { Scale = scale }).ToArray();
        FaceUv[] OffsetFaces(Vec2 offset) => FaceUvs().Select(f => f with { Offset = offset }).ToArray();

        yield return [
            Cube() with { Center = new(double.MaxValue, 0, 0), HalfSize = new(double.MaxValue, 1, 1) },
            Origin, LoadErrorCode.InvalidNumber, "value.Parts[0]"];
        yield return [
            Cube() with { Center = new(1e16, 0, 0), HalfSize = new(.1, 1, 1) },
            Origin, LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        yield return [
            Cube() with { HalfSize = new(.1, 1, 1) },
            At(1e16, 0, 0), LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        yield return [
            Cube() with { Center = new(1e16, 0, 0), HalfSize = new(.1, .2, .3), Rotation = rational },
            Origin, LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        yield return [
            Cube() with { Center = new(1e16, 1e16, 1e16), HalfSize = new(.4, .6, 5), Rotation = rational },
            Origin, LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        yield return [
            Cube() with { Center = new(1e16, 0, 0), HalfSize = new(.1, .2, .3), Rotation = turnedAboutZ },
            Origin, LoadErrorCode.InvalidGeometry, "value.Parts[0]"];
        yield return [
            Cube() with { FaceUvs = ScaledFaces(new(double.MaxValue, 1)), HalfSize = new(2, 2, 2) },
            Origin, LoadErrorCode.InvalidNumber, "value.Parts[0].FaceUvs"];
        yield return [
            Cube() with { FaceUvs = OffsetFaces(new(double.MaxValue, 0)), HalfSize = new(double.MaxValue, 1, 1) },
            Origin, LoadErrorCode.InvalidNumber, "value.Parts[0].FaceUvs"];
    }

    [Theory, MemberData(nameof(UnrepresentableGeometry))]
    public void LoadRejectsDerivedNonfiniteOrDegenerateGeometry(BoxPart part, Pose4 pose, LoadErrorCode code, string property)
    {
        var entity = FixedCube(pose) with { Shape = Shape(part) };

        var errors = LoadErrors(Description(entity));

        Assert.Contains(errors, e => e.Code == code && e.Source == Source with { Property = property } && e.Entity == entity.Id);
    }

    [Fact]
    public void LoadRejectsFixedWThicknessLostToFloatingPointPrecision()
    {
        var fixedObject = FixedCube(At(0, 0, 0, 1e16));
        var description = Description(fixedObject) with { Sections = [SectionAt("I", 1e16)] };

        var errors = LoadErrors(description);

        var expectedSource = Source with { Property = "value.Pose.Position.W" };
        Assert.Contains(errors, e => e.Code == LoadErrorCode.InvalidGeometry && e.Entity == fixedObject.Id && e.Source == expectedSource);
    }

    [Theory]
    [InlineData(1e200)]
    [InlineData(double.MaxValue)]
    public void LargeButRepresentableBoxesDoNotRequireACoordinateLimit(double halfSize)
    {
        var huge = FixedCube(Origin) with { Shape = Shape(Cube() with { HalfSize = new(halfSize, halfSize, halfSize) }) };
        var world = Load(Description(huge));

        var slice = Assert.Single(world.SlicesAt(0));

        Assert.All(slice.Positions.Concat(slice.Normals).Concat(slice.Uvs), v => Assert.True(double.IsFinite(v)));
    }
}
