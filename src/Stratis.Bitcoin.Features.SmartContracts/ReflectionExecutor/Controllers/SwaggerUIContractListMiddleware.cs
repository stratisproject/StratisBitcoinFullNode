using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NBitcoin;
using Stratis.Bitcoin.Interfaces;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.Receipts;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    /// <summary>
    /// Middleware to intercept requests to swagger UI and dynamically add to the list of
    /// available endpoints. Must be registered before UseSwaggerUI is called.
    /// </summary>
    public class SwaggerUIContractListMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ChainIndexer chainIndexer;
        private readonly IBlockStore blockStore;
        private readonly IReceiptRepository receiptRepository;
        private readonly SwaggerUIOptions config;
        private readonly Network network;
        private List<UrlDescriptor> baseUrls;

        private const int MaxRecordsToReturn = 20;

        public SwaggerUIContractListMiddleware(RequestDelegate next,
            ChainIndexer chainIndexer,
            IBlockStore blockStore, 
            IReceiptRepository receiptRepository,
            SwaggerUIOptions options,
            Network network)
        {
            this.next = next;
            this.chainIndexer = chainIndexer;
            this.blockStore = blockStore;
            this.receiptRepository = receiptRepository;
            this.config = options;
            this.network = network;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            string httpMethod = httpContext.Request.Method;
            string path = httpContext.Request.Path.Value;

            // Skip requests that don't match.
            if (httpMethod != "GET" || !Regex.IsMatch(path, $"/{this.config.RoutePrefix}/?index.html"))
            {
                await this.next.Invoke(httpContext);
                return;
            }

            // If we've set no URLs yet, keep a reference to the original config URLs.
            if (this.baseUrls == null)
                this.baseUrls = new List<UrlDescriptor>(this.config.ConfigObject.Urls);

            // Enumerate all headers is fine for this test use case.
            IEnumerable<uint256> blockHashes = this.chainIndexer.EnumerateAfter(this.chainIndexer.Genesis).Select(h => h.HashBlock);

            List<uint256> transactionHashes = this.blockStore.GetBlocks(blockHashes.ToList())
                .SelectMany(block => block.Transactions)
                .Select(t => t.GetHash())
                .ToList();

            IList<Receipt> contractCreationReceipts = this.receiptRepository.RetrieveMany(transactionHashes)
                .Where(r => r != null && r.Success && r.NewContractAddress != null)
                .Take(MaxRecordsToReturn) // TODO increase?
                .ToList();

            var newUrls = new List<UrlDescriptor>(this.baseUrls);

            foreach (Receipt receipt in contractCreationReceipts)
            {
                string contractAddress = receipt.NewContractAddress.ToBase58Address(this.network);

                newUrls.Add(new UrlDescriptor
                {
                    Name = $"Contract {contractAddress}",
                    Url = $"/swagger/contracts/{contractAddress}"
                });
            }

            this.config.ConfigObject.Urls = newUrls;

            await this.next.Invoke(httpContext);
        }
    }
}