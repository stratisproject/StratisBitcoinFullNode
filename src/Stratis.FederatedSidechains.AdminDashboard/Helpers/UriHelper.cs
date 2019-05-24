using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.FederatedSidechains.AdminDashboard.Helpers
{
    public static class UriHelper
    {
        /// <summary>
        /// Build an Uri with specified parameters
        /// </summary>
        public static Uri BuildUri(string endpoint, string path = null, string query = null) => new UriBuilder(endpoint)
        {
            Path = path,
            Query = query
        }.Uri;
    }
}
