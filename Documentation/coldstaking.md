# Cold staking


## Motivation

In order to produce blocks on Stratis network, a miner has to be online with running node and have its wallet open. This is necessary because at each time slot, the minor is supposed to check whether 1 of its UTXOs is eligible to be used as so-called coinstake kernel input and if so, it needs to use the private key associated with this UTXO in order to produce the coinstake transaction.

The chance of a UTXO to be eligible for producing a coinstake transaction grows linearly with the number of coins that this UTXO presents.

This implies that the biggest miners on the network are required to keep the coins in a hot wallet. This is dangerous in case the machine where the hot wallet runs is compromised.

We propose cold staking, which is mechanism that eliminates the need to keep the coins in the hot wallet. With cold staking implemented, the minor still needs to be online with running the node and open wallet, but the coins that are used for staking, can be safely stored in cold storage. Therefore the open hot wallet does not need to hold any significant amount of coins, or it can even be completely empty.


## User interface flow

We want to implement cold staking in a way that allows even inexperienced users to participate in production of new blocks. Since all the logic is implemented on the node side and the wallet is only using interfaces provided by the node, it is sufficient to describe the flow from inexperienced users point of view. Advanced users can then easily use the underlying functionality of the node.

We expect the user to interact with graphic user interface of its hot wallet, which should be HD wallet. The interface should provide a new tab, transaction option or button, which is designed for cold staking. We also expect the user to have any kind of cold storage wallet, such as hardware wallet or paper wallet et cetera. When the user wants to start cold staking, it should be asked to create a new transaction that will send the coins to the cold storage wallet. This special transaction will be constructed in a way that the coins will be safely transferred to the code storage while the hot wallet sending this transaction will still be able to participate in mining activity.

The user should also be allowed to cancel the setup at any time, especially when its hot wallet is compromised. In order to cancel the setup, the user only needs to send the coins out from the cold storage UTXO that was created with the special transaction. Therefore any move of the coin from cold storage to hot wallet or even another address in cold storage is sufficient to cancel the setup.

Additionally, we would like to support a 2nd flow of enabling the cold staking. In this case, we want it to be possible for coins to be stored already inside a hardware wallet and not to leave it while enabling the cold staking. This is done by creating the special transaction (see below) and let the hardware wallet to sign it, while using an address from a hot wallet in the 1st `OP_IF` branch and another address in the hardware wallet in the 2nd branch. 

In this 2nd flow, once the cold staking setup is created inside the cold wallet, the user needs to inform its hot wallet about the setup, so that the hot wallet can then start staking. For this, the hot wallet needs to have a new interface, which will allow the user to specify the transaction ID of the cold staking setup made with the cold wallet. The hot wallet can then verify the outputs of this transaction to see if any of its address was allowed to stake with cold wallet coins.


## Wallet UTXOs tracking

When a hot wallet creates and propagates the special transaction that sends the coins to the cold storage while preserving the possibility to stake with the hot wallet key, the hot wallet needs to start tracking the cold storage UTXO as if it was one of its own UTXOs. This serves 2 purposes. The 1st one is natural, when a hot wallet creates new coinstake transaction using this UTXO, it has to be aware that this UTXO has been spent and new UTXO is now there to be watched. 

The 2nd purpose is to detect that the cold staking setup was cancelled when the cold staking key was used to move the coin.

Similarly, the hot wallet needs to track the cold storage UTXOs from the 2nd flow cold staking setup.


## Cold wallet requirements

Any wallet that wants to support cold staking, needs to implement recognition of the new template transaction as described in the next section. When a transaction in such a format is detected on the chain that allows the cold wallet to manipulate new coins, it should inform the user about this transaction anyway that clearly distinguish this transaction from normal payments to this wallet. The user must be fully aware that such a transaction is specially designed for cold staking setup so that the user cannot be misled into thinking that such a transaction was provided as a payment from other party. This distinction is necessary to prevent attackers to send coins as payments for goods while keeping staking rights to themselves. While the wallet will be able to fully control the next movement of the coins, the delegation of staking rights must also be considered as a security risk.

Cold wallet should not mix coins from the cold staking setup with other coins, because any movement of cold staking setup coin cancels the setup. The wallet interface should provide a mechanism to cancel the cold staking setup and merge the coins with other coins in the wallet.

These requirements are similar as if you considered a wallet that supports multi-signature transactions and someone sent you 1 of 2 multi-signature transaction, in which the coin could be moved either by your key or the key of the other party. The wallet should not mix such a coin with other coins that are fully under the wallet control.


### OP_CHECKCOLDSTAKEVERIFY

We propose to introduce cold staking to Stratis using a soft fork mechanism that changes the behaviour of OP_NOP10 script instruction, newly renamed to `OP_CHECKCOLDSTAKEVERIFY`. 

The new behaviour of this opcode is as follows:

- Check if the transaction spending an output, which contains this instruction, is a coinstake transaction. If it is not, the script fails.

- Check that ScriptPubKeys of all inputs of this transaction are the same. If they are not, the script fails.

- Check that ScriptPubKeys of all outputs of this transaction, except for the marker output (a special first output of each coinstake transaction) and the pubkey output (an optional special second output that contains public key in coinstake transaction), are the same as ScriptPubKeys of the inputs. If they are not, the script fails.

- Check that the sum of values of all inputs is smaller or equal to the sum of values of all outputs. If this does not hold, the script fails

- If the above-mentioned checks pass, the instruction does nothing.

Having the instruction behaviour defined as above, when a hot wallet wants to create a coinstake transaction using cold staking, the ScriptPubKey of its output will look as follows:

`OP_DUP OP_HASH160 OP_ROT`

`OP_IF` 

`OP_CHECKCOLDSTAKEVERIFY <Hash(hotPubKey)>` 

`OP_ELSE` 

`<Hash(coldPubKey)>` 

`OP_ENDIF`

`OP_EQUALVERIFY OP_CHECKSIG`

the corresponding ScriptSig to spend such an output in order to produce another coinstake using hot wallet is going to be:

`<sig> 1 <hotPubKey>`

In the corresponding ScriptSig to spend such an output in order to cancel the setup using the cold storage private key is going to be: 

`<sig> 0 <coldPubKey>`

It follows that the private key for hotPubKey is stored inside the hot wallet and the private key for coldPubKey is stored in the cold storage. Therefore the new instruction protects the branch of the script that uses the hot wallet key in the way that it cannot be used for anything as except for creating another coinstake  transaction. Moreover, due to the limitation on the values of outputs, the attacker cannot even burn the coins in the cold storage if they gain access to the hot wallet keys.


## Change in validation and staking

Each proof of stake block contains a signature of block header. During the validation, we extract the public key from the 2nd coinstake output to check that the block signature is signed with corresponding private key. Currently only P2PK transaction template is supported. Newly, the validation code must be able to extract the public keys from the cold staking transaction.

Similarly, when node wants to stake, it has to newly understand those special transaction outputs created for cold staking and be able to create a valid cold coinstake transaction.


## Risks and impacts

We expect that implementation of cold staking would allow more people to participate in the staking process without fear of the hot wallet been compromised and their coins stolen. Currently estimate that about 30% of all coins are used for staking, while 70% of all coins are not. Having more people and more coins participate in the staking process will make the whole network stronger against certain kinds of attacks.

Moreover, as we see that some exchanges are using the coins of their customers for staking, This presents a huge risk to the whole Stratis economy, should Such an exchange be Hacked, because they need to be present in the hot wallet without cold staking implemented. Cold staking does enable to and eliminate this risk.

On the other hand, implementation of cold staking would allow to implement a pool staking, which was previously not possible without huge risks for the participants of such Pools. Pool staking by itself presents new dangerous to the network some attack vectors are stronger when higher stake is used to perform the attacks. This means that cold staking suddenly enable more stronger attacks. Currently we consider this disadvantage not to overweight the benefits of cold staking. Therefore, we recommend to implement it.


## Compatibility

As we proposed implementation to be done using soft fork, All wallets and nodes including  unupgraded wallet and nodes will still able to track the network even after the soft fork activates.

All miners will be recommended to upgrade to the new software that understands the soft fork change. However, even miners are allowed not to upgrade and in most cases that would cause no harm to them or the network.The only problem that can arise is when someone attempts to spend the cold staking transaction using the hot wallet key against the new rules the new opcode prescribes. If a block is mined that contains such an invalid transaction according to the new rules, unupgraded miners could prolong the chain Containing this block, which would be considered invalid by all upgraded nodes.Only in this case not upgrading causes a problem and it only causes a problem to a non/upgraded miner. All other parts of the infrastructure, upgraded or not, won't be affected.

## References 
https://talk.peercoin.net/t/cold-storage-minting-proposal/2336
