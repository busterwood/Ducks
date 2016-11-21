﻿using System;
using System.Linq;
using System.Reflection;

namespace Ducks
{
    static class Extensions
    {
        public static Type[] ParameterTypes(this MethodInfo method) => method.GetParameters().Select(p => p.ParameterType).ToArray();

        public static Type[] ParameterTypes(this PropertyInfo method) => method.GetIndexParameters().Select(p => p.ParameterType).ToArray();

        public static string AsmName(this Type type) => type.Name.Replace(".", "_").Replace("+", "-");
    }
}