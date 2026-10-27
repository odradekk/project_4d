using Godot;

namespace Game.Rendering;

/// <summary>
/// Reverses the 3D view left to right while the player is a mirror image. It sits just above the 3D view and below
/// everything else, so the lens turns itself over and the subtitles, hints and notebook stay as they are.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class MirrorView : CanvasLayer
{
    private readonly ColorRect screen = new() { Name = "Screen", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };

    public bool Flipped
    {
        get => screen.Visible;
        set => screen.Visible = value;
    }

    public override void _Ready()
    {
        Layer = -1;
        AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        screen.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://rendering/mirror.gdshader") };
    }
}
