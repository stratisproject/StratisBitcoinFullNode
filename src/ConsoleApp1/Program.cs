using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.IO;
using System.Linq;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Networks;
using Stratis.Bitcoin.Features.Wallet;
using Stratis.Bitcoin.Networks;
using Stratis.Bitcoin.Utilities;

namespace ConsoleApp1
{
    public class ScriptTest
    {
        private readonly Network network;

        public ScriptTest()
        {
            this.network = new StratisTest();
            NetworkRegistration.Register(this.network);
        }

        public void Test1()
        {
            // This is a P2PK transaction (not P2PKH?)

            // Testnet tx 1167be011516e74db6785923a63893f29de00f30fdd59409b185d9828f79b8af, output 1 
            //var scriptPubKey = new NBitcoin.Script("039cdb2bc68b3d776ee8671364f216d5d8cd3841cea0a38d35cbb908b087c79bda OP_CHECKSIG");
            var scriptPubKey = new NBitcoin.Script("21039cdb2bc68b3d776ee8671364f216d5d8cd3841cea0a38d35cbb908b087c79bdaac");

            // Spent in testnet tx 6b1ba16fd15e30a1ebec038e4e725245048b5da60e2eebb2455f17f2bab144b0, input 0 
            var scriptSig = new NBitcoin.Script("483045022100cc2d68f383ed6c48232a03ba5494f7e8e02a43bc16e1b35d8c705d15f7bde6d702207a4082e8c9fd89c24ae48f2f31e59c72df284d768c7d60756279d7da50a0ad9101");

            /*var scriptSigBytes = new byte[]
            {
                0x30, 0x45, 0x02, 0x21, 0x00, 0xcc, 0x2d, 0x68, 0xf3, 0x83, 0xed, 0x6c, 0x48, 0x23, 0x2a, 0x03, 0xba,
                0x54, 0x94, 0xf7, 0xe8, 0xe0, 0x2a, 0x43, 0xbc, 0x16, 0xe1, 0xb3, 0x5d, 0x8c, 0x70, 0x5d, 0x15, 0xf7,
                0xbd, 0xe6, 0xd7, 0x02, 0x20, 0x7a, 0x40, 0x82, 0xe8, 0xc9, 0xfd, 0x89, 0xc2, 0x4a, 0xe4, 0x8f, 0x2f,
                0x31, 0xe5, 0x9c, 0x72, 0xdf, 0x28, 0x4d, 0x76, 0x8c, 0x7d, 0x60, 0x75, 0x62, 0x79, 0xd7, 0xda, 0x50,
                0xa0, 0xad, 0x91
            }.ToList();

            // Add SIGHASH_ALL to end of signature
            scriptSigBytes.Add(0x01);

            List<Op> ops = new List<Op>();
            ops.Add(Op.GetPushOp(scriptSigBytes.ToArray()));*/

            //var scriptVerify = ScriptVerify.Standard;

            // It appears that ScriptVerify.Standard fails, whereas None does not
            var scriptVerify = ScriptVerify.None;
            var sigHash = SigHash.All;

            //var fundingTx = CreateCreditingTransaction(scriptPubKey, Money.Coins(1));
            //var spendingTx = CreateSpendingTransaction(scriptSig, fundingTx);

            int indexOfInterest = 0;

            //Console.WriteLine(NBitcoin.Script.VerifyScript(this.network, scriptSig, scriptPubKey, spendingTx, indexOfInterest, scriptVerify, sigHash));
        }

        public void Test2()
        {
            // Mainnet tx a9ac947f94b7c7a7e9627cfa6b762249bbf51abcccb18ed85217e4a1bee3e506, output 1
            var scriptPubKey = new Script("OP_DUP OP_HASH160 79661545408f19cee9cb59e4c575ffbd1f0f3baf OP_EQUALVERIFY OP_CHECKSIG");

            // Spent in mainnet tx cbdda696d8b9b39f9002aff4649ab9e4037ef846a29a01a5f42d0079eae73901, input 0
            // Note missing data push on public key portion of scriptSig, whereas signature does have the length (and prefixed with ASN.1 sequence marker)
            var scriptSig = new Script("3045022100a04c9988ad84ef7cc48cdceb66e832751b93b32cf0e5e6ae77f1d0608bb4e5e20220298c49c9186a6f611cbdbfeb87baac5a6c4fe061a47a3da7ca320b309eb8fb7b01 03e451d3ac9376441a3a0d2a43634deb5b7c607952f330c6e258f5a3c6cba676c4");

            //var scriptVerify = ScriptVerify.Mandatory | ScriptVerify.DerSig | ScriptVerify.CheckLockTimeVerify; -> set in RuleContext. Need to check if stratisX has similar flags enabled
            var scriptVerify = ScriptVerify.Standard;
            //var scriptVerify = ScriptVerify.Standard; -> seems to succeed for this particular transaction; not sure if it is correct in general
            //var scriptVerify = ScriptVerify.None;
            var sigHash = SigHash.All;

            var fundingTx = this.network.CreateTransaction("010000000d38235c033621a42364c4d4423e8e2bcc0a66dc8d430a8e2f7489c315943aa377f6fc594b010000006a4730440220738677807cd154478bb7a84725036df65a7acfaf72991518df47a6b39f7336da02201dceeb344c0951849707adf0bb02c874df75dd0f9439b27aa7b4550adb7329a8012103c3a5df8c0dca0630cb4237989034bf682e272a86aa640b3edb2a3a21ded1cf74feffffff1f4ccb3eb34d8a8d2ea079b20a302f465300717ccc3cce52a188052cef162567010000006a4730440220461b5def8b9c07e8857421a361b72ca090c5bd0986a06ddcad56721d40c1c55b0220511aceadc5ede2054828f2802c0223cf5ab428c0ad85ca5b0260556b5ec1f0ce01210377be7fd6ac70e970542d82418225e911ace2df22abe657445f5e8e4ddc56094ffeffffff4e59fdbd2a209bff084c77335b2bec71043e403ca7e23518239750a0923f3f73000000006b4830450221009a44dd557a31fd5d89831a0b567bef9e99d70e5837f4a4971597144225758dce022008a5602e5668800b5e98eb4c5cf60b94a3db787a27733605b50fdf9ed453f2d10121023b10bcc56ec402e08b4f727d78fe862a253d790ab075d7949013bb990c642401feffffff022b271d00000000001976a91429b9c9fcf7fb3c5a2eef83d64f25aed02eb7da5588ac780c1581030000001976a91479661545408f19cee9cb59e4c575ffbd1f0f3baf88ac0b311100");
            var spendingTx = this.network.CreateTransaction("01000000bd3f235c0106e5e3bea1e41752d88eb1ccbc1af5bb4922766bfa7c62e9a7c7b7947f94aca9010000006b483045022100a04c9988ad84ef7cc48cdceb66e832751b93b32cf0e5e6ae77f1d0608bb4e5e20220298c49c9186a6f611cbdbfeb87baac5a6c4fe061a47a3da7ca320b309eb8fb7b012103e451d3ac9376441a3a0d2a43634deb5b7c607952f330c6e258f5a3c6cba676c4feffffff02009d693a000000001976a9145019062c239e19d13553ea6a5e39f2607d4810c088acf8d81246030000001976a914914ac769e94c37847be167dc18bbb79dd4afeb4d88ac27311100");

            int indexOfInterest = 0;

            Console.WriteLine(Script.VerifyScript(this.network, scriptSig, scriptPubKey, spendingTx, indexOfInterest, scriptVerify, sigHash));
        }

        /*
        public void Test3()
        {
            var scriptPubKey = new Script("02253399452993da670c1020b310d2b608c2757357cf28d6cfe4c99dd0fb510e65 OP_CHECKSIG");
            //var scriptSig = new Script("3045022100eea18726ee5d256d34cdfe9f6c2e7b2fcec7d5cb280e17a2a547c532cd959b9302203f6398436b976cf6ed263f78dc08d4c0bce785e420ca262ce5fde58b33046a0101");
            var scriptSig = new Script("3045022100eea18726ee5d256d34cdfe9f6c2e7b2fcec7d5cb280e17a2a547c532cd959b9302203f6398436b976cf6ed263f78dc08d4c0bce785e420ca262ce5fde58b33046a0101");

            // This is a coinstake transaction so do not apply as rigorous a check
            var scriptVerify = ScriptVerify.None;
            var sigHash = SigHash.All;

            var fundingTx = CreateCreditingTransaction(scriptPubKey, Money.Coins(1));
            var spendingTx = CreateSpendingTransaction(scriptSig, fundingTx);

            int indexOfInterest = 0;

            Console.WriteLine(Script.VerifyScript(this.network, scriptSig, scriptPubKey, spendingTx, indexOfInterest, scriptVerify, sigHash));
        }
        */

        public void WalletScan()
        {
            var fs = new FileStorage<Wallet>(@"c:\Temp");
            Wallet wallet = fs.LoadByFileName($"TestNetStaking.wallet.json");

            var unspent = wallet.GetAllSpendableTransactions(CoinType.Stratis, 708967);

            Console.WriteLine("===");

            var stream = File.AppendText(@"c:\Temp\output1.txt");
            foreach (var unsp in unspent)
            {
                var amt = unsp.Transaction.SpendableAmount(false);
                Console.WriteLine(amt);

                stream.WriteLine(amt);
            }
            stream.Close();
            Console.WriteLine("===");
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            /*
            var network1 = new StratisTest();
            var ms = new MemoryStream();
            var stream = new BitcoinStream(ms, true);
            stream.ConsensusFactory = network1.Consensus.ConsensusFactory;

            var x = uint256.Parse("0000ffff00000000000000000000000000000000000000000000000000000000");
            stream.ReadWrite(ref x);

            Console.WriteLine(stream.ToString());
            */

            /*
            var x1 = "79661545408f19cee9cb59e4c575ffbd1f0f3baf";
            //var x2 = "3045022100a04c9988ad84ef7cc48cdceb66e832751b93b32cf0e5e6ae77f1d0608bb4e5e20220298c49c9186a6f611cbdbfeb87baac5a6c4fe061a47a3da7ca320b309eb8fb7b012103e451d3ac9376441a3a0d2a43634deb5b7c607952f330c6e258f5a3c6cba676c4";
            //var x2 = "483045022100a04c9988ad84ef7cc48cdceb66e832751b93b32cf0e5e6ae77f1d0608bb4e5e20220298c49c9186a6f611cbdbfeb87baac5a6c4fe061a47a3da7ca320b309eb8fb7b012103e451d3ac9376441a3a0d2a43634deb5b7c607952f330c6e258f5a3c6cba676c4";
            var x2 = "3045022100a04c9988ad84ef7cc48cdceb66e832751b93b32cf0e5e6ae77f1d0608bb4e5e20220298c49c9186a6f611cbdbfeb87baac5a6c4fe061a47a3da7ca320b309eb8fb7b 2103e451d3ac9376441a3a0d2a43634deb5b7c607952f330c6e258f5a3c6cba676c4";

            var b1 = Encoders.Hex.DecodeData(x1);
            var b2 = Encoders.Hex.DecodeData(x2);

            var h0 = x1;
            var h1 = Hashes.Hash160(b2, 0, b2.Length).ToString();

            //var h1 = Hashes.Hash160(vch, 0, vch.Length).ToBytes();
            */

            var scriptTest = new ScriptTest();
            //scriptTest.Test1();
            scriptTest.Test2();
            //scriptTest.Test3();

            scriptTest.WalletScan();

            var network = new StratisTest();

            var tx1 = network.CreateTransaction("0100000029e6135c02fb2c56d70e38de676fa5025fd4715b6b7aeb76065c370fd08323f863aa3906cb01000000b5004730440220737b302c4e08b533e102fab3d93526f7ad56baf4e0405de8274db2e6cd286c3002204422c5fc1751422685d672f7b928e1a9c94dfe0832051166d292fd6a623e5c4001004c69522102a6beaa4aebb8d7190476c7437f2162c650a70e23b8c735bb41c1a97b4f3f88932102503f03243d41c141172465caca2f5cef7524f149e965483be7ce4e44107d7d35210394f5229367658539ab435674af982dcc763300ddb7b5e33d0e20f017690af23253aeffffffffa9e041e4fc20daa4151b5d6a2146be28a29d3e56c9049ea43a66ca0d6192f55c00000000b500473044022007e09c34d4c5f7af941b3685f49b376392953164c3d7073217fcb972d81ffe7c0220702bb9f06461ae74c003ac8a5489a8f761b434c1ddada8c3f4ddbff350d3f9c201004c69522102a6beaa4aebb8d7190476c7437f2162c650a70e23b8c735bb41c1a97b4f3f88932102503f03243d41c141172465caca2f5cef7524f149e965483be7ce4e44107d7d35210394f5229367658539ab435674af982dcc763300ddb7b5e33d0e20f017690af23253aeffffffff03c003b4230000000017a914772a0f95abed290df20a6d9a28aad41e4f6cea5e870065cd1d000000001976a914bd9a3ba7e394fa5f79c0db1eb55e8d2751ae9e6888ac0000000000000000226a2000a7d8f15eea832faf46a1f135f57e3a929f1f604ae993bcc8c61221ef4abc8400000000");
            var tx2 = network.CreateTransaction("010000001ee6135c02fb2c56d70e38de676fa5025fd4715b6b7aeb76065c370fd08323f863aa3906cb01000000b50047304402200f2fff3da921557c9ee2a6b8d83f2954e4a93f549953af70d61c019371188f5002202ffddb81129a7ae64837171bac323ca6841e91252c458c5e747c5e961e1c177101004c69522102a6beaa4aebb8d7190476c7437f2162c650a70e23b8c735bb41c1a97b4f3f88932102503f03243d41c141172465caca2f5cef7524f149e965483be7ce4e44107d7d35210394f5229367658539ab435674af982dcc763300ddb7b5e33d0e20f017690af23253aeffffffffa9e041e4fc20daa4151b5d6a2146be28a29d3e56c9049ea43a66ca0d6192f55c00000000b6004830450221009666bd988ddf7c0c5703401abf8bf1c45fc6a4008caac0c3efe2cab434ee5c2702206268f0ee1472fcffda4edc2848170be605089d4e2f1c2effec42e5b16874479c01004c69522102a6beaa4aebb8d7190476c7437f2162c650a70e23b8c735bb41c1a97b4f3f88932102503f03243d41c141172465caca2f5cef7524f149e965483be7ce4e44107d7d35210394f5229367658539ab435674af982dcc763300ddb7b5e33d0e20f017690af23253aeffffffff03c003b4230000000017a914772a0f95abed290df20a6d9a28aad41e4f6cea5e870065cd1d000000001976a914bd9a3ba7e394fa5f79c0db1eb55e8d2751ae9e6888ac0000000000000000226a2000a7d8f15eea832faf46a1f135f57e3a929f1f604ae993bcc8c61221ef4abc8400000000");

            var pubkey1 = new PubKey("02a6beaa4aebb8d7190476c7437f2162c650a70e23b8c735bb41c1a97b4f3f8893");
            var pubkey2 = new PubKey("02503f03243d41c141172465caca2f5cef7524f149e965483be7ce4e44107d7d35");
            var pubkey3 = new PubKey("0394f5229367658539ab435674af982dcc763300ddb7b5e33d0e20f017690af232");

            var builder = new TransactionBuilder(network);

            var tx3 = builder.CombineSignatures(new Transaction[] { tx1, tx2 });

            Console.WriteLine(tx3.ToHex());
        }
    }
}
