using FourD;
namespace FourD.Tests;

public class OccupancyTests
{
    private static readonly Source Source = new("/room", "shape");
    private static readonly Rotation3 R3 = new(new(1,0,0), new(0,1,0), new(0,0,1));
    private static readonly Rotation4 R4 = new(new(1,0,0,0), new(0,1,0,0), new(0,0,1,0), new(0,0,0,1));
    private static World World()
    {
        var faces = Enum.GetValues<Axis3>().SelectMany(a => new[] { false, true }.Select(p => new FaceUv(a,p,new(1,1),new(0,0)))).ToArray();
        var part = new BoxPart(new("part"), default, new(1,1,1), R3, faces);
        var wall = new BuildingElementDefinition(new("wall"), new(default,R4), Source, new("b"), new(-1,1), [], new([part]));
        return Assert.IsType<LoadResult.Loaded>(FourD.World.Load(new([new(new("I"),0,Source)], [new(new("b"),new(-2,2),Source)], [wall], [], [], []))).World;
    }
    [Fact]
    public void BoxVolumeIntersectionReturnsRealPoint()
    {
        var result = Assert.IsType<OccupancyResult.Occupancy>(World().QueryOccupancy(new OccupancyBox(new(new(.5,0,0,0),R4),new(.5,.5,.5,.5)), []));
        var hit = Assert.Single(result.Hits);
        Assert.Equal(new EntityId("wall"), hit.Entity);
        Assert.Equal(new PartId("part"), hit.Part);
        Assert.InRange(hit.Point.X,0,1); Assert.InRange(hit.Point.Y,-.5,.5); Assert.InRange(hit.Point.Z,-.5,.5); Assert.InRange(hit.Point.W,-.5,.5);
    }
    [Theory]
    [InlineData(1.494, true)]
    [InlineData(1.495, false)]
    [InlineData(1.496, false)]
    public void FiveMillimeterContactIsNotOccupied(double x, bool occupied)
    {
        var hits = Assert.IsType<OccupancyResult.Occupancy>(World().QueryOccupancy(new OccupancyBox(new(new(x,0,0,0),R4),new(.5,.5,.5,.5)), [])).Hits;
        Assert.Equal(occupied, hits.Count != 0);
    }
    [Fact]
    public void SphereQueryReturnsPointInsideBothShapes()
    {
        var hit = Assert.Single(Assert.IsType<OccupancyResult.Occupancy>(World().QueryOccupancy(new OccupancySphere(new(1.2,0,0,.2), .3), [])).Hits);
        Assert.InRange(hit.Point.X,-1,1); Assert.InRange(hit.Point.W,-1,1);
        Assert.True(Math.Pow(hit.Point.X-1.2,2)+Math.Pow(hit.Point.W-.2,2) <= .09);
        Assert.Empty(Assert.IsType<OccupancyResult.Occupancy>(World().QueryOccupancy(new OccupancySphere(new(1.296,0,0,0), .3), [])).Hits);
    }
    [Fact]
    public void ExclusionsAndInvalidShapesAreChecked()
    {
        var world=World(); var shape=new OccupancySphere(default,.5);
        Assert.Empty(Assert.IsType<OccupancyResult.Occupancy>(world.QueryOccupancy(shape,[new("wall")])).Hits);
        Assert.Equal(FailureCode.UnknownId,Assert.IsType<OccupancyResult.Rejected>(world.QueryOccupancy(shape,[new("missing")])).Failure.Code);
        Assert.Equal(FailureCode.InvalidInput,Assert.IsType<OccupancyResult.Rejected>(world.QueryOccupancy(shape,[new("wall"),new("wall")])).Failure.Code);
        Assert.Equal(FailureCode.InvalidInput,Assert.IsType<OccupancyResult.Rejected>(world.QueryOccupancy(new OccupancySphere(default,0),[])).Failure.Code);
    }
    [Fact]
    public void MixedFourDimensionalAxisSeparatesBoxesEvenWhenFacesOverlap()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var part=new BoxPart(new("part"),default,new(1,1,.25),R3,faces);
        var fixedBox=new FixedDefinition(new("fixed"),new(default,R4),Source,new("I"),new([part]));
        var world=Assert.IsType<LoadResult.Loaded>(FourD.World.Load(new([new(new("I"),0,Source)],[],[fixedBox],[],[],[]))).World;
        var s=Math.Sqrt(.5);
        var rotation=new Rotation4(new(s,s,0,0),new(-.5,.5,s,0),new(.5,-.5,s,0),R4.W);
        var query=new OccupancyBox(new(new(0,2.25,0,0),rotation),new(1,1,.25,.006));
        Assert.Empty(Assert.IsType<OccupancyResult.Occupancy>(world.QueryOccupancy(query,[])).Hits);
    }
    [Fact]
    public void MixedAxisPenetrationWithinToleranceIsNotOccupied()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        var part=new BoxPart(new("part"),default,new(1,1,.25),R3,faces);
        var fixedBox=new FixedDefinition(new("fixed"),new(default,R4),Source,new("I"),new([part]));
        var world=Assert.IsType<LoadResult.Loaded>(FourD.World.Load(new([new(new("I"),0,Source)],[],[fixedBox],[],[],[]))).World;
        var c=Math.Sqrt(.5); var rotation=new Rotation4(new(c,c,0,0),new(-.5,.5,c,0),new(.5,-.5,c,0),R4.W);
        const double shift=.0978087444407;
        var query=new OccupancyBox(new(new(0,2.25-shift*Math.Sqrt(2d/3d),shift/Math.Sqrt(3),0),rotation),new(1,1,.25,.006));
        Assert.Empty(Assert.IsType<OccupancyResult.Occupancy>(world.QueryOccupancy(query,[])).Hits);
    }
}
