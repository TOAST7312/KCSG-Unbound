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
        
        // Patch for DefDatabase<SymbolDef>.Add method
        // This patch intercepts symbol def registrations to handle the 65,535 limit
        [HarmonyPatch]
        public static class Patch_DefDatabase_Add_SymbolDef
        {
            // Use Prepare to dynamically locate the method to patch
            public static bool Prepare()
            {
                // First try to find the SymbolDef type
                Type symbolDefType = AccessTools.TypeByName("KCSG.SymbolDef");
                if (symbolDefType == null)
                {
                    Log.Warning("[KCSG Unbound] Could not find SymbolDef type, skipping DefDatabase patch");
                    return false;
                }
                
                // Then try to find the generic method
                Type defDatabaseType = typeof(DefDatabase<>).MakeGenericType(symbolDefType);
                return AccessTools.Method(defDatabaseType, "Add") != null;
            }
            
            // Use TargetMethod to specify the method to patch
            public static MethodBase TargetMethod()
            {
                Type symbolDefType = AccessTools.TypeByName("KCSG.SymbolDef");
                Type defDatabaseType = typeof(DefDatabase<>).MakeGenericType(symbolDefType);
                return AccessTools.Method(defDatabaseType, "Add");
            }
            
            // Prefix method to intercept DefDatabase.Add calls for SymbolDef
            public static void Prefix(object __0)
            {
                if (__0 == null) return;
                
                try
                {
                    // Get the defName from the def
                    PropertyInfo defNameProperty = __0.GetType().GetProperty("defName");
                    if (defNameProperty != null)
                    {
                        string defName = defNameProperty.GetValue(__0) as string;
                        if (!string.IsNullOrEmpty(defName))
                        {
                            // Register the def in our custom registry
                            SymbolRegistry.RegisterDef(defName, __0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in Patch_DefDatabase_Add_SymbolDef: {ex}");
                }
            }
        }
        
        // Patch for DefDatabase<SymbolDef>.GetNamed method
        // This patch intercepts GetNamed calls to also check our unlimited registry
        [HarmonyPatch]
        public static class Patch_DefDatabase_GetNamed_SymbolDef
        {
            // Use Prepare to dynamically locate the method to patch
            public static bool Prepare()
            {
                // First try to find the SymbolDef type
                Type symbolDefType = AccessTools.TypeByName("KCSG.SymbolDef");
                if (symbolDefType == null)
                {
                    Log.Warning("[KCSG Unbound] Could not find SymbolDef type, skipping DefDatabase.GetNamed patch");
                    return false;
                }
                
                // Then try to find the generic method
                Type defDatabaseType = typeof(DefDatabase<>).MakeGenericType(symbolDefType);
                return AccessTools.Method(defDatabaseType, "GetNamed", new Type[] { typeof(string), typeof(bool) }) != null;
            }
            
            // Use TargetMethod to specify the method to patch
            public static MethodBase TargetMethod()
            {
                Type symbolDefType = AccessTools.TypeByName("KCSG.SymbolDef");
                Type defDatabaseType = typeof(DefDatabase<>).MakeGenericType(symbolDefType);
                return AccessTools.Method(defDatabaseType, "GetNamed", new Type[] { typeof(string), typeof(bool) });
            }
            
            // Postfix method to check our registry if vanilla database doesn't have the def
            public static void Postfix(string defName, bool errorOnFail, ref object __result)
            {
                // If vanilla DefDatabase returned null, check our registry
                if (__result == null && !string.IsNullOrEmpty(defName))
                {
                    object symbolDef;
                    if (SymbolRegistry.TryGetDef(defName, out symbolDef))
                    {
                        __result = symbolDef;
                    }
                    else if (errorOnFail)
                    {
                        // If still not found and errorOnFail is true, log a custom error
                        Log.Error($"[KCSG Unbound] Failed to find SymbolDef named '{defName}' in either vanilla database or extended registry");
                    }
                }
            }
        }
        
        // Patch for DefDatabase<SymbolDef>.GetByShortHash method
        // This patch intercepts GetByShortHash calls to handle unlimited symbols
        [HarmonyPatch]
        public static class Patch_DefDatabase_GetByShortHash_SymbolDef
        {
            // Store known short hash to defName mappings
            private static Dictionary<ushort, string> hashToDefName = new Dictionary<ushort, string>();
            
            // Reverse lookup for faster performance
            private static Dictionary<string, ushort> defNameToHash = new Dictionary<string, ushort>();
            
            // Flag to track if we've initialized our hash cache
            private static bool hashCacheInitialized = false;
            
            // Cache the hash method once we find it
            private static MethodInfo shortHashMethod = null;
            
            // Get the hash method using reflection
            private static ushort CalculateShortHash(string text)
            {
                // Initialize method if not done already
                if (shortHashMethod == null)
                {
                    Type shortHashGiverType = typeof(ShortHashGiver);
                    
                    // Try different possible method names
                    string[] possibleMethodNames = new string[] { "GetShortHash", "GiveShortHash", "GiveShortHashString", "GetHashForString" };
                    
                    foreach (string methodName in possibleMethodNames)
                    {
                        shortHashMethod = shortHashGiverType.GetMethod(methodName, 
                            BindingFlags.Public | BindingFlags.Static, 
                            null, 
                            new Type[] { typeof(string) }, 
                            null);
                        
                        if (shortHashMethod != null && shortHashMethod.ReturnType == typeof(ushort))
                        {
                            Log.Message($"[KCSG Unbound] Found hash method: {methodName}");
                            break;
                        }
                    }
                    
                    // If still not found, try to find any method that takes a string and returns ushort
                    if (shortHashMethod == null)
                    {
                        foreach (MethodInfo method in shortHashGiverType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (method.ReturnType == typeof(ushort) && 
                                method.GetParameters().Length == 1 && 
                                method.GetParameters()[0].ParameterType == typeof(string))
                            {
                                shortHashMethod = method;
                                Log.Message($"[KCSG Unbound] Found fallback hash method: {method.Name}");
                                break;
                            }
                        }
                    }
                    
                    // Last resort - try to create a hash manually
                    if (shortHashMethod == null)
                    {
                        Log.Error("[KCSG Unbound] Could not find a suitable hash method, using simple hash fallback");
                        return (ushort)(text.GetHashCode() & 0xFFFF);
                    }
                }
                
                // Invoke the found method
                try
                {
                    return (ushort)shortHashMethod.Invoke(null, new object[] { text });
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error calculating hash for '{text}': {ex}");
                    return (ushort)(text.GetHashCode() & 0xFFFF);
                }
            }
            
            // Use Prepare to dynamically locate the method to patch
            public static bool Prepare()
            {
                // First try to find the SymbolDef type
                Type symbolDefType = AccessTools.TypeByName("KCSG.SymbolDef");
                if (symbolDefType == null)
                {
                    Log.Warning("[KCSG Unbound] Could not find SymbolDef type, skipping DefDatabase.GetByShortHash patch");
                    return false;
                }
                
                // Then try to find the generic method
                Type defDatabaseType = typeof(DefDatabase<>).MakeGenericType(symbolDefType);
                return AccessTools.Method(defDatabaseType, "GetByShortHash") != null;
            }
            
            // Use TargetMethod to specify the method to patch
            public static MethodBase TargetMethod()
            {
                Type symbolDefType = AccessTools.TypeByName("KCSG.SymbolDef");
                Type defDatabaseType = typeof(DefDatabase<>).MakeGenericType(symbolDefType);
                return AccessTools.Method(defDatabaseType, "GetByShortHash");
            }
            
            // Initialize hash cache to avoid repeated hash calculations
            private static void EnsureHashCache()
            {
                if (hashCacheInitialized) return;
                
                try
                {
                    hashToDefName.Clear();
                    defNameToHash.Clear();
                    
                    // Pre-compute hashes for all registered def names
                    foreach (string defName in SymbolRegistry.AllRegisteredDefNames)
                    {
                        if (string.IsNullOrEmpty(defName)) continue;
                        
                        ushort hash = CalculateShortHash(defName);
                        hashToDefName[hash] = defName;
                        defNameToHash[defName] = hash;
                    }
                    
                    hashCacheInitialized = true;
                    Log.Message($"[KCSG Unbound] Hash cache initialized with {hashToDefName.Count} entries");
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error initializing hash cache: {ex}");
                }
            }
            
            // Method to update hash cache when new defs are registered
            public static void RegisterDefHash(string defName)
            {
                if (string.IsNullOrEmpty(defName)) return;
                
                try
                {
                    ushort hash = CalculateShortHash(defName);
                    hashToDefName[hash] = defName;
                    defNameToHash[defName] = hash;
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error registering hash for def '{defName}': {ex}");
                }
            }
            
            // Clear cache if needed (for testing or debugging)
            public static void ClearHashCache()
            {
                hashToDefName.Clear();
                defNameToHash.Clear();
                hashCacheInitialized = false;
            }
            
            // Postfix method to check our registry if vanilla database doesn't have the def
            public static void Postfix(ushort shortHash, ref object __result)
            {
                // If vanilla DefDatabase returned null, check our registry
                if (__result != null) return;
                
                // Ensure hash cache is initialized
                EnsureHashCache();
                
                // Fast lookup using our pre-computed hash cache
                string defName;
                if (hashToDefName.TryGetValue(shortHash, out defName))
                {
                    // We know this hash, try to get the def from our registry
                    object symbolDef;
                    if (SymbolRegistry.TryGetDef(defName, out symbolDef))
                    {
                        __result = symbolDef;
                    }
                }
            }
        }
    }
} 