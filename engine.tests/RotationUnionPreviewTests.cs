using FourD;
namespace FourD.Tests;
public partial class RotationTests
{
    private static IReadOnlyList<Polyline> PreviewParts(params BoxPart[] parts)
    {
        var maxX=parts.Max(p=>p.Center.X+Math.Abs(p.Rotation.X.X)*p.HalfSize.X+Math.Abs(p.Rotation.Y.X)*p.HalfSize.Y+Math.Abs(p.Rotation.Z.X)*p.HalfSize.Z);
        var item=new TurnableDefinition(new("item"),new(default,R),S,new("I"),new("b"),new(parts),new(true,false,.5,Axis3.X,new(-.48,0,0),maxX+.48));
        var actor=new Creature3Definition(new("actor"),new(new(0,1,1,0),R),S,new("I"),.2,1.6,false);
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S),new(new("II"),.42,S)],[new(new("b"),new(-2,2),S)],[item,actor],[],[],[]))).World;
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        return Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan,new("I"))).Value.Samples[0].Contours;
    }
    private static BoxPart PreviewPart(string id,Vec3 center,Vec3 half,Rotation3? rotation=null)
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        return new(new(id),center,half,rotation ?? new(new(1,0,0),new(0,1,0),new(0,0,1)),faces);
    }
    private static IEnumerable<(double[] A,double[] B)> Segments(IReadOnlyList<Polyline> lines)
    {
        foreach(var line in lines)
        {
            var count=line.Positions.Count/3;
            for(var i=0;i<count-1+(line.Closed?1:0);i++)
            {
                var j=(i+1)%count;
                yield return (line.Positions.Skip(i*3).Take(3).ToArray(),line.Positions.Skip(j*3).Take(3).ToArray());
            }
        }
    }
    private static bool Covers(IReadOnlyList<Polyline> lines,double x,double y,double z)
    {
        double[] p=[x,y,z];
        return Segments(lines).Any(e=>
        {
            var d=Enumerable.Range(0,3).Select(i=>e.B[i]-e.A[i]).ToArray();
            var q=Enumerable.Range(0,3).Select(i=>p[i]-e.A[i]).ToArray();
            var length=d.Sum(v=>v*v); var t=Enumerable.Range(0,3).Sum(i=>q[i]*d[i])/length;
            return t>=-1e-8 && t<=1+1e-8 && Enumerable.Range(0,3).Sum(i=>Math.Pow(q[i]-t*d[i],2))<1e-16;
        });
    }
    private static void AssertCuboidBoundary(IReadOnlyList<Polyline> lines)
    {
        var segments=Segments(lines).ToArray(); Assert.NotEmpty(segments);
        Assert.Equal(8.8,segments.Sum(e=>Math.Sqrt(Enumerable.Range(0,3).Sum(i=>Math.Pow(e.B[i]-e.A[i],2)))),9);
        double[] half=[.5,.3,.3];
        Assert.All(segments,e=>Assert.True(Enumerable.Range(0,3).Count(i=>Math.Abs(e.A[i]-e.B[i])<1e-8 && Math.Abs(Math.Abs(e.A[i])-half[i])<1e-8)>=2));
        for(int varying=0;varying<3;varying++) foreach(var sign1 in new[]{-1,1}) foreach(var sign2 in new[]{-1,1})
        {
            var fixedAxes=Enumerable.Range(0,3).Where(i=>i!=varying).ToArray(); var p=new double[3];
            p[varying]=.031; p[fixedAxes[0]]=sign1*half[fixedAxes[0]];p[fixedAxes[1]]=sign2*half[fixedAxes[1]];
            Assert.True(Covers(lines,p[0],p[1],p[2]));
        }
    }
    private static void AssertExtrudedBoundary(IReadOnlyList<Polyline> lines,(double X,double Z)[] vertices)
    {
        var expected=new List<Polyline>();
        void Add(double[] a,double[] b) => expected.Add(new Polyline(a.Concat(b).ToArray(),false));
        for(var i=0;i<vertices.Length;i++)
        {
            var a=vertices[i]; var b=vertices[(i+1)%vertices.Length];
            Add([a.X,-.3,a.Z],[a.X,.3,a.Z]);
            foreach(var y in new[]{-.3,.3}) Add([a.X,y,a.Z],[b.X,y,b.Z]);
        }
        var actual=Segments(lines).ToArray(); Assert.NotEmpty(actual);
        Assert.All(actual,segment=>Assert.Contains(expected,edge=>Covers([edge],segment.A[0],segment.A[1],segment.A[2]) && Covers([edge],segment.B[0],segment.B[1],segment.B[2])));
        foreach(var edge in Segments(expected)) foreach(var t in new[]{.25,.5,.75})
            Assert.True(Covers(lines,edge.A[0]+t*(edge.B[0]-edge.A[0]),edge.A[1]+t*(edge.B[1]-edge.A[1]),edge.A[2]+t*(edge.B[2]-edge.A[2])));
        double Total(IEnumerable<(double[] A,double[] B)> edges) => edges.Sum(e=>Math.Sqrt(Enumerable.Range(0,3).Sum(i=>Math.Pow(e.B[i]-e.A[i],2))));
        Assert.Equal(Total(Segments(expected)),Total(actual),9);
    }
    [Fact]
    public void PreviewOmitsEdgesOfAContainedPart()
    {
        var lines=PreviewParts(PreviewPart("outer",default,new(.5,.3,.3)),PreviewPart("inner",default,new(.1,.1,.1)));
        AssertCuboidBoundary(lines);
    }
    [Fact]
    public void PreviewOmitsCoplanarSeamsOfOverlappingParts()
    {
        var lines=PreviewParts(PreviewPart("left",new(-.2,0,0),new(.3,.3,.3)),PreviewPart("right",new(.2,0,0),new(.3,.3,.3)));
        AssertCuboidBoundary(lines);
    }
    [Fact]
    public void PreviewKeepsNewConcaveEdgesOfLShapedUnion()
    {
        var lines=PreviewParts(PreviewPart("a",new(-.2,0,0),new(.3,.3,.3)),PreviewPart("b",new(.2,0,.2),new(.3,.3,.3)));
        AssertExtrudedBoundary(lines,[(-.5,-.3),(.1,-.3),(.1,-.1),(.5,-.1),(.5,.5),(-.1,.5),(-.1,.3),(-.5,.3)]);
        Assert.True(Covers(lines,.1,0,-.1)); Assert.True(Covers(lines,-.1,0,.3));
        Assert.False(Covers(lines,.1,0,0)); Assert.False(Covers(lines,-.1,0,.1));
        Assert.True(Covers(lines,-.5,0,-.3)); Assert.True(Covers(lines,.5,0,.5));
    }
    [Fact]
    public void PreviewKeepsIntersectionEdgesWithRotatedOverlappingPart()
    {
        var c=Math.Sqrt(.5); var rotation=new Rotation3(new(c,0,-c),new(0,1,0),new(c,0,c));
        var lines=PreviewParts(PreviewPart("a",default,new(.5,.3,.3)),PreviewPart("b",new(.4,0,0),new(.2,.3,.2),rotation));
        var z=.2*Math.Sqrt(2)-.1;
        AssertExtrudedBoundary(lines,[(-.5,-.3),(.5,-.3),(.5,-z),(.4+.2*Math.Sqrt(2),0),(.5,z),(.5,.3),(-.5,.3)]);
        Assert.True(Covers(lines,.5,0,z)); Assert.True(Covers(lines,.5,0,-z));
        Assert.False(Covers(lines,.5,0,0)); Assert.True(Covers(lines,.4+.2*Math.Sqrt(2),0,0));
    }
    [Fact]
    public void PreviewOfObliqueSlicesOmitsContainedPartEdges()
    {
        var parts=new[]{PreviewPart("outer",default,new(.5,.3,.3)),PreviewPart("inner",default,new(.1,.1,.1))};
        var item=new TurnableDefinition(new("item"),new(default,R),S,new("I"),new("b"),new(parts),new(true,false,.5,Axis3.X,new(-.48,0,0),.98));
        var actor=new Creature3Definition(new("actor"),new(new(0,1,1,0),R),S,new("I"),.2,1.6,false);
        var world=Assert.IsType<LoadResult.Loaded>(World.Load(new([new(new("I"),0,S),new(new("middle"),.3,S),new(new("II"),.42,S)],[new(new("b"),new(-2,2),S)],[item,actor],[],[],[]))).World;
        var plan=Assert.IsType<RotationPlanResult.Allowed>(world.PlanRotation(new StandUp(actor.Id,item.Id,new("II")))).Plan;
        var preview=Assert.IsType<PreviewResult.Preview>(world.GetRotationPreview(plan,new("middle"))).Value;
        var lines=preview.Samples.Single(s=>s.Progress==.5).Contours;
        var minX=-.18-.006*Math.Sqrt(2); var maxX=-.18+.006*Math.Sqrt(2);
        var segments=Segments(lines).ToArray(); Assert.NotEmpty(segments);
        Assert.All(segments,e=>
        {
            var xEdge=Math.Abs(e.A[0]-e.B[0])<1e-8 && (Math.Abs(e.A[0]-minX)<1e-8 || Math.Abs(e.A[0]-maxX)<1e-8);
            var yEdge=Math.Abs(e.A[1]-e.B[1])<1e-8 && Math.Abs(Math.Abs(e.A[1])-.3)<1e-8;
            var zEdge=Math.Abs(e.A[2]-e.B[2])<1e-8 && Math.Abs(Math.Abs(e.A[2])-.3)<1e-8;
            Assert.True((xEdge?1:0)+(yEdge?1:0)+(zEdge?1:0)>=2);
        });
        Assert.True(Covers(lines,minX,0,-.3)); Assert.True(Covers(lines,maxX,0,.3));
    }
    [Fact]
    public void PreviewOmitsSharedFaceOfAdjacentParts()
    {
        var lines=PreviewParts(PreviewPart("left",new(-.25,0,0),new(.25,.3,.3)),PreviewPart("right",new(.25,0,0),new(.25,.3,.3)));
        AssertCuboidBoundary(lines);
    }
    [Fact]
    public void PreviewFindsEdgesAtNarrowAngledIntersections()
    {
        var angle=Math.PI/180; var c=Math.Cos(angle); var sn=Math.Sin(angle);
        var rotation=new Rotation3(new(c,0,-sn),new(0,1,0),new(sn,0,c));
        var lines=PreviewParts(PreviewPart("a",default,new(.5,.3,.3)),PreviewPart("b",new(.2,0,0),new(.3,.3,.3),rotation));
        var lower=(.3*c-.3)/sn; var upper=(.3-.3*sn)/c;
        Assert.True(Covers(lines,.5,0,lower)); Assert.True(Covers(lines,.5,0,upper));
        Assert.False(Covers(lines,.5,0,0));
        Assert.True(Covers(lines,.2+.3*(c+sn),0,.3*(c-sn)));
    }
    [Fact]
    public void PreviewDoesNotDuplicateCoincidentPartEdges()
    {
        var lines=PreviewParts(PreviewPart("a",default,new(.5,.3,.3)),PreviewPart("b",default,new(.5,.3,.3)));
        AssertCuboidBoundary(lines);
    }
}
