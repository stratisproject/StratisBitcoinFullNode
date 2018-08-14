﻿using System;
using System.Collections.Generic;
using System.Text;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Interfaces;

namespace Stratis.Bitcoin.Features.SmartContracts
{
    public class SmartContractVersionProvider : IVersionProvider
    {
        public string GetVersion()
        {
            return "0.11.0";
        }
    }
}
