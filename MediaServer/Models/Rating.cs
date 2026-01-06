using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaServer.Models
{
    public class Rating
    {
        public int Id { get; set; }
        public required int MediaEntryId { get; set; }
        public required int UserId { get; set; }
        public required int Score { get; set; }

        public string? Comment { get; set; } // Optional
        public bool IsConfirmed { get; set; } = false; // Moderation: Erst false
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int LikeCount { get; set; } = 0;
    }
}
