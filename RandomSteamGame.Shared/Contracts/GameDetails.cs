namespace RandomSteamGame.Shared.Contracts;

public class GameDetails
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string HeaderImage { get; set; } = default!;
}