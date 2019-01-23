namespace Stratis.SmartContracts.RuntimeObserver
{
    /// <summary>
    /// Value type representing an amount of Gas. Use this to avoid confusion with many other ulong values.
    /// </summary>
    public struct Gas
    {
        public Gas(ulong value)
        {
            this.Value = value;
        }

        public static Gas None = (Gas)0;

        public readonly ulong Value;

        /// <summary>
        /// Values of type ulong must be explicitly cast to Gas to ensure developer's intention.
        /// <para>
        /// ulong u = 10000;
        /// Gas g = (Gas)u;
        /// </para>
        /// </summary>
        public static explicit operator Gas(ulong value)
        {
            return new Gas(value);
        }

        public static explicit operator Gas(int value)
        {
            return new Gas((ulong)value);
        }

        /// <summary>
        /// Ensures we can implicitly treat Gas as an ulong.
        /// <para>
        /// Gas g = new Gas(10000);
        /// ulong u = g;
        /// </para>
        /// </summary>
        public static implicit operator ulong(Gas gas)
        {
            return gas.Value;
        }

        public override string ToString()
        {
            return this.Value.ToString();
        }
    }
}