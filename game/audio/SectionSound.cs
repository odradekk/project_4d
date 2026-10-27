using System;
using Game.Adapter;
using Godot;

namespace Game.Audio;

/// <summary>
/// A spatial sound in the main listening world, filtered by section distance.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class SectionSound : AudioStreamPlayer3D
{
    public const string EnvironmentBus = "Environment";
    private Region3D? region;
    private string section = "";
    private string busName = "";
    private AudioEffectLowPassFilter? filter;

    /// <summary>Makes sure the bus that the environment sounds are mixed into exists.</summary>
    public static void EnsureEnvironmentBus()
    {
        if (AudioServer.GetBusIndex(EnvironmentBus) >= 0) return;
        AudioServer.AddBus();
        AudioServer.SetBusName(AudioServer.BusCount - 1, EnvironmentBus);
    }

    /// <summary>Assigns a loaded region and source section before playback. Add this node to the main viewport first.</summary>
    public void Configure(Region3D region, string section)
    {
        if (!IsInsideTree()) throw new InvalidOperationException("Add the sound to the scene before configuring it.");
        var distance = region.SectionDistance(section);
        this.region = region;
        this.section = section;
        if (filter is null)
        {
            EnsureEnvironmentBus();
            AudioServer.AddBus();
            var index = AudioServer.BusCount - 1;
            busName = $"SectionSound-{GetInstanceId()}";
            AudioServer.SetBusName(index, busName);
            AudioServer.SetBusSend(index, EnvironmentBus);
            filter = new AudioEffectLowPassFilter();
            AudioServer.AddBusEffect(index, filter);
            Bus = busName;
        }
        ApplyDistance(distance);
    }

    public override void _Process(double delta)
    {
        if (region is not null) ApplyDistance(region.SectionDistance(section));
    }

    private void ApplyDistance(double distance)
    {
        AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex(busName), (float)(-20 * distance));
        filter!.CutoffHz = (float)(18000 / (1 + 18 * distance));
    }

    public override void _ExitTree()
    {
        Stop();
        if (filter is not null)
        {
            AudioServer.RemoveBus(AudioServer.GetBusIndex(busName));
            filter = null;
            region = null;
        }
    }
}
