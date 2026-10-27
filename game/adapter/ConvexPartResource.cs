using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class ConvexPartResource : Resource
{
    [Export] public string PartId { get; set; } = "";
    [Export] public Vector3[] Vertices { get; set; } = [];
    [Export] public Godot.Collections.Array<ConvexFaceResource> Faces { get; set; } = [];
    [Export] public Material? Material { get; set; }
}
