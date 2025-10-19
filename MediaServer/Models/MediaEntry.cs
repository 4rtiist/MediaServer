using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaServer.Models
{
    public enum MediaEntryType { Movie, Series, Game }
    public class MediaEntry
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required MediaEntryType Type { get; set; }
        public required string Description { get; set; }
        public required string Genre { get; set; }
        public required int ReleaseYear { get; set; }
        public required int AgeRestriction { get; set; }
        public double AverageRating { get; set; } = 0;
    }
}
