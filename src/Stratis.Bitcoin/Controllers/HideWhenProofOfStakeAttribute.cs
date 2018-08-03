using System;

namespace Stratis.Bitcoin.Controllers
{
    /// <summary>
    /// Swagger Documentation filter for removing mining related methods from API documentation.
    /// </summary>
    public class HideWhenProofOfStakeAttribute : Attribute
    {
    }
}
