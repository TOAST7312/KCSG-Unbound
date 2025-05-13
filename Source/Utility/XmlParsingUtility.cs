using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Text;
using System.Threading;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Efficient XML parsing utilities for structure definitions
    /// </summary>
    public static class XmlParsingUtility
    {
        // Hot path optimization: Cache parsed files to avoid reprocessing
        private static Dictionary<string, List<string>> fileDefNamesCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
        // Hot path optimization: Cache structure detection results
        private static Dictionary<string, bool> structureDefFileCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        
        // Hot path optimization: Pre-allocate buffers for string operations
        private static readonly StringBuilder stringBuilder = new StringBuilder(8192);
        
        // Common structure definition keywords - precompiled for faster checks
        private static readonly string[] structureKeywords = new[]
        {
            "<StructureLayoutDef>", 
            "<SettlementLayoutDef>", 
            "<CustomStructureDef>", 
            "<KCSG.StructureLayoutDef>", 
            "<KCSG.SettlementLayoutDef>"
        };
        
        /// <summary>
        /// Clear all internal caches to free memory
        /// </summary>
        public static void ClearCaches()
        {
            Log.Message($"Clearing XML parsing caches: {fileDefNamesCache.Count} file caches and {structureDefFileCache.Count} structure detection results");
            
            fileDefNamesCache?.Clear();
            structureDefFileCache?.Clear();
            
            // Force a cleanup of the StringBuilder to release memory
            if (stringBuilder.Capacity > 16384) // Only if it grew too large
            {
                stringBuilder.Capacity = 8192;
            }
            
            // Don't actually null the collections as they might be needed later
            Log.Message("XML parsing caches cleared");
        }
        
        /// <summary>
        /// Efficiently extract all defNames from XML files in a directory
        /// </summary>
        /// <param name="directory">Directory containing XML files</param>
        /// <param name="searchOption">Whether to search recursively</param>
        /// <param name="priorityThreshold">Maximum priority level to include (lower = higher priority)</param>
        /// <returns>HashSet of all defNames found</returns>
        public static HashSet<string> ExtractDefNamesFromDirectory(
            string directory, 
            SearchOption searchOption = SearchOption.AllDirectories,
            StructureAnalyzer.StructurePriority priorityThreshold = StructureAnalyzer.StructurePriority.Medium)
        {
            HashSet<string> defNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            try
            {
                if (!Directory.Exists(directory))
                    return defNames;
                
                // Get all XML files in the directory (and subdirectories if recursive)
                string[] xmlFiles = Directory.GetFiles(directory, "*.xml", searchOption);
                
                // Hot path optimization: Pre-filter files that contain structure definitions
                List<string> relevantFiles = new List<string>(xmlFiles.Length);
                foreach (string file in xmlFiles)
                {
                    if (ContainsStructureDefinitions(file))
                    {
                        relevantFiles.Add(file);
                    }
                }
                
                // First scan for all defNames
                List<string> allDefNames = new List<string>();
                
                foreach (string xmlFile in relevantFiles)
                {
                    try
                    {
                        // Extract defNames from this file and add to our collection
                        var fileDefNames = ExtractDefNamesFromFile(xmlFile);
                        allDefNames.AddRange(fileDefNames);
                    }
                    catch (Exception ex)
                    {
                        Log.Message($"Error parsing XML file {xmlFile}: {ex.Message}");
                    }
                }
                
                // Now filter based on priority
                var filteredDefNames = StructureAnalyzer.FilterStructuresByPriority(allDefNames, priorityThreshold);
                
                // Add the filtered defNames to the result set
                foreach (var defName in filteredDefNames)
                {
                    defNames.Add(defName);
                }
                
                Log.Message($"Extracted {allDefNames.Count} total defNames, filtered to {defNames.Count} based on priority");
            }
            catch (Exception ex)
            {
                Log.Error($"Error scanning directory {directory}: {ex.Message}");
            }
            
            return defNames;
        }
        
        /// <summary>
        /// Efficiently extract all defNames from a single XML file
        /// </summary>
        /// <param name="filePath">Path to XML file</param>
        /// <returns>List of defNames found in the file</returns>
        public static List<string> ExtractDefNamesFromFile(string filePath)
        {
            // Hot path optimization: Check cache first
            if (fileDefNamesCache.TryGetValue(filePath, out var cachedDefNames))
            {
                return new List<string>(cachedDefNames); // Return a copy to prevent modification
            }
            
            List<string> defNames = new List<string>();
            
            try
            {
                // Hot path optimization: Skip files that don't have structure definitions
                if (!ContainsStructureDefinitions(filePath))
                {
                    fileDefNamesCache[filePath] = defNames; // Cache the empty result
                    return defNames;
                }
                
                // Create XML reader settings - hot path optimization: reuse settings
                XmlReaderSettings settings = GetXmlReaderSettings();
                
                // Use XmlReader for efficient streaming instead of loading entire file
                using (XmlReader reader = XmlReader.Create(filePath, settings))
                {
                    while (reader.Read())
                    {
                        // Only process element nodes
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            // Hot path optimization: Use string comparison instead of string creation
                            string elementName = reader.Name;
                            
                            // Check if we found a defName element
                            if (string.Equals(elementName, "defName", StringComparison.OrdinalIgnoreCase))
                            {
                                // Read the value of the defName element
                                string defName = reader.ReadElementContentAsString().Trim();
                                
                                // Only add non-empty defNames
                                if (!string.IsNullOrEmpty(defName))
                                {
                                    defNames.Add(defName);
                                }
                            }
                            // Also check for symbol elements which might be references
                            else if (string.Equals(elementName, "symbol", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(elementName, "symbolDef", StringComparison.OrdinalIgnoreCase))
                            {
                                // Read the value of the symbol element
                                string symbol = reader.ReadElementContentAsString().Trim();
                                
                                // Only add non-empty symbols
                                if (!string.IsNullOrEmpty(symbol))
                                {
                                    defNames.Add(symbol);
                                }
                            }
                        }
                    }
                }
                
                // Hot path optimization: Cache the result for future use
                fileDefNamesCache[filePath] = new List<string>(defNames);
            }
            catch (Exception ex)
            {
                Log.Message($"Error parsing XML file {filePath}: {ex.Message}");
                
                // Fallback to a more basic but robust method if the XML is malformed
                try
                {
                    string content = File.ReadAllText(filePath);
                    defNames.AddRange(FallbackExtractDefNames(content));
                    
                    // Still cache the result even if we had to use fallback
                    fileDefNamesCache[filePath] = new List<string>(defNames);
                }
                catch
                {
                    // Silently continue if even the fallback fails
                }
            }
            
            return defNames;
        }
        
        /// <summary>
        /// Get or create XML reader settings - hot path optimization: reuse settings
        /// </summary>
        private static XmlReaderSettings GetXmlReaderSettings()
        {
            return new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                IgnoreProcessingInstructions = true,
                DtdProcessing = DtdProcessing.Ignore // Prevent DTD processing for security and performance
            };
        }
        
        /// <summary>
        /// Fallback method for extracting defNames from potentially malformed XML
        /// Only used if the proper XML parsing fails
        /// </summary>
        private static List<string> FallbackExtractDefNames(string content)
        {
            List<string> defNames = new List<string>();
            
            try
            {
                // Hot path optimization: Use StringBuilder for string operations
                stringBuilder.Clear();
                
                // Use our old string-based method as a fallback
                int pos = 0;
                while (true)
                {
                    int defNameStart = content.IndexOf("<defName>", pos, StringComparison.OrdinalIgnoreCase);
                    if (defNameStart == -1) break;
                    
                    int defNameEnd = content.IndexOf("</defName>", defNameStart, StringComparison.OrdinalIgnoreCase);
                    if (defNameEnd == -1) break;
                    
                    stringBuilder.Clear();
                    stringBuilder.Append(content, defNameStart + 9, defNameEnd - defNameStart - 9);
                    string defName = stringBuilder.ToString().Trim();
                    
                    if (!string.IsNullOrEmpty(defName))
                    {
                        defNames.Add(defName);
                    }
                    
                    pos = defNameEnd;
                }
                
                // Also check for symbol tags
                pos = 0;
                while (true)
                {
                    int symbolStart = content.IndexOf("<symbol>", pos, StringComparison.OrdinalIgnoreCase);
                    if (symbolStart == -1) break;
                    
                    int symbolEnd = content.IndexOf("</symbol>", symbolStart, StringComparison.OrdinalIgnoreCase);
                    if (symbolEnd == -1) break;
                    
                    stringBuilder.Clear();
                    stringBuilder.Append(content, symbolStart + 8, symbolEnd - symbolStart - 8);
                    string symbol = stringBuilder.ToString().Trim();
                    
                    if (!string.IsNullOrEmpty(symbol))
                    {
                        defNames.Add(symbol);
                    }
                    
                    pos = symbolEnd;
                }
            }
            catch
            {
                // Silently continue if even the basic string parsing fails
            }
            
            return defNames;
        }
        
        /// <summary>
        /// Check if a file contains CustomGenDef-related structure definitions
        /// </summary>
        public static bool ContainsStructureDefinitions(string filePath)
        {
            // Hot path optimization: Check cache first
            if (structureDefFileCache.TryGetValue(filePath, out bool cacheResult))
            {
                return cacheResult;
            }
            
            try
            {
                // Hot path optimization: Use a buffer to avoid reading the entire file
                const int bufferSize = 8192; // 8KB buffer
                byte[] buffer = new byte[bufferSize];
                
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize))
                {
                    int bytesRead = fs.Read(buffer, 0, bufferSize);
                    if (bytesRead > 0)
                    {
                        // Convert the buffer to a string
                        string contentSnippet = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        
                        // Check for typical structure definition patterns
                        bool hasStructureKeywords = false;
                        foreach (string keyword in structureKeywords)
                        {
                            if (contentSnippet.Contains(keyword))
                            {
                                hasStructureKeywords = true;
                                break;
                            }
                        }
                        
                        // Cache the result for future use
                        structureDefFileCache[filePath] = hasStructureKeywords;
                        return hasStructureKeywords;
                    }
                }
                
                // Cache as false for empty files
                structureDefFileCache[filePath] = false;
                return false;
            }
            catch
            {
                // If we can't read the file, assume it doesn't contain structures and cache the result
                structureDefFileCache[filePath] = false;
                return false;
            }
        }
        
        /// <summary>
        /// Batch process and register structure definitions from a list of files
        /// </summary>
        /// <param name="xmlFiles">XML files to process</param>
        /// <param name="batchSize">Size of batches for registration</param>
        /// <param name="priorityThreshold">Maximum priority level to include (lower = higher priority)</param>
        /// <returns>Number of registered structures</returns>
        public static int BatchRegisterStructuresFromFiles(
            IEnumerable<string> xmlFiles, 
            int batchSize = 50,
            StructureAnalyzer.StructurePriority priorityThreshold = StructureAnalyzer.StructurePriority.Medium)
        {
            int totalRegistered = 0;
            HashSet<string> uniqueDefNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // Step 1: Extract all defNames from files and ensure uniqueness
            foreach (string xmlFile in xmlFiles)
            {
                try
                {
                    // Only parse files that actually contain structure definitions - cache check is inside ExtractDefNamesFromFile
                    List<string> fileDefNames = ExtractDefNamesFromFile(xmlFile);
                    
                    // Add to our unique set
                    foreach (string defName in fileDefNames)
                    {
                        if (!string.IsNullOrEmpty(defName))
                        {
                            uniqueDefNames.Add(defName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Message($"Error processing file {xmlFile}: {ex.Message}");
                }
            }
            
            // Step 2: Filter and prioritize defNames
            List<string> allFoundDefNames = new List<string>(uniqueDefNames);
            var filteredDefNames = StructureAnalyzer.FilterStructuresByPriority(allFoundDefNames, priorityThreshold);
            var prioritizedDefNames = StructureAnalyzer.PrioritizeStructures(filteredDefNames);
            
            // Step 3: Register in batches
            List<string> currentBatch = new List<string>(batchSize);
            
            foreach (string defName in prioritizedDefNames)
            {
                try
                {
                    // Hot path optimization: Skip already registered symbols
                    if (SymbolRegistry.IsDefRegistered(defName))
                        continue;
                        
                    currentBatch.Add(defName);
                    
                    // If we've reached the batch size, register them
                    if (currentBatch.Count >= batchSize)
                    {
                        int registered = RegisterBatch(currentBatch);
                        totalRegistered += registered;
                        currentBatch.Clear();
                    }
                }
                catch (Exception ex)
                {
                    Log.Message($"Error adding defName {defName} to batch: {ex.Message}");
                }
            }
            
            // Register any remaining structures
            if (currentBatch.Count > 0)
            {
                int registered = RegisterBatch(currentBatch);
                totalRegistered += registered;
            }
            
            Log.Message($"Found {allFoundDefNames.Count} defNames, filtered to {filteredDefNames.Count}, registered {totalRegistered}");
            
            return totalRegistered;
        }
        
        /// <summary>
        /// Register a batch of structure names efficiently
        /// </summary>
        private static int RegisterBatch(List<string> defNames)
        {
            int registered = 0;
            
            foreach (string defName in defNames)
            {
                try
                {
                    // Hot path optimization: double-check registration to avoid duplicates
                    if (!SymbolRegistry.IsDefRegistered(defName))
                    {
                        SymbolRegistry.Register(defName);
                        registered++;
                    }
                }
                catch
                {
                    // Silently continue to next def if one fails
                }
            }
            
            return registered;
        }
    }
} 