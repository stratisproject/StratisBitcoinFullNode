using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Stratis.Bitcoin.Features.Api;
using Stratis.Bitcoin.Features.Miner.Interfaces;
using Stratis.Bitcoin.IntegrationTests.Common;
using Xunit;

namespace Stratis.Bitcoin.IntegrationTests.API
{
    public partial class ApiSpecification
    {
        [Fact]
        public void CanPerformMultipleParallelCallsToTheSameController()
        {
            this.stratisPosApiNode = this.posNodeBuilder.CreateStratisPosNode(this.posNetwork).Start();

            this.stratisPosApiNode.FullNode.NodeService<IPosMinting>(true);
            this.apiUri = this.stratisPosApiNode.FullNode.NodeService<ApiSettings>().ApiUri;

            // With these tests we still need to create the wallets outside of the builder
            this.stratisPosApiNode.FullNode.WalletManager().CreateWallet(WalletPassword, WalletName, WalletPassphrase);

            var indexes = new List<int>();
            for (int i = 0; i < 1024; i++)
                indexes.Add(i);

            var success = new bool[indexes.Count];

            var options = new ParallelOptions { MaxDegreeOfParallelism = 16 };
            Parallel.ForEach(indexes, options, ndx =>
            {
                success[ndx] = this.APICallGetsExpectedResult(ndx);
            });

            // Check that none failed.
            Assert.Equal(0, success.Count(s => !s));
        }

        private bool APICallGetsExpectedResult(int ndx)
        {
            string apiendpoint = $"{GeneralInfoUri}?name={WalletName}";

            // One out of two API calls will be invalid.
            bool fail = (ndx & 1) == 0;

            if (fail)
            {
                // Induce failure by omitting the "name" argument.
                apiendpoint = $"{GeneralInfoUri}";
            }

            HttpResponseMessage response = this.httpClient.GetAsync($"{this.apiUri}{apiendpoint}").GetAwaiter().GetResult();

            // It's only ok to fail when its expected.
            return fail == !response.IsSuccessStatusCode;
        }
    }
}