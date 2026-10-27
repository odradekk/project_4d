using static FourD.Tests.Fixtures;
using static FourD.Tests.ItemScenes;
using static FourD.Tests.SeamScenes;

namespace FourD.Tests;

// Loading seams, reading their state and severing them, and the rules a seam must meet to load.
public class SeamTests
{
    private static readonly SectionDefinition SectionIII = new(new("III"), 1, TestSource);
    private static readonly SectionDefinition SectionIV = new(new("IV"), 1.4, TestSource);

    private static WorldDescription WithSeams(params SeamDefinition[] seams) =>
        Description([]) with { Seams = seams };

    private static WorldDescription WithFourSections(params SeamDefinition[] seams) =>
        WithSeams(seams) with { Sections = [SectionI, SectionII, SectionIII, SectionIV] };

    private static SeamState Read(World world, string seam) =>
        Assert.IsType<SeamReadResult.Found>(world.ReadSeam(new(seam))).Seam;

    [Fact]
    public void SeamsLoadWithTheirOpenAndSeveredState()
    {
        var world = Load(WithSeams(
            Seam(id: "plain"),
            Seam(id: "closed", open: false, position: new(1, 0, 0)),
            Seam(id: "cut", severed: true, position: new(2, 0, 0))));

        Assert.Equal(new SeamState(new("plain"), true, false), Read(world, "plain"));
        Assert.Equal(new SeamState(new("closed"), false, false), Read(world, "closed"));
        Assert.Equal(new SeamState(new("cut"), true, true), Read(world, "cut"));
        Assert.Equal(new SeamReadResult.Missing(new("none")), world.ReadSeam(new("none")));
    }

    [Fact]
    public void SeveringASeamIsPermanentAndReportedOnce()
    {
        var world = SeamScene(Seam(), Biter());

        Assert.IsType<UpdateResult.Updated>(world.Sever("seam"));
        Assert.IsType<UpdateResult.Updated>(world.Sever("seam"));

        Assert.True(Read(world, "seam").Severed);
        var occurrence = Assert.Single(world.StepEvents(.1));
        Assert.Equal(new SeamSevered(new("seam")), occurrence.Event);
        Assert.Equal(0, occurrence.Time);
        Assert.Empty(world.StepEvents(.1));
    }

    [Fact]
    public void EverySeamWriteCountsAsAChangeOfTheWorldEvenWhenNothingChanges()
    {
        var world = SeamScene(Seam(), Rod(), Actor(x: .1), Biter());
        Action<World>[] writes =
        [
            world => world.SetSeamOpen(new("seam"), true),
            world => world.Sever("seam"),
            world => world.Sever("seam"),
        ];

        foreach (var write in writes)
        {
            var plan = world.AllowedPlan(new StandUp(ActorId, RodId, II));

            write(world);

            var stale = Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(plan));
            Assert.Equal(FailureCode.StalePlan, stale.Failure.Code);
        }
    }

    [Fact]
    public void ASeamCanBeClosedAndOpenedAgain()
    {
        var world = SeamScene(Seam(), Actor(x: .1));

        Assert.IsType<UpdateResult.Updated>(world.SetSeamOpen(new("seam"), false));
        Assert.False(Read(world, "seam").Open);
        world.Refuse(Request(), FailureCode.SeamClosed, ActorId);
        Assert.IsType<UpdateResult.Updated>(world.SetSeamOpen(new("seam"), true));

        world.Cross(Request());
    }

    [Fact]
    public void ASeveredSeamCanStillBeClosedButNotOpened()
    {
        var world = SeamScene(Seam(open: false, severed: true));

        var rejected = Assert.IsType<UpdateResult.Rejected>(world.SetSeamOpen(new("seam"), true));

        Assert.Equal(FailureCode.SeamSevered, rejected.Failure.Code);
        Assert.Equal(new SeamId("seam"), rejected.Failure.Seam);
        Assert.False(Read(world, "seam").Open);
        Assert.IsType<UpdateResult.Updated>(world.SetSeamOpen(new("seam"), false));
    }

    [Fact]
    public void AnUnknownSeamCannotBeOpened()
    {
        var world = SeamScene(Seam());

        var rejected = Assert.IsType<UpdateResult.Rejected>(world.SetSeamOpen(new("none"), true));

        Assert.Equal(FailureCode.UnknownId, rejected.Failure.Code);
    }

    [Fact]
    public void SeveringAnUnknownSeamIsRefusedAndChangesNothing()
    {
        var world = SeamScene(Seam(), Biter());

        var rejected = Assert.IsType<UpdateResult.Rejected>(world.Sever("seam", "none"));

        Assert.Equal(FailureCode.UnknownId, rejected.Failure.Code);
        Assert.Equal(new SeamId("none"), rejected.Failure.Seam);
        Assert.False(Read(world, "seam").Severed);
        Assert.Empty(world.StepEvents(.1));
    }

    [Fact]
    public void ASeamKeepsTheSectionsItWasLoadedWith()
    {
        var sections = new[] { I, II };
        var world = SeamScene(Seam() with { Sections = sections }, Actor(x: .1));

        sections[1] = new("elsewhere");

        Assert.IsType<CrossSeamResult.Crossed>(world.CrossSeam(Request()));
    }

    public static IEnumerable<object[]> InvalidSeams()
    {
        var elsewhere = new Vec3(5, 0, 0);
        yield return [Seam(id: ""), LoadErrorCode.InvalidId, "definition.Id"];
        yield return [Seam(position: new(double.NaN, 0, 0)), LoadErrorCode.InvalidNumber, "definition.Position"];
        yield return [Seam(normal: new(double.NaN, 0, 0)), LoadErrorCode.InvalidNumber, "definition.HorizontalNormal"];
        yield return [Seam(normal: new(0, 1, 0)), LoadErrorCode.InvalidGeometry, "definition.HorizontalNormal"];
        yield return [Seam(normal: new(1, .5, 0)), LoadErrorCode.InvalidGeometry, "definition.HorizontalNormal"];
        yield return [Seam(normal: new(2, 0, 0)), LoadErrorCode.InvalidGeometry, "definition.HorizontalNormal"];
        yield return [Seam(sections: ["I"]), LoadErrorCode.InvalidGeometry, "definition.Sections"];
        yield return [Seam(sections: ["I", "none"]), LoadErrorCode.MissingReference, "definition.Sections"];
        yield return [Seam(sections: ["I", "I"]), LoadErrorCode.DuplicateId, "definition.Sections"];
        yield return [Seam(sections: ["II", "I"]), LoadErrorCode.InvalidSectionOrder, "definition.Sections"];
        yield return [Seam() with { Sections = null! }, LoadErrorCode.InvalidGeometry, "definition.Sections"];
    }

    [Theory, MemberData(nameof(InvalidSeams))]
    public void LoadRejectsAnInvalidSeamAtTheFieldThatIsWrong(SeamDefinition seam, LoadErrorCode code, string property)
    {
        var errors = LoadErrors(WithSeams(seam));

        Assert.Contains(errors, error => error.Code == code && error.Source.Property == property);
    }

    [Fact]
    public void LoadRejectsTwoSeamsWithTheSameId()
    {
        var errors = LoadErrors(WithSeams(Seam(), Seam(position: new(5, 0, 0))));

        Assert.Contains(errors, error => error.Code == LoadErrorCode.DuplicateId && error.Source.Property == "definition.Id");
    }

    [Fact]
    public void LoadRejectsAMissingSeamAndAMissingCollection()
    {
        Assert.Contains(LoadErrors(WithSeams(null!)), error => error.Code == LoadErrorCode.InvalidGeometry);
        Assert.Contains(LoadErrors(Description([]) with { Seams = null! }), error => error.Code == LoadErrorCode.InvalidGeometry);
    }

    [Theory]
    [InlineData(.79, 0, false)]
    [InlineData(.8, 0, true)]
    [InlineData(1.5, 0, true)]
    [InlineData(0, .79, false)]
    [InlineData(0, 1, true)]
    [InlineData(.6, .6, true)]
    [InlineData(.5, .6, false)]
    public void TwoSeamsOfASectionAreAtLeastEightyCentimetresApart(double x, double z, bool loads)
    {
        var description = WithSeams(Seam(id: "a"), Seam(id: "b", position: new(x, 0, z)));

        if (loads) Load(description);
        else Assert.Contains(LoadErrors(description), error =>
            error.Code == LoadErrorCode.SeamTooClose && error.Source.Property == "definition.Position");
    }

    [Fact]
    public void TwoSeamsThatAreTooCloseAreReportedEvenWhenOneOfThemHasAnotherError()
    {
        var errors = LoadErrors(WithSeams(Seam(), Seam(position: new(.3, 0, 0))));

        Assert.Contains(errors, error => error.Code == LoadErrorCode.DuplicateId);
        Assert.Contains(errors, error => error.Code == LoadErrorCode.SeamTooClose);
    }

    [Fact]
    public void SeamsThatShareOnlyASectionThatDoesNotExistAreNotTooClose()
    {
        var errors = LoadErrors(WithSeams(Seam(id: "a", sections: ["I", "none"]), Seam(id: "b", sections: ["II", "none"])));

        Assert.DoesNotContain(errors, error => error.Code == LoadErrorCode.SeamTooClose);
        Assert.Contains(errors, error => error.Code == LoadErrorCode.MissingReference);
    }

    [Fact]
    public void SeamSpacingIsHorizontalAndIgnoresHeight()
    {
        // 5 m apart in height and .6 m apart on the floor.
        var description = WithSeams(Seam(id: "a"), Seam(id: "b", position: new(.36, 5, .48)));

        Assert.Contains(LoadErrors(description), error => error.Code == LoadErrorCode.SeamTooClose);
    }

    [Fact]
    public void SeamsThatShareOnlyOneSectionStillHaveToBeApart()
    {
        // Seam a joins I and II, seam b joins II and III: they meet in II, and a different W does not set them apart.
        var description = WithFourSections(Seam(id: "a"), Seam(id: "b", sections: ["II", "III"]));

        Assert.Contains(LoadErrors(description), error => error.Code == LoadErrorCode.SeamTooClose);
    }

    [Fact]
    public void SeamsThatShareNoSectionMayStandAtTheSamePlace()
    {
        Load(WithFourSections(Seam(id: "a"), Seam(id: "b", sections: ["III", "IV"])));
    }
}
