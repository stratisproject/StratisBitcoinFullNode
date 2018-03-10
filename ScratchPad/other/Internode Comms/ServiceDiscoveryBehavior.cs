using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NBitcoin.Protocol.Behaviors;
using BreezeCommon;
using NBitcoin;

namespace Stratis.MasterNode.Features.InterNodeComms
{
	//This service discovery bahavior currently supports only 'tumblebit',  However
	//it would be easy to use the ServiceDiscoveryPayload.ServiceName property to
	//implement routing back to a dictionary of stores to make this fully generic.
    public class ServiceDiscoveryBehavior : NodeBehavior
    {
        private Timer BroadcastTimer = null;
	    private List<RegistrationCapsule> capsules;
        private RegistrationStore store;

		public ServiceDiscoveryBehavior(List<RegistrationCapsule> capsules, RegistrationStore store)
		{
			this.capsules = capsules;
            this.store = store;
		}

        public override object Clone()
        {
            return new ServiceDiscoveryBehavior(new List<RegistrationCapsule>(this.capsules), this.store);
        }

        protected override void AttachCore()
        {
            this.BroadcastTimer = new Timer(o =>
            {
                this.Broadcast();

            }, null, 0, (int)TimeSpan.FromMinutes(1).TotalMilliseconds);

            //tell someone to clean up after us
            this.RegisterDisposable(this.BroadcastTimer);

            this.AttachedNode.StateChanged += AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived += AttachedNode_MessageReceived;
        }

        private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
        {
            if (message.Message.Payload is ServiceDiscoveryPayload)
            {
                var serviceDiscovery = (ServiceDiscoveryPayload)message.Message.Payload;
                if (serviceDiscovery.ServiceName != "tumblebit") return;

                //get list
                var incomingList = new List<RegistrationCapsule>(serviceDiscovery.Capsules);

                //if we are synced ...
                if (AreEquivalent(this.capsules, incomingList))
                {
                    return;
                }

                //...if not, merge back to the store here

                // TODO: As an optimisation, these dictionaries should be member variables so they are reused between messages received
                Dictionary<string, bool> recordIds = new Dictionary<string, bool>();
                Dictionary<string, bool> capsuleIds = new Dictionary<string, bool>();

                // Get list of currently known capsules (maybe some aren't in the store yet)
                foreach (RegistrationCapsule capsule in this.capsules)
                {
                    capsuleIds[capsule.RegistrationTransaction.GetHash().ToString()] = true;
                }

                // Get list of records currently in the store
                foreach (RegistrationRecord record in this.store.GetAll())
                {
                    recordIds[record.RecordTxId] = true;

                    if (!capsuleIds.ContainsKey(record.RecordTxId))
                    {
                        // Make capsule out of record and add to capsule list
                        this.capsules.Add(new RegistrationCapsule(record.RecordTxProof, Transaction.Parse(record.RecordTxHex)));
                    }
                }

                // Check if any in synced list are missing from the store & capsule list and add them

                foreach (RegistrationCapsule capsule in incomingList)
                {
                    if (!recordIds.ContainsKey(capsule.RegistrationTransaction.GetHash().ToString()))
                    {
                        this.store.AddCapsule(capsule, node.Network);
                        recordIds[capsule.RegistrationTransaction.GetHash().ToString()] = true;
                    }

                    if (!capsuleIds.ContainsKey(capsule.RegistrationTransaction.GetHash().ToString()))
                    {
                        this.capsules.Append(capsule);
                        capsuleIds[capsule.RegistrationTransaction.GetHash().ToString()] = true;
                    }
                }

                foreach (RegistrationCapsule capsule in this.capsules)
                {
                    if (!recordIds.ContainsKey(capsule.RegistrationTransaction.GetHash().ToString()))
                    {
                        this.store.AddCapsule(capsule, node.Network);
                        recordIds[capsule.RegistrationTransaction.GetHash().ToString()] = true;
                    }
                }
            }
        }

        void AttachedNode_StateChanged(Node node, NodeState oldState)
        {
			if (node.State == NodeState.Connected)
				this.Broadcast();
        }

        void Broadcast()
        {
            if (this.AttachedNode != null)
            {
                if (this.AttachedNode.State == NodeState.HandShaked)
                {
                    this.AttachedNode.SendMessageAsync(new ServiceDiscoveryPayload("tumblebit", new List<RegistrationCapsule>(this.capsules).ToArray()));
                }
            }
        }

        protected override void DetachCore()
        {
            this.AttachedNode.StateChanged -= this.AttachedNode_StateChanged;
            this.AttachedNode.MessageReceived -= this.AttachedNode_MessageReceived;
        }

	    internal static bool AreEquivalent(List<RegistrationCapsule> list1, List<RegistrationCapsule> list2)
	    {
		    var listOne = list1.ToImmutableSortedSet();
		    var listTwo = list2.ToImmutableSortedSet();
		    return listOne.SequenceEqual(listTwo);
	    }
    }
}
