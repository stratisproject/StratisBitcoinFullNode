using System.Collections.Generic;
using DBreeze;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Features.IndexStore;
using Stratis.Bitcoin.Features.RPC;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    public class IndexStoreTests
    {
        // Disable for now as not to impact NBitcoin.
        /*
        [Fact]
        public void CanCreateIndexFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node = builder.CreateStratisNode();
                builder.StartAll();
                var client = node.CreateRPCClient();
                var hash = client.GetBestBlockHash();
                bool response = client.CreateIndexAsync("Output", false, 
                    "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })").GetAwaiter().GetResult();

                Assert.True(response);
            }
        }

        [Fact]
        public void CanDropIndexFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node = builder.CreateStratisNode();
                builder.StartAll();
                var client = node.CreateRPCClient();
                var hash = client.GetBestBlockHash();
                bool response1 = client.CreateIndexAsync("Output", false,
                    "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })").GetAwaiter().GetResult();
                bool response2 = client.DropIndex("Output").GetAwaiter().GetResult();

                Assert.True(response1 && response2);
            }
        }

        [Fact]
        public void CanListIndexesFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node = builder.CreateStratisNode();
                builder.StartAll();
                var client = node.CreateRPCClient();
                var hash = client.GetBestBlockHash();
                bool response1 = client.CreateIndexAsync("Output", false,
                    "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })").GetAwaiter().GetResult();
                bool response2 = client.CreateIndexAsync("Script", true,
                    "(t,b,n) => t.Outputs.Where(o => o.ScriptPubKey.GetDestinationAddress(n)!=null).Select((o, N) => new object[] { new uint160(o.ScriptPubKey.Hash.ToBytes()), new object[] { t.GetHash(), (uint)N } })").GetAwaiter().GetResult();
                string[] indexes = client.ListIndexNames();

                Assert.True(response1);
                Assert.True(response2);
                Assert.True(indexes.Length == 2);
                Assert.Equal("Output", indexes[0]);
                Assert.Equal("Script", indexes[1]);
            }
        }

        [Fact]
        public void CanDescribeIndexFromRPC()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node = builder.CreateStratisNode();
                builder.StartAll();
                var client = node.CreateRPCClient();
                var hash = client.GetBestBlockHash();
                bool response = client.CreateIndexAsync("Output", false,
                    "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })").GetAwaiter().GetResult();
                string description = client.DescribeIndex("Output");

                Assert.True(response);
                Assert.Equal("{\"Name\":\"Output\",\"Table\":\"Index_Output\",\"Builder\":\"(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })\",\"Many\":false,\"Uses\":[\"System\",\"System.Linq\",\"System.Linq.Expressions\",\"System.Collections.Generic\",\"NBitcoin\"]}", description);
            }
        }
        */
        [Fact]
        public void CanRegisterSingleValueIndexFromIndexStoreSettings()
        {
            using (NodeBuilder builder = NodeBuilder.Create())
            {
                var node = builder.CreateStratisNode(false, fullNodeBuilder =>
                {
                    fullNodeBuilder
                    .UseConsensus()
                    .UseIndexStore(settings =>
                    {
                        settings.RegisterIndex("Output", "(t,b,n) => t.Inputs.Select((i, N) => new object[] { new object[] { i.PrevOut.Hash, i.PrevOut.N }, t.GetHash() })", false);
                    })
                    .AddRPC();
                 });

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

                repository.PutAsync(block.GetHash(), new List<Block> { block, block2 }).GetAwaiter().GetResult();

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
