using System;
using Game.Characters;
using Game.Interaction;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 7, the evening at home: a new note and a hair clip, heating the meal, sharing the ham sausage with the cat,
/// the weather forecast, and the homework that ends with the last page of the notebook. The notebook can be opened
/// from here on.
/// Decision note: 2026-10-26-prologue-greybox
/// Decision note: 2026-10-29-home-scene-layout
/// </summary>
public partial class HomeEvening : Node
{
    private const double HeatSeconds = 2.5;
    private const double HomeworkSeconds = 8.5;
    // In the entrance hall just inside the front door, which is where scene 6 ends, facing into the flat with the door
    // behind her, as the compound's spawn faces.
    private static readonly Vector3 Arrival = new(.75f, HomeScene.Floor + .02f, 6.7f);
    // On the floor between the tea table and the television.
    private static readonly Vector3 Dinner = new(5.4f, HomeScene.Floor, 2.75f);
    // The cat stops this close to its spot, since she may be standing on it.
    private const float DinnerReach = .4f;
    private static readonly Color ScreenOn = new(.7f, .85f, 1f);

    private HomeScene home = null!;
    private Character3D catBody = null!;
    private Cat cat = null!;
    private bool homeworkStarted;

    [Signal] public delegate void FinishedEventHandler();

    /// <summary>For the debug entry: the pages of the notebook that the earlier scenes would have drawn.</summary>
    [Export] public bool WithEarlierPages { get; set; }

    /// <summary>Whether she comes in with the cat in her arms, as scene 6 ends. Otherwise it follows her, from beside her.</summary>
    public bool CarryingCat { get; set; }

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
            HomeScene.AddCat(region, Arrival + new Vector3(0, 0, -.6f));
        });
        home.Player.Position = Arrival;
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
        FollowPlayer();
        if (CarryingCat) catBody.GetNode<Interactable3D>("Pickup").Interact();
        if (WithEarlierPages)
            for (var page = 1; page <= 4; page++) home.Hud.UnlockPage(page);
        home.Hud.NotebookAvailable = true;
        On("Note", ReadNote);
        On("HairClip", TakeClip);
        On("Stove", Heat);
        On("Sausage", Share);
        On("Tv", Forecast);
        On("Desk", Homework);
        // She stays in for the evening, and HomeRoute leads nowhere outside the flat.
        home.Door("FrontDoor").Locked = () => true;
        On("HomeDoors/FrontDoor/Leaf", () => Say(2.5, "天黑了，不出去了。"));
        home.Hud.Say("", "爸爸已经去厂里上中班了。", 3);
        home.Hud.Say("", "煤球，小声点。按 N 翻看数学笔记本。", 3.5);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
            home.Hud.ShowNotice("棱镜在书包里，作业还没写呢。", 1.5);
    }

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
