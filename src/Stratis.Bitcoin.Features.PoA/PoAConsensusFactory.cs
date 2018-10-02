using System;
using System.Collections.Generic;
using System.Text;
using NBitcoin;

namespace Stratis.Bitcoin.Features.PoA
{
    public class PoAConsensusFactory : ConsensusFactory
    {
        public PoAConsensusFactory()
            : base()
        {
        }

        /// <inheritdoc />
        public override Block CreateBlock()
        {
            // TODO POA create PoA block
            return new Block(this.CreateBlockHeader());
        }

        /// <inheritdoc />
        public override BlockHeader CreateBlockHeader()
        {
            // TODO POA create PoA block header
            return new BlockHeader();
        }
    }
}
