using System;
using System.Threading.Tasks;
using Game.Debugging;
using Game.Player;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The campus region's school grounds, walked with the movement keys: in through the gate, across the plaza and up the
/// steps onto the stand; into both rooms of the lodge and into the sports store; through the open bay under the toilet
/// block into the boys' toilet, then up the outside stair to the landing and into the girls' toilet. The steps and the
/// stair are each walked and run up and down on three lines across them, from start points 3 mm apart over one physics
/// frame's travel. The school wall on each side, the stand's side, the railings on both sides of the stair and the open
/// edges of its landing stop her. The routes keep clear of the two teaching buildings.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class CampusChecks : Node
{
    private const float Stand = 1.2f, Upstairs = 3.6f;
    // The school wall's inner faces; the stand's west side and north face; the lines the outside stair's railings keep her
    // between, and the east end of its landing.
    private const float NorthWall = .12f, WestWall = .12f, SouthWall = 91.88f, EastWall = 148.88f;
    private const float StandWest = 79, StandNorth = 79.5f, StairNorth = 35.22f, StairSouth = 36.7f, LandingEnd = 146.7f;
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
            // The steps' slope leaves the ground at z 76.58 and meets the stand at z 79.08.
            var walks = await Sweep((a, c) => new(c, 0, a), 76.58f, 79.08f, Stand, 83.5f, 87, 90.5f);
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

            // North of teaching building 2, over the courts and round to the sports store's door.
            await WalkTo((81.5f, 10), (25, 10), (25, 3));
            await Push((25, -3));
            Check(Stopped(feet.Z, NorthWall) && OnGround, $"the north wall stops her; she is at {feet}");
            await WalkTo((25, 10), (25, 64), (3, 64));
            await Push((-3, 64));
            Check(Stopped(feet.X, WestWall) && OnGround, $"the west wall stops her; she is at {feet}");
            await WalkTo((11, 64), (11, 69.5f));
            Check(Inside(3.12f, 18.88f, 67.12f, 74.88f), $"she walks into the sports store; she is at {feet}");

            // South of both teaching buildings to the toilet block.
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

            // The stair's slope leaves the ground at x 138.22 and meets the landing at x 144.4.
            walks += await Sweep((a, c) => new(a, 0, c), 138.22f, 144.4f, Upstairs, 35.5f, StairLine, 36.4f);
            await WalkTo((141.3f, StairLine));
            await Push((141.3f, 39));
            Check(Stopped(feet.Z, StairSouth) && feet.Y > 1.5f, $"the railing along the stair's south side stops her; she is at {feet}");
            // West of the bay's middle column, so that only the railing can stop her.
            await WalkTo((140, StairLine));
            await Push((140, 33));
            Check(Stopped(feet.Z, StairNorth) && feet.Y > .5f, $"the railing along the stair's north side keeps her out of the bay; she is at {feet}");
            await WalkTo((Landing, StairLine));
            await Push((Landing, 39));
            Check(Stopped(feet.Z, StairSouth) && OnLanding, $"the railing along the landing stops her; she is at {feet}");
            await WalkTo((Landing, StairLine));
            await Push((152, StairLine));
            Check(Stopped(feet.X, LandingEnd) && OnLanding, $"the railing across the landing's end stops her; she is at {feet}");

            // Through the girls' door, round the screen inside it, past the washing trough.
            await WalkTo((Landing, StairLine), (Landing, 33.9f), (143.4f, 33.9f), (143.4f, 28));
            Check(Inside(134.12f, 148.64f, 19.62f, 34.88f) && Mathf.Abs(feet.Y - Upstairs) < .05f,
                $"she walks from the landing into the girls' toilet; she is at {feet}");
            await WalkTo((143.4f, 33.9f), (Landing, 33.9f), (Landing, StairLine), (StairFoot, StairLine));
            Check(OnGround, $"she walks back out and down the stair; she is at {feet}");

            GD.Print($"Campus checks passed: the start inside the gate; in through the gate and across the plaza; {walks} walks and runs up or down the stand's steps and the outside stair, from start points 3 mm apart on three lines across each, none stopped; the stand's side and north face; both rooms of the lodge; the sports store; the school wall on every side; through the open bay into the boys' toilet; up the outside stair onto the landing and into the girls' toilet; the railings on both sides of the stair and its landing.");
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

    // Up a flight and down again on each line across it, walking and running, from start points 3 mm apart over one physics
    // frame's travel, so that a frame ends at every distance from each tread's front edge. at(a, c) is the point a along the
    // flight and c across it; its slope leaves the ground at foot and reaches height at top. She goes up until at least
    // 0.5 m past the top, which on the steps takes her across the top tread onto the stand. Returns the number of walks.
    private async Task<int> Sweep(Func<float, float, Vector3> at, float foot, float top, float height, params float[] lines)
    {
        var walks = 0;
        foreach (var line in lines)
            foreach (var (run, speed) in new[] { (false, FirstPersonPlayer.WalkSpeed), (true, FirstPersonPlayer.RunSpeed) })
                for (var start = .25f; start < .25f + speed / Engine.PhysicsTicksPerSecond; start += .003f, walks += 2)
                {
                    await Cross(run, at(foot - start, line), at(top + .6f, line) with { Y = height });
                    await Cross(run, at(top + start, line) with { Y = height }, at(foot - .25f, line));
                }
        return walks;
    }

    // Puts her at from, standing still, then walks or runs her straight toward to until she is within 0.1 m of it, at its
    // height. Like the spiral stair's check, she fails if half a second passes without her getting 1 cm nearer.
    private async Task Cross(bool run, Vector3 from, Vector3 to)
    {
        play.LookAt(from, to + new Vector3(0, 1.3f, 0));
        await play.Frames(2);
        var (best, still) = (Apart(from, to), 0);
        while (Apart(feet, to) > .1f)
        {
            await Step((to.X, to.Z), run);
            if (Apart(feet, to) < best - .01f) (best, still) = (Apart(feet, to), 0);
            else Check(++still < 30, $"she must {(run ? "run" : "walk")} from {from} to {to}; she is stuck at {feet}");
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
        Check(Mathf.Abs(feet.Y - to.Y) < .05f, $"she {Pace(run)} from {from} to {to}; she is at {feet}");
    }

    // Runs the player to each point in turn with the movement keys, in straight lines, and lets go of them there. She gets
    // ten seconds and one more for every 2 m to each point.
    private async Task WalkTo(params (float X, float Z)[] points)
    {
        foreach (var point in points)
        {
            var target = new Vector3(point.X, 0, point.Z);
            var limit = 60 * (10 + Apart(feet, target) / 2);
            for (var frame = 0; Apart(feet, target) > .1f; frame++)
            {
                Check(frame < limit, $"the player must run to {point}; she is stopped at {feet}");
                await Step(point, true);
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
