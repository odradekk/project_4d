using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Adapter;
using Game.Player;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Plays the turning actions through the player's input on the test region: reading the objects, their slices
/// and collision, hold, stand up, lay down, flip and the choice of target section.
/// </summary>
public partial class TurningChecks : Node
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
            CheckAuthoring();
            await play.Load();
            await CheckSlicesAndCollision();
            await CheckHoldAndTurn();
            await play.Load();
            await CheckFlip();
            await play.Load();
            await CheckHeldFlip();
            await play.Load();
            await CheckTargetSection();
            await play.Load(MakeCrateHeavy);
            await CheckHeavyObject();
            var interactions = 0;
            await play.Load(region => AddHatch(region, () => interactions++));
            await CheckHatchWhileHolding(() => interactions);
            GD.Print("Turning checks passed: authoring, slices, collision, hold, stand up, lay down, flip, target section, heavy object, hatch.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.ActionRelease("move_forward");
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    // An interactable in the middle of the room, like the hatch the player pulls with an object in hand.
    private static void AddHatch(Region3D region, Action pulled)
    {
        var hatch = new Game.Interaction.Interactable3D { Prompt = "拉开", Position = new(1.5f, 1.3f, .6f) };
        hatch.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new(.4f, .4f, .4f) } });
        hatch.Interacted += () => pulled();
        region.AddChild(hatch);
    }

    private async Task CheckHatchWhileHolding(Func<int> pulls)
    {
        play.LookAt(new(1.5f, .02f, 2.4f), new(1.5f, .03125f, 1.5f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        play.LookAt(new(1.5f, .02f, 2.4f), new(1.5f, 1.3f, .6f));
        await play.Frames(2);
        Check(player.Hud.HintText.Contains("拉开"), $"the interactable's prompt must show while holding; saw '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(3);
        Check(pulls() == 1 && actions.Held == "pole", "E on an interactable must use it and keep what is held");
    }

    private static void MakeCrateHeavy(Region3D region)
    {
        region.GetNode<TurnableObject3D>("Sections/Ⅰ/Turnables/Crate").Interaction =
            new TurnableResource { Holdable = false, HorizontalRadius = .25, Hinge = new(-.21875f, 0, 0), Reach = .46875 };
    }

    private async Task CheckHeavyObject()
    {
        // An object that cannot be held offers no actions, and says why when the player tries.
        play.LookAt(new(-2.6f, .02f, .4f), new(-2.6f, .25f, -.9f));
        await play.Frames(2);
        Check(!player.Hud.HintText.Contains("拿起") && !player.Hud.HintText.Contains("竖起"), $"an object that cannot be held must not offer to pick it up; saw '{player.Hud.HintText}'");
        Press(Key.E);
        await play.Frames(3);
        Check(actions.Held is null && player.Hud.SubtitleText == "小满：这个拿不动。", $"trying to take it must be explained; saw '{player.Hud.SubtitleText}'");
    }

    private void CheckAuthoring()
    {
        var pole = play.Region.GetNode<TurnableObject3D>("Sections/Ⅰ/Turnables/Pole");
        var spawn = play.Region.GetNode<CreatureSpawn3D>("Sections/Ⅰ/Spawns/Xiaoman");

        // The pencil of the contract: 0.48 m long, the hinge 0.02 m in from the end, so 0.46 m of reach. An author types
        // these as decimals, which a float holds only approximately and the engine checks to 1e-10 m.
        pole.GetNode<BoxPart3D>("Part").Mesh = new BoxMesh { Size = new(.48f, .0625f, .0625f) };
        pole.Interaction = new TurnableResource { Holdable = true, HorizontalRadius = .24, Hinge = new(-.22f, 0, 0), Reach = .46 };
        Check(LoadErrors().Count == 0, $"a pencil sized in decimals must load; got {string.Join(", ", LoadErrors().Select(e => $"{e.Code} {e.Source.Property}"))}");

        pole.Interaction = null;
        Check(LoadErrors().Any(e => e.Source == new Source("Sections/Ⅰ/Turnables/Pole", "Interaction")), "a turnable object without interaction data must be reported");

        // The hinge 0.01 m in from the end leaves less than 0.02 m of the object beyond it.
        pole.Interaction = new TurnableResource { Holdable = true, HorizontalRadius = .5, Hinge = new(-.49f, 0, 0), Reach = .99 };
        pole.BuildingId = "missing";
        spawn.PassageRadius = 0;
        var errors = LoadErrors();
        Check(errors.Any(e => e.Code == LoadErrorCode.InvalidInteraction && e.Source == new Source("Sections/Ⅰ/Turnables/Pole", "Interaction")),
            "an engine error about the hinge must point at the interaction data");
        Check(errors.Any(e => e.Code == LoadErrorCode.MissingReference && e.Source == new Source("Sections/Ⅰ/Turnables/Pole", "BuildingId")),
            "an engine error about the building must point at the building ID");
        Check(errors.Any(e => e.Source == new Source("Sections/Ⅰ/Spawns/Xiaoman", "PassageRadius")), "an engine error about the passage must point at the spawn");
    }

    private IReadOnlyList<LoadError> LoadErrors() =>
        new SceneReader(play.Region).Load() is LoadResult.Invalid invalid ? invalid.Errors : [];

    private async Task CheckSlicesAndCollision()
    {
        Check(play.HasSlice("Ⅰ", "pole") && !play.HasSlice("Ⅱ", "pole"), "a flat object has a slice only in its own section");
        var first = play.Region.GetNode<SubViewport>("Generated/Ⅰ").World3D;
        var second = play.Region.GetNode<SubViewport>("Generated/Ⅱ").World3D;
        Check(Hit(first, new(1.5f, .5f, 1.5f), new(1.5f, -.5f, 1.5f)) == "pole", "a slice must collide in its section");
        Check(Hit(second, new(1.5f, .5f, 1.5f), new(1.5f, -.5f, 1.5f)) == "Floor", "a flat object must not collide in another section");
        Check(play.Mask("Ⅰ", "pole") == Colors.Black, "a flat object has no glowing outline");

        // The crate's face is at x = -2.35 and the player's body is 0.2 m in radius.
        player.Position = new(-1.5f, .02f, -.9f);
        player.Rotation = new(0, Mathf.Pi / 2, 0);
        Input.ActionPress("move_forward");
        await play.Frames(60);
        Input.ActionRelease("move_forward");
        Check(player.Position.X is > -2.2f and < -2.1f, $"a slice must block the player; stopped at x = {player.Position.X}");
    }

    private async Task CheckHoldAndTurn()
    {
        play.LookAt(new(1.5f, .02f, 2.4f), new(1.5f, .03125f, 1.5f));
        await play.Frames(2);
        var hint = player.Hud.HintText;
        Check(hint.Contains("拿起") && hint.Contains("竖起") && hint.Contains("翻面"), $"aiming at a flat object must offer its actions; saw '{hint}'");
        Press(Key.E);
        await play.Frames(3);
        Check(actions.Held == "pole" && play.State("pole").Holder == new EntityId("xiaoman"), "E must pick up the aimed object");
        Check(player.Hud.HintText.Contains("放下"), "holding a flat object must offer putting it down");

        // The hand is 0.6 m ahead of the body, at 1.0 + 0.7 sin(pitch) above the floor the player stands on.
        player.Camera.Rotation = new(-Mathf.Pi / 6, 0, 0);
        await play.Frames(3);
        Near(play.Position("pole"), new(1.5f, .65f, 1.8f), "a held object must follow the hand");
        player.Camera.Rotation = new(Mathf.Pi / 6, 0, 0);
        await play.Frames(3);
        Near(play.Position("pole"), new(1.5f, 1.35f, 1.8f), "looking up must raise the held object");
        player.Camera.Rotation = Vector3.Zero;
        await play.Frames(3);
        Near(play.Position("pole"), new(1.5f, 1.0f, 1.8f), "a held object must stay in the hand when the view returns");

        play.LookAt(new(1.5f, .02f, 2.4f), new(.5f, .03125f, 1.5f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        Check(actions.Held is null && play.State("rubber").Holder is null, "E with a full hand must let go, not take a second object");
        await play.Frames(60);
        // The pole followed the hand to where the view turned, 0.6 m ahead of the body toward the rubber.
        var dropped = play.Position("pole");
        Near(dropped, new(1.054f, .03125f, 1.9986f), "a released object falls onto the floor");

        // Stand the pole up from the hand toward the other section.
        play.LookAt(new(dropped.X, .02f, dropped.Z + .9f), dropped);
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(2);
        player.Camera.Rotation = new(-Mathf.Pi / 6, 0, 0);
        await play.Frames(2);
        Press(Key.F);
        await play.Frames(10);
        var early = play.SliceBounds("Ⅰ", "pole");
        await play.Frames(1);
        Check(play.State("pole").Rotating && !early.IsEqualApprox(play.SliceBounds("Ⅰ", "pole")), "the slice must change on every frame of a turn");
        await play.Frames(TurnFrames);
        var upright = play.State("pole");
        Check(upright.UprightOrigin == new SectionId("Ⅰ") && !upright.Rotating && actions.Held == "pole", "the pole must end upright and still held");
        Check(play.HasSlice("Ⅰ", "pole") && play.HasSlice("Ⅱ", "pole"), "an upright object has a slice in every section it reaches");
        var here = play.SliceBounds("Ⅰ", "pole");
        var there = play.SliceBounds("Ⅱ", "pole");
        Check(Mathf.IsEqualApprox(here.Size.X, .012f) && Mathf.IsEqualApprox(here.Size.Y, .0625f) && Mathf.IsEqualApprox(here.Size.Z, .0625f),
            $"a slice of the upright pole is a plate 12 mm thick; got {here.Size}");
        Check(here.GetCenter().DistanceTo(there.GetCenter()) < .001f, "the slices of an upright object lie at the same place in each section");
        Check(play.Mask("Ⅰ", "pole") != Colors.Black && play.Mask("Ⅱ", "pole") != Colors.Black, "an upright object glows in the lens");
        Check(play.Mask("Ⅱ", "pole") != play.Mask("Ⅱ", "Floor"), "each outlined object has its own color in the mask");
        // The pole was held at its middle, 0.6 m ahead of the body, and its hinge end is 0.46875 m from there along x.
        Near(play.Position("pole"), new(dropped.X - .46875f, play.Position("pole").Y, dropped.Z + .3f), "a turn must leave the slice where it ends, not move it back to the hand");
        Check(player.Hud.HintText.Contains("松手") && player.Hud.HintText.Contains("放倒"), "holding a slice must offer letting go and laying down");

        // Moving the hand moves every slice together, with the view as well as the body.
        var hereBefore = here.GetCenter();
        var thereBefore = there.GetCenter();
        player.Position += new Vector3(0, 0, -.4f);
        player.Camera.Rotation = new(Mathf.Pi / 6, 0, 0);
        await play.Frames(3);
        var moved = new Vector3(0, .7f, -.4f);
        Near(play.SliceBounds("Ⅰ", "pole").GetCenter() - hereBefore, moved, "the slice in this section must follow the hand");
        Near(play.SliceBounds("Ⅱ", "pole").GetCenter() - thereBefore, moved, "the slice in the other section must move with it");

        // Laying it down brings the slices back to one flat object in the section it was stood up from.
        Press(Key.F);
        await play.Frames(TurnFrames);
        var laid = play.State("pole");
        Check(laid.UprightOrigin is null && laid.Section == new SectionId("Ⅰ") && !play.HasSlice("Ⅱ", "pole"), "laying down must bring the object back to its section");
        Press(Key.E);
        await play.Frames(90);
        Check(actions.Held is null && play.Position("pole").Y < .04f, "a laid pole drops onto the floor when let go");
    }

    private async Task CheckFlip()
    {
        play.LookAt(new(.5f, .02f, 2.4f), new(.5f, .03125f, 1.5f));
        await play.Frames(2);
        var lettering = "Generated/Ⅰ/Dynamic/rubber/Mark";
        var text = play.Region.GetNode<Label3D>(lettering);
        Check(text.GlobalBasis.Determinant() > 0 && text.GlobalBasis.Z.Y > .99f, "the lettering starts upright and readable");

        play.Region.PrismEnabled = false;
        Press(Key.R);
        await play.Frames(3);
        Check(!play.State("rubber").Rotating, "turning needs the prism");
        play.Region.PrismEnabled = true;

        Press(Key.R);
        await play.Frames(10);
        Check(play.State("rubber").Rotating && !play.Region.HasNode(lettering), "the lettering is hidden while the object turns");
        await play.Frames(TurnFrames);
        var flipped = play.State("rubber");
        Check(flipped.Handedness == Handedness.Mirrored && !flipped.Rotating && flipped.Section == new SectionId("Ⅰ"),
            "a flip must leave the object in its section with the opposite handedness");
        text = play.Region.GetNode<Label3D>(lettering);
        Check(text.GlobalBasis.Determinant() < 0 && text.GlobalBasis.Z.Y > .99f, "after a flip the lettering is a mirror image, still facing up");
        Press(Key.R);
        await play.Frames(TurnFrames);
        Check(play.State("rubber").Handedness == Handedness.Normal && play.Region.GetNode<Label3D>(lettering).GlobalBasis.Determinant() > 0,
            "a second flip restores the lettering");
    }

    private async Task CheckHeldFlip()
    {
        // Held east of its middle, the pole sweeps through the other section when it flips, and must show there while it turns.
        play.LookAt(new(3f, .02f, 1.5f), new(1.9f, .03125f, 1.5f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        Press(Key.R);
        var showedInOtherSection = false;
        for (var frame = 0; frame < TurnFrames; frame++)
        {
            await play.Frames(1);
            showedInOtherSection |= play.HasSlice("Ⅱ", "pole");
        }
        Check(showedInOtherSection, "a held object that turns into the other section must show there");
        Check(play.State("pole").Handedness == Handedness.Mirrored && !play.HasSlice("Ⅱ", "pole") && actions.Held == "pole", "after the flip the pole is flat and still held");
    }

    private async Task CheckTargetSection()
    {
        // The lens starts on the nearest section, II at W = 0.42; scrolling moves it to III at W = -0.6, shallower than the
        // first section. Standing up turns toward the target.
        play.LookAt(new(1.5f, .02f, 2.4f), new(1.5f, .03125f, 1.5f));
        await play.Frames(2);
        Press(Key.E);
        await play.Frames(3);
        Button(MouseButton.Right, true);
        await play.Frames(3);
        Press(Key.F);
        await play.Frames(TurnFrames);
        Check(play.State("pole").Pose.Position.W > 0 && play.HasSlice("Ⅱ", "pole") && !play.HasSlice("Ⅲ", "pole"), "a deeper target must stand the pole up toward +W");
        Press(Key.F);
        await play.Frames(TurnFrames);
        Check(play.State("pole").Section == new SectionId("Ⅰ"), "the pole lies down again in its section");
        Button(MouseButton.WheelDown, true);
        await play.Frames(3);
        Press(Key.F);
        await play.Frames(TurnFrames);
        Check(play.State("pole").Pose.Position.W < 0 && play.HasSlice("Ⅲ", "pole") && play.HasSlice("Ⅰ", "pole") && !play.HasSlice("Ⅱ", "pole"), "a shallower target must stand the pole up toward -W");
    }

    private static string? Hit(World3D world, Vector3 from, Vector3 to)
    {
        var hit = world.DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
        return hit.Count == 0 ? null : hit["collider"].AsGodotObject() is Node node ? node.GetParent().Name.ToString() : null;
    }
}
