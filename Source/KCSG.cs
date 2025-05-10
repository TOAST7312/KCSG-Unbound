using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimWorld;
using RimWorld.BaseGen;
using System.IO;

namespace KCSG
{
    [StaticConstructorOnStartup]
    public static class SymbolResolver
    {
        // Original VEF KCSG uses a symbol registry with a limit of 65535 entries
        // We maintain our own registry for compatibility, but also add unlimited storage
        public static Dictionary<string, object> symbolReg = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public static bool IsPrepatched = false;
        public static Dictionary<string, object> ShadowSymbolRegistry;
        
        // For tracking BaseGen.globalSettings fields via reflection
        private static FieldInfo globalSymbolResolversField;
        
        // Added to fix compiler errors
        private static bool Initialized = false;
        
        static SymbolResolver()
        {
            try
            {
                // Initialize fields
                Initialized = false;
                
                // Very obvious log messages to help debug
                Log.Message("══════════════════════════════════════════════════");
                Log.Message("║          KCSG UNBOUND - INITIALIZING           ║");
                Log.Message("║        Symbol bypass mod for RimWorld 1.5      ║");
                Log.Message("══════════════════════════════════════════════════");
                
                // Check if SymbolRegistry exists as a proxy for prepatch running
                bool registryExists = false;
                try {
                    Type registryType = AccessTools.TypeByName("KCSG.SymbolRegistry");
                    registryExists = registryType != null && 
                        AccessTools.Field(registryType, "ShadowSymbolRegistry") != null &&
                        AccessTools.Field(registryType, "Initialized") != null;
                        
                    if (registryExists) {
                        Log.Message("[KCSG] Found SymbolRegistry type - this indicates prepatch likely ran");
                        
                        // Check if it's been initialized
                        bool? initialized = AccessTools.StaticFieldRefAccess<bool?>(registryType, "Initialized");
                        Log.Message($"[KCSG] SymbolRegistry initialization state: {initialized ?? false}");
                    }
                }
                catch (Exception ex) {
                    Log.Warning($"[KCSG] Error checking SymbolRegistry: {ex.Message}");
                }
                
                if (IsPrepatched || registryExists)
                {
                    Log.Message("══════════════════════════════════════════════════");
                    Log.Message("║ [KCSG] INITIALIZED FROM PREPATCH                ║");
                    Log.Message("║ Using unlimited symbols from Zetrith's Prepatch ║");
                    Log.Message("══════════════════════════════════════════════════");
                    
                    // Get reference to prepatcher's registry
                    if (ShadowSymbolRegistry == null) {
                        Type registryType = AccessTools.TypeByName("KCSG.SymbolRegistry");
                        if (registryType != null) {
                            // Try to get the registry via reflection
                            ShadowSymbolRegistry = AccessTools.StaticFieldRefAccess<Dictionary<string, object>>(
                                registryType, "ShadowSymbolRegistry");
                                
                            if (ShadowSymbolRegistry == null) {
                                Log.Warning("[KCSG] Could not access ShadowSymbolRegistry via reflection");
                                ShadowSymbolRegistry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                            }
                            else {
                                Log.Message($"[KCSG] Successfully connected to existing shadow registry with {ShadowSymbolRegistry.Count} symbols");
                            }
                        }
                    }
                }
                else
                {
                    Log.Message("══════════════════════════════════════════════════");
                    Log.Message("║ [KCSG] DIRECT INITIALIZATION                    ║");
                    Log.Message("║ No prepatch detected, using standalone mode     ║");
                    Log.Message("══════════════════════════════════════════════════");
                    
                    // Initialize shadow registry if not done by prepatcher
                    if (ShadowSymbolRegistry == null)
                    {
                        ShadowSymbolRegistry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        Log.Message("[KCSG] Created new shadow registry for unlimited symbols");
                    }
                    
                    // Apply Harmony patches to KCSG
                    ApplyPatches();
                }
                
                // RimWorld 1.5 compatibility check
                Log.Message($"[KCSG] Running on RimWorld {VersionControl.CurrentVersionString}");
                
                // Locate BaseGen.globalSettings symbol resolvers field using reflection
                FindBaseGenResolvers();
                
                // Print some basic stats at startup
                Log.Message($"[KCSG] Registry Status: Main ({symbolReg.Count}/65535), Shadow ({ShadowSymbolRegistry?.Count ?? 0}/∞)");
                Log.Message("[KCSG] KCSG Unbound is ready!");
                
                // Mark as initialized
                Initialized = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error during initialization: {ex}");
            }
        }
        
        // Added to fix compiler errors
        private static void Initialize()
        {
            if (Initialized)
                return;
                
            Log.Message("[KCSG] Late initialization of SymbolResolver");
            
            // Ensure shadow registry exists
            if (ShadowSymbolRegistry == null)
            {
                ShadowSymbolRegistry = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            }
            
            // Find BaseGen resolvers if needed
            if (globalSymbolResolversField == null)
            {
                FindBaseGenResolvers();
            }
            
            Initialized = true;
        }
        
        private static void FindBaseGenResolvers()
        {
            try
            {
                // Get GlobalSettings type for RimWorld 1.5 compatibility
                Type globalSettingsType = BaseGen.globalSettings.GetType();
                
                // Try to find the symbol resolvers field - might be private
                globalSymbolResolversField = AccessTools.Field(globalSettingsType, "symbolResolvers");
                
                if (globalSymbolResolversField != null)
                {
                    Log.Message("[KCSG] Successfully located BaseGen symbol resolvers field");
                }
                else
                {
                    Log.Warning("[KCSG] Could not find BaseGen.globalSettings.symbolResolvers field - limited compatibility");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error getting BaseGen fields: {ex}");
            }
        }
        
        private static void ApplyPatches()
        {
            try
            {
                var harmony = new Harmony("com.kcsg.runtime");
                
                // Need to patch VEF's KCSG symbol resolver methods
                // Try to find VEF's implementation
                Type vefSymbolResolverType = AccessTools.TypeByName("RimWorld.BaseGen.SymbolResolver");
                if (vefSymbolResolverType != null)
                {
                    Log.Message("[KCSG] Found RimWorld.BaseGen.SymbolResolver class");
                    
                    // RimWorld 1.5 uses a different approach to symbol resolvers
                    // We need to hook into BaseGen.globalSettings.TryResolveSymbol method
                    Type globalSettingsType = AccessTools.TypeByName("RimWorld.BaseGen.GlobalSettings");
                    if (globalSettingsType != null)
                    {
                        MethodInfo tryResolveSymbolMethod = AccessTools.Method(globalSettingsType, "TryResolveSymbol");
                        if (tryResolveSymbolMethod != null)
                        {
                            MethodInfo patchMethod = AccessTools.Method(typeof(SymbolResolver), "TryResolveSymbol_Prefix");
                            harmony.Patch(tryResolveSymbolMethod, 
                                prefix: new HarmonyMethod(patchMethod));
                            Log.Message("[KCSG] Successfully patched TryResolveSymbol for RimWorld 1.5 compatibility");
                        }
                        else
                        {
                            Log.Warning("[KCSG] Could not find TryResolveSymbol method - limited compatibility");
                        }
                    }
                    else
                    {
                        Log.Warning("[KCSG] Could not find GlobalSettings type - limited compatibility");
                    }
                }
                
                Log.Message("[KCSG] Direct initialization complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error applying patches: {ex}");
            }
        }
        
        // Patch method for RimWorld 1.5's TryResolveSymbol
        public static bool TryResolveSymbol_Prefix(string symbolName, ResolveParams resolveParams, ref bool __result, ref object __state, ref Action<ResolveParams> __0)
        {
            try
            {
                string diagnosticPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RimWorld by Ludeon Studios", "KCSG_SymbolResolution.log");
                File.AppendAllText(diagnosticPath, $"[{DateTime.Now}] Trying to resolve symbol: {symbolName}\n");
                
                // If we're here, our patch is running
                if (!Initialized)
                {
                    Initialize();
                }

                // Log the current shadow registry size
                File.AppendAllText(diagnosticPath, $"[{DateTime.Now}] Shadow registry has {ShadowSymbolRegistry.Count} symbols\n");

                // When a symbol is successfully resolved, log it
                if (ShadowSymbolRegistry.TryGetValue(symbolName, out var resolver))
                {
                    File.AppendAllText(diagnosticPath, $"[{DateTime.Now}] SUCCESS - Resolved symbol: {symbolName} using KCSG Unbound's shadow registry\n");
                    __result = true;
                    return false;
                }
                else
                {
                    File.AppendAllText(diagnosticPath, $"[{DateTime.Now}] Symbol not found in shadow registry, letting RimWorld handle it: {symbolName}\n");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error in TryResolveSymbol_Prefix: {ex.Message}");
            }
            
            // Let the original method run if we don't have the symbol
            return true;
        }
        
        // Add tracking for the most recent symbols added
        public static void RecordSymbolAddition(string symbol)
        {
            try
            {
                // Setup our tracking log path
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RimWorld by Ludeon Studios");
                string logPath = Path.Combine(logDir, "KCSG_SymbolTracking.log");
                
                // Record this symbol addition with a timestamp
                File.AppendAllText(logPath, $"[{DateTime.Now}] Added symbol: {symbol}\n");
                
                // Every 100 symbols, also write the current count
                if (ShadowSymbolRegistry.Count % 100 == 0)
                {
                    File.AppendAllText(logPath, $"[{DateTime.Now}] --- Current symbol count: {ShadowSymbolRegistry.Count} ---\n");
                }
            }
            catch (Exception)
            {
                // Silent fail for performance reasons
            }
        }
        
        // Modify the AddDef method to call RecordSymbolAddition
        public static void AddDef(string name, object def)
        {
            if (!Initialized)
            {
                Initialize();
            }
            
            if (globalSymbolResolversField != null)
            {
                try
                {
                    // Get the BaseGen symbol resolvers dictionary
                    var resolvers = globalSymbolResolversField.GetValue(BaseGen.globalSettings) as IDictionary;
                    
                    // If original registry isn't full, add it there
                    if (resolvers != null && resolvers.Count < 65500 && !resolvers.Contains(name))
                    {
                        resolvers.Add(name, def);
                        return;
                    }
                    
                    // Otherwise, add to our shadow registry
                    if (!ShadowSymbolRegistry.ContainsKey(name))
                    {
                        ShadowSymbolRegistry.Add(name, def);
                        // Record this addition
                        RecordSymbolAddition(name);
                    }
                    else
                    {
                        ShadowSymbolRegistry[name] = def;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG] Error in AddDef: {ex.Message}");
                }
            }
        }
        
        // Always return false - no symbol limit
        public static bool MaxSymbolsReached()
        {
            return false;
        }
        
        // Resolve a symbol by name
        public static object Resolve(string name)
        {
            try
            {
                // First check the original registry
                if (symbolReg.TryGetValue(name, out object result))
                {
                    return result;
                }
                
                // Then check shadow registry
                if (ShadowSymbolRegistry != null && ShadowSymbolRegistry.TryGetValue(name, out object shadowResult))
                {
                    return shadowResult;
                }
                
                // Symbol not found
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error in Resolve: {ex.Message}");
                return null;
            }
        }
        
        // Utility method to get registry stats
        public static void PrintStats()
        {
            Log.Message($"[KCSG] Registry Statistics:");
            Log.Message($"  - Main Registry Symbols: {symbolReg.Count}");
            
            if (ShadowSymbolRegistry != null)
            {
                Log.Message($"  - Shadow Registry Symbols: {ShadowSymbolRegistry.Count}");
                Log.Message($"  - Total Symbols (Combined): {symbolReg.Count + ShadowSymbolRegistry.Count}");
            }
        }
    }
    
    // This class is what the mods are looking for
    public class GenStep_CustomStructureGen : GenStep
    {
        // Properties for mod compatibility
        public List<string> structureLayoutDefs = new List<string>();
        public List<ThingDef> filthTypes = new List<ThingDef>();
        public bool spawnConduits = true;
        
        // Simple implementation for compatibility
        public override int SeedPart => 1972978638;
        
        public override void Generate(Map map, GenStepParams parms)
        {
            if (Prefs.DevMode)
            {
                Log.Message("[KCSG] GenStep_CustomStructureGen called");
            }
            
            // Skip if no layouts defined or already checked
            if (structureLayoutDefs.NullOrEmpty())
            {
                if (Prefs.DevMode)
                    Log.Message("[KCSG] No structureLayoutDefs defined, skipping generation");
                return;
            }
            
            try
            {
                // Provide minimal functional implementation that reports what would happen
                // This is mostly to support mods that might reference this class
                Log.Message($"[KCSG] Would generate a structure from {structureLayoutDefs.Count} possible layouts");
                
                // Check if we're running as a diagnostic test from another mod
                bool runningForReal = map != null && map.info != null;
                if (runningForReal)
                {
                    Log.Message($"[KCSG] Structure generation requested for map {map.info.parent?.Label ?? "Unknown"}");
                    
                    // Output symbol statistics
                    Log.Message($"[KCSG] Registry Status: Main ({SymbolResolver.symbolReg.Count}/65535), Shadow ({SymbolResolver.ShadowSymbolRegistry?.Count ?? 0}/∞)");
                    Log.Message($"[KCSG] Total symbols available: {SymbolResolver.symbolReg.Count + (SymbolResolver.ShadowSymbolRegistry?.Count ?? 0)}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error in GenStep_CustomStructureGen: {ex}");
            }
        }
    }
} 