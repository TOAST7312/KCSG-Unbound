using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Utility for capturing diagnostic information
    /// </summary>
    public static class Diagnostics
    {
        private static List<string> startupMessages = new List<string>();
        private static bool hasDumped = false;
        
        /// <summary>
        /// Safely log a diagnostic message
        /// </summary>
        public static void LogDiagnostic(string message)
        {
            try
            {
                startupMessages.Add($"[{DateTime.Now.ToString("HH:mm:ss.fff")}] {message}");
                Log.Message($"[KCSG Diagnostics] {message}");
            }
            catch
            {
                // Silently fail
            }
        }
        
        /// <summary>
        /// Dump diagnostic info to a file
        /// </summary>
        public static void DumpDiagnostics()
        {
            if (hasDumped) return;
            
            try
            {
                string path = Path.Combine(Application.persistentDataPath, "KCSG_Startup_Diagnostic.log");
                
                string content = $"=== KCSG Unbound Startup Diagnostic: {DateTime.Now} ===\n\n";
                content += $"Unity Version: {Application.unityVersion}\n";
                content += $"Platform: {Application.platform}\n";
                content += $"Product Name: {Application.productName}\n";
                content += $"System Memory: {SystemInfo.systemMemorySize} MB\n";
                content += $"Processor: {SystemInfo.processorType}\n";
                content += $"Graphics Card: {SystemInfo.graphicsDeviceName}\n\n";
                
                content += "=== Startup Log ===\n";
                foreach (var msg in startupMessages)
                {
                    content += msg + "\n";
                }
                
                content += "\n=== Assemblies ===\n";
                try
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        content += $"{assembly.FullName}\n";
                    }
                }
                catch (Exception ex)
                {
                    content += $"Error getting assemblies: {ex.Message}\n";
                }
                
                File.WriteAllText(path, content);
                hasDumped = true;
                
                LogDiagnostic($"Startup diagnostic written to {path}");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Diagnostics] Failed to write diagnostics: {ex}");
            }
        }
        
        /// <summary>
        /// Setup diagnostics collector
        /// </summary>
        public static void Initialize()
        {
            LogDiagnostic("Diagnostics initialized");
            
            // Schedule diagnostics to be dumped at the end of loading
            LongEventHandler.ExecuteWhenFinished(DumpDiagnostics);
            
            // Also set a backup timer to dump in case the normal flow is interrupted
            GameObject go = new GameObject("KCSG_DiagnosticDumper");
            var component = go.AddComponent<DiagnosticDumper>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }
        
        /// <summary>
        /// Component to ensure diagnostics are dumped
        /// </summary>
        private class DiagnosticDumper : MonoBehaviour
        {
            private float timeToWait = 30f;
            
            public void Update()
            {
                timeToWait -= Time.deltaTime;
                if (timeToWait <= 0f)
                {
                    DumpDiagnostics();
                    Destroy(this);
                }
            }
        }
    }
} 