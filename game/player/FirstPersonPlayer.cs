using Godot;
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
    private bool captureBeforeNotebook;
    private Interactable3D? target;
    internal bool PointerCaptured => pointerCaptured;
    public GameHud Hud { get; private set; } = null!;
    public Camera3D Camera => GetNode<Camera3D>("Eyes");

    internal void Bind(PrismView view) => prism = view;

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
        if (pointerCaptured && !Hud.NotebookOpen)
        {
            var from = Camera.GlobalPosition;
            var query = PhysicsRayQueryParameters3D.Create(from, from - Camera.GlobalBasis.Z * 2f);
            query.Exclude = [GetRid()];
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count > 0) target = hit["collider"].AsGodotObject() as Interactable3D;
        }
        Hud.SetHint(target is null ? "" : $"E  {target.Prompt}");
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
