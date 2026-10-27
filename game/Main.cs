using Game.Debugging;
using Godot;

namespace Game;

/// <summary>Debug builds accept -- --start=systems-demo or --start=seam-demo to launch a registered scene.</summary>
public partial class Main : Node
{
    public override void _Ready()
    {
        var path = "res://regions/section_test.tscn";
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--start=")) path = DebugScenes.Find(arg[8..]) ?? path;
        AddChild(GD.Load<PackedScene>(path).Instantiate());
    }
}
