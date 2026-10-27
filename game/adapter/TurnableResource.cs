using FourD;
using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class TurnableResource : Resource
{
    [Export] public bool Holdable { get; set; }
    [Export] public bool Packable { get; set; }
    [Export] public double HorizontalRadius { get; set; }
    [Export] public Axis3 LongAxis { get; set; }
    [Export] public Vector3 Hinge { get; set; }
    [Export] public double Reach { get; set; }
}
