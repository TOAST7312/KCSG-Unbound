using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;
using System.Linq;
using System.IO;

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
                
                Log.Message("════════════════════════════════════════════════════");
                Log.Message("║ [KCSG Unbound] Early initialization              ║");
                Log.Message("════════════════════════════════════════════════════");
                
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
                
                // Preload common KCSG structure layouts
                PreloadStructureLayouts();
                
                // Explicitly initialize Vanilla Factions Expanded - Deserters layouts
                InitializeVFEDesertersLayouts();
                
                // Initialize Vanilla Base Generation Expanded layouts
                InitializeVBGELayouts();
                
                // Initialize Alpha Books layouts
                InitializeAlphaBooksLayouts();
                
                // Initialize VFE Mechanoids layouts
                InitializeVFEMechanoidLayouts();
                
                // Initialize VFE Medieval layouts
                InitializeVFEMedievalLayouts();
                
                // Initialize Save Our Ship 2 layouts
                InitializeSaveOurShip2Layouts();
                
                // Initialize Vanilla Outposts Expanded layouts
                InitializeVanillaOutpostsLayouts();
                
                // Initialize VFE Ancients and Vault layouts
                InitializeVFEAncientsLayouts();
                
                // Initialize VFE Insectoids layouts
                InitializeVFEInsectoidsLayouts();
                
                // Initialize VFE Classical layouts
                InitializeVFEClassicalLayouts();
                
                // Initialize VFE Empire layouts
                InitializeVFEEmpireLayouts();
                
                // We'll only mark as successful if we get this far
                initializationSuccess = true;
                
                stopwatch.Stop();
                CurrentStatus = $"Loaded ({stopwatch.ElapsedMilliseconds}ms)";
                
                // No more relying on LongEventHandler - we want to be EARLY
                Log.Message($"[KCSG Unbound] Early initialization complete in {stopwatch.ElapsedMilliseconds}ms");
                Diagnostics.LogDiagnostic("Early initialization complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error during mod initialization: {ex}");
                Diagnostics.LogDiagnostic($"Error during initialization: {ex}");
                CurrentStatus = "Error during initialization";
            }
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
                    Log.Message("[KCSG] Registry initialization complete in constructor");
                }
                
                Log.Message("══════════════════════════════════════════════════");
                Log.Message("║          KCSG UNBOUND - EARLY SETUP           ║");
                Log.Message("║        Symbol bypass mod for RimWorld 1.5      ║");
                Log.Message("══════════════════════════════════════════════════");
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
                    "RBME_", "AG_", "BM_", "BS_", "MM_", "VC_", "VE_", "VM_", "VBGE_"
                };
                
                // Get common base names from the log file cross-references
                string[] commonBaseNames = new[] { 
                    "CitadelBunkerStart", "LargeBallroomA", "SurveillanceStationF", "ServantQuartersA",
                    "GrandNobleThroneRoomA", "LargeNobleBedroomA", "TechPrinterMainA", "UnderfarmMainA",
                    "ShuttleLandingPadA", "AerodroneStationA", "ImperialConvoyA", "StockpileDepotA"
                };
                
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
                            }
                            catch {}
                            
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
                                    }
                                }
                                catch {}
                            }
                        }
                    }
                }
                
                Log.Message($"[KCSG Unbound] Pre-registered common layout names, total defs: {SymbolRegistry.RegisteredDefCount}");
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
                Log.Message("[KCSG Unbound] Setting up Harmony patches in constructor");
                
                // Apply patches directly
                HarmonyPatches.ApplyPatches(harmony);
                
                Log.Message("[KCSG Unbound] Harmony patches applied successfully in constructor");
                Log.Message("════════════════════════════════════════════════════");
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
                Log.Message("[KCSG Unbound] Implementing enhanced registration for VFE Deserters layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for VFE Deserters layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VFE-DESERTERS MOD BEING LOADED
                bool desertersModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Deserter") || 
                    m.PackageId.Contains("3025493377") || 
                    m.PackageId.Contains("oskar.vfe.deserter"));
                    
                if (desertersModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Deserters mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    Log.Message("[KCSG Unbound] VFE Deserters mod is NOT directly loaded, but still registering structures as fallback");
                }
                
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
                            Log.Message($"[KCSG Unbound] Registered {defName} with hash {hash}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error calculating short hashes: {ex.Message}");
                }
                
                Log.Message($"[KCSG Unbound] Registered {count} critical VFE Deserters layouts using enhanced methods");
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
                }
                else
                {
                    Log.Message("[KCSG Unbound] VBGE mod is NOT directly loaded, but still registering structures as fallback");
                }
                
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
                    Log.Message("[KCSG Unbound] Alpha Books mod is loaded - implementing structure registration");
                }
                else
                {
                    Log.Message("[KCSG Unbound] Alpha Books mod is NOT directly loaded, but still registering structures as fallback");
                }
                
                // Track the defs we create
                int count = 0;
                HashSet<string> registeredNames = new HashSet<string>();
                
                // SCAN XML FILES DIRECTLY - for Alpha Books we'll focus on scanning files
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
                        "294100/3403180654", // Workshop folder format
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
                        
                        // Focus on the CustomGenDefs folder specifically
                        string customGenDefsFolder = Path.Combine(alphaBooksModFolder, "1.5", "Defs", "CustomGenDefs");
                        if (Directory.Exists(customGenDefsFolder))
                        {
                            // SCAN through all XML files in this folder for symbols and libraries
                            foreach (var xmlFile in Directory.GetFiles(customGenDefsFolder, "*.xml"))
                            {
                                try
                                {
                                    string xmlContent = File.ReadAllText(xmlFile);
                                    
                                    // Parse for defName elements
                                    int pos = 0;
                                    while (true)
                                    {
                                        int defNameStart = xmlContent.IndexOf("<defName>", pos);
                                        if (defNameStart == -1) break;
                                        
                                        int defNameEnd = xmlContent.IndexOf("</defName>", defNameStart);
                                        if (defNameEnd == -1) break;
                                        
                                        string defName = xmlContent.Substring(defNameStart + 9, defNameEnd - defNameStart - 9);
                                        
                                        // Register all def names found, as they might be symbols or libraries
                                        if (!registeredNames.Contains(defName))
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
                                                Log.Warning($"[KCSG Unbound] Failed to register Alpha Books def {defName}: {ex.Message}");
                                            }
                                        }
                                        
                                        // Move to next part of file
                                        pos = defNameEnd;
                                    }
                                    
                                    // Also scan for symbols in the file that might not have defName elements
                                    string[] symbolMarkers = new[] { 
                                        "<symbol>", "<root>", "<path>", "<symbolPart>"
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
                                    Log.Warning($"[KCSG Unbound] Error processing Alpha Books file {xmlFile}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            Log.Warning($"[KCSG Unbound] Alpha Books CustomGenDefs folder not found at expected location {customGenDefsFolder}");
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
                
                Log.Message($"[KCSG Unbound] Registered {count} Alpha Books symbols and layouts");
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
                }
                else
                {
                    Log.Message("[KCSG Unbound] VFE Mechanoids mod is NOT directly loaded, but still registering structures as fallback");
                }
                
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
                                    Log.Message($"[KCSG Unbound] Processing VFE Mechanoids file: {Path.GetFileName(xmlFile)}");
                                    
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
                
                Log.Message($"[KCSG Unbound] Registered {count} VFE Mechanoids layouts using enhanced methods");
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
                    m.PackageId.Contains("3444347874") || // Medieval 2
                    m.PackageId.Contains("oskarpotocki.vfe.medieval") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.medievalmodule"));
                    
                if (vfeMedievalModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Medieval mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    Log.Message("[KCSG Unbound] VFE Medieval mod is NOT directly loaded, but still registering structures as fallback");
                }
                
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
                
                // CHECK FOR SOS2 MOD BEING LOADED
                bool sos2ModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Save Our Ship") || 
                    m.Name.Contains("SOS2") ||
                    m.PackageId.Contains("1909914131") || 
                    m.PackageId.Contains("ludeon.rimworld.shipshavecomeback"));
                    
                if (sos2ModLoaded)
                {
                    Log.Message("[KCSG Unbound] Save Our Ship 2 mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    Log.Message("[KCSG Unbound] Save Our Ship 2 mod is NOT directly loaded, but still registering structures as fallback");
                }
                
                // COMPREHENSIVE LIST OF ALL KNOWN SOS2 STRUCTURES
                List<string> criticalBaseNames = new List<string>
                {
                    // Ship parts/sections
                    "ShipSection", "ShipHull", "ShipBridge", "ShipEngineRoom", "ShipReactor",
                    "ShipHangar", "ShipCrew", "ShipCargo", "ShipMedical", "ShipDefense",
                    "ShipWeapons", "ShipLiving", "ShipGenerators", "ShipThrusters",
                    
                    // Ship types
                    "Shuttle", "Corvette", "Frigate", "Destroyer", "Cruiser", "Battleship",
                    "Dreadnought", "CargoShip", "ColonyShip", "MilitaryShip", "ScienceShip",
                    
                    // Derelict ships
                    "DerelictShip", "AbandonedShip", "AncientShip", "ShipWreckage",
                    
                    // Specific components
                    "ShipBridge", "ShipComputer", "ShipSensor", "ShipShield", "ShipHeatSink",
                    "ShipCryptosleep", "ShipReactor", "ShipEngine", "ShipCapacitor", "ShipTurret"
                };
                
                // Track the defs we create
                int count = 0;
                HashSet<string> registeredNames = new HashSet<string>();
                
                // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                foreach (string baseName in criticalBaseNames)
                {
                    // PRIMARY NAMING: SOS2_BaseName
                    string primaryDefName = $"SOS2_{baseName}";
                    string alternateDefName = $"SaveOurShip_{baseName}";
                    string shortDefName = $"SOS_{baseName}";
                    
                    // NAMING PATTERNS - These are all the formats that could be used for references
                    List<string> allNamingPatterns = new List<string>();
                    
                    // 1. Standard patterns with different prefixes
                    allNamingPatterns.Add(primaryDefName);
                    allNamingPatterns.Add(alternateDefName);
                    allNamingPatterns.Add(shortDefName);
                    allNamingPatterns.Add($"Ship_{baseName}");
                    allNamingPatterns.Add(baseName);
                    
                    // 2. Numbered variants (1-20) for all structures
                    for (int i = 1; i <= 20; i++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{i}");
                        allNamingPatterns.Add($"{alternateDefName}{i}");
                        allNamingPatterns.Add($"{shortDefName}{i}");
                        allNamingPatterns.Add($"Ship_{baseName}{i}");
                    }
                    
                    // 3. Letter suffixes (A-Z) - standard variation for most structures
                    for (char letter = 'A'; letter <= 'Z'; letter++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{letter}");
                        allNamingPatterns.Add($"{alternateDefName}{letter}");
                        allNamingPatterns.Add($"{shortDefName}{letter}");
                        allNamingPatterns.Add($"Ship_{baseName}{letter}");
                    }
                    
                    // 4. Common suffix variations
                    string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex", "Small", "Medium", "Large" };
                    foreach (string suffix in suffixes)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{suffix}");
                        allNamingPatterns.Add($"{alternateDefName}{suffix}");
                        allNamingPatterns.Add($"{shortDefName}{suffix}");
                        allNamingPatterns.Add($"Ship_{baseName}{suffix}");
                        allNamingPatterns.Add($"{baseName}{suffix}");
                    }
                    
                    // 5. Alternative prefixing styles that some mods might use
                    string[] prefixingStyles = new[] 
                    {
                        $"Structure_SOS2_{baseName}", 
                        $"Layout_SOS2_{baseName}",
                        $"StructureLayout_SOS2_{baseName}",
                        $"SOS2_Structure_{baseName}",
                        $"SOS2_Layout_{baseName}",
                        $"SOS2.{baseName}",
                        $"Ship.{baseName}"
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
                    string sos2ModFolder = null;
                    
                    // Check common paths where SOS2 might be installed
                    string[] possiblePaths = new[]
                    {
                        "Mods/Save Our Ship 2",
                        "Mods/SaveOurShip2",
                        "Mods/SOS2",
                        "Mods/1909914131", // Steam Workshop ID
                        "294100/1909914131", // Workshop folder format
                        Path.Combine(GenFilePaths.ModsFolderPath, "Save Our Ship 2"),
                        Path.Combine(GenFilePaths.ModsFolderPath, "SaveOurShip2"),
                        Path.Combine(GenFilePaths.ModsFolderPath, "SOS2"),
                        Path.Combine(GenFilePaths.ModsFolderPath, "1909914131")
                    };
                    
                    foreach (var path in possiblePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            sos2ModFolder = path;
                            break;
                        }
                    }
                    
                    if (sos2ModFolder != null)
                    {
                        Log.Message($"[KCSG Unbound] Found Save Our Ship 2 mod folder at {sos2ModFolder}");
                        
                        // Try to find the structure generation folders - there are a few possibilities
                        string[] possibleDefFolders = new[]
                        {
                            Path.Combine(sos2ModFolder, "1.5", "Defs", "ShipDefs"),
                            Path.Combine(sos2ModFolder, "1.5", "Defs", "ShipStructureDefs"),
                            Path.Combine(sos2ModFolder, "1.5", "Defs", "CustomGenDefs"),
                            Path.Combine(sos2ModFolder, "1.5", "Defs", "Gen"),
                            Path.Combine(sos2ModFolder, "Defs", "ShipDefs"),
                            Path.Combine(sos2ModFolder, "Defs", "ShipStructureDefs"),
                            Path.Combine(sos2ModFolder, "Defs", "Gen")
                        };
                        
                        bool foundAnyDefFolder = false;
                        
                        foreach (var defFolder in possibleDefFolders)
                        {
                            if (Directory.Exists(defFolder))
                            {
                                foundAnyDefFolder = true;
                                Log.Message($"[KCSG Unbound] Found SOS2 ship structure folder at {defFolder}");
                                
                                // RECURSIVELY scan all XML files in this folder for structure layouts
                                foreach (var xmlFile in Directory.GetFiles(defFolder, "*.xml", SearchOption.AllDirectories))
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
                                            
                                            // Register all def names from SOS2 XML files
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
                                            "<symbol>", "<symbolDef>", "<name>", "<root>", "<path>", "<shipPart>"
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
                        }
                        
                        if (!foundAnyDefFolder)
                        {
                            Log.Warning($"[KCSG Unbound] No specific ship structure folders found in SOS2 mod - scanning entire mod folder");
                            
                            // Try to find any XML files that might contain structure layouts
                            var xmlFiles = Directory.GetFiles(sos2ModFolder, "*.xml", SearchOption.AllDirectories)
                                .Where(f => Path.GetFileName(f).Contains("Ship") || 
                                           Path.GetFileName(f).Contains("Structure") ||
                                           Path.GetFileName(f).Contains("Vessel") ||
                                           Path.GetFileName(f).Contains("SOS2") ||
                                           Path.GetFileName(f).Contains("Derelict") ||
                                           Path.GetFileName(f).Contains("Gen"));
                                           
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
                                        
                                        // Register all structure definitions from ship-related files
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
                        Log.Message("[KCSG Unbound] Save Our Ship 2 mod folder not found directly - relying on preregistered templates only");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                }
                
                Log.Message($"[KCSG Unbound] Registered {count} Save Our Ship 2 layouts using enhanced methods");
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
                    Log.Message("[KCSG Unbound] Vanilla Outposts Expanded mod is NOT directly loaded, but still registering structures as fallback");
                }
                
                // COMPREHENSIVE LIST OF ALL KNOWN OUTPOST STRUCTURES
                List<string> criticalBaseNames = new List<string>
                {
                    // Outpost base types
                    "Outpost", "MiningOutpost", "FarmingOutpost", "LoggingOutpost",
                    "ResearchOutpost", "TradingOutpost", "MilitaryOutpost", "PowerOutpost",
                    "FishingOutpost", "ChemfuelOutpost", "HuntingOutpost", "FactoryOutpost",
                    
                    // Structure types
                    "OutpostBuilding", "OutpostWalls", "OutpostDefense", "OutpostEntrance",
                    "OutpostStorage", "OutpostPower", "OutpostMain", "OutpostBarracks",
                    "OutpostCore", "OutpostPerimeter", "OutpostCommand", "OutpostResearch",
                    
                    // Specific faction outposts
                    "ImperialOutpost", "TribalOutpost", "PirateOutpost", "MechanoidOutpost",
                    "InsectoidOutpost", "OutlanderOutpost",
                    
                    // Special symbols
                    "VOE_Symbol", "OutpostSymbol"
                };
                
                // Track the defs we create
                int count = 0;
                HashSet<string> registeredNames = new HashSet<string>();
                
                // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                foreach (string baseName in criticalBaseNames)
                {
                    // PRIMARY NAMING: VOE_BaseName
                    string primaryDefName = $"VOE_{baseName}";
                    string alternateDefName = $"VE_Outposts_{baseName}";
                    
                    // NAMING PATTERNS - These are all the formats that could be used for references
                    List<string> allNamingPatterns = new List<string>();
                    
                    // 1. Standard patterns with different prefixes
                    allNamingPatterns.Add(primaryDefName);
                    allNamingPatterns.Add(alternateDefName);
                    allNamingPatterns.Add($"Outpost_{baseName}");
                    allNamingPatterns.Add(baseName);
                    
                    // 2. Numbered variants (1-20) for all structures
                    for (int i = 1; i <= 20; i++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{i}");
                        allNamingPatterns.Add($"{alternateDefName}{i}");
                        allNamingPatterns.Add($"Outpost_{baseName}{i}");
                    }
                    
                    // 3. Letter suffixes (A-Z) - standard variation for most structures
                    for (char letter = 'A'; letter <= 'Z'; letter++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{letter}");
                        allNamingPatterns.Add($"{alternateDefName}{letter}");
                        allNamingPatterns.Add($"Outpost_{baseName}{letter}");
                    }
                    
                    // 4. Common suffix variations
                    string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex", "Small", "Medium", "Large" };
                    foreach (string suffix in suffixes)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{suffix}");
                        allNamingPatterns.Add($"{alternateDefName}{suffix}");
                        allNamingPatterns.Add($"Outpost_{baseName}{suffix}");
                        allNamingPatterns.Add($"{baseName}{suffix}");
                    }
                    
                    // 5. Alternative prefixing styles that some mods might use
                    string[] prefixingStyles = new[] 
                    {
                        $"Structure_VOE_{baseName}", 
                        $"Layout_VOE_{baseName}",
                        $"StructureLayout_VOE_{baseName}",
                        $"VOE_Structure_{baseName}",
                        $"VOE_Layout_{baseName}",
                        $"VOE.{baseName}",
                        $"Outpost.{baseName}"
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
                    string voeModFolder = null;
                    
                    // Check common paths where Vanilla Outposts Expanded might be installed
                    string[] possiblePaths = new[]
                    {
                        "Mods/Vanilla Outposts Expanded",
                        "Mods/VanillaOutpostsExpanded",
                        "Mods/VOE",
                        "Mods/VE Outposts",
                        "Mods/oskarpotocki.vfe.outposts",
                        "Mods/vanillaexpanded.outposts",
                        "Mods/2688941031", // Steam Workshop ID
                        "294100/2688941031", // Workshop folder format
                        Path.Combine(GenFilePaths.ModsFolderPath, "Vanilla Outposts Expanded"),
                        Path.Combine(GenFilePaths.ModsFolderPath, "VanillaOutpostsExpanded"),
                        Path.Combine(GenFilePaths.ModsFolderPath, "VOE"),
                        Path.Combine(GenFilePaths.ModsFolderPath, "VE Outposts"),
                        Path.Combine(GenFilePaths.ModsFolderPath, "oskarpotocki.vfe.outposts"),
                        Path.Combine(GenFilePaths.ModsFolderPath, "vanillaexpanded.outposts"),
                        Path.Combine(GenFilePaths.ModsFolderPath, "2688941031")
                    };
                    
                    foreach (var path in possiblePaths)
                    {
                        if (Directory.Exists(path))
                        {
                            voeModFolder = path;
                            break;
                        }
                    }
                    
                    if (voeModFolder != null)
                    {
                        Log.Message($"[KCSG Unbound] Found Vanilla Outposts Expanded mod folder at {voeModFolder}");
                        
                        // Try to find the structure generation folders - there are a few possibilities
                        string[] possibleDefFolders = new[]
                        {
                            Path.Combine(voeModFolder, "1.5", "Defs", "OutpostDefs"),
                            Path.Combine(voeModFolder, "1.5", "Defs", "OutpostStructureDefs"),
                            Path.Combine(voeModFolder, "1.5", "Defs", "CustomGenDefs"),
                            Path.Combine(voeModFolder, "1.5", "Defs", "Gen"),
                            Path.Combine(voeModFolder, "Defs", "OutpostDefs"),
                            Path.Combine(voeModFolder, "Defs", "OutpostStructureDefs"),
                            Path.Combine(voeModFolder, "Defs", "CustomGenDefs"),
                            Path.Combine(voeModFolder, "Defs", "Gen")
                        };
                        
                        bool foundAnyDefFolder = false;
                        
                        foreach (var defFolder in possibleDefFolders)
                        {
                            if (Directory.Exists(defFolder))
                            {
                                foundAnyDefFolder = true;
                                Log.Message($"[KCSG Unbound] Found Vanilla Outposts Expanded structure folder at {defFolder}");
                                
                                // RECURSIVELY scan all XML files in this folder for structure layouts
                                foreach (var xmlFile in Directory.GetFiles(defFolder, "*.xml", SearchOption.AllDirectories))
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
                                            
                                            // Register all def names from VOE XML files
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
                                            "<symbol>", "<symbolDef>", "<name>", "<root>", "<path>", "<outpostPart>"
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
                        }
                        
                        if (!foundAnyDefFolder)
                        {
                            Log.Warning($"[KCSG Unbound] No specific outpost structure folders found in VOE mod - scanning entire mod folder");
                            
                            // Try to find any XML files that might contain structure layouts
                            var xmlFiles = Directory.GetFiles(voeModFolder, "*.xml", SearchOption.AllDirectories)
                                .Where(f => Path.GetFileName(f).Contains("Outpost") || 
                                           Path.GetFileName(f).Contains("VOE") ||
                                           Path.GetFileName(f).Contains("Structure") ||
                                           Path.GetFileName(f).Contains("Layout") ||
                                           Path.GetFileName(f).Contains("Settlement") ||
                                           Path.GetFileName(f).Contains("Gen"));
                                           
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
                                        
                                        // Register all structure definitions from outpost-related files
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
                        Log.Message("[KCSG Unbound] Vanilla Outposts Expanded mod folder not found directly - relying on preregistered templates only");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                }
                
                Log.Message($"[KCSG Unbound] Registered {count} Vanilla Outposts Expanded layouts using enhanced methods");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing Vanilla Outposts Expanded layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Explicitly initializes and registers VFE Ancients and Vault layouts
        /// </summary>
        private static void InitializeVFEAncientsLayouts()
        {
            try
            {
                Log.Message("[KCSG Unbound] Implementing enhanced registration for VFE Ancients and Vault layouts");
                
                if (!SymbolRegistry.Initialized)
                {
                    Log.Message("[KCSG Unbound] Initializing SymbolRegistry for VFE Ancients and Vault layouts");
                    SymbolRegistry.Initialize();
                }
                
                // CHECK FOR VFE ANCIENTS MOD BEING LOADED
                bool ancientsModLoaded = LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Ancients") || 
                    m.PackageId.Contains("3444347874") || // Medieval 2
                    m.PackageId.Contains("oskarpotocki.vfe.ancients") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.ancientsmodule"));
                    
                if (ancientsModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Ancients mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    Log.Message("[KCSG Unbound] VFE Ancients mod is NOT directly loaded, but still registering structures as fallback");
                }
                
                // COMPREHENSIVE LIST OF ALL KNOWN VFE ANCIENTS STRUCTURES
                List<string> criticalBaseNames = new List<string>
                {
                    // Base ancient structures
                    "AncientHouse", "AncientTent", "AncientKeep", "AncientCastle", "AncientLabratory",
                    "AncientTemple", "AncientFarm", "AncientSlingshot", "AncientVault", "AncientRuin",
                    "Tower", "Hall", "Barracks", "Stable", "Blacksmith", "Church", "Sanctum",
                    "Tavern", "Market", "Farm", "Laboratory", "Walls", "Gate", "Monument",
                    
                    // Special symbols and structures
                    "Symbol", "AncientSymbol", "AbandonedSlingshot", "LootedVault", "SealedVault"
                };
                
                // Additional vault types from sub-mods
                List<string> vaultTypes = new List<string>
                {
                    // Animal vaults from Lost Vaults
                    "Kilo1", "Kilo2", "Kilo3", "Crow", "Badger", "Bear", "Mole", "Fox", 
                    "Mouse", "Ox", "Turtle", "Eagle", "Owl",
                    
                    // Specialized vaults from Even More Vaults
                    "GeneBank", "Outpost", "Warehouse", "AgriculturalResearch",
                    
                    // Tree vaults from Soups Vault Collection
                    "Redwood", "Mangrove", "Bonsai", "Cedar", "Magnolia", "Oak", "Sequoia",
                    "Sycamore", "Manuka", "Maple", "Pando", "Blackwood", "Bristlecone", "Birch"
                };
                
                // Generate all vault-specific naming patterns
                foreach (var vaultType in vaultTypes)
                {
                    // Common patterns for vault names
                    criticalBaseNames.Add($"SealedVault{vaultType}");
                    criticalBaseNames.Add($"VFEA_SV_{vaultType}Vault");
                    criticalBaseNames.Add($"VFEA_Sealed{vaultType}Vault");
                    criticalBaseNames.Add($"SealedVault_{vaultType}");
                    criticalBaseNames.Add($"Vault{vaultType}");
                    criticalBaseNames.Add($"{vaultType}Vault");
                }
                
                // Track the defs we create
                int count = 0;
                HashSet<string> registeredNames = new HashSet<string>();
                
                // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                foreach (string baseName in criticalBaseNames)
                {
                    // PRIMARY NAMING: VFEA_BaseName or VFE_Ancients_BaseName
                    string primaryDefName = $"VFEA_{baseName}";
                    string alternateDefName = $"VFE_Ancients_{baseName}";
                    
                    // NAMING PATTERNS - These are all the formats that could be used for references
                    List<string> allNamingPatterns = new List<string>();
                    
                    // 1. Standard patterns with different prefixes
                    allNamingPatterns.Add(primaryDefName);
                    allNamingPatterns.Add(alternateDefName);
                    allNamingPatterns.Add($"Ancient_{baseName}");
                    allNamingPatterns.Add(baseName);
                    
                    // 2. Numbered variants (1-20) for all structures
                    for (int i = 1; i <= 20; i++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{i}");
                        allNamingPatterns.Add($"{alternateDefName}{i}");
                        allNamingPatterns.Add($"Ancient_{baseName}{i}");
                    }
                    
                    // 3. Letter suffixes (A-Z) - standard variation for most structures
                    for (char letter = 'A'; letter <= 'Z'; letter++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{letter}");
                        allNamingPatterns.Add($"{alternateDefName}{letter}");
                        allNamingPatterns.Add($"Ancient_{baseName}{letter}");
                    }
                    
                    // 4. Common suffix variations
                    string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex", "Small", "Medium", "Large" };
                    foreach (string suffix in suffixes)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{suffix}");
                        allNamingPatterns.Add($"{alternateDefName}{suffix}");
                        allNamingPatterns.Add($"Ancient_{baseName}{suffix}");
                        allNamingPatterns.Add($"{baseName}{suffix}");
                    }
                    
                    // 5. Alternative prefixing styles that some mods might use
                    string[] prefixingStyles = new[] 
                    {
                        $"Structure_VFEA_{baseName}", 
                        $"Layout_VFEA_{baseName}",
                        $"StructureLayout_VFEA_{baseName}",
                        $"VFEA_Structure_{baseName}",
                        $"VFEA_Layout_{baseName}",
                        $"VFEA.{baseName}",
                        $"Ancient.{baseName}"
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
                    // Main VFE Ancients mod (2654846754)
                    ScanAncientsMod("Vanilla Factions Expanded - Ancients", "VFE - Ancients", "2654846754", 
                                   registeredNames, ref count);
                    
                    // VFE - Ancients: Lost Vaults (2946113288)
                    ScanAncientsMod("VFE - Ancients : Lost Vaults", "VFE Ancients Lost Vaults", "2946113288", 
                                   registeredNames, ref count);
                    
                    // VFE - Ancients: Even More Vaults (3160710884)
                    ScanAncientsMod("VFE - Ancients: Even More Vaults", "VFE Ancients Even More Vaults", "3160710884", 
                                   registeredNames, ref count);
                    
                    // VFE - Ancients: Soups Vault Collection (3325594457)
                    ScanAncientsMod("VFE - Ancients: Soups Vault Collection", "VFE Ancients Soups Vault Collection", "3325594457", 
                                   registeredNames, ref count);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                }
                
                Log.Message($"[KCSG Unbound] Registered {count} VFE Ancients layouts using enhanced methods");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VFE Ancients layouts: {ex}");
            }
        }
        
        /// <summary>
        /// Scans a VFE Ancients or related mod for structure layouts
        /// </summary>
        private static void ScanAncientsMod(string modName, string altModName, string workshopId, 
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
                                    "<symbol>", "<symbolDef>", "<name>", "<root>", "<path>", "<vaultName>"
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
                            .Where(f => Path.GetFileName(f).Contains("Vault") || 
                                       Path.GetFileName(f).Contains("Ancient") || 
                                       Path.GetFileName(f).Contains("VFEA") ||
                                       Path.GetFileName(f).Contains("Symbol") ||
                                       Path.GetFileName(f).Contains("Sealed"));
                                    
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
                    m.PackageId.Contains("3309003431") || 
                    m.PackageId.Contains("oskarpotocki.vfe.insectoid") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.insectoid"));
                    
                if (insectoidsModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Insectoids mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    Log.Message("[KCSG Unbound] VFE Insectoids mod is NOT directly loaded, but still registering structures as fallback");
                }
                
                // COMPREHENSIVE LIST OF ALL KNOWN VFE INSECTOIDS STRUCTURES
                List<string> criticalBaseNames = new List<string>
                {
                    // Main structure types
                    "Infestation", "Hive", "Nest", "Tunnel", "Cavern", "Chamber", 
                    "Formation", "InsectoidDen", "InsectoidSetup", "TunnelSystem",
                    
                    // Special symbols and structures
                    "Symbol", "InsectoidSymbol", "VFEI_Symbol"
                };
                
                // Track the defs we create
                int count = 0;
                HashSet<string> registeredNames = new HashSet<string>();
                
                // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                foreach (string baseName in criticalBaseNames)
                {
                    // PRIMARY NAMING: VFEI_BaseName or VFE_Insectoids_BaseName
                    string primaryDefName = $"VFEI_{baseName}";
                    string alternateDefName = $"VFE_Insectoids_{baseName}";
                    
                    // NAMING PATTERNS - These are all the formats that could be used for references
                    List<string> allNamingPatterns = new List<string>();
                    
                    // 1. Standard patterns with different prefixes
                    allNamingPatterns.Add(primaryDefName);
                    allNamingPatterns.Add(alternateDefName);
                    allNamingPatterns.Add($"Insectoid_{baseName}");
                    allNamingPatterns.Add(baseName);
                    
                    // 2. Numbered variants (1-20) for all structures
                    for (int i = 1; i <= 20; i++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{i}");
                        allNamingPatterns.Add($"{alternateDefName}{i}");
                        allNamingPatterns.Add($"Insectoid_{baseName}{i}");
                    }
                    
                    // 3. Letter suffixes (A-Z) - standard variation for most structures
                    for (char letter = 'A'; letter <= 'Z'; letter++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{letter}");
                        allNamingPatterns.Add($"{alternateDefName}{letter}");
                        allNamingPatterns.Add($"Insectoid_{baseName}{letter}");
                    }
                    
                    // 4. Common suffix variations
                    string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex", "Small", "Medium", "Large" };
                    foreach (string suffix in suffixes)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{suffix}");
                        allNamingPatterns.Add($"{alternateDefName}{suffix}");
                        allNamingPatterns.Add($"Insectoid_{baseName}{suffix}");
                        allNamingPatterns.Add($"{baseName}{suffix}");
                    }
                    
                    // 5. Alternative prefixing styles that some mods might use
                    string[] prefixingStyles = new[] 
                    {
                        $"Structure_VFEI_{baseName}", 
                        $"Layout_VFEI_{baseName}",
                        $"StructureLayout_VFEI_{baseName}",
                        $"VFEI_Structure_{baseName}",
                        $"VFEI_Layout_{baseName}",
                        $"VFEI.{baseName}",
                        $"Insectoid.{baseName}"
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
                
                // SCAN XML FILES DIRECTLY
                try
                {
                    // VFE Insectoids mod (3309003431)
                    ScanVFEMod("Vanilla Factions Expanded - Insectoids", "VFE - Insectoids", "3309003431", 
                                    registeredNames, ref count);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                }
                
                Log.Message($"[KCSG Unbound] Registered {count} VFE Insectoids layouts using enhanced methods");
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
                    m.PackageId.Contains("2787850474") || 
                    m.PackageId.Contains("oskarpotocki.vfe.classical") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.classical"));
                    
                if (classicalModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Classical mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    Log.Message("[KCSG Unbound] VFE Classical mod is NOT directly loaded, but still registering structures as fallback");
                }
                
                // COMPREHENSIVE LIST OF ALL KNOWN VFE CLASSICAL STRUCTURES
                List<string> criticalBaseNames = new List<string>
                {
                    // Main structure types
                    "ClassicalCamp", "ClassicalSettlement", "Camp", "Settlement", "Temple", 
                    "Villa", "Amphitheater", "PublicBaths", "Forum", "Barracks", "Market",
                    "Thermae", "Basilica", "Palace", "Insula", "Domus", "Circus", "Theater",
                    
                    // Special symbols and structures
                    "Symbol", "ClassicalSymbol", "VFEC_Symbol"
                };
                
                // Track the defs we create
                int count = 0;
                HashSet<string> registeredNames = new HashSet<string>();
                
                // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                foreach (string baseName in criticalBaseNames)
                {
                    // PRIMARY NAMING: VFEC_BaseName or VFE_Classical_BaseName
                    string primaryDefName = $"VFEC_{baseName}";
                    string alternateDefName = $"VFE_Classical_{baseName}";
                    
                    // NAMING PATTERNS - These are all the formats that could be used for references
                    List<string> allNamingPatterns = new List<string>();
                    
                    // 1. Standard patterns with different prefixes
                    allNamingPatterns.Add(primaryDefName);
                    allNamingPatterns.Add(alternateDefName);
                    allNamingPatterns.Add($"Classical_{baseName}");
                    allNamingPatterns.Add(baseName);
                    
                    // 2. Numbered variants (1-20) for all structures
                    for (int i = 1; i <= 20; i++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{i}");
                        allNamingPatterns.Add($"{alternateDefName}{i}");
                        allNamingPatterns.Add($"Classical_{baseName}{i}");
                    }
                    
                    // 3. Letter suffixes (A-Z) - standard variation for most structures
                    for (char letter = 'A'; letter <= 'Z'; letter++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{letter}");
                        allNamingPatterns.Add($"{alternateDefName}{letter}");
                        allNamingPatterns.Add($"Classical_{baseName}{letter}");
                    }
                    
                    // 4. Common suffix variations
                    string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex", "Small", "Medium", "Large" };
                    foreach (string suffix in suffixes)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{suffix}");
                        allNamingPatterns.Add($"{alternateDefName}{suffix}");
                        allNamingPatterns.Add($"Classical_{baseName}{suffix}");
                        allNamingPatterns.Add($"{baseName}{suffix}");
                    }
                    
                    // 5. Alternative prefixing styles that some mods might use
                    string[] prefixingStyles = new[] 
                    {
                        $"Structure_VFEC_{baseName}", 
                        $"Layout_VFEC_{baseName}",
                        $"StructureLayout_VFEC_{baseName}",
                        $"VFEC_Structure_{baseName}",
                        $"VFEC_Layout_{baseName}",
                        $"VFEC.{baseName}",
                        $"Classical.{baseName}"
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
                
                // SCAN XML FILES DIRECTLY
                try
                {
                    // VFE Classical mod (2787850474)
                    ScanVFEMod("Vanilla Factions Expanded - Classical", "VFE - Classical", "2787850474", 
                                    registeredNames, ref count);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                }
                
                Log.Message($"[KCSG Unbound] Registered {count} VFE Classical layouts using enhanced methods");
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
                    m.PackageId.Contains("2938820380") || 
                    m.PackageId.Contains("oskarpotocki.vfe.empire") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.empire"));
                    
                if (empireModLoaded)
                {
                    Log.Message("[KCSG Unbound] VFE Empire mod is loaded - implementing comprehensive structure registration");
                }
                else
                {
                    Log.Message("[KCSG Unbound] VFE Empire mod is NOT directly loaded, but still registering structures as fallback");
                }
                
                // COMPREHENSIVE LIST OF ALL KNOWN VFE EMPIRE STRUCTURES
                List<string> criticalBaseNames = new List<string>
                {
                    // Main structure types
                    "ImperialFort", "ImperialOutpost", "ImperialSettlement", "ImperialCity",
                    "Fort", "Outpost", "Settlement", "City", "Palace", "Barracks", "Throne", 
                    "GrandHall", "ImperialBarracks", "ImperialPalace", "ImperialThrone",
                    "CommandPost", "GarrisonHouse", "TaxOffice", "AdministrationBuilding",
                    
                    // Special symbols and structures
                    "Symbol", "EmpireSymbol", "ImperialSymbol"
                };
                
                // Track the defs we create
                int count = 0;
                HashSet<string> registeredNames = new HashSet<string>();
                
                // SYSTEMATIC APPROACH: Generate all common naming variants for each structure
                foreach (string baseName in criticalBaseNames)
                {
                    // PRIMARY NAMING: VFEE_BaseName or VFE_Empire_BaseName
                    string primaryDefName = $"VFEE_{baseName}";
                    string alternateDefName = $"VFE_Empire_{baseName}";
                    
                    // NAMING PATTERNS - These are all the formats that could be used for references
                    List<string> allNamingPatterns = new List<string>();
                    
                    // 1. Standard patterns with different prefixes
                    allNamingPatterns.Add(primaryDefName);
                    allNamingPatterns.Add(alternateDefName);
                    allNamingPatterns.Add($"Empire_{baseName}");
                    allNamingPatterns.Add($"Imperial_{baseName}");
                    allNamingPatterns.Add(baseName);
                    
                    // 2. Numbered variants (1-20) for all structures
                    for (int i = 1; i <= 20; i++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{i}");
                        allNamingPatterns.Add($"{alternateDefName}{i}");
                        allNamingPatterns.Add($"Empire_{baseName}{i}");
                        allNamingPatterns.Add($"Imperial_{baseName}{i}");
                    }
                    
                    // 3. Letter suffixes (A-Z) - standard variation for most structures
                    for (char letter = 'A'; letter <= 'Z'; letter++)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{letter}");
                        allNamingPatterns.Add($"{alternateDefName}{letter}");
                        allNamingPatterns.Add($"Empire_{baseName}{letter}");
                        allNamingPatterns.Add($"Imperial_{baseName}{letter}");
                    }
                    
                    // 4. Common suffix variations
                    string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex", "Small", "Medium", "Large" };
                    foreach (string suffix in suffixes)
                    {
                        allNamingPatterns.Add($"{primaryDefName}{suffix}");
                        allNamingPatterns.Add($"{alternateDefName}{suffix}");
                        allNamingPatterns.Add($"Empire_{baseName}{suffix}");
                        allNamingPatterns.Add($"Imperial_{baseName}{suffix}");
                        allNamingPatterns.Add($"{baseName}{suffix}");
                    }
                    
                    // 5. Alternative prefixing styles that some mods might use
                    string[] prefixingStyles = new[] 
                    {
                        $"Structure_VFEE_{baseName}", 
                        $"Layout_VFEE_{baseName}",
                        $"StructureLayout_VFEE_{baseName}",
                        $"VFEE_Structure_{baseName}",
                        $"VFEE_Layout_{baseName}",
                        $"VFEE.{baseName}",
                        $"Empire.{baseName}",
                        $"Imperial.{baseName}"
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
                
                // SCAN XML FILES DIRECTLY
                try
                {
                    // VFE Empire mod (2938820380)
                    ScanVFEMod("Vanilla Factions Expanded - Empire", "VFE - Empire", "2938820380", 
                                    registeredNames, ref count);
                }
                catch (Exception ex)
                {
                    Log.Warning($"[KCSG Unbound] Error during mod folder scan: {ex.Message}");
                }
                
                Log.Message($"[KCSG Unbound] Registered {count} VFE Empire layouts using enhanced methods");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error initializing VFE Empire layouts: {ex}");
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
    }
} 