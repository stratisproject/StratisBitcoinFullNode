using System;
using NBitcoin.BouncyCastle.Security;

namespace NBitcoin.BouncyCastle.Crypto.Paddings
{
    /**
    * A wrapper class that allows block ciphers to be used to process data in
    * a piecemeal fashion with padding. The PaddedBufferedBlockCipher
    * outputs a block only when the buffer is full and more data is being added,
    * or on a doFinal (unless the current block in the buffer is a pad block).
    * The default padding mechanism used is the one outlined in Pkcs5/Pkcs7.
    */
    internal class PaddedBufferedBlockCipher
        : BufferedBlockCipher
    {
        private readonly IBlockCipherPadding padding;

        /**
        * Create a buffered block cipher with the desired padding.
        *
        * @param cipher the underlying block cipher this buffering object wraps.
        * @param padding the padding type.
        */
        public PaddedBufferedBlockCipher(
            IBlockCipher cipher,
            IBlockCipherPadding padding)
        {
            this.cipher = cipher;
            this.padding = padding;

            this.buf = new byte[cipher.GetBlockSize()];
            this.bufOff = 0;
        }

        /**
        * Create a buffered block cipher Pkcs7 padding
        *
        * @param cipher the underlying block cipher this buffering object wraps.
        */
        public PaddedBufferedBlockCipher(
            IBlockCipher cipher)
            : this(cipher, new Pkcs7Padding()) { }

        /**
        * initialise the cipher.
        *
        * @param forEncryption if true the cipher is initialised for
        *  encryption, if false for decryption.
        * @param param the key and other data required by the cipher.
        * @exception ArgumentException if the parameters argument is
        * inappropriate.
        */
        public override void Init(
            bool forEncryption,
            ICipherParameters parameters)
        {
            this.forEncryption = forEncryption;

            SecureRandom initRandom = null;

            Reset();
            this.padding.Init(initRandom);
            this.cipher.Init(forEncryption, parameters);
        }

        /**
        * return the minimum size of the output buffer required for an update
        * plus a doFinal with an input of len bytes.
        *
        * @param len the length of the input.
        * @return the space required to accommodate a call to update and doFinal
        * with len bytes of input.
        */
        public override int GetOutputSize(
            int length)
        {
            int total = length + this.bufOff;
            int leftOver = total % this.buf.Length;

            if(leftOver == 0)
            {
                if(this.forEncryption)
                {
                    return total + this.buf.Length;
                }

                return total;
            }

            return total - leftOver + this.buf.Length;
        }

        /**
        * return the size of the output buffer required for an update
        * an input of len bytes.
        *
        * @param len the length of the input.
        * @return the space required to accommodate a call to update
        * with len bytes of input.
        */
        public override int GetUpdateOutputSize(
            int length)
        {
            int total = length + this.bufOff;
            int leftOver = total % this.buf.Length;

            if(leftOver == 0)
            {
                return total - this.buf.Length;
            }

            return total - leftOver;
        }

        /**
        * process a single byte, producing an output block if necessary.
        *
        * @param in the input byte.
        * @param out the space for any output that might be produced.
        * @param outOff the offset from which the output will be copied.
        * @return the number of output bytes copied to out.
        * @exception DataLengthException if there isn't enough space in out.
        * @exception InvalidOperationException if the cipher isn't initialised.
        */
        public override int ProcessByte(
            byte input,
            byte[] output,
            int outOff)
        {
            int resultLen = 0;

            if(this.bufOff == this.buf.Length)
            {
                resultLen = this.cipher.ProcessBlock(this.buf, 0, output, outOff);
                this.bufOff = 0;
            }

            this.buf[this.bufOff++] = input;

            return resultLen;
        }

        /**
        * process an array of bytes, producing output if necessary.
        *
        * @param in the input byte array.
        * @param inOff the offset at which the input data starts.
        * @param len the number of bytes to be copied out of the input array.
        * @param out the space for any output that might be produced.
        * @param outOff the offset from which the output will be copied.
        * @return the number of output bytes copied to out.
        * @exception DataLengthException if there isn't enough space in out.
        * @exception InvalidOperationException if the cipher isn't initialised.
        */
        public override int ProcessBytes(
            byte[] input,
            int inOff,
            int length,
            byte[] output,
            int outOff)
        {
            if(length < 0)
            {
                throw new ArgumentException("Can't have a negative input length!");
            }

            int blockSize = GetBlockSize();
            int outLength = GetUpdateOutputSize(length);

            if(outLength > 0)
            {
                Check.OutputLength(output, outOff, outLength, "output buffer too short");
            }

            int resultLen = 0;
            int gapLen = this.buf.Length - this.bufOff;

            if(length > gapLen)
            {
                Array.Copy(input, inOff, this.buf, this.bufOff, gapLen);

                resultLen += this.cipher.ProcessBlock(this.buf, 0, output, outOff);

                this.bufOff = 0;
                length -= gapLen;
                inOff += gapLen;

                while(length > this.buf.Length)
                {
                    resultLen += this.cipher.ProcessBlock(input, inOff, output, outOff + resultLen);

                    length -= blockSize;
                    inOff += blockSize;
                }
            }

            Array.Copy(input, inOff, this.buf, this.bufOff, length);

            this.bufOff += length;

            return resultLen;
        }

        /**
        * Process the last block in the buffer. If the buffer is currently
        * full and padding needs to be added a call to doFinal will produce
        * 2 * GetBlockSize() bytes.
        *
        * @param out the array the block currently being held is copied into.
        * @param outOff the offset at which the copying starts.
        * @return the number of output bytes copied to out.
        * @exception DataLengthException if there is insufficient space in out for
        * the output or we are decrypting and the input is not block size aligned.
        * @exception InvalidOperationException if the underlying cipher is not
        * initialised.
        * @exception InvalidCipherTextException if padding is expected and not found.
        */
        public override int DoFinal(
            byte[] output,
            int outOff)
        {
            int blockSize = this.cipher.GetBlockSize();
            int resultLen = 0;

            if(this.forEncryption)
            {
                if(this.bufOff == blockSize)
                {
                    if((outOff + 2 * blockSize) > output.Length)
                    {
                        Reset();

                        throw new OutputLengthException("output buffer too short");
                    }

                    resultLen = this.cipher.ProcessBlock(this.buf, 0, output, outOff);
                    this.bufOff = 0;
                }

                this.padding.AddPadding(this.buf, this.bufOff);

                resultLen += this.cipher.ProcessBlock(this.buf, 0, output, outOff + resultLen);

                Reset();
            }
            else
            {
                if(this.bufOff == blockSize)
                {
                    resultLen = this.cipher.ProcessBlock(this.buf, 0, this.buf, 0);
                    this.bufOff = 0;
                }
                else
                {
                    Reset();

                    throw new DataLengthException("last block incomplete in decryption");
                }

                try
                {
                    resultLen -= this.padding.PadCount(this.buf);

                    Array.Copy(this.buf, 0, output, outOff, resultLen);
                }
                finally
                {
                    Reset();
                }
            }

            return resultLen;
        }
    }

}
