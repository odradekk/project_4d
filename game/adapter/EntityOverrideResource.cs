using Godot;

namespace Game.Adapter;

/// <summary>
/// Where an operation starts an entity: the entity of the node at <see cref="Entity"/> (a path from the region), with this
/// position, and with the depth, rotation or device position that are given.
/// </summary>
[Tool, GlobalClass]
public partial class EntityOverrideResource : Resource
{
    [Export] public NodePath Entity { get; set; } = new();
    [Export] public Vector3 Position { get; set; }
    /// <summary>NaN keeps the depth of the entity as authored.</summary>
    [Export] public double W { get; set; } = double.NaN;
    /// <summary>Empty keeps the rotation of the entity as authored.</summary>
    [Export] public double[] Rotation4 { get; set; } = [];
    /// <summary>NaN keeps the position the device starts at.</summary>
    [Export] public double DeviceInitial { get; set; } = double.NaN;
}
