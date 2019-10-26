using NBitcoin;

namespace Stratis.Bitcoin.Interfaces
{
    /// <summary>
    /// A reader for extracting an address from a Script
    /// </summary>
    public interface IScriptAddressReader
    {
        /// <summary>
        /// Extracts an address from a given Script, if available. Otherwise returns <see cref="string.Empty"/>
        /// </summary>
        /// <param name="network"></param>
        /// <param name="script"></param>
        /// <returns></returns>
        string GetAddressFromScriptPubKey(Network network, Script script);
    }
}
