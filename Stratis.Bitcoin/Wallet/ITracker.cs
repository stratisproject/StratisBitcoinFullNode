using System;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Wallet
{
    public interface ITracker
    {
        /// <summary>
        /// Initializes the tracker.
        /// </summary>
        /// <returns></returns>
        void Initialize();
    }
}
