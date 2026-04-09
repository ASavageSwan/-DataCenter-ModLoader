using MelonLoader;
using MelonLoader.Utils;

namespace DataCenter_SteamPlugin;

internal sealed class ModLoader
{
    private List<string> _libsUnique = new();
    
    internal void LoadMods(DllsContext dllsContext)
    {
        MelonLogger.Msg("Load Mod metadata into System");
        
        // As Plugins will also get 
        var tempLibs = _libsUnique;
        tempLibs.AddRange(dllsContext.Libs);
        _libsUnique = tempLibs.Distinct().ToList();
        
        LoadUserLibs(_libsUnique);
        LoadMods(dllsContext.Mods.Distinct().ToList());
    }
    
    private void LoadUserLibs(List<string> libs)
    {
        MelonLogger.Msg($"Loading user libs dlls");
        foreach (var lib in libs)
        {
            var libName = Path.GetFileName(lib);
            MelonLogger.Msg($"Loading lib: {libName}...");
            MelonAssembly.LoadMelonAssembly(lib);
        }
    }
    
    private void LoadMods(List<string> mods)
    {
        MelonLogger.Msg($"Loading user libs dlls");
        foreach (var mod in mods)
        {
            var moddlls = Path.GetFileName(mod);
            MelonLogger.Msg($"Loading mod: {mod}...");
            MelonLogger.Msg($"Loading Mod dlls: {moddlls}...");
            Console.Read();
            var melonAssembly = MelonAssembly.LoadMelonAssembly(moddlls);
            if (melonAssembly == null) continue;
            if (melonAssembly.LoadedMelons.Count == 0)
            {
                MelonLogger.Warning($"Couldn't load into MelonLoader has no MelonInfo: {mod}");
            }

            foreach (var melon in melonAssembly.LoadedMelons)
            {
                melon.Register();
            }
        }
    }

    private void LoadPlugins(List<string> plugins)
    {
        
    }
}