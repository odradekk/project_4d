using Godot;

namespace Game.Flow;

/// <summary>
/// Where a character walks next on its way through the flat. Characters have no path finding and walk straight at their
/// goal, so a goal in another room is reached through the doorways: out of the room it is in, then into the room it wants.
/// Every doorway of the flat leads into the hallway. The entrance hall runs on into the hallway without a wall between,
/// but counts as a room with that opening as its doorway, so that no way from it cuts across the bathroom's corner. The
/// balcony, off the living room, is left out.
/// Decision note: 2026-10-26-prologue-greybox
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
internal static class HomeRoute
{
    // A doorway as the spot inside its room and the spot in front of it in the hallway, and the axis it is crossed on.
    private readonly record struct Doorway(Vector3 Inside, Vector3 Outside, bool AlongX);

    private const float Y = HomeScene.Floor;
    private static readonly Doorway Kitchen = new(new(1f, Y, 3.5f), new(1f, Y, 4.5f), false);
    private static readonly Doorway Living = new(new(2.6f, Y, 3.5f), new(2.6f, Y, 4.5f), false);
    private static readonly Doorway Room = new(new(3.75f, Y, 4.6f), new(2.75f, Y, 4.6f), true);
    private static readonly Doorway Bath = new(new(2.55f, Y, 5.95f), new(2.55f, Y, 5f), false);
    private static readonly Doorway Hall = new(new(.9f, Y, 5.9f), new(.9f, Y, 5f), false);

    /// <summary>The point to walk to now, from <paramref name="from"/> toward <paramref name="goal"/>.</summary>
    internal static Vector3 Next(Vector3 from, Vector3 goal)
    {
        var start = DoorwayOf(from);
        var end = DoorwayOf(goal);
        if (start == end) return goal;
        if (start is { } leaving) return Lined(from, leaving.Inside, leaving) ? leaving.Outside : leaving.Inside;
        var entering = end!.Value;
        return Lined(from, entering.Outside, entering) ? entering.Inside : entering.Outside;
    }

    // The doorway of the room a point is in, by the walls' middle lines; null in the hallway. Outside the front door counts
    // as the entrance hall.
    private static Doorway? DoorwayOf(Vector3 point)
    {
        if (point.Z < 4f) return point.X < 1.8f ? Kitchen : Living;
        if (point.X > 3.25f) return Room;
        if (point.Z > 5.5f) return point.X < 1.8f ? Hall : Bath;
        return null;
    }

    // Whether the point is in line with the doorway, so that walking straight on goes through it.
    private static bool Lined(Vector3 point, Vector3 spot, Doorway doorway) =>
        Mathf.Abs(doorway.AlongX ? point.Z - spot.Z : point.X - spot.X) < .3f;
}
