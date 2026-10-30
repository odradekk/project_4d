using System;
using Game.Audio;
using Game.Interaction;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 1, the morning at home: the alarm, her father asleep in the living room, the note and the two yuan on the tea
/// table, brushing her teeth, the school bag, out of the front door and down the stairs. The prism stays in the pencil case
/// until school.
/// Decision note: 2026-10-26-prologue-greybox
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
public partial class HomeMorning : Node
{
    // She wakes beside her bed, facing it.
    private static readonly Vector3 Wake = new(5f, HomeScene.Floor + .02f, 4.8f);
    // Lower than the flat's floor by half a riser: only the flight down from the second storey takes her there.
    private const float Downstairs = HomeScene.Floor - .06f;

    private HomeScene home = null!;
    private SectionSound alarm = null!;
    private bool finished;

    [Signal] public delegate void FinishedEventHandler();

    public bool AlarmRinging { get; private set; } = true;
    public bool NoteWritten { get; private set; }
    public bool MoneyTaken { get; private set; }
    public bool TeethBrushed { get; private set; }
    public bool HasBackpack { get; private set; }

    /// <summary>What still keeps her from leaving, in her own words; null when she is ready. The note is her own to answer.</summary>
    public string? Missing =>
        AlarmRinging ? "闹钟还在响。"
        : !TeethBrushed ? "牙还没刷。"
        : !MoneyTaken ? "早饭钱还在桌上。"
        : !HasBackpack ? "书包还在凳子上。"
        : null;

    public override void _Ready()
    {
        home = HomeScene.Open(this);
        home.Player.Position = Wake;
        home.Player.Rotation = new(0, -Mathf.Pi / 2, 0);
        foreach (var name in new[] { "HairClip", "Sausage" }) home.Show(name, false);
        foreach (var name in new[] { "Stove", "Tv", "Desk" }) home.Enable(name, false);
        alarm = home.Loop("alarm", home.Prop("Clock").Position, 0, wanted: () => AlarmRinging);
        home.Loop("breathing", home.Prop("Father").Position, -8, .5f);
        On("Clock", TurnOffAlarm);
        On("Father", () => Say(3, "爸爸睡得很沉。他凌晨才下班回来。", "轻一点，别吵醒他。"));
        On("Note", WriteBack);
        On("Money", TakeMoney);
        On("Sink", BrushTeeth);
        On("Backpack", TakeBackpack);
        var door = home.Door("FrontDoor");
        door.Locked = () => Missing is not null;
        door.GetNode<Interactable3D>("Leaf").Interacted += () =>
        {
            if (Missing is { } reason) Say(2.5, reason);
            else if (door.State == Door3D.DoorState.Open) Say(2, "我走啦。");
        };
        home.Hud.Say("", "6:50。闹钟响了。", 3);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (finished || home.Player.GlobalPosition.Y > Downstairs) return;
        finished = true;
        EmitSignal(SignalName.Finished);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            home.Hud.ShowNotice("棱镜收在书包的文具盒里。", 1.5);
    }

    private void On(string prop, Action handler) => home.Prop(prop).Interacted += () => handler();

    private void Say(double seconds, params string[] lines)
    {
        foreach (var line in lines) home.Hud.Say("", line, seconds);
    }

    private void TurnOffAlarm()
    {
        AlarmRinging = false;
        alarm.Stop();
        home.Enable("Clock", false);
        Say(2, "起床了。");
    }

    private void WriteBack()
    {
        if (NoteWritten)
        {
            Say(2, "爸，我上学去了。");
            return;
        }
        NoteWritten = true;
        var paper = home.Prop("Note").GetNode<Node3D>("Paper");
        paper.Rotation = new(Mathf.Pi, 0, 0);
        paper.GetNode<Label3D>("Front").Visible = false;
        paper.GetNode<Label3D>("Back").Visible = true;
        Say(3, "满满：饭钱放桌上了，放学早点回来。——爸");
        Say(2, "翻过来，在背面写上：");
        Say(2.5, "爸，我上学去了。");
    }

    private void TakeMoney()
    {
        MoneyTaken = true;
        home.Show("Money", false);
        Say(3, "两块钱。买个包子，还能剩一点。");
    }

    private void BrushTeeth()
    {
        TeethBrushed = true;
        Say(3, "挤牙膏，刷牙。", "镜子里的我也在刷牙。左手变成了右手。");
    }

    private void TakeBackpack()
    {
        HasBackpack = true;
        home.Show("Backpack", false);
        Say(3.5, "文具盒里，是昨天在铁道边捡到的那个……");
    }
}
