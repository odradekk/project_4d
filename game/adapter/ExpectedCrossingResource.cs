using FourD;
using Godot;

namespace Game.Adapter;

/// <summary>An actor crosses a seam to a section, with the prism in hand or not and the given room left in the backpack.</summary>
[Tool, GlobalClass]
public partial class ExpectedCrossingResource : ExpectedStepResource
{
    [Export] public string Actor { get; set; } = "";
    [Export] public string Seam { get; set; } = "";
    [Export] public string Target { get; set; } = "";
    [Export] public bool HasPrism { get; set; }
    [Export] public int FreeBackpackSlots { get; set; }
    [Export] public bool ShouldFail { get; set; }
    [Export] public FailureCode Failure { get; set; }
}
