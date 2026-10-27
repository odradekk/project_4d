using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class FixedObject3D : Node3D
{
    [Export] public string EntityId { get; set; } = "";
}
