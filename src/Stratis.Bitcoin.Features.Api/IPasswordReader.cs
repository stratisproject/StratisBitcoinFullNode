using System.Security;

namespace Stratis.Bitcoin.Features.Api
{
    public interface IPasswordReader
    {
        SecureString ReadSecurePassword(string passwordContext = "Please enter your password");
    }
}