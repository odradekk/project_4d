using Godot;

namespace Game.Adapter;

/// <summary>
/// One way to solve a puzzle of the region, which the engine plays against the loaded world and reports if it ends otherwise
/// than the steps say. Place these under the region's ExpectedPuzzleOperations.
/// Decision note: 2026-10-26-region-scene-authoring
/// </summary>
[Tool, GlobalClass]
public partial class ExpectedOperationNode : Node
{
    [Export] public ExpectedOperationResource? Operation { get; set; }
}
