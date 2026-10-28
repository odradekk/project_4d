using System;
using System.Collections.Generic;
using FourD;
using Game.Adapter;
using Game.Audio;
using Game.Characters;
using Game.Creatures;
using Game.Rendering;
using Godot;
using static Game.Flow.RoomQueries;

namespace Game.Flow;

/// <summary>
/// Scene 9, the deep room: she follows the cat through the mirror seam into section III and finds the room strange and
/// beautiful. Once she picks the cat up the seam she came by is cut and the grazer starts its way in along W. She can walk
/// about the whole time, and look at her own room through the lens. The grazer bites the extra door, the corner and the
/// window frame, and then comes for her; the cat tears loose, she cannot move and screams, and the screen cuts to black
/// with the title on it. The scene reads the engine through the generic queries and her own state; the grazer is only told
/// where to go (see <see cref="DeepGrazer"/>).
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class DeepRoom : Node
{
    public enum Stage { Following, Arrived, Panic, Grazing, Scream, Black }

    private const double ThinkSeconds = .1;
    // How long she is left alone before the scene tells her what to do next.
    private const double FollowHintSeconds = 40;
    private const double CarryHintSeconds = 30;
    private const double LensHintSeconds = 30;
    // The seam is found gone when she raises the lens at it, or after this long.
    private const double DiscoverSeconds = 14;
    private const double LookSeconds = 1.5;
    private const double CrySeconds = 50;
    private const string Xiaoman = "小满";

    private HomeScene home = null!;
    private RoomQueries ask = null!;
    private SceneScript script = null!;
    private Character3D catBody = null!;
    private Cat cat = null!;
    private PrismView prism = null!;
    private GrazerMusic music = null!;
    private GrazerKey chaseFrom = null!;
    private double stageClock, thought, lookedHome;
    private readonly int[] entries = new int[Enum.GetValues<Stage>().Length];
    private readonly HashSet<string> told = [];
    private bool discovered, sawHome, appeared, carried;

    [Signal] public delegate void FinishedEventHandler();

    /// <summary>Raised when the scene enters a stage, after the stage has begun.</summary>
    public event Action<Stage>? StageEntered;

    /// <summary>The room region as scene 8 leaves it. Left empty, the scene loads a fresh one and puts it in that state.</summary>
    public Region3D? Region { get; set; }

    public Stage Now { get; private set; }
    public Character3D CatBody => catBody;
    internal HomeScene Home => home;

    /// <summary>How many times the scene has entered a stage.</summary>
    public int Entries(Stage stage) => entries[(int)stage];

    public override void _Ready()
    {
        var fresh = Region is null;
        home = fresh ? HomeScene.Open(this, path: HomeScene.RoomPath) : HomeScene.Of(Region!);
        Region = home.Region;
        ask = new RoomQueries(home.Region);
        script = new(home.Hud);
        catBody = home.Region.Character("cat");
        cat = catBody.GetNode<Cat>("Cat");
        prism = home.Region.GetNode<PrismView>("Generated/Prism");
        cat.PickedUp += PickedUp;
        home.Hud.NotebookAvailable = true;
        MarkHomeInSectionI();
        music = new GrazerMusic { Name = "Music" };
        AddChild(music);
        music.Configure(home.Region, DeepGrazer.Id);
        flicker = new GrazerFlicker { Name = "Flicker" };
        AddChild(flicker);
        flicker.Configure(home.Region, DeepGrazer.Id);
        MakeSounds();
        if (fresh) AfterSceneEight();
        Become(Stage.Following);
    }

    public override void _PhysicsProcess(double delta)
    {
        stageClock += delta;
        thought += delta;
        script.Tick(delta);
        if (thought < ThinkSeconds) return;
        Think(thought);
        thought = 0;
    }

    public override void _Process(double delta) => LookAtGrazer(delta);

    // Everything that does not need looking at every frame.
    private void Think(double elapsed)
    {
        Hints();
        switch (Now)
        {
            case Stage.Following:
                if (home.Region.CurrentSection == SectionIII) Become(Stage.Arrived);
                break;
            case Stage.Arrived:
                if (carried) Become(Stage.Panic);
                break;
            case Stage.Panic:
                InPanic(elapsed);
                break;
            case Stage.Grazing:
                Grazing();
                break;
            case Stage.Scream:
                if (stageClock >= ScreamSeconds) Become(Stage.Black);
                break;
        }
    }

    private void Become(Stage next)
    {
        Now = next;
        stageClock = 0;
        entries[(int)next]++;
        switch (next)
        {
            case Stage.Following:
                if (home.Region.CurrentSection != SectionIII) Say("", 4, "煤球进了圆环前面那道缝。得去找它。");
                break;
            case Stage.Arrived:
                Say(Xiaoman, 2.5, "咦，反过来了。");
                Say("", 5, "还是我的房间，可又不是。屋子大了许多，墙上多出一扇门，哪儿也不通。");
                Say("", 4, "窗外的天光是粉金色的，干净得不像真的。");
                Say("", 4, "煤球坐在屋子中间，安安静静的。把它抱回去吧。");
                break;
            case Stage.Panic:
                StartPanic();
                break;
            case Stage.Grazing:
                Say("", 4, "它朝这边滑过来了，很慢。", now: true);
                break;
            case Stage.Scream:
                StartScream();
                break;
            case Stage.Black:
                CutToBlack();
                break;
        }
        StageEntered?.Invoke(next);
    }

    // Picking the cat up is the turn of the scene: the grazer starts, and the seam she came by goes with its first bite. The scene
    // takes it up at its next look, so that a cat picked up in the moment she arrives is not missed.
    private void PickedUp() => carried = true;

    private void StartPanic()
    {
        var approach = DeepGrazer.Approach();
        chaseFrom = DeepGrazer.End(approach);
        home.Region.Grazers!.Play(DeepGrazer.Id, approach);
        script.Wait(1);
        script.Narrate(Xiaoman, "好了，回家吧。", 2);
        script.Narrate(Xiaoman, "咦……缝呢？", 2.5);
        script.Narrate("", "来时的那道缝不见了。举起棱镜，对着刚才的地方看看。", 5);
    }

    // The seam is gone, the sounds of her own body come up, and the rest is what she finds by looking.
    private void InPanic(double elapsed)
    {
        if (!discovered && (prism.Raised && prism.TargetSection == SectionII && ask.IsSevered(DeepGrazer.Seam) || stageClock > DiscoverSeconds))
        {
            discovered = true;
            Say("", 4, "半空中只剩一截线头，切得平平的，像是刀切的。");
            Say(Xiaoman, 3, "回不去了……");
            StartBody();
        }
        lookedHome = prism.Raised && prism.TargetSection == SectionI ? lookedHome + elapsed : 0;
        if (!sawHome && (lookedHome >= LookSeconds || stageClock > CrySeconds)) SeenHome();
        Tell("rhythm", music.Levels.Rhythm > 0, "远处传来有节奏的刮擦声，一下一下，越来越近。");
        Tell("nothing", stageClock > 25 && discovered, "别的缝呢？一道也没有……", Xiaoman);
        if (!appeared && ask.Bounds(DeepGrazer.Id, SectionIII) is not null)
        {
            appeared = true;
            Say("", 5, "半空中冒出几块互不相连的东西，慢慢胀大，连成了一块。", now: true);
        }
        Ate("door", DeepGrazer.Door, "那扇多出来的门不见了。断面平得像刀切的。");
        Ate("corner", DeepGrazer.Corner, "墙角也被它吃掉了。");
        Ate("frame", DeepGrazer.Frame, "窗框没了，粉色的天光直接照了进来。");
        if (!home.Region.Grazers!.IsPlaying(DeepGrazer.Id)) Become(Stage.Grazing);
    }

    private void SeenHome()
    {
        sawHome = true;
        Say(Xiaoman, 3, lookedHome < LookSeconds ? "爸爸……" : "那是我的房间……台灯还亮着。");
        Say("", 4, "笔记本摊在桌上，闹钟指着九点四十。");
        Say(Xiaoman, 2.5, "爸爸！");
        Say(Xiaoman, 3, "爸爸——！");
        Say("", 4, "爸爸在厂里上中班，家里没有人。");
    }

    // A thing the grazer has eaten, said once when the engine no longer has it.
    private void Ate(string key, string element, string line) => Tell(key, !ask.Exists(element), line, now: true);

    private void Hints()
    {
        Tell("follow", Now == Stage.Following && stageClock > FollowHintSeconds,
            home.Region.CurrentSection == SectionII ? "举着棱镜，滚轮切到第Ⅲ页，走到缝跟前按左键。" : "那道缝在第Ⅱ页。先从墙角那道缝过去。");
        Tell("carry", Now == Stage.Arrived && stageClock > CarryHintSeconds, "走到煤球跟前，按 E 抱起它。");
        Tell("lens", Now == Stage.Panic && discovered && !sawHome && stageClock > LensHintSeconds, "举起棱镜，滚轮切到第Ⅰ页，看看家里。");
    }

    private void Tell(string key, bool when, string line, string speaker = "", bool now = false)
    {
        if (when && told.Add(key)) Say(speaker, 4, line, now);
    }

    // A line about the grazer does not wait behind the lines that are queued: it is said when it happens.
    private void Say(string speaker, double seconds, string line, bool now = false)
    {
        if (now) home.Hud.ClearText();
        home.Hud.Say(speaker, line, seconds);
    }

    // Her room in section I as it is at 9:40: the lamp is lit, the notebook lies open, and the clock says so. In the white
    // box they are labels, and the lens shows them.
    private void MarkHomeInSectionI()
    {
        var props = home.Region.Props(HomeScene.Section)!;
        foreach (var (text, at) in new[] { ("台灯亮着", new Vector3(7.34f, 1.4f, 2.92f)), ("笔记本摊开着", new Vector3(7.2f, 1f, 3.47f)), ("闹钟 9:40", new Vector3(4.22f, .85f, 2.72f)) })
            props.AddChild(new Label3D
            {
                Text = text,
                Position = at,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Shaded = false,
                NoDepthTest = true,
                PixelSize = .004f,
                FontSize = 48,
                OutlineSize = 12,
                Modulate = new(1f, .95f, .6f),
                // Layer 2 is what the lens draws.
                Layers = 3
            });
    }
}
