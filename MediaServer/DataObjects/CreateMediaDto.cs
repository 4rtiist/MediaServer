using MediaServer.Models;
using System.Text.Json.Serialization;

namespace MediaServer.DataObjects
{
    public class CreateMediaDto
    {
        [JsonPropertyName("title")]
        public required string Title { get; set; }

        [JsonPropertyName("description")]
        public required string Description { get; set; }

        [JsonPropertyName("type")]
        public required MediaEntryType Type { get; set; } // 0=Movie, 1=Series, 2=Game

        [JsonPropertyName("genre")]
        public required string Genre { get; set; }

        [JsonPropertyName("releaseYear")]
        public required int ReleaseYear { get; set; }

        [JsonPropertyName("ageRestriction")]
        public required int AgeRestriction { get; set; }
    }
}