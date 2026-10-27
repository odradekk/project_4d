namespace FourD;

internal sealed partial class WorldValidation
{
    // Error allowed in a rotation the host passes in (docs/frame/contract.md: Rotation4 allows an orthogonal error
    // of 1e-10). The host converts its matrices from Godot floats, so they are not exact even where they should be.
    private const double MatrixTolerance = 1e-10;

    private readonly List<LoadError> errors = [];
    private readonly Dictionary<SectionId, SectionDefinition> sections = [];
    private readonly Dictionary<BuildingId, BuildingDefinition> buildings = [];

    internal static LoadError[] Check(WorldDescription? description)
    {
        var validation = new WorldValidation();
        validation.Validate(description);
        return Sorted(validation.errors);
    }

    internal static LoadError[] Sorted(IEnumerable<LoadError> errors) =>
        errors.OrderBy(error => error.Source.NodePath, StringComparer.Ordinal)
            .ThenBy(error => error.Source.Property, StringComparer.Ordinal)
            .ThenBy(error => error.Code.ToString(), StringComparer.Ordinal)
            .ThenBy(error => error.Entity?.Value, StringComparer.Ordinal)
            .ThenBy(error => error.Operation?.Value, StringComparer.Ordinal)
            .ThenBy(error => error.StepIndex)
            .ToArray();

    private static Source Field(Source source, string field) => source with { Property = $"{source.Property}.{field}" };

    private void Error(LoadErrorCode code, Source source, string detail, EntityId? entity = null) =>
        errors.Add(new(code, source, entity, null, null, detail, Array.Empty<Contact>()));

    private void Validate(WorldDescription? description)
    {
        var root = new Source("", "World");
        if (description is null)
        {
            Error(LoadErrorCode.InvalidGeometry, root, "World description is required.");
            return;
        }
        ValidateSections(description, root);
        ValidateBuildings(description, root);
        ValidateEntities(description, root);
        errors.AddRange(WorldInterference.Check(description, errors));
        Unsupported(description.Seams, seam => seam.Source, root, "Seams");
        Unsupported(description.SharedValues, value => value.Source, root, "SharedValues");
        if (description.ExpectedOperations is null)
            Error(LoadErrorCode.InvalidGeometry, root, "ExpectedOperations collection is required.");
    }

    private void ValidateSections(WorldDescription description, Source root)
    {
        if (description.Sections is null || description.Sections.Count == 0)
            Error(LoadErrorCode.InvalidSectionOrder, root, "At least one section is required.");
        double? previous = null;
        foreach (var section in description.Sections ?? [])
        {
            if (section is null)
            {
                Error(LoadErrorCode.InvalidSectionOrder, root, "Section is required.");
                continue;
            }
            Id(section.Id.Value, Field(section.Source, "Id"));
            if (!sections.TryAdd(section.Id, section))
                Error(LoadErrorCode.DuplicateId, Field(section.Source, "Id"), "Duplicate section ID.");
            if (!double.IsFinite(section.W))
                Error(LoadErrorCode.InvalidNumber, section.Source, "Section W must be finite.");
            else if (previous is double last && section.W <= last)
                Error(LoadErrorCode.InvalidSectionOrder, section.Source, "Sections must have increasing W.");
            previous = section.W;
        }
    }

    private void ValidateBuildings(WorldDescription description, Source root)
    {
        if (description.Buildings is null) Error(LoadErrorCode.InvalidGeometry, root, "Buildings collection is required.");
        foreach (var building in description.Buildings ?? [])
        {
            if (building is null)
            {
                Error(LoadErrorCode.InvalidGeometry, root, "Building is required.");
                continue;
            }
            Id(building.Id.Value, building.Source);
            if (!buildings.TryAdd(building.Id, building))
                Error(LoadErrorCode.DuplicateId, building.Source, "Duplicate building ID.");
            Range(building.WRange, Field(building.Source, "WRange"));
        }
    }

    private void ValidateEntities(WorldDescription description, Source root)
    {
        var ids = new HashSet<EntityId>();
        if (description.Entities is null) Error(LoadErrorCode.InvalidGeometry, root, "Entities collection is required.");
        foreach (var entity in description.Entities ?? [])
        {
            if (entity is null)
            {
                Error(LoadErrorCode.InvalidGeometry, root, "Entity is required.");
                continue;
            }
            Id(entity.Id.Value, entity.Source, entity.Id);
            if (!ids.Add(entity.Id)) Error(LoadErrorCode.DuplicateId, entity.Source, "Duplicate entity ID.", entity.Id);
            var errorsBeforePose = errors.Count;
            Pose(entity.Pose, Field(entity.Source, "Pose"), entity.Id);
            Pose4? validPose = errors.Count == errorsBeforePose ? entity.Pose : null;
            switch (entity)
            {
                case BuildingElementDefinition element:
                    BuildingElement(element, validPose);
                    break;
                case FixedDefinition fixedObject:
                    Fixed(fixedObject, validPose);
                    break;
                case TurnableDefinition turnable:
                    Turnable(turnable, validPose);
                    break;
                case Creature3Definition creature:
                    Creature(creature);
                    break;
                default:
                    Error(LoadErrorCode.UnsupportedDefinition, entity.Source, "This entity category is not implemented.", entity.Id);
                    break;
            }
        }
    }

    private void BuildingElement(BuildingElementDefinition element, Pose4? validPose)
    {
        var rangeValid = Range(element.WRange, Field(element.Source, "WRange"), element.Id);
        if (FindBuilding(element.Building, Field(element.Source, "Building"), element.Id) is { } building
            && rangeValid && ValidRange(building.WRange)
            && (element.WRange.Min < building.WRange.Min || element.WRange.Max > building.WRange.Max))
            Error(LoadErrorCode.BuildingCoverage,
                Field(element.Source, element.WRange.Min < building.WRange.Min ? "WRange.Min" : "WRange.Max"),
                "Element extends outside its building range.", element.Id);
        if (element.Pose.Position.W != 0 || element.Pose.Rotation.W.W != 1)
            Error(LoadErrorCode.InvalidPose, Field(element.Source, "Pose"), "Building pose must be an XYZ transform with W=0.", element.Id);
        Gaps(element, rangeValid);
        Shape(element.Shape, element.Source, element.Id, validPose);
    }

    private void Fixed(FixedDefinition fixedObject, Pose4? validPose)
    {
        if (FindSection(fixedObject.Section, fixedObject.Source, fixedObject.Id) is { } section
            && double.IsFinite(section.W) && fixedObject.Pose.Position.W != section.W)
            Error(LoadErrorCode.InvalidPose, fixedObject.Source, "Fixed position W must equal its section W.", fixedObject.Id);
        FixedThickness(fixedObject.Pose, fixedObject.Source, fixedObject.Id);
        Shape(fixedObject.Shape, fixedObject.Source, fixedObject.Id, validPose);
    }

    private void Creature(Creature3Definition creature)
    {
        var source = creature.Source;
        if (FindSection(creature.Section, Field(source, "Section"), creature.Id) is { } section
            && double.IsFinite(section.W) && creature.Pose.Position.W != section.W)
            Error(LoadErrorCode.InvalidPose, Field(source, "Pose.Position.W"), "Actor W must equal its section W.", creature.Id);
        var radiusValid = PositiveNumber(creature.PassageRadius, Field(source, "PassageRadius"), "Passage radius", creature.Id);
        var heightValid = PositiveNumber(creature.PassageHeight, Field(source, "PassageHeight"), "Passage height", creature.Id);
        if (radiusValid && heightValid && creature.PassageHeight / 2 < creature.PassageRadius)
            Error(LoadErrorCode.InvalidGeometry, Field(source, "PassageHeight"), "Passage height must be at least twice the radius.", creature.Id);
    }

    private SectionDefinition? FindSection(SectionId id, Source source, EntityId entity)
    {
        if (sections.TryGetValue(id, out var section)) return section;
        Error(LoadErrorCode.MissingReference, source, "Unknown section.", entity);
        return null;
    }

    private BuildingDefinition? FindBuilding(BuildingId id, Source source, EntityId entity)
    {
        if (buildings.TryGetValue(id, out var building)) return building;
        Error(LoadErrorCode.MissingReference, source, "Unknown building.", entity);
        return null;
    }

    // Reports a value that is not a finite positive number, and returns whether it is one.
    private bool PositiveNumber(double value, Source source, string label, EntityId entity)
    {
        if (!double.IsFinite(value)) Error(LoadErrorCode.InvalidNumber, source, $"{label} must be finite.", entity);
        else if (value <= 0) Error(LoadErrorCode.InvalidGeometry, source, $"{label} must be positive.", entity);
        else return true;
        return false;
    }

    private void Unsupported<T>(IReadOnlyList<T>? values, Func<T, Source> source, Source root, string name)
    {
        if (values is null) Error(LoadErrorCode.InvalidGeometry, root, $"{name} collection is required.");
        else
            foreach (var value in values)
                Error(LoadErrorCode.UnsupportedDefinition, value is null ? root : source(value), $"{name} are not implemented.");
    }

    private void Id(string? id, Source source, EntityId? entity = null)
    {
        if (string.IsNullOrWhiteSpace(id)) Error(LoadErrorCode.InvalidId, source, "ID must be nonempty.", entity);
    }

    private static bool ValidRange(Interval range) =>
        double.IsFinite(range.Min) && double.IsFinite(range.Max) && range.Min < range.Max;

    private bool Range(Interval range, Source source, EntityId? entity = null)
    {
        if (!double.IsFinite(range.Min)) Error(LoadErrorCode.InvalidNumber, Field(source, "Min"), "Interval ends must be finite.", entity);
        if (!double.IsFinite(range.Max)) Error(LoadErrorCode.InvalidNumber, Field(source, "Max"), "Interval ends must be finite.", entity);
        if (double.IsFinite(range.Min) && double.IsFinite(range.Max) && range.Min >= range.Max)
            Error(LoadErrorCode.InvalidInterval, source, "Interval must have positive length.", entity);
        return ValidRange(range);
    }

    private void Gaps(BuildingElementDefinition element, bool rangeValid)
    {
        var source = Field(element.Source, "Gaps");
        if (element.Gaps is null)
        {
            Error(LoadErrorCode.InvalidInterval, source, "Gaps collection is required.", element.Id);
            return;
        }
        var valid = rangeValid;
        double? previous = null;
        foreach (var gap in element.Gaps)
        {
            var gapValid = Range(gap, source, element.Id);
            if (gapValid && rangeValid
                && (gap.Min < element.WRange.Min || gap.Max > element.WRange.Max || (previous is double last && gap.Min < last)))
            {
                Error(LoadErrorCode.InvalidInterval, source, "Gaps must be ordered, nonoverlapping, and inside the element range.", element.Id);
                valid = false;
            }
            valid &= gapValid;
            previous = gap.Max;
        }
        if (!valid) return;
        var start = element.WRange.Min;
        foreach (var gap in element.Gaps)
        {
            Thickness(gap.Min - start, source, element.Id);
            start = gap.Max;
        }
        Thickness(element.WRange.Max - start, element.Gaps.Count == 0 ? Field(element.Source, "WRange") : source, element.Id);
    }

    private void Thickness(double value, Source source, EntityId entity)
    {
        if (value > 0 && value <= World.SamplingDistance)
            Error(LoadErrorCode.InvalidGeometry, source, "Solid thickness must exceed sampling distance.", entity);
    }

    private void Pose(Pose4 pose, Source source, EntityId entity)
    {
        if (!IsFinite(pose))
        {
            Error(LoadErrorCode.InvalidNumber, source, "Pose components must be finite.", entity);
            return;
        }
        if (!Rotation(Box4.AxesOf(pose.Rotation)) || !IsFlat(pose.Rotation))
            Error(LoadErrorCode.InvalidPose, source, "Static pose must be right-handed orthonormal and preserve the W axis.", entity);
    }

    private void FixedThickness(Pose4 pose, Source source, EntityId entity)
    {
        var w = pose.Position.W;
        if (!double.IsFinite(w)) return;
        var min = w - World.Epsilon;
        var max = w + World.Epsilon;
        if (!(min < w && w < max) || max - min <= World.SamplingDistance)
            Error(LoadErrorCode.InvalidGeometry, Field(source, "Pose.Position.W"),
                "W thickness must exceed the sampling distance at this coordinate precision.", entity);
    }

    private static bool IsFinite(Pose4 pose) =>
        Box4.ToArray(pose.Position).Concat(Box4.AxesOf(pose.Rotation).SelectMany(axis => axis)).All(double.IsFinite);

    // A flat pose keeps the local W axis on the world W axis, so the object lies within one section.
    internal static bool IsFlat(Rotation4 rotation) =>
        Math.Abs(rotation.W.X) <= MatrixTolerance
        && Math.Abs(rotation.W.Y) <= MatrixTolerance
        && Math.Abs(rotation.W.Z) <= MatrixTolerance;

    internal static bool IsValidPose(Pose4 pose) => IsFinite(pose) && Rotation(Box4.AxesOf(pose.Rotation));

    // Whether the axes are orthonormal and form a right-handed basis.
    internal static bool Rotation(double[][] axes)
    {
        for (var i = 0; i < axes.Length; i++)
            for (var j = 0; j < axes.Length; j++)
            {
                var dot = 0d;
                for (var k = 0; k < axes.Length; k++) dot += axes[i][k] * axes[j][k];
                if (Math.Abs(dot - (i == j ? 1 : 0)) > MatrixTolerance) return false;
            }
        return Math.Abs(Determinant(axes) - 1) <= MatrixTolerance;
    }

    // Gaussian elimination with partial pivoting; a singular matrix gives 0.
    private static double Determinant(double[][] rows)
    {
        var matrix = rows.Select(row => row.ToArray()).ToArray();
        var determinant = 1d;
        for (var i = 0; i < matrix.Length; i++)
        {
            var pivot = i;
            for (var j = i + 1; j < matrix.Length; j++)
                if (Math.Abs(matrix[j][i]) > Math.Abs(matrix[pivot][i])) pivot = j;
            if (pivot != i)
            {
                (matrix[i], matrix[pivot]) = (matrix[pivot], matrix[i]);
                determinant = -determinant;
            }
            var diagonal = matrix[i][i];
            if (diagonal == 0) return 0;
            determinant *= diagonal;
            for (var j = i + 1; j < matrix.Length; j++)
            {
                var factor = matrix[j][i] / diagonal;
                for (var k = i + 1; k < matrix.Length; k++) matrix[j][k] -= factor * matrix[i][k];
            }
        }
        return determinant;
    }
}
