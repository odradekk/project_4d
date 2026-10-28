using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class ExpectedOperationResource : Resource
{
    [Export] public string Id { get; set; } = "";
    /// <summary>Where the entities start and what the actors carry; empty starts from the world as authored.</summary>
    [Export] public ExpectedSetupResource? Setup { get; set; }
    [Export] public Godot.Collections.Array<ExpectedStepResource> Steps { get; set; } = [];
}
