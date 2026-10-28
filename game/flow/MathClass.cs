using System;
using System.Collections.Generic;
using System.Linq;
using Game.Adapter;
using Game.Characters;
using Game.Player;
using Game.Rendering;
using Godot;

namespace Game.Flow;

/// <summary>
/// Scene 3 of the prologue, the math class, in the school Region. Xiaoman sits at her desk and can only look around.
/// The teacher lectures, asks her a question that does not matter, and sets to writing on the board; from then on she
/// can hold the prism up in her desk to see the empty classroom of section Ⅱ. She marks the three differences by
/// putting the crosshair on them in the lens and pressing E, and the teacher, who walks the aisle when the chalk stops, sends her back to the
/// start of this stretch if the teacher sees her peeking. The third difference ends the scene.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public partial class MathClass : Node
{
    private const double WriteSeconds = 12;
    private const double AnswerSeconds = 10;
    private const string Laughter = "（全班一阵偷笑。）";
    private const string AnswerKeysShown = "←  长方形      ↑  圆      →  三角形";
    private static readonly Key[] AnswerKeys = [Key.Left, Key.Up, Key.Right];

    private const string Question = "小满，你来说说，圆柱的侧面展开是什么形状？";
    private const string MarkPrompt = "记下这处不一样";

    // The entities in section Ⅱ that make each difference, and what she says about it.
    private static readonly (string[] Entities, string Line)[] Differences =
    [
        (["clockII"], "挂钟停在另一个时间……这边明明是九点多，那边的钟指着四点二十。"),
        (["deskII49", "chairII49"], "那边多了一张课桌！数一数……第四十九张。我们班明明只有四十八个人。"),
        (["chimneyII1", "chimneyII2"], "窗外的烟囱，这边是三根，那边只剩两根。")
    ];

    /// <summary>Emitted once, after the third difference is found and the notebook page and the remark on the podium are shown.</summary>
    [Signal] public delegate void FinishedEventHandler();

    /// <summary>The school region to play in, which the scenes after this one play in too. Left empty, the scene loads one.</summary>
    public Region3D? Region { get; set; }

    private SceneScript script = null!;
    private readonly HashSet<int> found = [];
    private FirstPersonPlayer player = null!;
    private PrismView prism = null!;
    private Character3D teacherBody = null!;
    private Teacher teacher = null!;
    private SceneCheckpoint checkpoint = null!;
    private Node3D props = null!;
    private Node3D aid = null!;
    private bool asking;
    private int? answer;

    public override void _Ready()
    {
        if (Region is null)
        {
            Region = GD.Load<PackedScene>(SchoolRoom.RegionPath).Instantiate<Region3D>();
            Region.Name = "Region";
            AddChild(Region);
        }
        // The prism stays in the desk until the lecture is over.
        Region.PrismEnabled = false;
        player = Region.GetNode<FirstPersonPlayer>("Generated/Player");
        script = new(player.Hud);
        prism = Region.GetNode<PrismView>("Generated/Prism");
        player.Seated = true;
        teacherBody = Region.Character("teacher");
        teacher = teacherBody.GetNode<Teacher>("Teacher");
        props = Region.Props("Ⅰ")!;
        aid = props.GetNode<Node3D>("TeachingAid");
        foreach (var classmate in Region.GetNode("Generated/Ⅰ").GetChildren().OfType<Character3D>().Where(c => c != teacherBody))
            classmate.Sit();
        ShowMarks();
        player.LensMarked += Found;
        var seat = player.Transform;
        checkpoint = new SceneCheckpoint();
        AddChild(checkpoint);
        checkpoint.Restarting += () =>
        {
            player.Hud.ClearText();
            prism.SetRaised(false);
        };
        checkpoint.Set(() =>
        {
            player.Transform = seat;
            player.Velocity = Vector3.Zero;
            player.Hud.Say("", Laughter, 2.5);
        });
        Lecture();
    }

    public override void _Process(double delta)
    {
        script.Tick(delta);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (!asking || input is not InputEventKey { Pressed: true, Echo: false } key) return;
        var choice = Array.IndexOf(AnswerKeys, key.Keycode);
        if (choice < 0) return;
        answer = choice;
        GetViewport().SetInputAsHandled();
    }

    private void Teach(string line, double seconds) => script.Say(teacherBody, line, seconds);

    private void Lecture()
    {
        script.Wait(1.5);
        Teach("同学们，翻开课本。这一节我们来认识圆柱。", 4);
        script.Do(() => aid.Visible = true);
        Teach("大家看，这是一块长方形的硬纸板，我把它钉在木棍上。", 4.5);
        script.Do(() => CreateTween().TweenProperty(aid, "rotation:y", Mathf.Tau, 2));
        Teach("现在转一圈——长方形绕着它的一条边转一圈，围出来的就是一个圆柱。", 6.5);
        script.Do(() => aid.Visible = false);
        Teach("点动成线，线动成面，面动成体。这句课本上没有，记住了。", 5);
        script.Narrate("", "小满听得入神，在课本页边写下一行小字：「那体动了，会变成什么？」", 5.5);
        Teach(Question, 3.5);
        script.Do(Ask);
    }

    // Any answer, or none, leads to the same place. The question stays on the screen with the answers.
    private void Ask()
    {
        asking = true;
        player.Hud.ShowNotice($"{Question}\n{AnswerKeysShown}", AnswerSeconds);
        var left = AnswerSeconds;
        script.Step(delta => (left -= delta) <= 0 || answer is not null);
        script.Do(Reply);
    }

    private void Reply()
    {
        asking = false;
        player.Hud.ClearText();
        Teach(answer switch
        {
            0 => "对，是长方形。把侧面沿着高剪开，铺平，就是一个长方形。",
            null => "没想起来？没关系。把侧面沿着高剪开，铺平，是个长方形。",
            _ => "再想想。把侧面沿着高剪开，铺平，是个长方形。"
        }, 5);
        Teach("好，大家把黑板上的内容抄下来。", 3);
        script.Do(Peek);
    }

    // From here the teacher walks the aisle, and the prism can come out of the desk.
    private void Peek()
    {
        Region!.PrismEnabled = true;
        teacher.Checkpoint = checkpoint;
        teacher.Patrol([teacherBody.Position, props.GetNode<Marker3D>("AisleEnd").Position], WriteSeconds);
        player.Hud.Say("小满", "粉笔声一停，老师就要下来巡视了……桌肚里的棱镜，偷偷看一眼应该没事。", 6);
        player.Hud.Say("小满", "要是看到哪里和这边不一样，就按 E 记下来。", 5);
    }

    // What she may mark: the differences that are not found yet.
    private void ShowMarks() => player.LensMarks = Differences.Where((_, i) => !found.Contains(i))
        .SelectMany(difference => difference.Entities)
        .ToDictionary(entity => entity, _ => MarkPrompt);

    private void Found(string entity)
    {
        var index = Array.FindIndex(Differences, difference => difference.Entities.Contains(entity));
        found.Add(index);
        ShowMarks();
        player.Hud.ShowNotice($"找到了一处不一样（{found.Count}/{Differences.Length}）", 2);
        player.Hud.Say("小满", Differences[index].Line, 4);
        if (found.Count == Differences.Length) Finish();
    }

    private void Finish()
    {
        teacher.Stop();
        // The last difference takes its notice and its line first.
        script.Wait(6);
        script.Do(() => player.Hud.UnlockPage(1));
        script.Narrate("", "小满在笔记本上画了一只眼睛，旁边写着：另一页的教室。", 4.5);
        script.Narrate("小满", "讲台……镜片里它发着一圈微光。这边是一整块，那边怎么成了两级台阶？", 6);
        script.Do(() => EmitSignal(SignalName.Finished));
    }
}
