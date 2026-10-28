using System.Collections.Generic;
using System.Linq;
using Game.Adapter;
using Game.Characters;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 5 of the prologue, the cleaning after school, in the school region where scene 4 left it. Her desk mate runs
/// off and leaves her the chores: sweeping, wiping the board, putting the chairs upside down on the desks, shutting the
/// four windows and locking the door. The last window will not shut because of the pencil that stands in its rail; in the
/// lens it is a glowing plate. Laid down, it lets the window shut, and then the chairs of section Ⅱ are back on the floor.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class Duty : Node
{
    private const int GlowPage = 4;
    // A window that has not moved for this long, and is not at an end of its way, has been stopped by something.
    private const double StuckSeconds = .4;
    private const string PushWindow = "推窗";

    /// <summary>Emitted once, after the door is locked.</summary>
    [Signal] public delegate void FinishedEventHandler();

    /// <summary>The school region as scene 4 leaves it. Left empty, the scene loads a fresh one and puts the pencil in the rail.</summary>
    public Region3D? Region { get; set; }

    private readonly SceneScript script = new();
    private readonly Dictionary<string, (double Position, double Still)> watched = [];
    private readonly HashSet<int> upturned = [];
    private SchoolRoom room = null!;
    private bool pencilStands;
    private bool swept, erased, stuck, found, shut;

    public override void _Ready()
    {
        room = Region is null ? SchoolRoom.Open(this) : SchoolRoom.Of(Region);
        Region = room.Region;
        // What scene 4 does, if it was not played: the class and the teacher are gone and the door is shut.
        foreach (var classmate in room.Classmates) classmate.Visible = false;
        room.Teacher.Visible = false;
        room.Deskmate.Sit();
        room.ShutDoor();
        room.ShowChairs();
        room.Region.PrismEnabled = true;
        room.Player.Seated = false;
        // She gets up into the lane in front of her desk, which leaves her desk mate the way out of his seat.
        room.Player.Position = room.Seat.Origin + new Vector3(0, 0, -1);
        room.Player.DevicePrompts = SchoolRoom.Windows.ToDictionary(window => window, _ => PushWindow);
        room.Broom.Interacted += Sweep;
        room.Chalkboard.Interacted += Erase;
        for (var row = 0; row < room.Rows.Count; row++)
        {
            var number = row;
            room.Rows[row].Interacted += () => Upturn(number);
        }
        room.Door.Interacted += Lock;

        Narrate("", "放学了。今天轮到我和同桌值日。", 3);
        Say(room.Deskmate, 3.5, "小满，你帮我扫一下呗，我急着去踢球！");
        script.Do(DeskmateRuns);
        Narrate("小满", "喂——！……算了。", 2.5);
        Narrate("", "扫地，擦黑板，把椅子倒扣上桌，关窗，锁门。", 4);
        script.Do(StartChores);
    }

    public override void _Process(double delta)
    {
        script.Tick(delta);
        pencilStands = pencilStands || room.KeepWindowPencilStanding();
        Observe(delta);
    }

    private void Narrate(string speaker, string line, double seconds)
    {
        script.Do(() => room.Player.Hud.Say(speaker, line, seconds));
        script.Wait(seconds);
    }

    private void Say(Character3D who, double seconds, string line)
    {
        script.Do(() => who.Say(line, seconds));
        script.Wait(seconds);
    }

    private void DeskmateRuns()
    {
        room.OpenDoor();
        var deskmate = room.Deskmate;
        SchoolRoom.Walk(deskmate, [new(room.DoorIn.X, 0, deskmate.Position.Z), room.DoorIn, room.DoorOut], true, arrived =>
        {
            // If he was stopped on the way he is still in the room.
            if (arrived) deskmate.Visible = false;
            room.ShutDoor();
        });
    }

    private void StartChores()
    {
        SchoolRoom.Offer(room.Broom, true);
        SchoolRoom.Offer(room.Chalkboard, true);
        foreach (var row in room.Rows) SchoolRoom.Offer(row, true);
        room.OfferLocking(true);
    }

    private void Sweep()
    {
        swept = true;
        SchoolRoom.Offer(room.Broom, false);
        room.Player.Hud.Say("小满", "沙沙沙……地扫干净了。", 2.5);
    }

    private void Erase()
    {
        erased = true;
        SchoolRoom.Offer(room.Chalkboard, false);
        room.BoardText.Visible = false;
        room.Player.Hud.Say("小满", "黑板擦干净了，粉笔灰呛人。", 2.5);
    }

    // One press turns the chairs of a whole row; the script puts them in place, since turning them over is not the lesson.
    private void Upturn(int row)
    {
        upturned.Add(row);
        SchoolRoom.Offer(room.Rows[row], false);
        room.UpturnRow(row);
        if (upturned.Count < room.Rows.Count) return;
        room.Player.Hud.Say("小满", "椅子都倒扣好了。教室空荡荡的，和上午镜片里那一页一模一样。", 4.5);
    }

    private void Lock()
    {
        var left = new List<string>();
        if (!swept) left.Add("扫地");
        if (!erased) left.Add("擦黑板");
        if (upturned.Count < room.Rows.Count) left.Add("倒扣椅子");
        if (!SchoolRoom.Windows.All(room.IsShut)) left.Add("关窗");
        if (left.Count > 0)
        {
            room.Player.Hud.Say("小满", $"还没弄完：{string.Join("、", left)}。", 3);
            return;
        }
        room.OfferLocking(false);
        script.Do(() => room.Player.Hud.Say("", "咔哒。门锁好了。", 2.5));
        script.Wait(2.5);
        script.Do(() => EmitSignal(SignalName.Finished));
    }

    // What the windows and the lens show her, once each, in any order.
    private void Observe(double delta)
    {
        foreach (var window in SchoolRoom.Windows)
        {
            var position = room.State(window).DevicePosition!.Value;
            var (before, still) = watched.GetValueOrDefault(window, (position, 0.0));
            still = position == before ? still + delta : 0;
            watched[window] = (position, still);
            if (!stuck && still >= StuckSeconds && room.IsHalfway(window)) Stuck();
        }
        if (!found && room.Prism.SeenEntity() == SchoolRoom.WindowPencil) Found();
        if (!shut && SchoolRoom.Windows.All(room.IsShut)) AllShut();
    }

    private void Stuck()
    {
        stuck = true;
        var hud = room.Player.Hud;
        hud.Say("小满", "推不动了……窗轨里好像卡着什么，可我什么也看不见。", 4);
        hud.ShowNotice("按住右键举起棱镜，看看窗轨。", 5);
    }

    private void Found()
    {
        found = true;
        var hud = room.Player.Hud;
        hud.Say("小满", "窗轨里立着一片发光的薄片……是课间忘在窗台上的那支铅笔！", 4.5);
        hud.ShowNotice("对准它按 F 放倒。", 5);
    }

    // The last window is shut, and the other section has changed: its chairs are back on the floor.
    private void AllShut()
    {
        shut = true;
        Narrate("", "最后一扇窗合上了。小满举起棱镜，瞥了一眼第Ⅱ页。", 3.5);
        script.Do(room.PutChairsOfSectionIIOnFloor);
        Narrate("小满", "那边的椅子……全都端端正正放回地上了，和这边正好相反。", 5);
        script.Do(() => room.Player.Hud.UnlockPage(GlowPage));
        Narrate("", "笔记本上多了一页：发光的东西。别的页也有它。", 4.5);
    }
}
