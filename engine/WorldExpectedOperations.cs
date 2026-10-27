namespace FourD;

public sealed partial class World
{
    private static LoadError[] CheckExpectedOperations(WorldDescription description)
    {
        var errors = new List<LoadError>();
        var ids = new HashSet<OperationId>();
        foreach (var operation in description.ExpectedOperations)
        {
            if (operation is null)
            {
                var root = new Source("", "World.ExpectedOperations");
                errors.Add(new(LoadErrorCode.InvalidGeometry, root, null, null, null, "Operation is required.", []));
                continue;
            }
            var problems = OperationProblems(operation, description, ids);
            errors.AddRange(problems);
            if (problems.Count == 0) errors.AddRange(RunOperation(operation, description));
        }
        return WorldValidation.Sorted(errors);
    }

    private static LoadError OperationError(
        ExpectedOperation operation, LoadErrorCode code, string detail,
        int? step = null, EntityId? entity = null, IReadOnlyList<Contact>? contacts = null, string? field = null) =>
        new(code,
            operation.Source with { Property = operation.Source.Property + (field ?? (step is int index ? $".Steps[{index}]" : "")) },
            entity, operation.Id, step, detail, contacts ?? []);

    // Finds what is wrong with an operation's ID, setup and steps without running anything, and records its ID in `ids`.
    private static List<LoadError> OperationProblems(ExpectedOperation operation, WorldDescription description, HashSet<OperationId> ids)
    {
        var problems = new List<LoadError>();
        void Report(LoadErrorCode code, string detail, int? step = null, EntityId? entity = null, string? field = null) =>
            problems.Add(OperationError(operation, code, detail, step, entity, field: field));

        if (string.IsNullOrWhiteSpace(operation.Id.Value)) Report(LoadErrorCode.InvalidId, "Operation ID must be nonempty.", field: ".Id");
        if (!ids.Add(operation.Id)) Report(LoadErrorCode.DuplicateId, "Duplicate operation ID.", field: ".Id");
        if (operation.Setup is null || operation.Steps is null)
        {
            Report(LoadErrorCode.InvalidGeometry, "Setup and steps are required.");
            return problems;
        }
        var setup = operation.Setup;
        if (setup.EntityOverrides is null || setup.Seams is null || setup.SharedValues is null || setup.Carry is null)
        {
            Report(LoadErrorCode.InvalidGeometry, "Setup collections are required.");
            return problems;
        }
        if (setup.SharedValues.Count > 0) Report(LoadErrorCode.UnsupportedDefinition, "Shared value overrides are not implemented.", field: ".Setup.SharedValues");

        var overriddenSeams = new HashSet<SeamId>();
        foreach (var replacement in setup.Seams)
        {
            if (replacement is null) Report(LoadErrorCode.InvalidGeometry, "Seam override is required.", field: ".Setup.Seams");
            else if (!overriddenSeams.Add(replacement.Id)) Report(LoadErrorCode.DuplicateId, "Duplicate seam override.", field: ".Setup.Seams");
            else if (description.Seams.All(seam => seam.Id != replacement.Id))
                Report(LoadErrorCode.MissingReference, "Unknown seam override.", field: ".Setup.Seams");
        }
        var carriedItems = new HashSet<EntityId>();
        foreach (var carry in setup.Carry)
        {
            if (carry is null) Report(LoadErrorCode.InvalidGeometry, "Carry setup is required.", field: ".Setup.Carry");
            else if (!carriedItems.Add(carry.Item)) Report(LoadErrorCode.DuplicateId, "An item has one holder.", entity: carry.Item, field: ".Setup.Carry");
            else if (description.Entities.All(entity => entity.Id != carry.Actor) || description.Entities.All(entity => entity.Id != carry.Item))
                Report(LoadErrorCode.MissingReference, "Unknown actor or item in carry setup.", entity: carry.Item, field: ".Setup.Carry");
        }

        var overridden = new HashSet<EntityId>();
        foreach (var replacement in setup.EntityOverrides)
        {
            if (replacement is null)
            {
                Report(LoadErrorCode.InvalidGeometry, "Entity override is required.");
                continue;
            }
            if (!overridden.Add(replacement.Id))
            {
                Report(LoadErrorCode.DuplicateId, "Duplicate entity override.", entity: replacement.Id);
                continue;
            }
            var original = description.Entities.FirstOrDefault(entity => entity.Id == replacement.Id);
            if (original is null)
                Report(LoadErrorCode.MissingReference, "Unknown entity override.", entity: replacement.Id);
            else if (original is not TurnableDefinition and not ItemDefinition and not Creature3Definition || original.GetType() != replacement.GetType())
                Report(LoadErrorCode.UnsupportedDefinition, "Override must match a supported dynamic entity kind.", entity: replacement.Id);
        }
        for (var i = 0; i < operation.Steps.Count; i++)
            if (operation.Steps[i] is not ExpectedRotation and not ExpectedCrossing and not ExpectedAdvance)
                Report(LoadErrorCode.UnsupportedDefinition, "This expected step is not implemented.", step: i);
        return problems;
    }

    // Loads the region with the operation's overrides and runs its steps until one does not end as expected.
    private static List<LoadError> RunOperation(ExpectedOperation operation, WorldDescription description)
    {
        var replacements = operation.Setup.EntityOverrides.ToDictionary(replacement => replacement.Id);
        var seamOverrides = operation.Setup.Seams.ToDictionary(replacement => replacement.Id);
        var loaded = Load(description with
        {
            Entities = description.Entities.Select(entity => replacements.GetValueOrDefault(entity.Id, entity)).ToArray(),
            Seams = description.Seams.Select(seam => OverriddenSeam(seam, seamOverrides)).ToArray(),
            ExpectedOperations = []
        });
        if (loaded is LoadResult.Invalid invalid)
            return invalid.Errors.Select(error => error with { Operation = operation.Id }).ToList();

        var world = ((LoadResult.Loaded)loaded).World;
        foreach (var carry in operation.Setup.Carry)
        {
            var carried = carry.Location == CarryLocation.Hand
                ? world.HoldEntity(carry.Actor, carry.Item)
                : world.PackEntity(carry.Actor, carry.Item, int.MaxValue);
            if (carried is CarryResult.Rejected rejected)
                return [OperationError(operation, LoadErrorCode.InvalidCarry, $"The item cannot be carried: {rejected.Failure.Code}.", entity: carry.Item, field: ".Setup.Carry")];
        }
        for (var i = 0; i < operation.Steps.Count; i++)
        {
            var step = operation.Steps[i];
            var expected = step switch
            {
                ExpectedRotation rotation => rotation.ExpectedFailure,
                ExpectedCrossing crossing => crossing.ExpectedFailure,
                _ => null
            };
            var failure = RunStep(world, step);
            if (failure?.Code != expected)
            {
                var detail = $"Expected {expected?.ToString() ?? "success"}; got {failure?.Code.ToString() ?? "success"}.";
                return [OperationError(operation, LoadErrorCode.ExpectedOperationFailed, detail, i, failure?.Entity, failure?.Contacts)];
            }
        }
        return [];
    }

    private static SeamDefinition OverriddenSeam(SeamDefinition seam, Dictionary<SeamId, SeamOverride> overrides) =>
        overrides.TryGetValue(seam.Id, out var replacement) ? seam with { Open = replacement.Open, Severed = replacement.Severed } : seam;

    private static Failure? RunStep(World world, ExpectedStep step) => step switch
    {
        ExpectedRotation rotation => RunRotation(world, rotation),
        ExpectedCrossing crossing => (world.CrossSeam(crossing.Request) as CrossSeamResult.Rejected)?.Failure,
        ExpectedAdvance advance => (world.Step(advance.Delta) as StepResult.Rejected)?.Failure,
        _ => null
    };

    // Plans and starts a rotation. Unless it is expected to fail, also advances time until it ends.
    private static Failure? RunRotation(World world, ExpectedRotation rotation)
    {
        switch (world.PlanRotation(rotation.Request))
        {
            case RotationPlanResult.Rejected rejected:
                return rejected.Failure;
            case RotationPlanResult.Blocked blocked:
                return blocked.Failure;
            case RotationPlanResult.Allowed allowed:
                switch (world.StartRotation(allowed.Plan))
                {
                    case StartRotationResult.Rejected startRejected:
                        return startRejected.Failure;
                    case StartRotationResult.Started started when rotation.ExpectedFailure is null:
                        return (world.Step(started.Duration) as StepResult.Rejected)?.Failure;
                }
                return null;
            default:
                return null;
        }
    }
}
