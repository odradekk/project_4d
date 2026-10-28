using Godot;

namespace Game.Adapter;

[GlobalClass]
public partial class MetaballShapeResource : Resource
{
    [Export] public Godot.Collections.Array<MetaballResource> Balls { get; set; } = [];
    [Export] public double Threshold { get; set; }
}
