using Godot;

namespace Game.Adapter;

/// <summary>
/// A four-dimensional ball. Its origin is the ball's center, at depth W; it has no mesh, and the engine cuts it
/// by each section.
/// Decision note: 2026-10-26-region-scene-authoring
/// </summary>
[Tool, GlobalClass]
public partial class SphereItem3D : Node3D
{
    [Export] public string EntityId { get; set; } = "";
    [Export] public string BuildingId { get; set; } = "";
    [Export] public string PartId { get; set; } = "";
    [Export] public double W { get; set; }
    [Export] public double[] Rotation4 { get; set; } = [];
    [Export] public double Radius { get; set; }
    [Export] public TurnableResource? Interaction { get; set; }
    [Export] public Material? Material { get; set; }
}
