using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using UnityEngine;

namespace KCSG
{
    /// <summary>
    /// Handles integration with Vanilla Expanded Framework's KCSG module
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VEFIntegration
    {
        // VEF module detection
        public static readonly bool VEFAvailable;
        public static readonly bool VEFKCSGAvailable;
        
        // Core VEF KCSG types
        private static readonly Type VEF_CustomGenOptionType;
        private static readonly Type VEF_StructureLayoutDefType;
        private static readonly Type VEF_SettlementLayoutDefType;
        private static readonly Type VEF_TiledStructureDefType;
        
        // VEF KCSG utility types
        private static readonly Type VEF_RandomUtilsType;
        private static readonly Type VEF_TileUtilsType;
        private static readonly Type VEF_LayoutUtilsType;
        private static readonly Type VEF_GenOptionType;
        
        // Important VEF methods
        private static readonly MethodInfo VEF_RandomLayoutFrom;
        private static readonly MethodInfo VEF_TileUtils_Generate;
        private static readonly MethodInfo VEF_LayoutUtils_CleanRect;
        
        static VEFIntegration()
        {
            try
            {
                // Check for VEF
                VEFAvailable = ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaFactionsExpanded.Core") != null;
                if (!VEFAvailable)
                {
                    Log.Message("[KCSG Unbound] VEF not detected, fallback unavailable");
                    return;
                }
                
                // Try to find VEF's KCSG types
                VEF_CustomGenOptionType = ReflectionCache.GetTypeByName("KCSG.CustomGenOption");
                VEF_StructureLayoutDefType = ReflectionCache.GetTypeByName("KCSG.StructureLayoutDef");
                VEF_SettlementLayoutDefType = ReflectionCache.GetTypeByName("KCSG.SettlementLayoutDef");
                VEF_TiledStructureDefType = ReflectionCache.GetTypeByName("KCSG.TiledStructureDef");
                
                VEF_RandomUtilsType = ReflectionCache.GetTypeByName("KCSG.RandomUtils");
                VEF_TileUtilsType = ReflectionCache.GetTypeByName("KCSG.TileUtils");
                VEF_LayoutUtilsType = ReflectionCache.GetTypeByName("KCSG.LayoutUtils");
                VEF_GenOptionType = ReflectionCache.GetTypeByName("KCSG.GenOption");
                
                // Check if KCSG is available
                VEFKCSGAvailable = VEF_CustomGenOptionType != null &&
                                  VEF_StructureLayoutDefType != null &&
                                  VEF_SettlementLayoutDefType != null;
                
                if (!VEFKCSGAvailable)
                {
                    Log.Warning("[KCSG Unbound] VEF detected, but KCSG module not found");
                    return;
                }
                
                // Cache important methods
                VEF_RandomLayoutFrom = ReflectionCache.GetMethod(VEF_RandomUtilsType, "RandomLayoutFrom");
                VEF_TileUtils_Generate = ReflectionCache.GetMethod(VEF_TileUtilsType, "Generate");
                VEF_LayoutUtils_CleanRect = ReflectionCache.GetMethod(VEF_LayoutUtilsType, "CleanRect");
                
                Log.Message("[KCSG Unbound] VEF KCSG module detected, fallback available");
            }
            catch (Exception ex)
            {
                VEFAvailable = false;
                VEFKCSGAvailable = false;
                Log.Error($"[KCSG Unbound] Error initializing VEF integration: {ex}");
            }
        }
        
        /// <summary>
        /// Attempts to call VEF's RandomLayoutFrom method as a fallback
        /// </summary>
        public static StructureLayoutDef TryVEFRandomLayoutFrom(List<StructureLayoutDef> layouts)
        {
            if (!VEFKCSGAvailable || VEF_RandomLayoutFrom == null)
                return null;
            
            try
            {
                // Convert our layouts to VEF layouts for the call
                var vefLayouts = new List<object>();
                foreach (var layout in layouts)
                {
                    // Create a VEF layout equivalent
                    var vefLayout = Activator.CreateInstance(VEF_StructureLayoutDefType);
                    
                    // Copy basic properties
                    typeof(Def).GetProperty("defName").SetValue(vefLayout, layout.defName);
                    VEF_StructureLayoutDefType.GetField("layouts").SetValue(vefLayout, layout.layouts);
                    VEF_StructureLayoutDefType.GetField("sizes").SetValue(vefLayout, layout.sizes);
                    
                    vefLayouts.Add(vefLayout);
                }
                
                // Call VEF's method
                var result = ReflectionCache.SafeInvoke(VEF_RandomLayoutFrom, null, new object[] { vefLayouts });
                if (result == null)
                    return null;
                
                // Convert result back to our type
                var ourLayout = new StructureLayoutDef();
                ourLayout.defName = (string)typeof(Def).GetProperty("defName").GetValue(result);
                ourLayout.layouts = (List<string>)VEF_StructureLayoutDefType.GetField("layouts").GetValue(result);
                ourLayout.sizes = (Vector3)VEF_StructureLayoutDefType.GetField("sizes").GetValue(result);
                
                return ourLayout;
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Failed to use VEF RandomLayoutFrom: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Attempts to call VEF's TileUtils.Generate method as a fallback
        /// </summary>
        public static bool TryVEFTileGenerate(TiledStructureDef def, IntVec3 position, Map map, Quest quest)
        {
            if (!VEFKCSGAvailable || VEF_TileUtils_Generate == null)
                return false;
            
            try
            {
                // Create VEF equivalent of our TiledStructureDef
                var vefDef = Activator.CreateInstance(VEF_TiledStructureDefType);
                typeof(Def).GetProperty("defName").SetValue(vefDef, def.defName);
                
                // Call VEF's method
                ReflectionCache.SafeInvoke(VEF_TileUtils_Generate, null, new object[] { vefDef, position, map, quest });
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Failed to use VEF TileUtils.Generate: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Attempts to call VEF's LayoutUtils.CleanRect method as a fallback
        /// </summary>
        public static bool TryVEFCleanRect(StructureLayoutDef layout, Map map, CellRect rect, bool fullClear)
        {
            if (!VEFKCSGAvailable || VEF_LayoutUtils_CleanRect == null)
                return false;
            
            try
            {
                // Create VEF equivalent of our StructureLayoutDef
                var vefLayout = Activator.CreateInstance(VEF_StructureLayoutDefType);
                typeof(Def).GetProperty("defName").SetValue(vefLayout, layout.defName);
                VEF_StructureLayoutDefType.GetField("layouts").SetValue(vefLayout, layout.layouts);
                VEF_StructureLayoutDefType.GetField("sizes").SetValue(vefLayout, layout.sizes);
                
                // Call VEF's method
                ReflectionCache.SafeInvoke(VEF_LayoutUtils_CleanRect, null, new object[] { vefLayout, map, rect, fullClear });
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Failed to use VEF LayoutUtils.CleanRect: {ex.Message}");
                return false;
            }
        }
    }
} 