using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BusterWood.Ducks
{
    static class Extensions
    {
        public static Type[] ParameterTypes(this MethodInfo method) => method.GetParameters().Select(p => p.ParameterType).ToArray();

        public static Type[] ParameterTypes(this PropertyInfo method) => method.GetIndexParameters().Select(p => p.ParameterType).ToArray();

        public static string AsmName(this Type type) => type.Name.Replace(".", "_").Replace("+", "-");

        public static IEnumerable<T> Concat<T>(this IEnumerable<T> source, params T[] items) => Enumerable.Concat(source, items);

    }
}