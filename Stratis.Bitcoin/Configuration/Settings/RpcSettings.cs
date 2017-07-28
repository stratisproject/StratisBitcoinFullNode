using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Stratis.Bitcoin.Configuration.Settings
{
    /// <summary>
    /// Configuration related to RPC interface.
    /// </summary>
    public class RpcSettings
    {
        /// <summary>
        /// Initializes an instance of the object.
        /// </summary>
        public RpcSettings()
        {
            this.Bind = new List<IPEndPoint>();
            this.AllowIp = new List<IPAddress>();
        }

        /// <summary>User name for RPC authorization.</summary>
        public string RpcUser
        {
            get; set;
        }

        /// <summary>Password for RPC authorization.</summary>
        public string RpcPassword
        {
            get; set;
        }

        /// <summary>TCP port for RPC interface.</summary>
        public int RPCPort
        {
            get; set;
        }

        /// <summary>List of network endpoints that the node will listen and provide RPC on.</summary>
        public List<IPEndPoint> Bind
        {
            get; set;
        }

        /// <summary>List of IP addresses that are allowed to connect to RPC interfaces.</summary>
        public List<IPAddress> AllowIp
        {
            get; set;
        }

        /// <summary>Obtains a list of HTTP URLs to RPC interfaces.</summary>
        /// <returns>List of HTTP URLs to RPC interfaces.</returns>
        public string[] GetUrls()
        {
            return this.Bind.Select(b => "http://" + b + "/").ToArray();
        }
    }
}