using Godot;

namespace Game.Flow;

/// <summary>
/// Where a character walks next on its way through the flat, and between the flat and the ground floor of its building.
/// Characters have no path finding and walk straight at their goal, so a goal in another room is reached through the
/// doorways: out of the room it is in, then into the room it wants. Every doorway of the flat leads into the hallway. The
/// entrance hall runs on into the hallway without a wall between, but counts as a room with that opening as its doorway, so
/// that no way from it cuts across the bathroom's corner. Nothing walks onto the balcony, off the living room: a goal there
/// is taken as the spot in front of the balcony door. A cat put down on the balcony walks in line with the balcony door
/// first, then through it into the living room.
/// Out of the flat the way is one line of stretches, each walked straight: the shared vestibule, the open corridor, the
/// stair landing of the flat's storey, the flight down to the turn, the turn, the flight down to the ground floor, the
/// ground floor's stair hall, and the forecourt in front of the stair hall's open west side. Between each two
/// the way passes a doorway as the flat's doorways are passed; the way out is in line with the middle of the slope out of
/// the stair hall, so that a cat coming along the building's west wall does not run into the slope's side.
/// The way ends at the forecourt: the rest of the compound is beyond it, and a goal there is taken as the spot in front of
/// the slope. From beyond the forecourt a character walks straight at its goal, and may be stopped on the way. The
/// stretches cover her building's north half only, up to her storey: every point higher than her storey's floor less
/// 0.3 m counts as on her storey, the corridor of the south half and the floor above included. The ground floor's corridor
/// is beyond the way too: it turns a corner off the stair hall, and nobody needs to go there.
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
    // The balcony door, crossed straight from the balcony in line with it into the living room. From there, and from
    // anywhere east of BalconyFront, a straight way to the living-room door would meet the television stand, so the cat
    // steps to BalconyFront first.
    private static readonly Doorway Balcony = new(new(8.3f, Y, 3.42f), new(7.6f, Y, 3.42f), true);
    // Declared after the doorways, since static fields are set in the order they are written.
    private static readonly Doorway[] Doorways = [Kitchen, Living, Room, Bath, Hall, Balcony];

    // The stair hall, as regions/compound/generate.py builds it: the turn's landing half a storey up from x 6.33 east, and
    // the wall between the two flights along z 18.525. The slope up from the ground floor, north of the wall, rises east
    // from x 0.97 to the landing's height; the one up to the flat's storey, south of it, rises west from x 6.75 to the
    // storey's height at x 1.43, short of its landing's edge at x 1.39. Each rises half a storey over 5.32 m.
    private const float Landing = Y / 2, Turn = 6.33f, Middle = 18.525f, Rise = Landing / 5.32f;
    private const float GroundFoot = .97f, TurnFoot = 6.75f, StoreyEdge = 1.39f;
    // The flat's front wall, the vestibule's south wall, the stair hall's north and south walls, and the corridor's railing.
    private const float FrontWall = 7.35f, Vestibule = 9.05f, StairHall = 16.4f, HallEnd = 20.65f, Railing = -1.72f;
    // The stair hall's east wall; the slope out of its west side ends at x −2.47, and a cat counts as out of the stair hall
    // once it is clear of the slope and the railings' ends. The forecourt reaches from there west to ForecourtWest, between
    // its north and south ends; the strips beside the railings are beyond it, since a straight way to them from the
    // forecourt would cross the slope.
    private const float HallEast = 7.85f, HallOut = -2.7f, ForecourtWest = -7f, ForecourtNorth = 14.5f, ForecourtSouth = 22.5f;
    private const int Forecourt = 8, Beyond = 9;

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
        new(OnFlight(1.6f, 17.5f), new(.5f, 0, 17.5f), true),  // its foot
        new(new(-1f, 0, 17.5f), new(-3.2f, -.2f, 17.5f), true)  // the way out, past the slope's foot
    ];

    /// <summary>The point to walk to now, from <paramref name="from"/> toward <paramref name="goal"/>.</summary>
    internal static Vector3 Next(Vector3 from, Vector3 goal)
    {
        var start = Stretch(from);
        var end = Stretch(goal);
        if (start == Beyond) return goal;
        if (end == Beyond) (goal, end) = (Way[^1].Outside, Forecourt);
        if (start == end) return start == 0 ? Indoors(from, goal) : goal;
        if (start == 0 && DoorwayOf(from) != Hall) return Indoors(from, Way[0].Inside);
        return start < end ? Through(from, Way[start], true) : Through(from, Way[start - 1], false);
    }

    /// <summary>Whether the way leads to the point: it is in the building or on the forecourt.</summary>
    internal static bool Covers(Vector3 point) => Stretch(point) != Beyond;

    /// <summary>Whether the point is in the flat.</summary>
    internal static bool InFlat(Vector3 point) => Stretch(point) == 0;

    private static Vector3 Indoors(Vector3 from, Vector3 goal)
    {
        if (OnBalcony(from))
            return OnBalcony(goal) ? goal : Through(from, Balcony, true);
        if (OnBalcony(goal)) goal = BalconyFront;
        var start = DoorwayOf(from);
        var end = DoorwayOf(goal);
        // Beyond where a walker stops at BalconyFront, which is within 0.1 m.
        if (start == Living && end != Living && from.X > BalconyFront.X + .15f) return BalconyFront;
        var next = start == end ? goal : start is { } leaving ? Through(from, leaving, true) : Through(from, end!.Value, false);
        // Partway through a doorway it walks on to the doorway's spot on the side it is on before it turns anywhere else,
        // so that it does not cut the jamb's corner.
        foreach (var doorway in Doorways)
            if (Passing(from, doorway) && next != doorway.Inside && next != doorway.Outside)
                return start == doorway ? doorway.Inside : doorway.Outside;
        return next;
    }

    // Whether the point is in line with the doorway and between its two spots, clear of where a walker stops at either,
    // which is within 0.1 m.
    private static bool Passing(Vector3 point, Doorway doorway)
    {
        var (at, inside, outside) = doorway.AlongX ? (point.X, doorway.Inside.X, doorway.Outside.X) : (point.Z, doorway.Inside.Z, doorway.Outside.Z);
        return Lined(point, doorway.Inside, doorway) && at > Mathf.Min(inside, outside) + .15f && at < Mathf.Max(inside, outside) - .15f;
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
    // flight down to the ground floor, 7 in the ground floor's stair hall, 8 on the forecourt, and 9
    // beyond it.
    private static int Stretch(Vector3 point)
    {
        if (point.Y > Y - .3f)
        {
            // Out of the vestibule's opening a point counts as on the corridor only once a cat there has cleared the jamb.
            if (point.Z < Vestibule && point.X > (point.Z < FrontWall ? 0 : -.3f)) return point.Z < FrontWall ? 0 : 1;
            if (point.Z < StairHall) return 2;
            return point.X >= StoreyEdge && point.Z > Middle ? 4 : 3;
        }
        if (point.Y > Landing - .3f) return point.X >= Turn ? 5 : point.Z > Middle ? 4 : 6;
        if (point.X is >= GroundFoot and < Turn && point.Z is > StairHall and < Middle) return 6;
        if (point.X is > HallOut and < HallEast && point.Z is >= StairHall and < HallEnd) return 7;
        return point.X is > ForecourtWest and <= HallOut && point.Z is > ForecourtNorth and < ForecourtSouth ? Forecourt : Beyond;
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
