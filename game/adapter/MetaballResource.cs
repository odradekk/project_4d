using Godot;

namespace Game.Adapter;

/// <summary>One ball of a grazer's body, in the grazer's own coordinates.</summary>
[GlobalClass]
public partial class MetaballResource : Resource
{
    [Export] public string Id { get; set; } = "";

    /// <summary>X, Y, Z and W of the ball's center.</summary>
    [Export] public double[] Center { get; set; } = [];

    [Export] public double Radius { get; set; }
    [Export] public double Weight { get; set; }
}
