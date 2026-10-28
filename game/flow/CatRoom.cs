using System;
using System.Collections.Generic;
using System.Linq;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Interaction;
using Godot;
using static Game.Flow.RoomQueries;

namespace Game.Flow;

/// <summary>
/// Scene 8, the room at 21:00: the cat stares at the corner and leaps into a seam; she leads it down from the cabinet in
/// section II with the wand, the cabinet, the ball and the nut, and the last leap through the ring takes it into section III.
/// The cat is told what to do by its stage, and every stage that waits for the cat to finish is told again if the player
/// picks the cat up in the middle of it. What the stages wait for is read from the engine; see <see cref="RoomQueries"/>.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class CatRoom : Node
{
    public enum Stage { Watching, Entering, Perched, Descending, AtPit, Crossing, Chasing, Leaping, Gone }

    private const string RoomPath = "res://regions/room.tscn";
    // Marks on the cabinet, the pit and the room of section III, for the cat's route; the seams are read from the region.
    private static readonly Vector3 Perch = new(3.85f, 2.4f, 5.78f);
    private static readonly Vector3 Deep = new(4f, 0, 5.9f);
    private const float PitFarZ = 3.7f;
    // Where along the board, as a fraction from the hinge to the foot, the cat comes onto it and waits; and how far above the
    // slice's line it stands.
    private const float BoardEntry = .1f;
    private const float BoardFoot = .95f;
    private const float BoardSurface = .03f;
    private const float SlideSpeed = 1.2f;
    private const double WatchSeconds = 40;
    private const double LookSeconds = 1;
    private const float LookAngle = .7f;
    // The ring is tried this often, and the rest of the scene thinks this often: each look at the engine cuts the whole world.
    private const double RingAttemptSeconds = 1;
    private const double ThinkSeconds = .1;
    private const float FallLimit = -3;
    private const double SafeSeconds = .5;
    private const float LureReach = 3;
    private const float RingLureReach = 1.5f;
    private const float RingCatReach = 3.5f;
    private const float YarnTravelForNote = 1.5f;
    private const int LeapsBeforeAdvice = 2;
    private const double GuardHintSeconds = 75;
    private const double RingHintSeconds = 60;

    private HomeScene home = null!;
    private RoomQueries ask = null!;
    private Character3D catBody = null!;
    private Cat cat = null!;
    private Vector3 seam1;
    private Action? then;
    private double clock, lookedAtSeam, stageClock, ringTimer, thought, lureClock;
    private float yarnTravel;
    private Vector3? yarn, lastYarn;
    private readonly SafeSpot playerSafe = new(), catSafe = new();
    private readonly int[] entries = new int[Enum.GetValues<Stage>().Length];
    private readonly HashSet<string> told = [];
    private bool bridged, ringSeen, boardSeen, wandFlipped;
    private int leaps;

    [Signal] public delegate void FinishedEventHandler();

    /// <summary>Raised when the cat enters a stage, after the stage has given it its command.</summary>
    public event Action<Stage>? StageEntered;

    /// <summary>For the debug entry: the pages of the notebook that the earlier scenes would have drawn.</summary>
    [Export] public bool WithEarlierPages { get; set; }

    public Stage Now { get; private set; }
    public Character3D CatBody => catBody;
    internal HomeScene Home => home;

    /// <summary>How many times the cat has entered a stage, which a short stage leaves behind for whoever was not looking at that moment.</summary>
    public int Entries(Stage stage) => entries[(int)stage];

    public override void _Ready()
    {
        home = HomeScene.Open(this, path: RoomPath);
        ask = new RoomQueries(home.Region);
        seam1 = SeamAt("S1");
        // This is the room at night: nothing of the flat's day is to be used. The hoop on the wall is for looking at.
        foreach (var prop in home.Region.Props(HomeScene.Section)!.GetChildren().OfType<Interactable3D>().Where(prop => prop.Name != "Hoop"))
            home.Enable(prop.Name, false);
        home.Prop("Hoop").Interacted += () => Say("", 4, "呼啦圈。煤球最爱钻这个，可它太大了，带不过缝。");
        if (WithEarlierPages)
            foreach (var page in new[] { 1, 2, 3, 4, 8 }) home.Hud.UnlockPage(page);
        home.Hud.NotebookAvailable = true;
        catBody = home.Region.Character("cat");
        cat = catBody.GetNode<Cat>("Cat");
        catBody.ActionFinished += () =>
        {
            var next = then;
            then = null;
            next?.Invoke();
        };
        catBody.ActionReplaced += () => then = null;
        playerSafe.Set(home.Player.Position, clock);
        catSafe.Set(catBody.Position, clock);
        Say("", 3.5, "晚上九点。煤球又弓着背盯住衣柜和书桌之间的墙角，尾巴一甩一甩。");
        Say("", 3.5, "那边什么也没有啊。举起棱镜看看？（按住右键）");
    }

    public override void _PhysicsProcess(double delta)
    {
        clock += delta;
        stageClock += delta;
        thought += delta;
        Recover();
        if (thought >= ThinkSeconds)
        {
            Think(thought);
            thought = 0;
        }
        if (cat.Held) return;
        if (Now == Stage.Leaping) Cross();
        // A stage whose command ended without the stage ending, or was dropped when the cat was picked up, is given again.
        if (!catBody.Busy) Give(Now);
    }

    // Everything that does not need looking at every frame.
    private void Think(double elapsed)
    {
        yarn = ask.Bounds("wand", SectionII, "yarn") is { } box ? box.GetCenter() : null;
        if (Now is Stage.Chasing or Stage.Leaping) lureClock += elapsed;
        Notes();
        Hints();
        if (!cat.Held) Advance(elapsed);
    }

    private void Advance(double elapsed)
    {
        switch (Now)
        {
            case Stage.Watching:
                lookedAtSeam = SeesSeam() ? lookedAtSeam + elapsed : 0;
                if (lookedAtSeam >= LookSeconds || clock >= WatchSeconds) Become(Stage.Entering);
                break;
            case Stage.Perched:
                if (BoardIsDown && yarn is { } lure && Flat(lure - Board(BoardFoot)).Length() < LureReach) Become(Stage.Descending);
                break;
            case Stage.AtPit:
                if (BallOverPit(out _)) Become(Stage.Crossing);
                break;
            case Stage.Chasing:
                ringTimer -= elapsed;
                if (ringTimer <= 0)
                {
                    ringTimer = RingAttemptSeconds;
                    if (RingIsLuring() && cat.JumpThroughRing("nut", "ring", "wand")) Become(Stage.Leaping, give: false);
                }
                break;
        }
    }

    // The leap is over the seam for a few frames only, so every frame the cat is in the air it tries the seam; the engine says
    // whether it is close enough.
    private void Cross()
    {
        if (catBody.Position.Y > .05f && catBody.CrossSeam("s2", SectionIII) is null) Vanish();
    }

    private void Become(Stage next, bool give = true)
    {
        Now = next;
        stageClock = 0;
        entries[(int)next]++;
        if (give) Give(next);
        StageEntered?.Invoke(next);
    }

    // What the cat does in a stage. A command that runs until it ends continues with the next stage when it does.
    private void Give(Stage stage)
    {
        switch (stage)
        {
            case Stage.Watching:
                cat.Sit(seam1 + Vector3.Up * .5f);
                cat.Stare(seam1 + Vector3.Up * .5f);
                break;
            case Stage.Entering:
                Run(() => cat.JumpTo(seam1 + Vector3.Up * .3f), EnterSectionII);
                break;
            case Stage.Perched:
                cat.Stare(() => yarn);
                break;
            case Stage.Descending:
                cat.Holdable = true;
                Run(() => cat.JumpTo(Board(BoardEntry)), () => Run(() => Slide(Board(BoardFoot)), () => Become(Stage.AtPit)));
                break;
            case Stage.AtPit:
                catBody.Posture = Posture.Sitting;
                catBody.Do(_ =>
                {
                    var foot = Board(BoardFoot);
                    catBody.Warp(foot);
                    catBody.Face(foot + Vector3.Forward);
                    return false;
                });
                break;
            case Stage.Crossing:
                if (!BallOverPit(out var top)) Become(Stage.AtPit);
                else Run(() => cat.JumpTo(top), () => Run(() => cat.JumpTo(new(top.X, 0, PitFarZ)), Bridged));
                break;
            case Stage.Chasing:
                catBody.Posture = Posture.Standing;
                cat.Chase(() => yarn);
                break;
            case Stage.Leaping:
                // The leap ended without the seam being reached, or was dropped: back to the lure.
                leaps++;
                Become(Stage.Chasing);
                break;
            case Stage.Gone:
                cat.Sit(Deep + Vector3.Right);
                cat.Stare(Deep + Vector3.Right);
                break;
        }
    }

    private void Run(Action command, Action next)
    {
        command();
        then = next;
    }

    private void EnterSectionII()
    {
        // The seam may be filled; the cat leaps into it again.
        if (catBody.CrossSeam("s1", SectionII) is not null)
        {
            Give(Stage.Entering);
            return;
        }
        catBody.PlaceAt(Perch, 0);
        catBody.Posture = Posture.Lying;
        catSafe.Set(Perch, clock);
        cat.Holdable = false;
        Say("小满", 2.5, "煤球？！");
        Say("", 4, "它跳进那道光里去了。透过镜片看，第Ⅱ页的吊柜顶上缩着一团黑影。");
        Say("", 4, "要过去看看吗？举着棱镜走到缝前，按左键。");
        Become(Stage.Perched);
    }

    private void Bridged()
    {
        bridged = true;
        catSafe.Set(catBody.Position, clock);
        Say("", 3, "煤球踩着皮球过去了。");
        Become(Stage.Chasing);
    }

    private void Vanish()
    {
        catBody.PlaceAt(Deep, 0);
        Say("小满", 2.5, "煤球？！");
        Say("", 4, "它在半空里消失了。圆环前面，还藏着一道从这一页看不见的缝。");
        GetTree().CreateTimer(5).Timeout += () => EmitSignal(SignalName.Finished);
        Become(Stage.Gone);
    }

    // The cat's slide down the board is a walk with no floor under it: the board is steeper than the cat can stand on.
    private void Slide(Vector3 end) => catBody.Do(delta =>
    {
        var next = catBody.Position.MoveToward(end, (float)delta * SlideSpeed);
        catBody.Face(end);
        catBody.Warp(next);
        return next.DistanceTo(end) < .001f;
    });

    private Vector3 SeamAt(string node) => SceneReader.RelativeTransform(home.Region.GetNode<Seam3D>($"Seams/{node}"), home.Region).Origin;

    // A point on the board of section II, a fraction of the way from its hinge to its foot. The slice of the board runs from its
    // highest corner at the hinge to its lowest at the foot, so the middle of its line is read from the bounds.
    private Vector3 Board(float fraction)
    {
        if (ask.Bounds("cabinet", SectionII, "plank") is not { } board) return catBody.Position;
        var middle = board.GetCenter().X;
        var hinge = new Vector3(middle, board.End.Y, board.End.Z);
        var foot = new Vector3(middle, board.Position.Y, board.Position.Z);
        return hinge.Lerp(foot, fraction) + Vector3.Up * BoardSurface;
    }

    private bool BoardIsDown => ask.DevicePosition("cabinet") <= -Math.PI / 2 + 1e-9;

    private bool SeesSeam()
    {
        if (!home.Player.PrismRaised || home.Region.CurrentSection != SectionI) return false;
        var eyes = home.Player.Camera;
        return (-eyes.GlobalBasis.Z).AngleTo(seam1 + Vector3.Up * .5f - eyes.GlobalPosition) < LookAngle;
    }

    // The ball lies still and holds itself up over empty space in section II: nothing is under the middle of its slice there.
    // The pit is the only hole in that floor, so the engine's answer is all that is asked.
    private bool BallOverPit(out Vector3 top)
    {
        top = default;
        if (ask.Bounds("ball", SectionII) is not { } slice || !ask.IsResting("ball") || !ask.OverEmptySpace(slice)) return false;
        top = new(slice.GetCenter().X, slice.End.Y, slice.GetCenter().Z);
        return true;
    }

    private bool RingIsLuring() =>
        yarn is { } lure && ask.Bounds("nut", SectionII, "ring") is { } ring
        && Flat(lure - ring.GetCenter()).Length() < RingLureReach && Flat(catBody.Position - ring.GetCenter()).Length() < RingCatReach;

    // The notebook pages that this scene draws, and the lines for what has just happened to the board.
    private void Notes()
    {
        if (home.Region.CurrentSection != SectionI) Unlock(5, "她穿过了缝。笔记本里多了一页：缝。");
        if (yarn is { } now)
        {
            if (lastYarn is { } last) yarnTravel += last.DistanceTo(now);
            if (yarnTravel >= YarnTravelForNote) Unlock(6, "这一页晃，那一页的毛线球也跟着晃。笔记本里多了一页：隔着一页。");
        }
        lastYarn = yarn;
        ringSeen |= ask.Bounds("nut", SectionII, "ring") is not null;
        wandFlipped |= ask.State("wand").Handedness == Handedness.Mirrored;
        if (bridged && ringSeen) Unlock(7, "皮球和圆环都试过了。笔记本里多了一页：三种东西。");
        if (!boardSeen && BoardIsDown)
        {
            boardSeen = true;
            var watching = home.Player.PrismRaised && home.Region.CurrentSection == SectionI;
            Say("", 3.5, watching ? "那边的木板放下来了！像个滑梯。" : "……那边好像有什么东西倒了。");
        }
    }

    private void Unlock(int page, string line)
    {
        if (home.Hud.UnlockedPages.Contains(page)) return;
        home.Hud.UnlockPage(page);
        Say("", 4, line);
    }

    // Falling out of the room takes her back to where she stood half a second before she left the floor, and the cat likewise.
    private void Recover()
    {
        var player = home.Player;
        if (player.IsOnFloor()) playerSafe.Stand(player.Position, clock);
        else if (player.Position.Y < FallLimit)
        {
            player.Position = playerSafe.Spot;
            player.Velocity = Vector3.Zero;
            Say("小满", 2.5, "差点掉下去！");
        }
        if (catBody.IsOnFloor()) catSafe.Stand(catBody.Position, clock);
        else if (catBody.Position.Y < FallLimit) catBody.PlaceAt(catSafe.Spot, catBody.Rotation.Y);
    }

    // The places something stood on the floor in the last half second; the oldest is far enough back from an edge it fell over.
    private sealed class SafeSpot
    {
        private readonly Queue<(double Time, Vector3 Position)> trail = new();
        internal Vector3 Spot => trail.Peek().Position;

        internal void Set(Vector3 position, double time)
        {
            trail.Clear();
            trail.Enqueue((time, position));
        }

        internal void Stand(Vector3 position, double time)
        {
            trail.Enqueue((time, position));
            while (trail.Count > 1 && time - trail.Peek().Time > SafeSeconds) trail.Dequeue();
        }
    }

    private void Hints()
    {
        Tell("guard", clock > GuardHintSeconds && !wandFlipped, "逗猫棒在窗台的防护栏后面。手伸不进去，就翻翻笔记本里“翻面”那一页。");
        Tell("high", Now == Stage.Perched && stageClock > 60 && !BoardIsDown, "吊柜太高了，够不着。得踩着什么才行。");
        Tell("sheet", Now == Stage.AtPit && ask.Bounds("stool", SectionII) is { } sheet && ask.OverEmptySpace(sheet), "薄得像张纸，煤球不敢踩。", "小满");
        Tell("pit", Now == Stage.AtPit && stageClock > 45, "要是洞中间有个能踩的地方……");
        Tell("ring", lureClock > RingHintSeconds && !ringSeen, "那一页有没有一个能让煤球钻的圈？");
        Tell("ringplace", leaps >= LeapsBeforeAdvice, "圈得立在缝跟前，煤球才跳得进去。", "小满");
    }

    private void Tell(string key, bool when, string line, string speaker = "")
    {
        if (when && told.Add(key)) Say(speaker, 4, line);
    }

    private void Say(string speaker, double seconds, string line) => home.Hud.Say(speaker, line, seconds);

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
