using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Main mod class for KCSG Unbound.
    /// Applies Harmony patches early during mod loading, before def loading begins.
    /// </summary>
    public class KCSGUnboundMod : Mod
    {
        // Store our Harmony instance
        private static Harmony harmony;
        
        // Track initialization success
        private static bool initializationSuccess = false;
        
        public KCSGUnboundMod(ModContentPack content) : base(content)
        {
            try 
            {
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG Unbound] Early initialization              ║");
                Log.Message("════════════════════════════════════════════════════");
                
                // Set up Harmony patches early
                SetupHarmony();
                
                LongEventHandler.ExecuteWhenFinished(() => {
                    if (initializationSuccess)
                    {
                        Log.Message("[KCSG Unbound] Initialization completed successfully");
                    }
                    else
                    {
                        Log.Warning("[KCSG Unbound] Initialization did not complete successfully");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error during mod initialization: {ex}");
            }
        }
        
        /// <summary>
        /// Set up Harmony patches
        /// </summary>
        private void SetupHarmony()
        {
            try
            {
                Log.Message("[KCSG Unbound] Setting up Harmony patches");
                
                // Create our Harmony instance
                harmony = new Harmony("KCSG.Unbound");
                
                // First initialize our registry for maximum compatibility
                SymbolRegistry.Initialize();
                RimWorldCompatibility.ExploreAPIs();
                
                Log.Message("══════════════════════════════════════════════════");
                Log.Message("║          KCSG UNBOUND - INITIALIZING           ║");
                Log.Message("║        Symbol bypass mod for RimWorld 1.5      ║");
                Log.Message("══════════════════════════════════════════════════");
                
                // Initialize the symbol registry
                Log.Message("══════════════════════════════════════════════════");
                Log.Message("║ [KCSG] DIRECT INITIALIZATION                    ║");
                Log.Message("══════════════════════════════════════════════════");
                Log.Message($"[KCSG] Setting up compatibility for RimWorld {RimWorldCompatibility.VersionString}");
                
                // Use the new safe patch approach
                HarmonyPatches.ApplyPatches(harmony);
                
                // Mark initialization as successful
                initializationSuccess = true;
                
                Log.Message("════════════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying Harmony patches: {ex}");
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG Unbound] EARLY INITIALIZATION FAILED        ║");
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
    }
} 