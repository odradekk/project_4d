using System;
using System.Threading.Tasks;
using Game.Debugging;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The home building of the compound region, walked with the movement keys: from the flat's entrance hall through the shared
/// vestibule, the open corridor and the stair to the top floor, then down and out of the ground floor. The railings and the
/// neighbor's front door stop her.
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
public partial class CompoundChecks : Node
{
    private const float Storey = 3.2f;
    // The middles of the flights that go up eastward and westward, the stair hall's landing at each floor, and the landing
    // at the turn; the flights run between x 1.39 and 6.33.
    private const float UpEast = 17.5f, UpWest = 19.55f, Landing = .8f, Turn = 7f;
    private const float CorridorRail = -1.72f, StairwellRail = 1.39f, BetweenFlights = 18.525f, UnderStairWall = 4.94f, NeighborDoor = 9.05f;

    private TurningFixture play = null!;
    private Vector3 feet => play.Player.Position;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(scene: "res://regions/compound.tscn");
            Check(Apart(feet, new(.75f, 0, 6.7f)) < .05f && OnFloor(1), $"she starts in the entrance hall of the flat on the second floor; she is at {feet}");

            await WalkTo((.75f, 8.2f));
            await Push((.75f, 10));
            Check(feet.Z < NeighborDoor, $"the neighbor's front door is shut; she is at {feet}");
            await WalkTo((-.9f, 8.2f));
            await Push((-3, 8.2f));
            Check(feet.X > CorridorRail && OnFloor(1), $"the corridor's railing stops her; she is at {feet}");

            await WalkTo((-.9f, 18.5f), (Landing, UpEast), (Turn, UpEast), (Turn, UpWest), (Landing, UpWest));
            Check(OnFloor(2), $"the stair takes her up to the top floor; she is at {feet}");
            await WalkTo((Landing, UpEast));
            await Push((3, UpEast));
            Check(feet.X < StairwellRail && OnFloor(2), $"the railing at the top of the stair stops her; she is at {feet}");

            // She stops short of the point, on the top treads, where the wall below is too low to hold her.
            await WalkTo((Landing, UpWest), (1.85f, UpWest));
            await Push((feet.X, 16.5f));
            Check(feet.Z > BetweenFlights && feet.Y > 6, $"the railing between the flights stops her at the top of the stair; she is at {feet}");

            await WalkTo((Turn, UpWest), (Turn, UpEast), (Landing, UpEast));
            Check(OnFloor(1), $"she walks down to the second floor; she is at {feet}");
            await WalkTo((Landing, UpWest), (Turn, UpWest), (Turn, UpEast), (Landing, UpEast));
            Check(OnFloor(0), $"she walks down to the ground floor; she is at {feet}");
            await WalkTo((Landing, UpWest));
            await Push((Turn, UpWest));
            Check(feet.X < UnderStairWall && OnFloor(0), $"under the stair she walks on the floor up to the wall; she is at {feet}");
            await WalkTo((Landing, UpWest));
            Check(OnFloor(0), $"she walks back out from under the stair; she is at {feet}");
            await WalkTo((-3, 18.5f));
            Check(feet.X < CorridorRail && Mathf.Abs(feet.Y + .2f) < .05f, $"she walks out of the west side of the stair hall onto the ground; she is at {feet}");
#if DEBUG
            Check(DebugScenes.Find("compound") == "res://regions/compound.tscn", "the debug entry starts the compound region");
#endif
            GD.Print("Compound checks passed: entrance hall, neighbor's door, corridor railing, up the stair, top railings, down, under the stair and out.");
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

    private bool OnFloor(int floor) => Mathf.Abs(feet.Y - floor * Storey) < .05f;

    // Walks the player to each point in turn with the movement keys, in straight lines, and lets go of them there.
    private async Task WalkTo(params (float X, float Z)[] points)
    {
        foreach (var point in points)
            for (var frame = 0; Apart(feet, new(point.X, 0, point.Z)) > .3f; frame++)
            {
                Check(frame < 10 * 60, $"the player must walk to {point}; she is stopped at {feet}");
                await Step(point);
            }
        Input.ActionRelease("move_forward");
        Input.ActionRelease("run");
    }

    // Walks toward a point for two seconds, longer than it takes to get there, whatever stands in the way.
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
