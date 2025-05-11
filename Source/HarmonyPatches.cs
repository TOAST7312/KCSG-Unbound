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
    /// Contains all Harmony patches for KCSG Unbound
    /// </summary>
    public static class HarmonyPatches
    {
        /// <summary>
        /// Patches GlobalSettings.TryResolveSymbol to use our unlimited symbol registry
        /// Instead of directly patching the abstract Resolve method, we patch the method that calls it
        /// </summary>
        [HarmonyPatch]
        public static class Patch_GlobalSettings_TryResolveSymbol
        {
            // Dynamically find the method to patch since it might have different names
            public static MethodBase TargetMethod()
            {
                try
                {
                    // First check for RimWorld's standard method
                    var method = typeof(GlobalSettings).GetMethod("TryResolveSymbol", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (method != null)
                    {
                        Log.Message("[KCSG Unbound] Found method GlobalSettings.TryResolveSymbol");
                        return method;
                    }
                    
                    // Try alternative names for the method that might exist in different versions
                    string[] possibleMethodNames = new[] { 
                        "ResolveSymbol", "Resolve", "TryResolve", "DoResolve", 
                        "ResolveSymbolMethod", "TrySymbolResolve" 
                    };
                    
                    foreach (var methodName in possibleMethodNames)
                    {
                        method = typeof(GlobalSettings).GetMethod(methodName, 
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            
                        if (method != null && 
                            method.GetParameters().Length >= 1 && 
                            method.GetParameters()[0].ParameterType == typeof(string))
                        {
                            Log.Message($"[KCSG Unbound] Found alternative symbol resolution method: {methodName}");
                            return method;
                        }
                    }
                    
                    Log.Warning("[KCSG Unbound] Could not find any suitable resolution method in GlobalSettings");
                    return null;
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error finding method to patch: {ex}");
                    return null;
                }
            }
            
            // Prefix for the resolution method - different parameter names to handle various method signatures
            public static bool Prefix(string ___symbol, string symbol, ResolveParams rp, ref bool __result)
            {
                try
                {
                    // Use whichever parameter is not null (handles different parameter names)
                    string symbolToUse = symbol ?? ___symbol;
                    
                    if (string.IsNullOrEmpty(symbolToUse))
                        return true; // Continue with original method if no symbol
                        
                    // Try to resolve using our registry
                    if (SymbolRegistry.TryResolve(symbolToUse, rp))
                    {
                        __result = true;
                        return false; // Skip original method
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in resolution method prefix: {ex}");
                }
                
                return true; // Continue with original method
            }
        }

        /// <summary>
        /// Patches BaseGen to intercept symbol resolution through a transpiler
        /// This will work even if we can't find the standard resolution methods
        /// </summary>
        [HarmonyPatch(typeof(BaseGen), "Generate")]
        public static class Patch_BaseGen_Generate
        {
            public static void Prefix()
            {
                try
                {
                    // Ensure registry is initialized before generation starts
                    if (!SymbolRegistry.Initialized)
                    {
                        SymbolRegistry.Initialize();
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in Patch_BaseGen_Generate.Prefix: {ex}");
                }
            }
        }

        /// <summary>
        /// Patches SymbolResolver's constructor to ensure any symbol param is registered
        /// This patch works on the concrete constructor, not an abstract method
        /// </summary>
        [HarmonyPatch(typeof(RimWorld.BaseGen.SymbolResolver), MethodType.Constructor)]
        public static class Patch_SymbolResolver_Constructor
        {
            public static void Postfix(RimWorld.BaseGen.SymbolResolver __instance)
            {
                try
                {
                    // Ensure registry is initialized
                    if (!SymbolRegistry.Initialized)
                    {
                        SymbolRegistry.Initialize();
                    }
                    
                    // Get the resolver type and register it if it has a resolverSymbol field/property
                    Type resolverType = __instance.GetType();
                    FieldInfo symbolField = resolverType.GetField("symbol", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (symbolField != null && symbolField.GetValue(__instance) is string symbol && !string.IsNullOrEmpty(symbol))
                    {
                        SymbolRegistry.Register(symbol, resolverType);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in Patch_SymbolResolver_Constructor.Postfix: {ex}");
                }
            }
        }

        /// <summary>
        /// Fix for the ambiguous match for DefDatabase.Add<SymbolDef>
        /// Uses a more specific targeting approach to avoid conflicts
        /// </summary>
        public static class Patch_DefDatabase_Add_SymbolDef
        {
            // Track if the patch has been applied
            private static bool patched = false;
            
            // Cache for registered def short hashes
            private static Dictionary<ushort, string> shortHashToDefName = new Dictionary<ushort, string>();
            
            /// <summary>
            /// Called by Harmony to determine if this patch should be applied
            /// </summary>
            public static bool Prepare()
            {
                try
                {
                    if (patched) return false;
                    
                    // Get the generic type definition
                    Type defDatabaseType = typeof(DefDatabase<>).GetGenericTypeDefinition();
                    
                    // Find the KCSG.SymbolDef type dynamically to avoid direct dependencies
                    Type symbolDefType = null;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try 
                        {
                            foreach (var type in assembly.GetTypes())
                            {
                                if (type.FullName == "KCSG.SymbolDef" || type.Name == "SymbolDef" && type.Namespace == "KCSG")
                                {
                                    symbolDefType = type;
                                    break;
                                }
                            }
                            if (symbolDefType != null) break;
                        }
                        catch (Exception) { continue; } // Skip assemblies that can't be reflected
                    }
                    
                    if (symbolDefType == null)
                    {
                        Log.Warning("[KCSG Unbound] Could not find KCSG.SymbolDef type - using fallback");
                        // Try using Def as a base type if we can't find the exact SymbolDef
                        symbolDefType = typeof(Def);
                    }
                    
                    // Create the closed generic type
                    Type closedDefDatabaseType = defDatabaseType.MakeGenericType(symbolDefType);
                    
                    // Get the Add method with parameter constraints to avoid ambiguity
                    MethodInfo addMethod = null;
                    
                    foreach (var method in closedDefDatabaseType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (method.Name == "Add" && 
                            method.GetParameters().Length == 1 && 
                            method.GetParameters()[0].ParameterType == symbolDefType)
                        {
                            addMethod = method;
                            break;
                        }
                    }
                    
                    if (addMethod == null)
                    {
                        Log.Error("[KCSG Unbound] Could not find DefDatabase.Add method for patching");
                        return false;
                    }
                    
                    // Get a reference to our static Prefix method
                    MethodInfo prefixMethod = typeof(Patch_DefDatabase_Add_SymbolDef).GetMethod(
                        "PrefixAdd", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (prefixMethod == null)
                    {
                        Log.Error("[KCSG Unbound] Could not find prefix method for patching");
                        return false;
                    }
                    
                    // Create a dynamic harmony patch
                    var harmony = new Harmony("com.kcsg.unbound.defdb");
                    harmony.Patch(addMethod, 
                        prefix: new HarmonyMethod(prefixMethod));
                    
                    Log.Message("[KCSG Unbound] Successfully applied Patch_DefDatabase_Add_SymbolDef");
                    patched = true;
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in Patch_DefDatabase_Add_SymbolDef.Prepare: {ex}");
                    return false;
                }
            }
            
            /// <summary>
            /// Prefix method for DefDatabase.Add<SymbolDef>
            /// </summary>
            private static bool PrefixAdd(object __0)
            {
                try
                {
                    // Ensure registry is initialized
                    if (!SymbolRegistry.Initialized)
                    {
                        SymbolRegistry.Initialize();
                    }
                    
                    if (__0 is Def def)
                    {
                        string defName = def.defName;
                        
                        // Register with our shadow registry
                        SymbolRegistry.RegisterDef(defName, def);
                        
                        // Calculate and register short hash
                        RegisterDefHash(defName);
                        
                        // Continue with original method
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in DefDatabase.Add<SymbolDef> prefix: {ex}");
                }
                return true;
            }
            
            /// <summary>
            /// Registers a def name with its hash for fast lookup
            /// </summary>
            public static void RegisterDefHash(string defName)
            {
                if (string.IsNullOrEmpty(defName)) return;
                
                // Calculate short hash (same algorithm as RimWorld)
                ushort shortHash = 0;
                for (int i = 0; i < defName.Length; i++)
                {
                    shortHash = (ushort)((shortHash << 5) - shortHash + defName[i]);
                }
                
                // Register in our cache
                shortHashToDefName[shortHash] = defName;
            }
            
            /// <summary>
            /// Clears the short hash cache
            /// </summary>
            public static void ClearHashCache()
            {
                shortHashToDefName.Clear();
            }
            
            /// <summary>
            /// Attempts to get a def name by its short hash
            /// </summary>
            public static bool TryGetDefNameByHash(ushort shortHash, out string defName)
            {
                return shortHashToDefName.TryGetValue(shortHash, out defName);
            }
        }
        
        /// <summary>
        /// Patch for DefDatabase.GetByShortHash to use our registry
        /// </summary>
        [HarmonyPatch]
        public static class Patch_DefDatabase_GetByShortHash_SymbolDef
        {
            /// <summary>
            /// Dynamically determine the method to patch
            /// </summary>
            public static MethodBase TargetMethod()
            {
                try
                {
                    // Find the KCSG.SymbolDef type
                    Type symbolDefType = null;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try 
                        {
                            foreach (var type in assembly.GetTypes())
                            {
                                if (type.FullName == "KCSG.SymbolDef" || type.Name == "SymbolDef" && type.Namespace == "KCSG")
                                {
                                    symbolDefType = type;
                                    break;
                                }
                            }
                            if (symbolDefType != null) break;
                        }
                        catch (Exception) { continue; }
                    }
                    
                    if (symbolDefType == null)
                    {
                        // Fallback to Def if needed
                        symbolDefType = typeof(Def);
                    }
                    
                    // Create the closed generic type
                    Type defDatabaseType = typeof(DefDatabase<>).MakeGenericType(symbolDefType);
                    
                    // Get the GetByShortHash method
                    return defDatabaseType.GetMethod("GetByShortHash", 
                        BindingFlags.Public | BindingFlags.Static, 
                        null, 
                        new[] { typeof(ushort) }, 
                        null);
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in Patch_DefDatabase_GetByShortHash_SymbolDef.TargetMethod: {ex}");
                    return null;
                }
            }
            
            /// <summary>
            /// Prefix for GetByShortHash to intercept calls and use our shadow registry
            /// </summary>
            public static bool Prefix(ushort shortHash, ref object __result)
            {
                try
                {
                    string defName;
                    if (Patch_DefDatabase_Add_SymbolDef.TryGetDefNameByHash(shortHash, out defName))
                    {
                        // Try to get from our shadow registry
                        object symbolDef;
                        if (SymbolRegistry.TryGetDef(defName, out symbolDef))
                        {
                            __result = symbolDef;
                            return false; // Skip original method
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in GetByShortHash prefix: {ex}");
                }
                return true; // Continue with original method
            }
        }
    }
} 