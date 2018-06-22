using System;
using System.Collections.Generic;
using System.Linq;
using Stratis.Bitcoin.Consensus.Rules;

namespace Stratis.Bitcoin.Features.Consensus.Rules
{
    public static class ConsensusRulesExtension
    {
        /// <summary>
        /// Try to find a consensus rule in the rules collection.
        /// </summary>
        /// <typeparam name="T">The type of rule to find.</typeparam>
        /// <param name="rules">The rules to look in.</param>
        /// <returns>The rule or <c>null</c> if not found in the list.</returns>
        public static T TryFindRule<T>(this IEnumerable<ConsensusRuleDescriptor> rules) where T : ConsensusRule
        {
            return rules.Select(s => s.Rule).OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Find a consensus rule in the rules collection or throw an exception if not found.
        /// </summary>
        /// <typeparam name="T">The type of rule to find.</typeparam>
        /// <param name="rules">The rules to look in.</param>
        /// <returns>The rule or <c>null</c> if not found in the list.</returns>
        public static T FindRule<T>(this IEnumerable<ConsensusRuleDescriptor> rules) where T : ConsensusRule
        {
            var rule = rules.TryFindRule<T>();

            if (rule == null)
            {
                throw new Exception(); // todo:
            }

            return rule;
        }
    }
}