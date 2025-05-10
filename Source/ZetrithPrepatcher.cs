using System;

// This is only used to satisfy the compiler
// Zetrith's Prepatcher doesn't actually need this class to be defined
// It only looks for the attribute by name
namespace ZetrithPrepatcher
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class PrepatchAttribute : Attribute
    {
        public string TypeName { get; }
        public string MethodName { get; }

        public PrepatchAttribute(string typeName, string methodName)
        {
            TypeName = typeName;
            MethodName = methodName;
        }
    }
} 