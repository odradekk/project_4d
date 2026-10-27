using System;
using Game.Adapter;
using Game.Audio;
using Godot;

namespace Game.Tests;

public partial class AudioChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var region = GD.Load<PackedScene>("res://regions/section_test.tscn").Instantiate<Region3D>();
            region.GetNode("Sections").AddChild(new Section3D { Name = "Half", SectionId = "Half", W = .21 });
            region.GetNode("Sections").MoveChild(region.GetNode("Sections/Half"), 2);
            AddChild(region);
            Check(GetViewport().AudioListenerEnable3D, "the main viewport must listen to spatial audio");
            Check(region.CurrentSection == "Ⅰ", "listener section must be the spawn section");
            Check(Math.Abs(region.SectionDistance("Ⅰ")) < 1e-8, "same section distance must be zero");
            Check(Math.Abs(region.SectionDistance("Ⅱ") - .42) < 1e-8, "adjacent section distance must be .42");
            var same = new SectionSound();
            var other = new SectionSound();
            region.AddChild(same); region.AddChild(other);
            same.Configure(region, "Ⅰ"); other.Configure(region, "Ⅱ");
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var sameBus = AudioServer.GetBusIndex(same.Bus);
            var otherBus = AudioServer.GetBusIndex(other.Bus);
            Check(sameBus != otherBus && sameBus > 0 && otherBus > 0, "sounds must have independent buses");
            var sameFilter = (AudioEffectLowPassFilter)AudioServer.GetBusEffect(sameBus, 0);
            var otherFilter = (AudioEffectLowPassFilter)AudioServer.GetBusEffect(otherBus, 0);
            Check(AudioServer.GetBusVolumeDb(otherBus) < AudioServer.GetBusVolumeDb(sameBus) - 3, "adjacent sound must be quieter");
            Check(otherFilter.CutoffHz < sameFilter.CutoffHz / 2, "adjacent sound must lose high frequencies");
            Check(AudioServer.GetBusSend(sameBus) == AudioServer.GetBusSend(otherBus), "sounds must share an environment bus");
            var nearVolume = AudioServer.GetBusVolumeDb(otherBus);
            var nearCutoff = otherFilter.CutoffHz;
            other.Configure(region, "Half");
            Check(AudioServer.GetBusVolumeDb(otherBus) > nearVolume && AudioServer.GetBusVolumeDb(otherBus) < AudioServer.GetBusVolumeDb(sameBus), "intermediate distance must produce intermediate volume");
            Check(otherFilter.CutoffHz > nearCutoff && otherFilter.CutoffHz < sameFilter.CutoffHz, "intermediate distance must produce intermediate cutoff");
            same.Stream = GD.Load<AudioStream>("res://audio/chalk.wav");
            Check(same.Stream is AudioStreamWav { LoopMode: AudioStreamWav.LoopModeEnum.Forward }, "chalk must loop until stopped");
            same.Play();
            Check(same.Playing, "chalk must start");
            same.Stop();
            Check(!same.Playing, "chalk must stop");
            foreach (var name in new[] { "footsteps", "cat", "thud", "scratch", "heartbeat", "breathing" })
                Check(GD.Load<AudioStream>($"res://audio/{name}.wav") is not null, $"{name} must be available");
            var oldBus = other.Bus;
            other.Free();
            Check(AudioServer.GetBusIndex(oldBus) == -1, "destroying a sound must release its bus");
            GD.Print("Audio checks passed.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError($"CHECK FAILED: {error}"); GetTree().Quit(1); }
    }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
