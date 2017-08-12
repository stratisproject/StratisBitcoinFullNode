BlockStoreLoop

    The BlockStoreLoop simultaneously finds and downloads blocks and stores them in the BlockRepository.

Initialize()
-----------------------------

    Initializes the BlockStore.

    If StoreTip is null, the store is out of sync.
         
    This can happen when:
        1: The node crashed
        2: The node was not closed down properly
             
    To recover we walk back the chain until a common block header is found and set the BlockStore's StoreTip to that.

AddToPending()
-----------------------------

    Adds a block to Pending Storage

    The BlockStoreSignaler calls AddToPending.

    Only add the block to pending storage if:
        1: The block does exist on the chain
        2: The store's tip is less than the block to add's height

Flush()
-----------------------------

    Flush the BlockStore by calling DownloadAndStoreBlocks with disposeMode of true. This happens when the node shuts down.


DownloadAndStoreBlocks()
-----------------------------

    Finds and downloads blocks to store in the Block Repository

    This method executes a chain of steps in order:
        1: Reorganise the repository
        2: Check if the next chained block exists
        3: Process the blocks in pending storage
        4: Find and download blocks

    All the steps return a BlockStoreLoopStepResult which either signals the While loop
    to break or continue execution.


DownloadAndStoreBlocks() : ReorganiseBlockRepositoryStep
-----------------------------

    This will happen when the BlockStore's tip does not match the next chained block's previous header.
    
    Steps:
        1: Add blocks to delete from the repository by walking back the chain until the last chained block is found.
        2: Delete those blocks from the BlockRepository.
        3: Set the last stored block (tip) to the last found chained block

    If the store/repository does not require reorganising the step will return Next() which will cause the BlockStoreLoop to 
    execute the next step.
     
    If the store/repository requires reorganising it will cause the BlockStoreLoop to break execution and start again.


DownloadAndStoreBlocks() : CheckNextChainedBlockExistStep
-----------------------------

     Check if the next chained block already exists in the BlockRepository

     If the block exists in the repository the step will return a Continue result which execute a 
     "Continute" on the BlockStore's while loop.

     If the block does not exists in the repository the step 
     will return a Next() result which'll cause the BlockStoreLoop to execute 
     the next step (ProcessPendingStorageStep)


DownloadAndStoreBlocks() : ProcessPendingStorageStep
-----------------------------

     Check if the next block is in pending storage i.e. first process pending storage blocks before find and downloading more blocks.

     Remove the BlockPair from PendingStorage and return for further processing. If the next chained block does not exist in pending storage
     return a Next() result which cause the BlockStoreLoop to execute the next step (DownloadBlockStep).

     If in IBD (Initial Block Download) and batch count is not yet reached, 
     return a Break() result causing the BlockStoreLoop to break out of the while loop
     and start again.

     Loop over the pending blocks and push to the repository in batches if a stop condition is met break from the inner loop 
     and return a Continue() result. This will cause the BlockStoreLoop to skip over DownloadBlockStep and start
     the loop again. 


DownloadAndStoreBlocks() : DownloadBlockStep
-----------------------------

    There are two operations:
        1: FindBlocks() to download by asking them from the BlockPuller
        2: DownloadBlocks() and persisting them as a batch to the BlockRepository
            
        After a "Stop" condition is found the FindBlocksTask will be removed from 
        the routine and only the DownloadBlocksTask will continue to execute until 
        the DownloadStack is empty.