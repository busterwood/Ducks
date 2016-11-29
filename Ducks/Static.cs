using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static System.Reflection.MethodAttributes;
using static System.Reflection.CallingConventions;

namespace BusterWood.Ducks
{
    public static class Static
    {
        const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
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

            string assemblyName = "Ducks_Static_" + @interface.AsmName() + "_" + duck.AsmName() + ".dll";
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
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
            foreach (var method in @interface.GetMethods().Where(m => !m.IsSpecialName))
            {
                MethodInfo duckMethod = duck.FindDuckMethod(method, PublicStatic);
                AddMethod(typeBuilder, duckMethod, method);
            }

            foreach (var prop in @interface.GetProperties())
            {
                var duckProp = duck.FindDuckProperty(prop, PublicStatic);
                AddProperty(typeBuilder, duckProp, prop);
            }

            foreach (var evt in @interface.GetEvents())
            {
                var duckEvent = duck.FindDuckEvent(evt, PublicStatic);
                AddEvent(typeBuilder, duckEvent, evt);
            }
        }

        static MethodBuilder AddMethod(TypeBuilder typeBuilder, MethodInfo duckMethod, MethodInfo interfaceMethod)
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
            return mb;
        }

        static void AddProperty(TypeBuilder typeBuilder, PropertyInfo duckProp, PropertyInfo prop)
        {
            PropertyBuilder propBuilder = typeBuilder.DefineProperty(prop.Name, PropertyAttributes.None, prop.PropertyType, prop.ParameterTypes());
            if (prop.CanRead)
            {
                var getMethod = AddMethod(typeBuilder, duckProp.GetGetMethod(), prop.GetGetMethod());
                propBuilder.SetGetMethod(getMethod);
            }
            if (prop.CanWrite)
            {
                var setMethod = AddMethod(typeBuilder, duckProp.GetSetMethod(), prop.GetSetMethod());
                propBuilder.SetSetMethod(setMethod);
            }
        }

        static void AddEvent(TypeBuilder typeBuilder, EventInfo duckEvent, EventInfo evt)
        {
            EventBuilder evtBuilder = typeBuilder.DefineEvent(evt.Name, EventAttributes.None, evt.EventHandlerType);
            var addMethod = AddMethod(typeBuilder, duckEvent.GetAddMethod(), evt.GetAddMethod());
            evtBuilder.SetAddOnMethod(addMethod);
            var removeMethod = AddMethod(typeBuilder, duckEvent.GetRemoveMethod(), evt.GetRemoveMethod());
            evtBuilder.SetRemoveOnMethod(removeMethod);
        }

    }
}