namespace Stratis.SmartContracts.CLR
{
    /// <summary>
    /// Generates an sequence of sequential numbers for one-time use.
    /// </summary>
    public class NonceGenerator
    {
        private ulong value;

        /// <summary>
        /// Creates a new <see cref="NonceGenerator"/>.
        /// </summary>
        /// <param name="value">The initial value of the NonceGenerator, defaulting to 0.</param>
        public NonceGenerator(ulong value = 0)
        {
            this.value = value;
        }

        /// <summary>
        /// Returns the next value in the sequence.
        /// </summary>
        public ulong Next => this.value++;

        public override string ToString()
        {
            return this.value.ToString();
        }
    }
}