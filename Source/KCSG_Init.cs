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
    /// Initialization for KCSG Unbound
    /// Handles mod startup and registration of components
    /// </summary>
    [StaticConstructorOnStartup]
    public static class KCSG_Init
    {
        // Track initialization status for error handling
        private static bool initialized = false;
        
        // Static constructor runs on game start
        static KCSG_Init()
        {
            try
        {
            Log.Message("════════════════════════════════════════════════════");
            Log.Message("║ [KCSG Unbound] Initializing                      ║");
            Log.Message("║ Enhanced Structure Generation System Loading     ║");
            Log.Message("════════════════════════════════════════════════════");

                // Mark the start of initialization time for performance tracking
                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();

                // NOTE: Harmony patching is now handled earlier in KCSGUnboundMod constructor
                // This ensures patches are applied before def loading begins

            // Initialize symbol registry if not done already
            if (!SymbolRegistry.Initialized)
            {
                SymbolRegistry.Initialize();
            }
            
            // Register our symbol resolvers
            RegisterSymbolResolvers();
                
                // Register our game component for monitoring SymbolDefs
                RegisterGameComponents();
                
                // Record end of initialization time
                stopwatch.Stop();
                
                // Mark as successfully initialized
                initialized = true;

            Log.Message("════════════════════════════════════════════════════");
            Log.Message("║ [KCSG Unbound] Initialization complete           ║");
            Log.Message($"║ Registered {SymbolRegistry.RegisteredSymbolCount} symbol resolvers     ║");
                Log.Message($"║ Startup time: {stopwatch.ElapsedMilliseconds}ms            ║");
            Log.Message("════════════════════════════════════════════════════");
        }
            catch (Exception ex)
            {
                Log.Error("════════════════════════════════════════════════════");
                Log.Error("║ [KCSG Unbound] INITIALIZATION FAILED              ║");
                Log.Error("════════════════════════════════════════════════════");
                Log.Error($"[KCSG Unbound] {ex}");
                
                // Try to initialize some minimal functionality for error recovery
                TryRecoveryInitialization();
            }
        }
        
        /// <summary>
        /// Last-resort recovery initialization if main startup fails
        /// </summary>
        private static void TryRecoveryInitialization()
        {
            try
            {
                // Make sure the registry is initialized at minimum
                if (!SymbolRegistry.Initialized)
                {
                    SymbolRegistry.Initialize();
                }
                
                // Add a game component that can display errors
                RegisterGameComponents();
                
                Log.Warning("[KCSG Unbound] Recovery initialization complete - limited functionality available");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Recovery initialization also failed: {ex}");
            }
        }

        /// <summary>
        /// Register the standard symbol resolvers with safety checks
        /// </summary>
        private static void RegisterSymbolResolvers()
        {
            try
            {
                // Find RimWorld's built-in resolvers we can use if custom ones aren't available
                Type buildingResolver = typeof(RimWorld.BaseGen.SymbolResolver_SingleThing);
                
                // Note: SymbolResolver_RandomBuilding doesn't exist in RimWorld 1.5 - using another resolver as fallback
                Type randomBuildingResolver = typeof(RimWorld.BaseGen.SymbolResolver_SingleThing);
                
                // Get types from BaseGen namespace as fallbacks
                List<Type> availableResolverTypes = GetAvailableSymbolResolverTypes();
                
                // Try to register our custom resolvers, with fallbacks
                SafeRegisterResolver("Building", "SymbolResolver_Building", buildingResolver, availableResolverTypes);
                SafeRegisterResolver("RandomBuilding", "SymbolResolver_RandomBuilding", randomBuildingResolver, availableResolverTypes);
                
                // More resolvers will be added here
                
                Log.Message($"[KCSG Unbound] Registered {SymbolRegistry.RegisteredSymbolCount} symbol resolvers");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error registering symbol resolvers: {ex}");
            }
        }
        
        /// <summary>
        /// Get all available SymbolResolver types in the game
        /// </summary>
        private static List<Type> GetAvailableSymbolResolverTypes()
        {
            List<Type> types = new List<Type>();
            try
            {
                // Get all loaded assemblies
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        // Skip system assemblies
                        if (assembly.GetName().Name.StartsWith("System.") ||
                            assembly.GetName().Name == "mscorlib" ||
                            assembly.GetName().Name.StartsWith("Unity"))
                            continue;
                        
                        // Get all types in this assembly
                        foreach (Type type in assembly.GetTypes())
                        {
                            if (type.IsClass && !type.IsAbstract && 
                                typeof(RimWorld.BaseGen.SymbolResolver).IsAssignableFrom(type))
                            {
                                types.Add(type);
                            }
                        }
                    }
                    catch (Exception) { /* Ignore errors for individual assemblies */ }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error finding resolver types: {ex}");
            }
            
            return types;
        }
        
        /// <summary>
        /// Safely register a resolver with fallbacks if the primary type isn't found
        /// </summary>
        private static void SafeRegisterResolver(string symbol, string resolverName, Type fallbackType, List<Type> availableTypes)
        {
            try
            {
                // First try to find the named type in the current assembly
                Type resolverType = Type.GetType($"KCSG.{resolverName}, Assembly-CSharp");
            
                // If not found, look in all assemblies
                if (resolverType == null)
                {
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        resolverType = assembly.GetType($"KCSG.{resolverName}");
                        if (resolverType != null) break;
                    }
                }
                
                // If still not found, try to find it in our available types list
                if (resolverType == null)
                {
                    foreach (Type type in availableTypes)
                    {
                        if (type.Name.Equals(resolverName) || type.Name.EndsWith($".{resolverName}"))
                        {
                            resolverType = type;
                            break;
                        }
                    }
                }
                
                // Final fallback to the passed fallback type
                if (resolverType == null)
                {
                    Log.Warning($"[KCSG Unbound] Could not find resolver type {resolverName}, using fallback {fallbackType.Name}");
                    resolverType = fallbackType;
                }
                
                // Register the resolver
                SymbolRegistry.Register(symbol, resolverType);
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Failed to register symbol '{symbol}': {ex}");
            }
        }
        
        /// <summary>
        /// Helper method to register a resolver with better error tracking
        /// </summary>
        private static void RegisterResolver(string symbol, Type resolverType)
        {
            try
            {
                if (resolverType == null)
                {
                    Log.Error($"[KCSG Unbound] Cannot register null resolver type for symbol '{symbol}'");
                    return;
                }
                
                SymbolRegistry.Register(symbol, resolverType);
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Failed to register symbol '{symbol}': {ex}");
            }
        }

        /// <summary>
        /// Register game components for monitoring and management
        /// </summary>
        private static void RegisterGameComponents()
        {
            try
            {
                // Only register UI components in appropriate contexts
                if (RenderingDetector.NoOutputRendering)
                {
                    Log.Message("[KCSG Unbound] Not registering UI components in headless/non-rendering context");
                    return;
                }

                // Register the SymbolDefMonitor component if we have a current game
                if (Current.Game != null)
                {
                    // First check if one already exists
                    SymbolDefMonitor existing = null;
                    foreach (var comp in Current.Game.components)
                    {
                        if (comp is SymbolDefMonitor)
                        {
                            existing = comp as SymbolDefMonitor;
                            break;
                        }
                    }
                    
                    // Only add if we don't already have one
                    if (existing == null)
                    {
                        try
                        {
                            Current.Game.components.Add(new SymbolDefMonitor(Current.Game));
                            Log.Message("[KCSG Unbound] SymbolDefMonitor component added to current game");
                        }
                        catch (Exception compEx)
                        {
                            Log.Error($"[KCSG Unbound] Failed to add SymbolDefMonitor component: {compEx.Message}");
                        }
                    }
                    else
                    {
                        Log.Message("[KCSG Unbound] SymbolDefMonitor component already exists");
                    }
                }
                else
                {
                    Log.Message("[KCSG Unbound] No current game available, SymbolDefMonitor will be added when a game is loaded/created");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error registering game components: {ex}");
            }
        }

        /// <summary>
        /// Check if KCSG Unbound initialized successfully
        /// </summary>
        public static bool IsInitialized()
        {
            return initialized;
        }
    }
} 