# Curl examples for the RPC API

## Get a new unused address:

This should return a new unused address:

```sh
curl rpcuser:rpcpassword@localhost:4334 -X POST --data-binary "{'jsonrpc': '1.0', 'id': 'curltest', 'method': 'getnewaddress', 'params': [''] }"
```

```json
{"result":"CdN8NWmmrXnjnX3fL8QKwn29bxQRCCPt4W","id":1,"error":null}
```

Note: The params can be either empty array [] or with empty string ['']..

This should fail with an error message

```sh
curl rpcuser:rpcpassword@localhost:4334 -X POST --data-binary "{'jsonrpc': '1.0', 'id': 'curltest', 'method': 'getnewaddress', 'params': ['myaccount'] }"
```

```json
{
  "result": null,
  "error": {
    "code": -32,
    "message": "Use of 'account' parameter has been deprecated"
  }
}
```

## Get node and wallet info

This should return node info:

```sh
curl rpcuser:rpcpassword@localhost:4334 -X POST --data-binary "{'jsonrpc': '1.0', 'id': 'curltest', 'method': 'getinfo' }"
```

```json
{"result":{"version":3000001,"protocolversion":70012,"blocks":60196,"timeoffset":-1,"connections":7,"proxy":"","difficulty":211089.12333191774,"testnet":false,"relayfee":0.0001,"errors":""},"id":1,"error":null}
```

Here is an example for getwalletinfo. The params should specify the wallet, but if excluded or set to empty string, will still work.:

```sh
curl rpcuser:rpcpassword@localhost:4334 -X POST --data-binary "{'jsonrpc': '1.0', 'id': 'curltest', 'method': 'getwalletinfo', 'params': ['default'] }"
```` 

```json
{"result":{"walletname":"default.wallet.json","walletversion":1,"balance":0,"unconfirmed_balance":0,"immature_balance":0},"id":1,"error":null}
```


## listtransactions alternative

Currently the RPC API does not support the "listtransactions", alternative is to use the REST API, example:

```sh
curl -X GET "http://localhost:4335/api/Wallet/history?WalletName=default&AccountName=account%200" -H "accept: application/json"
```
