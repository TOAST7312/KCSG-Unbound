using System;
using UnityEngine;
using Verse;
using RimWorld;

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

        /// <summary>
        /// Save settings to file
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enableAutoDetection, "enableAutoDetection", false);
            Scribe_Values.Look(ref structurePriorityThreshold, "structurePriorityThreshold", 2);
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
            Listing_Standard listingStandard = new Listing_Standard();
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, 400f);
            
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);
            listingStandard.Begin(viewRect);
            
            // Header
            Text.Font = GameFont.Medium;
            listingStandard.Label("KCSG Unbound Settings");
            Text.Font = GameFont.Small;
            listingStandard.Gap();
            
            // Structure Priority Filtering 
            listingStandard.GapLine();
            listingStandard.Label("Structure Filtering");
            listingStandard.Gap(10f);
            
            // Priority slider
            listingStandard.Label("Structure priority threshold:");
            listingStandard.Gap(5f);
            int oldPriority = settings.structurePriorityThreshold;
            settings.structurePriorityThreshold = (int)listingStandard.Slider(settings.structurePriorityThreshold, 0, 4);
            
            // Show current selection
            GUI.color = Color.yellow;
            listingStandard.Label($"Current: {priorityLabels[settings.structurePriorityThreshold]}");
            GUI.color = Color.white;
            
            // Description of what this does
            listingStandard.Gap(5f);
            listingStandard.Label("Controls which structures get registered:\n" +
                "• Lower values (Essential, High) = Faster loading but may miss some structures\n" +
                "• Higher values (Low, All) = More structures but slower loading");
            
            // Note that changes require restart
            if (oldPriority != settings.structurePriorityThreshold)
            {
                listingStandard.Gap(5f);
                GUI.color = Color.yellow;
                listingStandard.Label("Changes will take effect after restarting the game.");
                GUI.color = Color.white;
            }
            
            // Auto-detection section
            listingStandard.GapLine();
            listingStandard.Label("Auto-Detection Settings");
            listingStandard.Gap(10f);
            
            // Auto-detection toggle with experimental warning
            Rect autoDetectLabelRect = listingStandard.GetRect(30f);
            
            // Draw the warning label with red text
            GUI.color = WarningColor;
            Widgets.Label(autoDetectLabelRect, "Auto-detect mods with structures (EXPERIMENTAL)");
            GUI.color = Color.white;
            
            // Warning description
            listingStandard.Gap(5f);
            Text.Font = GameFont.Tiny;
            GUI.color = WarningColor;
            listingStandard.Label("WARNING: May cause extremely long loading times (10+ minutes)!");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            // Checkbox for enabling auto-detection
            listingStandard.Gap(5f);
            bool oldValue = settings.enableAutoDetection;
            bool autoDetect = settings.enableAutoDetection;
            listingStandard.CheckboxLabeled("Enable auto-detection", ref autoDetect);
            settings.enableAutoDetection = autoDetect;
            
            // Description of what auto-detection does
            listingStandard.Gap(5f);
            listingStandard.Label("Scans all mods for CustomGenDefs structures and registers them.\nOnly enable if you're experiencing issues with structures not generating correctly.");
            
            // Note that changes require restart
            if (oldValue != settings.enableAutoDetection)
            {
                listingStandard.Gap(10f);
                GUI.color = Color.yellow;
                listingStandard.Label("Changes will take effect after restarting the game.");
                GUI.color = Color.white;
            }
            
            listingStandard.End();
            Widgets.EndScrollView();
            
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>
        /// Name shown in the settings list
        /// </summary>
        public override string SettingsCategory()
        {
            return "KCSG Unbound";
        }
    }
} 