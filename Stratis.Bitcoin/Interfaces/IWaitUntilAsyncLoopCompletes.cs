using Stratis.Bitcoin.Utilities;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Interfaces
{
    public interface IWaitUntilAsyncLoopCompletes
    {
        IAsyncLoopFactory AsyncLoopFactory { get; }
        Task LoopTask { get; }
        void ShutDown();
    }
}