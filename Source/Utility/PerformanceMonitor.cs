using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Performance monitoring and profiling utility
    /// </summary>
    public static class PerformanceMonitor
    {
        // Whether monitoring is enabled
        private static bool enabled = false;
        
        // Track timing for different operations
        private static Dictionary<string, List<double>> operationTimings = new Dictionary<string, List<double>>();
        
        // Track counts for different metrics
        private static Dictionary<string, int> counters = new Dictionary<string, int>();
        
        // Cache hit tracking
        private static Dictionary<string, (int hits, int misses)> cacheStats = new Dictionary<string, (int, int)>();
        
        // For detailed profiling of hot paths
        private static Dictionary<string, Stopwatch> activeStopwatches = new Dictionary<string, Stopwatch>();
        
        // Track memory usage
        private static long startingMemory = 0;
        private static long peakMemory = 0;
        
        /// <summary>
        /// Enable performance monitoring
        /// </summary>
        public static void Enable()
        {
            enabled = true;
            Reset();
            startingMemory = GC.GetTotalMemory(false);
            Log.Message("[KCSG Unbound] Performance monitoring enabled");
        }
        
        /// <summary>
        /// Disable performance monitoring
        /// </summary>
        public static void Disable()
        {
            enabled = false;
            Log.Message("[KCSG Unbound] Performance monitoring disabled");
        }
        
        /// <summary>
        /// Reset all monitoring data
        /// </summary>
        public static void Reset()
        {
            operationTimings.Clear();
            counters.Clear();
            cacheStats.Clear();
            activeStopwatches.Clear();
            startingMemory = GC.GetTotalMemory(false);
            peakMemory = startingMemory;
        }
        
        /// <summary>
        /// Start timing an operation
        /// </summary>
        public static Stopwatch StartTiming(string operationName)
        {
            if (!enabled) return null;
            
            var sw = new Stopwatch();
            sw.Start();
            activeStopwatches[operationName] = sw;
            return sw;
        }
        
        /// <summary>
        /// Stop timing an operation and record the result
        /// </summary>
        public static void StopTiming(string operationName)
        {
            if (!enabled) return;
            
            if (activeStopwatches.TryGetValue(operationName, out var sw))
            {
                sw.Stop();
                double milliseconds = sw.Elapsed.TotalMilliseconds;
                
                if (!operationTimings.ContainsKey(operationName))
                {
                    operationTimings[operationName] = new List<double>();
                }
                
                operationTimings[operationName].Add(milliseconds);
                activeStopwatches.Remove(operationName);
                
                // Check memory usage
                long currentMemory = GC.GetTotalMemory(false);
                if (currentMemory > peakMemory)
                {
                    peakMemory = currentMemory;
                }
            }
        }
        
        /// <summary>
        /// Increment a counter
        /// </summary>
        public static void IncrementCounter(string counterName, int amount = 1)
        {
            if (!enabled) return;
            
            if (!counters.ContainsKey(counterName))
            {
                counters[counterName] = 0;
            }
            
            counters[counterName] += amount;
        }
        
        /// <summary>
        /// Record a cache hit
        /// </summary>
        public static void RecordCacheHit(string cacheName)
        {
            if (!enabled) return;
            
            if (!cacheStats.ContainsKey(cacheName))
            {
                cacheStats[cacheName] = (0, 0);
            }
            
            var current = cacheStats[cacheName];
            cacheStats[cacheName] = (current.hits + 1, current.misses);
        }
        
        /// <summary>
        /// Record a cache miss
        /// </summary>
        public static void RecordCacheMiss(string cacheName)
        {
            if (!enabled) return;
            
            if (!cacheStats.ContainsKey(cacheName))
            {
                cacheStats[cacheName] = (0, 0);
            }
            
            var current = cacheStats[cacheName];
            cacheStats[cacheName] = (current.hits, current.misses + 1);
        }
        
        /// <summary>
        /// Get performance report
        /// </summary>
        public static string GenerateReport()
        {
            if (!enabled) return "Performance monitoring is disabled";
            
            StringBuilder report = new StringBuilder();
            report.AppendLine("========== KCSG Unbound Performance Report ==========");
            report.AppendLine($"Report generated: {DateTime.Now}");
            report.AppendLine();
            
            // Memory usage stats
            long currentMemory = GC.GetTotalMemory(false);
            report.AppendLine("--- Memory Usage ---");
            report.AppendLine($"Starting: {FormatBytes(startingMemory)}");
            report.AppendLine($"Current: {FormatBytes(currentMemory)}");
            report.AppendLine($"Peak: {FormatBytes(peakMemory)}");
            report.AppendLine($"Delta: {FormatBytes(currentMemory - startingMemory)}");
            report.AppendLine();
            
            // Operation timings
            report.AppendLine("--- Operation Timings ---");
            foreach (var kvp in operationTimings.OrderByDescending(x => x.Value.Sum()))
            {
                string name = kvp.Key;
                List<double> timings = kvp.Value;
                
                double total = timings.Sum();
                double avg = timings.Count > 0 ? total / timings.Count : 0;
                double min = timings.Count > 0 ? timings.Min() : 0;
                double max = timings.Count > 0 ? timings.Max() : 0;
                
                report.AppendLine($"{name}:");
                report.AppendLine($"  Calls: {timings.Count}");
                report.AppendLine($"  Total: {total:F2}ms");
                report.AppendLine($"  Avg: {avg:F2}ms");
                report.AppendLine($"  Min: {min:F2}ms");
                report.AppendLine($"  Max: {max:F2}ms");
            }
            report.AppendLine();
            
            // Counters
            report.AppendLine("--- Counters ---");
            foreach (var kvp in counters.OrderByDescending(x => x.Value))
            {
                report.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
            report.AppendLine();
            
            // Cache stats
            report.AppendLine("--- Cache Stats ---");
            foreach (var kvp in cacheStats)
            {
                string name = kvp.Key;
                int hits = kvp.Value.hits;
                int misses = kvp.Value.misses;
                int total = hits + misses;
                double hitRate = total > 0 ? (double)hits / total * 100 : 0;
                
                report.AppendLine($"{name}:");
                report.AppendLine($"  Hits: {hits}");
                report.AppendLine($"  Misses: {misses}");
                report.AppendLine($"  Total: {total}");
                report.AppendLine($"  Hit Rate: {hitRate:F2}%");
            }
            
            report.AppendLine();
            report.AppendLine("========== End of Performance Report ==========");
            
            return report.ToString();
        }
        
        /// <summary>
        /// Write performance report to log and to a file
        /// </summary>
        public static void WriteReportToLog()
        {
            if (!enabled) return;
            
            try
            {
                string report = GenerateReport();
                
                // Write to log
                string[] lines = report.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    Log.Message($"[KCSG Unbound] {line}");
                }
                
                // Write to file
                string reportPath = System.IO.Path.Combine(GenFilePaths.ConfigFolderPath, "KCSG_Performance_Report.txt");
                System.IO.File.WriteAllText(reportPath, report);
                
                Log.Message($"[KCSG Unbound] Performance report written to {reportPath}");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error writing performance report: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Format bytes to a human-readable string
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int suffixIndex = 0;
            double value = bytes;
            
            while (value >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                value /= 1024;
                suffixIndex++;
            }
            
            return $"{value:F2} {suffixes[suffixIndex]} ({bytes:N0} bytes)";
        }
    }
} 