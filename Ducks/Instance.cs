using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace BusterWood.Ducks
{
    public static class Instance
    {
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

            AppDomain domain = Thread.GetDomain();
            string assemblyName = "Ducks_Instance_" + @interface.AsmName() + "_" + duck.AsmName() + ".dll";
            var assemblyBuilder = domain.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
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
                var duckMethod = FindDuckMethod(duck, method);
                typeBuilder.AddMethod(duckMethod, method, duckField);
            }

            foreach (var prop in @interface.GetProperties())
            {
                var duckProp = FindDuckProperty(duck, prop);
                AddProperty(typeBuilder, duckProp, prop, duckField);
            }

            foreach (var evt in @interface.GetEvents())
            {
                var duckEvent = FindDuckEvent(duck, evt);
                AddEvent(typeBuilder, duckEvent, evt, duckField);
            }
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
                
        static PropertyInfo FindDuckProperty(Type duck, PropertyInfo prop)
        {
            try
            {
                var found = duck.GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance, null, prop.PropertyType, prop.ParameterTypes(), null);
                if (found == null)
                    throw new InvalidCastException($"Type {duck.Name} does not have a property {prop.Name} or parameters types do not match");
                if (prop.CanRead && !found.CanRead)
                    throw new InvalidCastException($"Type {duck.Name} does not have a get property {prop.Name}");
                if (prop.CanWrite && !found.CanWrite)
                    throw new InvalidCastException($"Type {duck.Name} does not have a set property {prop.Name}");
                return found;
            }
            catch (AmbiguousMatchException)
            {
                throw new InvalidCastException($"Type {duck.Name} has an ambiguous match for property {prop.Name}"); //TODO: parameter list
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

        private static EventInfo FindDuckEvent(Type duck, EventInfo evt)
        {
            try
            {
                var found = duck.GetEvent(evt.Name);
                if (found == null)
                    throw new InvalidCastException($"Type {duck.Name} does not have an event {evt.Name}");
                if (evt.EventHandlerType != found.EventHandlerType)
                    throw new InvalidCastException($"Type {duck.Name} event {evt.Name} has type {found.EventHandlerType.Name} but expected type {evt.EventHandlerType.Name}");
                return found;
            }
            catch (AmbiguousMatchException)
            {
                throw new InvalidCastException($"Type {duck.Name} has an ambiguous match for event {evt.Name}");
            }
        }

        private static void AddEvent(TypeBuilder typeBuilder, EventInfo duckEvent, EventInfo evt, FieldBuilder duckField)
        {
            EventBuilder evtBuilder = typeBuilder.DefineEvent(evt.Name, EventAttributes.None, evt.EventHandlerType);
            var addMethod = typeBuilder.AddMethod(duckEvent.GetAddMethod(), evt.GetAddMethod(), duckField);
            evtBuilder.SetAddOnMethod(addMethod);
            var removeMethod = typeBuilder.AddMethod(duckEvent.GetRemoveMethod(), evt.GetRemoveMethod(), duckField);
            evtBuilder.SetRemoveOnMethod(removeMethod);
        }

    }
}