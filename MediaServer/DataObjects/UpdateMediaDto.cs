using MediaServer.Models;
using System.Text.Json.Serialization;

namespace MediaServer.DataObjects
{
    public class UpdateMediaDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public required string Title { get; set; }

        [JsonPropertyName("description")]
        public required string Description { get; set; }

        [JsonPropertyName("type")]
        public required MediaEntryType Type { get; set; }

        [JsonPropertyName("genre")]
        public required string Genre { get; set; }

        [JsonPropertyName("releaseYear")]
        public required int ReleaseYear { get; set; }

        [JsonPropertyName("ageRestriction")]
        public required int AgeRestriction { get; set; }
    }
}