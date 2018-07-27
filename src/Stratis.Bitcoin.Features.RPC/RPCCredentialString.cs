using System;
using System.Linq;
using System.Net;

namespace Stratis.Bitcoin.Features.RPC
{
    public class RPCCredentialString
    {
        public static RPCCredentialString Parse(string str)
        {
            RPCCredentialString r;
            if(!TryParse(str, out r))
                throw new FormatException("Invalid RPC Credential string");
            return r;
        }

        public static bool TryParse(string str, out RPCCredentialString connectionString)
        {
            if(str == null)
                throw new ArgumentNullException(nameof(str));
            str = str.Trim();
            if(str.Equals("default", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(str))
            {
                connectionString = new RPCCredentialString();
                return true;
            }

            if(str.StartsWith("cookiefile=", StringComparison.OrdinalIgnoreCase))
            {
                string path = str.Substring("cookiefile=".Length);
                connectionString = new RPCCredentialString();
                connectionString.CookieFile = path;
                return true;
            }

            if(str.IndexOf(':') != -1)
            {
                string[] parts = str.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if(parts.Length >= 2)
                {
                    parts[1] = string.Join(":", parts.Skip(1).ToArray());
                    connectionString = new RPCCredentialString();
                    connectionString.UserPassword = new NetworkCredential(parts[0], parts[1]);
                    return true;
                }
            }

            connectionString = null;
            return false;
        }

        /// <summary>
        /// Use default connection settings of the chain
        /// </summary>
        public bool UseDefault
        {
            get
            {
                return this.CookieFile == null && this.UserPassword == null;
            }
        }


        /// <summary>
        /// Path to cookie file
        /// </summary>
        public string CookieFile
        {
            get
            {
                return this._CookieFile;
            }
            set
            {
                if(value != null)
                    Reset();
                this._CookieFile = value;
            }
        }

        private void Reset()
        {
            this._CookieFile = null;
            this._UsernamePassword = null;
        }

        private string _CookieFile;

        /// <summary>
        /// Username and password
        /// </summary>
        public NetworkCredential UserPassword
        {
            get
            {
                return this._UsernamePassword;
            }
            set
            {
                if(value != null)
                    Reset();
                this._UsernamePassword = value;
            }
        }

        private NetworkCredential _UsernamePassword;

        public override string ToString()
        {
            return this.UseDefault ? "default" :
                this.CookieFile != null ? ("cookiefile=" + this.CookieFile) :
                this.UserPassword != null ? $"{this.UserPassword.UserName}:{this.UserPassword.Password}" :
                   "default";
        }
    }
}
