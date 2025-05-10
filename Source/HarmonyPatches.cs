using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Contains Harmony patches for KCSG Unbound
    /// These are used when the prepatcher is not available
    /// </summary>
    public static class HarmonyPatches
    {
        // Patch for KCSG.SymbolResolver.AddDef method
        [HarmonyPatch]
        public static class Patch_SymbolResolver_AddDef
        {
            // Use Prepare to dynamically locate the method to patch
            public static bool Prepare()
            {
                return AccessTools.Method("KCSG.SymbolResolver:AddDef") != null;
            }
            
            // Use TargetMethod to specify the method to patch
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method("KCSG.SymbolResolver:AddDef");
            }
            
            // Prefix method that mirrors the prepatcher functionality
            public static bool Prefix(string symbolDef, Type resolver)
            {
                // Register with our system
                SymbolRegistry.Register(symbolDef, resolver);
                
                // Still let the original method run - this is important for backward compatibility
                return true;
            }
        }
        
        // Patch for KCSG.SymbolResolver.MaxSymbolsReached method
        [HarmonyPatch]
        public static class Patch_SymbolResolver_MaxSymbolsReached
        {
            // Use Prepare to dynamically locate the method to patch
            public static bool Prepare()
            {
                return AccessTools.Method("KCSG.SymbolResolver:MaxSymbolsReached") != null;
            }
            
            // Use TargetMethod to specify the method to patch
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method("KCSG.SymbolResolver:MaxSymbolsReached");
            }
            
            // Prefix method that mirrors the prepatcher functionality
            public static bool Prefix(ref bool __result)
            {
                // Always return false - we handle unlimited symbols
                __result = false;
                return false; // Skip the original method
            }
        }
        
        // Patch for KCSG.SymbolResolver.Resolve method
        [HarmonyPatch]
        public static class Patch_SymbolResolver_Resolve
        {
            // Use Prepare to dynamically locate the method to patch
            public static bool Prepare()
            {
                return AccessTools.Method("KCSG.SymbolResolver:Resolve") != null;
            }
            
            // Use TargetMethod to specify the method to patch
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method("KCSG.SymbolResolver:Resolve");
            }
            
            // Prefix method that mirrors the prepatcher functionality
            public static bool Prefix(string symbol, ResolveParams rp, ref bool __result)
            {
                // Try to resolve using our unlimited registry first
                if (SymbolRegistry.TryResolve(symbol, rp))
                {
                    __result = true;
                    return false; // Skip the original method
                }
                
                // Let the original method handle it
                return true;
            }
        }
        
        // Patch for RimWorld 1.5's GlobalSettings.TryResolveSymbol method if present
        [HarmonyPatch]
        public static class Patch_GlobalSettings_TryResolveSymbol
        {
            // Use Prepare to dynamically locate the method to patch
            public static bool Prepare()
            {
                return AccessTools.Method("RimWorld.BaseGen.GlobalSettings:TryResolveSymbol") != null;
            }
            
            // Use TargetMethod to specify the method to patch
            public static MethodBase TargetMethod()
            {
                return AccessTools.Method("RimWorld.BaseGen.GlobalSettings:TryResolveSymbol");
            }
            
            // Prefix method that mirrors the prepatcher functionality
            public static bool Prefix(string symbol, ResolveParams rp, ref bool __result)
            {
                // Try to resolve using our unlimited registry first
                if (SymbolRegistry.TryResolve(symbol, rp))
                {
                    __result = true;
                    return false; // Skip the original method
                }
                
                // Let the original method handle it
                return true;
            }
        }
    }
} 