using System;

namespace Prepatcher
{
    /// <summary>
    /// Marks a field or method to be processed by Zetrith's Prepatcher
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Method, AllowMultiple = false)]
    public class PrepatcherFieldAttribute : Attribute
    {
    }
} 