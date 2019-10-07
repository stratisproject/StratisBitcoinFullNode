using System;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stratis.Bitcoin.Features.SmartContracts.Models;
using Stratis.Bitcoin.Features.SmartContracts.Wallet;
using Stratis.SmartContracts.CLR;
using Stratis.SmartContracts.Core.State;

namespace Stratis.Bitcoin.Features.SmartContracts.ReflectionExecutor.Controllers
{
    public class DynamicContractController : Controller
    {
        private readonly SmartContractWalletController smartContractWalletController;
        private readonly SmartContractsController localCallController;
        private readonly IStateRepositoryRoot stateRoot;
        private readonly Network network;

        public DynamicContractController(SmartContractWalletController smartContractWalletController, SmartContractsController localCallController, IStateRepositoryRoot stateRoot, Network network)
        {
            this.smartContractWalletController = smartContractWalletController;
            this.localCallController = localCallController;
            this.stateRoot = stateRoot;
            this.network = network;
        }

        [Route("api/contract/{address}/method/{method}")]
        [HttpPost]
        public IActionResult CallMethod([FromRoute] string address, [FromRoute] string method)
        {
            string requestBody;
            using (StreamReader reader = new StreamReader(this.Request.Body, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            // TODO map request body to JSON object, extract transaction-related params, build new request model, then call the regular SC controller.
            JObject requestData;
            
            try
            {
                requestData = JObject.Parse(requestBody);

            }
            catch (JsonReaderException e)
            {
                return this.BadRequest(e.Message);
            }

            var contractCode = this.stateRoot.GetCode(address.ToUint160(this.network));

            // Map the jobject to the parameter + types expected by the call.

            BuildCallContractTransactionRequest request = this.MapCallRequest(address, method, requestData, this.Request.Headers);

            // Proxy to the actual SC controller.
            return this.smartContractWalletController.Call(request);
        }

        [Route("api/contract/{address}/property/{property}")]
        [HttpGet]
        public IActionResult LocalCallProperty([FromRoute] string address, [FromRoute] string property)
        {
            string requestBody;
            using (StreamReader reader = new StreamReader(this.Request.Body, Encoding.UTF8))
            {
                requestBody = reader.ReadToEnd();
            }

            // TODO map request body to JSON object, extract transaction-related params, build new request model, then call the regular SC controller.
            JObject requestData;

            try
            {
                requestData = JObject.Parse(requestBody);
            }
            catch (JsonReaderException e)
            {
                return this.BadRequest(e.Message);
            }

            LocalCallContractRequest request = this.MapLocalCallRequest(address, property, requestData, this.Request.Headers);

            // Proxy to the actual SC controller.
            return this.localCallController.LocalCallSmartContractTransaction(request);
        }

        private BuildCallContractTransactionRequest MapCallRequest(string address, string method, JObject requestData, IHeaderDictionary headers)
        {
            var call = new BuildCallContractTransactionRequest
            {
                GasPrice = ulong.Parse(headers["GasPrice"]),
                GasLimit = ulong.Parse(headers["GasLimit"]),
                Amount = headers["Amount"],
                FeeAmount = headers["FeeAmount"],
                WalletName = headers["WalletName"],
                Password = headers["WalletPassword"],
                Sender = headers["Sender"],
                AccountName = "account 0",
                ContractAddress = address,
                MethodName = method,
                // TODO map parameters
                
            };

            return call;
        }

        private LocalCallContractRequest MapLocalCallRequest(string address, string property, JObject requestData, IHeaderDictionary headers)
        {
            return new LocalCallContractRequest
            {
                GasPrice = ulong.Parse(headers["GasPrice"]),
                GasLimit = ulong.Parse(headers["GasLimit"]),
                Amount = headers["Amount"],
                Sender = headers["Sender"],
                ContractAddress = address,
                MethodName = "get_" + property, // This is an assumption but should be correct 100% of the time in this use case.
                // TODO map parameters

            };
        }
    }
}