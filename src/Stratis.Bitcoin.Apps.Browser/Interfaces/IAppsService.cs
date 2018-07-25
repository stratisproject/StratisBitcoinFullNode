using System.Collections.Generic;
using System.Threading.Tasks;
using Stratis.Bitcoin.Apps.Browser.Dto;

namespace Stratis.Bitcoin.Apps.Browser.Interfaces
{
    /// <summary>
    /// Service abstraction that communicates with the AppsController to return StratisApp data.
    /// Implementation is injected into components as required.
    /// </summary>
    public interface IAppsService
    {
        Task<List<StratisApp>> GetApplicationsAsync();
    }
}
