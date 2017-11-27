﻿using NBitcoin;

namespace Stratis.Bitcoin.Base.Deployments
{
    public class DeploymentFlags
    {
        public Transaction.LockTimeFlags LockTimeFlags { get; set; }

        public bool EnforceBIP30 { get; set; }

        public bool EnforceBIP34 { get; set; }

        public ScriptVerify ScriptFlags { get; set; }

        public DeploymentFlags()
        {
        }

        public DeploymentFlags(ChainedBlock nextBlock, ThresholdState[] prevBlockStates, Consensus chainparams, ConcurrentChain chain)
        {
            // Do not allow blocks that contain transactions which 'overwrite' older transactions,
            // unless those are already completely spent.
            // If such overwrites are allowed, coinbases and transactions depending upon those
            // can be duplicated to remove the ability to spend the first instance -- even after
            // being sent to another address.
            // See BIP30 and http://r6.ca/blog/20120206T005236Z.html for more information.
            // This logic is not necessary for memory pool transactions, as AcceptToMemoryPool
            // already refuses previously-known transaction ids entirely.
            // This rule was originally applied to all blocks with a timestamp after March 15, 2012, 0:00 UTC.
            // Now that the whole chain is irreversibly beyond that time it is applied to all blocks except the
            // two in the chain that violate it. This prevents exploiting the issue against nodes during their
            // initial block download.
            this.EnforceBIP30 = (nextBlock.HashBlock == null) // Enforce on CreateNewBlock invocations which don't have a hash.
                || !((nextBlock.Height == 91842 && nextBlock.HashBlock == new uint256("00000000000a4d0a398161ffc163c503763b1f4360639393e0e4c8e300e0caec"))
                || (nextBlock.Height == 91880 && nextBlock.HashBlock == new uint256("00000000000743f190a18c5577a3c2d2a1f610ae9601ac046a38084ccb7cd721")));

            // Once BIP34 activated it was not possible to create new duplicate coinbases and thus other than starting
            // with the 2 existing duplicate coinbase pairs, not possible to create overwriting txs.  But by the
            // time BIP34 activated, in each of the existing pairs the duplicate coinbase had overwritten the first
            // before the first had been spent.  Since those coinbases are sufficiently buried its no longer possible to create further
            // duplicate transactions descending from the known pairs either.
            // If we're on the known chain at height greater than where BIP34 activated, we can save the db accesses needed for the BIP30 check.
            ChainedBlock bip34HeightChainedBlock = chain.GetBlock(chainparams.BuriedDeployments[BuriedDeployments.BIP34]);

            //Only continue to enforce if we're below BIP34 activation height or the block hash at that height doesn't correspond.
            this.EnforceBIP30 = this.EnforceBIP30 && ((bip34HeightChainedBlock == null) || !(bip34HeightChainedBlock.HashBlock == chainparams.BIP34Hash));

            // BIP16 didn't become active until Apr 1 2012.
            var nBIP16SwitchTime = Utils.UnixTimeToDateTime(1333238400);
            bool fStrictPayToScriptHash = (nextBlock.Header.BlockTime >= nBIP16SwitchTime);

            this.ScriptFlags = fStrictPayToScriptHash ? ScriptVerify.P2SH : ScriptVerify.None;

            // Start enforcing the DERSIG (BIP66) rule.
            if (nextBlock.Height >= chainparams.BuriedDeployments[BuriedDeployments.BIP66])
            {
                this.ScriptFlags |= ScriptVerify.DerSig;
            }

            // Start enforcing CHECKLOCKTIMEVERIFY, (BIP65) for block.nVersion=4
            // blocks, when 75% of the network has upgraded.
            if (nextBlock.Height >= chainparams.BuriedDeployments[BuriedDeployments.BIP65])
            {
                this.ScriptFlags |= ScriptVerify.CheckLockTimeVerify;
            }

            // Start enforcing BIP68 (sequence locks), BIP112 (CHECKSEQUENCEVERIFY) and BIP113 (Median Time Past) using versionbits logic.
            if (prevBlockStates[(int)BIP9Deployments.CSV] == ThresholdState.Active)
            {
                this.ScriptFlags |= ScriptVerify.CheckSequenceVerify;
                this.LockTimeFlags |= Transaction.LockTimeFlags.VerifySequence;
                this.LockTimeFlags |= Transaction.LockTimeFlags.MedianTimePast;
            }

            // Start enforcing WITNESS rules using versionbits logic.
            if (prevBlockStates[(int)BIP9Deployments.Segwit] == ThresholdState.Active)
            {
                this.ScriptFlags |= ScriptVerify.Witness;
            }

            // Enforce block.nVersion=2 rule that the coinbase starts with serialized block height
            if (nextBlock.Height >= chainparams.BuriedDeployments[BuriedDeployments.BIP34])
            {
                this.EnforceBIP34 = true;
            }
        }
    }
}
