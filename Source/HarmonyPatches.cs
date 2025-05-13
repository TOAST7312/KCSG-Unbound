using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Linq.Expressions;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace KCSG
{
    public static class HarmonyPatches
    {
        // Statics to track what we've scanned
        private static bool patchesApplied = false;
        private static Harmony harmonyInstance = null;
        
        // Used for caching reflection discoveries
        private static Dictionary<string, Type> typeCache = new Dictionary<string, Type>();
        
        /// <summary>
        /// Helper method to check if a ResolveParams struct is essentially empty/default
        /// </summary>
        public static bool IsDefault(ResolveParams rp)
        {
            // Since ResolveParams is a struct, we can't directly check for null
            // Instead, we'll check a few common properties that would typically be set
            try
            {
                // Try to get a few key properties or fields via reflection
                var rect = typeof(ResolveParams).GetField("rect").GetValue(rp);
                var faction = typeof(ResolveParams).GetField("faction").GetValue(rp);
                var singlePawnLord = typeof(ResolveParams).GetField("singlePawnLord").GetValue(rp);
                
                // If all are null/default, it's probably an uninitialized struct
                return rect == null && faction == null && singlePawnLord == null;
            }
            catch
            {
                // If we get an exception, something is wrong with the struct
                return true;
            }
        }
        
        /// <summary>
        /// Applies all necessary Harmony patches
        /// </summary>
        public static void ApplyPatches(Harmony harmony)
        {
            if (patchesApplied)
            {
                Log.Message("[KCSG Unbound] Patches already applied, skipping");
                return;
            }
            
            harmonyInstance = harmony;
            
            try
            {
                Log.Message("[KCSG Unbound] Applying BaseGen.Generate prefix patch");
                
                // Let's try multiple strategies to find the correct method
                var originalMethod = GetBaseGenGenerateMethod();
                if (originalMethod != null)
                {
                    // Create a dynamic prefix method based on the parameters
                    var dynamicPrefix = CreateDynamicPrefixMethod(originalMethod);
                    
                    // Apply the patch
                    harmony.Patch(originalMethod, new HarmonyMethod(dynamicPrefix));
                    Log.Message($"[KCSG Unbound] Successfully patched BaseGen.Generate with signature: {DescribeMethodSignature(originalMethod)}");
                }
                else
                {
                    Log.Error("[KCSG Unbound] Failed to find BaseGen.Generate method!");
                    ApplyFallbackPatches(harmony);
                }
                
                // Also apply the DefDatabase patch to check for maximum def ID
                PatchDefDatabaseTryGetDef(harmony);
                
                // Track that we've applied patches
                patchesApplied = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying patches: {ex}");
                ApplyFallbackPatches(harmony);
            }
        }
        
        /// <summary>
        /// Creates a method description for logging
        /// </summary>
        private static string DescribeMethodSignature(MethodInfo method)
        {
            if (method == null) return "null";
            
            return $"{method.ReturnType.Name} {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})";
        }
        
        /// <summary>
        /// Creates a dynamic prefix method appropriate for the target method's parameter list
        /// </summary>
        private static MethodInfo CreateDynamicPrefixMethod(MethodInfo targetMethod)
        {
            // Check the parameters to decide which prefix to use
            var parameters = targetMethod.GetParameters();
            
            if (parameters.Length == 0)
            {
                // No parameters - use a simple prefix
                return typeof(BaseGenPatch).GetMethod("Prefix_NoParams", 
                    BindingFlags.Public | BindingFlags.Static);
            }
            
            // Look for a string parameter that might be the symbol
            var stringParam = parameters.FirstOrDefault(p => p.ParameterType == typeof(string));
            var resolveParamsParam = parameters.FirstOrDefault(p => p.ParameterType.Name == "ResolveParams");
            
            if (stringParam != null && resolveParamsParam != null)
            {
                // Has both string and ResolveParams - use the standard prefix
                return typeof(BaseGenPatch).GetMethod("Prefix", 
                    BindingFlags.Public | BindingFlags.Static);
            }
            else if (resolveParamsParam != null)
            {
                // Only has ResolveParams - the symbol might be inside it
                return typeof(BaseGenPatch).GetMethod("Prefix_ResolveParamsOnly", 
                    BindingFlags.Public | BindingFlags.Static);
            }
            else
            {
                // Fallback case - use a generic prefix that can inspect at runtime
                return typeof(BaseGenPatch).GetMethod("Prefix_Generic", 
                    BindingFlags.Public | BindingFlags.Static);
            }
        }
        
        /// <summary>
        /// Apply fallback patches if primary patch fails
        /// </summary>
        private static void ApplyFallbackPatches(Harmony harmony)
        {
            Log.Message("[KCSG Unbound] Applying fallback patch to BaseGen.Generate");
            try
            {
                // Direct patching of symbol resolvers instead
                Type symbolResolverType = typeof(SymbolResolver);
                MethodInfo resolveMethod = symbolResolverType.GetMethod("Resolve", 
                    BindingFlags.Public | BindingFlags.Instance);
                
                if (resolveMethod != null)
                {
                    Log.Message("[KCSG Unbound] Found SymbolResolver.Resolve method, applying patch");
                    
                    // Use a specialized prefix for SymbolResolver.Resolve
                    var prefix = typeof(SymbolResolverPatch).GetMethod("Prefix", 
                        BindingFlags.Public | BindingFlags.Static);
                        
                    harmony.Patch(resolveMethod, new HarmonyMethod(prefix));
                    Log.Message("[KCSG Unbound] Successfully patched SymbolResolver.Resolve");
                    return;
                }
                
                // If direct patching failed, try to find all methods containing "symbol" in parameter names
                Log.Message("[KCSG Unbound] Direct patching failed, trying to find methods with symbol parameters");
                
                var symbolMethods = FindMethodsWithSymbolParameter();
                if (symbolMethods.Count > 0)
                {
                    int patchCount = 0;
                    
                    foreach (var method in symbolMethods)
                    {
                        try
                        {
                            // Create a dynamic prefix based on the method's parameters
                            var dynamicPrefix = CreateDynamicPrefixMethod(method);
                            
                            harmony.Patch(method, new HarmonyMethod(dynamicPrefix));
                            patchCount++;
                            
                            Log.Message($"[KCSG Unbound] Successfully patched {method.DeclaringType.Name}.{method.Name}");
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[KCSG Unbound] Could not patch method {method.DeclaringType.Name}.{method.Name}: {ex.Message}");
                        }
                    }
                    
                    Log.Message($"[KCSG Unbound] Applied fallback patches to {patchCount} methods with symbol parameters");
                }
                else
                {
                    Log.Error("[KCSG Unbound] Failed to find any patchable methods!");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Critical error applying fallback patches: {ex}");
            }
        }
        
        /// <summary>
        /// Try to find BaseGen.Generate method
        /// </summary>
        private static MethodInfo GetBaseGenGenerateMethod()
        {
            try
            {
                // First try with direct access
                Type baseGenType = typeof(BaseGen);
                
                // Look for methods named Generate without assuming parameters
                var candidates = baseGenType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "Generate")
                    .ToList();
                
                if (candidates.Count > 0)
                {
                    // Log what we found
                    foreach (var candidate in candidates)
                    {
                        Log.Message($"[KCSG Unbound] Found BaseGen.Generate candidate: {DescribeMethodSignature(candidate)}");
                    }
                    
                    // Prefer methods with both string and ResolveParams parameters
                    var bestMatch = candidates.FirstOrDefault(m => 
                        m.GetParameters().Any(p => p.ParameterType == typeof(string)) &&
                        m.GetParameters().Any(p => p.ParameterType.Name == "ResolveParams"));
                    
                    if (bestMatch != null)
                    {
                        Log.Message("[KCSG Unbound] Found ideal BaseGen.Generate with string and ResolveParams parameters");
                        return bestMatch;
                    }
                    
                    // Next, prefer methods with ResolveParams
                    bestMatch = candidates.FirstOrDefault(m => 
                        m.GetParameters().Any(p => p.ParameterType.Name == "ResolveParams"));
                    
                    if (bestMatch != null)
                    {
                        Log.Message("[KCSG Unbound] Found BaseGen.Generate with ResolveParams parameter");
                        return bestMatch;
                    }
                    
                    // Last resort, use the first Generate method we found
                    Log.Message("[KCSG Unbound] Using first found BaseGen.Generate method");
                    return candidates[0];
                }
                
                // If that fails, try alternative approaches
                return FindSymbolResolverMethod();
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error finding BaseGen.Generate: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Patches DefDatabase.TryGetDef to handle short IDs greater than 65535
        /// </summary>
        private static void PatchDefDatabaseTryGetDef(Harmony harmony)
        {
            try
        {
                // Get the generic method definition first
                Type defDatabaseType = typeof(DefDatabase<>);
                MethodInfo tryGetDefMethod = defDatabaseType.GetMethod("TryGetDef", 
                    BindingFlags.Public | BindingFlags.Static);
                
                if (tryGetDefMethod != null)
                {
                    Log.Message("[KCSG Unbound] Found DefDatabase.TryGetDef method");
                    
                    // Get our prefix method
                    MethodInfo prefixMethod = typeof(DefDatabasePatch).GetMethod("Prefix", 
                        BindingFlags.Public | BindingFlags.Static);
                    
                    // Now we need to create closed generic versions for types we care about
                    Type defType = typeof(Def);
                    var closedType = defDatabaseType.MakeGenericType(defType);
                    
                    // Use Cecil to get the specific method
                    MethodInfo specificMethod = AccessTools.Method(closedType, "TryGetDef");
                    
                    if (specificMethod != null)
                    {
                        harmony.Patch(specificMethod, new HarmonyMethod(prefixMethod));
                        Log.Message("[KCSG Unbound] Successfully patched DefDatabase<Def>.TryGetDef");
                    }
                    else
                    {
                        Log.Error("[KCSG Unbound] Could not find specific TryGetDef method to patch");
                    }
                }
                else
                {
                    Log.Error("[KCSG Unbound] Could not find DefDatabase.TryGetDef method");
                    }
                }
                catch (Exception ex)
                {
                Log.Error($"[KCSG Unbound] Error patching DefDatabase.TryGetDef: {ex}");
            }
        }
        
        /// <summary>
        /// Find methods with a symbol parameter that are used for symbol resolution
        /// </summary>
        private static List<MethodInfo> FindMethodsWithSymbolParameter()
        {
            var result = new List<MethodInfo>();
            var potentialMethods = new List<Tuple<Type, MethodInfo>>();
            
            // First check all SymbolResolvers
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        // Skip non-RimWorld types
                        if (type.Namespace != "RimWorld" && 
                            type.Namespace != "RimWorld.BaseGen" && 
                            type.Namespace != "Verse" &&
                            !type.Name.Contains("SymbolResolver"))
                            continue;
                            
                        // Check each method
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                        {
                            if (MethodMightResolveSymbols(type, method))
                            {
                                potentialMethods.Add(new Tuple<Type, MethodInfo>(type, method));
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors from problematic assemblies
                }
            }
            
            // Log a summary of what we found
            if (potentialMethods.Count > 0)
            {
                Log.Message($"[KCSG Unbound] Found {potentialMethods.Count} potential resolution methods");
                
                // Group methods by type for more organized logging
                var byNameSignature = potentialMethods.Where(t => 
                    (t.Item2.Name.Contains("Resolve") || t.Item2.Name.Contains("Symbol") || 
                     t.Item2.Name.Contains("Generate")) && 
                    t.Item2.GetParameters().Any(p => p.ParameterType == typeof(string)))
                    .ToList();
                
                var byResolveParamsWithSymbol = potentialMethods.Where(t => 
                    t.Item2.GetParameters().Any(p => p.ParameterType.Name == "ResolveParams") && 
                    t.Item2.GetParameters().Any(p => p.Name?.ToLower().Contains("symbol") ?? false))
                    .ToList();
                
                var byExactNameMatch = potentialMethods.Where(t => 
                    (t.Item2.Name == "Resolve" || t.Item2.Name == "Generate"))
                    .ToList();
                
                // Log summaries instead of individual methods
                if (byNameSignature.Count > 0)
                    Log.Message($"[KCSG Unbound] Found {byNameSignature.Count} methods by name+signature match");
                
                if (byResolveParamsWithSymbol.Count > 0)
                    Log.Message($"[KCSG Unbound] Found {byResolveParamsWithSymbol.Count} methods with ResolveParams+symbol field");
                
                if (byExactNameMatch.Count > 0)
                    Log.Message($"[KCSG Unbound] Found {byExactNameMatch.Count} methods by exact name match");
                
                // Only output up to 10 methods of each type to avoid spam
                Diagnostics.LogVerbose($"By name+signature (first 10 of {byNameSignature.Count}):");
                foreach (var m in byNameSignature.Take(10))
                    Diagnostics.LogVerbose($"  {m.Item1.Name}.{m.Item2.Name}");
                
                Diagnostics.LogVerbose($"By ResolveParams+symbol (first 10 of {byResolveParamsWithSymbol.Count}):");
                foreach (var m in byResolveParamsWithSymbol.Take(10))
                    Diagnostics.LogVerbose($"  {m.Item1.Name}.{m.Item2.Name}");
                
                Diagnostics.LogVerbose($"By exact name match (first 10 of {byExactNameMatch.Count}):");
                foreach (var m in byExactNameMatch.Take(10))
                    Diagnostics.LogVerbose($"  {m.Item1.Name}.{m.Item2.Name}");
                
                // Add all methods to the result
                result.AddRange(potentialMethods.Select(t => t.Item2));
            }
            
            return result;
        }
        
        /// <summary>
        /// Checks if a method might be used to resolve symbols
        /// </summary>
        private static bool MethodMightResolveSymbols(Type type, MethodInfo method)
        {
            try
            {
                // Skip some methods we know aren't relevant
                if (method.IsGenericMethod || method.IsConstructor || method.ReturnType == typeof(void))
                    return false;
                    
                // Check method name - does it contain keywords?
                bool nameMatch = method.Name.Contains("Resolve") || 
                                 method.Name.Contains("Symbol") || 
                                 method.Name.Contains("Generate") ||
                                 method.Name == "Resolve" ||
                                 method.Name == "Generate";
                
                // Check parameters - are there any string params that might be symbols?
                var parameters = method.GetParameters();
                bool hasStringParam = parameters.Any(p => p.ParameterType == typeof(string));
                
                // Check for ResolveParams type with symbol field
                bool hasResolveParams = parameters.Any(p => p.ParameterType.Name == "ResolveParams");
                bool hasSymbolParamName = parameters.Any(p => p.Name?.ToLower().Contains("symbol") ?? false);
                
                // Return true if this looks like a resolution method
                if ((nameMatch && hasStringParam) || 
                    (hasResolveParams && hasSymbolParamName) ||
                    (method.Name == "Resolve" || method.Name == "Generate"))
                {
                    return true;
                }
                
                return false;
                }
            catch
            {
                // If we hit any errors evaluating the method, skip it
                return false;
            }
        }

        /// <summary>
        /// Tries to find a method that resolves symbols using reflection
        /// </summary>
        private static MethodInfo FindSymbolResolverMethod()
        {
            Log.Message("[KCSG Unbound] Trying to find symbol resolver method through reflection");
            
            // Try to find the method that takes a symbol string
                try
                {
                // Find methods that might resolve symbols
                var methods = FindMethodsWithSymbolParameter();
                
                // Return the first method that looks most promising
                foreach (var method in methods)
                {
                    if (method.Name == "Generate" && method.DeclaringType.Name == "BaseGen")
                    {
                        Log.Message("[KCSG Unbound] Found BaseGen.Generate via reflection!");
                        return method;
                    }
                }
                
                // If we can't find BaseGen.Generate specifically, return the first Generate or Resolve method
                foreach (var method in methods)
                {
                    if (method.Name == "Generate" || method.Name == "Resolve")
                    {
                        Log.Message($"[KCSG Unbound] Found fallback method {method.DeclaringType.Name}.{method.Name}");
                        return method;
                    }
                }
                
                // Last resort - return any method that might work
                if (methods.Count > 0)
                    {
                    Log.Message($"[KCSG Unbound] Found last-resort method {methods[0].DeclaringType.Name}.{methods[0].Name}");
                    return methods[0];
                    }
                }
                catch (Exception ex)
                {
                Log.Error($"[KCSG Unbound] Error finding symbol resolver method: {ex}");
                }
            
            return null;
        }

        /// <summary>
        /// Patch for DefDatabase.Add - specifically for SymbolDef types
        /// </summary>
        public static class Patch_DefDatabase_Add_SymbolDef
        {
            /// <summary>
            /// This prevents RimWorld from throwing errors when it exceeds the 65535 limit for symbols
            /// </summary>
            public static bool PrefixAdd(object __instance, ref object __0)
            {
                try 
                {
                    // If we're not initialized or this isn't a symbol def, let the original method handle it
                    if (!SymbolRegistry.Initialized)
                        return true;
                        
                    // Check if this is a def that we should handle
                    var def = __0 as Def;
                    if (def != null && 
                        !string.IsNullOrEmpty(def.defName) && 
                        (def.defName.StartsWith("VFED_") || 
                         def.defName.StartsWith("VFEM_") ||
                         def.defName.StartsWith("VFEA_") ||
                         def.defName.StartsWith("VFEC_") ||
                         def.defName.StartsWith("VFEE_") ||
                         def.defName.StartsWith("VBGE_") ||
                         def.defName.StartsWith("VOE_") ||
                         def.defName.StartsWith("VFE_") ||
                         def.defName.StartsWith("KCSG_")))
                    {
                        // Register this def with our system instead
                        Diagnostics.LogVerbose($"[DefDatabase.Add Patch] Registering {def.defName} with SymbolRegistry");
                        SymbolRegistry.RegisterDef(def.defName, __0);
                    
                        // Skip the original method to avoid defName hash collisions
                    return false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in DefDatabase.Add patch: {ex}");
                    }
                    
                // Continue to original method for other types
                    return true;
            }
        }
        
        /// <summary>
        /// Patch for DefDatabase.GetByShortHash - specifically for SymbolDef types
        /// </summary>
        public static class Patch_DefDatabase_GetByShortHash_SymbolDef
        {
            /// <summary>
            /// This intercepts short hash lookups and routes them through our registry
            /// </summary>
            public static bool Prefix(ref Type __0, ref ushort __1, ref object __result)
            {
                try
                {
                    // Only intercept when registry is initialized
                    if (!SymbolRegistry.Initialized)
                        return true;
                        
                    // Check if we have this hash
                    if (SymbolRegistry.HasShortHashOverride(__1))
                            {
                        // Get the def from our registry
                        __result = SymbolRegistry.GetDefByShortHash(__0, __1);
                        
                        // Skip original method
                        return false;
                    }
                        }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error in DefDatabase.GetByShortHash patch: {ex}");
                }
                
                // Continue to original method
                return true;
            }
        }
    }
    
    /// <summary>
    /// Patch for BaseGen.Generate
    /// </summary>
    public static class BaseGenPatch
    {
        /// <summary>
        /// Standard prefix for symbol resolution (expects string symbol, ResolveParams resolveParams)
        /// </summary>
        public static bool Prefix(string symbol, ResolveParams resolveParams)
        {
            if (string.IsNullOrEmpty(symbol) || HarmonyPatches.IsDefault(resolveParams))
                return true; // Continue to original method
                
            // If SymbolRegistry isn't initialized, we have a problem!
            if (!SymbolRegistry.Initialized)
            {
                try
                {
                    SymbolRegistry.Initialize();
                    Diagnostics.LogVerbose($"[BaseGenPatch] Initialized SymbolRegistry during Generate call for symbol {symbol}");
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Failed to initialize SymbolRegistry during Generate call: {ex}");
                    return true; // Continue to original method
                }
            }
            
            // Check if we have a resolver for this symbol
            if (SymbolRegistry.HasResolver(symbol))
            {
                try
                {
                    // Let the registry handle the symbol
                    Diagnostics.LogVerbose($"[BaseGenPatch] Redirecting symbol {symbol} to SymbolRegistry");
                    SymbolRegistry.Resolve(symbol, resolveParams);
                    return false; // Skip original method
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error resolving symbol {symbol}: {ex}");
                    return true; // Continue to original method as fallback
                }
            }
            
            // If we don't know this symbol, let the original method handle it
            return true;
        }
        
        /// <summary>
        /// Prefix for methods with only ResolveParams (symbol might be inside)
        /// </summary>
        public static bool Prefix_ResolveParamsOnly(ResolveParams resolveParams)
        {
            if (HarmonyPatches.IsDefault(resolveParams))
                return true; // Continue to original method
                
            // Try to extract symbol from ResolveParams
            string symbol = null;
            
            try
            {
                // Use reflection to get the symbol field or property
                var symbolField = AccessTools.Field(typeof(ResolveParams), "symbol");
                if (symbolField != null)
                {
                    symbol = symbolField.GetValue(resolveParams) as string;
                }
                else
                {
                    // Try as a property
                    var symbolProperty = AccessTools.Property(typeof(ResolveParams), "symbol");
                    if (symbolProperty != null)
                    {
                        symbol = symbolProperty.GetValue(resolveParams) as string;
                    }
                }
            }
            catch
            {
                // If we can't extract the symbol, continue to original method
                return true;
            }
            
            if (string.IsNullOrEmpty(symbol))
                return true; // Continue to original method
                
            // Now use our standard symbol resolution logic
            return Prefix(symbol, resolveParams);
        }
        
        /// <summary>
        /// Generic prefix for unknown method signatures (inspects at runtime)
        /// </summary>
        public static bool Prefix_Generic(object __instance, MethodBase __originalMethod, object[] __args)
        {
            // Extract the ResolveParams and symbol from args
            ResolveParams resolveParams = default;
            bool hasResolveParams = false;
            string symbol = null;
            
            foreach (var arg in __args)
            {
                if (arg is ResolveParams rp)
                {
                    resolveParams = rp;
                    hasResolveParams = true;
                }
                else if (arg is string str && string.IsNullOrEmpty(symbol))
                {
                    symbol = str;
                }
            }
            
            // If no symbol and we have ResolveParams, try to extract symbol from it
            if (string.IsNullOrEmpty(symbol) && hasResolveParams && !HarmonyPatches.IsDefault(resolveParams))
            {
                try
                {
                    // Use reflection to get the symbol field or property
                    var symbolField = AccessTools.Field(typeof(ResolveParams), "symbol");
                    if (symbolField != null)
                    {
                        symbol = symbolField.GetValue(resolveParams) as string;
                    }
                    else
                    {
                        // Try as a property
                        var symbolProperty = AccessTools.Property(typeof(ResolveParams), "symbol");
                        if (symbolProperty != null)
                        {
                            symbol = symbolProperty.GetValue(resolveParams) as string;
                        }
                    }
                }
                catch
                {
                    // If we can't extract the symbol, continue to original method
                    return true;
                }
            }
            
            if (string.IsNullOrEmpty(symbol) || !hasResolveParams || HarmonyPatches.IsDefault(resolveParams))
                return true; // Continue to original method
                
            // Now use our standard symbol resolution logic
            return Prefix(symbol, resolveParams);
        }
        
        /// <summary>
        /// Prefix for methods with no parameters
        /// </summary>
        public static bool Prefix_NoParams()
        {
            // No parameters, so we can't handle symbol resolution
            // Just let the original method run
            return true;
        }
    }
    
    /// <summary>
    /// Patch specifically for SymbolResolver.Resolve
    /// </summary>
    public static class SymbolResolverPatch
    {
        /// <summary>
        /// Prefix for SymbolResolver.Resolve
        /// </summary>
        public static bool Prefix(object __instance, ResolveParams rp)
        {
            if (__instance == null || HarmonyPatches.IsDefault(rp))
                return true; // Continue to original method
                
            // Try to extract the symbol from the resolver's class name
            string resolverName = __instance.GetType().Name;
            string symbol = null;
            
            if (resolverName.StartsWith("SymbolResolver_"))
            {
                symbol = resolverName.Substring("SymbolResolver_".Length);
            }
            
            if (string.IsNullOrEmpty(symbol))
                return true; // Continue to original method
                
            // Check if we have a resolver for this symbol
            if (SymbolRegistry.HasResolver(symbol))
            {
                try
                {
                    // Let the registry handle the symbol
                    Diagnostics.LogVerbose($"[SymbolResolverPatch] Redirecting symbol {symbol} to SymbolRegistry");
                    SymbolRegistry.Resolve(symbol, rp);
                    return false; // Skip original method
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG Unbound] Error resolving symbol {symbol}: {ex}");
                    return true; // Continue to original method as fallback
                }
            }
            
            // If we don't know this symbol, let the original method handle it
            return true;
        }
    }

    /// <summary>
    /// Patch for DefDatabase.TryGetDef
    /// </summary>
    public static class DefDatabasePatch
    {
        /// <summary>
        /// Prefix for TryGetDef to handle short hashes
        /// </summary>
        public static bool Prefix(ref Type __1, ref ushort __2, ref Def __result)
        {
            if (__1 == null)
                return true; // Continue to original method
            
            try
            {
                // Only intercept when SymbolRegistry is initialized
                if (!SymbolRegistry.Initialized)
                    return true; // Continue to original method
                
                // Check if it's a short hash ID beyond the ushort range
                if (SymbolRegistry.HasShortHashOverride(__2))
                    {
                    // Let our registry handle it
                    __result = SymbolRegistry.GetDefByShortHash(__1, __2);
                    Diagnostics.LogVerbose($"[DefDatabasePatch] Resolved def with short hash {__2} to {__result?.defName ?? "null"}");
                    
                    return false; // Skip original method
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error in DefDatabase.TryGetDef patch: {ex}");
            }
            
            // Continue to original method for other cases
            return true;
        }
    }
} 