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
                
                // Apply Harmony patches immediately, before def loading begins
                SetupHarmony();
                
                // Initialize symbol registry early
                if (!SymbolRegistry.Initialized)
                {
                    SymbolRegistry.Initialize();
                }
                
                // Successfully initialized
                initializationSuccess = true;
                
                Log.Message("[KCSG Unbound] Early initialization complete");
            }
            catch (Exception ex)
            {
                initializationSuccess = false;
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
                
                Log.Message("══════════════════════════════════════════════════");
                Log.Message("║          KCSG UNBOUND - INITIALIZING           ║");
                Log.Message("║        Symbol bypass mod for RimWorld 1.5      ║");
                Log.Message("══════════════════════════════════════════════════");
                
                // First, check for conflicting Harmony instances
                foreach (var inst in Harmony.GetAllPatchedMethods())
                {
                    if (inst.Name.Contains("SymbolDef") || inst.Name.Contains("SymbolResolver"))
                    {
                        Log.Warning($"[KCSG Unbound] Found potentially conflicting patch target: {inst.DeclaringType?.FullName}.{inst.Name}");
                    }
                }
                
                Log.Message("══════════════════════════════════════════════════");
                Log.Message("║ [KCSG] DIRECT INITIALIZATION                    ║");
                Log.Message("══════════════════════════════════════════════════");
                
                // Initialize and explore RimWorld API compatibility 
                RimWorldCompatibility.Initialize();
                
                Log.Message("[KCSG] Created new shadow registry for unlimited symbols");
                
                // Create a new harmony instance if not already created
                harmony = new Harmony("com.kcsg.unbound");
                
                // Apply all patches from patch classes in this assembly
                harmony.PatchAll();
                
                // Update registry status
                int nativeCount = 0;
                Dictionary<string, Type> nativeResolvers = RimWorldCompatibility.GetSymbolResolvers();
                if (nativeResolvers != null)
                {
                    nativeCount = nativeResolvers.Count;
                }
                
                Log.Message($"[KCSG] Registry Status: Main ({nativeCount}/65535), Shadow (0/∞)");
                
                Log.Message("[KCSG] KCSG Unbound is ready!");
                
                Log.Message("[KCSG Unbound] Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying Harmony patches: {ex}");
                throw; // Re-throw to ensure the mod initialization fails properly
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