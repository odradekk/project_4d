namespace FourD;

// Decision note: 2026-10-26-prologue-greybox
public sealed partial class World
{
    // The seams as loaded; severing one replaces its entry.
    private readonly Dictionary<SeamId, SeamDefinition> seams = [];

    /// <summary>Reads whether a seam is open and whether it has been severed.</summary>
    public SeamReadResult ReadSeam(SeamId seam) => seams.TryGetValue(seam, out var found)
        ? new SeamReadResult.Found(new(seam, found.Open, found.Severed))
        : new SeamReadResult.Missing(seam);

    /// <summary>
    /// Severs a seam for good. Severing an already severed seam succeeds without a second event.
    /// Rejected(UnknownId) for an unknown seam.
    /// </summary>
    public UpdateResult SeverSeam(SeamId seam)
    {
        if (!seams.TryGetValue(seam, out var found))
            return new UpdateResult.Rejected(Fail(FailureCode.UnknownId, "Unknown seam.") with { Seam = seam });
        if (!found.Severed)
        {
            seams[seam] = found with { Severed = true };
            Enqueue(new SeamSevered(seam), time);
        }
        revision++;
        return new UpdateResult.Updated();
    }
}
