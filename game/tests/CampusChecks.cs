using System;
using System.Threading.Tasks;
using Game.Debugging;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The campus region's school grounds, walked with the movement keys: in through the gate, across the plaza and up the
/// steps onto the stand; into both rooms of the lodge and into the sports store; through the open bay under the toilet
/// block into the boys' toilet, then up the outside stair to the landing and into the girls' toilet. The steps and the
/// stair are walked and run, up and down. The school wall on each side, the stand's side, and the open edges of the stair
/// and its landing stop her. The routes keep clear of the sites left for the two teaching buildings.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class CampusChecks : Node
{
    private const float Stand = 1.2f, Upstairs = 3.6f;
    // The school wall's inner faces; the stand's west side and north face; the line the outside stair's railing keeps her
    // to, and the east end of its landing.
    private const float NorthWall = .12f, WestWall = .12f, SouthWall = 91.88f, EastWall = 148.88f;
    private const float StandWest = 79, StandNorth = 79.5f, StairEdge = 36.7f, LandingEnd = 146.7f;
    // Along the outside stair; west of its foot; on its landing, in front of the girls' door.
    private const float StairLine = 35.9f, StairFoot = 137.4f, Landing = 145.6f;

    private TurningFixture play = null!;
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/campus.tscn");
            Check(Apart(feet, new(76, 0, 2)) < .05f && OnGround, $"she starts inside the gate; she is at {feet}");
#if DEBUG
            Check(DebugScenes.Find("campus") == "res://regions/campus.tscn", "the debug entry starts the campus region");
#endif

            // In through the gate, across the plaza, round the flagpole's base to the foot of the steps.
            play.LookAt(new(76, 0, 0), new(76, 1.3f, 5));
            await WalkTo((76, 18), (82, 47), (82, 75.5f), (87, 76));
            Check(OnGround, $"she walks in through the gate and across the plaza to the steps; she is at {feet}");
            foreach (var run in new[] { false, true })
            {
                await Go(run, (87, 82));
                Check(Mathf.Abs(feet.Y - Stand) < .05f, $"she {Pace(run)} up the steps onto the stand; she is at {feet}");
                await Go(run, (87, 76));
                Check(OnGround, $"she {Pace(run)} down the steps; she is at {feet}");
            }
            await WalkTo((77, 76), (77, 82));
            await Push((81, 82));
            Check(Stopped(feet.X, StandWest) && OnGround, $"the stand's west side stops her; she is at {feet}");
            await WalkTo((77, 77.5f), (81, 77.5f));
            await Push((81, 81));
            Check(Stopped(feet.Z, StandNorth) && OnGround, $"beside the steps the stand's north face stops her; she is at {feet}");

            // The lodge: through its door from the gate into the duty room, and on into the gatekeeper's room.
            await WalkTo((81.5f, 70), (81.5f, 4.85f), (84.5f, 4.85f));
            Check(Inside(83.12f, 86.94f, .12f, 5.88f), $"she walks into the lodge's duty room; she is at {feet}");
            await WalkTo((86.2f, 5.05f), (88.5f, 5.05f));
            Check(Inside(87.06f, 89.88f, .12f, 5.88f), $"she walks on into the gatekeeper's room; she is at {feet}");
            await WalkTo((86.2f, 5.05f), (84.5f, 4.85f), (81.5f, 4.85f));

            // North of the site left for teaching building 2, over the courts and round to the sports store's door.
            await WalkTo((81.5f, 10), (25, 10), (25, 3));
            await Push((25, -3));
            Check(Stopped(feet.Z, NorthWall) && OnGround, $"the north wall stops her; she is at {feet}");
            await WalkTo((25, 10), (25, 64), (3, 64));
            await Push((-3, 64));
            Check(Stopped(feet.X, WestWall) && OnGround, $"the west wall stops her; she is at {feet}");
            await WalkTo((11, 64), (11, 69.5f));
            Check(Inside(3.12f, 18.88f, 67.12f, 74.88f), $"she walks into the sports store; she is at {feet}");

            // South of both teaching buildings' sites to the toilet block.
            await WalkTo((11, 64), (25, 64), (25, 88), (100, 88));
            await Push((100, 95));
            Check(Stopped(feet.Z, SouthWall) && OnGround, $"the south wall stops her; she is at {feet}");
            await WalkTo((100, 88), (146, 88));
            await Push((152, 88));
            Check(Stopped(feet.X, EastWall) && OnGround, $"the east wall stops her; she is at {feet}");

            // Through the open bay under the girls' toilet to the boys' door, round the screen inside it.
            await WalkTo((131, 88), (131, 31.5f), (StairFoot, 31.5f), (StairFoot, 27.7f), (140, 27.7f), (140, 24));
            Check(Inside(134.12f, 148.64f, 19.62f, 28.38f), $"she walks through the open bay into the boys' toilet; she is at {feet}");
            await WalkTo((140, 27.7f), (StairFoot, 27.7f), (StairFoot, 31.5f), (StairFoot, StairLine));
            Check(OnGround, $"she walks out of the bay between its columns to the foot of the outside stair; she is at {feet}");

            foreach (var run in new[] { false, true })
            {
                await Go(run, (Landing, StairLine));
                Check(OnLanding, $"she {Pace(run)} up the outside stair onto the landing; she is at {feet}");
                await Go(run, (StairFoot, StairLine));
                Check(OnGround, $"she {Pace(run)} down the outside stair; she is at {feet}");
            }
            await WalkTo((141.3f, StairLine));
            await Push((141.3f, 39));
            Check(Stopped(feet.Z, StairEdge) && feet.Y > 1.5f, $"the railing along the stair's open side stops her; she is at {feet}");
            await WalkTo((Landing, StairLine));
            await Push((Landing, 39));
            Check(Stopped(feet.Z, StairEdge) && OnLanding, $"the railing along the landing stops her; she is at {feet}");
            await WalkTo((Landing, StairLine));
            await Push((152, StairLine));
            Check(Stopped(feet.X, LandingEnd) && OnLanding, $"the railing across the landing's end stops her; she is at {feet}");

            // Through the girls' door, round the screen inside it, past the washing trough.
            await WalkTo((Landing, StairLine), (Landing, 33.9f), (143.4f, 33.9f), (143.4f, 28));
            Check(Inside(134.12f, 148.64f, 19.62f, 34.88f) && Mathf.Abs(feet.Y - Upstairs) < .05f,
                $"she walks from the landing into the girls' toilet; she is at {feet}");
            await WalkTo((143.4f, 33.9f), (Landing, 33.9f), (Landing, StairLine), (StairFoot, StairLine));
            Check(OnGround, $"she walks back out and down the stair; she is at {feet}");

            GD.Print("Campus checks passed: the start inside the gate; in through the gate, across the plaza and up the stand's steps, walking and running; the stand's side and north face; both rooms of the lodge; the sports store; the school wall on every side; through the open bay into the boys' toilet; up the outside stair, walking and running, onto the landing and into the girls' toilet; the railings of the stair and its landing.");
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

    private bool OnGround => Mathf.Abs(feet.Y) < .05f;
    private bool OnLanding => Mathf.Abs(feet.Y - Upstairs) < .05f;
    private bool Inside(float x0, float x1, float z0, float z1) => feet.X > x0 && feet.X < x1 && feet.Z > z0 && feet.Z < z1;
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
