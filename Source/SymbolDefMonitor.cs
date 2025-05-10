using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Monitors the number of SymbolDefs registered and provides debug information
    /// </summary>
    public class SymbolDefMonitor : GameComponent
    {
        // Static instance for easy access
        private static SymbolDefMonitor instance;
        
        // Counter for registered symbols during this session
        private int registeredSymbolsCount = 0;
        private int registeredDefsCount = 0;
        
        // Time of last check
        private int lastCheckTick = 0;
        
        // Check interval (once per 250 ticks = about 4 seconds)
        private const int CHECK_INTERVAL = 250;
        
        // Error tracking
        private bool hadErrors = false;
        private List<string> recentErrors = new List<string>();
        private const int MAX_RECENT_ERRORS = 10;
        
        // UI state
        private bool showDebugWindow = false;
        private Vector2 scrollPosition = Vector2.zero;
        private Rect windowRect = new Rect(200f, 200f, 500f, 400f);
        
        /// <summary>
        /// Creates a new SymbolDefMonitor for a game
        /// </summary>
        /// <param name="game">The game this monitor is attached to</param>
        public SymbolDefMonitor(Game game) : base()
        {
            instance = this;
            
            try
            {
                // Initialize counters
                UpdateCounters();
                Log.Message($"[KCSG Unbound] Monitor initialized, tracking {registeredDefsCount} defs and {registeredSymbolsCount} symbols");
            }
            catch (Exception ex)
            {
                LogError($"Error initializing SymbolDefMonitor: {ex}");
            }
        }
        
        /// <summary>
        /// Access the singleton instance of the monitor
        /// </summary>
        public static SymbolDefMonitor Instance => instance;

        /// <summary>
        /// Updates the monitor on each tick
        /// </summary>
        public override void GameComponentTick()
        {
            // Only update occasionally to avoid performance impact
            if (Find.TickManager.TicksGame - lastCheckTick >= CHECK_INTERVAL)
            {
                try
                {
                    lastCheckTick = Find.TickManager.TicksGame;
                    UpdateCounters();
                    
                    // Log warning if approaching limit (only in debug mode)
                    if (Prefs.DevMode && registeredDefsCount > 60000)
                    {
                        Log.Warning($"[KCSG Unbound] Number of SymbolDefs ({registeredDefsCount}) is approaching the vanilla limit (65,535). Your mods are now relying on KCSG Unbound to work properly.");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error updating symbol counters: {ex}");
                }
            }
        }
        
        /// <summary>
        /// Safely update the counters from the registry
        /// </summary>
        private void UpdateCounters()
        {
            try
            {
                registeredSymbolsCount = SymbolRegistry.RegisteredSymbolCount;
                registeredDefsCount = SymbolRegistry.RegisteredDefCount;
            }
            catch (Exception ex)
            {
                LogError($"Failed to read registry counts: {ex}");
            }
        }
        
        /// <summary>
        /// Log an error and add it to the recent errors list
        /// </summary>
        private void LogError(string message)
        {
            Log.Error($"[KCSG Unbound] {message}");
            hadErrors = true;
            
            // Add to recent errors, keeping only the most recent ones
            recentErrors.Add($"{DateTime.Now.ToShortTimeString()}: {message}");
            if (recentErrors.Count > MAX_RECENT_ERRORS)
            {
                recentErrors.RemoveAt(0);
            }
        }
        
        /// <summary>
        /// Draw UI when needed
        /// </summary>
        public override void GameComponentOnGUI()
        {
            try
            {
                if (showDebugWindow && Prefs.DevMode)
                {
                    windowRect = GUI.Window(594873, windowRect, DrawDebugWindow, "KCSG Unbound Symbol Monitor");
                }
                
                // Draw toggle button when Dev Mode is enabled
                if (Prefs.DevMode)
                {
                    Rect buttonRect = new Rect(Screen.width - 150, 10, 140, 24);
                    if (Widgets.ButtonText(buttonRect, "Symbol Monitor"))
                    {
                        showDebugWindow = !showDebugWindow;
                    }
                    
                    // Draw error indicator if there have been errors
                    if (hadErrors)
                    {
                        Rect errorRect = new Rect(Screen.width - 150, 40, 140, 24);
                        GUI.color = Color.red;
                        if (Widgets.ButtonText(errorRect, "KCSG Errors!"))
                        {
                            showDebugWindow = true;
                        }
                        GUI.color = Color.white;
                    }
                }
            }
            catch (Exception ex)
            {
                // Avoid recursive errors in the error handling UI
                if (!ex.Message.Contains("Error in UI drawing"))
                {
                    Log.Error($"[KCSG Unbound] Error in UI drawing: {ex}");
                }
            }
        }
        
        /// <summary>
        /// Draw the debug window with all monitoring information
        /// </summary>
        private void DrawDebugWindow(int windowId)
        {
            try
            {
                Text.Font = GameFont.Small;
                float margin = 10f;
                float lineHeight = Text.LineHeight;
                
                Rect innerRect = windowRect.AtZero();
                innerRect = innerRect.ContractedBy(margin);
                
                Rect statusRect = innerRect.TopPartPixels(lineHeight * 5);
                
                float listTop = statusRect.yMax + margin;
                float remainingHeight = innerRect.height - statusRect.height - margin * 2;
                
                // If we have errors, show them in the top part
                if (hadErrors && recentErrors.Count > 0)
                {
                    float errorSectionHeight = Math.Min(recentErrors.Count * lineHeight + lineHeight, 120f);
                    Rect errorRect = new Rect(innerRect.x, listTop, innerRect.width, errorSectionHeight);
                    
                    // Error section header
                    GUI.color = Color.red;
                    Widgets.Label(errorRect.TopPartPixels(lineHeight), "Recent Errors:");
                    GUI.color = Color.white;
                    
                    // Error list
                    Rect errorListRect = new Rect(errorRect.x + 10f, errorRect.y + lineHeight, errorRect.width - 10f, errorRect.height - lineHeight);
                    float y = errorListRect.y;
                    foreach (string error in recentErrors)
                    {
                        Widgets.Label(new Rect(errorListRect.x, y, errorListRect.width, lineHeight), error);
                        y += lineHeight;
                    }
                    
                    // Adjust remaining space
                    listTop = errorRect.yMax + margin;
                    remainingHeight -= (errorSectionHeight + margin);
                }
                
                Rect listRect = new Rect(innerRect.x, listTop, innerRect.width, remainingHeight);
                Rect viewRect = new Rect(0, 0, listRect.width - 16f, Math.Max(lineHeight * 20, SymbolRegistry.RegisteredDefCount * lineHeight * 0.5f));
                
                // Draw status information
                float statusY = statusRect.y;
                
                Widgets.Label(new Rect(statusRect.x, statusY, statusRect.width, lineHeight), $"Total SymbolDefs registered: {registeredDefsCount}");
                statusY += lineHeight;
                
                Widgets.Label(new Rect(statusRect.x, statusY, statusRect.width, lineHeight), $"Total Symbols registered: {registeredSymbolsCount}");
                statusY += lineHeight;
                
                string status = registeredDefsCount > 65535 ? "EXCEEDED VANILLA LIMIT (Using Unbound)" : "Within vanilla limits";
                string color = registeredDefsCount > 65535 ? "<color=orange>" : "<color=green>";
                
                Widgets.Label(new Rect(statusRect.x, statusY, statusRect.width, lineHeight), $"Status: {color}{status}</color>");
                statusY += lineHeight;
                
                bool isPrepatched = false;
                try 
                {
                    isPrepatched = KCSGPrepatchData.Instance.IsPrepatched();
                }
                catch 
                {
                    // In case of error, assume not prepatched
                }
                
                Widgets.Label(new Rect(statusRect.x, statusY, statusRect.width, lineHeight), 
                    $"Unbound mode: {(isPrepatched ? "<color=green>Prepatched</color>" : "<color=yellow>Runtime</color>")}");
                statusY += lineHeight;
                
                // Draw action buttons
                Rect buttonRect = new Rect(statusRect.x, statusY, statusRect.width * 0.48f, lineHeight);
                if (Widgets.ButtonText(buttonRect, "Copy statistics to log"))
                {
                    LogStatistics();
                }
                
                // Add clear errors button
                if (hadErrors)
                {
                    Rect clearErrorsRect = new Rect(statusRect.x + statusRect.width * 0.52f, statusY, statusRect.width * 0.48f, lineHeight);
                    if (Widgets.ButtonText(clearErrorsRect, "Clear Errors"))
                    {
                        recentErrors.Clear();
                        hadErrors = false;
                    }
                }
                
                // Draw list of defs (just the count by first letter to avoid performance issues)
                Widgets.BeginScrollView(listRect, ref scrollPosition, viewRect);
                
                try
                {
                    if (registeredDefsCount > 0)
                    {
                        DrawDefsList(viewRect, lineHeight);
                    }
                    else
                    {
                        Widgets.Label(new Rect(0, 0, viewRect.width, lineHeight), "No defs registered yet");
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error drawing defs list: {ex}");
                    Widgets.Label(new Rect(0, 0, viewRect.width, lineHeight), $"<color=red>Error drawing defs list: {ex.Message}</color>");
                }
                
                Widgets.EndScrollView();
                
                // Allow window to be dragged
                GUI.DragWindow();
            }
            catch (Exception ex)
            {
                // Last-resort error handling to avoid issues with the debug window itself
                Log.Error($"[KCSG Unbound] Error in debug window: {ex}");
            }
        }
        
        /// <summary>
        /// Draw the list of defs by first letter
        /// </summary>
        private void DrawDefsList(Rect viewRect, float lineHeight)
        {
            Dictionary<char, int> defsByFirstLetter = new Dictionary<char, int>();
            foreach (string defName in SymbolRegistry.AllRegisteredDefNames)
            {
                if (string.IsNullOrEmpty(defName)) continue;
                
                char firstChar = char.ToUpper(defName[0]);
                if (defsByFirstLetter.ContainsKey(firstChar))
                {
                    defsByFirstLetter[firstChar]++;
                }
                else
                {
                    defsByFirstLetter[firstChar] = 1;
                }
            }
            
            // Sort by letter
            List<char> sortedLetters = new List<char>(defsByFirstLetter.Keys);
            sortedLetters.Sort();
            
            float y = 0;
            foreach (char letter in sortedLetters)
            {
                int count = defsByFirstLetter[letter];
                Widgets.Label(new Rect(0, y, viewRect.width, lineHeight), $"Defs starting with '{letter}': {count}");
                y += lineHeight;
            }
        }
        
        /// <summary>
        /// Log detailed statistics to the game log
        /// </summary>
        private void LogStatistics()
        {
            Log.Message("════════════════════════════════════════════════════");
            Log.Message($"[KCSG Unbound] Symbol Registry Statistics:");
            Log.Message($"Total SymbolDefs registered: {registeredDefsCount}");
            Log.Message($"Total Symbols registered: {registeredSymbolsCount}");
            
            if (registeredDefsCount > 65535)
            {
                Log.Message($"Status: EXCEEDED VANILLA LIMIT - Unbound active and working");
                Log.Message($"Defs above vanilla limit: {registeredDefsCount - 65535}");
            }
            else
            {
                Log.Message($"Status: Within vanilla limits ({registeredDefsCount} / 65535)");
            }
            
            bool isPrepatched = false;
            try 
            {
                isPrepatched = KCSGPrepatchData.Instance.IsPrepatched();
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG Unbound] Error checking prepatch status: {ex}");
            }
            
            Log.Message($"Prepatched mode active: {isPrepatched}");
            
            if (hadErrors)
            {
                Log.Message("════════════════════════════════════════════════════");
                Log.Message($"Recent Errors ({recentErrors.Count}):");
                foreach (string error in recentErrors)
                {
                    Log.Message($"  - {error}");
                }
            }
            
            Log.Message("════════════════════════════════════════════════════");
        }
        
        /// <summary>
        /// Save our state
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            
            // We don't need to save anything - registry data is reloaded at game start
        }
    }
} 