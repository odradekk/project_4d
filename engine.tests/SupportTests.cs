using FourD;
namespace FourD.Tests;
public class SupportTests
{
    private static readonly Source S=new("/room","shape");
    private static readonly Rotation3 R3=new(new(1,0,0),new(0,1,0),new(0,0,1));
    private static readonly Rotation4 R4=new(new(1,0,0,0),new(0,1,0,0),new(0,0,1,0),new(0,0,0,1));
    private static BoxAssembly Box(Vec3 half) => new([new(new("body"),default,half,R3,Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray())]);
    [Fact]
    public void ObjectFallsOntoFloorAndReportsRealContact()
    {
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(2,.05,2)));
        var item=new TurnableDefinition(new("item"),new(new(0,2,0,0),R4),S,new("I"),new("b"),Box(new(.5,.5,.5)),new(true,false,.5,Axis3.X,new(-.48,-.5,0),.98));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[floor,item],[],[],[]))).World;
        var support=Assert.IsType<SupportResult.Supported>(world.FindSupport(item.Id));
        Assert.Equal(.5,support.RestingOriginY,10);
        var contact=Assert.Single(support.Contacts);
        Assert.Equal(new EntityId("floor"),contact.Support);
        Assert.Equal(0,contact.Point.Y,10); Assert.InRange(contact.Point.W,-.006,.006); Assert.True(contact.Normal.Y>0);
        var step=Assert.IsType<StepResult.Advanced>(world.Step(.1));
        var fell=Assert.IsType<EntityFell>(Assert.Single(step.Events).Event);
        Assert.Equal(.5,fell.ToY,10);
        Assert.Equal(.5,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity.Pose.Position.Y,10);
    }
    [Fact]
    public void FloorBetweenSectionsSupportsTiltedBoxEvenWhenFloorHasNoSectionSlice()
    {
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-.05,0,0),R4),S,new("b"),new(.18,.24),[],Box(new(2,.05,2)));
        var item=new TurnableDefinition(new("item"),new(new(0,2,0,0),R4),S,new("I"),new("b"),Box(new(.5,.5,.5)),new(true,false,.5,Axis3.X,new(-.48,-.5,0),.98));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S),new(new("II"),.42,S)],[new(new("b"),new(-2,2),S)],[floor,item],[],[],[]))).World;
        var turn=new Rotation4(new(0,0,0,1),R4.Y,R4.Z,new(-1,0,0,0));
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(item.Id,new(new(0,2,0,.21),turn)));
        Assert.False(Assert.IsType<SlicePresenceResult.Present>(world.HasSlice(floor.Id,new("I"))).HasVolume);
        Assert.False(Assert.IsType<SlicePresenceResult.Present>(world.HasSlice(floor.Id,new("II"))).HasVolume);
        var support=Assert.IsType<SupportResult.Supported>(world.FindSupport(item.Id));
        Assert.Equal(.5,support.RestingOriginY,10);
        var contact=Assert.Single(support.Contacts);
        Assert.Equal(0,contact.Point.Y,10); Assert.InRange(contact.Point.W,.18,.24); Assert.True(contact.Normal.Y>0);
    }
    [Fact]
    public void SideTouchAloneDoesNotSupportAndNoFloorLeavesPositionUnchanged()
    {
        var obstacle=new BuildingElementDefinition(new("wall"),new(new(1,0,0,0),R4),S,new("b"),new(-1,1),[],Box(new(.5,1,.5)));
        var item=new TurnableDefinition(new("item"),new(new(0,2,0,0),R4),S,new("I"),new("b"),Box(new(.5,.5,.5)),new(true,false,.5,Axis3.X,new(-.48,-.5,0),.98));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[obstacle,item],[],[],[]))).World;
        Assert.IsType<SupportResult.Unsupported>(world.FindSupport(item.Id));
        Assert.Empty(Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events);
        Assert.Equal(2,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(item.Id)).Entity.Pose.Position.Y);
    }
    [Theory]
    [InlineData(.497)]
    [InlineData(.5)]
    public void TouchOrSmallExistingPenetrationDoesNotLiftObject(double y)
    {
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(2,.05,2)));
        var item=new TurnableDefinition(new("item"),new(new(0,y,0,0),R4),S,new("I"),new("b"),Box(new(.5,.5,.5)),new(true,false,.5,Axis3.X,new(-.48,-.5,0),.98));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[floor,item],[],[],[]))).World;
        var support=Assert.IsType<SupportResult.Supported>(world.FindSupport(item.Id));
        Assert.Equal(y,support.RestingOriginY,10);
        var p=Assert.Single(support.Contacts).Point;
        Assert.InRange(p.Y,y-.5,0);
        Assert.Empty(Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events);
    }
    [Fact]
    public void SlightPenetrationIntoCeilingDoesNotSupportObjectBelow()
    {
        var ceiling=new BuildingElementDefinition(new("ceiling"),new(new(0,.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(2,.05,2)));
        var item=new TurnableDefinition(new("item"),new(new(0,-.497,0,0),R4),S,new("I"),new("b"),Box(new(.5,.5,.5)),new(true,false,.5,Axis3.X,new(-.48,-.5,0),.98));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[ceiling,item],[],[],[]))).World;
        Assert.IsType<SupportResult.Unsupported>(world.FindSupport(item.Id));
    }
    [Fact]
    public void ShallowOverlapInWDoesNotEraseExistingFloorSupport()
    {
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-.05,0,0),R4),S,new("b"),new(.003,.015),[],Box(new(2,.05,2)));
        var item=new TurnableDefinition(new("item"),new(new(0,.497,0,0),R4),S,new("I"),new("b"),Box(new(.5,.5,.5)),new(true,false,.5,Axis3.X,new(-.48,-.5,0),.98));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[floor,item],[],[],[]))).World;
        var support=Assert.IsType<SupportResult.Supported>(world.FindSupport(item.Id));
        Assert.Equal(.497,support.RestingOriginY,10);
        var p=Assert.Single(support.Contacts).Point;
        Assert.InRange(p.W,.003,.006); Assert.InRange(p.Y,-.003,0);
    }
    [Fact]
    public void StaticEntityCanBeQueriedForSupportWithoutMovingIt()
    {
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(2,.05,2)));
        var cabinet=new FixedDefinition(new("cabinet"),new(new(0,2,0,0),R4),S,new("I"),Box(new(.5,.5,.5)));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[floor,cabinet],[],[],[]))).World;
        Assert.Equal(.5,Assert.IsType<SupportResult.Supported>(world.FindSupport(cabinet.Id)).RestingOriginY,10);
        Assert.Empty(Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events);
        Assert.Equal(2,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(cabinet.Id)).Entity.Pose.Position.Y);
    }
    [Theory]
    [InlineData("a-upper", "z-lower")]
    [InlineData("z-upper", "a-lower")]
    public void StepSettlesStackInOneCallRegardlessOfIdOrder(string upperId, string lowerId)
    {
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(2,.05,2)));
        TurnableDefinition Item(string id,double y) => new(new(id),new(new(0,y,0,0),R4),S,new("I"),new("b"),Box(new(.5,.5,.5)),new(true,false,.5,Axis3.X,new(-.48,-.5,0),.98));
        var upper=Item(upperId,4); var lower=Item(lowerId,2);
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[floor,upper,lower],[],[],[]))).World;
        var events=Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events;
        Assert.Equal(1.5,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(upper.Id)).Entity.Pose.Position.Y,10);
        Assert.Equal(.5,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(lower.Id)).Entity.Pose.Position.Y,10);
        Assert.Equal(new[]{upperId,lowerId}.Order(StringComparer.Ordinal),events.Select(e=>Assert.IsType<EntityFell>(e.Event).Entity.Value));
        Assert.All(events,e=>Assert.Equal(.1,e.Time));
        var upperEvent=Assert.IsType<EntityFell>(events.Single(e=>((EntityFell)e.Event).Entity==upper.Id).Event);
        Assert.Equal(4,upperEvent.FromY); Assert.Equal(1.5,upperEvent.ToY,10);
        Assert.Equal(1.5,Assert.IsType<SupportResult.Supported>(world.FindSupport(upper.Id)).RestingOriginY,10);
        Assert.Empty(Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MutualPotentialSupportSettlesToFloorOrStaysWhenUnbounded(bool hasFloor)
    {
        var template=Box(new(.1,.1,.1)).Parts[0];
        BoxAssembly Shape(bool first) => new([template with {Id=new("left"),Center=new(-1,first?1:-1,0)}, template with {Id=new("right"),Center=new(1,first?-1:1,0)}]);
        TurnableDefinition Item(string id,bool first) => new(new(id),new(default,R4),S,new("I"),new("b"),Shape(first),new(true,false,2,Axis3.X,new(-1.08,0,0),2.18));
        var a=Item("a",true); var b=Item("b",false);
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-10.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(3,.05,3)));
        EntityDefinition[] items=hasFloor?[a,b,floor]:[a,b];
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-4,4),S)],items,[],[],[]))).World;
        var events=Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events;
        var expected=hasFloor?-8.9:0;
        Assert.Equal(expected,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(a.Id)).Entity.Pose.Position.Y,9);
        Assert.Equal(expected,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(b.Id)).Entity.Pose.Position.Y,9);
        Assert.Equal(hasFloor?2:0,events.Count);
    }
    [Fact]
    public void DescendingSupportPreservesEarlierShallowPenetration()
    {
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(2,.05,2)));
        TurnableDefinition Item(string id,double y) => new(new(id),new(new(0,y,0,0),R4),S,new("I"),new("b"),Box(new(.5,.5,.5)),new(true,false,.5,Axis3.X,new(-.48,-.5,0),.98));
        var upper=Item("a",2.997); var lower=Item("z",2);
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[floor,upper,lower],[],[],[]))).World;
        Assert.IsType<StepResult.Advanced>(world.Step(.1));
        Assert.Equal(1.497,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(upper.Id)).Entity.Pose.Position.Y,10);
        Assert.Equal(.5,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(lower.Id)).Entity.Pose.Position.Y,10);
        var contact=Assert.Single(Assert.IsType<SupportResult.Supported>(world.FindSupport(upper.Id)).Contacts);
        Assert.Equal(lower.Id,contact.Support); Assert.InRange(contact.Point.Y,.997,1); Assert.True(contact.Normal.Y>0);
    }
    [Theory]
    [InlineData("a", "z")]
    [InlineData("z", "a")]
    public void MutuallyEmbeddedPartsKeepRelativePoseWhileFallingTogether(string firstId, string secondId)
    {
        var template=Box(new(.1,.1,.1)).Parts[0];
        BoxAssembly Shape(bool first) => new([template with {Id=new("left"),Center=new(-1,first?.1:-.097,0)}, template with {Id=new("right"),Center=new(1,first?-.097:.1,0)}]);
        TurnableDefinition Item(string id,bool first) => new(new(id),new(default,R4),S,new("I"),new("b"),Shape(first),new(true,false,2,Axis3.X,new(-1.08,0,0),2.18));
        var a=Item(firstId,true); var b=Item(secondId,false);
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-10.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(3,.05,3)));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-4,4),S)],[a,b,floor],[],[],[]))).World;
        Assert.IsType<StepResult.Advanced>(world.Step(.1));
        Assert.Equal(-9.803,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(a.Id)).Entity.Pose.Position.Y,9);
        Assert.Equal(-9.803,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(b.Id)).Entity.Pose.Position.Y,9);
        Assert.Empty(Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events);
    }
    [Fact]
    public void MutualPotentialSupportKeepsIndependentFallDistances()
    {
        var template=Box(new(.1,.1,.1)).Parts[0];
        BoxAssembly Shape(bool first) => new([template with {Id=new("left"),Center=new(-1,first?1:-1,0)}, template with {Id=new("right"),Center=new(1,first?-1:1,0)}]);
        TurnableDefinition Item(string id,bool first) => new(new(id),new(default,R4),S,new("I"),new("b"),Shape(first),new(true,false,2,Axis3.X,new(-1.08,0,0),2.18));
        var a=Item("z",true); var b=Item("a",false);
        var floor=new BuildingElementDefinition(new("floor"),new(new(1,-10.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(.2,.05,.2)));
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-4,4),S)],[a,b,floor],[],[],[]))).World;
        Assert.IsType<StepResult.Advanced>(world.Step(.1));
        Assert.Equal(-8.9,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(a.Id)).Entity.Pose.Position.Y,9);
        Assert.Equal(-10.7,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(b.Id)).Entity.Pose.Position.Y,9);
    }
    [Fact]
    public void ThreeLevelsSettleInOneStepWithOneEventPerEntity()
    {
        var floor=new BuildingElementDefinition(new("floor"),new(new(0,-.05,0,0),R4),S,new("b"),new(-1,1),[],Box(new(2,.05,2)));
        TurnableDefinition Item(string id,double y) => new(new(id),new(new(0,y,0,0),R4),S,new("I"),new("b"),Box(new(.5,.5,.5)),new(true,false,.5,Axis3.X,new(-.48,-.5,0),.98));
        var top=Item("a-top",6); var middle=Item("m-middle",4); var bottom=Item("z-bottom",2);
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-2,2),S)],[floor,top,middle,bottom],[],[],[]))).World;
        var events=Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events;
        Assert.Equal(2.5,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(top.Id)).Entity.Pose.Position.Y,10);
        Assert.Equal(1.5,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(middle.Id)).Entity.Pose.Position.Y,10);
        Assert.Equal(.5,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(bottom.Id)).Entity.Pose.Position.Y,10);
        Assert.Equal(new[]{"a-top","m-middle","z-bottom"},events.Select(e=>Assert.IsType<EntityFell>(e.Event).Entity.Value));
        Assert.Equal(2.5,((EntityFell)events[0].Event).ToY,10);
        Assert.Equal(1.5,((EntityFell)events[1].Event).ToY,10);
        Assert.Equal(.5,((EntityFell)events[2].Event).ToY,10);
    }
    [Fact]
    public void UnanchoredTerminalCycleDoesNotFreezeTheCycleAboveIt()
    {
        var template=Box(new(.1,.1,.1)).Parts[0];
        BoxAssembly Shape(double center,bool first,bool upper) => new([
            template with {Id=new("left"),Center=new(center-1,first?1:-1,0)},
            template with {Id=new("right"),Center=new(center+1,first?-1:1,0)},
            ..(upper ? new[]{template with {Id=new("bridge"),Center=new(-3,3,0)}} : Array.Empty<BoxPart>())]);
        TurnableDefinition Item(string id,double center,bool first,bool upper,double hinge,double reach) =>
            new(new(id),new(default,R4),S,new("I"),new("b"),Shape(center,first,upper),new(true,false,4,Axis3.X,new(hinge,0,0),reach));
        var a=Item("a",-2,true,false,-3.08,2.18); var b=Item("b",-2,false,false,-3.08,2.18);
        var c=Item("c",2,true,true,-3.08,6.18); var d=Item("d",2,false,false,.92,2.18);
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S)],[new(new("b"),new(-8,8),S)],[a,b,c,d],[],[],[]))).World;
        var events=Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events;
        Assert.Equal(0,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(a.Id)).Entity.Pose.Position.Y,9);
        Assert.Equal(0,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(b.Id)).Entity.Pose.Position.Y,9);
        Assert.Equal(-1.8,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(c.Id)).Entity.Pose.Position.Y,9);
        Assert.Equal(-3.6,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(d.Id)).Entity.Pose.Position.Y,9);
        Assert.Equal(new[]{"c","d"},events.Select(e=>Assert.IsType<EntityFell>(e.Event).Entity.Value));
    }
}
