using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FourD;
using Game.Flow;
using Game.Player;
using Godot;
using static Game.Tests.CharacterFixture;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>What the checks of the school scenes share: the lines that are said, aiming, turns, and waits at a faster pace.</summary>
internal sealed class SchoolFixture(TurningFixture play)
{
    // How much faster than real time the long waits of the scenes run.
    private const float Speed = 8;

    private readonly List<string> said = [];

    internal SchoolRoom Room { get; set; } = null!;
    internal FirstPersonPlayer Player => Room.Player;

    /// <summary>Starts the list of lines again, for a scene that is loaded anew.</summary>
    internal void Forget() => said.Clear();

    /// <summary>Notes the line on the screen if it is a new one. Call it every frame.</summary>
    internal void Listen()
    {
        if (Room is null || !GodotObject.IsInstanceValid(Room.Player)) return;
        var text = Room.Player.Hud.SubtitleText;
        if (text != "" && (said.Count == 0 || said[^1] != text)) said.Add(text);
    }

    internal bool Heard(string part) => said.Any(line => line.Contains(part));

    internal Vector3 Where(string entity)
    {
        var position = Room.State(entity).Pose.Position;
        return new((float)position.X, (float)position.Y, (float)position.Z);
    }

    /// <summary>Turns her view to a point. The eyes are a little in front of her feet, so it takes a second turn to be right close up.</summary>
    internal void Aim(Vector3 target)
    {
        for (var turn = 0; turn < 2; turn++)
        {
            var offset = target - Player.Camera.GlobalPosition;
            Player.Rotation = new(0, Mathf.Atan2(-offset.X, -offset.Z), 0);
            Player.Camera.Rotation = new(Mathf.Atan2(offset.Y, new Vector2(offset.X, offset.Z).Length()), 0, 0);
        }
    }

    /// <summary>Puts her feet at a place and turns her view to a point, once she has settled on the floor.</summary>
    internal async Task Stand(Vector3 feet, Vector3 target)
    {
        Player.Position = feet;
        Player.Velocity = Vector3.Zero;
        await play.Frames(3);
        Aim(target);
        await play.Frames(2);
    }

    /// <summary>Moves her to a point half a metre a frame, as a walk. The way there must be free of desks.</summary>
    internal async Task Walk(Vector3 feet)
    {
        for (var frame = 0; Player.Position.DistanceTo(feet) > .01f; frame++)
        {
            Check(frame < 60, $"she must get to {feet}; she is at {Player.Position}");
            Player.Position = Player.Position.MoveToward(feet, .5f);
            await play.Frames(1);
        }
    }

    internal async Task Fast(Func<bool> condition, double seconds, string message)
    {
        Engine.TimeScale = Speed;
        try
        {
            await play.Until(condition, seconds, message);
        }
        finally
        {
            Engine.TimeScale = 1;
        }
    }

    /// <summary>Makes a turn through the engine, as the keys do, and waits for it to end.</summary>
    internal async Task Turn(RotationRequest request)
    {
        var world = Room.Region.World!;
        var plan = world.PlanRotation(request) as RotationPlanResult.Allowed;
        Check(plan is not null, $"{request} must be allowed");
        Check(world.StartRotation(plan!.Plan) is StartRotationResult.Started, $"{request} must start");
        await play.Until(() => !Room.State(request.Entity.Value).Rotating, 4, $"{request} must end");
        // The scenes look at what she has done from frame to frame; a turn that is over is seen to be over.
        await play.Frames(3);
    }

    /// <summary>The tricks of scene 4 up to the window, made through the engine.</summary>
    internal async Task DoTheTricks()
    {
        var actor = new EntityId(SchoolRoom.Actor);
        await Turn(new Flip(actor, new("eraser")));
        await Turn(new StandUp(actor, new("pencilDesk"), new("Ⅱ")));
        await Turn(new LayDown(actor, new("pencilDesk")));
    }
}
