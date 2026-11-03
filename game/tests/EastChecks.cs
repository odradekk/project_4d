using System;
using System.Threading.Tasks;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The campus region's east side, walked with the movement keys: from the bus stop north along the main road's west
/// sidewalk, round the corner and west along the asphalt road's south sidewalk to the school's gate; then back out, in at
/// the staff compound's gate and down the alley to the wall at its south end, and through the openings in the compound's
/// wall into its yard and out again. The bus stop's bench, a shop front in each row, the wall at the alley's south end, the
/// compound's wall and a block of flats stop her.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class EastChecks : Node
{
    // The middle of the main road's west sidewalk clear of the bus stop's bench and the poplars (x), of the asphalt road's
    // south sidewalk (z), and of the alley (x).
    private const float Walk = 241.8f, Sidewalk = -1.8f, Alley = 150.5f;
    // The faces that stop her: the bench's front; the shop fronts in the east row and the north row; the wall at the alley's
    // south end; the compound's wall on the alley; a block of flats' west side. The lines through the middles of the fronts.
    private const float Bench = 241.1f, EastFront = 239.24f, NorthFront = -.04f, AlleyEnd = 89.88f, CompoundWall = 151.78f;
    private const float Flats = 160, EastShop = 30.5f, NorthShop = 194.4f;
    // The compound's yard inside its walls and rows of shops; the middles of the two openings from the alley (z).
    private const float YardWest = 152.02f, YardEast = 227.2f, YardNorth = 12, YardSouth = 92, NorthDoor = 17.5f, SouthDoor = 59.5f;

    private TurningFixture play = null!;
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/campus.tscn");

            // At the bus stop, by the kerb where the bus would stop, facing north.
            play.LookAt(new(243.4f, 0, 15), new(243.4f, 1.3f, 0));
            await play.Frames(3);
            await WalkTo((Walk, 15));
            await Push((239.5f, 15));
            Check(Stopped(feet.X, Bench, Walk) && OnGround, $"the bus stop's bench stops her: the stop stands there; she is at {feet}");
            await WalkTo((Walk, EastShop));
            await Push((236, EastShop));
            Check(Stopped(feet.X, EastFront, Walk) && OnGround, $"a shop front in the east row stops her; she is at {feet}");

            // North along the sidewalk, round the corner and west to the school's gate, past the north row of shops.
            await WalkTo((Walk, Sidewalk), (NorthShop, Sidewalk));
            await Push((NorthShop, 4));
            Check(Stopped(feet.Z, NorthFront, Sidewalk) && OnGround, $"a shop front in the north row stops her; she is at {feet}");
            await WalkTo((NorthShop, Sidewalk), (76, Sidewalk), (76, 2));
            Check(feet.Z > .12f && OnGround, $"she walks from the bus stop along the sidewalks in at the school's gate; she is at {feet}");

            // Back out, in at the compound's gate and down the alley to its south end.
            await WalkTo((76, Sidewalk), (Alley, Sidewalk), (Alley, 87));
            await Push((Alley, 95));
            Check(Stopped(feet.Z, AlleyEnd, 87) && OnGround, $"the wall at the alley's south end stops her; she is at {feet}");
            await WalkTo((Alley, 40));
            await Push((157, 40));
            Check(Stopped(feet.X, CompoundWall, Alley) && OnGround, $"the compound's wall stops her; she is at {feet}");

            // Through the north opening into the yard, to a block of flats, and out through the south opening.
            await WalkTo((Alley, NorthDoor), (156, NorthDoor));
            Check(Inside(YardWest, YardEast, YardNorth, YardSouth) && OnGround,
                $"she walks through the north opening in the compound's wall into its yard; she is at {feet}");
            await WalkTo((156, 27.5f));
            await Push((165, 27.5f));
            Check(Stopped(feet.X, Flats, 156) && OnGround, $"a block of flats stops her; she is at {feet}");
            await WalkTo((156, SouthDoor), (Alley, SouthDoor));
            Check(feet.X < CompoundWall && OnGround, $"she walks out through the south opening into the alley; she is at {feet}");

            GD.Print("East checks passed: from the bus stop along the main road's and the asphalt road's sidewalks in at the school's gate; the bus stop's bench; a shop front in each row; down the alley to the wall at its south end; the compound's wall; into the yard through the north opening and out through the south one; a block of flats.");
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

    // Pushed from `from` toward a face past which her target lies, she stops by it on her side: her middle is about her
    // radius, 0.2 m, from it.
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
