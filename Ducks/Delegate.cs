using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace BusterWood.Ducks
{
    public static class Delegates
    {
        static readonly MostlyReadDictionary<TypePair, Func<Delegate, object>> casts = new MostlyReadDictionary<TypePair, Func<Delegate, object>>();

        internal static object Cast(Delegate from, Type to)
        {
            var func = casts.GetOrAdd(new TypePair(from.GetType(), to), pair => CreateProxy(pair.From, pair.To));
            return func(from);
        }

        /// <param name="duck">The duck</param>
        /// <param name="interface">the interface to cast <paramref name="duck"/></param>
        static Func<Delegate, object> CreateProxy(Type duck, Type @interface)
        {
            if (duck == null)
                throw new ArgumentNullException(nameof(duck));
            if (@interface == null)
                throw new ArgumentNullException(nameof(@interface));
            if (!@interface.IsInterface)
                throw new ArgumentException($"{@interface} is not an interface");

            AppDomain domain = Thread.GetDomain();
            string assemblyName = "Ducks_Instance_" + @interface.AsmName() + "_" + duck.AsmName() + ".dll";
            var assemblyBuilder = domain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);

            TypeBuilder typeBuilder = moduleBuilder.DefineType("Proxy");
            foreach (var face in @interface.GetInterfaces().Concat(@interface, typeof(IDuck)))
                typeBuilder.AddInterfaceImplementation(face);
    
            var duckField = typeBuilder.DefineField("duck", duck, FieldAttributes.Private | FieldAttributes.InitOnly);

            var ctor = typeBuilder.DefineConstructor(duck, duckField);

            bool defined = false;
            foreach (var face in @interface.GetInterfaces().Concat(@interface))
                DefineMembers(duck, face, typeBuilder, duckField, ref defined);

            var create = typeBuilder.DefineStaticCreateMethod(duck, ctor, typeof(Delegate));
            typeBuilder.DefineUnwrapMethod(duckField);

            Type t = typeBuilder.CreateType();
            return (Func<Delegate, object>)Delegate.CreateDelegate(typeof(Func<Delegate, object>), t.GetMethod("Create", BindingFlags.Static | BindingFlags.Public));
        }

        static void DefineMembers(Type duck, Type @interface, TypeBuilder typeBuilder, FieldBuilder duckField, ref bool defined)
        {
            foreach (var method in @interface.GetMethods().Where(mi => !mi.IsSpecialName)) // ignore get and set property methods
            {
                if (defined)
                    throw new InvalidCastException("More than one method one interface");
                CheckDelegateMatchesMethod(duck, @interface, method);
                var duckMethod = duck.GetMethod("Invoke");
                typeBuilder.AddMethod(duckMethod, method, duckField);
                defined = true;
            }
        }

        static void CheckDelegateMatchesMethod(Type duck, Type @interface, MethodInfo method)
        {
            var duckMethod = duck.GetMethod("Invoke");
            if (method.GetParameters().Length != duckMethod.GetParameters().Length)
                throw new InvalidCastException($"Delegate has a different number of parameters to {@interface.Name}.{method.Name}");

            int i = 0;
            var dps = duckMethod.GetParameters();
            foreach (var mp in method.GetParameters())
            {
                if (mp.ParameterType != dps[i].ParameterType)
                    throw new InvalidCastException($"Parameters types differs at index {i}, delegate parameter type {dps[i].ParameterType.Name} does not match {@interface.Name}.{method.Name} parameter type {mp.ParameterType.Name}");
                i++;
            }
            if (method.ReturnType != duckMethod.ReturnType)
                throw new InvalidCastException($"Return type differs, delegate returns {dps[i].ParameterType.Name} but method {@interface.Name}.{method.Name} returns {method.Name}");
        }
                
    }
}