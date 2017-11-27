﻿using System.Linq;
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
        private NetworkAddress[] addr_list = new NetworkAddress[0];
        public NetworkAddress[] Addresses { get { return this.addr_list; } }

        public AddrPayload()
        {
        }

        public AddrPayload(NetworkAddress address)
        {
            this.addr_list = new NetworkAddress[] { address };
        }

        public AddrPayload(NetworkAddress[] addresses)
        {
            this.addr_list = addresses.ToArray();
        }

        public override void ReadWriteCore(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.addr_list);
        }

        public override string ToString()
        {
            return this.Addresses.Length + " address(es)";
        }
    }
}