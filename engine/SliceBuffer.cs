// A point of a slice as it is built. It is a tuple rather than a Vec3 because a Debug build, which the editor runs, reads
// a tuple's fields directly but calls a getter for each of Vec3's properties.
global using Point3 = (double X, double Y, double Z);

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

    // Makes room for this many more triangles at once, so that a large slice is not copied as it grows.
    internal void Reserve(int triangles)
    {
        Positions.EnsureCapacity(Positions.Count + 9 * triangles);
        Normals.EnsureCapacity(Normals.Count + 9 * triangles);
        Uvs.EnsureCapacity(Uvs.Count + 6 * triangles);
        Parts.EnsureCapacity(Parts.Count + 3 * triangles);
    }

    internal SliceGeometry ToSlice(EntityId entity, bool through) =>
        new(entity, Positions.AsReadOnly(), Normals.AsReadOnly(), Uvs.AsReadOnly(), Parts.AsReadOnly(), through);

    // Adds a triangle that faces away from the point `from`: wound counterclockwise seen from its far side, with the
    // normal of its plane. The vertices are exchanged when they come in the other order. A triangle without area
    // is left out.
    internal void AddFacingAwayFrom(PartId part, Point3 a, Point3 b, Point3 c, Point3 from)
    {
        double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
        double vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
        double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
        var length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
        if (length <= World.RoundingMargin * World.RoundingMargin) return;
        double awayX = (a.X + b.X + c.X) / 3 - from.X, awayY = (a.Y + b.Y + c.Y) / 3 - from.Y, awayZ = (a.Z + b.Z + c.Z) / 3 - from.Z;
        if (nx * awayX + ny * awayY + nz * awayZ < 0)
        {
            (b, c) = (c, b);
            (nx, ny, nz) = (-nx, -ny, -nz);
        }
        AddVertex(part, a, nx / length, ny / length, nz / length);
        AddVertex(part, b, nx / length, ny / length, nz / length);
        AddVertex(part, c, nx / length, ny / length, nz / length);
    }

    private void AddVertex(PartId part, Point3 vertex, double nx, double ny, double nz)
    {
        Positions.Add(vertex.X);
        Positions.Add(vertex.Y);
        Positions.Add(vertex.Z);
        Normals.Add(nx);
        Normals.Add(ny);
        Normals.Add(nz);
        Uvs.Add(vertex.X);
        Uvs.Add(vertex.Z);
        Parts.Add(part);
    }
}
