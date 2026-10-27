using Godot;

namespace Game.Adapter;

/// <summary>
/// A seam between sections. Its origin is the point on the floor where the player crosses; the engine decides
/// whether a crossing is allowed, and the lens draws the seam over VisualHeight.
/// Decision note: 2026-10-26-region-scene-authoring
/// </summary>
[Tool, GlobalClass]
public partial class Seam3D : Node3D
{
    [Export] public string SeamId { get; set; } = "";
    [Export] public Vector3 HorizontalNormal { get; set; } = Vector3.Right;
    [Export] public string[] Sections { get; set; } = [];
    [Export] public bool Mirrored { get; set; }
    [Export] public bool Open { get; set; } = true;
    [Export] public bool Severed { get; set; }
    [Export] public double VisualHeight { get; set; } = 3;
}
