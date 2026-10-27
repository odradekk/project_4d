using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class CreatureSpawn3D : Node3D
{
    [Export] public string EntityId { get; set; } = "";
    [Export] public double PassageRadius { get; set; } = 0.2;
    [Export] public double PassageHeight { get; set; } = 1.4;
    [Export] public bool CanUseSeamsWithoutPrism { get; set; }
    [Export] public PackedScene? CharacterScene { get; set; }

    /// <summary>The name shown with the character's lines, in place of the one its scene has; empty keeps that one.</summary>
    [Export] public string DisplayName { get; set; } = "";
}
