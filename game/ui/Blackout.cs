using Godot;

namespace Game.UI;

/// <summary>
/// The screen cut to black over everything, with the title of the game on it once there is one.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class Blackout : CanvasLayer
{
    private const int TitleSize = 64;

    private readonly Label title = new() { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };

    public string TitleText => title.Text;
    public bool TitleVisible => title.Visible;

    public override void _Ready()
    {
        Layer = 100;
        var black = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(black);
        black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.VerticalAlignment = VerticalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", TitleSize);
        AddChild(title);
        title.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    public void ShowTitle(string text)
    {
        title.Text = text;
        title.Visible = true;
    }
}
