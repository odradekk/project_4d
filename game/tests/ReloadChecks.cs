using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Flow;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// A region that is freed and loaded again loads, also when the GC has collected the C# objects of its resources and not yet
/// finalized them, which is when Godot loses track of a resource that the next load takes back from its cache (see
/// <see cref="Scenes"/>). The check holds the .NET finalizer thread over that time, and reloads the room the way the prologue
/// does when scene 8 or 9 starts again, through <see cref="HomeScene.Open"/>, and the test region the way the checks do, through
/// <see cref="TurningFixture.Load"/>. Each path loads a scene of its own, so that neither finds its scene kept by the other.
/// </summary>
public partial class ReloadChecks : Node
{
    public override async void _Ready()
    {
        try
        {
            var home = HomeScene.Open(this, path: HomeScene.RoomPath);
            await WithoutFinalizers(() => Free(home.Region), () =>
            {
                home = HomeScene.Open(this, path: HomeScene.RoomPath);
                return Task.CompletedTask;
            });
            await Free(home.Region);
            var play = new TurningFixture(this);
            await play.Load();
            await WithoutFinalizers(play.Unload, () => (play = new TurningFixture(this)).Load());
            await play.Unload();
            GD.Print("Reload checks passed: the room through the home scene and the test region through the fixture load again while the GC's collections wait to be finalized.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
    }

    private async Task Free(Region3D region)
    {
        RemoveChild(region);
        region.Free();
        // The audio thread fades a stopped sound out before it lets go of it, which takes time and not frames.
        await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
    }

    // Frees a region and loads it again, with what the GC collects after the region is freed left unfinalized until the load is done.
    private static async Task WithoutFinalizers(Func<Task> free, Func<Task> load)
    {
        // What is garbage already, such as the C# object of a scene that its loader did not keep, is finalized first.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Stall.Entered.Reset();
        Stall.Released.Reset();
        MakeStall();
        GC.Collect();
        try
        {
            Check(Stall.Entered.Wait(TimeSpan.FromSeconds(10)), "the finalizer thread must reach the stall");
            await free();
            GC.Collect();
            await load();
        }
        finally
        {
            Stall.Released.Set();
        }
        GC.WaitForPendingFinalizers();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MakeStall() => _ = new Stall();

    // Its finalizer holds the finalizer thread until Released is set.
    private sealed class Stall
    {
        internal static readonly ManualResetEventSlim Entered = new(), Released = new();

        ~Stall()
        {
            Entered.Set();
            Released.Wait();
        }
    }
}
