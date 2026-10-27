using FourD;
namespace FourD.Tests;
public class TurnableTests
{
    private static readonly Source S = new("/object","definition");
    private static readonly Rotation3 R3 = new(new(1,0,0),new(0,1,0),new(0,0,1));
    private static readonly Rotation4 R4 = new(new(1,0,0,0),new(0,1,0,0),new(0,0,1,0),new(0,0,0,1));
    private static TurnableDefinition Object()
    {
        var faces=Enum.GetValues<Axis3>().SelectMany(a=>new[]{false,true}.Select(p=>new FaceUv(a,p,new(1,1),default))).ToArray();
        return new(new("object"),new(default,R4),S,new("I"),new("b"),new([new(new("body"),default,new(.5,.1,.1),R3,faces)]),new(true,false,.5,Axis3.X,new(-.48,-.1,0),.98));
    }
    private static World World() => Assert.IsType<LoadResult.Loaded>(FourD.World.Load(new([new(new("I"),0,S),new(new("II"),.42,S)],[new(new("b"),new(-2,2),S)],[],[],[],[]))).World;
    [Fact]
    public void AddedObjectIsImmediatelyReadableAndVisible()
    {
        var world=World(); var definition=Object();
        var added=Assert.IsType<AddEntityResult.Added>(world.AddEntity(definition));
        Assert.Equal(definition.Id,added.Entity.Id);
        Assert.Equal(definition.Pose,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(definition.Id)).Entity.Pose);
        Assert.Single(Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(new("I"))).Entities);
    }
    [Fact]
    public void PlacementTiltsFourDimensionalBoxAndPreservesOrigin()
    {
        var world=World(); var obj=Object(); Assert.IsType<AddEntityResult.Added>(world.AddEntity(obj));
        var turn=new Rotation4(new(0,0,0,1),R4.Y,R4.Z,new(-1,0,0,0));
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(obj.Id,new(new(0,0,0,.3),turn)));
        var state=Assert.IsType<EntityReadResult.Found>(world.ReadEntity(obj.Id)).Entity;
        Assert.Null(state.Section); Assert.Equal(new SectionId("I"),state.UprightOrigin);
        var geometry=Assert.Single(Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(new("I"))).Entities);
        Assert.Equal(-.006,geometry.Positions.Where((_,i)=>i%3==0).Min(),10);
        Assert.Equal(.006,geometry.Positions.Where((_,i)=>i%3==0).Max(),10);
        Assert.True(Assert.IsType<SlicePresenceResult.Present>(world.HasSlice(obj.Id,new("I"))).HasVolume);
    }
    [Fact]
    public void ObliqueCutHasHandCalculatedBoundsAndOutwardTriangles()
    {
        var world=World(); var obj=Object(); Assert.IsType<AddEntityResult.Added>(world.AddEntity(obj));
        var c=Math.Sqrt(.5); var rotation=new Rotation4(new(c,0,0,c),R4.Y,R4.Z,new(-c,0,0,c));
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(obj.Id,new(new(0,0,0,.21),rotation)));
        var mesh=Assert.Single(Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(new("I"))).Entities);
        Assert.Equal(-.21-.006*Math.Sqrt(2),mesh.Positions.Where((_,i)=>i%3==0).Min(),10);
        Assert.Equal(-.21+.006*Math.Sqrt(2),mesh.Positions.Where((_,i)=>i%3==0).Max(),10);
        Assert.Equal(-.1,mesh.Positions.Where((_,i)=>i%3==1).Min(),10);
        Assert.Equal(.1,mesh.Positions.Where((_,i)=>i%3==2).Max(),10);
        for(int i=0;i<mesh.Parts.Count;i+=3)
        {
            var x=mesh.Positions; var n=mesh.Normals; var j=i*3;
            var ax=x[j+3]-x[j]; var ay=x[j+4]-x[j+1]; var az=x[j+5]-x[j+2];
            var bx=x[j+6]-x[j]; var by=x[j+7]-x[j+1]; var bz=x[j+8]-x[j+2];
            Assert.True((ay*bz-az*by)*n[j]+(az*bx-ax*bz)*n[j+1]+(ax*by-ay*bx)*n[j+2]>0);
        }
        var endpoint=.21+c*(.5+.006);
        Assert.Empty(Assert.IsType<SectionGeometryResult.Geometry>(world.GetGeometryAtW(endpoint+1e-8)).Entities);
    }
    [Fact]
    public void RemoveRetiresIdAndDeliversEventsOnce()
    {
        var world=World(); var obj=Object();
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(obj));
        Assert.IsType<RemoveEntityResult.Removed>(world.RemoveEntity(obj.Id));
        Assert.IsType<EntityReadResult.Missing>(world.ReadEntity(obj.Id));
        Assert.Empty(Assert.IsType<SectionGeometryResult.Geometry>(world.GetSectionGeometry(new("I"))).Entities);
        Assert.Equal(FailureCode.DuplicateId,Assert.IsType<AddEntityResult.Rejected>(world.AddEntity(obj)).Failure.Code);
        Assert.Equal(FailureCode.InvalidInput,Assert.IsType<StepResult.Rejected>(world.Step(0)).Failure.Code);
        var events=Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events;
        Assert.Equal(2,events.Count); Assert.IsType<EntityAdded>(events[0].Event); Assert.IsType<EntityRemoved>(events[1].Event);
        Assert.True(events[0].Sequence<events[1].Sequence); Assert.Equal(0,events[0].Time);
        Assert.Empty(Assert.IsType<StepResult.Advanced>(world.Step(.1)).Events);
    }
    [Fact]
    public void SourceSectionCannotChangeByFlatPlacementAndFailureIsAtomic()
    {
        var world=World(); var obj=Object(); Assert.IsType<AddEntityResult.Added>(world.AddEntity(obj));
        var result=Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(obj.Id,new(new(0,0,0,.42),R4)));
        Assert.Equal(FailureCode.WrongSection,result.Failure.Code);
        Assert.Equal(obj.Pose,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(obj.Id)).Entity.Pose);
    }
    [Fact]
    public void ObliqueSideAndEndCapUseTheirDocumentedLocalUv()
    {
        var world=World(); var obj=Object();
        var part=obj.Shape.Parts[0];
        obj=obj with {Shape=new([part with {FaceUvs=part.FaceUvs.Select(f=>f with {Scale=new(2,3),Offset=new(.25,.5)}).ToArray()}])};
        Assert.IsType<AddEntityResult.Added>(world.AddEntity(obj));
        var c=Math.Sqrt(.5); var rotation=new Rotation4(new(c,0,0,c),R4.Y,R4.Z,new(-c,0,0,c));
        Assert.IsType<UpdateResult.Updated>(world.PlaceEntity(obj.Id,new(new(0,0,0,.21),rotation)));
        var w=.21+.5*c;
        var mesh=Assert.Single(Assert.IsType<SectionGeometryResult.Geometry>(world.GetGeometryAtW(w)).Entities);
        var side=Enumerable.Range(0,mesh.Parts.Count).Where(i=>mesh.Normals[i*3]>.9).ToArray();
        var cap=Enumerable.Range(0,mesh.Parts.Count).Where(i=>mesh.Normals[i*3]<-.9).ToArray();
        Assert.NotEmpty(side); Assert.NotEmpty(cap);
        Assert.All(side,i=> {Assert.Equal(mesh.Positions[i*3+2]*2+.25,mesh.Uvs[i*2],9); Assert.Equal(mesh.Positions[i*3+1]*3+.5,mesh.Uvs[i*2+1],9);});
        Assert.All(cap,i=> {Assert.Equal((mesh.Positions[i*3]+w-.21)/(2*c),mesh.Uvs[i*2],9); Assert.Equal(mesh.Positions[i*3+2],mesh.Uvs[i*2+1],9);});
    }
    [Fact]
    public void UprightEndTooCloseToSectionIsRejected()
    {
        var world=World(); var obj=Object(); Assert.IsType<AddEntityResult.Added>(world.AddEntity(obj));
        var turn=new Rotation4(new(0,0,0,1),R4.Y,R4.Z,new(-1,0,0,0));
        var result=Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(obj.Id,new(new(0,0,0,-.08),turn)));
        Assert.Equal(FailureCode.InvalidInput,result.Failure.Code);
        Assert.Equal(obj.Pose,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(obj.Id)).Entity.Pose);
    }
    [Fact]
    public void PlacementRejectsUnrepresentableGeometryWithoutMutatingState()
    {
        var world=World(); var obj=Object(); Assert.IsType<AddEntityResult.Added>(world.AddEntity(obj));
        var result=Assert.IsType<UpdateResult.Rejected>(world.PlaceEntity(obj.Id,new(new(1e30,0,0,0),R4)));
        Assert.Equal(FailureCode.InvalidInput,result.Failure.Code);
        Assert.Equal(obj.Pose,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(obj.Id)).Entity.Pose);
    }
    [Fact]
    public void LoadRejectsTurnableWhoseWThicknessCollapsesAtCoordinatePrecision()
    {
        var item=Object() with {Pose=new(new(0,0,0,1e20),R4)};
        var result=Assert.IsType<LoadResult.Invalid>(FourD.World.Load(new([new(new("I"),1e20,S)],[new(new("b"),new(-1e21,1e21),S)],[item],[],[],[])));
        Assert.Contains(result.Errors,e=>e.Code==LoadErrorCode.InvalidGeometry && e.Source.Property.EndsWith("Pose.Position.W"));
    }
    [Fact]
    public void ReadFixedEntityReportsHandednessFromItsPose()
    {
        var obj=Object(); var mirror=new Rotation4(new(-1,0,0,0),R4.Y,R4.Z,new(0,0,0,-1));
        var fixedItem=new FixedDefinition(new("fixed"),new(default,mirror),S,new("I"),obj.Shape);
        var world=Assert.IsType<LoadResult.Loaded>(FourD.World.Load(new([new(new("I"),0,S)],[],[fixedItem],[],[],[]))).World;
        Assert.Equal(Handedness.Mirrored,Assert.IsType<EntityReadResult.Found>(world.ReadEntity(fixedItem.Id)).Entity.Handedness);
    }
}
