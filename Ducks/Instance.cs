using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using static System.Reflection.MethodAttributes;
using static System.Reflection.CallingConventions;

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

            var ctor = DefineConstructor(typeBuilder, duck, duckField);

            foreach (var face in @interface.GetInterfaces().Concat(@interface))
                DefineMembers(duck, face, typeBuilder, duckField);

            var create = DefineStaticCreateMethod(duck, typeBuilder, ctor);
            DefineUnwrapMethod(typeBuilder, duckField);

            Type t = typeBuilder.CreateType();
            return (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>), t.GetMethod("Create", BindingFlags.Static | BindingFlags.Public));
        }

        static void DefineMembers(Type duck, Type @interface, TypeBuilder typeBuilder, FieldBuilder duckField)
        {
            foreach (var method in @interface.GetMethods().Where(mi => !mi.IsSpecialName)) // ignore get and set property methods
            {
                var duckMethod = FindDuckMethod(duck, method);
                AddMethod(typeBuilder, duckMethod, method, duckField);
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
            var create = typeBuilder.DefineMethod("Create", Public | MethodAttributes.Static, Standard, typeof(object), new[] { typeof(object) });
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
                var getMethod = AddMethod(typeBuilder, duckProp.GetGetMethod(), prop.GetGetMethod(), duckField);
                propBuilder.SetGetMethod(getMethod);
            }
            if (prop.CanWrite)
            {
                var setMethod = AddMethod(typeBuilder, duckProp.GetSetMethod(), prop.GetSetMethod(), duckField);
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
            var addMethod = AddMethod(typeBuilder, duckEvent.GetAddMethod(), evt.GetAddMethod(), duckField);
            evtBuilder.SetAddOnMethod(addMethod);
            var removeMethod = AddMethod(typeBuilder, duckEvent.GetRemoveMethod(), evt.GetRemoveMethod(), duckField);
            evtBuilder.SetRemoveOnMethod(removeMethod);
        }

    }
}