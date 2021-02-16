using System;
using System.IO;
using NBitcoin;

namespace LedgerWallet
{
    public class TrustedInput
    {
        public TrustedInput(byte[] response)
        {
            var stream = new BitcoinStream(new MemoryStream(response), false);
            ReadWrite(stream);
        }

        void ReadWrite(BitcoinStream stream)
        {
            if(stream.Serializing)
            {
                stream.ReadWrite((byte)0x32);
                stream.ReadWrite(Flags);
            }
            else
            {
                if(stream.Inner.ReadByte() != 0x32)
                    throw new FormatException("Invalid magic version");
                Flags = (byte)stream.Inner.ReadByte();
            }
            stream.ReadWrite(ref _Nonce);

            if(stream.Serializing)
            {
                var txId = OutPoint.Hash;
                stream.ReadWrite(ref txId);
                var index = OutPoint.N;
                stream.ReadWrite(ref index);
            }
            else
            {
                var txId = new uint256();
                stream.ReadWrite(ref txId);
                uint index = 0;
                stream.ReadWrite(ref index);
                OutPoint = new OutPoint(txId, index);
            }

            var amount = stream.Serializing ? (ulong)_Amount.Satoshi : 0;
            stream.ReadWrite(ref amount);
            _Amount = Money.Satoshis(amount);

            _Signature = stream.Serializing ? _Signature : new byte[8];
            stream.ReadWrite(ref _Signature);
        }


        public byte Flags
        {
            get;
            internal set;
        }

        private short _Nonce;
        public short Nonce
        {
            get
            {
                return _Nonce;
            }
        }
        public OutPoint OutPoint { get; private set; }

        private Money _Amount;
        public Money Amount
        {
            get
            {
                return _Amount;
            }
        }

        private byte[] _Signature;
        public byte[] Signature
        {
            get
            {
                return _Signature;
            }
        }

        public byte[] ToBytes()
        {
            var ms = new MemoryStream();
            var stream = new BitcoinStream(ms, true);
            ReadWrite(stream);
            return ms.ToArray();
        }
    }
}
