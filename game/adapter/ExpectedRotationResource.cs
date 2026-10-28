using FourD;
using Godot;

namespace Game.Adapter;

public enum ExpectedRotationKind { StandUp, LayDown, Flip }

/// <summary>A turn the actor makes of an entity. <c>Target</c> is the section to stand toward and is given for StandUp only.</summary>
[Tool, GlobalClass]
public partial class ExpectedRotationResource : ExpectedStepResource
{
    [Export] public ExpectedRotationKind Kind { get; set; }
    [Export] public string Actor { get; set; } = "";
    [Export] public string Entity { get; set; } = "";
    [Export] public string Target { get; set; } = "";
    [Export] public bool ShouldFail { get; set; }
    [Export] public FailureCode Failure { get; set; }
}
