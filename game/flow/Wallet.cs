namespace Game.Flow;

/// <summary>
/// The money Xiaoman carries, in mao (a tenth of a yuan), which scene scripts pass on to each other during a playthrough.
/// Decision note: 2026-10-26-prologue-greybox
/// </summary>
public sealed class Wallet(int mao)
{
    public int Mao { get; private set; } = mao;

    /// <summary>Takes the price out of the wallet; false, and nothing taken, when there is not enough.</summary>
    public bool Spend(int price)
    {
        if (price > Mao) return false;
        Mao -= price;
        return true;
    }

    /// <summary>An amount as it is said: "5 毛", "1 块", "1 块 5 毛".</summary>
    public static string Format(int mao) => mao switch
    {
        < 10 => $"{mao} 毛",
        _ when mao % 10 == 0 => $"{mao / 10} 块",
        _ => $"{mao / 10} 块 {mao % 10} 毛"
    };

    public override string ToString() => Format(Mao);
}
