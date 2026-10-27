using System;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Debugging;
using Game.Player;
using Game.UI;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The backpack on the seam test region: what she holds goes into it when she crosses, the number keys take things out, it
/// has four slots, and the debug entry presets it and the hand.
/// </summary>
public partial class BackpackChecks : Node
{
    private static readonly string[] SmallThings = ["rubber", "ruler", "key", "bun", "note"];

    private TurningFixture play = null!;
    private SeamPlay seam = null!;
    private FirstPersonPlayer player => play.Player;
    private BackpackBar bar => seam.Bar;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        seam = new SeamPlay(this, play);
        try
        {
            await seam.Load();
            await CheckPackFromHand();
            await seam.Load();
            await CheckSlotsKeepTheirPlace();
            await seam.Load();
            await CheckHandIntoBackpack();
            await seam.Load();
            await CheckTakeOutBlocked();
            await seam.Load();
            await CheckFullBackpack();
            await CheckDebugEntry();
            await play.Unload();
            GD.Print("Backpack checks passed: pack, cross, take out, four slots, debug entry.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    // The engine can also pack what is in the hand without a crossing; the thing is gone from every section at once.
    private async Task CheckPackFromHand()
    {
        await seam.Hold("rubber");
        Check(play.HasSlice("Ⅰ", "rubber"), "what is in the hand is drawn in its section");
        seam.Pack("rubber");
        await play.Frames(3);
        Check(play.Actions.Held is null && !play.HasSlice("Ⅰ", "rubber"), "what is packed from the hand is no longer drawn or held");
    }

    // "bun" sorts before "rubber", and must not push the rubber into the second slot; a slot that is emptied stays empty
    // until something new comes, and the number key of a thing is the number it has.
    private async Task CheckSlotsKeepTheirPlace()
    {
        seam.Pack("rubber");
        seam.Pack("bun");
        await play.Frames(3);
        Check(bar.Texts[0].Contains("rubber") && bar.Texts[1].Contains("bun"), $"things stay in the slots they came into: {string.Join(" | ", bar.Texts)}");
        Press(Key.Key1);
        await play.Frames(5);
        Check(play.Actions.Held == "rubber" && bar.Texts[0].Contains('—') && bar.Texts[1].Contains("bun"), $"a slot that is emptied stays empty: {string.Join(" | ", bar.Texts)}");
        seam.Pack("key");
        await play.Frames(3);
        Check(bar.Texts[0].Contains("key") && bar.Texts[1].Contains("bun"), $"the next thing takes the first empty slot: {string.Join(" | ", bar.Texts)}");
        Press(Key.Key2);
        await play.Frames(2);
        Check(play.Actions.Held == "rubber", "the hand is full, and the number key of the bun does not take it out");
        Check(seam.InBackpack.SequenceEqual(["key", "bun"]), "and the backpack still holds the key and the bun in their slots");
    }

    private async Task CheckHandIntoBackpack()
    {
        await seam.Stand(new(-1.35f, .02f, 1.5f), new(-1.5f, 1, 1.5f));
        await seam.Hold("rubber");
        await seam.RaiseLens();
        Check(play.Actions.Held == "rubber", "the rubber must be in her hand");
        await seam.Click();
        seam.CheckArrived("Ⅱ", "Ⅰ");
        Check(play.State("rubber").InBackpack && play.Actions.Held is null, "what she held goes into the backpack when she crosses");
        Check(!play.HasSlice("Ⅰ", "rubber") && !play.HasSlice("Ⅱ", "rubber"), "what is in the backpack has no slice in any section");
        Check(seam.InBackpack.SequenceEqual(["rubber"]), "the backpack must hold the rubber");
        Check(bar.Texts.Count == Backpack.Slots && bar.Texts[0].StartsWith('1') && bar.Texts[3].StartsWith('4') && bar.Texts[0].Contains("rubber") && bar.Texts[1].Contains('—'),
            $"the backpack bar must show it in the first slot: {string.Join(" | ", bar.Texts)}");

        Press(Key.Key2);
        await play.Frames(2);
        Check(play.Actions.Held is null && seam.InBackpack.Length == 1, "an empty slot takes nothing out");
        Press(Key.Key1);
        await play.Frames(5);
        Check(play.Actions.Held == "rubber" && seam.InBackpack.Length == 0, "a number key takes the thing in that slot into the hand");
        Check(play.HasSlice("Ⅱ", "rubber") && !play.HasSlice("Ⅰ", "rubber"), "out of the backpack it lies in the section she is in");
        var rubber = play.Position("rubber");
        var reach = new Vector2(rubber.X - player.Position.X, rubber.Z - player.Position.Z).Length();
        Check(reach is > .45f and < .75f, $"the engine puts it at her feet and the hand then carries it to where the hand is, 0.6 m ahead; it is {reach} m from her");
        Check(bar.Texts.All(text => text.Contains('—')), "the bar shows the backpack empty again");
    }

    // The key lies where she stands, so the thing from her backpack would come out inside it: the engine refuses, before any
    // physics step could lift her onto the key.
    private async Task CheckTakeOutBlocked()
    {
        seam.Pack("ruler");
        await seam.Stand(new(0, .02f, 3), new(0, 1, 2));
        var feet = play.State("xiaoman").Pose.Position;
        Check(seam.World.PlaceEntity(new("key"), new Pose4(new(feet.X, .03125, feet.Z, 0), play.State("key").Pose.Rotation)) is UpdateResult.Updated,
            "the key can be put where she stands");
        var refusal = play.Region.Travel!.TakeOut(0);
        Check(refusal is { Code: FailureCode.TranslationBlocked } && RefusalLines.ForTakingOut(refusal).Contains("这里放不开"),
            $"a thing with no room to come out is refused with a line saying so; got {refusal}");
        Check(seam.InBackpack.SequenceEqual(["ruler"]) && play.Actions.Held is null, "and it stays in the backpack");
    }

    private async Task CheckFullBackpack()
    {
        Check(Backpack.Slots == 4, "the backpack has four slots");
        foreach (var id in SmallThings.Skip(1)) seam.Pack(id);
        await seam.Stand(new(-1.35f, .02f, 1.5f), new(-1.5f, 1, 1.5f));
        await seam.Hold("rubber");
        await seam.RaiseLens();
        await seam.Click();
        Check(play.Region.CurrentSection == "Ⅰ" && play.Actions.Held == "rubber", "with a full backpack she stays, with the thing still in her hand");
        Check(seam.Said.Contains("装不下"), $"she must say it does not fit; she said \"{seam.Said}\"");

        player.Hud.ClearText();
        Press(Key.Key1);
        await play.Frames(2);
        Check(play.Actions.Held == "rubber" && seam.Said.Contains("手里已经拿着东西"), $"she cannot take a second thing in her hand; she said \"{seam.Said}\"");

        Press(Key.E);
        await play.Frames(5);
        Check(play.Actions.Held is null, "she lets the rubber go");
        await seam.Click();
        seam.CheckArrived("Ⅱ", "Ⅰ");
        Check(seam.InBackpack.Length == Backpack.Slots, "a full backpack crosses with her, since what is in it takes no new slot");
    }

    private async Task CheckDebugEntry()
    {
        var path = DebugScenes.Find("seam-demo");
#if DEBUG
        Check(path == "res://tests/seam_demo.tscn", "the debug entry for the seam scene must be registered");
        var demo = GD.Load<PackedScene>(path).Instantiate();
        AddChild(demo);
        await play.Frames(5);
        var region = demo.GetNode<Region3D>("Region");
        Check(region.Travel!.Backpack.Contents.Select(item => item?.Value).SequenceEqual(["rubber", "ruler", null, null]), "the debug scene presets the backpack");
        Check(region.Actions!.Held == "pole", "the debug scene presets the hand");
        Check(!region.HasNode("Generated/Ⅰ/Dynamic/rubber") && !region.HasNode("Generated/Ⅱ/Dynamic/rubber"), "what it put in the backpack is not drawn in the world");
        var presetBar = region.GetNode<FirstPersonPlayer>("Generated/Player").Hud.GetNode<BackpackBar>("Backpack");
        Check(presetBar.Texts[0].Contains("rubber") && presetBar.Texts[1].Contains("ruler"), "and the backpack bar shows it");
        demo.Free();
        await play.Frames(2);
#else
        Check(path is null, "release builds hide debug entries");
        await play.Frames(1);
#endif
    }
}
