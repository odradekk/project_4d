using Game.Debugging;
using Game.Flow;
using Godot;

namespace Game;

/// <summary>Plays the prologue from scene 1. Debug builds accept -- --start=&lt;id&gt; to launch a scene that DebugScenes lists, such as math-class.</summary>
public partial class Main : Node
{
    public override void _Ready()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--start=") && DebugScenes.Find(arg[8..]) is { } path)
            {
                AddChild(GD.Load<PackedScene>(path).Instantiate());
                return;
            }
        AddChild(new Prologue());
    }
}
