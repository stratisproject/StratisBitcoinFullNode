using System;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// Swagger Documentation filter for removing staking related methods from API documentation.
    /// </summary>
    public class HideWhenProofOfWorkAttribute : Attribute
    {
    }
}
