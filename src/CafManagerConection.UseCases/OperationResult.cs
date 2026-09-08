namespace CafManagerConection.UseCases;

/// <summary>Las causas previstas devuelven esto; las excepciones quedan para defectos de programación.</summary>
public readonly record struct OperationResult(bool Success, string? ErrorMessage)
{
    public static OperationResult Ok() => new(true, null);

    public static OperationResult Fail(string message) => new(false, message);

    public static implicit operator bool(OperationResult result) => result.Success;
}

public readonly record struct OperationResult<T>(bool Success, T? Value, string? ErrorMessage)
{
    public static OperationResult<T> Ok(T value) => new(true, value, null);

    public static OperationResult<T> Fail(string message) => new(false, default, message);

    public static implicit operator bool(OperationResult<T> result) => result.Success;
}

public sealed record ValidationError(string Field, string Message);

public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationError> Errors)
{
    public static ValidationResult Valid { get; } = new(true, []);

    public static ValidationResult Invalid(params ValidationError[] errors) => new(false, errors);

    public string ToMessage() => string.Join(Environment.NewLine, Errors.Select(e => e.Message));
}
