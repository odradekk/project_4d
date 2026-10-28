using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class ExpectedOperationResource : Resource
{
    [Export] public string Id { get; set; } = "";
    [Export] public Godot.Collections.Array<ExpectedStepResource> Steps { get; set; } = [];
}
