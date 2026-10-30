using Godot;

namespace Game.Interaction;

/// <summary>
/// A door that E opens and shuts. Its leaf, the <see cref="Interactable3D"/> child named Leaf, turns about this node's
/// origin, the hinge. Shut, the leaf stops the player; open or ajar, it lets her through, and E on it shuts it. Like the
/// stair slopes, a door is a plain body that the engine does not know of.
/// </summary>
[GlobalClass]
public partial class Door3D : Node3D
{
    public enum DoorState { Shut, Open, Ajar }

    private const uint Solid = 1;
    private DoorState state;
    private Interactable3D? leaf;

    /// <summary>Where the leaf stands; set in the scene, it is the door's state at the start.</summary>
    [Export]
    public DoorState State
    {
        get => state;
        set
        {
            state = value;
            if (leaf is null) return;
            leaf.RotationDegrees = new(0, value switch { DoorState.Open => Swing, DoorState.Ajar => Swing / 2, _ => 0 }, 0);
            leaf.CollisionLayer = value == DoorState.Shut ? Solid | PhysicsLayers.Pickups : PhysicsLayers.Pickups;
            leaf.Prompt = value == DoorState.Shut ? "开门" : "关门";
        }
    }

    /// <summary>The degrees the leaf turns through about y from shut to open; the sign picks the side it opens to.</summary>
    [Export] public float Swing { get; set; } = 90;

    public override void _Ready()
    {
        leaf = GetNode<Interactable3D>("Leaf");
        leaf.Interacted += () => State = State == DoorState.Shut ? DoorState.Open : DoorState.Shut;
        State = state;
    }
}
