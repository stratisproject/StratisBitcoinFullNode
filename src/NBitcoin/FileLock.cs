using System;
using System.IO;
using System.Threading;

namespace NBitcoin
{
    public enum FileLockType
    {
        Read,
        ReadWrite
    }
    public class FileLock : IDisposable
    {
        private FileStream _Fs = null;
        public FileLock(string filePath, FileLockType lockType)
        {
            if(filePath == null)
                throw new ArgumentNullException("filePath");
            if(!File.Exists(filePath))
            {
                try
                {
                    File.Create(filePath).Dispose();
                }
                catch
                {
                }
            }

            var source = new CancellationTokenSource();
            source.CancelAfter(20000);
            while(true)
            {
                try
                {
                    if(lockType == FileLockType.Read) this._Fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if(lockType == FileLockType.ReadWrite) this._Fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    break;
                }
                catch(IOException)
                {
                    Thread.Sleep(50);
                    source.Token.ThrowIfCancellationRequested();
                }
            }
        }


        public void Dispose()
        {
            this._Fs.Dispose();
        }


        //public void SetString(string str)
        //{
        //    _Fs.Position = 0;
        //    StreamWriter writer = new StreamWriter(_Fs);
        //    writer.Write(str);
        //    writer.Flush();
        //}

        //public string GetString()
        //{
        //    _Fs.Position = 0;
        //    StreamReader reader = new StreamReader(_Fs);
        //    return reader.ReadToEnd();
        //}
    }
}
