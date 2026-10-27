using System;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Characters;
using Game.Player;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// The cat and a schoolmate on the test region: they are loaded from their spawns, reported to the engine, and
/// do what a scene script tells them to.
/// </summary>
public partial class CharacterChecks : Node
{
    private static readonly Vector3 CatSpawn = new(-3, .02f, 1);
    private static readonly Vector3 CabinetTop = new(2.9f, 1.2f, .2f);

    private TurningFixture play = null!;
    private FirstPersonPlayer player => play.Player;
    private Character3D catBody => play.Region.Character("cat");
    private Cat cat => catBody.GetNode<Cat>("Cat");
    private Character3D mate => play.Region.Character("mate");

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load(AddCharacters);
            await CheckSpawns();
            await CheckGoTo();
            await CheckWander();
            await CheckFollow();
            await CheckFollowAround();
            await CheckChase();
            await CheckJump();
            await CheckPerchSitAndStare();
            await CheckFlee();
            await CheckSchoolmate();
            GD.Print("Character checks passed: spawns, sync, go to, wander, follow, chase, jump, perch, sit, stare, flee, schoolmates.");
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

    private static void AddCharacters(Region3D region)
    {
        AddCat(region, "cat", CatSpawn);
        AddSchoolmate(region, "mate", new(-3, .02f, -1), "同桌");
        AddSchoolmate(region, "extra", new(-3.5f, .02f, -1));
    }

    private async Task CheckSpawns()
    {
        Check(catBody.GetParent() == play.Region.GetNode("Generated/Ⅰ"), "a character stands in the physics world of its section");
        Check(Apart(catBody.Position, CatSpawn) < .01f, "a character starts at its spawn");
        var state = play.State("cat");
        Check(state.Section == new SectionId("Ⅰ") && state.Pose.Position.W == 0, "the engine has the character in the spawn's section");
        Check(catBody.Section == "Ⅰ" && !catBody.Mirrored, "a character reads its section and its handedness from the engine");
        Check(Apart(player.Position, CatSpawn) > 2, "the player's own spawn is still the player");
        // The ray of the crosshair stops at a character, but nothing that walks does.
        Check(catBody.CollisionLayer == 2 && player.CollisionMask == 1, "characters are on a layer that the player does not collide with");
        await play.Frames(1);
    }

    private async Task CheckGoTo()
    {
        var finished = 0;
        void Finished() => finished++;
        catBody.ActionFinished += Finished;
        var goal = new Vector3(-3, 0, 3);
        catBody.GoTo(goal);
        Check(catBody.Busy, "a character is busy while it goes somewhere");
        var walking = await FramesToFinish();
        Check(Apart(catBody.Position, goal) < .15f, $"the character must arrive; it is at {catBody.Position}");
        Check(finished == 1, "ActionFinished follows one arrival");
        catBody.ActionFinished -= Finished;
        await play.Frames(2);
        var engine = play.State("cat");
        Near(new((float)engine.Pose.Position.X, (float)engine.Pose.Position.Y, (float)engine.Pose.Position.Z), catBody.Position, "the engine has the character where it is");
        Check(Math.Abs(engine.ActorYaw!.Value - catBody.Rotation.Y) < .001, "the engine has the way the character faces");
        Check(Forward(catBody).Dot(Vector3.Back) > .99f, "walking to a point faces it");
        catBody.GoTo(CatSpawn, run: true);
        var running = await FramesToFinish();
        Check(Apart(catBody.Position, CatSpawn) < .15f, "the character must run to a point too");
        Check(running < walking * .6f, $"running covers the same ground faster; it took {running} frames against {walking}");
    }

    private async Task<int> FramesToFinish()
    {
        var frames = 0;
        while (catBody.Busy)
        {
            Check(frames++ < 10 * 60, "the character must finish what it was told to do");
            await play.Frames(1);
        }
        return frames;
    }

    private async Task CheckWander()
    {
        var center = new Vector3(-3, 0, 1);
        var farthest = 0f;
        var walked = 0f;
        var last = catBody.Position;
        cat.Wander(center, 1.5f);
        for (var frame = 0; frame < 12 * 60; frame++)
        {
            await play.Frames(1);
            farthest = Mathf.Max(farthest, Apart(catBody.Position, center));
            walked += Apart(catBody.Position, last);
            last = catBody.Position;
        }
        Check(farthest <= 1.6f, $"a wandering cat stays within its radius; it went {farthest} m from the center");
        Check(walked > .5f, $"a wandering cat walks about; it walked {walked} m");
        Check(catBody.Busy, "wandering goes on until the cat is told something else");
    }

    private async Task CheckFollow()
    {
        player.Position = new(0, .02f, 3);
        cat.Follow();
        await play.Until(() => Apart(catBody.Position, player.Position) < 1.05f, 8, "a following cat must come to the player");
        await play.Frames(20);
        var rest = catBody.Position;
        await play.Frames(30);
        Check(Apart(catBody.Position, rest) < .01f, "a following cat stays where it is while it is near the player");
        player.Position = new(3, .02f, 4);
        await play.Until(() => Apart(catBody.Position, player.Position) < 1.2f, 8, "a following cat must keep up with the player");
        Check(catBody.Busy, "following goes on until the cat is told something else");
    }

    private async Task CheckFollowAround()
    {
        // The cabinet stands between the cat and the player. A cat goes straight and stays at the cabinet, unless it is given a way round.
        catBody.PlaceAt(new(1.8f, .02f, 0), 0);
        player.Position = new(4.5f, .02f, 0);
        cat.Follow();
        await play.Frames(6 * 60);
        Check(catBody.Position.X < 2.6f, $"a cat that follows goes straight and does not get round a cabinet; it is at {catBody.Position}");
        var passed = false;
        cat.Follow(goal =>
        {
            passed |= catBody.Position.Z > 1.2f;
            return passed ? goal : new Vector3(3.2f, 0, 1.5f);
        });
        await play.Until(() => Apart(catBody.Position, player.Position) < 1.05f, 10, "a cat that follows by the points it is given gets round the cabinet");
    }

    private async Task CheckChase()
    {
        // The engine's slice of the rubber is what the cat runs after, wherever the rubber is moved.
        var rubber = new EntityId("rubber");
        var expected = new Vector3(.5f, .03125f, 1.5f);
        Near(cat.SliceCenter("rubber", "body")!.Value, expected, "the cat reads the middle of a slice from the engine");
        Check(cat.SliceCenter("rubber", "missing") is null && cat.SliceCenter("nothing", "body") is null, "a slice that is not there has no middle");
        cat.Chase(() => cat.SliceCenter("rubber", "body"));
        await play.Until(() => Apart(catBody.Position, expected) < .5f, 8, "a chasing cat must reach its target");
        var pose = play.State("rubber").Pose;
        var moved = new Vector3(-3.5f, .03125f, 1.5f);
        Check(play.Region.World!.PlaceEntity(rubber, pose with { Position = new(moved.X, moved.Y, moved.Z, 0) }) is UpdateResult.Updated, "the target is moved");
        await play.Until(() => Apart(catBody.Position, moved) < .5f, 8, "a chasing cat must follow a target that moves");
        Check(catBody.Busy, "chasing goes on until the cat is told something else");
    }

    private async Task CheckJump()
    {
        catBody.PlaceAt(new(1.8f, .02f, .2f), 0);
        await play.Frames(3);
        var apex = 0f;
        cat.JumpTo(CabinetTop);
        while (catBody.Busy)
        {
            await play.Frames(1);
            apex = Mathf.Max(apex, catBody.Position.Y);
        }
        Check(Apart(catBody.Position, CabinetTop) < .1f && Mathf.Abs(catBody.Position.Y - CabinetTop.Y) < .05f, $"the cat must land where it jumped to; it is at {catBody.Position}");
        Check(apex > CabinetTop.Y + .3f, $"the cat jumps over the edge of what it climbs onto; its highest point was {apex}");
        await play.Frames(60);
        Check(Mathf.Abs(catBody.Position.Y - CabinetTop.Y) < .05f, "the cat stays on the cabinet once it has landed");
    }

    private async Task CheckPerchSitAndStare()
    {
        catBody.PlaceAt(new(1.8f, .02f, .2f), 0);
        await play.Frames(3);
        cat.Perch(CabinetTop);
        await play.UntilIdle(catBody, 4);
        Check(catBody.Posture == Posture.Lying && Mathf.Abs(catBody.Position.Y - CabinetTop.Y) < .05f, "a perched cat lies on the high place");
        await play.Frames(2);
        Check(catBody.GetNode<Node3D>("Visual").Scale.Y < .5f, "a lying cat is flat");

        var corner = new Vector3(0, 0, -3);
        cat.Sit(corner);
        await play.Frames(2);
        var toCorner = (Flat(corner - catBody.Position)).Normalized();
        Check(catBody.Posture == Posture.Sitting && Forward(catBody).Dot(toCorner) > .99f && !catBody.Busy, "a sitting cat faces the point");
        Check(Mathf.Abs(catBody.GetNode<Node3D>("Visual").Scale.Y - .6f) < .01f, "a sitting cat is lower");

        cat.Stare(corner);
        catBody.Rotation = new(0, 2, 0);
        await play.Frames(2);
        Check(catBody.Posture == Posture.Sitting && Forward(catBody).Dot(toCorner) > .99f && catBody.Busy, "a staring cat keeps its eyes on the point, sitting as it was");

        var watched = new Vector3(3, 0, 3);
        cat.Stare(() => watched);
        await play.Frames(2);
        Check(Forward(catBody).Dot(Flat(watched - catBody.Position).Normalized()) > .99f, "a staring cat looks at a target");
        watched = new Vector3(-3, 0, 3);
        await play.Frames(2);
        Check(Forward(catBody).Dot(Flat(watched - catBody.Position).Normalized()) > .99f, "a staring cat turns its eyes with a target that moves");
    }

    private async Task CheckFlee()
    {
        catBody.PlaceAt(CatSpawn, 0);
        await play.Frames(3);
        var scare = new Vector3(-2, 0, 1);
        cat.Flee(scare);
        await play.UntilIdle(catBody, 4);
        Check(Mathf.Abs(Apart(catBody.Position, scare) - 4) < .2f && catBody.Position.X < CatSpawn.X, $"a scared cat runs away from what scared it; it is at {catBody.Position}");
    }

    private async Task CheckSchoolmate()
    {
        mate.Sit();
        await play.Frames(2);
        Check(mate.Posture == Posture.Sitting && Mathf.Abs(mate.GetNode<Node3D>("Visual").Scale.Y - .6f) < .01f, "a schoolmate sits");
        mate.Stand();
        mate.GoTo(new(-3, 0, 0));
        await play.UntilIdle(mate, 4);
        Check(mate.Posture == Posture.Standing && Apart(mate.Position, new(-3, 0, 0)) < .15f, "a schoolmate stands and walks to a point");
        mate.Say("你怎么变的？", 2);
        Check(player.Hud.SubtitleText == "同桌：你怎么变的？", $"a schoolmate's line carries the name its spawn gives; saw '{player.Hud.SubtitleText}'");
        player.Hud.ClearText();
        play.Region.Character("extra").Say("好。", 2);
        Check(player.Hud.SubtitleText == "同学：好。", $"a schoolmate whose spawn gives no name has the name of its scene; saw '{player.Hud.SubtitleText}'");
        Check(mate.CrossSeam("none", "Ⅱ") == FailureCode.UnknownId, "a character cannot cross a seam that is not there");
    }
}
