namespace FourD;

// Meshes the surface of a region on a grid of cubes by marching tetrahedra, each cube split in the same fixed way
// into six tetrahedra. Rings that tilt into W and metaball bodies are meshed this way (docs/frame/contract-views.md),
// each on a grid of its own.
internal sealed class Marching(
    PartId part, double[] values, Func<int, double[]> position, Func<int, int, double[]> cut, SliceBuffer buffer)
{
    // The six tetrahedra of a cube, each running along one of its diagonals from corner 0 to corner 7.
    // Corner i has bit 0 for x, bit 1 for y and bit 2 for z.
    private static readonly int[][] Tetrahedra =
    [
        [0, 1, 3, 7], [0, 1, 5, 7], [0, 2, 3, 7], [0, 2, 6, 7], [0, 4, 5, 7], [0, 4, 6, 7]
    ];

    // Adds the triangles of the surface of the region where `values` is positive, facing out of the region. The
    // grid has counts[0] x counts[1] x counts[2] nodes, and node (x, y, z) has the index (x * counts[1] + y) * counts[2] + z.
    // `position` gives the place of a node, and `cut` the point of the surface on the edge from a node inside the
    // region to one outside it. Tetrahedra that share an edge each ask for its cut, so a `cut` that is costly to
    // compute should remember its points.
    internal static void Surface(
        PartId part, int[] counts, double[] values, Func<int, double[]> position, Func<int, int, double[]> cut, SliceBuffer buffer)
    {
        var marching = new Marching(part, values, position, cut, buffer);
        var cornerOffsets = Enumerable.Range(0, 8)
            .Select(i => (i & 1) * counts[1] * counts[2] + (i >> 1 & 1) * counts[2] + (i >> 2 & 1))
            .ToArray();
        for (var x = 0; x < counts[0] - 1; x++)
            for (var y = 0; y < counts[1] - 1; y++)
                for (var z = 0; z < counts[2] - 1; z++)
                {
                    var corner = (x * counts[1] + y) * counts[2] + z;
                    var inside = 0;
                    foreach (var offset in cornerOffsets)
                        if (values[corner + offset] > 0) inside++;
                    if (inside == 0 || inside == cornerOffsets.Length) continue;
                    foreach (var tetrahedron in Tetrahedra)
                        marching.Tetrahedron(
                            corner + cornerOffsets[tetrahedron[0]], corner + cornerOffsets[tetrahedron[1]],
                            corner + cornerOffsets[tetrahedron[2]], corner + cornerOffsets[tetrahedron[3]]);
                }
    }

    // Adds the triangles where the surface, on the edges between corners inside and outside, cuts the tetrahedron.
    private void Tetrahedron(int a, int b, int c, int d)
    {
        Span<int> corners = [a, b, c, d];
        Span<int> inside = stackalloc int[4];
        Span<int> outside = stackalloc int[4];
        int insideCount = 0, outsideCount = 0;
        foreach (var corner in corners)
            if (values[corner] > 0) inside[insideCount++] = corner;
            else outside[outsideCount++] = corner;
        if (insideCount is 0 or 4) return;

        switch (insideCount)
        {
            case 1:
                Triangle(inside[0], cut(inside[0], outside[0]), cut(inside[0], outside[1]), cut(inside[0], outside[2]));
                break;
            case 3:
                Triangle(inside[0], cut(inside[0], outside[0]), cut(inside[1], outside[0]), cut(inside[2], outside[0]));
                break;
            default:
                double[] p = cut(inside[0], outside[0]), q = cut(inside[0], outside[1]);
                double[] r = cut(inside[1], outside[1]), s = cut(inside[1], outside[0]);
                Triangle(inside[0], p, q, r);
                Triangle(inside[0], p, r, s);
                break;
        }
    }

    // The surface faces away from the corner inside it.
    private void Triangle(int insideCorner, double[] a, double[] b, double[] c) =>
        buffer.AddFacingAwayFrom(part, a, b, c, position(insideCorner));
}
