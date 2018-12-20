# Additional Test Tools

This folder contains some additional test tools for City Chain. While the majority of testing is done through .NET and Unit Tests / Integration Tests, there are some additional tests and tools that we use and those can be placed in this folder.

## Exchange RPC integration suite

This is a NodeJS based test suite that validates RPC calls that are used by exchanges.

Make sure you run the City Chain daemon with RPC server enabled. Launch with these parameters:

```
-server -rpcallowip=127.0.0.1 -rpcpassword=rpcpassword -rpcuser=rpcuser -txindex=1
```

Note: Set the rpcallowip to 0.0.0.0 if you want everyone on the network to call your RPC server. Or specific IP addresses. Can be specified multiple times.

To run the tests, navigate to the "exchange-rpc" folder and run from shell:

```sh
npm install
npm run test
```

If you receive an output with:

code: 'ECONNREFUSED'

That means you likely have not started the daemon, the port is wrong, or the configuration on allowed IP is wrong.

If everything works, the first log output should be "info: " followed by a JSON output with results from the rpc "getInfo" call.

