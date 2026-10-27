using FourD;
namespace FourD.Tests;
public partial class RotationTests
{
    [Theory]
    [InlineData(false,0d)]
    [InlineData(true,0d)]
    [InlineData(false,.42)]
    [InlineData(true,.42)]
    public void ObstacleFlipEndsAtSectionAndAllowsXYZPlacement(bool forceNegative,double sectionW)
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var item=new TurnableDefinition(new("item"),new(new(1,0,0,sectionW),R),S,new("I"),new("b"),new([new(new("body"),default,new(.24,.03,.03),r3,faces)]),new(true,false,.24,Axis3.X,new(-.22,0,0),.46));
        var actor=new Creature3Definition(new("actor"),new(new(-1,0,0,sectionW),R),S,new("I"),.2,1.6,false);
        var wall=new FixedDefinition(new("wall"),new(new(0,0,0,sectionW),R),S,new("I"),new([new(new("part"),default,new(.02,.5,.5),r3,faces)]));
        var entities=new List<EntityDefinition>{item,actor,wall};
        if(forceNegative) entities.Add(new BuildingElementDefinition(new("block"),new(default,R),S,new("b"),new(sectionW-.52,sectionW-.50),[],new([new(new("part"),new(.863,0,0),new(.03,.1,.1),r3,faces)])));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),sectionW,S)],[new(new("b"),new(-2,2),S)],entities,[],[],[]))).World;
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id,item.Id))).Plan;
        var preview=Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan,new("I"))).Value.Samples[^1].Contours;
        Assert.Equal(1.4,Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        world.Step(.35);
        var midpoint=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.True(forceNegative ? midpoint.Pose.Position.W>sectionW : midpoint.Pose.Position.W<sectionW);
        world.Step(1.05);
        var end=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(new SectionId("I"),end.Section); Assert.False(end.Rotating);
        var geometry=Assert.Single(Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(new("I"))).Entities,e=>e.Entity==item.Id);
        var previewPoints=Segments(preview).SelectMany(e=>new[]{e.A,e.B}).ToArray(); Assert.NotEmpty(previewPoints);
        for(int i=0;i<3;i++)
        {
            Assert.Equal(geometry.Positions.Where((_,index)=>index%3==i).Min(),previewPoints.Min(p=>p[i]),9);
            Assert.Equal(geometry.Positions.Where((_,index)=>index%3==i).Max(),previewPoints.Max(p=>p[i]),9);
        }
        var target=end.Pose with {Position=end.Pose.Position with {X=end.Pose.Position.X+.1}};
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id,target));
        Assert.Equal(sectionW,end.Pose.Position.W);
    }
    [Theory]
    [InlineData(0d)]
    [InlineData(.42)]
    public void InPlaceFlipWithOffsetShapeAllowsXYZPlacement(double sectionW)
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var item=new TurnableDefinition(new("item"),new(new(0,0,0,sectionW),R),S,new("I"),new("b"),new([new(new("body"),new(.24,0,0),new(.24,.03,.03),r3,faces)]),new(true,false,.24,Axis3.X,new(.02,0,0),.46));
        var actor=new Creature3Definition(new("actor"),new(new(-1,0,0,sectionW),R),S,new("I"),.2,1.6,false);
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),sectionW,S)],[new(new("b"),new(-2,2),S)],[item,actor],[],[],[]))).World;
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id,item.Id))).Plan;
        Assert.Equal(1.2,Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        world.Step(1.2);
        var end=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id,end.Pose with {Position=end.Pose.Position with {X=end.Pose.Position.X+.1}}));
        Assert.Equal(sectionW,end.Pose.Position.W);
    }
    [Fact]
    public void NearHalfTurnIsNotSnappedBeforeItsEnd()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var item=new TurnableDefinition(new("item"),new(new(1,0,0,0),R),S,new("I"),new("b"),new([new(new("body"),default,new(.24,.03,.03),r3,faces)]),new(true,false,.24,Axis3.X,new(-.22,0,0),.46));
        var actor=new Creature3Definition(new("actor"),new(new(-1,0,0,0),R),S,new("I"),.2,1.6,false);
        var wall=new FixedDefinition(new("wall"),new(default,R),S,new("I"),new([new(new("part"),default,new(.02,.5,.5),r3,faces)]));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[item,actor,wall],[],[],[]))).World;
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id,item.Id))).Plan;
        world.StartRotation(plan); world.Step(1.4-1e-9);
        var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.True(state.Rotating); Assert.NotEqual(0,state.Pose.Position.W);
        world.Step(1e-9);
        Assert.Equal(0,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity.Pose.Position.W);
    }
}
