using Godot;

namespace Game.Adapter;

/// <summary>
/// An ordinary object with a ring that rests between sections until the object is stood up. Its BoxPart3D
/// children give the body; W is the depth of the section the body lies in.
/// Decision note: 2026-10-26-region-scene-authoring
/// </summary>
[Tool, GlobalClass]
public partial class HiddenRingItem3D : Node3D
{
    [Export] public string EntityId { get; set; } = "";
    [Export] public string BuildingId { get; set; } = "";
    [Export] public double W { get; set; }
    [Export] public double[] Rotation4 { get; set; } = [];
    [Export] public TurnableResource? Interaction { get; set; }
    [Export] public RingResource? Ring { get; set; }
}
