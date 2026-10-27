using Godot;

namespace Game.Adapter;

/// <summary>How a device moves: a DeviceRotationResource or a DeviceTranslationResource, within Min to Max from Initial.</summary>
[Tool, GlobalClass]
public abstract partial class DeviceMotionResource : Resource
{
    [Export] public double Min { get; set; }
    [Export] public double Max { get; set; }
    [Export] public double Initial { get; set; }
}
