using System;
using System.Collections.Generic;

namespace Stratis.Bitcoin.BlockPulling2
{
    /// <summary>Represents a single download assignment to a peer.</summary>
    public struct AssignedDownload
    {
        /// <summary>Unique identifier of a job to which this assignment belongs.</summary>
        public int JobId;

        /// <summary>Id of a peer that was assigned to deliver a block.</summary>
        public int PeerId;

        /// <summary>Time when download was assigned to a peer.</summary>
        public DateTime AssignedTime;

        /// <summary>Height of a block associated with this assignment.</summary>
        public int BlockHeight;

        public LinkedListNode<AssignedDownload> LinkedListNode;

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format("{0}:{1},{2}:{3},{4}:{5}", nameof(this.JobId), this.JobId, nameof(this.PeerId), this.PeerId, nameof(this.BlockHeight), this.BlockHeight);
        }
    }
}
