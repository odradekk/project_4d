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
}
