using System;
using System.Collections.Generic;
using NBitcoin.Rules;

namespace NBitcoin
{
    public class ConsensusRules
    {
        /// <inheritdoc />
        public List<Type> IntegrityValidationRules { get; set; }

        /// <inheritdoc />
        public List<Type> HeaderValidationRules { get; set; }

        /// <inheritdoc />
        public List<Type> PartialValidationRules { get; set; }

        /// <inheritdoc />
        public List<Type> FullValidationRules { get; set; }

        public ConsensusRules()
        {
            this.IntegrityValidationRules = new List<Type>();
            this.HeaderValidationRules = new List<Type>();
            this.PartialValidationRules = new List<Type>();
            this.FullValidationRules = new List<Type>();
        }
    }
}
