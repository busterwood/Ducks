using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using static System.Reflection.MethodAttributes;
using static System.Reflection.CallingConventions;
using System.Linq;

namespace Ducks
{
    public static class Delegates
    {
        static readonly ConcurrentDictionary<TypePair, Func<Delegate, object>> casts = new ConcurrentDictionary<TypePair, Func<Delegate, object>>();

        public static T Cast<T>(object from) where T : class
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));
            var duck = from as IDuck;
            if (duck != null && duck.Unwrap() is Delegate)
                from = duck.Unwrap() as Delegate;
            if (!(from is Delegate))
                throw new InvalidCastException($"{from.GetType().Name} is not a Delegate");
            return (T)Cast((Delegate)from, typeof(T));
        }

        public static T Cast<T>(Delegate obj) where T : class
        {
            var t = obj as T;
            return t != null ? t : (T)Cast(obj, typeof(T));
        }

        public static object Cast(Delegate from, Type to)
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));
            if (to == null)
                throw new ArgumentNullException(nameof(to));

            var duck = from as IDuck;
            if (duck != null && duck.Unwrap() is Delegate)
                from = duck.Unwrap() as Delegate;

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

            var ctor = DefineConstructor(typeBuilder, duck, duckField);

            bool defined = false;
            foreach (var face in @interface.GetInterfaces().Concat(@interface))
                DefineMembers(duck, face, typeBuilder, duckField, ref defined);

            var create = DefineStaticCreateMethod(duck, typeBuilder, ctor);
            DefineUnwrapMethod(typeBuilder, duckField);

            Type t = typeBuilder.CreateType();
            return (Func<Delegate, object>)Delegate.CreateDelegate(typeof(Func<Delegate, object>), t.GetMethod("Create", BindingFlags.Static | BindingFlags.Public));
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

        static void DefineMembers(Type duck, Type @interface, TypeBuilder typeBuilder, FieldBuilder duckField, ref bool defined)
        {
            foreach (var method in @interface.GetMethods().Where(mi => !mi.IsSpecialName)) // ignore get and set property methods
            {
                if (defined)
                    throw new InvalidCastException("More than one method one interface");
                CheckDelegateMatchesMethod(duck, @interface, method);
                var duckMethod = duck.GetMethod("Invoke");
                AddMethod(typeBuilder, duckMethod, method, duckField);
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

        static MethodBuilder AddMethod(TypeBuilder typeBuilder, MethodInfo duckMethod, MethodInfo interfaceMethod, FieldBuilder duckField)
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

            return mb;
        }

        static MethodBuilder DefineStaticCreateMethod(Type duck, TypeBuilder typeBuilder, ConstructorBuilder ctor)
        {
            var create = typeBuilder.DefineMethod("Create", Public | MethodAttributes.Static, Standard, typeof(object), new[] { typeof(Delegate) });
            var il = create.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // push obj
            il.Emit(OpCodes.Castclass, duck);   // cast obj to duck
            il.Emit(OpCodes.Newobj, ctor);  // call ctor(duck)
            il.Emit(OpCodes.Ret);   // end of create
            return create;
        }

        static MethodBuilder DefineUnwrapMethod(TypeBuilder typeBuilder, FieldBuilder duckField)
        {
            var create = typeBuilder.DefineMethod(nameof(IDuck.Unwrap), Public | Virtual | Final, HasThis, typeof(object), new Type[0]);
            var il = create.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // push this
            il.Emit(OpCodes.Ldfld, duckField); // push duck field
            il.Emit(OpCodes.Castclass, typeof(object));   // cast duck to object
            il.Emit(OpCodes.Ret);   // return the object
            return create;
        }
        
    }
    //public class Sample
    //{
    //    Func<int> duckD;

    //    public int Execute()
    //    {
    //        return duckD();
    //    }
    //}
}