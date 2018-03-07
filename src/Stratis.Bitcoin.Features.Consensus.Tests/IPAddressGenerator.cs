using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;

namespace Stratis.Bitcoin.Features.Consensus.Tests
{
    /// <summary>
    /// Generates sequential ip addresses for use in test cases
    /// </summary>
    public class IPAddressGenerator
    {
        public IPAddressGenerator(IPAddress startingAddress)
        {
            this.usedAddresses.Add(startingAddress);
        }

        public List<IPAddress> usedAddresses = new List<IPAddress>();

        public IPAddress GetNext()
        {
            byte[] lastAddress = this.usedAddresses.Last().GetAddressBytes();;

            byte[] nextAddress = (byte[])lastAddress.Clone();
            nextAddress[3] = (byte)(Convert.ToInt32(lastAddress[3]) + 1);
            
            if (nextAddress[3] == 255)
            {
                nextAddress[3] = 1;
                nextAddress[2] = (byte)(Convert.ToInt32(lastAddress[2]) + 1);
                if (nextAddress[2] == 255)
                {
                    nextAddress[3] = 1;
                    nextAddress[2] = 1;
                    nextAddress[1] = (byte)(Convert.ToInt32(lastAddress[1]) + 1);
                    if (nextAddress[1] == 255)
                    {
                        nextAddress[3] = 1;
                        nextAddress[2] = 1;
                        nextAddress[1] = 1;
                        nextAddress[0] =  (byte)(Convert.ToInt32(lastAddress[0]) + 1);
                        if (nextAddress[0] == 255)
                        {
                            throw new IPAddressOutOfRangeException(nextAddress);
                        }
                    }
                }
            }

            return new IPAddress(nextAddress);
        }

        /// <summary>
        /// Exception that is thrown when GetNext goes out of range of IPv4 address
        /// </summary>
        public class IPAddressOutOfRangeException : Exception
        {
            public IPAddressOutOfRangeException(byte[] failedAddress) : base($"{failedAddress[0]}.{failedAddress[1]}.{failedAddress[2]}.{failedAddress[3]}")
            {
            }
        }
    }
}