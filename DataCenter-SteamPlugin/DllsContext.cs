namespace DataCenter_SteamPlugin;

public class DllsContext
{
    public List<string> Mods { get; set; } = new();
    public List<string> Libs { get; set; } = new();
    public List<string> Plugins { get; set; } = new();
}