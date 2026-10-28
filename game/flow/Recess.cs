using System.Collections.Generic;
using System.Linq;
using Game.Adapter;
using Game.Characters;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 4 of the prologue, the ten minutes between lessons, in the school region where scene 3 left it. The class runs
/// out and Xiaoman does tricks with the prism for her desk mate: the eraser flipped, a pencil stood up and laid down. Then
/// she stands a pencil up in the window rail, and the bell rings before she can lay it down. The pencil stays standing
/// there for scene 5.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class Recess : Node
{
    private const string Eraser = "eraser";
    private const string DeskPencil = "pencilDesk";
    private const int FlipPage = 2;
    private const int StandPage = 3;

    /// <summary>Emitted once, when Xiaoman is back in her seat and the teacher has come in.</summary>
    [Signal] public delegate void FinishedEventHandler();

    /// <summary>The school region as scene 3 leaves it. Left empty, the scene loads a fresh one.</summary>
    public Region3D? Region { get; set; }

    private readonly Dictionary<Character3D, Transform3D> desks = [];
    private SceneScript script = null!;
    private SchoolRoom room = null!;
    private Vector3 board;
    private int leaving;
    private bool flipped, stood, laid;

    public override void _Ready()
    {
        room = Region is null ? SchoolRoom.Open(this) : SchoolRoom.Of(Region);
        Region = room.Region;
        script = new(room.Player.Hud);
        // The teacher keeps the checkpoint of scene 3, which must not take her back to that scene's seat when the teacher sees her.
        room.Teacher.GetNode<Teacher>("Teacher").Checkpoint = null;
        room.Region.PrismEnabled = true;
        room.Player.Seated = true;
        board = room.Region.GetNode<Node3D>("Sections/Ⅰ/Spawns/Teacher").Position;
        // The class sits at the desks, as scene 3 leaves it.
        foreach (var classmate in room.Classmates.Append(room.Deskmate))
        {
            classmate.Sit();
            desks[classmate] = new Transform3D(new Basis(Vector3.Up, classmate.Rotation.Y), classmate.Position);
        }

        script.Narrate("", "下课铃响了。同学们一窝蜂地跑出了教室。", 3);
        script.Do(Dismiss);
        script.Wait(3);
        script.Say(room.Deskmate, "小满，你不出去玩？", 3);
        script.Narrate("小满", "不去。我给你变个魔术。", 2.5);
        script.Do(() => Notice("把棱镜对准桌上的橡皮，按 R 翻面。"));
        script.Until(() => flipped && laid);
        script.Do(CueWindow);
        script.Until(() => room.State(SchoolRoom.WindowPencil).UprightOrigin is not null);
        script.Do(Bell);
        script.Until(() => room.IsUpright(SchoolRoom.WindowPencil));
        script.Do(SitBack);
        script.Narrate("", "小满慌慌张张地坐好，把窗台上那支竖着的铅笔忘在了脑后。", 4.5);
        script.Do(() => EmitSignal(SignalName.Finished));
    }

    public override void _Process(double delta)
    {
        script.Tick(delta);
        Observe();
    }

    // After her hand has moved things this frame: the pencil on the sill is not hers to take away, since it would not come back
    // from outside the window.
    public override void _PhysicsProcess(double delta) => room.KeepWindowPencilOnSill();

    private void Notice(string text) => room.Player.Hud.ShowNotice(text, 5);

    // Everyone but her desk mate goes out through the door, which shuts behind the last of them.
    private void Dismiss()
    {
        room.OpenDoor();
        leaving = room.Classmates.Count + 1;
        foreach (var classmate in room.Classmates)
            SchoolRoom.Walk(classmate, [new(room.DoorIn.X, 0, classmate.Position.Z), room.DoorIn, room.DoorOut], true, arrived => Left(classmate, arrived));
        SchoolRoom.Walk(room.Teacher, [room.DoorIn, room.DoorOut], false, arrived => Left(room.Teacher, arrived));
    }

    private void Left(Character3D who, bool arrived)
    {
        // Whoever was stopped on the way is still in the room.
        if (arrived) who.Visible = false;
        if (--leaving == 0) room.ShutDoor();
    }

    // What she has done with her hands, in any order: each trick is answered once.
    private void Observe()
    {
        if (!flipped && room.IsFlipped(Eraser))
        {
            flipped = true;
            room.Deskmate.Say("哇，你怎么变的？", 3);
            room.Player.Hud.UnlockPage(FlipPage);
            Notice("再试试铅笔：对准它按 F 竖起。");
        }
        if (!stood && room.IsUpright(DeskPencil))
        {
            stood = true;
            room.Deskmate.Say("笔呢？", 2.5);
            Notice("再按一次 F，把它放倒。");
        }
        if (stood && !laid && room.IsLying(DeskPencil))
        {
            laid = true;
            room.Deskmate.Say("又回来了！", 2.5);
            room.Player.Hud.UnlockPage(StandPage);
        }
    }

    // She is free to go to the window, which is out of reach of her seat; the pencil lying on the sill is the next one.
    private void CueWindow()
    {
        room.Player.Seated = false;
        room.Player.Hud.Say("小满", "窗台上还有一支铅笔，再变一支！", 3);
        Notice("走到窗台边，对准铅笔按 F 竖起。");
    }

    // The bell rings the moment the pencil starts to stand up: the prism goes back in the desk, so that she cannot lay it down.
    private void Bell()
    {
        room.Region.PrismEnabled = false;
        room.OpenDoor();
        room.Player.Hud.Say("", "上课铃突然响了！老师迈步进了教室。", 3);
        foreach (var (classmate, place) in desks) SchoolRoom.SitDown(classmate, place);
        room.Teacher.Visible = true;
        room.Teacher.PlaceAt(room.DoorOut, 0);
        SchoolRoom.Walk(room.Teacher, [room.DoorIn, board], false, _ => { });
    }

    private void SitBack()
    {
        room.Player.Transform = room.Seat;
        room.Player.Velocity = Vector3.Zero;
        room.Player.Seated = true;
        room.ShutDoor();
    }
}
