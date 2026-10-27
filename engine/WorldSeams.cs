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

    /// <summary>Opens or closes a seam. A severed seam cannot be opened: Rejected(SeamSevered).</summary>
    public UpdateResult SetSeamOpen(SeamId seam, bool open)
    {
        if (!seams.TryGetValue(seam, out var found))
            return new UpdateResult.Rejected(Fail(FailureCode.UnknownId, "Unknown seam.") with { Seam = seam });
        if (found.Severed && open)
            return new UpdateResult.Rejected(Fail(FailureCode.SeamSevered, "A severed seam cannot be opened.") with { Seam = seam });
        seams[seam] = found with { Open = open };
        revision++;
        return new UpdateResult.Updated();
    }

    // Severs a seam that exists, and reports it unless it was severed already. The caller counts the change.
    // ConsumeStructure is the one way to sever a seam.
    private void Sever(SeamId seam)
    {
        var found = seams[seam];
        if (found.Severed) return;
        seams[seam] = found with { Severed = true };
        Enqueue(new SeamSevered(seam), time);
    }
}
