using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using HarmonyLib;

namespace KCSG
{
    /// <summary>
    /// Handles safe initialization of the mod to prevent startup crashes
    /// </summary>
    public class SafeStart : MonoBehaviour
    {
        // Singleton instance
        private static SafeStart instance;
        
        // Track initialization state
        private bool initialized = false;
        private bool initializationAttempted = false;
        private bool harmonyInitialized = false;
        private int retryCount = 0;
        private const int MAX_RETRIES = 3;
        
        // Track any startup errors
        private List<string> startupErrors = new List<string>();
        
        // Harmony instance
        private Harmony harmony;
        
        /// <summary>
        /// Initialize the singleton on game load
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            try
            {
                // Create a persistent game object to host our MonoBehaviour
                GameObject gameObject = new GameObject("KCSG_SafeStart");
                DontDestroyOnLoad(gameObject);
                
                // Add our component
                instance = gameObject.AddComponent<SafeStart>();
                
                Log.Message("[KCSG Unbound] SafeStart initialized");
            }
            catch (Exception ex)
            {
                // This is critical - log it but don't throw
                Log.Error($"[KCSG Unbound] Critical error initializing SafeStart: {ex}");
            }
        }
        
        /// <summary>
        /// Core initialization called in a safe context
        /// </summary>
        public void Start()
        {
            try
            {
                // Schedule the actual initialization for later
                // This gives RimWorld time to fully initialize other systems
                Invoke("DelayedInitialize", 2.0f);
                
                Log.Message("[KCSG Unbound] SafeStart scheduled delayed initialization");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error in Start: {ex}");
            }
        }
        
        /// <summary>
        /// Handles initialization after a delay
        /// </summary>
        private void DelayedInitialize()
        {
            try
            {
                if (initialized || initializationAttempted)
                    return;
                
                initializationAttempted = true;
                
                // Initialize core registry first
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] SafeStart initializing registry");
                    SymbolRegistry.Initialize();
                }
                
                // Set up harmony patching
                InitializeHarmony();
                
                // Mark as initialized if successful
                initialized = harmonyInitialized && SymbolRegistry.Initialized;
                
                if (initialized)
                {
                    Log.Message("[KCSG Unbound] SafeStart successfully completed initialization");
                }
                else
                {
                    retryCount++;
                    if (retryCount < MAX_RETRIES)
                    {
                        Log.Warning($"[KCSG Unbound] SafeStart initialization incomplete, scheduling retry {retryCount}/{MAX_RETRIES}");
                        Invoke("DelayedInitialize", 2.0f);
                    }
                    else
                    {
                        Log.Error("[KCSG Unbound] SafeStart failed to initialize after multiple attempts");
                    }
                }
            }
            catch (Exception ex)
            {
                LogStartupError($"Error in DelayedInitialize: {ex}");
            }
        }
        
        /// <summary>
        /// Set up Harmony patching safely
        /// </summary>
        private void InitializeHarmony()
        {
            if (harmonyInitialized)
                return;
                
            try
            {
                // Create a unique Harmony instance
                harmony = new Harmony("kcsg.unbound.safestart");
                
                // Apply only the minimal patches needed for functionality
                ApplyMinimalPatches();
                
                harmonyInitialized = true;
                
                Log.Message("[KCSG Unbound] SafeStart initialized Harmony safely");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error initializing Harmony: {ex}");
            }
        }
        
        /// <summary>
        /// Apply only the most critical patches to keep the game running
        /// </summary>
        private void ApplyMinimalPatches()
        {
            try
            {
                // Patch DefDatabase.Add for SymbolDef
                var defDatabaseAddMethod = SafeFindMethod(typeof(DefDatabase<>), "Add");
                if (defDatabaseAddMethod != null)
                {
                    var prefixMethod = typeof(HarmonyPatches.Patch_DefDatabase_Add_SymbolDef)
                        .GetMethod("PrefixAdd", BindingFlags.Public | BindingFlags.Static);
                    
                    if (prefixMethod != null)
                    {
                        harmony.Patch(defDatabaseAddMethod, prefix: new HarmonyMethod(prefixMethod));
                        Log.Message("[KCSG Unbound] SafeStart applied DefDatabase.Add patch");
                    }
                }
                
                // Patch DefDatabase.GetByShortHash for SymbolDef
                var getByShortHashMethod = SafeFindMethod(typeof(DefDatabase<>), "GetByShortHash");
                if (getByShortHashMethod != null)
                {
                    var getByShortHashPrefix = typeof(HarmonyPatches.Patch_DefDatabase_GetByShortHash_SymbolDef)
                        .GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
                    
                    if (getByShortHashPrefix != null)
                    {
                        harmony.Patch(getByShortHashMethod, prefix: new HarmonyMethod(getByShortHashPrefix));
                        Log.Message("[KCSG Unbound] SafeStart applied GetByShortHash patch");
                    }
                }
            }
            catch (Exception ex)
            {
                LogStartupError($"Error applying minimal patches: {ex}");
            }
        }
        
        /// <summary>
        /// Safely find a method to patch
        /// </summary>
        private MethodInfo SafeFindMethod(Type genericType, string methodName)
        {
            try
            {
                foreach (var method in genericType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (method.Name == methodName)
                    {
                        return method;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                LogStartupError($"Error finding method {methodName}: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// Log a startup error safely
        /// </summary>
        private void LogStartupError(string message)
        {
            try
            {
                startupErrors.Add(message);
                Log.Error($"[KCSG Unbound] {message}");
            }
            catch
            {
                // Silently fail if even logging fails
            }
        }
        
        /// <summary>
        /// Close the application if we've hit critical errors
        /// </summary>
        private void AbortStartup()
        {
            try
            {
                Log.Error("[KCSG Unbound] CRITICAL ERROR: Aborting game startup to prevent crashes");
                Application.Quit();
            }
            catch
            {
                // Silently fail
            }
        }
    }
} 