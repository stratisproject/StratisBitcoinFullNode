using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Stratis.Bitcoin.Base.Deployments;
using Stratis.Bitcoin.Features.Consensus;
using Stratis.Bitcoin.Utilities;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SCConsensusValidator : PowConsensusValidator
    {
        /// <summary>Instance logger.</summary>
        private readonly ILogger logger;

        public SCConsensusValidator(
            Network network,
            ICheckpoints checkpoints,
            IDateTimeProvider dateTimeProvider,
            ILoggerFactory loggerFactory)
            : base(network, checkpoints, dateTimeProvider, loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
        }

        protected override void UpdateCoinView(RuleContext context, Transaction transaction)
        {
            // check for presence of special opcodes in txouts
            if (!transaction.Outputs.Any(x => x.ScriptPubKey.ToOps().Any(y => y.IsContractExecution)))
            {
                base.UpdateCoinView(context, transaction);
                return;
            }

            throw new NotImplementedException();

            //foreach (var txOut in transaction.Outputs)
            //{

            //}


            //    // if create 
            //    // instantiate contract, run to update database
            //    // update contract code

            //    // if call
            //    // get values from transaction
            //    // get contract code
            //    // execute and save storage

            //    throw new NotImplementedException();
        }
    }
}
