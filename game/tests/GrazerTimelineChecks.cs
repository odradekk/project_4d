using System;
using System.Linq;
using FourD;
using Game.Creatures;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>The timeline of a grazer and the body that its mouth opens, which need no region.</summary>
public partial class GrazerTimelineChecks : Node
{
    public override void _Ready()
    {
        try
        {
            CheckTimeline();
            CheckBadTimelines();
            CheckBody();
            GD.Print("Grazer timeline checks passed: timeline, bad timelines, body.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
    }

    private static GrazerKey Key(double time, Vec4 position, double angle, double mouth) =>
        new(time, position, [new(RotationPlane.XZ, angle), new(RotationPlane.XW, 2 * angle)], mouth);

    private static GrazerTimeline Straight() => new(
        [Key(0, new(0, 0, 0, 10), 0, 0), Key(10, new(2, 1, -4, 0), 1, 1), Key(14, new(2, 3, -4, 0), 1, 1)],
        [new(10, [new EntityId("a")], []), new(12, [], [new SeamId("s")])]);

    private void CheckTimeline()
    {
        var timeline = Straight();
        Check(timeline.Duration == 14, "a timeline lasts until its last key");
        var start = timeline.At(0);
        Check(start.Position == new Vec4(0, 0, 0, 10) && start.Mouth == 0, "at time 0 the first key");
        var middle = timeline.At(5);
        Check(middle.Position == new Vec4(1, .5, -2, 5) && middle.Mouth == .5, "half way between two keys is half way in position and mouth");
        Check(middle.Turns.Select(turn => (turn.Plane, turn.Angle)).SequenceEqual([(RotationPlane.XZ, .5), (RotationPlane.XW, 1.0)]),
            "half way is half way in each plane's angle, with the planes in the keys' order");
        Check(timeline.At(10).Position == new Vec4(2, 1, -4, 0), "at a key time, the key");
        Check(timeline.At(12).Position == new Vec4(2, 2, -4, 0), "between the later keys, the later segment");
        Check(timeline.At(-3).Position == new Vec4(0, 0, 0, 10) && timeline.At(99).Position == new Vec4(2, 3, -4, 0),
            "before the start the first key, after the end the last");
        Check(timeline.At(99).Time == 14, "the key out of range is at the time it was clamped to");
    }

    private static void CheckBadTimelines()
    {
        GrazerKey Plain(double time, double mouth = 0, RotationPlane plane = RotationPlane.XZ) => new(time, new(0, 0, 0, 0), [new(plane, 0)], mouth);
        void Rejected(GrazerKey[] keys, GrazerBite[] bites, string reason)
        {
            try { _ = new GrazerTimeline(keys, bites); }
            catch (ArgumentException) { return; }
            throw new InvalidOperationException($"a timeline must be refused: {reason}");
        }
        Rejected([Plain(0)], [], "one key");
        Rejected([Plain(1), Plain(2)], [], "the first key is not at time 0");
        Rejected([Plain(0), Plain(0)], [], "two keys at one time");
        Rejected([Plain(0), Plain(2), Plain(1)], [], "keys out of order");
        Rejected([Plain(0), new(1, new(0, double.NaN, 0, 0), [new(RotationPlane.XZ, 0)], 0)], [], "a position that is not a number");
        Rejected([Plain(0), new(1, new(0, 0, 0, 0), [new(RotationPlane.XZ, double.PositiveInfinity)], 0)], [], "an angle that is not finite");
        Rejected([Plain(0), Plain(1, mouth: 1.1)], [], "a mouth past 1");
        Rejected([Plain(0, mouth: -.1), Plain(1)], [], "a mouth below 0");
        Rejected([Plain(0), Plain(1, plane: RotationPlane.XW)], [], "keys that turn in different planes");
        Rejected([Plain(0), new(1, new(0, 0, 0, 0), [], 0)], [], "keys with different numbers of turns");
        Rejected([Plain(0), Plain(1)], [new(1.5, [], [])], "a bite after the end");
        Rejected([Plain(0), Plain(2)], [new(1, [], []), new(.5, [], [])], "bites out of order");
        _ = new GrazerTimeline([Plain(0), Plain(1)], [new(1, [], [])]);
    }

    private static void CheckBody()
    {
        MetaballShape Shape(double x, double radius, double threshold) =>
            new([new Metaball(new("a"), new(x, 0, 0, 0), radius, 1), new Metaball(new("b"), new(0, x, 0, 2 * x), radius, 2)], threshold);
        var body = new GrazerBody(Shape(1, .5, .25), Shape(3, 1.5, .75));
        Check(Same(body.At(0), body.Shut) && Same(body.At(1), body.Open!), "the mouth shut is the shut shape and wide open the open shape");
        var half = body.At(.5);
        Check(half.Balls[0].Center == new Vec4(2, 0, 0, 0) && half.Balls[1].Center == new Vec4(0, 2, 0, 4),
            "half open is half way between the ball centers");
        Check(half.Balls[0].Radius == 1 && half.Balls[1].Weight == 2 && half.Threshold == .5 && half.Balls[1].Id == new PartId("b"),
            "and between radii and thresholds, with the weights and IDs kept");
        Check(Same(new GrazerBody(Shape(1, .5, .25), null).At(.7), Shape(1, .5, .25)), "a mouth that cannot open leaves the body as it is");
        Check(GrazerBody.SameBalls(Shape(1, .5, .25), Shape(3, 1, .5)), "shapes with the same balls in the same order can be opened between");
        Check(!GrazerBody.SameBalls(Shape(1, .5, .25), new([new Metaball(new("a"), new(0, 0, 0, 0), 1, 1)], 1)), "a ball fewer is not the same body");
        Check(!GrazerBody.SameBalls(Shape(1, .5, .25), new([.. Shape(1, .5, .25).Balls.Reverse()], .25)), "the same balls in another order are not the same body");
    }

    private static bool Same(MetaballShape a, MetaballShape b) => a.Threshold == b.Threshold && a.Balls.SequenceEqual(b.Balls);
}
