using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class RingResource : Resource
{
    [Export] public string PartId { get; set; } = "";
    [Export] public double[] Center { get; set; } = [];
    [Export] public double[] Rotation4 { get; set; } = [];
    [Export] public double MajorRadius { get; set; }
    [Export] public double TubeRadius { get; set; }
    [Export] public Material? Material { get; set; }
}
