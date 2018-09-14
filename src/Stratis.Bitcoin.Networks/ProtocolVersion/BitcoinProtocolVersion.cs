namespace Stratis.Bitcoin.Networks
{
    /// <summary>
    /// Class enumeration containing Bitcoin protocol versions, by name and version number (Id).
    /// </summary>
    /// <remarks>
    /// This enumeration class extends <see cref="ProtocolVersion"/>.
    /// </remarks>
    public class BitcoinProtocolVersion : ProtocolVersion
    {
        public BitcoinProtocolVersion() { }

        public BitcoinProtocolVersion(int id, string name) : base(id, name) { }
    }
}
