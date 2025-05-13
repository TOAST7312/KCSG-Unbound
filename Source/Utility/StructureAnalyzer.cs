using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Utility class for analyzing and prioritizing structure definitions
    /// </summary>
    public static class StructureAnalyzer
    {
        // Track initialization state
        public static bool HasInitialized { get; private set; } = false;
        
        // Cache of structure priorities to avoid recalculating
        private static Dictionary<string, StructurePriority> structurePriorities = new Dictionary<string, StructurePriority>();
        
        // Cache of reference counts to track how often structures are referenced
        private static Dictionary<string, int> referenceCount = new Dictionary<string, int>();
        
        // List of settlement and faction definition files that might reference structures
        private static List<string> settlementAndFactionFiles = new List<string>();
        
        // Memory optimization: Store analyzed file paths to avoid reprocessing
        private static HashSet<string> analyzedFiles = new HashSet<string>();
        
        // Structure naming patterns that indicate usage priority
        private static readonly string[] HighPriorityKeywords = new[]
        {
            "Base", "Main", "Core", "Primary", "Center", "Central", "Default", "Standard",
            "Common", "Settlement", "Village", "Town", "City", "Outpost", "Camp"
        };
        
        private static readonly string[] MediumPriorityKeywords = new[]
        {
            "Building", "House", "Home", "Structure", "Layout", "Facility", "Room",
            "Complex", "Station", "Quarters", "Barracks", "Storage", "Workshop"
        };
        
        // Memory optimization: Precompute uppercase keywords for faster comparison
        private static readonly HashSet<string> HighPriorityKeywordsUpper;
        private static readonly HashSet<string> MediumPriorityKeywordsUpper;
        
        /// <summary>
        /// Static constructor to initialize keyword sets
        /// </summary>
        static StructureAnalyzer()
        {
            // Precompute uppercase keywords for faster comparison
            HighPriorityKeywordsUpper = new HashSet<string>(HighPriorityKeywords.Select(k => k.ToUpperInvariant()));
            MediumPriorityKeywordsUpper = new HashSet<string>(MediumPriorityKeywords.Select(k => k.ToUpperInvariant()));
        }
        
        /// <summary>
        /// Priority levels for structure definitions
        /// </summary>
        public enum StructurePriority
        {
            Essential = 0,  // Must be registered - core structures
            High = 1,       // Important structures with common usage
            Medium = 2,     // Moderately important structures
            Low = 3,        // Less important variant structures
            VeryLow = 4     // Rarely used structures or special variants
        }
        
        /// <summary>
        /// Initialize the analyzer by scanning for settlement and faction files
        /// </summary>
        public static void Initialize()
        {
            structurePriorities.Clear();
            referenceCount.Clear();
            settlementAndFactionFiles.Clear();
            analyzedFiles.Clear();
            
            // Scan for settlement and faction files that might reference structures
            FindSettlementAndFactionFiles();
            
            // Scan the files for structure references to build reference counts
            BuildReferenceTable();
            
            Log.Message($"StructureAnalyzer initialized with {settlementAndFactionFiles.Count} settlement/faction files");
            
            HasInitialized = true;
        }
        
        /// <summary>
        /// Cleans up temporary caches to free memory after processing is complete
        /// </summary>
        public static void CleanupTemporaryCaches()
        {
            // Release memory from temporary collections that aren't needed after processing
            analyzedFiles?.Clear();
            analyzedFiles = null;
            
            settlementAndFactionFiles?.Clear();
            settlementAndFactionFiles = null;
            
            // Note: We keep the structurePriorities and referenceCount dictionaries
            // as they may be needed for additional operations during the game
            
            Log.Message($"StructureAnalyzer temporary caches cleaned up");
        }
        
        /// <summary>
        /// Find all files that might contain references to structures
        /// </summary>
        private static void FindSettlementAndFactionFiles()
        {
            try
            {
                // Start with all loaded mods
                foreach (var mod in LoadedModManager.RunningModsListForReading)
                {
                    if (mod?.RootDir == null)
                        continue;
                    
                    // Look in common locations for settlement and faction definitions
                    string defsFolder = Path.Combine(mod.RootDir, "Defs");
                    if (!Directory.Exists(defsFolder))
                        continue;
                    
                    // Areas to check for relevant definitions
                    string[] relevantFolders = new[]
                    {
                        Path.Combine(defsFolder, "SettlementDefs"),
                        Path.Combine(defsFolder, "FactionDefs"),
                        Path.Combine(defsFolder, "Settlement"),
                        Path.Combine(defsFolder, "Faction"),
                        Path.Combine(defsFolder, "Settlements"),
                        Path.Combine(defsFolder, "Factions"),
                        Path.Combine(defsFolder, "MapGeneration"),
                        Path.Combine(defsFolder, "MapGen"),
                        Path.Combine(defsFolder, "WorldGeneration")
                    };
                    
                    foreach (var folder in relevantFolders)
                    {
                        if (Directory.Exists(folder))
                        {
                            string[] xmlFiles = Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories);
                            settlementAndFactionFiles.AddRange(xmlFiles);
                        }
                    }
                }
                
                Log.Message($"Found {settlementAndFactionFiles.Count} settlement and faction definition files");
            }
            catch (Exception ex)
            {
                Log.Error($"Error finding settlement and faction files: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Build a table of references to structures from settlement and faction files
        /// </summary>
        private static void BuildReferenceTable()
        {
            try
            {
                // Process all the settlement and faction files
                foreach (string filePath in settlementAndFactionFiles)
                {
                    try
                    {
                        // Extract text content that might reference structure layouts
                        string content = File.ReadAllText(filePath);
                        
                        // Scan for references to structures in the content
                        ScanForStructureReferences(content);
                    }
                    catch (Exception ex)
                    {
                        Log.Message($"Error processing file {filePath}: {ex.Message}");
                    }
                }
                
                Log.Message($"Built reference table with {referenceCount.Count} structure references");
            }
            catch (Exception ex)
            {
                Log.Error($"Error building reference table: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Scan content for references to structure layouts
        /// </summary>
        private static void ScanForStructureReferences(string content)
        {
            try
            {
                // Look for common XML patterns that reference structure layouts
                string[] referenceTags = new[]
                {
                    "<structureLayout>", "<settlementLayout>", "<structure>", "<layoutDef>",
                    "<symbol>", "<symbolDef>", "<layoutRef>", "<structureRef>", "<value>"
                };
                
                foreach (string tag in referenceTags)
                {
                    int pos = 0;
                    while (true)
                    {
                        int startPos = content.IndexOf(tag, pos, StringComparison.OrdinalIgnoreCase);
                        if (startPos == -1) break;
                        
                        int valueStart = startPos + tag.Length;
                        int valueEnd = content.IndexOf("</" + tag.Substring(1), valueStart, StringComparison.OrdinalIgnoreCase);
                        if (valueEnd == -1) break;
                        
                        // Extract the reference value
                        string refValue = content.Substring(valueStart, valueEnd - valueStart).Trim();
                        
                        // If it's a valid reference, increment the reference count
                        if (!string.IsNullOrEmpty(refValue))
                        {
                            if (!referenceCount.ContainsKey(refValue))
                            {
                                referenceCount[refValue] = 0;
                            }
                            referenceCount[refValue]++;
                        }
                        
                        pos = valueEnd;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Message($"Error scanning for structure references: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Determine the priority of a structure definition
        /// </summary>
        public static StructurePriority GetStructurePriority(string defName)
        {
            // Check if we've already calculated this
            if (structurePriorities.TryGetValue(defName, out StructurePriority priority))
            {
                return priority;
            }
            
            // Analyze the structure name to determine priority
            priority = AnalyzeStructureName(defName);
            
            // Store in cache for future lookups
            structurePriorities[defName] = priority;
            
            return priority;
        }
        
        /// <summary>
        /// Analyze a structure name to determine its priority
        /// </summary>
        private static StructurePriority AnalyzeStructureName(string defName)
        {
            // Hot path optimization: return cached result if available
            if (structurePriorities.TryGetValue(defName, out StructurePriority cachedPriority))
            {
                return cachedPriority;
            }
            
            // If we have reference data, prioritize based on it
            if (referenceCount.TryGetValue(defName, out int references))
            {
                if (references >= 5)
                    return StructurePriority.Essential; // Heavily referenced
                else if (references >= 3)
                    return StructurePriority.High;
                else if (references >= 1)
                    return StructurePriority.Medium;
            }
            
            // Check for naming patterns that suggest important structures
            string upperDefName = defName.ToUpperInvariant();
            
            // Primary structure indicators - Hot path optimization: check most common patterns first
            if (upperDefName.Contains("MAIN") || upperDefName.Contains("BASE") || 
                upperDefName.EndsWith("1") || upperDefName.EndsWith("A") ||
                upperDefName.Contains("DEFAULT") || upperDefName.Contains("PRIMARY"))
            {
                return StructurePriority.Essential;
            }
            
            // Check for high priority keywords - Hot path optimization: use HashSet for O(1) lookups
            foreach (string keyword in HighPriorityKeywordsUpper)
            {
                if (upperDefName.Contains(keyword))
                {
                    return StructurePriority.High;
                }
            }
            
            // Check for medium priority keywords
            foreach (string keyword in MediumPriorityKeywordsUpper)
            {
                if (upperDefName.Contains(keyword))
                {
                    return StructurePriority.Medium;
                }
            }
            
            // Check for variant patterns
            if (upperDefName.Contains("VARIANT") || 
                (upperDefName.Length > 0 && char.IsDigit(upperDefName[upperDefName.Length - 1])))
            {
                return StructurePriority.Low;
            }
            
            // Default to medium-low priority
            return StructurePriority.Low;
        }
        
        /// <summary>
        /// Filter a list of structure defNames based on priority threshold
        /// </summary>
        public static List<string> FilterStructuresByPriority(
            IEnumerable<string> defNames, 
            StructurePriority maxPriority = StructurePriority.Medium)
        {
            List<string> filtered = new List<string>();
            
            foreach (string defName in defNames)
            {
                StructurePriority priority = GetStructurePriority(defName);
                
                // Only include if priority is less than or equal to threshold
                // (lower numbers = higher priority)
                if (priority <= maxPriority)
                {
                    filtered.Add(defName);
                }
            }
            
            return filtered;
        }
        
        /// <summary>
        /// Prioritize structuring the registration order to register most important first
        /// </summary>
        public static List<string> PrioritizeStructures(IEnumerable<string> defNames)
        {
            // Group structures by priority
            Dictionary<StructurePriority, List<string>> priorityGroups = new Dictionary<StructurePriority, List<string>>();
            
            foreach (string defName in defNames)
            {
                StructurePriority priority = GetStructurePriority(defName);
                
                if (!priorityGroups.ContainsKey(priority))
                {
                    priorityGroups[priority] = new List<string>();
                }
                
                priorityGroups[priority].Add(defName);
            }
            
            // Build ordered list from highest priority to lowest
            List<string> ordered = new List<string>();
            
            // Add each priority group in order
            for (StructurePriority p = StructurePriority.Essential; p <= StructurePriority.VeryLow; p++)
            {
                if (priorityGroups.ContainsKey(p))
                {
                    ordered.AddRange(priorityGroups[p]);
                }
            }
            
            return ordered;
        }
    }
} 