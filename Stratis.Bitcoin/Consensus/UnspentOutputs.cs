using NBitcoin;
using NBitcoin.BitcoinCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus
{
	public class UnspentOutputs : IBitcoinSerializable
	{
		public UnspentOutputs()
		{

		}
		public UnspentOutputs(uint height, Transaction tx)
		{
            Guard.NotNull(tx, nameof(tx));

            _Outputs = tx.Outputs.ToArray();
			_TransactionId = tx.GetHash();
			_Height = height;
			_Version = tx.Version;
			_IsCoinbase = tx.IsCoinBase;
		}

		public UnspentOutputs(uint256 txId, Coins coins)
		{
			_TransactionId = txId;
			SetCoins(coins);
		}

		private void SetCoins(Coins coins)
		{
			_IsCoinbase = coins.Coinbase;
			_Height = coins.Height;
			_Version = coins.Version;
			_Outputs = new TxOut[coins.Outputs.Count];
			for(uint i = 0; i < _Outputs.Length; i++)
			{
				_Outputs[i] = coins.TryGetOutput(i);
			}
		}

		public UnspentOutputs(UnspentOutputs unspent)
		{
			_TransactionId = unspent.TransactionId;
			_IsCoinbase = unspent.IsCoinbase;
			_Height = unspent.Height;
			_Version = unspent.Version;
			_Outputs = unspent._Outputs.ToArray();
		}

		internal TxOut[] _Outputs;


		private uint256 _TransactionId;
		public uint256 TransactionId
		{
			get
			{
				return _TransactionId;
			}
		}


		private uint _Version;
		public uint Version
		{
			get
			{
				return _Version;
			}
		}

		private bool _IsCoinbase;
		public bool IsCoinbase
		{
			get
			{
				return _IsCoinbase;
			}
		}

		private uint _Height;
		public uint Height
		{
			get
			{
				return _Height;
			}
		}

		public bool IsPrunable
		{
			get
			{
				return _Outputs.All(o => o == null ||
									(o.ScriptPubKey.Length > 0 && o.ScriptPubKey.ToBytes(true)[0] == (byte)OpcodeType.OP_RETURN));
			}
		}

		public bool IsFull
		{
			get
			{
				return _Outputs.All(o => o != null);
			}
		}

		public int UnspentCount
		{
			get
			{
				return _Outputs.Count(o => o != null);
			}
		}

		public bool IsAvailable(uint outputIndex)
		{
			return TryGetOutput(outputIndex) != null;
		}

		public TxOut TryGetOutput(uint outputIndex)
		{
			if(outputIndex >= _Outputs.Length)
				return null;
			return _Outputs[outputIndex];
		}		

		public bool Spend(uint outputIndex)
		{
			if(outputIndex >= _Outputs.Length)
				return false;
			if(_Outputs[outputIndex] == null)
				return false;
			_Outputs[outputIndex] = null;
			return true;
		}

		public void Spend(UnspentOutputs c)
		{
			for(int i = 0; i < _Outputs.Length; i++)
			{
				if(c._Outputs[i] == null)
					_Outputs[i] = null;
			}
		}

		static TxIn CoinbaseTxIn = TxIn.CreateCoinbase(0);
		static TxIn NonCoinbaseTxIn = new TxIn(new OutPoint(uint256.One, 0));
		public Coins ToCoins()
		{
			var coins = new Coins()
			{
				Coinbase = IsCoinbase,
				Height = Height,
				Version = Version,
			};
			foreach(var output in _Outputs)
			{
				coins.Outputs.Add(output == null ? Coins.NullTxOut : output);
			}
			coins.ClearUnspendable();
			return coins;
		}

		public void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref _TransactionId);
			if(stream.Serializing)
			{
				var c = ToCoins();
				stream.ReadWrite(c);
			}
			else
			{
				Coins c = null;
				stream.ReadWrite(ref c);
				SetCoins(c);
			}
		}

		public UnspentOutputs Clone()
		{
			return new UnspentOutputs(this);
		}
	}
}
