using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Regions;

/// <summary>
/// The mountains, the same around the compound and the campus: the prototypes draw them with the same code, about each
/// region's centre.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class City3D
{
    private static readonly Color Sky = new(0xd4d8ddff), Loess = new(0xa89a84ff);

    // Two ridges on each side of the east–west valley, about 1.2 km and 2.3 km out. They ignore the fog and are blended
    // toward the sky by hand, the far ridge more, so they stay visible as hazy ridges past the fogged city.
    private ArrayMesh Mountains()
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var sun = new Vector3(10, 35, 8).Normalized();
        foreach (var (side, dist, height, haze) in new[] { (-1, 1200.0, 520.0, .6f), (-1, 2300, 820, .78f), (1, 1250, 480, .6f), (1, 2400, 780, .78f) })
        {
            const double span = 8000, step = 160;
            var xs = Enumerable.Range(0, (int)(2 * span / step) + 1).Select(i => -span + i * step).ToArray();
            var crest = xs.Select(x => height * (1 - Math.Pow(x / span, 2))
                * (.6 + .25 * Math.Sin(x / 530 + side) + .1 * Math.Sin(x / 170 - side) + .15 * Rand())).ToArray();
            // the valley floor, the foothills and the crest
            var rows = new[] { (dist - 500, 0.0), (dist - 250, .35), (dist, 1) }.Select(row => xs.Select((x, i) =>
            {
                var px = fx + x + (Rand() - .5) * step * .4;
                return new Vector3((float)px, row.Item2 > 0 ? (float)(crest[i] * row.Item2) : -20, (float)(fz + side * (row.Item1 + (Rand() - .5) * 120)));
            }).ToArray()).ToArray();
            void Triangle(Vector3 a, Vector3 b, Vector3 d)
            {
                var n = (b - a).Cross(d - a).Normalized();
                if (n.Z * side > 0) n = -n;  // facing the region
                var c = Loess.SrgbToLinear() * (.75f + .25f * Math.Max(0, n.Dot(sun)));
                c = c.Lerp(Sky.SrgbToLinear(), haze);
                vertices.AddRange([a, b, d]);
                colors.AddRange([c, c, c]);
            }
            for (var r = 0; r < 2; r++)
                for (var i = 0; i + 1 < xs.Length; i++)
                {
                    Triangle(rows[r][i], rows[r][i + 1], rows[r + 1][i + 1]);
                    Triangle(rows[r][i], rows[r + 1][i + 1], rows[r + 1][i]);
                }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
            DisableFog = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled
        });
        return mesh;
    }
}
