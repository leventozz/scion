namespace Scion.Domain;

public sealed class Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    private Result(bool isSuccess, TValue? value, TError? error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    public bool IsSuccess { get; }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public TError Error => !IsSuccess
        ? _error!
        : throw new InvalidOperationException("A successful result has no error.");

    public static Result<TValue, TError> Success(TValue value) => new(true, value, default);

    public static Result<TValue, TError> Failure(TError error) => new(false, default, error);
}
