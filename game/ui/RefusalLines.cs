using FourD;
using Game.Adapter;

namespace Game.UI;

/// <summary>
/// What Xiaoman says when the engine refuses a hand action. The reason picks the line.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public static class RefusalLines
{
    public static string? For(Refusal refusal, string currentSection) => refusal.Code switch
    {
        FailureCode.RotationBlocked when refusal.Section == currentSection => "转不开……这边太挤了。",
        FailureCode.RotationBlocked when refusal.Section is not null => "转不过去……那一页有东西挡着。",
        FailureCode.RotationBlocked => "转不过去……好像卡在两页中间了。",
        FailureCode.OutOfRange => "转不过去……再转就转出屋子了。",
        FailureCode.WrongSection => "得回到它原来那一页，才能把它放倒。",
        FailureCode.NotHoldable => "这个拿不动。",
        _ => null
    };

    /// <summary>Why she cannot go through a seam. Every refusal gets a line, since she just tried to go.</summary>
    public static string ForCrossing(Refusal refusal, string currentSection) => refusal.Code switch
    {
        FailureCode.WrongSection => "再靠近缝一点。",
        FailureCode.SeamClosed => "这道缝合上了，过不去。",
        FailureCode.SeamSevered => "缝被咬断了，过不去。",
        FailureCode.RequiresPrism => "得举着棱镜才能穿缝。",
        FailureCode.CapacityExceeded => "背包装不下了，先放下一样东西。",
        FailureCode.NotPackable => "这个带不过缝，先放下它。",
        FailureCode.PassageOccupied when refusal.Section == currentSection => "缝口这边有东西挡着。",
        FailureCode.PassageOccupied when refusal.Section is not null => "那一页的缝口有东西挡着。",
        FailureCode.PassageOccupied => "缝口有东西挡着。",
        FailureCode.TranslationBlocked => "手里的东西挪不到缝口，路上有东西挡着。",
        FailureCode.RotationBlocked when refusal.Section == currentSection => "它竖不进缝里……这边太挤了。",
        FailureCode.RotationBlocked when refusal.Section is not null => "它竖不进缝里……那一页有东西挡着。",
        FailureCode.RotationBlocked => "它竖不进缝里……好像卡在两页中间了。",
        FailureCode.OutOfRange => "它竖不进缝里……再转就转出屋子了。",
        _ => "过不去。"
    };

    /// <summary>Why she cannot take a thing out of the backpack.</summary>
    public static string ForTakingOut(Refusal refusal) => refusal.Code switch
    {
        FailureCode.EntityInUse => "手里已经拿着东西了。",
        FailureCode.TranslationBlocked => "这里放不开，换个地方再拿。",
        _ => "拿不出来。"
    };
}
