using System.Collections.Generic;
using Verse;

namespace KCSG
{
    public static class RandomUtils
    {
        public static StructureLayoutDef RandomLayoutFrom(List<StructureLayoutDef> layouts)
        {
            // Simple implementation - always try our implementation first
            if (layouts == null || layouts.Count == 0)
                return null;
                
            if (layouts.Count == 1)
                return layouts[0];
            
            // Choose random element (basic implementation)
            int index = Rand.Range(0, layouts.Count);
            StructureLayoutDef result = layouts[index];
            
            // If we have a valid result, return it; otherwise try VEF fallback
            if (result != null)
                return result;
                
            // Try fallback to VEF implementation
            try
            {
                Log.Message("[KCSG Unbound] Using VEF fallback for RandomLayoutFrom");
                return VEFIntegration.TryVEFRandomLayoutFrom(layouts);
            }
            catch
            {
                // If all else fails, return the first item if it exists
                return layouts.Count > 0 ? layouts[0] : null;
            }
        }
    }
} 