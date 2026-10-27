using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class LoftKeyResource : Resource
{
    [Export] public double W { get; set; }
    [Export] public Godot.Collections.Array<ConvexPartResource> Parts { get; set; } = [];
}
