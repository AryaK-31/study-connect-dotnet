namespace StudyConnect.Core.Common;

/// <summary>
/// Outcome of a business operation that does not return a value.
/// Services return results instead of throwing for expected failures
/// (validation errors, permission problems, ...) so the UI can show friendly messages.
/// </summary>
public class OperationResult
{
    protected OperationResult(bool succeeded, string? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }

    public bool Failed => !Succeeded;

    /// <summary>User-friendly error message when <see cref="Succeeded"/> is false.</summary>
    public string? Error { get; }

    public static OperationResult Success() => new(true, null);

    public static OperationResult Failure(string error) => new(false, error);

    public override string ToString() => Succeeded ? "Success" : $"Failure: {Error}";
}

/// <summary>
/// Generic outcome of a business operation that produces a value of type <typeparamref name="T"/>.
/// Demonstrates generics and inheritance (it extends <see cref="OperationResult"/>).
/// </summary>
/// <typeparam name="T">Type of the value produced on success.</typeparam>
public sealed class Result<T> : OperationResult
{
    private Result(bool succeeded, T? value, string? error)
        : base(succeeded, error)
    {
        Value = value;
    }

    /// <summary>The produced value (only meaningful when the operation succeeded).</summary>
    public T? Value { get; }

    public static Result<T> Success(T value) => new(true, value, null);

    public static new Result<T> Failure(string error) => new(false, default, error);
}
