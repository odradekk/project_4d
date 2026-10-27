using System.Linq;
using FourD;

namespace Game.Creatures;

/// <summary>A grazer's body as authored: shut, and wide open when its mouth can open.</summary>
internal sealed record GrazerBody(MetaballShape Shut, MetaballShape? Open)
{
    /// <summary>Whether the shapes have the same balls in the same order, which is what lets the mouth open between them.</summary>
    internal static bool SameBalls(MetaballShape a, MetaballShape b) => a.Balls.Select(ball => ball.Id).SequenceEqual(b.Balls.Select(ball => ball.Id));

    /// <summary>
    /// The body with its mouth open by <paramref name="mouth"/>, from 0 for <see cref="Shut"/> to 1 for <see cref="Open"/>:
    /// every ball's center, radius and weight, and the threshold, change linearly, which keeps them positive.
    /// </summary>
    internal MetaballShape At(double mouth)
    {
        if (Open is null) return Shut;
        var balls = Shut.Balls.Zip(Open.Balls, (shut, open) => new Metaball(shut.Id, GrazerTimeline.Lerp(shut.Center, open.Center, mouth),
            double.Lerp(shut.Radius, open.Radius, mouth), double.Lerp(shut.Weight, open.Weight, mouth)));
        return new(balls.ToArray(), double.Lerp(Shut.Threshold, Open.Threshold, mouth));
    }
}
