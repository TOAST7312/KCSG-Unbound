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
        
        // Track if we've been initialized
        public static bool Initialized { get; private set; } = false;

        // Initialize the registry
        public static void Initialize()
        {
            Log.Message("[KCSG] Initializing SymbolRegistry for unlimited symbols");
            symbolResolvers.Clear();
            Initialized = true;
        }

        // Register a symbol with its resolver
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

        // Try to resolve a symbol
        public static bool TryResolve(string symbol, ResolveParams rp)
        {
            if (string.IsNullOrEmpty(symbol) || !symbolResolvers.ContainsKey(symbol))
            {
                return false;
            }

            try
            {
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

        // Check if a symbol is registered
        public static bool IsRegistered(string symbol)
        {
            return !string.IsNullOrEmpty(symbol) && symbolResolvers.ContainsKey(symbol);
        }

        // Get count of registered symbols
        public static int RegisteredSymbolCount => symbolResolvers.Count;

        // Get all registered symbols
        public static IEnumerable<string> AllRegisteredSymbols => symbolResolvers.Keys;

        // Clear all registered symbols
        public static void Clear()
        {
            symbolResolvers.Clear();
            Log.Message("[KCSG] SymbolRegistry cleared");
        }

        // Get debug status report
        public static string GetStatusReport()
        {
            return $"KCSG SymbolRegistry: {RegisteredSymbolCount} symbols registered";
        }
    }
} 