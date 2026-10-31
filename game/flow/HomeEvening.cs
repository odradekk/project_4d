using System;
using Game.Characters;
using Game.Interaction;
using Game.Regions;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 7, the evening: she rides the bus home to the stop outside the gate and gets off with E on its door; the cat
/// waits at the foot of her building's stair and follows her up once she comes near, and she whispers to it; the front door
/// shuts and stays locked once both are in. At home: a new note and a hair clip, heating the meal, sharing the ham sausage
/// with the cat, the weather forecast, and the homework that ends with the last page of the notebook. The notebook can be
/// opened from here on.
/// Decision note: 2026-10-26-prologue-greybox
/// Decision note: 2026-10-29-home-scene-layout
/// Decision note: 2026-10-31-bus-commute
/// </summary>
public partial class HomeEvening : Node
{
    private const double HeatSeconds = 2.5;
    private const double HomeworkSeconds = 8.5;
    // On the sidewalk in front of the bus stop, by the bus's middle door. She stands there unseen while she rides, and gets
    // off there facing south to the gate.
    private static readonly Vector3 Stop = new(83.4f, -.08f, 69.7f);
    // The cat sits at the mouth of the ground-floor stair hall, facing out to the west, and follows her once she comes
    // within CatReach of it on its floor; put down on the way home, it follows again in the same way. It follows her only
    // as far as HomeRoute leads: when she goes beyond the forecourt, it goes back to its place and waits there again.
    private static readonly Vector3 CatWaits = new(-1f, .02f, 18.5f), Outside = new(-5f, 0, 18.5f);
    private const float CatReach = 3;
    // The front door swings shut behind her once she and the cat are in and this far from its hinge.
    private const float SwingClear = 1.4f;
    // Off the bus she watches it shut its doors and pull away this long before she can move.
    private const double PullAwaySeconds = 3;
    // On the floor between the tea table and the television.
    private static readonly Vector3 Dinner = new(5.4f, HomeScene.Floor, 2.75f);
    // The cat stops this close to its spot, since she may be standing on it.
    private const float DinnerReach = .4f;
    private static readonly Color ScreenOn = new(.7f, .85f, 1f);

    private HomeScene home = null!;
    private Character3D catBody = null!;
    private Cat cat = null!;
    private Bus3D bus = null!;
    private Door3D frontDoor = null!;
    private bool catCalled, catGoingBack, homeworkStarted;

    [Signal] public delegate void FinishedEventHandler();

    /// <summary>For the debug entry: the pages of the notebook that the earlier scenes would have drawn.</summary>
    [Export] public bool WithEarlierPages { get; set; }

    /// <summary>Whether she and the cat are in the flat for the night, with the front door locked behind them.</summary>
    public bool Indoors { get; private set; }

    public bool NoteRead { get; private set; }
    public bool ClipTaken { get; private set; }
    public bool Heated { get; private set; }
    public bool CatFed { get; private set; }
    public bool ForecastHeard { get; private set; }
    public bool HomeworkDone { get; private set; }

    public Character3D CatBody => catBody;

    public override void _Ready()
    {
        home = HomeScene.Open(this, region =>
        {
            // Her father has gone to work, and the living-room door is open: ajar, it would stand in the cat's way.
            region.GetNode<Door3D>("Sections/Ⅰ/Props/HomeDoors/LivingRoomDoor").State = Door3D.DoorState.Open;
            HomeScene.AddCat(region, CatWaits);
        });
        var player = home.Player;
        player.Position = Stop;
        foreach (var name in new[] { "Father", "Money", "Backpack", "Sink" }) home.Show(name, false);
        home.Enable("Clock", false);
        home.Prop("Tv").GetNode<MeshInstance3D>("Screen").MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = ScreenOn,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        home.Prop("Note").GetNode<Label3D>("Paper/Front").Text = "饭在锅里，热一下再吃。\n作业写完早点睡。——爸";
        catBody = home.Region.Character("cat");
        cat = catBody.GetNode<Cat>("Cat");
        cat.Sit(Outside);
        if (WithEarlierPages)
            for (var page = 1; page <= 4; page++) home.Hud.UnlockPage(page);
        home.Hud.NotebookAvailable = true;
        On("Note", ReadNote);
        On("HairClip", TakeClip);
        On("Stove", Heat);
        On("Sausage", Share);
        On("Tv", Forecast);
        On("Desk", Homework);
        // Once she is in, she stays in for the evening.
        frontDoor = home.Door("FrontDoor");
        frontDoor.Locked = () => Indoors;
        On("HomeDoors/FrontDoor/Leaf", () =>
        {
            if (Indoors) Say(2.5, "天黑了，不出去了。");
        });
        // She rides as a passenger who only watches, until the bus has stopped with its doors open.
        bus = home.Prop<Bus3D>("Bus");
        player.Watching = true;
        SetProcessUnhandledInput(false);
        bus.Seat(player.Camera);
        bus.Arrived += () =>
        {
            player.Watching = false;
            player.Seated = true;
        };
        bus.GettingOff += GetOff;
        bus.Arrive();
        home.Hud.Say("", "放学了。公交车开回小区门口。", 3);
    }

    public override void _PhysicsProcess(double delta)
    {
        var feet = home.Player.Position;
        var at = catBody.Position;
        var near = Flat(feet - at) < CatReach && Mathf.Abs(feet.Y - at.Y) < 1;
        // Beyond the way it knows, she is out of the cat's reach however near.
        var away = !HomeRoute.Covers(feet) && HomeRoute.Covers(at);
        if (Indoors || cat.Held) { }
        else if (away)
        {
            if (catCalled && !catGoingBack) GoBack();
        }
        else if (near && (!catBody.Busy || catGoingBack))
        {
            catGoingBack = false;
            FollowPlayer();
            if (!catCalled) home.Hud.Say("小满", "爸不让养猫，你可别出声啊。", 3.5);
            catCalled = true;
        }
        if (!Indoors && HomeRoute.InFlat(feet) && HomeRoute.InFlat(at) && Flat(feet - frontDoor.Position) > SwingClear && Flat(at - frontDoor.Position) > SwingClear)
        {
            Indoors = true;
            frontDoor.State = Door3D.DoorState.Shut;
            home.Hud.Say("", "爸爸已经去厂里上中班了。", 3);
            home.Hud.Say("", "煤球，小声点。按 N 翻看数学笔记本。", 3.5);
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            home.Hud.ShowNotice("棱镜在书包里，作业还没写呢。", 1.5);
    }

    // Off the bus onto the sidewalk where she stands, facing the gate; she only watches while the bus shuts its doors and
    // pulls away.
    private void GetOff()
    {
        var player = home.Player;
        bus.Unseat();
        player.Seated = false;
        player.Watching = true;
        player.Rotation = new(0, Mathf.Pi, 0);
        bus.Depart();
        // A tween, not a timer, so that it goes with the scene if the scene is started again meanwhile.
        var pullAway = CreateTween();
        pullAway.TweenInterval(PullAwaySeconds);
        pullAway.TweenCallback(Callable.From(() =>
        {
            player.Watching = false;
            SetProcessUnhandledInput(true);
        }));
    }

    // Back to the stair hall's mouth, where it sits and waits for her again.
    private void GoBack()
    {
        catGoingBack = true;
        catBody.Do(_ =>
        {
            var next = HomeRoute.Next(catBody.Position, CatWaits);
            if (!catBody.WalkTo(next, catBody.WalkSpeed) || next != CatWaits) return false;
            catGoingBack = false;
            cat.Sit(Outside);
            return true;
        });
    }

    private static float Flat(Vector3 offset) => new Vector2(offset.X, offset.Z).Length();

    private void FollowPlayer() => cat.Follow(player => HomeRoute.Next(catBody.Position, player));

    private void On(string prop, Action handler) => home.Prop(prop).Interacted += () => handler();

    private void Say(double seconds, params string[] lines)
    {
        foreach (var line in lines) home.Hud.Say("", line, seconds);
    }

    private void ReadNote()
    {
        NoteRead = true;
        Say(4, "饭在锅里，热一下再吃。作业写完早点睡。——爸");
    }

    private void TakeClip()
    {
        ClipTaken = true;
        home.Show("HairClip", false);
        Say(3, "新发卡。爸爸顺路买的吧。");
    }

    private void Heat()
    {
        if (Heated)
        {
            Say(2, "饭热好了。");
            return;
        }
        home.Enable("Stove", false);
        home.Prop("Stove").GetNode<Node3D>("Flame").Visible = true;
        Say(2, "点火。咔嗒。");
        GetTree().CreateTimer(HeatSeconds).Timeout += () =>
        {
            Heated = true;
            home.Enable("Stove", true);
            home.Prop("Stove").GetNode<Node3D>("Flame").Visible = false;
            home.Prop("Stove").GetNode<Node3D>("Steam").Visible = true;
            Say(2.5, "饭热好了。");
        };
    }

    private void Share()
    {
        Say(2.5, "一半给你，一半给我。");
        catBody.Do(_ =>
        {
            var next = HomeRoute.Next(catBody.Position, Dinner);
            if (next == Dinner && catBody.Position.DistanceTo(Dinner) < DinnerReach) return true;
            catBody.WalkTo(next, catBody.WalkSpeed);
            return false;
        });
        // The sausage stays until the cat has eaten: picking the cat up on its way takes the walk away from it, and she
        // sends it again.
        void Arrived()
        {
            Forget();
            home.Show("Sausage", false);
            CatFed = true;
            cat.Sit(Dinner + new Vector3(0, 0, -1));
            Say(2.5, "煤球吃得好快。");
            GetTree().CreateTimer(3).Timeout += () =>
            {
                if (!cat.Held) FollowPlayer();
            };
        }
        void Forget()
        {
            catBody.ActionFinished -= Arrived;
            catBody.ActionReplaced -= Forget;
        }
        catBody.ActionFinished += Arrived;
        catBody.ActionReplaced += Forget;
    }

    private void Forecast()
    {
        ForecastHeard = true;
        home.Hud.Say("电视", "明天白天，晴转多云，偏南风二到三级。", 3.5);
        home.Hud.Say("电视", "最高气温十八度，最低气温九度。", 3.5);
    }

    private void Homework()
    {
        if (!Heated || !CatFed)
        {
            Say(2.5, !Heated ? "先把饭热上吧。" : "煤球还没吃呢。");
            return;
        }
        if (homeworkStarted) return;
        homeworkStarted = true;
        Say(2.5, "摊开作业本，开始写。");
        Say(3, "老师那句话还在脑子里：体动了，会变成什么？");
        Say(3, "翻到笔记本最后一页，用很大的字写下：体动成什么？");
        GetTree().CreateTimer(HomeworkSeconds).Timeout += () =>
        {
            home.Hud.UnlockPage(8);
            HomeworkDone = true;
            EmitSignal(SignalName.Finished);
        };
    }
}
