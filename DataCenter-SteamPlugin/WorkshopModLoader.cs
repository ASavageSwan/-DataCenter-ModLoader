using MelonLoader;

[assembly: MelonInfo(typeof(DataCenter_SteamPlugin.WorkshopModLoader), "Workshop Mod Loader", "1.0.0", "ASavageSwan")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace DataCenter_SteamPlugin;

public class WorkshopModLoader : MelonPlugin
{
    private DllsContext _dllsContext;
    private readonly ModLoader _modLoader = new();

    public override void OnEarlyInitializeMelon()
    {
        var dlls = new DllsContext();
        var steamWorkShop = new SteamWorkshop();
        _dllsContext = steamWorkShop.Steam(dlls);
        _modLoader.LoadPlugins(_dllsContext);
    }

    public override void OnPreInitialization()
    {
        _modLoader.LoadMods(_dllsContext);
    }
}
