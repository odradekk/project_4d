using Godot;

namespace Game.Adapter;

/// <summary>
/// A device whose shape is lofted between key shapes at different depths and that turns or slides along one
/// track. See docs/frame/device-authoring.md for how to build one.
/// Decision note: 2026-10-26-device-translation
/// </summary>
[Tool, GlobalClass]
public partial class LoftDevice3D : Node3D
{
    [Export] public string EntityId { get; set; } = "";
    [Export] public double W { get; set; }
    [Export] public double[] Rotation4 { get; set; } = [];
    [Export] public Godot.Collections.Array<LoftSegmentResource> Segments { get; set; } = [];
    [Export] public DeviceMotionResource? Motion { get; set; }
}
