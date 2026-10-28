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
/// stops the load, the hidden seam is not seen from section I, the wand is flipped over the guard from in front of it, and
/// a wand in the hand cannot be walked out past the ends of the guard. Positions are given in docs/frame/home-region.md.
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
            await CheckGuardKeepsTheWand();
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
        // From section I the lens shows the seam to section II and nothing of the mirror seam between II and III.
        Check(play.Region.HasNode("Generated/Ⅱ/Overlay/Seams/s1") && !play.Region.HasNode("Generated/Ⅱ/Overlay/Seams/s2")
            && !play.Region.HasNode("Generated/Ⅲ/Overlay/Seams/s2"), "section I sees the seam s1 and not the hidden seam s2");
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
        // Only a stand-up has a target; given to a flip, it would hide a mistake.
        diagnostics = Fails("wand-flip", step => step.Target = "Ⅱ");
        Check(diagnostics.Contains("Only a stand-up has a target"), $"a target on a flip must be refused: {diagnostics}");
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

    // The engine folds a flip about the nearest face that a line from her feet to the wand runs into. Straight in front of the
    // guard that is the front of the heater cover under the sill, since the rods and the sill are above the line, and the wand
    // comes over to her side. From far to the west or east the line meets an end of the cover first and the fold goes sideways.
    // The rods are in section I alone, so the wand does not meet them on its swing through W.
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

    // Aimed at through the rods, the wand can be taken in the hand, but it cannot be walked out along the sill past either end of
    // the guard: the way to her side of the guard is the flip.
    private async Task CheckGuardKeepsTheWand()
    {
        var play = new TurningFixture(this);
        foreach (var (side, along) in new[] { ("west", -1f), ("east", 1f) })
        {
            await play.Load(scene: Room);
            // The stool would stop her before the east end.
            var stool = play.State("stool").Pose;
            Check(play.Region.World!.PlaceEntity(new("stool"), stool with { Position = new(3.5, stool.Position.Y, 5.2, 0) }) is UpdateResult.Updated, "the stool is put out of the way");
            play.LookAt(new(5.44f, .02f, 3.3f), new(5.44f, 1.09f, 2.63f));
            await play.Frames(3);
            Check(play.Actions.Aimed == "wand" && play.Actions.Use() is null && play.Actions.Held == "wand", $"she takes the wand through the rods; the crosshair is on {play.Actions.Aimed}");
            for (var i = 0; i < 6; i++)
            {
                play.Player.Position += new Vector3(.2f * along, 0, 0);
                await play.Frames(3);
            }
            await play.Frames(10);
            var wand = play.State("wand");
            Check(wand.Pose.Position.Z < 2.745 && wand.Pose.Position.X is > 4.95 and < 6.25 && wand.Handedness == Handedness.Normal,
                $"walking 1.2 m {side} must not take the wand out past the guard: {wand.Pose.Position}");
        }
        await play.Unload();
    }
}
