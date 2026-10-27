using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class IntervalResource : Resource
{
    [Export] public double Min { get; set; }
    [Export] public double Max { get; set; }
}
