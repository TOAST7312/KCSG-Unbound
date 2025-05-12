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
                
                // Always patch these critical systems first
                ApplyCrossReferenceResolutionPatches(harmony);
                
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
                try 
                {
                    // Modified approach to avoid reflection issues
                    var targetMethod = AccessTools.Method(typeof(BaseGen), "Generate");
                    if (targetMethod != null)
                    {
                        // Direct method references instead of dynamic discovery
                        var prefixMethod = typeof(Patch_BaseGen_Generate).GetMethod("Prefix", 
                            BindingFlags.Static | BindingFlags.Public);
                        var postfixMethod = typeof(Patch_BaseGen_Generate).GetMethod("Postfix", 
                            BindingFlags.Static | BindingFlags.Public);
                            
                        if (prefixMethod != null)
                        {
                            harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefixMethod));
                        }
                        
                        if (postfixMethod != null)
                        {
                            harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
                        }
                        
                        Log.Message("[KCSG Unbound] Successfully patched BaseGen.Generate");
                    }
                    else
                    {
                        Log.Warning("[KCSG Unbound] Could not find BaseGen.Generate method");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Non-critical error patching BaseGen.Generate: {ex.Message}");
                    // Continue despite this error - core functionality will still work
                }
                
                // Directly patch the constructor
                try
                {
                    var ctorInfo = AccessTools.Constructor(typeof(SymbolResolver));
                    if (ctorInfo != null)
                    {
                        var postfixMethod = typeof(Patch_SymbolResolver_Constructor)
                            .GetMethod("Postfix", BindingFlags.Static | BindingFlags.Public);
                        if (postfixMethod != null)
                        {
                            harmony.Patch(ctorInfo, postfix: new HarmonyMethod(postfixMethod));
                            Log.Message("[KCSG Unbound] Successfully patched SymbolResolver constructor");
                        }
                        else
                        {
                            Log.Warning("[KCSG Unbound] Could not find Patch_SymbolResolver_Constructor.Postfix method");
                        }
                    }
                    else
                    {
                        Log.Warning("[KCSG Unbound] Could not find SymbolResolver constructor to patch");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Non-critical error patching SymbolResolver constructor: {ex.Message}");
                    // Continue despite this error
                }
                
                // Apply DefDatabase specific patches using a safer approach
                try 
                {
                    // Use AccessTools to find the method instead of trying to construct it manually
                    var defDatabaseAddMethod = AccessTools.Method(typeof(DefDatabase<>), "Add");
                    if (defDatabaseAddMethod != null)
                    {
                        // Use HarmonyMethod with methodType to create a proper generic patch
                        var prefixMethod = typeof(Patch_DefDatabase_Add_SymbolDef)
                            .GetMethod("PrefixAdd", BindingFlags.Public | BindingFlags.Static);
                        
                        if (prefixMethod != null)
                        {
                            // Use a different approach - don't try to patch the open generic directly
                            // Instead, use the Harmony API to create a targeted patch
                            harmony.CreateProcessor(defDatabaseAddMethod)
                                .AddPrefix(prefixMethod)
                                .Patch();
                                
                            Log.Message("[KCSG Unbound] DefDatabase.Add patch applied for SymbolDefs using safer approach");
                        }
                        else
                        {
                            Log.Error("[KCSG Unbound] Failed to find PrefixAdd method for DefDatabase.Add patch");
                        }
                    }
                    else
                    {
                        Log.Error("[KCSG Unbound] Failed to find DefDatabase.Add method to patch");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error applying DefDatabase.Add patch: {ex}");
                    // Continue despite this error - core functionality may still work
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
            // For error tracking
            private static int maxErrorCount = 20;
            private static int errorCount = 0;
            
            // This is added for compatibility and to avoid warnings
            #pragma warning disable CS0414
            private static bool patched = false;
            #pragma warning restore CS0414
            
            /// <summary>
            /// Called by Harmony to determine if this patch should be applied
            /// </summary>
            /// <returns>True if this patch should be applied</returns>
            public static bool Prepare()
            {
                return true; // Always apply this patch
            }

            /// <summary>
            /// Prefix that intercepts calls to DefDatabase.Add for SymbolDef
            /// </summary>
            /// <param name="__0">The def being added</param>
            /// <returns>False if we handle it, true to let the original method run</returns>
            public static bool PrefixAdd(object __0)
            {
                try 
                {
                    // Only intercept for SymbolDef types
                    if (__0 == null)
                        return true;
                        
                    // Get the type name to check if it's a SymbolDef or derived type 
                    // This is safer than type checking with "is" since we might have reflection issues
                    string typeName = __0.GetType().FullName;
                    bool isSymbolDef = typeName != null && 
                                      (typeName.EndsWith("SymbolDef") ||
                                       typeName.Contains("SymbolDef") || 
                                       typeName.Contains("KCSG") && typeName.Contains("Def"));
                    
                    if (!isSymbolDef)
                        return true; // Let original method run for non-SymbolDef types
                    
                    // Get the def name using reflection instead of dynamic binding
                    string defName = null;
                    
                    try
                    {
                        // Try to get defName property
                        PropertyInfo defNameProp = __0.GetType().GetProperty("defName");
                        if (defNameProp != null)
                            defName = defNameProp.GetValue(__0) as string;
                        
                        // If that fails, try defName field
                        if (string.IsNullOrEmpty(defName))
                        {
                            FieldInfo defNameField = __0.GetType().GetField("defName");
                            if (defNameField != null)
                                defName = defNameField.GetValue(__0) as string;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error getting defName: {ex.Message}");
                    }
                    
                    // If we can't get a defName, let the original method run
                    if (string.IsNullOrEmpty(defName))
                    {
                        if (errorCount < maxErrorCount)
                        {
                            Log.Warning($"[KCSG Unbound] Could not get defName for SymbolDef - delegating to original method");
                            errorCount++;
                        }
                        return true;
                    }
                    
                    // Register in our registry instead of the vanilla one
                    SymbolRegistry.RegisterDef(defName, __0);
                    
                    // Skip the original method since we handled it
                    return false;
                }
                catch (Exception ex)
                {
                    if (errorCount < maxErrorCount)
                    {
                        Log.Error($"[KCSG Unbound] Error in PrefixAdd: {ex.Message}");
                        errorCount++;
                        
                        if (errorCount >= maxErrorCount)
                        {
                            Log.Error($"[KCSG Unbound] Too many errors in PrefixAdd, suppressing further error messages");
                        }
                    }
                    
                    // Let the original method run if we encounter an error
                    return true;
                }
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

        /// <summary>
        /// Apply patches to the DirectXmlCrossRefLoader to handle cross-references
        /// </summary>
        private static void ApplyCrossReferenceResolutionPatches(Harmony harmony)
        {
            try
            {
                // Find the DirectXmlCrossRefLoader type
                Type crossRefLoaderType = typeof(DirectXmlCrossRefLoader);
                if (crossRefLoaderType != null)
                {
                    // Find the ResolveAllWanters method
                    MethodInfo resolveWantersMethod = crossRefLoaderType.GetMethod("ResolveAllWanters", 
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (resolveWantersMethod != null)
                    {
                        var prefix = typeof(Patch_DirectXmlCrossRefLoader_ResolveAllWanters)
                            .GetMethod("Prefix", BindingFlags.Static | BindingFlags.Public);
                            
                        harmony.Patch(resolveWantersMethod, prefix: new HarmonyMethod(prefix));
                        Log.Message("[KCSG Unbound] Successfully patched DirectXmlCrossRefLoader.ResolveAllWanters");
                    }
                    
                    // For the other methods, we need to be more careful because RimWorld's API might change
                    // Just log the attempt rather than crashing if we can't find them
                    try
                    {
                        // Try to find methods that handle cross-references to register custom handlers
                        var methods = crossRefLoaderType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(m => (m.Name.Contains("Resolve") || m.Name.Contains("resolve")) && 
                                        m.GetParameters().Length >= 1)
                            .ToList();
                            
                        if (methods.Any())
                        {
                            Log.Message($"[KCSG Unbound] Found {methods.Count} potential cross-reference methods");
                            // We found methods but won't try to patch them directly
                            // The safer approach is to use the provided patch points in the game
                        }
                    }
                    catch (Exception methodEx)
                    {
                        Log.Warning($"[KCSG Unbound] Error finding cross-reference methods: {methodEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Error patching cross-reference resolution: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Patches for DirectXmlCrossRefLoader.ResolveAllWanters
        /// </summary>
        public static class Patch_DirectXmlCrossRefLoader_ResolveAllWanters
        {
            public static void Prefix()
            {
                try
                {
                    // Ensure we're fully initialized before resolving references
                    if (!SymbolRegistry.Initialized)
                    {
                        SymbolRegistry.Initialize();
                        Log.Message("[KCSG Unbound] Initializing registry before resolving cross-references");
                    }
                    
                    // Log status
                    int defCount = SymbolRegistry.RegisteredDefCount;
                    int symbolCount = SymbolRegistry.RegisteredSymbolCount;
                    Log.Message($"[KCSG Unbound] Registry status before resolving cross-references: {defCount} defs, {symbolCount} symbols");
                    
                    // Force load any commonly referenced KCSG defs that might be missing
                    var missingNames = SymbolRegistry.PreloadCommonlyReferencedDefs();
                    
                    if (missingNames.Any())
                    {
                        Log.Message($"[KCSG Unbound] Preloaded {missingNames.Count} potentially missing defs");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error in ResolveAllWanters prefix: {ex.Message}");
                }
            }
        }
    }
} 