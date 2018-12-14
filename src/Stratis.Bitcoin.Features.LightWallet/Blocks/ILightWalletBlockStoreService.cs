using System;
using NBitcoin;

namespace Stratis.Bitcoin.Features.LightWallet.Blocks
{
    public interface ILightWalletBlockStoreService : IDisposable
    {
        ChainedHeader PrunedUpToHeaderTip { get; }
        void Start();
    }
}
