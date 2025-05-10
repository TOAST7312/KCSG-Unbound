using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using UnityEngine;

namespace KCSG
{
    /// <summary>
    /// Base class for all KCSG symbol resolvers
    /// Extends the vanilla SymbolResolver to provide additional functionality
    /// </summary>
    public abstract class SymbolResolver_KCSG : RimWorld.BaseGen.SymbolResolver
    {
        // Common properties useful for all KCSG resolvers
        protected IntVec3 Center => resolveParams.rect.CenterCell;
        protected IntVec3 RandomCell => resolveParams.rect.RandomCell;
        protected Map CurrentMap => BaseGen.globalSettings.map;
        protected ResolveParams resolveParams;

        // Cache for common parameters to improve performance
        protected Faction Faction => resolveParams.faction ?? Find.FactionManager.OfPlayer;
        protected ThingDef Stuff => resolveParams.singleThingStuff;
        
        // Properties using the extension methods
        protected float ChanceToSkip => ResolveParamsExtensions.GetChanceToSkip(resolveParams);
        protected bool IsDebugResolver => ResolveParamsExtensions.GetDebugResolver(resolveParams);
        
        // Override the base Resolve method to store resolveParams
        public override void Resolve(ResolveParams rp)
        {
            resolveParams = rp;
            
            // Log debug info if needed
            if (IsDebugResolver)
            {
                Log.Message($"[KCSG] Resolving {this.GetType().Name} in rect {rp.rect.minX},{rp.rect.minZ},{rp.rect.maxX},{rp.rect.maxZ}");
            }
            
            // Check if we should skip resolution based on chance
            if (Rand.Value < ChanceToSkip)
            {
                if (IsDebugResolver) Log.Message($"[KCSG] Skipping {this.GetType().Name} due to chanceToSkip");
                return;
            }
            
            // Actual resolution logic is in ResolveInt
            ResolveInt(rp);
        }
        
        // Abstract method to be implemented by derived classes
        protected abstract void ResolveInt(ResolveParams rp);
        
        // Helper methods commonly used by resolvers
        
        // Create a thing at the specified location
        protected Thing CreateThing(IntVec3 pos, ThingDef def, Faction faction = null, ThingDef stuff = null, Rot4? rot = null)
        {
            if (def == null) return null;
            
            Rot4 rotation = rot ?? Rot4.North;
            Thing thing = ThingMaker.MakeThing(def, stuff ?? GenStuff.DefaultStuffFor(def));
            thing.SetFaction(faction ?? Faction);
            
            GenSpawn.Spawn(thing, pos, CurrentMap, rotation);
            return thing;
        }
        
        // Get a random value based on current resolver seed
        protected T GetRandomValue<T>(List<T> options)
        {
            if (options == null || options.Count == 0) return default;
            return options.RandomElement();
        }
        
        // Check if cell is valid for placement
        protected bool CanPlaceAt(IntVec3 c, ThingDef def, Rot4 rot = default)
        {
            return c.InBounds(CurrentMap) && 
                   c.Standable(CurrentMap) && 
                   GenConstruct.CanPlaceBlueprintAt(def, c, rot, CurrentMap).Accepted;
        }
        
        // Check if def is a building
        protected bool IsBuilding(ThingDef def)
        {
            return def != null && def.category == ThingCategory.Building;
        }
        
        // Get a child resolve params with the same default values
        protected ResolveParams GetChildParams()
        {
            return new ResolveParams
            {
                rect = resolveParams.rect,
                faction = Faction,
                singleThingStuff = Stuff
            };
        }
        
        // Get a child resolve params for a specific rect
        protected ResolveParams GetChildParams(CellRect rect)
        {
            var childParams = GetChildParams();
            childParams.rect = rect;
            return childParams;
        }
    }
} 