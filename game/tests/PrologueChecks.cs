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
/// The prologue from scene 1 to scene 9 played through in one run, started as the main scene starts it and without a debug
/// entry. Each scene is finished by the quickest moves that its own checks use. What is checked is the way between the scenes:
/// every scene starts with a checkpoint and with what the scenes before gave it, the scenes of the school share one region,
/// a region is gone before the next one is loaded, and a scene that is started again starts as it did the first time. Waits for
/// the scripts of the scenes run at a faster pace.
/// </summary>
public partial class PrologueChecks : Node
{
    private const float FastSpeed = 8;

    private readonly Dictionary<int, (bool Checkpoint, int[] Pages)> starts = [];
    private TurningFixture play = null!;
    private SchoolFixture school = null!;
    private Prologue prologue = null!;
    private float speed = FastSpeed;
    private int startCount;
    private long ticks;
    private bool ended;

    private Region3D Region => prologue.Region;
    private FirstPersonPlayer Player => Region.GetNode<FirstPersonPlayer>("Generated/Player");

    public override void _PhysicsProcess(double delta) => ticks++;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        school = new SchoolFixture(play);
        try
        {
            prologue = new Prologue();
            prologue.SceneStarted += number =>
            {
                startCount++;
                starts[number] = (prologue.Scene!.GetChildren().OfType<SceneCheckpoint>().Any(), Player.Hud.UnlockedPages.Order().ToArray());
            };
            prologue.Finished += () => ended = true;
            AddChild(prologue);
            await PlayMorning();
            await PlaySchoolRoad();
            var schoolRegion = await PlayMathClass();
            schoolRegion = await PlayRecess(schoolRegion);
            schoolRegion = await PlayDuty(schoolRegion);
            await PlayHomeRoad(schoolRegion);
            await PlayHomeEvening();
            var roomRegion = await PlayCatRoom();
            await PlayDeepRoom(roomRegion);
            await Until(() => ended, 15, "the prologue must end after scene 9");
            Check(Player.Hud.UnlockedPages.Order().SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]), "the notebook has all eight pages at the end");
            RemoveChild(prologue);
            prologue.Free();
            // The audio thread fades a stopped sound out before it lets go of it, which takes time and not frames.
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            GD.Print("Prologue checks passed: scenes 1 to 9 in a row, with the notebook, the wallet, the cat and a checkpoint at every start, and six scenes started again.");
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

    // The scene has started.
    private async Task Arrive(int number, params int[] pages)
    {
        await Until(() => starts.ContainsKey(number), 20, $"scene {number} must start");
        await play.Frames(3);
        CheckStart(number, pages);
    }

    // The scene is started again by what asks for it, and starts as it did the first time.
    private async Task Again(Action ask, int number, params int[] pages)
    {
        var before = startCount;
        ask();
        await Until(() => startCount > before, 20, $"scene {number} must start again");
        await play.Frames(3);
        CheckStart(number, pages);
    }

    // The scene has a checkpoint, only its own region is loaded, she holds nothing and carries nothing in the backpack, and the
    // notebook has the pages that the scenes before drew.
    private void CheckStart(int number, int[] pages)
    {
        Check(prologue.Number == number, $"scene {number} is the one being played");
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
}
