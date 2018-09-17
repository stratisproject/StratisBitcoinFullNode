using System;

namespace NBitcoin
{
    /// <summary>
    /// Enumeration classes that enable all the rich features of an object-oriented language
    /// <seealso cref="https://docs.microsoft.com/en-us/dotnet/standard/microservices-architecture/microservice-ddd-cqrs-patterns/enumeration-classes-over-enum-types"/>
    /// </summary
    public abstract class Enumeration : IComparable
    {
        public string Name { get; private set; }

        public int Id { get; private set; }

        protected Enumeration() { }

        protected Enumeration(int id, string name)
        {
            this.Id = id;
            this.Name = name;
        }

        public override string ToString() => this.Name;

        public override bool Equals(object obj)
        {
            if (!(obj is Enumeration otherValue))
                return false;

            var typeMatches = this.GetType().Equals(obj.GetType());
            var valueMatches = this.Id.Equals(otherValue.Id);

            return typeMatches && valueMatches;
        }

        public override int GetHashCode() => this.Id.GetHashCode();

        public int CompareTo(object other) => this.Id.CompareTo(((Enumeration)other).Id);
    }
}
