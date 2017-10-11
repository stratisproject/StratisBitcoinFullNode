using System.Collections.Generic;
using DBreeze;
using NBitcoin;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.IndexStore;
using Stratis.Bitcoin.Features.RPC;
using Xunit;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class IndexStoreTests
    {     
        private async Task<CoreNode> CreateStratisNodeAsync(NodeBuilder builder)
        {
            return await builder.CreateStratisPowNodeAsync(false, fullNodeBuilder =>
            {
                fullNodeBuilder
                .UseConsensus()
                .UseIndexStore()
                .AddRPC();
            }).ConfigureAwait(false);
        }

        [Fact]
        public async Task CanCreateIndexFromRPCAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var node = await CreateStratisNodeAsync(builder).ConfigureAwait(false);
                builder.StartAll();
                var client = node.CreateRPCClient();
                var response = bool.Parse((string)client.SendCommand("createindex", "Output", false,
                    "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })").Result);

                Assert.True(response);
            }            
        }
        
        [Fact]
        public async Task CanDropIndexFromRPCAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var node = await CreateStratisNodeAsync(builder).ConfigureAwait(false);
                builder.StartAll();
                var client = node.CreateRPCClient();
                bool response1 = bool.Parse((string)client.SendCommand("createindex", "Output", false,
                    "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })").Result);
                bool response2 = bool.Parse((string)client.SendCommand("dropindex", "Output").Result);

                Assert.True(response1);
                Assert.True(response2);
            }
        }
        
        [Fact]
        public async Task CanListIndexesFromRPCAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var node = await CreateStratisNodeAsync(builder).ConfigureAwait(false);
                builder.StartAll();
                var client = node.CreateRPCClient();
                bool response1 = bool.Parse((string)client.SendCommand("createindex", "Output", false,
                    "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })").Result);
                bool response2 = bool.Parse((string)client.SendCommand("createindex", "Script", true,
                    "(t,b,n) => t.Outputs.Select((o, N) => new { Item = o, Index = N }).Where(o => o.Item.ScriptPubKey.GetDestinationAddress(n) != null).Select(o => new object[] { new uint160(o.Item.ScriptPubKey.Hash.ToBytes()), new object[] { t.GetHash(), (uint)o.Index } })").Result);
                var result = client.SendCommand("listindexnames").Result?.ToObject<JArray>();

                Assert.True(response1);
                Assert.True(response2);
                Assert.Equal(2, result?.Count);
                Assert.Equal("Output", (string)result[0]);
                Assert.Equal("Script", (string)result[1]);
            }
        }
        
        [Fact]
        public async Task CanDescribeIndexFromRPCAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var node = await CreateStratisNodeAsync(builder).ConfigureAwait(false);
                builder.StartAll();
                var client = node.CreateRPCClient();
                var expr = "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })";
                bool response = bool.Parse((string)client.SendCommand("createindex", "Output", false, expr).Result);
                string description = (string)client.SendCommand("describeindex", "Output").Result?.ToObject<JArray>()?[0];

                Assert.True(response);
                Assert.Equal("{\"Name\":\"Output\",\"Table\":\"Index_Output\",\"Builder\":\"" + expr + "\",\"Many\":false,\"Uses\":[\"System\",\"System.Linq\",\"System.Linq.Expressions\",\"System.Collections.Generic\",\"NBitcoin\"]}", description);
            }
        }
        
        [Fact]
        public async Task CanRegisterSingleValueIndexFromIndexStoreSettingsAsync()
        {
            using (NodeBuilder builder = await NodeBuilder.CreateAsync().ConfigureAwait(false))
            {
                var node = await builder.CreateStratisPowNodeAsync(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                    .UseConsensus()
                    .UseIndexStore(settings =>
                    {
                        settings.RegisterIndex("Output", "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })", false);
                    })
                    .AddRPC();
                 }).ConfigureAwait(false);

                builder.StartAll();
                
                // Transaction has outputs
                var block = new Block();
                var trans = new Transaction();
                Key key = new Key(); // generate a random private key
                var scriptPubKeyOut = PayToPubkeyHashTemplate.Instance.GenerateScriptPubKey(key.PubKey);
                trans.Outputs.Add(new TxOut(100, scriptPubKeyOut));
                block.Transactions.Add(trans);
                var hash = trans.GetHash().ToBytes();

                // Transaction has inputs (i.PrevOut)
                var block2 = new Block();
                block2.Header.HashPrevBlock = block.GetHash();
                var trans2 = new Transaction();
                trans2.Inputs.Add(new TxIn(new OutPoint(trans, 0)));
                block2.Transactions.Add(trans2);
                var hash2 = trans2.GetHash().ToBytes();

                var repository = node.FullNode.NodeService<IIndexRepository>() as IndexRepository;

                await repository.PutAsync(block.GetHash(), new List<Block> { block, block2 }).ConfigureAwait(false);

                var indexTable = repository.Indexes["Output"].Table;
                var expectedJSON = repository.Indexes["Output"].ToString();

                repository.Dispose();

                using (var engine = new DBreezeEngine(node.FullNode.DataFolder.IndexPath))
                {
                    var transaction = engine.GetTransaction();

                    var indexKeyRow = transaction.Select<string, string>("Common", indexTable);
                    Assert.True(indexKeyRow.Exists && indexKeyRow.Value != null);
                    Assert.Equal(expectedJSON, indexKeyRow.Value);

                    // Block 2 has been indexed?
                    var indexKey = new byte[hash.Length + 4];
                    hash.CopyTo(indexKey, 0);
                    var IndexedRow = transaction.Select<byte[], byte[]>(indexTable, indexKey);
                    Assert.True(IndexedRow.Exists);
                    // Correct value indexed?
                    var compare = new byte[32];
                    hash2.CopyTo(compare, 0);
                    Assert.Equal(compare, IndexedRow.Value);
                }
            }
        }
    }       
}
