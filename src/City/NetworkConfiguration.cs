using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace City
{
    public class NetworkConfiguration
    {
        public string Identifier { get; set; }

        public string Chain { get; set; }

        public string Name { get; set; }

        public int Port { get; set; }

        public int RpcPort { get; set; }

        public int ApiPort { get; set; }

        public int WsPort { get; set; }

    }

    public class NetworkConfigurations
    {
        private NetworkConfiguration[] networks;

        public NetworkConfigurations()
        {
            networks = new NetworkConfiguration[] {

                new NetworkConfiguration() {
                    Identifier = "main",
                    Chain = "city",
                    Name = "City Main",
                    Port = 4333,
                    RpcPort = 4334,
                    ApiPort = 4335,
                    WsPort = 4336
                },


                new NetworkConfiguration() {
                    Identifier = "regtest",
                    Chain = "city",
                    Name = "City RegTest",
                    Port = 14333,
                    RpcPort = 14334,
                    ApiPort = 14335,
                    WsPort = 14336
                },

                new NetworkConfiguration() {
                    Identifier = "testnet",
                    Chain = "city",
                    Name = "City Test",
                    Port = 24333 ,
                    RpcPort = 24334,
                    ApiPort = 24335,
                    WsPort = 24336
                },


            };
            
        }

        public NetworkConfiguration[] GetNetworks()
        {
            return this.networks;
        }

        public NetworkConfiguration GetNetwork(string identifier, string chain)
        {
            return this.networks.FirstOrDefault(n => n.Identifier == identifier && n.Chain == chain);
        }
    }
}
