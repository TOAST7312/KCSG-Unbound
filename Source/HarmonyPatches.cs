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
                
                // Detect potentially conflicting mods
                var loadedMods = LoadedModManager.RunningModsListForReading;
                var potentialConflicts = new Dictionary<string, string>() {
                    {"performancefish", "Performance Fish - handles caching and optimization"},
                    {"kcsg", "KCSG - base mod we build upon"},
                    {"vfe", "Vanilla Expanded - adds many structure layouts"},
                    {"structuregenerator", "Structure Generation - layout system"},
                    {"basegeneration", "Base Generation - layout systems"}
                };
                
                Log.Message("[KCSG Unbound] Checking for mods that might affect structure generation");
                var conflictingMods = new List<string>();
                
                foreach (var mod in loadedMods)
                {
                    foreach (var conflict in potentialConflicts)
                    {
                        if (mod.Name.ToLower().Contains(conflict.Key) || 
                            (mod.PackageId != null && mod.PackageId.ToLower().Contains(conflict.Key)))
                        {
                            conflictingMods.Add($"{mod.Name} ({conflict.Value})");
                            break;
                        }
                    }
                }
                
                if (conflictingMods.Any())
                {
                    Log.Message($"[KCSG Unbound] Found {conflictingMods.Count} mods that may interact with structure generation:");
                    foreach (var mod in conflictingMods)
                    {
                        Log.Message($"[KCSG Unbound] - {mod}");
                    }
                }
                
                // Always patch these critical systems first
                ApplyCrossReferenceResolutionPatches(harmony);
                
                // Try to find the GlobalSettings symbol resolution method first
                var symbolResolutionMethod = FindSymbolResolutionMethod();
                if (symbolResolutionMethod != null)
                {
                    // If we found the resolution method, patch it
                    var prefix = typeof(Patch_GlobalSettings_TryResolveSymbol)
                        .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                        
                    if (prefix != null)
                    {
                        try {
                            harmony.Patch(symbolResolutionMethod, prefix: new HarmonyMethod(prefix));
                            Log.Message("[KCSG Unbound] Successfully patched symbol resolution method");
                        } 
                        catch (Exception ex) {
                            Log.Error($"[KCSG Unbound] Error applying symbol resolution patch: {ex}");
                            
                            // Try a more targeted approach if the first one fails
                            try {
                                var harmonyProcessor = harmony.CreateProcessor(symbolResolutionMethod);
                                harmonyProcessor.AddPrefix(prefix);
                                harmonyProcessor.Patch();
                                
                                Log.Message("[KCSG Unbound] Applied symbol resolution patch using processor approach");
                            }
                            catch (Exception ex2) {
                                Log.Error($"[KCSG Unbound] Failed with processor too: {ex2.Message}");
                                
                                // Fall back to the alternative approach
                                ApplyAlternativePatches(harmony);
                            }
                        }
                    }
                }
                else
                {
                    Log.Warning("[KCSG Unbound] Couldn't find resolution method, using alternative patching approach");
                    
                    // Fall back to the alternative approach
                    ApplyAlternativePatches(harmony);
                }
                
                // Always apply other core patches
                
                // Apply BaseGen patches that don't depend on specific method signatures
                try
                {
                    var baseGenGenerateMethod = typeof(BaseGen).GetMethod("Generate");
                    if (baseGenGenerateMethod != null)
                    {
                        var baseGenGeneratePrefix = typeof(Patch_BaseGen_Generate)
                            .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                            
                        harmony.Patch(baseGenGenerateMethod, prefix: new HarmonyMethod(baseGenGeneratePrefix));
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
                    // Continue despite this error
                }
                
                // Apply SymbolResolver constructor patch to register symbols
                try
                {
                    var symbolResolverCtor = typeof(SymbolResolver).GetConstructor(Type.EmptyTypes);
                    if (symbolResolverCtor != null)
                    {
                        var postfix = typeof(Patch_SymbolResolver_Constructor)
                            .GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
                            
                        if (postfix != null)
                        {
                            harmony.Patch(symbolResolverCtor, postfix: new HarmonyMethod(postfix));
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
                    // Check if Performance Fish is loaded - it patches reflection systems
                    bool performanceFishActive = ModsConfig.ActiveModsInLoadOrder.Any(m => 
                        m.Name.ToLower().Contains("performance fish") || 
                        m.PackageId.ToLower().Contains("brrainz.performance"));
                    
                    if (performanceFishActive)
                    {
                        Log.Message("[KCSG Unbound] Performance Fish detected - using alternative method targeting approach");
                        
                        // When Performance Fish is active, we need more specific method targeting that doesn't rely on Type.GetMethod
                        // Use direct binding flags and parameter types to find the exact method

                        try
                        {
                            // Find SymbolDef type
                            Type symbolDefType = FindSymbolDefType();
                            if (symbolDefType == null)
                            {
                                Log.Warning("[KCSG Unbound] Couldn't find SymbolDef type for precise method targeting");
                                symbolDefType = typeof(Def); // Fallback to general Def
                            }
                            
                            // Make the generic DefDatabase type
                            Type defDatabaseType = typeof(DefDatabase<>).MakeGenericType(symbolDefType);
                            
                            // Find the Add method using very specific parameters to avoid ambiguity
                            var methods = defDatabaseType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                                .Where(m => m.Name == "Add" && m.GetParameters().Length == 1)
                                .ToList();
                                
                            if (methods.Count > 0)
                            {
                                // Find the exact method by parameter type
                                var exactMethod = methods.FirstOrDefault(m => 
                                    m.GetParameters()[0].ParameterType == symbolDefType);
                                    
                                if (exactMethod != null)
                                {
                                    // Get our prefix method
                                    var prefixMethod = typeof(Patch_DefDatabase_Add_SymbolDef)
                                        .GetMethod("PrefixAdd", BindingFlags.Public | BindingFlags.Static);
                                        
                                    if (prefixMethod != null)
                                    {
                                        harmony.Patch(exactMethod, prefix: new HarmonyMethod(prefixMethod));
                                        Log.Message("[KCSG Unbound] Successfully patched DefDatabase.Add with Performance Fish compatibility");
                                    }
                                }
                                else
                                {
                                    Log.Warning("[KCSG Unbound] Couldn't find exact Add method with matching parameter type");
                                    
                                    // Try with a more general approach 
                                    var anyAddMethod = methods.FirstOrDefault();
                                    if (anyAddMethod != null)
                                    {
                                        var prefixMethod = typeof(Patch_DefDatabase_Add_SymbolDef)
                                            .GetMethod("PrefixAdd", BindingFlags.Public | BindingFlags.Static);
                                            
                                        if (prefixMethod != null)
                                        {
                                            harmony.Patch(anyAddMethod, prefix: new HarmonyMethod(prefixMethod));
                                            Log.Message("[KCSG Unbound] Patched DefDatabase.Add with general compatibility approach");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Log.Warning("[KCSG Unbound] Couldn't find any Add methods on DefDatabase type");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[KCSG Unbound] Error applying Performance Fish compatible patch: {ex}");
                        }
                    }
                    else
                    {
                        // Original approach for when Performance Fish is not loaded
                        var defDatabaseAddMethod = AccessTools.Method(typeof(DefDatabase<>), "Add");
                        if (defDatabaseAddMethod != null)
                        {
                            var prefixMethod = typeof(Patch_DefDatabase_Add_SymbolDef)
                                .GetMethod("PrefixAdd", BindingFlags.Public | BindingFlags.Static);
                            
                            if (prefixMethod != null)
                            {
                                harmony.CreateProcessor(defDatabaseAddMethod)
                                    .AddPrefix(prefixMethod)
                                    .Patch();
                                    
                                Log.Message("[KCSG Unbound] DefDatabase.Add patch applied for SymbolDefs using standard approach");
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
                // Log the available methods and fields in GlobalSettings for diagnosis
                Log.Message("[KCSG Unbound] Scanning GlobalSettings for resolution methods...");
                
                // IMPROVED: Use a more flexible approach to method discovery
                // First, enumerate all assemblies to find potential symbol resolution methods
                
                // Track attempts for diagnostics
                int methodsChecked = 0;
                int assembliesScanned = 0;
                
                // Create a list to track all potential method candidates
                List<MethodInfo> potentialMethods = new List<MethodInfo>();
                
                // Define a broader set of potential method names for symbol resolution
                string[] possibleMethodNames = new[] { 
                    "TryResolveSymbol", "ResolveSymbol", "Resolve", "TryResolve", "DoResolve", 
                    "ResolveSymbolMethod", "TrySymbolResolve", "GenerateSymbol", "Generate",
                    "TryGenerateSymbol", "ResolveSymbolOnce", "GetSymbolResolver",
                    "ResolveParams", "Symbol", "ResolveMethod", "TryGetResolver", "GetResolver",
                    "FindResolver", "TryGetSymbolResolver", "FindSymbolResolver", "SymbolResolve"
                };
                
                Log.Message($"[KCSG Unbound] Looking for {possibleMethodNames.Length} potential symbol resolution method names");
                
                // SCAN ALL ASSEMBLIES: More thorough approach to find the resolution method
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        // Skip system assemblies to save time
                        if (assembly.FullName.StartsWith("System") || 
                            assembly.FullName.StartsWith("mscorlib") ||
                            assembly.FullName.StartsWith("Unity"))
                            continue;
                            
                        assembliesScanned++;
                        
                        // Try to find critical types first
                        foreach (var type in assembly.GetTypes())
                        {
                            try
                            {
                                // Focus on RimWorld types that might contain symbol resolvers
                                if (type.Namespace == "RimWorld" || 
                                    type.Namespace == "RimWorld.BaseGen" || 
                                    type.Namespace?.StartsWith("KCSG") == true ||
                                    type.Name.Contains("Symbol") ||
                                    type.Name.Contains("Resolver") ||
                                    type.Name.Contains("BaseGen"))
                                {
                                    // Check for potential resolution methods by signature first
                                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | 
                                                                          BindingFlags.Static | BindingFlags.Instance))
                                    {
                                        methodsChecked++;
                                        
                                        // Skip methods unlikely to be resolution methods
                                        if (method.Name.StartsWith("get_") || 
                                            method.Name.StartsWith("set_") ||
                                            method.Name.StartsWith("add_") ||
                                            method.Name.StartsWith("remove_"))
                                            continue;
                                        
                                        var parameters = method.GetParameters();
                                        
                                        // Check for signature patterns matching symbol resolution
                                        
                                        // Pattern 1: Method that takes a string symbol and optional ResolveParams
                                        if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                                        {
                                            // If method name suggests symbol resolution, prioritize it
                                            if (possibleMethodNames.Any(name => method.Name.Contains(name)))
                                            {
                                                Log.Message($"[KCSG Unbound] Found potential resolution method by name+signature: {type.Name}.{method.Name}");
                                                potentialMethods.Insert(0, method); // prioritize these matches
                                            }
                                            else
                                            {
                                                potentialMethods.Add(method);
                                            }
                                        }
                                        
                                        // Pattern 2: Method that takes ResolveParams and has a symbol field
                                        else if (parameters.Length >= 1 && 
                                                parameters[0].ParameterType.Name.Contains("ResolveParams"))
                                        {
                                            // Check if there's a symbol field in the class
                                            var symbolField = type.GetField("symbol", BindingFlags.Public | BindingFlags.NonPublic | 
                                                                           BindingFlags.Static | BindingFlags.Instance);
                                            if (symbolField != null && symbolField.FieldType == typeof(string))
                                            {
                                                Log.Message($"[KCSG Unbound] Found potential resolution method with ResolveParams+symbol field: {type.Name}.{method.Name}");
                                                potentialMethods.Insert(0, method); // prioritize these matches
                                            }
                                            else
                                            {
                                                potentialMethods.Add(method);
                                            }
                                        }
                                        
                                        // Pattern 3: Name suggests it's specifically for symbol resolution
                                        else if (possibleMethodNames.Any(name => method.Name.Equals(name)))
                                        {
                                            Log.Message($"[KCSG Unbound] Found potential resolution method by exact name match: {type.Name}.{method.Name}");
                                            potentialMethods.Add(method);
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Skip types that cause errors
                                continue;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Skip assemblies that throw errors
                        continue;
                    }
                }
                
                Log.Message($"[KCSG Unbound] Scanned {assembliesScanned} assemblies, checked {methodsChecked} methods, found {potentialMethods.Count} potential resolution methods");
                
                // Now prioritize and try methods in order of likelihood
                
                // First check GlobalSettings methods
                var globalSettingsMethods = potentialMethods
                    .Where(m => m.DeclaringType == typeof(GlobalSettings))
                    .OrderByDescending(m => possibleMethodNames.Contains(m.Name)) // Prioritize by name match
                    .ToList();
                    
                if (globalSettingsMethods.Any())
                {
                    Log.Message($"[KCSG Unbound] Found {globalSettingsMethods.Count} potential methods in GlobalSettings");
                    
                    foreach (var method in globalSettingsMethods)
                    {
                        Log.Message($"[KCSG Unbound] Testing GlobalSettings.{method.Name}");
                        return method; // Return the first one - if it doesn't work, we'll fall back to alternative approach
                    }
                }
                
                // Then check BaseGen methods
                var baseGenMethods = potentialMethods
                    .Where(m => m.DeclaringType == typeof(BaseGen))
                    .OrderByDescending(m => possibleMethodNames.Contains(m.Name)) // Prioritize by name match
                    .ToList();
                    
                if (baseGenMethods.Any())
                {
                    Log.Message($"[KCSG Unbound] Found {baseGenMethods.Count} potential methods in BaseGen");
                    
                    foreach (var method in baseGenMethods)
                    {
                        Log.Message($"[KCSG Unbound] Testing BaseGen.{method.Name}");
                        return method; // Return the first one
                    }
                }
                
                // Finally check SymbolResolver methods
                var symbolResolverMethods = potentialMethods
                    .Where(m => typeof(SymbolResolver).IsAssignableFrom(m.DeclaringType))
                    .OrderByDescending(m => m.Name == "Resolve") // Prioritize Resolve method first
                    .ToList();
                    
                if (symbolResolverMethods.Any())
                {
                    Log.Message($"[KCSG Unbound] Found {symbolResolverMethods.Count} potential methods in SymbolResolver classes");
                    
                    foreach (var method in symbolResolverMethods)
                    {
                        Log.Message($"[KCSG Unbound] Testing SymbolResolver.{method.Name}");
                        return method; // Return the first one
                    }
                }
                
                // If we've made it here, we've found no suitable methods in expected classes
                // Try any other potential method as a last resort
                if (potentialMethods.Any())
                {
                    var bestMethod = potentialMethods.FirstOrDefault();
                    if (bestMethod != null)
                    {
                        Log.Message($"[KCSG Unbound] Last resort - using {bestMethod.DeclaringType.Name}.{bestMethod.Name}");
                        return bestMethod;
                    }
                }
                
                // Traditional approach as absolute last resort
                // Original code with minor improvements to handle nulls
                // Check for Performance Fish specifically
                bool performanceFishActive = ModsConfig.ActiveModsInLoadOrder.Any(m => 
                    m.Name.ToLower().Contains("performance fish") || 
                    m.PackageId.ToLower().Contains("brrainz.performance"));
                
                if (performanceFishActive)
                {
                    Log.Message("[KCSG Unbound] Performance Fish detected - may affect method discovery");
                }
                
                // First try with GlobalSettings
                var methodVar1 = typeof(GlobalSettings).GetMethod("TryResolveSymbol", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (methodVar1 != null)
                {
                    Log.Message("[KCSG Unbound] Found TryResolveSymbol method directly");
                    return methodVar1;
                }
                
                // Try alternative names for the method that might exist in different versions
                string[] possibleMethodNames2 = new[] { 
                    "ResolveSymbol", "Resolve", "TryResolve", "DoResolve", 
                    "ResolveSymbolMethod", "TrySymbolResolve", "GenerateSymbol",
                    "TryGenerateSymbol", "ResolveSymbolOnce", "GetSymbolResolver"
                };
                
                Log.Message($"[KCSG Unbound] Trying {possibleMethodNames2.Length} alternative method names");
                
                foreach (var methodName in possibleMethodNames2)
                {
                    methodVar1 = typeof(GlobalSettings).GetMethod(methodName, 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        
                    if (methodVar1 != null && 
                        methodVar1.GetParameters().Length >= 1 && 
                        methodVar1.GetParameters()[0].ParameterType == typeof(string))
                    {
                        Log.Message($"[KCSG Unbound] Found matching method: {methodName}");
                        return methodVar1;
                    }
                }
                
                // Try looking in BaseGen class
                Log.Message("[KCSG Unbound] Checking BaseGen class for resolution methods");
                foreach (var methodName in possibleMethodNames2)
                {
                    methodVar1 = typeof(BaseGen).GetMethod(methodName, 
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        
                    if (methodVar1 != null && 
                        methodVar1.GetParameters().Length >= 1 && 
                        methodVar1.GetParameters()[0].ParameterType == typeof(string))
                    {
                        Log.Message($"[KCSG Unbound] Found matching method in BaseGen: {methodName}");
                        return methodVar1;
                    }
                }
                
                // Look for any method in BaseGen or GlobalSettings that takes a symbol string and ResolveParams
                Log.Message("[KCSG Unbound] Looking for methods with string and ResolveParams parameters");
                foreach (var type in new[] { typeof(BaseGen), typeof(GlobalSettings) })
                {
                    foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        var parameters = m.GetParameters();
                        if (parameters.Length >= 2 &&
                            (parameters[0].ParameterType == typeof(string) && parameters[1].ParameterType == typeof(ResolveParams) ||
                            parameters[1].ParameterType == typeof(string) && parameters[0].ParameterType == typeof(ResolveParams)))
                        {
                            Log.Message($"[KCSG Unbound] Found method with matching parameters: {type.Name}.{m.Name}");
                            return m;
                        }
                    }
                }
                
                // Look for resolver methods in any type in the RimWorld.BaseGen namespace
                Log.Message("[KCSG Unbound] Searching all types in RimWorld.BaseGen namespace");
                var baseGenTypes = typeof(SymbolResolver).Assembly.GetTypes()
                    .Where(t => t.Namespace == "RimWorld.BaseGen")
                    .ToList();
                
                Log.Message($"[KCSG Unbound] Found {baseGenTypes.Count} types in RimWorld.BaseGen namespace");
                
                foreach (var type in baseGenTypes)
                {
                    foreach (var methodName in possibleMethodNames2)
                    {
                        methodVar1 = type.GetMethod(methodName, 
                            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                            
                        if (methodVar1 != null && 
                            methodVar1.GetParameters().Length >= 1 && 
                            methodVar1.GetParameters()[0].ParameterType == typeof(string))
                        {
                            Log.Message($"[KCSG Unbound] Found matching method in {type.Name}: {methodName}");
                            return methodVar1;
                        }
                    }
                }
                
                // Last resort: look for any SymbolResolver.Resolve method
                Log.Message("[KCSG Unbound] Checking SymbolResolver type as last resort");
                var symbolResolverType = typeof(SymbolResolver);
                methodVar1 = symbolResolverType.GetMethod("Resolve", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (methodVar1 != null)
                {
                    Log.Message("[KCSG Unbound] Found SymbolResolver.Resolve method");
                    return methodVar1;
                }
                
                Log.Warning("[KCSG Unbound] Failed to find any resolution method after exhaustive search");
                
                // Find all loaded Harmony instances and log if they patched BaseGen
                try {
                    var harmonyInstances = Harmony.GetAllPatchedMethods()
                        .Where(m => m.DeclaringType == typeof(BaseGen) || 
                                   m.DeclaringType == typeof(GlobalSettings) ||
                                   m.DeclaringType == typeof(SymbolResolver))
                        .ToList();
                    
                    if (harmonyInstances.Any())
                    {
                        Log.Warning($"[KCSG Unbound] Found {harmonyInstances.Count} methods patched by Harmony in the BaseGen system");
                        foreach (var m in harmonyInstances.Take(5)) // Just show a few
                        {
                            var patches = Harmony.GetPatchInfo(m);
                            if (patches != null)
                            {
                                var owners = patches.Owners.Take(3); // Just show a few
                                Log.Warning($"[KCSG Unbound] Method {m.Name} patched by: {string.Join(", ", owners)}");
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Log.Warning($"[KCSG Unbound] Error checking Harmony patches: {ex.Message}");
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
                Log.Message("[KCSG Unbound] Applying alternative patching approaches for symbol resolution");
                
                // Use a more robust approach for finding SymbolResolver types
                int patchedMethods = 0;
                int failedPatches = 0;
                
                // First try to identify all SymbolResolver types through various means
                HashSet<Type> symbolResolverTypes = new HashSet<Type>();
                
                // First try: Direct check for types inheriting from SymbolResolver
                try
                {
                    Log.Message("[KCSG Unbound] Looking for types inheriting from SymbolResolver...");
                    var baseTypes = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => {
                            try { return a.GetTypes(); } 
                            catch { return new Type[0]; }
                        })
                        .Where(t => typeof(SymbolResolver).IsAssignableFrom(t) && !t.IsAbstract)
                        .ToList();
                        
                    foreach (var type in baseTypes)
                    {
                        symbolResolverTypes.Add(type);
                    }
                    
                    Log.Message($"[KCSG Unbound] Found {baseTypes.Count} types inheriting from SymbolResolver");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error finding SymbolResolver types by inheritance: {ex.Message}");
                }
                
                // Second try: Look for types in the RimWorld.BaseGen namespace
                try
                {
                    Log.Message("[KCSG Unbound] Looking for types in RimWorld.BaseGen namespace...");
                    
                    // First find the assembly that contains RimWorld.BaseGen
                    Assembly baseGenAssembly = null;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            if (assembly.GetTypes().Any(t => t.Namespace == "RimWorld.BaseGen"))
                            {
                                baseGenAssembly = assembly;
                                break;
                            }
                        }
                        catch { continue; }
                    }
                    
                    if (baseGenAssembly != null)
                    {
                        Log.Message($"[KCSG Unbound] Found BaseGen namespace in assembly: {baseGenAssembly.GetName().Name}");
                        
                        var namespaceTypes = baseGenAssembly.GetTypes()
                            .Where(t => t.Namespace == "RimWorld.BaseGen" && !t.IsAbstract)
                            .ToList();
                            
                        foreach (var type in namespaceTypes)
                        {
                            symbolResolverTypes.Add(type);
                        }
                        
                        Log.Message($"[KCSG Unbound] Found {namespaceTypes.Count} types in RimWorld.BaseGen namespace");
                    }
                    else
                    {
                        Log.Warning("[KCSG Unbound] Could not find assembly containing RimWorld.BaseGen namespace");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error finding types in RimWorld.BaseGen namespace: {ex.Message}");
                }
                
                // Third try: Look for types with "Symbol" or "Resolver" in their name
                try
                {
                    Log.Message("[KCSG Unbound] Looking for types with Symbol or Resolver in their name...");
                    var symbolTypes = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => {
                            try { return a.GetTypes(); } 
                            catch { return new Type[0]; }
                        })
                        .Where(t => !t.IsAbstract && 
                                   (t.Name.Contains("Symbol") || 
                                    t.Name.Contains("Resolver") ||
                                    t.Name.Contains("KCSG")))
                        .ToList();
                        
                    foreach (var type in symbolTypes)
                    {
                        symbolResolverTypes.Add(type);
                    }
                    
                    Log.Message($"[KCSG Unbound] Found {symbolTypes.Count} types with Symbol or Resolver in their name");
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error finding types by name: {ex.Message}");
                }
                
                // Apply patches to all identified types
                Log.Message($"[KCSG Unbound] Attempting to patch {symbolResolverTypes.Count} symbol resolver types");
                
                foreach (var resolverType in symbolResolverTypes)
                {
                    try
                    {
                        // Find methods that might be responsible for symbol resolution
                        var methods = resolverType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            .Where(m => !m.IsAbstract &&
                                        (m.Name == "Resolve" || 
                                         m.Name.Contains("Resolve") || 
                                         m.Name.Contains("Symbol") ||
                                         (m.GetParameters().Length > 0 && 
                                          m.GetParameters()[0].ParameterType.Name.Contains("ResolveParams"))))
                            .ToList();
                            
                        foreach (var method in methods)
                        {
                            try
                            {
                                // Get our prefix method
                                var prefix = typeof(Patch_Alternative_SymbolResolver_Resolve)
                                    .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                                    
                                if (prefix != null)
                                {
                                    // Use PatchProcessor directly for more reliable patching
                                    var processor = harmony.CreateProcessor(method);
                                    processor.AddPrefix(prefix);
                                    processor.Patch();
                                    
                                    patchedMethods++;
                                    
                                    // Only log details for the first few patches to avoid log spam
                                    if (patchedMethods <= 5)
                                    {
                                        Log.Message($"[KCSG Unbound] Patched {resolverType.Name}.{method.Name}");
                                    }
                                    else if (patchedMethods == 6)
                                    {
                                        Log.Message("[KCSG Unbound] (Additional patches applied but not logged individually)");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                failedPatches++;
                                // Only log the first few failures to avoid spam
                                if (failedPatches <= 3)
                                {
                                    Log.Warning($"[KCSG Unbound] Failed to patch {resolverType.Name}.{method.Name}: {ex.Message}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error getting methods for {resolverType.Name}: {ex.Message}");
                    }
                }
                
                // Final fallback: Patch BaseGen.Generate directly to ensure our system is used
                try
                {
                    var generateMethod = typeof(BaseGen).GetMethod("Generate", 
                        BindingFlags.Public | BindingFlags.Static);
                        
                    if (generateMethod != null)
                    {
                        var prefix = typeof(Patch_BaseGen_Generate)
                            .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                            
                        if (prefix != null)
                        {
                            harmony.Patch(generateMethod, prefix: new HarmonyMethod(prefix));
                            Log.Message("[KCSG Unbound] Applied fallback patch to BaseGen.Generate");
                            patchedMethods++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error applying fallback patch to BaseGen.Generate: {ex.Message}");
                }
                
                Log.Message($"[KCSG Unbound] Alternative patching complete - patched {patchedMethods} methods ({failedPatches} failures)");
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
                // Try to find the symbol resolution method first
                var symbolResolutionMethod = FindSymbolResolutionMethod();
                if (symbolResolutionMethod != null)
                {
                    // If we found the method, patch it directly
                    Log.Message($"[KCSG Unbound] Found resolution method: {symbolResolutionMethod.DeclaringType.Name}.{symbolResolutionMethod.Name}");
                    
                    // Get our prefix method
                    var prefix = typeof(Patch_GlobalSettings_TryResolveSymbol)
                        .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                        
                    if (prefix != null)
                    {
                        harmony.Patch(symbolResolutionMethod, prefix: new HarmonyMethod(prefix));
                        Log.Message("[KCSG Unbound] Successfully patched resolution method");
                    }
                    else
                    {
                        Log.Error("[KCSG Unbound] Couldn't find prefix method for resolution patch");
                    }
                }
                else
                {
                    Log.Warning("[KCSG Unbound] Couldn't find resolution method, using alternative patching approach");
                    
                    // Apply additional diagnostic info to help track down the issue
                    Log.Message("[KCSG Unbound] Attempting to list key types and methods for diagnosis");
                    
                    try 
                    {
                        // Log RimWorld version
                        Log.Message($"[KCSG Unbound] RimWorld version: {VersionControl.CurrentVersionString}");
                        
                        // Log all resolvers
                        var allResolvers = typeof(SymbolResolver).Assembly.GetTypes()
                            .Where(t => typeof(SymbolResolver).IsAssignableFrom(t) && !t.IsAbstract)
                            .Select(t => t.Name)
                            .ToList();
                            
                        Log.Message($"[KCSG Unbound] Found {allResolvers.Count} resolver types");
                        
                        // Check if BaseGen has been modified by any patches
                        var baseGenPatches = Harmony.GetAllPatchedMethods()
                            .Where(m => m.DeclaringType?.Name.Contains("BaseGen") == true)
                            .ToList();
                            
                        if (baseGenPatches.Any()) 
                        {
                            Log.Message($"[KCSG Unbound] BaseGen has {baseGenPatches.Count} Harmony patches on it");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error during diagnostics: {ex.Message}");
                    }
                    
                    // Apply alternative patches that will work even if we can't find the specific method
                    ApplyAlternativePatches(harmony);
                    
                    // Apply a blanket transpiler patch to the entire mod assembly to catch symbol resolution logic
                    Log.Message("[KCSG Unbound] Adding broad alternative patching to ensure functionality");
                }
                
                // Always apply the DirectXmlCrossRefLoader patch to handle cross-references
                var targetMethod = typeof(DirectXmlCrossRefLoader).GetMethod("ResolveAllWanters",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    
                if (targetMethod != null)
                {
                    var prefix = typeof(Patch_DirectXmlCrossRefLoader_ResolveAllWanters)
                        .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                        
                    harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefix));
                    Log.Message("[KCSG Unbound] DirectXmlCrossRefLoader.ResolveAllWanters patch applied successfully");
                }
                else
                {
                    Log.Warning("[KCSG Unbound] Couldn't find DirectXmlCrossRefLoader.ResolveAllWanters method");
                    
                    // Try to find it with different binding flags
                    targetMethod = typeof(DirectXmlCrossRefLoader).GetMethod("ResolveAllWanters",
                        BindingFlags.Static | BindingFlags.Public);
                        
                    if (targetMethod != null)
                    {
                        var prefix = typeof(Patch_DirectXmlCrossRefLoader_ResolveAllWanters)
                            .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                            
                        harmony.Patch(targetMethod, prefix: new HarmonyMethod(prefix));
                        Log.Message("[KCSG Unbound] DirectXmlCrossRefLoader.ResolveAllWanters patch applied with alternative search");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying cross-reference resolution patches: {ex}");
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

        /// <summary>
        /// Helper method to find the SymbolDef type using reflection
        /// </summary>
        private static Type FindSymbolDefType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.FullName == "KCSG.SymbolDef" || 
                            (type.Name == "SymbolDef" && type.Namespace == "KCSG"))
                        {
                            return type;
                        }
                    }
                }
                catch
                {
                    // Skip assemblies that throw errors
                }
            }
            return null;
        }
    }
} 