## How to setup new PoA network



### Step 1: Creating new projects

Clone Stratis FN (https://github.com/stratisproject/StratisBitcoinFullNode.git).

Open the solution and add new .NET Core console application project. It will be a daemon for your new network. Let's call it MyPoAChainD.

Reference following projects:

```
Stratis.Bitcoin.Features.Api
Stratis.Bitcoin.Features.Apps
Stratis.Bitcoin.Features.BlockStore
Stratis.Bitcoin.Features.Consensus
Stratis.Bitcoin.Features.MemoryPool
Stratis.Bitcoin.Features.Miner
Stratis.Bitcoin.Features.PoA
Stratis.Bitcoin.Features.RPC
Stratis.Bitcoin.Features.Wallet
Stratis.Bitcoin
```



Enable latest C# features by going to MyPoAChainD's properties => Build => Advanced... => Language Version  and select `C# latest minor version (latest)`.

Open `Program.cs` and copy there everything from `Stratis.PoAChainD.Program.cs`, don't forget to change namespace.



Change startup project to `MyPoAChainD` and hit F5. Node should start and you will see console output similar to the following:

```
info: Stratis.Bitcoin.FullNode[0]
      Full node initialized on PoAMain.
info: Stratis.Bitcoin.FullNode[0]
      Starting node.
info: Stratis.Bitcoin.Base.BaseFeature[0]
      Loading finalized block height.
info: Stratis.Bitcoin.Base.BaseFeature[0]
      Loading chain.
info: Stratis.Bitcoin.Base.BaseFeature[0]
      Chain loaded at height 51759.
info: Stratis.Bitcoin.FullNode[0]
      FlushChain starting in 60 seconds.
info: Stratis.Bitcoin.Base.BaseFeature[0]
      Loading peers from : C:\Users\user\AppData\Roaming\StratisNode\poa\PoAMain.
info: Stratis.Bitcoin.FullNode[0]
      Periodic peer flush starting in 300 seconds.
info: Stratis.Bitcoin.Features.PoA.FederationManager[0]
      Federation contains 3 members. Their public keys are:
      03e6f19ea3dc6c145d98a0e0838af952755798e5bc3950bbca4f9485aa23873d7f
      02ddebcf18207072bdd172a25f85f2ea12e2de1d9d794f136722634aad08400fcb
      02067b38d777690aaaf23a5b371a819e6ddc6d2aae734b0199fe59df28dc056dd7
      ...
```

You can run daemon from visual studio or by using `dotnet exec MyPoAChainD.dll`.

To stop the node press `ctrl+c`.



Now you will need to create a new library project where you will add network and other files related to your new blockchain. Or you can add new classes directly to `Stratis.Bitcoin.Features.PoA`, however, this is not advised.



### Step 2: Federation keys generation and network setup

First you will need to generate keys for federation members. It is strongly advised that each federation member generates a key on his own and then shares public key with someone who is setting the network up.

To generate a key you should run `dotnet exec MyPoAChainD.dll -generateKeyPair`

After executing this command you should see similar output:

```
Federation key pair was generated and saved to C:\Users\user\AppData\Roaming\StratisNode\poa\PoAMain\federationKey.dat.
Make sure to back it up!
Your public key is 03a9295049f537e0a3c90dd644e5309f56c3dd1a378c2b5e3f25238dc46c6eb5fc.

Press any key to exit...
```

Navigate to directory given in output and backup your key. Share public key with one who set's up the node.



Now let's create a network class named `MyPoANetwork.cs`. Inherit your class from `Network` and copy-paste everything from PoANetwork.



Modify messageStart, GenesisTime (it should be equal to the time at which you are creating the network), federationPublicKeys (insert there keys collected from federation members), targetSpacingSeconds (this is the target delay between blocks), premineReward, base58 prefixes and data in genesis transaction. Also you can potentially modify other things if you know exactly what are the consequences of such modifications.

In `MyPoAChainD.Program.cs` instantiate your network instead of `PoANetwork`.

Run the node in debugger and copy new values for `Consensus.HashGenesisBlock` and `Genesis.Header.HashMerkleRoot`. Replace old values used in assertions and checkpoint with values for your genesis block.



Now run the node and you will see similar output after running it for a few seconds:

```
info: Stratis.Bitcoin.FullNode[0]
      ======Node stats====== 10/27/2018 14:05:35
      Headers.Height:      0        Headers.Hash:     564655bb087dd49164a03542475c7038be0bcb958696811cff1a6a8433f60a1c
      Consensus.Height:    0        Consensus.Hash:   564655bb087dd49164a03542475c7038be0bcb958696811cff1a6a8433f60a1c
      BlockStore.Height:   0        BlockStore.Hash:  564655bb087dd49164a03542475c7038be0bcb958696811cff1a6a8433f60a1c
      Wallet.Height:       No Wallet

      ======Connection====== agent StratisBitcoin:1.2.3

      ======Consensus Manager======
      Unconsumed blocks: 0 -- (0 / 209 715 200 bytes). Cache is filled by: 0%

      ======Block Puller======
      Blocks being downloaded: 0
      Queued downloads: 0
      Average block size: 0 KB
      Total download speed: 0 KB/sec
      Average time to download a block: NaN ms
      Amount of blocks node can download in 1 second: NaN

      ======BlockStore======
      Batch Size: 0 kb / 5000 kb  (0 blocks)

      ======PoA Miner======
      Mining information for the last 20 blocks.
      MISS means that miner didn't produce a block at the timestamp he was supposed to.
      ...
      =======Mempool=======
      MempoolSize: 0    DynamicSize: 0 kb   OrphanSize: 0
```



Stop the node and add (if it's not already there) `federationKey.dat` file to node folder.

Run the node and make sure you see similar output:

```
info: Stratis.Bitcoin.Features.PoA.FederationManager[0]
      Federation key pair was successfully loaded. Your public key is: 021315fd095630fb155d750420a5eef7cb614918f6dbc89215e40993b4a0ac0f5f.
```



Now wait a bit until you see

```
info: Stratis.Bitcoin.Features.PoA.PoAMiner[0]
      <<==============================================================>>
      Block was mined 1-01ed0a5f66d8a2628e790968e96e5ddd53d66005eb9284f9fb29d7fb1a19a8b7.
      <<==============================================================>>
```

to make sure that you are mining.



### Step 3: wallet creation and premine

When block at height `cosnesususOptions.premineHeight` is generated it will contain a premine reward.

Before you've generated such a block you need to create wallet. To do so start the node without mining (just move `federationKey.dat` somewhere) to avoid mining a premine block before you have the wallet.

At node's startup you will see similar line:

```
API starting on URL 'http://localhost:37220/'
```

Open in your browser: `http://localhost:37220/swagger/index.html`

Navigate to `Wallet` section and execute create command. Example of input parameters:

```
{
  "password": "123123123",
  "passphrase": "123123123",
  "name": "mywallet"
}
```



After doing that you should see in console periodic log:

```
      ======Wallets======
      Wallet: mywallet, Confirmed balance: 0.00000000 Unconfirmed balance: 0.00000000
```

Now stop the node and start it with mining enabled.



Wait before enough blocks are mined and make sure your wallet now has access to the premine funds:

```
      ======Wallets======
      Wallet: mywallet,            Confirmed balance: 100000000.00000000   Unconfirmed balance: 0.00000000
```





Congrats, you've created a new blockchain of your own!
