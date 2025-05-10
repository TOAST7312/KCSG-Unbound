using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using Verse;
using System.Collections.Generic;

namespace KCSG
{
    /// <summary>
    /// Initializes and manages Harmony patches for KCSG Unbound
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        /// <summary>
        /// Harmony instance for patching
        /// </summary>
        public static Harmony harmony;
        
        /// <summary>
        /// Static constructor runs on game load
        /// </summary>
        static HarmonyInit()
        {
            try
            {
                // Write directly to a file for verification
                try
                {
                    string path = "KCSGUnbound_runtime_log.txt";
                    System.IO.File.WriteAllText(path, $"KCSG Unbound runtime initialized at {DateTime.Now}\n");
                    System.IO.File.AppendAllText(path, $"RimWorld version: {VersionControl.CurrentVersionString}\n");
                    
                    // Check for Zetrith's Prepatcher
                    bool prepatcherFound = false;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.FullName.Contains("Prepatcher"))
                        {
                            System.IO.File.AppendAllText(path, $"Found assembly: {assembly.FullName}\n");
                            prepatcherFound = true;
                        }
                    }
                    
                    if (!prepatcherFound)
                    {
                        System.IO.File.AppendAllText(path, "WARNING: No Prepatcher assembly found!\n");
                    }
                    
                    // Check for VEF
                    bool vefFound = false;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.FullName.Contains("VanillaExpanded"))
                        {
                            System.IO.File.AppendAllText(path, $"Found VEF assembly: {assembly.FullName}\n");
                            vefFound = true;
                        }
                    }
                    
                    if (!vefFound)
                    {
                        System.IO.File.AppendAllText(path, "WARNING: No VEF assembly found!\n");
                    }
                }
                catch (Exception ex)
                {
                    // Just ignore file writing errors
                }
                
                // Very obvious log message to help with debugging
                Log.Message("══════════════════════════════════════════════════");
                Log.Message("║     KCSG UNBOUND - HARMONY INITIALIZATION      ║");
                Log.Message("══════════════════════════════════════════════════");
                
                // Create harmony instance if not already created
                harmony = new Harmony("com.rimworld.kcsg.unbound");
                
                // Check if we're running with Zetrith's Prepatcher
                bool isPrepatcherRunning = false;
                try
                {
                    Type prepatcherType = AccessTools.TypeByName("ZetrithPrepatcher.PrepatchManager");
                    if (prepatcherType != null)
                    {
                        PropertyInfo runningProperty = AccessTools.Property(prepatcherType, "Running");
                        if (runningProperty != null)
                        {
                            isPrepatcherRunning = (bool)runningProperty.GetValue(null);
                        }
                    }
                    
                    // Check if our prepatch ran successfully
                    Type kcsGPrepatchType = AccessTools.TypeByName("KCSG.KCSGPrepatch");
                    if (kcsGPrepatchType != null)
                    {
                        MethodInfo hasPatchedMethod = AccessTools.Method(kcsGPrepatchType, "HasPatchedSuccessfully");
                        if (hasPatchedMethod != null)
                        {
                            bool patchSuccess = (bool)hasPatchedMethod.Invoke(null, null);
                            Log.Message($"[KCSG] KCSGPrepatch.HasPatchedSuccessfully() = {patchSuccess}");
                        }
                        }
                    }
                    catch (Exception ex)
                {
                    Log.Warning($"[KCSG] Could not check Prepatcher status: {ex.Message}");
                }
                
                if (isPrepatcherRunning)
                {
                    Log.Message("[KCSG] Zetrith's Prepatcher detected - early loading available");
                }
                else
                {
                    Log.Warning("[KCSG] Zetrith's Prepatcher not detected - using fallback mode");
                    // Apply direct patches since prepatcher isn't available
                    ApplyDirectPatches();
                }
                
                // Check KCSG.SymbolResolver state
                Type symbolResolverType = AccessTools.TypeByName("KCSG.SymbolResolver");
                if (symbolResolverType != null)
                {
                    FieldInfo isPrepatched = AccessTools.Field(symbolResolverType, "IsPrepatched");
                    if (isPrepatched != null)
                    {
                        bool prepatched = (bool)isPrepatched.GetValue(null);
                        Log.Message($"[KCSG] SymbolResolver prepatched status: {prepatched}");
                    }
                }
                
                Log.Message("[KCSG] Harmony initialization complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Critical error in HarmonyInit: {ex}");
            }
        }
        
        /// <summary>
        /// Apply direct patches when prepatcher isn't available
        /// </summary>
        private static void ApplyDirectPatches()
        {
            try
            {
                Log.Message("[KCSG] Applying direct patches for fallback mode");
                
                // Deep analysis of BaseGen structure
                try 
                {
                    string path = "KCSGUnbound_basegen_analysis.txt";
                    System.IO.File.WriteAllText(path, $"BaseGen structure analysis at {DateTime.Now}\n");
                    
                    // Get BaseGen structure
                    Type baseGenType = typeof(BaseGen);
                    System.IO.File.AppendAllText(path, $"BaseGen type: {baseGenType.FullName}\n");
                    
                    // Get globalSettings
                    FieldInfo globalSettingsField = baseGenType.GetField("globalSettings", BindingFlags.Static | BindingFlags.Public);
                    if (globalSettingsField != null)
                    {
                        object globalSettings = globalSettingsField.GetValue(null);
                        Type globalSettingsType = globalSettings.GetType();
                        System.IO.File.AppendAllText(path, $"GlobalSettings type: {globalSettingsType.FullName}\n");
                        
                        // List all fields of globalSettings
                        foreach (var field in globalSettingsType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            System.IO.File.AppendAllText(path, $"Field: {field.Name}, Type: {field.FieldType.FullName}\n");
                            
                            // If this is a dictionary, try to get info about it
                            if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                            {
                                var dict = field.GetValue(globalSettings);
                                if (dict != null)
                                {
                                    // Get dictionary count via reflection
                                    var countProp = field.FieldType.GetProperty("Count");
                                    if (countProp != null)
                                    {
                                        var count = countProp.GetValue(dict);
                                        System.IO.File.AppendAllText(path, $"  Dictionary items: {count}\n");
                                        
                                        // Get key and value types
                                        var genArgs = field.FieldType.GetGenericArguments();
                                        if (genArgs.Length == 2)
                                        {
                                            System.IO.File.AppendAllText(path, $"  Key type: {genArgs[0].FullName}\n");
                                            System.IO.File.AppendAllText(path, $"  Value type: {genArgs[1].FullName}\n");
                                        }
                                    }
                                }
                            }
                        }
                        
                        // List all methods of globalSettings
                        foreach (var method in globalSettingsType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            System.IO.File.AppendAllText(path, $"Method: {method.Name}, Return: {method.ReturnType.Name}, Parameters: {method.GetParameters().Length}\n");
                            if (method.GetParameters().Length > 0)
                            {
                                System.IO.File.AppendAllText(path, $"  Parameters: {string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))}\n");
                            }
                        }
                    }
                    
                    System.IO.File.AppendAllText(path, "Analysis complete\n");
                }
                catch (Exception ex)
                {
                    try {
                        System.IO.File.AppendAllText("KCSGUnbound_basegen_analysis.txt", $"Error: {ex}\n");
                    } catch {}
                }
                
                // Look for VEF's KCSG implementation
                Type vefSymbolResolverType = AccessTools.TypeByName("RimWorld.BaseGen.SymbolResolver");
                if (vefSymbolResolverType != null)
                {
                    Log.Message("[KCSG] Found RimWorld.BaseGen.SymbolResolver class");
                    
                    // RimWorld 1.5 uses a different approach to symbol resolvers
                    Type globalSettingsType = AccessTools.TypeByName("RimWorld.BaseGen.GlobalSettings");
                    if (globalSettingsType != null)
                    {
                        // Try to patch TryResolveSymbol method
                        MethodInfo tryResolveMethod = AccessTools.Method(globalSettingsType, "TryResolveSymbol");
                        if (tryResolveMethod != null)
                        {
                            Log.Message($"[KCSG] Found TryResolveSymbol method with signature: {tryResolveMethod.ToString()}");
                            
                            // Get our patch method
                            Type symbolResolverType = AccessTools.TypeByName("KCSG.SymbolResolver");
                            if (symbolResolverType != null)
                            {
                                MethodInfo patchMethod = AccessTools.Method(symbolResolverType, "TryResolveSymbol_Prefix");
                                if (patchMethod != null)
                                {
                                    Log.Message($"[KCSG] Found our patch method with signature: {patchMethod.ToString()}");
                                    
                                    // Apply the patch
                                    try
                                    {
                                        harmony.Patch(tryResolveMethod, prefix: new HarmonyMethod(patchMethod));
                                        Log.Message("[KCSG] Successfully patched GlobalSettings.TryResolveSymbol");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error($"[KCSG] Failed to apply patch: {ex.Message}");
                                        Log.Error($"[KCSG] Exception details: {ex}");
                                    }
                                }
                                else
                                {
                                    Log.Error("[KCSG] Could not find TryResolveSymbol_Prefix in SymbolResolver");
                                }
                            }
                            else
                            {
                                Log.Error("[KCSG] Could not find KCSG.SymbolResolver type");
                            }
                        }
                        else
                        {
                            Log.Error("[KCSG] Could not find TryResolveSymbol method in GlobalSettings");
                            
                            // List available methods to help diagnose
                            try
                            {
                                MethodInfo[] methods = globalSettingsType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                Log.Message($"[KCSG] Available methods in GlobalSettings ({methods.Length}):");
                                foreach (var method in methods.Take(10)) // Only show first 10 to avoid spam
                                {
                                    Log.Message($"  - {method.Name}: {method.ToString()}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"[KCSG] Error listing methods: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        Log.Error("[KCSG] Could not find RimWorld.BaseGen.GlobalSettings type");
                        
                        // List types in RimWorld.BaseGen to help diagnose
                        try 
                        {
                            Type baseGenType = AccessTools.TypeByName("RimWorld.BaseGen");
                            Type[] nestedTypes = baseGenType?.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
                            if (nestedTypes != null && nestedTypes.Length > 0)
                            {
                                Log.Message($"[KCSG] Available types in RimWorld.BaseGen ({nestedTypes.Length}):");
                                foreach (var type in nestedTypes.Take(10)) // Only show first 10
                                {
                                    Log.Message($"  - {type.Name}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"[KCSG] Error listing BaseGen types: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Log.Error("[KCSG] Could not find RimWorld.BaseGen.SymbolResolver class");
                }
                
                Log.Message("[KCSG] Direct patching complete");
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Critical error applying direct patches: {ex}");
            }
        }
    }
} 