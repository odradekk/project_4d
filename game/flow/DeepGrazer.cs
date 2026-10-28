using System;
using FourD;
using Game.Creatures;

namespace Game.Flow;

/// <summary>
/// The grazer of scene 9, the timelines it is given. It comes in along W from far off while the music rises, shows up in the
/// room as lumps that swell and join, and bites the extra door, the corner and the window frame in turn, walking round the
/// walls. After that it goes for her in legs of a few seconds, each aimed at where she stands when it starts.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal static class DeepGrazer
{
    internal const string Id = "grazer";
    internal const string Seam = "s2";
    internal const string Door = "DeepDoorExtra";
    internal const string Corner = "DeepCorner";
    internal const string Frame = "DeepFrame";
    internal const string Window = "DeepFittings";

    // The body origin is the middle of the mouth ring, at the height of the middle of the room.
    private const double Height = 2.05;
    // W of the room, and a little deeper: the lips are cut through their middle and the throat behind them is cut as well.
    private const double Depth = 1.75;
    private const double Quarter = Math.PI / 2;
    private const double Speed = .45;
    private const double LegSeconds = 3;
    private const double TurnSpeed = .5;
    // A leg stops this far from her and opens the mouth from this far on.
    private const double StopAt = 1.5;
    private const double OpenFrom = 8;
    private const double OpenSpan = 3;

    /// <summary>
    /// The first minute and a half: the seam is cut at once, and the body is cut by the room's W from 3.9 m on, after about half
    /// a minute. The extra door, the corner and the frame and glass of the window are bitten at 56, 65 and 86 seconds.
    /// </summary>
    internal static GrazerTimeline Approach() => new(
        [
            Key(0, 8.3, 3.6, 14, -Quarter, .6, 0),
            Key(12, 8.3, 3.6, 9, -Quarter, .6, 0),
            Key(24, 8.3, 3.6, 5, -Quarter, .6, 0),
            Key(32, 8.3, 3.6, 3.3, -Quarter, .6, 0),
            Key(38, 8.3, 3.6, 2.4, -Quarter, .45, 0),
            Key(44, 8.3, 3.6, Depth + .1, -Quarter, .25, 0),
            // Along the east wall to the south wall, its mouth turning to the extra door.
            Key(52, 7, 5.9, Depth, -2 * Quarter, .1, .2),
            Key(55, 7, 6.2, Depth, -2 * Quarter, .1, 1),
            Key(58, 7, 6.2, Depth, -2 * Quarter, .1, 1),
            // To the corner of the south and east walls.
            Key(64, 8.3, 6.1, Depth, -2.5 * Quarter, .1, 1),
            Key(67, 8.3, 6.1, Depth, -2.5 * Quarter, .1, 1),
            // Up the east wall and along the north wall to the window.
            Key(77, 8.3, 2, Depth, -4 * Quarter, .1, .6),
            Key(85, 5.5, 1.5, Depth, -4 * Quarter, .1, 1),
            Key(88, 5.5, 1.5, Depth, -4 * Quarter, .1, 1)
        ],
        [
            new(1, [], [new SeamId(Seam)]),
            new(56, [new EntityId(Door)], []),
            new(65, [new EntityId(Corner)], []),
            new(86, [new EntityId(Frame), new EntityId(Window)], [])
        ]);

    /// <summary>
    /// The next leg of the chase from <paramref name="start"/> toward the place she stands at: the body turns toward her as far
    /// as it can in the time, walks to within a step of her, and opens its mouth the nearer it is.
    /// </summary>
    internal static GrazerTimeline Leg(GrazerKey start, double x, double z)
    {
        var (fromX, fromZ, yaw) = (start.Position.X, start.Position.Z, start.Turns[0].Angle);
        var (dx, dz) = (x - fromX, z - fromZ);
        var distance = Math.Sqrt(dx * dx + dz * dz);
        var turn = Math.IEEERemainder(Math.Atan2(dx, -dz) - yaw, Math.Tau);
        var step = Math.Clamp(distance - StopAt, 0, Speed * LegSeconds);
        var toward = distance == 0 ? 0 : step / distance;
        var (endX, endZ) = (fromX + dx * toward, fromZ + dz * toward);
        var left = Math.Max(distance - step, 0);
        var mouth = Math.Clamp((OpenFrom - left) / OpenSpan, .4, 1);
        var turned = yaw + Math.Clamp(turn, -TurnSpeed * LegSeconds, TurnSpeed * LegSeconds);
        return new GrazerTimeline([start with { Time = 0 }, Key(LegSeconds, endX, endZ, start.Position.W, turned, start.Turns[1].Angle, mouth)], []);
    }

    /// <summary>The key at the end of a timeline, which the next one starts from.</summary>
    internal static GrazerKey End(GrazerTimeline timeline) => timeline.At(timeline.Duration);

    private static GrazerKey Key(double time, double x, double z, double w, double yaw, double tilt, double mouth) =>
        new(time, new(x, Height, z, w), [new(RotationPlane.XZ, yaw), new(RotationPlane.YW, tilt)], mouth);
}
