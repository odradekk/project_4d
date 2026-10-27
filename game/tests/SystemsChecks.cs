using System;
using Game.Player;
using Game.Flow;
using Game.Interaction;
using Game.Debugging;
using Godot;

namespace Game.Tests;

public partial class SystemsChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var floor = new StaticBody3D { Position = new Vector3(0, -.1f, 0) };
            floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(10, .2f, 10) } });
            AddChild(floor);
            var player = GD.Load<PackedScene>("res://player/first_person_player.tscn").Instantiate<FirstPersonPlayer>();
            AddChild(player); await Frames(2);
            var hud = player.Hud;
            hud.Say("小满", "第一句", 1); hud.Say("老师", "第二句", 2);
            Check(hud.SubtitleText == "小满：第一句", "first subtitle starts immediately");
            hud._Process(.9); Check(hud.SubtitleText == "小满：第一句", "first subtitle stays for its duration");
            hud._Process(.2); Check(hud.SubtitleText == "老师：第二句", "subtitles play in order");
            hud._Process(2); Check(hud.SubtitleText == "", "last subtitle disappears");
            hud.Say("", "独白", 1); hud.ClearText(); Check(hud.SubtitleText == "", "restart clears subtitles");
            hud.Say("小满", "先说的话", 2); hud.Say("老师", "接着说的话", 1);
            hud._Process(.5); hud.ShowNotice("被挡住了", .6);
            Check(!hud.SubtitleVisible && hud.CrosshairVisible, "a notice hides the subtitle and restores the crosshair");
            hud._Process(.4); Check(!hud.SubtitleVisible, "the subtitle stays hidden while the notice is active");
            hud._Process(.4);
            Check(hud.SubtitleVisible && !hud.CrosshairVisible && hud.SubtitleText == "小满：先说的话",
                "the interrupted subtitle resumes when the notice ends");
            hud._Process(1.29); Check(hud.SubtitleText == "小满：先说的话", "only time after the notice counts toward subtitle duration");
            hud._Process(.02); Check(hud.SubtitleText == "老师：接着说的话", "the queued subtitle follows after the first visible duration");
            hud.ShowNotice("被挡住了", 1); hud.ClearText(); hud._Process(2);
            Check(hud.SubtitleText == "" && hud.HintText == "" && hud.CrosshairVisible, "restart clears interrupted subtitles and notices");
            hud.ShowNotice("等一下", 1); hud.Say("", "稍后出现", 1);
            hud._Process(.8); Check(!hud.SubtitleVisible, "a line queued during a notice waits until it ends");
            hud._Process(.3); Check(hud.SubtitleVisible && hud.SubtitleText == "稍后出现", "a waiting line starts after the notice");
            hud._Process(.89); Check(hud.SubtitleText == "稍后出现", "a waiting line keeps its full visible duration");
            hud._Process(.02); Check(hud.SubtitleText == "", "the waiting line disappears after its visible duration");
            hud.NotebookAvailable = true;
            Check(!hud.OpenNotebook(), "a notebook with no unlocked pages cannot open");
            hud.NotebookAvailable = false;
            hud.UnlockPage(1); hud.UnlockPage(1);
            Check(hud.UnlockedPages.Count == 1 && !hud.OpenNotebook(), "pre-scene-seven pages are remembered but cannot open");
            for (int i = 2; i <= 8; i++) hud.UnlockPage(i);
            hud.NotebookAvailable = true;
            Check(hud.OpenNotebook() && hud.CurrentPage == 1, "scene seven opens the first unlocked page");
            hud.TurnPage(-1); Check(hud.CurrentPage == 1, "turning stops at the first page");
            for (int i = 2; i <= 8; i++) { hud.TurnPage(1); Check(hud.CurrentPage == i, "all eight pages can be visited"); }
            hud.TurnPage(1); Check(hud.CurrentPage == 8, "turning stops at the last page");
            hud.TurnPage(-1); Check(hud.CurrentPage == 7, "left turns to the preceding page");
            hud.CloseNotebook();
            var nextHud = new Game.UI.GameHud { Notebook = hud.Notebook }; AddChild(nextHud);
            Check(nextHud.OpenNotebook() && nextHud.UnlockedPages.Count == 8, "scene changes retain the same notebook progress");
            nextHud.QueueFree();
            hud.SetHint("E  翻字条"); hud.ShowNotice("被挡住了", 1);
            Check(hud.HintText == "被挡住了", "blocked reason uses the shared hint display");
            hud._Process(1); Check(hud.HintText == "E  翻字条", "the current prompt returns after a notice");
            var target = new Interactable3D { Prompt = "翻字条", Position = new Vector3(0, 1.3f, -1.2f) };
            target.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(.4f, .4f, .4f) } });
            AddChild(target);
            var interactions = 0; target.Interacted += () => interactions++;
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }); await Frames(2);
            Check(hud.HintText.Contains("翻字条"), "aiming at a target shows its prompt");
            KeyPress(Key.N); await Frames(1);
            Check(hud.NotebookOpen && !player.PointerCaptured, $"opening the notebook releases capture: open={hud.NotebookOpen}, mode={Input.MouseMode}");
            KeyPress(Key.Right); await Frames(1); Check(hud.CurrentPage == 2, "right key turns the notebook page");
            KeyPress(Key.Left); await Frames(1); Check(hud.CurrentPage == 1, "left key turns back");
            var position = player.Position;
            Input.ActionPress("move_forward"); await Frames(5); Input.ActionRelease("move_forward");
            Check(Mathf.Abs(player.Position.Z - position.Z) < .001f, "the notebook blocks movement");
            KeyPress(Key.E); await Frames(1);
            Check(!hud.NotebookOpen && interactions == 0 && player.PointerCaptured,
                $"E close: open={hud.NotebookOpen}, hits={interactions}, mode={Input.MouseMode}");
            KeyPress(Key.E); await Frames(1); Check(interactions == 1, "E activates the aimed target");
            KeyPress(Key.N); KeyPress(Key.E); await Frames(1);
            Check(!hud.NotebookOpen && interactions == 1, "closing in the opening frame cannot activate the target");
            KeyPress(Key.N); KeyPress(Key.Escape); await Frames(1);
            Check(!hud.NotebookOpen && player.PointerCaptured, "Esc closes only the notebook");
            KeyPress(Key.Escape); KeyPress(Key.N); KeyPress(Key.N); await Frames(1);
            Check(!player.PointerCaptured, "closing restores an initially released pointer");
            var checkpoint = new SceneCheckpoint(); AddChild(checkpoint);
            var state = 0; var clears = 0;
            checkpoint.Set(() => state = 7); checkpoint.Restarting += () => { clears++; hud.ClearText(); };
            hud.Say("", "旧台词", 1); state = 9;
            Check(checkpoint.Restart() && state == 7 && hud.SubtitleText == "", "restart restores the saved scene entry and clears text");
            state = 12; Check(checkpoint.Restart() && state == 7 && clears == 2, "repeated restarts restore once each");
            checkpoint.Set(() => state = 3); checkpoint.Restart();
            Check(state == 3 && clears == 3, "the most recent checkpoint replaces the previous entry");
            target.QueueFree(); floor.QueueFree(); player.QueueFree(); await Frames(1);
            var path = DebugScenes.Find("systems-demo");
#if DEBUG
            Check(path == "res://tests/systems_demo.tscn", "debug entry resolves the registered real scene");
            var demo = GD.Load<PackedScene>(path).Instantiate(); AddChild(demo); await Frames(2);
            var demoPlayer = demo.GetNode<FirstPersonPlayer>("Region/Generated/Player");
            Check(demoPlayer.Hud.NotebookAvailable && demoPlayer.Hud.UnlockedPages.Count == 2, "debug scene applies its own opening pages");
            var entrance = demo.GetNode<Node3D>("Region/Sections/Ⅰ/Spawns/Xiaoman").Transform;
            var scene = (SystemsDemo)demo;
            for (var i = 0; i < 2; i++)
            {
                demoPlayer.Position += new Vector3(1, 0, 0); demoPlayer.Hud.Say("", "旧台词", 5);
                Check(scene.Checkpoint.Restart() && demoPlayer.Transform.IsEqualApprox(entrance) && demoPlayer.Hud.SubtitleText == "",
                    "the real scene restores its entry and clears old dialogue on every retry");
            }
            var prism = demo.GetNode<Game.Rendering.PrismView>("Region/Generated/Prism");
            var controls = prism.GetNode<Label>("Controls");
            demoPlayer.Hud.Say("小满", "台词", 5); await Frames(1);
            Check(!controls.Visible, "control help does not cover a visible subtitle");
            demoPlayer.Hud.ClearText(); await Frames(1);
            Check(controls.Visible, "control help returns after subtitles clear");
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true }); await Frames(1);
            Check(prism.Raised, "the prism is raised before opening the notebook");
            KeyPress(Key.N); await Frames(1);
            Check(demoPlayer.Hud.NotebookOpen && !prism.Raised, "opening the notebook releases the prism");
#else
            Check(path is null, "release builds hide debug entries");
#endif
            GD.Print("Systems checks passed: subtitles, notebook, interaction, input, checkpoint and debug entry.");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError($"CHECK FAILED: {error.Message}"); GetTree().Quit(1); }
        finally { Input.MouseMode = Input.MouseModeEnum.Visible; }
    }
    private static void KeyPress(Key key) => Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
    private async System.Threading.Tasks.Task Frames(int count)
    { for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
