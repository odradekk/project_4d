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
    private const string ArmsFull = "手里抱着猫呢。";
    private float pitch;
    private bool pointerCaptured;
    private PrismView? prism;
    private PlayerActions? actions;
    private SeamTravel? travel;
    private bool captureBeforeNotebook;
    private Interactable3D? target;
    private string? aimedEntity;
    internal bool PointerCaptured => pointerCaptured;
    internal bool PrismRaised => prism?.Raised ?? false;

    /// <summary>What E does while both arms hold something, such as a cat; null when they are free.</summary>
    internal Action? PutDown { get; set; }

    /// <summary>Whether the hand holds an object.</summary>
    internal bool HoldsItem => actions is { Holding: true };
    public GameHud Hud { get; private set; } = null!;
    public Camera3D Camera => GetNode<Camera3D>("Eyes");

    internal void Bind(PrismView view, PlayerActions hands, SeamTravel seams)
    {
        prism = view;
        actions = hands;
        travel = seams;
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
            if (key.Keycode == Key.E && pointerCaptured && GodotObject.IsInstanceValid(target))
            {
                target!.Interact();
                GetViewport().SetInputAsHandled();
                return;
            }
            // E uses what the crosshair is on, else lets go of what is in the hand, else puts down what is in the arms,
            // else takes what the crosshair is on; the keys shown follow the same order.
            if (key.Keycode == Key.E && pointerCaptured && actions is { Holding: true })
            {
                SayRefusal(actions.Use());
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.E && pointerCaptured && PutDown is { } putDown)
            {
                putDown();
                GetViewport().SetInputAsHandled();
                return;
            }
            if (key.Keycode == Key.E && pointerCaptured && actions is not null && aimedEntity is not null)
            {
                SayRefusal(actions.Use());
                GetViewport().SetInputAsHandled();
                return;
            }
            var slot = key.Keycode - Key.Key1;
            if (slot is >= 0 and < Backpack.Slots && pointerCaptured && travel is not null)
            {
                if (PutDown is not null) Say(ArmsFull);
                else SayTakingOut(travel.TakeOut((int)slot));
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
            if (button.ButtonIndex == MouseButton.Left && button.Pressed && travel is not null)
                SayCrossing(travel.Cross());
            if (button.ButtonIndex == MouseButton.Right) prism?.SetRaised(button.Pressed);
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelDown) prism?.MoveTarget(1);
            if (button.Pressed && button.ButtonIndex == MouseButton.WheelUp) prism?.MoveTarget(-1);
        }
        else if (input is InputEventMouseMotion motion && pointerCaptured)
        {
            RotateY(-motion.ScreenRelative.X * LookSensitivity * ScreenSide);
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
            var query = PhysicsRayQueryParameters3D.Create(from, from - Camera.GlobalBasis.Z * 2f, ~PhysicsLayers.Characters);
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
        var direction = Basis * new Vector3(input.X * ScreenSide, 0, input.Y);
        var speed = Input.IsActionPressed("run") ? RunSpeed : WalkSpeed;
        var velocity = direction * speed;
        velocity.Y = IsOnFloor() ? 0 : Velocity.Y - 9.8f * (float)delta;
        Velocity = velocity;
        MoveAndSlide();
    }

    // The prompt of what the crosshair is on if it can be used, else the keys that act on a seam at her feet and on what is
    // held or aimed at.
    private string HintText()
    {
        if (target is not null) return $"E  {target.Prompt}";
        var keys = new List<string>();
        if (travel is { CanCross: true }) keys.Add("左键  穿缝");
        keys.AddRange(HandKeys());
        return string.Join("    ", keys);
    }

    private List<string> HandKeys()
    {
        var keys = new List<string>();
        var holding = actions is { Holding: true };
        if (PutDown is not null && !holding) keys.Add("E  放下");
        if (actions is null || (!holding && (aimedEntity is null || !actions.IsHoldable(aimedEntity)))) return keys;
        var upright = holding ? actions.HeldIsUpright : actions.IsUpright(aimedEntity!);
        if (holding) keys.Add(upright ? "E  松手" : "E  放下");
        else if (PutDown is null) keys.Add(upright ? "E  抓住" : "E  拿起");
        if (prism is { Enabled: true })
        {
            keys.Add(upright ? "F  放倒" : "F  竖起");
            if (!upright) keys.Add("R  翻面");
        }
        return keys;
    }

    // The side of the screen her body feels as left: it is reversed while she is a mirror image, so turning and
    // stepping aside go the way the flipped picture shows.
    private float ScreenSide => actions is { Mirrored: true } ? -1 : 1;

    private void SayRefusal(Refusal? refusal) =>
        Say(refusal is null ? null : RefusalLines.For(refusal, actions!.CurrentSection));

    private void SayCrossing(Refusal? refusal) =>
        Say(refusal is null ? null : RefusalLines.ForCrossing(refusal, actions!.CurrentSection));

    private void SayTakingOut(Refusal? refusal) => Say(refusal is null ? null : RefusalLines.ForTakingOut(refusal));

    private void Say(string? line)
    {
        if (line is not null) Hud.Say("小满", line, 2.5);
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
