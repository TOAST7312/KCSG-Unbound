using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.BaseGen;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Comprehensive compatibility utilities for RimWorld
    /// </summary>
    public static class RimWorldCompatibility
    {
        // Get RimWorld version
        public static Version RimWorldVersion => typeof(Game).Assembly.GetName().Version;
        
        // Check if we're on 1.5+
        public static bool IsRimWorld15OrLater => 
            RimWorldVersion.Major > 1 || 
            (RimWorldVersion.Major == 1 && RimWorldVersion.Minor >= 5);
            
        // Get version string for display
        public static string VersionString => 
            $"{RimWorldVersion.Major}.{RimWorldVersion.Minor}.{RimWorldVersion.Build}";
        
        // Store discovered methods and fields    
        private static MethodInfo _resolveSymbolMethod = null;
        private static FieldInfo _symbolResolversField = null;
        
        // Cache of reflection-found fields and methods
        private static Dictionary<string, MethodInfo> _foundMethods = new Dictionary<string, MethodInfo>();
        private static Dictionary<string, FieldInfo> _foundFields = new Dictionary<string, FieldInfo>();
            
        /// <summary>
        /// Initialize compatibility layer and perform version checks
        /// </summary>
        public static void Initialize()
        {
            Log.Message($"[KCSG] Setting up compatibility for RimWorld {VersionString}");
            
            // Perform deep API exploration
            ExploreGlobalSettingsAPIs();
            
            // Attempt to discover key BaseGen components
            ExploreBaseGenComponents();
            
            // Try to find SymbolResolver class
            Type symbolResolverType = typeof(RimWorld.BaseGen.SymbolResolver);
            if (symbolResolverType != null)
            {
                Log.Message("[KCSG] Found RimWorld.BaseGen.SymbolResolver class");
            }
        }
        
        /// <summary>
        /// Try to discover available BaseGen.GlobalSettings APIs
        /// </summary>
        private static void ExploreGlobalSettingsAPIs()
        {
            try
            {
                Type globalSettingsType = typeof(RimWorld.BaseGen.GlobalSettings);
                
                // List all methods in the class
                Log.Message("[KCSG] Exploring RimWorld.BaseGen.GlobalSettings APIs...");
                Log.Message("[KCSG] Available methods:");
                
                // Find all methods
                foreach (var method in globalSettingsType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    // Skip inherited methods from Object
                    if (method.DeclaringType == typeof(object)) continue;
                    
                    string paramInfo = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Log.Message($"[KCSG] - {method.ReturnType.Name} {method.Name}({paramInfo})");
                    
                    // Cache all method references for later use
                    _foundMethods[method.Name] = method;
                    
                    // Try to identify symbol resolution methods more aggressively
                    if ((method.Name.Contains("Resolve") || method.Name.Contains("Symbol") || 
                         method.Name.ToLowerInvariant().Contains("resolver")) && 
                        method.GetParameters().Length >= 1)
                    {
                        if (method.GetParameters()[0].ParameterType == typeof(string))
                        {
                            Log.Message($"[KCSG] Found potential symbol resolution method: {method.Name}");
                            if (_resolveSymbolMethod == null)
                            {
                                _resolveSymbolMethod = method;
                            }
                        }
                    }
                }
                
                // Find all fields
                Log.Message("[KCSG] Available fields:");
                foreach (var field in globalSettingsType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    Log.Message($"[KCSG] - {field.FieldType.Name} {field.Name}");
                    
                    // Cache all field references
                    _foundFields[field.Name] = field;
                    
                    // Try to identify symbol resolver collections
                    if ((field.Name.Contains("symbol") || field.Name.Contains("resolver") || 
                         field.Name.Contains("Symbol") || field.Name.Contains("Resolver")) && 
                        field.FieldType.IsGenericType && 
                        field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                    {
                        Log.Message($"[KCSG] Found potential symbol resolvers field: {field.Name}");
                        _symbolResolversField = field;
                    }
                }
                
                // Look for properties
                Log.Message("[KCSG] Available properties:");
                foreach (var prop in globalSettingsType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    Log.Message($"[KCSG] - {prop.PropertyType.Name} {prop.Name}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error exploring GlobalSettings APIs: {ex}");
            }
        }
        
        /// <summary>
        /// Explore more BaseGen components to handle compatibility
        /// </summary>
        private static void ExploreBaseGenComponents()
        {
            try
            {
                // Try to find classes related to BaseGen symbol resolvers
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            // Look for SymbolResolver base or derived classes
                            if (type.Name.Contains("SymbolResolver") || 
                                type.Namespace?.Contains("BaseGen") == true)
                            {
                                // Check if this type has methods we might need
                                foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                                {
                                    if ((method.Name.Contains("Resolve") || method.Name.Contains("Symbol")) && 
                                        method.GetParameters().Length >= 1 &&
                                        method.GetParameters()[0].ParameterType == typeof(string))
                                    {
                                        // Found a method that might be useful
                                        string key = $"{type.Name}.{method.Name}";
                                        _foundMethods[key] = method;
                                    }
                                }
                            }
                        }
                    }
                    catch 
                    { 
                        // Skip assemblies that can't be reflected
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[KCSG] Error exploring BaseGen components: {ex}");
            }
        }
        
        /// <summary>
        /// Try to get any native symbol resolvers from RimWorld
        /// </summary>
        public static Dictionary<string, Type> GetSymbolResolvers()
        {
            try
            {
                // First try the field we found during exploration
                if (_symbolResolversField != null)
                {
                    Log.Message($"[KCSG] Using discovered field: {_symbolResolversField.Name}");
                    try 
                    {
                        return _symbolResolversField.GetValue(BaseGen.globalSettings) as Dictionary<string, Type>;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG] Error accessing discovered field: {ex.Message}");
                        // Continue to try other approaches
                    }
                }
                
                // Try standard field name
                FieldInfo field = typeof(RimWorld.BaseGen.GlobalSettings).GetField("symbolResolvers", 
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                
                if (field != null)
                {
                    Log.Message("[KCSG] Found symbolResolvers field in RimWorld");
                    _symbolResolversField = field; // Cache for future use
                    return field.GetValue(BaseGen.globalSettings) as Dictionary<string, Type>;
                }
                else
                {
                    // Try alternative field names
                    string[] possibleFieldNames = new[] { 
                        "resolvers", "SymbolResolvers", "Resolvers", "symbolResolver", 
                        "symResolvers", "symbolTable", "resolverDictionary", "symbols",
                        "defSymbols", "symbolRegistry", "registeredSymbols"
                    };
                    
                    foreach (var fieldName in possibleFieldNames)
                    {
                        field = typeof(RimWorld.BaseGen.GlobalSettings).GetField(fieldName, 
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            
                        if (field != null && 
                            field.FieldType.IsGenericType && 
                            field.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                        {
                            Log.Message($"[KCSG] Found alternative symbol resolvers field: {fieldName}");
                            _symbolResolversField = field; // Cache for future use
                            return field.GetValue(BaseGen.globalSettings) as Dictionary<string, Type>;
                        }
                    }
                    
                    // Last resort - try any dictionary field that might contain symbols
                    foreach (var foundField in _foundFields.Values)
                    {
                        if (foundField.FieldType.IsGenericType && 
                            foundField.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                        {
                            Type[] genericArgs = foundField.FieldType.GetGenericArguments();
                            if (genericArgs.Length == 2 && 
                                genericArgs[0] == typeof(string) && 
                                typeof(Type).IsAssignableFrom(genericArgs[1]))
                            {
                                Log.Message($"[KCSG] Trying field as candidate: {foundField.Name}");
                                try 
                                {
                                    var value = foundField.GetValue(BaseGen.globalSettings) as Dictionary<string, Type>;
                                    if (value != null && value.Count > 0)
                                    {
                                        Log.Message($"[KCSG] Using field {foundField.Name} with {value.Count} entries");
                                        _symbolResolversField = foundField;
                                        return value;
                                    }
                                }
                                catch (Exception) { }
                            }
                        }
                    }
                    
                    Log.Warning("[KCSG] Could not find symbolResolvers field in RimWorld - limited compatibility");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG] Error accessing native symbol resolvers: {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// Try to use native symbol resolution if available
        /// </summary>
        public static bool TryNativeResolve(string symbol, ResolveParams rp)
        {
            try
            {
                // First try the method we found during exploration
                if (_resolveSymbolMethod != null)
                {
                    try
                    {
                        var parameters = _resolveSymbolMethod.GetParameters();
                        
                        if (parameters.Length == 2 && 
                            parameters[0].ParameterType == typeof(string) && 
                            parameters[1].ParameterType == typeof(ResolveParams))
                        {
                            object result = _resolveSymbolMethod.Invoke(BaseGen.globalSettings, new object[] { symbol, rp });
                            if (result is bool) return (bool)result;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG] Error using discovered resolution method: {ex.Message}");
                        // Continue to try other approaches
                    }
                }
                
                // Try standard method name
                MethodInfo method = typeof(RimWorld.BaseGen.GlobalSettings).GetMethod("TryResolveSymbol", 
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (method != null)
                {
                    try
                    {
                        return (bool)method.Invoke(BaseGen.globalSettings, new object[] { symbol, rp });
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[KCSG] Error using TryResolveSymbol: {ex.Message}");
                        // Continue to try other approaches
                    }
                }
                
                // Try alternative method names
                string[] possibleMethodNames = new[] { 
                    "ResolveSymbol", "Resolve", "TryResolve", "DoResolve", 
                    "ResolveSymbolMethod", "TrySymbolResolve", "TryResolveUsingSymbol",
                    "ProcessSymbol", "ApplySymbol", "ExecuteSymbol"
                };
                
                foreach (var methodName in possibleMethodNames)
                {
                    if (_foundMethods.TryGetValue(methodName, out method))
                    {
                        // Use method from cache
                    }
                    else
                    {
                        method = typeof(RimWorld.BaseGen.GlobalSettings).GetMethod(methodName, 
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                        
                    if (method != null)
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length >= 1 && parameters[0].ParameterType == typeof(string))
                        {
                            Log.Message($"[KCSG] Found alternative symbol resolution method: {methodName}");
                            
                            try
                            {
                                if (parameters.Length == 2 && parameters[1].ParameterType == typeof(ResolveParams))
                                {
                                    _resolveSymbolMethod = method; // Cache for future use
                                    object result = method.Invoke(BaseGen.globalSettings, new object[] { symbol, rp });
                                    if (result is bool) return (bool)result;
                                }
                                else if (parameters.Length == 1)
                                {
                                    // Try one parameter version and ignore ResolveParams
                                    object result = method.Invoke(BaseGen.globalSettings, new object[] { symbol });
                                    if (result is bool) return (bool)result;
                                }
                            }
                            catch (Exception) { }
                        }
                    }
                }
                
                // Last resort - try to use any method that takes the right parameters
                foreach (var foundMethod in _foundMethods.Values)
                {
                    if (foundMethod.GetParameters().Length >= 1 && 
                        foundMethod.GetParameters()[0].ParameterType == typeof(string))
                    {
                        try 
                        {
                            var parameters = foundMethod.GetParameters();
                            if (parameters.Length == 2 && parameters[1].ParameterType == typeof(ResolveParams))
                            {
                                Log.Message($"[KCSG] Trying method as candidate: {foundMethod.Name}");
                                object result = foundMethod.Invoke(BaseGen.globalSettings, new object[] { symbol, rp });
                                if (result is bool) return (bool)result;
                            }
                        }
                        catch (Exception) { }
                    }
                }
                
                Log.Warning($"[KCSG] Could not find TryResolveSymbol method - falling back to custom resolution for '{symbol}'");
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG] Error using native resolution method: {ex}");
            }
            return false;
        }
    }
} 