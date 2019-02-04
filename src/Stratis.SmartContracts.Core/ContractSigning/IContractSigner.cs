using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;

namespace Stratis.SmartContracts.Core.ContractSigning
{
    public interface IContractSigner
    {
        byte[] Sign(Key privKey, byte[] contractCode);

        bool Verify(PubKey pubKey, byte[] contractCode, byte[] signature);
    }
}
