using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class BoxPart3D : MeshInstance3D
{
    [Export] public string PartId { get; set; } = "";
    [Export] public Godot.Collections.Array<FaceUvResource> FaceUvs { get; set; } = [];
}
