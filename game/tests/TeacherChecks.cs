using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Audio;
using Game.Characters;
using Game.Flow;
using Game.Player;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The teacher on the test region: patrols with chalk and footsteps, and sees the player who holds the prism up
/// in view, but not otherwise.
/// </summary>
public partial class TeacherChecks : Node
{
    // The teacher writes at the board and faces away from the aisle, which runs two metres toward -z.
    private static readonly Vector3 Board = new(-2, .02f, 0);
    private static readonly Vector3 AisleEnd = new(-2, .02f, -2);
    private static readonly Vector3[] Route = [Board, AisleEnd];

    private TurningFixture play = null!;
    private FirstPersonPlayer player => play.Player;
    private Character3D body => play.Region.Character("teacher");
    private Teacher teacher => body.GetNode<Teacher>("Teacher");
    private int spotted;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await LoadTeacher(Board);
            await CheckPatrol();
            await LoadTeacher(Board);
            await CheckSpotted();
            await LoadTeacher(Board);
            await CheckNotSpotted();
            await LoadTeacher(new(-2, .02f, -4.7f));
            await CheckBehindWall();
            await LoadTeacher(Board, "Ⅱ");
            await CheckOtherSection();
            // A sound that is still playing at exit keeps its stream in use, which Godot reports as an error.
            RemoveChild(play.Region);
            play.Region.Free();
            await play.Frames(10);
            GD.Print("Teacher checks passed: patrol, chalk and footsteps, seen, not seen.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private async Task LoadTeacher(Vector3 position, string section = "Ⅰ")
    {
        spotted = 0;
        await play.Load(region => AddTeacher(region, "teacher", position, section));
        teacher.Spotted += () => spotted++;
    }

    private SectionSound Sound(string file) => player.GetParent().GetChildren().OfType<SectionSound>().Single(sound => sound.Stream.ResourcePath.EndsWith($"{file}.wav"));

    private async Task CheckPatrol()
    {
        teacher.Patrol(Route, 1);
        await play.Frames(5);
        Check(Sound("chalk").Playing && !Sound("footsteps").Playing, "the teacher writes with the chalk sounding");
        Check(Forward(body).Dot(Vector3.Back) > .99f, "the teacher at the board faces away from the aisle");
        await play.Until(() => Apart(body.Position, Board) > .5f, 3, "the teacher must set off along the aisle");
        Check(!Sound("chalk").Playing && Sound("footsteps").Playing, "the chalk stops and the footsteps start when the teacher walks");
        Check(Forward(body).Dot(Vector3.Forward) > .99f, "the teacher walks the way of the aisle");
        await play.Until(() => Apart(body.Position, AisleEnd) < .15f, 4, "the teacher must walk to the end of the aisle");
        await play.Until(() => Apart(body.Position, Board) < .15f, 4, "the teacher must walk back");
        await play.Until(() => Sound("chalk").Playing, 1, "the teacher must write again at the board");
        Check(!Sound("footsteps").Playing, "the footsteps stop when the teacher writes");
        // The sounds are heard from where the teacher is.
        Check(Apart(Sound("chalk").Position, body.Position) < .01f, "the chalk sounds at the teacher");
    }

    private async Task CheckSpotted()
    {
        var checkpoint = new SceneCheckpoint();
        AddChild(checkpoint);
        var restored = 0;
        checkpoint.Set(() => restored++);
        checkpoint.Restarting += player.Hud.ClearText;
        teacher.Checkpoint = checkpoint;
        teacher.Patrol(Route, 100);
        player.Position = new(-2, .02f, 3);
        await play.Frames(5);
        Check(spotted == 0, "a teacher does not see a player whose prism is down");
        Button(MouseButton.Right, true);
        await play.Frames(3);
        Check(spotted == 1, "the teacher sees the player who holds the prism up in view");
        Check(player.Hud.SubtitleText == "老师：看哪儿呢？", $"the teacher says so; saw '{player.Hud.SubtitleText}'");
        Button(MouseButton.Right, false);
        await play.Frames(60);
        Check(restored == 0 && spotted == 1, "the teacher gives the player a moment before the scene goes back");
        await play.Until(() => restored == 1, 2, "the scene must go back to its checkpoint");
        await play.Frames(2);
        Check(spotted == 1 && player.Hud.SubtitleText == "" && Sound("chalk").Playing, "the teacher is back at the board once the scene has gone back");
        checkpoint.Restarting -= player.Hud.ClearText;
    }

    private async Task CheckNotSpotted()
    {
        teacher.Patrol(Route, 100);
        await play.Frames(5);
        Button(MouseButton.Right, true);
        // Behind the teacher, to its side, and too far.
        foreach (var feet in new Vector3[] { new(-2, .02f, -1.5f), new(1, .02f, 0), new(-2, .02f, 4.9f) })
        {
            player.Position = feet;
            await play.Frames(10);
            Check(spotted == 0, $"a player at {feet} is not in the teacher's view");
        }
        // Control: the same spot, but near enough and in front.
        player.Position = new(-2, .02f, 3);
        await play.Frames(5);
        Check(spotted == 1, "a player in view is seen from the same distance with the prism up");
        Button(MouseButton.Right, false);
    }

    private async Task CheckBehindWall()
    {
        teacher.Patrol([new(-2, .02f, -4.7f), new(-2, .02f, -4.95f)], 100);
        player.Position = new(-2, .02f, -2.7f);
        Button(MouseButton.Right, true);
        await play.Frames(10);
        Check(spotted == 0, "a teacher does not see through a wall");
        Button(MouseButton.Right, false);
    }

    private async Task CheckOtherSection()
    {
        teacher.Patrol(Route, 100);
        player.Position = new(-2, .02f, 3);
        Button(MouseButton.Right, true);
        await play.Frames(10);
        Check(body.Section == "Ⅱ" && spotted == 0, "a teacher does not see a player in another section");
        Button(MouseButton.Right, false);
    }
}
