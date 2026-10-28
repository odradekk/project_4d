using FourD;
using Godot;

namespace Game.Adapter;

/// <summary>
/// A pull on a device toward <c>Target</c>. The step is met when the device ends at <c>ExpectedPosition</c>, which is short of
/// the target when something stops it.
/// </summary>
[Tool, GlobalClass]
public partial class ExpectedDeviceResource : ExpectedStepResource
{
    [Export] public string Device { get; set; } = "";
    [Export] public double Target { get; set; }
    [Export] public double ExpectedPosition { get; set; }
    [Export] public bool ShouldFail { get; set; }
    [Export] public FailureCode Failure { get; set; }
}
