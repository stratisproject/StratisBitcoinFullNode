using System;
using NBitcoin.DataEncoders;
using Stratis.Bitcoin.Tests.Common;
using Xunit;

namespace NBitcoin.Tests
{
    public class sighash_tests
    {
        private readonly Network networkMain;

        public sighash_tests()
        {
            this.networkMain = KnownNetworks.Main;
        }

        private static Random rand = new Random();

        private static Script RandomScript()
        {
            OpcodeType[] oplist = { OpcodeType.OP_FALSE, OpcodeType.OP_1, OpcodeType.OP_2, OpcodeType.OP_3, OpcodeType.OP_CHECKSIG, OpcodeType.OP_IF, OpcodeType.OP_VERIF, OpcodeType.OP_RETURN, OpcodeType.OP_CODESEPARATOR };
            var script = new Script();
            int ops = (rand.Next() % 10);
            for(int i = 0; i < ops; i++)
                script += oplist[rand.Next() % oplist.Length];

            return script;
        }


        //Compare between new old implementation of signature in reference bitcoin. But NBitcoin is like the old one, so we don't care about this test
        //[Fact]
        //public void sighash_test()
        //{

        //    int nRandomTests = 50000;


        //    for(int i = 0 ; i < nRandomTests ; i++)
        //    {
        //        int nHashType = rand.Next();
        //        Transaction txTo = RandomTransaction((nHashType & 0x1f) == SigHash.Single);
        //        Script scriptCode = RandomScript();
        //        int nIn = rand.Next() % txTo.VIn.Length;

        //        var sho = SignatureHashOld(scriptCode, txTo, nIn, nHashType);
        //        var sh = scriptCode.SignatureHash(txTo, nIn, (SigHash)nHashType);

        //        Assert.True(sh == sho);
        //    }
        //}

        // Goal: check that SignatureHash generates correct hash
        [Fact]
        [Trait("Core", "Core")]
        public void sighash_from_data()
        {
            TestCase[] tests = TestCase.read_json(TestDataLocations.GetFileFromDataFolder("sighash.json"));

            foreach(TestCase test in tests)
            {
                string strTest = test.ToString();
                if(test.Count < 1) // Allow for extra stuff (useful for comments)
                {
                    Assert.True(false, "Bad test: " + strTest);
                    continue;
                }
                if(test.Count == 1)
                    continue; // comment

                string raw_tx, raw_script, sigHashHex;
                int nIn, nHashType;
                var tx = new Transaction();
                var scriptCode = new Script();


                // deserialize test data
                raw_tx = (string)test[0];
                raw_script = (string)test[1];
                nIn = (int)(long)test[2];
                nHashType = (int)(long)test[3];
                sigHashHex = (string)test[4];


                tx.ReadWrite(ParseHex(raw_tx), this.networkMain.Consensus.ConsensusFactory);

                byte[] raw = ParseHex(raw_script);
                scriptCode = new Script(raw);

                uint256 sh = Script.SignatureHash(KnownNetworks.Main, scriptCode, tx, nIn, (SigHash)nHashType);
                Assert.True(sh.ToString() == sigHashHex, strTest);
            }
        }
        private byte[] ParseHex(string data)
        {
            return Encoders.Hex.DecodeData(data);
        }
    }
}