using System;
using System.Linq;
using System.Threading.Tasks;
using Game.Adapter;
using Game.Rendering;
using Godot;
using static Game.Tests.TurningFixture;

namespace Game.Tests;

/// <summary>
/// Pixel checks of the lens, in a window: run with --scene res://tests/seam_visual_checks.tscn -- lens|band|flip.
/// The player stands at (1.5, 0, 3) facing the mirror seam at (1.5, 0, 1.5), 1.5 m away, in the middle of the view.
/// A red post stands in the first section and a blue one in the second, both to the right of the seam.
/// </summary>
public partial class SeamVisualChecks : Node
{
    private const int Width = 1280;
    private const int Height = 720;
    private TurningFixture play = null!;
    private Region3D region => play.Region;

    public override async void _Ready()
    {
        var mode = OS.GetCmdlineUserArgs().FirstOrDefault() ?? "lens";
        play = new TurningFixture(this);
        try
        {
            await play.Load(AddPosts, "res://regions/seam_test.tscn");
            play.LookAt(new(1.5f, 0, 3f), new(1.5f, 1.32f, 1.5f));
            // The views are set by hand below, and not by the player's state.
            region.Travel!.SetPhysicsProcess(false);
            await (mode switch { "lens" => Lens(), "band" => Band(), "flip" => Flip(), _ => throw new InvalidOperationException($"No mode {mode}.") });
            GD.Print($"Seam visual check passed: {mode}.");
            GetTree().Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"CHECK FAILED: {error.Message}");
            GetTree().Quit(1);
        }
        finally
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    // A thin post 0.1 m wide and 1.4 m high, 0.05 to 0.15 m right of the mirror seam for the second section, and 0.4 to 0.5 m for the first.
    private static void AddPosts(Region3D region)
    {
        Post(region, "red", new(1.95f, .7f, 1.5f), new(.1f, 1.4f, .1f), new(.9f, .1f, .1f), wMin: -1, wMax: .2);
        Post(region, "blue", new(1.95f, .7f, 1.5f), new(.1f, 1.4f, .1f), new(.1f, .1f, .9f), wMin: .2, wMax: 1);
        Post(region, "near", new(1.6f, .7f, 1.5f), new(.1f, 1.4f, .1f), new(.9f, .1f, .1f), wMin: .2, wMax: 1);
    }

    private static void Post(Region3D region, string id, Vector3 position, Vector3 size, Color color, double wMin, double wMax)
    {
        var post = (BuildingElement3D)region.GetNode<BuildingElement3D>("Buildings/Home/Floor").Duplicate();
        post.Name = id;
        post.EntityId = id;
        post.WMin = wMin;
        post.WMax = wMax;
        post.Position = position;
        post.GetNode<BoxPart3D>("Part").Mesh = new BoxMesh { Size = size, Material = new StandardMaterial3D { AlbedoColor = color } };
        region.GetNode("Buildings/Home").AddChild(post);
    }

    private async Task<Image> Capture()
    {
        await play.Frames(3);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        return GetViewport().GetTexture().GetImage();
    }

    // The mean column of the pixels in the box for which <paramref name="matches"/> holds, and how many there are.
    private static (int Count, float Column) Centroid(Image image, Rect2I box, Func<Color, bool> matches)
    {
        long count = 0, columns = 0;
        for (var y = box.Position.Y; y < box.End.Y; y++)
            for (var x = box.Position.X; x < box.End.X; x++)
                if (matches(image.GetPixel(x, y)))
                {
                    count++;
                    columns += x;
                }
        return ((int)count, count == 0 ? 0 : (float)columns / count);
    }

    private static bool Red(Color c) => c.R > .4f && c.R > c.G * 2.5f && c.R > c.B * 2.5f;
    private static bool Blue(Color c) => c.B > .4f && c.B > c.R * 2.5f && c.B > c.G * 2.5f;
    private static bool SeamLight(Color c) => c.R > .85f && c.G > .8f && c.B is > .4f and < .75f;
    private static readonly Rect2I Strip = new(Width / 2 - 20, 250, 40, 220);
    // Inside the lens circle, which is 230 px in radius: 170 px each side of the middle and 140 px above and below it.
    private static readonly Rect2I InLens = new(470, 220, 340, 280);

    // Looking at the plain seam through the lens shows its light, and without the lens nothing of it is seen.
    private async Task Lens()
    {
        play.LookAt(new(-1.5f, 0, 3f), new(-1.5f, 1.32f, 1.5f));
        var main = await Capture();
        Check(Centroid(main, Strip, SeamLight).Count == 0, "the seam must not be seen without the lens");
        Button(MouseButton.Right, true);
        var lens = await Capture();
        var (count, column) = Centroid(lens, Strip, SeamLight);
        Check(count > 100, $"the seam's thin light must be seen in the lens; {count} light pixels");
        Check(Mathf.Abs(column - Width / 2) < 6, $"and at the seam's own place, in the middle of the view; at column {column}");
    }

    // The picture inside the band is turned about the seam's light: the post to the right of the seam shows to the left.
    private async Task Band()
    {
        Button(MouseButton.Right, true);
        var outside = Centroid(await Capture(), InLens, Blue);
        var near = Centroid(await Capture(), InLens, Red);
        Check(near.Count > 100 && near.Column < Width / 2 - 10, $"a post just right of the mirror seam shows to its left in the band; column {near.Column}, {near.Count} pixels");
        Check(outside.Count > 100 && outside.Column > Width / 2 + 100, $"the blue post outside the band is not turned; column {outside.Column}, {outside.Count} pixels");
        var prism = region.GetNode<PrismView>("Generated/Prism");
        prism.MirrorBands = null;
        var plain = Centroid(await Capture(), InLens, Red);
        Check(plain.Count > 100 && plain.Column > Width / 2 + 10, $"without the band the same post is to the right of the seam; column {plain.Column}");
    }

    // Both the view and the lens are turned over left to right, and what the interface draws is not.
    private async Task Flip()
    {
        var mirror = region.GetNode<MirrorView>("Generated/Travel/Mirror");
        var prism = region.GetNode<PrismView>("Generated/Prism");
        var before = await Capture();
        var red = Centroid(before, new(0, 0, Width, Height), Red);
        Check(red.Count > 100 && red.Column > Width / 2 + 50, $"the red post is to the right of the middle; column {red.Column}");
        var help = Centroid(before, new(20, 20, 400, 160), c => c.R > .9f && c.G > .9f && c.B > .9f);

        mirror.Flipped = true;
        var after = await Capture();
        var flipped = Centroid(after, new(0, 0, Width, Height), Red);
        Check(Mathf.Abs(flipped.Column - (Width - red.Column)) < 4, $"flipped, it is as far to the left as it was to the right; column {flipped.Column}");
        var text = Centroid(after, new(20, 20, 400, 160), c => c.R > .9f && c.G > .9f && c.B > .9f);
        Check(Mathf.Abs(text.Count - help.Count) < help.Count / 5 + 20, $"the help text is not flipped; {text.Count} white pixels, were {help.Count}");

        Button(MouseButton.Right, true);
        mirror.Flipped = false;
        var lens = Centroid(await Capture(), InLens, Blue);
        prism.Flipped = true;
        mirror.Flipped = true;
        var turned = Centroid(await Capture(), InLens, Blue);
        Check(lens.Count > 100 && Mathf.Abs(turned.Column - (Width - lens.Column)) < 4 && Mathf.Abs(turned.Count - lens.Count) < 20,
            $"the lens turns over with the view; the blue post was at column {lens.Column} and is at {turned.Column}");
    }
}
