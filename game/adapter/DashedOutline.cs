using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

/// <summary>
/// Draws the outline of a round shape as dashes. The engine gives such an outline as many short pieces, each of two points;
/// they are chained into loops first, since a dash must run along the whole loop and not restart at every piece.
/// </summary>
internal static class DashedOutline
{
    private const float Dash = .04f;
    private const float Gap = .03f;
    // The engine's points are doubles cut to floats here.
    private const float Meet = 1e-4f;

    /// <summary>Adds the dashes along every loop that the pieces make, as pairs of end points of line segments.</summary>
    internal static void AddDashes(List<Vector3> vertices, IEnumerable<Polyline> pieces)
    {
        foreach (var loop in Loops(pieces)) AddDashes(vertices, loop);
    }

    // Chains pieces whose ends meet; a chain that comes back to its start is a loop.
    private static List<List<Vector3>> Loops(IEnumerable<Polyline> pieces)
    {
        var open = pieces.Select(piece => Points(piece)).ToList();
        var chains = new List<List<Vector3>>();
        while (open.Count > 0)
        {
            var chain = open[^1];
            open.RemoveAt(open.Count - 1);
            while (Extend(chain, open)) { }
            chains.Add(chain);
        }
        return chains;
    }

    // Joins one piece that meets an end of the chain to it. False when none does.
    private static bool Extend(List<Vector3> chain, List<List<Vector3>> open)
    {
        for (var i = 0; i < open.Count; i++)
        {
            var piece = open[i];
            if (chain[^1].DistanceTo(piece[0]) < Meet) chain.AddRange(piece.Skip(1));
            else if (chain[^1].DistanceTo(piece[^1]) < Meet) chain.AddRange(Enumerable.Reverse(piece).Skip(1));
            else if (chain[0].DistanceTo(piece[^1]) < Meet) chain.InsertRange(0, piece.SkipLast(1));
            else if (chain[0].DistanceTo(piece[0]) < Meet) chain.InsertRange(0, Enumerable.Reverse(piece).SkipLast(1));
            else continue;
            open.RemoveAt(i);
            return true;
        }
        return false;
    }

    private static List<Vector3> Points(Polyline line)
    {
        var points = Enumerable.Range(0, line.Positions.Count / 3)
            .Select(i => new Vector3((float)line.Positions[i * 3], (float)line.Positions[i * 3 + 1], (float)line.Positions[i * 3 + 2]))
            .ToList();
        if (line.Closed) points.Add(points[0]);
        return points;
    }

    // Walks the chain, switching between dash and gap by distance walked, which carries over from one segment to the next.
    private static void AddDashes(List<Vector3> vertices, List<Vector3> chain)
    {
        var into = 0f;
        for (var i = 0; i + 1 < chain.Count; i++)
        {
            var length = chain[i].DistanceTo(chain[i + 1]);
            for (var at = 0f; at < length;)
            {
                var dashing = into < Dash;
                var toggle = dashing ? Dash : Dash + Gap;
                var step = Mathf.Min(toggle - into, length - at);
                if (dashing)
                {
                    vertices.Add(chain[i].Lerp(chain[i + 1], at / length));
                    vertices.Add(chain[i].Lerp(chain[i + 1], (at + step) / length));
                }
                // Land exactly on the end of the dash, the gap or the segment, so that rounding cannot leave a step too small to move.
                if (toggle - into <= length - at)
                {
                    at += step;
                    into = toggle < Dash + Gap ? toggle : 0;
                }
                else
                {
                    at = length;
                    into += step;
                }
            }
        }
    }
}
