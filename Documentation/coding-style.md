C# Coding Style
===============

For non code files (xml etc) our current best guidance is consistency. When editing files, keep new code and changes consistent with the style in the files. For new files, it should conform to the style for that component. Last, if there's a completely new component, anything that is reasonably broadly accepted is fine.

The general rules:

1. We use [Allman style](http://en.wikipedia.org/wiki/Indent_style#Allman_style) braces, where each brace begins on a new line. A single line statement block can go without braces, one line statements are allowed (without braces) if it makes readability better. 
2. We use four spaces of indentation (no tabs).
3. We use `camelCase` for internal and private fields and use `readonly` where possible. When used static fields, `readonly` should come after `static` (i.e. `static readonly` not `readonly static`). Static readonly fields, visible or private, should begin with an upper case letter. Public fields should be used sparingly and should use PascalCasing with no prefix when used.
4. We always use `this.` to easily distinguish instance members.  
5. We always specify the visibility, even if it's the default (i.e.
   `private string foo` not `string foo`). Visibility should be the first modifier (i.e. 
   `public abstract` not `abstract public`).
6. Namespace imports should be specified at the top of the file, *outside* of
   `namespace` declarations and should be sorted alphabetically, with the exception of `System.*` namespaces, which are to be placed on top of all others.
7. Avoid more than one empty line at any time. For example, do not have two
   blank lines between members of a type.
8. Avoid spurious free spaces.
   For example avoid `if (someVar == 0)...`, where the dots mark the spurious free spaces.
   Consider enabling "View White Space (Ctrl+E, S)" if using Visual Studio, to aid detection.
9. If a file happens to differ in style from these guidelines (e.g. private members are named `_member`
   rather than `member`), change it to the guideline style.
10. We only use `var` when it's obvious what the variable type is (i.e. `var stream = new FileStream(...)` not `var stream = OpenStandardInput()`).
11. We use language keywords instead of BCL types (i.e. `int, string, float` instead of `Int32, String, Single`, etc) for both type references as well as method calls (i.e. `int.Parse` instead of `Int32.Parse`).
12. We use PascalCasing to name all our constant local variables and fields. The only exception is for interop code where the constant value should exactly match the name and value of the code you are calling via interop.
13. We use ```nameof(...)``` instead of ```"..."``` whenever possible and relevant.
14. Fields should be specified at the top within type declarations.
15. When including non-ASCII characters in the source code use Unicode escape sequences (\uXXXX) instead of literal characters. Literal non-ASCII characters occasionally get garbled by a tool or editor.
16. Use single line for simple propery setters and getters:  
`public int X { get; set; }`  
`public int X { get; private set; }`  
17. Avoid initializing instance properties outside of constructor.  
`public int MaxItems { get; set; } = 100000;`  
18. - Always provide a blank line after a curly brace that is not followed by another curly brace.  
    - Opening curly brackets must not be preceded or followed by blank line
    - Closing curly brackets must not be preceded by blank line
    - Code must not contain multiple blank lines in a row

   Therefore this is correct:
   ```
      if ()
      {
          block
      }
      [EMPTY LINE HERE]
      //more code here...
   ```

   And this is correct:
   ```
      if ()
      {
         foreach ()
         {
           block
         }      
      }
      [EMPTY LINE HERE]
      //more code here...
   ```
19. We declare only one type per file.  
    A few exceptions exist, which are permitted, although not enforced:
    - inclusion of small POCO classes if they're closely related in functionality (like API model classes)
    - inclusion of small classes, interfaces and enums inside the hosting file, if and only if they're only used by the hosting type
    The name of the file should be that of the hosting type.
20. We prefix all interfaces with an "I".
21. We suffix all asynchronous methods with "-Async".
22. We suffix extensions classes with "-Extensions" 
22. We add parentheses to group the parts of complex boolean expressions.  
    For example:
	```
	if ((x.Value > 5) && y && z.Eval() && ((a + b) > c)) // good
	bool q = (x.Value > 5) && y && z.Eval() && ((a + b) > c); // good
	if (x.Value > 5 && y) // parentheses needed to avoid confusion
	bool q = (x.Value > 5); // parentheses unnecessary
	```

24. We don't expose internal logic of the class (methods, fields, properties) just in order to test them. Find a way to expose them that doesn't requires access modifier changes in production code. If the method\field\property is not used or not expected to be used by any other component it shouldn't be public.

We have provided a Visual Studio 2017 EditorConfig file (`.editorconfig`) at the root of the full node repository, enabling C# auto-formatting and warnings conforming to some of the above guidelines.

### Example File:

``BlockPuller.cs:``

```
using NBitcoin.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using NBitcoin;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>
    /// Base class for pullers that download blocks from peers.
    /// <para>
    /// This must be inherited and the implementing class
    /// needs to handle taking blocks off the queue and stalling.
    /// </para>
    /// </summary>
    /// <remarks>
    /// There are 4 important objects that hold the state of the puller and that need to be kept in sync:
    /// <see cref="assignedBlockTasks"/>, <see cref="pendingInventoryVectors"/>, <see cref="downloadedBlocks"/>, 
    /// and <see cref="peersPendingDownloads"/>.
    /// <para>
    /// <see cref="downloadedBlocks"/> is a list of blocks that have been downloaded recently but not processed 
    /// by the consumer of the puller.
    /// </para>
    /// <para>
    /// When a typical consumer wants a next block from the puller, it first checks <see cref="downloadedBlocks"/>, 
    /// if the block is available (the consumer does know the header of the block it wants from the puller,
    /// if not, it simply waits until this information is available). If it is available, it is removed 
    /// from DownloadedBlocks and consumed. Otherwise, the consumer checks whether this block is being 
    /// downloaded (or soon to be). If not, it asks the puller to request it from the connect network peers.
    /// <para>
    /// Besides this "on demand" way of requesting blocks from peers, the consumer also tries to keep puller 
    /// ahead of the demand, so that the blocks are downloaded some time before they are needed.
    /// </para>
    /// </para>
    /// <para>
    /// For a block to be considered as currently (or soon to be) being downloaded, its hash has to be 
    /// either in <see cref="assignedBlockTasks"/> or <see cref="pendingInventoryVectors"/>.
    /// </para>
    /// <para>
    /// When the puller is about to request blocks from the peers, it selects which of its peers will 
    /// be asked to provide which blocks. These assignments of block downloading tasks is kept inside 
    /// <see cref="assignedBlockTasks"/>. Unsatisfied requests go to <see cref="pendingInventoryVectors"/>, which happens 
    /// when the puller find out that neither of its peers can be asked for certain block. It also happens 
    /// when something goes wrong (e.g. the peer disconnects) and the downloading request to a peer is not 
    /// completed. Such requests need to be reassigned later. Note that it is possible for a peer 
    /// to be operating well, but slowly, which can cause its quality score to go down and its work 
    /// to be taken from it. However, this reassignment of the work does not mean the node is stopped 
    /// in its current task and it is still possible that it will deliver the blocks it was asked for.
    /// Such late blocks deliveries are currently ignored and wasted.
    /// </para>
    /// <para><see cref="peersPendingDownloads"/> is an inverse mapping to <see cref="assignedBlockTasks"/>. Each connected 
    /// peer node has its list of assigned tasks here and there is an equivalence between tasks in both structures.</para>
    /// </remarks>
    public abstract class BlockPuller : IBlockPuller
    {
        /// <summary>Maximal quality score of a peer node based on the node's past experience with the peer node.</summary>
        public const int MaxQualityScore = 150;

        /// <summary>Minimal quality score of a peer node based on the node's past experience with the peer node.</summary>
        public const int MinQualityScore = 1;

        /// <summary>Instance logger.</summary>
        protected readonly ILogger logger;

        /// <summary>Lock protecting access to <see cref="assignedBlockTasks"/>, <see cref="pendingInventoryVectors"/>, <see cref="downloadedBlocks"/>, and <see cref="peersPendingDownloads"/></summary>
        private readonly object lockObject = new object();

        /// <summary>
        /// Hashes of blocks to be downloaded mapped by the peers that the download tasks are assigned to.
        /// </summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<uint256, BlockPullerBehavior> assignedBlockTasks;

        /// <summary>List of block header hashes that the node wants to obtain from its peers.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Queue<uint256> pendingInventoryVectors;

        /// <summary>List of unprocessed downloaded blocks mapped by their header hashes.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<uint256, DownloadedBlock> downloadedBlocks;

        /// <summary>Number of items in <see cref="downloadedBlocks"/>. This is for statistical purposes only.</summary>
        public int DownloadedBlocksCount
        {
            get
            {
                lock (this.lockObject)
                {
                    return this.downloadedBlocks.Count;
                }
            }
        }

        /// <summary>Sets of block header hashes that are being downloaded mapped by peers they are assigned to.</summary>
        /// <remarks>All access to this object has to be protected by <see cref="lockObject"/>.</remarks>
        private readonly Dictionary<BlockPullerBehavior, HashSet<uint256>> peersPendingDownloads = new Dictionary<BlockPullerBehavior, HashSet<uint256>>();

        /// <summary>Collection of available network peers.</summary>
        protected readonly IReadOnlyNodesCollection Nodes;

        /// <summary>Best chain that the node is aware of.</summary>
        protected readonly ConcurrentChain Chain;

        /// <summary>Random number generator.</summary>
        private Random Rand = new Random();

        /// <summary>Specification of requirements the puller has on its peer nodes to consider asking them to provide blocks.</summary>
        private readonly NodeRequirement requirements;
        /// <summary>Specification of requirements the puller has on its peer nodes to consider asking them to provide blocks.</summary>
        public virtual NodeRequirement Requirements => this.requirements;

        /// <summary>Description of a block together with its size.</summary>
        public class DownloadedBlock
        {
            /// <summary>Size of the serialized block in bytes.</summary>
            public int Length;

            /// <summary>Description of a block.</summary>
            public Block Block;
        }

        /// <summary>
        /// Initializes a new instance of the object having a chain of block headers and a list of available nodes. 
        /// </summary>
        /// <param name="chain">Chain of block headers.</param>
        /// <param name="nodes">Network peers of the node.</param>
        /// <param name="protocolVersion">Version of the protocol that the node supports.</param>
        /// <param name="loggerFactory">Factory to be used to create logger for the puller.</param>
        protected BlockPuller(ConcurrentChain chain, IReadOnlyNodesCollection nodes, ProtocolVersion protocolVersion, ILoggerFactory loggerFactory)
        {
            this.Chain = chain;
            this.Nodes = nodes;
            this.logger = loggerFactory.CreateLogger(this.GetType().FullName);
            this.downloadedBlocks = new Dictionary<uint256, DownloadedBlock>();
            this.pendingInventoryVectors = new Queue<uint256>();
            this.assignedBlockTasks = new Dictionary<uint256, BlockPullerBehavior>();

            // set the default requirements
            this.requirements = new NodeRequirement
            {
                MinVersion = protocolVersion,
                RequiredServices = NodeServices.Network
            };
        }

        /// <inheritdoc />
        public virtual void PushBlock(int length, Block block, CancellationToken token)
        {
            uint256 hash = block.Header.GetHash();

            DownloadedBlock downloadedBlock = new DownloadedBlock()
            {
                Block = block,
                Length = length,
            };

            lock (this.lockObject)
            {
                this.downloadedBlocks.TryAdd(hash, downloadedBlock);
            }
        }
        
        /// <summary>
        /// Constructs relations to peer nodes that meet the requirements.
        /// </summary>
        /// <returns>Array of relations to peer nodes that can be asked for blocks.</returns>
        /// <remarks>TODO: https://github.com/stratisproject/StratisBitcoinFullNode/issues/246</remarks>
        /// <seealso cref="requirements"/>
        private BlockPullerBehavior[] GetNodeBehaviors()
        {
            return this.Nodes
                .Where(n => this.requirements.Check(n.PeerVersion))
                .SelectMany(n => n.Behaviors.OfType<BlockPullerBehavior>())
                .Where(b => b.Puller == this)
                .ToArray();
        }

        /// <summary>
        /// Assigns a pending download task to a specific peer.
        /// </summary>
        /// <param name="peer">Peer to be assigned the new task.</param>
        /// <param name="blockHash">If the function succeeds, this is filled with the hash of the block that will be requested from <paramref name="peer"/>.</param>
        /// <returns>
        /// <c>true</c> if a download task was assigned to the peer, <c>false</c> otherwise, 
        /// which indicates that there was no pending task.
        /// </returns>
        internal bool AssignPendingDownloadTaskToPeer(BlockPullerBehavior peer, out uint256 blockHash)
        {
            blockHash = null;

            lock (this.lockObject)
            {
                if (this.pendingInventoryVectors.Count > 0)
                {
                    blockHash = this.pendingInventoryVectors.Dequeue();
                    this.assignedBlockTasks.Add(blockHash, peer);

                    AddPeerPendingDownloadLocked(peer, blockHash);
                }
            }

            bool res = blockHash != null;
            return res;
        }
    }
}

```
