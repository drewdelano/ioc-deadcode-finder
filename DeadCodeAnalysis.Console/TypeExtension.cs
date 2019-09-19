using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DeadCodeAnalysis.ConsoleExample
{
    static class TypeExtension
    {
        public static bool ImplementsInterface(this Type type, Type interfaceType)
        {
            return type.GetInterfaces().Any(i => i.IsGenericType ? i.GetGenericTypeDefinition().ToString() == interfaceType.ToString() : i.ToString() == interfaceType.ToString());
        }

        public static List<MethodInfo> GetMethods(this Type type, string name)
        {
            return type.GetMethods().Where(mi => mi.Name == name).ToList();
        }

        public static bool IgnoreProbableConstClasses(this Type type)
        {
            return type
                .GetMembers(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Any(m => !((m as FieldInfo)?.Attributes.HasFlag(FieldAttributes.Literal) ?? true));
        }
    }
}
