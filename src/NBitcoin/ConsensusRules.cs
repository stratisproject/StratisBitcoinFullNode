using System;
using System.Collections.Generic;
using NBitcoin.Rules;

namespace NBitcoin
{
    public class ConsensusRules
    {
        /// <inheritdoc />
        public List<Type> HeaderValidationRules { get; set; }

        /// <inheritdoc />
        public List<Type> IntegrityValidationRules { get; set; }

        /// <inheritdoc />
        public List<Type> PartialValidationRules { get; set; }

        /// <inheritdoc />
        public List<Type> FullValidationRules { get; set; }

        public ConsensusRules()
        {
            this.HeaderValidationRules = new List<Type>();
            this.IntegrityValidationRules = new List<Type>();
            this.PartialValidationRules = new List<Type>();
            this.FullValidationRules = new List<Type>();
        }

        /// <summary>
        /// Register a rule of type <see cref="IConsensusRuleBase"/>.
        /// </summary>
        public ConsensusRules Register<T>() where T : IConsensusRuleBase
        {
            if (typeof(IHeaderValidationConsensusRule).IsAssignableFrom(typeof(T)))
            {
                this.HeaderValidationRules.Add(typeof(T));
                return this;
            }

            if (typeof(IIntegrityValidationConsensusRule).IsAssignableFrom(typeof(T)))
            {
                this.IntegrityValidationRules.Add(typeof(T));
                return this;
            }

            if (typeof(IPartialValidationConsensusRule).IsAssignableFrom(typeof(T)))
            {
                this.PartialValidationRules.Add(typeof(T));
                return this;
            }

            if (typeof(IFullValidationConsensusRule).IsAssignableFrom(typeof(T)))
            {
                this.FullValidationRules.Add(typeof(T));
                return this;
            }

            throw new InvalidOperationException();
        }
    }
}
