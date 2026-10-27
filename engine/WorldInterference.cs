namespace FourD;

internal static class WorldInterference
{
    internal static IEnumerable<LoadError> Check(WorldDescription description, IReadOnlyList<LoadError> existing)
    {
        var valid=description.Entities?.Where(e=>e is not null && !existing.Any(error=>error.Entity==e.Id)).ToArray() ?? [];
        foreach(var item in valid.OfType<TurnableDefinition>())
        {
            var contacts=new List<Contact>();
            foreach(var sign in new[] {-1d,1d})
            {
                var pose=Upright(item,sign);
                foreach(var part in item.Shape.Parts)
                {
                    var moving=Box4.From(item,pose,part);
                    foreach(var element in valid.OfType<BuildingElementDefinition>().Where(e=>e.Building!=item.Building))
                    {
                        var segments=World.RemainingSegments(element.WRange,element.Gaps);
                        var solid=new StaticEntity(element.Id,element.Pose,element.Shape.Parts.ToArray(),segments,false);
                        foreach(var other in element.Shape.Parts) foreach(var segment in segments)
                        {
                            var obstacle=Box4.From(solid,other,segment);
                            if(!Box4.Overlaps(moving,obstacle,.005)) continue;
                            var point=Box4.IntersectionPoint(moving,obstacle);
                            if(point is null) continue;
                            var delta=Box4.Sub(moving.Center,obstacle.Center);
                            var normal=Box4.SeparatingAxes(moving,obstacle).MinBy(n=>moving.Radius(n)+obstacle.Radius(n)-Math.Abs(Box4.Dot(n,delta)))!;
                            if(Box4.Dot(normal,delta)<0) normal=normal.Select(v=>-v).ToArray();
                            var section=description.Sections?.FirstOrDefault(s=>s is not null && s.W==point[3])?.Id;
                            contacts.Add(new(item.Id,part.Id,element.Id,other.Id,Box4.Vec(point),Box4.Vec(normal),section));
                        }
                    }
                }
            }
            if(contacts.Count>0)
                yield return new(LoadErrorCode.BuildingInterference,item.Source with {Property=$"{item.Source.Property}.Building"},item.Id,null,null,
                    "Turned object intersects another building.",Array.AsReadOnly(contacts.OrderBy(c=>c.Obstacle.Value,StringComparer.Ordinal).ThenBy(c=>c.ObstaclePart.Value,StringComparer.Ordinal).ThenBy(c=>c.MovingPart.Value,StringComparer.Ordinal).ToArray()));
        }
    }
    private static Pose4 Upright(TurnableDefinition item,double sign)
    {
        var rotation=item.Pose.Rotation;
        var axes=new[] {rotation.X,rotation.Y,rotation.Z};
        var index=(int)item.Interaction.LongAxis;
        var old=axes[index]; var w=rotation.W;
        axes[index]=new(sign*w.X,sign*w.Y,sign*w.Z,sign*w.W);
        var newW=new Vec4(-sign*old.X,-sign*old.Y,-sign*old.Z,-sign*old.W);
        var hinge=item.Interaction.Hinge;
        var distance=index==0?hinge.X:index==1?hinge.Y:hinge.Z;
        var p=item.Pose.Position;
        var position=new Vec4(p.X+distance*(old.X-axes[index].X),p.Y+distance*(old.Y-axes[index].Y),
            p.Z+distance*(old.Z-axes[index].Z),p.W+distance*(old.W-axes[index].W));
        return new(position,new(axes[0],axes[1],axes[2],newW));
    }
}
