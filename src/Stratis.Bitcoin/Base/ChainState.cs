using System;
using NBitcoin;

namespace Stratis.Bitcoin.Base
{
    /// <summary>
    /// Chain state holds various information related to the status of the chain and its validation.
    /// </summary>
    public interface IChainState
    {
        /// <summary>ChainBehaviors sharing this state will not broadcast headers which are above <see cref="ConsensusTip"/>.</summary>
        ChainedHeader ConsensusTip { get; set; }

        /// <summary>The highest stored block in the repository or <c>null</c> if block store feature is not enabled.</summary>
        ChainedHeader BlockStoreTip { get; set; }

        /// <summary>Indicates whether consensus tip is equal to the tip of the most advanced peer node is connected to.</summary>
        bool IsAtBestChainTip { get; set; }

        /// <summary>Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.</summary>
        /// <remarks>TODO: This should be removed once consensus options are part of network.</remarks>
        uint MaxReorgLength { get; set; }
    }

    /// <summary>
    /// Chain state holds various information related to the status of the chain and its validation.
    /// The data are provided by different components and the chaine state is a mechanism that allows
    /// these components to share that data without creating extra dependencies.
    /// </summary>
    /// TODO this class should be removed since consensus and block store are moved or about to be moved to base feature
    public class ChainState : IChainState
    {
        /// <inheritdoc />
        public ChainedHeader ConsensusTip { get; set; }

        /// <inheritdoc />
        public ChainedHeader BlockStoreTip { get; set; }

        /// <inheritdoc />
        public bool IsAtBestChainTip { get; set; }

        /// <summary>Maximal length of reorganization that the node is willing to accept, or 0 to disable long reorganization protection.</summary>
        /// <remarks>TODO: This should be removed once consensus options are part of network.</remarks>
        public uint MaxReorgLength { get; set; }
    }
}
