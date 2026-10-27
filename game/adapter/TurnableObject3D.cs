using Godot;

namespace Game.Adapter;

/// <summary>
/// An object the player can pick up and turn. Its BoxPart3D children give the shape; Label3D children are
/// markings that follow the object while it lies flat.
/// Decision note: 2026-10-26-region-scene-authoring
/// </summary>
[Tool, GlobalClass]
public partial class TurnableObject3D : Node3D
{
    [Export] public string EntityId { get; set; } = "";
    [Export] public string BuildingId { get; set; } = "";
    [Export] public TurnableResource? Interaction { get; set; }
}
