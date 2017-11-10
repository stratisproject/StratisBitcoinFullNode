using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;
using Newtonsoft.Json;
using Stratis.Bitcoin.Configuration;
using Stratis.Bitcoin.Features.RPC;

namespace Stratis.Bitcoin.Cli
{
    class Program
    {
        /// <summary>
        /// The expected sequence of arguments:
        ///    1) [network-name] [rpc-command] [rpc-params].
        /// OR
        ///    2) [network-name] [api-controller "/" api-command] [api-params].
        /// </summary>
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length == 0 || args.Contains("-help") || args.Contains("--help"))
                {
                    var builder = new StringBuilder();
                    builder.AppendLine("Usage:");
                    // TODO: Shouldn't this be dotnet run instead of dotnet exec?
                    builder.AppendLine(" dotnet exec <Stratis.Bitcoin.Cli/Stratis.Bitcoin.Cli.dll> [network-name] [command] [arguments]");
                    builder.AppendLine();
                    builder.AppendLine("Command line arguments:");
                    builder.AppendLine();
                    builder.AppendLine($"[network-name]            Name of the network - e.g. \"stratis\".");
                    builder.AppendLine($"[command]                 Name of RPC method or API <controller>/<method>.");
                    builder.AppendLine($"[arguments]               Argument by position (RPC) or Name = Value pairs (API).");
                    Console.WriteLine(builder);
                    return;
                }
            
                // hack until static flags are removed
                string blockchain = "bitcoin";
                if (args.Any(a => a.Contains("stratis")))
                {
                    blockchain = "stratis";
                    var s = Network.StratisMain;
                    var st = Network.StratisTest;
                }

                // The first argument is the network name
                var network = Network.GetNetwork(args.First());

                // API calls require both the contoller name and the method name separated by "/".
                // If this is not an API call then assume it is an RPC call.
                if (!args.ElementAt(1).Contains("/"))
                {
                    // Process RPC call
                    try
                    {
                        NodeSettings nodeSettings = NodeSettings.FromArguments(args, blockchain, network);
                        var rpcSettings = new RpcSettings();
                        rpcSettings.Load(nodeSettings);

                        // Find the binding to 127.0.0.1 or the first available. The logic in RPC settings ensures there will be at least 1.
                        System.Net.IPEndPoint nodeEndPoint = rpcSettings.Bind.FirstOrDefault(b => b.Address.ToString() == "127.0.0.1") ?? rpcSettings.Bind[0];

                        // Initilize the RPC client with the configured or passed userid, password and endpoint
                        RPCClient rpc = new RPCClient($"{rpcSettings.RpcUser}:{rpcSettings.RpcPassword}", new Uri($"http://{nodeEndPoint}"));

                        // Execute the RPC command
                        RPCResponse response = rpc.SendCommand(args.ElementAt(1), args.Skip(2).ToArray());

                        // Return the result as a string to the console
                        Console.WriteLine(response.ResultString);
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine(err.Message);
                    }
                }
                else
                {
                    // Process API call

                    var ApiURI = "http://localhost:37220/";

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Accept.Clear();
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        var url = $"{ApiURI}api/{args.ElementAt(1)}?{string.Join("&", args.Skip(2))}";
                        try
                        {
                            var response = client.GetStringAsync(url).GetAwaiter().GetResult();
                            // Attempt to format the response
                            try
                            {
                                response = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<object>(response), Formatting.Indented);
                            }
                            finally { }

                            // Return the result as a string to the console
                            Console.WriteLine(response);
                        }
                        catch (Exception err)
                        {
                            Console.WriteLine(err.Message);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                // Report any errors to the console
                Console.WriteLine(err.ToString());
            }
        }
    }
}
