using Godot;

namespace Game.Adapter;

/// <summary>
/// A step of an expected operation: an ExpectedRotationResource, an ExpectedDeviceResource or an ExpectedCrossingResource.
/// It is not abstract. With this class abstract, street_checks logged "Handle is not initialized" (17 to 27 ERROR lines a
/// run) in 4 of 5 runs of the room branch on one machine, and in none of 5 runs with it plain; on main with only the reader and
/// the rotation and device steps it logged nothing in 9 runs. Godot's issue 112067 describes the same message as intermittent,
/// with the weak handle of a cached native resource already freed. That the abstract class has a part in it is not established.
/// </summary>
[Tool, GlobalClass]
public partial class ExpectedStepResource : Resource
{
}
