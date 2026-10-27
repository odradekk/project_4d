using System;
using Game.Adapter;
using Game.Audio;
using Game.Player;
using Godot;

namespace Game.Tests;

public partial class AudioDemo : Node
{
    private readonly (string File, string Name)[] sounds = [
        ("chalk", "粉笔"), ("footsteps", "脚步"), ("cat", "猫"), ("thud", "闷响"),
        ("scratch", "刮擦"), ("heartbeat", "心跳"), ("breathing", "呼吸")];
    private Region3D region = null!;
    private SectionSound sound = null!;
    private Label label = null!;
    private string section = "Ⅰ";
    private int selected;

    public override async void _Ready()
    {
        region = GD.Load<PackedScene>("res://regions/section_test.tscn").Instantiate<Region3D>();
        region.GetNode("Sections").AddChild(new Section3D { Name = "III", SectionId = "Ⅲ", W = .84 });
        AddChild(region);
        region.PrismEnabled = false;
        region.GetNode<Label>("Generated/Prism/Controls").Text = "";
        GetViewport().AudioListenerEnable3D = true;
        var camera = region.GetNode<FirstPersonPlayer>("Generated/Player").Camera;
        sound = new SectionSound { Position = camera.GlobalPosition + new Vector3(0, 0, -2) };
        region.AddChild(sound);
        sound.Configure(region, section);
        sound.Stream = GD.Load<AudioStream>("res://audio/chalk.wav");
        var layer = new CanvasLayer(); AddChild(layer);
        label = new Label { Position = new Vector2(20, 20) }; layer.AddChild(label);
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        UpdateLabel();
        var args = OS.GetCmdlineUserArgs();
        if (args.Length == 2 && args[0] == "--capture") await Capture(args[1]);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode is Key.Key1 or Key.Key2)
        {
            section = key.Keycode == Key.Key1 ? "Ⅰ" : "Ⅱ";
            sound.Configure(region, section);
        }
        else if (key.Keycode >= Key.Key3 && key.Keycode <= Key.Key9)
        {
            selected = (int)key.Keycode - (int)Key.Key3;
            sound.Stop();
            sound.Stream = GD.Load<AudioStream>($"res://audio/{sounds[selected].File}.wav");
            sound.Play();
        }
        else if (key.Keycode == Key.Space)
        {
            if (sound.Playing) sound.Stop(); else sound.Play();
        }
        UpdateLabel();
    }

    private void UpdateLabel() => label.Text = $"1 同页  2 邻页\n空格 播放/停止\n3 粉笔  4 脚步  5 猫\n6 闷响  7 刮擦  8 心跳\n9 呼吸\n当前：{(section == "Ⅰ" ? "同页" : "邻页")} / {sounds[selected].Name}";

    private async System.Threading.Tasks.Task Capture(string directory)
    {
        var bus = AudioServer.GetBusIndex(SectionSound.EnvironmentBus);
        var recorder = new AudioEffectRecord();
        AudioServer.AddBusEffect(bus, recorder);
        foreach (var id in new[] { "Ⅰ", "Ⅱ", "Ⅲ" })
        {
            sound.Configure(region, id);
            await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
            recorder.SetRecordingActive(true);
            sound.Play();
            await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
            sound.Stop();
            recorder.SetRecordingActive(false);
            var error = recorder.GetRecording().SaveToWav($"{directory}/audio-{id}.wav");
            GD.Print($"Capture {id}: {error}");
        }
        GetTree().Quit();
    }
}
