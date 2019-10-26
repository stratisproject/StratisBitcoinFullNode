using System;
using System.Collections.Generic;
using NBitcoin;

namespace Stratis.Bitcoin.BlockPulling
{
    /// <summary>Represents a single download assignment to a peer.</summary>
    public class AssignedDownload
    {
        /// <summary>Unique identifier of a job to which this assignment belongs.</summary>
        public int JobId { get; set; }

        /// <summary>Id of a peer that was assigned to deliver a block.</summary>
        public int PeerId { get; set; }

        /// <summary>Time when download was assigned to a peer.</summary>
        public DateTime AssignedTime { get; set; }

        /// <summary>Header of a block associated with this assignment.</summary>
        public ChainedHeader Header { get; set; }

        public LinkedListNode<AssignedDownload> LinkedListNode { get; set; }

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0}:{1},{2}:{3},{4}:'{5}'", nameof(this.JobId), this.JobId, nameof(this.PeerId), this.PeerId, nameof(this.Header), this.Header);
        }
    }
}
