using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;
using HarmonyLib;
using System.Linq;
using System.Linq.Expressions;
using System.IO;
using System.Xml;
using RimWorld;

namespace KCSG
{
    /// <summary>
    /// Provides safe initialization for KCSG Unbound
    /// </summary>
    public static class SafeStart
    {
        // For tracking initialization
        private static bool initialized = false;
        private static bool loadingPhaseComplete = false;
        
        // For tracking detected mods
        private static HashSet<string> detectedModIds = new HashSet<string>();
        
        // Common mod IDs to check for
        private static readonly Dictionary<string, string> knownStructureModIds = new Dictionary<string, string>
        {
            { "oskar.vfe.deserter", "VFE Deserters" },
            { "oskarpotocki.vfe.mechanoid", "VFE Mechanoids" },
            { "vanillaexpanded.basegen", "VBGE" },
            { "oskarpotocki.vfe.medieval", "VFE Medieval" },
            { "ludeon.rimworld.shipshavecomeback", "Save Our Ship 2" },
            { "oskarpotocki.vfe.outposts", "VFE Outposts" },
            { "oskarpotocki.vfe.insectoids", "VFE Insectoids" },
            { "oskarpotocki.vfe.classical", "VFE Classical" },
            { "oskarpotocki.vfe.empire", "VFE Empire" },
            { "3403180654", "Alpha Books" },
            { "alphabooks", "Alpha Books" }
        };
        
        /// <summary>
        /// Initialize the mod with safety guards
        /// </summary>
        public static void Initialize()
        {
            try
            {
                if (initialized) return;
                
                // Diagnostic logging
                Diagnostics.Initialize();
                Diagnostics.LogDiagnostic("[KCSG Unbound] SafeStart Initialize() called");
                
                // Start logging
                Log.Message("[KCSG Unbound] Starting initialization");
                
                // Initialize structures registry
                if (!SymbolRegistry.Initialized)
                {
                    SymbolRegistry.Initialize();
                }
                
                // Scan loaded mods to detect which structure sets we need
                DetectActiveStructureMods();
                
                // Set up Harmony patches
                SetupHarmony();
                
                // Start listening for game loading events
                LongEventHandler.QueueLongEvent(OnGameLoadingComplete, "KCSG_InitComplete", false, null);
                
                initialized = true;
                
                Log.Message("[KCSG Unbound] Initialization complete, lazy loading scheduled");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error during safe initialization: {ex}");
            }
        }
        
        /// <summary>
        /// Detects which structure-using mods are active
        /// </summary>
        private static void DetectActiveStructureMods()
        {
            try
            {
                Log.Message("[KCSG Unbound] Detecting active structure mods");
                
                // Clear existing detection data
                detectedModIds.Clear();
                
                // Check all running mods
                foreach (var mod in LoadedModManager.RunningModsListForReading)
                {
                    foreach (var knownMod in knownStructureModIds)
                    {
                        if (mod.PackageId.Contains(knownMod.Key))
                        {
                            detectedModIds.Add(knownMod.Key);
                            Log.Message($"[KCSG Unbound] Detected active structure mod: {knownMod.Value}");
                        }
                    }
                    
                    // Also check for mods with KCSG in their About.xml
                    string aboutPath = Path.Combine(mod.RootDir, "About", "About.xml");
                    if (File.Exists(aboutPath))
                    {
                        try
                        {
                            string aboutContent = File.ReadAllText(aboutPath);
                            if (aboutContent.Contains("KCSG") || 
                                aboutContent.Contains("Krypt's") || 
                                aboutContent.Contains("structure generation"))
                            {
                                detectedModIds.Add(mod.PackageId);
                                Log.Message($"[KCSG Unbound] Detected active structure mod: {mod.Name}");
                            }
                        }
                        catch
                        {
                            // Skip if we can't read the About.xml
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Error detecting active structure mods: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Setup Harmony patching
        /// </summary>
        private static void SetupHarmony()
        {
            try
            {
                Log.Message("[KCSG Unbound] Setting up Harmony patches");
                
                // Create our Harmony instance
                Harmony harmony = new Harmony("KCSG.Unbound");
                
                // Apply necessary patches
                HarmonyPatches.ApplyPatches(harmony);
                
                Log.Message("[KCSG Unbound] Harmony patching complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error during Harmony setup: {ex}");
            }
        }
        
        /// <summary>
        /// Called when game loading is complete
        /// </summary>
        private static void OnGameLoadingComplete()
        {
            if (loadingPhaseComplete) return;
            
            try
            {
                Log.Message("[KCSG Unbound] Game loading complete, performing delayed structure loading");
                
                // Load only the structures for mods that are actually active
                foreach (var modId in detectedModIds)
                {
                    LoadModSpecificStructures(modId);
                }
                
                loadingPhaseComplete = true;
                
                Log.Message("[KCSG Unbound] Delayed structure loading complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error during delayed loading: {ex}");
            }
        }
        
        /// <summary>
        /// Loads structures for a specific mod
        /// </summary>
        private static void LoadModSpecificStructures(string modId)
        {
            try
            {
                // Skip if no ID
                if (string.IsNullOrEmpty(modId)) return;
                
                // Check for common mod IDs and load appropriate structures
                if (modId.Contains("vfe.deserter"))
                {
                    LoadDesertersStructures();
                }
                else if (modId.Contains("vfe.mechanoid"))
                {
                    LoadMechanoidStructures();
                }
                else if (modId.Contains("basegen"))
                {
                    LoadVBGEStructures();
                }
                else if (modId.Contains("vfe.medieval"))
                {
                    LoadMedievalStructures();
                }
                else if (modId.Contains("shipshavecomeback"))
                {
                    LoadSOS2Structures();
                }
                else if (modId.Contains("vfe.outposts"))
                {
                    LoadOutpostsStructures();
                }
                else if (modId.Contains("vfe.insectoids"))
                {
                    LoadInsectoidsStructures();
                }
                else if (modId.Contains("vfe.classical"))
                {
                    LoadClassicalStructures();
                }
                else if (modId.Contains("vfe.empire"))
                {
                    LoadEmpireStructures();
                }
                else if (modId.Contains("3403180654") || modId.Contains("alpha.books") || modId.Contains("alphabooks"))
                {
                    LoadAlphaBooksStructures();
                }
                else
                {
                    // Generic structure loading for other mods
                    LoadGenericModStructures(modId);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Error loading structures for mod {modId}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load structures for VFE Deserters mod
        /// </summary>
        private static void LoadDesertersStructures()
        {
            Log.Message("[KCSG Unbound] Loading structures for VFE Deserters");
            
            // Load critical base names for Deserters
            List<string> criticalBaseNames = new List<string>
            {
                // Main structures
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
            
            RegisterStructureVariants("VFED_", criticalBaseNames);
        }
        
        /// <summary>
        /// Load structures for VFE Mechanoids mod
        /// </summary>
        private static void LoadMechanoidStructures()
        {
            Log.Message("[KCSG Unbound] Loading structures for VFE Mechanoids");
            
            // Load critical base names for Mechanoids
            List<string> criticalBaseNames = new List<string>
            {
                "Carrier", "CarrierDLC", "Frigate", "FrigateDLC", 
                "Destroyer", "DestroyerDLC", "Cruiser", "CruiserDLC",
                "BroadcastingStation"
            };
            
            RegisterStructureVariants("VFEM_", criticalBaseNames);
            
            // Also register numbered variants
            foreach (var baseName in criticalBaseNames)
            {
                for (int i = 1; i <= 20; i++)
                {
                    string defName = $"VFEM_{baseName}{i}";
                    if (!SymbolRegistry.IsDefRegistered(defName))
                    {
                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                    }
                }
            }
        }
        
        /// <summary>
        /// Load structures for VBGE mod
        /// </summary>
        private static void LoadVBGEStructures()
        {
            Log.Message("[KCSG Unbound] Loading structures for Vanilla Base Generation Expanded");
            
            // Load critical base names for VBGE
            List<string> criticalBaseNames = new List<string>
            {
                "Empire", "Production", "Mining", "Slavery",
                "Logging", "Defence", "CentralEmpire", "Outlander",
                "OutlanderProduction", "OutlanderMining", "OutlanderSlavery",
                "OutlanderLogging", "OutlanderDefence", "OutlanderFields",
                "TribalProduction", "TribalMining", "TribalSlavery",
                "TribalLogging", "TribalDefence", "PiratesDefence",
                "PirateSlavery"
            };
            
            RegisterStructureVariants("VBGE_", criticalBaseNames);
        }
        
        /// <summary>
        /// Load structures for VFE Medieval mod
        /// </summary>
        private static void LoadMedievalStructures()
        {
            Log.Message("[KCSG Unbound] Loading structures for VFE Medieval");
            
            // Load critical base names for Medieval
            List<string> criticalBaseNames = new List<string>
            {
                "Castle", "Hamlet", "Village", "Monastery",
                "Keep", "Tower", "Hall", "Tavern", "Blacksmith",
                "Barracks", "GuildHall", "ThroneRoom"
            };
            
            RegisterStructureVariants("VFEM_", criticalBaseNames);
        }
        
        /// <summary>
        /// Load structures for SOS2 mod
        /// </summary>
        private static void LoadSOS2Structures()
        {
            Log.Message("[KCSG Unbound] Loading structures for Save Our Ship 2");
            
            // Load critical base names for SOS2
            List<string> criticalBaseNames = new List<string>
            {
                "Ship", "Bridge", "Quarters", "Cargo",
                "Hangar", "Engineering", "Weapons", "Shields"
            };
            
            RegisterStructureVariants("SOS_", criticalBaseNames);
        }
        
        /// <summary>
        /// Load structures for VFE Outposts mod
        /// </summary>
        private static void LoadOutpostsStructures()
        {
            Log.Message("[KCSG Unbound] Loading structures for VFE Outposts");
            
            // Load critical base names for Outposts
            List<string> criticalBaseNames = new List<string>
            {
                "Outpost", "Camp", "Base", "Settlement",
                "Trading", "Military", "Mining", "Research"
            };
            
            RegisterStructureVariants("VOE_", criticalBaseNames);
        }
        
        /// <summary>
        /// Load structures for VFE Insectoids mod
        /// </summary>
        private static void LoadInsectoidsStructures()
        {
            Log.Message("[KCSG Unbound] Loading structures for VFE Insectoids");
            
            // Load critical base names for Insectoids
            List<string> criticalBaseNames = new List<string>
            {
                "Hive", "Nest", "Chamber", "Tunnels"
            };
            
            RegisterStructureVariants("VFEI_", criticalBaseNames);
        }
        
        /// <summary>
        /// Load structures for VFE Classical mod
        /// </summary>
        private static void LoadClassicalStructures()
        {
            Log.Message("[KCSG Unbound] Loading structures for VFE Classical");
            
            // Load critical base names for Classical
            List<string> criticalBaseNames = new List<string>
            {
                "Villa", "Temple", "Senate", "Barracks",
                "Forum", "Baths", "Market", "Colosseum"
            };
            
            RegisterStructureVariants("VFEC_", criticalBaseNames);
        }
        
        /// <summary>
        /// Load structures for VFE Empire mod
        /// </summary>
        private static void LoadEmpireStructures()
        {
            Log.Message("[KCSG Unbound] Loading structures for VFE Empire");
            
            // Load critical base names for Empire
            List<string> criticalBaseNames = new List<string>
            {
                "Estate", "Palace", "Citadel", "Administration",
                "Garrison", "ThroneRoom", "Chambers", "Court"
            };
            
            RegisterStructureVariants("VFEE_", criticalBaseNames);
        }
        
        /// <summary>
        /// Load structures for a generic mod by looking at its XML files
        /// </summary>
        private static void LoadGenericModStructures(string modId)
        {
            // Find the mod in the running mods
            ModContentPack mod = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => 
                m.PackageId.Contains(modId));
            
            if (mod == null) return;
            
            Log.Message($"[KCSG Unbound] Loading structures for {mod.Name}");
            
            // Check common folder patterns where structure layouts might be stored
            string[] structureFolderPatterns = new[] {
                "Defs/StructureDefs",
                "Defs/StructureLayoutDefs",
                "Defs/StructureGen",
                "Defs/Structures",
                "Defs/CustomGenDefs",
                "Defs/CustomGenDefs/StructureLayoutDefs",
                "Defs/SettlementLayoutDefs",
                "Defs/SettlementDefs",
                "Defs/LayoutDefs"
            };
            
            // Track registered defNames to avoid duplicates
            HashSet<string> registeredDefNames = new HashSet<string>();
            
            // Scan XML files for structure defs
            foreach (string folder in structureFolderPatterns)
            {
                string folderPath = Path.Combine(mod.RootDir, folder);
                if (!Directory.Exists(folderPath)) continue;
                
                foreach (string file in Directory.GetFiles(folderPath, "*.xml", SearchOption.AllDirectories))
                {
                    try
                    {
                        // Use XmlReader for efficient streaming
                        using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
                        using (System.Xml.XmlReader reader = System.Xml.XmlReader.Create(fs, new System.Xml.XmlReaderSettings { IgnoreWhitespace = true, IgnoreComments = true }))
                        {
                            while (reader.Read())
                            {
                                if (reader.NodeType == System.Xml.XmlNodeType.Element && reader.Name == "defName")
                                {
                                    string defName = reader.ReadElementContentAsString().Trim();
                                    
                                    // Register if not already registered
                                    if (!string.IsNullOrEmpty(defName) && !registeredDefNames.Contains(defName) && !SymbolRegistry.IsDefRegistered(defName))
                                    {
                                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                                        registeredDefNames.Add(defName);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG Unbound] Error processing XML file {file}: {ex.Message}");
                    }
                }
            }
            
            Log.Message($"[KCSG Unbound] Registered {registeredDefNames.Count} structures for {mod.Name}");
        }
        
        /// <summary>
        /// Load structures for Alpha Books mod
        /// </summary>
        private static void LoadAlphaBooksStructures()
        {
            Log.Message("[KCSG Unbound] Loading structures for Alpha Books");
            
            // Critical Alpha Books symbols and structures
            List<string> criticalBaseNames = new List<string>
            {
                "BookSymbol", "Library", "BookLibrary", "BookStore", 
                "ABooks", "AB_Root", "AB_Ancient", "AB_Modern", "AB_Library",
                "BookSymbol_Floor", "BookSymbol_Wall", "BookSymbol_Door", "BookSymbol_Light",
                "BookSymbol_Table", "BookSymbol_Chair", "BookSymbol_Bookshelf", "BookSymbol_Shelf",
                "BookSymbol_Computer", "BookSymbol_Kitchen", "BookSymbol_Bedroom", "BookSymbol_Library"
            };
            
            // Generate structures with various prefixes
            string[] prefixes = new[] { "", "ABooks_", "AB_", "AlphaBooks_" };
            HashSet<string> registeredNames = new HashSet<string>();
            int count = 0;
            
            foreach (var prefix in prefixes)
            {
                foreach (var baseName in criticalBaseNames)
                {
                    string defName = $"{prefix}{baseName}";
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
                            // Try simpler placeholder
                            try
                            {
                                var basicPlaceholder = new BasicPlaceholderDef { defName = defName };
                                SymbolRegistry.RegisterDef(defName, basicPlaceholder);
                                count++;
                                registeredNames.Add(defName);
                            }
                            catch
                            {
                                Log.Warning($"[KCSG Unbound] Failed to register Alpha Books structure {defName}: {ex.Message}");
                            }
                        }
                    }
                    
                    // Register size variants
                    string[] sizes = new[] { "Small", "Medium", "Large" };
                    foreach (var size in sizes)
                    {
                        string sizedDefName = $"{prefix}{baseName}_{size}";
                        if (!SymbolRegistry.IsDefRegistered(sizedDefName) && !registeredNames.Contains(sizedDefName))
                        {
                            try
                            {
                                object placeholderDef = SymbolRegistry.CreatePlaceholderDef(sizedDefName);
                                SymbolRegistry.RegisterDef(sizedDefName, placeholderDef);
                                count++;
                                registeredNames.Add(sizedDefName);
                            }
                            catch
                            {
                                // Ignore errors
                            }
                        }
                    }
                }
            }
            
            Log.Message($"[KCSG Unbound] Registered {count} Alpha Books structures");
        }
        
        /// <summary>
        /// Register structure variants with different suffixes
        /// </summary>
        private static void RegisterStructureVariants(string prefix, List<string> baseNames)
        {
            foreach (string baseName in baseNames)
            {
                // Standard naming with prefix
                string primaryDefName = $"{prefix}{baseName}";
                if (!SymbolRegistry.IsDefRegistered(primaryDefName))
                {
                    object placeholderDef = SymbolRegistry.CreatePlaceholderDef(primaryDefName);
                    SymbolRegistry.RegisterDef(primaryDefName, placeholderDef);
                }
                
                // Letter variants (A-Z)
                for (char letter = 'A'; letter <= 'F'; letter++)
                {
                    string defName = $"{prefix}{baseName}{letter}";
                    if (!SymbolRegistry.IsDefRegistered(defName))
                    {
                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                    }
                }
                
                // Common suffixes
                string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex" };
                foreach (string suffix in suffixes)
                {
                    string defName = $"{prefix}{baseName}{suffix}";
                    if (!SymbolRegistry.IsDefRegistered(defName))
                    {
                        object placeholderDef = SymbolRegistry.CreatePlaceholderDef(defName);
                        SymbolRegistry.RegisterDef(defName, placeholderDef);
                    }
                }
            }
        }
    }
} 