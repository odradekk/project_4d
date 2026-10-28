using System.Collections.Generic;
using System.Linq;
using Game.Adapter;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 5 of the prologue, the cleaning after school, in the school region where scene 4 left it. Her desk mate runs
/// off and leaves her the chores: sweeping, wiping the board, putting the chairs upside down on the desks, shutting the
/// four windows and locking the door. The first window will not shut because of the pencil that stands in its rail; in
/// the lens it is a glowing plate. Laid down, it lets the window shut, and then the chairs of section Ⅱ are back on the floor.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class Duty : Node
{
    private const int GlowPage = 4;

    /// <summary>Emitted once, after the door is locked.</summary>
    [Signal] public delegate void FinishedEventHandler();

    /// <summary>The school region as scene 4 leaves it. Left empty, the scene loads a fresh one and stands the pencil in the rail.</summary>
    public Region3D? Region { get; set; }

    private readonly HashSet<int> upturned = [];
    private SceneScript script = null!;
    private SchoolRoom room = null!;
    private bool choresStarted;
    private bool swept, erased, stuck, found, shut;

    public override void _Ready()
    {
        room = Region is null ? SchoolRoom.Open(this) : SchoolRoom.Of(Region);
        Region = room.Region;
        script = new(room.Player.Hud);
        // What scene 4 does, if it was not played: the class and the teacher are gone, the door is shut, and the pencil stands.
        foreach (var classmate in room.Classmates) room.SendHome(classmate);
        room.SendHome(room.Teacher);
        room.Deskmate.Sit();
        room.ShutDoor();
        room.StandWindowPencilIfLying();
        room.Region.PrismEnabled = true;
        room.Player.Seated = false;
        // She gets up into the aisle, which leaves her desk mate the way out of his seat.
        room.Player.Position = new(room.DoorIn.X, room.Seat.Origin.Y, room.Seat.Origin.Z - 1);
        room.Player.DeviceBlocked += Blocked;
        room.Broom.Interacted += Sweep;
        room.Chalkboard.Interacted += Erase;
        for (var row = 0; row < room.RowBoxes.Count; row++)
        {
            var number = row;
            room.RowBoxes[row].Interacted += () => Upturn(number);
        }
        room.DoorLock.Interacted += Lock;

        script.Narrate("", "放学了。今天轮到我和同桌值日。", 3);
        script.Say(room.Deskmate, "小满，你帮我扫一下呗，我急着去踢球！", 3.5);
        script.Do(DeskmateRuns);
        script.Narrate("小满", "喂——！……算了。", 2.5);
        script.Narrate("", "扫地，擦黑板，把椅子倒扣上桌，关窗，锁门。", 4);
        script.Do(StartChores);
    }

    public override void _Process(double delta)
    {
        script.Tick(delta);
        if (!found && room.Prism.SeenEntity() == SchoolRoom.WindowPencil) Found();
        if (!shut && SchoolRoom.Windows.All(room.IsShut)) AllShut();
    }

    private void DeskmateRuns()
    {
        room.OpenDoor();
        var deskmate = room.Deskmate;
        SchoolRoom.Walk(deskmate, [new(room.DoorIn.X, 0, deskmate.Position.Z), room.DoorIn, room.DoorOut], true, arrived =>
        {
            // If he was stopped on the way he is still in the room.
            if (arrived) deskmate.Visible = false;
            room.ShutDoor(choresStarted);
        });
    }

    private void StartChores()
    {
        choresStarted = true;
        room.PutChairsOnFloor();
        SchoolRoom.Offer(room.Broom, true);
        SchoolRoom.Offer(room.Chalkboard, true);
        foreach (var row in room.RowBoxes) SchoolRoom.Offer(row, true);
        if (room.Door.Visible) room.ShutDoor(lockable: true);
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

    // One press turns the chairs of a whole row; the script puts them in place, since turning them over is not the lesson. A chair
    // with something on its desk where its seat would go stays on the floor, and the row is not done.
    private void Upturn(int row)
    {
        if (room.UpturnRow(row).Count > 0)
        {
            room.Player.Hud.Say("小满", "桌上有东西，这排的椅子有几把倒扣不上去。", 3.5);
            return;
        }
        upturned.Add(row);
        SchoolRoom.Offer(room.RowBoxes[row], false);
        if (upturned.Count < room.RowBoxes.Count) return;
        room.Player.Hud.Say("小满", "椅子都倒扣好了。教室空荡荡的，和上午镜片里那一页一模一样。", 4.5);
    }

    private void Lock()
    {
        var left = new List<string>();
        if (!swept) left.Add("扫地");
        if (!erased) left.Add("擦黑板");
        if (upturned.Count < room.RowBoxes.Count) left.Add("倒扣椅子");
        if (!SchoolRoom.Windows.All(room.IsShut)) left.Add("关窗");
        if (left.Count > 0)
        {
            room.Player.Hud.Say("小满", $"还没弄完：{string.Join("、", left)}。", 3);
            return;
        }
        room.ShutDoor();
        script.Narrate("", "咔哒。门锁好了。", 2.5);
        script.Do(() => EmitSignal(SignalName.Finished));
    }

    // A window she pushed has been stopped; by the pencil in the rail, which she cannot see, the first time.
    private void Blocked(string device, string obstacle)
    {
        if (stuck || obstacle != SchoolRoom.WindowPencil) return;
        stuck = true;
        room.Player.Hud.ShowNotice("窗轨里好像卡着什么。按住右键举起棱镜，看看窗轨。", 5);
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
        script.Narrate("", "最后一扇窗合上了。小满举起棱镜，瞥了一眼第Ⅱ页。", 3.5);
        script.Do(room.PutChairsOfSectionIIOnFloor);
        script.Narrate("小满", "那边的椅子……全都端端正正放回地上了，和这边正好相反。", 5);
        script.Do(() => room.Player.Hud.UnlockPage(GlowPage));
        script.Narrate("", "笔记本上多了一页：发光的东西。别的页也有它。", 4.5);
    }
}
