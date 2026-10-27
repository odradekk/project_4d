namespace FourD;

// Computes the boundary edges of the union of the current box slices.
internal static class BoxUnionOutline
{
    private const double Tolerance=1e-10;
    private sealed record Plane(double[] Normal,double Limit);
    private sealed record Face(int Solid,Plane Plane);
    private static double Dot(double[] a,double[] b) => a[0]*b[0]+a[1]*b[1]+a[2]*b[2];
    private static double[] Cross(double[] a,double[] b) => [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]];
    private static double[] Add(double[] a,double[] b,double scale) => [a[0]+b[0]*scale,a[1]+b[1]*scale,a[2]+b[2]*scale];
    private static double Length(double[] a) => Math.Sqrt(Dot(a,a));
    private static double[] Scale(double[] a,double factor) => [a[0]*factor,a[1]*factor,a[2]*factor];

    internal static IReadOnlyList<Polyline> At(IEnumerable<Box4> boxes,double w)
    {
        var solids=boxes.Select(box=>Slice(box,w)).OfType<Plane[]>().ToArray();
        var faces=solids.SelectMany((planes,index)=>planes.Select(plane=>new Face(index,plane))).ToArray();
        var segments=new Dictionary<string,Polyline>();
        for(var i=0;i<faces.Length;i++) for(var j=i+1;j<faces.Length;j++)
        {
            var a=faces[i]; var b=faces[j]; var cross=Cross(a.Plane.Normal,b.Plane.Normal); var length=Length(cross);
            if(length<=Tolerance) continue;
            var direction=Scale(cross,1/length);
            var point=Scale(Add(Scale(Cross(b.Plane.Normal,cross),a.Plane.Limit),Cross(cross,a.Plane.Normal),b.Plane.Limit),1/(length*length));
            var first=Clip(solids[a.Solid],point,direction); var second=Clip(solids[b.Solid],point,direction);
            if(first is null || second is null) continue;
            var low=Math.Max(first.Value.Low,second.Value.Low); var high=Math.Min(first.Value.High,second.Value.High);
            if(high-low<=Tolerance) continue;
            var cuts=new List<double>{low,high};
            foreach(var solid in solids)
            {
                var interval=Clip(solid,point,direction); if(interval is null) continue;
                if(interval.Value.Low>low+Tolerance && interval.Value.Low<high-Tolerance) cuts.Add(interval.Value.Low);
                if(interval.Value.High>low+Tolerance && interval.Value.High<high-Tolerance) cuts.Add(interval.Value.High);
            }
            cuts.Sort();
            for(var k=0;k+1<cuts.Count;k++)
            {
                var left=cuts[k]; var right=cuts[k+1]; if(right-left<=Tolerance) continue;
                var middle=Add(point,direction,(left+right)/2);
                if(!IsEdge(solids,middle,direction)) continue;
                var start=Add(point,direction,left); var end=Add(point,direction,right);
                var key=Key(start,end); segments.TryAdd(key,new Polyline(Array.AsReadOnly(start.Concat(end).ToArray()),false));
            }
        }
        return Array.AsReadOnly(segments.OrderBy(e=>e.Key,StringComparer.Ordinal).Select(e=>e.Value).ToArray());
    }
    private static Plane[]? Slice(Box4 box,double w)
    {
        var radius=box.Radius([0,0,0,1]); var min=box.Center[3]-radius; var max=box.Center[3]+radius;
        if(w<min || w>max || ((w==min || w==max) && box.Axes.Count(a=>a[3]!=0)!=1)) return null;
        var planes=new List<Plane>();
        foreach(var (normal,limit) in box.Planes())
        {
            double[] n=[normal[0],normal[1],normal[2]]; var length=Length(n); var d=limit-normal[3]*w;
            if(length<=1e-14) {if(d < -Tolerance) return null; continue;}
            planes.Add(new(Scale(n,1/length),d/length));
        }
        return planes.ToArray();
    }
    private static (double Low,double High)? Clip(Plane[] solid,double[] point,double[] direction)
    {
        var low=double.NegativeInfinity; var high=double.PositiveInfinity;
        foreach(var plane in solid)
        {
            var denominator=Dot(plane.Normal,direction); var distance=plane.Limit-Dot(plane.Normal,point);
            if(Math.Abs(denominator)<=Tolerance) {if(distance < -Tolerance) return null; continue;}
            var t=distance/denominator;
            if(denominator>0) high=Math.Min(high,t); else low=Math.Max(low,t);
            if(low>high+Tolerance) return null;
        }
        return (low,high);
    }
    private static bool IsEdge(Plane[][] solids,double[] point,double[] direction)
    {
        var active=new List<Plane[]>();
        foreach(var solid in solids)
        {
            if(solid.Any(p=>Dot(p.Normal,point)>p.Limit+Tolerance)) continue;
            var boundary=solid.Where(p=>Math.Abs(Dot(p.Normal,point)-p.Limit)<=Tolerance).ToArray();
            if(boundary.Length==0) return false;
            active.Add(boundary);
        }
        if(active.Count==0) return false;
        var reference=Math.Abs(direction[0])<.9 ? new double[]{1,0,0} : new double[]{0,1,0};
        var u=Cross(direction,reference); u=Scale(u,1/Length(u)); var v=Cross(direction,u);
        // Each active face changes inside/outside only at its two tangent directions.
        // Classifying the intervals between those directions also covers narrow wedges.
        var angles=new List<double>();
        foreach(var plane in active.SelectMany(x=>x))
        {
            var x=Dot(plane.Normal,u); var y=Dot(plane.Normal,v);
            if(x*x+y*y<=Tolerance*Tolerance) continue;
            var angle=Math.Atan2(y,x)+Math.PI/2;
            for(var i=0;i<2;i++) {var t=(angle+i*Math.PI)%(2*Math.PI); if(t<0) t+=2*Math.PI; angles.Add(t);}
        }
        angles.Sort(); var unique=new List<double>();
        foreach(var angle in angles) if(unique.Count==0 || angle-unique[^1]>Tolerance) unique.Add(angle);
        if(unique.Count>1 && unique[0]+2*Math.PI-unique[^1]<=Tolerance) unique.RemoveAt(unique.Count-1);
        if(unique.Count<2) return false;
        bool Covered(double angle)
        {
            var ray=Add(Scale(u,Math.Cos(angle)),v,Math.Sin(angle));
            return active.Any(solid=>solid.All(p=>Dot(p.Normal,ray)<0));
        }
        var inside=new bool[unique.Count];
        for(var i=0;i<unique.Count;i++)
        {
            var next=i+1<unique.Count?unique[i+1]:unique[0]+2*Math.PI;
            inside[i]=Covered((unique[i]+next)/2);
        }
        var transitions=new List<double>();
        for(var i=0;i<unique.Count;i++) if(inside[(i+unique.Count-1)%unique.Count]!=inside[i]) transitions.Add(unique[i]);
        return transitions.Count>2 || transitions.Count==2 && Math.Abs(Math.Sin(transitions[0]-transitions[1]))>Tolerance;
    }
    private static string Key(double[] a,double[] b)
    {
        string Point(double[] p) => string.Join(",",p.Select(x=>Math.Round(x,10)).Select(x=>(x==0?0:x).ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        var first=Point(a); var second=Point(b);
        return string.CompareOrdinal(first,second)<0?first+";"+second:second+";"+first;
    }
}
