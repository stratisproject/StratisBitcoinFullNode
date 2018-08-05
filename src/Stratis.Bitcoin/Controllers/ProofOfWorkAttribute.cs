using System;

namespace Stratis.Bitcoin.Controllers
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class ProofOfWorkAttribute : Attribute
    {
    }
}
