using System;
using System.Collections.Generic;

namespace Game.Flow;

/// <summary>A scene's script as a queue of steps. A step is done when it returns true.</summary>
internal sealed class SceneScript
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

    internal void Tick(double delta)
    {
        if (steps.Count > 0 && steps.Peek()(delta)) steps.Dequeue();
    }
}
