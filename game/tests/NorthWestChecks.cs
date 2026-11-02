using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The campus region's north and west sides, walked with the movement keys: out of the gate and west along the asphalt
/// road's south sidewalk to the dirt road; up the dirt road to the building site's north end; across the road and along the
/// estate's lawn strip; down the dirt road between the stalls to the iron gate; and from inside the school out of the back
/// gate, over the dirt road into the sports ground and once round its track. The building site's hoarding, the estate's
/// railing on its west and south sides and its shut gate, a vendor's stool, and the sports ground's wire fence on each side
/// stop her.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class NorthWestChecks : Node
{
    // The middle of the dirt road (x), of the south sidewalk (z), and of the lawn strip outside the estate's railing (z).
    private const float Road = -8.8f, Sidewalk = -1.8f, Lawn = -15;
    // Lines through the belts of poplars clear of their trunks: to the building site's hoarding, and to the estate's railing.
    private const float ToHoarding = -69.9f, ToRailing = -59.7f;
    // The faces that stop her: the hoarding's east side; the estate's low wall on its west and south sides and its gate;
    // the east side of the stool by the first stall on the road's west edge, and the line through it.
    private const float Hoarding = -16.95f, EstateWest = -.72f, EstateSouth = -19.68f, EstateGate = -19.76f;
    private const float Stool = -11.15f, StoolLine = 54.65f;
    // The sports ground's wire fence, the inner faces of its east, south, west and north sides.
    private const float FieldEast = -17.63f, FieldSouth = 91.97f, FieldWest = -80.27f, FieldNorth = -27.77f;
    // The track: the middle of its four lanes is Lane from the middles of the half circles at its ends.
    private const float TrackX = -49, NorthEnd = 5.3f, SouthEnd = 60.3f, Lane = 24.44f;

    private TurningFixture play = null!;
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/campus.tscn");

            // Out of the gate onto the south sidewalk, and west along it to the dirt road.
            await WalkTo((76, Sidewalk), (Road, Sidewalk));
            Check(OnGround, $"she walks out of the gate and west along the south sidewalk to the dirt road; she is at {feet}");

            // Up the dirt road, past the building site to its north end.
            await WalkTo((Road, ToHoarding), (-13, ToHoarding));
            await Push((-21, ToHoarding));
            Check(Stopped(feet.X, Hoarding, -13) && OnGround, $"the building site's hoarding stops her; she is at {feet}");
            await WalkTo((Road, ToHoarding), (Road, -105.5f));
            Check(OnGround, $"she walks up the dirt road to the building site's north end; she is at {feet}");

            // The estate's railing, from the belt east of the dirt road and from the lawn strip, and its gate.
            await WalkTo((Road, ToRailing), (-4.5f, ToRailing));
            await Push((3, ToRailing));
            Check(Stopped(feet.X, EstateWest, -4.5f) && OnGround, $"the estate's railing on its west side stops her; she is at {feet}");
            await WalkTo((Road, ToRailing), (Road, -12), (50, -12), (50, -16));
            await Push((50, -24));
            Check(Stopped(feet.Z, EstateSouth, -16) && OnGround, $"the estate's railing on its south side stops her; she is at {feet}");
            await WalkTo((50, Lawn), (105, Lawn), (105, -16));
            await Push((105, -24));
            Check(Stopped(feet.Z, EstateGate, -16) && OnGround, $"the estate's shut gate stops her; she is at {feet}");

            // Back across to the dirt road, and down it between the stalls to the iron gate.
            await WalkTo((105, -12), (Road, -12), (Road, StoolLine));
            await Push((-14, StoolLine));
            Check(Stopped(feet.X, Stool, Road) && OnGround, $"a vendor's stool stops her: the stalls are set out; she is at {feet}");
            await WalkTo((Road, StoolLine), (Road, 91.5f));
            Check(Inside(-11.86f, -5.74f, 88.85f, 92) && OnGround,
                $"she walks down the dirt road between the stalls to the iron gate, between its open leaves; she is at {feet}");

            // Into the school by the back gate and out again, over the dirt road through the sports ground's gate.
            await WalkTo((Road, 8), (3, 8));
            Check(feet.X > .12f && OnGround, $"she walks in through the back gate; she is at {feet}");
            await WalkTo((Road, 8), (-21, 8));
            Check(Inside(FieldWest, FieldEast, FieldNorth, FieldSouth) && OnGround,
                $"she walks out of the back gate, over the dirt road and into the sports ground; she is at {feet}");

            // Once round the track along the middle of its lanes, stepping off it to each side of the fence.
            await WalkTo((TrackX + Lane, 8), (TrackX + Lane, 20), (-21, 20));
            await Push((-14, 20));
            Check(Stopped(feet.X, FieldEast, -21) && OnGround, $"the sports ground's fence on its east side stops her; she is at {feet}");
            await WalkTo((TrackX + Lane, 20), (TrackX + Lane, SouthEnd));
            await WalkTo(Round(SouthEnd, 0, 90));
            await WalkTo((TrackX, 88.5f));
            await Push((TrackX, 96));
            Check(Stopped(feet.Z, FieldSouth, 88.5f) && OnGround, $"the sports ground's fence on its south side stops her; she is at {feet}");
            await WalkTo(Round(SouthEnd, 90, 180));
            await WalkTo((TrackX - Lane, 40), (-77, 40));
            await Push((-84, 40));
            Check(Stopped(feet.X, FieldWest, -77) && OnGround, $"the sports ground's fence on its west side stops her; she is at {feet}");
            await WalkTo((TrackX - Lane, 40), (TrackX - Lane, NorthEnd));
            await WalkTo(Round(NorthEnd, 180, 270));
            await WalkTo((TrackX, -24.5f));
            await Push((TrackX, -31));
            Check(Stopped(feet.Z, FieldNorth, -24.5f) && OnGround, $"the sports ground's fence on its north side stops her; she is at {feet}");
            await WalkTo(Round(NorthEnd, 270, 360));
            await WalkTo((TrackX + Lane, 8));
            Check(OnGround, $"she runs once round the track; she is at {feet}");

            GD.Print("North and west checks passed: out of the gate and along the south sidewalk to the dirt road; up the dirt road to the building site's north end; the hoarding; the estate's railing on two sides and its gate; a vendor's stool; down the dirt road between the stalls to the iron gate; out of the back gate into the sports ground; once round the track; the sports ground's fence on every side.");
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
    private bool Inside(float x0, float x1, float z0, float z1) => feet.X > x0 && feet.X < x1 && feet.Z > z0 && feet.Z < z1;

    // Points every 15° along the middle of the track's lanes round the half circle about (TrackX, z), from one angle to
    // another, both in degrees from east toward south.
    private static (float X, float Z)[] Round(float z, int from, int to) =>
        Enumerable.Range(1, (to - from) / 15).Select(i => Mathf.DegToRad(from + 15 * i))
            .Select(a => (TrackX + Lane * Mathf.Cos(a), z + Lane * Mathf.Sin(a))).ToArray();

    // Pushed from `from` toward a face past which her target lies, she stops by it on her side: her middle is about her
    // radius, 0.2 m, from it. Something else close behind the face, such as the barrier arm behind the estate's gate,
    // stops her on the far side.
    private static bool Stopped(float at, float face, float from) => (at - face) * Mathf.Sign(from - face) is > .1f and < .5f;

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
