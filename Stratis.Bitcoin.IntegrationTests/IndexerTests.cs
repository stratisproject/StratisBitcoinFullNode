using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.RPC;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests
{
    // Disable for now as not to impact NBitcoin.
    /*
    public class IndexStoreTests
    {
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
    }
    */
}
