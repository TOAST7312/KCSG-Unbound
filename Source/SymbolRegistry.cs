using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using System.Reflection;

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
        
        // Track if we've been initialized - changed from property to field for prepatcher compatibility
        public static bool Initialized = false;
        
        // Safeguard against bad initialization
        private static bool dictInitError = false;

        /// <summary>
        /// Initializes the symbol registry, clearing any existing registrations
        /// </summary>
        public static void Initialize()
        {
            try
            {
                Log.Message("[KCSG] Initializing SymbolRegistry for unlimited symbols");
                
                // Initialize with initial capacities to avoid excessive resizing
                symbolResolvers = new Dictionary<string, Type>(4096);
                symbolDefs = new Dictionary<string, object>(8192);
                shortHashToDefName = new Dictionary<ushort, string>(8192);
                defNameToShortHash = new Dictionary<string, ushort>(8192);
                
                Initialized = true;
                dictInitError = false;
                
                // Try to synchronize with RimWorld's native resolver system if available
                SynchronizeWithNative();
                
                Log.Message("[KCSG] SymbolRegistry initialized successfully with pre-allocated dictionaries");
            }
            catch (Exception ex)
            {
                dictInitError = true;
                Log.Error($"[KCSG] Error initializing SymbolRegistry: {ex}");
                
                // Fallback initialization with smaller capacity
                try
                {
                    Log.Warning("[KCSG] Attempting fallback initialization with smaller dictionaries");
                    symbolResolvers = new Dictionary<string, Type>(1024);
                    symbolDefs = new Dictionary<string, object>(1024);
                    shortHashToDefName = new Dictionary<ushort, string>(1024);
                    defNameToShortHash = new Dictionary<string, ushort>(1024);
                    Initialized = true;
                }
                catch (Exception fallbackEx)
                {
                    Log.Error($"[KCSG] Critical error in fallback initialization: {fallbackEx}");
                    Initialized = false;
                }
            }
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
                Log.Error($"[KCSG] Error synchronizing with native symbol registry: {ex}");
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
                // Compute short hash first to avoid unnecessary work if it fails
                ushort shortHash = CalculateShortHash(defName);
                
                // Register or replace existing registration
                if (symbolDefs.ContainsKey(defName))
                {
                    Log.Message($"[KCSG] Replacing existing SymbolDef registration for '{defName}'");
                    symbolDefs[defName] = symbolDef;
                }
                else
                {
                    // Add to the defs dictionary
                    symbolDefs.Add(defName, symbolDef);
                    
                    // Store hash mappings
                    if (!defNameToShortHash.ContainsKey(defName))
                    {
                        defNameToShortHash[defName] = shortHash;
                    }
                    
                    if (!shortHashToDefName.ContainsKey(shortHash))
                    {
                        shortHashToDefName[shortHash] = defName;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log less frequently to avoid log spam
                if (symbolDefs.Count % 1000 == 0 || symbolDefs.Count < 100)
                {
                    Log.Error($"[KCSG] Error registering def '{defName}': {ex}");
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
            return shortHashToDefName.TryGetValue(hash, out defName);
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
            
            return symbolDefs.TryGetValue(defName, out symbolDef);
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
                    Log.Error($"[KCSG] Error resolving symbol '{symbol}' from shadow registry: {ex}");
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
        /// Clears all registered symbols and defs from the registry
        /// </summary>
        public static void Clear()
        {
            symbolResolvers.Clear();
            symbolDefs.Clear();
            shortHashToDefName.Clear();
            defNameToShortHash.Clear();
            
            Log.Message("[KCSG] SymbolRegistry cleared");
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
    }
} 