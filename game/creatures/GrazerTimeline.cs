using System;
using System.Collections.Generic;
using System.Linq;
using FourD;

namespace Game.Creatures;

/// <summary>
/// Where a grazer is and how it looks at one moment: its body origin in four dimensions, its turns in the coordinate
/// planes, and how far its mouth is open, 0 shut to 1 wide open. A turn is the angle of one plane, so the body turns
/// by scalars and never by interpolated matrices.
/// </summary>
public sealed record GrazerKey(double Time, Vec4 Position, IReadOnlyList<PlaneAngle> Turns, double Mouth);

/// <summary>The structure a grazer eats at a moment: whole building elements, and seams, which are cut.</summary>
public sealed record GrazerBite(double Time, IReadOnlyList<EntityId> Elements, IReadOnlyList<SeamId> Seams);

/// <summary>
/// A grazer's scripted path: keys from time 0, between which position, turns and mouth change linearly, and the bites
/// it takes on the way. Decision note: 2026-10-26-prologue-greybox
/// </summary>
public sealed class GrazerTimeline
{
    private readonly GrazerKey[] keys;

    /// <summary>
    /// At least two keys, the first at time 0 and the rest strictly later, each with the same planes in the same order
    /// in <see cref="GrazerKey.Turns"/> and a mouth from 0 to 1. Bites in time order, none after the last key.
    /// ArgumentException for anything else.
    /// </summary>
    public GrazerTimeline(IReadOnlyList<GrazerKey> keys, IReadOnlyList<GrazerBite> bites)
    {
        if (keys.Count < 2) throw new ArgumentException("A timeline needs at least two keys.", nameof(keys));
        if (keys[0].Time != 0) throw new ArgumentException("The first key is at time 0.", nameof(keys));
        for (var i = 1; i < keys.Count; i++)
            if (!(keys[i].Time > keys[i - 1].Time)) throw new ArgumentException($"Key {i} is not later than the key before it.", nameof(keys));
        foreach (var key in keys)
        {
            if (!(key.Mouth is >= 0 and <= 1)) throw new ArgumentException($"The mouth at {key.Time} s is not from 0 to 1.", nameof(keys));
            if (!IsFinite(key)) throw new ArgumentException($"The key at {key.Time} s has a position or angle that is not finite.", nameof(keys));
            if (!key.Turns.Select(turn => turn.Plane).SequenceEqual(keys[0].Turns.Select(turn => turn.Plane)))
                throw new ArgumentException($"The key at {key.Time} s turns in other planes than the first.", nameof(keys));
        }
        for (var i = 0; i < bites.Count; i++)
            if (!(bites[i].Time <= keys[^1].Time) || i > 0 && bites[i].Time < bites[i - 1].Time)
                throw new ArgumentException($"Bite {i} is out of order or after the last key.", nameof(bites));
        this.keys = keys.ToArray();
        Bites = bites.ToArray();
    }

    internal IReadOnlyList<GrazerBite> Bites { get; }

    /// <summary>The time of the last key.</summary>
    public double Duration => keys[^1].Time;

    /// <summary>The key at <paramref name="time"/>, linear between the keys around it; the first before time 0 and the last after the end.</summary>
    public GrazerKey At(double time)
    {
        time = Math.Clamp(time, 0, Duration);
        var next = 1;
        while (keys[next].Time < time) next++;
        var (from, to) = (keys[next - 1], keys[next]);
        var u = (time - from.Time) / (to.Time - from.Time);
        var turns = from.Turns.Zip(to.Turns, (a, b) => new PlaneAngle(a.Plane, Lerp(a.Angle, b.Angle, u))).ToArray();
        return new(time, Lerp(from.Position, to.Position, u), turns, Lerp(from.Mouth, to.Mouth, u));
    }

    private static bool IsFinite(GrazerKey key) =>
        new[] { key.Position.X, key.Position.Y, key.Position.Z, key.Position.W }.Concat(key.Turns.Select(turn => turn.Angle)).All(double.IsFinite);

    // Each component alone, as the contract asks of the host: a timeline interpolates scalars. Two equal values give that
    // value, which the sum of two weighted halves does not always do to the last bit.
    private static double Lerp(double a, double b, double u) => a == b ? a : double.Lerp(a, b, u);

    internal static Vec4 Lerp(Vec4 a, Vec4 b, double u) => new(Lerp(a.X, b.X, u), Lerp(a.Y, b.Y, u), Lerp(a.Z, b.Z, u), Lerp(a.W, b.W, u));
}
