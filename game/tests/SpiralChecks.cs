using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Player;
using Game.Regions;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Teaching building 2's spiral stair in a region of its own, three floors high, walked with the movement keys along a
/// line midway between the column and the rail: from the ground floor out onto the second and third floors' corridors and
/// back, and down again; outward against the rail and in toward the column round each turn, past the top landing and the
/// top corridor's edge, and in under the lowest turn; then up from the ground floor to the third and back down, walking
/// and running, from start points 3 mm apart over one physics step's travel, so that a step ends at every point along
/// the line; and once each nearer the column and nearer the rail.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class SpiralChecks : Node
{
    // The last tread of each turn, from −Going to 0 round from a floor, is level with that floor; she stands on it at
    // Landing, clear of the panel at 0 above the top floor.
    private const float Storey = SpiralStair3D.Storey, Going = SpiralStair3D.Going, Line = 1.2f, Corridor = -2.9f, Tau = Mathf.Tau, Landing = -.9f * Going;
    private TurningFixture play = null!;
    private float turned;  // how far round she is from the ground floor's corridor side, clockwise, in radians
    private readonly List<float> walkUp = [], walkDown = [], runUp = [], runDown = [];
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/spiral_test.tscn");
            await Stand(-.3f);
            await Climb(Tau + Landing, false);
            Check(OnFloor(1), $"she walks up a turn to the second floor's landing; she is at {feet}");
            await CorridorAndBack(1);
            await Climb(2 * Tau + Landing, false);
            Check(OnFloor(2), $"she walks up a turn to the third floor's landing; she is at {feet}");
            await Push(SpiralStair3D.At(Line, .5f, 0));
            Check(feet.X < 0 && OnFloor(2), $"above the top floor the panel across the stairwell stops her; she is at {feet}");
            await CorridorAndBack(2);
            await Climb(Tau + Going / 2, false);
            await WalkTo(feet.X, Corridor);
            Check(OnFloor(1) && feet.Z < Corridor + .3f, $"coming down, she walks off the ramp onto the second floor's corridor; she is at {feet}");
            await WalkTo(SpiralStair3D.At(Line, Landing, 0));
            turned = Angle + Tau;
            await Climb(Landing, false);
            Check(OnFloor(0), $"she walks down to the ground floor; she is at {feet}");

            for (var n = 0; n < 2; n++)
                foreach (var degrees in new[] { 45, 60, 120, 180, 240, 300, 315 })
                {
                    var at = n * Tau + Mathf.DegToRad(degrees);
                    await Stand(at);
                    await Push(SpiralStair3D.At(3, at, 0));
                    Check(Out < 1.6f && Mathf.Abs(feet.Y - Height(at)) < .3f, $"the rail stops her {degrees}° into the turn up from floor {n}; she is at {feet}");
                    await Stand(at);
                    await Push(Vector3.Zero);
                    Check(Out > .75f && Mathf.Abs(feet.Y - Height(at)) < .3f, $"toward the column she stops {degrees}° into the turn up from floor {n} where the ramp is still shallow enough to stand on; she is at {feet}");
                }
            play.LookAt(new(.6f, 2 * Storey, -2.4f), new(.6f, 2 * Storey + 1.3f, -1));
            await Push(new(.6f, 0, -1));
            Check(feet.Z < -1.9f && OnFloor(2), $"on the top floor the panel along the corridor's edge stops her walking into the stairwell; she is at {feet}");
            play.LookAt(SpiralStair3D.At(2.6f, Mathf.DegToRad(175), 0), new(0, 1.3f, 0));
            await Push(Vector3.Zero);
            Check(Out > 1.9f && OnFloor(0), $"from the ground floor she cannot walk in under the low end of the lowest turn; she is at {feet}");
            play.LookAt(SpiralStair3D.At(Line, Mathf.DegToRad(250), 0), SpiralStair3D.At(Line, Mathf.DegToRad(220), 1.3f));
            await Push(SpiralStair3D.At(Line, Mathf.DegToRad(220), 0));
            await Push(SpiralStair3D.At(Line, Mathf.DegToRad(170), 0));
            var under = Mathf.RadToDeg(Mathf.PosMod(Angle, Tau));
            Check(under > 180 && under < 200 && OnFloor(0), $"under the lowest turn a wall stops her before it is too low to walk under; she is at {feet}");
            await WalkTo(SpiralStair3D.At(2.6f, Mathf.DegToRad(250), 0));
            Check(OnFloor(0), $"she walks back out from under the stair; she is at {feet}");

            var passes = 0;
            foreach (var (run, speed) in new[] { (false, FirstPersonPlayer.WalkSpeed), (true, FirstPersonPlayer.RunSpeed) })
                for (var start = .25f; start < .25f + speed / Engine.PhysicsTicksPerSecond; start += .003f, passes++)
                {
                    await Stand(-start / Line);
                    await Climb(2 * Tau + Landing, run, run ? runUp : walkUp);
                    // Down from the top landing, which is too short for the same start points: from 5 cm past where the ramp
                    // starts down, and on 3 mm at a time in step with the way up.
                    await Stand(2 * Tau - Going + (start - .2f) / Line);
                    await Climb(Landing, run, run ? runDown : walkDown);
                }
            // On lines through two more of the ramp's rings, clear of the keep-out and the rail, which MovementChecks walks
            // pressed against.
            foreach (var r in new[] { .9f, 1.5f })
                foreach (var run in new[] { false, true })
                {
                    await Stand(-.25f / r, r);
                    await Climb(2 * Tau + Landing, run, r: r);
                    await Stand(2 * Tau - Going + .05f / r, r);
                    await Climb(Landing, run, r: r);
                }
            GD.Print($"Spiral checks passed: up to each floor's corridor and back, down off the ramp onto a corridor, the rail and the column's keep-out round each turn, the panels above the top, under the lowest turn; {passes} times up three floors and down 1.2 m from the column and twice each at 0.9 m and 1.5 m, none stuck or fallen. "
                + $"A storey takes {walkUp.Average():0.00} s walking up, {walkDown.Average():0.00} s walking down, {runUp.Average():0.00} s running up, {runDown.Average():0.00} s running down.");
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

    private float Angle => Mathf.Atan2(feet.X, -feet.Z);
    private float Out => new Vector2(feet.X, feet.Z).Length();
    private bool OnFloor(int floor) => Mathf.Abs(feet.Y - floor * Storey) < .05f;

    // The walking surface's height that far round, on the stair or on the floors at either end.
    private static float Height(float at)
    {
        var n = Mathf.Min(Mathf.FloorToInt(at / Tau), 1);
        return at <= 0 ? 0 : n * Storey + SpiralStair3D.Surface(at - n * Tau);
    }

    // From the landing at floor n out onto its corridor, and back onto the landing.
    private async Task CorridorAndBack(int n)
    {
        await WalkTo(feet.X, Corridor);
        Check(OnFloor(n) && feet.Z < Corridor + .3f, $"she walks off the landing onto the corridor of floor {n}; she is at {feet}");
        await WalkTo(SpiralStair3D.At(Line, Landing, 0));
        Check(OnFloor(n) && Out < 1.6f, $"she walks back from the corridor onto the landing of floor {n}; she is at {feet}");
        turned = Angle + n * Tau;
    }

    // Puts her r from the column that far round, standing still and facing round the way the stair climbs.
    private async Task Stand(float at, float r = Line)
    {
        var spot = SpiralStair3D.At(r, at, Height(at) + .02f);
        play.LookAt(spot, spot + new Vector3(Mathf.Cos(at), 1.3f, Mathf.Sin(at)));
        turned = at;
        await play.Frames(2);
    }

    // Walks or runs round r from the column until she is that far round, steering back onto that line each frame, and adds
    // the seconds she took for each whole storey to times. She is stuck if she gets no 1 cm nearer for half a second.
    private async Task Climb(float to, bool run, List<float>? times = null, float r = Line)
    {
        var way = Mathf.Sign(to - turned);
        var best = (to - turned) * way;
        var still = 0;
        ulong? crossed = null;
        Input.ActionPress("move_forward");
        if (run) Input.ActionPress("run");
        while ((to - turned) * way > 0)
        {
            var a = Angle;
            var heading = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * way + new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)) * (r - Out) * 2;
            play.Player.Rotation = new(0, Mathf.Atan2(-heading.X, -heading.Z), 0);
            await play.Frames(1);
            var before = turned;
            turned += Mathf.Wrap(Angle - turned, -Mathf.Pi, Mathf.Pi);
            Check(Mathf.Abs(feet.Y - Height(turned)) < .3f, $"she has fallen off the stair {turned:0.00} rad round; she is at {feet}");
            if ((to - turned) * way < best - .01f / r) (best, still) = ((to - turned) * way, 0);
            else Check(++still < 30, $"she must {(run ? "run" : "walk")} {(way > 0 ? "up" : "down")} the stair {r} m from the column to {to:0.00} rad round; she is stuck at {feet}");
            if (Mathf.FloorToInt(before / Tau) == Mathf.FloorToInt(turned / Tau)) continue;
            if (crossed is { } then) times?.Add((Engine.GetPhysicsFrames() - then) / (float)Engine.PhysicsTicksPerSecond);
            crossed = Engine.GetPhysicsFrames();
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    private Task WalkTo(float x, float z) => WalkTo(new Vector3(x, 0, z));

    // Walks straight to the point, ignoring its height.
    private async Task WalkTo(Vector3 point)
    {
        var limit = 60 * (10 + Apart(feet, point) / 2);
        Input.ActionPress("move_forward");
        for (var frame = 0; Apart(feet, point) > .1f; frame++)
        {
            Check(frame < limit, $"she must walk to {point}; she is stopped at {feet}");
            play.Player.Rotation = new(0, Mathf.Atan2(feet.X - point.X, feet.Z - point.Z), 0);
            await play.Frames(1);
        }
        Input.ActionRelease("move_forward");
    }

    // Walks toward the point for two seconds, longer than it takes to get there, whatever stands in the way.
    private async Task Push(Vector3 point)
    {
        Input.ActionPress("move_forward");
        for (var frame = 0; frame < 2 * 60; frame++)
        {
            play.Player.Rotation = new(0, Mathf.Atan2(feet.X - point.X, feet.Z - point.Z), 0);
            await play.Frames(1);
        }
        Input.ActionRelease("move_forward");
    }
}
