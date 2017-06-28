using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stratis.Bitcoin.Consensus
{
    public class RewindData : IBitcoinSerializable
    {
		public RewindData()
		{

		}

		public RewindData(uint256 previousBlockHash)
		{
            this._PreviousBlockHash = previousBlockHash;
		}


		uint256 _PreviousBlockHash;
		public uint256 PreviousBlockHash
		{
			get
			{
				return this._PreviousBlockHash;
			}
			set
			{
                this._PreviousBlockHash = value;
			}
		}

		List<uint256> _TransactionsToRemove = new List<uint256>();
		public List<uint256> TransactionsToRemove
		{
			get
			{
				return this._TransactionsToRemove;
			}
			set
			{
                this._TransactionsToRemove = value;
			}
		}

		List<UnspentOutputs> _OutputsToRestore = new List<UnspentOutputs>();
		public List<UnspentOutputs> OutputsToRestore
		{
			get
			{
				return this._OutputsToRestore;
			}
			set
			{
                this._OutputsToRestore = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{			
			stream.ReadWrite(ref this._PreviousBlockHash);
			stream.ReadWrite(ref this._TransactionsToRemove);
			stream.ReadWrite(ref this._OutputsToRestore);
		}
	}
}
