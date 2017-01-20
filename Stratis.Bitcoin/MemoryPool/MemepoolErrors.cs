using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stratis.Bitcoin.Consensus;

namespace Stratis.Bitcoin.MemoryPool
{

	public class MempoolErrorException : Exception
	{
		public MempoolErrorException(MemepoolValidationState error) : base(error.Error.Message)
		{
			ValidationState = error;
		}

		public MemepoolValidationState ValidationState
		{
			get;
			private set;
		}
	}

	public class MemepoolError
	{
		public MemepoolError(int code, string messageCode, string message)
		{
			this.Code = code;
			this.MessageCode = messageCode;
			this.Message = message;
		}

		public MemepoolError(int code, string messageCode)
		{
			this.Code = code;
			this.MessageCode = messageCode;
		}

		public MemepoolError(int code)
		{
			this.Code = code;
		}

		public MemepoolError(string message)
		{
			this.Message = message;
		}

		public MemepoolError(ConsensusError consensusError)
		{
			this.ConsensusError = consensusError;
		}

		public int Code { get; set; }
		public string Message { get; set; }
		public string MessageCode { get; set; }

		public ConsensusError ConsensusError { get; set; }
	}

	public class MemepoolErrors
	{
		//  "reject" message codes 
		public const int REJECT_MALFORMED = 0x01;
		public const int REJECT_INVALID = 0x10;
		public const int REJECT_OBSOLETE = 0x11;
		public const int REJECT_DUPLICATE = 0x12;
		public const int REJECT_NONSTANDARD = 0x40;
		public const int REJECT_DUST = 0x41;
		public const int REJECT_INSUFFICIENTFEE = 0x42;
		public const int REJECT_CHECKPOINT = 0x43;
		// Reject codes greater or equal to this can be returned by AcceptToMemPool
		// for transactions, to signal internal conditions. They cannot and should not
		// be sent over the P2P network.
		public const int REJECT_INTERNAL = 0x100;
		// Too high fee. Can not be triggered by P2P transactions 
		public const int REJECT_HIGHFEE = 0x100;
		// Transaction is already known (either in mempool or blockchain) 
		public const int REJECT_ALREADY_KNOWN = 0x101;
		// Transaction conflicts with a transaction already known 
		public const int REJECT_CONFLICT = 0x102;
	}

}
