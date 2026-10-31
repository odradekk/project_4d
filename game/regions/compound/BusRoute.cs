using System;
using System.Collections.Generic;
using Godot;

namespace Game.Regions;

/// <summary>
/// A path the bus's middle drives on the ground (x, z), sampled about every quarter metre, with the fastest it may go at
/// each point: no faster than its cruising speed, slower through turns, and braking in time for them and for the speed it
/// must have at the end.
/// </summary>
internal sealed class BusRoute
{
    private const float Step = .25f;
    private readonly List<(Vector2 At, float Cap)> points = [];
    private readonly float cruise, corner, brake;
    private float[] along = [], limit = [];  // the distance to each point, and the squared speed allowed there

    internal BusRoute(Vector2 start, float cruise, float corner, float brake)
    {
        (this.cruise, this.corner, this.brake) = (cruise, corner, brake);
        points.Add((start, cruise));
    }

    internal float Length => along[^1];

    /// <summary>A straight line to <paramref name="to"/>.</summary>
    internal BusRoute Line(Vector2 to)
    {
        var from = Last;
        return Add(t => from.Lerp(to, t), from.DistanceTo(to), cruise);
    }

    /// <summary>A change of lane while driving along z to <paramref name="to"/>, eased at both ends.</summary>
    internal BusRoute Ease(Vector2 to)
    {
        var from = Last;
        return Add(t => new(from.X + (to.X - from.X) * t * t * (3 - 2 * t), Mathf.Lerp(from.Y, to.Y, t)), Mathf.Abs(to.Y - from.Y), cruise);
    }

    /// <summary>A turn about <paramref name="centre"/> through the given angles of the point seen from it, at the corner speed.</summary>
    internal BusRoute Turn(Vector2 centre, float from, float to)
    {
        var radius = Last.DistanceTo(centre);
        return Add(t => centre + radius * Vector2.FromAngle(Mathf.Lerp(from, to, t)), radius * Mathf.Abs(to - from), corner);
    }

    /// <summary>Fixes the route, braking to <paramref name="endSpeed"/> at its end.</summary>
    internal BusRoute End(float endSpeed)
    {
        along = new float[points.Count];
        for (var i = 1; i < points.Count; i++) along[i] = along[i - 1] + points[i - 1].At.DistanceTo(points[i].At);
        limit = new float[points.Count];
        limit[^1] = endSpeed * endSpeed;
        for (var i = points.Count - 2; i >= 0; i--)
            limit[i] = Math.Min(points[i].Cap * points[i].Cap, limit[i + 1] + 2 * brake * (along[i + 1] - along[i]));
        return this;
    }

    /// <summary>
    /// The point at distance <paramref name="s"/> along the route, and the direction it heads there, turning from one
    /// sample's direction to the next's along the way, so that it turns smoothly rather than in steps.
    /// </summary>
    internal (Vector2 At, Vector2 Heading) At(float s)
    {
        var (i, t) = Find(s);
        var (a, b) = (points[i].At, points[i + 1].At);
        var next = i + 2 < points.Count ? points[i + 2].At - b : b - a;
        return (a.Lerp(b, t), (b - a).Normalized().Lerp(next.Normalized(), t).Normalized());
    }

    /// <summary>The fastest the bus may go at distance <paramref name="s"/>.</summary>
    internal float Limit(float s)
    {
        var (i, t) = Find(s);
        return Mathf.Sqrt(Mathf.Lerp(limit[i], limit[i + 1], t));
    }

    private Vector2 Last => points[^1].At;

    private BusRoute Add(Func<float, Vector2> at, float length, float cap)
    {
        var n = Math.Max(1, (int)Math.Ceiling(length / Step));
        for (var k = 1; k <= n; k++) points.Add((at((float)k / n), cap));
        return this;
    }

    private (int, float) Find(float s)
    {
        s = Math.Clamp(s, 0, Length);
        var i = Array.BinarySearch(along, s);
        if (i < 0) i = ~i - 1;
        i = Math.Min(i, along.Length - 2);
        return (i, (s - along[i]) / (along[i + 1] - along[i]));
    }
}
