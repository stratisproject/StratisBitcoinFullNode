﻿#if WIN
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin
{
    public class RNGCryptoServiceProviderRandom : IRandom
    {
        readonly RNGCryptoServiceProvider _Instance;
        public RNGCryptoServiceProviderRandom()
        {
            _Instance = new RNGCryptoServiceProvider();
        }
        #region IRandom Members

        public void GetBytes(byte[] output)
        {
            _Instance.GetBytes(output);
        }

        #endregion
    }

    public partial class RandomUtils
    {
        static RandomUtils()
        {
            //Thread safe http://msdn.microsoft.com/en-us/library/system.security.cryptography.rngcryptoserviceprovider(v=vs.110).aspx
            Random = new RNGCryptoServiceProviderRandom();
            AddEntropy(Guid.NewGuid().ToByteArray());
        }
    }
}
#endif

#if !WIN && !NODEFAULTRNG
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
#endif

#if DEBUG && NODEFAULTRNG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin
{
    public partial class RandomUtils
    {
        static RandomUtils()
        {
            Random = new UnsecureRandom();
        }
    }
}
#endif