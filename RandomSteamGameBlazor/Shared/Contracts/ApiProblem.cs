namespace RandomSteamGameBlazor.Shared.Contracts;

public sealed class ApiProblem
{
    public string? Title { get; set; }
    public int? Status { get; set; }
    public string? Detail { get; set; }
}
