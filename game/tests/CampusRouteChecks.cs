using System;
using System.Threading.Tasks;
using Game.Regions;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The campus region in one trip with the movement keys, running all the way, from the bus stop and back to it: north
/// along the main road's west sidewalk to its north end; back to the asphalt road, west along its south sidewalk and in
/// at the school's gate; across the plaza into teaching building 1, up its wide stair to the fourth floor and down;
/// across to teaching building 2, up its north spiral stair to the sixth floor and down its south one; out of the back
/// gate into the sports ground; up the dirt road to the gap between the building site and the sports ground and on to the
/// road's north end; down the dirt road through the iron gate and the side door into the mall, up its middle stair to
/// the upper floor and down; along the passage out of the east entrance to the sidewalk's south end, and north to the bus
/// stop. She runs outward into each of the unseen walls that bound where she can go, 0.5 m from what it meets at each of
/// its ends, and it stops her: the main road's kerb, the sidewalk's north and south ends, the dirt road's north end and
/// the gap; the kerb also at the bus stop and across the asphalt road's mouth. Nothing else stands behind any of them, so
/// with a wall gone, or 0.7 m or more short of either end of the opening it closes, she runs on past it. Long runs that
/// the checks of each part also run at the real speed, along the asphalt road, over the plaza and the yard, along the dirt
/// road and the mall's passage, go at eight times it.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class CampusRouteChecks : Node
{
    // The faces of the unseen walls toward her: the kerb (x), the sidewalk's north and south ends (z), the dirt road's
    // north end (z), and the gap (x).
    private const float Kerb = 244.2f, NorthEnd = -30, SouthEnd = 122.02f, DirtEnd = -106, Gap = -17.6f;
    // Lines 0.5 m from what the walls meet at their ends: the kerb (x); the sidewalk's north and south ends (z); the
    // estate's east railing and the mall's east wall (x); the building site's east hoarding and the estate's west railing
    // (x); the hoarding's south side and the sports ground's fence (z).
    private const float ByKerb = Kerb - .5f, ByNorthEnd = NorthEnd + .5f, BySouthEnd = SouthEnd - .5f;
    private const float ByEstate = 237.62f, ByMall = 239.7f, ByHoarding = -16.45f, ByRailing = -1.22f;
    private const float ByHoardingSouth = -31.85f, ByFence = -28.33f;
    // The bus stop by the kerb (x) and its middle (z); a line along the main road's west sidewalk clear of the bus stop's
    // bench and the poplars (x); the middles of the asphalt road and of its south sidewalk (z); the middle of the dirt
    // road (x).
    private const float Kerbside = 243.4f, Stop = 15, Walk = 241.8f, Mouth = -7, Sidewalk = -1.8f, Road = -8.8f;
    // Teaching building 1: outside door 1 (x) and its middle (z); the stairs' landing on each floor (x); the wide stair's
    // half landing (x), the flight up westward from a floor and the one on up eastward to the next (z).
    private const float WestOfDoor1 = 110, Door1 = 51.3f, Landing = 121, Half = 114.45f, UpWest = 46.74f, UpEast = 48.94f;
    // Teaching building 2: outside door 1 (x) and its middle (z), outside door 2 (x) and its middle (z); the middles of
    // the corridor on the atrium's north, east, south and west sides.
    private const float EastOfDoor1 = 67, Door1Z = 29.2f, WestOfDoor2 = 35, Door2Z = 37.2f;
    private const float NorthSide = 26.4f, EastSide = 56.1f, SouthSide = 67.6f, WestSide = 44.7f;
    // A spiral stair: its column, and the angle from north, clockwise, at which the corridors meet it. She climbs Line from
    // the column, and steps on and off it at Level, on the last tread of a turn, which is level with the floor. Both
    // buildings' storeys are as high.
    private readonly record struct Spiral(string Name, float X, float Z, float E);
    private static readonly Spiral North = new("north", 50.4f, 29.3f, 0), South = new("south", 50.4f, 64.7f, Mathf.Pi);
    private const float Tau = Mathf.Tau, Line = 1.2f, Level = -.9f * SpiralStair3D.Going, Storey = SpiralStair3D.Storey;
    private const int Floors = 6;
    // The mall: the middle of the passage (z), of its middle stair (x) and of the stair's landing upstairs (z); the upper
    // floor's level.
    private const float Passage = 107.12f, Stair = 60, Upstairs = 93.6f, Upper = 5.3f;

    private TurningFixture play = null!;
    private float turned;  // how far round a spiral stair she is from the ground floor's corridor side, clockwise, in radians
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/campus.tscn");

            // At the bus stop, by the kerb where the bus would stop. North along the sidewalk: the kerb by the sidewalk's
            // north end, and the north end by the kerb and by the estate's railing.
            play.LookAt(new(Kerbside, 0, Stop), new(Kerb, 1.3f, Stop));
            await play.Frames(3);
            await Against("the main road's kerb", (Kerbside, Stop), Kerb, true);
            await RunTo((Walk, Stop));
            await Against("the main road's kerb", (Walk, ByNorthEnd), Kerb, true);
            await Against("the sidewalk's north end", (ByKerb, NorthEnd + 3.5f), NorthEnd, false);
            await RunTo((ByKerb, NorthEnd + 3.5f));
            await Against("the sidewalk's north end", (ByEstate, NorthEnd + 3.5f), NorthEnd, false);

            // Back to the asphalt road, where the kerb stops her across its mouth; west along its south sidewalk, in at the
            // school's gate and across the plaza.
            await RunTo((ByEstate, NorthEnd + 3.5f));
            await Against("the main road's kerb", (Walk, Mouth), Kerb, true);
            await RunTo((Walk, Sidewalk), (236, Sidewalk));
            await FarTo((76, Sidewalk), (76, 2), (WestOfDoor1, Door1));
            Check(OnGround, $"she runs from the bus stop in at the school's gate and across the plaza; she is at {feet}");

            // Teaching building 1: in through door 1, up the wide stair to the top floor, down again and out.
            await RunTo((Landing, Door1));
            for (var floor = 1; floor < 4; floor++)
            {
                await RunTo((Landing, UpWest), (Half, UpWest), (Half, UpEast), (Landing, UpEast));
                Check(OnFloor(floor + 1), $"she runs up teaching building 1's wide stair to floor {floor + 1}; she is at {feet}");
            }
            for (var floor = 4; floor > 1; floor--)
            {
                await RunTo((Landing, UpEast), (Half, UpEast), (Half, UpWest), (Landing, UpWest));
                Check(OnFloor(floor - 1), $"she runs down teaching building 1's wide stair to floor {floor - 1}; she is at {feet}");
            }
            await RunTo((Landing, Door1), (WestOfDoor1, Door1));
            Check(OnGround, $"she runs out of teaching building 1's door 1; she is at {feet}");

            // Teaching building 2: in through door 1, up the north spiral stair to the top floor, down the south one, out
            // of door 2.
            await FarTo((EastOfDoor1, Door1Z));
            await RunTo((EastSide, Door1Z), (EastSide, NorthSide), Off(North), At(North, Level));
            turned = Angle(North);
            await Climb(North, (Floors - 1) * Tau + Level);
            await RunTo(Off(North));
            Check(OnFloor(Floors), $"she runs up teaching building 2's north spiral stair to the top floor; she is at {feet}");
            await RunTo((WestSide, NorthSide), (WestSide, SouthSide), Off(South), At(South, Level));
            turned = Angle(South) + (Floors - 1) * Tau;
            await Climb(South, Level);
            await RunTo(Off(South), (WestSide, SouthSide), (WestSide, Door2Z), (WestOfDoor2, Door2Z));
            Check(OnGround, $"she runs down teaching building 2's south spiral stair and out of door 2; she is at {feet}");

            // Out of the back gate, over the dirt road into the sports ground, and back onto the road.
            await FarTo((3, 8), (-21, 8));
            Check(OnGround, $"she runs out of the back gate, over the dirt road and into the sports ground; she is at {feet}");

            // Up the dirt road: the gap between the building site and the sports ground by the fence and by the hoarding,
            // then the road's north end by the hoarding and by the estate's railing. The runs across the belts of poplars
            // pass every trunk with 0.25 m or more to spare; the one by the railing starts north of a trunk near it.
            await FarTo((Road, 8), (Road, ByFence));
            await Against("the gap between the building site and the sports ground", (Gap + 3.6f, ByFence), Gap, true);
            await RunTo((Road, ByFence), (Road, ByHoardingSouth));
            await Against("the gap between the building site and the sports ground", (Gap + 3.6f, ByHoardingSouth), Gap, true);
            await RunTo((Road, ByHoardingSouth));
            await FarTo((Road, DirtEnd + 3.5f));
            await Against("the dirt road's north end", (ByHoarding, DirtEnd + 3.5f), DirtEnd, false);
            await RunTo((ByHoarding, DirtEnd + 3.5f), (Road, DirtEnd + 3.5f), (Road, DirtEnd + 2.3f));
            await Against("the dirt road's north end", (ByRailing, DirtEnd + 2.3f), DirtEnd, false);
            await RunTo((ByRailing, DirtEnd + 2.3f), (Road, DirtEnd + 2.3f));

            // Down the dirt road through the iron gate and the side door into the mall, up its middle stair and down.
            await FarTo((Road, Passage), (Stair, Passage));
            await RunTo((Stair, Upstairs));
            Check(Mathf.Abs(feet.Y - Upper) < .05f, $"she runs from the dirt road into the mall and up its middle stair to the upper floor; she is at {feet}");
            await RunTo((Stair, Passage));
            Check(OnGround, $"she runs down the mall's middle stair into the passage; she is at {feet}");

            // Along the passage, out of the east entrance onto the sidewalk: its south end by the mall and by the kerb, the
            // kerb by the south end. The run by the kerb starts south of the poplar at z 120. North to the bus stop.
            await FarTo((236, Passage));
            await RunTo((Walk, Passage));
            await Against("the sidewalk's south end", (ByMall, SouthEnd - 3.5f), SouthEnd, false);
            await Against("the main road's kerb", (Walk, BySouthEnd), Kerb, true);
            await Against("the sidewalk's south end", (ByKerb, SouthEnd - 1.12f), SouthEnd, false);
            await RunTo((Walk, BySouthEnd), (Walk, Stop));
            Check(OnGround, $"she runs out of the mall's east entrance back to the bus stop; she is at {feet}");

            GD.Print("Campus route checks passed: from the bus stop in at the school's gate, up both teaching buildings to their top floors, out of the back gate into the sports ground, along the dirt road into the mall and up to its upper floor, out of its east entrance and back to the bus stop; the main road's kerb, the sidewalk's north and south ends, the dirt road's north end and the gap between the building site and the sports ground stop her.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Engine.TimeScale = 1;
            Input.ActionRelease("move_forward");
            Input.ActionRelease("run");
        }
    }

    private bool OnGround => Mathf.Abs(feet.Y) < .05f;
    private bool OnFloor(int floor) => Mathf.Abs(feet.Y - (floor - 1) * Storey) < .05f;

    // Pushed from `from` toward a face past which her target lies, she stops by it on her side: her middle is about her
    // radius, 0.2 m, from it.
    private static bool Stopped(float at, float face, float from) => (at - face) * Mathf.Sign(from - face) is > .1f and < .5f;

    // How far round the stair she is from its corridor side, clockwise; how far she is from its column.
    private float Angle(Spiral s) => Mathf.Atan2(feet.X - s.X, s.Z - feet.Z) - s.E;
    private float Out(Spiral s) => new Vector2(feet.X - s.X, feet.Z - s.Z).Length();

    // The point Line from the stair's column that far round from its corridor side; the corridor's middle beside the landing.
    private static (float X, float Z) At(Spiral s, float angle) => (s.X + Line * Mathf.Sin(angle + s.E), s.Z - Line * Mathf.Cos(angle + s.E));
    private static (float X, float Z) Off(Spiral s) => (At(s, Level).X, s.E == 0 ? NorthSide : SouthSide);

    // The walking surface's height that far round from the ground floor, on the stair or on the floors at either end.
    private static float Height(float at)
    {
        var n = Mathf.Min(Mathf.FloorToInt(at / Tau), Floors - 2);
        return at <= 0 ? 0 : n * Storey + SpiralStair3D.Surface(at - n * Tau);
    }

    // Runs round the stair Line from its column until she is that far round, steering back onto that line each frame. She
    // is stuck if she gets no 1 cm nearer for half a second, and off the stair if 0.3 m off its height.
    private async Task Climb(Spiral s, float to)
    {
        var way = Mathf.Sign(to - turned);
        var best = (to - turned) * way;
        var still = 0;
        Input.ActionPress("move_forward");
        Input.ActionPress("run");
        while ((to - turned) * way > 0)
        {
            var a = Angle(s) + s.E;
            var heading = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * way + new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)) * (Line - Out(s)) * 2;
            play.Player.Rotation = new(0, Mathf.Atan2(-heading.X, -heading.Z), 0);
            await play.Frames(1);
            turned += Mathf.Wrap(Angle(s) - turned, -Mathf.Pi, Mathf.Pi);
            Check(Mathf.Abs(feet.Y - Height(turned)) < .3f, $"she has fallen off the {s.Name} spiral stair {turned:0.00} rad round; she is at {feet}");
            if ((to - turned) * way < best - .01f / Line) (best, still) = ((to - turned) * way, 0);
            else Check(++still < 30, $"she must run {(way > 0 ? "up" : "down")} the {s.Name} spiral stair to {to:0.00} rad round; she is stuck at {feet}");
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // Runs the player to each point in turn with the movement keys, in straight lines, and lets go of them there. She gets
    // ten seconds and one more for every 2 m to each point.
    private Task RunTo(params (float X, float Z)[] points) => Go(.1f, points);

    // The same at eight times the real speed. Each physics step then takes her about 0.5 m, so she gets within 0.3 m.
    private async Task FarTo(params (float X, float Z)[] points)
    {
        Engine.TimeScale = 8;
        await Go(.3f, points);
        Engine.TimeScale = 1;
    }

    private async Task Go(float near, (float X, float Z)[] points)
    {
        foreach (var point in points)
        {
            var target = new Vector3(point.X, 0, point.Z);
            var limit = 60 * (10 + Apart(feet, target) / 2);
            for (var frame = 0; Apart(feet, target) > near; frame++)
            {
                Check(frame < limit, $"the player must run to {point}; she is stopped at {feet}");
                await Toward(point);
            }
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // From `from`, runs at an unseen wall whose face lies across x, if she runs along x, or else across z, toward a point
    // 4 m past it; the wall must stop her by the face.
    private async Task Against(string wall, (float X, float Z) from, float face, bool alongX)
    {
        await RunTo(from);
        var start = alongX ? from.X : from.Z;
        var past = face + Mathf.Sign(face - start) * 4;
        await Push(alongX ? (past, from.Z) : (from.X, past));
        Check(Stopped(alongX ? feet.X : feet.Z, face, start) && OnGround, $"{wall} must stop her, run into from {from}; she is at {feet}");
    }

    // Runs toward a point for two seconds, longer than it takes to get there, whatever stands in the way.
    private async Task Push((float X, float Z) point)
    {
        for (var frame = 0; frame < 2 * 60; frame++) await Toward(point);
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    private async Task Toward((float X, float Z) point)
    {
        Input.ActionPress("move_forward");
        Input.ActionPress("run");
        play.Player.Rotation = new(0, Mathf.Atan2(feet.X - point.X, feet.Z - point.Z), 0);
        await play.Frames(1);
    }
}
