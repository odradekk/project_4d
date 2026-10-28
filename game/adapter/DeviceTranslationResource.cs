using FourD;
using Godot;

namespace Game.Adapter;

[Tool, GlobalClass]
public partial class DeviceTranslationResource : DeviceMotionResource
{
    [Export] public Axis3 Axis { get; set; }
}
