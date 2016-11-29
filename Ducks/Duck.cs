using System;

namespace BusterWood.Ducks
{
    public enum MissingMethods
    {
        /// <summary>Calling Duck.Cast(...) will throw a <see cref="InvalidCastException"/> if the target object does not fully implement the interface</summary>
        /// <remarks>This is the default behaviour of all Duck.Cast() methods</remarks>
        InvalidCast,

        /// <summary>Calling Duck.Cast(...) always return a proxy, but an <see cref="NotImplementedException"/> will be thrown by any interface methods not present on the target object</summary>
        /// <remarks>Useful for mocking interfaces for unit testing</remarks>
        NotImplemented
    }

    public static class Duck
    {
        /// <summary>Tries to cast the a delegate to a interface with a single method</summary>
        /// <param name="from">The delegate to cast from</param>
        /// <typeparam name="T">The type of the interface to cast to</typeparam>
        public static T Cast<T>(Delegate from, MissingMethods missingMethods = MissingMethods.InvalidCast) where T : class => (T)Cast(from, typeof(T), missingMethods);

        /// <summary>Tries to cast the a delegate to a interface with a single method</summary>
        /// <param name="from">The delegate to cast from</param>
        /// <param name="to">The type of the interface to cast to</param>
        public static object Cast(Delegate from, Type to, MissingMethods missingMethods = MissingMethods.InvalidCast) => Delegates.Cast(from, to, missingMethods);

        /// <summary>Tries to cast the static methods of a type to an interface</summary>
        /// <param name="from">The type containing static methods</param>
        /// <typeparam name="T">The type of the interface to cast to</typeparam>
        public static T Cast<T>(Type from, MissingMethods missingMethods = MissingMethods.InvalidCast) where T : class => (T)Cast(from, typeof(T), missingMethods);

        /// <summary>Tries to cast the static methods of a type to an interface</summary>
        /// <param name="from">The type containing static methods</param>
        /// <param name="to">The type of the interface to cast to</param>
        public static object Cast(Type from, Type to, MissingMethods missingMethods = MissingMethods.InvalidCast) => Static.Cast(from, to, missingMethods);

        /// <summary>Tries to cast an instance to an interface</summary>
        /// <param name="from">The object to cast from</param>
        /// <typeparam name="T">The type of the interface to cast to</typeparam>
        public static T Cast<T>(object from, MissingMethods missingMethods = MissingMethods.InvalidCast) where T : class
        {
            var t = from as T;
            return t != null ? t : (T)Cast(from, typeof(T), missingMethods);
        }

        /// <summary>Tries to cast an object an interface</summary>
        /// <param name="from">The object to cast, which can be a type (for static casting), a delegate, or an instance of any object or struct</param>
        /// <param name="to">The type of the interface to cast to</param>
        /// <remarks>This methods supports static casts of types, casts of delegates, casts of objects, and re-casting existing ducks</remarks>
        public static object Cast(object from, Type to, MissingMethods missingMethods = MissingMethods.InvalidCast)
        {
            var duck = from as IDuck;
            if (duck != null)
                from = duck.Unwrap();

            if (from is Type) // static cast
                return Cast((Type)from, to); 

            if (from is Delegate) // delegate cast
                return Cast((Delegate)from, to);    
            
            // else instance cast try
            return Instance.Cast(from, to, missingMethods);
        }

    }
}
