using System;
using UnityEngine;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Utility to detect rendering status in the game
    /// </summary>
    public static class RenderingDetector
    {
        /// <summary>
        /// Checks if rendering is suppressed (e.g. during loading, headless mode, etc.)
        /// </summary>
        public static bool NoOutputRendering
        {
            get
            {
                try
                {
                    // Check for headless mode or non-rendering contexts
                    if (UnityData.IsInMainThread == false || Current.Game == null)
                    {
                        return true;
                    }
                    
                    // Check if any UI views are available
                    if (Find.WindowStack == null)
                    {
                        return true;
                    }
                    
                    // Check if we're in a play mode context
                    if (Current.ProgramState != ProgramState.Playing)
                    {
                        return true;
                    }
                    
                    return false;
                }
                catch (Exception)
                {
                    // If we can't determine, assume no rendering for safety
                    return true;
                }
            }
        }
    }
} 