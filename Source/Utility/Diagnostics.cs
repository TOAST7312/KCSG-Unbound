using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Handles diagnostic logging for KCSG Unbound
    /// </summary>
    public static class Diagnostics
    {
        // Whether verbose logging is enabled
        private static bool verboseLogging = false;
        
        // Path to the diagnostic log file
        private static string diagnosticLogPath = null;
        
        // StringBuilder to buffer log messages
        private static StringBuilder logBuffer = new StringBuilder();
        
        // Time of last flush
        private static DateTime lastFlush = DateTime.MinValue;
        
        // Track if initialized
        private static bool initialized = false;
        
        /// <summary>
        /// Initialize the diagnostics system
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
                return;
                
            try
            {
                // Set up the log path
                diagnosticLogPath = Path.Combine(GenFilePaths.ConfigFolderPath, "KCSG_Unbound_Diagnostics.log");
                
                // Create a new log file
                using (StreamWriter writer = new StreamWriter(diagnosticLogPath, false))
                {
                    writer.WriteLine($"[{DateTime.Now}] KCSG Unbound diagnostic log initialized");
                    writer.WriteLine("----------------------------------------");
                }
                
                initialized = true;
                Log.Message("[KCSG Unbound] Diagnostic logging initialized");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Failed to initialize diagnostic logging: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Set verbose logging mode
        /// </summary>
        public static void SetVerboseLogging(bool enabled)
        {
            verboseLogging = enabled;
            LogDiagnostic($"Verbose logging {(enabled ? "enabled" : "disabled")}");
        }
        
        /// <summary>
        /// Log a diagnostic message
        /// </summary>
        public static void LogDiagnostic(string message, bool force = false)
        {
            if (!initialized)
                Initialize();
            
            try
            {
                // Add timestamp
                string timestampedMessage = $"[{DateTime.Now}] {message}";
                
                // Add to buffer
                lock (logBuffer)
                {
                    logBuffer.AppendLine(timestampedMessage);
                }
                
                // Check if we should flush
                if (force || (DateTime.Now - lastFlush).TotalSeconds > 5)
                {
                    FlushLogBuffer();
                }
            }
            catch
            {
                // Ignore errors during logging
            }
        }
        
        /// <summary>
        /// Log verbose diagnostic information
        /// </summary>
        public static void LogVerbose(string message)
        {
            if (verboseLogging)
            {
                LogDiagnostic($"[VERBOSE] {message}");
            }
            else
            {
                // Still log to file but not console
                TryAppendToFile($"[{DateTime.Now}] [VERBOSE] {message}");
            }
        }
        
        /// <summary>
        /// Log warning information to diagnostics
        /// </summary>
        public static void LogWarning(string message)
        {
            LogDiagnostic($"[WARNING] {message}");
            
            // Also log to main log if verbose is enabled
            if (verboseLogging)
            {
                Log.Warning($"[KCSG Unbound] {message}");
            }
        }
        
        /// <summary>
        /// Log error information to diagnostics
        /// </summary>
        public static void LogError(string message)
        {
            LogDiagnostic($"[ERROR] {message}", true);
            Log.Error($"[KCSG Unbound] {message}");
        }
        
        /// <summary>
        /// Flush the log buffer to disk
        /// </summary>
        private static void FlushLogBuffer()
        {
            try
            {
                string bufferContent;
                
                // Extract and clear buffer
                lock (logBuffer)
                {
                    if (logBuffer.Length == 0)
                        return;
                        
                    bufferContent = logBuffer.ToString();
                    logBuffer.Clear();
                }
                
                // Write to file
                TryAppendToFile(bufferContent);
                
                lastFlush = DateTime.Now;
            }
            catch
            {
                // Ignore errors during flushing
            }
        }
        
        /// <summary>
        /// Try to append text to the log file
        /// </summary>
        private static void TryAppendToFile(string content)
        {
            if (string.IsNullOrEmpty(diagnosticLogPath))
                return;
            
            try
            {
                using (StreamWriter writer = new StreamWriter(diagnosticLogPath, true))
                {
                    writer.Write(content);
                }
            }
            catch
            {
                // Ignore file IO errors
            }
        }
    }
} 