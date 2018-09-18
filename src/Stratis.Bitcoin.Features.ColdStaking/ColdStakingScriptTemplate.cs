using System;
using System.Linq;
using NBitcoin;
using Stratis.Bitcoin.Utilities;
using static NBitcoin.OpcodeType;

namespace Stratis.Bitcoin.Features.ColdStaking
{
    /// <summary>
    /// Script template for the cold staking script.
    /// </summary>
    public class ColdStakingScriptTemplate : ScriptTemplate
    {
        /// <summary>Returns a static instance of this class.</summary>
        public static ColdStakingScriptTemplate Instance { get; } = new ColdStakingScriptTemplate();

        /// <summary>
        /// Returns the transaction type of the cold staking script.
        /// </summary>
        public override TxOutType Type => TxOutType.TX_COLDSTAKE;

        /// <summary>
        /// Extracts the scriptSig parameters from the supplied scriptSig.
        /// </summary>
        /// <param name="network">The network that the scriptSig is for.</param>
        /// <param name="scriptSig">The scriptSig to extract parameters from.</param>
        /// <returns>The extracted scriptSig paramers as a <see cref="ColdStakingScriptSigParameters"/> object.</returns>
        public ColdStakingScriptSigParameters ExtractScriptSigParameters(Network network, Script scriptSig)
        {
            Op[] ops = scriptSig.ToOps().ToArray();
            if (!this.CheckScriptSigCore(network, scriptSig, ops, null, null))
                return null;

            try
            {
                return new ColdStakingScriptSigParameters()
                {
                    TransactionSignature = new TransactionSignature(ops[0].PushData),
                    IsColdPublicKey = (ops[0].Code == OP_0),
                    PublicKey = new PubKey(ops[2].PushData, true),
                };
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Generates the scriptSig.
        /// </summary>
        /// <param name="parameters">The scriptSig parameters.</param>
        /// <returns>The scriptSig.</returns>
        public Script GenerateScriptSig(ColdStakingScriptSigParameters parameters)
        {
            return this.GenerateScriptSig(parameters.TransactionSignature, parameters.IsColdPublicKey, parameters.PublicKey);
        }

        /// <summary>
        /// Generates the scriptSig.
        /// </summary>
        /// <param name="signature">The transaction signature. For unsigned inputs this can be <c>null</c> in which case it is encoded as an <c>OP_0</c>.</param>
        /// <param name="coldPubKey">A flag indicating whether the cold wallet versus the hot wallet is signing.</param>
        /// <param name="publicKey">The cold or hot wallet public key.</param>
        /// <returns>The scriptSig.</returns>
        public Script GenerateScriptSig(TransactionSignature signature, bool coldPubKey, PubKey publicKey)
        {
            Guard.NotNull(signature, nameof(signature));
            Guard.NotNull(publicKey, nameof(publicKey));

            return new Script(
                Op.GetPushOp(signature.ToBytes()),
                coldPubKey ? OP_0 : OP_1,
                Op.GetPushOp(publicKey.ToBytes())
                );
        }

        /// <summary>
        /// Creates a cold staking script.
        /// </summary>
        /// <remarks>Two keys control the balance associated with the script.
        /// The hot wallet key allows transactions to only spend amounts back to themselves while the cold
        /// wallet key allows amounts to be moved to different addresses. This makes it possible to perform
        /// staking using the hot wallet key so that even if the key becomes compromised it can't be used
        /// to reduce the balance. Only the person with the cold wallet key can retrieve the coins and move
        /// them elsewhere. This behavior is enforced by the <see cref="OP_CHECKCOLDSTAKEVERIFY"/>
        /// opcode within the script flow related to hot wallet key usage. It sets the <see cref="PosTransaction.IsColdCoinStake"/>
        /// flag if the transaction spending an output, which contains this instruction, is a coinstake
        /// transaction. If this flag is set then further rules are enforced by <see cref="Stratis.Bitcoin.Features.Consensus.Rules.CommonRules.PosColdStakingRule"/>.
        /// </remarks>
        /// <param name="hotPubKeyHash">The hot wallet public key hash to use.</param>
        /// <param name="coldPubKeyHash">The cold wallet public key hash to use.</param>
        /// <returns>The cold staking script.</returns>
        /// <seealso cref="Consensus.Rules.CommonRules.PosColdStakingRule"/>
        public Script GenerateScriptPubKey(KeyId hotPubKeyHash, KeyId coldPubKeyHash)
        {
            // The initial stack consumed by this script will be set up differently depending on whether a
            // hot or cold pubkey is used - i.e. either <scriptSig> 0 <coldPubKey> OR <scriptSig> 1 <hotPubKey>.
            return new Script(
                // Duplicates the last stack entry resulting in:
                // <scriptSig> 0/1 <coldPubKey/hotPubKey> <coldPubKey/hotPubKey>.
                OP_DUP,
                // Replaces the last stack entry with its hash resulting in:
                // <scriptSig> 0/1 <coldPubKey/hotPubKey> <coldPubKeyHash/hotPubKeyHash>.
                OP_HASH160,
                // Rotates the top 3 stack entries resulting in:
                // <scriptSig> <coldPubKey/hotPubKey> <coldPubKeyHash/hotPubKeyHash> 0/1.
                OP_ROT,
                // Consumes the top stack entry and continues from the OP_ELSE if the value was 0. Results in:
                // <scriptSig> <coldPubKey/hotPubKey> <coldPubKeyHash/hotPubKeyHash>.
                OP_IF,
                // Reaching this point means that the value was 1 - i.e. the hotPubKey is being used.
                // Executes the opcode as described in the remarks section. Stack remains unchanged.
                OP_CHECKCOLDSTAKEVERIFY,
                // Pushes the expected hotPubKey value onto the stack for later comparison purposes. Results in:
                // <scriptSig> <hotPubKey> <hotPubKeyHash> <hotPubKeyHash for comparison>.
                Op.GetPushOp(hotPubKeyHash.ToBytes()),
                // The code contained in the OP_ELSE is executed when the value was 0 - i.e. the coldPubKey is used.
                OP_ELSE,
                // Pushes the expected coldPubKey value onto the stack for later comparison purposes. Results in:
                // <scriptSig> <coldPubKey> <coldPubKeyHash> <coldPubKeyHash for comparison>.
                Op.GetPushOp(coldPubKeyHash.ToBytes()),
                OP_ENDIF,
                // Checks that the <coldPubKeyHash/hotPubKeyHash> matches the comparison value and removes both values
                // from the stack. The script fails at this point if the values mismatch. Results in:
                // <scriptSig> <coldPubKey/hotPubKey>.
                OP_EQUALVERIFY,
                // Consumes the top 2 stack entries and uses those values to verify the signature. Results in:
                // true/false - i.e. true if the signature is valid and false otherwise.
                OP_CHECKSIG);
        }

        /// <summary>
        /// This does a fast check of whether the script is a cold staking script.
        /// </summary>
        /// <param name="scriptPubKey">The script to check.</param>
        /// <param name="needMoreCheck">Whether additional checks are required.</param>
        /// <returns>The result is <c>true</c> if the script is a cold staking script and <c>false</c> otherwise.</returns>
        protected override bool FastCheckScriptPubKey(Script scriptPubKey, out bool needMoreCheck)
        {
            needMoreCheck = false;

            byte[] bytes = scriptPubKey.ToBytes(true);
            return (bytes.Length == 51)
                && (bytes[0] == (byte)OP_DUP)
                && (bytes[1] == (byte)OP_HASH160)
                && (bytes[2] == (byte)OP_ROT)
                && (bytes[3] == (byte)OP_IF)
                && (bytes[4] == (byte)OP_CHECKCOLDSTAKEVERIFY)
                && (bytes[5] == 0x14)
                && (bytes[26] == (byte)OP_ELSE)
                && (bytes[27] == 0x14)
                && (bytes[48] == (byte)OP_ENDIF)
                && (bytes[49] == (byte)OP_EQUALVERIFY)
                && (bytes[50] == (byte)OP_CHECKSIG);
        }

        /// <inheritdoc />
        /// <remarks>
        /// This method is called to implement additional checks if <see cref="FastCheckScriptPubKey"/>
        /// sets <c>needMoreCheck</c> to <c>true</c>. In our case it is not set and this method is not
        /// called. However this method is defined as <c>abstract</c> in <see cref="ScriptTemplate"/>
        /// so we still need this dummy implementation.
        /// </remarks>
        protected override bool CheckScriptPubKeyCore(Script scriptPubKey, Op[] scriptPubKeyOps)
        {
            return true;
        }

        /// <summary>
        /// Extracts the hot and cold wallet public key hashes from the script.
        /// </summary>
        /// <param name="scriptPubKey">The script to extract the public key hashes from.</param>
        /// <param name="hotPubKeyHash">The extracted hot wallet public key hash.</param>
        /// <param name="coldPubKeyHash">The extracted cold wallet public key hash.</param>
        /// <returns>Returns <c>true</c> if this is a cold staking script and the keys have been extracted.</returns>
        public bool ExtractScriptPubKeyParameters(Script scriptPubKey, out KeyId hotPubKeyHash, out KeyId coldPubKeyHash)
        {
            if (!this.FastCheckScriptPubKey(scriptPubKey, out bool needMoreCheck))
            {
                hotPubKeyHash = null;
                coldPubKeyHash = null;

                return false;
            }

            Guard.Assert(!needMoreCheck);

            hotPubKeyHash = new KeyId(scriptPubKey.ToBytes(true).SafeSubarray(6, 20));
            coldPubKeyHash = new KeyId(scriptPubKey.ToBytes(true).SafeSubarray(28, 20));

            return true;
        }

        /// <summary>
        /// Checks whether the scriptSig is valid.
        /// </summary>
        /// <param name="network">The network the script belongs to.</param>
        /// <param name="scriptSig">The scriptSig to check (not used).</param>
        /// <param name="scriptSigOps">The scriptSig opcodes.</param>
        /// <param name="scriptPubKey">The scriptPubKey to check (not used).</param>
        /// <param name="scriptPubKeyOps">The scriptPubKey opcodes (not used).</param>
        /// <returns>Returns <c>true</c> if the format of the scriptSig is valid and <c>false</c> otherwise.</returns>
        protected override bool CheckScriptSigCore(Network network, Script scriptSig, Op[] scriptSigOps, Script scriptPubKey, Op[] scriptPubKeyOps)
        {
            Op[] ops = scriptSigOps;
            if (ops.Length != 3)
                return false;

            return ((ops[0].PushData != null) && TransactionSignature.IsValid(network, ops[0].PushData, ScriptVerify.None))
                && ((ops[1].Code == OP_0) || (ops[1].Code == OP_1))
                && (ops[2].PushData != null) && PubKey.Check(ops[2].PushData, false);
        }
    }
}