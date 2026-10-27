using Godot;

namespace Game.Adapter;

/// <summary>
/// A grazer as authored: where its body starts, and its body as balls. The body origin is the node's X, Y and Z and
/// <see cref="W"/>, and its turn is <see cref="Rotation4"/>; the node's own rotation is not read. A grazer moves by
/// script, so the node only gives its first frame.
/// Decision note: 2026-10-26-region-scene-authoring
/// </summary>
[Tool, GlobalClass]
public partial class Grazer3D : Node3D
{
    [Export] public string EntityId { get; set; } = "";
    [Export] public double W { get; set; }

    /// <summary>The 16 numbers of the body's four-dimensional rotation, a column of X, Y, Z and W at a time, or none for no rotation.</summary>
    [Export] public double[] Rotation4 { get; set; } = [];

    [Export] public MetaballShapeResource? Shape { get; set; }

    /// <summary>The body with its mouth wide open: the same balls as <see cref="Shape"/>, in the same order, moved and resized. None when the mouth does not open.</summary>
    [Export] public MetaballShapeResource? OpenShape { get; set; }
}
