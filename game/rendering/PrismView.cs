using System;
using System.Collections.Generic;
using Godot;

namespace Game.Rendering;

public partial class PrismView : CanvasLayer
{
    private readonly SubViewport viewport = new() { Name = "PrismViewport", RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
    private readonly SubViewport mask = new() { Name = "MaskViewport", UseDebanding = false, UseTaa = false,
        Msaa3D = Viewport.Msaa.Disabled, ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
        RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
    private readonly Camera3D maskCamera = new() { Name = "MaskCamera", CullMask = 4, Current = true,
        Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = Colors.Black,
            TonemapMode = Godot.Environment.ToneMapper.Linear, TonemapExposure = 1 },
        Attributes = new CameraAttributesPractical { ExposureMultiplier = 1, AutoExposureEnabled = false } };
    private readonly Camera3D camera = new() { Name = "PrismCamera", CullMask = 2, Current = true };
    private readonly TextureRect lens = new() { Name = "Lens", MouseFilter = Control.MouseFilterEnum.Ignore };
    private readonly Label label = new() { Name = "PrismTarget", HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
    private readonly Label help = new() { Name = "Controls", MouseFilter = Control.MouseFilterEnum.Ignore };
    private IReadOnlyList<(string Id, World3D World)> targets = [];
    private Camera3D? source;
    private int target;
    private bool enabled;
    private bool requested;
    public bool Raised => lens.Visible;
    public string? TargetSection => targets.Count == 0 ? null : targets[target].Id;

    internal void Configure(Camera3D playerCamera, IReadOnlyList<(string Id, World3D World)> orderedTargets, bool prismEnabled)
    {
        source = playerCamera;
        targets = orderedTargets;
        AddChild(viewport);
        viewport.AddChild(camera);
        AddChild(mask);
        mask.AddChild(maskCamera);
        AddChild(lens);
        lens.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        lens.Texture = viewport.GetTexture();
        lens.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        lens.Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://rendering/prism.gdshader") };
        ((ShaderMaterial)lens.Material).SetShaderParameter("object_mask", mask.GetTexture());
        AddChild(label);
        label.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        label.OffsetLeft = -220; label.OffsetRight = 220;
        AddChild(help);
        help.Position = new Vector2(20, 20);
        SetEnabled(prismEnabled);
        SelectTarget();
        Refresh();
    }

    internal void SetEnabled(bool value)
    {
        enabled = value;
        if (!value) requested = false;
        help.Text = value ? "WASD 移动 · Shift 跑\n鼠标转向 · Esc 释放鼠标\n按住右键观察\n滚轮切换截面"
            : "WASD 移动 · Shift 跑\n鼠标转向 · Esc 释放鼠标";
        Refresh();
    }
    internal void SetRaised(bool value) { requested = value && enabled; Refresh(); }
    internal void MoveTarget(int direction)
    {
        if (!Raised || targets.Count == 0) return;
        target = Math.Clamp(target + direction, 0, targets.Count - 1);
        SelectTarget();
    }
    private void SelectTarget()
    {
        if (targets.Count > 0) viewport.World3D = mask.World3D = targets[target].World;
        label.Text = targets.Count > 0 ? $"截面{targets[target].Id}" + (targets.Count > 1 ? " · 滚轮切换" : "") : "没有可观察的截面";
    }
    private void Refresh()
    {
        var visible = requested && enabled;
        lens.Visible = visible && targets.Count > 0;
        label.Visible = visible;
        help.Visible = !lens.Visible;
        viewport.RenderTargetUpdateMode = mask.RenderTargetUpdateMode = lens.Visible ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
    }
    public override void _Process(double delta)
    {
        if (source is null) return;
        var size = GetViewport().GetTexture().GetSize();
        viewport.Size = mask.Size = new Vector2I(Math.Max(2, (int)size.X), Math.Max(2, (int)size.Y));
        ((ShaderMaterial)lens.Material).SetShaderParameter("viewport_size", size);
        var logicalSize = GetViewport().GetVisibleRect().Size;
        label.OffsetTop = .32f * Math.Min(logicalSize.X, logicalSize.Y) + 8;
        label.OffsetBottom = label.OffsetTop + 34;
        CopyCamera(camera, source);
        CopyCamera(maskCamera, source);
    }
    private static void CopyCamera(Camera3D camera, Camera3D source)
    {
        camera.GlobalTransform = source.GlobalTransform;
        camera.Projection = source.Projection;
        camera.Fov = source.Fov;
        camera.KeepAspect = source.KeepAspect;
        camera.Near = source.Near;
        camera.Far = source.Far;
        camera.Size = source.Size;
        camera.FrustumOffset = source.FrustumOffset;
        camera.HOffset = source.HOffset;
        camera.VOffset = source.VOffset;
    }
}
