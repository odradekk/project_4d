using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The room of scene 8 as a region: it loads, the solutions written into it are played by the engine and a wrong one
/// stops the load, the two seams are where the cat's leap and the clearance need them, and the first step's flip is cut by the
/// guard and not by the wall behind it. Expected values are worked out by hand in docs/frame/home-region.md.
/// </summary>
public partial class RoomChecks : Node
{
    private const string Room = "res://regions/room.tscn";

    public override async void _Ready()
    {
        try
        {
            await CheckLoad();
            CheckWrongSolutions();
            await CheckFlipPlane();
            GD.Print("Room checks passed: load, solutions played by the engine, wrong solutions refused, the flip plane.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task CheckLoad()
    {
        var play = new TurningFixture(this);
        await play.Load(scene: Room);
        var names = play.Region.GetNode("ExpectedPuzzleOperations/Operations").GetChildren().OfType<ExpectedOperationNode>()
            .Select(node => node.Operation!.Id).Order().ToArray();
        string[] expected = ["cabinet-open", "nut-at-wall", "nut-stand", "seam-cross", "seam-cross-wand", "wand-flip", "wand-stand"];
        Check(names.SequenceEqual(expected), $"the room must hold the solutions of every step: {string.Join(", ", names)}");
        // From page I the lens shows the seam to page II and nothing of the mirror seam between II and III.
        Check(play.Region.HasNode("Generated/Ⅱ/Overlay/Seams/s1") && !play.Region.HasNode("Generated/Ⅱ/Overlay/Seams/s2")
            && !play.Region.HasNode("Generated/Ⅲ/Overlay/Seams/s2"), "page I sees the seam s1 and not the hidden seam s2");
        await play.Unload();
    }

    // A solution that is wrong makes the load fail at its step, so the engine does play them.
    private static void CheckWrongSolutions()
    {
        var authored = Instance();
        Check(new SceneReader(authored).Load() is LoadResult.Loaded, "the room as authored must be valid");
        authored.Free();
        // The nut against the wall is refused; written as a success, the load must fail at step 0 of that operation.
        var diagnostics = Fails("nut-at-wall", step => step.ShouldFail = false);
        Check(diagnostics.Contains("ExpectedOperationFailed nut-at-wall[0]") && diagnostics.Contains("RotationBlocked"),
            $"a refusal written as a success must be reported by the operation: {diagnostics}");
        // The nut stood up in the gap between desk and wardrobe works; written as a refusal, the load fails too.
        diagnostics = Fails("nut-stand", step => { step.ShouldFail = true; step.Failure = FailureCode.RotationBlocked; });
        Check(diagnostics.Contains("ExpectedOperationFailed nut-stand[0]"), $"a success written as a refusal must be reported: {diagnostics}");
    }

    private static Region3D Instance() => GD.Load<PackedScene>(Room).Instantiate<Region3D>();

    // Reads the room with the first step of an operation changed, and returns why it is not valid.
    private static string Fails(string operation, Action<ExpectedRotationResource> change)
    {
        var region = Instance();
        var node = region.GetNode("ExpectedPuzzleOperations/Operations").GetChildren().OfType<ExpectedOperationNode>().Single(n => n.Operation!.Id == operation);
        var old = (ExpectedRotationResource)node.Operation!.Steps[0];
        var step = new ExpectedRotationResource { Kind = old.Kind, Actor = old.Actor, Entity = old.Entity, Target = old.Target, ShouldFail = old.ShouldFail, Failure = old.Failure };
        change(step);
        node.Operation = new ExpectedOperationResource { Id = operation, Setup = node.Operation.Setup, Steps = [step] };
        var errors = (new SceneReader(region).Load() as LoadResult.Invalid)?.Errors ?? [];
        region.Free();
        Check(errors.Count > 0, $"the room must not be valid with a wrong solution of {operation}");
        return string.Join('\n', errors.Select(e => $"{e.Code} {e.Operation?.Value}[{e.StepIndex}] {e.Detail}"));
    }

    // Flipping the wand over the guard folds about the face of the heater under the sill, in front of the guard rods, wherever
    // along the way she aims from; the rods are in page I alone, so the wand does not meet them on its swing.
    private async Task CheckFlipPlane()
    {
        var play = new TurningFixture(this);
        await play.Load(scene: Room);
        var actions = play.Actions;
        play.Player.Position = new(5.25f, .02f, 3.3f);
        play.Player.Velocity = Vector3.Zero;
        play.LookAt(new(5.25f, .02f, 3.3f), new(5.25f, 1.09f, 2.63f));
        await play.Frames(3);
        Check(play.State("wand").Handedness == Handedness.Normal, "the wand starts as it was made");
        actions.Aim("wand");
        Check(actions.Flip() is null, "the flip over the guard must be accepted");
        await play.Frames(TurningFixture.TurnFrames + 30);
        var wand = play.State("wand");
        Check(wand.Handedness == Handedness.Mirrored && wand.Pose.Position.Z > 2.76, $"the wand must come over the guard to her side: {wand.Pose.Position}");
        await play.Unload();
    }
}
