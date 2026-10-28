using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class ExpectedSetupResource : Resource
{
    [Export] public Godot.Collections.Array<EntityOverrideResource> EntityOverrides { get; set; } = [];
    [Export] public Godot.Collections.Array<CarrySetupResource> Carry { get; set; } = [];
}
