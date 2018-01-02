using HashLib;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Stratis.SmartContracts.Hashing
{
    public static class HashHelper
    {
        public static byte[] Keccak256(byte[] input)
        {
            return HashFactory.Crypto.SHA3.CreateKeccak256().ComputeBytes(input).GetBytes();
        }

        /// <summary>
        /// TODO: Concrete rules around byte size here
        /// </summary>
        /// <param name="address"></param>
        /// <param name="nonce"></param>
        /// <returns></returns>
        public static byte[] NewContractAddress(byte[] address, byte[] nonce)
        {
            return Keccak256(address.Concat(nonce).ToArray()).Skip(12).ToArray();
        }
    }
}
