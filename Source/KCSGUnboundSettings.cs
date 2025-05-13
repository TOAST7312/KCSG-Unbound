using System;
using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace KCSG
{
    /// <summary>
    /// Settings for KCSG Unbound
    /// </summary>
    public class KCSGUnboundSettings : ModSettings
    {
        // Auto-detection setting
        public bool enableAutoDetection = false;
        
        // Structure priority threshold
        public int structurePriorityThreshold = 2; // Medium priority by default

        // Instance fields for settings
        public bool debugModeValue = false;
        public LogLevel loggingLevelValue = LogLevel.Normal;
        public bool enableFullScanningValue = true;
        public bool highPerformanceModeValue = true;
        public bool lazyRegistrationValue = true;
        public CollisionPolicy hashCollisionPolicyValue = CollisionPolicy.FirstDefWins;
        public bool showDebugVisualsValue = false;
        public bool aggressiveOptimizationsValue = false;
        
        // Static fields for global access
        public static bool DebugMode = false;
        public static LogLevel LoggingLevel = LogLevel.Normal;
        public static bool EnableFullScanning = true;
        public static bool HighPerformanceMode = true;
        public static bool LazyRegistration = true;
        public static CollisionPolicy HashCollisionPolicy = CollisionPolicy.FirstDefWins;
        public static bool ShowDebugVisuals = false;
        public static bool AggressiveOptimizations = false;

        // Constructor to initialize static fields
        public KCSGUnboundSettings()
        {
            // Update static fields from instance fields
            UpdateStaticFields();
        }
        
        // Helper method to update static fields from instance fields
        private void UpdateStaticFields()
        {
            DebugMode = debugModeValue;
            LoggingLevel = loggingLevelValue;
            EnableFullScanning = enableFullScanningValue;
            HighPerformanceMode = highPerformanceModeValue;
            LazyRegistration = lazyRegistrationValue;
            HashCollisionPolicy = hashCollisionPolicyValue;
            ShowDebugVisuals = showDebugVisualsValue;
            AggressiveOptimizations = aggressiveOptimizationsValue;
        }

        /// <summary>
        /// Save settings to file
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            
            Scribe_Values.Look(ref enableAutoDetection, "enableAutoDetection", false);
            Scribe_Values.Look(ref structurePriorityThreshold, "structurePriorityThreshold", 2);
            Scribe_Values.Look(ref debugModeValue, "debugMode", false);
            Scribe_Values.Look(ref enableFullScanningValue, "enableFullScanning", true);
            Scribe_Values.Look(ref highPerformanceModeValue, "highPerformanceMode", true);
            Scribe_Values.Look(ref lazyRegistrationValue, "lazyRegistration", true);
            Scribe_Values.Look(ref aggressiveOptimizationsValue, "aggressiveOptimizations", false);
            Scribe_Values.Look(ref showDebugVisualsValue, "showDebugVisuals", false);
            
            // Save enum values as integers
            int logLevel = (int)loggingLevelValue;
            Scribe_Values.Look(ref logLevel, "loggingLevel", (int)LogLevel.Normal);
            loggingLevelValue = (LogLevel)logLevel;
            
            int collisionPolicy = (int)hashCollisionPolicyValue;
            Scribe_Values.Look(ref collisionPolicy, "hashCollisionPolicy", (int)CollisionPolicy.FirstDefWins);
            hashCollisionPolicyValue = (CollisionPolicy)collisionPolicy;
            
            // Update static fields to match instance fields
            UpdateStaticFields();
        }
        
        /// <summary>
        /// Get the structure priority threshold as an enum value
        /// </summary>
        public StructureAnalyzer.StructurePriority GetStructurePriorityThreshold()
        {
            // Ensure the value is within valid bounds
            int clampedValue = Math.Max(0, Math.Min(4, structurePriorityThreshold));
            
            // Convert to enum
            return (StructureAnalyzer.StructurePriority)clampedValue;
        }
    }

    /// <summary>
    /// Mod settings window for KCSG Unbound
    /// </summary>
    public class KCSGUnboundModMenu : Mod
    {
        private KCSGUnboundSettings settings;
        private Vector2 scrollPosition = Vector2.zero;
        private static readonly Color WarningColor = new Color(1f, 0.3f, 0.3f); // Bright red for warning
        private static readonly string[] priorityLabels = new[] 
        { 
            "Essential Only", 
            "High Priority", 
            "Medium Priority (Default)", 
            "Low Priority", 
            "All Structures" 
        };

        public KCSGUnboundModMenu(ModContentPack content) : base(content)
        {
            settings = GetSettings<KCSGUnboundSettings>();
        }

        /// <summary>
        /// Draw the settings UI
        /// </summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            // Use a scrollable view to accommodate all settings
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, 600f); // Increased height
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            
            // Use a single listing standard for all settings to avoid overlapping sections
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(viewRect);
            
            // ===== HEADER SECTION =====
            Text.Font = GameFont.Medium;
            listingStandard.Label("KCSG Unbound Settings");
            Text.Font = GameFont.Small;
            listingStandard.Gap(15f); // Increased gap
            
            // ===== STRUCTURE PRIORITY SECTION =====
            listingStandard.GapLine(24f); // Increased line gap
            Text.Font = GameFont.Medium;
            listingStandard.Label("Structure Filtering");
            Text.Font = GameFont.Small;
            listingStandard.Gap(15f); // Increased gap
            
            // Priority slider
            listingStandard.Label("Structure priority threshold:");
            listingStandard.Gap(8f); // Gap between label and slider
            int oldPriority = settings.structurePriorityThreshold;
            settings.structurePriorityThreshold = (int)listingStandard.Slider(settings.structurePriorityThreshold, 0, 4);
            listingStandard.Gap(8f); // Gap after slider
            
            // Show current selection
            GUI.color = Color.yellow;
            listingStandard.Label($"Current: {priorityLabels[settings.structurePriorityThreshold]}");
            GUI.color = Color.white;
            listingStandard.Gap(10f); // Gap after current selection
            
            // Description of what this does
            listingStandard.Label("Controls which structures get registered:");
            listingStandard.Gap(3f);
            listingStandard.Label("• Lower values (Essential, High) = Faster loading but may miss some structures");
            listingStandard.Gap(3f);
            listingStandard.Label("• Higher values (Low, All) = More structures but slower loading");
            listingStandard.Gap(10f); // Gap after description
            
            // Note that changes require restart
            if (oldPriority != settings.structurePriorityThreshold)
            {
                GUI.color = Color.yellow;
                listingStandard.Label("Changes will take effect after restarting the game.");
                GUI.color = Color.white;
                listingStandard.Gap(10f); // Gap after note
            }
            
            // ===== PERFORMANCE SETTINGS SECTION =====
            listingStandard.GapLine(24f); // Increased line gap
            Text.Font = GameFont.Medium;
            listingStandard.Label("Performance Settings");
            Text.Font = GameFont.Small;
            listingStandard.Gap(15f); // Increased gap
            
            bool lazyReg = KCSGUnboundSettings.LazyRegistration;
            listingStandard.CheckboxLabeled("Enable lazy registration", 
                ref lazyReg, 
                "Only register symbols when needed instead of all at once. Improves startup time.");
            if (lazyReg != KCSGUnboundSettings.LazyRegistration)
            {
                settings.lazyRegistrationValue = lazyReg;
                KCSGUnboundSettings.LazyRegistration = lazyReg;
            }
            listingStandard.Gap(10f); // Gap between checkboxes
            
            bool highPerf = KCSGUnboundSettings.HighPerformanceMode;
            listingStandard.CheckboxLabeled("High performance mode", 
                ref highPerf, 
                "Limit scanning to improve performance. Reduces memory usage but might miss some structures.");
            if (highPerf != KCSGUnboundSettings.HighPerformanceMode)
            {
                settings.highPerformanceModeValue = highPerf;
                KCSGUnboundSettings.HighPerformanceMode = highPerf;
            }
            listingStandard.Gap(10f); // Gap between checkboxes
            
            bool aggressiveOpt = KCSGUnboundSettings.AggressiveOptimizations;
            listingStandard.CheckboxLabeled("Aggressive optimizations", 
                ref aggressiveOpt, 
                "Apply more aggressive performance optimizations. May reduce compatibility.");
            if (aggressiveOpt != KCSGUnboundSettings.AggressiveOptimizations)
            {
                settings.aggressiveOptimizationsValue = aggressiveOpt;
                KCSGUnboundSettings.AggressiveOptimizations = aggressiveOpt;
            }
            listingStandard.Gap(15f); // Gap after section
            
            // ===== DEBUG SETTINGS SECTION =====
            listingStandard.GapLine(24f); // Increased line gap
            Text.Font = GameFont.Medium;
            listingStandard.Label("Debug Settings");
            Text.Font = GameFont.Small;
            listingStandard.Gap(15f); // Increased gap
            
            bool debugMode = KCSGUnboundSettings.DebugMode;
            listingStandard.CheckboxLabeled("Debug mode", 
                ref debugMode, 
                "Enable debug logging and additional diagnostics");
            if (debugMode != KCSGUnboundSettings.DebugMode)
            {
                settings.debugModeValue = debugMode;
                KCSGUnboundSettings.DebugMode = debugMode;
            }
            listingStandard.Gap(10f); // Gap between checkboxes
            
            bool showDebug = KCSGUnboundSettings.ShowDebugVisuals;
            listingStandard.CheckboxLabeled("Show debug visualizations", 
                ref showDebug, 
                "Show additional visual information for debugging");
            if (showDebug != KCSGUnboundSettings.ShowDebugVisuals)
            {
                settings.showDebugVisualsValue = showDebug;
                KCSGUnboundSettings.ShowDebugVisuals = showDebug;
            }
            listingStandard.Gap(10f); // Gap between checkbox and next element
            
            // Logging level control
            string[] logLevelLabels = Enum.GetNames(typeof(LogLevel));
            listingStandard.Label("Logging level: " + KCSGUnboundSettings.LoggingLevel.ToString());
            listingStandard.Gap(5f); // Gap between label and button
            
            if (listingStandard.ButtonText("Cycle logging level"))
            {
                LogLevel newLevel = (LogLevel)(((int)KCSGUnboundSettings.LoggingLevel + 1) % Enum.GetValues(typeof(LogLevel)).Length);
                settings.loggingLevelValue = newLevel;
                KCSGUnboundSettings.LoggingLevel = newLevel;
            }
            listingStandard.Gap(10f); // Gap after button
            
            // Hash collision policy
            listingStandard.Label("Hash collision policy: " + KCSGUnboundSettings.HashCollisionPolicy.ToString());
            listingStandard.Gap(5f); // Gap between label and button
            
            if (listingStandard.ButtonText("Cycle collision policy"))
            {
                CollisionPolicy newPolicy = (CollisionPolicy)(((int)KCSGUnboundSettings.HashCollisionPolicy + 1) % Enum.GetValues(typeof(CollisionPolicy)).Length);
                settings.hashCollisionPolicyValue = newPolicy;
                KCSGUnboundSettings.HashCollisionPolicy = newPolicy;
            }
            listingStandard.Gap(15f); // Gap after section
            
            // ===== SCANNING SETTINGS SECTION =====
            listingStandard.GapLine(24f); // Increased line gap
            Text.Font = GameFont.Medium;
            listingStandard.Label("Scanning Settings");
            Text.Font = GameFont.Small;
            listingStandard.Gap(15f); // Increased gap
            
            bool fullScan = KCSGUnboundSettings.EnableFullScanning;
            listingStandard.CheckboxLabeled("Enable full scanning", 
                ref fullScan, 
                "Scan all mods for structure layouts instead of just known ones");
            if (fullScan != KCSGUnboundSettings.EnableFullScanning)
            {
                settings.enableFullScanningValue = fullScan;
                KCSGUnboundSettings.EnableFullScanning = fullScan;
            }
            listingStandard.Gap(15f); // Gap after checkbox
            
            // ===== AUTO-DETECTION SECTION =====
            listingStandard.GapLine(24f); // Increased line gap
            Text.Font = GameFont.Medium;
            listingStandard.Label("Auto-Detection Settings");
            Text.Font = GameFont.Small;
            listingStandard.Gap(15f); // Increased gap
            
            // Auto-detection toggle with experimental warning
            GUI.color = WarningColor;
            listingStandard.Label("Auto-detect mods with structures (EXPERIMENTAL)");
            GUI.color = Color.white;
            listingStandard.Gap(10f); // Gap after label
            
            // Warning description
            Text.Font = GameFont.Tiny;
            GUI.color = WarningColor;
            listingStandard.Label("WARNING: May cause extremely long loading times (10+ minutes)!");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listingStandard.Gap(10f); // Gap after warning
            
            // Checkbox for enabling auto-detection
            bool oldValue = settings.enableAutoDetection;
            bool autoDetect = settings.enableAutoDetection;
            listingStandard.CheckboxLabeled("Enable auto-detection", ref autoDetect);
            settings.enableAutoDetection = autoDetect;
            listingStandard.Gap(10f); // Gap after checkbox
            
            // Description of what auto-detection does
            listingStandard.Label("Scans all mods for CustomGenDefs structures and registers them.");
            listingStandard.Label("Only enable if you're experiencing issues with structures not generating correctly.");
            listingStandard.Gap(10f); // Gap after description
            
            // Note that changes require restart
            if (oldValue != settings.enableAutoDetection)
            {
                GUI.color = Color.yellow;
                listingStandard.Label("Changes will take effect after restarting the game.");
                GUI.color = Color.white;
                listingStandard.Gap(15f); // Gap after note
            }
            
            // End listing and scroll view
            listingStandard.End();
            Widgets.EndScrollView();
            
            // Save settings
            settings.Write();
        }

        /// <summary>
        /// Name shown in the settings list
        /// </summary>
        public override string SettingsCategory()
        {
            return "KCSG Unbound";
        }
    }

    /// <summary>
    /// Log levels for controlling output verbosity
    /// </summary>
    public enum LogLevel
    {
        Minimal,
        Normal,
        Verbose,
        Debug
    }
    
    /// <summary>
    /// Policies for handling hash collisions
    /// </summary>
    public enum CollisionPolicy
    {
        FirstDefWins,
        LastDefWins,
        RandomDefWins,
        ThrowError
    }
} 