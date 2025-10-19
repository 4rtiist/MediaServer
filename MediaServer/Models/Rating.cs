using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaServer.Models
{
    public class Rating
    {
        public required int MediaEntryId { get; set; }
        public required int UserId { get; set; }
        public required int Score { get; set; }
    }
}
