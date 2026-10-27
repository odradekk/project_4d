using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class Section3D : Node3D
{
    [Export] public string SectionId { get; set; } = "";
    [Export] public double W { get; set; }
}
