using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static System.Reflection.MethodAttributes;
using static System.Reflection.CallingConventions;

namespace BusterWood.Ducks
{
    static class Extensions
    {
        public static Type[] ParameterTypes(this MethodInfo method) => method.GetParameters().Select(p => p.ParameterType).ToArray();

        public static Type[] ParameterTypes(this PropertyInfo method) => method.GetIndexParameters().Select(p => p.ParameterType).ToArray();

        public static string AsmName(this Type type) => type.Name.Replace(".", "_").Replace("+", "-");

        public static IEnumerable<T> Concat<T>(this IEnumerable<T> source, params T[] items) => Enumerable.Concat(source, items);


        public static ConstructorBuilder DefineConstructor(this TypeBuilder typeBuilder, Type duck, FieldBuilder duckField)
        {
            var ctor = typeBuilder.DefineConstructor(Public, HasThis, new[] { duck });
            var il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // push this
            il.Emit(OpCodes.Ldarg_1); // push duck
            il.Emit(OpCodes.Stfld, duckField); // store the parameter in the duck field
            il.Emit(OpCodes.Ret);   // end of ctor
            return ctor;
        }


        public static MethodBuilder DefineUnwrapMethod(this TypeBuilder typeBuilder, FieldBuilder duckField)
        {
            var create = typeBuilder.DefineMethod(nameof(IDuck.Unwrap), Public | Virtual | Final, HasThis, typeof(object), new Type[0]);
            var il = create.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // push this
            il.Emit(OpCodes.Ldfld, duckField); // push duck field
            il.Emit(OpCodes.Castclass, typeof(object));   // cast duck to object
            il.Emit(OpCodes.Ret);   // return the object
            return create;
        }


        public static MethodBuilder DefineStaticCreateMethod(this TypeBuilder typeBuilder, Type duck, ConstructorBuilder ctor, Type paramType)
        {
            var create = typeBuilder.DefineMethod("Create", Public | MethodAttributes.Static, Standard, typeof(object), new[] { paramType });
            var il = create.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); // push obj
            il.Emit(OpCodes.Castclass, duck);   // cast obj to duck
            il.Emit(OpCodes.Newobj, ctor);  // call ctor(duck)
            il.Emit(OpCodes.Ret);   // end of create
            return create;
        }

        public static MethodBuilder AddMethod(this TypeBuilder typeBuilder, MethodInfo duckMethod, MethodInfo interfaceMethod, FieldBuilder duckField)
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

    }
}