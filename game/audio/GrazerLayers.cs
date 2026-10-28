using System;

namespace Game.Audio;

/// <summary>How far each layer of the grazer's music is in, each from 0 to 1.</summary>
/// <param name="Bass">The low drone under everything.</param>
/// <param name="Rhythm">The pulse and the scraping, which come faster and louder the nearer the grazer is.</param>
/// <param name="Duck">How far the other ambient sounds are pushed down.</param>
public readonly record struct GrazerLevels(double Bass, double Rhythm, double Duck);

/// <summary>
/// The distances, in metres of four-dimensional distance from the player to the body origin of the grazer, at which
/// the layers of its music come in. Playtests set them.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public static class GrazerLayers
{
    /// <summary>The bass comes in at this distance and is fully in at <see cref="NearDistance"/>.</summary>
    public const double FarDistance = 12;

    /// <summary>The pulse and the scraping come in at this distance and are fully in at <see cref="DangerDistance"/>.</summary>
    public const double NearDistance = 7;

    /// <summary>Inside this distance the other ambient sounds are pushed down, fully after <see cref="DuckWidth"/> more.</summary>
    public const double DangerDistance = 3.5;

    public const double DuckWidth = 1.5;

    public static GrazerLevels At(double distance) => new(
        Ramp(FarDistance - distance, FarDistance - NearDistance),
        Ramp(NearDistance - distance, NearDistance - DangerDistance),
        Ramp(DangerDistance - distance, DuckWidth));

    // How far along a ramp of the given length: 0 before it starts, 1 once it is over.
    private static double Ramp(double along, double length) => Math.Clamp(along / length, 0, 1);
}
