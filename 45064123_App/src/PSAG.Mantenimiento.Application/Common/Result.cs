namespace PSAG.Mantenimiento.Application.Common;

/// <summary>
/// Resultado de una operación con posibilidad de éxito o error.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
    public string? Error => ErrorMessage;
    public IReadOnlyList<string> ValidationErrors { get; }

    protected Result(bool isSuccess, string? errorCode, string? errorMessage, IEnumerable<string>? validationErrors = null)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        ValidationErrors = validationErrors?.ToList().AsReadOnly() ?? Array.Empty<string>().ToList().AsReadOnly();
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string errorMessage) => new(false, "ERROR", errorMessage);
    public static Result Failure(string errorCode, string errorMessage) => new(false, errorCode, errorMessage);
    public static Result ValidationFailure(IEnumerable<string> errors) => new(false, "VALIDATION_ERROR", "Error de validación", errors);
}

/// <summary>
/// Resultado de una operación con valor de retorno.
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage, IEnumerable<string>? validationErrors = null)
        : base(isSuccess, errorCode, errorMessage, validationErrors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public new static Result<T> Failure(string errorMessage) => new(false, default, "ERROR", errorMessage);
    public new static Result<T> Failure(string errorCode, string errorMessage) => new(false, default, errorCode, errorMessage);
    public new static Result<T> ValidationFailure(IEnumerable<string> errors) => new(false, default, "VALIDATION_ERROR", "Error de validación", errors);
}
