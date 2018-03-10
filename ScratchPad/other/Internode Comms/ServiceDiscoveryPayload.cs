using System;
using System.Collections.Generic;
using System.Linq;

namespace NBitcoin.Protocol
{
	public class DiscoveryCapsule : IBitcoinSerializable
	{
		public DiscoveryCapsule()
		{
			
		}

		public virtual void ReadWrite(BitcoinStream stream)
		{
			//override
		}
	}

	public class RegistrationCapsule : DiscoveryCapsule, IComparable
	{
		Transaction registrationTransaction;
		PartialMerkleTree registrationTransactionProof;

		public RegistrationCapsule()
		{
			
		}

		public RegistrationCapsule(Block block, Transaction tx)
		{
			this.RegistrationTransaction = tx;
			this.RegistrationTransactionProof = new PartialMerkleTree();
		}

		public RegistrationCapsule(PartialMerkleTree merkleTree, Transaction tx)
		{
			this.RegistrationTransaction = tx;
			this.RegistrationTransactionProof = merkleTree;
		}

		public Transaction RegistrationTransaction {
			get { return registrationTransaction; }
			set { registrationTransaction = value; }
		}

		public PartialMerkleTree RegistrationTransactionProof {
			get { return registrationTransactionProof; }
			set { registrationTransactionProof = value; }
		}

		public override void ReadWrite(BitcoinStream stream)
		{
			stream.ReadWrite(ref registrationTransaction);
			stream.ReadWrite(ref registrationTransactionProof);
		}

		int IComparable.CompareTo(object obj)
		{
			RegistrationCapsule c = (RegistrationCapsule)obj;
			return String.Compare(this.registrationTransaction.GetHash().ToString(),
				c.registrationTransaction.GetHash().ToString());
		}
	}

	[Payload("discovery")]
	public class ServiceDiscoveryPayload : Payload
	{
		private readonly string serviceName;

		private RegistrationCapsule[] capsules;

		public ServiceDiscoveryPayload(string serviceName, RegistrationCapsule[] capsules)
		{
			this.serviceName = serviceName;
			this.capsules = capsules;
		}

		public ServiceDiscoveryPayload()
		{
			
		}

		public RegistrationCapsule[] Capsules {
			get { return this.capsules; }
			set { this.capsules = value; }
		}

		public override void ReadWriteCore(BitcoinStream stream)
		{
			stream.ReadWrite<RegistrationCapsule>(ref capsules);
		}

		public string ServiceName
		{
			get { return "tumblebit"; }
		}

		public override string ToString()
		{
			return $"Service: {this.serviceName}";
		}
	}
}
