using FourD;
using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class DeviceRotationResource : DeviceMotionResource
{
    [Export] public RotationPlane Plane { get; set; }
    [Export] public double[] Pivot { get; set; } = [];
}
