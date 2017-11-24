using System.Collections;

namespace NBitcoin.Protocol.Payloads
{
    public class UTxOutputs : IBitcoinSerializable
    {
        private int chainHeight;
        public int ChainHeight
        {
            get
            {
                return this.chainHeight;
            }
            internal set
            {
                this.chainHeight = value;
            }
        }

        private uint256 chainTipHash;
        public uint256 ChainTipHash
        {
            get
            {
                return this.chainTipHash;
            }
            internal set
            {
                this.chainTipHash = value;
            }
        }

        private VarString bitmap;
        public BitArray Bitmap
        {
            get
            {
                return new BitArray(this.bitmap.ToBytes());
            }
            internal set
            {
                BitArray bits = value;
                byte[] buffer = new BitReader(bits).ToWriter().ToBytes();
                this.bitmap = new VarString(buffer);
            }
        }

        private UTxOut[] outputs;
        public UTxOut[] Outputs
        {
            get
            {
                return this.outputs;
            }
            internal set
            {
                this.outputs = value;
            }
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.chainHeight);
            stream.ReadWrite(ref this.chainTipHash);
            stream.ReadWrite(ref this.bitmap);
            stream.ReadWrite(ref this.outputs);
        }
    }

    public class UTxOut : IBitcoinSerializable
    {
        private uint version;
        public uint Version
        {
            get
            {
                return this.version;
            }
            internal set
            {
                this.version = value;
            }
        }

        private uint height;
        public uint Height
        {
            get
            {
                return this.height;
            }
            internal set
            {
                this.height = value;
            }
        }

        private TxOut txOut;
        public TxOut Output
        {
            get
            {
                return this.txOut;
            }
            internal set
            {
                this.txOut = value;
            }
        }

        public void ReadWrite(BitcoinStream stream)
        {
            stream.ReadWrite(ref this.version);
            stream.ReadWrite(ref this.height);
            stream.ReadWrite(ref this.txOut);
        }
    }
}