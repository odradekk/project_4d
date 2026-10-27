using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class Building3D : Node3D
{
    [Export] public string BuildingId { get; set; } = "";
    [Export] public double WMin { get; set; }
    [Export] public double WMax { get; set; }
}
