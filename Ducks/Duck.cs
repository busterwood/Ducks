using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Ducks
{
    public static class Duck
    {
        static readonly ConcurrentDictionary<TypePair, Func<object, object>> casts = new ConcurrentDictionary<TypePair, Func<object, object>>();

        public static T Cast<T>(object obj) where T : class
        {

            var item = obj as T;
            if (item != null)
                return item;

            return (T)Cast(obj, typeof(T));
        }

        static object Cast(object from, Type to)
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));
            if (to == null)
                throw new ArgumentNullException(nameof(to));

            var func = casts.GetOrAdd(new TypePair(from.GetType(), to), pair => CreateProxy(pair.From, pair.To));
            return func(from);
        }

        /// <param name="duck">The duck</param>
        /// <param name="interface">the interface to cast <paramref name="duck"/></param>
        static Func<object, object> CreateProxy(Type duck, Type @interface)
        {
            if (duck == null)
                throw new ArgumentNullException(nameof(duck));
            if (@interface == null)
                throw new ArgumentNullException(nameof(@interface));
            if (!@interface.IsInterface)
                throw new ArgumentException($"{@interface} is not an interface");

            AppDomain domain = Thread.GetDomain();
            string assemblyName = "Ducks_Proxy_" + @interface.AsmName() + "_" + duck.AsmName() + ".dll";
            var assemblyBuilder = domain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);
            var typeBuilder = moduleBuilder.DefineType("Proxy");
            typeBuilder.AddInterfaceImplementation(@interface);


        }

        static string AsmName(this Type type) => type.Name.Replace(".", "_").Replace("+", "-");
    }
}