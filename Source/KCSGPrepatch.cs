using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using HarmonyLib;
using Verse;
using RimWorld;
using RimWorld.BaseGen;
using KCSG;

namespace KCSG
{
    // This class will be found by Prepatcher through AssemblyInfo's InternalsVisibleTo attribute
    public static class KCSGPrepatch
    {
        // Static constructor - runs as soon as class is loaded
        static KCSGPrepatch()
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

                string logPath = Path.Combine(logDir, "KCSG_ClassLoaded.txt");
                
                try
                {
                    System.IO.File.WriteAllText(logPath, $"KCSG Unbound class loaded at {DateTime.Now}\n");
                    System.IO.File.AppendAllText(logPath, $"Assembly: {typeof(KCSGPrepatch).Assembly.FullName}\n");
                    System.IO.File.AppendAllText(logPath, $"Location: {typeof(KCSGPrepatch).Assembly.Location}\n");
                }
                catch (Exception ex)
                {
                    // If log folder isn't writable, fallback to base directory
                    logPath = "KCSG_ClassLoaded.txt";
                    try
                    {
                        System.IO.File.WriteAllText(logPath, $"KCSG Unbound class loaded at {DateTime.Now}\n");
                        System.IO.File.AppendAllText(logPath, $"Assembly: {typeof(KCSGPrepatch).Assembly.FullName}\n");
                        System.IO.File.AppendAllText(logPath, $"Location: {typeof(KCSGPrepatch).Assembly.Location}\n");
                        System.IO.File.AppendAllText(logPath, $"Note: Failed to write to {logDir}: {ex.Message}\n");
                    }
                    catch
                    {
                        // If can't write at all, just ignore
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    System.IO.File.WriteAllText("KCSG_Error.txt", $"Error in static constructor: {ex}");
                }
                catch
                {
                    // If can't write at all, just ignore
                }
            }
        }

        private static bool hasPatchedSuccessfully = false;
        
        // Public method to check if patch was successful - allows other parts to verify
        public static bool HasPatchedSuccessfully() => hasPatchedSuccessfully;
        
        // PatchAll is called by Prepatcher during very early loading
        // This specific signature is required by Zetrith's Prepatcher
        // The method must be public static bool PatchAll() or internal static bool PatchAll()
        public static bool PatchAll()
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
                    System.IO.File.AppendAllText(logPath, $"This file confirms that the KCSG Unbound prepatcher has run.\n");
                    System.IO.File.AppendAllText(logPath, $"RimWorld version: {VersionControl.CurrentVersionString}\n");
                    System.IO.File.AppendAllText(logPath, $"Assembly path: {typeof(KCSGPrepatch).Assembly.Location}\n");
                    
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
                        System.IO.File.AppendAllText(logPath, $"Assembly path: {typeof(KCSGPrepatch).Assembly.Location}\n");
                        System.IO.File.AppendAllText(logPath, $"Note: Failed to write to {logDir}: {ex.Message}\n");
                    }
                    catch {}
                }
                
                // More visible banner to clearly indicate when prepatcher starts
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG] PREPATCH - Initializing KCSG Unbound      ║");
                Log.Message("║ Enabling unlimited structure symbols via Zetrith ║");
                Log.Message("════════════════════════════════════════════════════");
                
                // Force write to log immediately to help with debugging
                try {
                    Type logType = AccessTools.TypeByName("Verse.Log");
                    if (logType != null) {
                        MethodInfo flushMethod = AccessTools.Method(logType, "Flush");
                        if (flushMethod != null) {
                            flushMethod.Invoke(null, null);
                        }
                    }
                } catch {}
                
                // Detect RimWorld version
                string versionString = VersionControl.CurrentVersionString;
                Log.Message($"[KCSG] PREPATCH - RimWorld version detected: {versionString}");
                
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
                    
                    Log.Message("[KCSG] PREPATCH - Symbol registry initialization completed successfully");
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
                
                // Mark our runtime as prepatched
                Type symbolResolverType = AccessTools.TypeByName("KCSG.SymbolResolver");
                if (symbolResolverType != null)
                {
                    FieldInfo isPrepatched = AccessTools.Field(symbolResolverType, "IsPrepatched");
                    if (isPrepatched != null)
                    {
                        isPrepatched.SetValue(null, true);
                        Log.Message("[KCSG] PREPATCH - Successfully marked runtime as prepatched");
                    }
                    else
                    {
                        Log.Warning("[KCSG] PREPATCH - Could not find IsPrepatched field");
                    }
                }
                else
                {
                    Log.Warning("[KCSG] PREPATCH - Could not find SymbolResolver type");
                }
                
                // Verify it was set properly
                if (symbolResolverType != null)
                {
                    FieldInfo isPrepatched = AccessTools.Field(symbolResolverType, "IsPrepatched");
                    if (isPrepatched != null)
                    {
                        bool value = (bool)isPrepatched.GetValue(null);
                        Log.Message($"[KCSG] PREPATCH - Verification: IsPrepatched = {value}");
                    }
                }
                
                // Write success marker
                hasPatchedSuccessfully = true;
                
                // Force write to log
                try {
                    Type logType = AccessTools.TypeByName("Verse.Log");
                    if (logType != null) {
                        MethodInfo flushMethod = AccessTools.Method(logType, "Flush");
                        if (flushMethod != null) {
                            flushMethod.Invoke(null, null);
                        }
                    }
                } catch {}
                
                // Clear banner to indicate completion
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG] PREPATCH - Initialization complete        ║");
                Log.Message("║ Unlimited structure symbols are now available    ║");
                Log.Message("════════════════════════════════════════════════════");
                
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("════════════════════════════════════════════════════");
                Log.Error("║ [KCSG] PREPATCH - ERROR DURING INITIALIZATION     ║");
                Log.Error("════════════════════════════════════════════════════");
                Log.Error("[KCSG] PREPATCH - Exception details: " + ex);
                return false;
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
        
        // Prefix for the original KCSG.SymbolResolver.AddDef method
        private static bool Prefix_AddDef(string symbolDef, Type resolver)
        {
            try
            {
                if (string.IsNullOrEmpty(symbolDef) || resolver == null)
                {
                    // Let the original method handle null validation
                    return true;
                }

                // Register with our system - use the fully qualified name to avoid ambiguity
                global::KCSG.SymbolRegistry.Register(symbolDef, resolver);
                
                // Still let the original method run - we're not replacing it, just extending
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error in Prefix_AddDef: {ex}");
                // Let the original method run
                return true;
            }
        }
        
        // Prefix for the original KCSG.SymbolResolver.MaxSymbolsReached method
        // Always returns false to prevent the symbol limit from being enforced
        private static bool Prefix_MaxSymbolsReached(ref bool __result)
        {
            // Always return false - symbols are not maxed out
            __result = false;
            
            // Skip the original method
            return false;
        }
        
        // Prefix for the original KCSG.SymbolResolver.Resolve method
        // Checks our registry first, then falls back to original method if needed
        private static bool Prefix_Resolve(string symbol, ResolveParams rp, ref bool __result)
        {
            try
            {
                // Try to resolve using our unlimited registry first - use fully qualified name
                if (global::KCSG.SymbolRegistry.TryResolve(symbol, rp))
                {
                    __result = true;
                    // Skip the original method
                    return false;
                }
                
                // If our registry couldn't resolve it, let the original method try
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error in Prefix_Resolve: {ex}");
                // Let the original method run
                return true;
            }
        }

        // For RimWorld 1.5 compatibility
        // Prefix for GlobalSettings.TryResolveSymbol method
        private static bool Prefix_TryResolveSymbol(string symbol, ResolveParams rp, ref bool __result)
        {
            try
            {
                // Try to resolve using our unlimited registry first - use fully qualified name
                if (global::KCSG.SymbolRegistry.TryResolve(symbol, rp))
                {
                    __result = true;
                    // Skip the original method
                    return false;
                }
                
                // If our registry couldn't resolve it, let the original method try
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error in Prefix_TryResolveSymbol: {ex}");
                // Let the original method run
                return true;
            }
        }
    }
} 