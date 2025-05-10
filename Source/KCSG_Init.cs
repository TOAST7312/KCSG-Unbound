using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Initialization for KCSG Unbound
    /// </summary>
    [StaticConstructorOnStartup]
    public static class KCSG_Init
    {
        // Static constructor runs on game start
        static KCSG_Init()
        {
            Log.Message("════════════════════════════════════════════════════");
            Log.Message("║ [KCSG Unbound] Initializing                      ║");
            Log.Message("║ Enhanced Structure Generation System Loading     ║");
            Log.Message("════════════════════════════════════════════════════");

            // Setup Harmony patches unless we've already been prepatched
            if (!KCSGPrepatchData.Instance.IsPrepatched())
            {
                SetupHarmony();
            }
            else
            {
                Log.Message("[KCSG Unbound] Using prepatcher, skipping runtime patching");
            }

            // Initialize symbol registry if not done already
            if (!SymbolRegistry.Initialized)
            {
                SymbolRegistry.Initialize();
            }
            
            // Register our symbol resolvers
            RegisterSymbolResolvers();

            Log.Message("════════════════════════════════════════════════════");
            Log.Message("║ [KCSG Unbound] Initialization complete           ║");
            Log.Message($"║ Registered {SymbolRegistry.RegisteredSymbolCount} symbol resolvers     ║");
            Log.Message("════════════════════════════════════════════════════");
        }

        // Register all symbol resolvers 
        private static void RegisterSymbolResolvers()
        {
            // Register standard symbol resolvers
            RegisterResolver("Building", typeof(SymbolResolver_Building));
            RegisterResolver("RandomBuilding", typeof(SymbolResolver_RandomBuilding));
            
            // More resolvers will be added here
        }
        
        // Helper method to register a resolver
        private static void RegisterResolver(string symbol, Type resolverType)
        {
            try
            {
                SymbolRegistry.Register(symbol, resolverType);
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Failed to register symbol '{symbol}': {ex}");
            }
        }

        // Setup Harmony patches for runtime operation (when prepatcher isn't used)
        private static void SetupHarmony()
        {
            try
            {
                Log.Message("[KCSG Unbound] Setting up runtime Harmony patches");
                
                // Create a new harmony instance
                Harmony harmony = new Harmony("com.kcsg.unbound");
                
                // Apply all patches from patch classes in this assembly
                harmony.PatchAll();
                
                Log.Message("[KCSG Unbound] Runtime Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying runtime Harmony patches: {ex}");
            }
        }
    }
} 