namespace FourD;

// The slice of one convex piece of a loft by the hyperplane at world W, as unindexed triangles with a PartId for each
// vertex. Each facet that the hyperplane cuts leaves one face of the slice's polyhedron.
internal static class LoftSlice
{
    internal static void Append(Loft4 piece, double w, SliceBuffer buffer)
    {
        var corners = piece.Corners();
        var (low, high) = (corners.Min(corner => corner[3]), corners.Max(corner => corner[3]));
        if (w < low || w > high) return;
        // When the cap at the upper key lies in the section and the next piece starts with the same shape, that piece
        // cuts it. Which key is on which side in W depends on how the device is turned, so the cap itself is asked.
        var endCap = piece.Piece.Facets.Single(facet => facet.Cap == FacetCap.End);
        if (piece.Piece.CoveredAtEnd && endCap.Vertices.All(vertex => corners[vertex][3] == w)) return;

        // Each point of the slice is made once: a vertex that lies in the hyperplane, or where an edge crosses it.
        var points = new Dictionary<(int, int), BoxSlice.Point>();
        var local = piece.Piece.Vertices;
        BoxSlice.Point Vertex(int vertex) => points.TryGetValue((vertex, vertex), out var known)
            ? known
            : points[(vertex, vertex)] = new(local[vertex], corners[vertex]);
        BoxSlice.Point Crossing(int first, int second)
        {
            if (first > second) (first, second) = (second, first);
            if (points.TryGetValue((first, second), out var known)) return known;
            var t = (w - corners[first][3]) / (corners[second][3] - corners[first][3]);
            double[] Between(double[][] at) => Enumerable.Range(0, 4).Select(i => at[first][i] + t * (at[second][i] - at[first][i])).ToArray();
            return points[(first, second)] = new(Between(local), Between(corners));
        }
        BoxSlice.Point[] CutBy(IEnumerable<int> vertices, IEnumerable<(int First, int Second)> edges) =>
        [
            .. vertices.Where(vertex => corners[vertex][3] == w).Select(Vertex),
            .. edges.Where(edge => (corners[edge.First][3] < w) != (corners[edge.Second][3] < w) && corners[edge.First][3] != w && corners[edge.Second][3] != w)
                .Select(edge => Crossing(edge.First, edge.Second))
        ];

        if (!HasVolume(CutBy(Enumerable.Range(0, corners.Length), piece.Piece.Edges))) return;
        for (var i = 0; i < piece.Piece.Facets.Length; i++)
        {
            var facet = piece.Piece.Facets[i];
            if ((facet.Cap == FacetCap.Start && piece.Piece.CoveredAtStart) || (facet.Cap == FacetCap.End && piece.Piece.CoveredAtEnd)) continue;
            // A facet parallel to the hyperplane is the slice itself, whose faces come from its neighbors.
            var normal = piece.Normals[i].Take(3).ToArray();
            var length = Math.Sqrt(Box4.Dot3(normal, normal));
            if (length <= World.RoundingMargin) continue;
            normal = [.. normal.Select(value => value / length)];

            var polygon = CutBy(facet.Vertices, facet.Edges).ToList();
            if (polygon.Count < 3) continue;
            BoxSlice.SortAroundCenter(polygon, normal);
            foreach (var vertex in BoxSlice.FanVertices(polygon, normal))
            {
                buffer.Positions.AddRange(vertex.World.Take(3));
                buffer.Normals.AddRange(normal);
                buffer.Uvs.AddRange(BoxSlice.Uv(vertex.Local, facet.Uv is null ? 3 : (int)facet.Uv.Axis, facet.Uv?.Positive ?? true, facet.Uv));
                buffer.Parts.Add(piece.Part);
            }
        }
    }

    // Whether the points span three dimensions, by Gram-Schmidt on their offsets from the first.
    private static bool HasVolume(BoxSlice.Point[] points)
    {
        var basis = new List<double[]>();
        foreach (var point in points)
        {
            var offset = Enumerable.Range(0, 3).Select(i => point.World[i] - points[0].World[i]).ToArray();
            foreach (var unit in basis)
            {
                var along = Box4.Dot3(offset, unit);
                offset = [.. offset.Select((value, i) => value - along * unit[i])];
            }
            var length = Math.Sqrt(Box4.Dot3(offset, offset));
            if (length > World.RoundingMargin) basis.Add([.. offset.Select(value => value / length)]);
        }
        return basis.Count == 3;
    }
}
