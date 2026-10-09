using ErrorOr;
using RandomSteamGame.Client.Services;
using RandomSteamGame.Services.Interfaces;
using RandomSteamGame.Shared.Contracts;
using System.Net;

namespace RandomSteamGame.Services;

public sealed class ServerRandomSteamApiClient : IRandomSteamApiClient
{
    private readonly GameApplicationService _operations;
    private readonly IAppStatsService _stats;
    private readonly SteamApiRequestLimiter _limiter;
    private readonly ILogger<ServerRandomSteamApiClient> _logger;
    private readonly GameRequestContext _requestContext;

    public ServerRandomSteamApiClient(GameApplicationService operations, IAppStatsService stats,
        SteamApiRequestLimiter limiter, IHttpContextAccessor accessor, ILogger<ServerRandomSteamApiClient> logger)
    {
        _operations = operations;
        _stats = stats;
        _limiter = limiter;
        _logger = logger;
        _requestContext = GameRequestContext.From(accessor.HttpContext);
    }

    public Task<ApiResult<OwnedGamesResponse>> GetOwnedGamesAsync(string provider, long steamId, CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _operations.GetLibrary(provider, steamId), cancellationToken);

    public async Task<ApiResult<bool>> InvalidateOwnedGamesCacheAsync(string provider, long steamId, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(() => _operations.RefreshLibraryAsync(provider, steamId), cancellationToken);
        if (!result.IsSuccess)
        {
            return ApiResult<bool>.Failure(result.StatusCode, result.Problem, result.ErrorMessage);
        }
        return result.Value!.CooldownProblem is { } problem
            ? ApiResult<bool>.Failure(HttpStatusCode.TooManyRequests, problem)
            : ApiResult<bool>.Success(true);
    }

    public Task<ApiResult<GameDetails>> GetRandomGameDetailsAsync(string provider, long? steamId = null,
        string? vanityUrl = null, bool unplayedOnly = false, CancellationToken cancellationToken = default,
        IReadOnlyCollection<int>? excludedGameIds = null) =>
        ExecuteAsync(() => _operations.GetRandomGameDetailsAsync(
            provider, steamId, vanityUrl, _requestContext, unplayedOnly, excludedGameIds), cancellationToken);

    public Task<ApiResult<long>> ResolveVanityUrlAsync(string provider, string vanityUrl, CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => _operations.ResolveVanityAsync(provider, vanityUrl), cancellationToken);

    public Task<ApiResult<AppStatsResponse>> GetStatsAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync<AppStatsResponse>(async () => await _stats.GetStatsAsync(), cancellationToken);

    private async Task<ApiResult<T>> ExecuteAsync<T>(Func<Task<ErrorOr<T>>> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var lease = _limiter.AttemptAcquire();
        if (!lease.IsAcquired)
        {
            return ApiResult<T>.Failure(HttpStatusCode.TooManyRequests,
                errorMessage: "Too many requests. Please slow down and try again in a few seconds.");
        }
        try
        {
            var result = await operation();
            if (!result.IsError)
            {
                return ApiResult<T>.Success(result.Value);
            }
            var error = result.FirstError;
            var status = error.Type switch
            {
                ErrorType.Validation => HttpStatusCode.BadRequest,
                ErrorType.NotFound => HttpStatusCode.NotFound,
                ErrorType.Conflict => HttpStatusCode.Conflict,
                _ => HttpStatusCode.InternalServerError
            };
            var validation = result.Errors.All(item => item.Type == ErrorType.Validation);
            return ApiResult<T>.Failure(status, new ApiProblem
            {
                Status = (int)status,
                Title = validation ? "One or more validation errors occurred." : error.Type.ToString(),
                Detail = validation ? null : error.Description
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed direct server application operation.");
            return ApiResult<T>.Failure(HttpStatusCode.InternalServerError,
                errorMessage: "Unable to reach the server right now.");
        }
    }
}
