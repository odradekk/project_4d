using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Player;
using Game.Rendering;
using Godot;

namespace Game.Tests;

public partial class RegionChecks : Node
{
    private Region3D region = null!;
    public override async void _Ready()
    {
        try
        {
            region = GD.Load<PackedScene>("res://regions/section_test.tscn").Instantiate<Region3D>();
            AddChild(region);
            Check(region.Loaded, "a valid region must load and generate section worlds");
            await Frames(3);
            CheckWorlds();
            await CheckInputAndPrism();
            CheckReader();
            await CheckTargets();
            GD.Print("Region checks passed: load, geometry, physics isolation, movement, prism, targets, diagnostics.");
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
            Input.ActionRelease("run");
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    private void CheckWorlds()
    {
        var first = region.GetNode<SubViewport>("Generated/Ⅰ").World3D;
        var second = region.GetNode<SubViewport>("Generated/Ⅱ").World3D;
        Check(first.Space != second.Space && first.Scenario != second.Scenario, "sections must isolate physics and rendering");
        Check(GetViewport().FindWorld3D() == first, "the spawn section must be the active world");
        foreach (var world in new[] { first, second })
        {
            Check(Hit(world, new(0, 1.5f, 3), new(0, 1.5f, -5)) == "SharedWall", "shared wall must collide in both sections");
            Check(Hit(world, new(-4, 1, 3), new(-4, -1, 3)) == "Floor", "generated floors must be collidable");
        }
        Check(Hit(first, new(-2, .5f, 3), new(-2, .5f, -3)) is null, "II-only geometry must not block I");
        Check(Hit(second, new(-2, .5f, 3), new(-2, .5f, -3)) == "OnlyII", "II-only geometry must block II");
        Check(Hit(first, new(3, .6f, 3), new(3, .6f, -2)) == "cabinet", "fixed furniture must load in its section");
        Check(Hit(second, new(3, .6f, 3), new(3, .6f, -2)) is null, "fixed furniture must not leak into another section");
        Check(Hit(first, new(0, 1, .4f), new(0, .1f, .4f)) == "PlatformI", "I must show the original platform shape");
        Check(Hit(second, new(0, 1, .4f), new(0, .1f, .4f)) == "PlatformIILower", "II must show the lower step in the same place");
        var surface = region.GetNode<MeshInstance3D>("Generated/Ⅰ/Geometry/SharedWall/Surface");
        var arrays = surface.Mesh.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var normals = arrays[(int)Mesh.ArrayType.Normal].AsVector3Array();
        for (var i = 0; i < vertices.Length; i += 3)
            Check((vertices[i + 1] - vertices[i]).Cross(vertices[i + 2] - vertices[i]).Dot(normals[i]) < 0,
                "Godot triangles must face clockwise with outward normals");
        var uv = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
        var frontCorners = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            if (normals[i].Dot(Vector3.Back) < .99f) continue;
            frontCorners++;
            var expected = vertices[i].X < 0
                ? (vertices[i].Y < 1 ? new Vector2(-5, -1.5f) : new Vector2(-5, 1.5f))
                : (vertices[i].Y < 1 ? new Vector2(5, -1.5f) : new Vector2(5, 1.5f));
            Check(uv[i].IsEqualApprox(expected), "vertex reordering must keep each corner's UV aligned");
        }
        Check(frontCorners == 6, "the UV fixture must inspect both front-face triangles");
        Check(!region.GetNode<BoxPart3D>("Buildings/Home/SharedWall/Part").Visible, "authored boxes must not duplicate generated geometry");
    }

    private async Task CheckInputAndPrism()
    {
        var player = region.GetNode<FirstPersonPlayer>("Generated/Player");
        var prism = region.GetNode<PrismView>("Generated/Prism");
        var camera = prism.GetNode<Camera3D>("PrismViewport/PrismCamera");
        var viewport = prism.GetNode<SubViewport>("PrismViewport");
        var mask = prism.GetNode<SubViewport>("MaskViewport");
        var maskCamera = mask.GetNode<Camera3D>("MaskCamera");
        Input.MouseMode = Input.MouseModeEnum.Captured;
        foreach (var (key, direction) in new[] { (Key.W, Vector3.Forward), (Key.A, Vector3.Left), (Key.S, Vector3.Back), (Key.D, Vector3.Right) })
        {
            player.Position = new(0, 0, 3);
            var start = player.Position;
            PhysicalKey(key, true); await Frames(8); PhysicalKey(key, false);
            var travelled = player.Position - start;
            Check(travelled.Dot(direction) > .1f && Math.Abs(travelled.Dot(direction.Rotated(Vector3.Up, Mathf.Pi / 2))) < .01f,
                $"physical {key} must move in its mapped direction");
        }
        player.Position = new(0, 0, 3);
        var before = player.Position;
        PhysicalKey(Key.W, true);
        await Frames(10);
        PhysicalKey(Key.W, false);
        var walk = before.DistanceTo(player.Position);
        before = player.Position;
        PhysicalKey(Key.W, true); PhysicalKey(Key.Shift, true);
        await Frames(10);
        PhysicalKey(Key.W, false); PhysicalKey(Key.Shift, false);
        Check(walk > 0 && before.DistanceTo(player.Position) > walk * 1.3f, $"Shift must run faster than walking (walk={walk}, run={before.DistanceTo(player.Position)}, capture={Input.MouseMode})");
        Check(Mathf.Abs(player.Camera.GlobalPosition.Y - 1.3f) < .015f, "settled eye height must be 1.3 metres above the floor");
        var original = player.Camera.GlobalTransform;
        Input.ParseInputEvent(new InputEventMouseMotion { Relative = new(30, -15), ScreenRelative = new(30, -15) });
        await Frames(2);
        Check(!player.Camera.GlobalTransform.IsEqualApprox(original), "mouse motion must change the view");
        Button(MouseButton.Right, true);
        await Frames(2);
        Check(prism.Raised && prism.TargetSection == "Ⅱ", "holding RMB must show the other section");
        Check(!prism.GetNode<Label>("Controls").Visible, "the raised lens must not be covered by control help");
        Check(camera.GlobalTransform.IsEqualApprox(player.Camera.GlobalTransform), "the prism camera must follow the same viewpoint");
        Check(camera.GetCameraProjection() == player.Camera.GetCameraProjection(), "the prism must use the same projection");
        Check(viewport.World3D == region.GetNode<SubViewport>("Generated/Ⅱ").World3D, "the prism must render the target's world");
        Check(viewport.Size == (Vector2I)GetViewport().GetTexture().GetSize(), "the prism must render at full viewport resolution");
        Check(mask.World3D == viewport.World3D && mask.Size == viewport.Size &&
            maskCamera.GlobalTransform.IsEqualApprox(camera.GlobalTransform) && maskCamera.GetCameraProjection() == camera.GetCameraProjection(),
            "the outline mask must follow the prism world, size, viewpoint and projection");
        Button(MouseButton.Right, false); await Frames(1);
        Check(!prism.Raised, "releasing RMB must lower the prism");
        Check(prism.GetNode<Label>("Controls").Visible, "lowering the lens must restore control help");
        Button(MouseButton.Right, true); await Frames(1);
        player.Notification((int)NotificationApplicationFocusOut);
        Check(!prism.Raised && Input.MouseMode == Input.MouseModeEnum.Visible, "losing focus must lower the prism and release the pointer");
        Button(MouseButton.Left, true); Button(MouseButton.Right, true); await Frames(1);
        region.PrismEnabled = false;
        Check(!prism.Raised, "a scene script must be able to disable an already raised prism");
        Button(MouseButton.Right, true); await Frames(1);
        Check(!prism.Raised, "disabled prism must ignore RMB");
        region.PrismEnabled = true;
        Button(MouseButton.Right, true); await Frames(1);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, Pressed = true }); await Frames(1);
        Check(!prism.Raised && Input.MouseMode == Input.MouseModeEnum.Visible, "Esc must release capture and lower the prism");
        Button(MouseButton.Left, true);
        player.Position = new(-2, 0, 2);
        player.Rotation = Vector3.Zero;
        Input.ActionPress("move_forward"); Input.ActionPress("run"); await Frames(65);
        Input.ActionRelease("move_forward"); Input.ActionRelease("run");
        Check(player.Position.Z < -.7f, "movement must pass through geometry that exists only in II");
        await Frames(40);
        Check(player.Position.Y > -.02f, "the player must stay on the generated floor");
    }

    private void CheckReader()
    {
        var part = region.GetNode<BoxPart3D>("Buildings/Home/SharedWall/Part");
        var cabinet = region.GetNode<FixedObject3D>("Sections/Ⅰ/Fixed/Cabinet");
        part.Scale = new Vector3(2, 1, 1);
        cabinet.EntityId = "SharedWall";
        var reader = new SceneReader(region);
        var result = reader.Load();
        Check(result is FourD.LoadResult.Invalid invalid &&
            invalid.Errors.Any(e => e.Source.NodePath.Contains("SharedWall/Part") && e.Source.Property == "Transform") &&
            invalid.Errors.Any(e => e.Code == FourD.LoadErrorCode.DuplicateId),
            "format errors must not hide independent engine errors");
        part.Scale = Vector3.One;
        cabinet.EntityId = "cabinet";
        var wall = region.GetNode<BuildingElement3D>("Buildings/Home/SharedWall");
        var faces = part.FaceUvs;
        var originalMesh = part.Mesh;
        var invalidBox = (BoxMesh)originalMesh.Duplicate();
        invalidBox.Size = new Vector3(.001f, 3, .2f);
        part.Mesh = invalidBox;
        part.FaceUvs = [];
        wall.WMin = 2;
        var locations = new SceneReader(region).Load();
        Check(locations is FourD.LoadResult.Invalid errors &&
            errors.Errors.Any(e => e.Source.NodePath == "Buildings/Home/SharedWall/Part" && e.Source.Property == "FaceUvs") &&
            errors.Errors.Any(e => e.Source.NodePath == "Buildings/Home/SharedWall/Part" && e.Source.Property == "Mesh") &&
            errors.Errors.Any(e => e.Source.NodePath == "Buildings/Home/SharedWall" && e.Source.Property == "WMin"),
            $"engine diagnostics must identify the authored UVs, box size and range; got {string.Join(", ", (locations as FourD.LoadResult.Invalid)?.Errors.Select(e => e.Source.ToString()) ?? [])}");
        part.Mesh = originalMesh;
        part.FaceUvs = faces;
        wall.WMin = -1;
        var secondPart = (BoxPart3D)part.Duplicate();
        secondPart.Name = "SecondPart";
        secondPart.PartId = "";
        secondPart.FaceUvs = [];
        wall.AddChild(secondPart);
        wall.BuildingId = "missing";
        wall.Gaps = [new IntervalResource { Min = -2, Max = 0 }];
        var building = region.GetNode<Building3D>("Buildings/Home");
        building.WMax = double.PositiveInfinity;
        var section = region.GetNode<Section3D>("Sections/Ⅱ");
        section.SectionId = "";
        locations = new SceneReader(region).Load();
        Check(locations is FourD.LoadResult.Invalid mapped &&
            mapped.Errors.Any(e => e.Source == new FourD.Source("Buildings/Home/SharedWall/SecondPart", "FaceUvs")) &&
            mapped.Errors.Any(e => e.Source == new FourD.Source("Buildings/Home/SharedWall/SecondPart", "PartId")) &&
            mapped.Errors.Any(e => e.Source == new FourD.Source("Buildings/Home/SharedWall", "BuildingId")) &&
            mapped.Errors.Any(e => e.Source == new FourD.Source("Buildings/Home/SharedWall", "Gaps")) &&
            mapped.Errors.Any(e => e.Source == new FourD.Source("Buildings/Home", "WMax")) &&
            mapped.Errors.Any(e => e.Source == new FourD.Source("Sections/Ⅱ", "SectionId")),
            $"diagnostics must identify each authored node and property; got {string.Join(", ", (locations as FourD.LoadResult.Invalid)?.Errors.Select(e => e.Source.ToString()) ?? [])}");
        secondPart.Free();
        wall.BuildingId = "home";
        wall.Gaps = [];
        building.WMax = 1;
        section.SectionId = "Ⅱ";
        wall.WMax = 2;
        locations = new SceneReader(region).Load();
        Check(locations is FourD.LoadResult.Invalid coverage && coverage.Errors.Any(e =>
            e.Code == FourD.LoadErrorCode.BuildingCoverage && e.Source == new FourD.Source("Buildings/Home/SharedWall", "WMax")),
            "an element extending above its building must identify its WMax");
        wall.WMax = 1;
        var mesh = part.Mesh;
        part.Mesh = null;
        cabinet.EntityId = "SharedWall";
        result = new SceneReader(region).Load();
        Check(result is FourD.LoadResult.Invalid missing && missing.Errors.Count == 2 &&
            missing.Errors.Any(e => e.Source.Property == "Mesh") &&
            missing.Errors.Any(e => e.Code == FourD.LoadErrorCode.DuplicateId),
            "a missing mesh must preserve independent errors without derived geometry errors");
        part.Mesh = mesh;
        cabinet.EntityId = "cabinet";
        cabinet.Rotation = new(.13f, .27f, .19f);
        Check(new SceneReader(region).Load() is FourD.LoadResult.Loaded, "ordinary host rotations must satisfy engine double-precision contracts");
        cabinet.Rotation = Vector3.Zero;
        region.Position = new(10, 0, 0);
        var shifted = (FourD.LoadResult.Loaded)new SceneReader(region).Load();
        var geometry = (FourD.SectionGeometryResult.Geometry)shifted.World.GetSectionGeometry(new("Ⅰ"));
        Check(Math.Abs(geometry.Entities.Single(e => e.Entity.Value == "cabinet").Positions.Where((_, i) => i % 3 == 0).Min() - 2.5) < 1e-6,
            "moving the region root must not change region-relative furniture coordinates through grouping nodes");
        region.Position = Vector3.Zero;
    }

    private async Task CheckTargets()
    {
        RemoveChild(region); region.Free();
        region = GD.Load<PackedScene>("res://regions/section_test.tscn").Instantiate<Region3D>();
        region.GetNode<Section3D>("Sections/Ⅱ").W = .8;
        // Section order is part of the load contract, not the target distance order: III is first and nearest here.
        region.GetNode<Section3D>("Sections/Ⅲ").W = -.3;
        AddChild(region);
        Check(region.Loaded, "the near/far fixture must load");
        await Frames(2);
        var prism = region.GetNode<PrismView>("Generated/Prism");
        Button(MouseButton.Right, true); await Frames(1);
        Check(prism.TargetSection == "Ⅲ", "nearest target must be chosen regardless of W sign");
        Button(MouseButton.WheelDown, true); await Frames(1);
        Check(prism.TargetSection == "Ⅱ", "scroll down must choose the next farther target");
        Check(prism.GetNode<SubViewport>("MaskViewport").World3D == region.GetNode<SubViewport>("Generated/Ⅱ").World3D,
            "switching targets must switch the outline mask world too");
        Button(MouseButton.WheelDown, true); await Frames(1);
        Check(prism.TargetSection == "Ⅱ", "scroll must stop at the far end");
        Button(MouseButton.WheelUp, true); await Frames(1);
        Check(prism.TargetSection == "Ⅲ", "scroll up must choose the nearer target");
        Button(MouseButton.Right, false); Button(MouseButton.WheelDown, true); await Frames(1);
        Check(prism.TargetSection == "Ⅲ", "scrolling with the prism lowered must not change target");
        RemoveChild(region); region.Free();
        region = GD.Load<PackedScene>("res://regions/section_test.tscn").Instantiate<Region3D>();
        region.GetNode("Sections/Ⅱ").Free();
        region.GetNode("Sections/Ⅲ").Free();
        AddChild(region); await Frames(2);
        prism = region.GetNode<PrismView>("Generated/Prism");
        Button(MouseButton.Right, true); await Frames(1);
        Check(!prism.Raised && prism.GetNode<Label>("PrismTarget").Visible && prism.GetNode<Label>("Controls").Visible,
            "no target must show an explanation and control help without rendering a lens");
    }

    private static string? Hit(World3D world, Vector3 from, Vector3 to)
    {
        var hit = world.DirectSpaceState.IntersectRay(PhysicsRayQueryParameters3D.Create(from, to));
        return hit.Count == 0 ? null : hit["collider"].AsGodotObject() is Node node ? node.GetParent().Name.ToString() : null;
    }
    private static void PhysicalKey(Key key, bool pressed) =>
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Pressed = pressed });
    private static void Button(MouseButton button, bool pressed) =>
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = pressed });
    private async Task Frames(int count)
    {
        for (var i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
