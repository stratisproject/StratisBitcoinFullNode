using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using Stratis.Bitcoin.Primitives;
using Stratis.Features.FederatedPeg.TargetChain;

namespace Stratis.Features.FederatedPeg.Interfaces
{
    public interface IInputConsolidator
    {
        void StartConsolidation();

        ConsolidationSignatureResult CombineSignatures(Transaction partialTransaction);

        void ProcessBlock(ChainedHeaderBlock chainedHeaderBlock);
    }
}
