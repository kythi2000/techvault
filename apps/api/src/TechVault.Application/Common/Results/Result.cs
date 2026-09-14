namespace TechVault.Application.Common.Results;

public enum ErrorType { Validation, NotFound }

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string message) => new("VALIDATION_ERROR", message, ErrorType.Validation);
    public static Error DeviceNotFound() => new("DEVICE_NOT_FOUND", "Device was not found.", ErrorType.NotFound);
}

public sealed class Result<T> where T : notnull
{
    private readonly T? _value;

    private Result(T? value, Error? error) { _value = value; Error = error; }

    public bool IsSuccess => Error is null;
    public Error? Error { get; }
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(value, null);
    }

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(default, error);
    }
}
