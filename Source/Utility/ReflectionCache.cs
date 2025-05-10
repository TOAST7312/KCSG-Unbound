using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace KCSG
{
    /// <summary>
    /// Utility class for caching reflection operations to improve performance
    /// </summary>
    public static class ReflectionCache
    {
        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();
        private static readonly Dictionary<(Type, string), MethodInfo> MethodCache = new Dictionary<(Type, string), MethodInfo>();
        private static readonly Dictionary<(Type, string), FieldInfo> FieldCache = new Dictionary<(Type, string), FieldInfo>();
        private static readonly Dictionary<(Type, string), PropertyInfo> PropertyCache = new Dictionary<(Type, string), PropertyInfo>();

        /// <summary>
        /// Get a type by its full name, using caching for performance
        /// </summary>
        public static Type GetTypeByName(string fullName)
        {
            if (TypeCache.TryGetValue(fullName, out Type cachedType))
                return cachedType;
            
            Type newType = GenTypes.GetTypeInAnyAssembly(fullName);
            if (newType != null)
                TypeCache[fullName] = newType;
            
            return newType;
        }

        /// <summary>
        /// Get a method from a type, using caching for performance
        /// </summary>
        public static MethodInfo GetMethod(Type type, string methodName, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
        {
            var key = (type, methodName);
            if (MethodCache.TryGetValue(key, out MethodInfo cachedMethod))
                return cachedMethod;
            
            MethodInfo newMethod = type?.GetMethod(methodName, bindingFlags);
            if (newMethod != null)
                MethodCache[key] = newMethod;
            
            return newMethod;
        }

        /// <summary>
        /// Get a field from a type, using caching for performance
        /// </summary>
        public static FieldInfo GetField(Type type, string fieldName, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
        {
            var key = (type, fieldName);
            if (FieldCache.TryGetValue(key, out FieldInfo cachedField))
                return cachedField;
            
            FieldInfo newField = type?.GetField(fieldName, bindingFlags);
            if (newField != null)
                FieldCache[key] = newField;
            
            return newField;
        }

        /// <summary>
        /// Get a property from a type, using caching for performance
        /// </summary>
        public static PropertyInfo GetProperty(Type type, string propertyName, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
        {
            var key = (type, propertyName);
            if (PropertyCache.TryGetValue(key, out PropertyInfo cachedProperty))
                return cachedProperty;
            
            PropertyInfo newProperty = type?.GetProperty(propertyName, bindingFlags);
            if (newProperty != null)
                PropertyCache[key] = newProperty;
            
            return newProperty;
        }

        /// <summary>
        /// Safely invoke a method using reflection
        /// </summary>
        public static object SafeInvoke(MethodInfo method, object instance, params object[] parameters)
        {
            try
            {
                return method?.Invoke(instance, parameters);
            }
            catch (Exception ex)
            {
                Log.Warning($"[KCSG Unbound] Failed to invoke method {method?.Name}: {ex.Message}");
                return null;
            }
        }
    }
} 