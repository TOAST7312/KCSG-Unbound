using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Verse;
using RimWorld;
using System.Collections.Concurrent;

namespace KCSG
{
    /// <summary>
    /// Serializable data structure that holds registry information between game sessions
    /// </summary>
    [Serializable]
    public class SymbolRegistryCacheData
    {
        // Cache version for compatibility checks
        public int CacheVersion = 2; // Increased to 2 for the new cache format
        
        // List of registered structure names (kept for backwards compatibility)
        public List<string> RegisteredSymbols = new List<string>();
        
        // Dictionary of mod IDs and their versions
        public Dictionary<string, string> ModVersions = new Dictionary<string, string>();
        
        // NEW: Dictionary of mod IDs to their cached symbols
        // This allows us to selectively update only changed mods
        public Dictionary<string, ModSymbolCache> ModSymbols = new Dictionary<string, ModSymbolCache>();
        
        /// <summary>
        /// Modular cache entry for a single mod
        /// </summary>
        [Serializable]
        public class ModSymbolCache
        {
            // Version of the mod when these symbols were cached
            public string Version { get; set; }
            
            // List of symbols defined by this mod
            public List<string> Symbols { get; set; } = new List<string>();
            
            // Optional metadata
            public DateTime LastUpdated { get; set; }
            public string ModName { get; set; }
            
            // Track which symbols are actually used in the game
            public Dictionary<string, int> SymbolUsageCounts { get; set; } = new Dictionary<string, int>();
        }
        
        /// <summary>
        /// Determines if the cache is valid by checking version 
        /// </summary>
        public bool IsValid()
        {
            // Check if cache is completely empty
            if ((RegisteredSymbols.Count == 0 || ModVersions.Count == 0) && ModSymbols.Count == 0)
                return false;
                
            return true; // The cache is now always valid, as we do partial updates
        }
        
        /// <summary>
        /// Updates the cache with current mod information
        /// </summary>
        public void Update()
        {
            // Clear old data in legacy format
            ModVersions.Clear();
            
            // Update all registered symbols (legacy format)
            RegisteredSymbols = SymbolRegistry.RegisteredSymbols.ToList();
            
            // Update version info for all mods
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                if (SymbolRegistryCache.HasCustomGenDefs(mod))
                {
                    ModVersions[mod.PackageId] = mod.VersionString();
                    
                    // Check if this mod needs a cache entry
                    if (!ModSymbols.ContainsKey(mod.PackageId))
                    {
                        // Create a new cache entry for this mod with all registered symbols
                        // This is a best-effort approach - some symbols might not actually belong to this mod
                        ModSymbols[mod.PackageId] = new ModSymbolCache
                        {
                            Version = mod.VersionString(),
                            Symbols = SymbolRegistry.RegisteredSymbols.ToList(),
                            LastUpdated = DateTime.Now,
                            ModName = mod.Name
                        };
                    }
                }
            }
        }
        
        /// <summary>
        /// Updates the cache with symbols from a specific mod
        /// </summary>
        public void UpdateModSymbols(ModContentPack mod, List<string> symbols)
        {
            if (mod == null || symbols == null)
                return;
                
            var modCache = new ModSymbolCache
            {
                Version = mod.VersionString(),
                Symbols = new List<string>(symbols),
                LastUpdated = DateTime.Now,
                ModName = mod.Name
            };
            
            ModSymbols[mod.PackageId] = modCache;
            Log.Message($"[KCSG Unbound] Updated cache for {mod.Name} with {symbols.Count} symbols");
        }
    }

    /// <summary>
    /// Provides caching and optimization for the SymbolRegistry
    /// </summary>
    public static class SymbolRegistryCache
    {
        // Safety controls
        private static bool safeMode = true; // Start in safe mode by default
        private static int maxFilesToScan = 50; // Limit file scanning
        private static int maxStructuresPerMod = 200; // Limit structures per mod
        private static int totalRegistrationLimit = 1000; // Total structure limit for safety
        private static int currentTotalRegistrations = 0;
        
        // Cache for quick symbol lookup by hash
        private static Dictionary<ushort, string> hashToSymbolCache = new Dictionary<ushort, string>();
        
        // Cache for structure lookup across sessions
        private static Dictionary<string, HashSet<string>> modToPrefixCache = new Dictionary<string, HashSet<string>>();
        private static Dictionary<string, HashSet<string>> prefixToStructuresCache = new Dictionary<string, HashSet<string>>();
        
        // Track mods we've already scanned
        private static HashSet<string> scannedMods = new HashSet<string>();
        private static ConcurrentQueue<string> modScanQueue = new ConcurrentQueue<string>();
        private static bool isScanning = false;
        
        // Cache file path
        private static readonly string cachePath = Path.Combine(GenFilePaths.ConfigFolderPath, "KCSGSymbolCache.xml");
        private static readonly string legacyCachePath = Path.Combine(GenFilePaths.ConfigFolderPath, "KCSG_Unbound_Registry.xml");
        
        // Track cache changes to know when to save
        private static bool cacheModified = false;
        
        // Store the cache data
        private static SymbolRegistryCacheData cacheData = new SymbolRegistryCacheData();
        
        /// <summary>
        /// Checks if a mod has CustomGenDefs folder or other potential structure layout folders
        /// </summary>
        public static bool HasCustomGenDefs(ModContentPack mod)
        {
            if (mod?.RootDir == null)
                return false;
                
            // Check multiple potential paths where structure layouts might be stored
            string[] potentialPaths = new[] {
                Path.Combine(mod.RootDir, "Defs", "CustomGenDefs"),
                Path.Combine(mod.RootDir, "Defs", "StructureLayoutDefs"),
                Path.Combine(mod.RootDir, "Defs", "StructureDefs"),
                Path.Combine(mod.RootDir, "Defs", "StructureLayout"),
                Path.Combine(mod.RootDir, "Defs", "Structure"),
                Path.Combine(mod.RootDir, "1.5", "Defs", "CustomGenDefs"),
                Path.Combine(mod.RootDir, "1.5", "Defs", "StructureLayoutDefs"),
                Path.Combine(mod.RootDir, "1.5", "Defs", "StructureDefs"),
                Path.Combine(mod.RootDir, "1.5", "Defs", "StructureLayout"),
                Path.Combine(mod.RootDir, "1.5", "Defs", "Structure")
            };

            foreach (var path in potentialPaths)
            {
                if (Directory.Exists(path))
                    return true;
            }
            
            // Also check for any XML file in the Defs directory that might contain structure layouts
            try {
                string defsPath = Path.Combine(mod.RootDir, "Defs");
                if (Directory.Exists(defsPath))
                {
                    foreach (var file in Directory.GetFiles(defsPath, "*.xml", SearchOption.AllDirectories))
                    {
                        try {
                            string content = File.ReadAllText(file);
                            if (content.Contains("StructureLayoutDef") || 
                                content.Contains("<KCSG.") ||
                                content.Contains("<layouts>") ||
                                content.Contains("<symbolDef"))
                            {
                                return true;
                            }
                        }
                        catch {
                            // Ignore errors reading files
                        }
                    }
                }
            }
            catch {
                // Ignore errors scanning directories
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets list of mods that need to be rescanned (new or changed version)
        /// </summary>
        public static List<ModContentPack> GetModsToRescan()
        {
            var modsToRescan = new List<ModContentPack>();
            
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                if (!HasCustomGenDefs(mod))
                    continue;
                    
                // If mod is new or version changed
                bool needsRescan = false;
                
                if (cacheData.ModSymbols.TryGetValue(mod.PackageId, out var symbolCache))
                {
                    // Mod exists in cache, check if version changed
                    if (symbolCache.Version != mod.VersionString())
                    {
                        needsRescan = true;
                        Log.Message($"[KCSG Unbound] Mod {mod.Name} version changed from {symbolCache.Version} to {mod.VersionString()}, rescanning");
                    }
                }
                else
                {
                    // Mod is new
                    needsRescan = true;
                    Log.Message($"[KCSG Unbound] New mod with structures detected: {mod.Name}, scanning");
                }
                
                if (needsRescan)
                {
                    modsToRescan.Add(mod);
                }
            }
            
            return modsToRescan;
        }
        
        /// <summary>
        /// Registers symbols from the cache for all mods that haven't changed
        /// </summary>
        public static void RegisterCachedSymbols()
        {
            int totalRegistered = 0;
            int modsRegistered = 0;
            
            // Process each mod in the cache
            foreach (var kvp in cacheData.ModSymbols)
            {
                string modId = kvp.Key;
                var symbolCache = kvp.Value;
                
                // Check if this mod is still loaded
                var mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.PackageId == modId);
                if (mod == null)
                {
                    Log.Message($"[KCSG Unbound] Mod {modId} ({symbolCache.ModName}) is no longer loaded, skipping {symbolCache.Symbols.Count} cached symbols");
                    continue;
                }
                
                // Check if version is the same
                if (mod.VersionString() != symbolCache.Version)
                {
                    Log.Message($"[KCSG Unbound] Mod {mod.Name} version changed, skipping cached symbols");
                    continue;
                }
                
                // Register all symbols from this mod
                int registered = 0;
                foreach (var symbol in symbolCache.Symbols)
                {
                    if (!SymbolRegistry.HasResolver(symbol))
                    {
                        SymbolRegistry.Register(symbol);
                        registered++;
                    }
                }
                
                if (registered > 0)
                {
                    Log.Message($"[KCSG Unbound] Registered {registered} cached symbols from {mod.Name}");
                    totalRegistered += registered;
                    modsRegistered++;
                }
            }
            
            Log.Message($"Registered {totalRegistered} symbols from {modsRegistered} cached mods");
        }
        
        /// <summary>
        /// Initialize the cache system
        /// </summary>
        public static void Initialize()
        {
            try
            {
                Log.Message("[KCSG Unbound] Initializing symbol registry cache");
                Diagnostics.LogDiagnostic("Initializing symbol registry cache");
                
                // Configure safety based on settings
                safeMode = !Prefs.DevMode;
                if (KCSGUnboundSettings.HighPerformanceMode)
                {
                    maxFilesToScan = 20;
                    maxStructuresPerMod = 100;
                    totalRegistrationLimit = 500;
                }
                else
                {
                    maxFilesToScan = 50;
                    maxStructuresPerMod = 200;
                    totalRegistrationLimit = 1000;
                }
                
                // Clear existing caches
                hashToSymbolCache.Clear();
                modToPrefixCache.Clear();
                prefixToStructuresCache.Clear();
                scannedMods.Clear();
                currentTotalRegistrations = 0;
                
                // Try to load cached data
                LoadCache();
                
                // Register for game exit to save cache
                LongEventHandler.ExecuteWhenFinished(() => {
                    // Schedule periodic cache saves
                    ScheduleCacheSaving();
                });
                
                Diagnostics.LogDiagnostic($"Symbol cache initialized with {hashToSymbolCache.Count} hash mappings");
                Log.Message($"[KCSG Unbound] Symbol cache initialized with {hashToSymbolCache.Count} hash mappings");
            }
            catch (Exception ex)
            {
                Diagnostics.LogError($"Error initializing symbol cache: {ex}");
                Log.Error($"[KCSG Unbound] Error initializing symbol cache: {ex}");
            }
        }
        
        /// <summary>
        /// Schedule regular cache saving
        /// </summary>
        private static void ScheduleCacheSaving()
        {
            try
            {
                Diagnostics.LogDiagnostic("Scheduling cache saving");
                
                // Save immediately
                SaveCache();
                
                // Register for game exit
                LongEventHandler.ExecuteWhenFinished(() => {
                    try
                    {
                        Diagnostics.LogDiagnostic("Executing deferred cache save");
                        SaveCache();
                    }
                    catch (Exception ex)
                    {
                        Diagnostics.LogError($"Error in deferred cache save: {ex}");
                    }
                });
                
                // Also try to register for OnGUI to catch finalization events
                try
                {
                    // Get the OnPreMainMenuWindowContent event method from Current.ProgramState
                    var programStateType = typeof(ProgramState);
                    var eventInfo = programStateType.GetEvent("OnPreMainMenuWindowContent");
                    
                    if (eventInfo != null)
                    {
                        // Create a delegate to call SaveCache
                        Action saveAction = () => SaveCache();
                        
                        // Add the delegate to the event
                        eventInfo.AddEventHandler(Current.ProgramState, saveAction);
                        Diagnostics.LogDiagnostic("Registered for OnPreMainMenuWindowContent event");
                    }
                }
                catch (Exception ex)
                {
                    Diagnostics.LogError($"Error registering for UI events: {ex}");
                }
            }
            catch (Exception ex)
            {
                Diagnostics.LogError($"Error scheduling cache saving: {ex}");
            }
        }
        
        /// <summary>
        /// Add a mod to the scan queue for incremental scanning
        /// </summary>
        public static void AddModToScanQueue(string modId, bool highPriority = false)
        {
            if (scannedMods.Contains(modId))
                return;
                
            if (highPriority)
            {
                // Create a new queue with this mod at the front
                var newQueue = new ConcurrentQueue<string>();
                newQueue.Enqueue(modId);
                
                // Add all other queued mods
                string otherModId;
                while (modScanQueue.TryDequeue(out otherModId))
                {
                    newQueue.Enqueue(otherModId);
                }
                
                modScanQueue = newQueue;
            }
            else
            {
                modScanQueue.Enqueue(modId);
            }
            
            // If not already scanning, start the process
            if (!isScanning)
            {
                StartIncrementalScan();
            }
        }
        
        /// <summary>
        /// Start scanning mods incrementally
        /// </summary>
        private static void StartIncrementalScan()
        {
            if (isScanning || modScanQueue.Count == 0)
                return;
                
            isScanning = true;
            
            LongEventHandler.QueueLongEvent(() => {
                try
                {
                    // Process one mod at a time
                    string modId;
                    if (modScanQueue.TryDequeue(out modId))
                    {
                        ScanModForStructures(modId);
                        scannedMods.Add(modId);
                        cacheModified = true;
                    }
                    
                    // If more mods in queue, continue scanning
                    if (modScanQueue.Count > 0)
                    {
                        StartIncrementalScan();
                    }
                    else
                    {
                        isScanning = false;
                        if (cacheModified)
                        {
                            SaveCache();
                            cacheModified = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error during incremental mod scanning: {ex}");
                    isScanning = false;
                }
            }, "KCSG_IncrementalModScan", false, null);
        }
        
        /// <summary>
        /// Scan a mod for structure definitions
        /// </summary>
        private static void ScanModForStructures(string modId)
        {
            try
            {
                // Find mod by packageId
                var mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => 
                    m.PackageId.Equals(modId, StringComparison.OrdinalIgnoreCase) ||
                    m.PackageId.Contains(modId));
                    
                if (mod == null)
                    return;
                    
                Log.Message($"[KCSG Unbound] Scanning mod {mod.Name} for structures");
                Diagnostics.LogDiagnostic($"Scanning mod {mod.Name} (ID: {mod.PackageId}) for structures");
                
                // High-priority folders to search first
                List<string> highPriorityFolders = new List<string>
                {
                    Path.Combine(mod.RootDir, "Defs", "StructureLayoutDefs"),
                    Path.Combine(mod.RootDir, "1.5", "Defs", "StructureLayoutDefs"),
                    Path.Combine(mod.RootDir, "Defs", "CustomGenDefs"),
                    Path.Combine(mod.RootDir, "1.5", "Defs", "CustomGenDefs"),
                    Path.Combine(mod.RootDir, "Defs", "StructureDefs"),
                    Path.Combine(mod.RootDir, "1.5", "Defs", "StructureDefs"),
                    Path.Combine(mod.RootDir, "Defs", "StructureLayout"),
                    Path.Combine(mod.RootDir, "1.5", "Defs", "StructureLayout"),
                    Path.Combine(mod.RootDir, "Defs", "Structure"),
                    Path.Combine(mod.RootDir, "1.5", "Defs", "Structure")
                };
                
                // Use safety limits
                int maxFiles = safeMode ? Math.Min(maxFilesToScan, 20) : 
                             (KCSGUnboundSettings.HighPerformanceMode ? 20 : maxFilesToScan);
                int filesScanned = 0;
                int structuresFound = 0;
                int structuresRegistered = 0;
                
                // Check if we already have prefixes for this mod
                if (!modToPrefixCache.TryGetValue(modId, out var modPrefixes))
                {
                    modPrefixes = new HashSet<string>();
                    modToPrefixCache[modId] = modPrefixes;
                }
                
                // Log the directories we will search
                foreach (var folder in highPriorityFolders)
                {
                    if (Directory.Exists(folder))
                    {
                        Diagnostics.LogDiagnostic($"Found structure folder: {folder}");
                    }
                }
                
                // First scan high-priority folders
                foreach (var folderPath in highPriorityFolders)
                {
                    if (!Directory.Exists(folderPath))
                        continue;
                        
                    Diagnostics.LogDiagnostic($"Scanning folder: {folderPath}");
                        
                    // Scan XML files in this folder
                    foreach (var filePath in Directory.GetFiles(folderPath, "*.xml", SearchOption.AllDirectories))
                    {
                        if (filesScanned++ >= maxFiles || structuresRegistered >= maxStructuresPerMod)
                            break;
                            
                        int newStructures = FastScanXmlFile(filePath, modPrefixes, maxStructuresPerMod - structuresRegistered);
                        structuresFound += newStructures;
                        structuresRegistered += newStructures;
                        
                        Diagnostics.LogDiagnostic($"Scanned file: {Path.GetFileName(filePath)}, found {newStructures} structures");
                        
                        // Safety check for total registrations
                        if (currentTotalRegistrations >= totalRegistrationLimit && safeMode)
                        {
                            Log.Warning($"[KCSG Unbound] Registration limit reached ({totalRegistrationLimit}), stopping scan for safety");
                            Diagnostics.LogWarning($"Registration limit reached ({totalRegistrationLimit}), stopping scan for safety");
                            break;
                        }
                    }
                    
                    if (filesScanned >= maxFiles || structuresRegistered >= maxStructuresPerMod)
                        break;
                }
                
                // If we haven't found many structures, also scan the general Defs folder
                if (structuresFound < 5 && filesScanned < maxFiles && structuresRegistered < maxStructuresPerMod)
                {
                    string defsPath = Path.Combine(mod.RootDir, "Defs");
                    if (Directory.Exists(defsPath))
                    {
                        Log.Message($"[KCSG Unbound] Scanning general Defs folder for {mod.Name}");
                        Diagnostics.LogDiagnostic($"Scanning general Defs folder for {mod.Name}: {defsPath}");
                        
                        // Scan for XML files that might contain structure layouts
                        var candidates = Directory.GetFiles(defsPath, "*.xml", SearchOption.AllDirectories)
                            .Where(f => !highPriorityFolders.Any(hpf => f.StartsWith(hpf)))
                            .Take(maxFiles - filesScanned);
                            
                        Diagnostics.LogDiagnostic($"Found {candidates.Count()} candidate files in general Defs folder");
                            
                        foreach (var filePath in candidates)
                        {
                            try
                            {
                                // Quick check if the file might contain structure layouts
                                string content = File.ReadAllText(filePath);
                                bool isKCSGFile = content.Contains("<KCSG.") || 
                                                 content.Contains("StructureLayoutDef") ||
                                                 content.Contains("<layouts>") ||
                                                 content.Contains("<symbolDef");
                                                 
                                if (!isKCSGFile)
                                    continue;
                                
                                if (filesScanned++ >= maxFiles || structuresRegistered >= maxStructuresPerMod)
                                    break;
                                    
                                int newStructures = FastScanXmlFile(filePath, modPrefixes, maxStructuresPerMod - structuresRegistered);
                                structuresFound += newStructures;
                                structuresRegistered += newStructures;
                                
                                Diagnostics.LogDiagnostic($"Scanned general file: {Path.GetFileName(filePath)}, found {newStructures} structures");
                                
                                // Safety check for total registrations
                                if (currentTotalRegistrations >= totalRegistrationLimit && safeMode)
                                    break;
                            }
                            catch (Exception ex)
                            {
                                // Log errors but continue processing
                                Diagnostics.LogError($"Error scanning file {Path.GetFileName(filePath)}: {ex.Message}");
                            }
                        }
                    }
                }
                
                // If we didn't find any structures, try a broader search
                if (structuresFound == 0 && filesScanned < maxFiles && structuresRegistered < maxStructuresPerMod)
                {
                    // Generate likely prefixes from mod name/ID
                    var inferredPrefixes = GenerateInferredPrefixes(mod);
                    
                    Diagnostics.LogDiagnostic($"Generating inferred prefixes for {mod.Name}: {string.Join(", ", inferredPrefixes)}");
                    
                    foreach (var prefix in inferredPrefixes)
                    {
                        modPrefixes.Add(prefix);
                    }
                    
                    // Register common variants for these prefixes
                    RegisterPrefixVariants(inferredPrefixes);
                    
                    Diagnostics.LogDiagnostic($"Registered variants for inferred prefixes");
                }
                
                Log.Message($"[KCSG Unbound] Found {structuresFound} structures in {filesScanned} files from {mod.Name}");
                Diagnostics.LogDiagnostic($"Found {structuresFound} structures in {filesScanned} files from {mod.Name}");
                
                // If we found structures, update the cache
                if (structuresFound > 0)
                {
                    // Mark the cache as modified so it will be saved
                    cacheModified = true;
                    
                    // Update the structure data in the registry
                    var registeredNames = SymbolRegistry.RegisteredSymbols.Where(s => 
                        // Only include structures that have one of our detected prefixes
                        modPrefixes.Any(p => s.StartsWith(p))).ToList();
                        
                    if (registeredNames.Count > 0)
                    {
                        // Update the cache data for this mod
                        try
                        {
                            cacheData.UpdateModSymbols(mod, registeredNames);
                            Log.Message($"[KCSG Unbound] Updated cache for {mod.Name} with {registeredNames.Count} symbols");
                            Diagnostics.LogDiagnostic($"Updated cache for {mod.Name} with {registeredNames.Count} symbols");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[KCSG Unbound] Error updating cache for {mod.Name}: {ex.Message}");
                            Diagnostics.LogError($"Error updating cache for {mod.Name}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Diagnostics.LogDiagnostic($"No structures found for {mod.Name} after scanning {filesScanned} files");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Error scanning mod {modId}: {ex.Message}");
                Diagnostics.LogError($"Error scanning mod {modId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Fast scan an XML file for structure definitions without full parsing
        /// </summary>
        private static int FastScanXmlFile(string filePath, HashSet<string> modPrefixes, int remainingSlots = int.MaxValue)
        {
            int structuresFound = 0;
            try
            {
                if (remainingSlots <= 0) return 0;
                
                string content = File.ReadAllText(filePath);
                
                // Extra logging for diagnostic purposes
                string fileName = Path.GetFileName(filePath);
                Diagnostics.LogVerbose($"Scanning file {fileName} for potential structures");
                
                // Check if this might be a KCSG file - use a broader set of markers
                bool isKCSGFile = content.Contains("<KCSG.") || 
                                 content.Contains("StructureLayoutDef") ||
                                 content.Contains("<layouts>") ||
                                 content.Contains("<symbolDef") ||
                                 content.Contains("GenStep") ||
                                 content.Contains("MapGenerator") ||
                                 content.Contains("<structures>") ||
                                 content.Contains("<symbols>") ||
                                 content.Contains("<layout");
                                 
                // If not obviously a KCSG file, try looking for prefixes and structure terms
                if (!isKCSGFile)
                {
                    // Check for any common prefixes in the file
                    bool hasAnyKnownPrefix = false;
                    foreach (var prefix in modPrefixes)
                    {
                        if (content.Contains(prefix))
                        {
                            hasAnyKnownPrefix = true;
                            break;
                        }
                    }
                    
                    // Check for common structure-related terms
                    bool hasStructureTerms = content.Contains("structure") || 
                                            content.Contains("layout") || 
                                            content.Contains("symbol") ||
                                            content.Contains("settlement") ||
                                            content.Contains("building") ||
                                            content.Contains("generator");
                                            
                    // Mark as KCSG file if it has both prefixes and terms
                    if (hasAnyKnownPrefix && hasStructureTerms)
                    {
                        isKCSGFile = true;
                        Diagnostics.LogVerbose($"File {fileName} matched based on prefix and structure terms");
                    }
                }
                
                // If not a KCSG file, return
                if (!isKCSGFile)
                {
                    Diagnostics.LogVerbose($"File {fileName} doesn't appear to contain structure definitions");
                    return 0;
                }
                
                // Log that we found a relevant file
                Log.Message($"[KCSG Unbound] Found potential structure definitions in {fileName}");
                
                // If this file mentions StructureLayoutDef, create a basic layout even if we don't find any specific def names
                if (remainingSlots > 0 && content.Contains("StructureLayoutDef") && !content.Contains("<defName>"))
                {
                    // Create a default structure layout based on the file name
                    string baseName = Path.GetFileNameWithoutExtension(filePath).Replace(" ", "");
                    
                    if (!string.IsNullOrEmpty(baseName) && baseName.Length > 2)
                    {
                        string defName = $"{baseName}Layout";
                        
                        // Register this layout explicitly
                        if (!SymbolRegistry.IsDefRegistered(defName))
                        {
                            try
                            {
                                object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                SymbolRegistry.RegisterDef(defName, placeholderDef);
                                structuresFound++;
                                currentTotalRegistrations++;
                                
                                // Extract potential prefix
                                string prefix = ExtractPrefix(defName);
                                if (!string.IsNullOrEmpty(prefix))
                                {
                                    modPrefixes.Add(prefix);
                                }
                                
                                Log.Message($"[KCSG Unbound] Created implicit layout from file name: {defName}");
                            }
                            catch (Exception ex)
                            {
                                Diagnostics.LogVerbose($"Error registering implicit layout {defName}: {ex.Message}");
                            }
                        }
                    }
                }
                
                // Look for structure def names
                int pos = 0;
                while (structuresFound < remainingSlots)
                {
                    // Find defName elements
                    int defNameStart = content.IndexOf("<defName>", pos);
                    if (defNameStart == -1) break;
                    
                    int defNameEnd = content.IndexOf("</defName>", defNameStart);
                    if (defNameEnd == -1) break;
                    
                    // Extract the actual name
                    string defName = content.Substring(defNameStart + 9, defNameEnd - defNameStart - 9).Trim();
                    
                    if (!string.IsNullOrEmpty(defName))
                    {
                        // Register the structure
                        if (RegisterStructureName(defName))
                        {
                            structuresFound++;
                            currentTotalRegistrations++;
                            
                            // Log each successful registration
                            Log.Message($"[KCSG Unbound] Registered structure: {defName}");
                            
                            // Safety check
                            if (currentTotalRegistrations >= totalRegistrationLimit && safeMode)
                            {
                                Log.Warning($"[KCSG Unbound] Registration limit reached ({totalRegistrationLimit}), stopping scan");
                                break;
                            }
                        }
                        
                        // Extract prefix for future use
                        string prefix = ExtractPrefix(defName);
                        if (!string.IsNullOrEmpty(prefix))
                        {
                            modPrefixes.Add(prefix);
                            
                            // Make sure we have a structure set for this prefix
                            if (!prefixToStructuresCache.TryGetValue(prefix, out var structures))
                            {
                                structures = new HashSet<string>();
                                prefixToStructuresCache[prefix] = structures;
                            }
                            
                            structures.Add(defName);
                        }
                    }
                    
                    // Move to next part of file
                    pos = defNameEnd;
                }
                
                // Also look for possible structure names in other patterns
                if (structuresFound < remainingSlots)
                {
                    // Look for <key>StructureName</key> pattern
                    pos = 0;
                    while (structuresFound < remainingSlots)
                    {
                        int keyStart = content.IndexOf("<key>", pos);
                        if (keyStart == -1) break;
                        
                        int keyEnd = content.IndexOf("</key>", keyStart);
                        if (keyEnd == -1) break;
                        
                        // Extract the key content
                        string keyContent = content.Substring(keyStart + 5, keyEnd - keyStart - 5).Trim();
                        
                        if (!string.IsNullOrEmpty(keyContent) && keyContent.Length > 3 && 
                            (keyContent.Contains("Structure") || keyContent.Contains("Layout") || keyContent.Contains("Symbol")))
                        {
                            if (RegisterStructureName(keyContent))
                            {
                                structuresFound++;
                                currentTotalRegistrations++;
                                
                                // Log each successful registration
                                Log.Message($"[KCSG Unbound] Registered structure from key: {keyContent}");
                                
                                // Safety check
                                if (currentTotalRegistrations >= totalRegistrationLimit && safeMode)
                                    break;
                            }
                        }
                        
                        // Move to next part of file
                        pos = keyEnd;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log errors but continue processing
                Log.Warning($"[KCSG Unbound] Error scanning file {Path.GetFileName(filePath)}: {ex.Message}");
            }
            
            return structuresFound;
        }
        
        /// <summary>
        /// Extract a prefix from a def name (e.g., "VFED_Structure" -> "VFED_")
        /// </summary>
        private static string ExtractPrefix(string defName)
        {
            int underscorePos = defName.IndexOf('_');
            if (underscorePos > 0 && underscorePos < defName.Length - 1)
            {
                return defName.Substring(0, underscorePos + 1);
            }
            return null;
        }
        
        /// <summary>
        /// Generate potential prefixes from a mod's name or packageId
        /// </summary>
        private static List<string> GenerateInferredPrefixes(ModContentPack mod)
        {
            List<string> prefixes = new List<string>();
            
            // 1. Acronym from mod name (e.g., "Amazing Cool Mod" -> "ACM_")
            string modName = mod.Name;
            if (!string.IsNullOrEmpty(modName))
            {
                string acronym = string.Join("", modName.Split(' ')
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => char.ToUpper(s[0])));
                    
                if (acronym.Length >= 2)
                {
                    prefixes.Add(acronym + "_");
                }
            }
            
            // 2. First part of packageId (e.g., "author.coolmod" -> "author_")
            string packageId = mod.PackageId;
            if (!string.IsNullOrEmpty(packageId) && packageId.Contains('.'))
            {
                string authorTag = packageId.Split('.')[0];
                if (!string.IsNullOrEmpty(authorTag))
                {
                    prefixes.Add(authorTag.ToUpperInvariant() + "_");
                }
            }
            
            // 3. Sanitized mod name prefix
            if (!string.IsNullOrEmpty(modName))
            {
                // Take first word of mod name
                string firstWord = modName.Split(' ').FirstOrDefault();
                if (!string.IsNullOrEmpty(firstWord) && firstWord.Length >= 2)
                {
                    prefixes.Add(firstWord.ToUpperInvariant() + "_");
                }
            }
            
            return prefixes;
        }
        
        /// <summary>
        /// Register common variants for a set of prefixes
        /// </summary>
        private static void RegisterPrefixVariants(List<string> prefixes)
        {
            string[] commonTerms = { 
                "Structure", "Layout", "Base", "Camp", "Outpost", "Settlement",
                "Hive", "Nest", "Bunker", "Tower", "Fortress", "Castle"
            };
            
            foreach (var prefix in prefixes)
            {
                foreach (var term in commonTerms)
                {
                    // Register base term
                    string baseTerm = $"{prefix}{term}";
                    RegisterStructureName(baseTerm);
                    
                    // Register a few variants
                    for (int i = 1; i <= 3; i++)
                    {
                        RegisterStructureName($"{baseTerm}{i}");
                    }
                    
                    // Register letter variants
                    for (char c = 'A'; c <= 'E'; c++)
                    {
                        RegisterStructureName($"{baseTerm}{c}");
                    }
                }
            }
        }
        
        /// <summary>
        /// Register a structure name with the symbol registry
        /// </summary>
        private static bool RegisterStructureName(string defName)
        {
            try
            {
                // Don't try to register if we've reached the limit
                if (currentTotalRegistrations >= totalRegistrationLimit && safeMode)
                    return false;
                    
                if (!SymbolRegistry.IsDefRegistered(defName))
                {
                    // Try different methods to register the definition
                    try
                    {
                        // First try the standard method
                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                        cacheModified = true;
                        return true;
                    }
                    catch
                    {
                        // If that fails, try using a basic placeholder def
                        try
                        {
                            // Create a basic placeholder using the Def class pattern
                            var basicDef = new BasicPlaceholderDef { defName = defName };
                            SymbolRegistry.RegisterDef(defName, basicDef);
                            cacheModified = true;
                            return true;
                        }
                        catch
                        {
                            // If that also fails, try using a direct registration without a def object
                            try
                            {
                                // Just register the name directly
                                SymbolRegistry.Register(defName);
                                cacheModified = true;
                                return true;
                            }
                            catch (Exception ex)
                            {
                                Diagnostics.LogVerbose($"All registration methods failed for {defName}: {ex.Message}");
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue
                Diagnostics.LogVerbose($"Error in RegisterStructureName for {defName}: {ex.Message}");
            }
            return false;
        }
        
        /// <summary>
        /// Save cached structure information to persistent storage
        /// </summary>
        public static void SaveCache()
        {
            try
            {
                // Always attempt to save even if not modified during dev
                bool devMode = Prefs.DevMode;
                if (!cacheModified && !devMode)
                {
                    Diagnostics.LogDiagnostic("Cache not modified, skipping save");
                    return;
                }
                    
                Diagnostics.LogDiagnostic($"Saving symbol cache to disk at {cachePath}");
                Log.Message($"[KCSG Unbound] Saving symbol cache to {cachePath}");
                
                // Update the cache data from the registry first
                try
                {
                    cacheData.Update();
                    Diagnostics.LogDiagnostic($"Updated cache data with {cacheData.RegisteredSymbols.Count} symbols");
                }
                catch (Exception ex)
                {
                    Diagnostics.LogError($"Error updating cache data: {ex}");
                }
                
                // Create cache data
                var data = new CacheData
                {
                    Version = 1,
                    Timestamp = DateTime.Now.ToString(),
                    ScannedMods = scannedMods.ToList(),
                    ModPrefixes = new List<ModPrefixData>()
                };
                
                // Add mod prefix data
                foreach (var entry in modToPrefixCache)
                {
                    var prefixData = new ModPrefixData
                    {
                        ModId = entry.Key,
                        Prefixes = entry.Value.ToList()
                    };
                    
                    data.ModPrefixes.Add(prefixData);
                }
                
                // Add structure data for each prefix
                data.PrefixStructures = new List<PrefixStructureData>();
                foreach (var entry in prefixToStructuresCache)
                {
                    if (entry.Value.Count > 0)
                    {
                        var structureData = new PrefixStructureData
                        {
                            Prefix = entry.Key,
                            Structures = entry.Value.ToList()
                        };
                        
                        data.PrefixStructures.Add(structureData);
                    }
                }
                
                // Make sure directory exists
                try
                {
                    string directory = Path.GetDirectoryName(cachePath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        Diagnostics.LogDiagnostic($"Created directory {directory}");
                    }
                }
                catch (Exception ex)
                {
                    Diagnostics.LogError($"Error creating directory for cache: {ex}");
                }
                
                // Serialize and save
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(CacheData));
                    using (StreamWriter writer = new StreamWriter(cachePath))
                    {
                        serializer.Serialize(writer, data);
                    }
                    Diagnostics.LogDiagnostic($"Successfully saved primary cache to {cachePath}");
                }
                catch (Exception ex)
                {
                    Diagnostics.LogError($"Error saving primary cache: {ex}");
                }
                
                // Also save the full cache data
                try
                {
                    using (StreamWriter writer = new StreamWriter(legacyCachePath))
                    {
                        XmlSerializer legacySerializer = new XmlSerializer(typeof(SymbolRegistryCacheData));
                        legacySerializer.Serialize(writer, cacheData);
                    }
                    Diagnostics.LogDiagnostic($"Successfully saved legacy cache to {legacyCachePath}");
                }
                catch (Exception ex)
                {
                    Diagnostics.LogError($"Error saving legacy cache: {ex}");
                }
                
                cacheModified = false;
                Log.Message($"[KCSG Unbound] Saved symbol cache with {data.PrefixStructures.Sum(p => p.Structures.Count)} structures");
            }
            catch (Exception ex)
            {
                Diagnostics.LogError($"Critical error in SaveCache: {ex}");
                Log.Error($"[KCSG Unbound] Critical error saving symbol cache: {ex}");
            }
        }
        
        /// <summary>
        /// Load the cache from disk with comprehensive error protection
        /// </summary>
        public static void LoadCache()
        {
            try
            {
                Log.Message("[KCSG Unbound] Loading symbol cache from disk...");
                
                // Check if cache file exists
                if (!File.Exists(cachePath))
                {
                    // Try to load legacy cache
                    if (File.Exists(legacyCachePath))
                    {
                        Log.Message("[KCSG Unbound] Legacy cache found, trying to migrate");
                        try
                        {
                            // Try to migrate the legacy cache data
                            XmlSerializer legacySerializer = new XmlSerializer(typeof(List<string>));
                            using (FileStream stream = new FileStream(legacyCachePath, FileMode.Open))
                            {
                                var legacyData = legacySerializer.Deserialize(stream) as List<string>;
                                
                                if (legacyData != null && legacyData.Count > 0)
                                {
                                    cacheData.RegisteredSymbols = legacyData;
                                    Log.Message($"[KCSG Unbound] Migrated {legacyData.Count} symbols from legacy cache");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[KCSG Unbound] Error migrating legacy cache: {ex.Message}");
                        }
                    }
                    
                    Log.Message("[KCSG Unbound] No cache file found, using safe defaults");
                    return;
                }
                
                // If the file is too large, skip it for safety
                FileInfo fileInfo = new FileInfo(cachePath);
                if (fileInfo.Length > 10 * 1024 * 1024) // 10 MB limit
                {
                    Log.Warning($"[KCSG Unbound] Cache file is too large ({fileInfo.Length / 1024 / 1024} MB), skipping for safety");
                    return;
                }
                
                // Use a staged loading approach with multiple fallback strategies
                
                // Stage 1: Try normal deserialization
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(SymbolRegistryCacheData));
                    using (FileStream stream = new FileStream(cachePath, FileMode.Open))
                    {
                        cacheData = (SymbolRegistryCacheData)serializer.Deserialize(stream);
                        Log.Message($"[KCSG Unbound] Successfully loaded cache: {cacheData.RegisteredSymbols.Count} symbols, {cacheData.ModSymbols.Count} mods");
                        return; // Success!
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Primary cache loading failed: {ex.Message}, trying fallback methods");
                }
                
                // Stage 2: Try loading with basic string parsing
                try
                {
                    // Read the file as text
                    string fileContent = File.ReadAllText(cachePath);
                    
                    // Extract symbol elements with simple string parsing
                    List<string> extractedSymbols = new List<string>();
                    int startIndex = 0;
                    
                    while ((startIndex = fileContent.IndexOf("<string>", startIndex)) != -1)
                    {
                        int endIndex = fileContent.IndexOf("</string>", startIndex);
                        if (endIndex == -1) break;
                        
                        int valueStart = startIndex + 8; // <string> is 8 chars
                        string symbolValue = fileContent.Substring(valueStart, endIndex - valueStart);
                        extractedSymbols.Add(symbolValue);
                        
                        startIndex = endIndex + 9; // </string> is 9 chars
                    }
                    
                    if (extractedSymbols.Count > 0)
                    {
                        cacheData.RegisteredSymbols = extractedSymbols;
                        Log.Message($"[KCSG Unbound] Successfully recovered {extractedSymbols.Count} symbols using fallback parsing");
                        return; // Partial success
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Fallback cache parsing failed: {ex.Message}");
                }
                
                // Stage 3: Failed all recovery attempts, create new cache
                Log.Warning("[KCSG Unbound] Could not load or recover cache, using empty cache");
                cacheData = new SymbolRegistryCacheData();
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Critical error loading cache: {ex}");
                cacheData = new SymbolRegistryCacheData();
            }
        }
        
        /// <summary>
        /// Add a hash mapping to the cache
        /// </summary>
        public static void AddHashMapping(ushort hash, string defName)
        {
            if (string.IsNullOrEmpty(defName))
                return;
                
            hashToSymbolCache[hash] = defName;
        }
        
        /// <summary>
        /// Try to get a def name from a hash using the cache
        /// </summary>
        public static bool TryGetDefNameFromHash(ushort hash, out string defName)
        {
            return hashToSymbolCache.TryGetValue(hash, out defName);
        }
        
        /// <summary>
        /// Clear the hash cache (used when registry is reset)
        /// </summary>
        public static void ClearHashCache()
        {
            hashToSymbolCache.Clear();
        }
    }
    
    /// <summary>
    /// Cache data saved to disk
    /// </summary>
    public class CacheData
    {
        public int Version { get; set; }
        public string Timestamp { get; set; }
        public List<string> ScannedMods { get; set; } = new List<string>();
        public List<ModPrefixData> ModPrefixes { get; set; } = new List<ModPrefixData>();
        public List<PrefixStructureData> PrefixStructures { get; set; } = new List<PrefixStructureData>();
    }
    
    /// <summary>
    /// Mod prefix data for the cache
    /// </summary>
    public class ModPrefixData
    {
        public string ModId { get; set; }
        public List<string> Prefixes { get; set; } = new List<string>();
    }
    
    /// <summary>
    /// Prefix structure data for the cache
    /// </summary>
    public class PrefixStructureData
    {
        public string Prefix { get; set; }
        public List<string> Structures { get; set; } = new List<string>();
    }
} 