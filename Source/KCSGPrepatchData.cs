using System;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Simple data class to hold runtime prepatcher status
    /// </summary>
    public class KCSGPrepatchData
    {
        // Singleton instance
        private static KCSGPrepatchData _instance;
        
        /// <summary>
        /// Gets the singleton instance of KCSGPrepatchData
        /// </summary>
        public static KCSGPrepatchData Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new KCSGPrepatchData();
                }
                return _instance;
            }
        }
        
        // Private constructor for singleton
        private KCSGPrepatchData()
        {
            // Default to not prepatched - will be overridden by prepatcher
            _isPrepatched = false;
        }
        
        // Backing field for prepatch status
        private bool _isPrepatched;
        
        /// <summary>
        /// Checks if the mod has been prepatched by Zetrith's Prepatcher
        /// This field is set by the prepatcher during early loading
        /// </summary>
        /// <returns>True if the mod was prepatched, false otherwise</returns>
        public bool IsPrepatched()
        {
            return _isPrepatched;
        }
        
        /// <summary>
        /// Sets the prepatched status - only for internal use
        /// </summary>
        /// <param name="value">The new prepatched status</param>
        internal void SetPrepatched(bool value)
        {
            _isPrepatched = value;
            if (value)
            {
                Log.Message("[KCSG Unbound] Prepatcher mode active");
            }
        }
    }
} 