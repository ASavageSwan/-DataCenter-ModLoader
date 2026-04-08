using MelonLoader;
using MelonLoader.Utils;
using Steamworks;


[assembly: MelonInfo(typeof(WorkshopModLoader.WorkshopModLoaderPlugin), "Workshop Mod Loader", "1.0.0", "ASavageSwan")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace WorkshopModLoader
{
    public class WorkshopModLoaderPlugin : MelonPlugin
    {
        private const string APP_ID       = "4170200";
        private const int    TIMEOUT_SECS = 300; // 5 minutes

        private bool _weInitedSteam = false;

        public override void OnEarlyInitializeMelon()
        {
            // Subscribe as early as possible so the callback is in place
            // before OnPreModsLoaded fires later in the ML lifecycle.
            MelonEvents.OnPreModsLoaded.Subscribe(OnPreModsLoaded);
        }

        public override void OnPreInitialization()
        {
            LoggerInstance.Msg("=== Workshop Mod Loader starting ===");

            // Phase 1: Download any pending items while we still have time
            // before the Il2Cpp assembly generator runs.
            // Steamworks isn't available yet so we init it ourselves,
            // use it, then shut it down so the game can re-init normally.
            bool steamReady = TryInitSteam();
            if (steamReady)
            {
                try
                {
                    DownloadPendingWorkshopItems();
                }
                catch (Exception ex)
                {
                    LoggerInstance.Error($"Download phase failed: {ex.Message}");
                }
                finally
                {
                    if (_weInitedSteam)
                    {
                        LoggerInstance.Msg("Releasing our Steamworks session...");
                        try { SteamAPI.Shutdown(); }
                        catch (Exception ex) { LoggerInstance.Warning($"Shutdown warning (non-fatal): {ex.Message}"); }
                        _weInitedSteam = false;
                    }
                }
            }
            else
            {
                LoggerInstance.Warning("Steamworks unavailable — skipping download check.");
            }

            LoggerInstance.Msg("Download phase complete. Workshop mods will be registered at mod load time.");
        }

        // ----------------------------------------------------------------
        // This fires at exactly the right moment:
        // AFTER  Il2Cpp interop setup
        // BEFORE MelonLoader scans the Mods/ folder
        // ----------------------------------------------------------------
        private void OnPreModsLoaded()
        {
            LoggerInstance.Msg("Registering workshop mods with MelonLoader...");

            string workshopPath = FindWorkshopPath();
            if (workshopPath == null) return;

            if (!Directory.Exists(workshopPath))
            {
                LoggerInstance.Msg("Workshop content folder doesn't exist yet — no mods to load.");
                return;
            }

            string[] itemFolders = Directory.GetDirectories(workshopPath);
            if (itemFolders.Length == 0)
            {
                LoggerInstance.Msg("No workshop item folders found.");
                return;
            }

            int registered = 0;

            foreach (string itemFolder in itemFolders)
            {
                string itemId = Path.GetFileName(itemFolder);

                try
                {
                    // UserLibs first — raw assembly loads so mods can reference them
                    string userLibsDir = Path.Combine(itemFolder, "UserLibs");
                    if (Directory.Exists(userLibsDir))
                    {
                        foreach (string lib in Directory.GetFiles(userLibsDir, "*.dll"))
                        {
                            try { System.Reflection.Assembly.LoadFrom(lib); }
                            catch (Exception ex) { LoggerInstance.Warning($"  UserLib {Path.GetFileName(lib)}: {ex.Message}"); }
                        }
                    }

                    // Collect all dll paths to load — check Mods/ subfolder first,
                    // fall back to item root if no Mods/ subfolder exists
                    string modsDir  = Path.Combine(itemFolder, "Mods");
                    string scanDir  = Directory.Exists(modsDir) ? modsDir : itemFolder;

                    // Also check Plugins/ subfolder
                    string pluginsDir = Path.Combine(itemFolder, "Plugins");

                    bool itemRegistered = false;

                    // LoadMelonAssembly parses MelonInfo and creates melon instances
                    // into melonAssembly.LoadedMelons, but does NOT call Register().
                    // We call Register() ourselves to push each melon into the full
                    // ML pipeline (RegisteredMelons, Harmony patching, callbacks etc.)
                    foreach (string dllPath in Directory.GetFiles(scanDir, "*.dll", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var melonAssembly = MelonAssembly.LoadMelonAssembly(dllPath);
                            if (melonAssembly == null) continue;

                            if (melonAssembly.LoadedMelons.Count == 0)
                            {
                                LoggerInstance.Warning($"  [{itemId}] {Path.GetFileName(dllPath)} has no MelonInfo — treating as UserLib.");
                                continue;
                            }

                            foreach (var melon in melonAssembly.LoadedMelons)
                            {
                                melon.Register();
                                LoggerInstance.Msg($"  [{itemId}] {Path.GetFileName(dllPath)} registered as {(melon is MelonMod ? "Mod" : "Plugin")}.");
                                itemRegistered = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            LoggerInstance.Error($"  [{itemId}] failed to load {Path.GetFileName(dllPath)}: {ex.Message}");
                        }
                    }

                    // Plugins subfolder
                    if (Directory.Exists(pluginsDir))
                    {
                        foreach (string dllPath in Directory.GetFiles(pluginsDir, "*.dll", SearchOption.TopDirectoryOnly))
                        {
                            try
                            {
                                var melonAssembly = MelonAssembly.LoadMelonAssembly(dllPath);
                                if (melonAssembly == null) continue;

                                foreach (var melon in melonAssembly.LoadedMelons)
                                {
                                    melon.Register();
                                    LoggerInstance.Msg($"  [{itemId}] {Path.GetFileName(dllPath)} registered as Plugin.");
                                    itemRegistered = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                LoggerInstance.Error($"  [{itemId}] plugin {Path.GetFileName(dllPath)}: {ex.Message}");
                            }
                        }
                    }

                    if (itemRegistered) registered++;
                }
                catch (Exception ex)
                {
                    LoggerInstance.Error($"  [{itemId}] failed: {ex.Message}");
                }
            }

            LoggerInstance.Msg($"Workshop registration complete — {registered}/{itemFolders.Length} item(s) registered.");
        }

        // ----------------------------------------------------------------
        // PHASE 1: Init Steamworks ourselves, download pending items, shut down.
        // ----------------------------------------------------------------

        private bool TryInitSteam()
        {
            try
            {
                SteamUGC.GetNumSubscribedItems();
                LoggerInstance.Msg("Steamworks already initialised — piggy-backing.");
                _weInitedSteam = false;
                return true;
            }
            catch (InvalidOperationException) { }

            LoggerInstance.Msg("Initialising Steamworks...");
            try
            {
                bool result = SteamAPI.Init();
                if (result)
                {
                    LoggerInstance.Msg("SteamAPI.Init() succeeded.");
                    _weInitedSteam = true;
                    return true;
                }
                LoggerInstance.Warning("SteamAPI.Init() returned false.");
                return false;
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"SteamAPI.Init() threw: {ex.Message}");
                return false;
            }
        }

        private void DownloadPendingWorkshopItems()
        {
            uint subscribedCount = SteamUGC.GetNumSubscribedItems();
            if (subscribedCount == 0)
            {
                LoggerInstance.Msg("No subscribed Workshop items found.");
                return;
            }

            LoggerInstance.Msg($"Found {subscribedCount} subscribed item(s). Checking state...");

            PublishedFileId_t[] subscribedIds = new PublishedFileId_t[subscribedCount];
            SteamUGC.GetSubscribedItems(subscribedIds, subscribedCount);

            var needDownload = new List<PublishedFileId_t>();

            foreach (var itemId in subscribedIds)
            {
                EItemState state       = (EItemState)SteamUGC.GetItemState(itemId);
                bool isInstalled       = (state & EItemState.k_EItemStateInstalled)   != 0;
                bool needsUpdate       = (state & EItemState.k_EItemStateNeedsUpdate) != 0;
                bool isDownloading     = (state & EItemState.k_EItemStateDownloading) != 0;

                if (!isInstalled || needsUpdate || isDownloading)
                {
                    LoggerInstance.Msg($"  Item {itemId} queued for download (state: {state})");
                    needDownload.Add(itemId);
                    SteamUGC.DownloadItem(itemId, true);
                }
                else
                {
                    LoggerInstance.Msg($"  Item {itemId} already up to date.");
                }
            }

            if (needDownload.Count == 0)
            {
                LoggerInstance.Msg("All subscribed items already up to date.");
                return;
            }

            LoggerInstance.Msg($"Waiting for {needDownload.Count} item(s) (timeout: {TIMEOUT_SECS}s)...");
            WaitForDownloads(needDownload);
        }

        private void WaitForDownloads(List<PublishedFileId_t> items)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(TIMEOUT_SECS);

            while (DateTime.UtcNow < deadline)
            {
                SteamAPI.RunCallbacks();

                bool allDone  = true;
                int  doneCount = 0;

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
                            LoggerInstance.Msg($"  [{itemId}] {pct:F1}% ({FormatBytes(bytesDownloaded)} / {FormatBytes(bytesTotal)})");
                        }
                        else
                            LoggerInstance.Msg($"  [{itemId}] Waiting for download to start...");
                    }
                }

                if (allDone)
                {
                    LoggerInstance.Msg($"All {items.Count} item(s) downloaded successfully.");
                    return;
                }

                LoggerInstance.Msg($"  {doneCount}/{items.Count} complete — rechecking in 2s...");
                Thread.Sleep(2000);
            }

            LoggerInstance.Warning($"Timeout after {TIMEOUT_SECS}s. Some items may be incomplete.");
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------

        private string FindWorkshopPath()
        {
            try
            {
                string gameRoot     = MelonEnvironment.GameRootDirectory;
                string steamApps    = Path.GetFullPath(Path.Combine(gameRoot, "..", ".."));
                string workshopPath = Path.Combine(steamApps, "workshop", "content", APP_ID);
                LoggerInstance.Msg($"Workshop path: {workshopPath}");
                return workshopPath;
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Could not resolve workshop path: {ex.Message}");
                return null;
            }
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes >= 1024 * 1024) return $"{bytes / (1024 * 1024):F1} MB";
            if (bytes >= 1024)        return $"{bytes / 1024:F1} KB";
            return $"{bytes} B";
        }
    }
}