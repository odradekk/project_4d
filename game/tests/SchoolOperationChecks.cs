using System.Linq;
using FourD;
using Game.Adapter;
using Game.Flow;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The expected operations written in the school region: the engine plays them when the region loads, and reports a step that
/// ends otherwise, a target on a step that takes none, and a step that is not there.
/// </summary>
internal static class SchoolOperationChecks
{
    /// <summary>How far the first window goes before the plate of the pencil stops it.</summary>
    internal const double StopsAt = .214;

    // The engine plays the solutions written in the school region when it loads, and reports a step that ends otherwise.
    internal static void Run(Node host)
    {
        var region = GD.Load<PackedScene>(SchoolRoom.RegionPath).Instantiate<Region3D>();
        host.AddChild(region);
        var operation = region.GetNode<ExpectedOperationNode>("ExpectedPuzzleOperations/WindowStuck").Operation!;
        var stop = (ExpectedDeviceResource)operation.Steps[1];
        var lay = operation.Steps[2];
        try
        {
            Check(region.Loaded && new SceneReader(region).Load() is LoadResult.Loaded, $"the school region must load: {region.Diagnostics}");
            stop.ExpectedPosition = .3;
            var wrong = new SceneReader(region).Load() as LoadResult.Invalid;
            stop.ExpectedPosition = StopsAt;
            Check(wrong is not null && wrong.Errors.Any(error => error.Code == LoadErrorCode.ExpectedOperationFailed && error.StepIndex == 1
                    && error.Source == new Source("ExpectedPuzzleOperations/WindowStuck", "Operation.Steps[1]")),
                "a window that stops somewhere else than the operation says is reported at its step");

            // Without laying the pencil down, the last push does not shut the window.
            operation.Steps.RemoveAt(2);
            var blocked = new SceneReader(region).Load() as LoadResult.Invalid;
            operation.Steps.Insert(2, lay);
            Check(blocked is not null && blocked.Errors.Any(error => error.Code == LoadErrorCode.ExpectedOperationFailed && error.StepIndex == 2),
                "a window pushed while the pencil still stands does not shut, and the operation says so");

            // A target on a step that takes none, and a step that is not there, are mistakes of the author, reported at the step.
            var source = new Source("ExpectedPuzzleOperations/WindowStuck", "Operation.Steps[2]");
            ((ExpectedRotationResource)lay).Target = "Ⅱ";
            var withTarget = new SceneReader(region).Load() as LoadResult.Invalid;
            ((ExpectedRotationResource)lay).Target = "";
            Check(withTarget is not null && withTarget.Errors.Any(error => error.Source == source), "a target on a step that lays a pencil down is reported at the step");
            operation.Steps.Insert(2, null!);
            var withGap = new SceneReader(region).Load() as LoadResult.Invalid;
            operation.Steps.RemoveAt(2);
            Check(withGap is not null && withGap.Errors.Any(error => error.Source == source), "a step that is missing is reported at its place");
            Check(new SceneReader(region).Load() is LoadResult.Loaded, "the operations are as they were");
        }
        finally
        {
            stop.ExpectedPosition = StopsAt;
            if (operation.Steps.Count == 3) operation.Steps.Insert(2, lay);
            host.RemoveChild(region);
            region.Free();
        }
    }
}
