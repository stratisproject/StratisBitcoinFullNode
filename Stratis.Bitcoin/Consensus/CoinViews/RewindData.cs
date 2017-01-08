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
			_PreviousBlockHash = previousBlockHash;
		}


		uint256 _PreviousBlockHash;
		public uint256 PreviousBlockHash
		{
			get
			{
				return _PreviousBlockHash;
			}
			set
			{
				_PreviousBlockHash = value;
			}
		}

		List<uint256> _TransactionsToRemove = new List<uint256>();
		public List<uint256> TransactionsToRemove
		{
			get
			{
				return _TransactionsToRemove;
			}
			set
			{
				_TransactionsToRemove = value;
			}
		}

		List<UnspentOutputs> _OutputsToRestore = new List<UnspentOutputs>();
		public List<UnspentOutputs> OutputsToRestore
		{
			get
			{
				return _OutputsToRestore;
			}
			set
			{
				_OutputsToRestore = value;
			}
		}

		public void ReadWrite(BitcoinStream stream)
		{			
			stream.ReadWrite(ref _PreviousBlockHash);
			stream.ReadWrite(ref _TransactionsToRemove);
			stream.ReadWrite(ref _OutputsToRestore);
		}
	}
}
