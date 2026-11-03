using System;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Player;
using Game.Regions;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// How the player moves along what stops her and up what holds her, walked and run with the movement keys in the spiral
/// stair's region with a yard beside it. Pressed against a rail that bends round in boxes she goes its whole length, and
/// pressed against the spiral stair's rail, and against the keep-out round its column, she goes up a turn and back down. Into a slope
/// steeper than she can stand on, and into a step 10 cm high, at an angle, she rises no more than 2 cm; nearly straight into
/// the step, she stops. Pushed into the corner of two walls she stands still, and into the corner of a wall and the step or
/// the steep slope she rises no more than 2 cm. On a slope of 44° she stands still without sliding, and she walks and runs
/// up it.
/// </summary>
public partial class MovementChecks : Node
{
    // The bent rail: 12 boxes, each 15° of a half circle round Bend whose inner face is BendRadius from it.
    private const float BendRadius = 2.5f;
    private static readonly Vector3 Bend = new(10, 0, 0);
    // The corners: of two upright walls, along +x and 120° round from it toward +z; and of an upright wall 60° round from the
    // edge of a step, and from the foot of a slope, both along +x.
    private static readonly Vector3 Walls = new(8, 0, -5), StepCorner = new(13.5f, 0, 3.5f), SlopeCorner = new(13.5f, 0, -5.5f);
    // The steep slope, the step and the slope she can stand on all start at x = Foot and rise toward +x.
    private const float Foot = 17, Steep = 50, Step = .1f, Slope = 44, SlopeLength = 3;
    // On the stair, steering for 1.7 m from the column keeps her against the rail, whose boxes start 1.75 m out, and steering
    // for 0.7 m against the keep-out, whose faces are 0.6 m out.
    private const float RailLine = 1.7f, KeepOutLine = .7f, Landing = Mathf.Tau - .9f * SpiralStair3D.Going;
    private TurningFixture play = null!;
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(AddYard, "res://regions/spiral_test.tscn");
            foreach (var run in new[] { false, true })
            {
                var how = run ? "runs" : "walks";
                await Stand(Bend + SpiralStair3D.At(BendRadius - .3f, .1f, .01f), Bend);
                await Round(Bend, BendRadius + .1f, .1f, Mathf.Pi - .1f, run);

                await Stand(SpiralStair3D.At(1.5f, Mathf.Pi / 4, SpiralStair3D.Surface(Mathf.Pi / 4) + .02f), Vector3.Zero);
                await Round(Vector3.Zero, RailLine, Mathf.Pi / 4, Landing, run);
                Check(OnSecondFloor, $"she {how} up a turn of the spiral stair against its rail; she is at {feet}");
                await Round(Vector3.Zero, RailLine, Landing, -.25f, run);
                Check(Mathf.Abs(feet.Y) < .05f, $"she {how} down a turn of the spiral stair against its rail; she is at {feet}");
                await Stand(SpiralStair3D.At(1, -.3f, .02f), Vector3.Zero);
                await Round(Vector3.Zero, KeepOutLine, -.3f, Landing, run);
                Check(OnSecondFloor, $"she {how} up a turn of the spiral stair against the keep-out round its column; she is at {feet}");
                await Round(Vector3.Zero, KeepOutLine, Landing, -.25f, run);
                Check(Mathf.Abs(feet.Y) < .05f, $"she {how} down a turn of the spiral stair against the keep-out round its column; she is at {feet}");

                foreach (var degrees in new[] { 20, 45 })
                {
                    var rise = await Straight(new(Foot - .5f, .01f, -7.5f), degrees, run);
                    Check(rise < .02f, $"she {how} {degrees}° into a {Steep}° slope and is lifted {rise:0.000} m");
                    rise = await Straight(new(Foot - .5f, .01f, 2.5f), degrees, run);
                    Check(rise < .02f, $"she {how} {degrees}° into a step {Step} m high and is lifted {rise:0.000} m");
                }
                // Within WallMinSlideAngle, 15°, of straight into a wall, she stops. Her capsule meets the step's edge 0.33 m on
                // from where she starts, so she goes tan 10° × 0.33 = 0.06 m along it, and then no further.
                await Straight(new(Foot - .5f, .01f, 2.5f), 10, run);
                Check(feet.Z < 2.6f, $"she {how} 10° from straight into the step and stops; she is at {feet}");

                var (_, wander) = await Corner(Walls, 60, run);
                Check(wander < .01f, $"she {how} into the corner of two walls 120° apart and goes {wander:0.000} m in its last second");
                var (highest, _) = await Corner(StepCorner, 30, run);
                Check(highest < .02f, $"she {how} into the corner of a wall and the step and is lifted {highest:0.000} m");
                (highest, _) = await Corner(SlopeCorner, 30, run);
                Check(highest < .02f, $"she {how} into the corner of a wall and a {Steep}° slope and is lifted {highest:0.000} m");

                var top = SlopeLength * Mathf.Sin(Mathf.DegToRad(Slope));
                await Straight(new(Foot - .5f, .01f, 0), 0, run, 240, () => feet.Y > top - .1f);
                Check(feet.Y > top - .1f, $"she {how} up a {Slope}° slope; she is at {feet}");
            }
            // Halfway up, where the bottom of her capsule rests on the slope: 0.2 (1 / cos 44° - 1) = 0.078 m above it.
            var halfway = new Vector3(Foot + SlopeLength / 2 * Mathf.Cos(Mathf.DegToRad(Slope)), SlopeLength / 2 * Mathf.Sin(Mathf.DegToRad(Slope)) + .09f, 0);
            play.LookAt(halfway, halfway + new Vector3(1, 1.3f, 0));
            await play.Frames(10);
            var still = feet;
            await play.Frames(120);
            Check(feet.DistanceTo(still) < .001f, $"she stands still for 2 s on a {Slope}° slope; she moved from {still} to {feet}");
            GD.Print($"Movement checks passed: walking and running along a rail of boxes, up and down the spiral stair against its rail and its keep-out, into a {Steep}° slope and a {Step} m step without rising, stopping nearly straight into the step, into corners without moving or rising, and up a {Slope}° slope, on which she stands without sliding.");
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

    private bool OnSecondFloor => Mathf.Abs(feet.Y - SpiralStair3D.Storey) < .05f;

    // Beside the region's ground: a yard, the bent rail, the steep slope, the step and the corners, each a fixed object; and,
    // in the props, the slope she can stand on, which only holds her up, as the compound's stair slopes do.
    private static void AddYard(Region3D region)
    {
        var part = GD.Load<PackedScene>("res://regions/box_part.tscn");
        var fixedObjects = region.GetNode("Sections/Ⅰ/Fixed");
        FixedObject3D Add(string id)
        {
            var entity = new FixedObject3D { Name = id, EntityId = id };
            fixedObjects.AddChild(entity);
            return entity;
        }
        void Box(FixedObject3D entity, Vector3 centre, Vector3 size, Basis basis)
        {
            var box = part.Instantiate<BoxPart3D>();
            box.PartId = $"p{entity.GetChildCount() + 1}";
            box.Mesh = new BoxMesh { Size = size };
            box.Transform = new Transform3D(basis, centre);
            entity.AddChild(box);
        }
        Box(Add("yard"), new(14, -.1f, 0), new(16, .2f, 16), Basis.Identity);
        var rail = Add("rail");
        const float piece = Mathf.Pi / 12, thick = .15f;
        for (var k = 0; k < 12; k++)
        {
            var a = (k + .5f) * piece;
            Box(rail, Bend + SpiralStair3D.At(BendRadius + thick / 2, a, .55f), new(2 * (BendRadius + thick) * Mathf.Tan(piece / 2), 1.1f, thick), new Basis(Vector3.Up, -a));
        }
        // A box whose top face rises from the ground at x = Foot as steeply as that.
        var steep = Mathf.DegToRad(Steep);
        const float length = 4, depth = .5f;
        Box(Add("steep"), new(Foot + length / 2 * Mathf.Cos(steep) + depth / 2 * Mathf.Sin(steep), length / 2 * Mathf.Sin(steep) - depth / 2 * Mathf.Cos(steep), -5),
            new(length, depth, 6), new Basis(Vector3.Back, steep));
        Box(Add("step"), new(Foot + .5f, Step / 2, 5), new(1, Step, 6), Basis.Identity);
        var corners = Add("corners");
        // A wall 1.1 m high whose face runs 2 m from the corner that many degrees round from +x toward +z, facing a quarter
        // turn further round when side is 1, and a quarter turn back when it is -1.
        void Upright(Vector3 corner, float degrees, float side)
        {
            var a = Mathf.DegToRad(degrees);
            var along = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var facing = new Vector3(-along.Z, 0, along.X) * side;
            Box(corners, corner + along * .9f - facing * .1f + new Vector3(0, .55f, 0), new(2.2f, 1.1f, .2f), new Basis(Vector3.Up, -a));
        }
        Upright(Walls, 0, 1);
        Upright(Walls, 120, -1);
        Upright(StepCorner, 60, -1);
        Box(corners, StepCorner + new Vector3(1.25f, Step / 2, -.5f), new(2.5f, Step, 1), Basis.Identity);
        Upright(SlopeCorner, 60, -1);
        Box(corners, SlopeCorner + new Vector3(1.25f, length / 2 * Mathf.Sin(steep) - depth / 2 * Mathf.Cos(steep), -depth / 2 * Mathf.Sin(steep) - length / 2 * Mathf.Cos(steep)),
            new(2.5f, depth, length), new Basis(Vector3.Right, steep));
        var slope = Mathf.DegToRad(Slope);
        var body = new StaticBody3D
        {
            Name = "Slope", CollisionMask = 0, Rotation = new(0, 0, slope),
            Position = new(Foot + SlopeLength / 2 * Mathf.Cos(slope) + .05f * Mathf.Sin(slope), SlopeLength / 2 * Mathf.Sin(slope) - .05f * Mathf.Cos(slope), 0)
        };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(SlopeLength, .1f, 2) } });
        region.GetNode("Sections/Ⅰ/Props").AddChild(body);
    }

    // Puts her feet there, facing round centre the way the angles grow, as SpiralStair3D.At measures them.
    private async Task Stand(Vector3 spot, Vector3 centre)
    {
        var a = Mathf.Atan2(spot.X - centre.X, -(spot.Z - centre.Z));
        play.LookAt(spot, spot + new Vector3(Mathf.Cos(a), 1.3f, Mathf.Sin(a)));
        await play.Frames(2);
    }

    // Walks or runs round centre from `from` to `to` round from its −z, steering each frame for `steer` from it, which a wall
    // in the way keeps her from reaching; `from` is where she stands, or that plus whole turns. She is stuck if she gets no
    // 1 cm further round for half a second. In a physics step she must not go more than 1 cm further than her speed takes
    // her: Godot pushing her back out of a wall she presses into adds a few millimetres, and a step taken twice adds 3.7 cm.
    private async Task Round(Vector3 centre, float steer, float from, float to, bool run)
    {
        float Angle() => Mathf.Atan2(feet.X - centre.X, -(feet.Z - centre.Z));
        var speed = run ? FirstPersonPlayer.RunSpeed : FirstPersonPlayer.WalkSpeed;
        var (turned, way) = (from, Mathf.Sign(to - from));
        var best = (to - turned) * way;
        var still = 0;
        Input.ActionPress("move_forward");
        if (run) Input.ActionPress("run");
        while ((to - turned) * way > 0)
        {
            var a = Angle();
            var heading = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * way + new Vector3(Mathf.Sin(a), 0, -Mathf.Cos(a)) * (steer - Apart(feet, centre)) * 2;
            play.Player.Rotation = new(0, Mathf.Atan2(-heading.X, -heading.Z), 0);
            var (spot, tick) = (feet, Engine.GetPhysicsFrames());
            await play.Frames(1);
            var reach = speed * (Engine.GetPhysicsFrames() - tick) / Engine.PhysicsTicksPerSecond;
            Check(Apart(feet, spot) < reach + .01f,$"she goes {Apart(feet, spot):0.000} m in a step from {spot}, further than her speed takes her, {reach:0.000} m");
            turned += Mathf.Wrap(Angle() - turned, -Mathf.Pi, Mathf.Pi);
            if ((to - turned) * way < best - .01f / steer) (best, still) = ((to - turned) * way, 0);
            else Check(++still < 30, $"she must {(run ? "run" : "walk")} round to {to:0.00} rad steering for {steer} m from {centre}; she is stuck at {feet}");
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // Walks or runs for two seconds into the corner, from 1 m out along the line that many degrees from +x toward +z, and
    // returns the highest her feet were and how far she went in the last second.
    private async Task<(float highest, float wander)> Corner(Vector3 corner, float degrees, bool run)
    {
        var a = Mathf.DegToRad(degrees);
        play.LookAt(corner + new Vector3(Mathf.Cos(a), .01f, Mathf.Sin(a)), corner + new Vector3(0, 1.3f, 0));
        await play.Frames(2);
        var (highest, wander) = (feet.Y, 0f);
        Input.ActionPress("move_forward");
        if (run) Input.ActionPress("run");
        for (var frame = 0; frame < 120; frame++)
        {
            var from = feet;
            await play.Frames(1);
            highest = Mathf.Max(highest, feet.Y);
            if (frame >= 60) wander += from.DistanceTo(feet);
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
        return (highest, wander);
    }

    // Walks or runs from the spot, that many degrees from +x toward +z, for that many frames or until done, and returns the
    // highest her feet were.
    private async Task<float> Straight(Vector3 spot, float degrees, bool run, int frames = 90, Func<bool>? done = null)
    {
        var heading = new Vector3(Mathf.Cos(Mathf.DegToRad(degrees)), 0, Mathf.Sin(Mathf.DegToRad(degrees)));
        play.LookAt(spot, spot + heading + new Vector3(0, 1.3f, 0));
        await play.Frames(2);
        var highest = feet.Y;
        Input.ActionPress("move_forward");
        if (run) Input.ActionPress("run");
        for (var frame = 0; frame < frames && done?.Invoke() != true; frame++)
        {
            await play.Frames(1);
            highest = Mathf.Max(highest, feet.Y);
        }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
        return highest;
    }
}
