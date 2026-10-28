using FourD;
using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class CarrySetupResource : Resource
{
    [Export] public string Actor { get; set; } = "";
    [Export] public string Item { get; set; } = "";
    [Export] public CarryLocation Location { get; set; }
}
