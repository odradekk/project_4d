using System;
using System.Linq;
using Godot;

namespace Game.Regions;

/// <summary>
/// The campus's city: shops along the main road's east side, facing it, and around the school blocks of mostly 4–7
/// storeys with a few towers. This is the greybox prototype's city code, in the campus's coordinates, started from the
/// prototype's random state at that point, so every block stands where the prototype's does. The node sits at y = 0, the
/// top of the ground.
/// Decision note: 2026-11-01-school-scene-layout
/// </summary>
public partial class City3D
{
    private const uint Window = 0x6fa8e0ff, Front = 0x5f6870ff;  // the prototype's window, and its glass shop front

    /// <summary>Builds the city around the campus instead of the compound's.</summary>
    [Export] public bool Campus { get; set; }

    private void CampusCity()
    {
        (seed, fx, fz) = (320957053, 100, -5);  // the prototype's random state after the unfinished frames, and its centre
        // Three blocks of flats over shops, to the main road's ends at z ±700: a front and a sign every 4 m along the road.
        foreach (var (z0, z1) in new[] { (-700.0, -42.0), (-30, 70), (82, 700) })
        {
            const double x0 = 266.2, x1 = 278.2;  // 6 m east of the main road
            Box(x0, x1, 0, 3.6 + 5 * 2.9 + .6, z0, z1, Wall2);
            for (var z = z0 + .5; z + 4 <= z1 - .5; z += 4)
            {
                Box(x0 - .04, x0, 0, 2.8, z + .3, z + 3.7, Rand() < .5 ? Shutter : Front);
                Box(x0 - .25, x0, 3, 3.5, z + .15, z + 3.85, Sign);
            }
            Windows(x0, x1, z0, z1, 5, 2.9, 3.6, "nsw");
        }
        // A block in most cells of a 44 m grid, clear of the site, the dirt road and the main road; windows within 330 m.
        (double West, double East, double North, double South)[] reserved = [(-135, 285, -112, 130), (-22, 3, -720, 92), (235, 285, -720, 720)];
        for (var x = -1000.0; x < 1400; x += 44)
            for (var z = -520.0; z < 660; z += 44)
            {
                if (Rand() < .12) continue;
                double w = 14 + Rand() * 20, d = 11 + Rand() * 18, x0 = x + Rand() * (40 - w), z0 = z + Rand() * (40 - d);
                var floors = 4 + (int)Math.Floor(Rand() * 4) + (Rand() < .15 ? 10 : 0);
                if (reserved.Any(r => x0 < r.East && x0 + w > r.West && z0 < r.South && z0 + d > r.North)) continue;
                Box(x0, x0 + w, 0, floors * 3, z0, z0 + d, Rand() < .5 ? Wall : Wall2);
                if (double.Hypot(x0 - fx, z0 - fz) < 330) Windows(x0, x0 + w, z0, z0 + d, floors, 3, 0, "nsew");
            }
        // Not in the prototype, which leaves bare ground in view: its grid keeps clear 55 m and more west of the sports
        // ground's fence (x −80.3), and it has no block west of the mall's west forecourt or behind the northern gap in the
        // shops. Six-storey blocks fill those places. They draw no numbers, so the mountains still are the prototype's.
        (double West, double East, double North, double South)[] added =
        [
            (-100, -88, -106, -64), (-100, -88, -56, -14), (-100, -88, -6, 36), (-100, -88, 44, 84),  // 8 m past the fence
            (-130, -116, -106, -82), (-130, -116, -74, -34), (-130, -116, -26, 14), (-130, -116, 22, 62), (-130, -116, 70, 88),
            (-152, -138, 60, 118),  // west of the forecourt
            (290, 318, -56, -18),  // behind the gap
        ];
        for (var i = 0; i < added.Length; i++)
        {
            var b = added[i];
            Box(b.West, b.East, 0, 18, b.North, b.South, i % 2 == 0 ? Wall : Wall2);
            Windows(b.West, b.East, b.North, b.South, 6, 3, 0, "nsew");
        }
    }

    // The prototype's windows(): on each of the sides, as many as fit 3.3 m apart, centred, on every storey from y0; each
    // 1.5 m wide and 1.4 m tall, 0.9 m over the storey's floor, standing 6 cm out.
    private void Windows(double x0, double x1, double z0, double z1, int floors, double storey, double y0, string sides)
    {
        foreach (var side in sides)
        {
            var (alongX, at, w0, w1) = side switch
            {
                'n' => (true, z0, -.06, 0.0), 's' => (true, z1, 0.0, .06), 'w' => (false, x0, -.06, 0.0), _ => (false, x1, 0.0, .06)
            };
            var (a0, a1) = alongX ? (x0, x1) : (z0, z1);
            var n = (int)Math.Floor((a1 - a0) / 3.3);
            var pad = (a1 - a0 - n * 3.3) / 2;
            for (var f = 0; f < floors; f++)
                for (var i = 0; i < n; i++)
                {
                    double c = a0 + pad + (i + .5) * 3.3, lo = y0 + f * storey + .9;
                    Along(alongX, at, c - .75, c + .75, lo, lo + 1.4, w0, w1, Window);
                }
        }
    }
}
