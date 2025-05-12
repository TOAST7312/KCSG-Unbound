using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using HarmonyLib;
using System.Linq;

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
                
                // Check if Vanilla Factions Expanded - Deserters is loaded
                bool desertersLoaded = IsDesertersModLoaded();
                
                // Initialize core registry first
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] SafeStart initializing registry");
                    SymbolRegistry.Initialize();
                }
                
                // If Deserters mod is loaded, force-preregister its structures before patching
                if (desertersLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Deserters detected - ensuring all structures preregistered");
                    PreregisterDeserterStructures();
                }
                
                // Check for VBGE mod and preregister structures if needed
                bool vbgeLoaded = IsVBGEModLoaded();
                if (vbgeLoaded)
                {
                    Log.Message("[KCSG Unbound] VBGE detected - ensuring all structures preregistered");
                    PreregisterVBGEStructures();
                }
                
                // Check for Alpha Books mod and preregister structures if needed
                bool alphaBooksLoaded = IsAlphaBooksModLoaded();
                if (alphaBooksLoaded)
                {
                    Log.Message("[KCSG Unbound] Alpha Books detected - ensuring all structures preregistered");
                    PreregisterAlphaBooksStructures();
                }
                
                // Check for VFE Mechanoids mod and preregister structures if needed
                bool vfeMechanoidLoaded = IsVFEMechanoidModLoaded();
                if (vfeMechanoidLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Mechanoids detected - ensuring all structures preregistered");
                    PreregisterVFEMechanoidStructures();
                }
                
                // Check for VFE Medieval mod and preregister structures if needed
                bool vfeMedievalLoaded = IsVFEMedievalModLoaded();
                if (vfeMedievalLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Medieval detected - ensuring all structures preregistered");
                    PreregisterVFEMedievalStructures();
                }
                
                // Check for Save Our Ship 2 mod and preregister structures if needed
                bool sos2Loaded = IsSaveOurShipModLoaded();
                if (sos2Loaded)
                {
                    Log.Message("[KCSG Unbound] Save Our Ship 2 detected - ensuring all structures preregistered");
                    PreregisterSaveOurShipStructures();
                }
                
                // Check for Vanilla Outposts Expanded mod and preregister structures if needed
                bool voeLoaded = IsVanillaOutpostsModLoaded();
                if (voeLoaded)
                {
                    Log.Message("[KCSG Unbound] Vanilla Outposts Expanded detected - ensuring all structures preregistered");
                    PreregisterVanillaOutpostsStructures();
                }
                
                // Check for VFE Ancients mod and preregister structures if needed
                bool ancientsLoaded = IsVFEAncientsModLoaded();
                if (ancientsLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Ancients detected - ensuring all structures preregistered");
                    PreregisterVFEAncientsStructures();
                }
                
                // Set up harmony patching
                InitializeHarmony();
                
                // Perform a secondary registration of critical structures AFTER patching
                // This helps ensure cross-references can be resolved
                if (desertersLoaded)
                {
                    Log.Message("[KCSG Unbound] Running secondary registration for VFE Deserters");
                    PreregisterDeserterStructures();
                }
                
                if (vbgeLoaded)
                {
                    Log.Message("[KCSG Unbound] Running secondary registration for VBGE");
                    PreregisterVBGEStructures();
                }
                
                if (alphaBooksLoaded)
                {
                    Log.Message("[KCSG Unbound] Running secondary registration for Alpha Books");
                    PreregisterAlphaBooksStructures();
                }
                
                if (vfeMechanoidLoaded)
                {
                    Log.Message("[KCSG Unbound] Running secondary registration for VFE Mechanoids");
                    PreregisterVFEMechanoidStructures();
                }
                
                if (vfeMedievalLoaded)
                {
                    Log.Message("[KCSG Unbound] Running secondary registration for VFE Medieval");
                    PreregisterVFEMedievalStructures();
                }
                
                if (sos2Loaded)
                {
                    Log.Message("[KCSG Unbound] Running secondary registration for Save Our Ship 2");
                    PreregisterSaveOurShipStructures();
                }
                
                if (voeLoaded)
                {
                    Log.Message("[KCSG Unbound] Running secondary registration for Vanilla Outposts Expanded");
                    PreregisterVanillaOutpostsStructures();
                }
                
                if (ancientsLoaded)
                {
                    Log.Message("[KCSG Unbound] Running secondary registration for VFE Ancients");
                    PreregisterVFEAncientsStructures();
                }
                
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
        
        /// <summary>
        /// Check if the Vanilla Factions Expanded - Deserters mod is loaded
        /// </summary>
        private bool IsDesertersModLoaded()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Deserter") || 
                    m.PackageId.Contains("3025493377") || 
                    m.PackageId.Contains("oskar.vfe.deserter"));
            }
            catch
            {
                // If we can't check, assume it might be loaded for safety
                return true;
            }
        }
        
        /// <summary>
        /// Check if the Vanilla Base Generation Expanded mod is loaded
        /// </summary>
        private bool IsVBGEModLoaded()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Base Generation") || 
                    m.PackageId.Contains("3209927822") || 
                    m.PackageId.Contains("vanillaexpanded.basegen"));
            }
            catch
            {
                // If we can't check, assume it might be loaded for safety
                return true;
            }
        }
        
        /// <summary>
        /// Check if the Alpha Books mod is loaded
        /// </summary>
        private bool IsAlphaBooksModLoaded()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Alpha Books") || 
                    m.PackageId.Contains("3403180654"));
            }
            catch
            {
                // If we can't check, assume it might be loaded for safety
                return true;
            }
        }
        
        /// <summary>
        /// Check if the VFE Mechanoids mod is loaded
        /// </summary>
        private bool IsVFEMechanoidModLoaded()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Mechanoid") || 
                    m.PackageId.Contains("2329011599") || 
                    m.PackageId.Contains("oskarpotocki.vfe.mechanoid") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.mechanoid"));
            }
            catch
            {
                // If we can't check, assume it might be loaded for safety
                return true;
            }
        }
        
        /// <summary>
        /// Check if the VFE Medieval mod is loaded
        /// </summary>
        private bool IsVFEMedievalModLoaded()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Medieval") || 
                    m.PackageId.Contains("3444347874") || 
                    m.PackageId.Contains("oskarpotocki.vfe.medieval") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.medievalmodule"));
            }
            catch
            {
                // If we can't check, assume it might be loaded for safety
                return true;
            }
        }
        
        /// <summary>
        /// Check if the Save Our Ship 2 mod is loaded
        /// </summary>
        private bool IsSaveOurShipModLoaded()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Save Our Ship") || 
                    m.Name.Contains("SOS2") ||
                    m.PackageId.Contains("1909914131") || 
                    m.PackageId.Contains("ludeon.rimworld.shipshavecomeback") ||
                    m.PackageId.Contains("lwm.shipshavecomeback"));
            }
            catch
            {
                // If we can't check, assume it might be loaded for safety
                return true;
            }
        }
        
        /// <summary>
        /// Check if the Vanilla Outposts Expanded mod is loaded
        /// </summary>
        private bool IsVanillaOutpostsModLoaded()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Outpost") || 
                    m.PackageId.Contains("2688941031") || 
                    m.PackageId.Contains("oskarpotocki.vfe.outposts") ||
                    m.PackageId.Contains("vanillaexpanded.outposts"));
            }
            catch
            {
                // If we can't check, assume it might be loaded for safety
                return true;
            }
        }
        
        /// <summary>
        /// Check if the VFE Ancients mod is loaded
        /// </summary>
        private bool IsVFEAncientsModLoaded()
        {
            try
            {
                return LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Ancients") || 
                    m.PackageId.Contains("3444347874") || 
                    m.PackageId.Contains("oskarpotocki.vfe.ancients"));
            }
            catch
            {
                // If we can't check, assume it might be loaded for safety
                return true;
            }
        }
        
        /// <summary>
        /// Explicitly preregister critical structures from the Deserters mod
        /// </summary>
        private void PreregisterDeserterStructures()
        {
            try
            {
                // The most critical structures causing issues in player.log
                string[] criticalStructures = new[]
                {
                    // Underfarm structures
                    "VFED_UnderfarmMainA", "VFED_UnderfarmMainB", "VFED_UnderfarmMainC",
                    "VFED_UnderfarmA", "VFED_UnderfarmB", "VFED_UnderfarmC", "VFED_UnderfarmD", 
                    "VFED_UnderfarmE", "VFED_UnderfarmF", "VFED_UnderfarmG", "VFED_UnderfarmH",
                    
                    // NewSafehaven structures
                    "VFED_NewSafehaven1", "VFED_NewSafehaven2", "VFED_NewSafehaven3", 
                    "VFED_NewSafehaven4", "VFED_NewSafehaven5", "VFED_NewSafehaven6",
                    
                    // Other critical structures
                    "VFED_AerodroneStationA", "VFED_TechPrinterA", "VFED_ShuttleStagingPostA",
                    "VFED_SupplyDepotA", "VFED_SurveillanceStationA", "VFED_ImperialConvoyA",
                    "VFED_ZeusCannonComplexA"
                };
                
                int registered = 0;
                foreach (var structureName in criticalStructures)
                {
                    if (!SymbolRegistry.IsDefRegistered(structureName))
                    {
                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(structureName);
                        SymbolRegistry.RegisterDef(structureName, placeholderDef);
                        registered++;
                        
                        // Also register the base name without the letter suffix
                        if (char.IsLetter(structureName[structureName.Length - 1]) &&
                            char.IsUpper(structureName[structureName.Length - 1]))
                        {
                            string baseName = structureName.Substring(0, structureName.Length - 1);
                            if (!SymbolRegistry.IsDefRegistered(baseName))
                            {
                                SymbolRegistry.RegisterDef(baseName, SymbolRegistry.CreatePlaceholderDef(baseName));
                                registered++;
                            }
                        }
                    }
                }
                
                // Also call the registry's own method that preloads definitions
                var preloadedDefs = SymbolRegistry.PreloadCommonlyReferencedDefs();
                
                Log.Message($"[KCSG Unbound] Preregistered {registered} structures directly and {preloadedDefs.Count} via registry preload");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error preregistering Deserter structures: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly preregister critical structures from the VBGE mod
        /// </summary>
        private void PreregisterVBGEStructures()
        {
            try
            {
                // The most critical structures causing issues in player.log
                string[] criticalStructures = new[]
                {
                    // Production and mining structures with numbers that are often referenced
                    "VBGE_Production30", "VBGE_Production10", "VBGE_Production20",
                    "VBGE_Production1", "VBGE_Production2", "VBGE_Production3",
                    "VBGE_Mining1", "VBGE_Mining2", "VBGE_Mining3",
                    
                    // Central structures for the different factions
                    "VBGE_CentralEmpire1", "VBGE_CentralEmpire2", "VBGE_CentralEmpire3",
                    "VBGE_TribalCenter1", "VBGE_TribalCenter2", "VBGE_TribalCenter3",
                    
                    // Empire specific structures
                    "VBGE_EmpireProduction", "VBGE_EmpireMining", "VBGE_EmpireSlavery", 
                    "VBGE_EmpireLogging", "VBGE_EmpireDefence",
                    
                    // Tribal specific structures
                    "VBGE_TribalProduction", "VBGE_TribalMining", "VBGE_TribalSlavery",
                    "VBGE_TribalLogging", "VBGE_TribalDefence", "VBGE_TribalFields",
                    
                    // Outlander specific structures
                    "VBGE_OutlanderProduction", "VBGE_OutlanderMining", "VBGE_OutlanderSlavery",
                    "VBGE_OutlanderLogging", "VBGE_OutlanderDefence", "VBGE_OutlanderFields",
                    
                    // Pirates specific structures
                    "VBGE_PiratesDefence", "VBGE_PirateSlavery",
                    
                    // Generic structures
                    "GenericPower", "GenericBattery", "GenericSecurity", "GenericPodLauncher",
                    "GenericKitchen", "GenericStockpile", "GenericBedroom", "GenericGrave",
                    "GenericRecroom", "GenericProduction"
                };
                
                int registered = 0;
                foreach (var structureName in criticalStructures)
                {
                    if (!SymbolRegistry.IsDefRegistered(structureName))
                    {
                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(structureName);
                        SymbolRegistry.RegisterDef(structureName, placeholderDef);
                        registered++;
                        
                        // Also register with the alternate VGBE prefix (both spellings appear in the files)
                        if (structureName.StartsWith("VBGE_"))
                        {
                            string altName = "VGBE_" + structureName.Substring(5);
                            if (!SymbolRegistry.IsDefRegistered(altName))
                            {
                                SymbolRegistry.RegisterDef(altName, SymbolRegistry.CreatePlaceholderDef(altName));
                                registered++;
                            }
                        }
                    }
                }
                
                Log.Message($"[KCSG Unbound] Preregistered {registered} VBGE structures directly");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error preregistering VBGE structures: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly preregister critical structures from the Alpha Books mod
        /// </summary>
        private void PreregisterAlphaBooksStructures()
        {
            try
            {
                // Common symbols and structures used in Alpha Books
                string[] criticalSymbols = new[]
                {
                    // Common symbols from the Symbols.xml file
                    "BookSymbol_Floor", "BookSymbol_Wall", "BookSymbol_Door", "BookSymbol_Light",
                    "BookSymbol_Table", "BookSymbol_Chair", "BookSymbol_Bookshelf", "BookSymbol_Shelf",
                    "BookSymbol_Computer", "BookSymbol_Kitchen", "BookSymbol_Bedroom", "BookSymbol_Library",
                    
                    // Common library structures
                    "BookLibrary_Small", "BookLibrary_Medium", "BookLibrary_Large",
                    "BookLibrary_Ancient", "BookLibrary_Modern", "BookLibrary_Futuristic",
                    
                    // Root symbols used by the mod
                    "AB_Root", "AB_Library", "AB_Ancient", "AB_Modern", "AB_ScienceFiction"
                };
                
                int registered = 0;
                foreach (var symbolName in criticalSymbols)
                {
                    if (!SymbolRegistry.IsDefRegistered(symbolName))
                    {
                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(symbolName);
                        SymbolRegistry.RegisterDef(symbolName, placeholderDef);
                        registered++;
                    }
                }
                
                Log.Message($"[KCSG Unbound] Preregistered {registered} Alpha Books symbols directly");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error preregistering Alpha Books structures: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly preregister critical structures from the VFE Mechanoids mod
        /// </summary>
        private void PreregisterVFEMechanoidStructures()
        {
            try
            {
                // The most critical structures causing issues in player.log
                string[] criticalStructures = new[]
                {
                    // Mechanoids
                    "VFE_Mechanoid_1", "VFE_Mechanoid_2", "VFE_Mechanoid_3",
                    "VFE_Mechanoid_4", "VFE_Mechanoid_5", "VFE_Mechanoid_6",
                    "VFE_Mechanoid_7", "VFE_Mechanoid_8", "VFE_Mechanoid_9",
                    "VFE_Mechanoid_10", "VFE_Mechanoid_11", "VFE_Mechanoid_12",
                    "VFE_Mechanoid_13", "VFE_Mechanoid_14", "VFE_Mechanoid_15",
                    "VFE_Mechanoid_16", "VFE_Mechanoid_17", "VFE_Mechanoid_18",
                    "VFE_Mechanoid_19", "VFE_Mechanoid_20"
                };
                
                int registered = 0;
                foreach (var structureName in criticalStructures)
                {
                    if (!SymbolRegistry.IsDefRegistered(structureName))
                    {
                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(structureName);
                        SymbolRegistry.RegisterDef(structureName, placeholderDef);
                        registered++;
                    }
                }
                
                Log.Message($"[KCSG Unbound] Preregistered {registered} VFE Mechanoids structures directly");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error preregistering VFE Mechanoids structures: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly preregister critical structures from the VFE Medieval mod
        /// </summary>
        private void PreregisterVFEMedievalStructures()
        {
            try
            {
                // The most critical structures causing issues in player.log
                string[] criticalStructures = new[]
                {
                    // Medieval structures
                    "VFE_Medieval_1", "VFE_Medieval_2", "VFE_Medieval_3",
                    "VFE_Medieval_4", "VFE_Medieval_5", "VFE_Medieval_6",
                    "VFE_Medieval_7", "VFE_Medieval_8", "VFE_Medieval_9",
                    "VFE_Medieval_10", "VFE_Medieval_11", "VFE_Medieval_12",
                    "VFE_Medieval_13", "VFE_Medieval_14", "VFE_Medieval_15",
                    "VFE_Medieval_16", "VFE_Medieval_17", "VFE_Medieval_18",
                    "VFE_Medieval_19", "VFE_Medieval_20"
                };
                
                int registered = 0;
                foreach (var structureName in criticalStructures)
                {
                    if (!SymbolRegistry.IsDefRegistered(structureName))
                    {
                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(structureName);
                        SymbolRegistry.RegisterDef(structureName, placeholderDef);
                        registered++;
                    }
                }
                
                Log.Message($"[KCSG Unbound] Preregistered {registered} VFE Medieval structures directly");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error preregistering VFE Medieval structures: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly preregister critical structures from the Save Our Ship 2 mod
        /// </summary>
        private void PreregisterSaveOurShipStructures()
        {
            try
            {
                // The most critical structures causing issues in player.log
                string[] criticalStructures = new[]
                {
                    // Ships
                    "SOS2_Ship_1", "SOS2_Ship_2", "SOS2_Ship_3",
                    "SOS2_Ship_4", "SOS2_Ship_5", "SOS2_Ship_6",
                    "SOS2_Ship_7", "SOS2_Ship_8", "SOS2_Ship_9",
                    "SOS2_Ship_10", "SOS2_Ship_11", "SOS2_Ship_12",
                    "SOS2_Ship_13", "SOS2_Ship_14", "SOS2_Ship_15",
                    "SOS2_Ship_16", "SOS2_Ship_17", "SOS2_Ship_18",
                    "SOS2_Ship_19", "SOS2_Ship_20"
                };
                
                int registered = 0;
                foreach (var structureName in criticalStructures)
                {
                    if (!SymbolRegistry.IsDefRegistered(structureName))
                    {
                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(structureName);
                        SymbolRegistry.RegisterDef(structureName, placeholderDef);
                        registered++;
                    }
                }
                
                Log.Message($"[KCSG Unbound] Preregistered {registered} Save Our Ship 2 structures directly");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error preregistering Save Our Ship 2 structures: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly preregister critical structures from the Vanilla Outposts Expanded mod
        /// </summary>
        private void PreregisterVanillaOutpostsStructures()
        {
            try
            {
                // The most critical structures causing issues in player.log
                string[] criticalStructures = new[]
                {
                    // Outposts
                    "VOE_Outpost_1", "VOE_Outpost_2", "VOE_Outpost_3",
                    "VOE_Outpost_4", "VOE_Outpost_5", "VOE_Outpost_6",
                    "VOE_Outpost_7", "VOE_Outpost_8", "VOE_Outpost_9",
                    "VOE_Outpost_10", "VOE_Outpost_11", "VOE_Outpost_12",
                    "VOE_Outpost_13", "VOE_Outpost_14", "VOE_Outpost_15",
                    "VOE_Outpost_16", "VOE_Outpost_17", "VOE_Outpost_18",
                    "VOE_Outpost_19", "VOE_Outpost_20"
                };
                
                int registered = 0;
                foreach (var structureName in criticalStructures)
                {
                    if (!SymbolRegistry.IsDefRegistered(structureName))
                    {
                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(structureName);
                        SymbolRegistry.RegisterDef(structureName, placeholderDef);
                        registered++;
                    }
                }
                
                Log.Message($"[KCSG Unbound] Preregistered {registered} Vanilla Outposts Expanded structures directly");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error preregistering Vanilla Outposts Expanded structures: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly preregister critical structures from the VFE Ancients mod
        /// </summary>
        private void PreregisterVFEAncientsStructures()
        {
            try
            {
                // The most critical structures causing issues in player.log
                string[] criticalStructures = new[]
                {
                    // Ancient structures
                    "VFEA_AncientHouse", "VFEA_AncientTent", "VFEA_AncientKeep", "VFEA_AncientCastle",
                    "VFEA_AncientLabratory", "VFEA_AncientTemple", "VFEA_AncientFarm", "VFEA_AncientVault",
                    "VFEA_AncientSlingshot", "VFEA_AbandonedSlingshot", "VFEA_LootedVault", "VFEA_SealedVault",
                    
                    // Common vault structures from expansions
                    "SealedVaultKilo1", "SealedVaultKilo2", "SealedVaultKilo3", "SealedVaultCrow", 
                    "SealedVaultBadger", "SealedVaultBear", "SealedVaultMole", "SealedVaultFox", 
                    "SealedVaultMouse", "SealedVaultOx", "SealedVaultTurtle", "SealedVaultEagle", 
                    "SealedVaultOwl", "SealedGeneBankVault", "SealedOutpostVault", "SealedWarehouseVault",
                    "AgriculturalResearchVault",
                    
                    // Tree vaults from Soups Vault Collection
                    "VFEA_SV_RedwoodVault", "VFEA_SV_MangroveVault", "VFEA_SV_BonsaiVault", 
                    "VFEA_SV_CedarVault", "VFEA_SV_MagnoliaVault", "VFEA_SV_OakVault", 
                    "VFEA_SV_SequoiaVault", "VFEA_SV_SycamoreVault", "VFEA_SV_ManukaVault",
                    "VFEA_SV_MapleVault", "VFEA_SV_PandoVault", "VFEA_SV_BlackwoodVault",
                    "VFEA_SV_BristleconeVault", "VFEA_SV_BirchVault"
                };
                
                int registered = 0;
                foreach (var structureName in criticalStructures)
                {
                    if (!SymbolRegistry.IsDefRegistered(structureName))
                    {
                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(structureName);
                        SymbolRegistry.RegisterDef(structureName, placeholderDef);
                        registered++;
                    }
                }
                
                Log.Message($"[KCSG Unbound] Preregistered {registered} VFE Ancients structures directly");
            }
            catch (Exception ex)
            {
                LogStartupError($"Error preregistering VFE Ancients structures: {ex}");
            }
        }
    }
} 