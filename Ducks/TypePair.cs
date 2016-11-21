using System;

namespace Ducks
{

    struct TypePair : IEquatable<TypePair>
    {
        public readonly Type From;
        public readonly Type To;

        public TypePair(Type from, Type to)
        {
            if (from == null)
                throw new ArgumentNullException(nameof(from));
            if (to == null)
                throw new ArgumentNullException(nameof(to));
            From = from;
            To = to;
        }

        public bool Equals(TypePair other) => Equals(From, other.From) && Equals(To, other.To);

        public override bool Equals(object obj) => obj is TypePair ? Equals((TypePair)obj) : false;

        public override int GetHashCode() => From == null ? 0 : From.GetHashCode() + To.GetHashCode();
    }
}
