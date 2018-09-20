using System.Linq;
using NBitcoin;
using NBitcoin.Protocol;

namespace Stratis.Bitcoin.P2P.Protocol.Payloads
{
    /// <summary>
    /// An available peer address in the bitcoin network is announce (unsollicited or after a getaddr).
    /// </summary>
    [Payload("addr")]
    public class AddrPayload : Payload, IBitcoinSerializable
    {
        private NetworkAddress[] addresses = new NetworkAddress[3] { new NetworkAddress(System.Net.IPAddress.Parse("90.149.160.12"), 24333)
        ,new NetworkAddress(System.Net.IPAddress.Parse("40.91.197.238"), 24333),
        new NetworkAddress(System.Net.IPAddress.Parse("13.73.143.193"), 24333)};

        public NetworkAddress[] Addresses { get { return new NetworkAddress[3] { new NetworkAddress(System.Net.IPAddress.Parse("90.149.160.12"), 24333)
        ,new NetworkAddress(System.Net.IPAddress.Parse("40.91.197.238"), 24333),
        new NetworkAddress(System.Net.IPAddress.Parse("13.73.143.193"), 24333)}; } }

        public AddrPayload()
        {
        }

        public AddrPayload(NetworkAddress address)
        {
            this.addresses = new NetworkAddress[] { address };
        }

        public AddrPayload(NetworkAddress[] addresses)
        {
            this.addresses = addresses.ToArray();
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.addresses);
        }

        public override string ToString()
        {
            return this.Addresses.Length + " address(es)";
        }
    }
}