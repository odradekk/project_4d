using System.Collections.Generic;
using System.Linq;
using FourD;
using Godot;

namespace Game.Adapter;

// The expected operations of a region.
// Decision note: 2026-10-26-region-scene-authoring
internal sealed partial class SceneReader
{
    private IReadOnlyList<ExpectedOperation> ReadOperations(IEnumerable<Node> nodes, IReadOnlyList<EntityDefinition> entities)
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
            for (var index = 0; index < operation.Steps.Count; index++)
            {
                var step = operation.Steps[index];
                if (step is ExpectedRotationResource { Kind: not ExpectedRotationKind.StandUp, Target: not "" })
                    Error(node, $"Operation.Steps[{index}]", "Only a StandUp has a Target.");
                else if (ReadStep(step) is { } read) steps.Add(read);
                else Error(node, $"Operation.Steps[{index}]", "A step is missing or of an unknown kind.");
            }
            operations.Add(new(new(operation.Id), At(node, "Operation"), ReadSetup(operation.Setup, node, entities), steps));
        }
        return operations;
    }

    private static ExpectedStep? ReadStep(ExpectedStepResource? step) => step switch
    {
        ExpectedRotationResource turn => new ExpectedRotation(Request(turn), turn.ShouldFail ? turn.Failure : null),
        ExpectedDeviceResource pull => new ExpectedDevice(new(pull.Device), pull.Target, pull.ExpectedPosition, pull.ShouldFail ? pull.Failure : null),
        ExpectedCrossingResource cross => new ExpectedCrossing(new(new(cross.Actor), new(cross.Seam), new(cross.Target), cross.HasPrism,
            cross.FreeBackpackSlots), cross.ShouldFail ? cross.Failure : null),
        _ => null
    };

    private static RotationRequest Request(ExpectedRotationResource turn) => turn.Kind switch
    {
        ExpectedRotationKind.StandUp => new StandUp(new(turn.Actor), new(turn.Entity), new(turn.Target)),
        ExpectedRotationKind.LayDown => new LayDown(new(turn.Actor), new(turn.Entity)),
        _ => new Flip(new(turn.Actor), new(turn.Entity))
    };

    // Where the operation starts: the entities put in their places, and what each actor carries.
    private ExpectedSetup ReadSetup(ExpectedSetupResource? setup, Node owner, IReadOnlyList<EntityDefinition> entities)
    {
        if (setup is null) return new([], [], [], []);
        var overrides = NonNull(setup.EntityOverrides, owner, "Operation").Select(entity => Overridden(entity, owner, entities)).OfType<EntityDefinition>().ToArray();
        var carry = NonNull(setup.Carry, owner, "Operation").Select(item => new CarrySetup(new(item.Actor), new(item.Item), item.Location)).ToArray();
        return new(overrides, [], [], carry);
    }

    // The definition of the entity of an authored node, started where the override puts it.
    private EntityDefinition? Overridden(EntityOverrideResource entity, Node owner, IReadOnlyList<EntityDefinition> entities)
    {
        var id = region.GetNodeOrNull(entity.Entity) switch
        {
            TurnableObject3D turnable => turnable.EntityId,
            SphereItem3D ball => ball.EntityId,
            HiddenRingItem3D nut => nut.EntityId,
            LoftDevice3D loft => loft.EntityId,
            CreatureSpawn3D spawn => spawn.EntityId,
            _ => null
        };
        if (id is null || entities.FirstOrDefault(definition => definition.Id.Value == id) is not { } definition)
        {
            Error(owner, "Operation", $"The override names no entity that can be moved: {entity.Entity}.");
            return null;
        }
        var pose = new Pose4(new(Typed(entity.Position.X), Typed(entity.Position.Y), Typed(entity.Position.Z),
            double.IsNaN(entity.W) ? definition.Pose.Position.W : entity.W),
            entity.Rotation4.Length == 0 ? definition.Pose.Rotation : Matrix(entity.Rotation4, owner, "Operation"));
        if (definition is DeviceDefinition device && !double.IsNaN(entity.DeviceInitial))
            return device with
            {
                Pose = pose,
                Motion = device.Motion switch
                {
                    DeviceRotation rotation => rotation with { Initial = entity.DeviceInitial },
                    DeviceTranslation slide => slide with { Initial = entity.DeviceInitial },
                    var other => other
                }
            };
        return definition with { Pose = pose };
    }
}
