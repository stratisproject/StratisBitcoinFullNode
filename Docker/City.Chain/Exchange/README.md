# City Chain Docker: Exchange Sample

This is a very basic sample on how to startup a City Chain daemon with a default wallet, and having it automatically unlock.

This is very similar behavior to running a Bitcoin Core daemon which has a default wallet.

## Security

This sample will expose port 4334 (RPC API) and 4335 (REST API) and bind those to the networks 0.0.0.0 and be callable by other 
machines that can access the network of the docker container. The APIs will run with life hot-wallets that are unlocked, and
anyone who can perform an HTTP REQUEST to the docker container, will be able to perform transactions and send coins.

YOU ARE RESPONSIBLE FOR YOUR SECURITY, this is the easiest and most basic way of getting starting, so it is not really the most
secure way of running a hot wallet.

- Make sure you don't use the default wallet password.
- Make sure you don't use the default RPC password.
- Make sure you don't expose any endpoint to machines that should not have access.

## Samples with comments

Here are the files you can use to run the default wallet node easily, they are also available in the repo.

[docker-compose.yml](docker-compose.yml) file:
```
version: '2'
services:
 citycoin:
    # Always use specific image version, check what the latest is.
    image: citychain/citychain:1.0.21
    volumes:
      - ./.citycoin:/root/.citychain
      - ./citycoin/city.conf:/root/.citychain/city/CityMain/city.conf
    ports:
        - 4334:4334 # RPC API
        - 4335:4335 # REST API
```

[citycoin/city.conf](citycoin/city.conf) file:

```
# Makes the node run as server and accept RPC calls.
server=1

# Make a full index of the blockchain database, making it possible to perform more queries in the APIs.
txindex=1

# Allow specific IP or subnets to perform calls to the RPC API.
rpcallowip=127.0.0.0/16

# Bind the REST API to the default network adapters. There is IP filter restriction, so beware that this can be used to create transactions (transfer coins), etc.
apiuri=http://0.0.0.0:4335/

# Username for the RPC API.
rpcuser=rpcuser

# Password for the RPC API.
rpcpassword=rpcpassword

# Name of the default wallet, to either generate or to load (if exists).
defaultwalletname=default

# Password for the default wallet, to be able to unlock it on startup.
defaultwalletpassword=default

# Indicates if the wallet should be unlocked by default.
unlockdefaultwallet=true

# Defines a shell task to be execute when any transactions related to the wallet is observed.
walletnotify=curl -X POST -d txid=%s http://localhost:9999/
```

## Run the sample

Download the docker-compose.yml file and the city.conf (must be within the citycoin sub-directory).

```
docker-compose up
```

This will start an interactive session with the City Chain node, and you can exit at any time pressing CTRL-C.

If you want to run the node as a background daemon (container), add the -d (detach) option.

```
docker-compose up -d
```

At startup, you should be able to verify some configuration which are logged to the console:

```
citycoin_1  | info: Stratis.Bitcoin.FullNode[0]
citycoin_1  |       Starting node.
citycoin_1  | info: Stratis.Bitcoin.Features.Api.ApiFeature[0]
citycoin_1  |       API starting on URL 'http://0.0.0.0:4335/'.

citycoin_1  | info: Stratis.Bitcoin.Features.RPC.RPCFeature[0]
citycoin_1  |       RPC Server listening on:
citycoin_1  |       http://[::]:4334/
citycoin_1  |       http://0.0.0.0:4334/

citycoin_1  | info: Stratis.Bitcoin.Connection.ConnectionManager[0]
citycoin_1  |       Node listening on:
citycoin_1  |       0.0.0.0:4333
```

Also in the startup log, you should verify that your configured -walletnotify option is parsed and configured correctly.

```
citycoin_1  | info: Blockcore.Features.WalletNotify.WalletNotifyFeature[0]
citycoin_1  |       -walletnotify was configured with command: curl -X POST -d txid=%s http://localhost:9999/.
citycoin_1  | info: Blockcore.Features.WalletNotify.WalletNotifyFeature[0]
citycoin_1  |       -walletnotify was parsed as: curl -X POST -d txid=%s http://localhost:9999/
```

The first log entry outputs exactly what the input was, while the second log entry the input has been parsed and prepared for execution when an transaction is observed.

## Backup of wallet

When you run the default wallet commands, a new file is automatically added to the disk that contains the default wallet. Since this is automatically generated, you will never be able to recover the recovery phrase (mnemonic) for this wallet.

It is therefor very important to take a physical copy of the generated wallet file and keep it very safe, never store it on unencrypted harddrives.

The wallet file should be located here:

```
.citycoin\city\CityMain\default.wallet.json
```

Make a backup of this file before starting to actively transfer any coins to the wallet. If the docker container is corrupted, deleted, or the volume (depending on your setup) is not persisted, you will loose all coins that has been transfered to the node's wallet.

## Reset wallet

If the wallet for any reason fails to observe transactions or has other issues with the transaction history, a rescan can be performed. A rescan can also be used to verify the -walletnotify, as it will re-run the shell executions whenever it rescans and finds historical transactions.

```
curl -X DELETE "http://localhost:4335/api/Wallet/remove-transactions?WalletName=default&all=true&ReSync=true" -H "accept: application/json"
```

## RPC: getnewaddress

Previously the "getnewaddress" RPC method returned the last unused address. That meant it would return the same address multiple times, until an address was used. This behavior has changed since latest releases, and the RPC method will now return a unique new address on every request. This is more aligned with Bitcoin Core and simplifies integrations.

## REST API: Documentation and testing

When running the node software, it exposes an OpenAPI documentation portal that includes tooling to perform testing. It is available by default on the URL: http://localhost:4335/swagger/index.html

## RPC API: Documentation and testing

To perform testing with the RCP API, it is adviced to use curl. You can reference the Bitcoin RPC specification to learn more about the RPC specification, but beware that the node is not 100% compliant with Bitcoin Core.

Example documentation for sendtoaddress, which can be used to move coins:

https://bitcoincore.org/en/doc/0.18.0/rpc/wallet/sendtoaddress/