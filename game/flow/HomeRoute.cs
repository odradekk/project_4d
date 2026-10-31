using Godot;

namespace Game.Flow;

/// <summary>
/// Where a character walks next on its way through the flat, and between the flat and the ground floor of its building.
/// Characters have no path finding and walk straight at their goal, so a goal in another room is reached through the
/// doorways: out of the room it is in, then into the room it wants. Every doorway of the flat leads into the hallway. The
/// entrance hall runs on into the hallway without a wall between, but counts as a room with that opening as its doorway, so
/// that no way from it cuts across the bathroom's corner. Nothing walks onto the balcony, off the living room: a goal there
/// is taken as the spot in front of the balcony door. A cat put down on the balcony walks in line with the balcony door
/// first, then through it to that spot.
/// Out of the flat the way is one line of stretches, each walked straight: the shared vestibule, the open corridor, the
/// stair landing of the flat's storey, the flight down to the turn, the turn, the flight down to the ground floor, and the
/// ground floor with the compound outside. Between each two the way passes a doorway as the flat's doorways are passed.
/// Decision note: 2026-10-29-home-scene-layout
/// Decision note: 2026-10-31-bus-commute
/// </summary>
internal static class HomeRoute
{
    // A doorway as the spot inside its room and the spot in front of it in the hallway, and the axis it is crossed on.
    // Along the way out of the flat, inside is the side nearer the flat.
    private readonly record struct Doorway(Vector3 Inside, Vector3 Outside, bool AlongX);

    private const float Y = HomeScene.Floor;
    private static readonly Doorway Kitchen = new(new(1f, Y, 3.5f), new(1f, Y, 4.5f), false);
    private static readonly Doorway Living = new(new(2.6f, Y, 3.3f), new(2.6f, Y, 4.5f), false);
    private static readonly Doorway Room = new(new(3.75f, Y, 4.6f), new(2.75f, Y, 4.6f), true);
    private static readonly Doorway Bath = new(new(2.55f, Y, 5.95f), new(2.55f, Y, 5f), false);
    private static readonly Doorway Hall = new(new(.9f, Y, 5.9f), new(.9f, Y, 5f), false);
    // In the living room before the balcony door, clear of the television stand on the way from the living-room door.
    private static readonly Vector3 BalconyFront = new(7.3f, Y, 3f);
    // On the balcony in line with its door, where a cat put down there starts its way back in.
    private static readonly Vector3 BalconyDoor = new(8.3f, Y, 3.42f);

    // The stair hall, as regions/compound/generate.py builds it: the turn's landing half a storey up from x 6.33 east, and
    // the wall between the two flights along z 18.525. The slope up from the ground floor, north of the wall, rises east
    // from x 1.01 to the landing; the one up to the flat's storey, south of it, rises west from x 6.71 to its landing's
    // edge at x 1.39. Each rises half a storey over 5.32 m.
    private const float Landing = Y / 2, Turn = 6.33f, Middle = 18.525f, Rise = Landing / 5.32f;
    private const float GroundFoot = 1.01f, TurnFoot = 6.71f, StoreyEdge = 1.39f;
    // The flat's front wall, the vestibule's south wall and the stair hall's north wall.
    private const float FrontWall = 7.35f, Vestibule = 9.05f, StairHall = 16.4f;

    // The doorways along the way out, the first between the flat and the vestibule; each joins the stretch of its number to
    // the next. Up the stair the cat keeps to the middle of each flight.
    private static readonly Doorway[] Way =
    [
        new(new(.75f, Y, 6.8f), new(.75f, Y, 7.9f), false),  // the front door
        new(new(.5f, Y, 8.2f), new(-.6f, Y, 8.2f), true),  // the vestibule's opening onto the corridor
        new(new(-.9f, Y, 16f), new(-.9f, Y, 16.9f), false),  // the corridor onto the stair landing
        new(new(.9f, Y, 19.55f), OnFlight(1.9f, 19.55f), true),  // the top of the flight down
        new(OnFlight(5.9f, 19.55f), new(7f, Landing, 19.55f), true),  // its foot at the turn
        new(new(7f, Landing, 17.5f), OnFlight(5.9f, 17.5f), true),  // the top of the flight to the ground floor
        new(OnFlight(1.6f, 17.5f), new(.5f, 0, 17.5f), true)  // its foot
    ];

    /// <summary>The point to walk to now, from <paramref name="from"/> toward <paramref name="goal"/>.</summary>
    internal static Vector3 Next(Vector3 from, Vector3 goal)
    {
        var start = Stretch(from);
        var end = Stretch(goal);
        if (start == end) return start == 0 ? Indoors(from, goal) : goal;
        if (start == 0 && DoorwayOf(from) != Hall) return Indoors(from, Way[0].Inside);
        return start < end ? Through(from, Way[start], true) : Through(from, Way[start - 1], false);
    }

    /// <summary>Whether the point is in the flat.</summary>
    internal static bool InFlat(Vector3 point) => Stretch(point) == 0;

    private static Vector3 Indoors(Vector3 from, Vector3 goal)
    {
        if (OnBalcony(from))
            return OnBalcony(goal) ? goal : new Vector2(from.X - BalconyDoor.X, from.Z - BalconyDoor.Z).Length() < .15f ? BalconyFront : BalconyDoor;
        if (OnBalcony(goal)) goal = BalconyFront;
        var start = DoorwayOf(from);
        var end = DoorwayOf(goal);
        if (start == end) return goal;
        if (start is { } leaving) return Through(from, leaving, true);
        return Through(from, end!.Value, false);
    }

    // Through the doorway from inside to outside or back: to the spot on this side until in line with it, then on to the
    // spot on the other side.
    private static Vector3 Through(Vector3 from, Doorway doorway, bool outward)
    {
        var (near, far) = outward ? (doorway.Inside, doorway.Outside) : (doorway.Outside, doorway.Inside);
        return Lined(from, near, doorway) ? far : near;
    }

    // Which stretch of the way a point is on, by its height and the walls' middle lines: 0 in the flat, 1 in the vestibule,
    // 2 on the corridor, 3 on the stair landing of the flat's storey, 4 on the flight down from it, 5 at the turn, 6 on the
    // flight down to the ground floor, and 7 on the ground floor or outside.
    private static int Stretch(Vector3 point)
    {
        if (point.Y > Y - .3f)
        {
            if (point.Z < Vestibule && point.X > 0) return point.Z < FrontWall ? 0 : 1;
            if (point.Z < StairHall) return 2;
            return point.X >= StoreyEdge && point.Z > Middle ? 4 : 3;
        }
        if (point.Y > Landing - .3f) return point.X >= Turn ? 5 : point.Z > Middle ? 4 : 6;
        return point.X is >= GroundFoot and < Turn && point.Z is > StairHall and < Middle ? 6 : 7;
    }

    // A point on the slope over a flight: north of the wall between them on the one from the ground floor, south of it on the
    // one from the turn.
    private static Vector3 OnFlight(float x, float z) =>
        new(x, z < Middle ? (x - GroundFoot) * Rise : Landing + (TurnFoot - x) * Rise, z);

    private static bool OnBalcony(Vector3 point) => point.X > 7.85f && point.Z < 4f;

    // The doorway of the room a point is in, by the walls' middle lines; null in the hallway.
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
