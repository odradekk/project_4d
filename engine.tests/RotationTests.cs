using FourD;
namespace FourD.Tests;
public partial class RotationTests
{
    private static readonly Source S = new("/actor", "definition");
    private static readonly Rotation4 R = new(new(1,0,0,0),new(0,1,0,0),new(0,0,1,0),new(0,0,0,1));
    [Fact]
    public void CreatureLoadsAsAnActorWithoutFourDimensionalGeometry()
    {
        var actor = new Creature3Definition(new("actor"), new(new(1,2,3,0), R), S, new("I"), .2, 1.6, false);
        var world = Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[],[actor],[],[],[]))).World;
        var state = Assert.IsType<EntityReadResult.Found>(world.ReadEntity(actor.Id)).Entity;
        Assert.Equal(actor.Pose, state.Pose);
        Assert.Equal(new SectionId("I"), state.Section);
        Assert.Empty(Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(new("I"))).Entities);
    }
    [Fact]
    public void StandUpRotatesAroundHingeAndDeliversTimedEvents()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var part=new BoxPart(new("body"),new(.24,0,0),new(.24,.03,.03),new(new(1,0,0),new(0,1,0),new(0,0,1)),faces);
        var item=new TurnableDefinition(new("item"),new(default,R),S,new("I"),new("b"),new([part]),new(true,false,.24,Axis3.X,new(.02,0,0),.46));
        var actor=new Creature3Definition(new("actor"),new(new(0,1,1,0),R),S,new("I"),.2,1.6,false);
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S),new(new("II"),.42,S)],[new(new("b"),new(-1,1),S)],[item,actor],[],[],[]))).World;
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        Assert.Equal(1.4,Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        var first=Assert.IsType<StepResult.Advanced>(world.Step(1));
        Assert.IsType<RotationStarted>(Assert.Single(first.Events).Event);
        var second=Assert.IsType<StepResult.Advanced>(world.Step(.5));
        Assert.Equal(1.4,Assert.Single(second.Events).Time,10);
        Assert.IsType<RotationEnded>(second.Events[0].Event);
        var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(.02,state.Pose.Position.X,10);
        Assert.Equal(-.02,state.Pose.Position.W,10);
        Assert.Equal(1,state.Pose.Rotation.X.W,10);
    }
    private static (World World, TurnableDefinition Item, Creature3Definition Actor) Scene(params EntityDefinition[] obstacles)
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var item=new TurnableDefinition(new("item"),new(default,R),S,new("I"),new("b"),new([new(new("body"),default,new(.5,.03,.03),new(new(1,0,0),new(0,1,0),new(0,0,1)),faces)]),new(true,false,.5,Axis3.X,new(-.48,0,0),.98));
        var actor=new Creature3Definition(new("actor"),new(new(0,1,1,0),R),S,new("I"),.2,1.6,false);
        var entities=obstacles.Concat(new EntityDefinition[]{item,actor}).ToArray();
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S),new(new("II"),.42,S)],[new(new("b"),new(-2,2),S)],entities,[],[],[]))).World;
        return (world,item,actor);
    }
    [Fact]
    public void LayDownIsTheInverseOfStandUp()
    {
        var (world,item,actor)=Scene();
        var stand=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(stand)); world.Step(1.4);
        var lay=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new LayDown(actor.Id,item.Id))).Plan;
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(lay)); world.Step(1);
        var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(item.Pose.Position.X,state.Pose.Position.X,10); Assert.Equal(item.Pose.Position.W,state.Pose.Position.W,10);
        Assert.Equal(item.Pose,state.Pose); Assert.Equal(new SectionId("I"),state.Section); Assert.Null(state.UprightOrigin);
    }
    [Fact]
    public void MidwayCollisionBlocksEvenWhenBothEndsAreClear()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var wall=new BuildingElementDefinition(new("wall"),new(default,R),S,new("b"),new(.34,.36),[],new([new(new("block"),new(.2,0,0),new(.02,.02,.02),new(new(1,0,0),new(0,1,0),new(0,0,1)),faces)]));
        var (world,item,actor)=Scene(wall);
        var blocked=Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II"))));
        Assert.Equal(FailureCode.RotationBlocked,blocked.Failure.Code); Assert.NotEmpty(blocked.Failure.Contacts);
        var preview=Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(blocked.Plan,new("II"))).Value;
        Assert.Equal(0,preview.Samples[0].Progress); Assert.Equal(1,preview.Samples[^1].Progress);
        Assert.Contains(preview.Samples,s=>s.Blocked); Assert.False(preview.Samples[0].Blocked);
        Assert.Equal(FailureCode.RotationBlocked,Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(blocked.Plan)).Failure.Code);
        Assert.Equal(item.Pose,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity.Pose);
    }
    [Fact]
    public void PlansAreBoundToWorldAndRevision()
    {
        var (world,item,actor)=Scene(); var (other,_,_)=Scene();
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        Assert.Equal(FailureCode.ForeignPlan,Assert.IsType<StartRotationResult.Rejected>(other.StartRotation(plan)).Failure.Code);
        Assert.IsType<UpdateResult.Updated>(world.SynchronizeActor(actor.Id,new(2,1,1),.3));
        Assert.Equal(FailureCode.StalePlan,Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(plan)).Failure.Code);
        Assert.Equal(FailureCode.StalePlan,Assert.IsType<PreviewResult.Rejected>(world.GetRotationPreview(plan,new("I"))).Failure.Code);
    }
    [Fact]
    public void StartEasingUsesFiveDegreesAtOneQuarterOfDuration()
    {
        var (world,item,actor)=Scene(); var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        world.StartRotation(plan); world.Step(.35);
        var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(Math.Sin(Math.PI/36),state.Pose.Rotation.X.W,10); Assert.True(state.Rotating);
    }
    [Fact]
    public void FlipInPlaceChangesProjectedHandedness()
    {
        var (world,item,actor)=Scene(); var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id,item.Id))).Plan;
        Assert.Equal(1.2,Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        world.Step(1.2);
        Assert.Equal(Handedness.Mirrored,Assert.IsType<HandednessResult.Value>(world.ReadHandedness(item.Id)).Handedness);
        Assert.Equal(HandednessComparison.Different,Assert.IsType<HandednessComparisonResult.Comparison>(world.CompareHandedness(actor.Id,item.Id)).Value);
    }
    [Fact]
    public void ExpectedRotationsUseIndependentWorldsAndDoNotChangeLoadedWorld()
    {
        var (world,item,actor)=Scene();
        var setup=new ExpectedSetup([],[],[],[]);
        var operations=new[]{new ExpectedOperation(new("one"),S,setup,[new ExpectedRotation(new StandUp(actor.Id,item.Id,new("II")),null)]),new ExpectedOperation(new("two"),S,setup,[new ExpectedRotation(new StandUp(actor.Id,item.Id,new("II")),null),new ExpectedAdvance(.1)])};
        var description=new WorldDescription([new(new("I"),0,S),new(new("II"),.42,S)],[new(new("b"),new(-2,2),S)],[item,actor],[],[],operations);
        var loaded=Assert.IsType<LoadResult.Loaded>(World.Load(description)).World;
        Assert.Equal(item.Pose,Assert.IsType<EntityReadResult.Found>(loaded.ReadEntity(item.Id)).Entity.Pose);
        Assert.Empty(Assert.IsType<StepResult.Advanced>(loaded.Step(.1)).Events);
    }
    [Fact]
    public void UnsupportedExpectedStepIsReportedAtItsIndex()
    {
        var (_,item,actor)=Scene();
        var operation=new ExpectedOperation(new("unsupported"),S,new([],[],[],[]),[new ExpectedMove(new(item.Id,default),default,null)]);
        var result=Assert.IsType<LoadResult.Invalid>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[item,actor],[],[],[operation])));
        Assert.Contains(result.Errors,e=>e.Code==LoadErrorCode.UnsupportedDefinition && e.Operation==operation.Id && e.StepIndex==0);
    }
}
