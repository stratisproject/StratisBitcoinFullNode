using System;

namespace Stratis.SmartContracts.Core
{
    /// <summary>
    /// Value type to hold smart contract errors
    /// </summary>
    public class ContractErrorMessage
    {
        public string Value { get; }

        public ContractErrorMessage(string val)
        {
            this.Value = val;
        }

        public override string ToString()
        {
            return this.Value;
        }

        public static explicit operator ContractErrorMessage(string value)
        {
            return new ContractErrorMessage(value);
        }

        public static implicit operator string(ContractErrorMessage x)
        {
            if (x == null)
                return null;

            return x.Value;
        }

        public static bool operator ==(ContractErrorMessage obj1, ContractErrorMessage obj2)
        {
            if (object.ReferenceEquals(obj1, null))
                return object.ReferenceEquals(obj2, null);

            return obj1.Equals(obj2);
        }

        public static bool operator !=(ContractErrorMessage obj1, ContractErrorMessage obj2)
        {
            return !(obj1 == obj2);
        }

        public override bool Equals(object obj)
        {
            var message = obj as ContractErrorMessage;
            return message != null &&
                   this.Value == message.Value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(this.Value);
        }
    }
}
