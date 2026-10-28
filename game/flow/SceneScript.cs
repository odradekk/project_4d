using System;
using System.Collections.Generic;
using Game.Characters;
using Game.UI;

namespace Game.Flow;

/// <summary>A scene's script as a queue of steps, and the lines of the scene as steps. A step is done when it returns true.</summary>
internal sealed class SceneScript(GameHud hud)
{
    private readonly Queue<Func<double, bool>> steps = new();

    internal void Do(Action run) => steps.Enqueue(_ =>
    {
        run();
        return true;
    });

    internal void Wait(double seconds)
    {
        var left = seconds;
        steps.Enqueue(delta => (left -= delta) <= 0);
    }

    /// <summary>Waits for something that other parts of the game do.</summary>
    internal void Until(Func<bool> condition) => steps.Enqueue(_ => condition());

    /// <summary>A step of its own, given the time of the frame; a step may queue the steps that follow it.</summary>
    internal void Step(Func<double, bool> step) => steps.Enqueue(step);

    /// <summary>A line of the narration or of Xiaoman, for as long as it shows.</summary>
    internal void Narrate(string speaker, string line, double seconds)
    {
        Do(() => hud.Say(speaker, line, seconds));
        Wait(seconds);
    }

    /// <summary>A line of a character, for as long as it shows.</summary>
    internal void Say(Character3D who, string line, double seconds)
    {
        Do(() => who.Say(line, seconds));
        Wait(seconds);
    }

    /// <summary>Drops the steps that have not run yet.</summary>
    internal void Clear() => steps.Clear();

    internal void Tick(double delta)
    {
        if (steps.Count > 0 && steps.Peek()(delta)) steps.Dequeue();
    }
}
