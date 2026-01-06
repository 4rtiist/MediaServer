using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaServer.Models
{
    public class UserStats
    {
        public required string Username { get; set; }
        public int TotalRatings { get; set; }
    }
}
