using System.Collections.Generic;
using System.Threading.Tasks;
using Stratis.Bitcoin.Apps.Browser.Dto;

namespace Stratis.Bitcoin.Apps.Browser.Interfaces
{
    public interface IAppsService
    {
        Task<List<StratisApp>> GetApplicationsAsync();
    }
}
