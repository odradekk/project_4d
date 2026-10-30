using System;
using System.Threading.Tasks;
using Game.Debugging;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The compound region, walked with the movement keys: from the flat's entrance hall through the shared vestibule, the open
/// corridor and the stair to the top floor, then down and out of the ground floor, along the roads and out of the gate to
/// the bus stop; and from every other building's exit along its path or lane to the road to the gate. The railings, the
/// fence and the shut front doors stop her.
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
public partial class CompoundChecks : Node
{
    private const float Storey = 3.2f;
    // The middles of the flights that go up eastward and westward, the stair hall's landing at each floor, and the landing
    // at the turn; the flights run between x 1.39 and 6.33.
    private const float UpEast = 17.5f, UpWest = 19.55f, Landing = .8f, Turn = 7f;
    private const float CorridorRail = -1.72f, StairwellRail = 1.39f, BetweenFlights = 18.525f, UnderStairWall = 4.94f, NeighborDoor = 9.05f;
    private const float FlatDoor = 7.35f, Exit = 18.5f;
    // The buildings' origins; the middles of the road along the west side, the road to the gate and, from a building's
    // origin, the lane west of it; and the fence on either side.
    private static readonly float[] Cols = [0, 21, 42, 63], Rows = [0, 45, 105, 150];
    private const float WestRoad = -14.5f, CrossRoad = 93.5f, Lane = -5.22f, WestFence = -24, EastFence = 78;

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
            Check(feet.X > 1.5f && feet.X < 1.62f, $"she stands on the top treads, where only the railing holds her; she is at {feet}");
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
            Check(feet.X < CorridorRail && OnGround, $"she walks out of the west side of the stair hall onto the ground; she is at {feet}");

            Engine.TimeScale = 8;  // the walks across the compound are long
            await WalkTo((WestRoad, 18.5f));
            await Push((-30, 19.5f));
            Check(feet.X > WestFence, $"the west fence stops her; she is at {feet}");
            // North of the sidewalk, where only the fence is in her way.
            await WalkTo((WestRoad, 19.5f), (WestRoad, CrossRoad), (70, CrossRoad), (76, 91), (76, 52));
            await Push((85, 52));
            Check(feet.X < EastFence, $"the east fence stops her; she is at {feet}");
            await WalkTo((76, 91), (76, CrossRoad), (81, CrossRoad), (82, 80), (82, 71));
            Check(feet.X > 80.6f && feet.X < 83 && feet.Z > 67 && feet.Z < 75 && Mathf.Abs(feet.Y + .1f) < .05f,
                $"she walks out of the gate and along the sidewalk into the bus shelter; she is at {feet}");

            foreach (var x in Cols)
                foreach (var z in Rows)
                {
                    if (x == 0 && z == 0) continue;
                    play.LookAt(new(x + Landing, 0, z + Exit), new(x - 3, 1.3f, z + Exit));
                    await WalkTo((x - 3, z + Exit));
                    if (x == 0) await WalkTo((WestRoad, z + Exit), (WestRoad, CrossRoad));
                    else await WalkTo((x + Lane, z + Exit), (x + Lane, CrossRoad));
                    Check(OnGround, $"she walks out of the building at ({x}, {z}) and along its path to the road to the gate; she is at {feet}");
                }

            // The building east of ours has, where Xiaoman's flat is in ours, a flat like all the others.
            play.LookAt(new(21 + Landing, 0, Exit), new(21 + Turn, 1.3f, UpEast));
            await WalkTo((21 + Landing, UpEast), (21 + Turn, UpEast), (21 + Turn, UpWest), (21 + Landing, UpWest), (21 - .9f, Exit), (21 - .9f, 8.2f), (21 + .75f, 8.2f));
            await Push((21 + .75f, 5));
            Check(feet.Z > FlatDoor && OnFloor(1), $"in the next building the front door on the second floor is shut; she is at {feet}");
            Engine.TimeScale = 1;
#if DEBUG
            Check(DebugScenes.Find("compound") == "res://regions/compound.tscn", "the debug entry starts the compound region");
#endif
            GD.Print("Compound checks passed: entrance hall, neighbor's door, corridor railing, up the stair, top railings, down, under the stair and out; fences, gate, bus stop, every building's way to the gate, the next building's shut door.");
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
    private bool OnGround => Mathf.Abs(feet.Y + .2f) < .05f;

    // Walks the player to each point in turn with the movement keys, in straight lines, and lets go of them there. She gets
    // ten seconds and one more for every 2 m to each point.
    private async Task WalkTo(params (float X, float Z)[] points)
    {
        foreach (var point in points)
        {
            var target = new Vector3(point.X, 0, point.Z);
            var limit = 60 * (10 + Apart(feet, target) / 2);
            for (var frame = 0; Apart(feet, target) > .3f; frame++)
            {
                Check(frame < limit, $"the player must walk to {point}; she is stopped at {feet}");
                await Step(point);
            }
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
