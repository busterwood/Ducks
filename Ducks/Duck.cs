using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using static System.Reflection.MethodAttributes;
using static System.Reflection.CallingConventions;

namespace Ducks
{
    public static class Duck
    {
        static readonly ConcurrentDictionary<TypePair, Func<object, object>> casts = new ConcurrentDictionary<TypePair, Func<object, object>>();

        public static T Cast<T>(object obj) where T : class
        {
            var t = obj as T;
            return t != null ? t : (T)Cast(obj, typeof(T));
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
            TypeBuilder typeBuilder = moduleBuilder.DefineType("Proxy");
            typeBuilder.AddInterfaceImplementation(@interface);

            var duckField = typeBuilder.DefineField("duck", duck, FieldAttributes.Private | FieldAttributes.InitOnly);

            var ctor = DefineConstructor(typeBuilder, duck, duckField);

            foreach (var method in @interface.GetMethods())
            {
                MethodInfo duckMethod = FindDuckMethod(duck, method);
                AddMethod(typeBuilder, duckMethod, method, duckField);
            }

            var create = DefineStaticCreateMethod(duck, typeBuilder, ctor);

            Type t = typeBuilder.CreateType();
            return (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>), t.GetMethod("Create", BindingFlags.Static | BindingFlags.Public));
        }

        static MethodBuilder DefineStaticCreateMethod(Type duck, TypeBuilder typeBuilder, ConstructorBuilder ctor)
        {
            var create = typeBuilder.DefineMethod("Create", Public | Static, Standard, typeof(object), new[] { typeof(object) });
            var il = create.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // push obj
            il.Emit(OpCodes.Castclass, duck);   // cast obj to duck
            il.Emit(OpCodes.Newobj, ctor);  // call ctor(duck)
            il.Emit(OpCodes.Ret);   // end of create
            return create;
        }

        static ConstructorBuilder DefineConstructor(TypeBuilder typeBuilder, Type duck, FieldBuilder duckField)
        {
            var ctor = typeBuilder.DefineConstructor(Public, HasThis, new[] { duck });
            var il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // push this
            il.Emit(OpCodes.Ldarg_1); // push duck
            il.Emit(OpCodes.Stfld, duckField); // store the parameter in the duck field
            il.Emit(OpCodes.Ret);   // end of ctor
            return ctor;
        }

        static MethodInfo FindDuckMethod(Type duck, MethodInfo method)
        {
            try
            {
                var found = duck.GetMethod(method.Name, method.ParameterTypes());
                if (found == null)
                    throw new InvalidCastException($"Type {duck.Name} does not have a method {method.Name}");
                return found;
            }
            catch (AmbiguousMatchException)
            {
                throw new InvalidCastException($"Type {duck.Name} has an ambiguous match for method {method.Name}"); //TODO: parameter list
            }
        }

        static void AddMethod(TypeBuilder typeBuilder, MethodInfo duckMethod, MethodInfo interfaceMethod, FieldBuilder duckField)
        {
            var mb = typeBuilder.DefineMethod(interfaceMethod.Name, Public | Virtual | Final, HasThis, interfaceMethod.ReturnType, interfaceMethod.ParameterTypes());
            var il = mb.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0); // push this
            il.Emit(OpCodes.Ldfld, duckField); // push duck field

            // push all the arguments onto the stack
            int i = 1;
            foreach (var p in interfaceMethod.GetParameters())
                il.Emit(OpCodes.Ldarg, i++);

            // call the duck's method
            il.EmitCall(OpCodes.Callvirt, duckMethod, null);

            // return
            il.Emit(OpCodes.Ret);
        }

        static Type[] ParameterTypes(this MethodInfo method) => method.GetParameters().Select(p => p.ParameterType).ToArray();

        static string AsmName(this Type type) => type.Name.Replace(".", "_").Replace("+", "-");
    }
}