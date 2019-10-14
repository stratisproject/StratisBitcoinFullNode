using System.Globalization;
using System.Threading;
using Stratis.Features.SQLiteWalletRepository.Tables;
using Xunit;

namespace Stratis.Features.SQLiteWalletRepository.Tests
{
    public class DBParameterTests
    {
        [Fact]
        public void DBParameterFormatsNumbersWithoutCommas()
        {
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            CultureInfo cultureUI = Thread.CurrentThread.CurrentUICulture;

            foreach (CultureInfo ci in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                Thread.CurrentThread.CurrentCulture = ci;
                Thread.CurrentThread.CurrentUICulture = ci;

                Assert.Equal("1234.56789", DBTable.DBParameter(1234.56789m));
                Assert.Equal("1234", DBTable.DBParameter((int)1234));
                Assert.Equal("1234", DBTable.DBParameter((long)1234));
            }

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = cultureUI;
        }
    }
}
