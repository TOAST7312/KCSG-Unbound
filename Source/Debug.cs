using Verse;

namespace KCSG
{
    /// <summary>
    /// Legacy debug class that forwards to the new Diagnostics system
    /// This exists for backward compatibility with older code
    /// </summary>
    public static class Debug
    {
        /// <summary>
        /// Logs a message in a backward-compatible way
        /// </summary>
        public static void Message(string message)
        {
            // Forward to both the RimWorld log and our diagnostic system
            Log.Message($"[KCSG] {message}");
            
            // Also log to our new diagnostics system
            Diagnostics.LogDiagnostic($"[Legacy] {message}");
        }
        
        /// <summary>
        /// Logs a warning in a backward-compatible way
        /// </summary>
        public static void Warning(string message)
        {
            // Forward to the warning system
            Log.Warning($"[KCSG] {message}");
            
            // Also log to our new diagnostics system
            Diagnostics.LogWarning($"[Legacy] {message}");
        }
        
        /// <summary>
        /// Logs an error in a backward-compatible way
        /// </summary>
        public static void Error(string message)
        {
            // Forward to the error system
            Log.Error($"[KCSG] {message}");
            
            // Also log to our new diagnostics system
            Diagnostics.LogError($"[Legacy] {message}");
        }
    }
} 