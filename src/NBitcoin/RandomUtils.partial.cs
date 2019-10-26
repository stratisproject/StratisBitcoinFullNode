using System;
using System.Security.Cryptography;

namespace NBitcoin
{
    public class RandomNumberGeneratorRandom : IRandom
    {
        private readonly RandomNumberGenerator _Instance;
        public RandomNumberGeneratorRandom()
        {
            this._Instance = RandomNumberGenerator.Create();
        }
#region IRandom Members

        public void GetBytes(byte[] output)
        {
            this._Instance.GetBytes(output);
        }

#endregion
    }

    public partial class RandomUtils
    {
        static RandomUtils()
        {
            //Thread safe http://msdn.microsoft.com/en-us/library/system.security.cryptography.rngcryptoserviceprovider(v=vs.110).aspx
            Random = new RandomNumberGeneratorRandom();
            AddEntropy(Guid.NewGuid().ToByteArray());
        }
    }
}
