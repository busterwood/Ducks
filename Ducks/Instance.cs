using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace BusterWood.Ducks
{
    public static class Instance
    {
        const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        static readonly MostlyReadDictionary<TypePair, Func<object, object>> casts = new MostlyReadDictionary<TypePair, Func<object, object>>();

        internal static object Cast(object from, Type to)
        {
            var func = casts.GetOrAdd(new TypePair(from.GetType(), to), pair => CreateProxy(pair.From, pair.To));
            return func(from);
        }

        /// <param name="duck">The duck</param>
        /// <param name="interface">the interface to cast <paramref name="duck"/></param>
        internal static Func<object, object> CreateProxy(Type duck, Type @interface)
        {
            if (duck == null)
                throw new ArgumentNullException(nameof(duck));
            if (@interface == null)
                throw new ArgumentNullException(nameof(@interface));
            if (!@interface.IsInterface)
                throw new ArgumentException($"{@interface} is not an interface");

            string assemblyName = "Ducks_Instance_" + @interface.AsmName() + "_" + duck.AsmName() + ".dll";
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName);

            TypeBuilder typeBuilder = moduleBuilder.DefineType("Proxy");

            foreach (var face in @interface.GetInterfaces().Concat(@interface, typeof(IDuck)))
                typeBuilder.AddInterfaceImplementation(face);

            var duckField = typeBuilder.DefineField("duck", duck, FieldAttributes.Private | FieldAttributes.InitOnly);

            var ctor = typeBuilder.DefineConstructor(duck, duckField);

            foreach (var face in @interface.GetInterfaces().Concat(@interface))
                DefineMembers(duck, face, typeBuilder, duckField);

            var create = typeBuilder.DefineStaticCreateMethod(duck, ctor, typeof(object));
            typeBuilder.DefineUnwrapMethod(duckField);

            Type t = typeBuilder.CreateType();
            return (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>), t.GetMethod("Create", BindingFlags.Static | BindingFlags.Public));
        }

        static void DefineMembers(Type duck, Type @interface, TypeBuilder typeBuilder, FieldBuilder duckField)
        {
            foreach (var method in @interface.GetMethods().Where(mi => !mi.IsSpecialName)) // ignore get and set property methods
            {
                var duckMethod = duck.FindDuckMethod(method, PublicInstance);
                typeBuilder.AddMethod(duckMethod, method, duckField);
            }

            foreach (var prop in @interface.GetProperties())
            {
                var duckProp = duck.FindDuckProperty(prop, PublicInstance);
                AddProperty(typeBuilder, duckProp, prop, duckField);
            }

            foreach (var evt in @interface.GetEvents())
            {
                var duckEvent = duck.FindDuckEvent(evt, PublicInstance);
                AddEvent(typeBuilder, duckEvent, evt, duckField);
            }
        }

        static void AddProperty(TypeBuilder typeBuilder, PropertyInfo duckProp, PropertyInfo prop, FieldBuilder duckField)
        {
            PropertyBuilder propBuilder = typeBuilder.DefineProperty(prop.Name, PropertyAttributes.None, prop.PropertyType, prop.ParameterTypes());
            if (prop.CanRead)
            {
                var getMethod = typeBuilder.AddMethod(duckProp.GetGetMethod(), prop.GetGetMethod(), duckField);
                propBuilder.SetGetMethod(getMethod);
            }
            if (prop.CanWrite)
            {
                var setMethod = typeBuilder.AddMethod(duckProp.GetSetMethod(), prop.GetSetMethod(), duckField);
                propBuilder.SetSetMethod(setMethod);
            }
        }

        static void AddEvent(TypeBuilder typeBuilder, EventInfo duckEvent, EventInfo evt, FieldBuilder duckField)
        {
            EventBuilder evtBuilder = typeBuilder.DefineEvent(evt.Name, EventAttributes.None, evt.EventHandlerType);
            var addMethod = typeBuilder.AddMethod(duckEvent.GetAddMethod(), evt.GetAddMethod(), duckField);
            evtBuilder.SetAddOnMethod(addMethod);
            var removeMethod = typeBuilder.AddMethod(duckEvent.GetRemoveMethod(), evt.GetRemoveMethod(), duckField);
            evtBuilder.SetRemoveOnMethod(removeMethod);
        }

    }
}