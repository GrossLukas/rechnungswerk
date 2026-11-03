namespace RechnungsWerk.Core.Validation;

public enum ValidationLevel
{
    Error,
    Warning,
    Information,
}

public sealed record ValidationMessage(
    ValidationLevel Level,
    string? RuleId,
    string Text,
    string? Location,
    string? Step,
    string Explanation);

public sealed record ValidationStep(string Name, bool Valid);

/// <summary>Ausgewerteter Prüfbericht des KoSIT-Validators.</summary>
public sealed class ValidationReport
{
    public required bool IsAcceptable { get; init; }
    public bool ScenarioMatched { get; init; }
    public string? ScenarioName { get; init; }
    public string? EngineName { get; init; }
    public IReadOnlyList<ValidationStep> Steps { get; init; } = [];
    public IReadOnlyList<ValidationMessage> Messages { get; init; } = [];

    public int ErrorCount => Messages.Count(m => m.Level == ValidationLevel.Error);
    public int WarningCount => Messages.Count(m => m.Level == ValidationLevel.Warning);
}

/// <summary>Der Prüfdienst ist nicht erreichbar oder konnte nicht prüfen – die Meldung ist für Nutzer formuliert.</summary>
public sealed class ValidatorUnavailableException : Exception
{
    public ValidatorUnavailableException(string message) : base(message)
    {
    }

    public ValidatorUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
