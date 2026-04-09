using System.Text.Json;
using System.Text.Json.Serialization;
using HarmonyLib.Tools;
using MelonLoader;
using MelonLoader.Utils;
using Steamworks;

namespace DataCenter_SteamPlugin;

internal class SteamWorkshop
{
    private static bool _steamworksRunning = false;
    private const int TimeOut = 300; // 5 minutes
    private DllsContext DllsContext = new();
    private const string ManifestFileName = "manifest.json";
    private static PublishedFileId_t[] subscribedIds;
    private const string AppID = "4170200";

    
    internal DllsContext Steam()
    {
        if (TryInitSteam())
        {
            try
            {
                DownloadPendingWorkshopItems();
                RegisterDllsForMelonLoading();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Download phase failed: {ex.Message}");
            }
            finally
            {
                if (_steamworksRunning)
                {
                    MelonLogger.Msg("Releasing our Streamworks session so not to break game session...");
                    try
                    {
                        SteamAPI.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"SteamAPI.Shutdown() threw: {ex.Message}");
                    }
                }
            }
        }
        else
        {
            MelonLogger.Warning("Steamworks unavailable — skipping download check.");
        }
        MelonLogger.Msg("Steam phase complete. Workshop mods will be registered at mod load time.");
        return DllsContext;
    }
    
    private static bool TryInitSteam()
    {
        try
        {
            SteamUGC.GetNumSubscribedItems();
            MelonLogger.Msg("Steamworks already initialised — piggy-backing.");
            _steamworksRunning = false;
            return true;
        }
        catch (InvalidOperationException) { }

        MelonLogger.Msg("Initialising Steamworks...");
        try
        {
            var result = SteamAPI.Init();
            if (result)
            {
                MelonLogger.Msg("SteamAPI.Init() succeeded.");
                _steamworksRunning = true;
                return true;
            }
            MelonLogger.Warning("SteamAPI.Init() returned false.");
            return false;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"SteamAPI.Init() threw: {ex.Message}");
            return false;
        }
    }
    
    private static void DownloadPendingWorkshopItems()
    {
        var subscribedCount = SteamUGC.GetNumSubscribedItems();
        if (subscribedCount == 0)
        {
            MelonLogger.Msg("No subscribed Workshop items found.");
            return;
        }
        
        MelonLogger.Msg($"Found {subscribedCount} subscribed item(s). Checking state...");
        subscribedIds = new PublishedFileId_t[subscribedCount];
        SteamUGC.GetSubscribedItems(subscribedIds, subscribedCount);
        
        var needDownload = new List<PublishedFileId_t>();
        
        foreach (var itemId in subscribedIds)
        {
            var state       = (EItemState)SteamUGC.GetItemState(itemId);
            var isInstalled       = (state & EItemState.k_EItemStateInstalled)   != 0;
            var needsUpdate       = (state & EItemState.k_EItemStateNeedsUpdate) != 0;
            var isDownloading     = (state & EItemState.k_EItemStateDownloading) != 0;

            if (!isInstalled || needsUpdate || isDownloading)
            {
                MelonLogger.Msg($"  Item {itemId} queued for download (state: {state})");
                needDownload.Add(itemId);
                SteamUGC.DownloadItem(itemId, true);
            }
            else
            {
                MelonLogger.Msg($"  Item {itemId} already up to date.");
            }
        }
        
        if (needDownload.Count == 0)
        {
            MelonLogger.Msg("All subscribed items already up to date.");
            return;
        }
        
        MelonLogger.Msg($"Waiting for {needDownload.Count} item(s) (timeout: {TimeOut}s)...");
        WaitForDownloads(needDownload);
    }

    private static void WaitForDownloads(List<PublishedFileId_t> items)
    {
        var deadline = DateTime.UtcNow.AddSeconds(TimeOut);
        while (DateTime.UtcNow < deadline)
        {
            SteamAPI.RunCallbacks();
            var allDone = true;
            var doneCount = 0;
            
            foreach (var itemId in items)
            {
                EItemState state   = (EItemState)SteamUGC.GetItemState(itemId);
                bool isInstalled   = (state & EItemState.k_EItemStateInstalled)   != 0;
                bool isDownloading = (state & EItemState.k_EItemStateDownloading) != 0;
                bool needsUpdate   = (state & EItemState.k_EItemStateNeedsUpdate) != 0;

                if (isInstalled && !needsUpdate && !isDownloading)
                    doneCount++;
                else
                {
                    allDone = false;
                    ulong bytesDownloaded, bytesTotal;
                    if (SteamUGC.GetItemDownloadInfo(itemId, out bytesDownloaded, out bytesTotal) && bytesTotal > 0)
                    {
                        float pct = (float)bytesDownloaded / bytesTotal * 100f;
                        MelonLogger.Msg($"  [{itemId}] {pct:F1}% ({FormatBytes(bytesDownloaded)} / {FormatBytes(bytesTotal)})");
                    }
                    else
                        MelonLogger.Msg($"  [{itemId}] Waiting for download to start...");
                }
            }

            if (allDone)
            {
                MelonLogger.Msg($"All {items.Count} item(s) downloaded successfully.");
                
                return;
            }

            MelonLogger.Msg($"  {doneCount}/{items.Count} complete — rechecking in 2s...");
            Thread.Sleep(2000);

        }
        MelonLogger.Warning($"Timeout after {TimeOut}s. Some items may be incomplete.");
        
    }
    
    private static string FormatBytes(ulong bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024 * 1024):F1} MB",
            >= 1024 => $"{bytes / 1024:F1} KB",
            _ => $"{bytes} B"
        };
    }

    private  void RegisterDllsForMelonLoading()
    {
        MelonLogger.Msg("Load Mod metadata into System");
        var workshopPath = FindWorkshopPath();
        if (workshopPath == null) return;
        
        if (!Directory.Exists(workshopPath))
        {
            MelonLogger.Msg("Workshop content folder doesn't exist yet — no mods to load.");
            return;
        }
        
        var itemFolders = Directory.GetDirectories(workshopPath);
        if (itemFolders.Length == 0)
        {
            MelonLogger.Msg("No workshop item folders found.");
            return;
        }
        
        foreach (var item in subscribedIds)
        {
            
            ReadManifest(workshopPath, item.ToString());
        }
    }
        
    
    private void ReadManifest(string workshopPath, string id)
    {
        var manifestPath = Path.Combine(workshopPath, id, ManifestFileName);
        MelonLogger.Msg($"Reading Manifest file: {manifestPath}");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        
        var rawData = File.ReadAllText(manifestPath);
        var data = JsonSerializer.Deserialize<Manifest>(rawData, options);

        MelonLogger.Msg(data);
        
        MelonLogger.Msg($"Setting up Dlls to be loaded in for Mod: {data.Name}...");

        if (data.Libs != null)
        {
            foreach (var lib in data.Libs)
            {
                MelonLogger.Msg($"Add lib to Load list: {lib}...");
                var path = Path.Combine(workshopPath, id, lib);
                DllsContext.Libs.Add(path);
            }
        }


        if (data.Mods != null)
        {
            foreach (var mod in data.Mods)
            {
                MelonLogger.Msg($"Add mod to Load list: {mod}...");
                var path = Path.Combine(workshopPath, id, mod);
                DllsContext.Mods.Add(path);
            }
        }

        if (data.Plugins != null)
        {
            foreach (var plugin in data.Plugins)
            {
                MelonLogger.Msg($"Add plugin to Load list: {plugin}...");
                var path = Path.Combine(workshopPath, id, plugin);
                DllsContext.Plugins.Add(path);
            }
        }
    }
    
    private static string FindWorkshopPath()
    {
        try
        {
            string gameRoot     = MelonEnvironment.GameRootDirectory;
            string steamApps    = Path.GetFullPath(Path.Combine(gameRoot, "..", ".."));
            string workshopPath = Path.Combine(steamApps, "workshop", "content", AppID);
            MelonLogger.Msg($"Workshop path: {workshopPath}");
            return workshopPath;
        }
        catch (Exception ex)
        {
            MelonLogger.Error($"Could not resolve workshop path: {ex.Message}");
            return null;
        }
    }
    
}