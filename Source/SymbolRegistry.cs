using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Xml;

namespace KCSG
{
    /// <summary>
    /// Registry for unlimited KCSG symbols
    /// Extends the vanilla KCSG system to allow unlimited structure generation symbols
    /// </summary>
    public static class SymbolRegistry
    {
        // Dictionary to store all registered symbols and their resolvers
        private static Dictionary<string, Type> symbolResolvers = new Dictionary<string, Type>(4096);
        
        // Dictionary to store symbol defs by name - bypassing the 65,535 limit
        // Using a generic type parameter for better type safety
        private static Dictionary<string, object> symbolDefs = new Dictionary<string, object>(8192);
        
        // Store hash to def name mappings for quick lookups
        private static Dictionary<ushort, string> shortHashToDefName = new Dictionary<ushort, string>(8192);
        
        // Store def name to hash mappings for quick generation
        private static Dictionary<string, ushort> defNameToShortHash = new Dictionary<string, ushort>(8192);
        
        // Cache for collision resolution
        private static Dictionary<ushort, List<string>> hashCollisionCache = new Dictionary<ushort, List<string>>();
        
        // Track if we've been initialized - changed from property to field for prepatcher compatibility
        public static bool Initialized = false;
        
        // Safeguard against bad initialization
        private static bool dictInitError = false;
        
        // Performance tracking
        private static int totalRegistrationCount = 0;

        /// <summary>
        /// Initializes the symbol registry, clearing any existing registrations
        /// </summary>
        public static void Initialize()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Log.Message($"[KCSG] [{timestamp}] Initializing SymbolRegistry for unlimited symbols");
                
                // Initialize with initial capacities to avoid excessive resizing
                symbolResolvers = new Dictionary<string, Type>(4096);
                symbolDefs = new Dictionary<string, object>(8192);
                shortHashToDefName = new Dictionary<ushort, string>(8192);
                defNameToShortHash = new Dictionary<string, ushort>(8192);
                hashCollisionCache = new Dictionary<ushort, List<string>>();
                
                Initialized = true;
                dictInitError = false;
                
                // Proactively scan and register all KCSG structure layouts
                ScanAndRegisterAllStructureLayouts();
                
                // Try to synchronize with RimWorld's native resolver system if available
                SynchronizeWithNative();
                
                Log.Message($"[KCSG] [{timestamp}] SymbolRegistry initialized successfully with pre-allocated dictionaries");
            }
            catch (Exception ex)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                dictInitError = true;
                Log.Error($"[KCSG] [{timestamp}] Error initializing SymbolRegistry: {ex}");
                
                // Fallback initialization with smaller capacity
                try
                {
                    Log.Warning($"[KCSG] [{timestamp}] Attempting fallback initialization with smaller dictionaries");
                    symbolResolvers = new Dictionary<string, Type>(1024);
                    symbolDefs = new Dictionary<string, object>(1024);
                    shortHashToDefName = new Dictionary<ushort, string>(1024);
                    defNameToShortHash = new Dictionary<string, ushort>(1024);
                    hashCollisionCache = new Dictionary<ushort, List<string>>();
                    Initialized = true;
                }
                catch (Exception fallbackEx)
                {
                    Log.Error($"[KCSG] [{timestamp}] Critical error in fallback initialization: {fallbackEx}");
                    Initialized = false;
                }
            }
        }
        
        /// <summary>
        /// Scan all loaded mods for KCSG.StructureLayoutDefs and register them
        /// </summary>
        public static void ScanAndRegisterAllStructureLayouts()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Log.Message($"[KCSG] [{timestamp}] Proactively scanning for all KCSG.StructureLayoutDefs in loaded mods");
                int regCount = 0;
                
                // Get all loaded mods
                List<ModContentPack> runningMods = LoadedModManager.RunningMods.ToList();
                
                // The safer way is to look for existing defs in the system
                foreach (var def in DefDatabase<Def>.AllDefs)
                {
                    if (def.GetType().FullName.Contains("StructureLayoutDef") || 
                        def.GetType().Name.Contains("StructureLayoutDef"))
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(def.defName))
                            {
                                // Register this def with our system
                                RegisterDef(def.defName, def);
                                regCount++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[KCSG] [{timestamp}] Error registering def {def.defName}: {ex.Message}");
                        }
                    }
                }
                
                // Pre-register common KCSG structure names
                PreregisterCommonStructureNames();
                
                Log.Message($"[KCSG] [{timestamp}] Proactively registered {regCount} KCSG.StructureLayoutDefs");
            }
            catch (Exception ex)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Log.Error($"[KCSG] [{timestamp}] Error scanning for structure layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Pre-register common structure names based on prefix patterns
        /// </summary>
        private static void PreregisterCommonStructureNames()
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            // Common prefixes used by structure generation mods
            string[] commonPrefixes = new[] { 
                // Vanilla Factions Expanded - Deserters specific prefixes
                "VFED_Underfarm", "VFED_UnderfarmMain", "VFED_NewSafehaven", "VFED_AerodroneStation",
                "VFED_TechPrinter", "VFED_ShuttleStagingPost", "VFED_SupplyDepot", "VFED_ZeusCannonComplex",
                "VFED_SurveillanceStation", "VFED_ImperialConvoy",
                
                // Vanilla Factions Expanded - Mechanoids specific prefixes
                "VFEM_Carrier", "VFEM_CarrierDLC", "VFEM_Frigate", "VFEM_FrigateDLC", 
                "VFEM_Destroyer", "VFEM_DestroyerDLC", "VFEM_Cruiser", "VFEM_CruiserDLC",
                "VFEM_BroadcastingStation",
                
                // General mod prefixes
                "VFED_", "VFEA_", "VFEC_", "VFEE_", "VFEM_", "VFET_", "VFE_", "VFEI_", "FTC_", 
                "RBME_", "AG_", "BM_", "BS_", "MM_", "VC_", "VE_", "VM_"
            };
            
            int count = 0;
            
            // Pre-register common structures with letter suffixes (A-Z)
            List<string> structureBasePrefixes = new List<string>() {
                "VFED_Underfarm", "VFED_UnderfarmMain", "VFED_AerodroneStation", 
                "VFED_TechPrinter", "VFED_ShuttleStagingPost", "VFED_SupplyDepot",
                "VFED_SurveillanceStation", "VFED_ZeusCannonComplex", "VFED_ImperialConvoy"
            };
            
            foreach (var basePrefix in structureBasePrefixes) {
                for (char letter = 'A'; letter <= 'Z'; letter++) {
                    string defName = basePrefix + letter;
                    if (!IsDefRegistered(defName)) {
                        var placeholderDef = new KCSG.StructureLayoutDef {
                            defName = defName
                        };
                        RegisterDef(defName, placeholderDef);
                        count++;
                    }
                }
            }
            
            // Pre-register numbered VFEM structures (1-20)
            List<string> numberedPrefixes = new List<string>() {
                "VFEM_Carrier", "VFEM_CarrierDLC", "VFEM_Frigate", "VFEM_FrigateDLC",
                "VFEM_Destroyer", "VFEM_DestroyerDLC", "VFEM_Cruiser", "VFEM_CruiserDLC",
                "VFEM_BroadcastingStation"
            };
            
            foreach (var basePrefix in numberedPrefixes) {
                for (int i = 1; i <= 20; i++) {
                    string defName = basePrefix + i;
                    if (!IsDefRegistered(defName)) {
                        var placeholderDef = new KCSG.StructureLayoutDef {
                            defName = defName
                        };
                        RegisterDef(defName, placeholderDef);
                        count++;
                    }
                }
            }
            
            // Pre-register numbered VFED structures (1-15)
            for (int i = 1; i <= 15; i++) {
                string defName = "VFED_NewSafehaven" + i;
                if (!IsDefRegistered(defName)) {
                    var placeholderDef = new KCSG.StructureLayoutDef {
                        defName = defName
                    };
                    RegisterDef(defName, placeholderDef);
                    count++;
                }
            }
            
            // Create placeholder registrations for any existing def with these prefixes
            // This helps ensure we catch cross-references even if the real def isn't loaded yet
            foreach (var def in DefDatabase<Def>.AllDefs)
            {
                if (!string.IsNullOrEmpty(def.defName))
                {
                    foreach (var prefix in commonPrefixes)
                    {
                        if (def.defName.StartsWith(prefix))
                        {
                            // Create variants for common structure types
                            string[] variants = new[] {
                                def.defName,
                                def.defName + "Layout",
                                def.defName + "Structure",
                                "Structure_" + def.defName,
                                "Layout_" + def.defName
                            };
                            
                            foreach (var variant in variants)
                            {
                                // Only register if not already in our system
                                if (!IsDefRegistered(variant))
                                {
                                    var placeholderDef = new KCSG.StructureLayoutDef
                                    {
                                        defName = variant
                                    };
                                    
                                    RegisterDef(variant, placeholderDef);
                                    count++;
                                }
                            }
                            
                            break; // Once we've matched a prefix, no need to check others
                        }
                    }
                }
            }
            
            Log.Message($"[KCSG] [{timestamp}] Preregistered {count} placeholder structure variants");
        }
        
        /// <summary>
        /// Attempt to sync with RimWorld's native symbol resolvers
        /// </summary>
        private static void SynchronizeWithNative()
        {
            try
            {
                // Use the new RimWorldCompatibility method to sync registries
                RimWorldCompatibility.SyncRegistries();
            }
            catch (Exception ex)
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Log.Error($"[KCSG] [{timestamp}] Error synchronizing with native symbol registry: {ex}");
            }
        }

        /// <summary>
        /// Registers a symbol with its resolver type
        /// </summary>
        /// <param name="symbol">The symbol name to register</param>
        /// <param name="resolverType">The type of the resolver that handles this symbol</param>
        public static void Register(string symbol, Type resolverType)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                Log.Error("[KCSG] Attempted to register null or empty symbol");
                return;
            }

            if (resolverType == null)
            {
                Log.Error($"[KCSG] Attempted to register symbol '{symbol}' with null resolver type");
                return;
            }

            // Check if the resolver type inherits from SymbolResolver
            if (!typeof(RimWorld.BaseGen.SymbolResolver).IsAssignableFrom(resolverType))
            {
                Log.Error($"[KCSG] Type {resolverType.Name} is not a SymbolResolver");
                return;
            }

            try
            {
                // Register or replace existing registration
                if (symbolResolvers.ContainsKey(symbol))
                {
                    symbolResolvers[symbol] = resolverType;
                }
                else
                {
                    symbolResolvers.Add(symbol, resolverType);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error registering symbol '{symbol}': {ex}");
            }
        }
        
        /// <summary>
        /// Registers a SymbolDef directly with the registry
        /// </summary>
        /// <param name="defName">The name of the def to register</param>
        /// <param name="symbolDef">The actual def object to register</param>
        public static void RegisterDef(string defName, object symbolDef)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return;
            }

            if (symbolDef == null)
            {
                return;
            }
            
            // If we had initialization issues, try to reinitialize
            if (dictInitError && symbolDefs.Count == 0)
            {
                Initialize();
            }

            try
            {
                totalRegistrationCount++;
                
                // Compute short hash first to avoid unnecessary work if it fails
                ushort shortHash = CalculateShortHash(defName);
                
                // Register or replace existing registration
                if (symbolDefs.ContainsKey(defName))
                {
                    symbolDefs[defName] = symbolDef;
                }
                else
                {
                    try
                    {
                        // Add to the defs dictionary
                        symbolDefs.Add(defName, symbolDef);
                    }
                    catch (Exception ex)
                    {
                        // If we can't add to the dictionary (perhaps due to a threading issue),
                        // try to recover rather than failing completely
                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        Log.Warning($"[KCSG] [{timestamp}] Failed to add def {defName} to registry: {ex.Message}");
                        
                        // Try to update instead if it already exists
                        if (symbolDefs.ContainsKey(defName))
                        {
                            symbolDefs[defName] = symbolDef;
                        }
                        else
                        {
                            // If we still can't add it, just log and continue
                            Log.Error($"[KCSG] [{timestamp}] Could not register def {defName} after multiple attempts");
                            return;
                        }
                    }
                    
                    try
                    {
                        // Store hash mappings
                        defNameToShortHash[defName] = shortHash;
                        
                        // Handle hash collisions by maintaining a list of def names for each hash
                        if (shortHashToDefName.TryGetValue(shortHash, out string existingDefName))
                        {
                            // We have a collision, make sure we track it
                            if (!hashCollisionCache.TryGetValue(shortHash, out List<string> collisions))
                            {
                                collisions = new List<string> { existingDefName };
                                hashCollisionCache[shortHash] = collisions;
                            }
                            
                            // Add this def to the collision list if it's not the primary
                            if (!collisions.Contains(defName))
                            {
                                collisions.Add(defName);
                            }
                            
                            // Only log collisions occasionally to avoid spam
                            if (totalRegistrationCount % 1000 == 0 && Prefs.DevMode)
                            {
                                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                                Log.Warning($"[KCSG] [{timestamp}] Hash collision detected: '{defName}' and '{existingDefName}' both hash to {shortHash}");
                            }
                        }
                        else
                        {
                            // No collision, this is the primary def for this hash
                            shortHashToDefName[shortHash] = defName;
                        }
                    }
                    catch (Exception ex)
                    {
                        // If we can't update the hash mappings, log but don't fail the registration
                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        Log.Warning($"[KCSG] [{timestamp}] Failed to update hash mappings for {defName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log less frequently to avoid log spam
                if (totalRegistrationCount % 1000 == 0 || totalRegistrationCount < 100)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    Log.Error($"[KCSG] [{timestamp}] Error registering def '{defName}': {ex}");
                }
            }
        }
        
        /// <summary>
        /// Calculates a short hash (ushort) for a def name
        /// Uses the same algorithm as RimWorld for consistency
        /// </summary>
        private static ushort CalculateShortHash(string text)
        {
            if (string.IsNullOrEmpty(text)) 
                return 0;
            
            // If we already calculated this hash, return it
            ushort hash;
            if (defNameToShortHash.TryGetValue(text, out hash))
                return hash;
                
            // Calculate short hash (same algorithm as RimWorld)
            hash = 0;
            for (int i = 0; i < text.Length; i++)
            {
                hash = (ushort)((hash << 5) - hash + text[i]);
            }
            
            return hash;
        }
        
        /// <summary>
        /// Attempts to get a def name by its short hash
        /// </summary>
        public static bool TryGetDefNameByHash(ushort hash, out string defName)
        {
            // First try direct lookup - the most common case
            if (shortHashToDefName.TryGetValue(hash, out defName))
            {
                return true;
            }
            
            // If we have a collision for this hash, try to handle it
            if (hashCollisionCache.TryGetValue(hash, out List<string> collisions) && collisions.Count > 0)
            {
                // Use the first collision as the result
                defName = collisions[0];
                
                if (Prefs.DevMode)
                {
                    Log.Warning($"[KCSG] Resolving hash collision for {hash} - using '{defName}' from collision list with {collisions.Count} entries");
                }
                
                return true;
            }
            
            // No match found
            defName = null;
            return false;
        }
        
        /// <summary>
        /// Attempts to get a SymbolDef by name from the registry
        /// </summary>
        /// <param name="defName">The name of the def to retrieve</param>
        /// <param name="symbolDef">Output parameter that will contain the def if found</param>
        /// <returns>True if the def was found, false otherwise</returns>
        public static bool TryGetDef(string defName, out object symbolDef)
        {
            if (string.IsNullOrEmpty(defName))
            {
                symbolDef = null;
                return false;
            }
            
            if (symbolDefs.TryGetValue(defName, out symbolDef))
            {
                return true;
            }
            
            // If not found, try to create a placeholder def for cross-reference resolution
            // This is critical to prevent "Could not resolve cross-reference" errors
            if (defName.Contains("StructureLayout") || 
                defName.StartsWith("VFED_") || defName.StartsWith("VFEM_") ||
                // Additional specific pattern detection for VFE mods
                (defName.StartsWith("VFE") && 
                 (defName.Contains("Safehaven") || defName.Contains("Underfarm") || 
                  defName.Contains("Carrier") || defName.Contains("Frigate") || 
                  defName.Contains("Cruiser") || defName.Contains("Destroyer") ||
                  defName.Contains("Broadcasting") || defName.Contains("Station"))) ||
                defName.Contains("VFEA_") || defName.Contains("VFEC_") ||
                defName.Contains("FTC_") || defName.Contains("RBME_") || defName.StartsWith("VFE"))
            {
                // Create a placeholder def
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    symbolDef = CreatePlaceholderDef(defName);
                    // Register the placeholder to avoid creating it again
                    RegisterDef(defName, symbolDef);
                    if (Prefs.DevMode)
                    {
                        Log.Message($"[KCSG] [{timestamp}] Created placeholder def for cross-reference: {defName}");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    Log.Warning($"[KCSG] [{timestamp}] Failed to create placeholder def for {defName}: {ex.Message}");
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Creates a placeholder def for cross-reference resolution
        /// </summary>
        public static object CreatePlaceholderDef(string defName)
        {
            // Try to find the SymbolDef type
            Type symbolDefType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.FullName == "KCSG.StructureLayoutDef" || 
                            type.Name == "StructureLayoutDef" && type.Namespace == "KCSG")
                        {
                            symbolDefType = type;
                            break;
                        }
                    }
                    if (symbolDefType != null) break;
                }
                catch { /* Ignore exceptions when scanning assemblies */ }
            }
            
            if (symbolDefType == null)
            {
                // Try Def as fallback
                symbolDefType = typeof(Def);
            }
            
            // Create a new instance of the def
            object placeholderDef = Activator.CreateInstance(symbolDefType);
            
            // Set the defName property
            try
            {
                PropertyInfo defNameProperty = symbolDefType.GetProperty("defName");
                if (defNameProperty != null)
                {
                    defNameProperty.SetValue(placeholderDef, defName);
                }
            }
            catch { /* Ignore if we can't set the property */ }
            
            return placeholderDef;
        }
        
        /// <summary>
        /// Attempts to get a SymbolDef by name with specific type
        /// </summary>
        /// <typeparam name="T">The type of the def</typeparam>
        /// <param name="defName">The name of the def to retrieve</param>
        /// <param name="result">Output parameter that will contain the def if found</param>
        /// <returns>True if the def was found and of correct type, false otherwise</returns>
        public static bool TryGetDef<T>(string defName, out T result) where T : class
        {
            object obj;
            if (symbolDefs.TryGetValue(defName, out obj) && obj is T)
            {
                result = obj as T;
                return true;
            }
            result = null;
            return false;
        }

        /// <summary>
        /// Attempts to resolve a symbol using a registered resolver
        /// </summary>
        /// <param name="symbol">The symbol to resolve</param>
        /// <param name="rp">The resolve parameters to use</param>
        /// <returns>True if the symbol was resolved, false otherwise</returns>
        public static bool TryResolve(string symbol, ResolveParams rp)
        {
            // Always check initialization first
            if (!Initialized)
            {
                Initialize();
            }

            // First try using our shadow registry
            if (symbolResolvers.TryGetValue(symbol, out Type symbolResolverType))
            {
                try
                {
                    // We have this symbol, try to create and use the resolver
                    object resolver = Activator.CreateInstance(symbolResolverType);
                    
                    // Invoke the Resolve method with reflection
                    MethodInfo resolveMethod = symbolResolverType.GetMethod("Resolve", 
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    
                    if (resolveMethod != null)
                    {
                        resolveMethod.Invoke(resolver, new object[] { rp });
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    Log.Error($"[KCSG] [{timestamp}] Error resolving symbol '{symbol}' from shadow registry: {ex}");
                }
            }

            // If we failed or don't have this symbol, try native resolution as fallback
            try
            {
                return RimWorldCompatibility.TryResolveWithNative(symbol, rp);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a symbol is registered in the registry
        /// </summary>
        /// <param name="symbol">The symbol to check</param>
        /// <returns>True if the symbol is registered, false otherwise</returns>
        public static bool IsRegistered(string symbol)
        {
            return !string.IsNullOrEmpty(symbol) && symbolResolvers.ContainsKey(symbol);
        }
        
        /// <summary>
        /// Checks if a symbol def is registered in the registry
        /// </summary>
        /// <param name="defName">The def name to check</param>
        /// <returns>True if the def is registered, false otherwise</returns>
        public static bool IsDefRegistered(string defName)
        {
            return !string.IsNullOrEmpty(defName) && symbolDefs.ContainsKey(defName);
        }

        /// <summary>
        /// Gets the count of registered symbols
        /// </summary>
        public static int RegisteredSymbolCount => symbolResolvers.Count;
        
        /// <summary>
        /// Gets the count of registered symbol defs
        /// </summary>
        public static int RegisteredDefCount => symbolDefs.Count;

        /// <summary>
        /// Gets all registered symbol names
        /// </summary>
        public static IEnumerable<string> AllRegisteredSymbols => symbolResolvers.Keys;
        
        /// <summary>
        /// Gets all registered symbol def names
        /// </summary>
        public static IEnumerable<string> AllRegisteredDefNames => symbolDefs.Keys;
        
        /// <summary>
        /// Gets all registered symbol names as a list for easier iteration
        /// </summary>
        public static List<string> AllRegisteredSymbolNames
        {
            get
            {
                List<string> result = new List<string>(symbolResolvers.Count);
                foreach (string symbol in symbolResolvers.Keys)
                {
                    result.Add(symbol);
                }
                return result;
            }
        }

        /// <summary>
        /// Clears all registered symbols and defs from the registry
        /// </summary>
        public static void Clear()
        {
            symbolResolvers.Clear();
            symbolDefs.Clear();
            shortHashToDefName.Clear();
            defNameToShortHash.Clear();
            hashCollisionCache.Clear();
            
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Log.Message($"[KCSG] [{timestamp}] SymbolRegistry cleared");
        }

        /// <summary>
        /// Gets a debug status report about the registry
        /// </summary>
        /// <returns>A string containing information about the registry state</returns>
        public static string GetStatusReport()
        {
            return $"KCSG SymbolRegistry: {RegisteredSymbolCount} symbols and {RegisteredDefCount} defs registered";
        }

        /// <summary>
        /// Checks if a symbol has a registered resolver
        /// </summary>
        public static bool HasResolver(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
                return false;
                
            return symbolResolvers.ContainsKey(symbol);
        }

        /// <summary>
        /// Preload commonly referenced defs to avoid cross-reference errors
        /// </summary>
        public static List<string> PreloadCommonlyReferencedDefs()
        {
            List<string> createdDefs = new List<string>();
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            try
            {
                // List of common def names that are referenced by other mods
                string[] commonlyReferencedDefs = new[]
                {
                    // VFE Deserters (VFED) Defs
                    "VFED_SurveillanceStationF", "VFED_LargeBallroomA", "VFED_LargeBallroomB",
                    "VFED_GrandNobleThroneRoomA", "VFED_LargeNobleBedroomA", "VFED_MediumGalleryA",
                    "VFED_ShuttleLandingPadA", "VFED_ShuttleLandingPadB", "VFED_StockpileDepotA",
                    "VFED_UnderfarmA", "VFED_UnderfarmB", "VFED_UnderfarmC",
                    "VFED_AerodroneStationA", "VFED_ServantQuartersA",
                    "VFED_TechPrinterMainA", "VFED_UnderfarmMainA", "VFED_UnderfarmMainB", "VFED_UnderfarmMainC",
                    "VFED_ImperialConvoyA", "VFED_NewSafehaven1", "VFED_NewSafehaven2", "VFED_NewSafehaven3", 
                    "VFED_NewSafehaven4", "VFED_NewSafehaven5", "VFED_SupplyDepotA", "VFED_ZeusCannonComplex", 
                    "VFED_SurveillanceStation", "VFED_ImperialConvoy",
                    
                    // VFE Mechanoids (VFEM) Defs
                    "VFEM_CarrierDLC1", "VFEM_CarrierDLC2", "VFEM_CarrierDLC3", "VFEM_CarrierDLC4",
                    "VFEM_CarrierDLC5", "VFEM_CarrierDLC6", "VFEM_CarrierDLC7", "VFEM_CarrierDLC8",
                    "VFEM_CarrierDLC9", "VFEM_CarrierDLC10", "VFEM_CarrierDLC11", "VFEM_CarrierDLC12",
                    "VFEM_Frigate9", "VFEM_DestroyerDLC10", "VFEM_Frigate10", "VFEM_DestroyerDLC11",
                    "VFEM_Frigate11", "VFEM_DestroyerDLC12", "VFEM_Frigate12", "VFEM_Cruiser1",
                    "VFEM_FrigateDLC1", "VFEM_Cruiser2", "VFEM_FrigateDLC2", "VFEM_FrigateDLC3",
                    "VFEM_BroadcastingStation1", "VFEM_BroadcastingStation2", "VFEM_BroadcastingStation3",
                    "VFEM_Symbols", "VFEM_StructureDLC", "VFEM_StructureNODLC", "VFEM_StatringFactories",
                    
                    // FTC (Frontier) Defs
                    "FTC_CitadelBunkerStart", "FTC_CitadelBunkerStart_B", "FTC_CitadelBunkerStart_C", 
                    "FTC_CitadelBunkerStart_D",
                    
                    // RBME (Minotaur) Defs
                    "RBME_MinotaurTribalStart",
                    
                    // AG (Alpha Genes) Defs
                    "AG_AbandonedBiotechLabDelta", "AG_AbandonedBiotechLabAlpha"
                };
                
                // Also preregister with common suffix variants
                var expandedDefs = new List<string>(commonlyReferencedDefs.Length * 3);
                expandedDefs.AddRange(commonlyReferencedDefs);
                
                // Add suffix variants for each def
                foreach (var baseDef in commonlyReferencedDefs)
                {
                    expandedDefs.Add(baseDef + "Layout");
                    expandedDefs.Add(baseDef + "Structure");
                    expandedDefs.Add("Structure_" + baseDef);
                    expandedDefs.Add("Layout_" + baseDef);
                }
                
                // Add numbered variants for common patterns
                for (int i = 1; i <= 12; i++)
                {
                    // VFED numbered variants
                    expandedDefs.Add($"VFED_UnderfarmMain{(char)('A' + i - 1)}");
                    expandedDefs.Add($"VFED_Underfarm{(char)('A' + i - 1)}");
                    expandedDefs.Add($"VFED_NewSafehaven{i}");
                    expandedDefs.Add($"VFED_AerodroneStation{(char)('A' + i - 1)}");
                    expandedDefs.Add($"VFED_TechPrinter{(char)('A' + i - 1)}");
                    expandedDefs.Add($"VFED_ShuttleStagingPost{(char)('A' + i - 1)}");
                    expandedDefs.Add($"VFED_SupplyDepot{(char)('A' + i - 1)}");
                    
                    // VFEM numbered variants
                    expandedDefs.Add($"VFEM_Frigate{i}");
                    expandedDefs.Add($"VFEM_Destroyer{i}");
                    expandedDefs.Add($"VFEM_Cruiser{i}");
                    expandedDefs.Add($"VFEM_Carrier{i}");
                    expandedDefs.Add($"VFEM_FrigateDLC{i}");
                    expandedDefs.Add($"VFEM_DestroyerDLC{i}");
                    expandedDefs.Add($"VFEM_CruiserDLC{i}");
                    expandedDefs.Add($"VFEM_CarrierDLC{i}");
                    expandedDefs.Add($"VFEM_BroadcastingStation{i}");
                }
                
                foreach (var defName in expandedDefs)
                {
                    if (!IsDefRegistered(defName))
                    {
                        try
                        {
                            object placeholderDef = CreatePlaceholderDef(defName);
                            RegisterDef(defName, placeholderDef);
                            createdDefs.Add(defName);
                        }
                        catch (Exception ex)
                        {
                            if (Prefs.DevMode)
                                Log.Warning($"[KCSG] [{timestamp}] Could not create placeholder for {defName}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG] [{timestamp}] Error in PreloadCommonlyReferencedDefs: {ex.Message}");
            }
            
            Log.Message($"[KCSG] [{timestamp}] Preloaded {createdDefs.Count} placeholder structure definitions");
            return createdDefs;
        }
    }
} 