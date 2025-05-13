using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Serializable data structure that holds registry information between game sessions
    /// </summary>
    [Serializable]
    public class SymbolRegistryCache
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
        
        // Path to the cache file
        private static string CacheFilePath => Path.Combine(GenFilePaths.ConfigFolderPath, "KCSG_Unbound_Registry.xml");
        
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
        /// Checks if a mod has CustomGenDefs folder
        /// </summary>
        public static bool HasCustomGenDefs(ModContentPack mod)
        {
            if (mod?.RootDir == null)
                return false;
                
            string customGenPath = Path.Combine(mod.RootDir, "Defs", "CustomGenDefs");
            return Directory.Exists(customGenPath);
        }
        
        /// <summary>
        /// Gets list of mods that need to be rescanned (new or changed version)
        /// </summary>
        public List<ModContentPack> GetModsToRescan()
        {
            var modsToRescan = new List<ModContentPack>();
            
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                if (!HasCustomGenDefs(mod))
                    continue;
                    
                // If mod is new or version changed
                bool needsRescan = false;
                
                if (ModSymbols.TryGetValue(mod.PackageId, out var symbolCache))
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
        public void RegisterCachedSymbols()
        {
            int totalRegistered = 0;
            int modsRegistered = 0;
            
            // Process each mod in the cache
            foreach (var kvp in ModSymbols)
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
                if (HasCustomGenDefs(mod))
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
        /// Saves the cache to disk
        /// </summary>
        public void Save()
        {
            try
            {
                // Ensure config directory exists
                Directory.CreateDirectory(GenFilePaths.ConfigFolderPath);
                
                // Serialize to XML
                XmlSerializer serializer = new XmlSerializer(typeof(SymbolRegistryCache));
                using (StreamWriter writer = new StreamWriter(CacheFilePath))
                {
                    serializer.Serialize(writer, this);
                }
                
                // Count total cached symbols across all mods
                int totalSymbols = ModSymbols.Values.Sum(mc => mc.Symbols.Count);
                Log.Message($"[KCSG Unbound] Saved registry cache with {totalSymbols} symbols from {ModSymbols.Count} mods");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Failed to save registry cache: {ex}");
            }
        }
        
        /// <summary>
        /// Loads the cache from disk
        /// </summary>
        public static SymbolRegistryCache Load()
        {
            if (!File.Exists(CacheFilePath))
                return null;
                
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SymbolRegistryCache));
                using (StreamReader reader = new StreamReader(CacheFilePath))
                {
                    var cache = serializer.Deserialize(reader) as SymbolRegistryCache;
                    
                    // Handle legacy cache format upgrading
                    if (cache.CacheVersion < 2 && cache.ModSymbols.Count == 0)
                    {
                        Log.Message("[KCSG Unbound] Upgrading cache format from v1 to v2");
                        
                        // Distribute registered symbols to all mods as a starting point
                        foreach (var modId in cache.ModVersions.Keys)
                        {
                            var mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.PackageId == modId);
                            if (mod != null)
                            {
                                cache.ModSymbols[modId] = new ModSymbolCache
                                {
                                    Version = cache.ModVersions[modId],
                                    Symbols = new List<string>(cache.RegisteredSymbols),
                                    LastUpdated = DateTime.Now,
                                    ModName = mod.Name
                                };
                            }
                        }
                        
                        // Update version
                        cache.CacheVersion = 2;
                    }
                    
                    // Count total cached symbols
                    int totalSymbols = cache.ModSymbols.Values.Sum(mc => mc.Symbols.Count);
                    Log.Message($"[KCSG Unbound] Loaded registry cache with {totalSymbols} symbols from {cache.ModSymbols.Count} mods");
                    
                    return cache;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Failed to load registry cache: {ex}");
                return null;
            }
        }
    }
} 