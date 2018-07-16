using System;
using System.Collections.Generic;
using System.Text;
using Stratis.SmartContracts;
using Xunit;

namespace Stratis.Bitcoin.Features.SmartContracts.Tests
{
    public class GasTests
    {
	    private const ulong Amount = 1000000;

		[Fact]
	    public void SmartContract_Gas_TestValue()
	    {
		    var gas = new Gas(Amount);

			Assert.Equal(gas.Value, Amount);
	    }

	    [Fact]
		public void SmartContract_Gas_TestExplicitOperator()
	    {
		    // If the explicit operator is removed or incorrect, this test will fail
			var gas = new Gas(Amount);
		    Gas g = (Gas) Amount;
			Assert.Equal(g, gas);
	    }

	    [Fact]
	    public void SmartContract_Gas_TestImplicitOperator()
	    {
		    // If the implicit operator is removed or incorrect, this test will fail

			var gas = new Gas(Amount);
		    ulong u = gas;

		    Assert.Equal(u, Amount);
	    }

	    [Fact]
	    public void SmartContract_Gas_TestNone()
	    {
		    var none = new Gas(0);
		    var explicitNone = (Gas) 0;

		    Assert.Equal(Gas.None, none);
		    Assert.Equal(Gas.None, explicitNone);
		}
	}
}
