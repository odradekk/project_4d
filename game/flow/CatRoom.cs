using System;
using System.Linq;
using Game.Characters;
using Game.Interaction;
using Godot;
using static Game.Flow.RoomQueries;

namespace Game.Flow;

/// <summary>
/// Scene 8, the room at 21:00: the cat stares at the corner and leaps into a seam; she leads it down from the cabinet in
/// page II with the wand, the cabinet, the ball and the nut, and the last leap through the ring takes it into page III.
/// The cat is told what to do by its stage, and every stage that waits for the cat to finish is told again if the player
/// picks the cat up in the middle of it. What the stages wait for is read from the engine; see <see cref="RoomQueries"/>.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class CatRoom : Node
{
    public enum Stage { Watching, Entering, Perched, Descending, AtPit, Crossing, Chasing, Leaping, Gone }

    private const string RoomPath = "res://regions/room.tscn";
    // Where the seams stand, and the place the cat looks at: the seam's light, at about the height of its head.
    private static readonly Vector3 S1 = new(7.1f, 0, 4.3f);
    private static readonly Vector3 S2 = new(6.25f, 0, 4.3f);
    private static readonly Vector3 SeamLook = S1 + new Vector3(0, .5f, 0);
    private static readonly Vector3 Perch = new(3.85f, 2.4f, 5.78f);
    // The wand falls here in the middle of the board; the board is Slope degrees steeper than level and lies north from its hinge.
    private static readonly float Slope = Mathf.DegToRad(27);
    private static readonly Vector3 BoardHinge = new(4.95f, 2f, 5.53f);
    private static readonly Vector3 PitCenter = new(4.95f, 0, 4.225f);
    private static readonly Vector3 Deep = new(4f, 0, 5.9f);
    private const float BoardLength = 2.138f;
    private const float BoardHalfThickness = .025f;
    private const float SlideSpeed = 1.2f;
    private const float PitFarZ = 3.7f;
    private const double WatchSeconds = 40;
    private const double LookSeconds = 1;
    private const float LookAngle = .7f;
    private const float CrossRadius = .12f;
    // The ring is tried this often, and the rest of the scene thinks this often: each look at the engine cuts the whole world.
    private const double RingAttemptSeconds = 1;
    private const double ThinkSeconds = .1;
    private const float FallLimit = -3;
    private const float LureReach = 3;
    private const float RingLureReach = 1.5f;
    private const float YarnTravelForNote = 1.5f;

    private HomeScene home = null!;
    private RoomQueries ask = null!;
    private Character3D catBody = null!;
    private Cat cat = null!;
    private Action? then;
    private double clock, lookedAtSeam, stageClock, ringTimer, thought;
    private float yarnTravel;
    private Vector3? lastYarn;
    private readonly SafeSpot playerSafe = new(), catSafe = new();
    private bool bridged, ringSeen, boardSeen;

    [Signal] public delegate void FinishedEventHandler();

    /// <summary>For the debug entry: the pages of the notebook that the earlier scenes would have drawn.</summary>
    [Export] public bool WithEarlierPages { get; set; }

    public Stage Now { get; private set; }
    public Character3D CatBody => catBody;
    internal HomeScene Home => home;

    public override void _Ready()
    {
        home = HomeScene.Open(this, path: RoomPath);
        ask = new RoomQueries(home.Region.World!);
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
        playerSafe.Set(home.Player.Position);
        catSafe.Set(catBody.Position);
        Say("", 3.5, "晚上九点。煤球又弓着背盯住衣柜和书桌之间的墙角，尾巴一甩一甩。");
        Say("", 3.5, "那边什么也没有啊。举起棱镜看看？（按住右键）");
    }

    public override void _PhysicsProcess(double delta)
    {
        clock += delta;
        stageClock += delta;
        thought += delta;
        Recover();
        if (cat.Held) return;
        if (Now == Stage.Leaping) Cross();
        if (thought >= ThinkSeconds)
        {
            Notes();
            Hints();
            Advance(thought);
            thought = 0;
        }
        // A stage whose command ended without the stage ending, or was dropped when the cat was picked up, is given again.
        if (!catBody.Busy) Give(Now);
    }

    private void Advance(double delta)
    {
        switch (Now)
        {
            case Stage.Watching:
                lookedAtSeam = SeesSeam() ? lookedAtSeam + delta : 0;
                if (lookedAtSeam >= LookSeconds || clock >= WatchSeconds) Enter(Stage.Entering);
                break;
            case Stage.Perched:
                if (BoardIsDown && Yarn() is { } yarn && Flat(yarn - FootSpot).Length() < LureReach) Enter(Stage.Descending);
                break;
            case Stage.AtPit:
                if (BallOverPit(out _)) Enter(Stage.Crossing);
                break;
            case Stage.Chasing:
                ringTimer -= delta;
                if (ringTimer <= 0)
                {
                    ringTimer = RingAttemptSeconds;
                    if (RingIsLuring() && cat.JumpThroughRing("nut", "ring", "wand")) Now = Stage.Leaping;
                }
                break;
        }
    }

    // The leap is over the seam for a few frames only, so it is looked at every frame.
    private void Cross()
    {
        if (catBody.Position.Y > .05f && Flat(catBody.Position - S2).Length() < CrossRadius && catBody.CrossSeam("s2", PageIII) is null) Vanish();
    }

    private void Enter(Stage next)
    {
        Now = next;
        stageClock = 0;
        Give(next);
    }

    // What the cat does in a stage. A command that runs until it ends continues with the next stage when it does.
    private void Give(Stage stage)
    {
        switch (stage)
        {
            case Stage.Watching:
                cat.Sit(SeamLook);
                cat.Stare(SeamLook);
                break;
            case Stage.Entering:
                Run(() => cat.JumpTo(S1 + new Vector3(0, .3f, 0)), EnterPageII);
                break;
            case Stage.Perched:
                cat.Stare(Yarn);
                break;
            case Stage.Descending:
                cat.Holdable = true;
                Run(() => cat.JumpTo(BoardPoint(.2f)), () => Run(() => Slide(FootSpot), () => Enter(Stage.AtPit)));
                break;
            case Stage.AtPit:
                catBody.Posture = Posture.Sitting;
                catBody.Do(_ =>
                {
                    catBody.Warp(FootSpot);
                    catBody.Face(PitCenter);
                    return false;
                });
                break;
            case Stage.Crossing:
                if (!BallOverPit(out var top)) Enter(Stage.AtPit);
                else Run(() => cat.JumpTo(top), () => Run(() => cat.JumpTo(new(top.X, 0, PitFarZ)), Bridged));
                break;
            case Stage.Chasing:
                catBody.Posture = Posture.Standing;
                cat.Chase(Yarn);
                break;
            case Stage.Leaping:
                // The leap ended without the seam being reached, or was dropped: back to the lure.
                Enter(Stage.Chasing);
                break;
            case Stage.Gone:
                cat.Sit(Deep + new Vector3(1, 0, 0));
                cat.Stare(Deep + new Vector3(1, 0, 0));
                break;
        }
    }

    private void Run(Action command, Action next)
    {
        command();
        then = next;
    }

    private void EnterPageII()
    {
        // The seam may be filled; the cat leaps into it again.
        if (catBody.CrossSeam("s1", PageII) is not null)
        {
            Give(Stage.Entering);
            return;
        }
        catBody.PlaceAt(Perch, 0);
        catBody.Posture = Posture.Lying;
        catSafe.Set(Perch);
        cat.Holdable = false;
        Now = Stage.Perched;
        Say("小满", 2.5, "煤球？！");
        Say("", 4, "它跳进那道光里去了。透过镜片看，第Ⅱ页的吊柜顶上缩着一团黑影。");
        Say("", 4, "要过去看看吗？举着棱镜走到缝前，按左键。");
        Give(Stage.Perched);
    }

    private void Bridged()
    {
        bridged = true;
        catSafe.Set(catBody.Position);
        Say("", 3, "煤球踩着皮球过去了。");
        Enter(Stage.Chasing);
    }

    private void Vanish()
    {
        catBody.PlaceAt(Deep, 0);
        Now = Stage.Gone;
        Give(Stage.Gone);
        Say("小满", 2.5, "煤球？！");
        Say("", 4, "它在半空里消失了。圆环前面，还藏着一道从这一页看不见的缝。");
        GetTree().CreateTimer(5).Timeout += () => EmitSignal(SignalName.Finished);
    }

    // The cat's slide down the board is a walk with no floor under it: the board is steeper than the cat can stand on.
    private void Slide(Vector3 end) => catBody.Do(delta =>
    {
        var next = catBody.Position.MoveToward(end, (float)delta * SlideSpeed);
        catBody.Face(end);
        catBody.Warp(next);
        return next.DistanceTo(end) < .001f;
    });

    // A point on the upper face of the board, s metres from its hinge along it.
    private static Vector3 BoardPoint(float s) =>
        BoardHinge + new Vector3(0, -Mathf.Cos(Slope), -Mathf.Sin(Slope)) * s + new Vector3(0, Mathf.Sin(Slope), -Mathf.Cos(Slope)) * BoardHalfThickness;

    // Where the cat waits at the foot of the board, with the pit in front of it.
    private static Vector3 FootSpot => BoardPoint(BoardLength - .1f);

    private bool BoardIsDown => ask.DevicePosition("cabinet") <= -Math.PI / 2 + 1e-9;

    private Vector3? Yarn() => ask.Bounds("wand", PageII, "yarn") is { } box ? box.GetCenter() : null;

    private bool SeesSeam()
    {
        if (!home.Player.PrismRaised || home.Region.CurrentSection != PageI) return false;
        var eyes = home.Player.Camera;
        return (-eyes.GlobalBasis.Z).AngleTo(SeamLook - eyes.GlobalPosition) < LookAngle;
    }

    // The ball lies still and holds itself up over empty space in page II: nothing is under the middle of its slice there.
    // The pit is the only hole in that floor, so the engine's answer is all that is asked.
    private bool BallOverPit(out Vector3 top)
    {
        top = default;
        if (ask.Bounds("ball", PageII) is not { } slice || !ask.IsResting("ball")) return false;
        var middle = slice.GetCenter();
        var reach = slice.Size.X / 2 * .7f;
        if (ask.InPageII(new(middle.X, -.05f, middle.Z), new(reach, .04f, reach)).Count > 0) return false;
        top = new(middle.X, slice.End.Y, middle.Z);
        return true;
    }

    private bool RingIsLuring() =>
        Yarn() is { } yarn && ask.Bounds("nut", PageII, "ring") is { } ring
        && Flat(yarn - ring.GetCenter()).Length() < RingLureReach && Flat(catBody.Position - ring.GetCenter()).Length() < 3.5f;

    // The notebook pages that this scene draws, and the lines for what has just happened to the board.
    private void Notes()
    {
        if (home.Region.CurrentSection != PageI) Unlock(5, "她穿过了缝。笔记本里多了一页：缝。");
        var yarn = Yarn();
        if (yarn is { } now)
        {
            if (lastYarn is { } last) yarnTravel += last.DistanceTo(now);
            if (yarnTravel >= YarnTravelForNote) Unlock(6, "这一页晃，那一页的毛线球也跟着晃。笔记本里多了一页：隔着一页。");
        }
        lastYarn = yarn;
        ringSeen |= ask.Bounds("nut", PageII, "ring") is not null;
        if (bridged && ringSeen) Unlock(7, "皮球和圆环都试过了。笔记本里多了一页：三种东西。");
        if (!boardSeen && BoardIsDown)
        {
            boardSeen = true;
            var watching = home.Player.PrismRaised && home.Region.CurrentSection == PageI;
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
        if (player.IsOnFloor()) playerSafe.Stand(player.Position);
        else if (player.Position.Y < FallLimit)
        {
            player.Position = playerSafe.Spot;
            player.Velocity = Vector3.Zero;
            Say("小满", 2.5, "差点掉下去！");
        }
        if (catBody.IsOnFloor()) catSafe.Stand(catBody.Position);
        else if (catBody.Position.Y < FallLimit) catBody.PlaceAt(catSafe.Spot, catBody.Rotation.Y);
    }

    // The places something stood on the floor in the last half second; the oldest is far enough back from an edge it fell over.
    private sealed class SafeSpot
    {
        private const int Frames = 30;
        private readonly System.Collections.Generic.Queue<Vector3> trail = new();
        internal Vector3 Spot => trail.Peek();

        internal void Set(Vector3 position)
        {
            trail.Clear();
            trail.Enqueue(position);
        }

        internal void Stand(Vector3 position)
        {
            trail.Enqueue(position);
            if (trail.Count > Frames) trail.Dequeue();
        }
    }

    private readonly System.Collections.Generic.HashSet<string> told = [];

    private void Hints()
    {
        Tell("guard", clock > 75 && ask.State("wand").Pose.Position.Z < 2.8, "逗猫棒在窗台的防护栏后面。手伸不进去，就翻翻笔记本里“翻面”那一页。");
        Tell("high", Now == Stage.Perched && stageClock > 60 && !BoardIsDown, "吊柜太高了，够不着。得踩着什么才行。");
        Tell("sheet", Now == Stage.AtPit && ask.InPageII(PitCenter + new Vector3(0, .5f, 0), new(.3f, .5f, .3f)).Contains("stool"),
            "薄得像张纸，煤球不敢踩。");
        Tell("pit", Now == Stage.AtPit && stageClock > 45, "要是洞中间有个能踩的地方……");
        Tell("ring", Now == Stage.Chasing && stageClock > 60, "那一页有没有一个能让煤球钻的圈？");
    }

    private void Tell(string key, bool when, string line)
    {
        if (when && told.Add(key)) Say("", 4, line);
    }

    private void Say(string speaker, double seconds, string line) => home.Hud.Say(speaker, line, seconds);

    private static Vector3 Flat(Vector3 v) => new(v.X, 0, v.Z);
}
