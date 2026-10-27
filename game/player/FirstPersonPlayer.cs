using System;
using System.Collections.Generic;
using Godot;
using Game.Adapter;
using Game.Rendering;
using Game.Interaction;
using Game.UI;

namespace Game.Player;

public partial class FirstPersonPlayer : CharacterBody3D
{
    private const float WalkSpeed = 2.2f;
    private const float RunSpeed = 3.8f;
    private const float LookSensitivity = 0.0025f;
    private const float PitchLimit = 1.5f;
    private float pitch;
    private bool pointerCaptured;
    private PrismView? prism;
    private PlayerActions? actions;
    private bool captureBeforeNotebook;
    private Interactable3D? target;
    private string? aimedEntity;
    internal bool PointerCaptured => pointerCaptured;
    internal bool PrismRaised => prism?.Raised ?? false;

    /// <summary>What E does while both arms hold something, such as a cat; null when they are free.</summary>
    internal Action? PutDown { get; set; }
    public GameHud Hud { get; private set; } = null!;
    public Camera3D Camera => GetNode<Camera3D>("Eyes");

    internal void Bind(PrismView view, PlayerActions hands)
    {
        prism = view;
        actions = hands;
    }

    public override void _Ready()
    {
        Hud = new GameHud { Name = "GameHud" };
        AddChild(Hud);
        CapturePointer();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (Hud.NotebookOpen)
            {
                if (key.Keycode is Key.N or Key.E or Key.Escape)
                {
                    Hud.CloseNotebook();
                    if (captureBeforeNotebook) CapturePointer();
                }
                else if (key.Keycode == Key.Left) Hud.TurnPage(-1);
                else if (key.Keycode == Key.Right) Hud.TurnPage(1);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.N && Hud.OpenNotebook())
            {
                captureBeforeNotebook = pointerCaptured;
                ReleasePointer();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.E && pointerCaptured && PutDown is { } putDown)
            {
                putDown();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.E && pointerCaptured && GodotObject.IsInstanceValid(target))
            {
                target!.Interact();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.E && pointerCaptured && actions is not null && (actions.Holding || aimedEntity is not null))
            {
                SayRefusal(actions.Use());
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode is Key.F or Key.R && pointerCaptured && actions is not null && prism is { Enabled: true })
            {
                SayRefusal(key.Keycode == Key.F ? actions.StandOrLay() : actions.Flip());
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            ReleasePointer();
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseButton button)
        {
            if (Hud.NotebookOpen) return;
            if (button.ButtonIndex == MouseButton.Left && button.Pressed)
                CapturePointer();
            if (!pointerCaptured) return;
            if (button.ButtonIndex == MouseButton.Right) prism?.SetRaised(button.Pressed);
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown) prism?.MoveTarget(1);
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp) prism?.MoveTarget(-1);
        }
        else if (input is InputEventMouseMotion motion && pointerCaptured)
        {
            RotateY(-motion.ScreenRelative.X * LookSensitivity);
            pitch = Mathf.Clamp(pitch - motion.ScreenRelative.Y * LookSensitivity, -PitchLimit, PitchLimit);
            Camera.Rotation = new Vector3(pitch, 0, 0);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        target = null;
        aimedEntity = null;
        if (pointerCaptured && !Hud.NotebookOpen)
        {
            var from = Camera.GlobalPosition;
            var query = PhysicsRayQueryParameters3D.Create(from, from - Camera.GlobalBasis.Z * 2f);
            query.Exclude = [GetRid()];
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count > 0)
            {
                var collider = hit["collider"].AsGodotObject();
                target = collider as Interactable3D;
                aimedEntity = (collider as EntityBody3D)?.Entity;
            }
        }
        actions?.Aim(aimedEntity);
        Hud.SetHint(HintText());
        if (prism is not null) prism.GetNode<Label>("Controls").Visible = !prism.Raised && Hud.HintText == "" &&
            (!Hud.SubtitleVisible || Hud.SubtitleText == "");
        var input = pointerCaptured && !Hud.NotebookOpen
            ? Input.GetVector("move_left", "move_right", "move_forward", "move_back") : Vector2.Zero;
        var direction = Basis * new Vector3(input.X, 0, input.Y);
        var speed = Input.IsActionPressed("run") ? RunSpeed : WalkSpeed;
        var velocity = direction * speed;
        velocity.Y = IsOnFloor() ? 0 : Velocity.Y - 9.8f * (float)delta;
        Velocity = velocity;
        MoveAndSlide();
    }

    // The prompt of what the crosshair is on if it can be used, else the keys that act on what is held or aimed at.
    private string HintText()
    {
        if (PutDown is not null) return "E  放下";
        if (target is not null) return $"E  {target.Prompt}";
        if (actions is null || (!actions.Holding && (aimedEntity is null || !actions.IsHoldable(aimedEntity)))) return "";
        var upright = actions.Holding ? actions.HeldIsUpright : actions.IsUpright(aimedEntity!);
        var keys = new List<string>();
        if (actions.Holding) keys.Add(upright ? "E  松手" : "E  放下");
        else keys.Add(upright ? "E  抓住" : "E  拿起");
        if (prism is { Enabled: true })
        {
            keys.Add(upright ? "F  放倒" : "F  竖起");
            if (!upright) keys.Add("R  翻面");
        }
        return string.Join("    ", keys);
    }

    private void SayRefusal(Refusal? refusal)
    {
        if (refusal is not null && RefusalLines.For(refusal, actions!.CurrentSection) is { } line) Hud.Say("小满", line, 2.5);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut) { captureBeforeNotebook = false; ReleasePointer(); }
    }

    private void CapturePointer()
    {
        pointerCaptured = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void ReleasePointer()
    {
        pointerCaptured = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        prism?.SetRaised(false);
    }
}
