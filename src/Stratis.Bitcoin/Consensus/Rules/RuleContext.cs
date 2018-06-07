﻿using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Consensus.Rules
{
    /// <summary>
    /// Context that contains variety of information regarding blocks validation and execution.
    /// </summary>
    public class RuleContext
    {
        private readonly Dictionary<Type, object> objectsBag;

        public NBitcoin.Consensus Consensus { get; set; }

        public DateTimeOffset Time { get; set; }

        public Target NextWorkRequired { get; set; }

        public ValidationContext ValidationContext { get; set; }

        public DeploymentFlags Flags { get; set; }

        public bool CheckMerkleRoot { get; set; }

        public bool CheckPow { get; set; }

        /// <summary>Whether to skip block validation for this block due to either a checkpoint or assumevalid hash set.</summary>
        public bool SkipValidation { get; set; }

        /// <summary>The current tip of the chain that has been validated.</summary>
        public ChainedHeader ConsensusTip { get; set; }

        public int PreviousHeight { get; set; }

        public RuleContext()
        {
            this.objectsBag = new Dictionary<Type, object>();
        }

        public T Item<T>() where T : class
        {
            if (this.objectsBag.TryGetValue(typeof(T), out object objecValue))
            {
                return objecValue as T;
            }

            throw new KeyNotFoundException();
        }

        public void SetItem<T>(T item) where T : class
        {
            if (this.objectsBag.TryAdd(typeof(T), item))
            {
                return;
            }

            throw new KeyNotFoundException();
        }

        public RuleContext(ValidationContext validationContext, NBitcoin.Consensus consensus, ChainedHeader consensusTip) : base()
        {
            Guard.NotNull(validationContext, nameof(validationContext));
            Guard.NotNull(consensus, nameof(consensus));

            this.ValidationContext = validationContext;
            this.Consensus = consensus;
            this.ConsensusTip = consensusTip;
            this.PreviousHeight = consensusTip.Height;

            // TODO: adding flags to determine the flow of logic is not ideal
            // a re-factor is in debate on moving to a consensus rules engine
            // this will remove the need for flags as validation will only use 
            // the required rules (i.e if the check pow rule will be omitted form the flow)
            this.CheckPow = true;
            this.CheckMerkleRoot = true;
        }
    }
}