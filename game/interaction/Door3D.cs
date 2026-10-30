using Godot;

namespace Game.Interaction;

/// <summary>
/// A door that E opens and shuts. Its leaf, the <see cref="Interactable3D"/> child named Leaf, turns about this node's
/// origin, the hinge. Shut or ajar, the leaf stops the player, and ajar it leaves her room to pass beside it; open, it
/// lets her through. E on an open or ajar door shuts it. Like the school room's door, it is a plain body that the engine
/// does not know of, so seams, the prism and the cat cannot see it (docs/frame/compound-region.md).
/// </summary>
[GlobalClass]
public partial class Door3D : Node3D
{
    public enum DoorState { Shut, Open, Ajar }

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
            leaf.RotationDegrees = new(0, value switch { DoorState.Open => Swing, DoorState.Ajar => AjarSwing, _ => 0 }, 0);
            leaf.CollisionLayer = value == DoorState.Open ? PhysicsLayers.Pickups : PhysicsLayers.World | PhysicsLayers.Pickups;
            leaf.Prompt = value == DoorState.Shut ? "开门" : "关门";
        }
    }

    /// <summary>The degrees the leaf turns through about y from shut to open; the sign picks the side it opens to.</summary>
    [Export] public float Swing { get; set; } = 90;

    /// <summary>The degrees, with the sign of <see cref="Swing"/>, from shut to ajar.</summary>
    [Export] public float AjarSwing { get; set; } = 45;

    public override void _Ready()
    {
        leaf = GetNode<Interactable3D>("Leaf");
        leaf.Interacted += () => State = State == DoorState.Shut ? DoorState.Open : DoorState.Shut;
        State = state;
    }
}
