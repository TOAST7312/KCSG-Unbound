using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using UnityEngine;

namespace KCSG
{
    /// <summary>
    /// Symbol resolver for placing a specific building
    /// </summary>
    public class SymbolResolver_Building : SymbolResolver_KCSG
    {
        protected override void ResolveInt(ResolveParams rp)
        {
            // Get the building def to place
            ThingDef buildingDef = ResolveParamsExtensions.GetThingDef(rp);
            if (buildingDef == null)
            {
                Log.Error("[KCSG] SymbolResolver_Building has null thingDef");
                return;
            }
            
            // Make sure it's actually a building
            if (!IsBuilding(buildingDef))
            {
                Log.Error($"[KCSG] SymbolResolver_Building tried to place non-building def: {buildingDef}");
                return;
            }
            
            // Get placement location
            IntVec3 position = ResolveParamsExtensions.GetSingleCell(rp) ?? Center;
            
            // Get rotation
            Rot4 rotation = ResolveParamsExtensions.GetThingRot(rp) ?? Rot4.North;
            
            // Check if we can place it
            if (!CanPlaceAt(position, buildingDef, rotation))
            {
                if (IsDebugResolver)
                {
                    Log.Warning($"[KCSG] Could not place {buildingDef} at {position} with rotation {rotation}");
                }
                return;
            }
            
            // Create the building
            Thing building = CreateThing(position, buildingDef, Faction, Stuff, rotation);
            
            // Apply additional properties if specified
            Color? stuffColor = ResolveParamsExtensions.GetSetStuffColor(rp);
            if (stuffColor.HasValue && building is IThingHolder thingHolder)
            {
                foreach (Thing contained in thingHolder.GetDirectlyHeldThings())
                {
                    CompColorable compColorable = contained.TryGetComp<CompColorable>();
                    if (compColorable != null)
                    {
                        compColorable.SetColor(stuffColor.Value);
                    }
                }
            }
            
            // Handle any post-placement actions
            Action<Thing> postThingGenerate = ResolveParamsExtensions.GetPostThingGenerate(rp);
            if (postThingGenerate != null)
            {
                postThingGenerate(building);
            }
            
            if (IsDebugResolver)
            {
                Log.Message($"[KCSG] Successfully placed {buildingDef} at {position}");
            }
        }
    }
    
    /// <summary>
    /// Symbol resolver for placing a random building from a list
    /// </summary>
    public class SymbolResolver_RandomBuilding : SymbolResolver_KCSG
    {
        protected override void ResolveInt(ResolveParams rp)
        {
            // Get the list of building defs to choose from
            List<ThingDef> possibleBuildings = ResolveParamsExtensions.GetThingDefs(rp);
            if (possibleBuildings == null || possibleBuildings.Count == 0)
            {
                Log.Error("[KCSG] SymbolResolver_RandomBuilding has empty or null thingDefs");
                return;
            }
            
            // Choose a random building
            ThingDef selectedDef = GetRandomValue(possibleBuildings);
            
            // Make sure it's actually a building
            if (!IsBuilding(selectedDef))
            {
                Log.Error($"[KCSG] SymbolResolver_RandomBuilding selected non-building def: {selectedDef}");
                return;
            }
            
            // Create child params with the selected building
            ResolveParams childParams = GetChildParams();
            ResolveParamsExtensions.SetThingDef(childParams, selectedDef);
            ResolveParamsExtensions.SetSingleCell(childParams, ResolveParamsExtensions.GetSingleCell(rp));
            ResolveParamsExtensions.SetThingRot(childParams, ResolveParamsExtensions.GetThingRot(rp));
            ResolveParamsExtensions.SetSetStuffColor(childParams, ResolveParamsExtensions.GetSetStuffColor(rp));
            ResolveParamsExtensions.SetPostThingGenerate(childParams, ResolveParamsExtensions.GetPostThingGenerate(rp));
            
            // Resolve using the building resolver
            BaseGen.symbolStack.Push("Building", childParams);
        }
    }
} 