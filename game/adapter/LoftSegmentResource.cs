using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class LoftSegmentResource : Resource
{
    [Export] public Godot.Collections.Array<LoftKeyResource> Keys { get; set; } = [];
}
