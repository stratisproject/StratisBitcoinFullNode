using LedgerWallet.Transports;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin.Crypto;
using Stratis.Bitcoin.Networks;

namespace LedgerWallet
{
    public class LedgerClient : LedgerClientBase
    {
        public LedgerClient(ILedgerTransport transport) : base(transport)
        {
        }

        public static async Task<IEnumerable<LedgerClient>> GetHIDLedgersAsync()
        {
            var ledgers = (await HIDLedgerTransport.GetHIDTransportsAsync())
                            .Select(t => new LedgerClient(t))
                            .ToList();
            return ledgers;
        }

        public async Task<LedgerWalletFirmware> GetFirmwareVersionAsync(CancellationToken cancellation = default(CancellationToken))
        {
            var response = await ExchangeSingleAPDUAsync(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_FIRMWARE_VERSION, 0x00, 0x00, 0x00, OK, cancellation).ConfigureAwait(false);
            return new LedgerWalletFirmware(response);
        }

        public async Task<GetCoinVersionResult> GetCoinVersion(CancellationToken cancellation = default(CancellationToken))
        {
            byte[] response = await ExchangeSingleAPDUAsync(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_BTCHIP_INS_GET_COIN_VER, 0x00, 0x00, 0x00, OK, cancellation).ConfigureAwait(false);
            return new GetCoinVersionResult(response);
        }

        [Flags]
        public enum AddressType
        {
            Legacy = 0x00,
            Segwit = 0x01,
            NativeSegwit = 0x02,
        }

        public async Task<GetWalletPubKeyResponse> GetWalletPubKeyAsync(KeyPath keyPath, AddressType addressType = AddressType.Legacy, bool display = false, CancellationToken cancellation = default(CancellationToken))
        {
            Guard.AssertKeyPath(keyPath);
            var bytes = Serializer.Serialize(keyPath);
            //bytes[0] = 10;
            var response = await ExchangeSingleAPDUAsync(
                LedgerWalletConstants.LedgerWallet_CLA,
                LedgerWalletConstants.LedgerWallet_INS_GET_WALLET_PUBLIC_KEY,
                (byte)(display ? 1 : 0),
                (byte)addressType, bytes, OK, cancellation).ConfigureAwait(false);
            return new GetWalletPubKeyResponse(response);
        }

        public async Task<GetWalletPubKeyResponse> GetWalletMasterKeyAsync()
        {
            return await GetWalletPubKeyAsync(new KeyPath("0'"));
        }

        public async Task<KeyPath> GetWalletHDKeyPathForSegwitAddressAsync(KeyPath rootKeyPath, string segwitAddress, Network network, int startAtIndex = 0, int? maxAttempts = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            Guard.AssertKeyPath(rootKeyPath);

            var response = await GetWalletPubKeyAsync(rootKeyPath);
            var hdKey = response.ExtendedPublicKey;

            var i = startAtIndex;
            var a = 0L;

            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var keyPath = new KeyPath($"0/{i}");

                var segwit = $"{hdKey.Derive(keyPath).PubKey.WitHash.ScriptPubKey.Hash.GetAddress(network)}";
                if(segwit == segwitAddress)
                {
                    return keyPath;
                }

                i++;
                a++;

                if(a > maxAttempts)
                {
                    return null;
                }
            }
        }

        public Task<TrustedInput> GetTrustedInputAsync(IndexedTxOut txout, CancellationToken cancellation = default(CancellationToken))
        {
            return GetTrustedInputAsync(txout.Transaction, (int)txout.N, cancellation);
        }

        public TrustedInput GetTrustedInput(IndexedTxOut txout, CancellationToken cancellation = default(CancellationToken))
        {
            return GetTrustedInputAsync(txout.Transaction, (int)txout.N, cancellation).GetAwaiter().GetResult();
        }

        public TrustedInput GetTrustedInput(Transaction transaction, int outputIndex, CancellationToken cancellation = default(CancellationToken))
        {
            return GetTrustedInputAsync(transaction, outputIndex, cancellation).GetAwaiter().GetResult();
        }

        public async Task<TrustedInput> GetTrustedInputAsync(Transaction transaction, int outputIndex, CancellationToken cancellation = default(CancellationToken))
        {
            List<byte[]> apdus = GetTrustedInputAPDUs(transaction, outputIndex);
            var response = await ExchangeApdusAsync(apdus.ToArray(), OK, cancellation).ConfigureAwait(false);
            return new TrustedInput(response);
        }

        private List<byte[]> GetTrustedInputAPDUs(Transaction transaction, int outputIndex)
        {
            if (outputIndex >= transaction.Outputs.Count)
                throw new ArgumentOutOfRangeException("outputIndex is bigger than the number of outputs in the transaction", "outputIndex");
            var apdus = new List<byte[]>();
            var data = new MemoryStream();
            // Header
            BufferUtils.WriteUint32BE(data, outputIndex);
            BufferUtils.WriteBuffer(data, transaction.Version);
            if (transaction is PosTransaction posTx)
                BufferUtils.WriteBuffer(data, posTx.Time);
            VarintUtils.write(data, transaction.Inputs.Count);
            apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, 0x00, 0x00, data.ToArray()));
            // Each input
            foreach (var input in transaction.Inputs)
            {
                data = new MemoryStream();
                BufferUtils.WriteBuffer(data, input.PrevOut);
                VarintUtils.write(data, input.ScriptSig.Length);
                apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, 0x80, 0x00, data.ToArray()));
                data = new MemoryStream();
                BufferUtils.WriteBuffer(data, input.ScriptSig.ToBytes());
                apdus.AddRange(CreateApduSplit2(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, 0x80, 0x00, data.ToArray(), Utils.ToBytes(input.Sequence, true)));
            }
            // Number of outputs
            data = new MemoryStream();
            VarintUtils.write(data, transaction.Outputs.Count);
            apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, 0x80, 0x00, data.ToArray()));
            // Each output
            foreach (var output in transaction.Outputs)
            {
                data = new MemoryStream();
                BufferUtils.WriteBuffer(data, Utils.ToBytes((ulong)output.Value.Satoshi, true));
                VarintUtils.write(data, output.ScriptPubKey.Length);
                apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, 0x80, 0x00, data.ToArray()));
                data = new MemoryStream();
                BufferUtils.WriteBuffer(data, output.ScriptPubKey.ToBytes());
                apdus.AddRange(CreateAPDUSplit(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, 0x80, 0x00, data.ToArray()));
            }
            // Locktime
            apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_GET_TRUSTED_INPUT, 0x80, 0x00, transaction.LockTime.ToBytes()));
            return apdus;
        }

        public enum InputStartType
        {
            New = 0x00,
            NewSegwit = 0x02,
            Continue = 0x80
        }

        public byte[][] UntrustedHashTransactionInputStart(
            InputStartType startType,
            IndexedTxIn txIn,
            Dictionary<OutPoint, TrustedInput> trustedInputs,
            Dictionary<OutPoint, ICoin> coins,
            bool segwitMode, bool segwitParsedOnce, uint timestamp)
        {
            var apdus = new List<byte[]>();
            trustedInputs = trustedInputs ?? new Dictionary<OutPoint, TrustedInput>();
            // Start building a fake transaction with the passed inputs
            var data = new MemoryStream();
            BufferUtils.WriteBuffer(data, txIn.Transaction.Version);

            if (txIn.Transaction is PosTransaction posTx)
                BufferUtils.WriteBuffer(data, txIn.Transaction.Time);

            if (segwitMode && segwitParsedOnce)
                VarintUtils.write(data, 1);
            else
                VarintUtils.write(data, txIn.Transaction.Inputs.Count);

            apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_START, 0x00, (byte)startType, data.ToArray()));
            // Loop for each input
            long currentIndex = 0;
            foreach(var input in txIn.Transaction.Inputs)
            {
                if(segwitMode && segwitParsedOnce && currentIndex != txIn.Index)
                {
                    currentIndex++;
                    continue;
                }
                var script = new byte[0];
                if(currentIndex == txIn.Index || segwitMode && !segwitParsedOnce)
                    script = coins[input.PrevOut].GetScriptCode(Network ?? new BitcoinMain()).ToBytes();

                data = new MemoryStream();
                if(segwitMode)
                {
                    data.WriteByte(0x02);
                    BufferUtils.WriteBuffer(data, input.PrevOut);
                    BufferUtils.WriteBuffer(data, Utils.ToBytes((ulong)coins[input.PrevOut].TxOut.Value.Satoshi, true));
                }
                else
                {
                    var trustedInput = trustedInputs[input.PrevOut];
                    if(trustedInput != null)
                    {
                        data.WriteByte(0x01);
                        var b = trustedInput.ToBytes();
                        // untrusted inputs have constant length
                        data.WriteByte((byte)b.Length);
                        BufferUtils.WriteBuffer(data, b);
                    }
                    else
                    {
                        data.WriteByte(0x00);
                        BufferUtils.WriteBuffer(data, input.PrevOut);
                    }
                }
                VarintUtils.write(data, script.Length);
                apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_START, 0x80, 0x00, data.ToArray()));
                data = new MemoryStream();
                BufferUtils.WriteBuffer(data, script);
                BufferUtils.WriteBuffer(data, input.Sequence);
                apdus.AddRange(CreateAPDUSplit(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_START, 0x80, 0x00, data.ToArray()));
                currentIndex++;
            }
            return apdus.ToArray();
        }

        public byte[][] UntrustedHashTransactionInputFinalizeFull(KeyPath change, IEnumerable<TxOut> outputs)
        {
            var apdus = new List<byte[]>();
            if(change != null)
            {
                apdus.Add(CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_FINALIZE_FULL, 0xFF, 0x00, Serializer.Serialize(change)));
            }

            var offset = 0;
            var ms = new MemoryStream();
            var bs = new BitcoinStream(ms, true);
            var list = outputs.ToList();
            bs.ReadWrite<List<TxOut>, TxOut>(ref list);
            var data = ms.ToArray();

            while(offset < data.Length)
            {
                var blockLength = ((data.Length - offset) > 255 ? 255 : data.Length - offset);
                var apdu = new byte[blockLength + 5];
                apdu[0] = LedgerWalletConstants.LedgerWallet_CLA;
                apdu[1] = LedgerWalletConstants.LedgerWallet_INS_HASH_INPUT_FINALIZE_FULL;
                apdu[2] = ((offset + blockLength) == data.Length ? (byte)0x80 : (byte)0x00);
                apdu[3] = 0x00;
                apdu[4] = (byte)(blockLength);
                Array.Copy(data, offset, apdu, 5, blockLength);
                apdus.Add(apdu);
                offset += blockLength;
            }
            return apdus.ToArray();
        }

        public async Task<Transaction> SignTransactionAsync(KeyPath keyPath, ICoin[] signedCoins, Transaction[] parents, Transaction transaction, KeyPath changePath = null)
        {
            var requests = new List<SignatureRequest>();
            foreach(var c in signedCoins)
            {
                var tx = parents.FirstOrDefault(t => t.GetHash() == c.Outpoint.Hash);
                if(tx != null)
                    requests.Add(new SignatureRequest()
                    {
                        InputCoin = c,
                        InputTransaction = tx,
                        KeyPath = keyPath
                    });
            }
            return await SignTransactionAsync(requests.ToArray(), transaction, changePath: changePath);
        }

        public Network Network { get; set; }
        public async Task<Transaction> SignTransactionAsync(SignatureRequest[] signatureRequests, Transaction transaction, KeyPath changePath = null, CancellationToken cancellation = default(CancellationToken))
        {
            if(signatureRequests.Length == 0)
                throw new ArgumentException("No signatureRequests is passed", "signatureRequests");
            var segwitCoins = signatureRequests.Where(s => s.InputCoin.GetHashVersion(Network ?? new BitcoinMain()) == HashVersion.Witness).Count();
            if(segwitCoins != signatureRequests.Count() && segwitCoins != 0)
                throw new ArgumentException("Mixing segwit input with non segwit input is not supported", "signatureRequests");

            var segwitMode = segwitCoins != 0;

            var requests = signatureRequests
                .ToDictionaryUnique(o => o.InputCoin.Outpoint);
            //transaction = transaction.Clone();
            var inputsByOutpoint = transaction.Inputs.AsIndexedInputs().ToDictionary(i => i.PrevOut);
            var coinsByOutpoint = requests.ToDictionary(o => o.Key, o => o.Value.InputCoin);

            var trustedInputs = new List<TrustedInput>();
            if(!segwitMode)
            {
                List<byte[]> trustedInputApdus = new List<byte[]>();
                foreach (var sigRequest in signatureRequests)
                {
                    trustedInputApdus.AddRange(GetTrustedInputAPDUs(sigRequest.InputTransaction, (int)sigRequest.InputCoin.Outpoint.N));
                }
                var trustedInputApdusResponses = await ExchangeAsync(trustedInputApdus.ToArray(), cancellation).ConfigureAwait(false);
                foreach(var trustedInputApdusResponse in trustedInputApdusResponses)
                {
                    CheckSW(OK, trustedInputApdusResponse.SW);
                    if(trustedInputApdusResponse.Response.Length != 0)
                        trustedInputs.Add(new TrustedInput(trustedInputApdusResponse.Response));
                }
            }

            var noPubKeyRequests = signatureRequests.Where(r => r.PubKey == null).ToArray();
            var getPubKeys = new List<Task<GetWalletPubKeyResponse>>();
            foreach(var previousReq in noPubKeyRequests)
            {
                getPubKeys.Add(GetWalletPubKeyAsync(previousReq.KeyPath, cancellation: cancellation));
            }
            await Task.WhenAll(getPubKeys).ConfigureAwait(false);

            for(var i = 0; i < noPubKeyRequests.Length; i++)
            {
                noPubKeyRequests[i].PubKey = getPubKeys[i].Result.UncompressedPublicKey.Compress();
            }

            var trustedInputsByOutpoint = trustedInputs.ToDictionaryUnique(i => i.OutPoint);
            var apdus = new List<byte[]>();
            var inputStartType = segwitMode ? InputStartType.NewSegwit : InputStartType.New;


            var segwitParsedOnce = false;
            for(var i = 0; i < signatureRequests.Length; i++)
            {
                var sigRequest = signatureRequests[i];
                var input = inputsByOutpoint[sigRequest.InputCoin.Outpoint];
                apdus.AddRange(UntrustedHashTransactionInputStart(inputStartType, input, trustedInputsByOutpoint, coinsByOutpoint, segwitMode, segwitParsedOnce, transaction.Time));
                inputStartType = InputStartType.Continue;
                if(!segwitMode || !segwitParsedOnce)
                    apdus.AddRange(UntrustedHashTransactionInputFinalizeFull(changePath, transaction.Outputs));
                changePath = null; //do not resubmit the changepath
                if(segwitMode && !segwitParsedOnce)
                {
                    segwitParsedOnce = true;
                    i--; //pass once more
                    continue;
                }
                apdus.Add(UntrustedHashSign(sigRequest.KeyPath, null, transaction.LockTime, SigHash.All));
            }
            var responses = await ExchangeAsync(apdus.ToArray(), cancellation).ConfigureAwait(false);
            foreach(var response in responses)
                if(response.Response.Length > 10) //Probably a signature
                    response.Response[0] = 0x30;
            var signatures = responses.Where(p => TransactionSignature.IsValid(Network ?? new StratisMain(), p.Response)).Select(p => new TransactionSignature(p.Response)).ToArray();

            if(signatureRequests.Length != signatures.Length)
                throw new LedgerWalletException("failed to sign some inputs");
            var sigIndex = 0;

            var builder = new TransactionBuilder(Network ?? new StratisMain());
            foreach(var sigRequest in signatureRequests)
            {
                var input = inputsByOutpoint[sigRequest.InputCoin.Outpoint];
                if(input == null)
                    continue;
                builder.AddCoins(sigRequest.InputCoin);
                builder.AddKnownSignature(sigRequest.PubKey, signatures[sigIndex]);
                sigIndex++;
            }
            builder.SignTransactionInPlace(transaction);

            sigIndex = 0;
            foreach(var sigRequest in signatureRequests)
            {
                var input = inputsByOutpoint[sigRequest.InputCoin.Outpoint];
                if(input == null)
                    continue;
                sigRequest.Signature = signatures[sigIndex];
                if(!sigRequest.PubKey.Verify(transaction.GetSignatureHash(Network ?? new StratisMain(), sigRequest.InputCoin, sigRequest.Signature.SigHash), sigRequest.Signature.Signature))
                {
                    foreach(var sigRequest2 in signatureRequests)
                        sigRequest2.Signature = null;
                    return null;
                }
                sigIndex++;
            }

            return transaction;
        }

        private async Task ModifyScriptSigAndVerifySignature(Task<TransactionSignature> sigTask, SignatureRequest previousReq, IndexedTxIn input)
        {
            var pubkey = previousReq.PubKey ??
                        (await GetWalletPubKeyAsync(previousReq.KeyPath).ConfigureAwait(false)).UncompressedPublicKey.Compress();

            var sig = await sigTask.ConfigureAwait(false);
        }

        public byte[] UntrustedHashSign(KeyPath keyPath, UserPin pin, LockTime lockTime, SigHash sigHashType)
        {
            var data = new MemoryStream();
            var path = Serializer.Serialize(keyPath);
            BufferUtils.WriteBuffer(data, path);

            var pinBytes = pin == null ? new byte[0] : pin.ToBytes();
            data.WriteByte((byte)pinBytes.Length);
            BufferUtils.WriteBuffer(data, pinBytes);
            BufferUtils.WriteUint32BE(data, (uint)lockTime);
            data.WriteByte((byte)sigHashType);
            return CreateAPDU(LedgerWalletConstants.LedgerWallet_CLA, LedgerWalletConstants.LedgerWallet_INS_HASH_SIGN, 0x00, 0x00, data.ToArray());
        }

        public async Task PrepareMessage(KeyPath keyPath, string messageString, CancellationToken cancellation = default(CancellationToken))
        {
            Guard.AssertKeyPath(keyPath);

            // The prefix is actually used for the formatted signature text. It is not passed to the secure element.
            //const string Prefix = "\x18Stratis Signed Message:\n";

            var data = new MemoryStream();

            // BIP32 path of key to be used to sign the message
            var path = Serializer.Serialize(keyPath);
            BufferUtils.WriteBuffer(data, path);

            // Message length
            data.WriteByte((byte)(Encoding.ASCII.GetBytes(messageString).Length));

            // Message
            BufferUtils.WriteBuffer(data, Encoding.ASCII.GetBytes(messageString));

            await ExchangeSingleAPDUAsync(
                LedgerWalletConstants.LedgerWallet_CLA,
                LedgerWalletConstants.LedgerWallet_INS_SIGN_MESSAGE,
                0x00, // Prepare message
                0x00,
                data.ToArray(),
                OK,
                cancellation).ConfigureAwait(false);
        }

        public async Task<byte[]> SignMessage(CancellationToken cancellation = default(CancellationToken))
        {
            var data = new MemoryStream();

            // User validation code length
            data.WriteByte(0);

            var resp = await ExchangeSingleAPDUAsync(
                LedgerWalletConstants.LedgerWallet_CLA,
                LedgerWalletConstants.LedgerWallet_INS_SIGN_MESSAGE,
                0x80, // Sign message
                0x00,
                data.ToArray(),
                OK,
                cancellation).ConfigureAwait(false);

            return resp;
        }
    }

    [Flags]
    public enum FirmwareFeatures : byte
    {
        Compressed = 0x01,
        SecureElementUI = 0x02,
        ExternalUI = 0x04,
        NFC = 0x08,
        BLE = 0x10,
        TrustedEnvironmentExecution = 0x20
    }


    //https://ledgerhq.github.io/LedgerWallet-doc/bitcoin-technical.html#_get_firmware_version
    public class LedgerWalletFirmware
    {
        public LedgerWalletFirmware(int major, int minor, int patch, bool compressedKeys)
        {

        }

        public LedgerWalletFirmware(byte[] bytes)
        {
            Features = (FirmwareFeatures)(bytes[0] & ~0xC0);
            Architecture = bytes[1];
            Major = bytes[2];
            Minor = bytes[3];
            Patch = bytes[4];
            LoaderMinor = bytes[5];
            LoaderMajor = bytes[6];
        }
        public FirmwareFeatures Features { get; }
        public byte Architecture { get; }
        public byte Major { get; }
        public byte Minor { get; }
        public byte Patch { get; }
        public byte LoaderMajor { get; }
        public byte LoaderMinor { get; }

        public override string ToString()
        {
            return (Architecture != 0 ? "Ledger " : "") + string.Format("{0}.{1}.{2} (Loader : {3}.{4})", Major, Minor, Patch, LoaderMajor, LoaderMinor);
        }
    }
}
