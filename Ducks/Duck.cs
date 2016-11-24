﻿using System;

namespace BusterWood.Ducks
{
    public static class Duck
    {
        /// <summary>Tries to cast the a delegate to a interface with a single method</summary>
        /// <param name="from">The delegate to cast from</param>
        /// <typeparam name="T">The type of the interface to cast to</typeparam>
        public static T Cast<T>(Delegate from) where T : class => (T)Cast(from, typeof(T));

        /// <summary>Tries to cast the a delegate to a interface with a single method</summary>
        /// <param name="from">The delegate to cast from</param>
        /// <param name="to">The type of the interface to cast to</param>
        public static object Cast(Delegate from, Type to) => Delegates.Cast(from, to);

        /// <summary>Tries to cast the static methods of a type to an interface</summary>
        /// <param name="from">The type containing static methods</param>
        /// <typeparam name="T">The type of the interface to cast to</typeparam>
        public static T Cast<T>(Type from) where T : class => (T)Cast(from, typeof(T));

        /// <summary>Tries to cast the static methods of a type to an interface</summary>
        /// <param name="from">The type containing static methods</param>
        /// <param name="to">The type of the interface to cast to</param>
        public static object Cast(Type from, Type to) => Static.Cast(from, to);

        /// <summary>Tries to cast an instance to an interface</summary>
        /// <param name="from">The object to cast from</param>
        /// <typeparam name="T">The type of the interface to cast to</typeparam>
        public static T Cast<T>(object from) where T : class
        {
            var t = from as T;
            return t != null ? t : (T)Cast(from, typeof(T));
        }

        /// <summary>Tries to cast an object an interface</summary>
        /// <param name="from">The object to cast, which can be a type (for static casting), a delegate, or an instance of any object or struct</param>
        /// <param name="to">The type of the interface to cast to</param>
        /// <remarks>This methods supports static casts of types, casts of delegates, casts of objects, and re-casting existing ducks</remarks>
        public static object Cast(object from, Type to)
        {
            var duck = from as IDuck;
            if (duck != null)
                from = duck.Unwrap();

            if (from is Type) // static cast
                return Cast((Type)from, to); 

            if (from is Delegate) // delegate cast
                return Cast((Delegate)from, to);    
            
            // else instance cast try
            return Instance.Cast(from, to);
        }

    }
}
