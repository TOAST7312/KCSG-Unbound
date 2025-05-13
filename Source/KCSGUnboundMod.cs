using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Xml;

namespace KCSG
{
    /// <summary>
    /// Simple placeholder def class when other methods fail
    /// </summary>
    public class BasicPlaceholderDef : Def
    {
        // Already inherits defName from Def base class
        // This class exists just to provide a reliable fallback
    }

    /// <summary>
    /// Main mod class for KCSG Unbound.
    /// Sets up basic mod structure but defers risky operations to SafeStart.
    /// </summary>
    public class KCSGUnboundMod : Mod
    {
        // Store our Harmony instance
        private static Harmony harmony;
        
        // Track initialization success
        private static bool initializationSuccess = false;
        
        // For performance monitoring
        private static System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        
        // For status tracking
        public static string CurrentStatus = "Not initialized";
        
        public KCSGUnboundMod(ModContentPack content) : base(content)
        {
            stopwatch.Start();
            
            try 
            {
                // Start diagnostics first
                Diagnostics.Initialize();
                Diagnostics.LogDiagnostic("KCSGUnboundMod constructor called");
                
                // Just a single message instead of the banner
                Log.Message("[KCSG Unbound] Starting initialization");
                
                CurrentStatus = "Initializing...";
                
                // Set thread priority higher for loading
                try
                {
                    System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.AboveNormal;
                }
                catch {}
                
                // Initialize Harmony and registry immediately
                SetupEarlyRegistry();
                
                // Apply patches in precise order
                SetupHarmony();
                
                // Instead of loading everything upfront, defer most loading to when it's actually needed
                // This significantly reduces startup memory pressure
                LongEventHandler.ExecuteWhenFinished(() => {
                    try {
                        // Preload is now deferred until the game has fully loaded
                        // This avoids competing for resources during critical startup
                        Log.Message("[KCSG Unbound] Deferring structure preloading to post-game initialization");
                        
                        // Schedule the actual loading with a slight delay to ensure all other mods are fully loaded
                        // This helps prevent issues where we load before dependencies
                        ScheduleDelayedLoading();
                        
                        initializationSuccess = true;
                    }
                    catch (Exception ex) {
                        Log.Error($"[KCSG Unbound] Error scheduling deferred loading: {ex}");
                    }
                });
                
                stopwatch.Stop();
                CurrentStatus = $"Initial setup complete ({stopwatch.ElapsedMilliseconds}ms)";
                
                Log.Message($"[KCSG Unbound] Initial setup complete in {stopwatch.ElapsedMilliseconds}ms, full loading deferred");
                Diagnostics.LogDiagnostic("Early initialization complete, full loading deferred");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error during mod initialization: {ex}");
                Diagnostics.LogDiagnostic($"Error during initialization: {ex}");
                CurrentStatus = "Error during initialization";
            }
        }
        
        /// <summary>
        /// Schedule the delayed loading of structure layouts
        /// </summary>
        private void ScheduleDelayedLoading()
        {
            // Only proceed if we're actually going to enter a game
            // This prevents wasting resources in the main menu
            if (Current.Game == null)
            {
                // Set up an event handler to detect when a game is started
                LongEventHandler.QueueLongEvent(() => {
                    Log.Message("[KCSG Unbound] Game detected, performing full structure loading");
                    
                    // Now load all the structure layouts we need
                    PerformFullLoading();
                }, "KCSG_Structures_Loading", false, null);
                
                return;
            }
            
            // If we already have a game, schedule loading after a short delay
            // This allows other mods to finish their initialization first
            LongEventHandler.QueueLongEvent(() => {
                Log.Message("[KCSG Unbound] Performing full structure loading");
                
                // Now load all the structure layouts we need
                PerformFullLoading();
            }, "KCSG_Structures_Loading", false, null);
        }
        
        /// <summary>
        /// Perform the actual loading of all structure layouts
        /// </summary>
        private void PerformFullLoading()
        {
            try
            {
                Log.Message("[KCSG Unbound] Performing full loading sequence");
                
                // Load module registrations
                InitializeVFEDesertersLayouts();
                InitializeVBGELayouts();
                InitializeAlphaBooksLayouts();
                InitializeVFEMechanoidLayouts();
                InitializeVFEMedievalLayouts();
                InitializeSaveOurShip2Layouts();
                InitializeVanillaOutpostsLayouts();
                InitializeVFEAncientsLayouts();
                InitializeVFEInsectoidsLayouts();
                InitializeVFEClassicalLayouts();
                InitializeVFEEmpireLayouts();
                InitializeReinforcedMechanoidsLayouts();
                InitializeDeadMansSwitchLayouts();
                InitializeMechanitorEncountersLayouts();
                
                // Explicitly register prefixes from any active mods (even if they don't have a specific initializer)
                PreregisterPrefixesFromActiveMods();
                
                // Save the cache to disk
                try {
                    SymbolRegistryCache.SaveCache();
                    Log.Message("[KCSG Unbound] Saving registry cache to disk after full loading");
                }
                catch (Exception saveEx) {
                    Log.Warning($"[KCSG Unbound] Error saving cache after full loading: {saveEx.Message}");
                }
                
                // Register successful initialization
                initializationSuccess = true;
                
                // Stop and report timing
                stopwatch.Stop();
                Log.Message($"[KCSG Unbound] Full loading sequence completed in {stopwatch.ElapsedMilliseconds}ms");
                CurrentStatus = "Initialized successfully";
            }
            catch (Exception ex)
            {
                // Report error
                Log.Error($"[KCSG Unbound] Error during full loading: {ex}");
                CurrentStatus = "Initialization failed with error";
            }
        }
        
        /// <summary>
        /// Pre-register prefixes from all active mods based on their names
        /// </summary>
        private void PreregisterPrefixesFromActiveMods()
        {
            try
            {
                Log.Message("[KCSG Unbound] Pre-registering prefixes from active mods");
                int prefixesRegistered = 0;
                
                foreach (var mod in LoadedModManager.RunningModsListForReading)
                {
                    try 
                    {
                        // Skip mods we've already processed in other initializers
                        if (string.IsNullOrEmpty(mod.PackageId))
                            continue;
                            
                        // Try to generate potential prefixes from this mod
                        List<string> prefixes = new List<string>();
                        
                        // 1. Acronym from mod name (e.g., "Amazing Cool Mod" -> "ACM_")
                        string modName = mod.Name;
                        if (!string.IsNullOrEmpty(modName))
                        {
                            string acronym = string.Join("", modName.Split(' ')
                                .Where(s => !string.IsNullOrEmpty(s))
                                .Select(s => char.ToUpper(s[0])));
                                
                            if (acronym.Length >= 2)
                            {
                                prefixes.Add(acronym + "_");
                            }
                        }
                        
                        // 2. First part of packageId (e.g., "author.coolmod" -> "author_")
                        string packageId = mod.PackageId;
                        if (!string.IsNullOrEmpty(packageId) && packageId.Contains('.'))
                        {
                            string authorTag = packageId.Split('.')[0];
                            if (!string.IsNullOrEmpty(authorTag))
                            {
                                prefixes.Add(authorTag.ToUpperInvariant() + "_");
                            }
                        }
                        
                        // 3. Sanitized mod name prefix
                        if (!string.IsNullOrEmpty(modName))
                        {
                            // Take first word of mod name
                            string firstWord = modName.Split(' ').FirstOrDefault();
                            if (!string.IsNullOrEmpty(firstWord) && firstWord.Length >= 2)
                            {
                                prefixes.Add(firstWord.ToUpperInvariant() + "_");
                            }
                        }
                        
                        // Register variants for each detected prefix
                        foreach (var prefix in prefixes)
                        {
                            RegisterWithVariations(prefix);
                            prefixesRegistered++;
                        }
                    }
                    catch (Exception modEx)
                    {
                        // Just log and continue - we don't want one bad mod to stop others
                        Log.Warning($"[KCSG Unbound] Error processing prefixes for mod {mod.Name}: {modEx.Message}");
                    }
                }
                
                Log.Message($"[KCSG Unbound] Pre-registered {prefixesRegistered} prefixes from active mods");
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Error pre-registering mod prefixes: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load only the structures for mods that are actually loaded
        /// </summary>
        private void LoadModSpecificStructures()
        {
            // Load Vanilla Factions Expanded - Deserters layouts if needed
            if (IsModLoaded("Deserter", "3025493377", "oskar.vfe.deserter"))
            {
                InitializeVFEDesertersLayouts();
            }
            
            // Load Vanilla Base Generation Expanded layouts if needed
            if (IsModLoaded("Base Generation", "3209927822", "vanillaexpanded.basegen"))
            {
                InitializeVBGELayouts();
            }
            
            // Load Alpha Books layouts if needed
            if (IsModLoaded("Alpha Books", "3403180654", ""))
            {
                InitializeAlphaBooksLayouts();
            }
            
            // Load VFE Mechanoids layouts if needed
            if (IsModLoaded("Mechanoid", "2329011599", "oskarpotocki.vfe.mechanoid"))
            {
                InitializeVFEMechanoidLayouts();
            }
            
            // Load VFE Medieval layouts if needed
            if (IsModLoaded("Medieval", "3444347874", "oskarpotocki.vfe.medieval"))
            {
                InitializeVFEMedievalLayouts();
            }
            
            // Load Save Our Ship 2 layouts if needed
            if (IsModLoaded("Save Our Ship", "1909914131", "ludeon.rimworld.shipshavecomeback"))
            {
                InitializeSaveOurShip2Layouts();
            }
            
            // Load Vanilla Outposts Expanded layouts if needed
            if (IsModLoaded("Outpost", "2688941031", "oskarpotocki.vfe.outposts"))
            {
                InitializeVanillaOutpostsLayouts();
            }
            
            // Load VFE Ancients layouts if needed
            if (IsModLoaded("Ancients", "2654846754", "oskarpotocki.vfe.ancients"))
            {
                InitializeVFEAncientsLayouts();
            }
            
            // Load VFE Insectoids layouts if needed
            if (IsModLoaded("Insectoid", "1938063127", "oskarpotocki.vfe.insectoids"))
            {
                InitializeVFEInsectoidsLayouts();
            }
            
            // Load VFE Classical layouts if needed
            if (IsModLoaded("Classical", "1221070409", "oskarpotocki.vfe.classical"))
            {
                InitializeVFEClassicalLayouts();
            }
            
            // Load VFE Empire layouts if needed
            if (IsModLoaded("Empire", "1967385037", "oskarpotocki.vfe.empire"))
            {
                InitializeVFEEmpireLayouts();
            }
            
            // Load Dead Man's Switch layouts if needed
            if (IsModLoaded("Dead Man's Switch", "3469398006", "aoba.deadmanswitch"))
            {
                InitializeDeadMansSwitchLayouts();
            }
            
            // Load Mechanitor Encounters layouts if needed
            if (IsModLoaded("Mechanitor Encounters", "3417287863", ""))
            {
                InitializeMechanitorEncountersLayouts();
            }
        }
        
        /// <summary>
        /// Helper method to check if a specific mod is loaded
        /// </summary>
        private bool IsModLoaded(string nameFragment, string workshopId, string packageIdFragment)
        {
            return RimWorldCompatibility.IsModLoaded(nameFragment, workshopId, packageIdFragment);
        }
        
        /// <summary>
        /// Perform early registry setup to intercept symbol loading
        /// </summary>
        private void SetupEarlyRegistry()
        {
            try
            {
                // Create our Harmony instance
                harmony = new Harmony("KCSG.Unbound");
                
                // Initialize registry immediately
                if (!SymbolRegistry.Initialized)
                {
                    SymbolRegistry.Initialize();
                    Log.Message("[KCSG Unbound] Registry initialized");
                }
                
                // Also initialize cache
                try
                {
                    SymbolRegistryCache.Initialize();
                    Log.Message("[KCSG Unbound] Cache system initialized");
                    
                    // IMPORTANT: Pre-register common structures BEFORE scanning to ensure we have something
                    PreloadStructureLayouts();
                    
                    // Force create and save a basic cache with the preloaded data
                    try
                    {
                        SymbolRegistryCache.SaveCache();
                        Log.Message("[KCSG Unbound] Initial registry cache saved to disk");
                    }
                    catch (Exception saveEx)
                    {
                        Log.Warning($"[KCSG Unbound] Error saving initial cache: {saveEx.Message}");
                    }
                    
                    // Now force a scan of at least Core and this mod's files
                    try 
                    {
                        // Make sure our own mod gets scanned first
                        var ourMod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => 
                            m.Name.Contains("KCSG") || 
                            m.PackageId.Contains("KCSG") ||
                            m.PackageId.Contains("unbound"));
                        
                        if (ourMod != null)
                        {
                            // Force scan this mod and save results regardless of success
                            Log.Message("[KCSG Unbound] Force scanning this mod for structures");
                            SymbolRegistryCache.AddModToScanQueue(ourMod.PackageId, true);
                        }
                        
                        // Also scan the Core mod for any structures
                        var coreMod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => 
                            m.Name == "Core" || 
                            m.PackageId == "ludeon.rimworld");
                        
                        if (coreMod != null)
                        {
                            Log.Message("[KCSG Unbound] Force scanning Core mod for structures");
                            SymbolRegistryCache.AddModToScanQueue(coreMod.PackageId, true);
                        }
                        
                        // Get a list of VE mods that might have structures
                        var veMods = LoadedModManager.RunningModsListForReading.Where(m => 
                            m.Name.Contains("Vanilla Expanded") || 
                            m.PackageId.Contains("vanillaexpanded") ||
                            m.PackageId.Contains("oskar.vfe") ||
                            m.PackageId.Contains("oskarpotocki.vfe"));
                        
                        foreach (var veMod in veMods.Take(3)) // Limit to first 3 to avoid overloading
                        {
                            Log.Message($"[KCSG Unbound] Force scanning VE mod: {veMod.Name}");
                            SymbolRegistryCache.AddModToScanQueue(veMod.PackageId, true);
                        }
                    }
                    catch (Exception scanEx)
                    {
                        Log.Warning($"[KCSG Unbound] Error during priority mod scanning: {scanEx.Message}");
                    }
                }
                catch (Exception cacheEx)
                {
                    Log.Warning($"[KCSG Unbound] Error initializing cache system: {cacheEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error in early registry setup: {ex}");
            }
        }
        
        /// <summary>
        /// Preload structure layouts to avoid issues during def resolution
        /// </summary>
        private void PreloadStructureLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Pre-registering common layout names...");
                
                // Common prefixes that appear in cross-references
                string[] commonPrefixes = new[] { 
                    "VFED_", "VFEA_", "VFEC_", "VFEE_", "VFEM_", "VFET_", "VFE_", "VFEI_", "FTC_", 
                    "RBME_", "AG_", "BM_", "BS_", "MM_", "VC_", "VE_", "VM_", "VBGE_", "RM_",
                    "DMS_", "DMSAC_", "SEX_", "FT_"
                };
                
                // Get common base names from the log file cross-references
                string[] commonBaseNames = new[] { 
                    "CitadelBunkerStart", "LargeBallroomA", "SurveillanceStationF", "ServantQuartersA",
                    "GrandNobleThroneRoomA", "LargeNobleBedroomA", "TechPrinterMainA", "UnderfarmMainA",
                    "ShuttleLandingPadA", "AerodroneStationA", "ImperialConvoyA", "StockpileDepotA",
                    // Add more common structure names
                    "Structure", "Layout", "Camp", "Base", "Outpost", "Settlement", "Tower",
                    "Bunker", "Fortress", "Castle", "Wall", "Room", "Building", "House",
                    "Factory", "Farm", "Barracks", "Storage", "PowerPlant", "Laboratory",
                    "Kitchen", "Bedroom", "Stockpile", "Workshop", "DefensivePosition"
                };
                
                // Count how many structures were registered
                int registeredCount = 0;
                
                // Create permutations of base names and prefixes
                foreach (var prefix in commonPrefixes) 
                {
                    foreach (var baseName in commonBaseNames)
                    {
                        string defName = prefix + baseName;
                        
                        if (!SymbolRegistry.IsDefRegistered(defName))
                        {
                            try 
                            {
                                var placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                SymbolRegistry.RegisterDef(defName, placeholderDef);
                                registeredCount++;
                            }
                            catch (Exception ex) {
                                Diagnostics.LogDiagnostic($"Error registering {defName}: {ex.Message}");
                            }
                            
                            // Also register variants with suffixes
                            foreach (var suffix in new[] { "_A", "_B", "_C", "_D", "Alpha", "Beta", "Gamma", "Delta" })
                            {
                                string variantName = prefix + baseName + suffix;
                                try 
                                {
                                    if (!SymbolRegistry.IsDefRegistered(variantName))
                                    {
                                        var placeholderDef = SymbolRegistry.CreatePlaceholderDef(variantName);
                                        SymbolRegistry.RegisterDef(variantName, placeholderDef);
                                        registeredCount++;
                                    }
                                }
                                catch (Exception ex) {
                                    Diagnostics.LogDiagnostic($"Error registering variant {variantName}: {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    // Also register common numbered variants for each prefix
                    for (int i = 1; i <= 10; i++)
                    {
                        string numDefName = $"{prefix}Structure{i}";
                        try
                        {
                            if (!SymbolRegistry.IsDefRegistered(numDefName))
                            {
                                var placeholderDef = SymbolRegistry.CreatePlaceholderDef(numDefName);
                                SymbolRegistry.RegisterDef(numDefName, placeholderDef);
                                registeredCount++;
                            }
                        }
                        catch (Exception ex) {
                            Diagnostics.LogDiagnostic($"Error registering numbered {numDefName}: {ex.Message}");
                        }
                    }
                }
                
                // Directly force register some basic layout names without prefixes as a last resort
                string[] basicLayouts = new[] {
                    "BaseStructure", "SimpleLayout", "BasicBase", "StandardCamp", "DefaultOutpost",
                    "GenericHouse", "StandardBuilding", "BasicRoom", "SimpleStructure", "DefaultLayout"
                };
                
                foreach (var layout in basicLayouts)
                {
                    try
                    {
                        if (!SymbolRegistry.IsDefRegistered(layout))
                        {
                            var placeholderDef = SymbolRegistry.CreatePlaceholderDef(layout);
                            SymbolRegistry.RegisterDef(layout, placeholderDef);
                            registeredCount++;
                        }
                    }
                    catch (Exception ex) {
                        Diagnostics.LogDiagnostic($"Error registering basic layout {layout}: {ex.Message}");
                    }
                }
                
                Log.Message($"[KCSG Unbound] Pre-registered {registeredCount} placeholder layout names (total defs: {SymbolRegistry.RegisteredDefCount})");
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Error preloading structure layouts: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set up Harmony patches immediately in the constructor
        /// </summary>
        private void SetupHarmony()
        {
            try
            {
                Diagnostics.LogVerbose("Setting up Harmony patches");
                
                // Apply patches directly
                HarmonyPatches.ApplyPatches(harmony);
                
                Diagnostics.LogVerbose("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error applying Harmony patches: {ex}");
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG Unbound] HARMONY INITIALIZATION FAILED    ║");
                Log.Message("════════════════════════════════════════════════════");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers Vanilla Factions Expanded - Deserters layouts
        /// </summary>
        private static void InitializeVFEDesertersLayouts()
        {
            try
            {
                Diagnostics.LogVerbose("Implementing enhanced registration for VFE Deserters layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Diagnostics.LogVerbose("Initializing SymbolRegistry for VFE Deserters layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VFE-DESERTERS MOD BEING LOADED
                bool desertersModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Deserter") || 
                    m.PackageId.Contains("3025493377") || 
                    m.PackageId.Contains("oskar.vfe.deserter"));
                    
                if (desertersModLoaded)
                {
                    Diagnostics.LogVerbose("VFE Deserters mod is loaded - implementing comprehensive structure registration");
                    
                    // COMPREHENSIVE LIST OF ALL KNOWN VFED STRUCTURES
                    List<string> criticalBaseNames = new List<string>
                    {
                        // Main structures from Deserters mod
                        "Underfarm", "UnderfarmMain", "NewSafehaven", "AerodroneStation",
                        "TechPrinter", "ShuttleStagingPost", "SupplyDepot", "ZeusCannonComplex",
                        "SurveillanceStation", "ImperialConvoy",
                        
                        // Noble structures
                        "LargeNobleBallroom", "LargeNobleBedroom", "LargeNobleGallery",
                        "LargeNobleThroneRoom", "MediumNobleBallroom", "MediumNobleBedroom",
                        "MediumNobleGallery", "MediumNobleThroneRoom", "SmallNobleBedroom",
                        "SmallNobleThroneRoom", "GrandNobleThroneRoom",
                        
                        // Plot structures
                        "Bunker", "Courtyard", "Gardens", "KontarionEmplacement",
                        "OnagerEmplacement", "PalintoneEmplacement", "ServantQuarters",
                        "ShuttleLandingPad", "StockpileDepot"
                    };
                    
                    // Track the defs we create
                    int count = 0;
                    HashSet<string> registeredNames = new HashSet<string>();
                    
                    // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                    foreach (string baseName in criticalBaseNames)
                    {
                        // PRIMARY NAMING: VFED_BaseName
                        string primaryDefName = $"VFED_{baseName}";
                        
                        // NAMING PATTERNS - These are all the formats that could be used for references
                        List<string> allNamingPatterns = new List<string>();
                        
                        // 1. Standard pattern with and without prefix
                        allNamingPatterns.Add(primaryDefName);
                        allNamingPatterns.Add(baseName);
                        
                        // 2. Letter suffixes (A-Z) - standard variation for most structures
                        for (char letter = 'A'; letter <= 'Z'; letter++)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{letter}");
                        }
                        
                        // 3. Numbered variants for specific structures like Safehaven
                        if (baseName == "NewSafehaven")
                        {
                            for (int i = 1; i <= 20; i++)
                            {
                                allNamingPatterns.Add($"{primaryDefName}{i}");
                            }
                        }
                        
                        // 4. Common suffix variations
                        string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex" };
                        foreach (string suffix in suffixes)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{suffix}");
                            allNamingPatterns.Add($"{baseName}{suffix}");
                        }
                        
                        // 5. Alternative prefixing styles that some mods might use
                        string[] prefixingStyles = new[] 
                        {
                            $"Structure_VFED_{baseName}", 
                            $"Layout_VFED_{baseName}",
                            $"StructureLayout_VFED_{baseName}",
                            $"VFED_Structure_{baseName}",
                            $"VFED_Layout_{baseName}",
                            $"VFED.{baseName}"
                        };
                        
                        foreach (string style in prefixingStyles)
                        {
                            allNamingPatterns.Add(style);
                        }
                        
                        // ADD ALL THE NAMING VARIANTS USING MULTIPLE FALLBACK METHODS
                        foreach (string defName in allNamingPatterns)
                        {
                            try
                            {
                                // Skip if already registered
                                if (SymbolRegistry.IsDefRegistered(defName) || registeredNames.Contains(defName))
                                    continue;
                                
                                // TIER 1: Try with the standard method first
                                try
                                {
                                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                    SymbolRegistry.RegisterDef(defName, placeholderDef);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch 
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 2: Try direct creation of KCSG.StructureLayoutDef
                                try
                                {
                                    // Look for the KCSG.StructureLayoutDef type
                                    Type structLayoutType = AppDomain.CurrentDomain.GetAssemblies()
                                        .SelectMany(a => {
                                            try { return a.GetTypes(); } 
                                            catch { return new Type[0]; }
                                        })
                                        .FirstOrDefault(t => t.FullName == "KCSG.StructureLayoutDef" || 
                                                           (t.Name == "StructureLayoutDef" && t.Namespace == "KCSG"));
                                    
                                    if (structLayoutType != null)
                                    {
                                        object placeholderDef = Activator.CreateInstance(structLayoutType);
                                        PropertyInfo defNameProp = structLayoutType.GetProperty("defName");
                                        defNameProp?.SetValue(placeholderDef, defName);
                                        
                                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                                        count++;
                                        registeredNames.Add(defName);
                                        continue; // Success, move to next name
                                    }
                                }
                                catch
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 3: Try with the BasicPlaceholderDef (defined specifically for this purpose)
                                try
                                {
                                    var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                    SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 4: Absolute last resort - use Dictionary object as a placeholder
                                try
                                {
                                    var dictPlaceholder = new Dictionary<string, string> { { "defName", defName } };
                                    SymbolRegistry.RegisterDef(defName, dictPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                }
                                catch (Exception ex)
                                {
                                    // Log the failure but don't throw - we want to continue with other names
                                    Log.Warning($"[KCSG Unbound] All methods failed for {defName}: {ex.Message}");
                                }
                            }
                            catch
                            {
                                // Completely ignore any outer exceptions - must not fail
                            }
                        }
                    }
                    
                    // SCAN XML FILES DIRECTLY - this finds actual structures defined in XML
                    // regardless of whether they follow the expected naming patterns
                    try
                    {
                        // Scan XML files in the mod folder to find all structure layouts
                        string desertersModFolder = null;
                        
                        // Check common paths where VFE Deserters might be installed
                        string[] possiblePaths = new[]
                        {
                            "Mods/Vanilla Factions Expanded - Deserters",
                            "Mods/VFE - Deserters",
                            "Mods/oskarpotocki.vfe.deserters",
                            "Mods/3025493377", // Steam Workshop ID
                            "294100/3025493377", // Workshop folder format
                            Path.Combine(GenFilePaths.ModsFolderPath, "Vanilla Factions Expanded - Deserters"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "VFE - Deserters"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "oskarpotocki.vfe.deserters"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "3025493377")
                        };
                        
                        foreach (var path in possiblePaths)
                        {
                            if (Directory.Exists(path))
                            {
                                desertersModFolder = path;
                                break;
                            }
                        }
                        
                        if (desertersModFolder != null)
                        {
                            Log.Message($"[KCSG Unbound] Found VFE Deserters mod folder at {desertersModFolder}");
                            
                            // RECURSIVELY scan all XML files in the mod folder for structure layouts
                            foreach (var xmlFile in Directory.GetFiles(desertersModFolder, "*.xml", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    // Use streaming XML reader instead of loading the entire file into memory
                                    using (XmlReader reader = XmlReader.Create(xmlFile))
                                    {
                                        // We're looking for defName elements
                                        while (reader.Read())
                                        {
                                            if (reader.NodeType == XmlNodeType.Element && reader.Name == "defName")
                                            {
                                                string defName = reader.ReadElementContentAsString();
                                                
                                                // Check if this is a VFED structure by name pattern
                                                if (defName.StartsWith("VFED_") && !registeredNames.Contains(defName))
                                                {
                                                    // Try to create a placeholder with multiple methods
                                                    try
                                                    {
                                                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                                                        count++;
                                                        registeredNames.Add(defName);
                                                    }
                                                    catch
                                                    {
                                                        // Fallback to basic placeholder
                                                        try
                                                        {
                                                            var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                                            SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                                            count++;
                                                            registeredNames.Add(defName);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            Log.Warning($"[KCSG Unbound] Failed to register {defName} from XML: {ex.Message}");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning($"[KCSG Unbound] Error processing {xmlFile}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            Log.Message("[KCSG Unbound] VFE Deserters mod folder not found directly - relying on preregistered templates only");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                    }
                    
                    // Finally, make sure any missing layouts can be properly looked up by their short hash
                    try
                    {
                        foreach (string defName in registeredNames)
                        {
                            if (!string.IsNullOrEmpty(defName))
                            {
                                ushort hash = CalculateShortHash(defName);
                                // Remove individual logging for each hash calculation
                                // Log.Message($"[KCSG Unbound] Registered {defName} with hash {hash}");
                            }
                        }
                        
                        // Add a summary log instead
                        if (registeredNames.Count > 0)
                        {
                            Log.Message($"[KCSG Unbound] Calculated hashes for {registeredNames.Count} structure definitions");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error calculating short hashes: {ex.Message}");
                    }
                    
                    Log.Message($"[KCSG Unbound] Registered {count} critical VFE Deserters layouts using enhanced methods");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] VFE Deserters mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VFE Deserters layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Calculates the short hash code used by RimWorld for def cross-references
        /// This matches how RimWorld DefDatabase calculates short hashes
        /// </summary>
        private static ushort CalculateShortHash(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            
            int num = 0;
            int num2 = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                num = ((num << 5) - num) + c;
                if (c == '_')
                {
                    num2 = i + 1;
                }
            }
            
            if (num2 > 0 && num2 < text.Length)
            {
                for (int j = num2; j < text.Length; j++)
                {
                    num2 = ((num2 << 5) - num2) + text[j];
                }
                return (ushort)(num2 & 65535);
            }
            else
            {
                return (ushort)(num & 65535);
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers Vanilla Base Generation Expanded layouts
        /// </summary>
        private static void InitializeVBGELayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for VBGE layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for VBGE layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VBGE MOD BEING LOADED
                bool vbgeModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Base Generation") || 
                    m.PackageId.Contains("3209927822") || 
                    m.PackageId.Contains("vanillaexpanded.basegen"));
                    
                if (vbgeModLoaded)
                {
                    Log.Message("[KCSG Unbound] VBGE mod is loaded - implementing comprehensive structure registration");
                    
                    // COMPREHENSIVE LIST OF ALL KNOWN VBGE STRUCTURES
                    List<string> criticalBaseNames = new List<string>
                    {
                        // Main structure types for different factions
                        "CentralEmpire", "Empire", "Tribal", "Outlander", "Pirates",
                        
                        // Structure categories
                        "Production", "Mining", "Slavery", "Logging", "Defence", "Fields",
                        
                        // Specific combinations
                        "EmpireProduction", "EmpireMining", "EmpireSlavery", "EmpireLogging", "EmpireDefence",
                        "TribalProduction", "TribalMining", "TribalSlavery", "TribalLogging", "TribalDefence", "TribalFields",
                        "OutlanderProduction", "OutlanderMining", "OutlanderSlavery", "OutlanderLogging", "OutlanderDefence", "OutlanderFields",
                        "PiratesDefence", "PirateSlavery",
                        
                        // Generic structure types
                        "GenericPower", "GenericBattery", "GenericSecurity", "GenericPodLauncher",
                        "GenericKitchen", "GenericStockpile", "GenericBedroom", "GenericGrave",
                        "GenericRecroom", "GenericProduction"
                    };
                    
                    // Track the defs we create
                    int count = 0;
                    HashSet<string> registeredNames = new HashSet<string>();
                    
                    // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                    foreach (string baseName in criticalBaseNames)
                    {
                        // PRIMARY NAMING: VBGE_BaseName and VGBE_BaseName (both appear in the files)
                        string primaryDefName = $"VBGE_{baseName}";
                        string alternateDefName = $"VGBE_{baseName}";
                        
                        // NAMING PATTERNS - These are all the formats that could be used for references
                        List<string> allNamingPatterns = new List<string>();
                        
                        // 1. Standard patterns with and without prefix
                        allNamingPatterns.Add(primaryDefName);
                        allNamingPatterns.Add(alternateDefName);
                        allNamingPatterns.Add(baseName);
                        
                        // 2. Numbered variants (1-30) for production and other structures
                        for (int i = 1; i <= 30; i++)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{i}");
                            allNamingPatterns.Add($"{alternateDefName}{i}");
                        }
                        
                        // 3. Letter suffixes (A-Z) - standard variation for most structures
                        for (char letter = 'A'; letter <= 'Z'; letter++)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{letter}");
                            allNamingPatterns.Add($"{alternateDefName}{letter}");
                        }
                        
                        // 4. Common suffix variations
                        string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex" };
                        foreach (string suffix in suffixes)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{suffix}");
                            allNamingPatterns.Add($"{alternateDefName}{suffix}");
                            allNamingPatterns.Add($"{baseName}{suffix}");
                        }
                        
                        // 5. Alternative prefixing styles that some mods might use
                        string[] prefixingStyles = new[] 
                        {
                            $"Structure_VBGE_{baseName}", 
                            $"Layout_VBGE_{baseName}",
                            $"StructureLayout_VBGE_{baseName}",
                            $"VBGE_Structure_{baseName}",
                            $"VBGE_Layout_{baseName}",
                            $"VBGE.{baseName}",
                            $"Structure_VGBE_{baseName}", 
                            $"Layout_VGBE_{baseName}",
                            $"StructureLayout_VGBE_{baseName}",
                            $"VGBE_Structure_{baseName}",
                            $"VGBE_Layout_{baseName}",
                            $"VGBE.{baseName}"
                        };
                        
                        foreach (string style in prefixingStyles)
                        {
                            allNamingPatterns.Add(style);
                        }
                        
                        // ADD ALL THE NAMING VARIANTS USING MULTIPLE FALLBACK METHODS
                        foreach (string defName in allNamingPatterns)
                        {
                            try
                            {
                                // Skip if already registered
                                if (SymbolRegistry.IsDefRegistered(defName) || registeredNames.Contains(defName))
                                    continue;
                                
                                // TIER 1: Try with the standard method first
                                try
                                {
                                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                    SymbolRegistry.RegisterDef(defName, placeholderDef);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch 
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 2: Try direct creation of KCSG.StructureLayoutDef
                                try
                                {
                                    // Look for the KCSG.StructureLayoutDef type
                                    Type structLayoutType = AppDomain.CurrentDomain.GetAssemblies()
                                        .SelectMany(a => {
                                            try { return a.GetTypes(); } 
                                            catch { return new Type[0]; }
                                        })
                                        .FirstOrDefault(t => t.FullName == "KCSG.StructureLayoutDef" || 
                                                           (t.Name == "StructureLayoutDef" && t.Namespace == "KCSG"));
                                    
                                    if (structLayoutType != null)
                                    {
                                        object placeholderDef = Activator.CreateInstance(structLayoutType);
                                        PropertyInfo defNameProp = structLayoutType.GetProperty("defName");
                                        defNameProp?.SetValue(placeholderDef, defName);
                                        
                                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                                        count++;
                                        registeredNames.Add(defName);
                                        continue; // Success, move to next name
                                    }
                                }
                                catch
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 3: Try with the BasicPlaceholderDef (defined specifically for this purpose)
                                try
                                {
                                    var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                    SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 4: Absolute last resort - use Dictionary object as a placeholder
                                try
                                {
                                    var dictPlaceholder = new Dictionary<string, string> { { "defName", defName } };
                                    SymbolRegistry.RegisterDef(defName, dictPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                }
                                catch (Exception ex)
                                {
                                    // Log the failure but don't throw - we want to continue with other names
                                    Log.Warning($"[KCSG Unbound] All methods failed for {defName}: {ex.Message}");
                                }
                            }
                            catch
                            {
                                // Completely ignore any outer exceptions - must not fail
                            }
                        }
                    }
                    
                    // SCAN XML FILES DIRECTLY - this finds actual structures defined in XML
                    // regardless of whether they follow the expected naming patterns
                    try
                    {
                        // Scan XML files in the mod folder to find all structure layouts
                        string vbgeModFolder = null;
                        
                        // Check common paths where VBGE might be installed
                        string[] possiblePaths = new[]
                        {
                            "Mods/Vanilla Base Generation Expanded",
                            "Mods/VanillaBaseGenerationExpanded",
                            "Mods/VBGE",
                            "Mods/vanillaexpanded.basegen",
                            "Mods/3209927822", // Steam Workshop ID
                            "294100/3209927822", // Workshop folder format
                            Path.Combine(GenFilePaths.ModsFolderPath, "Vanilla Base Generation Expanded"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "VanillaBaseGenerationExpanded"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "VBGE"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "vanillaexpanded.basegen"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "3209927822")
                        };
                        
                        foreach (var path in possiblePaths)
                        {
                            if (Directory.Exists(path))
                            {
                                vbgeModFolder = path;
                                break;
                            }
                        }
                        
                        if (vbgeModFolder != null)
                        {
                            Log.Message($"[KCSG Unbound] Found VBGE mod folder at {vbgeModFolder}");
                            
                            // RECURSIVELY scan all XML files in the mod folder for structure layouts
                            foreach (var xmlFile in Directory.GetFiles(vbgeModFolder, "*.xml", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    // Use streaming XML reader to minimize memory usage
                                    using (var fileStream = new FileStream(xmlFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                    using (var reader = XmlReader.Create(fileStream, new XmlReaderSettings 
                                    { 
                                        IgnoreWhitespace = true,
                                        IgnoreComments = true,
                                        IgnoreProcessingInstructions = true,
                                        CloseInput = true  // Ensure resources are properly disposed
                                    }))
                                    {
                                        // We're looking for defName elements
                                        while (reader.Read())
                                        {
                                            if (reader.NodeType == XmlNodeType.Element && reader.Name == "defName")
                                            {
                                                string defName = reader.ReadElementContentAsString();
                                                
                                                // Check if this is a VBGE structure by name pattern
                                                if ((defName.StartsWith("VBGE_") || defName.StartsWith("VGBE_")) && !registeredNames.Contains(defName))
                                                {
                                                    // Try to create a placeholder with multiple methods
                                                    try
                                                    {
                                                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                                                        count++;
                                                        registeredNames.Add(defName);
                                                    }
                                                    catch
                                                    {
                                                        // Fallback to basic placeholder
                                                        try
                                                        {
                                                            var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                                            SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                                            count++;
                                                            registeredNames.Add(defName);
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            Log.Warning($"[KCSG Unbound] Failed to register {defName} from XML: {ex.Message}");
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning($"[KCSG Unbound] Error processing {xmlFile}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            Log.Message("[KCSG Unbound] VBGE mod folder not found directly - relying on preregistered templates only");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                    }
                    
                    Log.Message($"[KCSG Unbound] Registered {count} critical VBGE layouts using enhanced methods");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] VBGE mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VBGE layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers Alpha Books layouts
        /// </summary>
        private static void InitializeAlphaBooksLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for Alpha Books layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for Alpha Books layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR ALPHA BOOKS MOD BEING LOADED
                bool alphaBooksModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Alpha Books") || 
                    m.PackageId.Contains("3403180654"));
                    
                if (alphaBooksModLoaded)
                {
                    Log.Message("[KCSG Unbound] Alpha Books mod is loaded - implementing comprehensive structure registration");
                
                    // COMPREHENSIVE LIST OF ALL KNOWN ALPHA BOOKS STRUCTURES AND SYMBOLS
                    List<string> criticalBaseNames = new List<string>
                    {
                        // Structure and library types
                        "BookSymbol", "Library", "BookLibrary", "BookStore", "BookCase", "Bookshelf",
                        "BookPile", "BookStack", "BookPlinth", "Books", "AncientLibrary", "ModernLibrary",
                        "FuturisticLibrary", "SmallLibrary", "MediumLibrary", "LargeLibrary", "HugeLibrary",
                        "BookDesk", "ReadingRoom", "StudyRoom", "PrivateLibrary", "PublicLibrary",
                        
                        // Prefixed symbols used by the mod
                        "ABooks", "AB_Root", "AB_Ancient", "AB_Modern", "AB_Medieval", "AB_Library",
                        "AB_ScienceFiction", "AB_BookShop", "AB_BookStore", "AB_Shelf", "AB_Desk",
                        "AB_Display", "AB_Reading", "AB_Study", "AB_Catalogue", "AB_Floor", "AB_Wall",
                        "AB_Door", "AB_Light", "AB_Table", "AB_Chair",
                        
                        // Symbol components
                        "BookSymbol_Floor", "BookSymbol_Wall", "BookSymbol_Door", "BookSymbol_Light",
                        "BookSymbol_Table", "BookSymbol_Chair", "BookSymbol_Bookshelf", "BookSymbol_Shelf",
                        "BookSymbol_Computer", "BookSymbol_Kitchen", "BookSymbol_Bedroom", "BookSymbol_Library"
                    };
                    
                    // Track the defs we create
                    int count = 0;
                    HashSet<string> registeredNames = new HashSet<string>();
                    
                    // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                    foreach (string baseName in criticalBaseNames)
                    {
                        // NAMING PATTERNS - These are all the formats that could be used for references
                        List<string> allNamingPatterns = new List<string>();
                        
                        // Primary names with common prefixes
                        allNamingPatterns.Add(baseName);
                        allNamingPatterns.Add($"ABooks_{baseName}");
                        allNamingPatterns.Add($"AB_{baseName}");
                        allNamingPatterns.Add($"AlphaBooks_{baseName}");
                        
                        // Numbered variants (1-10)
                        for (int i = 1; i <= 10; i++)
                        {
                            allNamingPatterns.Add($"{baseName}{i}");
                            allNamingPatterns.Add($"ABooks_{baseName}{i}");
                            allNamingPatterns.Add($"AB_{baseName}{i}");
                            allNamingPatterns.Add($"AlphaBooks_{baseName}{i}");
                        }
                        
                        // Size variants
                        string[] sizes = new[] { "Small", "Medium", "Large", "Huge", "Tiny" };
                        foreach (string size in sizes)
                        {
                            allNamingPatterns.Add($"{baseName}_{size}");
                            allNamingPatterns.Add($"{size}{baseName}");
                            allNamingPatterns.Add($"ABooks_{baseName}_{size}");
                            allNamingPatterns.Add($"ABooks_{size}{baseName}");
                            allNamingPatterns.Add($"AB_{baseName}_{size}");
                            allNamingPatterns.Add($"AB_{size}{baseName}");
                        }
                        
                        // Style variants
                        string[] styles = new[] { "Ancient", "Medieval", "Modern", "Future", "ScienceFiction", "Industrial" };
                        foreach (string style in styles)
                        {
                            allNamingPatterns.Add($"{baseName}_{style}");
                            allNamingPatterns.Add($"{style}{baseName}");
                            allNamingPatterns.Add($"ABooks_{baseName}_{style}");
                            allNamingPatterns.Add($"ABooks_{style}{baseName}");
                            allNamingPatterns.Add($"AB_{baseName}_{style}");
                            allNamingPatterns.Add($"AB_{style}{baseName}");
                        }
                        
                        // ADD ALL THE NAMING VARIANTS USING MULTIPLE FALLBACK METHODS
                        foreach (string defName in allNamingPatterns)
                        {
                            try
                            {
                                // Skip if already registered
                                if (SymbolRegistry.IsDefRegistered(defName) || registeredNames.Contains(defName))
                                    continue;
                                
                                // TIER 1: Try with the standard method first
                                try
                                {
                                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                    SymbolRegistry.RegisterDef(defName, placeholderDef);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch 
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 2: Try with the BasicPlaceholderDef (defined specifically for this purpose)
                                try
                                {
                                    var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                    SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 3: Absolute last resort - use Dictionary object as a placeholder
                                try
                                {
                                    var dictPlaceholder = new Dictionary<string, string> { { "defName", defName } };
                                    SymbolRegistry.RegisterDef(defName, dictPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                }
                                catch (Exception ex)
                                {
                                    // Log the failure but don't throw - we want to continue with other names
                                    Log.Warning($"[KCSG Unbound] All methods failed for {defName}: {ex.Message}");
                                }
                            }
                            catch
                            {
                                // Completely ignore any outer exceptions - must not fail
                            }
                        }
                    }
                    
                    // SCAN XML FILES DIRECTLY - this finds actual structures defined in XML
                    try
                    {
                        // Scan XML files in the mod folder to find all structure layouts
                        string alphaBooksModFolder = null;
                        
                        // Check common paths where Alpha Books might be installed
                        string[] possiblePaths = new[]
                        {
                            "Mods/Alpha Books",
                            "Mods/AlphaBooks",
                            "Mods/3403180654", // Steam Workshop ID
                            Path.Combine(GenFilePaths.ModsFolderPath, "Alpha Books"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "AlphaBooks"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "3403180654")
                        };
                        
                        foreach (var path in possiblePaths)
                        {
                            if (Directory.Exists(path))
                            {
                                alphaBooksModFolder = path;
                                break;
                            }
                        }
                        
                        if (alphaBooksModFolder != null)
                        {
                            Log.Message($"[KCSG Unbound] Found Alpha Books mod folder at {alphaBooksModFolder}");
                            
                            // Focus on critical folders where structure layouts might be stored
                            string[] folderPatterns = new[] {
                                "1.5/Defs/CustomGenDefs",
                                "1.5/Defs/SettlementLayoutDefs",
                                "1.5/Defs/StructureLayoutDefs",
                                "1.5/Defs/ThingDefs_Buildings",
                                "Defs/CustomGenDefs",
                                "Defs/SettlementLayoutDefs",
                                "Defs/StructureLayoutDefs",
                                "Defs/ThingDefs_Buildings"
                            };
                            
                            // Visit each potential folder
                            foreach (var folderPattern in folderPatterns)
                            {
                                string folderPath = Path.Combine(alphaBooksModFolder, folderPattern);
                                if (Directory.Exists(folderPath))
                                {
                                    // SCAN through all XML files in this folder
                                    foreach (var xmlFile in Directory.GetFiles(folderPath, "*.xml", SearchOption.AllDirectories))
                                    {
                                        try
                                        {
                                            using (var fileStream = new FileStream(xmlFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                            using (var reader = XmlReader.Create(fileStream, new XmlReaderSettings { 
                                                IgnoreWhitespace = true, 
                                                IgnoreComments = true,
                                                IgnoreProcessingInstructions = true
                                            }))
                                            {
                                                while (reader.Read())
                                                {
                                                    // Look for: defName, symbol, root, and all common structure element names
                                                    bool isRelevantElement = false;
                                                    if (reader.NodeType == XmlNodeType.Element)
                                                    {
                                                        string elementName = reader.Name.ToLower();
                                                        isRelevantElement = (elementName == "defname" || 
                                                                            elementName == "symbol" || 
                                                                            elementName == "root" ||
                                                                            elementName == "path" ||
                                                                            elementName == "symbolpart");
                                                    }
                                                    
                                                    if (isRelevantElement && !reader.IsEmptyElement)
                                                    {
                                                        string elementValue = reader.ReadElementContentAsString().Trim();
                                                        
                                                        if (!string.IsNullOrEmpty(elementValue) && !registeredNames.Contains(elementValue))
                                                        {
                                                            try
                                                            {
                                                                object placeholderDef = SymbolRegistry.CreatePlaceholderDef(elementValue);
                                                                SymbolRegistry.RegisterDef(elementValue, placeholderDef);
                                                                count++;
                                                                registeredNames.Add(elementValue);
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                // Try simpler placeholder method
                                                                try
                                                                {
                                                                    var basicPlaceholder = new BasicPlaceholderDef { defName = elementValue };
                                                                    SymbolRegistry.RegisterDef(elementValue, basicPlaceholder);
                                                                    count++;
                                                                    registeredNames.Add(elementValue);
                                                                }
                                                                catch
                                                                {
                                                                    Log.Warning($"[KCSG Unbound] Failed to register Alpha Books element {elementValue}: {ex.Message}");
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Warning($"[KCSG Unbound] Error processing Alpha Books file {xmlFile}: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log.Message("[KCSG Unbound] Alpha Books mod folder not found directly - relying on preregistered templates only");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error during Alpha Books mod folder scan: {ex.Message}");
                    }
                    
                    Log.Message($"[KCSG Unbound] Registered {count} Alpha Books structures and symbols");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] Alpha Books mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing Alpha Books layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers Vanilla Factions Expanded - Mechanoids layouts
        /// </summary>
        private static void InitializeVFEMechanoidLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for VFE Mechanoids layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for VFE Mechanoids layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VFE MECHANOIDS MOD BEING LOADED
                bool vfeMechanoidModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Mechanoid") || 
                    m.PackageId.Contains("2329011599") || 
                    m.PackageId.Contains("oskarpotocki.vfe.mechanoid") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.mechanoid"));
                    
                if (vfeMechanoidModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Mechanoids mod is loaded - implementing comprehensive structure registration");
                
                    // COMPREHENSIVE LIST OF ALL KNOWN VFEM STRUCTURES
                    List<string> criticalBaseNames = new List<string>
                    {
                        // Ship types
                        "Carrier", "CarrierDLC", "Frigate", "FrigateDLC", 
                        "Destroyer", "DestroyerDLC", "Cruiser", "CruiserDLC",
                        
                        // Base structures
                        "BroadcastingStation", "MechShipBeacon", "MechShipLanding",
                        "MechShipCrashing", "MechShipDebris", "FactoryRemnants",
                        
                        // Special symbols and structures
                        "StructureDLC", "StructureNODLC", "StatringFactories", "Symbols"
                    };
                    
                    // Track the defs we create
                    int count = 0;
                    HashSet<string> registeredNames = new HashSet<string>();
                    
                    // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                    foreach (string baseName in criticalBaseNames)
                    {
                        // PRIMARY NAMING: VFEM_BaseName
                        string primaryDefName = $"VFEM_{baseName}";
                        
                        // NAMING PATTERNS - These are all the formats that could be used for references
                        List<string> allNamingPatterns = new List<string>();
                        
                        // 1. Standard pattern with and without prefix
                        allNamingPatterns.Add(primaryDefName);
                        allNamingPatterns.Add(baseName);
                        
                        // 2. Numbered variants (1-20) for all structures
                        for (int i = 1; i <= 20; i++)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{i}");
                        }
                        
                        // 3. Letter suffixes (A-Z) - standard variation for most structures
                        for (char letter = 'A'; letter <= 'Z'; letter++)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{letter}");
                        }
                        
                        // 4. Common suffix variations
                        string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Ship", "Complex" };
                        foreach (string suffix in suffixes)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{suffix}");
                            allNamingPatterns.Add($"{baseName}{suffix}");
                        }
                        
                        // 5. Alternative prefixing styles that some mods might use
                        string[] prefixingStyles = new[] 
                        {
                            $"Structure_VFEM_{baseName}", 
                            $"Layout_VFEM_{baseName}",
                            $"StructureLayout_VFEM_{baseName}",
                            $"VFEM_Structure_{baseName}",
                            $"VFEM_Layout_{baseName}",
                            $"VFEM.{baseName}"
                        };
                        
                        foreach (string style in prefixingStyles)
                        {
                            allNamingPatterns.Add(style);
                        }
                        
                        // ADD ALL THE NAMING VARIANTS USING MULTIPLE FALLBACK METHODS
                        foreach (string defName in allNamingPatterns)
                        {
                            try
                            {
                                // Skip if already registered
                                if (SymbolRegistry.IsDefRegistered(defName) || registeredNames.Contains(defName))
                                    continue;
                                
                                // TIER 1: Try with the standard method first
                                try
                                {
                                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                    SymbolRegistry.RegisterDef(defName, placeholderDef);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch 
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 2: Try direct creation of KCSG.StructureLayoutDef
                                try
                                {
                                    // Look for the KCSG.StructureLayoutDef type
                                    Type structLayoutType = AppDomain.CurrentDomain.GetAssemblies()
                                        .SelectMany(a => {
                                            try { return a.GetTypes(); } 
                                            catch { return new Type[0]; }
                                        })
                                        .FirstOrDefault(t => t.FullName == "KCSG.StructureLayoutDef" || 
                                                           (t.Name == "StructureLayoutDef" && t.Namespace == "KCSG"));
                                    
                                    if (structLayoutType != null)
                                    {
                                        object placeholderDef = Activator.CreateInstance(structLayoutType);
                                        PropertyInfo defNameProp = structLayoutType.GetProperty("defName");
                                        defNameProp?.SetValue(placeholderDef, defName);
                                        
                                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                                        count++;
                                        registeredNames.Add(defName);
                                        continue; // Success, move to next name
                                    }
                                }
                                catch
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 3: Try with the BasicPlaceholderDef (defined specifically for this purpose)
                                try
                                {
                                    var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                    SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 4: Absolute last resort - use Dictionary object as a placeholder
                                try
                                {
                                    var dictPlaceholder = new Dictionary<string, string> { { "defName", defName } };
                                    SymbolRegistry.RegisterDef(defName, dictPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                }
                                catch (Exception ex)
                                {
                                    // Log the failure but don't throw - we want to continue with other names
                                    Log.Warning($"[KCSG Unbound] All methods failed for {defName}: {ex.Message}");
                                }
                            }
                            catch
                            {
                                // Completely ignore any outer exceptions - must not fail
                            }
                        }
                    }
                    
                    // SCAN XML FILES DIRECTLY - this finds actual structures defined in XML
                    // regardless of whether they follow the expected naming patterns
                    try
                    {
                        // Scan XML files in the mod folder to find all structure layouts
                        string vfemModFolder = null;
                        
                        // Check common paths where VFE Mechanoids might be installed
                        string[] possiblePaths = new[]
                        {
                            "Mods/Vanilla Factions Expanded - Mechanoids",
                            "Mods/VFE - Mechanoids",
                            "Mods/Vanilla Factions Expanded - Mechanoid",
                            "Mods/VanillaFactionsExpandedMechanoids",
                            "Mods/oskarpotocki.vfe.mechanoid",
                            "Mods/2329011599", // Steam Workshop ID
                            "294100/2329011599", // Workshop folder format
                            Path.Combine(GenFilePaths.ModsFolderPath, "Vanilla Factions Expanded - Mechanoids"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "VFE - Mechanoids"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "Vanilla Factions Expanded - Mechanoid"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "VanillaFactionsExpandedMechanoids"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "oskarpotocki.vfe.mechanoid"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "2329011599")
                        };
                        
                        foreach (var path in possiblePaths)
                        {
                            if (Directory.Exists(path))
                            {
                                vfemModFolder = path;
                                break;
                            }
                        }
                        
                        if (vfemModFolder != null)
                        {
                            Log.Message($"[KCSG Unbound] Found VFE Mechanoids mod folder at {vfemModFolder}");
                            
                            // Focus on the CustomGenDefs folder for VFEM
                            string customGenDefsFolder = Path.Combine(vfemModFolder, "1.5", "Defs", "CustomGenDefs");
                            if (Directory.Exists(customGenDefsFolder))
                            {
                                Log.Message($"[KCSG Unbound] Found VFE Mechanoids CustomGenDefs folder at {customGenDefsFolder}");
                                
                                // RECURSIVELY scan all XML files in this folder for structure layouts
                                foreach (var xmlFile in Directory.GetFiles(customGenDefsFolder, "*.xml", SearchOption.AllDirectories))
                                {
                                    try
                                    {
                                        string xmlContent = File.ReadAllText(xmlFile);
                                        
                                        // Only log processing for a few files instead of all of them
                                        if (count % 10 == 0)
                                        {
                                            Log.Message($"[KCSG Unbound] Processing VFE Mechanoids file: {Path.GetFileName(xmlFile)}");
                                        }
                                        
                                        // Simple parsing to find defName elements within the XML
                                        int pos = 0;
                                        while (true)
                                        {
                                            int defNameStart = xmlContent.IndexOf("<defName>", pos);
                                            if (defNameStart == -1) break;
                                            
                                            int defNameEnd = xmlContent.IndexOf("</defName>", defNameStart);
                                            if (defNameEnd == -1) break;
                                            
                                            string defName = xmlContent.Substring(defNameStart + 9, defNameEnd - defNameStart - 9);
                                            
                                            // Check if this is a VFEM structure by name pattern or register all structures from these files
                                            if ((defName.StartsWith("VFEM_") || 
                                                 Path.GetFileNameWithoutExtension(xmlFile).StartsWith("VFEM_")) && 
                                                !registeredNames.Contains(defName))
                                            {
                                                // Try to create a placeholder with multiple methods
                                                try
                                                {
                                                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                                    SymbolRegistry.RegisterDef(defName, placeholderDef);
                                                    count++;
                                                    registeredNames.Add(defName);
                                                }
                                                catch
                                                {
                                                    // Fallback to basic placeholder
                                                    try
                                                    {
                                                        var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                                        SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                                        count++;
                                                        registeredNames.Add(defName);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Log.Warning($"[KCSG Unbound] Failed to register {defName} from XML: {ex.Message}");
                                                    }
                                                }
                                            }
                                            
                                            // Move to next part of file
                                            pos = defNameEnd;
                                        }
                                        
                                        // Also scan for symbols in the file that might not have defName elements
                                        string[] symbolMarkers = new[] { 
                                            "<symbol>", "<symbolDef>", "<name>", "<root>", "<path>"
                                        };
                                        
                                        foreach (var marker in symbolMarkers)
                                        {
                                            pos = 0;
                                            while (true)
                                            {
                                                int symbolStart = xmlContent.IndexOf(marker, pos);
                                                if (symbolStart == -1) break;
                                                
                                                string closingTag = marker.Replace("<", "</");
                                                int symbolEnd = xmlContent.IndexOf(closingTag, symbolStart);
                                                if (symbolEnd == -1) break;
                                                
                                                string symbol = xmlContent.Substring(symbolStart + marker.Length, symbolEnd - symbolStart - marker.Length);
                                                symbol = symbol.Trim();
                                                
                                                if (!string.IsNullOrEmpty(symbol) && !registeredNames.Contains(symbol))
                                                {
                                                    try
                                                    {
                                                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(symbol);
                                                        SymbolRegistry.RegisterDef(symbol, placeholderDef);
                                                        count++;
                                                        registeredNames.Add(symbol);
                                                    }
                                                    catch
                                                    {
                                                        // Ignore failures for these secondary symbols
                                                    }
                                                }
                                                
                                                // Move to next part of file
                                                pos = symbolEnd;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning($"[KCSG Unbound] Error processing {xmlFile}: {ex.Message}");
                                    }
                                }
                            }
                            else
                            {
                                Log.Warning($"[KCSG Unbound] VFE Mechanoids CustomGenDefs folder not found at expected location {customGenDefsFolder}");
                                
                                // Try to find any XML files that might contain structure layouts
                                var xmlFiles = Directory.GetFiles(vfemModFolder, "*.xml", SearchOption.AllDirectories)
                                    .Where(f => Path.GetFileName(f).Contains("VFEM_") || 
                                               Path.GetFileName(f).Contains("Structure") ||
                                               Path.GetFileName(f).Contains("Carrier") ||
                                               Path.GetFileName(f).Contains("Frigate") ||
                                               Path.GetFileName(f).Contains("Destroyer") ||
                                               Path.GetFileName(f).Contains("Cruiser") ||
                                               Path.GetFileName(f).Contains("Station"));
                                        
                                foreach (var xmlFile in xmlFiles)
                                {
                                    try
                                    {
                                        string xmlContent = File.ReadAllText(xmlFile);
                                        
                                        // Simple parsing to find defName elements within the XML
                                        int pos = 0;
                                        while (true)
                                        {
                                            int defNameStart = xmlContent.IndexOf("<defName>", pos);
                                            if (defNameStart == -1) break;
                                            
                                            int defNameEnd = xmlContent.IndexOf("</defName>", defNameStart);
                                            if (defNameEnd == -1) break;
                                            
                                            string defName = xmlContent.Substring(defNameStart + 9, defNameEnd - defNameStart - 9);
                                            
                                            // Check if this is a VFEM structure by name pattern
                                            if (defName.StartsWith("VFEM_") && !registeredNames.Contains(defName))
                                            {
                                                // Try to create a placeholder
                                                try
                                                {
                                                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                                    SymbolRegistry.RegisterDef(defName, placeholderDef);
                                                    count++;
                                                    registeredNames.Add(defName);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Log.Warning($"[KCSG Unbound] Failed to register {defName} from XML: {ex.Message}");
                                                }
                                            }
                                            
                                            // Move to next part of file
                                            pos = defNameEnd;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning($"[KCSG Unbound] Error processing {xmlFile}: {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log.Message("[KCSG Unbound] VFE Mechanoids mod folder not found directly - relying on preregistered templates only");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                    }
                    
                    // Calculate hashes but don't log every single one
                    try
                    {
                        // Only log a summary instead of individual hashes
                        if (registeredNames.Count > 0)
                        {
                            // Sample a few hashes to aid debugging but don't log all
                            int sampleCount = Math.Min(5, registeredNames.Count);
                            Log.Message($"[KCSG Unbound] Calculated hashes for {registeredNames.Count} VFE Mechanoids structures. Sample: " +
                                string.Join(", ", registeredNames.Take(sampleCount)));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error during hash calculation: {ex.Message}");
                    }
                    
                    Log.Message($"[KCSG Unbound] Registered {count} VFE Mechanoids layouts using enhanced methods");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] VFE Mechanoids mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VFE Mechanoids layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers Vanilla Factions Expanded - Medieval layouts
        /// </summary>
        private static void InitializeVFEMedievalLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for VFE Medieval layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for VFE Medieval layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VFE MEDIEVAL MOD BEING LOADED
                bool vfeMedievalModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Medieval") || 
                    m.PackageId.Contains("3444347874") || 
                    m.PackageId.Contains("oskarpotocki.vfe.medieval") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.medievalmodule"));
                    
                if (vfeMedievalModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Medieval mod is loaded - implementing comprehensive structure registration");
                    
                    // COMPREHENSIVE LIST OF ALL KNOWN VFE MEDIEVAL STRUCTURES
                    List<string> criticalBaseNames = new List<string>
                    {
                        // Structure types from the XML files
                        "MedievalHouse", "MedievalTent", "MedievalKeep", "MedievalCastle",
                        "Tower", "Hall", "Barracks", "Stable", "Blacksmith", "Church",
                        "Tavern", "Market", "Farm", "Laboratory", "Walls", "Gate",
                        
                        // Special symbols and structures
                        "Symbol", "MedievalSymbol"
                    };
                    
                    // Track the defs we create
                    int count = 0;
                    HashSet<string> registeredNames = new HashSet<string>();
                    
                    // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                    foreach (string baseName in criticalBaseNames)
                    {
                        // PRIMARY NAMING: VFEM_BaseName or VFE_Medieval_BaseName
                        string primaryDefName = $"VFEM_{baseName}";
                        string alternateDefName = $"VFE_Medieval_{baseName}";
                        
                        // NAMING PATTERNS - These are all the formats that could be used for references
                        List<string> allNamingPatterns = new List<string>();
                        
                        // 1. Standard patterns with different prefixes
                        allNamingPatterns.Add(primaryDefName);
                        allNamingPatterns.Add(alternateDefName);
                        allNamingPatterns.Add($"Medieval_{baseName}");
                        allNamingPatterns.Add(baseName);
                        
                        // 2. Numbered variants (1-20) for all structures
                        for (int i = 1; i <= 20; i++)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{i}");
                            allNamingPatterns.Add($"{alternateDefName}{i}");
                            allNamingPatterns.Add($"Medieval_{baseName}{i}");
                        }
                        
                        // 3. Letter suffixes (A-Z) - standard variation for most structures
                        for (char letter = 'A'; letter <= 'Z'; letter++)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{letter}");
                            allNamingPatterns.Add($"{alternateDefName}{letter}");
                            allNamingPatterns.Add($"Medieval_{baseName}{letter}");
                        }
                        
                        // 4. Common suffix variations
                        string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex", "Small", "Medium", "Large" };
                        foreach (string suffix in suffixes)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{suffix}");
                            allNamingPatterns.Add($"{alternateDefName}{suffix}");
                            allNamingPatterns.Add($"Medieval_{baseName}{suffix}");
                            allNamingPatterns.Add($"{baseName}{suffix}");
                        }
                        
                        // 5. Alternative prefixing styles that some mods might use
                        string[] prefixingStyles = new[] 
                        {
                            $"Structure_VFEM_{baseName}", 
                            $"Layout_VFEM_{baseName}",
                            $"StructureLayout_VFEM_{baseName}",
                            $"VFEM_Structure_{baseName}",
                            $"VFEM_Layout_{baseName}",
                            $"VFEM.{baseName}",
                            $"Medieval.{baseName}"
                        };
                        
                        foreach (string style in prefixingStyles)
                        {
                            allNamingPatterns.Add(style);
                        }
                        
                        // ADD ALL THE NAMING VARIANTS USING MULTIPLE FALLBACK METHODS
                        foreach (string defName in allNamingPatterns)
                        {
                            try
                            {
                                // Skip if already registered
                                if (SymbolRegistry.IsDefRegistered(defName) || registeredNames.Contains(defName))
                                    continue;
                                
                                // TIER 1: Try with the standard method first
                                try
                                {
                                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                    SymbolRegistry.RegisterDef(defName, placeholderDef);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch 
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 2: Try direct creation of KCSG.StructureLayoutDef
                                try
                                {
                                    // Look for the KCSG.StructureLayoutDef type
                                    Type structLayoutType = AppDomain.CurrentDomain.GetAssemblies()
                                        .SelectMany(a => {
                                            try { return a.GetTypes(); } 
                                            catch { return new Type[0]; }
                                        })
                                        .FirstOrDefault(t => t.FullName == "KCSG.StructureLayoutDef" || 
                                                           (t.Name == "StructureLayoutDef" && t.Namespace == "KCSG"));
                                    
                                    if (structLayoutType != null)
                                    {
                                        object placeholderDef = Activator.CreateInstance(structLayoutType);
                                        PropertyInfo defNameProp = structLayoutType.GetProperty("defName");
                                        defNameProp?.SetValue(placeholderDef, defName);
                                        
                                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                                        count++;
                                        registeredNames.Add(defName);
                                        continue; // Success, move to next name
                                    }
                                }
                                catch
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 3: Try with the BasicPlaceholderDef (defined specifically for this purpose)
                                try
                                {
                                    var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                    SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                    continue; // Success, move to next name
                                }
                                catch
                                {
                                    // Fall through to next method if this fails
                                }
                                
                                // TIER 4: Absolute last resort - use Dictionary object as a placeholder
                                try
                                {
                                    var dictPlaceholder = new Dictionary<string, string> { { "defName", defName } };
                                    SymbolRegistry.RegisterDef(defName, dictPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                }
                                catch (Exception ex)
                                {
                                    // Log the failure but don't throw - we want to continue with other names
                                    Log.Warning($"[KCSG Unbound] All methods failed for {defName}: {ex.Message}");
                                }
                            }
                            catch
                            {
                                // Completely ignore any outer exceptions - must not fail
                            }
                        }
                    }
                    
                    // SCAN XML FILES DIRECTLY - this finds actual structures defined in XML
                    // regardless of whether they follow the expected naming patterns
                    try
                    {
                        // Scan XML files in the mod folder to find all structure layouts
                        string vfeMedievalModFolder = null;
                        
                        // Check common paths where VFE Medieval might be installed
                        string[] possiblePaths = new[]
                        {
                            "Mods/Vanilla Factions Expanded - Medieval",
                            "Mods/VFE - Medieval",
                            "Mods/Vanilla Factions Expanded - Medieval 2",
                            "Mods/VFE Medieval 2",
                            "Mods/VanillaFactionsExpandedMedieval",
                            "Mods/oskarpotocki.vfe.medieval",
                            "Mods/3444347874", // Steam Workshop ID for Medieval 2
                            "294100/3444347874", // Workshop folder format
                            Path.Combine(GenFilePaths.ModsFolderPath, "Vanilla Factions Expanded - Medieval"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "VFE - Medieval"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "Vanilla Factions Expanded - Medieval 2"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "VFE Medieval 2"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "VanillaFactionsExpandedMedieval"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "oskarpotocki.vfe.medieval"),
                            Path.Combine(GenFilePaths.ModsFolderPath, "3444347874")
                        };
                        
                        foreach (var path in possiblePaths)
                        {
                            if (Directory.Exists(path))
                            {
                                vfeMedievalModFolder = path;
                                break;
                            }
                        }
                        
                        if (vfeMedievalModFolder != null)
                        {
                            Log.Message($"[KCSG Unbound] Found VFE Medieval mod folder at {vfeMedievalModFolder}");
                            
                            // Focus on the CustomGenDefs folder for VFE Medieval
                            string customGenDefsFolder = Path.Combine(vfeMedievalModFolder, "1.5", "Defs", "CustomGenDefs");
                            if (Directory.Exists(customGenDefsFolder))
                            {
                                Log.Message($"[KCSG Unbound] Found VFE Medieval CustomGenDefs folder at {customGenDefsFolder}");
                                
                                // RECURSIVELY scan all XML files in this folder for structure layouts
                                foreach (var xmlFile in Directory.GetFiles(customGenDefsFolder, "*.xml", SearchOption.AllDirectories))
                                {
                                    try
                                    {
                                        string xmlContent = File.ReadAllText(xmlFile);
                                        Log.Message($"[KCSG Unbound] Processing VFE Medieval file: {Path.GetFileName(xmlFile)}");
                                        
                                        // Simple parsing to find defName elements within the XML
                                        int pos = 0;
                                        while (true)
                                        {
                                            int defNameStart = xmlContent.IndexOf("<defName>", pos);
                                            if (defNameStart == -1) break;
                                            
                                            int defNameEnd = xmlContent.IndexOf("</defName>", defNameStart);
                                            if (defNameEnd == -1) break;
                                            
                                            string defName = xmlContent.Substring(defNameStart + 9, defNameEnd - defNameStart - 9);
                                            
                                            // Register all def names from the medieval XML files
                                            if (!registeredNames.Contains(defName))
                                            {
                                                // Try to create a placeholder with multiple methods
                                                try
                                                {
                                                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                                    SymbolRegistry.RegisterDef(defName, placeholderDef);
                                                    count++;
                                                    registeredNames.Add(defName);
                                                }
                                                catch
                                                {
                                                    // Fallback to basic placeholder
                                                    try
                                                    {
                                                        var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                                        SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                                        count++;
                                                        registeredNames.Add(defName);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Log.Warning($"[KCSG Unbound] Failed to register {defName} from XML: {ex.Message}");
                                                    }
                                                }
                                            }
                                            
                                            // Move to next part of file
                                            pos = defNameEnd;
                                        }
                                        
                                        // Also scan for symbols in the file that might not have defName elements
                                        string[] symbolMarkers = new[] { 
                                            "<symbol>", "<symbolDef>", "<name>", "<root>", "<path>"
                                        };
                                        
                                        foreach (var marker in symbolMarkers)
                                        {
                                            pos = 0;
                                            while (true)
                                            {
                                                int symbolStart = xmlContent.IndexOf(marker, pos);
                                                if (symbolStart == -1) break;
                                                
                                                string closingTag = marker.Replace("<", "</");
                                                int symbolEnd = xmlContent.IndexOf(closingTag, symbolStart);
                                                if (symbolEnd == -1) break;
                                                
                                                string symbol = xmlContent.Substring(symbolStart + marker.Length, symbolEnd - symbolStart - marker.Length);
                                                symbol = symbol.Trim();
                                                
                                                if (!string.IsNullOrEmpty(symbol) && !registeredNames.Contains(symbol))
                                                {
                                                    try
                                                    {
                                                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(symbol);
                                                        SymbolRegistry.RegisterDef(symbol, placeholderDef);
                                                        count++;
                                                        registeredNames.Add(symbol);
                                                    }
                                                    catch
                                                    {
                                                        // Ignore failures for these secondary symbols
                                                    }
                                                }
                                                
                                                // Move to next part of file
                                                pos = symbolEnd;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning($"[KCSG Unbound] Error processing {xmlFile}: {ex.Message}");
                                    }
                                }
                            }
                            else
                            {
                                Log.Warning($"[KCSG Unbound] VFE Medieval CustomGenDefs folder not found at expected location {customGenDefsFolder}");
                                
                                // Try to find any XML files that might contain structure layouts
                                var xmlFiles = Directory.GetFiles(vfeMedievalModFolder, "*.xml", SearchOption.AllDirectories)
                                    .Where(f => Path.GetFileName(f).Contains("Medieval") || 
                                               Path.GetFileName(f).Contains("House") ||
                                               Path.GetFileName(f).Contains("Tent") ||
                                               Path.GetFileName(f).Contains("Keep") ||
                                               Path.GetFileName(f).Contains("Castle") ||
                                               Path.GetFileName(f).Contains("Tower") ||
                                               Path.GetFileName(f).Contains("Symbol"));
                                           
                                foreach (var xmlFile in xmlFiles)
                                {
                                    try
                                    {
                                        string xmlContent = File.ReadAllText(xmlFile);
                                        
                                        // Simple parsing to find defName elements within the XML
                                        int pos = 0;
                                        while (true)
                                        {
                                            int defNameStart = xmlContent.IndexOf("<defName>", pos);
                                            if (defNameStart == -1) break;
                                            
                                            int defNameEnd = xmlContent.IndexOf("</defName>", defNameStart);
                                            if (defNameEnd == -1) break;
                                            
                                            string defName = xmlContent.Substring(defNameStart + 9, defNameEnd - defNameStart - 9);
                                            
                                            // Register all structure definitions from medieval files
                                            if (!registeredNames.Contains(defName))
                                            {
                                                // Try to create a placeholder
                                                try
                                                {
                                                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                                    SymbolRegistry.RegisterDef(defName, placeholderDef);
                                                    count++;
                                                    registeredNames.Add(defName);
                                                }
                                                catch (Exception ex)
                                                {
                                                    Log.Warning($"[KCSG Unbound] Failed to register {defName} from XML: {ex.Message}");
                                                }
                                            }
                                            
                                            // Move to next part of file
                                            pos = defNameEnd;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning($"[KCSG Unbound] Error processing {xmlFile}: {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            Log.Message("[KCSG Unbound] VFE Medieval mod folder not found directly - relying on preregistered templates only");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                    }
                    
                    Log.Message($"[KCSG Unbound] Registered {count} VFE Medieval layouts using enhanced methods");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] VFE Medieval mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VFE Medieval layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers Save Our Ship 2 layouts
        /// </summary>
        private static void InitializeSaveOurShip2Layouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for Save Our Ship 2 layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for Save Our Ship 2 layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR SAVE OUR SHIP 2 MOD BEING LOADED
                bool sos2ModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Save Our Ship") || 
                    m.Name.Contains("SOS2") ||
                    m.PackageId.Contains("1909914131") || 
                    m.PackageId.Contains("ludeon.rimworld.shipshavecomeback") ||
                    m.PackageId.Contains("lwm.shipshavecomeback"));
                    
                if (sos2ModLoaded)
                {
                    Log.Message("[KCSG Unbound] Save Our Ship 2 mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] Save Our Ship 2 mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing Save Our Ship 2 layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers Vanilla Outposts Expanded layouts
        /// </summary>
        private static void InitializeVanillaOutpostsLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for Vanilla Outposts Expanded layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for Vanilla Outposts Expanded layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VANILLA OUTPOSTS EXPANDED MOD BEING LOADED
                bool voeModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Outpost") || 
                    m.PackageId.Contains("2688941031") || 
                    m.PackageId.Contains("oskarpotocki.vfe.outposts") ||
                    m.PackageId.Contains("vanillaexpanded.outposts"));
                    
                if (voeModLoaded)
                {
                    Log.Message("[KCSG Unbound] Vanilla Outposts Expanded mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] Vanilla Outposts Expanded mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing Vanilla Outposts Expanded layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers VFE Ancients layouts
        /// </summary>
        private static void InitializeVFEAncientsLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for VFE Ancients layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for VFE Ancients layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VFE ANCIENTS MOD BEING LOADED
                bool ancientsModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Ancients") || 
                    m.PackageId.Contains("2654846754") || // Main VFE Ancients 
                    m.PackageId.Contains("2946113288") || // Lost Vaults
                    m.PackageId.Contains("3160710884") || // Even More Vaults
                    m.PackageId.Contains("3325594457") || // Soups Vault Collection
                    m.PackageId.Contains("oskarpotocki.vfe.ancients"));
                    
                if (ancientsModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Ancients mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] VFE Ancients mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VFE Ancients layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers VFE Insectoids layouts
        /// </summary>
        private static void InitializeVFEInsectoidsLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for VFE Insectoids layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for VFE Insectoids layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VFE INSECTOIDS MOD BEING LOADED
                bool insectoidsModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Insectoid") || 
                    m.PackageId.Contains("1938063127") || 
                    m.PackageId.Contains("oskarpotocki.vfe.insectoids") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.insectoidsmodule"));
                    
                if (insectoidsModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Insectoids mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] VFE Insectoids mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VFE Insectoids layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers VFE Classical layouts
        /// </summary>
        private static void InitializeVFEClassicalLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for VFE Classical layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for VFE Classical layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VFE CLASSICAL MOD BEING LOADED
                bool classicalModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Classical") || 
                    m.PackageId.Contains("1221070409") || 
                    m.PackageId.Contains("oskarpotocki.vfe.classical") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.classicalmodule"));
                    
                if (classicalModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Classical mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] VFE Classical mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VFE Classical layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers VFE Empire layouts
        /// </summary>
        private static void InitializeVFEEmpireLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for VFE Empire layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for VFE Empire layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VFE EMPIRE MOD BEING LOADED
                bool empireModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Empire") || 
                    m.PackageId.Contains("1967385037") || 
                    m.PackageId.Contains("oskarpotocki.vfe.empire") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.empiremodule"));
                    
                if (empireModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Empire mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] VFE Empire mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VFE Empire layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers Reinforced Mechanoids 2 layouts
        /// </summary>
        private static void InitializeReinforcedMechanoidsLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for Reinforced Mechanoids 2 layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for Reinforced Mechanoids 2 layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR REINFORCED MECHANOIDS 2 MOD BEING LOADED
                bool rmModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Reinforced Mech") || 
                    m.PackageId.Contains("reinforcedmechanoids") ||
                    m.PackageId.Contains("rm2"));
                    
                if (rmModLoaded)
                {
                    Log.Message("[KCSG Unbound] Reinforced Mechanoids 2 mod is loaded - implementing comprehensive structure registration");
                    
                    // Track the defs we create
                    int count = 0;
                    HashSet<string> registeredNames = new HashSet<string>();
                    
                    // Load critical base names for Reinforced Mechanoids
                    List<string> criticalBaseNames = new List<string>
                    {
                        // Main mechanoid structures
                        "Base", "Camp", "Outpost", "Bunker", "Hive", "Nest",
                        "Factory", "Barracks", "Hangar", "Fabricator", "Assembler",
                        "Armory", "Laboratory", "Command", "Station", "Sentinel",
                        
                        // Specific mechanoid types from the mod
                        "Behemoth", "Buffer", "Caretaker", "Falcon", "Gremlin", 
                        "Harpy", "Locust", "Marshal", "Matriarch", "Ranger",
                        "Sentinel", "SentinelBrawler", "Spartan", "Vulture", 
                        "Wraith", "Zealot", "ZealotAssassin"
                    };
                    
                    // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                    foreach (string baseName in criticalBaseNames)
                    {
                        // PRIMARY NAMING: RM_BaseName
                        string primaryDefName = $"RM_{baseName}";
                        
                        // NAMING PATTERNS - These are all the formats that could be used for references
                        List<string> allNamingPatterns = new List<string>();
                        
                        // 1. Standard pattern with prefix
                        allNamingPatterns.Add(primaryDefName);
                        
                        // 2. Letter suffixes (A-Z) - standard variation for most structures
                        for (char letter = 'A'; letter <= 'Z'; letter++)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{letter}");
                        }
                        
                        // 3. Numbered variants for specific structures
                        for (int i = 1; i <= 10; i++)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{i}");
                        }
                        
                        // 4. Common suffix variations
                        string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex" };
                        foreach (string suffix in suffixes)
                        {
                            allNamingPatterns.Add($"{primaryDefName}{suffix}");
                        }
                        
                        // 5. Alternative prefixing styles that some mods might use
                        string[] prefixingStyles = new[] 
                        {
                            $"Structure_RM_{baseName}", 
                            $"Layout_RM_{baseName}",
                            $"StructureLayout_RM_{baseName}",
                            $"RM_Structure_{baseName}",
                            $"RM_Layout_{baseName}",
                            $"RM.{baseName}"
                        };
                        
                        foreach (string style in prefixingStyles)
                        {
                            allNamingPatterns.Add(style);
                        }
                        
                        // ADD ALL THE NAMING VARIANTS USING MULTIPLE FALLBACK METHODS
                        foreach (string defName in allNamingPatterns)
                        {
                            try
                            {
                                // Skip if already registered
                                if (SymbolRegistry.IsDefRegistered(defName) || registeredNames.Contains(defName))
                                    continue;
                                
                                // Register the def
                                object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                SymbolRegistry.RegisterDef(defName, placeholderDef);
                                count++;
                                registeredNames.Add(defName);
                            }
                            catch (Exception ex)
                            {
                                // Try with basic placeholder if the standard method fails
                                try
                                {
                                    var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                    SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                }
                                catch
                                {
                                    // Log only occasionally to avoid spam
                                    if (count % 50 == 0)
                                    {
                                        Log.Warning($"[KCSG Unbound] Failed to register {defName}: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    
                    // Also register compound variants
                    string[] compoundNames = new[]
                    {
                        "MechBase", "MechHive", "MechNest", "MechOutpost", "MechStation",
                        "MechFactory", "AssemblyComplex", "CommandCenter", "DefensePost"
                    };
                    
                    foreach(var name in compoundNames)
                    {
                        string defName = $"RM_{name}";
                        if (!SymbolRegistry.IsDefRegistered(defName) && !registeredNames.Contains(defName))
                        {
                            try
                            {
                                object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                SymbolRegistry.RegisterDef(defName, placeholderDef);
                                count++;
                                registeredNames.Add(defName);
                            }
                            catch (Exception ex)
                            {
                                // Try with basic placeholder
                                try
                                {
                                    var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                    SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                    count++;
                                    registeredNames.Add(defName);
                                }
                                catch 
                                {
                                    Log.Warning($"[KCSG Unbound] Failed to register {defName}: {ex.Message}");
                                }
                            }
                        }
                    }
                    
                    Log.Message($"[KCSG Unbound] Registered {count} Reinforced Mechanoids 2 structure variants");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] Reinforced Mechanoids 2 mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing Reinforced Mechanoids 2 layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Scans a general VFE mod for structure layouts
        /// </summary>
        private static void ScanVFEMod(string modName, string altModName, string workshopId, 
                                      HashSet<string> registeredNames, ref int count)
        {
            try
            {
                // Check common paths where the mod might be installed
                string[] possiblePaths = new[]
                {
                    Path.Combine("Mods", modName),
                    Path.Combine("Mods", altModName),
                    Path.Combine("Mods", workshopId),
                    Path.Combine("294100", workshopId),
                    Path.Combine(GenFilePaths.ModsFolderPath, modName),
                    Path.Combine(GenFilePaths.ModsFolderPath, altModName),
                    Path.Combine(GenFilePaths.ModsFolderPath, workshopId)
                };
                
                string modFolder = null;
                foreach (var path in possiblePaths)
                {
                    if (Directory.Exists(path))
                    {
                        modFolder = path;
                        break;
                    }
                }
                
                if (modFolder != null)
                {
                    Log.Message($"[KCSG Unbound] Found {modName} mod folder at {modFolder}");
                    
                    // Focus on the CustomGenDefs folder
                    string customGenDefsFolder = Path.Combine(modFolder, "1.5", "Defs", "CustomGenDefs");
                    if (Directory.Exists(customGenDefsFolder))
                    {
                        Log.Message($"[KCSG Unbound] Found {modName} CustomGenDefs folder at {customGenDefsFolder}");
                        
                        // RECURSIVELY scan all XML files in this folder for structure layouts
                        foreach (var xmlFile in Directory.GetFiles(customGenDefsFolder, "*.xml", SearchOption.AllDirectories))
                        {
                            try
                            {
                                string xmlContent = File.ReadAllText(xmlFile);
                                Log.Message($"[KCSG Unbound] Processing {modName} file: {Path.GetFileName(xmlFile)}");
                                
                                // Simple parsing to find defName elements within the XML
                                int pos = 0;
                                while (true)
                                {
                                    int defNameStart = xmlContent.IndexOf("<defName>", pos);
                                    if (defNameStart == -1) break;
                                    
                                    int defNameEnd = xmlContent.IndexOf("</defName>", defNameStart);
                                    if (defNameEnd == -1) break;
                                    
                                    string defName = xmlContent.Substring(defNameStart + 9, defNameEnd - defNameStart - 9);
                                    
                                    // Register all def names
                                    if (!registeredNames.Contains(defName))
                                    {
                                        // Try to create a placeholder with multiple methods
                                        try
                                        {
                                            object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                            SymbolRegistry.RegisterDef(defName, placeholderDef);
                                            count++;
                                            registeredNames.Add(defName);
                                        }
                                        catch
                                        {
                                            // Fallback to basic placeholder
                                            try
                                            {
                                                var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                                SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                                count++;
                                                registeredNames.Add(defName);
                                            }
                                            catch (Exception ex)
                                            {
                                                Log.Warning($"[KCSG Unbound] Failed to register {defName} from XML: {ex.Message}");
                                            }
                                        }
                                    }
                                    
                                    // Move to next part of file
                                    pos = defNameEnd;
                                }
                                
                                // Also scan for symbols in the file that might not have defName elements
                                string[] symbolMarkers = new[] { 
                                    "<symbol>", "<symbolDef>", "<name>", "<root>", "<path>", "<formation>", "<settlement>", "<camp>"
                                };
                                
                                foreach (var marker in symbolMarkers)
                                {
                                    pos = 0;
                                    while (true)
                                    {
                                        int symbolStart = xmlContent.IndexOf(marker, pos);
                                        if (symbolStart == -1) break;
                                        
                                        string closingTag = marker.Replace("<", "</");
                                        int symbolEnd = xmlContent.IndexOf(closingTag, symbolStart);
                                        if (symbolEnd == -1) break;
                                        
                                        string symbol = xmlContent.Substring(symbolStart + marker.Length, symbolEnd - symbolStart - marker.Length);
                                        symbol = symbol.Trim();
                                        
                                        if (!string.IsNullOrEmpty(symbol) && !registeredNames.Contains(symbol))
                                        {
                                            try
                                            {
                                                object placeholderDef = SymbolRegistry.CreatePlaceholderDef(symbol);
                                                SymbolRegistry.RegisterDef(symbol, placeholderDef);
                                                count++;
                                                registeredNames.Add(symbol);
                                            }
                                            catch
                                            {
                                                // Ignore failures for these secondary symbols
                                            }
                                        }
                                        
                                        // Move to next part of file
                                        pos = symbolEnd;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"[KCSG Unbound] Error processing {xmlFile}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Log.Warning($"[KCSG Unbound] {modName} CustomGenDefs folder not found at expected location {customGenDefsFolder}");
                        
                        // Try to find any XML files that might contain structure layouts
                        var xmlFiles = Directory.GetFiles(modFolder, "*.xml", SearchOption.AllDirectories)
                            .Where(f => Path.GetFileName(f).Contains("Structure") || 
                                        Path.GetFileName(f).Contains("Symbol") ||
                                        Path.GetFileName(f).Contains("Settlement") ||
                                        Path.GetFileName(f).Contains("Camp") ||
                                        Path.GetFileName(f).Contains("Layout") ||
                                        Path.GetFileName(f).Contains("Formation"));
                                        
                        foreach (var xmlFile in xmlFiles)
                        {
                            try
                            {
                                string xmlContent = File.ReadAllText(xmlFile);
                                
                                // Simple parsing to find defName elements within the XML
                                int pos = 0;
                                while (true)
                                {
                                    int defNameStart = xmlContent.IndexOf("<defName>", pos);
                                    if (defNameStart == -1) break;
                                    
                                    int defNameEnd = xmlContent.IndexOf("</defName>", defNameStart);
                                    if (defNameEnd == -1) break;
                                    
                                    string defName = xmlContent.Substring(defNameStart + 9, defNameEnd - defNameStart - 9);
                                    
                                    // Register all structure definitions
                                    if (!registeredNames.Contains(defName))
                                    {
                                        // Try to create a placeholder
                                        try
                                        {
                                            object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                            SymbolRegistry.RegisterDef(defName, placeholderDef);
                                            count++;
                                            registeredNames.Add(defName);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Warning($"[KCSG Unbound] Failed to register {defName} from XML: {ex.Message}");
                                        }
                                    }
                                    
                                    // Move to next part of file
                                    pos = defNameEnd;
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"[KCSG Unbound] Error processing {xmlFile}: {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    Log.Message($"[KCSG Unbound] {modName} mod folder not found");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Error scanning {modName} mod: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if the mod initialized successfully
        /// </summary>
        public static bool IsInitialized => initializationSuccess;
        
        /// <summary>
        /// Get the shared Harmony instance
        /// </summary>
        public static Harmony HarmonyInstance => harmony;
        
        /// <summary>
        /// Override SettingsCategory to avoid creating a settings button
        /// </summary>
        public override string SettingsCategory() => null;
        
        /// <summary>
        /// Dead Man's Switch implementation
        /// </summary>
        private static void InitializeDeadMansSwitchLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for Dead Man's Switch layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for Dead Man's Switch layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR DEAD MAN'S SWITCH MOD BEING LOADED
                bool dmsModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Dead Man's Switch") || 
                    m.PackageId.Contains("3469398006") ||
                    m.PackageId.Contains("aoba.deadmanswitch"));
                    
                if (dmsModLoaded)
                {
                    Log.Message("[KCSG Unbound] Dead Man's Switch mod is loaded - registering prefixes");
                    
                    string[] dmsPrefixes = new[] {
                        "DMS_ChunkSlag", "DMS_Mech_Mushketer", "DMS_Mech_Zabor", 
                        "DMS_Mech_Dogge", "DMS_Mech_Ape", "DMS_Mech_Arquebusier",
                        "DMS_Mech_BattleFrame", "DMS_Mech_Caretta", "DMS_Mech_Dogge",
                        "DMS_Mech_EscortLifter", "DMS_Mech_Falcon", "DMS_Mech_FieldCommand",
                        "DMS_Mech_Gecko", "DMS_Mech_Geochelone", "DMS_Mech_Gladiator",
                        "DMS_Mech_Grenadier", "DMS_Mech_HermitCrab", "DMS_Machine_Hound",
                        "DMS_Mech_Iguana", "DMS_Mech_Jaeger", "DMS_Mech_Kanonier",
                        "DMS_Mech_Killdozer", "DMS_Mech_Lady", "DMS_Mech_Noctula",
                        "DMS_Structure", "DMS_Camp", "DMS_Base", "DMS_Army",
                        "DMSAC_Building_DerelictMech", "DMSAC_Building_DeactivatedLargeMech",
                        "DMSAC_Building_DeactivatedMediumMech", "DMSAC_Building_DeactivatedSmallMech",
                        "DMSAC_Graveyard", "DMSAC_Base", "DMSAC_Structure"
                    };
                    
                    foreach (var prefix in dmsPrefixes)
                    {
                        RegisterWithVariations(prefix);
                    }
                    
                    Log.Message("[KCSG Unbound] Dead Man's Switch prefixes registered");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] Dead Man's Switch mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing Dead Man's Switch layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Mechanitor Encounters implementation
        /// </summary>
        private static void InitializeMechanitorEncountersLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for Mechanitor Encounters layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for Mechanitor Encounters layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR MECHANITOR ENCOUNTERS MOD BEING LOADED
                bool sexModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Mechanitor Encounters") || 
                    m.PackageId.Contains("3417287863"));
                    
                if (sexModLoaded)
                {
                    Log.Message("[KCSG Unbound] Mechanitor Encounters mod is loaded - registering prefixes");
                    
                    string[] sexPrefixes = new[] {
                        "SEX_MechArrayNode", "SEX_MechTeleporter", "SEX_MechResurrector",
                        "SEX_EgoProjector", "SEX_MassMind", "SEX_MechanitorResourceCrate",
                        "SEX_MechanitorMedicalCrate", "SEX_Mechanitor", "SEX_MechanitorEgoProjector",
                        "SEX_Legionary", "SEX_Apocriton", "SEX_Lifter", "SEX_Warqueen",
                        "SEX_Centurion", "SEX_CentipedeBlaster", "SEX_Lancer", "SEX_Scyther",
                        "SEX_Militor", "SEX_MakeshiftChargeTurret", "SEX_JunkyardComponentCrate",
                        "SEX_JunkyardResourceCrateVertical", "SEX_JunkyardResourceCrateHorizontal",
                        "SEX_Junkyard"
                    };
                    
                    foreach (var prefix in sexPrefixes)
                    {
                        RegisterWithVariations(prefix);
                    }
                    
                    // Also register any layout names from Mechanitor Encounters
                    string[] layoutNames = new[] {
                        "SEX_MechanitorBaseOne", "SEX_MechanitorBaseTwo", "SEX_MechanitorBaseThree",
                        "SEX_MechanitorBaseFour", "SEX_MechanitorBaseFive", "SEX_MechanitorBaseSite",
                        "SEX_JunkyardOne", "SEX_JunkyardTwo", "SEX_JunkyardThree", "SEX_JunkyardFour"
                    };
                    
                    foreach (var layoutName in layoutNames)
                    {
                        if (!SymbolRegistry.IsDefRegistered(layoutName))
                        {
                            try
                            {
                                var placeholderDef = SymbolRegistry.CreatePlaceholderDef(layoutName);
                                SymbolRegistry.RegisterDef(layoutName, placeholderDef);
                            }
                            catch {}
                        }
                    }
                    
                    Log.Message("[KCSG Unbound] Mechanitor Encounters prefixes registered");
                }
                else
                {
                    // If the mod isn't loaded, log this but don't register structures
                    Log.Message("[KCSG Unbound] Mechanitor Encounters mod is NOT loaded - skipping structure registration");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing Mechanitor Encounters layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Helper method to register a prefix with common variations
        /// </summary>
        private static void RegisterWithVariations(string prefix)
        {
            // Use the centralized implementation from RimWorldCompatibility
            RimWorldCompatibility.RegisterWithVariations(prefix);
        }

        /// <summary>
        /// Schedule known mods for priority scanning
        /// </summary>
        private void ScheduleKnownModsForScanning()
        {
            // Register common mod IDs for priority scanning
            string[] knownModIds = new string[]
            {
                "oskar.vfecore", // Vanilla Expanded Core
                "oskar.vfemechanoids", // VFE Mechanoids
                "oskarpotocki.vfe.deserters", // VFE Deserters
                "oskarpotocki.vfe.classical", // VFE Classical
                "oskarpotocki.vfe.insectoids", // VFE Insectoids
                "oskarpotocki.vfe.vikings", // VFE Vikings
                "oskarpotocki.vfe.ancients", // VFE Ancients
                "vanillaexpanded.vfecore", // VE Core
                "brrainz.achtung", // Achtung!
                "frozensnowfox.betterancestraltree", // Better Ancestral Tree
                "vanillaexpanded.vanillatraitsexpanded", // VTE
                "vanillaexpanded.vbookse", // Books Expanded
                "vanillaexpanded.vee", // Vanilla Events Expanded
                "vanillaexpanded.outposts", // Outposts
                "vanillaexpanded.vfemedical", // Medical
                "vanillaexpanded.vfesecurity", // Security
                "vanillaexpanded.vfeproduction", // Production
                "vanillaexpanded.vfeart", // Art
                "vanillaexpanded.vwe", // Weapons Expanded
                "vanillaexpanded.vwel", // Laser Weapons
                "vanillaexpanded.vwehw", // Heavy Weapons
                "vanillaexpanded.vfepropsanddecor", // Props and Decor
                "vanillaexpanded.vfefurniture", // Furniture
                "vanillaexpanded.vfepower", // Power
                "vanillaexpanded.vfefarming", // Farming
                "vanillaexpanded.vfecooking", // Cooking
                "oskarpotocki.vanillaexpanded.royaltypatches", // Royalty Patches
                "oskarpotocki.vanillaexpanded.ideologypatches", // Ideology Patches
                "vanillaexpanded.vwenl", // Non-lethal
                "rimworld.3469398006", // Dead Man's Switch
                "rimworld.3417287863", // Mechanitor Encounters
                "reinforcedmechanoids" // Reinforced Mechanoids 2
            };
            
            // Schedule these mods for scanning with high priority
            foreach (var modId in knownModIds)
            {
                SymbolRegistryCache.AddModToScanQueue(modId, true);
            }
        }

        /// <summary>
        /// Load structures for a specific mod with optimization and caching
        /// </summary>
        private void LoadModSpecificStructures(string modId, ModContentPack mod)
        {
            try
            {
                // Call the specialized method based on mod ID
                if (modId.Contains("vfe") || modId.Contains("vanilla") || modId.Contains("expanded"))
                {
                    // VE family mods
                    if (modId.Contains("deserters") || modId.Contains("3440971742") || modId.Contains("vfedeserters"))
                    {
                        InitializeVFEDesertersLayouts();
                    }
                    else if (modId.Contains("vbge") || modId.Contains("vanillabooksexpanded") || modId.Contains("2193152410"))
                    {
                        InitializeVBGELayouts();
                    }
                    else if (modId.Contains("alphabooks") || modId.Contains("alpha.books") || modId.Contains("3403180654"))
                    {
                        InitializeAlphaBooksLayouts();
                    }
                    else if (modId.Contains("mechanoids") || modId.Contains("vfem"))
                    {
                        InitializeVFEMechanoidLayouts();
                    }
                    else if (modId.Contains("medieval") || modId.Contains("vfemedieval"))
                    {
                        InitializeVFEMedievalLayouts();
                    }
                    else if (modId.Contains("saveourship") || modId.Contains("sos2"))
                    {
                        InitializeSaveOurShip2Layouts();
                    }
                    else if (modId.Contains("outposts") || modId.Contains("vanillaoutposts"))
                    {
                        InitializeVanillaOutpostsLayouts();
                    }
                    else if (modId.Contains("ancients") || modId.Contains("vfeancients"))
                    {
                        InitializeVFEAncientsLayouts();
                    }
                    else if (modId.Contains("insectoids") || modId.Contains("vfeinsectoids"))
                    {
                        InitializeVFEInsectoidsLayouts();
                    }
                    else if (modId.Contains("classical") || modId.Contains("vfeclassical"))
                    {
                        InitializeVFEClassicalLayouts();
                    }
                }
                else if (modId.Contains("fortress") || modId.Contains("ft_") || modId.Contains("ftc_"))
                {
                    InitializeFortressLayouts();
                }
                else if (modId.Contains("deadmanswitch") || modId.Contains("3469398006") || modId.Contains("aoba.deadmanswitch"))
                {
                    InitializeDeadMansSwitchLayouts();
                }
                else if (modId.Contains("mechanitorencounters") || modId.Contains("3417287863"))
                {
                    InitializeMechanitorEncountersLayouts();
                }
                else if (modId.Contains("reinforcedmechanoids") || modId.Contains("rm2"))
                {
                    InitializeReinforcedMechanoidsLayouts();
                }
                else
                {
                    // Generic structure loading for other mods
                    LoadGenericModStructures(modId);
                }
                
                // Schedule this mod for full scanning later if needed
                if (KCSGUnboundSettings.EnableFullScanning)
                {
                    SymbolRegistryCache.AddModToScanQueue(modId, false);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Error loading mod-specific structures for {modId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Register native symbols with the SymbolRegistry
        /// </summary>
        private void RegisterNativeSymbols()
        {
            try
            {
                // Register standard native KCSG symbols
                Log.Message("[KCSG Unbound] Registering native symbols");
                
                // Native symbols from the base KCSG library
                List<string> nativeSymbols = new List<string>
                {
                    // Core structure resolvers
                    "randomDoor", "door", "doubleDoor", "monument", "monumentMarker",
                    "bed", "bedPair", "storage", "bench", "light", "animalNet", "animalBed",
                    "moundDoor", "barricades", "mound", "moundWall", "naturalWall",
                    "smoothNaturalWall", "path", "path_go", "openingSymbol", "newEvent",
                    "empty", "unfoggable", "unroofed", "roofed", "roomHediff", "animalOnly",
                    "droppable", "neverDrop", "itemHediff", "hediff", "neverSpawn",
                    "plantGenerator", "replantable", "colonyCenter", "colonyEdge",
                    
                    // Core style symbols
                    "null", "clear", "natural", "blocked", "none", "forbidden", "unroofed", "water",
                    "marsh", "bridge", "colonyBuilding", "colonyBuildable", "road", "floor", "floored",
                    "wall", "wallStuff", "wallType", "door", "window", "column", "lamp", "furniture",
                    "building", "bridge", "wallLight", "indoorLamp", "outdoorLamp", "roofSupport",
                    
                    // Faction and structure markers
                    "factionTheme", "factionBase", "factionCenter", "factionBorder", 
                    "barrier", "borderBarrier", "borderWall", "borderFence", "borderHedge",
                    "factionDefense", "factionStorage", "factionPower", "factionResource",
                    "factionProduction", "factionMilitary", "factionMedical", "factionResearch",
                    "factionLiving", "factionRecreation", "factionReligion", "factionPrisoner",
                    "factionSlave", "factionAnimal", "factionFarm", "factionWorkshop"
                };
                
                // Register each native symbol
                foreach (var symbol in nativeSymbols)
                {
                    if (!SymbolRegistry.IsDefRegistered(symbol))
                    {
                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(symbol);
                        SymbolRegistry.RegisterDef(symbol, placeholderDef);
                    }
                }
                
                Log.Message($"[KCSG Unbound] Registered {nativeSymbols.Count} native symbols");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error registering native symbols: {ex}");
            }
        }

        // Add missing InitializeFortressLayouts
        private static void InitializeFortressLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for Fortress/Citadel layouts");
                
                // Register FT_ and FTC_ prefixes for Fortress/Citadel mods
                string[] ftPrefixes = new[] {
                    "FT_", "FTC_", "Fortress_", "Citadel_", "FT_BlocksConcrete", 
                    "FTC_CitadelWall", "FTC_CitadelBlock", "FTC_Citadel"
                };
                
                foreach (var prefix in ftPrefixes)
                {
                    RegisterWithVariations(prefix);
                }
                
                // Also register compound prefixes known to be used
                string[] compoundNames = new[]
                {
                    "FTC_CitadelWall_FT_BlocksConcrete",
                    "FTC_CitadelBlock_FT_BlocksConcrete", 
                    "FT_BlocksConcrete_Wall",
                    "FT_BlocksConcrete_Floor",
                    "FT_BlocksConcrete_Door"
                };
                
                foreach(var name in compoundNames)
                {
                    if (!SymbolRegistry.IsDefRegistered(name))
                    {
                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(name);
                        SymbolRegistry.RegisterDef(name, placeholderDef);
                    }
                }
                
                Log.Message("[KCSG Unbound] Fortress/Citadel prefixes registered");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing Fortress/Citadel layouts: {ex}");
            }
        }

        // Add missing PreregisterCommonStructureNames method
        /// <summary>
        /// Pre-register common structure names with prefixes
        /// </summary>
        private void PreregisterCommonStructureNames(string prefix)
        {
            // Skip if empty prefix
            if (string.IsNullOrEmpty(prefix)) return;
            
            // Common structure types to register
            string[] structureTypes = new[] {
                "Structure", "Layout", "Building", "Base", "Camp", "Settlement", 
                "Outpost", "Hive", "Nest", "Site", "Bunker", "Tower"
            };
            
            // Register each combination
            foreach (var type in structureTypes)
            {
                string defName = $"{prefix}{type}";
                if (!SymbolRegistry.IsDefRegistered(defName))
                {
                    try
                    {
                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                    }
                    catch {}
                }
                
                // Also register numbered variants 1-5
                for (int i = 1; i <= 5; i++)
                {
                    string numberedName = $"{defName}{i}";
                    if (!SymbolRegistry.IsDefRegistered(numberedName))
                    {
                        try
                        {
                            object placeholderDef = SymbolRegistry.CreatePlaceholderDef(numberedName);
                            SymbolRegistry.RegisterDef(numberedName, placeholderDef);
                        }
                        catch {}
                    }
                }
                
                // Also register lettered variants A-E
                for (char c = 'A'; c <= 'E'; c++)
                {
                    string letteredName = $"{defName}{c}";
                    if (!SymbolRegistry.IsDefRegistered(letteredName))
                    {
                        try
                        {
                            object placeholderDef = SymbolRegistry.CreatePlaceholderDef(letteredName);
                            SymbolRegistry.RegisterDef(letteredName, placeholderDef);
                        }
                        catch {}
                    }
                }
            }
        }

        /// <summary>
        /// Load generic mod structures by using the SafeStart implementation
        /// </summary>
        private void LoadGenericModStructures(string modId)
        {
            // Use SafeStart's implementation for generic mod loading
            SafeStart.LoadGenericModStructures(modId);
        }
    }
} 