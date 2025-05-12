using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Utility class to detect when rendering operations should be avoided
    /// Prevents errors during game initialization or when RimWorld is rendering in background-only mode
    /// </summary>
    public static class RenderingDetector
    {
        // Last value cached for performance reasons
        private static bool lastNoOutputResult = false;
        private static int lastCheckTick = -1;
        
        /// <summary>
        /// Checks if we should avoid rendering operations
        /// Cached for performance so it doesn't run checks constantly
        /// </summary>
        public static bool NoOutputRendering
        {
            get
            {
                try
                {
                    // Only recalculate occasionally
                    int currentTick = Find.TickManager?.TicksGame ?? -1;
                    if (currentTick >= 0 && (currentTick - lastCheckTick > 60 || lastCheckTick < 0))
                    {
                        lastCheckTick = currentTick;
                        lastNoOutputResult = ShouldSuppressRendering();
                    }
                    
                    return lastNoOutputResult;
                }
                catch (Exception)
                {
                    // If we get an exception, that's a good sign we're in a state where rendering should be avoided
                    return true;
                }
            }
        }
        
        /// <summary>
        /// Checks if rendering operations should be suppressed
        /// </summary>
        private static bool ShouldSuppressRendering()
        {
            try
            {
                // No rendering if game is null
                if (Current.Game == null)
                    return true;
                    
                // No rendering if UI root is null
                if (Find.UIRoot == null)
                    return true;
                
                // Check if we're in a situation where UI might be disabled
                if (UI.screenWidth <= 0 || UI.screenHeight <= 0)
                    return true;
                
                // Check if the game is in a non-rendering state
                if (Time.frameCount < 5)
                    return true;
                
                // All checks passed, rendering should be fine
                return false;
            }
            catch (Exception)
            {
                // If we get any exception, we should suppress rendering
                return true;
            }
        }
        
        /// <summary>
        /// Safely executes a rendering operation with proper error handling
        /// </summary>
        /// <param name="renderAction">The action to perform if rendering is allowed</param>
        public static void SafeRender(Action renderAction)
        {
            if (NoOutputRendering || renderAction == null)
                return;
            
            try
            {
                renderAction();
            }
            catch (Exception ex)
            {
                // Silent failure for rendering errors 
                // Only log in dev mode to avoid spam
                if (Prefs.DevMode)
                {
                    Log.Warning($"[KCSG Unbound] Rendering error: {ex.Message}");
                }
                
                // Update the cache to avoid repeating errors
                lastNoOutputResult = true;
            }
        }
    }
} 