using Godot;

namespace Game.Flow;

/// <summary>
/// Where a character walks next on its way through the flat. Characters have no path finding and walk straight at their
/// goal, so a goal in another room is reached through the doorways: out of the room it is in, then into the room it wants.
/// Every doorway of the flat leads into the living room, except the front door, which leads in from the corridor.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
internal static class HomeRoute
{
    // A doorway as the spot inside its room and the spot in front of it in the living room, and the axis it is crossed on.
    private readonly record struct Doorway(Vector3 Inside, Vector3 Outside, bool AlongX);

    private static readonly Doorway Corridor = new(new(2.3f, 0, 7.45f), new(3.4f, 0, 7.45f), true);
    private static readonly Doorway Bedroom = new(new(3.85f, 0, 5.6f), new(3.85f, 0, 6.5f), false);
    private static readonly Doorway Father = new(new(9.05f, 0, 5.5f), new(9.05f, 0, 6.5f), false);
    private static readonly Doorway Kitchen = new(new(4.05f, 0, 9.4f), new(4.05f, 0, 8.4f), false);
    private static readonly Doorway Bath = new(new(9.05f, 0, 9.4f), new(9.05f, 0, 8.4f), false);

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

    // The doorway of the room a point is in; null in the living room, which has no doorway to itself.
    private static Doorway? DoorwayOf(Vector3 point)
    {
        if (point.X < 2.8f) return Corridor;
        if (point.Z < 6.1f) return point.X < 7.7f ? Bedroom : Father;
        if (point.Z < 8.9f) return null;
        return point.X < 7.1f ? Kitchen : Bath;
    }

    // Whether the point is in line with the doorway, so that walking straight on goes through it.
    private static bool Lined(Vector3 point, Vector3 spot, Doorway doorway) =>
        Mathf.Abs(doorway.AlongX ? point.Z - spot.Z : point.X - spot.X) < .3f;
}
