using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class ConvexFaceResource : Resource
{
    [Export] public int[] Vertices { get; set; } = [];
    [Export] public FaceUvResource? Uv { get; set; }
}
