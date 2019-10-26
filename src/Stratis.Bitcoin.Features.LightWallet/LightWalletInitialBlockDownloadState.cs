using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.LightWallet
{
    /// <summary>
    /// Dummy IBD state provider for cases when we don't have a consensus. Always assumes that we're not in IBD.
    /// </summary>
    public class LightWalletInitialBlockDownloadState : IInitialBlockDownloadState
    {
        private readonly bool isInInitialBlockDownload;

        public LightWalletInitialBlockDownloadState()
        {
            this.isInInitialBlockDownload = false;
        }

        /// <inheritdoc />
        public bool IsInitialBlockDownload()
        {
            return this.isInInitialBlockDownload;
        }
    }
}
