using FourD;
using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class FaceUvResource : Resource
{
    [Export] public Axis3 Axis { get; set; }
    [Export] public bool Positive { get; set; }
    [Export] public Vector2 Scale { get; set; } = Vector2.One;
    [Export] public Vector2 Offset { get; set; }
}
