using System;
using Game.Audio;
using Game.Characters;
using Game.Interaction;
using Game.Regions;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 1, the morning at home: the alarm, her father asleep in the living room, the note and the two yuan on the tea
/// table, brushing her teeth, the school bag, out of the front door and down the stairs, where the cat stares at the empty
/// corner under the ground-floor stair and she kneels to ask it what it sees; out of the compound to the bus stop, where the
/// bus comes once she is near and waits for her. E on its door seats her on the bus, which drives off; the screen fades and
/// <see cref="Finished"/> follows. The prism stays in the pencil case until school.
/// Decision note: 2026-10-26-prologue-greybox
/// Decision note: 2026-10-29-home-scene-layout
/// Decision note: 2026-10-31-bus-commute
/// </summary>
public partial class HomeMorning : Node
{
    // She wakes beside her bed, facing it.
    private static readonly Vector3 Wake = new(5f, HomeScene.Floor + .02f, 4.8f);
    // The cat sits under the ground-floor stair, west of the wall at x 5, facing the corner between that wall and the
    // stair hall's south wall. She meets it within CatReach of it on the ground floor, which she cannot help as she comes
    // off the flight down, and not from the floor above.
    private static readonly Vector3 CatSpot = new(2.6f, .02f, 19.8f), Corner = new(4.9f, .2f, 20.6f);
    private const float CatReach = 3, GroundFloor = .5f;
    // How far her eyes drop when she kneels.
    private const float Kneel = .55f;
    private const double KneelSeconds = .45;
    // The middle of the bus shelter on the sidewalk; the bus is called when she comes within CallDistance of it.
    private static readonly Vector3 BusStop = new(81.8f, 0, 71);
    private const float CallDistance = 15;
    private const double RideSeconds = 5, FadeSeconds = 1;

    private HomeScene home = null!;
    private SectionSound alarm = null!;
    private Character3D cat = null!;
    private Bus3D bus = null!;
    private bool catMet, busCalled;

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
        home = HomeScene.Open(this, region => HomeScene.AddCat(region, CatSpot));
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
        cat = home.Region.Character("cat");
        var staring = cat.GetNode<Cat>("Cat");
        // The cat is only looked at this morning, not carried.
        staring.Holdable = false;
        staring.Sit(Corner);
        bus = home.Prop<Bus3D>("Bus");
        bus.Boarding += Board;
        home.Hud.Say("", "6:50。闹钟响了。", 3);
    }

    public override void _PhysicsProcess(double delta)
    {
        var feet = home.Player.GlobalPosition;
        if (!catMet && feet.Y < GroundFloor && Flat(feet - cat.GlobalPosition) < CatReach)
        {
            catMet = true;
            MeetCat();
        }
        if (!busCalled && Flat(feet - BusStop) < CallDistance)
        {
            busCalled = true;
            bus.Arrive();
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            home.Hud.ShowNotice("棱镜收在书包的文具盒里。", 1.5);
    }

    private static float Flat(Vector3 offset) => new Vector2(offset.X, offset.Z).Length();

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

    // She kneels beside the cat and asks it what it is staring at in the empty corner.
    private void MeetCat()
    {
        var eyes = home.Player.Camera;
        var standing = eyes.Position.Y;
        var beat = CreateTween();
        beat.TweenProperty(eyes, "position:y", standing - Kneel, KneelSeconds);
        beat.TweenCallback(Callable.From(() => Say(2.5, "你看什么呢？")));
        beat.TweenInterval(3);
        beat.TweenCallback(Callable.From(() => Say(3, "那里明明什么也没有……")));
        beat.TweenInterval(2);
        beat.TweenProperty(eyes, "position:y", standing, KneelSeconds);
        beat.TweenCallback(Callable.From(() => Say(2.5, "算了，要迟到了。")));
    }

    // On the bus she only watches: her keys and the mouse are off while the doors close and the bus moves off, and the
    // screen fades under the subtitles.
    private void Board()
    {
        var player = home.Player;
        player.Seated = true;
        player.SetProcessUnhandledInput(false);
        bus.Seat(player.Camera);
        bus.Depart();
        var layer = new CanvasLayer();
        var black = new ColorRect { Color = new(0, 0, 0, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(black);
        AddChild(layer);
        black.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var ride = CreateTween();
        ride.TweenInterval(RideSeconds);
        ride.TweenProperty(black, "color:a", 1f, FadeSeconds);
        ride.TweenCallback(Callable.From(() => EmitSignal(SignalName.Finished)));
    }
}
