namespace FourD;

// The unindexed triangles of a slice as they are collected: three vertices per triangle.
internal sealed class SliceBuffer
{
    internal List<double> Positions { get; } = [];
    internal List<double> Normals { get; } = [];
    internal List<double> Uvs { get; } = [];
    internal List<PartId> Parts { get; } = [];

    internal void AddRange(SliceBuffer other)
    {
        Positions.AddRange(other.Positions);
        Normals.AddRange(other.Normals);
        Uvs.AddRange(other.Uvs);
        Parts.AddRange(other.Parts);
    }

    // Adds a triangle that faces away from the point `from`: wound counterclockwise seen from its far side, with the
    // normal of its plane. The vertices are exchanged when they come in the other order. A triangle without area
    // is left out.
    internal void AddFacingAwayFrom(PartId part, double[] a, double[] b, double[] c, double[] from)
    {
        double ux = b[0] - a[0], uy = b[1] - a[1], uz = b[2] - a[2];
        double vx = c[0] - a[0], vy = c[1] - a[1], vz = c[2] - a[2];
        double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
        var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (length <= World.RoundingMargin * World.RoundingMargin) return;
        var away = new[] { (a[0] + b[0] + c[0]) / 3 - from[0], (a[1] + b[1] + c[1]) / 3 - from[1], (a[2] + b[2] + c[2]) / 3 - from[2] };
        if (nx * away[0] + ny * away[1] + nz * away[2] < 0)
        {
            (b, c) = (c, b);
            (nx, ny, nz) = (-nx, -ny, -nz);
        }
        AddVertex(part, a, nx / length, ny / length, nz / length);
        AddVertex(part, b, nx / length, ny / length, nz / length);
        AddVertex(part, c, nx / length, ny / length, nz / length);
    }

    private void AddVertex(PartId part, double[] vertex, double nx, double ny, double nz)
    {
        Positions.Add(vertex[0]);
        Positions.Add(vertex[1]);
        Positions.Add(vertex[2]);
        Normals.Add(nx);
        Normals.Add(ny);
        Normals.Add(nz);
        Uvs.Add(vertex[0]);
        Uvs.Add(vertex[2]);
        Parts.Add(part);
    }
}
