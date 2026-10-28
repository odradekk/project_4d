namespace FourD;

internal sealed partial class WorldValidation
{
    // The errors of one grazer's ID, pose and body, without the rest of the region: the host sends a frame every tick.
    internal static LoadError[] CheckGrazer(GrazerDefinition grazer)
    {
        var validation = new WorldValidation();
        validation.Id(grazer.Id.Value, grazer.Source, grazer.Id);
        validation.Pose(grazer.Pose, Field(grazer.Source, "Pose"), grazer.Id, mustBeFlat: false);
        validation.GrazerBody(grazer);
        return Sorted(validation.errors);
    }

    private void GrazerBody(GrazerDefinition grazer)
    {
        var errorsBefore = errors.Count;
        var source = Field(grazer.Source, "Shape");
        if (grazer.Shape is null)
        {
            Error(LoadErrorCode.InvalidGeometry, source, "Shape is required.", grazer.Id);
            return;
        }
        var balls = grazer.Shape.Balls;
        if (balls is null || balls.Count == 0) Error(LoadErrorCode.InvalidGeometry, Field(source, "Balls"), "At least one metaball is required.", grazer.Id);
        var ids = new HashSet<PartId>();
        for (var i = 0; i < (balls?.Count ?? 0); i++)
        {
            var ball = balls![i];
            var location = Field(source, $"Balls[{i}]");
            if (ball is null)
            {
                Error(LoadErrorCode.InvalidGeometry, location, "Metaball is required.", grazer.Id);
                continue;
            }
            Id(ball.Id.Value, Field(location, "Id"), grazer.Id);
            if (!ids.Add(ball.Id)) Error(LoadErrorCode.DuplicateId, Field(location, "Id"), "Duplicate ball ID.", grazer.Id);
            if (!Box4.ToArray(ball.Center).All(double.IsFinite))
                Error(LoadErrorCode.InvalidNumber, Field(location, "Center"), "Ball center must be finite.", grazer.Id);
            PositiveNumber(ball.Radius, Field(location, "Radius"), "Ball radius", grazer.Id);
            PositiveNumber(ball.Weight, Field(location, "Weight"), "Ball weight", grazer.Id);
        }
        PositiveNumber(grazer.Shape.Threshold, Field(source, "Threshold"), "Threshold", grazer.Id);
        if (errors.Count == errorsBefore && CannotReachThreshold(grazer.Shape))
            Error(LoadErrorCode.InvalidGeometry, Field(source, "Threshold"), "The weights of the balls cannot add up to the threshold.", grazer.Id);
    }

    // The field at a point is at most the weights of the balls that reach it, and all of those overlap the ball whose
    // reach the point is in. So the body is empty when no ball and the balls overlapping it together weigh enough.
    private static bool CannotReachThreshold(MetaballShape shape) =>
        shape.Balls.Max(ball => shape.Balls.Where(other => Overlap(ball, other)).Sum(other => other.Weight)) < shape.Threshold;

    private static bool Overlap(Metaball a, Metaball b)
    {
        var offset = Box4.Sub(Box4.ToArray(a.Center), Box4.ToArray(b.Center));
        return Math.Sqrt(Box4.Dot(offset, offset)) < a.Radius + b.Radius;
    }
}
