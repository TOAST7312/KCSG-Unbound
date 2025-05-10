using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using UnityEngine;

namespace KCSG
{
    // Custom extension properties for ResolveParams
    public static class ResolveParamsExtensions
    {
        public static bool GetDebugResolver(this ResolveParams rp) => Prefs.DevMode && rp.GetCustom<bool>("debugResolver");
        public static void SetDebugResolver(this ResolveParams rp, bool value) => rp.SetCustom("debugResolver", value);
        
        public static float GetChanceToSkip(this ResolveParams rp) => rp.GetCustom<float>("chanceToSkip");
        public static void SetChanceToSkip(this ResolveParams rp, float value) => rp.SetCustom("chanceToSkip", value);
        
        public static ThingDef GetThingDef(this ResolveParams rp) => rp.GetCustom<ThingDef>("thingDef");
        public static void SetThingDef(this ResolveParams rp, ThingDef value) => rp.SetCustom("thingDef", value);
        
        public static List<ThingDef> GetThingDefs(this ResolveParams rp) => rp.GetCustom<List<ThingDef>>("thingDefs");
        public static void SetThingDefs(this ResolveParams rp, List<ThingDef> value) => rp.SetCustom("thingDefs", value);
        
        public static IntVec3? GetSingleCell(this ResolveParams rp) => rp.GetCustom<IntVec3?>("singleCell");
        public static void SetSingleCell(this ResolveParams rp, IntVec3? value) => rp.SetCustom("singleCell", value);
        
        public static Rot4? GetThingRot(this ResolveParams rp) => rp.GetCustom<Rot4?>("thingRot");
        public static void SetThingRot(this ResolveParams rp, Rot4? value) => rp.SetCustom("thingRot", value);
        
        public static Color? GetSetStuffColor(this ResolveParams rp) => rp.GetCustom<Color?>("SetStuffColor");
        public static void SetSetStuffColor(this ResolveParams rp, Color? value) => rp.SetCustom("SetStuffColor", value);
        
        public static Action<Thing> GetPostThingGenerate(this ResolveParams rp) => rp.GetCustom<Action<Thing>>("postThingGenerate");
        public static void SetPostThingGenerate(this ResolveParams rp, Action<Thing> value) => rp.SetCustom("postThingGenerate", value);
    }
} 