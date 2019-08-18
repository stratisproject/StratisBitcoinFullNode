## COLD STAKING SETUP INSTRUCTIONS

**What you will need** 

 - **Hot wallet** - this will be used for staking online, you can skip this if you are using a cold staking service
 - **Cold wallet** - this will hold your funds offline
   
_Please note. It's important that both wallets are used with separate full nodes instances, and are not present in the same data folder._

---
****Hot Wallet****

_If you are using a cold staking service, your **coldstakinghotaddress** will be provided for you on their website and you can skip this section._

**1.1.** Convert your Hot wallet to be enable cold staking by using the the `cold-staking-account` API method with isColdWalletAccount set to "false" <br />
**1.2.** Get your Hot wallets coldstakinghotaddress using by using the `cold-staking-address` API method with "IsColdWalletAddress" set to "false"<br />
**1.3.** Then start the node staking with the Hot wallet from the command line or config file.
 
 ---
****Cold wallet****

**2.1.** Fund the Cold wallet with coins that you eventually want to stake, these coins should go into the standard "account 0".<br />
**2.2.** Convert your Cold wallet to be enabled for cold staking by using the `cold-staking-account` API method with "isColdWalletAccount" set to "true"<br />
**2.3.** Get your Cold wallets coldstakingcoldaddress using by using the `cold-staking-address` API method with "IsColdWalletAddress" set to "true"<br />
**2.4.** Then, call the `setup-cold-staking` API to build the transaction, linking the funds in your Cold Wallet Address (Step #2.3) to the Hot Wallet Address (Step #1.2). This will return the hex that you use in the next step.

```
{
  "coldWalletAddress": "<<coldstakingcoldaddress>>",
  "hotWalletAddress": "<<coldstakinghotaddress>>",
  "walletName": "<<coldwalletname>>",
  "walletPassword": "<<coldwalletpassword>>",
  "walletAccount": "account 0",
  "amount": "<<amount to stake>>",
  "fees": "0.0002"
}
```

**2.5.** Finally, you use the "send-transaction" API to broadcast the transaction from step #2.4.

---

## To withdraw funds back to your regular wallet

**3.1.** From the PC running your cold wallet, call the "cold-staking-withdrawal" API to build a transaction and return coins from the Hot Wallet Address to Cold Wallet Address (account 0). This will return the hex that you use in the next step.

```
{
  "receivingAddress": "<<cold wallet address/ account 0>>",
  "walletName": "<<coldwalletname>>",
  "walletPassword": "<<coldwalletpassword>>",
  "amount": "<<amount to to return>>",
  "fees": "0.0001"
}
```

**3.2.** Then simply use the "send-transaction" API to broadcast the transaction hex from step #3.1.