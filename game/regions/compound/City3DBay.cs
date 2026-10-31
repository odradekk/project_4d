namespace Game.Regions;

/// <summary>
/// The gaps in the west side of the main road: where the north and south streets cross it, with their sidewalks, and the
/// openings in the west green belt that let the bus (<see cref="Bus3D"/>) out of the motor lanes into the bicycle lane
/// before the stop and back after it, paved like the motor lanes. They are where its body crosses the belt, x 89 to 91,
/// with 2 m to spare; the one after the stop takes in the road to the gate, whose own paving stays as it is.
/// Decision note: 2026-10-31-bus-commute
/// </summary>
public partial class City3D
{
    // Properties, not fields: City3D.cs's WestCross reads them, and static fields in two files start in no set order.
    private static (double, double) NorthCross => (R2.Item1 - 3, R2.Item2 + 3);
    private static (double, double) SouthCross => (R3.Item1 - 3, R3.Item2 + 3);
    private static (double, double) BayIn => (36, 58);
    private static (double, double) BayOut => (82, 104);

    private void BusBay()
    {
        Box(89, 91, 0, .02, BayIn.Item1, BayIn.Item2, Asphalt);
        Box(89, 91, 0, .02, BayOut.Item1, Cross.Item1, Asphalt);
        Box(89, 91, 0, .02, Cross.Item2, BayOut.Item2, Asphalt);
    }
}
