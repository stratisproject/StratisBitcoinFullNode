using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IHttpClient
    {
        Task<HttpResponseMessage> PostAsync(Uri uri, HttpContent content);
    }
}
