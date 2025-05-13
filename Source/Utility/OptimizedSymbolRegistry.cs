using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Optimized symbol registry for fast lookup and reduced memory usage
    /// </summary>
    public static class OptimizedSymbolRegistry
    {
        // Constants for initial capacity and load factor
        private const int InitialCapacity = 4096; // Start with room for 4K symbols
        private const float LoadFactor = 0.75f;   // Rehash when 75% full
        
        // Structure to store symbol data with minimal memory usage
        private struct SymbolEntry
        {
            public uint Hash;       // 32-bit hash of the symbol name
            public string Name;     // The symbol name
            public int ModIndex;    // Index into mods array (-1 if unknown)
            public byte Flags;      // Bit flags for symbol state
            
            // Flag constants - using bit flags to save memory
            public const byte FlagRegistered = 0x01;
            public const byte FlagEssential = 0x02;
            public const byte FlagPriority1 = 0x04;
            public const byte FlagPriority2 = 0x08;
            public const byte FlagReference = 0x10;
            public const byte FlagCached = 0x20;
            
            public bool IsRegistered => (Flags & FlagRegistered) != 0;
            public bool IsEssential => (Flags & FlagEssential) != 0;
            public StructureAnalyzer.StructurePriority Priority 
            {
                get
                {
                    if ((Flags & FlagEssential) != 0) return StructureAnalyzer.StructurePriority.Essential;
                    if ((Flags & FlagPriority1) != 0) return StructureAnalyzer.StructurePriority.High;
                    if ((Flags & FlagPriority2) != 0) return StructureAnalyzer.StructurePriority.Medium;
                    return StructureAnalyzer.StructurePriority.Low;
                }
                set
                {
                    // Clear existing priority flags using unchecked to avoid the byte conversion error
                    unchecked
                    {
                        Flags &= (byte)~(FlagEssential | FlagPriority1 | FlagPriority2);
                    }
                    
                    // Set new priority flags
                    switch (value)
                    {
                        case StructureAnalyzer.StructurePriority.Essential:
                            Flags |= FlagEssential;
                            break;
                        case StructureAnalyzer.StructurePriority.High:
                            Flags |= FlagPriority1;
                            break;
                        case StructureAnalyzer.StructurePriority.Medium:
                            Flags |= FlagPriority2;
                            break;
                        // Low priority uses no flags (default)
                    }
                }
            }
        }
        
        // Main hash table storage
        private static SymbolEntry[] entries;
        private static int count;
        private static int capacity;
        private static int rehashThreshold;
        
        // Mod lookup table
        private static ModContentPack[] mods;
        private static int modCount;
        
        // String interning for efficiency
        private static Dictionary<string, string> internedStrings;
        
        // Cache of mod name to index
        private static Dictionary<string, int> modIndexCache;
        
        // Statistics
        private static int collisions;
        private static int lookups;
        private static int misses;
        private static int hits;
        
        /// <summary>
        /// Initialize the registry with default capacity
        /// </summary>
        public static void Initialize()
        {
            // Start performance monitoring if enabled
            var sw = PerformanceMonitor.StartTiming("OptimizedSymbolRegistry.Initialize");
            
            // Initialize core arrays
            capacity = InitialCapacity;
            rehashThreshold = (int)(capacity * LoadFactor);
            entries = new SymbolEntry[capacity];
            count = 0;
            
            // Initialize mod storage
            modCount = 0;
            mods = new ModContentPack[256]; // Up to 256 mods
            
            // Initialize caches
            internedStrings = new Dictionary<string, string>(StringComparer.Ordinal);
            modIndexCache = new Dictionary<string, int>(StringComparer.Ordinal);
            
            // Reset stats
            collisions = 0;
            lookups = 0;
            misses = 0;
            hits = 0;
            
            // Pre-populate with currently loaded mods
            foreach (var mod in LoadedModManager.RunningModsListForReading)
            {
                if (mod != null)
                {
                    AddModReference(mod);
                }
            }
            
            Log.Message($"[KCSG Unbound] Initialized optimized symbol registry with capacity for {capacity} symbols");
            
            if (sw != null) PerformanceMonitor.StopTiming("OptimizedSymbolRegistry.Initialize");
        }
        
        /// <summary>
        /// Register a symbol with the registry
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Register(string symbolName)
        {
            if (string.IsNullOrEmpty(symbolName))
                return;
                
            var sw = PerformanceMonitor.StartTiming("OptimizedSymbolRegistry.Register");
            
            // Fast path - check if already registered
            int index = FindIndex(symbolName);
            if (index != -1)
            {
                // Already exists, just mark as registered
                ref SymbolEntry entry = ref entries[index];
                entry.Flags |= SymbolEntry.FlagRegistered;
                
                PerformanceMonitor.RecordCacheHit("SymbolRegistry");
                PerformanceMonitor.IncrementCounter("Symbols_AlreadyRegistered");
                
                if (sw != null) PerformanceMonitor.StopTiming("OptimizedSymbolRegistry.Register");
                return;
            }
            
            PerformanceMonitor.RecordCacheMiss("SymbolRegistry");
            PerformanceMonitor.IncrementCounter("Symbols_NewRegistration");
            
            // Check if we need to resize
            if (count >= rehashThreshold)
            {
                Resize();
            }
            
            // Create a new entry
            uint hash = ComputeHash(symbolName);
            int slot = FindSlot(hash, symbolName);
            
            // Intern the string to save memory
            string internedName = InternString(symbolName);
            
            // Create the entry
            entries[slot] = new SymbolEntry
            {
                Hash = hash,
                Name = internedName,
                ModIndex = -1, // Unknown mod
                Flags = SymbolEntry.FlagRegistered
            };
            
            count++;
            
            if (sw != null) PerformanceMonitor.StopTiming("OptimizedSymbolRegistry.Register");
        }
        
        /// <summary>
        /// Register a symbol with a specific mod and priority
        /// </summary>
        public static void Register(string symbolName, ModContentPack mod, StructureAnalyzer.StructurePriority priority)
        {
            if (string.IsNullOrEmpty(symbolName))
                return;
                
            var sw = PerformanceMonitor.StartTiming("OptimizedSymbolRegistry.RegisterExtended");
            
            // Fast path - check if already registered
            int index = FindIndex(symbolName);
            if (index != -1)
            {
                // Already exists, update its data
                ref SymbolEntry entry = ref entries[index];
                entry.Flags |= SymbolEntry.FlagRegistered;
                
                // Update mod reference if provided
                if (mod != null)
                {
                    entry.ModIndex = AddModReference(mod);
                }
                
                // Update priority if higher than current
                if (priority < entry.Priority)
                {
                    entry.Priority = priority;
                }
                
                PerformanceMonitor.RecordCacheHit("SymbolRegistry");
                
                if (sw != null) PerformanceMonitor.StopTiming("OptimizedSymbolRegistry.RegisterExtended");
                return;
            }
            
            PerformanceMonitor.RecordCacheMiss("SymbolRegistry");
            
            // Check if we need to resize
            if (count >= rehashThreshold)
            {
                Resize();
            }
            
            // Create a new entry
            uint hash = ComputeHash(symbolName);
            int slot = FindSlot(hash, symbolName);
            
            // Intern the string to save memory
            string internedName = InternString(symbolName);
            
            // Get or add mod reference
            int modIndex = mod != null ? AddModReference(mod) : -1;
            
            // Create the entry with priority flags
            byte flags = SymbolEntry.FlagRegistered;
            switch (priority)
            {
                case StructureAnalyzer.StructurePriority.Essential:
                    flags |= SymbolEntry.FlagEssential;
                    break;
                case StructureAnalyzer.StructurePriority.High:
                    flags |= SymbolEntry.FlagPriority1;
                    break;
                case StructureAnalyzer.StructurePriority.Medium:
                    flags |= SymbolEntry.FlagPriority2;
                    break;
            }
            
            entries[slot] = new SymbolEntry
            {
                Hash = hash,
                Name = internedName,
                ModIndex = modIndex,
                Flags = flags
            };
            
            count++;
            
            if (sw != null) PerformanceMonitor.StopTiming("OptimizedSymbolRegistry.RegisterExtended");
        }
        
        /// <summary>
        /// Check if a symbol is registered
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRegistered(string symbolName)
        {
            if (string.IsNullOrEmpty(symbolName))
                return false;
                
            var sw = PerformanceMonitor.StartTiming("OptimizedSymbolRegistry.IsRegistered");
            lookups++;
            
            int index = FindIndex(symbolName);
            bool result = index != -1 && entries[index].IsRegistered;
            
            if (result)
                hits++;
            else
                misses++;
                
            if (sw != null) PerformanceMonitor.StopTiming("OptimizedSymbolRegistry.IsRegistered");
            return result;
        }
        
        /// <summary>
        /// Set the priority of a symbol
        /// </summary>
        public static void SetPriority(string symbolName, StructureAnalyzer.StructurePriority priority)
        {
            int index = FindIndex(symbolName);
            if (index != -1)
            {
                ref SymbolEntry entry = ref entries[index];
                entry.Priority = priority;
            }
        }
        
        /// <summary>
        /// Get a list of all registered symbols
        /// </summary>
        public static List<string> GetAllSymbols()
        {
            var sw = PerformanceMonitor.StartTiming("OptimizedSymbolRegistry.GetAllSymbols");
            
            List<string> result = new List<string>(count);
            
            for (int i = 0; i < capacity; i++)
            {
                if (entries[i].Name != null && entries[i].IsRegistered)
                {
                    result.Add(entries[i].Name);
                }
            }
            
            if (sw != null) PerformanceMonitor.StopTiming("OptimizedSymbolRegistry.GetAllSymbols");
            return result;
        }
        
        /// <summary>
        /// Get symbols filtered by priority
        /// </summary>
        public static List<string> GetSymbolsByPriority(StructureAnalyzer.StructurePriority maxPriority)
        {
            var result = new List<string>();
            
            for (int i = 0; i < capacity; i++)
            {
                if (entries[i].Name != null && entries[i].IsRegistered && entries[i].Priority <= maxPriority)
                {
                    result.Add(entries[i].Name);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get symbols from a specific mod
        /// </summary>
        public static List<string> GetSymbolsFromMod(ModContentPack mod)
        {
            if (mod == null)
                return new List<string>();
                
            int modIndex = GetModIndex(mod);
            if (modIndex == -1)
                return new List<string>();
                
            var result = new List<string>();
            
            for (int i = 0; i < capacity; i++)
            {
                if (entries[i].Name != null && entries[i].ModIndex == modIndex && entries[i].IsRegistered)
                {
                    result.Add(entries[i].Name);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Get statistics about the symbol registry
        /// </summary>
        public static Dictionary<string, object> GetStats()
        {
            return new Dictionary<string, object>
            {
                { "TotalSymbols", count },
                { "Capacity", capacity },
                { "LoadFactor", (float)count / capacity },
                { "Collisions", collisions },
                { "CollisionRate", count > 0 ? (float)collisions / count : 0 },
                { "Lookups", lookups },
                { "Hits", hits },
                { "Misses", misses },
                { "HitRate", lookups > 0 ? (float)hits / lookups : 0 },
                { "ModsTracked", modCount },
                { "InternedStrings", internedStrings.Count }
            };
        }
        
        /// <summary>
        /// Find the index of a symbol in the hash table
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindIndex(string name)
        {
            uint hash = ComputeHash(name);
            int index = (int)(hash % (uint)capacity);
            
            // Handle collisions with linear probing
            int probeCount = 0;
            while (entries[index].Name != null)
            {
                // Check if this is the entry we want
                if (entries[index].Hash == hash && string.Equals(entries[index].Name, name, StringComparison.Ordinal))
                {
                    return index;
                }
                
                // Move to next slot (linear probing)
                index = (index + 1) % capacity;
                probeCount++;
                
                // Safety check - this should never happen with proper resizing
                if (probeCount >= capacity)
                {
                    Log.Error($"[KCSG Unbound] Symbol registry hash table probe limit reached for {name}");
                    return -1;
                }
            }
            
            // Not found
            return -1;
        }
        
        /// <summary>
        /// Find an empty slot for a new symbol
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int FindSlot(uint hash, string name)
        {
            int index = (int)(hash % (uint)capacity);
            
            // Find an empty slot with linear probing
            int probeCount = 0;
            while (entries[index].Name != null)
            {
                // If we find the same symbol, use that slot
                if (entries[index].Hash == hash && string.Equals(entries[index].Name, name, StringComparison.Ordinal))
                {
                    return index;
                }
                
                // Move to next slot
                index = (index + 1) % capacity;
                probeCount++;
                
                // Track collisions for statistics
                if (probeCount == 1)
                {
                    collisions++;
                }
                
                // Safety check
                if (probeCount >= capacity)
                {
                    // Resize and try again
                    Resize();
                    return FindSlot(hash, name);
                }
            }
            
            return index;
        }
        
        /// <summary>
        /// Compute a high-quality hash for a string
        /// This is a custom implementation of FNV-1a hash, optimized for faster calculation
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ComputeHash(string str)
        {
            const uint FNV_PRIME = 16777619;
            const uint FNV_OFFSET_BASIS = 2166136261;
            
            uint hash = FNV_OFFSET_BASIS;
            
            // Process 4 characters at a time when possible
            int length = str.Length;
            int i = 0;
            
            for (; i < length; i++)
            {
                hash ^= str[i];
                hash *= FNV_PRIME;
            }
            
            return hash;
        }
        
        /// <summary>
        /// Add a mod to the reference table and return its index
        /// </summary>
        private static int AddModReference(ModContentPack mod)
        {
            if (mod == null)
                return -1;
                
            // Check if already in the cache
            string modId = mod.PackageId;
            if (modIndexCache.TryGetValue(modId, out int existingIndex))
            {
                return existingIndex;
            }
            
            // Add new mod
            if (modCount < mods.Length)
            {
                int index = modCount;
                mods[index] = mod;
                modCount++;
                
                // Add to cache
                modIndexCache[modId] = index;
                
                return index;
            }
            
            // Mod table is full
            Log.Warning($"[KCSG Unbound] Mod reference table is full, cannot add {mod.Name}");
            return -1;
        }
        
        /// <summary>
        /// Get the index of a mod in the reference table
        /// </summary>
        private static int GetModIndex(ModContentPack mod)
        {
            if (mod == null)
                return -1;
                
            // Check cache first
            string modId = mod.PackageId;
            if (modIndexCache.TryGetValue(modId, out int cachedIndex))
            {
                return cachedIndex;
            }
            
            // Look through mods array
            for (int i = 0; i < modCount; i++)
            {
                if (mods[i] == mod || (mods[i] != null && mods[i].PackageId == modId))
                {
                    // Add to cache
                    modIndexCache[modId] = i;
                    return i;
                }
            }
            
            return -1;
        }
        
        /// <summary>
        /// Resize the hash table when it gets too full
        /// </summary>
        private static void Resize()
        {
            var sw = PerformanceMonitor.StartTiming("OptimizedSymbolRegistry.Resize");
            
            int oldCapacity = capacity;
            SymbolEntry[] oldEntries = entries;
            
            // Double the capacity
            capacity = capacity * 2;
            rehashThreshold = (int)(capacity * LoadFactor);
            entries = new SymbolEntry[capacity];
            
            Log.Message($"[KCSG Unbound] Resizing symbol registry from {oldCapacity} to {capacity}");
            
            // Reinsert all existing entries
            for (int i = 0; i < oldCapacity; i++)
            {
                if (oldEntries[i].Name != null)
                {
                    int newSlot = FindSlot(oldEntries[i].Hash, oldEntries[i].Name);
                    entries[newSlot] = oldEntries[i];
                }
            }
            
            if (sw != null) PerformanceMonitor.StopTiming("OptimizedSymbolRegistry.Resize");
        }
        
        /// <summary>
        /// Intern a string to save memory (if strings are the same, store only one copy)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string InternString(string str)
        {
            if (internedStrings.TryGetValue(str, out string existing))
            {
                return existing;
            }
            
            internedStrings[str] = str;
            return str;
        }
        
        /// <summary>
        /// Get memory usage information
        /// </summary>
        public static long GetEstimatedMemoryUsage()
        {
            // Base array size
            long memoryUsage = capacity * (4 + 4 + 1 + IntPtr.Size); // Hash + ModIndex + Flags + Name reference
            
            // String table (rough estimate)
            foreach (string s in internedStrings.Keys)
            {
                memoryUsage += s.Length * sizeof(char) + IntPtr.Size;
            }
            
            // Mod references
            memoryUsage += mods.Length * IntPtr.Size;
            
            // Dictionary overhead (very rough estimate)
            memoryUsage += internedStrings.Count * (IntPtr.Size * 3);
            memoryUsage += modIndexCache.Count * (IntPtr.Size * 3);
            
            return memoryUsage;
        }
    }
} 