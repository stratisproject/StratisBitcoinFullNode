using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.Wallet.Interfaces
{
    public interface IScriptDestinationReader : IScriptAddressReader
    {
        IEnumerable<TxDestination> GetDestinationFromScriptPubKey(Network network, Script script);
    }

    public class ScriptDestinationReader : IScriptAddressReader
    {
        private readonly IScriptAddressReader scriptAddressReader;

        public ScriptDestinationReader(IScriptAddressReader scriptAddressReader)
        {
            this.scriptAddressReader = scriptAddressReader;
        }

        public string GetAddressFromScriptPubKey(Network network, Script script)
        {
            return this.scriptAddressReader.GetAddressFromScriptPubKey(network, script);
        }

        public virtual IEnumerable<TxDestination> GetDestinationFromScriptPubKey(Network network, Script redeemScript)
        {
            string base58 = this.scriptAddressReader.GetAddressFromScriptPubKey(network, redeemScript);

            if (base58 != null)
            {
                TxDestination destination = ScriptDestinationReader.GetDestinationForAddress(base58, network);

                if (destination != null)
                    yield return destination;
            }
        }

        public static TxDestination GetDestinationForAddress(string address, Network network)
        {
            if (address == null)
                return null;

            byte[] decoded = Encoders.Base58Check.DecodeData(address);
            return new KeyId(new uint160(decoded.Skip(network.GetVersionBytes(Base58Type.PUBKEY_ADDRESS, true).Length).ToArray()));
        }
    }
}
