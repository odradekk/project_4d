using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class CreatureSpawn3D : Node3D
{
    [Export] public string EntityId { get; set; } = "";
    [Export] public PackedScene? CharacterScene { get; set; }
}
