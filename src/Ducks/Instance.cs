using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static System.Reflection.MethodAttributes;
using static System.Reflection.CallingConventions;

namespace BusterWood.Ducks
{
    public static class Instance
    {
        const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
        static readonly MostlyReadDictionary<TypePair, IDuckInstanceFactory> casts = new MostlyReadDictionary<TypePair, IDuckInstanceFactory>();

        internal static object Cast(object from, Type to, MissingMethods missingMethods)
        {
            var factory = casts.GetOrAdd(new TypePair(from.GetType(), to, missingMethods), pair => CreateProxy(pair.From, pair.To, pair.MissingMethods));
            return factory.Create(from);
        }

        /// <param name="duck">The duck</param>
        /// <param name="interface">the interface to cast <paramref name="duck"/></param>
        internal static IDuckInstanceFactory CreateProxy(Type duck, Type @interface, MissingMethods missingMethods)
        {
            if (duck == null)
                throw new ArgumentNullException(nameof(duck));
            if (@interface == null)
                throw new ArgumentNullException(nameof(@interface));
            if (!@interface.GetTypeInfo().IsInterface)
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
                DefineMembers(duck, face, typeBuilder, duckField, missingMethods);

            typeBuilder.DefineUnwrapMethod(duckField);

            var factoryBuilder = CreateInstanceFactory(moduleBuilder, duck, ctor);

            Type factory = typeBuilder.CreateTypeInfo().GetType();
            return (IDuckInstanceFactory)Activator.CreateInstance(factory);
        }

        static TypeBuilder CreateInstanceFactory(ModuleBuilder moduleBuilder, Type duck, ConstructorInfo ctor)
        {
            TypeBuilder typeBuilder = moduleBuilder.DefineType("Factory");
            typeBuilder.AddInterfaceImplementation(typeof(IDuckInstanceFactory));

            var create = typeBuilder.DefineMethod("Create", Public | Virtual | Final, HasThis, typeof(object), new[] { typeof(object) });
            var il = create.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // push obj parameter
            il.Emit(OpCodes.Castclass, duck);   // cast obj to duck
            il.Emit(OpCodes.Newobj, ctor);  // call ctor(duck)
            il.Emit(OpCodes.Ret);   // end of create
            return typeBuilder;
        }

        static void DefineMembers(Type duck, Type @interface, TypeBuilder typeBuilder, FieldBuilder duckField, MissingMethods missingMethods)
        {
            foreach (var method in @interface.GetMethods().Where(mi => !mi.IsSpecialName)) // ignore get and set property methods
            {
                var duckMethod = duck.FindDuckMethod(method, PublicInstance, missingMethods);
                typeBuilder.AddMethod(duckMethod, method, duckField);
            }

            foreach (var prop in @interface.GetProperties())
            {
                var duckProp = duck.FindDuckProperty(prop, PublicInstance, missingMethods);
                AddProperty(typeBuilder, duckProp, prop, duckField);
            }

            foreach (var evt in @interface.GetEvents())
            {
                var duckEvent = duck.FindDuckEvent(evt, PublicInstance, missingMethods);
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