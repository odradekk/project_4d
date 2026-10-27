using FourD;
namespace FourD.Tests;
public partial class RotationTests
{
    [Fact]
    public void ShortLayDownMovesUniformlyAlongTheCheckedPathWithoutEnteringWall()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var wall=new BuildingElementDefinition(new("wall"),new(default,R),S,new("b"),new(-.07,-.05),[],new([new(new("part"),new(.4,0,0),new(.05,.03,.03),r3,faces)]));
        var (world,item,actor)=Scene(wall);
        var one=Math.PI/180; var c=Math.Cos(one); var sn=Math.Sin(one);
        var tilted=new Pose4(new(-.48+.48*c,0,0,.48*sn),new(new(c,0,0,sn),R.Y,R.Z,new(-sn,0,0,c)));
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id,tilted));
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new LayDown(actor.Id,item.Id))).Plan;
        var query=new OccupancyBox(new(new(.4,0,0,-.06),R),new(.05,.03,.03,.01));
        Assert.Empty(Assert.IsType<OccupancyResult.Occupancy>(world.QueryOccupancy(query,[wall.Id])).Hits);
        Assert.Equal(1,Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        foreach(var (delta,expected) in new[]{(.25,.75),(.25,.5),(.5,0d)})
        {
            world.Step(delta);
            var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
            Assert.Empty(Assert.IsType<OccupancyResult.Occupancy>(world.QueryOccupancy(query,[wall.Id])).Hits);
            Assert.Equal(expected,Math.Atan2(state.Pose.Rotation.X.W,state.Pose.Rotation.X.X)*180/Math.PI,9);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RotationEndsAtTheSpecifiedTimeWhenStartedAtNonzeroTime(bool split)
    {
        var (world,item,actor)=Scene(); world.Step(.7);
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        world.StartRotation(plan);
        if(split) world.Step(.7);
        var end=Assert.IsType<StepResult.Advanced>(world.Step(split?.7:1.4));
        Assert.Contains(end.Events,e=>e.Event is RotationEnded && e.Time==.7+1.4);
        Assert.False(Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity.Rotating);
        Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new LayDown(actor.Id,item.Id)));
    }
    [Fact]
    public void ExpectedSequenceCanAdvanceThenStandAndLayDown()
    {
        var (_,item,actor)=Scene();
        var operation=new ExpectedOperation(new("sequence"),S,new([],[],[],[]),[new ExpectedAdvance(.7),new ExpectedRotation(new StandUp(actor.Id,item.Id,new("II")),null),new ExpectedRotation(new LayDown(actor.Id,item.Id),null)]);
        var desc=new WorldDescription([new(new("I"),0,S),new(new("II"),.42,S)],[new(new("b"),new(-2,2),S)],[item,actor],[],[],[operation]);
        Assert.IsType<LoadResult.Loaded>(World.Load(desc));
    }
    [Fact]
    public void OriginIsReportedThroughoutBothRotationDirections()
    {
        var (world,item,actor)=Scene();
        var stand=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        world.StartRotation(stand);
        void UprightState() { var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity; Assert.Null(state.Section); Assert.Equal(new SectionId("I"),state.UprightOrigin); }
        UprightState(); world.Step(.35); UprightState(); world.Step(1.05); UprightState();
        var lay=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new LayDown(actor.Id,item.Id))).Plan;
        world.StartRotation(lay); UprightState(); world.Step(.5); UprightState(); world.Step(.5);
        var final=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(new SectionId("I"),final.Section); Assert.Null(final.UprightOrigin);
    }
    [Fact]
    public void CreatureHeightMustFitItsDiameterOnLoadAndAdd()
    {
        var (world,_,actor)=Scene(); var bad=actor with {Id=new("bad"),PassageHeight=.1};
        var d=new WorldDescription([new(new("I"),0,S)],[],[bad],[],[],[]);
        var invalid=Assert.IsType<LoadResult.Invalid>(World.Load(d));
        Assert.Contains(invalid.Errors,e=>e.Entity==bad.Id && e.Source.Property=="definition.PassageHeight");
        Assert.Equal(FailureCode.InvalidInput,Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(bad)).Failure.Code);
        Assert.IsType<LoadResult.Loaded>(World.Load(d with {Entities=[bad with {PassageHeight=.4}]}));
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(bad with {PassageHeight=.4}));
    }
    [Fact]
    public void ShortRotationReservesOnlyItsCheckedPath()
    {
        var (_,item,actor)=Scene();
        var description=new WorldDescription([new(new("before"),-.06,S),new(new("I"),0,S),new(new("II"),.42,S)],[new(new("b"),new(-2,2),S)],[item,actor],[],[],[]);
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(description)).World;
        var one=Math.PI/180; var c=Math.Cos(one); var sn=Math.Sin(one);
        var tilted=new Pose4(new(-.48+.48*c,0,0,.48*sn),new(new(c,0,0,sn),R.Y,R.Z,new(-sn,0,0,c)));
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id,tilted));
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new LayDown(actor.Id,item.Id))).Plan;
        world.StartRotation(plan);
        var other=item with {Id=new("outside"), Section=new("before"), Pose=new(new(.4,0,0,-.06),R),
            Shape=new([item.Shape.Parts[0] with {HalfSize=new(.1,.03,.03)}]), Interaction=item.Interaction with {Hinge=new(-.08,0,0),Reach=.18}};
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(other));
        var query=new OccupancyBox(other.Pose,new(.1,.03,.03,.006));
        foreach(var delta in new[]{.25,.25,.5})
        {
            world.Step(delta);
            Assert.Empty(Assert.IsType<OccupancyResult.Occupancy>(world.QueryOccupancy(query,[other.Id])).Hits);
        }
    }
    [Fact]
    public void PreviewSamplesRespectOneCentimeterFarthestSurfaceTravel()
    {
        var (world,item,actor)=Scene();
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        var preview=Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan,new("II"))).Value;
        // The farthest vertex is sqrt(0.98^2 + 0.006^2) from the hinge in the turn plane.
        // Its quarter-circle arc needs 154 intervals of at most one centimetre.
        Assert.Equal(155,preview.Samples.Count);
        Assert.Equal(1d/154,preview.Samples[1].Progress,12);
        Assert.Equal(1,preview.Samples[^1].Progress);
    }
    [Fact]
    public void FlipKeepsItsSectionThroughoutAndRejectedStartDoesNotChangeState()
    {
        var (world,item,actor)=FlipScene();
        var before=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id,item.Id))).Plan;
        var other=FlipScene().World;
        Assert.Equal(FailureCode.ForeignPlan,Assert.IsType<StartRotationResult.Rejected>(other.StartRotation(plan)).Failure.Code);
        Assert.Equal(before,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity);
        world.StartRotation(plan);
        foreach(var delta in new[]{.3,.9})
        {
            var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
            Assert.Equal(new SectionId("I"),state.Section); Assert.Null(state.UprightOrigin);
            world.Step(delta);
        }
        var final=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(new SectionId("I"),final.Section); Assert.Null(final.UprightOrigin); Assert.False(final.Rotating);
    }
}
