namespace FourD;

public sealed partial class World
{
    internal static LoadError[] CheckExpectedOperations(WorldDescription description)
    {
        var errors = new List<LoadError>();
        var ids = new HashSet<OperationId>();
        var root = new Source("", "World.ExpectedOperations");
        foreach (var operation in description.ExpectedOperations)
        {
            if (operation is null)
            {
                errors.Add(new(LoadErrorCode.InvalidGeometry, root, null, null, null, "Operation is required.", []));
                continue;
            }
            void Error(LoadErrorCode code, string detail, int? step = null, EntityId? entity = null, IReadOnlyList<Contact>? contacts = null, string? field = null) =>
                errors.Add(new(code, operation.Source with { Property = operation.Source.Property + (field ?? (step is int index ? $".Steps[{index}]" : "")) }, entity, operation.Id, step, detail, contacts ?? []));
            var start = errors.Count;
            if (string.IsNullOrWhiteSpace(operation.Id.Value)) Error(LoadErrorCode.InvalidId, "Operation ID must be nonempty.", field: ".Id");
            if (!ids.Add(operation.Id)) Error(LoadErrorCode.DuplicateId, "Duplicate operation ID.", field: ".Id");
            if (operation.Setup is null || operation.Steps is null)
            {
                Error(LoadErrorCode.InvalidGeometry, "Setup and steps are required.");
                continue;
            }
            var setup = operation.Setup;
            if (setup.EntityOverrides is null || setup.Seams is null || setup.SharedValues is null || setup.Carry is null)
            {
                Error(LoadErrorCode.InvalidGeometry, "Setup collections are required.");
                continue;
            }
            if (setup.Seams.Count > 0) Error(LoadErrorCode.UnsupportedDefinition, "Seam overrides are not implemented.", field: ".Setup.Seams");
            if (setup.SharedValues.Count > 0) Error(LoadErrorCode.UnsupportedDefinition, "Shared value overrides are not implemented.", field: ".Setup.SharedValues");
            if (setup.Carry.Count > 0) Error(LoadErrorCode.UnsupportedDefinition, "Carry setup is not implemented.", field: ".Setup.Carry");
            var replacements = new Dictionary<EntityId, EntityDefinition>();
            foreach (var replacement in setup.EntityOverrides)
            {
                if (replacement is null) { Error(LoadErrorCode.InvalidGeometry, "Entity override is required."); continue; }
                if (!replacements.TryAdd(replacement.Id, replacement))
                { Error(LoadErrorCode.DuplicateId, "Duplicate entity override.", entity: replacement.Id); continue; }
                var original = description.Entities.FirstOrDefault(e => e.Id == replacement.Id);
                if (original is null) Error(LoadErrorCode.MissingReference, "Unknown entity override.", entity: replacement.Id);
                else if (original is not TurnableDefinition and not Creature3Definition || original.GetType() != replacement.GetType())
                    Error(LoadErrorCode.UnsupportedDefinition, "Override must match a supported dynamic entity kind.", entity: replacement.Id);
            }
            for (var i = 0; i < operation.Steps.Count; i++)
                if (operation.Steps[i] is not ExpectedRotation and not ExpectedAdvance)
                    Error(LoadErrorCode.UnsupportedDefinition, "This expected step is not implemented.", i);
            if (errors.Count != start) continue;
            var copy = description with
            {
                Entities = description.Entities.Select(e => replacements.GetValueOrDefault(e.Id, e)).ToArray(),
                ExpectedOperations = []
            };
            var result = Load(copy);
            if (result is LoadResult.Invalid invalid)
            {
                errors.AddRange(invalid.Errors.Select(e => e with { Operation = operation.Id }));
                continue;
            }
            var world = ((LoadResult.Loaded)result).World;
            for (var i = 0; i < operation.Steps.Count; i++)
            {
                Failure? failure = null;
                FailureCode? expected = null;
                switch (operation.Steps[i])
                {
                    case ExpectedRotation rotation:
                        expected = rotation.ExpectedFailure;
                        switch (world.PlanRotation(rotation.Request))
                        {
                            case RotationPlanResult.Rejected rejected: failure = rejected.Failure; break;
                            case RotationPlanResult.Blocked blocked: failure = blocked.Failure; break;
                            case RotationPlanResult.Allowed allowed:
                                switch (world.StartRotation(allowed.Plan))
                                {
                                    case StartRotationResult.Rejected startRejected: failure = startRejected.Failure; break;
                                    case StartRotationResult.Started started when expected is null:
                                        if (world.Step(started.Duration) is StepResult.Rejected stepRejected) failure = stepRejected.Failure;
                                        break;
                                }
                                break;
                        }
                        break;
                    case ExpectedAdvance advance:
                        if (world.Step(advance.Delta) is StepResult.Rejected advanceRejected) failure = advanceRejected.Failure;
                        break;
                }
                if (failure?.Code != expected)
                {
                    Error(LoadErrorCode.ExpectedOperationFailed, $"Expected {expected?.ToString() ?? "success"}; got {failure?.Code.ToString() ?? "success"}.", i, failure?.Entity, failure?.Contacts);
                    break;
                }
            }
        }
        return errors.OrderBy(e => e.Source.NodePath, StringComparer.Ordinal)
            .ThenBy(e => e.Source.Property, StringComparer.Ordinal).ThenBy(e => e.Code.ToString(), StringComparer.Ordinal)
            .ThenBy(e => e.Entity?.Value, StringComparer.Ordinal).ThenBy(e => e.Operation?.Value, StringComparer.Ordinal)
            .ThenBy(e => e.StepIndex).ToArray();
    }
}
