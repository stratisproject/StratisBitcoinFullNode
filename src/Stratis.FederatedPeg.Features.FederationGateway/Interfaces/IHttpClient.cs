using System;
using System.Threading.Tasks;
using System.Net.Http;

namespace Stratis.FederatedPeg.Features.FederationGateway.Interfaces
{
    public interface IHttpClient
    {
        Task<HttpResponseMessage> PostAsync(Uri uri, HttpContent content);
    }
}
