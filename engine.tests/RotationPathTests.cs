using FourD;
namespace FourD.Tests;
public partial class RotationTests
{
    private static (World World, TurnableDefinition Item, Creature3Definition Actor) FlipScene(params double[] blockedW)
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var item=new TurnableDefinition(new("item"),new(default,R),S,new("I"),new("b"),new([new(new("body"),default,new(.24,.03,.03),r3,faces),new(new("marker"),new(.2,0,.1),new(.03,.03,.03),r3,faces)]),new(true,false,.24,Axis3.X,new(-.22,0,0),.46));
        var actor=new Creature3Definition(new("actor"),new(new(-1,0,.05,0),R),S,new("I"),.2,1.6,false);
        var obstacles=blockedW.Select((w,i)=>(EntityDefinition)new BuildingElementDefinition(new($"obstacle{i}"),new(default,R),S,new("b"),new(w-.01,w+.01),[],new([new(new("part"),new(0,0,.1),new(.02,.02,.02),r3,faces)]))).ToArray();
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("minus"),-.2,S),new(new("I"),0,S),new(new("plus"),.2,S)],[new(new("b"),new(-1,1),S)],obstacles.Concat(new EntityDefinition[]{item,actor}).ToArray(),[],[],[]))).World;
        return (world,item,actor);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FlipPrefersPositiveAndUsesNegativeWhenPositiveIsBlocked(bool blockPositive)
    {
        var (world,item,actor)=blockPositive?FlipScene(-.2):FlipScene();
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id,item.Id))).Plan;
        var target=blockPositive?new SectionId("plus"):new SectionId("minus");
        var preview=Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan,target)).Value;
        Assert.Contains(preview.Samples,s=>s.Contours.Count>0);
        world.StartRotation(plan); world.Step(.6);
        var w=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity.Pose.Rotation.X.W;
        if(blockPositive) Assert.True(w>0); else Assert.True(w<0);
    }
    [Fact]
    public void FlipBlockedInBothDirectionsReturnsThePositivePathWithoutMutation()
    {
        var (world,item,actor)=FlipScene(-.2,.2);
        var blocked=Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new Flip(actor.Id,item.Id)));
        Assert.Equal(new EntityId("obstacle0"),Assert.Single(blocked.Failure.Contacts).Obstacle);
        Assert.Contains(Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(blocked.Plan,new("minus"))).Value.Samples,s=>s.Blocked);
        Assert.Equal(FailureCode.RotationBlocked,Assert.IsType<StartRotationResult.Rejected>(world.StartRotation(blocked.Plan)).Failure.Code);
        Assert.Equal(item.Pose,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity.Pose);
    }
    [Fact]
    public void UprightObjectTranslatedInXReturnsAlongTranslatedInverse()
    {
        var (world,item,actor)=Scene(); var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        world.StartRotation(plan); world.Step(1.4);
        var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id,state.Pose with {Position=state.Pose.Position with {X=state.Pose.Position.X+.3}}));
        var inverse=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new LayDown(actor.Id,item.Id))).Plan;
        world.StartRotation(inverse);world.Step(1);
        state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(.3,state.Pose.Position.X,10); Assert.Equal(0,state.Pose.Position.W,10);
    }
    [Fact]
    public void ArbitraryTiltThatCannotReturnAroundItsHingeIsRejected()
    {
        var (world,item,actor)=Scene(); var c=Math.Sqrt(.5);
        var rotation=new Rotation4(new(c,0,0,c),R.Y,R.Z,new(-c,0,0,c));
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id,new(new(0,0,0,.21),rotation)));
        Assert.Equal(FailureCode.InvalidInput,Assert.IsType<RotationPlanResult.Rejected>(world.PlanRotation(new LayDown(actor.Id,item.Id))).Failure.Code);
    }
    [Fact]
    public void FlipAroundObstacleEndsOnActorsSide()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var item=new TurnableDefinition(new("item"),new(new(1,0,0,0),R),S,new("I"),new("b"),new([new(new("body"),default,new(.24,.03,.03),r3,faces)]),new(true,false,.24,Axis3.X,new(-.22,0,0),.46));
        var actor=new Creature3Definition(new("actor"),new(new(-1,0,0,0),R),S,new("I"),.2,1.6,false);
        var wall=new FixedDefinition(new("wall"),new(default,R),S,new("I"),new([new(new("part"),default,new(.02,.5,.5),r3,faces)]));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[item,actor,wall],[],[],[]))).World;
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id,item.Id))).Plan;
        Assert.Equal(1.4,Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        world.Step(1.4);
        Assert.Equal(-1.04,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity.Pose.Position.X,10);
    }
    [Fact]
    public void ReservedSweepRejectsPlacementAndKeepsFallingObjectAboveIt()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var floor=new BuildingElementDefinition(new("floor"),new(default,R),S,new("b"),new(-1,1),[],new([new(new("part"),new(0,-1,0),new(2,.1,2),r3,faces)]));
        var (world,item,actor)=Scene(floor);
        var other=new TurnableDefinition(new("other"),new(new(-.48,.5,0,.42),R),S,new("II"),new("b"),new([new(new("part"),default,new(.1,.03,.03),r3,faces)]),new(true,false,.1,Axis3.X,new(-.08,0,0),.18));
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(other));
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        world.StartRotation(plan);
        var rejected=Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(other.Id,other.Pose with {Position=other.Pose.Position with {Y=0}}));
        Assert.Equal(FailureCode.TranslationBlocked,rejected.Failure.Code);
        world.Step(.3);
        Assert.Equal(.5,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(other.Id)).Entity.Pose.Position.Y,10);
    }
    [Fact]
    public void ActorSynchronizationPreservesSectionRotationAndRejectsNonfiniteInput()
    {
        var (world,_,actor)=Scene();
        Assert.IsType<UpdateResult.Updated>(world.SynchronizeActor(actor.Id,new(2,3,4),1.2));
        var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(actor.Id)).Entity;
        Assert.Equal(new Vec4(2,3,4,0),state.Pose.Position); Assert.Equal(R,state.Pose.Rotation); Assert.Equal(1.2,state.ActorYaw); Assert.Equal(new SectionId("I"),state.Section);
        Assert.Equal(FailureCode.InvalidInput,Assert.IsType<UpdateResult.Rejected>(world.SynchronizeActor(actor.Id,new(double.NaN,0,0),0)).Failure.Code);
        Assert.Equal(state,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(actor.Id)).Entity);
    }
    [Fact]
    public void ExpectedFailureAndWrongExpectedFailureAreChecked()
    {
        var (_,item,actor)=Scene(); var setup=new ExpectedSetup([],[],[],[]);
        var expected=new ExpectedOperation(new("fail"),S,setup,[new ExpectedRotation(new LayDown(actor.Id,item.Id),FailureCode.InvalidInput)]);
        var desc=new WorldDescription([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[item,actor],[],[],[expected]);
        Assert.IsType<LoadResult.Loaded>(World.Load(desc));
        var invalid=Assert.IsType<LoadResult.Invalid>(World.Load(desc with {ExpectedOperations=[expected with {Steps=[new ExpectedRotation(new LayDown(actor.Id,item.Id),FailureCode.Busy)]}]}));
        Assert.Contains(invalid.Errors,e=>e.Code==LoadErrorCode.ExpectedOperationFailed && e.Operation==expected.Id && e.StepIndex==0);
    }
    [Fact]
    public void ArbitraryTiltWithAHingeOnOriginCanReturnToItsSection()
    {
        var (world,item,actor)=Scene(); var c=Math.Sqrt(.5);
        var rotation=new Rotation4(new(c,0,0,c),R.Y,R.Z,new(-c,0,0,c));
        var position=new Vec4(-.48+.48*c,0,0,.48*c);
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id,new(position,rotation)));
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new LayDown(actor.Id,item.Id))).Plan;
        Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)); world.Step(1);
        var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(0,state.Pose.Position.X,10); Assert.Equal(0,state.Pose.Position.W,10); Assert.Equal(1,state.Pose.Rotation.W.W,10);
    }
    [Fact]
    public void ShallowerTargetTurnsTowardNegativeW()
    {
        var (_,source,actor)=Scene(); var item=source with {Section=new("II"),Pose=source.Pose with {Position=new(0,0,0,.42)}};
        actor=actor with {Section=new("II"),Pose=actor.Pose with {Position=new(0,1,1,.42)}};
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S),new(new("II"),.42,S)],[new(new("b"),new(-2,2),S)],[item,actor],[],[],[]))).World;
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("I")))).Plan;
        world.StartRotation(plan); world.Step(1.4);
        Assert.Equal(-1,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity.Pose.Rotation.X.W,10);
    }
    [Theory]
    [InlineData(.044,true)]
    [InlineData(.046,false)]
    public void SweepHonorsFiveMillimeterContactTolerance(double z,bool blocked)
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var wall=new BuildingElementDefinition(new("wall"),new(default,R),S,new("b"),new(.34,.36),[],new([new(new("block"),new(.2,0,z),new(.02,.02,.02),new(new(1,0,0),new(0,1,0),new(0,0,1)),faces)]));
        var (world,item,actor)=Scene(wall);
        Assert.Equal(blocked,world.PlanRotation(new StandUp(actor.Id,item.Id,new("II"))) is RotationPlanResult.Blocked);
    }
    [Fact]
    public void InitialSupportContactWithinToleranceDoesNotBlockRotation()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var floor=new BuildingElementDefinition(new("floor"),new(default,R),S,new("b"),new(-1,1),[],new([new(new("part"),new(0,-.126,0),new(2,.1,2),r3,faces)]));
        var (world,item,actor)=Scene(floor);
        Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II"))));
    }

    [Fact]
    public void InitialSupportEntityStillBlocksLaterMovement()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var item=new TurnableDefinition(new("item"),new(new(.5,0,0,0),R),S,new("I"),new("b"),new([new(new("body"),default,new(.24,.03,.03),r3,faces)]),new(true,false,.24,Axis3.X,new(-.22,0,0),.46));
        var actor=new Creature3Definition(new("actor"),new(new(-1,0,0,0),R),S,new("I"),.2,1.6,false);
        var support=new BuildingElementDefinition(new("support"),new(default,R),S,new("b"),new(-1,1),[],new([new(new("floor"),new(0,-.13,0),new(2,.1,2),r3,faces),new(new("wall"),default,new(.02,.1,.1),r3,faces)]));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[item,actor,support],[],[],[]))).World;
        var blocked=Assert.IsType<RotationPlanResult.Blocked>(world.PlanRotation(new Flip(actor.Id,item.Id)));
        Assert.Contains(blocked.Failure.Contacts,c=>c.Obstacle==support.Id && c.ObstaclePart==new PartId("wall"));
    }
}
