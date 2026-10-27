using System;
using Godot;

namespace Game.Flow;

/// <summary>
/// The scene supplies the entry restoration action. Connect Restarting once to stop its
/// timelines and audio and clear HUD text before restoring the entry state.
/// </summary>
public partial class SceneCheckpoint : Node
{
    private Action? restore;
    [Signal] public delegate void RestartingEventHandler();
    public void Set(Action restoreEntry) => restore = restoreEntry ?? throw new ArgumentNullException(nameof(restoreEntry));
    public bool Restart()
    {
        if (restore is null) return false;
        EmitSignal(SignalName.Restarting);
        restore();
        return true;
    }
}
