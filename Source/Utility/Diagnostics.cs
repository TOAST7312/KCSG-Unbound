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
        // Whether verbose logging is enabled for console output
        private static bool verboseLogging = false;
        
        // Path to the diagnostic log file
        private static string diagnosticLogPath = null;
        
        // StringBuilder to buffer log messages
        private static StringBuilder logBuffer = new StringBuilder();
        
        // Time of last flush
        private static DateTime lastFlush = DateTime.MinValue;
        
        // Track if initialized
        private static bool initialized = false;
        
        // Minimum log level based on settings
        private static LogLevel minimumLogLevel = LogLevel.Normal;
        
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
                
                // Set verbose logging based on settings or dev mode
                verboseLogging = Prefs.DevMode || KCSGUnboundSettings.LoggingLevel >= LogLevel.Verbose;
                minimumLogLevel = KCSGUnboundSettings.LoggingLevel;
                
                // Log initialization
                WriteToLog($"[{DateTime.Now}] Diagnostics initialized with log level: {minimumLogLevel}, verbose: {verboseLogging}");
                
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
            WriteToLog($"[{DateTime.Now}] Verbose logging {(enabled ? "enabled" : "disabled")}");
        }
        
        /// <summary>
        /// Log a diagnostic message - always goes to file, conditionally to console
        /// </summary>
        public static void LogDiagnostic(string message, bool force = false)
        {
            if (!initialized)
                Initialize();
            
            try
            {
                // Add timestamp
                string timestampedMessage = $"[{DateTime.Now}] {message}";
                
                // Always write to the file regardless of settings
                WriteToLog(timestampedMessage);
                
                // Only write to console if verbose logging is enabled or if forced
                if (verboseLogging || force || minimumLogLevel >= LogLevel.Verbose)
                {
                    Log.Message($"[KCSG Unbound] {message}");
                }
            }
            catch
            {
                // Ignore errors during logging
            }
        }
        
        /// <summary>
        /// Log verbose diagnostic information - always to file, conditionally to console
        /// </summary>
        public static void LogVerbose(string message)
        {
            if (!initialized)
                Initialize();
                
            // Always write to the log file
            WriteToLog($"[{DateTime.Now}] [VERBOSE] {message}");
            
            // Only write to console if verbose logging is enabled
            if (verboseLogging || minimumLogLevel >= LogLevel.Verbose)
            {
                Log.Message($"[KCSG Unbound] [VERBOSE] {message}");
            }
        }
        
        /// <summary>
        /// Log warning information to diagnostics
        /// </summary>
        public static void LogWarning(string message)
        {
            if (!initialized)
                Initialize();
                
            // Always log warnings to file
            WriteToLog($"[{DateTime.Now}] [WARNING] {message}");
            
            // Only log to console based on minimum level
            if (minimumLogLevel >= LogLevel.Minimal)
            {
                Log.Warning($"[KCSG Unbound] {message}");
            }
        }
        
        /// <summary>
        /// Log error information to diagnostics - always goes to both file and console
        /// </summary>
        public static void LogError(string message)
        {
            if (!initialized)
                Initialize();
                
            // Always log errors
            WriteToLog($"[{DateTime.Now}] [ERROR] {message}");
            Log.Error($"[KCSG Unbound] {message}");
        }
        
        /// <summary>
        /// Write directly to the log file
        /// </summary>
        private static void WriteToLog(string message)
        {
            if (string.IsNullOrEmpty(diagnosticLogPath))
                return;
                
            try
            {
                // Ensure there's a newline
                if (!message.EndsWith(Environment.NewLine))
                    message += Environment.NewLine;
                    
                // Use a direct file append to ensure it gets written
                using (StreamWriter writer = new StreamWriter(diagnosticLogPath, true))
                {
                    writer.Write(message);
                }
            }
            catch (Exception ex)
            {
                // If we can't write to the log file, at least try to log to the console
                try
                {
                    Log.Error($"[KCSG Unbound] Failed to write to diagnostic log: {ex.Message}");
                }
                catch
                {
                    // Truly last-resort - just swallow the error if even that fails
                }
            }
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
                
                // Write to file directly
                WriteToLog(bufferContent);
                
                lastFlush = DateTime.Now;
            }
            catch (Exception ex)
            {
                // Try to log the error
                try
                {
                    Log.Error($"[KCSG Unbound] Error flushing log buffer: {ex.Message}");
                }
                catch
                {
                    // Ignore if even that fails
                }
            }
        }
        
        /// <summary>
        /// Try to append text to the log file - deprecated in favor of WriteToLog
        /// </summary>
        private static void TryAppendToFile(string content)
        {
            WriteToLog(content);
        }
    }
} 