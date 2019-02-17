using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public interface ISignedCodePubKeyHolder
    {
        PubKey SigningContractPubKey { get; }
    }
}
