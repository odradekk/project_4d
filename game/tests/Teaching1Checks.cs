using System;
using System.Threading.Tasks;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Teaching building 1 in the campus region, walked with the movement keys twice: from the gate in through door 1, up the
/// wide stair to the fourth floor, down the narrow stair at the north end to the ground floor, up the narrow stair at the
/// south end to the second floor and back down, and out of door 2. The first time she walks the stairs, and on every
/// floor she also goes into the water room, each classroom and the teachers' toilet, walking on the first and third
/// floors and running on the others: into a classroom by its front door, down its middle aisle and out of its back door,
/// past the chairs, which do not stop her. The second time she runs the stairs. On the top floor the rail where no flight
/// goes up stops her at each stair; a desk stops her.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class Teaching1Checks : Node
{
    // A stair: x of its half landing; z of the flight that goes up westward from a floor and of the one that goes on up
    // eastward to the next; x of the face of the top floor's rail. Its landing on each floor is at x Landing.
    private readonly record struct Stair(string Name, float Half, float West, float East, float Rail);
    private static readonly Stair North = new("north", 115.3f, 18.01f, 19.63f, 119.12f);
    private static readonly Stair Wide = new("wide", 114.45f, 46.74f, 48.94f, 118.92f);
    private static readonly Stair South = new("south", 115.3f, 78.65f, 80.27f, 119.12f);
    private const float Storey = 3.6f, Landing = 121, Corridor = 125;
    // Each classroom's north wall; the middle of the water room's door and of the toilet's.
    private static readonly float[] Classrooms = [20.44f, 28.84f, 37.24f, 52.64f, 61.04f, 69.44f];
    private const float WaterDoor = 15.4f, ToiletDoor = 82.88f;
    // Outside door 1 in the tower's west wall, and outside door 2 in the south wall.
    private const float Door1 = 51.3f, WestOfDoor1 = 110, Door2 = 125, SouthOfDoor2 = 87;

    private TurningFixture play = null!;
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/campus.tscn");
            foreach (var run in new[] { false, true })
            {
                if (run) await WalkTo((WestOfDoor1, SouthOfDoor2));
                await WalkTo((WestOfDoor1, Door1));
                await Go(run, (Landing, Door1));
                Check(OnFloor(1), $"she {Pace(run)} in through door 1 and along the passage beside the wide stair; she is at {feet}");
                for (var floor = 1; floor <= 4; floor++)
                {
                    if (!run) await Rooms(floor);
                    if (floor < 4) await Up(Wide, floor, run);
                }
                if (!run)
                    foreach (var stair in new[] { South, Wide, North })
                    {
                        await WalkTo((Corridor, feet.Z), (Corridor, stair.West), (Landing, stair.West));
                        await Push((WestOfDoor1, stair.West));
                        Check(Stopped(feet.X, stair.Rail) && OnFloor(4), $"on the top floor the rail at the {stair.Name} stair stops her; she is at {feet}");
                    }
                await WalkTo((Corridor, feet.Z), (Corridor, North.East));
                for (var floor = 4; floor > 1; floor--) await Down(North, floor, run);
                await WalkTo((Corridor, North.West), (Corridor, South.West));
                await Up(South, 1, run);
                await Down(South, 2, run);
                await Go(run, (Corridor, South.West), (Door2, SouthOfDoor2));
                Check(OnFloor(1), $"she {Pace(run)} out of door 2; she is at {feet}");
            }

            GD.Print("Teaching building 1 checks passed: in through door 1, up the wide stair to the fourth floor, down the north stair, up the south stair to the second floor and down, out of door 2, walking and running on the stairs; into every water room, classroom and teachers' toilet, walking and running; the top floor's rails; a desk.");
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

    // From the stair's landing on a floor up its two flights to the next floor, and down.
    private async Task Up(Stair stair, int floor, bool run)
    {
        await Go(run, (Landing, stair.West), (stair.Half, stair.West));
        Check(OnHalf(floor), $"she {Pace(run)} up the {stair.Name} stair from floor {floor} to its half landing; she is at {feet}");
        await Go(run, (stair.Half, stair.East), (Landing, stair.East));
        Check(OnFloor(floor + 1), $"she {Pace(run)} on up the {stair.Name} stair to floor {floor + 1}; she is at {feet}");
    }

    private async Task Down(Stair stair, int floor, bool run)
    {
        await Go(run, (Landing, stair.East), (stair.Half, stair.East));
        Check(OnHalf(floor - 1), $"she {Pace(run)} down the {stair.Name} stair from floor {floor} to its half landing; she is at {feet}");
        await Go(run, (stair.Half, stair.West), (Landing, stair.West));
        Check(OnFloor(floor - 1), $"she {Pace(run)} on down the {stair.Name} stair to floor {floor - 1}; she is at {feet}");
    }

    // Along the corridor into the water room at its north end, each classroom from the north, and the toilet at its south
    // end: round the screen inside the toilet's door, past its south end. The point she is checked at is inside the room.
    private async Task Rooms(int floor)
    {
        var run = floor % 2 == 0;
        await Go(run, (Corridor, feet.Z), (Corridor, WaterDoor), (118, WaterDoor));
        Check(OnFloor(floor), $"she {Pace(run)} into the water room on floor {floor}; she is at {feet}");
        await Go(run, (Corridor, WaterDoor));
        for (var i = 0; i < Classrooms.Length; i++)
        {
            var z = Classrooms[i];
            await Go(run, (Corridor, z + 1.1f), (122.8f, z + 1.1f), (122.8f, z + 1.75f), (119, z + 1.75f));
            if (floor == 1 && i == 0)
            {
                await WalkTo((120, z + 1.75f));
                await Push((120, z + 9));
                Check(Stopped(feet.Z, z + 2.4f), $"a desk in the front row stops her; she is at {feet}");
                await WalkTo((119, z + 1.75f));
            }
            await Go(run, (119, z + 7.95f));
            Check(OnFloor(floor), $"she {Pace(run)} into classroom {i + 1} on floor {floor} and down its middle aisle; she is at {feet}");
            await Go(run, (123, z + 7.95f), (123, z + 7.3f), (Corridor, z + 7.3f));
        }
        await Go(run, (Corridor, ToiletDoor), (122.7f, ToiletDoor), (122.7f, 84.15f), (119.5f, 84.15f), (119.5f, 82.9f));
        Check(OnFloor(floor), $"she {Pace(run)} round the screen into the teachers' toilet on floor {floor}; she is at {feet}");
        await Go(run, (119.5f, 84.15f), (122.7f, 84.15f), (122.7f, ToiletDoor), (Corridor, ToiletDoor), (Corridor, Wide.West));
    }

    private bool OnFloor(int floor) => Mathf.Abs(feet.Y - (floor - 1) * Storey) < .05f;
    private bool OnHalf(int floor) => Mathf.Abs(feet.Y - (floor - .5f) * Storey) < .05f;
    private static string Pace(bool run) => run ? "runs" : "walks";

    // Pushed toward a face past which her target lies, she stops by it: her middle is about her radius, 0.2 m, from it.
    private static bool Stopped(float at, float face) => Mathf.Abs(at - face) is > .1f and < .5f;

    private Task WalkTo(params (float X, float Z)[] points) => Go(true, points);

    // Walks or runs the player to each point in turn with the movement keys, in straight lines, and lets go of them there.
    // She gets ten seconds and one more for every 2 m to each point.
    private async Task Go(bool run, params (float X, float Z)[] points)
    {
        foreach (var point in points)
        {
            var target = new Vector3(point.X, 0, point.Z);
            var limit = 60 * (10 + Apart(feet, target) / 2);
            for (var frame = 0; Apart(feet, target) > .1f; frame++)
            {
                Check(frame < limit, $"the player must {(run ? "run" : "walk")} to {point}; she is stopped at {feet}");
                await Step(point, run);
            }
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // Walks toward a point for two seconds, longer than it takes to get there, whatever stands in the way.
    private async Task Push((float X, float Z) point)
    {
        for (var frame = 0; frame < 2 * 60; frame++) await Step(point, true);
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    private async Task Step((float X, float Z) point, bool run)
    {
        Input.ActionPress("move_forward");
        if (run) Input.ActionPress("run");
        else Input.ActionRelease("run");
        play.Player.Rotation = new(0, Mathf.Atan2(feet.X - point.X, feet.Z - point.Z), 0);
        await play.Frames(1);
    }
}
