using System;
using System.Collections.Generic;
using NBitcoin;
using Stratis.Bitcoin.Features.Consensus.CoinViews;

namespace Stratis.SmartContracts.State.AccountAbstractionLayer
{
    public class CondensingTx
    {
        private IList<TransferInfo> transfers;
        private SmartContractTransaction transaction;
        private Dictionary<uint160, Tuple<ulong, ulong>> plusMinusInfo;
        private Dictionary<uint160, ulong> balances;

        public CondensingTx(IList<TransferInfo> transfers, SmartContractTransaction transaction)
        {
            this.transfers = transfers;
            this.transaction = transaction;
            this.plusMinusInfo = new Dictionary<uint160, Tuple<ulong, ulong>>();
            this.balances = new Dictionary<uint160, ulong>();
        }

        public Transaction CreateCondensingTx()
        {
            //SelectionVin();
            CalculatePlusAndMinus();
            CreateNewBalances();

            Transaction ret = new Transaction();
            foreach (TransferInfo transfer in this.transfers)
            {
                ret.AddInput(CreateInput(transfer));
                ret.AddOutput(CreateOutput(transfer));
            }
            return ret;
        }

        private void CalculatePlusAndMinus()
        {
            foreach(TransferInfo transfer in this.transfers)
            {
                if (!this.plusMinusInfo.ContainsKey(transfer.From))
                {
                    this.plusMinusInfo.Add(transfer.From, new Tuple<ulong, ulong>(0, transfer.Value));
                }
                else
                {
                    this.plusMinusInfo[transfer.From] = new Tuple<ulong, ulong>(this.plusMinusInfo[transfer.From].Item1, this.plusMinusInfo[transfer.To].Item2 + transfer.Value);
                }

                if (!this.plusMinusInfo.ContainsKey(transfer.To))
                {
                    this.plusMinusInfo.Add(transfer.To, new Tuple<ulong, ulong>(transfer.Value, 0));
                }
                else
                {
                    this.plusMinusInfo[transfer.To] = new Tuple<ulong, ulong>(this.plusMinusInfo[transfer.From].Item1 + transfer.Value, this.plusMinusInfo[transfer.To].Item2);
                }
            }
        }

        private void CreateNewBalances()
        {
            foreach(var kvp in this.plusMinusInfo)
            {
                ulong balance = 0;
                new CachedCoinView().
            }
        }

        private TxIn CreateInput(TransferInfo transfer)
        {
            // for now we use the hash and nvout from the transaction but in the future this will have to be changed to get it from somewhere real
            OutPoint outpoint = new OutPoint(this.transaction.Hash, this.transaction.Nvout);
            return new TxIn(outpoint, new Script(OpcodeType.OP_SPEND));
        }

        private TxOut CreateOutput(TransferInfo transfer)
        {
            // this is only for outputs to ordinary addresses right now - not to contracts
            // contract calls need to use the OP_CALL transaction. 
            Script script = new Script(
                    OpcodeType.OP_DUP,
                    OpcodeType.OP_HASH160,
                    Op.GetPushOp(transfer.To.ToBytes()),
                    OpcodeType.OP_EQUALVERIFY,
                    OpcodeType.OP_CHECKSIG
                ); // hope this is doing the right thing ???

            return new TxOut(new Money(transfer.Value), script);
        }
    }
}

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using NBitcoin;

//namespace Stratis.SmartContracts.State.AccountAbstractionLayer
//{
//    public class CondensingTx
//    {
//        /// <summary>
//        /// Same as in Qtum. Can adjust later
//        /// </summary>
//        private const uint MAX_CONTRACT_VOUTS = 1000;

//        private Dictionary<uint160, Tuple<ulong, ulong>> plusMinusInfo;
//        private Dictionary<uint160, ulong> balances;
//        private Dictionary<uint160, uint> nVouts;
//        private Dictionary<uint160, Vin> vins;
//        private IList<TransferInfo> transfers;
//        private HashSet<uint160> deleteAddresses;
//        private IContractStateRepository state;
//        private SmartContractTransaction scTransaction;
//        bool voutOverflow = false;

//        public CondensingTx(IContractStateRepository state, IList<TransferInfo> transfers, SmartContractTransaction scTx, HashSet<uint160> deleteAddresses)
//        {
//            this.state = state;
//            this.transfers = transfers;
//            this.scTransaction = scTx;
//            this.deleteAddresses = deleteAddresses;
//            this.balances = new Dictionary<uint160, ulong>();
//            this.nVouts = new Dictionary<uint160, uint>();
//            this.vins = new Dictionary<uint160, Vin>();
//        }

//        public Transaction CreateCondensingTx()
//        {
//            SelectionVin();
//            CalculatePlusAndMinus();
//            if (!CreateNewBalances())
//                return new Transaction();
//            Transaction tx = new Transaction();
//            foreach (var txIn in CreateVins())
//            {
//                tx.AddInput(txIn);
//            }
//            foreach (var txOut in CreateVout())
//            {
//                tx.AddOutput(txOut);
//            }

//            return !tx.Inputs.Any() || !tx.Outputs.Any() ? new Transaction() : tx;
//        }

//        private Dictionary<uint160, Vin> CreateVin(Transaction tx)
//        {
//            Dictionary<uint160, Vin> vins = new Dictionary<uint160, Vin>();

//            foreach (var b in balances)
//            {
//                if (b.Key == scTransaction.From)
//                    continue;

//                if (b.Value > 0)
//                {
//                    vins[b.Key] = new Vin
//                    {
//                        Hash = tx.GetHash(),
//                        Nvout = nVouts[b.Key],
//                        Value = b.Value,
//                        Alive = 1
//                    };
//                }
//                else
//                {
//                    vins[b.Key] = new Vin
//                    {
//                        Hash = tx.GetHash(),
//                        Nvout = 0,
//                        Value = 0,
//                        Alive = 0,
//                    };
//                }
//            }
//            return vins;
//        }

//        private void SelectionVin()
//        {
//            foreach (TransferInfo ti in transfers)
//            {
//                if (!vins.ContainsKey(ti.From))
//                {
//                    //var a = state.Vin(ti.From);
//                    //if (a != null)
//                    //{
//                    //    vins[ti.From] = a;
//                    //}
//                    if (ti.From == scTransaction.From && scTransaction.Value > 0)
//                    {
//                        vins[ti.From] = new Vin
//                        {
//                            Hash = scTransaction.Hash,
//                            Nvout = scTransaction.Nvout,
//                            Value = scTransaction.Value,
//                            Alive = 1
//                        };
//                    }
//                }

//                if (!vins.ContainsKey(ti.To))
//                {
//                    //var a = state.Vin(ti.To);
//                    //if (a != null)
//                    //    vins[ti.To] = a;
//                }
//            }
//        }

//        private void CalculatePlusAndMinus()
//        {
//            foreach (TransferInfo ti in transfers)
//            {
//                if (!plusMinusInfo.ContainsKey(ti.From))
//                {
//                    plusMinusInfo[ti.From] = new Tuple<ulong, ulong>(0, ti.Value);
//                }
//                else
//                {
//                    plusMinusInfo[ti.From] = new Tuple<ulong, ulong>(plusMinusInfo[ti.From].Item1, plusMinusInfo[ti.From].Item2 + ti.Value);
//                }

//                if (!plusMinusInfo.ContainsKey(ti.To))
//                {

//                    plusMinusInfo[ti.To] = new Tuple<ulong, ulong>(ti.Value, 0);
//                }
//                else
//                {
//                    plusMinusInfo[ti.To] = new Tuple<ulong, ulong>(plusMinusInfo[ti.To].Item1 + ti.Value, plusMinusInfo[ti.To].Item2);
//                }
//            }
//        }

//        private bool CreateNewBalances()
//        {
//            foreach (KeyValuePair<uint160, Tuple<ulong, ulong>> p in this.plusMinusInfo)
//            {
//                ulong balance = 0;
//                if ((vins.ContainsKey(p.Key) && vins[p.Key].Alive != 0)
//                    || (vins[p.Key].Alive == 0 && !CheckDeleteAddress(p.Key)))
//                {
//                    balance = vins[p.Key].Value;
//                }
//                balance += p.Value.Item1;
//                if (balance < p.Value.Item2)
//                    return false;
//                balance -= p.Value.Item2;
//                balances[p.Key] = balance;
//            }
//            return true;
//        }

//        private IList<TxIn> CreateVins()
//        {
//            List<TxIn> txIns = new List<TxIn>();

//            foreach (KeyValuePair<uint160, Vin> v in this.vins)
//            {
//                if (
//                    (v.Value.Value > 0 && v.Value.Alive != 0)
//                    || (v.Value.Value > 0 && this.vins[v.Key].Alive == 0 && !CheckDeleteAddress(v.Key)))
//                {
//                    OutPoint outpoint = new OutPoint(v.Value.Hash, v.Value.Nvout);
//                    txIns.Add(new TxIn(outpoint, new Script(OpcodeType.OP_SPEND)));
//                }
//            }
//            return txIns;
//        }

//        private IList<TxOut> CreateVout()
//        {
//            uint count = 0;
//            List<TxOut> outs = new List<TxOut>();
//            foreach (KeyValuePair<uint160, ulong> b in this.balances)
//            {
//                if (b.Value > 0)
//                {
//                    Script script = null;
//                    var a = state.GetAccountState(b.Key);
//                    if (a != null)
//                    {
//                        // Create a send to contract
//                        // script = CScript() << valtype{ 0} << valtype{ 0} << valtype{ 0} << valtype{ 0} << b.first.asBytes() << OP_CALL;
//                        throw new NotImplementedException();
//                    }
//                    else
//                    {
//                        // Create a send to given address
//                        // script = CScript() << OP_DUP << OP_HASH160 << b.first.asBytes() << OP_EQUALVERIFY << OP_CHECKSIG;
//                        throw new NotImplementedException();
//                    }
//                    outs.Add(new TxOut(new Money(b.Value), script));
//                    this.nVouts[b.Key] = count;
//                    count++;
//                    if (count > MAX_CONTRACT_VOUTS)
//                    {
//                        voutOverflow = true;
//                        return outs;
//                    }
//                }
//            }
//            return outs;
//        }

//        private bool CheckDeleteAddress(uint160 address)
//        {
//            return this.deleteAddresses.Any(x => x == address);
//        }
//    }
//}
