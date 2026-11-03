using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Regions;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Teaching building 2 in the campus region, walked with the movement keys: from the gate in through door 1, up the north
/// spiral stair a floor at a time to the sixth floor, down the south spiral stair to the ground floor, and out of door 2.
/// On every floor she goes round the corridor into each of the 16 rooms, the water room and the teachers' toilet among
/// them, round the screen inside the toilet's door; above the ground floor the parapet on each side of the atrium stops her.
/// She walks up the stair, runs down it, and runs everywhere else.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class Teaching2Checks : Node
{
    // A spiral stair: its column, and the angle from north, clockwise, at which the corridors meet it.
    private readonly record struct Spiral(string Name, float X, float Z, float E);
    private static readonly Spiral North = new("north", 50.4f, 29.3f, 0), South = new("south", 50.4f, 64.7f, Mathf.Pi);
    // She climbs Line from the column. The last tread of each turn, up to 20° short of the corridor side, is level with
    // the floor; she steps on and off the stair there, at Landing.
    private const float Storey = SpiralStair3D.Storey, Tau = Mathf.Tau, Line = 1.2f, Landing = -.9f * SpiralStair3D.Going;
    private const int Floors = 6;
    // The middle of the corridor on each side of the atrium; the faces of the atrium's parapets toward it.
    private const float NorthSide = 26.4f, EastSide = 56.1f, SouthSide = 67.6f, WestSide = 44.7f;
    private const float NorthFace = 27.52f, EastFace = 54.98f, SouthFace = 66.48f, WestFace = 45.82f;
    // The north walls of the rooms east of the atrium from the north, and of those west of it from the south.
    private static readonly float[] East = [25.2f, 33.2f, 42.1f, 51, 59.9f], West = [59.6f, 50.4f, 41.2f, 33.2f, 25.2f];
    // The middle of door 1 in the east wall and of door 2 in the west wall, and points outside them.
    private const float Door1 = 29.2f, EastOfDoor1 = 67, Door2 = 37.2f, WestOfDoor2 = 35;

    private TurningFixture play = null!;
    private float turned;  // how far round the stair she is from the ground floor's corridor side, clockwise, in radians
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/campus.tscn");
            await RunTo((EastOfDoor1, Door1), (61, Door1));
            Check(OnFloor(1), $"she runs in through door 1 into the hall; she is at {feet}");
            await RunTo((EastSide, Door1), (EastSide, NorthSide), Off(North));
            for (var floor = 1; ; floor++)
            {
                await Tour(floor);
                if (floor == Floors) break;
                await RunTo(At(North, Landing));
                turned = Angle(North) + (floor - 1) * Tau;
                await Climb(North, floor * Tau + Landing, false);
                Check(OnFloor(floor + 1), $"she walks up the north stair to floor {floor + 1}; she is at {feet}");
                await RunTo(Off(North));
                Check(OnFloor(floor + 1), $"she runs off the north stair onto the corridor of floor {floor + 1}; she is at {feet}");
            }
            await RunTo((WestSide, NorthSide), (WestSide, SouthSide), Off(South), At(South, Landing));
            turned = Angle(South) + (Floors - 1) * Tau;
            await Climb(South, Landing, true);
            Check(OnFloor(1), $"she runs down the south stair to the ground floor; she is at {feet}");
            await RunTo(Off(South), (WestSide, SouthSide), (WestSide, Door2), (WestOfDoor2, Door2));
            Check(OnFloor(1), $"she runs out of door 2; she is at {feet}");

            GD.Print("Teaching building 2 checks passed: in through door 1, up the north spiral stair to the sixth floor, walking, down the south one to the ground floor, running, out of door 2; into every room on every floor, the water room and the teachers' toilet among them; the atrium's parapets on every side above the ground floor.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.ActionRelease("move_forward");
            Input.ActionRelease("run");
        }
    }

    // From the corridor north of the north stair round the corridor clockwise and back, into each room from the water room
    // in the north-west corner on, and above the ground floor into the parapets. The paths into the rooms north and east of
    // the atrium keep clear of the classrooms' platforms and of the first row of desks or benches in any room there.
    private async Task Tour(int floor)
    {
        await Room(floor, "the water room", (WestSide, NorthSide), (WestSide, 23.6f), (41.5f, 21.6f));
        await Room(floor, "room 2", (47.25f, NorthSide), (47.25f, 24.6f), (47.5f, 22.5f));
        await Parapet(floor, (47.4f, NorthSide), (47.4f, 31), NorthFace);
        await Room(floor, "room 3", (56.25f, NorthSide), (56.25f, 24.6f), (56.5f, 22.5f));
        for (var i = 0; i < East.Length; i++)
        {
            await Room(floor, $"room {i + 4}", (EastSide, East[i] + 1.1f), (57.7f, East[i] + 1.4f), (59.9f, East[i] + 1.6f));
            if (i == 2) await Parapet(floor, (EastSide, 47), (51.4f, 47), EastFace);
        }
        await Room(floor, "room 9", (EastSide, SouthSide), (EastSide, 70));
        await Room(floor, "room 10", (53.8f, SouthSide), (53.8f, 70));
        await Parapet(floor, (47.4f, SouthSide), (47.4f, 63), SouthFace);
        await Room(floor, "the teachers' toilet", (WestSide, SouthSide), (WestSide, 69.6f), (43.3f, 69.6f), (43.3f, 72.5f));
        for (var i = 0; i < West.Length; i++)
        {
            await Room(floor, $"room {i + 12}", (WestSide, West[i] + 1.1f), (41.5f, West[i] + 1.1f));
            if (i == 1) await Parapet(floor, (WestSide, 47), (49.4f, 47), WestFace);
        }
        await RunTo((WestSide, NorthSide), Off(North));
    }

    // Along the path from the corridor into the room, and back out the same way.
    private async Task Room(int floor, string name, params (float X, float Z)[] path)
    {
        await RunTo(path);
        Check(OnFloor(floor), $"she runs into {name} on floor {floor}; she is at {feet}");
        await RunTo([.. path.Reverse().Skip(1)]);
    }

    // From the corridor toward a point in the atrium, past the parapet's face. The ground floor has no parapets.
    private async Task Parapet(int floor, (float X, float Z) from, (float X, float Z) toward, float face)
    {
        if (floor == 1) return;
        await RunTo(from);
        await Push(toward);
        Check(Stopped(from.X == toward.X ? feet.Z : feet.X, face) && OnFloor(floor), $"on floor {floor} the atrium's parapet stops her; she is at {feet}");
    }

    private bool OnFloor(int floor) => Mathf.Abs(feet.Y - (floor - 1) * Storey) < .05f;

    // Pushed toward a face past which her target lies, she stops by it: her middle is about her radius, 0.2 m, from it.
    private static bool Stopped(float at, float face) => Mathf.Abs(at - face) is > .1f and < .5f;

    // How far round the stair she is from its corridor side, clockwise; how far she is from its column.
    private float Angle(Spiral s) => Mathf.Atan2(feet.X - s.X, s.Z - feet.Z) - s.E;
    private float Out(Spiral s) => new Vector2(feet.X - s.X, feet.Z - s.Z).Length();

    // The point Line from the stair's column that far round from its corridor side; the corridor's middle beside the landing.
    private static (float X, float Z) At(Spiral s, float angle) => (s.X + Line * Mathf.Sin(angle + s.E), s.Z - Line * Mathf.Cos(angle + s.E));
    private static (float X, float Z) Off(Spiral s) => (At(s, Landing).X, s.E == 0 ? NorthSide : SouthSide);

    // The walking surface's height that far round from the ground floor, on the stair or on the floors at either end.
    private static float Height(float at)
    {
        var n = Mathf.Min(Mathf.FloorToInt(at / Tau), Floors - 2);
        return at <= 0 ? 0 : n * Storey + SpiralStair3D.Surface(at - n * Tau);
    }

    // Walks or runs round the stair Line from its column until she is that far round, steering back onto that line each
    // frame. She is stuck if she gets no 1 cm nearer for half a second, and off the stair if 0.3 m off its height.
    private async Task Climb(Spiral s, float to, bool run)
    {
        var way = Mathf.Sign(to - turned);
        var best = (to - turned) * way;
        var still = 0;
        Input.ActionPress("move_forward");
        if (run) Input.ActionPress("run");
        while ((to - turned) * way > 0)
        {
            var a = Angle(s) + s.E;
            var heading = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * way + new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)) * (Line - Out(s)) * 2;
            play.Player.Rotation = new(0, Mathf.Atan2(-heading.X, -heading.Z), 0);
            await play.Frames(1);
            turned += Mathf.Wrap(Angle(s) - turned, -Mathf.Pi, Mathf.Pi);
            Check(Mathf.Abs(feet.Y - Height(turned)) < .3f, $"she has fallen off the {s.Name} stair {turned:0.00} rad round; she is at {feet}");
            if ((to - turned) * way < best - .01f / Line) (best, still) = ((to - turned) * way, 0);
            else Check(++still < 30, $"she must {(run ? "run" : "walk")} {(way > 0 ? "up" : "down")} the {s.Name} stair to {to:0.00} rad round; she is stuck at {feet}");
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // Runs the player to each point in turn with the movement keys, in straight lines, and lets go of them there. She gets
    // ten seconds and one more for every 2 m to each point.
    private async Task RunTo(params (float X, float Z)[] points)
    {
        foreach (var point in points)
        {
            var target = new Vector3(point.X, 0, point.Z);
            var limit = 60 * (10 + Apart(feet, target) / 2);
            for (var frame = 0; Apart(feet, target) > .1f; frame++)
            {
                Check(frame < limit, $"the player must run to {point}; she is stopped at {feet}");
                await Step(point);
            }
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // Runs toward a point for two seconds, longer than it takes to get there, whatever stands in the way.
    private async Task Push((float X, float Z) point)
    {
        for (var frame = 0; frame < 2 * 60; frame++) await Step(point);
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    private async Task Step((float X, float Z) point)
    {
        Input.ActionPress("move_forward");
        Input.ActionPress("run");
        play.Player.Rotation = new(0, Mathf.Atan2(feet.X - point.X, feet.Z - point.Z), 0);
        await play.Frames(1);
    }
}
