using System;

namespace Stratis.SmartContracts.CLR.Serialization
{
    /// <summary>
    /// The prefix to use when serializing a method parameter. Represents the <see cref="Type"/> of parameter being serialized.
    /// </summary>
    public class Prefix
    {
        public byte Value { get; }

        public Prefix(byte value)
        {
            this.Value = value;
        }

        public Type Type => this.GetType(this.Value);

        public MethodParameterDataType DataType => (MethodParameterDataType) this.Value;

        public int Length { get; } = 1;

        private Type GetType(byte b)
        {
            switch ((MethodParameterDataType)b)
            {
                case MethodParameterDataType.Address:
                    return typeof(Address);
                case MethodParameterDataType.Bool:
                    return typeof(bool);
                case MethodParameterDataType.Byte:
                    return typeof(byte);
                case MethodParameterDataType.Char:
                    return typeof(char);
                case MethodParameterDataType.String:
                    return typeof(string);
                case MethodParameterDataType.Int:
                    return typeof(int);
                case MethodParameterDataType.UInt:
                    return typeof(uint);
                case MethodParameterDataType.Long:
                    return typeof(long);
                case MethodParameterDataType.ULong:
                    return typeof(ulong);
                case MethodParameterDataType.ByteArray:
                    return typeof(byte[]);
                default:
                    throw new ArgumentOutOfRangeException(nameof(b), b, "Unsupported type");
            }
        }

        public static Prefix ForObject(object o)
        {
            byte type = (byte) GetPrimitiveType(o);
            return new Prefix(type);
        }

        private static MethodParameterDataType GetPrimitiveType(object o)
        {
            if (o is bool)
                return MethodParameterDataType.Bool;

            if (o is byte)
                return MethodParameterDataType.Byte;

            if (o is byte[])
                return MethodParameterDataType.ByteArray;

            if (o is char)
                return MethodParameterDataType.Char;

            if (o is string)
                return MethodParameterDataType.String;

            if (o is uint)
                return MethodParameterDataType.UInt;

            if (o is ulong)
                return MethodParameterDataType.ULong;

            if (o is Address)
                return MethodParameterDataType.Address;

            if (o is long)
                return MethodParameterDataType.Long;

            if (o is int)
                return MethodParameterDataType.Int;

            // Any other types are not supported.
            throw new ArgumentOutOfRangeException(nameof(o), o, string.Format("{0} is not supported.", o.GetType().Name));
        }

        public void CopyTo(byte[] result)
        {
            result[0] = this.Value;
        }
    }
}