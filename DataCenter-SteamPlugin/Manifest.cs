namespace DataCenter_SteamPlugin;

public class Manifest
{
    public string Name { get; init; } = string.Empty;
    public List<string> Mods { get; init; } = new();
    public List<string> Libs { get; init; } = new();
    public List<string> Plugins { get; init; } = new();
}
