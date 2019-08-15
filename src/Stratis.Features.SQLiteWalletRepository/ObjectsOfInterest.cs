namespace Stratis.Features.SQLiteWalletRepository
{
    internal class ObjectsOfInterest
    {
        internal int MaxHashArrayLength {get; private set; }

        private byte[] hashArray;

        public ObjectsOfInterest(int MaxHashArrayLength = 1024 * 1024)
        {
            this.hashArray = new byte[MaxHashArrayLength];
        }

        private uint GetHashCode(byte[] obj)
        {
            uint hash = 17;

            foreach (byte objByte in obj)
            {
                hash = hash * 31 + objByte;
            }

            return hash;
        }

        public bool MayContain(byte[] obj)
        {
            int hash = (int)(this.GetHashCode(obj) % (this.hashArray.Length * 8));

            return (this.hashArray[hash / 8] & (1 << (hash % 8))) != 0;
        }

        public void Add(byte[] obj)
        {
            int hash = (int)(this.GetHashCode(obj) % (this.hashArray.Length * 8));

            this.hashArray[hash / 8] |= (byte)(1 << (hash % 8));
        }
    }
}
