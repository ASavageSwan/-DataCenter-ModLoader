using MelonLoader;
using MelonLoader.Utils;

namespace DataCenter_SteamPlugin;

internal sealed class ModLoader
{
    private List<string> _libsUnique = new();

    internal void LoadMods(DllsContext dllsContext)
    {
        MelonLogger.Msg("Load Mods into MelonLoader");
        LoadAssemblies("mods", dllsContext.Mods.Distinct());
    }

    internal void LoadPlugins(DllsContext dllsContext)
    {
        MelonLogger.Msg("Load Plugins into MelonLoader");
        _libsUnique = _libsUnique.Concat(dllsContext.Libs).Distinct().ToList();
        LoadAssemblies("libs", _libsUnique);
        LoadAssemblies("plugins", dllsContext.Plugins.Distinct());
    }
    
    private static bool IsAlreadyInDefaultFolders(string path)
    {
        var fileName = Path.GetFileName(path);
        return File.Exists(Path.Combine(MelonEnvironment.ModsDirectory, fileName))
            || File.Exists(Path.Combine(MelonEnvironment.UserLibsDirectory, fileName))
            || File.Exists(Path.Combine(MelonEnvironment.PluginsDirectory, fileName));
    }

    private static void LoadAssemblies(string label, IEnumerable<string> paths)
    {
        MelonLogger.Msg($"Loading {label} dlls");
        foreach (var path in paths)
        {
            var fileName = Path.GetFileName(path);
            if (IsAlreadyInDefaultFolders(path))
            {
                MelonLogger.Msg($"Skipping {label}: {fileName} (already present in Mods/UserLibs folder)");
                continue;
            }

            MelonLogger.Msg($"Loading {label}: {fileName}...");
            var melonAssembly = MelonAssembly.LoadMelonAssembly(path);
            if (melonAssembly == null) continue;
            if (melonAssembly.LoadedMelons.Count == 0)
            {
                MelonLogger.Warning($"Couldn't load into MelonLoader has no MelonInfo: {fileName}");
            }

            foreach (var melon in melonAssembly.LoadedMelons)
            {
                melon.Register();
            }
            MelonLogger.Msg($"Loaded Dll into Melon: {fileName}");
        }
    }
}