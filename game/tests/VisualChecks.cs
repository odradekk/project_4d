using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Player;
using Godot;

namespace Game.Tests;

// Run with --scene res://tests/visual_checks.tscn -- prism|main|disabled|empty|scale2|outline*|upright-outline.
public partial class VisualChecks : Node
{
    public override async void _Ready()
    {
        var mode = OS.GetCmdlineUserArgs().FirstOrDefault() ?? "prism";
        var region = GD.Load<PackedScene>("res://regions/section_test.tscn").Instantiate<Region3D>();
        if (mode == "empty")
        {
            region.GetNode("Sections/Ⅱ").Free();
            region.GetNode("Sections/Ⅲ").Free();
        }
        if (mode == "disabled") region.PrismEnabled = false;
        if (mode == "scale2") GetWindow().ContentScaleFactor = 2;
        if (mode == "outline-many")
        {
            var original = region.GetNode<BuildingElement3D>("Buildings/Home/OnlyII");
            var parent = original.GetParent();
            for (var i = 0; i < 256; i++)
            {
                var copy = (BuildingElement3D)original.Duplicate();
                copy.Name = $"Copy{i:D3}";
                copy.EntityId = $"copy{i:D3}";
                copy.Position = i is >= 249 and <= 251 ? new Vector3(-1.5f + (i - 249) * .8f, 1.1f, -1) : new Vector3(100 + i * 2, .5f, -1);
                parent.AddChild(copy);
            }
            original.Free();
        }
        if (mode == "outline-multipart" || mode == "outline-acute")
        {
            var lower = region.GetNode<BuildingElement3D>("Buildings/Home/PlatformIILower");
            var upper = region.GetNode<BuildingElement3D>("Buildings/Home/PlatformIIUpper");
            var part = (BoxPart3D)upper.GetNode<BoxPart3D>("Part").Duplicate();
            part.Name = "UpperPart";
            part.PartId = "upper";
            part.Position = upper.Position - lower.Position;
            if (mode == "outline-acute") part.Rotation = new Vector3(0, .5f, 0);
            lower.AddChild(part);
            upper.Free();
        }
        if (mode == "outline-occluded")
        {
            var original = region.GetNode<FixedObject3D>("Sections/Ⅰ/Fixed/Cabinet");
            var cover = (FixedObject3D)original.Duplicate();
            cover.Name = "Cover";
            cover.EntityId = "cover";
            cover.Position = new Vector3(0, 1.3f, 1.5f);
            var mesh = (BoxMesh)cover.GetNode<BoxPart3D>("Part").Mesh.Duplicate();
            mesh.Size = new Vector3(10, 10, .2f);
            cover.GetNode<BoxPart3D>("Part").Mesh = mesh;
            region.GetNode("Sections/Ⅱ/Fixed").AddChild(cover);
        }
        AddChild(region);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (mode == "upright-outline")
        {
            await UprightOutline(region);
            return;
        }
        if (mode.StartsWith("outline"))
        {
            var prism = region.GetNode<Game.Rendering.PrismView>("Generated/Prism");
            if (mode != "outline-main")
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
            for (var i = 0; i < 8; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            var image = GetViewport().GetTexture().GetImage();
            if (mode == "outline-many")
            {
                var ids = prism.GetNode<SubViewport>("MaskViewport").GetTexture().GetImage();
                var first = ids.GetPixel(456, 360);
                var second = ids.GetPixel(554, 360);
                var third = ids.GetPixel(652, 360);
                if (first == second || second == third || first == third || first == Colors.Black || second == Colors.Black || third == Colors.Black ||
                    CyanPixels(image, 475, 330, 483, 430) < 50 || CyanPixels(image, 582, 330, 590, 430) < 50)
                {
                    GD.PushError($"CHECK FAILED: 254, 255 and 256 must have distinct visible mask colors: {first}, {second}, {third}");
                    GetTree().Quit(1);
                    return;
                }
                GD.Print($"Mask colors across 254/255/256: {first}, {second}, {third}");
                GetTree().Quit();
                return;
            }
            var orange = CyanPixels(image, 420, 385, 480, 405);
            var green = CyanPixels(image, 530, 438, 750, 452);
            var hidden = mode == "outline-main" || mode == "outline-occluded";
            var seam = CyanPixels(image, 520, 480, 760, 520);
            var corner = CyanPixels(image, 465, 470, 480, 485);
            var acuteSeam = CyanPixels(image, 545, 550, 575, 565);
            if ((hidden ? orange != 0 || green != 0 : orange < 20 || green < 20) ||
                (mode == "outline-multipart" && (seam != 0 || corner < 3)) ||
                (mode == "outline-acute" && acuteSeam != 0))
            {
                GD.PushError($"CHECK FAILED: prism outline {mode}: orange={orange}, green={green}, seam={seam}, corner={corner}, acuteSeam={acuteSeam}");
                GetTree().Quit(1);
                return;
            }
            GD.Print("Prism outline visual check passed.");
            GetTree().Quit();
            return;
        }
        if (mode != "main")
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
    }

    // Stands the pole up from the hand, then looks at its slice in the other section through the lens.
    private async Task UprightOutline(Region3D region)
    {
        var player = region.GetNode<FirstPersonPlayer>("Generated/Player");
        player.Position = new(3f, 0, 1.5f);
        var offset = new Vector3(1.9f, .03125f, 1.5f) - (player.Position + new Vector3(0, 1.3f, 0));
        player.Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
        player.Camera.Rotation = new(Mathf.Atan2(offset.Y, new Vector2(offset.X, offset.Z).Length()), 0, 0);
        await Frames(3);
        Press(Key.E);
        await Frames(3);
        // The pole lies along the view when held, so its hinge, where the slice stands, ends 1.07 m ahead and near the middle of the lens.
        player.Camera.Rotation = new(-.5f, 0, 0);
        await Frames(3);
        Press(Key.F);
        await Frames(100);
        // Let go, so that the preview lines do not add to the outline, and look a little beside the slice where it falls.
        Press(Key.E);
        await Frames(80);
        var slice = ((FourD.EntityReadResult.Found)region.World!.ReadEntity(new("pole"))).Entity.Pose.Position;
        var eye = player.Camera.GlobalPosition;
        var toSlice = new Vector3((float)slice.X, (float)slice.Y, (float)slice.Z) - eye;
        player.Camera.Rotation = new(Mathf.Atan2(toSlice.Y, new Vector2(toSlice.X, toSlice.Z).Length()) + .15f, 0, 0);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true });
        await Frames(10);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        var screen = player.Camera.UnprojectPosition(new((float)slice.X, (float)slice.Y, (float)slice.Z));
        var outline = CyanPixels(image, (int)screen.X - 30, (int)screen.Y - 30, (int)screen.X + 30, (int)screen.Y + 30);
        if (outline < 20)
        {
            GD.PushError($"CHECK FAILED: the upright pole's slice must glow in the lens at {screen}; cyan pixels = {outline}");
            GetTree().Quit(1);
            return;
        }
        GD.Print($"Upright outline visual check passed: {outline} outline pixels around the slice.");
        GetTree().Quit();
    }

    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Press(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
    }

    private static int CyanPixels(Image image, int x0, int y0, int x1, int y1)
    {
        var count = 0;
        for (var y = y0; y < y1; y++)
            for (var x = x0; x < x1; x++)
            {
                var c = image.GetPixel(x, y);
                if (c.B > .7f && c.G > .7f && c.R < .82f && c.B > c.R * 1.1f) count++;
            }
        return count;
    }
}
