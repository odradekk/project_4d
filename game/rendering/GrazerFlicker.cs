using System;
using Game.Adapter;
using Game.Audio;
using Godot;

namespace Game.Rendering;

/// <summary>
/// The picture flickers dark now and then when the grazer is near, as a second hint beside the music: as often as the
/// music's rhythm layer is in, at most a few times a second, and never to black.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class GrazerFlicker : CanvasLayer
{
    // Flickers a second when the rhythm is full, how long one lasts in seconds, and how dark the weakest and the strongest are.
    private const double Rate = 3;
    private const double Length = .08;
    private const double Weakest = .25;
    private const double Strongest = .6;

    private readonly ColorRect veil = new() { Name = "Veil", MouseFilter = Control.MouseFilterEnum.Ignore };
    private Region3D? region;
    private string grazer = "";
    private double remaining;

    internal RandomNumberGenerator Random { get; } = new();

    /// <summary>How dark the picture is made now, from 0 for not at all to below 1.</summary>
    public double Darkness { get; private set; }

    public override void _Ready()
    {
        Layer = 2;
        veil.Color = Colors.Transparent;
        veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(veil);
    }

    public void Configure(Region3D region, string grazer)
    {
        this.region = region;
        this.grazer = grazer;
    }

    public override void _Process(double delta) => Advance(delta);

    public void Advance(double delta)
    {
        remaining -= delta;
        if (remaining > 0) return;
        Darkness = 0;
        var rhythm = GrazerLayers.At(region!.Grazers!.Distance(grazer)).Rhythm;
        if (Random.Randf() < rhythm * Rate * delta)
        {
            Darkness = Random.RandfRange((float)Weakest, (float)Strongest);
            remaining = Length;
        }
        veil.Color = new(0, 0, 0, (float)Darkness);
    }
}
