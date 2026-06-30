/*
 * Random Steam Game
 * 
 *  Copyright (c) 2026 Kyle Givler
 * Licensed under the MIT License.
 */

using RandomSteamGame.Shared.Contracts;
using System.Net;

namespace RandomSteamGame.Client.Services;

public sealed record ApiResult<T>(
    bool IsSuccess,
    T? Value,
    HttpStatusCode? StatusCode = null,
    ApiProblem? Problem = null,
    string? ErrorMessage = null)
{
    public bool IsTooManyRequests => StatusCode == HttpStatusCode.TooManyRequests;

    public string GetErrorMessage(string fallbackMessage)
    {
        if (!string.IsNullOrWhiteSpace(Problem?.Title) && !string.IsNullOrWhiteSpace(Problem?.Detail))
        {
            return $"{Problem.Title}: {Problem.Detail}";
        }

        if (!string.IsNullOrWhiteSpace(Problem?.Title))
        {
            return Problem.Title;
        }

        return string.IsNullOrWhiteSpace(ErrorMessage) ? fallbackMessage : ErrorMessage;
    }

    public static ApiResult<T> Success(T value) => new(true, value);

    public static ApiResult<T> Failure(
        HttpStatusCode? statusCode,
        ApiProblem? problem = null,
        string? errorMessage = null) =>
        new(false, default, statusCode, problem, errorMessage);
}
