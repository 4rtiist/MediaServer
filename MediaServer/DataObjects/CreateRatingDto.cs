using System.Text.Json.Serialization;

namespace MediaServer.DataObjects
{
    public class CreateRatingDto
    {
        [JsonPropertyName("mediaId")]
        public int MediaId { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; } // Muss 1-5 sein

        [JsonPropertyName("comment")]
        public string? Comment { get; set; } // Optional
    }
}
