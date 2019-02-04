using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.SmartContracts.Networks
{
    public class SignedContractsPoARegTest : SmartContractsPoARegTest
    {
        public Key SigningContractPrivKey { get;}

        public PubKey SigningContractPubKey { get;}

        public SignedContractsPoARegTest()
        {
            this.SigningContractPrivKey = new Mnemonic("lava frown leave wedding virtual ghost sibling able mammal liar wide wisdom").DeriveExtKey().PrivateKey;
            this.SigningContractPubKey = this.SigningContractPrivKey.PubKey;
        }
    }
}
