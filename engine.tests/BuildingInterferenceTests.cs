using FourD;
namespace FourD.Tests;
public class BuildingInterferenceTests
{
    private static readonly Source S=new("/object","definition");
    private static readonly Rotation3 R3=new(new(1,0,0),new(0,1,0),new(0,0,1));
    private static readonly Rotation4 R4=new(new(1,0,0,0),new(0,1,0,0),new(0,0,1,0),new(0,0,0,1));
    private static BoxAssembly Box(Vec3 half) => new([new(new("part"),default,half,R3,Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray())]);
    private static TurnableDefinition Item() => new(new("item"),new(default,R4),S,new("I"),new("owner"),Box(new(.5,.1,.1)),new(true,false,.5,Axis3.X,new(-.48,-.1,0),.98));
    private static BuildingElementDefinition Other(double x,Interval range,Interval[]? gaps=null) => new(new("wall"),new(new(x,0,0,0),R4),new("/wall","shape"),new("other"),range,gaps??[],Box(new(.1,.1,.1)));
    private static WorldDescription Description(params EntityDefinition[] entities) => new([new(new("I"),0,S)],[new(new("owner"),new(-2,2),S),new(new("other"),new(-2,2),S)],entities,[],[],[]);
    [Theory]
    [InlineData(.5,1.5)]
    [InlineData(-1.5,-.5)]
    public void UprightObjectInterferingWithOtherBuildingsSolidIsRejected(double min,double max)
    {
        var result=Assert.IsType<LoadResult.Invalid>(World.Load(Description(Item(),Other(-.48,new(min,max)))));
        var error=Assert.Single(result.Errors,e=>e.Code==LoadErrorCode.BuildingInterference);
        Assert.Equal(new EntityId("item"),error.Entity); Assert.EndsWith(".Building",error.Source.Property);
        var contact=Assert.Single(error.Contacts);
        Assert.Equal(new EntityId("wall"),contact.Obstacle);
        Assert.InRange(contact.Point.X,-.486,-.474); Assert.InRange(contact.Point.W,min,max);
    }
    [Fact]
    public void OverlappingBuildingRangesWithDistantGeometryAreAllowed()
    {
        Assert.IsType<LoadResult.Loaded>(World.Load(Description(Item(),Other(10,new(.5,1.5)))));
    }
    [Fact]
    public void BuildingGapDoesNotBlockTheUprightObject()
    {
        Assert.IsType<LoadResult.Loaded>(World.Load(Description(Item(),Other(-.48,new(.5,1.5),[new(.5,.99)]))));
    }
    [Fact]
    public void DynamicAdditionUsesTheSameOtherBuildingValidation()
    {
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(Description(Other(-.48,new(.5,1.5))))).World;
        var rejected=Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(Item()));
        Assert.Equal(FailureCode.InvalidInput,rejected.Failure.Code);
        Assert.Equal(new EntityId("wall"),Assert.Single(rejected.Failure.Contacts).Obstacle);
        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(Item().Id));
    }
    [Fact]
    public void RotatedInitialPoseAndLongAxisUseTheActualHinge()
    {
        var item=Item() with {Pose=new(default,new(new(0,1,0,0),new(-1,0,0,0),R4.Z,R4.W))};
        var obstacle=Other(0,new(.5,1.5)) with {Pose=new(new(0,-.48,0,0),R4)};
        var invalid=Assert.IsType<LoadResult.Invalid>(World.Load(Description(item,obstacle)));
        var error=Assert.Single(invalid.Errors,e=>e.Code==LoadErrorCode.BuildingInterference);
        var contact=Assert.Single(error.Contacts);
        Assert.InRange(contact.Point.Y,-.486,-.474); Assert.InRange(contact.Point.W,.5,.98);
    }
    [Fact]
    public void LongAxisYUsesItsOwnHingeCoordinate()
    {
        var item=Item() with {Shape=Box(new(.1,.5,.1)),Interaction=new(true,false,.5,Axis3.Y,new(-.1,-.48,0),.98)};
        var obstacle=Other(0,new(.5,1.5)) with {Pose=new(new(0,-.48,0,0),R4)};
        Assert.Contains(Assert.IsType<LoadResult.Invalid>(World.Load(Description(item,obstacle))).Errors,e=>e.Code==LoadErrorCode.BuildingInterference);
    }
    [Theory]
    [InlineData(-.378, false)]
    [InlineData(-.380, true)]
    public void InterferenceHonorsFiveMillimeterContactTolerance(double wallX,bool blocked)
    {
        var result=World.Load(Description(Item(),Other(wallX,new(.5,1.5))));
        Assert.Equal(blocked,result is LoadResult.Invalid invalid && invalid.Errors.Any(e=>e.Code==LoadErrorCode.BuildingInterference));
    }
    [Fact]
    public void ObstaclesInTheOwnerBuildingDoNotTriggerOtherBuildingInterference()
    {
        var sameBuilding=Other(-.48,new(.5,1.5)) with {Building=new("owner")};
        Assert.IsType<LoadResult.Loaded>(World.Load(Description(Item(),sameBuilding)));
    }
}
