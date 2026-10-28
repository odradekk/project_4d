using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Debugging;
using Game.Player;
using Game.Prologue;
using Game.Rendering;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The math class: the school region has the classroom as the two sections show it, and the scene plays through, from
/// the lecture and its optional question to the three differences, the teacher's round and the notebook page.
/// </summary>
public partial class SchoolChecks : Node
{
    private const string ScenePath = "res://prologue/math_class.tscn";
    private const string Prompt = "E  记下这处不一样";
    // How much faster than real time the waits for the lecture and the teacher's round run.
    private const float Speed = 8;

    private readonly List<string> said = [];
    private TurningFixture play = null!;
    private MathClass scene = null!;
    private int finished;

    // The front-left leg of the chair on the extra desk, which is what she sees of the desk from her seat.
    private Vector3 DeskTarget => Where("deskII49") + new Vector3(-.17f, 1.1f, -.03f);

    private Region3D region => scene.GetNode<Region3D>("Region");
    private FirstPersonPlayer player => region.GetNode<FirstPersonPlayer>("Generated/Player");
    private PrismView prism => region.GetNode<PrismView>("Generated/Prism");
    private Character3D teacher => region.Character("teacher");
    private World world => region.World!;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
#if DEBUG
            Check(DebugScenes.Find("math-class") == ScenePath, "the math class has a debug entry");
#endif
            await Load();
            CheckClassroom();
            CheckDifferencesAreSeen();
            await CheckSeat();
            await CheckLecture(Key.Left, "对，是长方形");
            await CheckRound();
            await CheckPeeking();
            await CheckCaught();
            await CheckFinish();
            await Load();
            await ReachPeek(null);
            Check(said.Any(line => line.Contains("没想起来")), "an unanswered question gets the teacher's answer and the class goes on");
            await CheckMarkedThenCaught();
            await Load();
            await ReachPeek(Key.Up);
            Check(said.Any(line => line.Contains("再想想")), "a wrong answer gets the teacher's correction and the class goes on");
            await Unload();
            GD.Print("School checks passed: classroom, differences, seat, lecture, question, round, peeking, caught, marked then caught, finish.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Engine.TimeScale = 1;
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public override void _Process(double delta)
    {
        if (scene is null || !IsInstanceValid(scene)) return;
        var text = player.Hud.SubtitleText;
        if (text != "" && (said.Count == 0 || said[^1] != text)) said.Add(text);
    }

    private async Task Load()
    {
        await Unload();
        said.Clear();
        finished = 0;
        scene = GD.Load<PackedScene>(ScenePath).Instantiate<MathClass>();
        scene.Finished += () => finished++;
        AddChild(scene);
        Check(region.Loaded, $"the school region must load: {region.Diagnostics}");
        await play.Frames(3);
    }

    private async Task Unload()
    {
        if (scene is null) return;
        // A sound that is still playing at exit keeps its stream in use, which Godot reports as an error.
        RemoveChild(scene);
        scene.Free();
        scene = null!;
        await play.Frames(10);
    }

    private async Task Fast(Func<bool> condition, double seconds, string message)
    {
        Engine.TimeScale = Speed;
        await play.Until(condition, seconds, message);
        Engine.TimeScale = 1;
    }

    // Plays the lecture up to the point where the prism comes out of the desk, answering the question with the key, if any.
    private async Task ReachPeek(Key? answer)
    {
        await Fast(() => player.Hud.HintText.Contains("长方形"), 60, "the teacher must ask the question");
        if (answer is { } key) Press(key);
        await Fast(() => region.PrismEnabled, 60, "the class must go on after the question");
    }

    private Vector3 Where(string entity)
    {
        var position = ((EntityReadResult.Found)world.ReadEntity(new EntityId(entity))).Entity.Pose.Position;
        return new((float)position.X, (float)position.Y, (float)position.Z);
    }

    private SliceBoundsResult.Bounds Bounds(string entity, string section, string? part = null) =>
        (SliceBoundsResult.Bounds)world.GetSliceBounds(new EntityId(entity), new SectionId(section), part is null ? null : new PartId(part));

    private IEnumerable<SliceGeometry> Slices(string section) =>
        ((SectionGeometryResult.Geometry)world.GetSectionGeometry(new SectionId(section))).Entities;

    private string[] Occupants(string section, Vector3 center, Vector3 half)
    {
        var w = region.GetNode<Section3D>($"Sections/{section}").W;
        var pose = SceneReader.PoseOf(new Transform3D(Basis.Identity, center), w);
        var result = (OccupancyResult.Occupancy)world.QueryOccupancy(new OccupancyBox(pose, new(half.X, half.Y, half.Z, .006)), []);
        return result.Hits.Select(hit => hit.Entity.Value).ToArray();
    }

    private void CheckClassroom()
    {
        var first = region.GetNode("Generated/Ⅰ").GetChildren().OfType<Character3D>().ToArray();
        Check(first.Contains(teacher) && first.Count(person => person.Posture == Posture.Sitting) == 47,
            "section Ⅰ has the teacher and a class sitting at their desks");
        Check(!region.GetNode("Generated/Ⅱ").GetChildren().OfType<Character3D>().Any(), "section Ⅱ has nobody in it");
        // The chair stands on the desk upside down in Ⅱ, which makes the desk reach higher than it does in Ⅰ.
        Check(Bounds("deskII22", "Ⅱ").Max.Y > Bounds("deskI22", "Ⅰ").Max.Y + .4, "in Ⅱ the chairs are on the desks");
        // The podium is one block in Ⅰ and two steps in Ⅱ, and it is the lens's glowing kind of thing in Ⅱ.
        var front = new Vector3(-1.26f, .15f, -5.1f);
        var back = new Vector3(-1.26f, .15f, -5.5f);
        var half = new Vector3(.5f, .02f, .15f);
        Check(Occupants("Ⅰ", front, half).Contains("PodiumBlock") && Occupants("Ⅰ", back, half).Contains("PodiumBlock"),
            "the podium is a block as high as both steps' upper one in Ⅰ");
        Check(Occupants("Ⅱ", front, half).Length == 0 && Occupants("Ⅱ", back, half).Contains("PodiumSteps"),
            "the podium has a low step in front and a high one behind in Ⅱ");
        Check(Slices("Ⅱ").Single(slice => slice.Entity.Value == "PodiumSteps").ThroughObject, "the podium glows in the lens");
        Check(!Slices("Ⅱ").Where(slice => slice.Entity.Value.StartsWith("desk")).Any(slice => slice.ThroughObject), "the desks do not");
        // The three differences.
        Check(Bounds("clockI", "Ⅰ", "minute") != Bounds("clockII", "Ⅱ", "minute") && Bounds("clockI", "Ⅰ", "hour") != Bounds("clockII", "Ⅱ", "hour"),
            "the clocks show different times");
        Check(Slices("Ⅰ").Count(slice => slice.Entity.Value.StartsWith("desk")) == 48 && Slices("Ⅱ").Count(slice => slice.Entity.Value.StartsWith("desk")) == 49,
            "section Ⅱ has one desk more");
        // The desks are where they were: the extra one is the only difference among them.
        var desks = Slices("Ⅰ").Select(slice => slice.Entity.Value).Where(id => Regex.IsMatch(id, @"^deskI\d+$")).ToArray();
        Check(desks.Length == 48 && desks.All(id => Where(id) == Where("deskII" + id[5..])), "every desk of Ⅰ has a desk of Ⅱ in the same place");
        Check(Slices("Ⅰ").Count(slice => slice.Entity.Value.StartsWith("chimney")) == 3 && Slices("Ⅱ").Count(slice => slice.Entity.Value.StartsWith("chimney")) == 2,
            "section Ⅱ has one chimney less");
        var spot = Where("deskII49");
        Check(Occupants("Ⅰ", spot + new Vector3(0, .5f, 0), new(.3f, .5f, .23f)).Length == 0, "the extra desk stands where Ⅰ has nothing");
    }

    // What she can see from her seat: a ray from her eyes through each difference in Ⅱ hits that difference first.
    private void CheckDifferencesAreSeen()
    {
        (string Entity, Vector3 Target)[] targets =
        [
            ("clockII", Where("clockII")),
            ("deskII49", DeskTarget),
            ("chimneyII1", Where("chimneyII1")),
            ("chimneyII2", Where("chimneyII2")),
            ("PodiumSteps", new(-.7f, .1f, -4.95f)),
            ("PodiumSteps", new(-.7f, .2f, -5.45f))
        ];
        var space = region.GetNode<SubViewport>("Generated/Ⅱ").World3D.DirectSpaceState;
        var eyes = player.Camera.GlobalPosition;
        foreach (var (entity, target) in targets)
        {
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(eyes, target + (target - eyes).Normalized() * .3f));
            var seen = hit.Count == 0 ? null : ((Node)hit["collider"].AsGodotObject()).GetParent().Name.ToString();
            Check(seen == entity, $"from her seat she sees {entity} in Ⅱ at {target}, but the first thing in the way is {seen}");
        }
    }

    private async Task CheckSeat()
    {
        var seat = player.Position;
        Input.ActionPress("move_forward");
        await play.Frames(30);
        Input.ActionRelease("move_forward");
        Check(seat.DistanceTo(player.Position) < .001f, "she sits at her desk and does not walk");
        Check(!prism.GetNode<Label>("Controls").Text.Contains("WASD"), "the help does not tell her to walk");
        var yaw = player.Rotation.Y;
        Input.ParseInputEvent(new InputEventMouseMotion { Relative = new(30, 0), ScreenRelative = new(30, 0) });
        await play.Frames(2);
        Check(Mathf.Abs(player.Rotation.Y - yaw) > .01f, "she can turn to look around");
        Button(MouseButton.Right, true);
        await play.Frames(3);
        Check(!prism.Raised, "the prism stays in the desk during the lecture");
        Button(MouseButton.Right, false);
    }

    private async Task CheckLecture(Key answer, string reply)
    {
        var aidSeen = false;
        var aidTurned = false;
        var aid = region.GetNode<Node3D>("Lesson/TeachingAid");
        Check(region.GetNode<Label3D>("Lesson/BoardText").Text.Contains("圆柱"), "the board says what the lesson is");
        await Fast(() =>
        {
            aidSeen |= aid.Visible;
            aidTurned |= aid.Visible && Mathf.Abs(aid.Rotation.Y) > .5f;
            Check(!region.PrismEnabled, "the prism stays in the desk until the question has been asked");
            return player.Hud.HintText.Contains("长方形");
        }, 90, "the teacher must ask the question");
        Check(aidSeen && aidTurned && !aid.Visible, "the teacher shows the board on a stick, turns it, and puts it away");
        Check(player.Hud.HintText.Contains("侧面展开"), "the question stays on the screen while she answers");
        Check(said.Any(line => line.Contains("长方形绕着它的一条边转一圈")) && said.Any(line => line.Contains("点动成线，线动成面，面动成体")),
            "the teacher says what a cylinder is and the line that is not in the book");
        Check(said.Any(line => line.Contains("那体动了，会变成什么？")), "she writes her question in the margin");
        Check(IndexOf("长方形绕着它的一条边转一圈") < IndexOf("点动成线") && IndexOf("点动成线") < IndexOf("那体动了") && IndexOf("那体动了") < IndexOf("侧面展开"),
            "the lecture goes in order");
        Press(answer);
        await Fast(() => region.PrismEnabled, 60, "the class must go on after the question");
        Check(said.Any(line => line.Contains(reply)), $"the teacher answers: {reply}");
    }

    private int IndexOf(string part) => said.FindIndex(line => line.Contains(part));

    // The chalk is going: the teacher writes, then walks the aisle; a lens that stays in the desk is never seen.
    private async Task CheckRound()
    {
        await Fast(() => teacher.Position.Z > 3.5f, 90, "the teacher must walk down the aisle");
        await Fast(() => teacher.Position.Z < -4.3f, 90, "the teacher must walk back to the board");
        Check(!said.Any(line => line.Contains("看哪儿呢")), "a teacher does not see a prism that is in the desk");
    }

    private void Aim(Vector3 target)
    {
        var offset = target - player.Camera.GlobalPosition;
        player.Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
        player.Camera.Rotation = new(Mathf.Atan2(offset.Y, new Vector2(offset.X, offset.Z).Length()), 0, 0);
    }

    // Marks the difference that the entity is a part of, and checks that what she says is about that difference.
    private async Task Find(string entity, Vector3 target, string count, string line)
    {
        Aim(target);
        await play.Frames(2);
        Check(player.Hud.HintText == Prompt, $"aiming at {entity} in the lens offers to mark it, but the hint is '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(2);
        Check(player.Hud.HintText.Contains(count), $"{entity} makes {count}, but the notice is '{player.Hud.HintText}'");
        await play.Until(() => player.Hud.SubtitleVisible && player.Hud.SubtitleText.Contains(line), 10, $"she must say what is different about {entity}");
        Check(player.Hud.CrosshairVisible, "the crosshair stays while she speaks with the lens up");
        await play.Frames(60);
        Check(player.Hud.HintText != Prompt, $"{entity} is marked once");
    }

    private async Task CheckPeeking()
    {
        var clock = Where("clockII");
        Aim(clock);
        await play.Frames(2);
        Check(player.Hud.HintText != Prompt, "nothing offers itself to be marked while the prism is in the desk");
        Button(MouseButton.Right, true);
        await play.Frames(2);
        Check(prism.Raised && prism.TargetSection == "Ⅱ", "she holds the lens up to section Ⅱ");
        // What is not a difference offers nothing, however near it is to one.
        (string What, Vector3 Target)[] others =
        [
            ("the middle of the board", new(-.7f, 1.6f, -5.66f)),
            ("the top of the board", new(-.7f, 2.2f, -5.66f)),
            ("the wall under the clock", new(-.7f, 2.3f, -5.7f)),
            ("the ceiling over the clock", new(-.7f, 3.2f, -5.2f)),
            ("an ordinary desk", Where("deskII31") + new Vector3(0, .76f, 0)),
            ("the wall between two windows", new(-3.6f, 1.5f, 0)),
            ("the sky between the chimneys", new(-30, 12, -3))
        ];
        foreach (var (what, target) in others)
        {
            Aim(target);
            await play.Frames(2);
            Check(player.Hud.HintText != Prompt, $"aiming at {what} in the lens offers nothing");
        }
        // The lens says what is under the crosshair in Ⅱ, however far, and nothing for the sky.
        (string Entity, Vector3 Target)[] seen = [("clockII", clock), ("chimneyII1", Where("chimneyII1")), ("Blackboard", new(-.7f, 2.1f, -5.66f)), (null!, new(-30, 12, -3))];
        foreach (var (entity, target) in seen)
        {
            Aim(target);
            Check(prism.SeenEntity() == entity, $"the lens sees {entity ?? "nothing"} at {target}, but it sees {prism.SeenEntity() ?? "nothing"}");
        }
        await Find("clockII", clock, "1/3", "挂钟");
        // Either chimney of Ⅱ makes the difference of the chimneys, and it is marked once for both.
        Aim(Where("chimneyII2"));
        await play.Frames(2);
        Check(player.Hud.HintText == Prompt, "aiming at a chimney of Ⅱ offers to mark it");
    }

    private async Task CheckCaught()
    {
        var seat = player.Position;
        await Fast(() => said.Any(line => line.Contains("看哪儿呢")), 90, "the teacher must see the prism held up");
        await Fast(() => !prism.Raised, 5, "the lens must go down when she is sent back");
        await play.Frames(2);
        Check(player.Position.DistanceTo(seat) < .001f && player.Hud.SubtitleText.Contains("偷笑"), "back at her desk, with the class laughing");
        Check(Apart(teacher.Position, new(-1.26f, 0, -4.55f)) < .3f, "the teacher is back at the board");
        Button(MouseButton.Right, false);
        await play.Frames(2);
        Button(MouseButton.Right, true);
        await play.Frames(2);
        Aim(Where("clockII"));
        await play.Frames(2);
        Check(player.Hud.HintText != Prompt, "what she marked before she was caught stays marked");
    }

    // She marks a difference just as the teacher comes into view: the teacher's words must still be seen.
    private async Task CheckMarkedThenCaught()
    {
        Button(MouseButton.Right, true);
        await Fast(() => teacher.Position.Z > -3.5f, 90, "the teacher must come down the aisle");
        await play.Until(() => teacher.Position.Z > -3.1f, 3, "the teacher must come nearer");
        Aim(DeskTarget);
        await play.Frames(1);
        Press(Key.E);
        await play.Frames(2);
        Check(player.Hud.HintText.Contains("1/3"), "she marks a difference");
        await Fast(() => said.Any(line => line.Contains("看哪儿呢")), 5, "the teacher's words are shown although she has just marked a difference");
        await Fast(() => !prism.Raised, 5, "the lens must go down when she is sent back");
        Button(MouseButton.Right, false);
    }

    private async Task CheckFinish()
    {
        await Find("deskII49", DeskTarget, "2/3", "第四十九张");
        Check(finished == 0 && !player.Hud.UnlockedPages.Contains(1), "the scene is not over with two differences found");
        await Find("chimneyII2", Where("chimneyII2"), "3/3", "烟囱");
        await Fast(() => finished == 1, 60, "the scene must finish after the third difference");
        Check(player.Hud.UnlockedPages.Count == 1 && player.Hud.UnlockedPages.Contains(1), "the first notebook page is unlocked");
        Check(!teacher.Busy, "the teacher stands where she was when the scene is over, and sees nobody");
        Check(said.Any(line => line.Contains("讲台")), "she remarks on the podium");
        Check(!player.Hud.NotebookAvailable && !player.Hud.OpenNotebook(), "the notebook can be opened only from scene seven on");
        Button(MouseButton.Right, false);
    }
}
