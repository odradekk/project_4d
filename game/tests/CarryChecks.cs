using System;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Player;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Carrying a flat object in the hand on the test region: it stops at furniture and at the platform it rests on, a bent
/// object keeps its gaps, and an object left behind is let go.
/// </summary>
public partial class CarryChecks : Node
{
    private TurningFixture play = null!;
    private FirstPersonPlayer player => play.Player;
    private PlayerActions actions => play.Actions;

    public override async void _Ready()
    {
        play = new TurningFixture(this);
        try
        {
            await play.Load();
            await CheckHeldObjectAtFurniture();
            await play.Load();
            await CheckHeldObjectLetGoWhenFar();
            await play.Load(PutRubberOnPlatform);
            await CheckHeldObjectOnPlatform();
            await play.Load(MakePoleAnL);
            await CheckHeldBentObject();
            GD.Print("Carry checks passed: furniture, let go when far, platform, bent object.");
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

    private async Task CheckHeldObjectAtFurniture()
    {
        // The engine leaves a flat object in the hand to the host's physics, so the whole pole must stop at the cabinet face at x = 2.5.
        play.LookAt(new(1.5f, .02f, 2.4f), new(1.5f, .03125f, 1.5f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        // Walk along the cabinet's width toward it. At x = 1.6 the hand is at x = 2.2, clear of the cabinet, but the pole's
        // far end, 0.5 m from its middle, would be at x = 2.7.
        player.Position = new(1.0f, 0, 0);
        player.Rotation = new(0, -Mathf.Pi / 2, 0);
        player.Camera.Rotation = Vector3.Zero;
        await play.Frames(5);
        player.Position = new(1.6f, 0, 0);
        await play.Frames(5);
        var end = play.SliceBounds("Ⅰ", "pole").End.X;
        Check(end <= 2.51f, $"a held object must not be carried into furniture; its end is at x = {end}");

        // Let go beside the cabinet, the pole is not caught in it: aiming at it from the front, it turns.
        Press(Key.E);
        await play.Frames(90);
        play.LookAt(new(2f, .02f, .9f), new(2f, .03125f, 0));
        await play.Frames(2);
        Press(Key.F);
        await play.Frames(3);
        Check(play.State("pole").Rotating, "a pole that was let go beside the cabinet must still turn");
    }

    // The platform spans x -1 to 1, z -0.5 to 0.5 and y 0 to 0.6 in the first section. The rubber, 0.0625 m thick, rests on its top.
    private static void PutRubberOnPlatform(Region3D region) =>
        region.GetNode<TurnableObject3D>("Sections/Ⅰ/Turnables/Rubber").Position = new(.5f, .63125f, 0);

    private async Task CheckHeldObjectOnPlatform()
    {
        // The rubber touches the platform it rests on and the hand is lower, inside the platform. Carried, it must stay out.
        var platform = new Aabb(new(-1, 0, -.5f), new(2, .6f, 1)).Grow(-.01f);
        void CheckOutside(string when) => Check(!platform.Intersects(play.SliceBounds("Ⅰ", "rubber")), $"a held object must not enter what it rests on {when}; at {play.SliceBounds("Ⅰ", "rubber")}");

        play.LookAt(new(.5f, .02f, .8f), new(.5f, .63125f, 0));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(5);
        Check(actions.Held == "rubber", "the rubber on the platform must be picked up");
        CheckOutside("when picked up");

        // Walk off the platform and back with the view level, which brings the hand above the platform.
        for (var step = 0; step < 6; step++)
        {
            player.Position = player.Position.MoveToward(new(.5f, 0, 1.6f), .3f);
            await play.Frames(1);
            CheckOutside("while it moves");
        }
        player.Camera.Rotation = Vector3.Zero;
        await play.Walk(new(.5f, 0, .8f));
        await play.Frames(5);
        CheckOutside("when it comes back");
        Press(Key.E);
        await play.Frames(60);
        Check(play.Position("rubber").Y > .6f, $"let go over the platform, the rubber must rest on it; y = {play.Position("rubber").Y}");
    }

    // The pole gets a foot at its +x end that reaches 0.5 m toward +z, making an L, and a small post stands at x = 1, z = 1.8,
    // y = 1, level with the hand. The foot ends at the pole's own extent along x, so the hinge and reach stay valid.
    private static void MakePoleAnL(Region3D region)
    {
        var pole = region.GetNode<TurnableObject3D>("Sections/Ⅰ/Turnables/Pole");
        var foot = (BoxPart3D)pole.GetNode<BoxPart3D>("Part").Duplicate();
        foot.Name = "Foot";
        foot.PartId = "foot";
        foot.Mesh = new BoxMesh { Size = new(.125f, .0625f, .5f) };
        foot.Position = new(.4375f, 0, .28125f);
        pole.AddChild(foot);

        var post = (FixedObject3D)region.GetNode<FixedObject3D>("Sections/Ⅰ/Fixed/Cabinet").Duplicate();
        post.Name = "Bollard";
        post.EntityId = "bollard";
        post.Position = new(1f, 1f, 1.8f);
        var part = post.GetNode<BoxPart3D>("Part");
        part.Mesh = new BoxMesh { Size = new(.1f, .2f, .1f) };
        region.GetNode("Sections/Ⅰ/Fixed").AddChild(post);
    }

    private async Task CheckHeldBentObject()
    {
        // Carried at x = 2.2 with the foot to one side, the L is drawn toward the post, which ends up in the L's inner corner.
        // The hull of the whole L would be stopped well before, at the pole's end near x = 1.1; its parts let the post in.
        play.LookAt(new(1.5f, .02f, 2.4f), new(1.5f, .03125f, 1.5f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        player.Camera.Rotation = Vector3.Zero;
        player.Rotation = new(0, Mathf.Pi / 2, 0);
        await play.Walk(new(2.8f, 0, 1.5f));
        await play.Walk(new(1.1f, 0, 1.5f));
        await play.Frames(5);
        // The foot's face at x_middle + 0.375 meets the post's face at x = 1.05 when the middle is at x = 0.675.
        var middle = play.Position("pole").X;
        Check(middle is > .5f and < .8f, $"the L must slide its inner corner over the post; its middle stopped at x = {middle}");
    }

    private async Task CheckHeldObjectLetGoWhenFar()
    {
        // A turn takes the object from the hand's side, so the player can walk off during it.
        play.LookAt(new(1.5f, .02f, 2.4f), new(1.5f, .03125f, 1.5f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        Press(Key.F);
        await play.Frames(10);
        player.Position = new(-3, 0, 2.4f);
        await play.Frames(TurnFrames);
        Check(actions.Held is null, "an object left more than an arm's length behind must be let go");
        Check(play.State("pole").Holder is null, "the engine must not keep it held");
    }
}
