using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace Stratis.Sidechains.Features.BlockGenerator.Models
{
	/// <summary>
	/// Base class for request objects received to the controllers
	/// </summary>
	public class RequestModel
	{
		public override string ToString()
		{
			return JsonConvert.SerializeObject(this, Formatting.Indented);
		}
	}

	/// <summary>
	/// Object used to perform a block generation.
	/// </summary>
	public class BlockGenerateRequest : RequestModel
	{
		[Required(ErrorMessage = "Number of blocks to generate is required")]
		public int NumberOfBlocks { get; set; }
	}
}
