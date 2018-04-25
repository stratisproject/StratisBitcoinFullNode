using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Stratis.Bitcoin.Features.BlockStore.Models
{
    public abstract class QueryModelBase
    {
        public bool OutputJson { get; set; }
    }
    public class ObjectByHashQueryModel : QueryModelBase
    {
        [Required(AllowEmptyStrings = false)]
        public string Hash { get; set; }
    }
}
