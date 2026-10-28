using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Flow;
using Game.Interaction;
using Game.Player;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The prologue from scene 1 to scene 8 played through in one run, from the start of the main scene and without a debug
/// entry. Each scene is finished by the quickest moves that its own checks use. What is checked is the way between the scenes:
/// every scene starts with a checkpoint and with what the scenes before gave it, the scenes of the school share one region,
/// and a region is gone before the next one is loaded. Waits for the scripts of the scenes run at a faster pace. With
/// -- --real-time they run at the pace of play and the time of every frame is reported by scene; --uncapped also switches vsync off.
/// </summary>
public partial class PrologueChecks : Node
{
    private const float FastSpeed = 8;

    private readonly Dictionary<int, (bool Checkpoint, int[] Pages)> starts = [];
    private readonly Dictionary<int, List<(double Milliseconds, bool Ticked, bool Raised)>> frames = [];
    private readonly bool realTime = OS.GetCmdlineUserArgs().Contains("--real-time");
    private TurningFixture play = null!;
    private SchoolFixture school = null!;
    private Prologue prologue = null!;
    private float speed;
    private long ticks, lastTicks;
    private ulong lastFrame;
    private bool ended;

    private Region3D Region => prologue.Region;
    private FirstPersonPlayer Player => Region.GetNode<FirstPersonPlayer>("Generated/Player");

    public override void _PhysicsProcess(double delta) => ticks++;

    public override void _Process(double delta)
    {
        var now = Time.GetTicksUsec();
        if (lastFrame != 0 && Engine.TimeScale == 1 && prologue?.Scene is { } scene && GodotObject.IsInstanceValid(scene))
        {
            if (!frames.TryGetValue(prologue.Number, out var times)) frames[prologue.Number] = times = [];
            times.Add(((now - lastFrame) / 1000.0, ticks != lastTicks, Player.PrismRaised));
        }
        lastFrame = now;
        lastTicks = ticks;
    }

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        school = new SchoolFixture(play);
        speed = realTime ? 1 : FastSpeed;
        if (OS.GetCmdlineUserArgs().Contains("--uncapped"))
        {
            DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            Engine.MaxFps = 0;
        }
        try
        {
            prologue = new Prologue();
            prologue.SceneStarted += number => starts[number] = (
                prologue.Scene!.GetChildren().OfType<SceneCheckpoint>().Any(), Player.Hud.UnlockedPages.Order().ToArray());
            prologue.Finished += () => ended = true;
            AddChild(prologue);
            await PlayMorning();
            var wallet = await PlaySchoolRoad();
            var schoolRegion = await PlayMathClass();
            await PlayRecess(schoolRegion);
            await PlayDuty(schoolRegion);
            await PlayHomeRoad(wallet, schoolRegion);
            await PlayHomeEvening();
            await PlayCatRoom();
            await Until(() => ended, 15, "the prologue must end after scene 8");
            Check(Player.Hud.UnlockedPages.Order().SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]), "the notebook has all eight pages at the end");
            RemoveChild(prologue);
            prologue.Free();
            // The audio thread fades a stopped sound out before it lets go of it, which takes time and not frames.
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            if (realTime) Report();
            GD.Print("Prologue checks passed: scenes 1 to 8 in a row, with the notebook, the wallet, the cat and a checkpoint at every start.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Engine.TimeScale = 1;
            Input.ActionRelease("move_forward");
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    // The scene has started: it has a checkpoint, she holds nothing and carries nothing in the backpack, and the notebook has
    // the pages that the scenes before drew.
    private async Task Arrive(int number, params int[] pages)
    {
        await Until(() => prologue.Number == number, 20, $"scene {number} must start");
        await play.Frames(3);
        Check(prologue.FindChildren("Region", "Region3D", true, false).Count == 1, $"only the region of scene {number} is loaded");
        Check(starts[number].Checkpoint, $"scene {number} starts with a checkpoint");
        Check(starts[number].Pages.SequenceEqual(pages), $"scene {number} starts with the pages [{string.Join(", ", pages)}], not [{string.Join(", ", starts[number].Pages)}]");
        Check(Region.Actions!.Held is null && Region.Travel!.Backpack.FreeSlots == Backpack.Slots, $"she carries nothing into scene {number}");
    }

    // Waits for a script, at most the given seconds of game time (physics ticks at 60 a second). A faster pace makes every tick longer.
    private async Task Until(Func<bool> condition, double seconds, string message)
    {
        var limit = ticks + (long)(seconds * 60);
        Engine.TimeScale = speed;
        try
        {
            while (!condition())
            {
                Check(ticks < limit, $"{message} (not within {seconds} s)");
                await play.Frames(1);
            }
        }
        finally
        {
            Engine.TimeScale = 1;
        }
    }

    private Interactable3D Prop(string name) => Region.Props("Ⅰ")!.GetNode<Interactable3D>(name);

    private Task Says(string text, double seconds = 6) => Until(() => Player.Hud.SubtitleText.Contains(text), seconds, $"the subtitle must say '{text}'");

    // A frame without a physics tick costs next to nothing when frames are not held back, so the frames with a tick are listed
    // too, and those with the prism up, when the lens draws the other sections.
    private void Report()
    {
        GD.Print("scene | all frames | frames with a physics tick | with a tick and the prism up (count, median ms, p95 ms, max ms)");
        foreach (var (number, times) in frames.OrderBy(pair => pair.Key))
            GD.Print($"{number,5} | {Describe(times)} | {Describe(times.Where(frame => frame.Ticked))} | {Describe(times.Where(frame => frame.Ticked && frame.Raised))}");
    }

    private static string Describe(IEnumerable<(double Milliseconds, bool Ticked, bool Raised)> frames)
    {
        var sorted = frames.Select(frame => frame.Milliseconds).Order().ToArray();
        return sorted.Length == 0 ? "none" : $"{sorted.Length,6} {sorted[sorted.Length / 2],7:F1} {sorted[(int)(sorted.Length * .95)],7:F1} {sorted[^1],7:F1}";
    }
}
