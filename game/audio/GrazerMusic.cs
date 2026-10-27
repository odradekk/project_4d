using System;
using Game.Adapter;
using Godot;

namespace Game.Audio;

/// <summary>
/// The grazer's music, in three layers that follow its distance (see <see cref="GrazerLayers"/>): a low bass from far off,
/// then a pulse that comes faster with scraping mixed in, and then the other ambient sounds pushed down. The sounds are
/// placeholders: a looped drone (grazer_bass.wav), the thud and the scratch.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class GrazerMusic : Node
{
    // Loudness of each layer at full, in dB, and how far the other ambient sounds are pushed down at full duck.
    private const float BassDb = -12;
    private const float PulseDb = -9;
    private const float ScrapeDb = -10;
    private const float DuckDb = 24;
    // Seconds between pulses when the rhythm has just begun, and when it is full.
    private const double SlowBeat = 1.6;
    private const double FastBeat = .5;

    private Region3D? region;
    private string grazer = "";
    private AudioStreamPlayer bass = null!;
    private AudioStreamPlayer pulse = null!;
    private AudioStreamPlayer scrape = null!;
    private int environment;
    private float environmentDb;
    private double untilBeat;

    public GrazerLevels Levels { get; private set; }

    /// <summary>How many times the pulse has beaten.</summary>
    public int Beats { get; private set; }

    public override void _Ready()
    {
        bass = Player("Bass", "grazer_bass");
        pulse = Player("Pulse", "thud");
        scrape = Player("Scrape", "scratch");
    }

    /// <summary>Follows the distance to a grazer of a loaded region. Add this node to the scene first.</summary>
    public void Configure(Region3D region, string grazer)
    {
        if (!IsInsideTree()) throw new InvalidOperationException("Add the music to the scene before configuring it.");
        this.region = region;
        this.grazer = grazer;
        SectionSound.EnsureEnvironmentBus();
        environment = AudioServer.GetBusIndex(SectionSound.EnvironmentBus);
        environmentDb = AudioServer.GetBusVolumeDb(environment);
    }

    public override void _Process(double delta) => Advance(delta);

    /// <summary>Sets the layers for the distance now and beats the pulse as often as <paramref name="delta"/> seconds hold.</summary>
    public void Advance(double delta)
    {
        Levels = GrazerLayers.At(region!.Grazers!.Distance(grazer));
        if (Levels.Bass == 0) bass.Stop();
        else
        {
            bass.VolumeDb = BassDb + (float)Mathf.LinearToDb(Levels.Bass);
            if (!bass.Playing) bass.Play();
        }
        AudioServer.SetBusVolumeDb(environment, environmentDb - DuckDb * (float)Levels.Duck);
        if (Levels.Rhythm == 0)
        {
            untilBeat = 0;
            return;
        }
        untilBeat -= delta;
        while (untilBeat <= 0)
        {
            Beat();
            untilBeat += double.Lerp(SlowBeat, FastBeat, Levels.Rhythm);
        }
    }

    public override void _ExitTree()
    {
        bass.Stop();
        pulse.Stop();
        scrape.Stop();
        if (region is not null) AudioServer.SetBusVolumeDb(environment, environmentDb);
    }

    private void Beat()
    {
        Beats++;
        pulse.VolumeDb = PulseDb;
        pulse.Play();
        scrape.VolumeDb = ScrapeDb + (float)Mathf.LinearToDb(Levels.Rhythm);
        scrape.Play();
    }

    private AudioStreamPlayer Player(string name, string sound)
    {
        var player = new AudioStreamPlayer { Name = name, Stream = GD.Load<AudioStream>($"res://audio/{sound}.wav") };
        AddChild(player);
        return player;
    }
}
