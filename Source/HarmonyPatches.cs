using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
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
        /// Apply all patches using a safe approach that won't crash if methods aren't found
        /// </summary>
        public static void ApplyPatches(Harmony harmony)
        {
            try
            {
                Log.Message("[KCSG Unbound] Applying Harmony patches using safe approach");
                
                // Try to find the GlobalSettings symbol resolution method first
                var symbolResolutionMethod = FindSymbolResolutionMethod();
                if (symbolResolutionMethod != null)
                {
                    // Get our prefix method
                    var prefix = typeof(Patch_GlobalSettings_TryResolveSymbol)
                        .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                    
                    if (prefix != null)
                    {
                        Log.Message($"[KCSG Unbound] Found resolution method, patching: {symbolResolutionMethod.DeclaringType.Name}.{symbolResolutionMethod.Name}");
                        harmony.Patch(symbolResolutionMethod, prefix: new HarmonyMethod(prefix));
                    }
                }
                else
                {
                    Log.Warning("[KCSG Unbound] Couldn't find resolution method, using alternative patching approach");
                    
                    // Apply alternative patches that don't depend on the exact method
                    ApplyAlternativePatches(harmony);
                }
                
                // Always patch these generic methods that don't rely on specific RimWorld API methods
                Log.Message("[KCSG Unbound] Applying generic patches");
                // Use direct patching instead of PatchAll
                foreach (var method in typeof(Patch_BaseGen_Generate).GetMethods(BindingFlags.Static | BindingFlags.Public))
                {
                    if (method.Name == "Prefix" || method.Name == "Postfix")
                    {
                        var patchMethod = new HarmonyMethod(method);
                        var targetMethod = AccessTools.Method(typeof(BaseGen), "Generate");
                        if (targetMethod != null)
                        {
                            harmony.Patch(targetMethod, 
                                prefix: method.Name == "Prefix" ? patchMethod : null, 
                                postfix: method.Name == "Postfix" ? patchMethod : null);
                        }
                    }
                }
                
                // Directly patch the constructor
                var ctorInfo = AccessTools.Constructor(typeof(SymbolResolver));
                if (ctorInfo != null)
                {
                    var postfixMethod = typeof(Patch_SymbolResolver_Constructor)
                        .GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
                    if (postfixMethod != null)
                    {
                        harmony.Patch(ctorInfo, postfix: new HarmonyMethod(postfixMethod));
                    }
                }
                
                // Apply DefDatabase specific patches
                if (Patch_DefDatabase_Add_SymbolDef.Prepare())
                {
                    Log.Message("[KCSG Unbound] DefDatabase.Add patch applied");
                }
                
                // Try to patch GetByShortHash if possible
                var getByShortHashMethod = Patch_DefDatabase_GetByShortHash_SymbolDef.TargetMethod();
                if (getByShortHashMethod != null)
                {
                    var getByShortHashPrefix = typeof(Patch_DefDatabase_GetByShortHash_SymbolDef)
                        .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                    
                    harmony.Patch(getByShortHashMethod, prefix: new HarmonyMethod(getByShortHashPrefix));
                    Log.Message("[KCSG Unbound] DefDatabase.GetByShortHash patch applied");
                }
                
                Log.Message("[KCSG Unbound] All patches applied successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying patches: {ex}");
            }
        }
        
        /// <summary>
        /// Find the symbol resolution method in RimWorld classes
        /// </summary>
        private static MethodInfo FindSymbolResolutionMethod()
        {
            try
            {
                // First try with GlobalSettings
                var method = typeof(GlobalSettings).GetMethod("TryResolveSymbol", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (method != null)
                {
                    return method;
                }
                
                // Try alternative names for the method that might exist in different versions
                string[] possibleMethodNames = new[] { 
                    "ResolveSymbol", "Resolve", "TryResolve", "DoResolve", 
                    "ResolveSymbolMethod", "TrySymbolResolve", "GenerateSymbol" 
                };
                
                foreach (var methodName in possibleMethodNames)
                {
                    method = typeof(GlobalSettings).GetMethod(methodName, 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        
                    if (method != null && 
                        method.GetParameters().Length >= 1 && 
                        method.GetParameters()[0].ParameterType == typeof(string))
                    {
                        return method;
                    }
                }
                
                // Try looking in BaseGen class
                foreach (var methodName in possibleMethodNames)
                {
                    method = typeof(BaseGen).GetMethod(methodName, 
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        
                    if (method != null && 
                        method.GetParameters().Length >= 1 && 
                        method.GetParameters()[0].ParameterType == typeof(string))
                    {
                        return method;
                    }
                }
                
                // Look for any method in BaseGen or GlobalSettings that takes a symbol string and ResolveParams
                foreach (var type in new[] { typeof(BaseGen), typeof(GlobalSettings) })
                {
                    foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var parameters = m.GetParameters();
                        if (parameters.Length >= 2 &&
                            (parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(ResolveParams) ||
                            parameters[1].ParameterType == typeof(string) && parameters[0].ParameterType == typeof(ResolveParams)))
                        {
                            return m;
                        }
                    }
                }
                
                // Last resort: look for any SymbolResolver.Resolve method
                var symbolResolverType = typeof(SymbolResolver);
                method = symbolResolverType.GetMethod("Resolve", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (method != null)
                {
                    return method;
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error finding symbol resolution method: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// Apply alternative patches that don't depend on specific method names
        /// </summary>
        private static void ApplyAlternativePatches(Harmony harmony)
        {
            try
            {
                // Patch ALL symbol resolver methods in the RimWorld.BaseGen namespace
                foreach (var type in typeof(SymbolResolver).Assembly.GetTypes())
                {
                    if (type.Namespace == "RimWorld.BaseGen" && 
                        (type.IsSubclassOf(typeof(SymbolResolver)) || type == typeof(SymbolResolver)))
                    {
                        // Look for Resolve method
                        var resolveMethod = type.GetMethod("Resolve", 
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            
                        if (resolveMethod != null && resolveMethod.GetParameters().Length > 0)
                        {
                            try
                            {
                                var prefix = typeof(Patch_Alternative_SymbolResolver_Resolve)
                                    .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                                    
                                harmony.Patch(resolveMethod, prefix: new HarmonyMethod(prefix));
                            }
                            catch (Exception ex)
                            {
                                // Skip if we can't patch this specific method
                                Log.Warning($"[KCSG Unbound] Couldn't patch {type.Name}.Resolve: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying alternative patches: {ex}");
            }
        }

        /// <summary>
        /// Patches GlobalSettings.TryResolveSymbol to use our unlimited symbol registry
        /// </summary>
        public static class Patch_GlobalSettings_TryResolveSymbol
        {
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
        /// Alternative patch for all SymbolResolver.Resolve methods
        /// </summary>
        public static class Patch_Alternative_SymbolResolver_Resolve
        {
            public static bool Prefix(object __instance, ResolveParams rp)
            {
                try
                {
                    // Ensure registry is initialized
                    if (!SymbolRegistry.Initialized)
                    {
                        SymbolRegistry.Initialize();
                    }
                    
                    // Try to get the symbol field from the resolver instance
                    Type resolverType = __instance.GetType();
                    FieldInfo symbolField = resolverType.GetField("symbol", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (symbolField != null && symbolField.GetValue(__instance) is string symbol && !string.IsNullOrEmpty(symbol))
                    {
                        // Check if our registry has a resolver for this symbol
                        if (SymbolRegistry.HasResolver(symbol))
                        {
                            // Let our registry try to resolve it first
                            if (SymbolRegistry.TryResolve(symbol, rp))
                            {
                                // Successfully resolved with our registry, skip the original
                                return false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in alternative resolve patch: {ex}");
                }
                
                // Continue with original method
                return true;
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
            
            // Maximum error threshold to avoid overwhelming logs
            private static int maxErrorCount = 20;
            private static int errorCount = 0;
            
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
                        Log.Warning("[KCSG Unbound] Could not find DefDatabase.Add method for patching");
                        return false;
                    }
                    
                    // Get a reference to our static Prefix method
                    MethodInfo prefixMethod = typeof(Patch_DefDatabase_Add_SymbolDef).GetMethod(
                        "PrefixAdd", 
                        BindingFlags.Static | BindingFlags.NonPublic);
                    
                    if (prefixMethod == null)
                    {
                        Log.Warning("[KCSG Unbound] Could not find prefix method for patching");
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
                        
                        if (string.IsNullOrEmpty(defName))
                        {
                            // Skip defs with no name
                            return true;
                        }
                        
                        try
                        {
                            // Register with our shadow registry 
                            SymbolRegistry.RegisterDef(defName, def);
                            
                            // Continue with original method
                            return true;
                        }
                        catch (Exception ex)
                        {
                            // Limit error logging to avoid flooding
                            if (errorCount < maxErrorCount)
                            {
                                errorCount++;
                                Log.Error($"[KCSG Unbound] Error in DefDatabase.Add<SymbolDef> prefix: {ex}");
                                
                                if (errorCount == maxErrorCount)
                                {
                                    Log.Warning("[KCSG Unbound] Maximum error threshold reached, suppressing further error messages");
                                }
                            }
                            
                            // Always continue with original method even if our registration failed
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (errorCount < maxErrorCount)
                    {
                        errorCount++;
                        Log.Error($"[KCSG Unbound] Critical error in DefDatabase.Add<SymbolDef> prefix: {ex}");
                    }
                }
                return true;
            }
        }
        
        /// <summary>
        /// Patch for DefDatabase.GetByShortHash to use our registry
        /// </summary>
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
                    // Ensure the registry is initialized
                    if (!SymbolRegistry.Initialized)
                    {
                        SymbolRegistry.Initialize();
                    }
                    
                    string defName;
                    if (SymbolRegistry.TryGetDefNameByHash(shortHash, out defName))
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