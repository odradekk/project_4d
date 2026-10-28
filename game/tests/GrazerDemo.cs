using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Audio;
using Game.Creatures;
using Game.Rendering;
using Godot;

namespace Game.Tests;

/// <summary>
/// The grazer debug scene: a grazer comes up from the deep sections of the grazer test region, opens its mouth at the
/// door, bites it and a seam, then turns toward the player. Its music and flicker follow its distance. Launch it with
/// <c>-- --run-through</c> to have it end by itself after the timeline.
/// </summary>
public partial class GrazerDemo : Node
{
    internal const string Scene = "res://regions/grazer_test.tscn";
    internal const string Grazer = "grazer";

    /// <summary>When the grazer bites, in seconds from the start.</summary>
    internal const double BiteTime = 29;

    private Region3D region = null!;
    private GrazerMusic music = null!;
    private Label label = null!;
    private double elapsed;

    /// <summary>
    /// W falls from 11 to 0.1 in four steps at about a third of a metre a second, the mouth opens for the bite at the
    /// door, and the body turns half way round and walks on toward the player at z = 3.
    /// </summary>
    internal static GrazerTimeline Timeline() => new(
        [
            Key(0, -2.2, 11, 0, 0, 0),
            Key(16, -2.2, 3.6, 0, .3, 0),
            Key(22, -2.2, 1.4, 0, .3, 0),
            Key(26, -2.2, .1, 0, 0, 0),
            Key(BiteTime, -2.2, .1, 0, 0, 1),
            Key(32, -2.2, .1, Math.PI, 0, 1),
            Key(38, .5, .1, Math.PI, 0, .3)
        ],
        [new GrazerBite(BiteTime, [new EntityId("Door")], [new SeamId("needle")])]);

    private static GrazerKey Key(double time, double z, double w, double yaw, double tilt, double mouth) =>
        new(time, new(0, 1.4, z, w), [new(RotationPlane.XZ, yaw), new(RotationPlane.XW, tilt)], mouth);

    public override async void _Ready()
    {
        region = GD.Load<PackedScene>(Scene).Instantiate<Region3D>();
        region.Name = "Region";
        AddChild(region);
        music = new GrazerMusic { Name = "Music" };
        AddChild(music);
        music.Configure(region, Grazer);
        var flicker = new GrazerFlicker { Name = "Flicker" };
        AddChild(flicker);
        flicker.Configure(region, Grazer);
        var layer = new CanvasLayer { Layer = 3 };
        AddChild(layer);
        label = new Label { Position = new Vector2(20, 200) };
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        layer.AddChild(label);
        region.Grazers!.Play(Grazer, Timeline());
        if (OS.GetCmdlineUserArgs().Contains("--run-through")) await RunThrough();
    }

    // For an unattended run: lets the timeline play out, then takes the music away and waits for the audio thread to let go of
    // its sounds before the run ends, which keeps a headless run free of errors about sounds still in use.
    private async Task RunThrough()
    {
        while (region.Grazers!.IsPlaying(Grazer)) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SetProcess(false);
        music.Free();
        await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
        GetTree().Quit();
    }

    public override void _Process(double delta)
    {
        elapsed += delta;
        var levels = music.Levels;
        label.Text = $"{elapsed:F0} s  距离 {region.Grazers!.Distance(Grazer):F1} m\n" +
            $"低音 {levels.Bass:F2}  节奏 {levels.Rhythm:F2}  压低 {levels.Duck:F2}";
    }
}
