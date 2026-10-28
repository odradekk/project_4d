using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Player;
using Game.UI;
using Godot;

namespace Game.Flow;

/// <summary>
/// The prologue from scene 1 to scene 8, one scene after the other. A scene is added to this node and plays until it emits
/// Finished. Between two scenes the screen fades to black, the sounds stop, and the scene before is freed with its region;
/// the scenes of the school share one region, which stays until the last of them is over. What the scenes pass on is kept
/// here: the notebook, the wallet, and a checkpoint at the start of every scene that did not set its own. The last scene
/// stays on the screen when it is over.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class Prologue : Node
{
    // The two yuan on the table in the morning, in mao.
    private const int TableMao = 20;
    private const double FadeSeconds = .4;
    // The audio thread fades a stopped sound out before it lets go of it, which takes time and not frames.
    private const double SoundSeconds = .5;

    private readonly ColorRect veil = new() { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
    private NotebookState notebook = new();
    private CanvasLayer layer = null!;
    private Tween? fade;

    /// <summary>Raised when a scene has been added and given what the scene before passed on, with its number from 1.</summary>
    [Signal] public delegate void SceneStartedEventHandler(int number);

    /// <summary>Raised when the last scene is over.</summary>
    [Signal] public delegate void FinishedEventHandler();

    /// <summary>The number of the scene being played, from 1.</summary>
    public int Number { get; private set; }

    public Node? Scene { get; private set; }

    /// <summary>The region the scene plays in.</summary>
    public Region3D Region { get; private set; } = null!;

    public override async void _Ready()
    {
        layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        layer.AddChild(veil);
        veil.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var morning = await Play(new HomeMorning());
        var wallet = new Wallet(morning.MoneyTaken ? TableMao : 0);
        await Play(new SchoolRoad { Wallet = wallet });
        var school = await EnterSchool();
        await Play(new MathClass { Region = school }, school);
        await Play(new Recess { Region = school }, school);
        await Play(new Duty { Region = school }, school);
        await Play(new HomeRoad { Wallet = wallet });
        await Play(new HomeEvening());
        await Play(new CatRoom());
        EmitSignal(SignalName.Finished);
    }

    /// <summary>
    /// Plays the scene until it finishes. <paramref name="shared"/> is the region that the scene plays in, if it does not load
    /// its own; the scene before leaves without it.
    /// </summary>
    private async Task<T> Play<T>(T next, Region3D? shared = null) where T : Node
    {
        await Leave(shared);
        AddChild(next);
        Scene = next;
        Region = shared ?? next.GetNode<Region3D>("Region");
        var player = Region.GetNode<FirstPersonPlayer>("Generated/Player");
        Carry(player.Hud);
        SetCheckpoint(next, player);
        Number++;
        EmitSignal(SignalName.SceneStarted, Number);
        Fade(0);
        // Every scene ends with its Finished signal.
        await ToSignal(next, SignalName.Finished);
        return next;
    }

    /// <summary>Leaves the scene before and loads the school region, for scenes 3 to 5 to play in.</summary>
    private async Task<Region3D> EnterSchool()
    {
        await Leave(null);
        var region = GD.Load<PackedScene>(SchoolRoom.RegionPath).Instantiate<Region3D>();
        region.Name = "Region";
        AddChild(region);
        if (!region.Loaded) throw new InvalidOperationException($"The school region did not load:\n{region.Diagnostics}");
        return region;
    }

    // Everything but the veil and the shared region goes. A region leaves the viewport's world as it is freed, so the next
    // region is added after that.
    private async Task Leave(Node? keep)
    {
        var gone = GetChildren().Where(node => node != layer && node != keep).ToArray();
        if (gone.Length == 0) return;
        await ToSignal(Fade(1), Tween.SignalName.Finished);
        foreach (var node in gone)
        {
            foreach (var type in new[] { "AudioStreamPlayer3D", "AudioStreamPlayer" })
                foreach (var sound in node.FindChildren("*", type, true, false)) sound.Call(AudioStreamPlayer.MethodName.Stop);
            RemoveChild(node);
            node.Free();
        }
        await ToSignal(GetTree().CreateTimer(SoundSeconds), SceneTreeTimer.SignalName.Timeout);
    }

    private Tween Fade(float alpha)
    {
        fade?.Kill();
        fade = CreateTween();
        fade.TweenProperty(veil, "modulate:a", alpha, FadeSeconds);
        return fade;
    }

    // The notebook of the scene before, with its pages and whether she can open it, in place of the new one. Whatever the new
    // scene did to its own notebook while it started is kept.
    private void Carry(GameHud hud)
    {
        foreach (var page in hud.Notebook.Pages.ToArray()) notebook.Unlock(page);
        notebook.Available |= hud.Notebook.Available;
        hud.Notebook = notebook;
    }

    // A scene without a checkpoint of its own puts her back where she started it.
    private static void SetCheckpoint(Node scene, FirstPersonPlayer player)
    {
        if (scene.GetChildren().OfType<SceneCheckpoint>().Any()) return;
        var entry = player.Transform;
        var checkpoint = new SceneCheckpoint();
        checkpoint.Set(() =>
        {
            player.Transform = entry;
            player.Velocity = Vector3.Zero;
            player.Hud.ClearText();
        });
        scene.AddChild(checkpoint);
    }
}
