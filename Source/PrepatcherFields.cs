using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using System.IO;
using System.Reflection;
using Prepatcher;

namespace KCSG
{
    // A simple class to hold our prepatcher data
    public class KCSGPrepatchData
    {
        // This is a singleton instance we'll use to access our extended field
        public static readonly KCSGPrepatchData Instance = new KCSGPrepatchData();
        
        private KCSGPrepatchData() { } // Private constructor for singleton
    }
    
    /// <summary>
    /// Class that contains fields for Zetrith's Prepatcher to modify
    /// This is the recommended approach rather than using PatchAll
    /// </summary>
    public static class PrepatcherFields
    {
        // Define the prepatcher field as an extension method on our data class
        [PrepatcherField]
        public static extern ref bool IsPrepatched(this KCSGPrepatchData data);
        
        // Static constructor runs when class is first accessed
        static PrepatcherFields()
        {
            try
            {
                // Get path to same folder as Player.log
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RimWorld by Ludeon Studios");
                if (!Directory.Exists(logDir))
                {
                    // Fallback to current directory if the expected log dir doesn't exist
                    logDir = ".";
                }

                // Create a very distinctive log file that will be easy to find
                string logPath = Path.Combine(logDir, "KCSG_PREPATCHER_RUN_VERIFICATION.log");
                try
                {
                    // Write with more distinctive content to easily spot in file explorer
                    System.IO.File.WriteAllText(logPath, $"==================================================================\n");
                    System.IO.File.AppendAllText(logPath, $"  KCSG UNBOUND PREPATCHER EXECUTED AT {DateTime.Now}\n");
                    System.IO.File.AppendAllText(logPath, $"==================================================================\n\n");
                    System.IO.File.AppendAllText(logPath, $"This file confirms that the KCSG Unbound prepatcher has run using the PrepatcherField approach.\n");
                    System.IO.File.AppendAllText(logPath, $"RimWorld version: {VersionControl.CurrentVersionString}\n");
                    System.IO.File.AppendAllText(logPath, $"Assembly path: {typeof(PrepatcherFields).Assembly.Location}\n");
                    System.IO.File.AppendAllText(logPath, $"IsPrepatched: {KCSGPrepatchData.Instance.IsPrepatched()}\n");
                    
                    // Include system info for better diagnostics
                    System.IO.File.AppendAllText(logPath, $"\nSystem Info:\n");
                    System.IO.File.AppendAllText(logPath, $"OS: {Environment.OSVersion}\n");
                    System.IO.File.AppendAllText(logPath, $".NET Version: {Environment.Version}\n");
                    System.IO.File.AppendAllText(logPath, $"Process: {System.Diagnostics.Process.GetCurrentProcess().ProcessName}\n");
                    System.IO.File.AppendAllText(logPath, $"Current Directory: {Environment.CurrentDirectory}\n");
                }
                catch (Exception ex)
                {
                    // Fallback to base directory if the log folder isn't writable
                    logPath = "KCSG_PREPATCHER_RUN_VERIFICATION.log";
                    try
                    {
                        System.IO.File.WriteAllText(logPath, $"KCSG Unbound prepatcher executed at {DateTime.Now}\n");
                        System.IO.File.AppendAllText(logPath, $"RimWorld version: {VersionControl.CurrentVersionString}\n");
                        System.IO.File.AppendAllText(logPath, $"Assembly path: {typeof(PrepatcherFields).Assembly.Location}\n");
                        System.IO.File.AppendAllText(logPath, $"IsPrepatched: {KCSGPrepatchData.Instance.IsPrepatched()}\n");
                        System.IO.File.AppendAllText(logPath, $"Note: Failed to write to {logDir}: {ex.Message}\n");
                    }
                    catch {}
                }
                
                // Log to console
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG] PREPATCH - Initializing KCSG Unbound      ║");
                Log.Message("║ Using PrepatcherField approach with Zetrith      ║");
                Log.Message("════════════════════════════════════════════════════");
                
                // Register our runtime for unlimited symbols
                try {
                    Log.Message($"[KCSG] PREPATCH - Checking SymbolRegistry.Initialized: {SymbolRegistry.Initialized}");
                    
                    if (!SymbolRegistry.Initialized) {
                        Log.Message("[KCSG] PREPATCH - Initializing symbol registry for the first time");
                        SymbolRegistry.Initialize();
                        Log.Message($"[KCSG] PREPATCH - SymbolRegistry initialized, status: {SymbolRegistry.Initialized}");
                    } else {
                        Log.Message("[KCSG] PREPATCH - Symbol registry was already initialized");
                    }
                    
                    Log.Message("[KCSG] PREPATCH - Symbol registry initialization check completed successfully");
                } catch (Exception ex) {
                    Log.Error($"[KCSG] PREPATCH - Error checking SymbolRegistry: {ex}");
                    // Force initialize as a fallback
                    try {
                        SymbolRegistry.Initialize();
                        Log.Message("[KCSG] PREPATCH - Forced SymbolRegistry initialization after error");
                    } catch (Exception ex2) {
                        Log.Error($"[KCSG] PREPATCH - Could not force initialize SymbolRegistry: {ex2}");
                    }
                }
                
                // Apply early harmony patches if needed
                ApplyEarlyPatches();
                
                // Clear banner to indicate completion
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG] PREPATCH - Initialization complete        ║");
                Log.Message("║ Unlimited structure symbols are now available    ║");
                Log.Message("════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Log.Error("════════════════════════════════════════════════════");
                Log.Error("║ [KCSG] PREPATCH - ERROR DURING INITIALIZATION     ║");
                Log.Error("════════════════════════════════════════════════════");
                Log.Error("[KCSG] PREPATCH - Exception details: " + ex);
            }
        }
        
        // Apply any early patches needed before the game fully loads
        private static void ApplyEarlyPatches()
        {
            // Create a new harmony instance for our prepatcher
            Harmony harmony = new Harmony("com.kcsg.prepatcher");
            Log.Message("[KCSG] PREPATCH - Created Harmony instance: com.kcsg.prepatcher");
            
            try
            {
                // Check if we're running RimWorld 1.5
                bool isRimworld15 = VersionControl.CurrentVersionString.StartsWith("1.5");
                
                // Look for VEF's KCSG implementation
                Type originalKCSG = AccessTools.TypeByName("KCSG.SymbolResolver");
                
                if (originalKCSG != null)
                {
                    Log.Message("[KCSG] PREPATCH - ✓ Found VEF KCSG implementation");
                    
                    // We need to patch these methods:
                    // 1. AddDef - To redirect symbol registration to our system
                    // 2. MaxSymbolsReached - To always return false
                    // 3. Resolve - To check both registries
                    
                    int patchedMethods = 0;
                    
                    // Patch the AddDef method to prevent unlimited symbol errors
                    MethodInfo originalAddDef = AccessTools.Method(originalKCSG, "AddDef");
                    if (originalAddDef != null)
                    {
                        Log.Message("[KCSG] PREPATCH - Found AddDef method, preparing patch");
                        
                        // Create our prefix to intercept AddDef calls
                        MethodInfo prefixMethod = typeof(KCSGPrepatch).GetMethod("Prefix_AddDef", 
                            BindingFlags.Static | BindingFlags.NonPublic);
                            
                        if (prefixMethod != null)
                        {
                            harmony.Patch(originalAddDef, prefix: new HarmonyMethod(prefixMethod));
                            Log.Message("[KCSG] PREPATCH - ✓ Successfully patched KCSG.SymbolResolver.AddDef");
                            patchedMethods++;
                        }
                    }
                    
                    // Patch the MaxSymbolsReached method to always return false
                    MethodInfo originalMaxSymbols = AccessTools.Method(originalKCSG, "MaxSymbolsReached");
                    if (originalMaxSymbols != null)
                    {
                        Log.Message("[KCSG] PREPATCH - Found MaxSymbolsReached method, preparing patch");
                        
                        MethodInfo prefixMaxSymbols = typeof(KCSGPrepatch).GetMethod("Prefix_MaxSymbolsReached", 
                            BindingFlags.Static | BindingFlags.NonPublic);
                            
                        if (prefixMaxSymbols != null)
                        {
                            harmony.Patch(originalMaxSymbols, prefix: new HarmonyMethod(prefixMaxSymbols));
                            Log.Message("[KCSG] PREPATCH - ✓ Successfully patched KCSG.SymbolResolver.MaxSymbolsReached");
                            patchedMethods++;
                        }
                    }
                    
                    // Patch the Resolve method to check both registries
                    MethodInfo originalResolve = AccessTools.Method(originalKCSG, "Resolve");
                    if (originalResolve != null)
                    {
                        Log.Message("[KCSG] PREPATCH - Found Resolve method, preparing patch");
                        
                        MethodInfo prefixResolve = typeof(KCSGPrepatch).GetMethod("Prefix_Resolve", 
                            BindingFlags.Static | BindingFlags.NonPublic);
                            
                        if (prefixResolve != null)
                        {
                            harmony.Patch(originalResolve, prefix: new HarmonyMethod(prefixResolve));
                            Log.Message("[KCSG] PREPATCH - ✓ Successfully patched KCSG.SymbolResolver.Resolve");
                            patchedMethods++;
                        }
                    }
                    
                    Log.Message($"[KCSG] PREPATCH - Patched {patchedMethods} methods successfully");
                }
                else
                {
                    Log.Message("[KCSG] PREPATCH - Original KCSG SymbolResolver not found, using standalone implementation");
                }
                
                // For RimWorld 1.5, we need additional patches to handle the new symbol resolver architecture
                if (isRimworld15)
                {
                    Log.Message("[KCSG] PREPATCH - Detected RimWorld 1.5, applying additional patches");
                    
                    Type globalSettingsType = AccessTools.TypeByName("RimWorld.BaseGen.GlobalSettings");
                    if (globalSettingsType != null)
                    {
                        // Try to patch TryResolveSymbol method
                        MethodInfo tryResolveMethod = AccessTools.Method(globalSettingsType, "TryResolveSymbol");
                        if (tryResolveMethod != null)
                        {
                            MethodInfo prefixTryResolve = typeof(KCSGPrepatch).GetMethod("Prefix_TryResolveSymbol", 
                                BindingFlags.Static | BindingFlags.NonPublic);
                                
                            if (prefixTryResolve != null)
                            {
                                harmony.Patch(tryResolveMethod, prefix: new HarmonyMethod(prefixTryResolve));
                                Log.Message("[KCSG] PREPATCH - ✓ Successfully patched GlobalSettings.TryResolveSymbol for RimWorld 1.5");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] PREPATCH - Error applying early patches: {ex}");
            }
        }
    }
} 