using Godot;

namespace Game.Flow;

// What her arms and the stool do in the room: the cabinet is out of reach from the floor, and the stool is climbed with Space.
public partial class CatRoom
{
    // She reaches this far above her feet.
    private const float ArmReach = 1.9f;
    // She is on something when her feet are this high, and the stool is climbed from this near.
    private const float OnSomething = .2f;
    private const float StoolReach = 1;
    private const float StepOff = .5f;

    public override void _Input(InputEvent input)
    {
        if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.E } || home.Region.Actions is not { Aimed: { } device } actions
            || !actions.IsDevice(device)) return;
        // E on a device that is out of reach from where she stands does nothing but say so.
        if (ask.Bounds(device, home.Region.CurrentSection) is { } box && box.Position.Y > home.Player.Position.Y + ArmReach)
        {
            Say("小满", 3, "太高了，够不着。得踩着什么才行。");
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space } && home.Player.PointerCaptured) ToggleStool();
    }

    // Space climbs onto the stool lying near her, or steps off whatever she stands on.
    private void ToggleStool()
    {
        var player = home.Player;
        if (player.Position.Y > OnSomething)
        {
            var ahead = -player.Basis.Z;
            player.Position += new Vector3(ahead.X, 0, ahead.Z).Normalized() * StepOff;
            return;
        }
        if (home.Region.Actions!.Holding || player.PutDown is not null)
        {
            Say("小满", 2, "手里拿着东西呢。");
            return;
        }
        if (!ask.IsResting("stool") || ask.Bounds("stool", home.Region.CurrentSection, "seat") is not { } seat) return;
        var middle = seat.GetCenter();
        if (Flat(middle - player.Position).Length() > StoolReach) return;
        player.Position = new(middle.X, seat.End.Y + .03f, middle.Z);
        player.Velocity = Vector3.Zero;
    }
}
