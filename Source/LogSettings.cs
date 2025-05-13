using System;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Controls logging behavior for KCSG Unbound
    /// </summary>
    public static class LogSettings
    {
        // Control whether verbose logging is enabled
        private static bool verboseLogging = false;
        
        // Enable verbose logging
        public static void EnableVerboseLogging()
        {
            verboseLogging = true;
            Log.Message("[KCSG Unbound] Verbose logging enabled");
        }
        
        // Disable verbose logging
        public static void DisableVerboseLogging()
        {
            verboseLogging = false;
            Log.Message("[KCSG Unbound] Verbose logging disabled");
        }
        
        // Check if verbose logging is enabled
        public static bool VerboseLoggingEnabled => verboseLogging;
        
        // Log a verbose message - goes to file always, console conditionally
        public static void LogVerbose(string message)
        {
            // Don't log to console at all, only to file if verbose is enabled
            if (verboseLogging)
            {
                // Write to diagnostic file only, no console output
                Diagnostics.LogVerbose(message);
            }
        }
        
        // Log warning only if verbose logging is enabled
        public static void LogWarningVerbose(string message)
        {
            if (VerboseLoggingEnabled)
            {
                Log.Warning(message);
            }
        }
        
        // Always log errors to both console and diagnostic file
        public static void LogError(string message)
        {
            Log.Error($"[KCSG Unbound] {message}");
        }

        // Add this method to ensure critical logs always appear
        public static void LogCritical(string message)
        {
            Log.Message($"[KCSG Unbound] {message}");
        }
    }
} 