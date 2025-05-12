using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;
using System.Linq;

namespace KCSG
{
    /// <summary>
    /// Main mod class for KCSG Unbound.
    /// Sets up basic mod structure but defers risky operations to SafeStart.
    /// </summary>
    public class KCSGUnboundMod : Mod
    {
        // Store our Harmony instance
        private static Harmony harmony;
        
        // Track initialization success
        private static bool initializationSuccess = false;
        
        // For performance monitoring
        private static System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        
        // For status tracking
        public static string CurrentStatus = "Not initialized";
        
        public KCSGUnboundMod(ModContentPack content) : base(content)
        {
            stopwatch.Start();
            
            try 
            {
                // Start diagnostics first
                Diagnostics.Initialize();
                Diagnostics.LogDiagnostic("KCSGUnboundMod constructor called");
                
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG Unbound] Early initialization              ║");
                Log.Message("════════════════════════════════════════════════════");
                
                CurrentStatus = "Initializing...";
                
                // Set thread priority higher for loading
                try
                {
                    System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.AboveNormal;
                }
                catch {}
                
                // Initialize Harmony and registry immediately
                SetupEarlyRegistry();
                
                // Apply patches in precise order
                SetupHarmony();
                
                // Preload common KCSG structure layouts
                PreloadStructureLayouts();
                
                // We'll only mark as successful if we get this far
                initializationSuccess = true;
                
                stopwatch.Stop();
                CurrentStatus = $"Loaded ({stopwatch.ElapsedMilliseconds}ms)";
                
                // No more relying on LongEventHandler - we want to be EARLY
                Log.Message($"[KCSG Unbound] Early initialization complete in {stopwatch.ElapsedMilliseconds}ms");
                Diagnostics.LogDiagnostic("Early initialization complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error during mod initialization: {ex}");
                Diagnostics.LogDiagnostic($"Error during initialization: {ex}");
                CurrentStatus = "Error during initialization";
            }
        }
        
        /// <summary>
        /// Perform early registry setup to intercept symbol loading
        /// </summary>
        private void SetupEarlyRegistry()
        {
            try
            {
                // Create our Harmony instance
                harmony = new Harmony("KCSG.Unbound");
                
                // Initialize registry immediately
                if (!SymbolRegistry.Initialized)
                {
                    SymbolRegistry.Initialize();
                    Log.Message("[KCSG] Registry initialization complete in constructor");
                }
                
                Log.Message("══════════════════════════════════════════════════");
                Log.Message("║          KCSG UNBOUND - EARLY SETUP           ║");
                Log.Message("║        Symbol bypass mod for RimWorld 1.5      ║");
                Log.Message("══════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error in early registry setup: {ex}");
            }
        }
        
        /// <summary>
        /// Preload structure layouts to avoid issues during def resolution
        /// </summary>
        private void PreloadStructureLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Pre-registering common layout names...");
                
                // Common prefixes that appear in cross-references
                string[] commonPrefixes = new[] { 
                    "VFED_", "VFEA_", "VFEC_", "VFEE_", "VFEM_", "VFET_", "VFE_", "VFEI_", "FTC_", 
                    "RBME_", "AG_", "BM_", "BS_", "MM_", "VC_", "VE_", "VM_", "VBGE_"
                };
                
                // Get common base names from the log file cross-references
                string[] commonBaseNames = new[] { 
                    "CitadelBunkerStart", "LargeBallroomA", "SurveillanceStationF", "ServantQuartersA",
                    "GrandNobleThroneRoomA", "LargeNobleBedroomA", "TechPrinterMainA", "UnderfarmMainA",
                    "ShuttleLandingPadA", "AerodroneStationA", "ImperialConvoyA", "StockpileDepotA"
                };
                
                // Create permutations of base names and prefixes
                foreach (var prefix in commonPrefixes) 
                {
                    foreach (var baseName in commonBaseNames)
                    {
                        string defName = prefix + baseName;
                        
                        if (!SymbolRegistry.IsDefRegistered(defName))
                        {
                            try 
                            {
                                var placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                SymbolRegistry.RegisterDef(defName, placeholderDef);
                            }
                            catch {}
                            
                            // Also register variants with suffixes
                            foreach (var suffix in new[] { "_A", "_B", "_C", "_D", "Alpha", "Beta", "Gamma", "Delta" })
                            {
                                string variantName = prefix + baseName + suffix;
                                try 
                                {
                                    if (!SymbolRegistry.IsDefRegistered(variantName))
                                    {
                                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(variantName);
                                        SymbolRegistry.RegisterDef(variantName, placeholderDef);
                                    }
                                }
                                catch {}
                            }
                        }
                    }
                }
                
                Log.Message($"[KCSG Unbound] Pre-registered common layout names, total defs: {SymbolRegistry.RegisteredDefCount}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Error preloading structure layouts: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set up Harmony patches immediately in the constructor
        /// </summary>
        private void SetupHarmony()
        {
            try
            {
                Log.Message("[KCSG Unbound] Setting up Harmony patches in constructor");
                
                // Apply patches directly
                HarmonyPatches.ApplyPatches(harmony);
                
                Log.Message("[KCSG Unbound] Harmony patches applied successfully in constructor");
                Log.Message("════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying Harmony patches: {ex}");
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG Unbound] HARMONY INITIALIZATION FAILED    ║");
                Log.Message("════════════════════════════════════════════════════");
            }
        }
        
        /// <summary>
        /// Check if the mod initialized successfully
        /// </summary>
        public static bool IsInitialized => initializationSuccess;
        
        /// <summary>
        /// Get the shared Harmony instance
        /// </summary>
        public static Harmony HarmonyInstance => harmony;
        
        /// <summary>
        /// Override SettingsCategory to avoid creating a settings button
        /// </summary>
        public override string SettingsCategory() => null;
    }
} 