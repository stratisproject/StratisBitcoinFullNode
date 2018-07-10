using NBitcoin;
using NBitcoin.DataEncoders;
using Newtonsoft.Json.Linq;

namespace Stratis.Bitcoin.Features.RPC
{
    public class UnspentCoin
    {
        public Network Network { get; }

        public UnspentCoin(JObject unspent, Network network)
        {
            this.Network = network;
            this.OutPoint = new OutPoint(uint256.Parse((string)unspent["txid"]), (uint)unspent["vout"]);
            string address = (string)unspent["address"];
            if(address != null) this.Address = network.Parse<BitcoinAddress>(address);
            this.Account = (string)unspent["account"];
            this.ScriptPubKey = new Script(Encoders.Hex.DecodeData((string)unspent["scriptPubKey"]));
            string redeemScriptHex = (string)unspent["redeemScript"];
            if(redeemScriptHex != null)
            {
                this.RedeemScript = new Script(Encoders.Hex.DecodeData(redeemScriptHex));
            }
            decimal amount = (decimal)unspent["amount"];
            this.Amount = new Money((long)(amount * Money.COIN));
            this.Confirmations = (uint)unspent["confirmations"];

            // Added in Bitcoin Core 0.10.0
            if(unspent["spendable"] != null)
            {
                this.IsSpendable = (bool)unspent["spendable"];
            }
            else
            {
                // Default to True for earlier versions, i.e. if not present
                this.IsSpendable = true;
            }
        }

        public OutPoint OutPoint
        {
            get;
            private set;
        }

        public BitcoinAddress Address
        {
            get;
            private set;
        }
        public string Account
        {
            get;
            private set;
        }
        public Script ScriptPubKey
        {
            get;
            private set;
        }

        public Script RedeemScript
        {
            get;
            private set;
        }

        public uint Confirmations
        {
            get;
            private set;
        }

        public Money Amount
        {
            get;
            private set;
        }

        public Coin AsCoin()
        {
            var coin = new Coin(this.OutPoint, new TxOut(this.Amount, this.ScriptPubKey));
            if(this.RedeemScript != null)
                coin = coin.ToScriptCoin(this.RedeemScript).AssertCoherent(this.Network);
            return coin;
        }

        public bool IsSpendable
        {
            get;
            private set;
        }
    }
}
