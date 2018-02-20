using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.SmartContracts
{
	/// <summary>
	/// Value type representing an amount of Gas. Use this to avoid developer confusion with many other ulong values in NBitcoin.
	/// </summary>
    public struct Gas
    {
	    public Gas(ulong value)
	    {
		    this.Value = value;
	    }

	    public static Gas None = (Gas) 0;

	    public readonly ulong Value;

		/// <summary>
		/// Values of type ulong must be explicitly cast to Gas to ensure developer's intention.
		/// ulong u = 10000;
		/// Gas g = (Gas) u;
		/// </summary>
		/// <param name="value"></param>
	    public static explicit operator Gas(ulong value)
	    {
		    return new Gas(value);
	    }

		/// <summary>
		/// Ensures we can implicitly treat Gas as an ulong.
		/// Gas g = new Gas(10000);
		/// ulong u = g;
		/// </summary>
		/// <param name="gas"></param>
	    public static implicit operator ulong(Gas gas)
	    {
		    return gas.Value;
	    }
    }
}
