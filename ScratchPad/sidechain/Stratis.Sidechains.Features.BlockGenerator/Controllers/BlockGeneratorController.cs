using System.Net;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Stratis.Bitcoin.Utilities.JsonErrors;
using Stratis.Sidechains.Features.BlockGenerator.Models;

namespace Stratis.Sidechains.Features.BlockGenerator.Controllers
{
	[Route("api/[controller]")]
	public class BlockGeneratorController : Controller
	{
		private BlockManager blockManager;

		public BlockGeneratorController(BlockManager blockManager)
		{
			this.blockManager = blockManager;
		}

		[Route("block-generate")]
		[HttpPost]
		public async Task<IActionResult> BlockGenerate([FromQuery] BlockGenerateRequest request)
		{
			bool result = await this.blockManager.BlockGenerate(request.NumberOfBlocks);

			if (result)
			{
				return this.Ok();
			}
			else
			{
				return ErrorHelpers.BuildErrorResponse(HttpStatusCode.InternalServerError, "Unable to generate block", "Unable to generate block");
			}
		}
	}
}