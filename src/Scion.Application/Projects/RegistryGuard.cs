using Scion.Domain;

namespace Scion.Application.Projects;

/// <summary>The one place that turns registry adapter exceptions into failure values.</summary>
internal static class RegistryGuard
{
    public static async Task<Result<TValue, RegistryFailure>> RunAsync<TValue>(Func<Task<TValue>> operation)
    {
        try
        {
            return Result<TValue, RegistryFailure>.Success(await operation());
        }
        catch (ProjectRegistryException exception)
        {
            return Result<TValue, RegistryFailure>.Failure(RegistryFailure.From(exception));
        }
    }

    public static async Task<Result<TValue, TError>> RunAsync<TValue, TError>(
        Func<Task<Result<TValue, TError>>> operation, Func<RegistryFailure, TError> toError)
    {
        try
        {
            return await operation();
        }
        catch (ProjectRegistryException exception)
        {
            return Result<TValue, TError>.Failure(toError(RegistryFailure.From(exception)));
        }
    }
}
