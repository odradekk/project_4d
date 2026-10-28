using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

// The expected operations of a region.
// Decision note: 2026-10-26-region-scene-authoring
internal sealed partial class SceneReader
{
    private IReadOnlyList<ExpectedOperation> ReadOperations(IEnumerable<Node> nodes)
    {
        var operations = new List<ExpectedOperation>();
        foreach (var node in nodes.OfType<ExpectedOperationNode>())
        {
            if (node.Operation is not { } operation)
            {
                Error(node, "Operation", "Assign an operation resource.");
                continue;
            }
            var steps = new List<ExpectedStep>();
            foreach (var step in operation.Steps)
            {
                if (ReadStep(step) is { } read) steps.Add(read);
                else Error(node, "Operation", "A step is missing or of an unknown kind.");
            }
            operations.Add(new(new(operation.Id), At(node, "Operation"), new([], [], [], []), steps));
        }
        return operations;
    }

    private static ExpectedStep? ReadStep(ExpectedStepResource? step) => step switch
    {
        ExpectedRotationResource turn => new ExpectedRotation(Request(turn), turn.ShouldFail ? turn.Failure : null),
        ExpectedDeviceResource pull => new ExpectedDevice(new(pull.Device), pull.Target, pull.ExpectedPosition, pull.ShouldFail ? pull.Failure : null),
        _ => null
    };

    private static RotationRequest Request(ExpectedRotationResource turn) => turn.Kind switch
    {
        ExpectedRotationKind.StandUp => new StandUp(new(turn.Actor), new(turn.Entity), new(turn.Target)),
        ExpectedRotationKind.LayDown => new LayDown(new(turn.Actor), new(turn.Entity)),
        _ => new Flip(new(turn.Actor), new(turn.Entity))
    };
}
