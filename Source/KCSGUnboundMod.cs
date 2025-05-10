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
        public KCSGUnboundMod(ModContentPack content) : base(content)
        {
            try 
            {
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG Unbound] Early initialization              ║");
                Log.Message("════════════════════════════════════════════════════");
                
                // Apply Harmony patches immediately, before def loading begins
                SetupHarmony();
                
                // Initialize symbol registry early
                if (!SymbolRegistry.Initialized)
                {
                    SymbolRegistry.Initialize();
                }
                
                Log.Message("[KCSG Unbound] Early initialization complete");
            }
            catch (Exception ex)
            {
                Log.Error("════════════════════════════════════════════════════");
                Log.Error("║ [KCSG Unbound] EARLY INITIALIZATION FAILED        ║");
                Log.Error("════════════════════════════════════════════════════");
                Log.Error($"[KCSG Unbound] {ex}");
            }
        }
        
        /// <summary>
        /// Setup Harmony patches early in the loading process
        /// </summary>
        private void SetupHarmony()
        {
            try
            {
                Log.Message("[KCSG Unbound] Setting up Harmony patches");
                
                // Create a new harmony instance
                Harmony harmony = new Harmony("com.kcsg.unbound");
                
                // Apply all patches from patch classes in this assembly
                harmony.PatchAll();
                
                Log.Message("[KCSG Unbound] Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying Harmony patches: {ex}");
            }
        }
    }
} 