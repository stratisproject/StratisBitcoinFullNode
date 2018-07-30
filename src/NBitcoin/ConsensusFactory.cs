using System;
using System.Collections.Concurrent;
using System.Reflection;
using NBitcoin.DataEncoders;

namespace NBitcoin
{
    /// <summary>
    /// A factory to create protocol types.
    /// </summary>
    public class ConsensusFactory
    {
        /// <summary>
        /// A dictionary for types assignable from <see cref="BlockHeader"/>.
        /// </summary>
        private readonly ConcurrentDictionary<Type, bool> isAssignableFromBlockHeader = new ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// The <see cref="BlockHeader"/> type.
        /// </summary>
        private readonly TypeInfo blockHeaderType = typeof(BlockHeader).GetTypeInfo();

        /// <summary>
        /// A dictionary for types assignable from <see cref="Block"/>.
        /// </summary>
        private readonly ConcurrentDictionary<Type, bool> isAssignableFromBlock = new ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// The <see cref="Block"/> type.
        /// </summary>
        private readonly TypeInfo blockType = typeof(Block).GetTypeInfo();

        /// <summary>
        /// A dictionary for types assignable from <see cref="Transaction"/>.
        /// </summary>
        private readonly ConcurrentDictionary<Type, bool> isAssignableFromTransaction = new ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// The <see cref="Transaction"/> type.
        /// </summary>
        private readonly TypeInfo transactionType = typeof(Transaction).GetTypeInfo();

        public ConsensusFactory()
        {
        }

        /// <summary>
        /// Check if the generic type is assignable from <see cref="BlockHeader"/>.
        /// </summary>
        /// <typeparam name="T">The type to check if it is IsAssignable from <see cref="BlockHeader"/>.</typeparam>
        /// <returns><c>true</c> if it is assignable.</returns>
        protected bool IsBlockHeader<T>()
        {
            return IsAssignable<T>(this.blockHeaderType, this.isAssignableFromBlockHeader);
        }

        /// <summary>
        /// Check if the generic type is assignable from <see cref="Block"/>.
        /// </summary>
        /// <typeparam name="T">The type to check if it is IsAssignable from <see cref="Block"/>.</typeparam>
        /// <returns><c>true</c> if it is assignable.</returns>
        protected bool IsBlock<T>()
        {
            return IsAssignable<T>(this.blockType, this.isAssignableFromBlock);
        }

        /// <summary>
        /// Check if the generic type is assignable from <see cref="Transaction"/>.
        /// </summary>
        /// <typeparam name="T">The type to check if it is IsAssignable from <see cref="Transaction"/>.</typeparam>
        /// <returns><c>true</c> if it is assignable.</returns>
        protected bool IsTransaction<T>()
        {
            return IsAssignable<T>(this.transactionType, this.isAssignableFromTransaction);
        }

        /// <summary>
        /// Check weather a type is assignable within the collection of types in the give dictionary.
        /// </summary>
        /// <typeparam name="T">The generic type to check.</typeparam>
        /// <param name="type">The type to compare against.</param>
        /// <param name="cache">A collection of already checked types.</param>
        /// <returns><c>true</c> if it is assignable.</returns>
        private bool IsAssignable<T>(TypeInfo type, ConcurrentDictionary<Type, bool> cache)
        {
            if (!cache.TryGetValue(typeof(T), out bool isAssignable))
            {
                isAssignable = type.IsAssignableFrom(typeof(T).GetTypeInfo());
                cache.TryAdd(typeof(T), isAssignable);
            }

            return isAssignable;
        }

        /// <summary>
        /// A method that will try to resolve a type and determine weather its part of the factory types.
        /// </summary>
        /// <typeparam name="T">The generic type to resolve.</typeparam>
        /// <param name="result">If the type is known it will be initialized.</param>
        /// <returns><c>true</c> if it is known.</returns>
        public virtual T TryCreateNew<T>() where T : IBitcoinSerializable
        {
            object result = null;

            if (IsBlock<T>())
                result = (T)(object)CreateBlock();

            if (IsBlockHeader<T>())
                result = (T)(object)CreateBlockHeader();

            if (IsTransaction<T>())
                result = (T)(object)CreateTransaction();

            return (T)result;
        }

        /// <summary>
        /// A set of flags representing the capabilities of the protocol.
        /// </summary>
        /// <param name="protocolVersion">The version to build the flags from.</param>
        /// <returns>The <see cref="ProtocolCapabilities"/>.</returns>
        public virtual ProtocolCapabilities GetProtocolCapabilities(uint protocolVersion)
        {
            return new ProtocolCapabilities()
            {
                PeerTooOld = protocolVersion < 209U,
                SupportTimeAddress = protocolVersion >= 31402U,
                SupportGetBlock = protocolVersion < 32000U || protocolVersion > 32400U,
                SupportPingPong = protocolVersion > 60000U,
                SupportMempoolQuery = protocolVersion >= 60002U,
                SupportReject = protocolVersion >= 70002U,
                SupportNodeBloom = protocolVersion >= 70011U,
                SupportSendHeaders = protocolVersion >= 70012U,
                SupportWitness = protocolVersion >= 70012U,
                SupportCompactBlocks = protocolVersion >= 70014U,
                SupportCheckSum = protocolVersion >= 60002,
                SupportUserAgent = protocolVersion >= 60002
            };
        }

        /// <summary>
        /// Create a <see cref="Block"/> instance.
        /// </summary>
        public virtual Block CreateBlock()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new Block(CreateBlockHeader());
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Create a <see cref="BlockHeader"/> instance.
        /// </summary>
        public virtual BlockHeader CreateBlockHeader()
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return new BlockHeader();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Create a <see cref="Transaction"/> instance.
        /// </summary>
        public virtual Transaction CreateTransaction()
        {
            return new Transaction();
        }

        /// <summary>
        /// Create a <see cref="Transaction"/> instance from a hex string representation.
        /// </summary>
        public virtual Transaction CreateTransaction(string hex)
        {
            var transaction = new Transaction();
            transaction.FromBytes(Encoders.Hex.DecodeData(hex));
            return transaction;
        }

        /// <summary>
        /// Create a <see cref="Transaction"/> instance from a byte array representation.
        /// </summary>
        public virtual Transaction CreateTransaction(byte[] bytes)
        {
            var transaction = new Transaction();
            transaction.FromBytes(bytes);
            return transaction;
        }
    }

    /// <summary>
    /// A class with a set of flags representing the capabilities of the protocol.
    /// </summary>
    public class ProtocolCapabilities
    {
        /// <summary>
        /// Disconnect from peers older than this protocol version.
        /// </summary>
        public bool PeerTooOld
        {
            get; set;
        }

        /// <summary>
        /// nTime field added to CAddress, starting with this version.
        /// if possible, avoid requesting addresses nodes older than this.
        /// </summary>
        public bool SupportTimeAddress
        {
            get; set;
        }

        /// <summary>
        /// Support Get Block.
        /// </summary>
        public bool SupportGetBlock
        {
            get; set;
        }

        /// <summary>
        /// BIP 0031, pong message, is enabled for all versions AFTER this one.
        /// </summary>
        public bool SupportPingPong
        {
            get; set;
        }

        /// <summary>
        /// "mempool" command, enhanced "getdata" behavior starts with this version.
        /// </summary>
        public bool SupportMempoolQuery
        {
            get; set;
        }

        /// <summary>
        /// "reject" command.
        /// </summary>
        public bool SupportReject
        {
            get; set;
        }

        /// <summary>
        /// "filter*" commands are disabled without NODE_BLOOM after and including this version.
        /// </summary>
        public bool SupportNodeBloom
        {
            get; set;
        }

        /// <summary>
        /// "sendheaders" command and announcing blocks with headers starts with this version.
        /// </summary>
        public bool SupportSendHeaders
        {
            get; set;
        }

        /// <summary>
        /// Version after which witness support potentially exists.
        /// </summary>
        public bool SupportWitness
        {
            get; set;
        }

        /// <summary>
        /// short-id-based block download starts with this version.
        /// </summary>
        public bool SupportCompactBlocks
        {
            get; set;
        }

        /// <summary>
        /// Support checksum at p2p message level.
        /// </summary>
        public bool SupportCheckSum
        {
            get;
            set;
        }

        /// <summary>
        /// Support a user agent.
        /// </summary>
        public bool SupportUserAgent
        {
            get;
            set;
        }

        /// <summary>
        /// Support all flags.
        /// </summary>
        public static ProtocolCapabilities CreateSupportAll()
        {
            return new ProtocolCapabilities()
            {
                PeerTooOld = false,
                SupportCheckSum = true,
                SupportCompactBlocks = true,
                SupportGetBlock = true,
                SupportMempoolQuery = true,
                SupportNodeBloom = true,
                SupportPingPong = true,
                SupportReject = true,
                SupportSendHeaders = true,
                SupportTimeAddress = true,
                SupportUserAgent = true,
                SupportWitness = true
            };
        }

        /// <summary>
        /// Is the set of flags a sub set of a given protocol flags.
        /// </summary>
        public bool IsSupersetOf(ProtocolCapabilities capabilities)
        {
            return (!capabilities.SupportCheckSum || this.SupportCheckSum) &&
                (!capabilities.SupportCompactBlocks || this.SupportCompactBlocks) &&
                (!capabilities.SupportGetBlock || this.SupportGetBlock) &&
                (!capabilities.SupportMempoolQuery || this.SupportMempoolQuery) &&
                (!capabilities.SupportNodeBloom || this.SupportNodeBloom) &&
                (!capabilities.SupportPingPong || this.SupportPingPong) &&
                (!capabilities.SupportReject || this.SupportReject) &&
                (!capabilities.SupportSendHeaders || this.SupportSendHeaders) &&
                (!capabilities.SupportTimeAddress || this.SupportTimeAddress) &&
                (!capabilities.SupportWitness || this.SupportWitness) &&
                (!capabilities.SupportUserAgent || this.SupportUserAgent) &&
                (!capabilities.SupportCheckSum || this.SupportCheckSum);
        }
    }
}