using System;
using System.Threading.Tasks;
using Game.Player;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The campus region's mall tunnel, walked with the movement keys. First up and down the middle stair on three lines
/// across it, walking and running, from start points 3 mm apart over one physics step's travel, so that her steps end at
/// every point along the treads; and once the walk up it on which she stopped dead on the treads' edges. Then from the
/// main road's west sidewalk in at the east entrance and along the passage to the forecourt at its west end; from the dirt
/// road through the iron gate and the side door into the passage; up the west stair to the upper floor, along the middle
/// aisle from its west end to its east end, and down and up again the middle and east stairs on the way. A shop front in
/// each row, the forecourt's railing on each side, the railings round each stairwell upstairs and the booths of every row
/// stop her; where she touches a shop front shows that the front, not the block behind it, is what stops her.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class MallChecks : Node
{
    // The middle of the main road's west sidewalk (x); the middles of the passage, and upstairs of the north, middle and
    // south aisles (z); the upper floor's level.
    private const float Walk = 241.8f, Passage = 107.12f, NorthAisle = 97.2f, Aisle = 107.25f, SouthAisle = 117.2f, Upper = 5.3f;
    // The shop fronts in the north and south rows, and the lines through the middles of the two she runs into.
    private const float NorthFront = 103.16f, SouthFront = 111.08f, NorthShop = 227.7f, SouthShop = 228.6f;
    // The forecourt: the faces of its railing on the west, north and south, and a line through it clear of the posts (x).
    private const float ForecourtWest = -128.55f, ForecourtNorth = 99.17f, ForecourtSouth = 115.07f, Forecourt = -125;
    // The middle of the side door (x), and a point on the dirt road north of the iron gate (z).
    private const float SideDoor = -8.8f, DirtRoad = 80;
    // Each stair's middle (x); on its landing (z); the faces of the railing south of its stairwell and of those beside it,
    // which stand this far from its middle; a line across the stairwell (z), and one south of it (z).
    private static readonly float[] Stairs = [-80, 60, 200];
    private const float Landing = 93.6f, StairwellSouth = 103.17f, StairwellSide = 1.55f, Across = 99, SouthOfWell = 106;
    // The faces of the booths' fronts, from the north: the row against the north wall, both sides of the two rows back to
    // back, the row against the south wall; and the lines through booths they are run into at (x).
    private const float Row1 = 95.36f, Row2North = 99, Row2South = 105, Row3North = 109.5f, Row3South = 115.5f, Row4 = 118.88f;
    private const float NorthBooths = -87, MiddleBooths = -100, SouthBooths = -66, CrossAisle = -61.6f;
    // The ends of the middle aisle upstairs (x).
    private const float AisleWest = -119.5f, AisleEast = 237;
    // Where each stair's slope meets the passage's floor and the top tread's level (z).
    private const float SlopeFoot = 103.41f, SlopeTop = 95.04f;

    private TurningFixture play = null!;
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/campus.tscn");

            // A walk up the middle stair, 1 m west of its middle, on which she stopped dead when its slope ran through the
            // treads' front edges; then that stair on three lines, up from the passage and down from the top tread. The three
            // stairs are one scene placed three times, with the same slope and the same blocks and slabs beside them; only
            // the middle one stands on a join, the cut in the passage's floor along its middle. So sweeping it covers all
            // three, and the route below walks the other two.
            await Climb(new(59, .004f, Passage), SlopeTop - .25f, false);
            Check(OnFloor, $"she walks up the middle stair 1 m west of its middle from the passage; she is at {feet}");
            var climbs = 1;
            foreach (var x in new[] { Stairs[1] - 1, Stairs[1], Stairs[1] + 1 })
                foreach (var (run, speed) in new[] { (false, FirstPersonPlayer.WalkSpeed), (true, FirstPersonPlayer.RunSpeed) })
                    for (var start = .25f; start < .25f + speed / Engine.PhysicsTicksPerSecond; start += .003f, climbs += 2)
                    {
                        await Climb(new(x, .004f, SlopeFoot + start), SlopeTop - .25f, run);
                        Check(OnFloor, $"she {(run ? "runs" : "walks")} up the middle stair at x {x} to the upper floor; she is at {feet}");
                        await Climb(new(x, Upper, SlopeTop - start), SlopeFoot + .25f, run);
                        Check(OnGround, $"she {(run ? "runs" : "walks")} down the middle stair at x {x} into the passage; she is at {feet}");
                    }

            // On the main road's west sidewalk beside the mall's north row of shops, facing south; in at the east entrance.
            play.LookAt(new(Walk, 0, 100), new(Walk, 1.3f, 120));
            await play.Frames(3);
            await WalkTo((Walk, Passage), (236, Passage));
            Check(Inside(-121.6f, 239.2f, NorthFront, SouthFront) && OnGround,
                $"she walks from the main road's sidewalk in at the mall's east entrance; she is at {feet}");
            await WalkTo((NorthShop, Passage));
            await Push((NorthShop, 100));
            Check(Stopped(feet.Z, NorthFront, Passage) && Mathf.Abs(Touched().Z - NorthFront) < .005f && OnGround,
                $"a shop front in the north row stops her; she is at {feet}, touching {Touched()}");
            await WalkTo((SouthShop, Passage));
            await Push((SouthShop, 114));
            Check(Stopped(feet.Z, SouthFront, Passage) && Mathf.Abs(Touched().Z - SouthFront) < .005f && OnGround,
                $"a shop front in the south row stops her; she is at {feet}, touching {Touched()}");

            // West along the passage to the forecourt, and against its railing on each side.
            await WalkTo((Forecourt, Passage));
            Check(Inside(-128.6f, -121.6f, 99.12f, 115.12f) && OnGround,
                $"she walks along the passage to the forecourt at its west end; she is at {feet}");
            await Push((-133, Passage));
            Check(Stopped(feet.X, ForecourtWest, Forecourt) && OnGround, $"the forecourt's railing on the west stops her; she is at {feet}");
            await WalkTo((Forecourt, 101));
            await Push((Forecourt, 95));
            Check(Stopped(feet.Z, ForecourtNorth, 101) && OnGround, $"the forecourt's railing on the north stops her; she is at {feet}");
            await WalkTo((Forecourt, 113));
            await Push((Forecourt, 119));
            Check(Stopped(feet.Z, ForecourtSouth, 113) && OnGround, $"the forecourt's railing on the south stops her; she is at {feet}");

            // Out through the side door to the dirt road, and back in from it through the iron gate and the side door.
            await WalkTo((Forecourt, Passage), (SideDoor, Passage), (SideDoor, DirtRoad));
            Check(feet.Z < 88.85f && OnGround, $"she walks out through the side door and the iron gate onto the dirt road; she is at {feet}");
            await WalkTo((SideDoor, Passage));
            Check(Inside(-121.6f, 239.2f, NorthFront, SouthFront) && OnGround,
                $"she walks from the dirt road through the iron gate and the side door into the passage; she is at {feet}");

            // Up the west stair; the north aisle and its booths on both sides.
            await Up(Stairs[0]);
            await WalkTo((Stairs[0] - 3, NorthAisle), (NorthBooths, NorthAisle));
            await Push((NorthBooths, 92));
            Check(Stopped(feet.Z, Row1, NorthAisle) && OnFloor, $"a booth in the row against the north wall stops her; she is at {feet}");
            await WalkTo((NorthBooths, NorthAisle));
            await Push((NorthBooths, 103));
            Check(Stopped(feet.Z, Row2North, NorthAisle) && OnFloor, $"a booth on the north side of the northern back-to-back row stops her; she is at {feet}");

            // The middle aisle from its west end, its booths on both sides; the south aisle through a cross aisle.
            await WalkTo((Stairs[0] - 3, NorthAisle), (Stairs[0] - 3, Aisle), (MiddleBooths, Aisle));
            await Push((MiddleBooths, 101));
            Check(Stopped(feet.Z, Row2South, Aisle) && OnFloor, $"a booth on the south side of the northern back-to-back row stops her; she is at {feet}");
            await WalkTo((MiddleBooths, Aisle));
            await Push((MiddleBooths, 113));
            Check(Stopped(feet.Z, Row3North, Aisle) && OnFloor, $"a booth on the north side of the southern back-to-back row stops her; she is at {feet}");
            await WalkTo((AisleWest, Aisle));
            Check(OnFloor, $"she walks to the middle aisle's west end; she is at {feet}");
            await WalkTo((CrossAisle, Aisle), (CrossAisle, SouthAisle), (SouthBooths, SouthAisle));
            await Push((SouthBooths, 122));
            Check(Stopped(feet.Z, Row4, SouthAisle) && OnFloor, $"a booth in the row against the south wall stops her; she is at {feet}");
            await WalkTo((SouthBooths, SouthAisle));
            await Push((SouthBooths, 112));
            Check(Stopped(feet.Z, Row3South, SouthAisle) && OnFloor, $"a booth on the south side of the southern back-to-back row stops her; she is at {feet}");

            // On east along the middle aisle, down each of the other two stairs and up again, to the aisle's east end.
            await WalkTo((SouthBooths, SouthAisle), (CrossAisle, SouthAisle), (CrossAisle, Aisle));
            foreach (var stair in Stairs[1..])
            {
                await WalkTo((stair + 3, Aisle), (stair + 3, Landing), (stair, Landing), (stair, Passage));
                Check(OnGround, $"she walks down the stair at x {stair} into the passage; she is at {feet}");
                await Up(stair);
                await WalkTo((stair - 3, Aisle));
            }
            await WalkTo((AisleEast, Aisle));
            Check(OnFloor, $"she walks the middle aisle to its east end; she is at {feet}");

            GD.Print($"Mall checks passed: {climbs} times up or down the middle stair, none stuck; from the main road's sidewalk in at the east entrance and along the passage to the forecourt; a shop front in each row; the forecourt's railing on three sides; from the dirt road through the iron gate and the side door into the passage; up each stair, and down the middle and east ones; the railings round each stairwell; the booths of every row; along the middle aisle from end to end.");
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

    // From the passage up a stair to its landing, then against the railings round its stairwell: the one on the east, the
    // one on the south and the one on the west, each from the side away from the stairwell.
    private async Task Up(float stair)
    {
        await WalkTo((stair, Passage), (stair, Landing));
        Check(OnFloor, $"she walks up the stair at x {stair} to the upper floor; she is at {feet}");
        await WalkTo((stair + 3, Landing), (stair + 3, Across));
        await Push((stair - 3, Across));
        Check(Stopped(feet.X, stair + StairwellSide, stair + 3) && OnFloor, $"the railing east of the stairwell at x {stair} stops her; she is at {feet}");
        await WalkTo((stair + 3, Across), (stair + 3, SouthOfWell), (stair, SouthOfWell));
        await Push((stair, Across));
        Check(Stopped(feet.Z, StairwellSouth, SouthOfWell) && OnFloor, $"the railing south of the stairwell at x {stair} stops her; she is at {feet}");
        await WalkTo((stair, SouthOfWell), (stair - 3, SouthOfWell), (stair - 3, Across));
        await Push((stair + 3, Across));
        Check(Stopped(feet.X, stair - StairwellSide, stair - 3) && OnFloor, $"the railing west of the stairwell at x {stair} stops her; she is at {feet}");
    }

    // From `from`, walks or runs north or south along its line until she is past z `to`, steering back onto the line each
    // frame. She is stuck if she gets no 1 cm nearer for half a second.
    private async Task Climb(Vector3 from, float to, bool run)
    {
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
        play.LookAt(from, new(from.X, 1.3f, to));
        await play.Frames(2);
        var way = Mathf.Sign(to - from.Z);
        var best = (to - feet.Z) * way;
        var still = 0;
        Input.ActionPress("move_forward");
        if (run) Input.ActionPress("run");
        while ((to - feet.Z) * way > 0)
        {
            play.Player.Rotation = new(0, Mathf.Atan2(feet.X - from.X, feet.Z - to), 0);
            await play.Frames(1);
            if ((to - feet.Z) * way < best - .01f) (best, still) = ((to - feet.Z) * way, 0);
            else Check(++still < 30, $"she must {(run ? "run" : "walk")} {(way < 0 ? "up" : "down")} the stair from {from}; she is stuck at {feet}");
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    private bool OnGround => Mathf.Abs(feet.Y) < .05f;
    private bool OnFloor => Mathf.Abs(feet.Y - Upper) < .05f;
    private bool Inside(float x0, float x1, float z0, float z1) => feet.X > x0 && feet.X < x1 && feet.Z > z0 && feet.Z < z1;

    // Pushed from `from` toward a face past which her target lies, she stops by it on her side: her middle is about her
    // radius, 0.2 m, from it.
    private static bool Stopped(float at, float face, float from) => (at - face) * Mathf.Sign(from - face) is > .1f and < .5f;

    // Where she touched what stopped her in the last physics frame: the contact of her last slide that does not hold her up.
    // A shop's front is 4 cm thick on its solid block, so this tells the front from the block behind it.
    private Vector3 Touched()
    {
        for (var i = 0; i < play.Player.GetSlideCollisionCount(); i++)
        {
            var hit = play.Player.GetSlideCollision(i);
            if (Mathf.Abs(hit.GetNormal().Y) < .1f) return hit.GetPosition();
        }
        return new(float.NaN, float.NaN, float.NaN);
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
