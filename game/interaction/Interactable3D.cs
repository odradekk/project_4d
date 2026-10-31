using Godot;

namespace Game.Interaction;

/// <summary>Attach a collision shape and connect Interacted in the scene script.</summary>
[GlobalClass]
public partial class Interactable3D : StaticBody3D
{
    [Signal] public delegate void InteractedEventHandler();
    [Export] public string Prompt { get; set; } = "交互";

    /// <summary>What Xiaoman says when E is pressed on it; empty for nothing.</summary>
    [Export] public string Line { get; set; } = "";

    public void Interact() => EmitSignal(SignalName.Interacted);
}
