# Logging Style

This document describes a specific logging style that we use that helps us having as useful logs as possible. 


## Logging Levels

There are three basic settings of logging that we commonly use:

 * default production mode - logging level is set to INFO;
 * debug mode - logging level is set to DEBUG, at least for some components;
 * deep development mode - logging level is set to TRACE, at least for some components.

In our current logging setup, all logs at INFO and higher levels are also written to the console. This means that in the default mode, all you see in the console is all that is logged.
The debug mode can be enabled using `debug` configuration option (see [Using Logging document](./using-logging.md)) and if it is enabled, DEBUG level logs will be also sent to the console.
The TRACE logging level can only be enabled in `NLog.config` file (again, see [Using Logging document](./using-logging.md)) and are only logged to files.

This setup implies which logging level should each logging line have:

 * use INFO level for something that the end user is supposed to see;
 * use DEBUG level for logs that contain very important information for developers, but avoid using DEBUG level for logs that appear often;
 * use TRACE level logs for any detailed logging;
 * higher levels - WARN, ERROR, FATAL - are also sent to the console, so use them accordingly. 
   
The rest of the document mostly describes style of TRACE level logs. It is up to the developer, which logs will be promoted to DEBUG or higher levels.


## Purpose

The purpose of TRACE level logs is to record the execution flow in a way it can later be inspected. This implies that the logs need to contain enough 
information and have a good degree of coverage. **Good logs of execution flow allow the reader to see the exact execution path of each object instance in particular component.**

Consider the following example:

```
while (true)
{
    uint256 hash = await this.UTXOSet.Rewind();
    ChainedBlock rewinded = this.Chain.GetBlock(hash);
    if (rewinded == null)
        continue;

    this.Tip = rewinded;
    this.Puller.SetLocation(rewinded);
    break;
}
```

Where should we introduce the logs in this code in order to always catch the whole execution flow? It is not enough to log just the exit state of the loop as we would 
lose the information about how many times the rewind operation was made. Therefore we use two logs in this code:

```
while (true)
{
    uint256 hash = await this.UTXOSet.Rewind();
    ChainedBlock rewinded = this.Chain.GetBlock(hash);
    if (rewinded == null)
    {
        this.logger.LogTrace("Rewound to '{0}', which is still not a part of the current best chain, rewinding further.", hash);
        continue;
    }

    this.logger.LogTrace("Rewound to '{0}'.", hash);
    this.Tip = rewinded;
    this.Puller.SetLocation(rewinded);
    break;
}
```

Logs from this code will contain hash of each block after each rewind operation as well as at the end of the loop.
**In general, each important execution path branch should be distinguishable from any other important execution path branch in the logs.**
Which execution path is important is up to the developer to decide. This is why it is always better if the author of the code 
is the one who introduces the logs to the code. Adding logs to the code later may lead to excessive logging of unimportant branches, 
or not enough logging of important branches. Note that in order to distinguish two branches, you only need a log in one of them.
Sometimes, you will find it useful to have a log in both branches, sometimes just having the log in the branch that is executed LESS often
will be enough. This can be seen in the following example:

```
VersionPayload version = node.PeerVersion;
if (version != null)
{
    TimeSpan timeOffset = version.Timestamp - this.dateTimeProvider.GetUtcNow();
    if (timeOffset != null) this.state.AddTimeData(address, timeOffset, node.Inbound);
}
else this.logger.LogTrace("Node '{0}' does not have an initialized time offset.", node.RemoteSocketEndpoint);                    

```

Note that in the context of this example, most `node` object do have `PeerVersion` set. Rarely it is not set. Therefore it is not necessary 
to explicitly mention that a particular node was "normal". Only if the node was abnormal, we will see it in the log file due to the log line 
in the else branch. If the node is normal, no log will be created and thus the reader knows it was normal.


## Method Entry/Exit Logs

Entry and exit logs are added automatically on compilation by fody weaver and therefore should not be added manually except for exit markers (`(-)[THIS_IS_MARKER]`).

This section is outdated but for historical reasons it's kept as it is in case we would like to return to manual entry\exit logs.



All important methods, which are almost all methods, except for methods that do very little, are very simple (trivial), and are called very often, 
should have special method entry and exit logs. These logs have a unified structure and provide an uniform looking logs that are very easy 
to read. A method is considered to be called too often, if it is called in a loop, in which the same set of objects are being used. 
However, if different objects are being used in each call, the method does not necessarily qualify as being called too often. 
Thus calling a method that processes a transaction in block can't be considered as called too often even if a block contains 1000 transactions.
Each transaction is different and we can very much be interested in how each transaction is processed. However, if there is a method 
that is called 100 times for each such transaction and only calculates and updates some internal value, that might be considered as 
a method that is called too often. The final decision is, as always, on the developer.

Each such method has a single entry log and one or more exit logs. An entry log is always placed at the very beginning of the method's code 
and only method guards and reusable variables that will also go into the entry log can be put in front of the entry log. The entry log format is as follows:

```
"(arg1:value1,arg2:value2,...)"
```

Example of an entry log:

```
private void AttachedNode_MessageReceived(Node node, IncomingMessage message)
{
    this.logger.LogTrace("({0}:'{1}',{2}:'{3}')", nameof(node), node.RemoteSocketEndpoint, nameof(message), message.Message.Command);
```

The intention here is to identify each object passed to the method, so that the reader of the logs can track the object through the execution flow.
This will produce the following log:

```
...AttachedNode_MessageReceived (node:'[::ffff:51.141.28.47]:26178',message:'block')
```

**In general, we try to log all input arguments to the method.** Usually, some kind of name or identifier is available, sometimes it is a pair of values 
(e.g. UTXO ID is a pair of transaction's ID and transaction output number). Sometimes, the input argument does not have such an ID, in which case 
we can decided not to log it (if that is not very important argument), or we may decide to use `GetHashCode` method. There is one exception 
to this general rule - we never log sensitive data such as passwords or cryptographic seeds.

Each entry log must have its matching exit log. This means that the method should have as many exit logs as there are ways to exit the method. 
The only exception to this entry-exit pairing are unhandled exceptions coming from methods that the method calls. The format of an exit log is as follows:

```
"(-):resultValue,outarg1=outval1,outarg2=outval2,..."
```

We log result value and values of all output arguments (same rules as for the input arguments) and in case the method changes a global state 
variable (or object state variable), we also consider such value as an output argument. A very common example of exit log:

```
    this.logger.LogTrace("(-):{0}", res);
    return res;
}
```

If a return variable is a structure, we only log some useful part of it. In this case, we use `*` that represents the returned object:

```
    FetchCoinsResponse result = null;

    ...


    this.logger.LogTrace("(-):*.{0}='{1}',*.{2}.{3}={4}", nameof(result.BlockHash), result.BlockHash, nameof(result.UnspentOutputs), nameof(result.UnspentOutputs.Length), result.UnspentOutputs.Length);
    return result;
}
```

This will produce the following log:

```
...FetchCoinsAsync (-):*.BlockHash='838b8efcb78c693087fc39dd0b8648e5bde92c019633be5340b7fe2629e839b6',*.UnspentOutputs.Length=1064
```

**We always want each log to be unique.** Therefore, if a method have more than 1 way of exit, we need to distinguish its exit logs. We do that by introducing a context 
identifier to all non-default exit logs and sometimes to all exit logs. The format of the exit log with context identifier is as follows:


```
"(-)[CONTEXT_NAME_ALL_CAPS]:resultValue,outarg1=outval1,outarg2=outval2,..."
```

Here is an example of that:

```
Block block = context.BlockResult.Block;

if (!BlockStake.IsProofOfStake(block))
{
    this.logger.LogTrace("(-)[NOT_POS]");
    return;
}

// Verify hash target and signature of coinstake tx.
BlockStake prevBlockStake = this.stakeChain.Get(chainTip.HashBlock);
if (prevBlockStake == null)
{
    this.logger.LogTrace("(-)[NO_PREV_STAKE]");
    ConsensusErrors.PrevStakeNull.Throw();
}
```

We can see two different ways of exiting the method. One simply returns and other throws. There are no output arguments or result values,
but there is a context identifier that allows the reader to distinguish which exit was used.

With both entry and exit logs we use nameof() to put names of arguments in the log. This makes it easy to refactor the code and avoid 
having old names in logs.

Method entry and exit logs are always logged on TRACE level.


## Other Logs

Non-entry and non-exit logs are used to give us some extra information about the execution flow or the state of the object or system.
The format of these logs is

```
"Not very complex, but unique, English sentence."
```

We do not use '[CONTEXT]' context identifier as the context is given by the sentence itself. We also do not use `nameof()` because we rather describe 
the event in English. We also don't use `"name:value"` notation or `"name=value"`. Example:

```
this.logger.LogTrace("UTXO '{0}/{1}' with value {2} might be available for staking.", stakeTx.OutPoint.Hash, stakeTx.OutPoint.N, utxo.Value);
```



## Value Log Format

Examples in this section are mostly for method exit logs, but the formatting of the values inside the log applies to all logs.
All value types, small integer types, enums, booleans etc. are logged without any apostrophes or quotes. 

```
bool res = false;

...

this.logger.LogTrace("(-):{0}", res);
```

In case of strings or block hashes or similar values, we use apostrophes:

```
uint256 blockHash;

...

this.logger.LogTrace("(-):'{0}'", blockHash);
```

For integer flags (not enums), bits, or other hex values, we use `0x` prefix and the composite format string:


```
uint flags = FLAG_1 | FLAG_3;

...

this.logger.LogTrace("(-):0x{0:x}", flags);
```

For date and time, we use the composity format string:

```
DateTime date = DateTime.UtcNow;

...

this.logger.LogTrace("(-):{0:yyyy-MM-dd HH:mm:ss}", date);
```

In case of collections, lists, arrays, sets, etc. we usually only log the number of items they contain. This is especially common for input arguments of such types:

```
private bool ReleaseDownloadTaskAssignmentLocked(Dictionary<uint256, DownloadAssignment> peerPendingDownloads, uint256 blockHash)
{
    this.logger.LogTrace("({0}.{1}:{2},{3}:'{4}')", nameof(peerPendingDownloads), nameof(peerPendingDownloads.Count), peerPendingDownloads.Count, nameof(blockHash), blockHash);

```

As a general rule, we try the log to be as light as possible and as such, it should not enumerate all the items in a collection unless this information is considered 
as very important. In case we need the log to be somewhat heavy, we should use `IsEnabled` method of the logger:

```
if (this.logger.IsEnabled(LogLevel.Debug))
{
    foreach (Item item in list)
        this.logger.LogDebug("... something about each item ...");
}

```

In some cases, we do want to log all items of a collection. In that case we use square brackets and comma as a separator:


```
int[] arr = new int[] { 1, 3, 5 };

...

this.logger.LogTrace("(-):[{0}]", string.Join(",", arr));
```



Note that with this method you have to be careful to check the same logging level as it is then used.

As stated and explained in [Using Logging document](./using-logging.md), do not use interpolated strings in logs.
