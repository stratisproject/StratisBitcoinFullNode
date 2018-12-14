using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;
using NBitcoin;
using NBitcoin.DataEncoders;
using Stratis.SmartContracts.Core;

namespace Stratis.SmartContracts.Tools.Sct
{
    [Command(Description = "Converts a 20-byte hexadecimal representation of a p2pkh address to a base58 string")]
    [HelpOption]
    class Base58Converter
    {
        private static readonly string[] AssemblyNames =
        {
            "Stratis.SmartContracts.Networks",
            "Stratis.Bitcoin.Networks"
        };

        public Base58Converter()
        {
            this.Network = "SmartContractsPoATest";
        }

        [Argument(0, Description = "A 20-byte p2pkh address represented as hexadecimal", Name = "<Address>")]
        [Required]
        public string Address { get; }

        [Option("-network|--network", CommandOptionType.SingleValue, Description = "The name of the network class object. Default is 'SmartContractsPoATest'")]
        public string Network { get; }

        [Option("-prefix|--prefix", CommandOptionType.SingleValue, Description = "The hexadecimal pubkey prefix to use. If defined, will override the -network option.")]
        public string PubKeyPrefix { get; }

        private int OnExecuteAsync(CommandLineApplication app, IConsole console)
        {
            byte[] hex;

            try
            {
                hex = this.Address.HexToByteArray();
            }
            catch (Exception e)
            {
                console.WriteLine("Error parsing hex value {0}", this.Address);
                return 0;
            }

            if (hex.Length != 20)
            {
                console.WriteLine("Address must be 20 bytes long");
                return 0;
            }

            if (!string.IsNullOrWhiteSpace(PubKeyPrefix))
            {
                byte[] versionBytes;
                try
                {
                    versionBytes = this.PubKeyPrefix.HexToByteArray();
                }
                catch (Exception e)
                {
                    console.WriteLine("Error parsing hex value {0}", this.PubKeyPrefix);
                    return 0;
                }

                var base58WithPrefix = Encoders.Base58Check.EncodeData(versionBytes.Concat(hex).ToArray());
                console.Write(base58WithPrefix);
                return 1;
            }

            Network network;
            try
            {
                network = this.ResolveNetwork(this.Network);
            }
            catch (Exception e)
            {
                console.WriteLine("Error resolving network {0} [{1}]", this.Network, e.Message);
                return 0;
            }

            var base58 = new BitcoinPubKeyAddress(new KeyId(hex), network).ToString();
            console.Write(base58);

            return 1;
        }

        private Network ResolveNetwork(string networkTypeName)
        {
            foreach (var assemblyName in AssemblyNames)
            {
                var assembly = Assembly.Load(new AssemblyName(assemblyName));
                var types = assembly.GetTypes();

                var type = types.FirstOrDefault(t => t.Name == networkTypeName);

                if (type == null)
                {
                    continue;
                }

                var network = (Network) Activator.CreateInstance(type);

                return network;
            }

            throw new Exception("Network not found");
        }
    }
}