using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;
using NBitcoin;

namespace Stratis.Benchmark
{
    public class SigningAndKeyDerivation
    {
        public SigningAndKeyDerivation()
        {

        }

        [Benchmark]
        public void PrivateKeyGeneration()
        {
            for (int i = 0; i < 10_000; i++)
            {
                new Key();
            }
        }
    }
}
