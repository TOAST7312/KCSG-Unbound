using System;
using System.Collections;
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
                
                // First try getting def types by name to ensure we find all structure def types
                Type structureLayoutDefType = null;

                // Try to find the KCSG structure layout type
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.FullName != null && 
                            (type.FullName.Contains("KCSG.StructureLayoutDef") ||
                             type.FullName.Contains("StructureLayoutDef") ||
                             type.Name == "StructureLayoutDef"))
                        {
                            structureLayoutDefType = type;
                            Log.Message($"[KCSG] [{timestamp}] Found structure layout type: {type.FullName}");
                            break;
                        }
                    }
                    if (structureLayoutDefType != null) break;
                }

                // If we couldn't find the specific type, try to find anything that might be a structure layout
                if (structureLayoutDefType == null)
                {
                    // Try to find types with names that suggest they're structure layouts
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.IsSubclassOf(typeof(Def)) && 
                                type.Name.Contains("Structure") || 
                                type.Name.Contains("Layout") ||
                                type.Name.Contains("KCSG"))
                            {
                                Log.Message($"[KCSG] [{timestamp}] Potentially found structure layout type: {type.FullName}");
                                structureLayoutDefType = type;
                                break;
                            }
                        }
                        if (structureLayoutDefType != null) break;
                    }
                }

                // If we still haven't found the type, try to scan all loaded defs to see if any match our criteria
                if (structureLayoutDefType == null)
                {
                    Log.Warning($"[KCSG] [{timestamp}] Could not find StructureLayoutDef type, checking all loaded defs");
                    
                    // Look at all loaded defs to find ones that might be structure layouts
                foreach (var def in DefDatabase<Def>.AllDefs)
                {
                        if (def.defName != null && (
                            def.defName.StartsWith("VFE") || 
                            def.defName.StartsWith("VFEM") ||
                            def.defName.StartsWith("VFED") ||
                            def.defName.StartsWith("VBGE")))
                        {
                            // This looks like a structure layout from a Vanilla Expanded mod
                            RegisterDef(def.defName, def);
                            regCount++;
                        }
                    }
                }
                else
                {
                    // Use reflection to get all defs of the structure layout type
                    Log.Message($"[KCSG] [{timestamp}] Searching for structure layouts in DefDatabase");
                    
                    // Create a generic method call to DefDatabase<T>.AllDefs
                    var defDatabaseType = typeof(DefDatabase<>).MakeGenericType(structureLayoutDefType);
                    var allDefsProperty = defDatabaseType.GetProperty("AllDefs");
                    
                    if (allDefsProperty != null)
                    {
                        var allDefs = allDefsProperty.GetValue(null) as IEnumerable;
                        if (allDefs != null)
                        {
                            foreach (var def in allDefs)
                            {
                                // Try to get the defName property
                                PropertyInfo defNameProp = def.GetType().GetProperty("defName");
                                if (defNameProp != null)
                                {
                                    string defName = defNameProp.GetValue(def) as string;
                                    if (!string.IsNullOrEmpty(defName))
                                    {
                                        RegisterDef(defName, def);
                                        regCount++;
                                    }
                                }
                            }
                        }
                    }
                }

                // Also directly check XML files from mods to find structure layout defs
                Log.Message($"[KCSG] [{timestamp}] Scanning XML files for structure layouts");
                
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
                
                // Create a set of unique defNames to avoid duplicates
                HashSet<string> processedDefNames = new HashSet<string>();
                
                foreach (ModContentPack mod in runningMods)
                {
                    foreach (string folder in structureFolderPatterns)
                    {
                        string folderPath = Path.Combine(mod.RootDir, folder);
                        if (Directory.Exists(folderPath))
                    {
                        try
                        {
                                foreach (string file in Directory.GetFiles(folderPath, "*.xml", SearchOption.AllDirectories))
                                {
                                    try
                                    {
                                        XmlDocument doc = new XmlDocument();
                                        doc.Load(file);
                                        
                                        // Look for layout defs with defName nodes
                                        XmlNodeList defNames = doc.SelectNodes("//Defs/*[defName]");
                                        if (defNames != null)
                                        {
                                            foreach (XmlNode node in defNames)
                                            {
                                                XmlNode nameNode = node.SelectSingleNode("defName");
                                                if (nameNode != null && !string.IsNullOrEmpty(nameNode.InnerText))
                                                {
                                                    string defName = nameNode.InnerText.Trim();
                                                    
                                                    // Check if this might be a structure layout def
                                                    if ((defName.Contains("VFEM") || 
                                                         defName.Contains("VFED") || 
                                                         defName.Contains("VBGE") ||
                                                         defName.Contains("Structure")) &&
                                                        !processedDefNames.Contains(defName))
                                                    {
                                                        // Add as a placeholder def for KCSG resolution
                                                        object placeholderDef = CreatePlaceholderDef(defName);
                                                        RegisterDef(defName, placeholderDef);
                                                        processedDefNames.Add(defName);
                                regCount++;
                                                    }
                                                }
                                            }
                            }
                        }
                        catch (Exception ex)
                        {
                                        Log.Warning($"[KCSG] [{timestamp}] Error processing XML file {file}: {ex.Message}");
                                        continue;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Warning($"[KCSG] [{timestamp}] Error accessing folder {folderPath}: {ex.Message}");
                                continue;
                            }
                        }
                    }
                }
                
                Log.Message($"[KCSG] [{timestamp}] Proactively registered {regCount} KCSG.StructureLayoutDefs");
                
                // Now, pre-register common names from mods like VFE that are frequently referenced
                PreregisterCommonStructureNames();
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error scanning for structure layouts: {ex}");
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
                
                // Vanilla Base Generation Expanded prefixes
                "VBGE_Empire", "VBGE_Production", "VBGE_Mining", "VBGE_Slavery",
                "VBGE_Logging", "VBGE_Defence", "VBGE_CentralEmpire", "VBGE_Outlander",
                "VBGE_OutlanderProduction", "VBGE_OutlanderMining", "VBGE_OutlanderSlavery",
                "VBGE_OutlanderLogging", "VBGE_OutlanderDefence", "VBGE_OutlanderFields",
                "VBGE_TribalProduction", "VBGE_TribalMining", "VBGE_TribalSlavery",
                "VBGE_TribalLogging", "VBGE_TribalDefence", "VBGE_PiratesDefence",
                "VBGE_PirateSlavery",
                
                // General mod prefixes
                "VFED_", "VFEA_", "VFEC_", "VFEE_", "VFEM_", "VFET_", "VFE_", "VFEI_", "FTC_", 
                "RBME_", "AG_", "BM_", "BS_", "MM_", "VC_", "VE_", "VM_", "VBGE_", "VGBE_"
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
            
            // PRE-REGISTER EXPLICIT DESERTERS MOD STRUCTURES THAT ARE CAUSING CROSS-REFERENCE ERRORS
            // This section ensures all problematic structure names from VFE Deserters are pre-registered
            string[] deserterSpecificLayouts = new[] {
                // Explicitly register Underfarm layouts (both by ID from XML and common variants)
                "VFED_UnderfarmMainA", "VFED_UnderfarmMainB", "VFED_UnderfarmMainC",
                "VFED_UnderfarmA", "VFED_UnderfarmB", "VFED_UnderfarmC", "VFED_UnderfarmD", 
                "VFED_UnderfarmE", "VFED_UnderfarmF", "VFED_UnderfarmG", "VFED_UnderfarmH",
                
                // Explicitly register NewSafehaven layouts
                "VFED_NewSafehaven1", "VFED_NewSafehaven2", "VFED_NewSafehaven3", 
                "VFED_NewSafehaven4", "VFED_NewSafehaven5", "VFED_NewSafehaven6",
                
                // Explicitly register Noble layouts
                "VFED_LargeNobleBallroom", "VFED_LargeNobleBedroom", "VFED_LargeNobleGallery",
                "VFED_LargeNobleThroneRoom", "VFED_MediumNobleBallroom", "VFED_MediumNobleBedroom",
                "VFED_MediumNobleGallery", "VFED_MediumNobleThroneRoom", "VFED_SmallNobleBedroom",
                "VFED_SmallNobleThroneRoom", "VFED_GrandNobleThroneRoom",
                
                // Explicitly register Plot layouts
                "VFED_Bunker", "VFED_Courtyard", "VFED_Gardens", "VFED_KontarionEmplacement",
                "VFED_OnagerEmplacement", "VFED_PalintoneEmplacement", "VFED_ServantQuarters",
                "VFED_ShuttleLandingPad", "VFED_StockpileDepot", "VFED_SurveillanceStation"
            };
            
            foreach (var defName in deserterSpecificLayouts) {
                if (!IsDefRegistered(defName)) {
                    var placeholderDef = new KCSG.StructureLayoutDef {
                        defName = defName
                    };
                    RegisterDef(defName, placeholderDef);
                    count++;
                    
                    // Also register with variant suffixes
                    RegisterDef(defName + "Layout", CreatePlaceholderDef(defName + "Layout"));
                    RegisterDef(defName + "Structure", CreatePlaceholderDef(defName + "Structure"));
                    count += 2;
                }
            }
            
            // Pre-register numbered VBGE structures (1-20)
            List<string> vbgeNumberedPrefixes = new List<string>() {
                "VBGE_CentralEmpire", "VBGE_Production", "VBGE_Mining", "VBGE_Slavery",
                "VBGE_Logging", "VBGE_Defence", "VBGE_Tribal", "VBGE_Outlander"
            };
            
            foreach (var basePrefix in vbgeNumberedPrefixes) {
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
            
            // Pre-register VBGE settlement tag names that might be referenced
            string[] vbgeSettlementTags = new[] {
                "GenericPower", "GenericBattery", "GenericSecurity", "GenericPodLauncher",
                "GenericKitchen", "GenericStockpile", "GenericBedroom", "GenericGrave",
                "GenericRecroom", "EmpireBedrooms", "EmpireShuttle", "EmpireThrone",
                "VGBE_Production", "VGBE_Mining", "VGBE_Slavery", "VGBE_Logging",
                "VGBE_Defence", "EmpireProduction", "VGBE_CentralEmpire", "VGBE_TribalDefence",
                "VGBE_TribalCenter", "VGBE_TribalProduction", "VGBE_TribalMining",
                "VGBE_TribalLogging", "VGBE_PiratesDefence", "VGBE_PirateSlavery"
            };
            
            foreach (var tag in vbgeSettlementTags) {
                if (!IsDefRegistered(tag)) {
                    // Create a placeholder def for each tag
                    var placeholderDef = CreatePlaceholderDef(tag);
                    RegisterDef(tag, placeholderDef);
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
            
            // Fast path: If already registered, return it
            if (symbolDefs.TryGetValue(defName, out symbolDef))
            {
                return true;
            }
            
            // ENHANCED: If this is a VFED structure, try extra hard to create a placeholder
            bool isVFEDStructure = defName.StartsWith("VFED_");
            
            // Special urgent handling for VFED_ structures that are known to cause issues
            if (isVFEDStructure && 
                (defName.Contains("Underfarm") || 
                 defName.Contains("NewSafehaven") || 
                 defName.Contains("AerodroneStation") || 
                 defName.Contains("ShuttleStagingPost") ||
                 defName.Contains("SupplyDepot") ||
                 defName.Contains("TechPrinter") ||
                 defName.Contains("SurveillanceStation") ||
                 defName.Contains("ImperialConvoy") ||
                 defName.Contains("ZeusCannonComplex")))
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                Log.Warning($"[KCSG] [{timestamp}] Critical VFED structure requested but not found: {defName} - Creating emergency placeholder");
                
                try
                {
                    // Try to get the existing StructureLayoutDef type first
                    Type symbolDefType = null;
                    
                    try
                    {
                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            try
                            {
                                foreach (var type in assembly.GetTypes())
                                {
                                    if (type.FullName == "KCSG.StructureLayoutDef" || 
                                        (type.Name == "StructureLayoutDef" && type.Namespace == "KCSG") ||
                                        type.Name == "StructureLayoutDef")
                                    {
                                        symbolDefType = type;
                                        break;
                                    }
                                }
                                if (symbolDefType != null) break;
                            }
                            catch { /* Ignore exceptions when scanning assemblies */ }
                        }
                    }
                    catch { /* Ignore any scanning exceptions */ }
                    
                    // If we can't find the type, try to use Def or create a BasicPlaceholderDef
                    if (symbolDefType == null)
                    {
                        try
                        {
                            // Try with a basic KCSG Def if available
                            var basicDefType = Type.GetType("KCSG.BasicPlaceholderDef");
                            if (basicDefType != null)
                            {
                                symbolDefType = basicDefType;
                            }
                            else
                            {
                                // Fall back to regular Def
                                symbolDefType = typeof(Def);
                            }
                        }
                        catch
                        {
                            // Last resort - use Def
                            symbolDefType = typeof(Def);
                        }
                    }
                    
                    // Create placeholder and set defName
                    try
                    {
                        symbolDef = Activator.CreateInstance(symbolDefType);
                        
                        // Set defName property
                        PropertyInfo defNameProperty = symbolDefType.GetProperty("defName");
                        if (defNameProperty != null)
                        {
                            defNameProperty.SetValue(symbolDef, defName);
                        }
                        
                        // Register it for future use
                        RegisterDef(defName, symbolDef);
                        
                        // Register variants for common suffixes too
                        string[] variants = new[] {
                            defName + "Layout",
                            defName + "Structure",
                            "Structure_" + defName,
                            "Layout_" + defName
                        };
                        
                        foreach (var variant in variants)
                        {
                            if (!IsDefRegistered(variant))
                            {
                                var variantDef = Activator.CreateInstance(symbolDefType);
                                defNameProperty?.SetValue(variantDef, variant);
                                RegisterDef(variant, variantDef);
                            }
                        }
                        
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[KCSG] [{timestamp}] Emergency placeholder creation error for {defName}: {ex.Message}");
                        
                        // Last resort fallback - manual object
                        try
                        {
                            // Create a super basic object that at least has a defName field
                            var basicObj = new BasicPlaceholderDef { defName = defName };
                            symbolDef = basicObj;
                            RegisterDef(defName, symbolDef);
                            return true;
                        }
                        catch
                        {
                            // If all else fails, create a dictionary as placeholder
                            var dict = new Dictionary<string, string> { { "defName", defName } };
                            symbolDef = dict;
                            RegisterDef(defName, symbolDef);
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[KCSG] Critical error handling VFED structure: {ex}");
                }
            }
            
            // If not found, try to create a placeholder def for cross-reference resolution
            // This is critical to prevent "Could not resolve cross-reference" errors
            if (defName.Contains("StructureLayout") || 
                defName.StartsWith("VFED_") || defName.StartsWith("VFEM_") ||
                defName.StartsWith("VBGE_") || defName.StartsWith("VGBE_") ||
                // Additional specific pattern detection for VFE mods
                (defName.StartsWith("VFE") && 
                 (defName.Contains("Safehaven") || defName.Contains("Underfarm") || 
                  defName.Contains("Carrier") || defName.Contains("Frigate") || 
                  defName.Contains("Cruiser") || defName.Contains("Destroyer") ||
                  defName.Contains("Broadcasting") || defName.Contains("Station"))) ||
                // Additional pattern detection for VBGE
                (defName.StartsWith("Generic") && 
                 (defName.Contains("Power") || defName.Contains("Battery") || 
                  defName.Contains("Security") || defName.Contains("PodLauncher") ||
                  defName.Contains("Kitchen") || defName.Contains("Stockpile") ||
                  defName.Contains("Bedroom") || defName.Contains("Grave") ||
                  defName.Contains("Recroom"))) ||
                (defName.StartsWith("Empire") && 
                 (defName.Contains("Bedrooms") || defName.Contains("Shuttle") || 
                  defName.Contains("Throne") || defName.Contains("Production"))) ||
                defName.Contains("VFEA_") || defName.Contains("VFEC_") ||
                defName.Contains("FTC_") || defName.Contains("RBME_") || defName.StartsWith("VFE"))
            {
                // Create a placeholder def
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    symbolDef = CreatePlaceholderDef(defName);
                    
                    // ENHANCED: Add more detailed logging for VFED structures that were missing
                    bool isVFEDeserters = defName.StartsWith("VFED_");
                    
                    // Register the placeholder to avoid creating it again
                    RegisterDef(defName, symbolDef);
                    
                    // Log more detailed information for VFED structures to help diagnose issues
                    if (isVFEDeserters && Prefs.DevMode)
                    {
                        Log.Warning($"[KCSG] [{timestamp}] VFED structure was missing from registry and had to be created on demand: {defName}");
                        
                        // For critical VFED structures, register additional variants too for better compatibility
                        if (defName.Contains("Underfarm") || defName.Contains("NewSafehaven"))
                        {
                            string[] variants = new[] {
                                defName + "Layout",
                                defName + "Structure",
                                "Structure_" + defName,
                                "Layout_" + defName
                            };
                            
                            foreach (var variant in variants)
                            {
                                if (!IsDefRegistered(variant))
                                {
                                    RegisterDef(variant, CreatePlaceholderDef(variant));
                                }
                            }
                        }
                    }
                    else if (Prefs.DevMode)
                    {
                        Log.Message($"[KCSG] [{timestamp}] Created placeholder def for cross-reference: {defName}");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    Log.Warning($"[KCSG] [{timestamp}] Failed to create placeholder def for {defName}: {ex.Message}");
                    
                    // Last-ditch fallback for VFED structures
                    if (defName.StartsWith("VFED_"))
                    {
                        try
                        {
                            // Create a super basic object that at least has a defName field
                            var basicObj = new Dictionary<string, string> { { "defName", defName } };
                            symbolDef = basicObj;
                            RegisterDef(defName, symbolDef);
                            return true;
                        }
                        catch
                        {
                            // We tried our best
                        }
                    }
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
                // ENHANCED - Critical-first approach: Load VFE Deserters structures first and most thoroughly
                if (LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Deserter") || 
                    m.PackageId.Contains("3025493377") || 
                    m.PackageId.Contains("oskar.vfe.deserter")))
                {
                    Log.Message($"[KCSG Unbound] Deserters mod detected - ensuring all structures are preregistered");
                    
                    // These are the critical structures from Vanilla Factions Expanded - Deserters
                    // that appear to be causing the issues with cross-references
                    List<string> vfedCriticalBaseNames = new List<string> {
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
                    
                    // Create with all common naming variations
                    foreach (string baseName in vfedCriticalBaseNames)
                    {
                        // Create with VFED_ prefix (primary naming convention)
                        string primaryDefName = $"VFED_{baseName}";
                        
                        // Create alphabetical variants (A-Z)
                        for (char letter = 'A'; letter <= 'Z'; letter++)
                        {
                            string lettered = $"{primaryDefName}{letter}";
                            if (!IsDefRegistered(lettered))
                            {
                                object placeholder = CreatePlaceholderDef(lettered);
                                RegisterDef(lettered, placeholder);
                                createdDefs.Add(lettered);
                            }
                        }
                        
                        // Create numbered variants (1-12) for specific structures
                        if (baseName == "NewSafehaven")
                        {
                            for (int i = 1; i <= 12; i++)
                            {
                                string numbered = $"{primaryDefName}{i}";
                                if (!IsDefRegistered(numbered))
                                {
                                    object placeholder = CreatePlaceholderDef(numbered);
                                    RegisterDef(numbered, placeholder);
                                    createdDefs.Add(numbered);
                                }
                            }
                        }
                        
                        // Create the base name version as well
                        if (!IsDefRegistered(primaryDefName))
                        {
                            object placeholder = CreatePlaceholderDef(primaryDefName);
                            RegisterDef(primaryDefName, placeholder);
                            createdDefs.Add(primaryDefName);
                        }
                        
                        // Create common suffix variations
                        string[] suffixes = new[] { "Layout", "Structure", "Base", "Main", "Complex" };
                        foreach (string suffix in suffixes)
                        {
                            string withSuffix = $"{primaryDefName}{suffix}";
                            if (!IsDefRegistered(withSuffix))
                            {
                                object placeholder = CreatePlaceholderDef(withSuffix);
                                RegisterDef(withSuffix, placeholder);
                                createdDefs.Add(withSuffix);
                            }
                        }
                        
                        // Create with alternative prefixing styles (used by some mods)
                        string[] alternativePrefixStyles = new[] {
                            $"Structure_VFED_{baseName}", 
                            $"Layout_VFED_{baseName}",
                            $"StructureLayout_VFED_{baseName}",
                            $"VFED_Structure_{baseName}",
                            $"VFED_Layout_{baseName}"
                        };
                        
                        foreach (string altName in alternativePrefixStyles)
                        {
                            if (!IsDefRegistered(altName))
                            {
                                object placeholder = CreatePlaceholderDef(altName);
                                RegisterDef(altName, placeholder);
                                createdDefs.Add(altName);
                            }
                        }
                    }
                    
                    int deserterStructures = createdDefs.Count;
                    Log.Message($"[KCSG Unbound] Registered {deserterStructures} critical VFE Deserters layouts");
                }
                
                // ENHANCED - Second critical pass: Load VBGE structures
                if (LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Base Generation") || 
                    m.PackageId.Contains("3209927822") || 
                    m.PackageId.Contains("vanillaexpanded.basegen")))
                {
                    Log.Message($"[KCSG Unbound] VBGE mod detected - ensuring all structures are preregistered");
                    
                    // Critical VBGE structures that need to be preregistered
                    List<string> vbgeCriticalBaseNames = new List<string> {
                        // Main structure types
                        "CentralEmpire", "Production", "Mining", "Slavery", "Logging", "Defence", 
                        "TribalCenter", "Outlander",
                        
                        // Faction-specific structures
                        "EmpireProduction", "EmpireMining", "EmpireSlavery", "EmpireLogging", "EmpireDefence",
                        "TribalProduction", "TribalMining", "TribalSlavery", "TribalLogging", "TribalDefence",
                        "OutlanderProduction", "OutlanderMining", "OutlanderSlavery", "OutlanderLogging", "OutlanderDefence",
                        "PiratesDefence", "PirateSlavery"
                    };
                    
                    // Process each base name with variants
                    foreach (string baseName in vbgeCriticalBaseNames)
                    {
                        // Create with both VBGE_ and VGBE_ prefixes (both appear in files)
                        string vbgeDefName = $"VBGE_{baseName}";
                        string vgbeDefName = $"VGBE_{baseName}";
                        
                        // Create numbered variants (1-20) for all base structures
                        for (int i = 1; i <= 20; i++)
                        {
                            string vbgeNumbered = $"{vbgeDefName}{i}";
                            string vgbeNumbered = $"{vgbeDefName}{i}";
                            
                            if (!IsDefRegistered(vbgeNumbered))
                            {
                                object placeholder = CreatePlaceholderDef(vbgeNumbered);
                                RegisterDef(vbgeNumbered, placeholder);
                                createdDefs.Add(vbgeNumbered);
                            }
                            
                            if (!IsDefRegistered(vgbeNumbered))
                            {
                                object placeholder = CreatePlaceholderDef(vgbeNumbered);
                                RegisterDef(vgbeNumbered, placeholder);
                                createdDefs.Add(vgbeNumbered);
                            }
                        }
                        
                        // Create the base name versions
                        if (!IsDefRegistered(vbgeDefName))
                        {
                            object placeholder = CreatePlaceholderDef(vbgeDefName);
                            RegisterDef(vbgeDefName, placeholder);
                            createdDefs.Add(vbgeDefName);
                        }
                        
                        if (!IsDefRegistered(vgbeDefName))
                        {
                            object placeholder = CreatePlaceholderDef(vgbeDefName);
                            RegisterDef(vgbeDefName, placeholder);
                            createdDefs.Add(vgbeDefName);
                        }
                    }
                    
                    // Generic tags that are referenced
                    string[] genericTags = new[] {
                        "GenericPower", "GenericBattery", "GenericSecurity", "GenericPodLauncher",
                        "GenericKitchen", "GenericStockpile", "GenericBedroom", "GenericGrave",
                        "GenericRecroom", "GenericProduction", "EmpireBedrooms", "EmpireShuttle", 
                        "EmpireThrone"
                    };
                    
                    foreach (string tag in genericTags)
                    {
                        if (!IsDefRegistered(tag))
                        {
                            object placeholder = CreatePlaceholderDef(tag);
                            RegisterDef(tag, placeholder);
                            createdDefs.Add(tag);
                        }
                    }
                    
                    int vbgeStructures = createdDefs.Count;
                    Log.Message($"[KCSG Unbound] Registered {vbgeStructures} critical VBGE layouts");
                }
                
                // ENHANCED - Third critical pass: Load Alpha Books structures
                if (LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Alpha Books") || 
                    m.PackageId.Contains("3403180654")))
                {
                    Log.Message($"[KCSG Unbound] Alpha Books mod detected - ensuring symbols are preregistered");
                    
                    // Critical Alpha Books symbols
                    string[] alphaBooksSymbols = new[] {
                        // Common book symbols
                        "BookSymbol_Floor", "BookSymbol_Wall", "BookSymbol_Door", "BookSymbol_Light",
                        "BookSymbol_Table", "BookSymbol_Chair", "BookSymbol_Bookshelf", "BookSymbol_Shelf",
                        "BookSymbol_Computer", "BookSymbol_Kitchen", "BookSymbol_Bedroom", "BookSymbol_Library",
                        
                        // Library structures
                        "BookLibrary_Small", "BookLibrary_Medium", "BookLibrary_Large",
                        "BookLibrary_Ancient", "BookLibrary_Modern", "BookLibrary_Futuristic",
                        
                        // Root symbols
                        "AB_Root", "AB_Library", "AB_Ancient", "AB_Modern", "AB_ScienceFiction"
                    };
                    
                    foreach (string symbol in alphaBooksSymbols)
                    {
                        if (!IsDefRegistered(symbol))
                        {
                            object placeholder = CreatePlaceholderDef(symbol);
                            RegisterDef(symbol, placeholder);
                            createdDefs.Add(symbol);
                        }
                    }
                    
                    int alphaBooksCount = createdDefs.Count;
                    Log.Message($"[KCSG Unbound] Registered {alphaBooksCount} Alpha Books symbols");
                }
                
                // ENHANCED - Fourth critical pass: Load VFE Mechanoids structures
                if (LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Mechanoid") || 
                    m.PackageId.Contains("2329011599") || 
                    m.PackageId.Contains("oskarpotocki.vfe.mechanoid") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.mechanoid")))
                {
                    Log.Message($"[KCSG Unbound] VFE Mechanoids mod detected - ensuring structures are preregistered");
                    
                    // Critical VFE Mechanoids structures
                    string[] vfeMechanoidStructures = new[] {
                        // Ship types
                        "VFEM_Carrier", "VFEM_CarrierDLC", "VFEM_Frigate", "VFEM_FrigateDLC", 
                        "VFEM_Destroyer", "VFEM_DestroyerDLC", "VFEM_Cruiser", "VFEM_CruiserDLC",
                        
                        // Base structures
                        "VFEM_BroadcastingStation", "VFEM_MechShipBeacon", "VFEM_MechShipLanding",
                        "VFEM_MechShipCrashing", "VFEM_MechShipDebris", "VFEM_FactoryRemnants",
                        
                        // Special symbols and files
                        "VFEM_StructureDLC", "VFEM_StructureNODLC", "VFEM_StatringFactories", "VFEM_Symbols"
                    };
                    
                    // Create with all common naming variations
                    foreach (string baseName in vfeMechanoidStructures)
                    {
                        // Create numbered variants (1-20) for ship types
                        if (baseName.Contains("Carrier") || baseName.Contains("Frigate") || 
                            baseName.Contains("Destroyer") || baseName.Contains("Cruiser") || 
                            baseName.Contains("BroadcastingStation"))
                        {
                            for (int i = 1; i <= 20; i++)
                            {
                                string numbered = $"{baseName}{i}";
                                if (!IsDefRegistered(numbered))
                                {
                                    object placeholder = CreatePlaceholderDef(numbered);
                                    RegisterDef(numbered, placeholder);
                                    createdDefs.Add(numbered);
                                }
                            }
                        }
                        
                        // Create the base name version as well
                        if (!IsDefRegistered(baseName))
                        {
                            object placeholder = CreatePlaceholderDef(baseName);
                            RegisterDef(baseName, placeholder);
                            createdDefs.Add(baseName);
                        }
                        
                        // Create common suffix variations
                        string[] suffixes = new[] { "Layout", "Structure", "Ship", "Main", "Complex" };
                        foreach (string suffix in suffixes)
                        {
                            string withSuffix = $"{baseName}{suffix}";
                            if (!IsDefRegistered(withSuffix))
                            {
                                object placeholder = CreatePlaceholderDef(withSuffix);
                                RegisterDef(withSuffix, placeholder);
                                createdDefs.Add(withSuffix);
                            }
                        }
                    }
                    
                    int vfemStructures = createdDefs.Count;
                    Log.Message($"[KCSG Unbound] Registered {vfemStructures} VFE Mechanoids structures");
                }
                
                // ENHANCED - Fifth critical pass: Load VFE Medieval structures
                if (LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Medieval") || 
                    m.PackageId.Contains("3444347874") || 
                    m.PackageId.Contains("oskarpotocki.vfe.medieval") ||
                    m.PackageId.Contains("oskarpotocki.vanillafactionsexpanded.medievalmodule")))
                {
                    Log.Message($"[KCSG Unbound] VFE Medieval mod detected - ensuring structures are preregistered");
                    
                    // Critical VFE Medieval structures
                    string[] vfeMedievalStructures = new[] {
                        // Structure types
                        "MedievalHouse", "MedievalTent", "MedievalKeep", "MedievalCastle",
                        "Tower", "Hall", "Barracks", "Stable", "Blacksmith", "Church",
                        "Tavern", "Market", "Farm", "Laboratory", "Walls", "Gate",
                        
                        // Special symbols
                        "Symbol", "MedievalSymbol"
                    };
                    
                    // Create with common prefixes and variations
                    foreach (string baseName in vfeMedievalStructures)
                    {
                        // Different prefixes
                        string[] prefixes = new[] { "VFEM_", "VFE_Medieval_", "Medieval_", "" };
                        
                        foreach (string prefix in prefixes)
                        {
                            string prefixedName = $"{prefix}{baseName}";
                            
                            // Create prefixed base name
                            if (!IsDefRegistered(prefixedName))
                            {
                                object placeholder = CreatePlaceholderDef(prefixedName);
                                RegisterDef(prefixedName, placeholder);
                                createdDefs.Add(prefixedName);
                            }
                            
                            // Create numbered variants (1-10)
                            for (int i = 1; i <= 10; i++)
                            {
                                string numbered = $"{prefixedName}{i}";
                                if (!IsDefRegistered(numbered))
                                {
                                    object placeholder = CreatePlaceholderDef(numbered);
                                    RegisterDef(numbered, placeholder);
                                    createdDefs.Add(numbered);
                                }
                            }
                            
                            // Create common variations with size suffixes
                            string[] suffixes = new[] { "Small", "Medium", "Large", "Layout", "Structure" };
                            foreach (string suffix in suffixes)
                            {
                                string withSuffix = $"{prefixedName}{suffix}";
                                if (!IsDefRegistered(withSuffix))
                                {
                                    object placeholder = CreatePlaceholderDef(withSuffix);
                                    RegisterDef(withSuffix, placeholder);
                                    createdDefs.Add(withSuffix);
                                }
                            }
                        }
                    }
                    
                    int vfemStructures = createdDefs.Count;
                    Log.Message($"[KCSG Unbound] Registered {vfemStructures} VFE Medieval structures");
                }
                
                // ENHANCED - Sixth critical pass: Load Save Our Ship 2 structures
                if (LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Save Our Ship") || 
                    m.Name.Contains("SOS2") ||
                    m.PackageId.Contains("1909914131") || 
                    m.PackageId.Contains("ludeon.rimworld.shipshavecomeback") ||
                    m.PackageId.Contains("lwm.shipshavecomeback")))
                {
                    Log.Message($"[KCSG Unbound] Save Our Ship 2 mod detected - ensuring structures are preregistered");
                    
                    // Critical Save Our Ship 2 structures
                    string[] sos2Structures = new[] {
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
                    
                    // Create with different prefixes and variations
                    foreach (string baseName in sos2Structures)
                    {
                        // Different prefixes
                        string[] prefixes = new[] { "SOS2_", "SaveOurShip_", "SOS_", "Ship_", "" };
                        
                        foreach (string prefix in prefixes)
                        {
                            string prefixedName = $"{prefix}{baseName}";
                            
                            // Create prefixed base name
                            if (!IsDefRegistered(prefixedName))
                            {
                                object placeholder = CreatePlaceholderDef(prefixedName);
                                RegisterDef(prefixedName, placeholder);
                                createdDefs.Add(prefixedName);
                            }
                            
                            // Create numbered variants (1-5)
                            for (int i = 1; i <= 5; i++)
                            {
                                string numbered = $"{prefixedName}{i}";
                                if (!IsDefRegistered(numbered))
                                {
                                    object placeholder = CreatePlaceholderDef(numbered);
                                    RegisterDef(numbered, placeholder);
                                    createdDefs.Add(numbered);
                                }
                            }
                            
                            // Create letter variants (A-E)
                            for (char letter = 'A'; letter <= 'E'; letter++)
                            {
                                string lettered = $"{prefixedName}{letter}";
                                if (!IsDefRegistered(lettered))
                                {
                                    object placeholder = CreatePlaceholderDef(lettered);
                                    RegisterDef(lettered, placeholder);
                                    createdDefs.Add(lettered);
                                }
                            }
                            
                            // Create common size variants
                            string[] suffixes = new[] { "Small", "Medium", "Large", "Layout", "Structure", "Class" };
                            foreach (string suffix in suffixes)
                            {
                                string withSuffix = $"{prefixedName}{suffix}";
                                if (!IsDefRegistered(withSuffix))
                                {
                                    object placeholder = CreatePlaceholderDef(withSuffix);
                                    RegisterDef(withSuffix, placeholder);
                                    createdDefs.Add(withSuffix);
                                }
                            }
                        }
                    }
                    
                    int sos2Structures2 = createdDefs.Count;
                    Log.Message($"[KCSG Unbound] Registered {sos2Structures2} Save Our Ship 2 structures");
                }
                
                // ENHANCED - Seventh critical pass: Load Vanilla Outposts Expanded structures
                if (LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Outpost") || 
                    m.PackageId.Contains("2688941031") || 
                    m.PackageId.Contains("oskarpotocki.vfe.outposts") ||
                    m.PackageId.Contains("vanillaexpanded.outposts")))
                {
                    Log.Message($"[KCSG Unbound] Vanilla Outposts Expanded mod detected - ensuring structures are preregistered");
                    
                    // Critical Vanilla Outposts Expanded structures
                    string[] voeStructures = new[] {
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
                    
                    // Create with different prefixes and variations
                    foreach (string baseName in voeStructures)
                    {
                        // Different prefixes
                        string[] prefixes = new[] { "VOE_", "VE_Outposts_", "Outpost_", "" };
                        
                        foreach (string prefix in prefixes)
                        {
                            string prefixedName = $"{prefix}{baseName}";
                            
                            // Create prefixed base name
                            if (!IsDefRegistered(prefixedName))
                            {
                                object placeholder = CreatePlaceholderDef(prefixedName);
                                RegisterDef(prefixedName, placeholder);
                                createdDefs.Add(prefixedName);
                            }
                            
                            // Create numbered variants (1-8)
                            for (int i = 1; i <= 8; i++)
                            {
                                string numbered = $"{prefixedName}{i}";
                                if (!IsDefRegistered(numbered))
                                {
                                    object placeholder = CreatePlaceholderDef(numbered);
                                    RegisterDef(numbered, placeholder);
                                    createdDefs.Add(numbered);
                                }
                            }
                            
                            // Create letter variants (A-E)
                            for (char letter = 'A'; letter <= 'E'; letter++)
                            {
                                string lettered = $"{prefixedName}{letter}";
                                if (!IsDefRegistered(lettered))
                                {
                                    object placeholder = CreatePlaceholderDef(lettered);
                                    RegisterDef(lettered, placeholder);
                                    createdDefs.Add(lettered);
                                }
                            }
                            
                            // Create common size variants
                            string[] suffixes = new[] { "Small", "Medium", "Large", "Layout", "Structure" };
                            foreach (string suffix in suffixes)
                            {
                                string withSuffix = $"{prefixedName}{suffix}";
                                if (!IsDefRegistered(withSuffix))
                                {
                                    object placeholder = CreatePlaceholderDef(withSuffix);
                                    RegisterDef(withSuffix, placeholder);
                                    createdDefs.Add(withSuffix);
                                }
                            }
                        }
                    }
                    
                    int voeStructures2 = createdDefs.Count;
                    Log.Message($"[KCSG Unbound] Registered {voeStructures2} Vanilla Outposts Expanded structures");
                }
                
                // ENHANCED - Eighth critical pass: Load VFE Ancients and Vault structures
                if (LoadedModManager.RunningModsListForReading.Any(m => 
                    m.Name.Contains("Ancients") || 
                    m.PackageId.Contains("2654846754") || // Main VFE Ancients 
                    m.PackageId.Contains("2946113288") || // Lost Vaults
                    m.PackageId.Contains("3160710884") || // Even More Vaults
                    m.PackageId.Contains("3325594457") || // Soups Vault Collection
                    m.PackageId.Contains("oskarpotocki.vfe.ancients")))
                {
                    Log.Message($"[KCSG Unbound] VFE Ancients mods detected - ensuring structures are preregistered");
                    
                    // Critical VFE Ancients structures
                    string[] ancientsBaseStructures = new[] {
                        // Base ancient structures
                        "AncientHouse", "AncientTent", "AncientKeep", "AncientCastle", "AncientLabratory",
                        "AncientTemple", "AncientFarm", "AncientSlingshot", "AncientVault", "AncientRuin",
                        "AbandonedSlingshot", "LootedVault", "SealedVault",
                        
                        // Special symbols
                        "Symbol", "AncientSymbol"
                    };
                    
                    // Create with different prefixes and variations
                    foreach (string baseName in ancientsBaseStructures)
                    {
                        // Different prefixes
                        string[] prefixes = new[] { "VFEA_", "VFE_Ancients_", "Ancient_", "" };
                        
                        foreach (string prefix in prefixes)
                        {
                            string prefixedName = $"{prefix}{baseName}";
                            
                            // Create prefixed base name
                            if (!IsDefRegistered(prefixedName))
                            {
                                object placeholder = CreatePlaceholderDef(prefixedName);
                                RegisterDef(prefixedName, placeholder);
                                createdDefs.Add(prefixedName);
                            }
                            
                            // Create letter variants (A-E)
                            for (char letter = 'A'; letter <= 'E'; letter++)
                            {
                                string lettered = $"{prefixedName}{letter}";
                                if (!IsDefRegistered(lettered))
                                {
                                    object placeholder = CreatePlaceholderDef(lettered);
                                    RegisterDef(lettered, placeholder);
                                    createdDefs.Add(lettered);
                                }
                            }
                            
                            // Create common variations with size suffixes
                            string[] suffixes = new[] { "Small", "Medium", "Large", "Layout", "Structure" };
                            foreach (string suffix in suffixes)
                            {
                                string withSuffix = $"{prefixedName}{suffix}";
                                if (!IsDefRegistered(withSuffix))
                                {
                                    object placeholder = CreatePlaceholderDef(withSuffix);
                                    RegisterDef(withSuffix, placeholder);
                                    createdDefs.Add(withSuffix);
                                }
                            }
                        }
                    }
                    
                    // Critical vault structures from Lost Vaults
                    string[] vaultStructures = new[] {
                        // Animal vaults from Lost Vaults
                        "SealedVaultKilo1", "SealedVaultKilo2", "SealedVaultKilo3", 
                        "SealedVaultCrow", "SealedVaultBadger", "SealedVaultBear", 
                        "SealedVaultMole", "SealedVaultFox", "SealedVaultMouse", 
                        "SealedVaultOx", "SealedVaultTurtle", "SealedVaultEagle", 
                        "SealedVaultOwl",
                        
                        // Specialized vaults from Even More Vaults
                        "SealedGeneBankVault", "SealedOutpostVault", 
                        "SealedWarehouseVault", "AgriculturalResearchVault",
                        
                        // Tree vaults from Soups Vault Collection
                        "VFEA_SV_RedwoodVault", "VFEA_SV_MangroveVault", "VFEA_SV_BonsaiVault", 
                        "VFEA_SV_CedarVault", "VFEA_SV_MagnoliaVault", "VFEA_SV_OakVault", 
                        "VFEA_SV_SequoiaVault", "VFEA_SV_SycamoreVault", "VFEA_SV_ManukaVault",
                        "VFEA_SV_MapleVault", "VFEA_SV_PandoVault", "VFEA_SV_BlackwoodVault",
                        "VFEA_SV_BristleconeVault", "VFEA_SV_BirchVault"
                    };
                    
                    // Register each vault directly
                    foreach (string vaultName in vaultStructures)
                    {
                        if (!IsDefRegistered(vaultName))
                        {
                            object placeholder = CreatePlaceholderDef(vaultName);
                            RegisterDef(vaultName, placeholder);
                            createdDefs.Add(vaultName);
                        }
                        
                        // For Soups vaults, also register alternative forms (specifically handle SV_ prefix patterns)
                        if (vaultName.Contains("VFEA_SV_"))
                        {
                            string treeName = vaultName.Replace("VFEA_SV_", "").Replace("Vault", "");
                            
                            string[] altPatterns = new[] {
                                $"SealedVault{treeName}",
                                $"TreeVault{treeName}",
                                $"VFEA_SV_{treeName}",
                                $"VFEA_{treeName}Vault",
                                $"{treeName}Vault"
                            };
                            
                            foreach (string altPattern in altPatterns)
                            {
                                if (!IsDefRegistered(altPattern))
                                {
                                    object placeholder = CreatePlaceholderDef(altPattern);
                                    RegisterDef(altPattern, placeholder);
                                    createdDefs.Add(altPattern);
                                }
                            }
                        }
                    }
                    
                    int ancientsStructures = createdDefs.Count;
                    Log.Message($"[KCSG Unbound] Registered {ancientsStructures} VFE Ancients and Vault structures");
                }
                
                Log.Message($"[KCSG Unbound] Preloaded a total of {createdDefs.Count} commonly referenced defs");
            return createdDefs;
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] [{timestamp}] Error preloading commonly referenced defs: {ex}");
                return createdDefs;
            }
        }
    }
} 
