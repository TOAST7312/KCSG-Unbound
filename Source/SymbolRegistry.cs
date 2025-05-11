using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Registry for unlimited KCSG symbols
    /// Extends the vanilla KCSG system to allow unlimited structure generation symbols
    /// </summary>
    public static class SymbolRegistry
    {
        // Dictionary to store all registered symbols and their resolvers
        private static Dictionary<string, Type> symbolResolvers = new Dictionary<string, Type>();
        
        // Dictionary to store symbol defs by name - bypassing the 65,535 limit
        // Using a generic type parameter for better type safety
        private static Dictionary<string, object> symbolDefs = new Dictionary<string, object>();
        
        // Store hash to def name mappings for quick lookups
        private static Dictionary<ushort, string> shortHashToDefName = new Dictionary<ushort, string>();
        
        // Store def name to hash mappings for quick generation
        private static Dictionary<string, ushort> defNameToShortHash = new Dictionary<string, ushort>();
        
        // Track if we've been initialized - changed from property to field for prepatcher compatibility
        public static bool Initialized = false;

        /// <summary>
        /// Initializes the symbol registry, clearing any existing registrations
        /// </summary>
        public static void Initialize()
        {
            Log.Message("[KCSG] Initializing SymbolRegistry for unlimited symbols");
            symbolResolvers.Clear();
            symbolDefs.Clear();
            shortHashToDefName.Clear();
            defNameToShortHash.Clear();
            Initialized = true;
            
            // Also clear hash cache
            // Remove references to classes that don't exist yet
            
            // Try to synchronize with RimWorld's native resolver system if available
            SynchronizeWithNative();
        }
        
        /// <summary>
        /// Attempt to sync with RimWorld's native symbol resolvers
        /// </summary>
        private static void SynchronizeWithNative()
        {
            try
            {
                // Use the compatibility layer to find and access RimWorld's native symbols
                Dictionary<string, Type> nativeResolvers = RimWorldCompatibility.GetSymbolResolvers();
                
                if (nativeResolvers != null && nativeResolvers.Count > 0)
                {
                    Log.Message($"[KCSG] Synchronizing with {nativeResolvers.Count} native symbol resolvers");
                    
                    // Copy all native resolvers to our registry for compatibility
                    foreach (var pair in nativeResolvers)
                    {
                        if (!symbolResolvers.ContainsKey(pair.Key))
                        {
                            symbolResolvers[pair.Key] = pair.Value;
                            Log.Message($"[KCSG] Imported native resolver: {pair.Key} -> {pair.Value.Name}");
                        }
                    }
                    
                    Log.Message("[KCSG] Successfully synchronized with native symbol system");
                }
                else
                {
                    Log.Message("[KCSG] No native symbol resolvers found for synchronization - running in standalone mode");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error synchronizing with native resolvers: {ex}");
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

            // Register or replace existing registration
            if (symbolResolvers.ContainsKey(symbol))
            {
                Log.Warning($"[KCSG] Replacing existing registration for symbol '{symbol}': {symbolResolvers[symbol].Name} -> {resolverType.Name}");
                symbolResolvers[symbol] = resolverType;
            }
            else
            {
                symbolResolvers.Add(symbol, resolverType);
                Log.Message($"[KCSG] Registered symbol '{symbol}' with resolver {resolverType.Name}");
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
                Log.Error("[KCSG] Attempted to register null or empty SymbolDef");
                return;
            }

            if (symbolDef == null)
            {
                Log.Error($"[KCSG] Attempted to register SymbolDef '{defName}' with null def");
                return;
            }

            try
            {
                // Register or replace existing registration
                if (symbolDefs.ContainsKey(defName))
                {
                    Log.Warning($"[KCSG] Replacing existing SymbolDef registration for '{defName}'");
                    symbolDefs[defName] = symbolDef;
                }
                else
                {
                    symbolDefs.Add(defName, symbolDef);
                    
                    // Compute and store short hash for fast lookup
                    if (!defNameToShortHash.ContainsKey(defName))
                    {
                        ushort shortHash = CalculateShortHash(defName);
                        defNameToShortHash[defName] = shortHash;
                        shortHashToDefName[shortHash] = defName;
                    }
                    
                    // Register hash directly with our local cache - no need for extra calls
                    
                    if (RegisteredDefCount % 1000 == 0)
                    {
                        Log.Message($"[KCSG] Registered {RegisteredDefCount} SymbolDefs so far");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error registering def '{defName}': {ex}");
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
            if (string.IsNullOrEmpty(symbol))
            {
                return false;
            }

            try
            {
                // First try using the native RimWorld resolver method if available
                if (RimWorldCompatibility.TryNativeResolve(symbol, rp))
                {
                    return true;
                }
                
                // If no native resolution or it failed, use our custom registry
                if (!symbolResolvers.ContainsKey(symbol))
                {
                    return false;
                }

                // Create an instance of the resolver
                RimWorld.BaseGen.SymbolResolver resolver = (RimWorld.BaseGen.SymbolResolver)Activator.CreateInstance(symbolResolvers[symbol]);
                if (resolver != null)
                {
                    resolver.Resolve(rp);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error resolving symbol '{symbol}': {ex}");
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
    }
} 