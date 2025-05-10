using System.Collections.Generic;
using Verse;
using UnityEngine;

namespace KCSG
{
    public class StructureLayoutDef : Def
    {
        // Layout data
        public List<string> layouts = new List<string>();
        public Vector3 sizes = Vector3.zero;
        
        // This is a minimal implementation for compatibility
        // The original class has more properties for full KCSG functionality
    }
} 