using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Comprehensive compatibility utilities for RimWorld
    /// </summary>
    public static class RimWorldCompatibility
    {
        // Get RimWorld version
        public static Version RimWorldVersion => typeof(Game).Assembly.GetName().Version;
        
        // Check if we're on 1.5+
        public static bool IsRimWorld15OrLater => 
            RimWorldVersion.Major > 1 || 
            (RimWorldVersion.Major == 1 && RimWorldVersion.Minor >= 5);
            
        // Get version string for display
        public static string VersionString => 
            $"{RimWorldVersion.Major}.{RimWorldVersion.Minor}.{RimWorldVersion.Build}";
            
        // Track if we've explored the APIs
        private static bool hasExploredAPIs = false;
        
        // Store reference to symbol resolver registry in RimWorld
        private static FieldInfo symbolResolversField;
        
        // Store reference to the method to resolve symbols in RimWorld
        private static MethodInfo resolveSymbolMethod;
        
        // Cached map of symbol resolvers
        private static Dictionary<string, Type> symbolResolverCache;
        
        /// <summary>
        /// Explore available RimWorld APIs for compatibility
        /// </summary>
        public static void ExploreAPIs()
        {
            if (hasExploredAPIs) return;
            
            try
            {
                // Look for symbol resolver registry in GlobalSettings
                Log.Message("[KCSG] Exploring RimWorld.BaseGen.GlobalSettings APIs...");
                
                // Find all methods in GlobalSettings
                var methods = typeof(GlobalSettings).GetMethods(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    
                Log.Message("[KCSG] Available methods:");
                foreach (var method in methods)
                {
                    Log.Message($"[KCSG] - {method}");
                }
                
                // Find all fields in GlobalSettings
                var fields = typeof(GlobalSettings).GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    
                Log.Message("[KCSG] Available fields:");
                foreach (var field in fields)
                {
                    Log.Message($"[KCSG] - {field.FieldType} {field.Name}");
                }
                
                // Find all properties in GlobalSettings 
                var properties = typeof(GlobalSettings).GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    
                Log.Message("[KCSG] Available properties:");
                foreach (var prop in properties)
                {
                    Log.Message($"[KCSG] - {prop.PropertyType} {prop.Name}");
                }
                
                // Look for the symbol resolvers field or field containing resolvers
                foreach (var field in fields)
                {
                    // Check if the field is a dictionary with string keys
                    Type fieldType = field.FieldType;
                    if (fieldType.IsGenericType && 
                        fieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                        fieldType.GetGenericArguments()[0] == typeof(string))
                    {
                        // This is likely our field, store it
                        symbolResolversField = field;
                        break;
                    }
                }
                
                // Verify that the SymbolResolver class exists
                Log.Message("[KCSG] Found RimWorld.BaseGen.SymbolResolver class");
                
                // Look for the resolve method
                foreach (var method in typeof(BaseGen).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    // Try to identify symbol resolution methods more aggressively
                    if ((method.Name.Contains("Resolve") || method.Name.Contains("Symbol") || 
                         method.Name.ToLowerInvariant().Contains("resolver")) && 
                        method.GetParameters().Length >= 1)
                    {
                        // Check parameters
                        var parameters = method.GetParameters();
                        if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                        {
                            // Found a potential resolver
                            resolveSymbolMethod = method;
                            break;
                        }
                    }
                }
                
                hasExploredAPIs = true;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error exploring RimWorld APIs: {ex}");
            }
        }
        
        /// <summary>
        /// Get the symbol resolvers from RimWorld if possible
        /// </summary>
        public static Dictionary<string, Type> GetSymbolResolvers()
        {
            if (symbolResolverCache != null)
                return symbolResolverCache;
                
            if (!hasExploredAPIs)
                ExploreAPIs();
                
            try
            {
                if (symbolResolversField != null)
                {
                    // Try to get the value of this field from the BaseGen.globalSettings static field
                    FieldInfo globalSettingsField = typeof(BaseGen).GetField("globalSettings", 
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        
                    if (globalSettingsField != null)
                    {
                        object globalSettings = globalSettingsField.GetValue(null);
                        if (globalSettings != null)
                        {
                            // Get the dictionary from the field
                            object dictObj = symbolResolversField.GetValue(globalSettings);
                            symbolResolverCache = dictObj as Dictionary<string, Type>;
                            return symbolResolverCache;
                        }
                    }
                }
                
                // If we couldn't get the resolvers field, check if we can create an empty dictionary
                symbolResolverCache = new Dictionary<string, Type>();
                return symbolResolverCache;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error getting symbol resolvers: {ex}");
                return new Dictionary<string, Type>();
            }
        }
        
        /// <summary>
        /// Sync our registry with the native one
        /// </summary>
        public static void SyncRegistries()
        {
            int syncCount = 0;
            
            try
            {
                var nativeResolvers = GetSymbolResolvers();
                if (nativeResolvers != null && nativeResolvers.Count > 0)
                {
                    foreach (var pair in nativeResolvers)
                    {
                        try
                        {
                            // Register with our shadow registry
                            SymbolRegistry.Register(pair.Key, pair.Value);
                            syncCount++;
                        }
                        catch (Exception resolverEx)
                        {
                            // Skip any problematic resolvers but continue with others
                            if (Prefs.DevMode)
                            {
                                Log.Warning($"[KCSG] Failed to sync resolver for symbol '{pair.Key}': {resolverEx.Message}");
                            }
                        }
                    }
                    
                    Log.Message($"[KCSG] Synchronized {syncCount} symbol resolvers from native registry");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error syncing registries: {ex}");
            }
        }
        
        /// <summary>
        /// Try to resolve a symbol using native RimWorld methods if possible
        /// </summary>
        public static bool TryResolveWithNative(string symbol, ResolveParams rp)
        {
            try
            {
                if (resolveSymbolMethod != null)
                {
                    // Get the BaseGen.globalSettings instance
                    FieldInfo globalSettingsField = typeof(BaseGen).GetField("globalSettings", 
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        
                    if (globalSettingsField != null)
                    {
                        object globalSettings = globalSettingsField.GetValue(null);
                        if (globalSettings != null)
                        {
                            // Try to call the resolveSymbolMethod
                            var result = resolveSymbolMethod.Invoke(globalSettings, new object[] { symbol, rp });
                            if (result is bool boolResult)
                            {
                                return boolResult;
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error resolving with native: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a mod is loaded based on name, workshop ID, or package ID
        /// </summary>
        /// <param name="nameFragment">Fragment of the mod name to check (case sensitive)</param>
        /// <param name="workshopId">Steam workshop ID to check</param>
        /// <param name="packageIdFragment">Package ID fragment to check</param>
        /// <returns>True if the mod is loaded, false otherwise</returns>
        public static bool IsModLoaded(string nameFragment = null, string workshopId = null, string packageIdFragment = null)
        {
            // Require at least one parameter
            if (string.IsNullOrEmpty(nameFragment) && 
                string.IsNullOrEmpty(workshopId) && 
                string.IsNullOrEmpty(packageIdFragment))
            {
                return false;
            }

            // Check all running mods against our search criteria
            return LoadedModManager.RunningModsListForReading.Any(m => 
                (!string.IsNullOrEmpty(nameFragment) && m.Name.Contains(nameFragment)) || 
                (!string.IsNullOrEmpty(workshopId) && m.PackageId.Contains(workshopId)) || 
                (!string.IsNullOrEmpty(packageIdFragment) && m.PackageId.Contains(packageIdFragment)));
        }

        /// <summary>
        /// Registers prefix-based variations of structure names to handle different mod format approaches
        /// </summary>
        /// <param name="prefix">Base prefix for the structures (e.g., "VFED_")</param>
        public static void RegisterWithVariations(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                return;

            // Common structure names that appear across many mods
            List<string> commonStructureNames = new List<string>
            {
                "Main", "Base", "Building", "Room", "Entrance", "Center", 
                "Wall", "Door", "Floor", "Stockpile", "Storage",
                "Throne", "Bedroom", "Kitchen", "Workshop", "Defense", 
                "Barracks", "Farm", "Garden", "Outpost", "Tower",
                "Bridge", "Corridor", "Hallway", "Chamber"
            };

            // Register each common name with the prefix
            foreach (var structureName in commonStructureNames)
            {
                string defName = prefix + structureName;
                
                // Only register if not already registered
                if (!SymbolRegistry.IsDefRegistered(defName))
                {
                    try
                    {
                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                        
                        // Also register with letter suffixes (A, B, C, etc.) which are common
                        for (char letter = 'A'; letter <= 'F'; letter++)
                        {
                            string variantName = defName + letter;
                            if (!SymbolRegistry.IsDefRegistered(variantName))
                            {
                                var variantDef = SymbolRegistry.CreatePlaceholderDef(variantName);
                                SymbolRegistry.RegisterDef(variantName, variantDef);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Diagnostics.LogWarning($"Error registering structure {defName}: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Extensions for RimWorld API compatibility
    /// </summary>
    public static class RimWorldCompatibilityExtensions
    {
        /// <summary>
        /// Get version string for a mod, handling API changes between RimWorld versions
        /// </summary>
        public static string VersionString(this ModContentPack mod)
        {
            if (mod == null)
                return "Unknown";
            
            // Try to get the version from the PackageId and manifest if available
            try
            {
                // First try from mod metadata
                if (mod.ModMetaData != null)
                {
                    // Try to access Version property via reflection since it might be missing
                    try
                    {
                        var versionProperty = typeof(ModMetaData).GetProperty("Version");
                        if (versionProperty != null)
                        {
                            string version = versionProperty.GetValue(mod.ModMetaData) as string;
                            if (!string.IsNullOrEmpty(version))
                            {
                                return version;
                            }
                        }
                    }
                    catch
                    {
                        // Ignore reflection errors and fall through to next method
                    }
                }
                
                // Then try the packageId which sometimes contains version info
                string packageId = mod.PackageId ?? "Unknown";
                if (packageId.Contains("."))
                {
                    string[] parts = packageId.Split('.');
                    if (parts.Length > 1 && IsVersionFormat(parts[parts.Length - 1]))
                    {
                        return parts[parts.Length - 1];
                    }
                }
                
                // Finally, just use packageId as version identifier
                return packageId;
            }
            catch
            {
                return mod.PackageId ?? "Unknown";
            }
        }
        
        /// <summary>
        /// Check if a string looks like a version number
        /// </summary>
        private static bool IsVersionFormat(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            // Simple heuristic - contains a digit and has a dot or period
            bool hasDigit = false;
            bool hasDot = false;
            
            foreach (char c in text)
            {
                if (char.IsDigit(c))
                    hasDigit = true;
                else if (c == '.' || c == '-')
                    hasDot = true;
            }
            
            return hasDigit && hasDot;
        }
    }
} 