namespace DataCenter_SteamPlugin;

public class DllsContext
{
    public List<string> Mods { get; init; } = new();
    public List<string> Libs { get; init; } = new();
    public List<string> Plugins { get; init; } = new();
}
