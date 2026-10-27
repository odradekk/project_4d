using FourD;
namespace FourD.Tests;
public partial class RotationTests
{
    [Theory]
    [InlineData(false,0d,.003)]
    [InlineData(true,0d,.003)]
    [InlineData(false,.003,0d)]
    [InlineData(true,.003,0d)]
    [InlineData(false,.42,.423)]
    [InlineData(true,.42,.423)]
    [InlineData(false,.423,.42)]
    [InlineData(true,.423,.42)]
    public void AdjacentSectionActorFlipsWithoutMovingItemToAnotherW(bool obstacle,double sourceW,double actorW)
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var item=new TurnableDefinition(new("item"),new(new(obstacle?1:0,0,0,sourceW),R),S,new("source"),new("b"),new([new(new("body"),default,new(.24,.03,.03),r3,faces)]),new(true,false,.24,Axis3.X,new(-.20,0,0),.44));
        var actor=new Creature3Definition(new("actor"),new(new(-1,0,0,actorW),R),S,new("actor-section"),.2,1.6,false);
        var entities=new List<EntityDefinition>{item,actor};
        if(obstacle) entities.Add(new FixedDefinition(new("wall"),new(new(0,0,0,actorW),R),S,new("actor-section"),new([new(new("part"),default,new(.02,.5,.5),r3,faces)])));
        var sections=new[]{new SectionDefinition(new("source"),sourceW,S),new SectionDefinition(new("actor-section"),actorW,S)}.OrderBy(s=>s.W).ToArray();
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new(sections,[new(new("b"),new(-2,2),S)],entities,[],[],[]))).World;
        Assert.True(Assert.IsType<SlicePresenceResult.Present>(world.HasSlice(item.Id,actor.Section)).HasVolume);
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id,item.Id))).Plan;
        var preview=Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan,item.Section)).Value.Samples[^1].Contours;
        Assert.Equal(obstacle?1.4:1.2,Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        world.Step(obstacle?1.4:1.2);
        var end=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(sourceW,end.Pose.Position.W); Assert.Equal(item.Section,end.Section); Assert.Null(end.UprightOrigin); Assert.False(end.Rotating);
        var geometry=Assert.Single(Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(item.Section)).Entities,e=>e.Entity==item.Id);
        var points=Segments(preview).SelectMany(e=>new[]{e.A,e.B}).ToArray(); Assert.NotEmpty(points);
        for(int i=0;i<3;i++)
        {
            Assert.Equal(geometry.Positions.Where((_,index)=>index%3==i).Min(),points.Min(p=>p[i]),9);
            Assert.Equal(geometry.Positions.Where((_,index)=>index%3==i).Max(),points.Max(p=>p[i]),9);
        }
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id,end.Pose with {Position=end.Pose.Position with {X=end.Pose.Position.X+.1}}));
    }
    [Theory]
    [InlineData(0d,.003,.002,.013)]
    [InlineData(.003,0d,-.010,.001)]
    public void FlipFindsObstacleInActorsSectionButKeepsItemsSection(double sourceW,double actorW,double minW,double maxW)
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var r3=new Rotation3(new(1,0,0),new(0,1,0),new(0,0,1));
        var item=new TurnableDefinition(new("item"),new(new(1,0,0,sourceW),R),S,new("source"),new("b"),new([new(new("body"),default,new(.24,.03,.03),r3,faces)]),new(true,false,.24,Axis3.X,new(-.20,0,0),.44));
        var actor=new Creature3Definition(new("actor"),new(new(-1,0,0,actorW),R),S,new("actor-section"),.2,1.6,false);
        var wall=new BuildingElementDefinition(new("wall"),new(default,R),S,new("b"),new(minW,maxW),[],new([new(new("part"),default,new(.02,.5,.5),r3,faces)]));
        var sections=new[]{new SectionDefinition(new("source"),sourceW,S),new SectionDefinition(new("actor-section"),actorW,S)}.OrderBy(s=>s.W).ToArray();
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new(sections,[new(new("b"),new(-2,2),S)],[item,actor,wall],[],[],[]))).World;
        Assert.False(Assert.IsType<SlicePresenceResult.Present>(world.HasSlice(wall.Id,item.Section)).HasVolume);
        Assert.True(Assert.IsType<SlicePresenceResult.Present>(world.HasSlice(wall.Id,actor.Section)).HasVolume);
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new Flip(actor.Id,item.Id))).Plan;
        Assert.Equal(1.4,Assert.IsType<StartRotationResult.Started>(world.StartRotation(plan)).Duration);
        world.Step(1.4);
        var end=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity;
        Assert.Equal(-1.04,end.Pose.Position.X,10); Assert.Equal(sourceW,end.Pose.Position.W); Assert.Equal(item.Section,end.Section);
    }
}
