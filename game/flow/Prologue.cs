using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Player;
using Game.UI;
using Godot;

namespace Game.Flow;

/// <summary>
/// The prologue from scene 1 to scene 9, one scene after the other. A scene is added to this node and plays until it emits
/// Finished and the line it is saying is said. Between two scenes the screen fades to black and the scene before is freed with
/// its region. A scene can play in the region of the scene before, as scenes 4 and 5 do in the school of scene 3 and scene 9 in
/// the room of scene 8; the scene that loaded the region stays, since the region is its child, until the last of them is over.
/// What the scenes pass on is kept here: the notebook, and the money, which a scene is given as a copy. Backspace starts the
/// scene again from what the scene before passed on, in a region loaded anew; every scene has a checkpoint at its start that
/// does the same, unless it set its own. The last scene, which ends on the title, stays on the screen when it is over.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class Prologue : Node
{
    private const double FadeSeconds = .4;

    private readonly ColorRect veil = new() { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
    private NotebookState notebook = new();
    private CanvasLayer layer = null!;
    private Label hint = null!;
    private TaskCompletionSource<bool>? running;
    private Tween? fade;

    /// <summary>Raised when a scene has been added and given what the scene before passed on, with its number from 1. It is raised again when the scene is started again.</summary>
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
        hint = new Label { Text = "Backspace  重来本场", Modulate = new Color(1, 1, 1, .7f), MouseFilter = Control.MouseFilterEnum.Ignore };
        hint.AddThemeFontSizeOverride("font_size", 16);
        hint.AddThemeColorOverride("font_shadow_color", Colors.Black);
        layer.AddChild(hint);
        hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft, Control.LayoutPresetMode.Minsize, 12);

        await Play(_ => new HomeMorning());
        var road = await Play(_ => new SchoolRoad());
        await Play(region => new MathClass { Region = region });
        await Play(region => new Recess { Region = region }, true);
        await Play(region => new Duty { Region = region }, true);
        // The money is a copy, so that starting the scene again gives it the money it started with.
        await Play(_ => new HomeRoad { Wallet = new Wallet(road.Wallet.Mao) });
        // Scene 6 ends with the cat in her arms.
        await Play(_ => new HomeEvening { CarryingCat = true });
        await Play(_ => new CatRoom());
        await Play(region => new DeepRoom { Region = region }, true);
        // The title is up: nothing is left to restart.
        hint.Hide();
        EmitSignal(SignalName.Finished);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Backspace }) Restart();
    }

    /// <summary>Starts the scene again, from what the scene before passed on.</summary>
    public void Restart() => running?.TrySetResult(false);

    /// <summary>
    /// Plays the scene until it finishes. <paramref name="create"/> makes the scene, given the region of the scene before if
    /// <paramref name="shares"/> says that the scene plays in it, and none otherwise, which is also the case when the scene is
    /// started again: then it loads a region of its own.
    /// </summary>
    private async Task<T> Play<T>(Func<Region3D?, T> create, bool shares = false) where T : Node
    {
        var pages = notebook.Pages.ToArray();
        var available = notebook.Available;
        var number = Number + 1;
        for (var again = false; ; again = true)
        {
            if (again)
            {
                notebook = new NotebookState { Available = available };
                foreach (var page in pages) notebook.Unlock(page);
            }
            var region = shares && !again ? Region : null;
            await Leave(region is null ? null : HolderOf(region));
            var next = create(region);
            AddChild(next);
            Number = number;
            Scene = next;
            Region = region ?? next.GetNode<Region3D>("Region");
            if (!Region.Loaded)
            {
                // The region draws what is wrong with it, under the veil. The flow stops here.
                veil.Hide();
                throw new InvalidOperationException($"The region of scene {number} did not load:\n{Region.Diagnostics}");
            }
            var player = Region.GetNode<FirstPersonPlayer>("Generated/Player");
            Carry(player.Hud);
            SetCheckpoint(next);
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            running = finished;
            // Every scene ends with its Finished signal.
            next.Connect(SignalName.Finished, Callable.From(() => { finished.TrySetResult(true); }));
            EmitSignal(SignalName.SceneStarted, number);
            Fade(0);
            if (!await finished.Task) continue;
            // The veil covers the lines, so the line that is being said is said before the screen fades.
            var said = player.Hud.SubtitleText;
            while (said != "" && player.Hud.SubtitleText == said) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            return next;
        }
    }

    // The child of this node that holds the region: the region itself, or the scene that loaded it.
    private Node HolderOf(Region3D region)
    {
        Node holder = region;
        while (holder.GetParent() != this) holder = holder.GetParent();
        return holder;
    }

    // The screen fades to black and everything but the veil and the node that holds a shared region goes. The next region is added
    // after that, since a region that is added while another one is still in the tree takes that one's world for the viewport's own,
    // and puts the viewport back on it when it leaves.
    private async Task Leave(Node? keep)
    {
        if (Scene is null) return;
        await ToSignal(Fade(1), Tween.SignalName.Finished);
        foreach (var node in GetChildren().Where(node => node != layer && node != keep).ToArray())
        {
            RemoveChild(node);
            node.Free();
        }
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

    // A scene without a checkpoint of its own is started again from its checkpoint.
    private void SetCheckpoint(Node scene)
    {
        if (scene.GetChildren().OfType<SceneCheckpoint>().Any()) return;
        var checkpoint = new SceneCheckpoint();
        checkpoint.Set(Restart);
        scene.AddChild(checkpoint);
    }
}
