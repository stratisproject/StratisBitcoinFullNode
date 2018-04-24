using System;
using System.Collections.Generic;
using System.Text;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public class Block
    {
        public string Hash { get; set; }
        public string Confirmations { get; set; }
    }
}
