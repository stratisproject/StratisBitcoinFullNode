namespace Stratis.Features.SQLiteWalletRepository
{
    internal class ObjectsOfInterest
    {
        private byte[] hashArray;
        private int maxHashArrayLengthLog;
        private uint bitIndexLimiter;

        public ObjectsOfInterest(int MaxHashArrayLengthLog = 26)
        {
            this.maxHashArrayLengthLog = MaxHashArrayLengthLog;
            this.hashArray = new byte[1 << MaxHashArrayLengthLog];
            this.bitIndexLimiter = ((uint)1 << (this.maxHashArrayLengthLog + 3)) - 1;
        }

        private uint GetHashCode(byte[] obj)
        {
            ulong hash = 17;

            foreach (byte objByte in obj)
            {
                hash = (hash << 5) - hash + objByte;
            }

            return (uint)hash;
        }

        public bool MayContain(byte[] obj)
        {
            uint hashArrayBitIndex = this.GetHashCode(obj) & this.bitIndexLimiter;

            return (this.hashArray[hashArrayBitIndex >> 3] & (1 << (int)(hashArrayBitIndex & 7))) != 0;
        }

        public void Add(byte[] obj)
        {
            uint hashArrayBitIndex = this.GetHashCode(obj) & this.bitIndexLimiter;

            this.hashArray[hashArrayBitIndex >> 3] |= (byte)(1 << (int)(hashArrayBitIndex & 7));
        }
    }
}
