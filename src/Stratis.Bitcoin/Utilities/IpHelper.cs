using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using NBitcoin;

namespace Stratis.Bitcoin.Utilities
{
    public static class IpHelper
    {
        // Don't re-use ports.
        private static HashSet<uint> usedPorts = new HashSet<uint>();

        /// <summary>
        /// Find ports that are free to use.
        /// </summary>
        /// <param name="ports">A list of ports to checked or fill/replace as necessary.</param>
        public static void FindPorts(int[] ports)
        {
            int i = 0;
            lock (usedPorts)
            {
                while (i < ports.Length)
                {
                    uint port = RandomUtils.GetUInt32() % 4000;
                    port = port + 10000;
                    if (usedPorts.Contains(port))
                        continue;

                    try
                    {
                        var l = new TcpListener(IPAddress.Loopback, (int)port);
                        l.Start();
                        l.Stop();
                        ports[i] = (int)port;
                        usedPorts.Add(port);
                        i++;
                    }
                    catch (SocketException)
                    {
                    }
                }
            }
        }
    }
}
