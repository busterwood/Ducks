using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using static System.Reflection.MethodAttributes;
using static System.Reflection.CallingConventions;

namespace BusterWood.Ducks
{
    public static class Static
    {
        static readonly MostlyReadDictionary<TypePair, object> proxies = new MostlyReadDictionary<TypePair, object>();

        internal static object Cast(Type from, Type to)
        {
            return proxies.GetOrAdd(new TypePair(from, to), pair => CreateStaticProxy(pair.From, pair.To));
        }

        /// <param name="duck">The duck</param>
        /// <param name="interface">the interface to cast <paramref name="duck"/></param>
        static object CreateStaticProxy(Type duck, Type @interface)
        {
            if (duck == null)
                throw new ArgumentNullException(nameof(duck));
            if (@interface == null)
                throw new ArgumentNullException(nameof(@interface));
            if (!@interface.IsInterface)
                throw new ArgumentException($"{@interface} is not an interface");

            AppDomain domain = Thread.GetDomain();
            string assemblyName = "Ducks_Static_" + @interface.AsmName() + "_" + duck.AsmName() + ".dll";
            var assemblyBuilder = domain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);

            TypeBuilder typeBuilder = moduleBuilder.DefineType("Proxy");

            typeBuilder.AddInterfaceImplementation(@interface);

            // interfaces that @interface inherits
            foreach (var parent in @interface.GetInterfaces())
                typeBuilder.AddInterfaceImplementation(parent);

            var ctor = DefineConstructor(typeBuilder);

            DefineMethods(typeBuilder, duck, @interface);

            // add methods for all parent interfaces
            foreach (var parent in @interface.GetInterfaces())
                DefineMethods(typeBuilder, duck, parent);

            Type t = typeBuilder.CreateType();
            return Activator.CreateInstance(t);
        }
        
        static ConstructorBuilder DefineConstructor(TypeBuilder typeBuilder)
        {
            var ctor = typeBuilder.DefineConstructor(Public, HasThis, new Type[0]);
            var il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ret);   // end of ctor
            return ctor;
        }

        static void DefineMethods(TypeBuilder typeBuilder, Type duck, Type @interface)
        {
            foreach (var method in @interface.GetMethods())
            {
                MethodInfo duckMethod = FindDuckMethod(duck, method);
                AddMethod(typeBuilder, duckMethod, method);
            }
        }

        static MethodInfo FindDuckMethod(Type duck, MethodInfo method)
        {
            try
            {
                var found = duck.GetMethod(method.Name, BindingFlags.Public | BindingFlags.Static, null, method.ParameterTypes(), null);
                if (found == null)
                    throw new InvalidCastException($"Type {duck.Name} does not have a static method {method.Name}");
                return found;
            }
            catch (AmbiguousMatchException)
            {
                throw new InvalidCastException($"Type {duck.Name} has an ambiguous match for method {method.Name}"); //TODO: parameter list
            }
        }

        static void AddMethod(TypeBuilder typeBuilder, MethodInfo duckMethod, MethodInfo interfaceMethod)
        {
            var mb = typeBuilder.DefineMethod(interfaceMethod.Name, Public | Virtual | Final, HasThis, interfaceMethod.ReturnType, interfaceMethod.ParameterTypes());
            var il = mb.GetILGenerator();

            // push all the arguments onto the stack
            int i = 1;
            foreach (var p in interfaceMethod.GetParameters())
                il.Emit(OpCodes.Ldarg, i++);

            // call the duck's method
            il.EmitCall(OpCodes.Call, duckMethod, null);

            // return
            il.Emit(OpCodes.Ret);
        }

    }
}